using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Zones;
using PugTilemap;
using Pug.UnityExtensions;
using PugWorldGen;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// The rooms a dungeon is made of, and the rules each one is placed under.
    /// </summary>
    public static partial class DimensionDungeonAssembler
    {
        /// <summary>
        /// Maps the Studio's authored room rules onto the game's placement structs, honoring the
        /// Studio's inclusive-range promise against the game's max-exclusive rolls.
        /// </summary>
        /// <remarks>
        /// Two authored conveniences are resolved here rather than at authoring time, because
        /// only the runtime knows the game's quirks: a fixed-spot rule is refused with a warning
        /// (the game's switch has no case for it — it would silently place nothing), and a rule
        /// set with no centre room gets a synthetic one, since FromOtherRooms errors out on an
        /// empty room buffer.
        /// </remarks>
        private static List<DungeonRoomPlacement> MapAuthoredRoomRules(
            DimensionDungeonDefinition definition,
            out int maxRoomRadius)
        {
            List<DungeonRoomPlacement> rules = new List<DungeonRoomPlacement>();
            maxRoomRadius = 0;
            bool hasCentre = false;
            bool growsFromOthers = false;

            for (int i = 0; i < definition.RoomRules.Count; i++)
            {
                DimensionDungeonRoomRule rule = definition.RoomRules[i];
                if (rule.Placement == DimensionRoomPlacement.NotPlaced)
                {
                    continue;
                }

                if (rule.Placement == DimensionRoomPlacement.AtAFixedSpot)
                {
                    DimensionFrameworkLog.Warning(
                        "Dungeon '" + definition.DungeonId + "' has a room " +
                        "rule placed 'at a fixed spot', which Core Keeper never implemented — " +
                        "the game's room placer has no code for it and would silently place " +
                        "nothing. The rule was skipped.");
                    continue;
                }

                RoomPlacementAlgorithm algorithm = (RoomPlacementAlgorithm)(int)rule.Placement;
                hasCentre |= algorithm == RoomPlacementAlgorithm.Center;
                growsFromOthers |= algorithm == RoomPlacementAlgorithm.FromOtherRooms;

                int minRadius = math.max(1, rule.MinRadius);
                int maxRadius = math.max(minRadius, rule.MaxRadius);
                maxRoomRadius = math.max(maxRoomRadius, maxRadius);

                // Vanilla's own restriction: overlap is only honored for the two algorithms
                // whose placement can actually retry around it.
                bool canOverlap = rule.MayOverlapOtherRooms &&
                                  (algorithm == RoomPlacementAlgorithm.Random ||
                                   algorithm == RoomPlacementAlgorithm.FromOtherRooms);

                // (0,0) angles mean the author never touched the field; a literal zero would
                // pin every room due east, so unset means anywhere.
                float angleMin = rule.AngleMinDegrees;
                float angleMax = rule.AngleMaxDegrees;
                if (angleMax <= angleMin)
                {
                    angleMin = 0f;
                    angleMax = 360f;
                }

                rules.Add(new DungeonRoomPlacement
                {
                    algorithm = algorithm,
                    roomType = rule.Kind == DimensionRoomKind.None
                        ? RoomFlags.Main
                        : (RoomFlags)(int)rule.Kind,
                    amount = new RangeInt
                    {
                        min = math.max(0, rule.MinCount),
                        max = math.max(rule.MinCount, rule.MaxCount) + 1
                    },
                    size = new RangeInt
                    {
                        min = minRadius,
                        // FromOtherRooms is the one algorithm the game rolls max-INCLUSIVE, so
                        // adding one there would grow rooms past what was authored.
                        max = algorithm == RoomPlacementAlgorithm.FromOtherRooms
                            ? maxRadius
                            : maxRadius + 1
                    },
                    fromExistingSpacing = new RangeInt
                    {
                        min = math.max(0, rule.MinSpacing),
                        max = math.max(rule.MinSpacing, rule.MaxSpacing)
                    },
                    fromExistingPerpendicular = rule.StraightPaths,
                    canIntersectRooms = canOverlap
                        ? (RoomFlags)(int)rule.MayOverlapKinds
                        : RoomFlags.None,
                    angleMin = math.radians(angleMin),
                    angleMax = math.radians(angleMax),
                    alignAngleWithCoreRadial = rule.AlignedWithTheCore
                });
            }

            if (rules.Count == 0)
            {
                // Every authored rule was refused or disabled; fall back to synthesis so the
                // dungeon still generates instead of matching the pipeline with nothing to do.
                return BuildRoomRules(definition, out maxRoomRadius);
            }

            if (!hasCentre && growsFromOthers)
            {
                rules.Insert(0, NewRule(RoomPlacementAlgorithm.Center, 1, 1, 4, 6));
                maxRoomRadius = math.max(maxRoomRadius, 6);
            }

            WarnWhenAuthoredRoomsCannotHoldTheirScenes(definition, rules);

            return rules;
        }

        /// <summary>
        /// Says so when an authored room rule can roll smaller than a scene it may have to hold.
        /// </summary>
        /// <remarks>
        /// A warning, not a clamp, on vanilla's own precedent: the game never checks fit (its
        /// City quarters overflow rooms half their size and live with the consequences), and an
        /// author who chose a number gets that number. What the overflow actually costs is
        /// spelled out so the author can decide: the overhang escapes the room's spawn-block
        /// circle, and spacing and corridor routing measure the too-small radius, so neighbours
        /// and corridors may legally stamp over the scene's edge.
        /// </remarks>
        private static void WarnWhenAuthoredRoomsCannotHoldTheirScenes(
            DimensionDungeonDefinition definition,
            List<DungeonRoomPlacement> rules)
        {
            for (int g = 0; g < definition.RoomGroups.Count; g++)
            {
                DimensionDungeonRoomGroup group = definition.RoomGroups[g];
                RoomFlags groupFlags = ToRoomFlags(group.Role);

                int groupFit = 0;
                string biggestScene = null;
                for (int s = 0; s < group.SceneNames.Count; s++)
                {
                    if (DimensionCustomSceneRegistry.TryGetFitRadius(group.SceneNames[s], out int fit) &&
                        fit > groupFit)
                    {
                        groupFit = fit;
                        biggestScene = group.SceneNames[s];
                    }
                }

                if (groupFit == 0)
                {
                    continue;
                }

                for (int r = 0; r < rules.Count; r++)
                {
                    // Rooms are claimed by ANY shared flag bit, so any intersecting rule can
                    // produce a room this group's scenes land in.
                    if ((rules[r].roomType & groupFlags) == RoomFlags.None)
                    {
                        continue;
                    }

                    // size.max is stored max-exclusive for most algorithms; min is the honest
                    // floor either way, and the floor is what a guarantee needs.
                    if (rules[r].size.min < groupFit)
                    {
                        DimensionFrameworkLog.Warning(
                            "Dungeon '" + definition.DungeonId + "': a " +
                            group.Role + " room can roll radius " + rules[r].size.min +
                            ", but '" + biggestScene + "' needs " + groupFit +
                            " to fit. The overhang will poke past the room's protected circle, " +
                            "and neighbouring rooms or corridors may stamp over its edge. Raise " +
                            "the room rule's size to " + groupFit + " to guarantee the fit.");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The corridor band the game will actually carve, as the stored width.
        /// </summary>
        /// <remarks>
        /// The carve test covers integer offsets <c>|z| ≤ width/2 + 0.1</c> around the
        /// room-to-room centre line, so every corridor is an odd tile count and an even request
        /// silently gets the next odd band. Storing the effective count keeps authored numbers
        /// truthful; the log line teaches the rule the one time it changes something.
        /// </remarks>
        private static float NormalizedCorridorWidth(
            DimensionDungeonDefinition definition,
            float authoredWidth)
        {
            int effective = DimensionSceneGeometry.EffectiveCorridorTiles(authoredWidth);
            if (math.abs(authoredWidth - effective) > 0.05f)
            {
                DimensionFrameworkLog.Info(
                    "Dungeon '" + definition.DungeonId + "': corridors are " +
                    "always an odd number of tiles wide, centred on the line between rooms — " +
                    "the authored width " + authoredWidth + " carves " + effective + " tiles.");
            }

            return effective;
        }

        /// <summary>
        /// Synthesizes the room placement rules the game's pipeline needs from the asset's
        /// room groups, sized so each group's scenes fit their rooms corner to corner.
        /// </summary>
        /// <remarks>
        /// The shape template will replace these defaults when it is wired; until then the
        /// rules mirror the most common vanilla dungeon: one room at the centre, the rest
        /// grown outward from rooms already placed, entrances pinned to the outline. The
        /// FromOtherRooms algorithm errors on an empty room buffer, so a Center rule must
        /// always come first — which is also why an entrance-only dungeon gets a synthetic
        /// centre room to grow from.
        /// </remarks>
        private static List<DungeonRoomPlacement> BuildRoomRules(
            DimensionDungeonDefinition definition,
            out int maxRoomRadius)
        {
            List<DungeonRoomPlacement> rules = new List<DungeonRoomPlacement>();
            maxRoomRadius = 0;
            bool centrePlaced = false;

            for (int i = 0; i < definition.RoomGroups.Count; i++)
            {
                DimensionDungeonRoomGroup group = definition.RoomGroups[i];
                int maxRadius = 4;
                for (int sceneIndex = 0; sceneIndex < group.SceneNames.Count; sceneIndex++)
                {
                    if (!DimensionCustomSceneRegistry.TryGetFitRadius(
                            group.SceneNames[sceneIndex],
                            out int radius))
                    {
                        continue;
                    }

                    maxRadius = math.max(maxRadius, radius);
                }

                // Every room in the group rolls at the largest scene's fit radius, because which
                // scene a room claims is a separate roll: a small room can claim the big scene,
                // and a room that cannot hold its scene loses the overhang to neighbours and
                // corridors. Guaranteed fit is what a synthesized default owes its author.
                int minRadius = maxRadius;

                maxRoomRadius = math.max(maxRoomRadius, maxRadius);

                bool entrance = group.Role == DimensionDungeonRoomRole.Entrance;
                RoomPlacementAlgorithm algorithm;
                int minCount = math.max(1, group.MinRooms);
                int maxCount = math.max(minCount, group.MaxRooms);
                if (entrance)
                {
                    algorithm = RoomPlacementAlgorithm.Entrance;
                }
                else if (!centrePlaced)
                {
                    // Center places exactly one and ignores the amount; the remainder of this
                    // group grows from it below.
                    algorithm = RoomPlacementAlgorithm.Center;
                    centrePlaced = true;
                }
                else
                {
                    algorithm = RoomPlacementAlgorithm.FromOtherRooms;
                }

                rules.Add(NewRule(algorithm, minCount, maxCount, minRadius, maxRadius));

                if (algorithm == RoomPlacementAlgorithm.Center && maxCount > 1)
                {
                    rules.Add(NewRule(
                        RoomPlacementAlgorithm.FromOtherRooms,
                        math.max(0, minCount - 1),
                        maxCount - 1,
                        minRadius,
                        maxRadius));
                }
            }

            if (!centrePlaced)
            {
                // Every dungeon needs one room at its heart for the others to grow from and
                // the corridors to reach. Small and plain; the fill shell covers it.
                rules.Insert(0, NewRule(RoomPlacementAlgorithm.Center, 1, 1, 4, 6));
                maxRoomRadius = math.max(maxRoomRadius, 6);
            }

            return rules;
        }

        private static DungeonRoomPlacement NewRule(
            RoomPlacementAlgorithm algorithm,
            int minCount,
            int maxCount,
            int minRadius,
            int maxRadius)
        {
            return new DungeonRoomPlacement
            {
                algorithm = algorithm,
                roomType = RoomFlags.Main,
                // The game rolls NextInt(min, max) — max-exclusive — so authored inclusive
                // counts and sizes get one added here, exactly like vanilla's converter.
                amount = new RangeInt { min = minCount, max = maxCount + 1 },
                size = new RangeInt { min = minRadius, max = maxRadius + 1 },
                canIntersectRooms = RoomFlags.None,
                fromExistingSpacing = new RangeInt { min = 3, max = 7 },
                fromExistingPerpendicular = false,
                angleMin = 0f,
                angleMax = math.PI * 2f,
                alignAngleWithCoreRadial = false
            };
        }

        /// <summary>
        /// The biome a table binding matches against — a VANILLA biome by its game name, or
        /// one of the mod's own by its authored id.
        /// </summary>
        /// <remarks>
        /// The vanilla parse comes FIRST, and the order is the bug this fixes: an author
        /// writing "Desert" means the game's Desert, but the identity mint happily coins a
        /// custom int for any string — so the binding matched a biome no Overworld cell ever
        /// reports and the dungeon silently never rolled there. A custom biome id (anything
        /// that is not a vanilla name) still mints as before and matches inside the author's
        /// own dimension, where the framework's biome sampling reports it.
        /// </remarks>
        internal static Biome ResolveBindingBiome(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return Biome.None;
            }

            Biome vanilla;
            if (System.Enum.TryParse(biomeId, false, out vanilla))
            {
                return vanilla;
            }

            return DimensionBiomeIdentity.GetOrAssign(biomeId);
        }

        private static WorldGenerationTypeDependentValue<Biome> ForBothWorldTypes(Biome biome)
        {
            return new WorldGenerationTypeDependentValue<Biome>
            {
                classic = biome,
                fullRelease = biome
            };
        }

        private static RoomFlags ToRoomFlags(DimensionDungeonRoomRole role)
        {
            switch (role)
            {
                case DimensionDungeonRoomRole.Entrance:
                    return RoomFlags.CustomScene | RoomFlags.Entrance;
                case DimensionDungeonRoomRole.End:
                    return RoomFlags.CustomScene | RoomFlags.End;
                case DimensionDungeonRoomRole.Connecting:
                    return RoomFlags.CustomScene | RoomFlags.Connecting;
                default:
                    return RoomFlags.CustomScene | RoomFlags.Main;
            }
        }
    }
}
