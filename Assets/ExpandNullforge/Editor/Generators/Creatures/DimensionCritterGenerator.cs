using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionCritterGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>Art files thrown away because nothing in the project describes them any more.</summary>
        public readonly List<string> Removed = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Critters: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns an authored critter into the small ambient creature Core Keeper spawns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This generator did not exist. The asset did, the dashboard editor did, the Add button did,
    /// and the drop-source checker already knew critter ids — so a creator could author critters,
    /// watch the dashboard count them, generate, and find nothing in the world. It is the second
    /// time the same shape of gap has turned up, which is why the reachability questions are now
    /// written down rather than remembered.
    /// </para>
    /// <para>
    /// A critter is not a creature with smaller numbers. It has no health, no faction and no
    /// combat — <c>CritterAuthoring</c> plus the spawn rules is nearly all of it. What makes it
    /// feel alive is the scatter: it runs when you get close, which is <c>FleeStateAuthoring</c>
    /// rather than anything on the critter component.
    /// </para>
    /// </remarks>
    internal static class DimensionCritterGenerator
    {
        public const string FolderName = "Critters";

        public static DimensionCritterGenerationReport Generate(
            IEnumerable<DimensionCritterAsset> critters,
            string outputFolder,
            DimensionNamingContext naming,
            IEnumerable<DimensionTilesetAsset> tilesets = null)
        {
            DimensionCritterGenerationReport report = new DimensionCritterGenerationReport();
            if (critters == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no critters were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);
            Func<string, int> resolveTileset = BuildTilesetResolver(tilesets);

            // Held so the sweep below can be told what this project still describes. The caller
            // hands over an IEnumerable, which may only be walked once.
            List<DimensionCritterAsset> critterList = new List<DimensionCritterAsset>(critters);

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < critterList.Count; i++)
                {
                    GenerateOne(critterList[i], outputFolder, naming, report, resolveTileset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            RemoveStaleArt(critterList, outputFolder, report);

            return report;
        }

        /// <summary>
        /// Throws away art belonging to critters this project no longer has.
        /// </summary>
        /// <remarks>
        /// The same sweep the creature generator runs, and needed here for the same reason: a
        /// critter's sprite asset is named after its id, so renaming one abandons its art in the
        /// mod's sprite manifest, where it is still compiled into the game's atlas and still holds
        /// the address its old name hashed to. A disabled critter keeps its art — disabling is
        /// putting something aside, not deleting it.
        /// </remarks>
        private static void RemoveStaleArt(
            List<DimensionCritterAsset> critters,
            string outputFolder,
            DimensionCritterGenerationReport report)
        {
            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> protectedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < critters.Count; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null || string.IsNullOrEmpty(critter.CritterId))
                {
                    continue;
                }

                wanted.Add(DimensionCreatureSpriteAssetUtility.AssetNameFor(critter.CritterId));
                DimensionCreatureGenerator.CollectPicturePaths(
                    critter.Visual == null ? null : critter.Visual.Animation, protectedPaths);
            }

            DimensionSpriteArtFileUtility.RemoveStaleGeneratedArt(
                outputFolder,
                wanted,
                protectedPaths,
                delegate(string path) { report.Removed.Add(path); });
        }

        private static void GenerateOne(
            DimensionCritterAsset critter,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCritterGenerationReport report,
            Func<string, int> resolveTileset)
        {
            if (critter == null || !critter.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(critter.CritterId))
            {
                report.Skipped.Add("A critter with no id was skipped.");
                return;
            }

            if (critter.LivesNowhere)
            {
                report.Warnings.Add(
                    "'" + critter.DisplayName + "' names nowhere to live, so it will never appear. " +
                    (critter.Home == DimensionCritterHome.ByGround
                        ? "Name the tilesets it lives on."
                        : "Name the biomes it lives in."));
            }

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(critter.CritterId, "Critter") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(critter.CritterId);

            try
            {
                Configure(root, critter, naming, report, resolveTileset, outputFolder);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    critter.CritterId + " failed to generate: " + exception.Message +
                    "\n" + exception.StackTrace);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            DimensionCritterAsset critter,
            DimensionNamingContext naming,
            DimensionCritterGenerationReport report,
            Func<string, int> resolveTileset,
            string outputFolder)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(critter.CritterId);

            // Critter, not Creature — the two are not interchangeable even though ObjectConverter
            // treats them the same. Putting a caught critter back down goes through
            // PlaceObjectSlot, which refuses anything that is not a critter, a placeable prefab or
            // cattle (`ck-db\Pug.Other\PlaceObjectSlot.cs:91`), and the critter catcher's own
            // display draws a Creature as bait rather than as the animal in the tank
            // (`ck-db\Pug.Objects\CritterCatcher.cs:50`). Every vanilla critter prefab is Critter.
            obj.objectType = ObjectType.Critter;
            obj.initialAmount = 1;

            // What a critter counts as to everything else's targeting, exactly as the game's own
            // critter prefabs tag themselves. Without it nothing can tell one from a rock.
            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.Critter, true);

            // A critter with no graphical prefab is a scuttling patch of nothing. It got one for
            // the same reason mobs did: the entity was already being told what it was doing.
            DimensionCreatureViewBuilder.Apply(
                root,
                critter.CritterId,
                obj.objectName,
                critter.Visual,
                outputFolder,
                delegate(string message)
                {
                    report.Warnings.Add("'" + critter.DisplayName + "' " + message);
                });

            CritterAuthoring authored = EnsureComponent<CritterAuthoring>(root);
            authored.isFlying = critter.IsFlying;
            authored.spawnContinuously = critter.SpawnContinuously;
            authored.isPersistent = critter.IsPersistent;
            authored.allowLargerAmount = critter.AllowLargerAmount;
            authored.spawnType = critter.Home == DimensionCritterHome.ByGround
                ? SpawnType.Tileset
                : SpawnType.Biome;

            // Both lists are always assigned, even the unused one. The converter reads whichever
            // matches the spawn type and a null list would throw before it got there.
            authored.biomesToSpawnIn = new List<Biome>();
            authored.tilesetsToSpawnIn = new List<Tileset>();

            if (critter.Home == DimensionCritterHome.ByGround)
            {
                string[] wanted = critter.TilesetIds;
                for (int i = 0; i < wanted.Length; i++)
                {
                    int resolved = resolveTileset == null ? -1 : resolveTileset(wanted[i]);
                    if (resolved < 0)
                    {
                        report.Warnings.Add(
                            "'" + critter.DisplayName + "' lives on '" + wanted[i] +
                            "', which is neither one of this mod's tilesets nor one of the game's.");
                        continue;
                    }

                    authored.tilesetsToSpawnIn.Add((Tileset)resolved);
                }
            }
            else
            {
                // A biome-homed critter needs its biome list filled in here. Left empty,
                // spawnType says Biome, the list says nowhere, and the critter is authored,
                // warned-clean and absent. Vanilla biome names bind the game's values; the mod's
                // own biomes bind the same minted ints the biome sampler reports.
                string[] biomes = critter.AllowedBiomeIds;
                for (int i = 0; i < biomes.Length; i++)
                {
                    if (string.IsNullOrEmpty(biomes[i]))
                    {
                        continue;
                    }

                    Biome vanilla;
                    authored.biomesToSpawnIn.Add(
                        Enum.TryParse(biomes[i], false, out vanilla)
                            ? vanilla
                            : ExpandNullforge.Zones.DimensionBiomeIdentity.GetOrAssign(biomes[i]));
                }

                if (authored.biomesToSpawnIn.Count == 0)
                {
                    report.Warnings.Add(
                        "'" + critter.DisplayName + "' is homed by biome but names none, so it " +
                        "spawns nowhere. Name a biome, or home it by ground instead.");
                }
            }

            // Everything a critter needs to be a critter rather than a prop. Scatter is what makes
            // one feel alive, and it is not on CritterAuthoring at all.
            DimensionObjectSpine.ApplyUniversal(root, false);

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                critter.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + critter.DisplayName + "' " + message);
                });
            EnsureComponent<MovementSpeedAuthoring>(root);
            EnsureComponent<FactionAuthoring>(root);

            if (critter.CanBeCaught)
            {
                EnsureComponent<Pug.Automation.CritterCatcherCatchableAuthoring>(root);

                // A Critter Catcher only ever considers a critter the world keeps making. The
                // catcher walks the list of critter kinds and skips any that is not set to keep
                // appearing, so "can be caught" with "keeps appearing" turned off is a critter no
                // catcher will ever take, and nothing said so.
                if (!critter.SpawnContinuously)
                {
                    report.Warnings.Add(
                        "'" + critter.DisplayName + "' can be caught but does not keep appearing, " +
                        "and a Critter Catcher only takes critters the world keeps making. Turn " +
                        "'keeps appearing' back on, or accept that this one can only be caught " +
                        "with a net.");
                }
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.CritterCatcherCatchableAuthoring>(root);
            }

            GiveItEverythingAVanillaCritterHas(root, critter, report);
        }

        /// <summary>
        /// The rest of what makes a critter a critter, diffed against the game's own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set beside <c>CritterLarvaEntity</c>, this is what a generated critter would otherwise
        /// be missing: its whole state machine, its health, its scatter, its gravity, its
        /// perception, its despawn and its placeability — without which it cannot move, be hurt,
        /// die, despawn, or be put back down after being caught. Scatter is the thing that makes a
        /// critter feel alive, and nothing else in this file adds it.
        /// </para>
        /// <para>
        /// Every number here is the smallest one that makes the game's systems look at the critter
        /// at all: one hit of health, the game's own scatter distances, and a reach that lets it be
        /// noticed. A critter is not something an author tunes — it is the moth in the corner of
        /// the room — so none of this asks a new question.
        /// </para>
        /// </remarks>
        private static void GiveItEverythingAVanillaCritterHas(
            GameObject root,
            DimensionCritterAsset critter,
            DimensionCritterGenerationReport report)
        {
            // First, because the scatter below belongs to a body. A critter with no hitbox cannot
            // be bumped into, cannot be swatted, cannot be caught, and — because a body is what
            // produces the velocity every movement state writes — cannot move.
            //
            // The critter's own body, not a creature's. CritterLarvaEntity and ButterflyBaseEntity
            // are a small sphere on the critter layer; a critter left on the enemy layer is chased,
            // shot at and counted as a monster by everything in the game that watches for one.
            DimensionQueryCompanions.HasTheBodyACritterHas(
                root,
                delegate(string message)
                {
                    report.Warnings.Add("'" + critter.DisplayName + "' " + message);
                });

            // Idle, took-damage and death. Without the state root nothing about the critter ever
            // enters a state, so the wander pass and the death pass both skip it.
            DimensionObjectSpine.ApplyDamageableStates(root);

            // One hit. Death is downstream of health reaching zero: with no health at all the
            // critter absorbs everything forever and can never be caught in a net or swatted.
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            if (health.maxHealth <= 0)
            {
                health.dontCalculateHealthFromLevel = true;
                health.maxHealth = 1;
                health.startHealth = 1;
                health.maxHealthMultiplier = 1f;
            }

            // The scatter. RandomWalkStateSystem is what vanilla critters actually move with, and
            // the numbers are the short nervous hops a critter makes rather than a creature's walk.
            RandomWalkStateAuthoring walk = EnsureComponent<RandomWalkStateAuthoring>(root);
            if (walk.maxWalkDistance <= 0f)
            {
                walk.minWalkDistance = 0.5f;
                walk.maxWalkDistance = 1.5f;
                walk.minIdleDuration = 0.5f;
                walk.maxIdleDuration = 2f;
                walk.movementSpeedMultiplier = 1f;
                walk.maxWalkDuration = 1f;
            }

            EnsureComponent<RandomWalkGravityAuthoring>(root);

            // Being noticed and noticing. A critter with no tracker is invisible to everything
            // else's overlap, which is what a catcher and a hungry animal both use to find one.
            DimensionQueryCompanions.SeesNearbyThings(
                root,
                DimensionQueryCompanions.VanillaNoticeRadius,
                DimensionQueryCompanions.VanillaNoticeLayers,
                false);

            // Critters are scenery that moves: the game clears them when nobody is around rather
            // than keeping every moth ever spawned alive in a saved world.
            EnsureComponent<DestroyWhenNoNearbyPlayerAuthoring>(root);

            // Putting a caught one back down. PlaceObjectSlot will accept a critter, but the
            // placement handler still needs the placeable answer to know where it may go.
            EnsureComponent<PlaceableObjectAuthoring>(root);

            // Being sent to players, having a body, and being able to turn — the same three every
            // one of the game's own creature and critter prefabs carries, and none of ours did.
            DimensionQueryCompanions.FinishACritter(
                root,
                critter.DisplayName,
                delegate(string message)
                {
                    report.Warnings.Add(message);
                });
        }

        /// <summary>Resolves a tileset name, the mod's own first, then the game's.</summary>
        private static Func<string, int> BuildTilesetResolver(
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            Dictionary<string, int> custom = new Dictionary<string, int>();
            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset == null)
                    {
                        continue;
                    }

                    string name = tileset.TilesetName;
                    if (!string.IsNullOrEmpty(name) && !custom.ContainsKey(name))
                    {
                        custom[name] = tileset.TilesetId;
                    }
                }
            }

            return delegate(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return -1;
                }

                int id;
                if (custom.TryGetValue(name, out id))
                {
                    return id;
                }

                Tileset vanilla;
                return Enum.TryParse(name, false, out vanilla) ? (int)vanilla : -1;
            };
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T existing = root.GetComponent<T>();
            return existing != null ? existing : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root) where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

    }
}
