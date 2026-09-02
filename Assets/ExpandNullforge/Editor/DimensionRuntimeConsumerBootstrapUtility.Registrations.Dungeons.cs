using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
using ExpandNullforge.Scenes;
using Pug.Sprite;
using PugMod;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What the emitted script registers about dungeons and the rooms in them.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Emits each dungeon: its size, where it may grow, and which scenes fill which rooms.
        /// </summary>
        /// <remarks>
        /// A dungeon with no entrance is reported but still emitted. It is a real mistake — the player
        /// finds a sealed pocket of rooms — but it is also a legitimate mid-build state, and refusing
        /// to generate it would stop an author testing the rooms they have so far.
        /// </remarks>
        private static void AppendDungeonRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionDungeonAsset[] dungeons = template == null ? null : template.GlobalDungeons;
            if (dungeons == null || dungeons.Length == 0)
            {
                return;
            }

            for (int i = 0; i < dungeons.Length; i++)
            {
                DimensionDungeonAsset dungeon = dungeons[i];
                if (dungeon == null || !dungeon.Enabled)
                {
                    continue;
                }

                if (!dungeon.HasEntrance)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' has no entrance rooms. " +
                        "It will still generate, but players will find a sealed pocket of rooms with " +
                        "no way in.");
                }

                builder.AppendLine("    {");
                builder.AppendLine(
                    "      var dungeonRooms = new System.Collections.Generic.List<DimensionDungeonRoomGroup>();");

                DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
                for (int g = 0; g < groups.Length; g++)
                {
                    DimensionDungeonRoomGroupTemplate group = groups[g];
                    if (group == null || group.Rooms.Length == 0)
                    {
                        continue;
                    }

                    builder.Append("      dungeonRooms.Add(new DimensionDungeonRoomGroup(")
                        .Append("DimensionDungeonRoomRole.").Append(group.Role.ToString()).Append(", ")
                        .Append(group.MinRooms.ToString(CultureInfo.InvariantCulture)).Append(", ")
                        .Append(group.MaxRooms.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(", new string[] {");

                    for (int r = 0; r < group.Rooms.Length; r++)
                    {
                        SceneTemplateAsset room = group.Rooms[r];
                        if (room == null || string.IsNullOrEmpty(room.SceneId))
                        {
                            continue;
                        }

                        builder.Append("        ")
                            .Append(ToCSharpString(
                                DimensionObjectNamespace.Qualify(modName, room.SceneId)))
                            .AppendLine(",");
                    }

                    builder.AppendLine("      }));");
                }

                builder.AppendLine("      DimensionDungeonRegistry.Register(new DimensionDungeonDefinition(");
                builder.Append("          ")
                    .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId)))
                    .AppendLine(",");
                builder.Append("          ").Append(ToCSharpString(dungeon.BiomeId)).AppendLine(",");
                builder.Append("          ")
                    .Append(dungeon.Radius.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
                builder.Append("          ")
                    .Append(dungeon.RoomSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.PathSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.SpawnChance.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
                builder.Append("          ")
                    .Append(dungeon.MinDistanceFromCentre.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.AppendLine("          " + (dungeon.BlockOtherSpawns ? "true" : "false") + ",");
                builder.Append("          dungeonRooms");
                AppendDungeonShapeArguments(builder, template, dungeon, modName);
                // Fillings are independent of the shape template: a dungeon of default rules
                // with authored contents is the common first dungeon.
                AppendDungeonFillings(builder, template, dungeon, modName);

                // Where it grows inside the author's own dimension — vanilla's placer never
                // runs there, so without these arguments the dungeon could only ever appear
                // in the Overworld.
                if (dungeon.GrowsInThisDimension)
                {
                    builder.AppendLine(",");
                    builder.Append("          dimensionId: ")
                        .Append(ToCSharpString(template.DimensionId)).AppendLine(",");
                    builder.Append("          dimensionPlacement: DimensionScenePlacementMode.")
                        .Append(dungeon.DimensionPlacement.ToString()).AppendLine(",");
                    builder.Append("          exactLocalPosition: new Unity.Mathematics.int2(")
                        .Append(dungeon.DimensionExactPosition.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(dungeon.DimensionExactPosition.y.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("),");
                    builder.Append("          minRadiusTiles: ")
                        .Append(dungeon.DimensionMinRadius.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(",");
                    builder.Append("          maxRadiusTiles: ")
                        .Append(dungeon.DimensionMaxRadius.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(",");
                    builder.Append("          countPerArea: ")
                        .Append(dungeon.DimensionCount.ToString(CultureInfo.InvariantCulture));
                }

                builder.AppendLine("));");

                // The Overworld pin: one guaranteed copy on the game's own unique-placement
                // rails. The pin's NAME is the save's memory of the dungeon — it is derived
                // from the qualified id and must never change once worlds exist, or an
                // updated mod places a second copy.
                if (dungeon.PinnedInOverworld)
                {
                    string pinName = DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId);
                    string pinError;
                    if (!DimensionCustomSceneNames.IsValid(pinName, out pinError))
                    {
                        Debug.LogWarning(
                            "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' cannot be " +
                            "pinned into the Overworld: " + pinError);
                    }
                    else
                    {
                        builder.AppendLine("      DimensionUniqueDungeonRegistry.Register(");
                        builder.AppendLine("          new DimensionUniqueDungeonDefinition(");
                        builder.Append("              ")
                            .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, dungeon.DungeonId)))
                            .AppendLine(",");
                        builder.Append("              ").Append(ToCSharpString(pinName)).AppendLine(",");
                        builder.AppendLine("              " + (dungeon.PinnedAtExactSpot ? "true" : "false") + ",");
                        builder.Append("              new int2(")
                            .Append(dungeon.PinnedPosition.x.ToString(CultureInfo.InvariantCulture))
                            .Append(", ")
                            .Append(dungeon.PinnedPosition.y.ToString(CultureInfo.InvariantCulture))
                            .AppendLine("),");
                        builder.Append("              ")
                            .Append(dungeon.PinnedDistanceFromCore.ToString(CultureInfo.InvariantCulture))
                            .AppendLine(",");
                        builder.Append("              ").Append(ToCSharpString(dungeon.PinnedBiomeName)).AppendLine(",");
                        builder.AppendLine("              " + (dungeon.PinnedSpawnsImmediately ? "true" : "false") + "));");
                    }
                }

                builder.AppendLine("    }");
            }
        }

        /// <summary>
        /// Emits the shape template's runtime mirrors as extra registration arguments, so the
        /// authored shape survives into the built mod instead of stopping at the asset.
        /// </summary>
        /// <remarks>
        /// A dungeon whose shape template was never switched on emits nothing extra and behaves
        /// exactly as before this existed. Generated-dungeon and single-handmade-room are
        /// alternatives; asking for both gets the generated dungeon and a warning, because
        /// letting the game decide which wins is how content works in testing and not in worlds.
        /// </remarks>
        private static void AppendDungeonShapeArguments(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon,
            string modName)
        {
            DimensionDungeonShapeTemplate shape = dungeon.GeneratedShape;
            if (shape == null)
            {
                return;
            }

            if (shape.IsBothGeneratedAndHandmade)
            {
                Debug.LogWarning(
                    "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is marked as both a " +
                    "generated dungeon and a single handmade room. They are alternatives; the " +
                    "generated dungeon wins.");
            }

            if (shape.IsASingleHandmadeRoom && !shape.GeneratesADungeon)
            {
                // The author names WHICH of the dungeon's own places it is; the first place in
                // its room groups is only the fallback. The named place must be one of them,
                // because a dungeon room is looked up by the name this dimension registered it
                // under — a name from anywhere else resolves to nothing and the dungeon is
                // skipped at assembly with no way for the author to see why.
                string singleSceneName = NamedRoomSceneName(dungeon, modName, shape.SingleRoomSceneId);
                if (singleSceneName == null && !string.IsNullOrEmpty(shape.SingleRoomSceneId))
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is the single " +
                        "handmade room '" + shape.SingleRoomSceneId + "', which is not one of " +
                        "the places in its room groups. Add that place to a room group, or " +
                        "clear the field to use the first place it names. It falls back to the " +
                        "first place for this build.");
                }

                if (string.IsNullOrEmpty(singleSceneName))
                {
                    singleSceneName = FirstRoomSceneName(dungeon, modName);
                }

                if (string.IsNullOrEmpty(singleSceneName))
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Dungeon '" + dungeon.DungeonId + "' is a single " +
                        "handmade room but its room groups name no place, so there is nothing " +
                        "to be. It will assemble as a generated dungeon instead.");
                    return;
                }

                builder.AppendLine(",");
                builder.Append("          singleScene: new DimensionDungeonSingleScene(")
                    .Append(ToCSharpString(singleSceneName))
                    .Append(", ")
                    .Append(shape.KeepsClearRadius.ToString(CultureInfo.InvariantCulture))
                    .Append(")");
                return;
            }

            if (!shape.GeneratesADungeon)
            {
                return;
            }

            builder.AppendLine(",");
            builder.AppendLine("          shape: new DimensionDungeonShape(");
            builder.Append("              ")
                .Append(shape.Seed.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.Append("              ")
                .Append(shape.Radius.ToString(CultureInfo.InvariantCulture)).AppendLine(",");
            builder.AppendLine("              " + (shape.HasAShapedOutline ? "true" : "false") + ",");
            builder.Append("              ")
                .Append(shape.OutlineWobble.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.Append("              ")
                .Append(shape.OutlineBusyness.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.AppendLine("              " + (shape.OutlineFollowsTheRooms ? "true" : "false") + ",");
            builder.AppendLine("              " + (shape.IsRectangular ? "true" : "false") + ",");
            builder.Append("              ")
                .Append(shape.RoomFillSize.ToString("R", CultureInfo.InvariantCulture)).AppendLine("f,");
            builder.Append("              ")
                .Append(shape.PathFillSize.ToString("R", CultureInfo.InvariantCulture)).Append("f)");

            DimensionDungeonRoom[] rooms = shape.Rooms;
            if (rooms.Length > 0)
            {
                builder.AppendLine(",");
                builder.AppendLine("          roomRules: new DimensionDungeonRoomRule[] {");
                for (int i = 0; i < rooms.Length; i++)
                {
                    DimensionDungeonRoom room = rooms[i];
                    builder.Append("            new DimensionDungeonRoomRule { Placement = DimensionRoomPlacement.")
                        .Append(room.Placement.ToString())
                        .Append(", Kind = (DimensionRoomKind)")
                        .Append(((int)room.Kind).ToString(CultureInfo.InvariantCulture))
                        .Append(", MinCount = ").Append(room.HowMany.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxCount = ").Append(room.HowMany.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinRadius = ").Append(room.HowBig.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxRadius = ").Append(room.HowBig.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinSpacing = ").Append(room.HowFarApart.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxSpacing = ").Append(room.HowFarApart.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", AngleMinDegrees = ").Append(room.AtWhatAngle.x.ToString(CultureInfo.InvariantCulture))
                        .Append("f, AngleMaxDegrees = ").Append(room.AtWhatAngle.y.ToString(CultureInfo.InvariantCulture))
                        .Append("f, AlignedWithTheCore = ").Append(room.AlignedWithTheCore ? "true" : "false")
                        .Append(", StraightPaths = ").Append(room.StraightPathsToThem ? "true" : "false")
                        .Append(", MayOverlapOtherRooms = ").Append(room.MayOverlapOtherRooms ? "true" : "false")
                        .Append(", MayOverlapKinds = (DimensionRoomKind)")
                        .Append(((int)room.MayOverlapKinds).ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" },");
                }

                builder.Append("          }");
            }

            DimensionDungeonPath[] paths = shape.Paths;
            if (paths.Length > 0)
            {
                builder.AppendLine(",");
                builder.AppendLine("          pathRules: new DimensionDungeonPathRule[] {");
                for (int i = 0; i < paths.Length; i++)
                {
                    DimensionDungeonPath path = paths[i];
                    builder.Append("            new DimensionDungeonPathRule { Placement = DimensionPathPlacement.")
                        .Append(path.Placement.ToString())
                        .Append(", Kind = (DimensionRoomKind)")
                        .Append(((int)path.Kind).ToString(CultureInfo.InvariantCulture))
                        .Append(", MinCount = ").Append(path.HowMany.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxCount = ").Append(path.HowMany.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", Width = ").Append(path.Width.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, Straight = ").Append(path.Straight ? "true" : "false")
                        .Append(", MayCrossPaths = ").Append(path.MayCrossPaths ? "true" : "false")
                        .Append(", MayCrossPathKinds = (DimensionRoomKind)")
                        .Append(((int)path.MayCrossPathKinds).ToString(CultureInfo.InvariantCulture))
                        .Append(", MayCrossRooms = ").Append(path.MayCrossRooms ? "true" : "false")
                        .Append(", MayCrossRoomKinds = (DimensionRoomKind)")
                        .Append(((int)path.MayCrossRoomKinds).ToString(CultureInfo.InvariantCulture))
                        .Append(", StartsFrom = (DimensionRoomKind)")
                        .Append(((int)path.StartsFrom).ToString(CultureInfo.InvariantCulture))
                        .Append(", EndsAt = (DimensionRoomKind)")
                        .Append(((int)path.EndsAt).ToString(CultureInfo.InvariantCulture))
                        .AppendLine(" },");
                }

                builder.Append("          }");
            }

            string[] outlineBlocks = shape.OutlineBlockIds;
            bool wroteOutline = false;
            for (int i = 0; i < outlineBlocks.Length; i++)
            {
                if (string.IsNullOrEmpty(outlineBlocks[i]))
                {
                    continue;
                }

                if (!wroteOutline)
                {
                    builder.AppendLine(",");
                    builder.Append("          outlineBlockIds: new string[] { ");
                    wroteOutline = true;
                }
                else
                {
                    builder.Append(", ");
                }

                // A mod-owned block ships under its qualified name; a vanilla one keeps the
                // name the game already knows it by.
                string blockName = IsModOwnedObjectName(template, outlineBlocks[i])
                    ? DimensionObjectNamespace.Qualify(modName, outlineBlocks[i])
                    : outlineBlocks[i];
                builder.Append(ToCSharpString(blockName));
            }

            if (wroteOutline)
            {
                builder.Append(" }");
            }

            DimensionDungeonSwap[] swaps = shape.Swaps;
            bool wroteSwaps = false;
            for (int i = 0; i < swaps.Length; i++)
            {
                DimensionDungeonSwap swap = swaps[i];
                if (string.IsNullOrEmpty(swap.ReplaceId) || string.IsNullOrEmpty(swap.WithId))
                {
                    continue;
                }

                if (!wroteSwaps)
                {
                    builder.AppendLine(",");
                    builder.AppendLine("          swaps: new DimensionDungeonSwapRule[] {");
                    wroteSwaps = true;
                }

                string replaceName = IsModOwnedObjectName(template, swap.ReplaceId)
                    ? DimensionObjectNamespace.Qualify(modName, swap.ReplaceId)
                    : swap.ReplaceId;
                string withName = IsModOwnedObjectName(template, swap.WithId)
                    ? DimensionObjectNamespace.Qualify(modName, swap.WithId)
                    : swap.WithId;

                builder.Append("            new DimensionDungeonSwapRule { ReplaceId = ")
                    .Append(ToCSharpString(replaceName))
                    .Append(", WithId = ").Append(ToCSharpString(withName))
                    .Append(", OnlyOneLook = ").Append(swap.OnlyOneLook ? "true" : "false")
                    .Append(", TheLook = ").Append(swap.TheLook.ToString(CultureInfo.InvariantCulture))
                    .Append(", MinReplacementLook = ")
                    .Append(swap.ReplacementLooks.x.ToString(CultureInfo.InvariantCulture))
                    .Append(", MaxReplacementLook = ")
                    .Append(swap.ReplacementLooks.y.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" },");
            }

            if (wroteSwaps)
            {
                builder.Append("          }");
            }
        }

        /// <summary>
        /// Emits the dungeon's room fillings — the procedural content layer. Ids that belong to
        /// the mod ship qualified; vanilla names pass through, and the assembler resolves both
        /// at runtime where the object database exists.
        /// </summary>
        private static void AppendDungeonFillings(
            StringBuilder builder,
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon,
            string modName)
        {
            DimensionRoomFillingAsset[] fillings = dungeon.RoomFillings;
            bool wroteAny = false;
            for (int i = 0; i < fillings.Length; i++)
            {
                DimensionRoomFillingAsset filling = fillings[i];
                if (filling == null || !filling.Enabled || filling.Entries.Length == 0)
                {
                    continue;
                }

                if (!wroteAny)
                {
                    builder.AppendLine(",");
                    builder.AppendLine("          fillings: new DimensionDungeonFillingRule[] {");
                    wroteAny = true;
                }

                builder.Append("            new DimensionDungeonFillingRule(")
                    .Append(ToCSharpString(filling.FillingId))
                    .Append(", (DimensionRoomKind)")
                    .Append(((int)filling.FillsRooms).ToString(CultureInfo.InvariantCulture))
                    .Append(", ").Append(filling.FillsCorridors ? "true" : "false")
                    .Append(", ").Append(filling.OnlyIfAtLeastThisBig.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(", new DimensionDungeonFillingEntry[] {");

                DimensionRoomFillingEntry[] entries = filling.Entries;
                for (int e = 0; e < entries.Length; e++)
                {
                    DimensionRoomFillingEntry entry = entries[e];
                    if (entry == null || string.IsNullOrEmpty(entry.ObjectId))
                    {
                        continue;
                    }

                    builder.Append("              new DimensionDungeonFillingEntry { ObjectId = ")
                        .Append(ToCSharpString(QualifyIfOwn(template, modName, entry.ObjectId)))
                        .Append(", Look = ").Append(entry.Look.ToString(CultureInfo.InvariantCulture))
                        .Append(", MinPatches = ").Append(entry.HowManyPatches.x.ToString(CultureInfo.InvariantCulture))
                        .Append(", MaxPatches = ").Append(entry.HowManyPatches.y.ToString(CultureInfo.InvariantCulture))
                        .Append(", PatchShape = ").Append(((int)entry.PatchShape).ToString(CultureInfo.InvariantCulture))
                        .Append(", PatchSize = ").Append(entry.PatchSize.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, ChanceToAppear = ").Append(entry.ChanceToAppear.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, Density = ").Append(entry.Density.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, MayLandOn = ").Append(QualifiedArrayLiteral(template, modName, entry.MayLandOn))
                        .Append(", NeverOn = ").Append(QualifiedArrayLiteral(template, modName, entry.NeverOn))
                        .Append(", StackCount = ").Append(entry.StackCount.ToString(CultureInfo.InvariantCulture))
                        .Append(", ChestLoot = ").Append(ToCSharpString(entry.ChestLoot))
                        .AppendLine(" },");
                }

                builder.AppendLine("            }),");
            }

            if (wroteAny)
            {
                builder.Append("          }");
            }
        }

        /// <summary>
        /// The registered name of the room the author named, or null when no room group holds it.
        /// </summary>
        /// <remarks>
        /// Matching is against the room groups rather than every scene in the project on purpose:
        /// the single room is also the dungeon's whole content, so a place that is not in a room
        /// group would be a dungeon whose one room is not one of its rooms.
        /// </remarks>
        private static string NamedRoomSceneName(
            DimensionDungeonAsset dungeon,
            string modName,
            string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                return null;
            }

            DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                {
                    continue;
                }

                SceneTemplateAsset[] rooms = groups[g].Rooms;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (rooms[r] != null &&
                        string.Equals(rooms[r].SceneId, sceneId, System.StringComparison.Ordinal))
                    {
                        return DimensionObjectNamespace.Qualify(modName, rooms[r].SceneId);
                    }
                }
            }

            return null;
        }

        private static string FirstRoomSceneName(DimensionDungeonAsset dungeon, string modName)
        {
            DimensionDungeonRoomGroupTemplate[] groups = dungeon.RoomGroups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null)
                {
                    continue;
                }

                SceneTemplateAsset[] rooms = groups[g].Rooms;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (rooms[r] != null && !string.IsNullOrEmpty(rooms[r].SceneId))
                    {
                        return DimensionObjectNamespace.Qualify(modName, rooms[r].SceneId);
                    }
                }
            }

            return null;
        }
    }
}
