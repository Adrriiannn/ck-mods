using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Plants;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{

    /// <summary>
    /// Turns one plant into the family of prefabs Core Keeper expects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A crop in Core Keeper is a seed object, a plant object, a produce object, and a second prefab
    /// of the plant that is already ripe. Measured from Carrock: <c>CarrockSeed</c> 8010,
    /// <c>CarrockPlant</c> 8011, <c>Carrock</c> 8012, plus <c>CompleteCarrockPlantEntity</c> which
    /// shares 8011 and sits at variation 1 with its stage already at the top.
    /// </para>
    /// <para>
    /// The seed's <c>turnsIntoPlantID</c> has to point at the plant, and the plant's
    /// <c>objectToDropWhenHarvested</c> at the produce. Those two references are the whole reason this
    /// is one asset rather than three: authored separately they are a pair of ids to keep in step by
    /// hand, and a typo in either produces a seed that grows nothing or a plant that yields nothing,
    /// with no error either way.
    /// </para>
    /// <para>
    /// NEITHER REFERENCE IS BAKED AS AN ID ANY MORE. <c>PugMod.API.Authoring</c> is empty in the
    /// editor, so an id resolved here for anything the mod itself makes comes out as <c>None</c> —
    /// which is how every seed this generator has ever produced ended up growing nothing at all. The
    /// prefabs now carry the framework's own seed and plant components, which hold NAMES, and the
    /// converters in <c>ExpandNullforge.Plants</c> resolve them inside the running game.
    /// </para>
    /// <para>
    /// A CROP WITH BETTER VERSIONS IS MORE PREFABS, NOT MORE OBJECTS. Each version is a variation of
    /// the same seed and the same plant, so it gets a prefab of each; a planting that comes up
    /// ordinary still gets a variation of its own, because variation 0 is the only mark the roll has
    /// for "not decided yet" and leaving it there would re-roll every seed on the next world load.
    /// </para>
    /// </remarks>
    internal static partial class DimensionPlantGenerator
    {
        /// <summary>The folder inside a mod where generated plant prefabs live.</summary>
        public const string FolderName = "Plants";

        /// <summary>Suffix for the seed object.</summary>
        public const string SeedSuffix = "Seed";

        /// <summary>Suffix for the already-ripe prefab of the plant.</summary>
        /// <remarks>
        /// Mirrors vanilla's own <c>Complete…PlantEntity</c> naming so the two prefabs sort together
        /// and it is obvious they are one object.
        /// </remarks>
        public const string RipeSuffix = "PlantComplete";

        /// <summary>Suffix for the growing plant object.</summary>
        public const string PlantSuffix = "Plant";

        /// <summary>
        /// Suffix for the seed a planting that came up ordinary sits on.
        /// </summary>
        /// <remarks>
        /// Identical to the ordinary seed in every way that matters. It exists only so a rolled
        /// planting is never left on variation 0, which is what the roll reads as "not rolled yet".
        /// </remarks>
        public const string PlainSeedSuffix = "SeedPlain";

        public static DimensionPlantGenerationReport Generate(
            IEnumerable<DimensionPlantAsset> plants,
            string outputFolder,
            DimensionNamingContext naming)
        {
            DimensionPlantGenerationReport report = new DimensionPlantGenerationReport();
            if (plants == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no plants were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);

            // Which files this run means to own, per plant. Collected as it goes so the prefabs a
            // removed or renamed version left behind can be taken away afterwards — left in place
            // they keep registering a variation of the same object, and two prefabs claiming one
            // variation is a collision the game resolves by picking one of them.
            Dictionary<string, HashSet<string>> expected =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            // The same, for the sprite assets and pictures. Renaming a version leaves its art
            // behind exactly as it leaves its prefab behind, and art nobody references is still
            // compiled into the game's sprite atlas for ever.
            HashSet<string> artFiles = new HashSet<string>(StringComparer.Ordinal);

            GameObject plantVisual = null;
            GameObject seedVisual = null;
            try
            {
                AssetDatabase.StartAssetEditing();

                // Two bodies for the whole folder, shared by every crop in it. See
                // DimensionPlantViewBuilder for why this is one prefab rather than one per crop.
                DimensionPlantViewBuilder.EnsureVisuals(outputFolder, out plantVisual, out seedVisual);
                if (plantVisual == null || seedVisual == null)
                {
                    report.Errors.Add(
                        "The shared plant and seed bodies could not be saved, so crops would have " +
                        "grown invisibly. Nothing was drawn for any of them this build.");
                }

                foreach (DimensionPlantAsset plant in plants)
                {
                    GenerateOne(
                        plant, outputFolder, naming, report, expected, artFiles,
                        plantVisual, seedVisual);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            RemoveStalePrefabs(outputFolder, expected, report);
            RemoveStaleArt(outputFolder, expected, artFiles, report);

            // The sweep above only reaches art belonging to a crop this project still has, so
            // renaming a whole CROP leaves its sprite assets behind — listed in the mod's manifest,
            // compiled into the game's atlas, and holding the address the old name hashed to. The
            // shared sweep answers "is anything still asking for this?" from the manifest instead
            // of from the crop list, which is the only question that catches a rename.
            DimensionSpriteArtFileUtility.RemoveStaleGeneratedArt(
                outputFolder,
                artFiles,
                CollectPlantPicturePaths(plants),
                delegate(string path) { report.Removed.Add(path); });
            return report;
        }

        private static void GenerateOne(
            DimensionPlantAsset plant,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionPlantGenerationReport report,
            Dictionary<string, HashSet<string>> expected,
            HashSet<string> artFiles,
            GameObject plantVisual,
            GameObject seedVisual)
        {
            if (plant == null || !plant.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(plant.PlantId))
            {
                report.Skipped.Add("A plant with no id was skipped.");
                return;
            }

            // A plant that spreads on its own is never planted, so nothing ever rolls for it and a
            // better version of it could never come up.
            DimensionCropVersionTemplate[] versions = plant.IsPlanted
                ? plant.EnabledVersions
                : new DimensionCropVersionTemplate[0];
            string[] versionKeys = BuildVersionKeys(plant, versions, report.Warnings);

            WarnAboutPlant(plant, versions, report);
            WarnAboutArt(plant, versions, report);

            string plantName = naming.QualifyGenerated(plant.PlantId + PlantSuffix);
            string seedName = naming.QualifyGenerated(plant.PlantId + SeedSuffix);
            HashSet<string> mine = new HashSet<string>(StringComparer.Ordinal);
            expected[DimensionGeneratedPrefabUtility.SanitizeAuthoredName(plant.PlantId, "Plant")] = mine;

            // The pictures before anything else, because every prefab below has to be told which
            // body to draw itself with and there is no body worth pointing at without them.
            DimensionPlantArtSet art = BuildArt(
                plant, versions, versionKeys, plantName, seedName, outputFolder, artFiles, report,
                plantVisual, seedVisual);

            // The growing plant first, because the seed has to point at it.
            Build(outputFolder, plant.PlantId + PlantSuffix, plant, report, mine,
                delegate(GameObject root)
                {
                    ConfigurePlant(
                        root, plant, plantName, seedName, null, 0, 0, versions.Length > 0, naming,
                        report, art.PlantVisualFor(-1));
                });

            // The same object again, already ripe. Not a second object — the same ObjectID at the
            // variation vanilla's Complete… prefabs use, with the stage at the top.
            Build(outputFolder, plant.PlantId + RipeSuffix, plant, report, mine,
                delegate(GameObject root)
                {
                    ConfigurePlant(
                        root,
                        plant,
                        plantName,
                        seedName,
                        null,
                        DimensionPlantAsset.CompletePlantVariation,
                        plant.GrowthStages,
                        versions.Length > 0,
                        naming,
                        report,
                        art.PlantVisualFor(-1));
                });

            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionTemplate version = versions[i];
                int plantVariation = PlantVariationFor(i);

                // Copied out of the loop variable: the closure below runs before the next
                // iteration, but a loop variable captured directly is one variable shared by every
                // closure, and the day this stops being called straight away it would silently
                // become the last version's body on all of them.
                GameObject body = art.PlantVisualFor(i);
                Build(outputFolder, plant.PlantId + PlantSuffix + versionKeys[i], plant, report, mine,
                    delegate(GameObject root)
                    {
                        ConfigurePlant(
                            root, plant, plantName, seedName, version, plantVariation, 0, true, naming,
                            report, body);
                    });
            }

            if (!plant.IsPlanted)
            {
                return;
            }

            Build(outputFolder, plant.PlantId + SeedSuffix, plant, report, mine,
                delegate(GameObject root)
                {
                    ConfigureSeed(
                        root, plant, plantName, seedName, 0, versions, report,
                        art.SeedVisualFor(-1));
                });

            for (int i = 0; i < versions.Length; i++)
            {
                int seedVariation = SeedVariationFor(i);
                GameObject body = art.SeedVisualFor(i);
                Build(outputFolder, plant.PlantId + SeedSuffix + versionKeys[i], plant, report, mine,
                    delegate(GameObject root)
                    {
                        ConfigureSeed(
                            root, plant, plantName, seedName, seedVariation, versions, report, body);
                    });
            }

            if (versions.Length > 0)
            {
                int plainVariation = PlainSeedVariationFor(versions.Length);
                Build(outputFolder, plant.PlantId + PlainSeedSuffix, plant, report, mine,
                    delegate(GameObject root)
                    {
                        ConfigureSeed(
                            root, plant, plantName, seedName, plainVariation, versions, report,
                            art.SeedVisualFor(-1));
                    });
            }
        }

        private static void Build(
            string outputFolder,
            string id,
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report,
            HashSet<string> mine,
            Action<GameObject> configure)
        {
            mine.Add(DimensionGeneratedPrefabUtility.SanitizeAuthoredName(id, "Plant"));
            BuildPrefab(outputFolder, id, plant, report, configure);
        }

        private static void BuildPrefab(
            string outputFolder,
            string id,
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report,
            Action<GameObject> configure)
        {
            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(id, "Plant") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(id);

            try
            {
                configure(root);

                // LAST, and after everything the crop's own passes wrote. A plant and a seed are
                // placed objects, and 26 of 26 vanilla seeds and 35 of the 38 vanilla plants that
                // carry a body have one. Without it the crop is not in the collision world, so no
                // swing, no tool and no blast can ever find it: the ripe crop sits there and
                // nothing harvests it, and nothing anywhere says why.
                //
                // A crop is NOT the ordinary placed-thing box. CarrockPlantEntity and
                // CarrockSeedEntity are a ball 0.6 across on Category06 that answers no collision
                // at all, so a player and an animal walk straight over it and can still hit it —
                // and a pass that left the collision answer at its fresh default gave every crop a
                // hitbox a player walks into.
                DimensionQueryCompanions.GiveItTheBodyItsFootprintAsksFor(
                    root,
                    id,
                    DimensionQueryCompanions.PlacedThingKind.Plant,
                    delegate(string message) { report.Warnings.Add(message); });

                // And what it IS, carried onto the running object. The system that applies burning
                // ground, acid, mould and oil names that on the object it works over, so a crop
                // without it stands in lava untouched.
                DimensionQueryCompanions.CarriesWhatItIsOntoTheRunningObject(root);

                // And the sweep, last, the way the other generators end.
                DimensionQueryCompanions.CloseTheGaps(
                    root,
                    id,
                    delegate(string message) { report.Warnings.Add(message); });

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
                // The stack is the whole diagnostic here: these failures are almost always a Core
                // Keeper OnValidate throwing inside AddComponent, and the message alone
                // ("Object reference not set") names neither the component nor the line.
                report.Errors.Add(
                    id + " failed to generate: " + exception.Message + "\n" + exception.StackTrace);
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

        /// <summary>
        /// Takes away prefabs this generator made for a plant and no longer wants.
        /// </summary>
        /// <remarks>
        /// Only files whose names begin with a plant id followed by this generator's own suffixes are
        /// considered, so a hand-authored prefab sitting in the same folder is invisible here. The
        /// case that makes this necessary is renaming a version: the old version's prefab keeps
        /// claiming the variation the new one now uses, and the game would have two prefabs to choose
        /// between for one seed.
        /// </remarks>
        private static void RemoveStalePrefabs(
            string outputFolder,
            Dictionary<string, HashSet<string>> expected,
            DimensionPlantGenerationReport report)
        {
            if (expected.Count == 0 || !AssetDatabase.IsValidFolder(outputFolder))
            {
                return;
            }

            // Every name this run wants, across all plants, gathered before anything is judged: two
            // plants can share a folder, and deciding per plant would let one of them delete a file
            // another one had just written.
            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);

            // The two shared bodies belong to the folder rather than to any one crop, so they are
            // never in a crop's own list and would look abandoned to the sweep below.
            wanted.Add(DimensionPlantViewBuilder.PlantVisualName);
            wanted.Add(DimensionPlantViewBuilder.SeedVisualName);
            foreach (KeyValuePair<string, HashSet<string>> entry in expected)
            {
                foreach (string stem in entry.Value)
                {
                    wanted.Add(stem);
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { outputFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = StemOf(path);
                if (wanted.Contains(name))
                {
                    continue;
                }

                bool ours = false;
                foreach (KeyValuePair<string, HashSet<string>> entry in expected)
                {
                    if (IsGeneratedNameFor(name, entry.Key))
                    {
                        ours = true;
                        break;
                    }
                }

                if (ours && AssetDatabase.DeleteAsset(path))
                {
                    report.Removed.Add(path);
                }
            }
        }

        /// <summary>The file name without its folder or its extension.</summary>
        private static string StemOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            string name = slash < 0 ? assetPath : assetPath.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(0, dot);
        }

        /// <summary>Whether a file name is one this generator would have made for that plant id.</summary>
        private static bool IsGeneratedNameFor(string stem, string plantId)
        {
            return stem.StartsWith(plantId + PlantSuffix, StringComparison.Ordinal)
                || stem.StartsWith(plantId + SeedSuffix, StringComparison.Ordinal);
        }

        /// <summary>
        /// A short, unique word per version to hang its prefab and art names on.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two versions sharing a name would share a prefab path, and the second would quietly
        /// overwrite the first — leaving one version with no prefab at all and roughly its share of
        /// plantings growing nothing. So a repeat gets its position appended and is reported.
        /// </para>
        /// <para>
        /// PUBLIC BECAUSE THE BOOTSTRAP HAS TO AGREE WITH IT. A version's art is addressed by this
        /// key, and the generated registration is written by a different step that never sees the
        /// prefabs: the two agree only by computing the same key from the same list. The warnings
        /// list is optional so that second caller can ask the question without reporting the
        /// answers twice.
        /// </para>
        /// </remarks>
        public static string[] BuildVersionKeys(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            List<string> warnings)
        {
            string[] keys = new string[versions.Length];
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            // Two words this generator already spends on prefabs of its own: a version called
            // "Complete" would land on the ripe plant's file, and one called "Plain" on the file the
            // ordinary planting uses. Claiming them up front sends such a version to a numbered name
            // and says so, rather than one prefab overwriting the other.
            used.Add(RipeSuffix.Substring(PlantSuffix.Length));
            used.Add(PlainSeedSuffix.Substring(SeedSuffix.Length));
            for (int i = 0; i < versions.Length; i++)
            {
                string key = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(versions[i].VersionName, "Plant");
                if (string.IsNullOrEmpty(versions[i].VersionName))
                {
                    key = "Version" + (i + 1).ToString();
                    Warn(
                        warnings,
                        "'" + plant.DisplayName + "' has a better version with no name, so its " +
                        "prefabs are called '" + key + "'. Give it a name to say what players get.");
                }

                if (!used.Add(key))
                {
                    string unique = key + (i + 1).ToString();
                    Warn(
                        warnings,
                        "'" + plant.DisplayName + "' cannot call a version '" + key + "': that name " +
                        "is already taken, either by another version or by one of the prefabs this " +
                        "framework writes for every crop. It is generated as '" + unique +
                        "' instead. Rename it.");
                    key = unique;
                    used.Add(key);
                }

                keys[i] = key;
            }

            return keys;
        }

        /// <summary>
        /// The name a reference should be written down as, for the game to look up when it loads.
        /// </summary>
        /// <remarks>
        /// Nothing is resolved to an id here on purpose. <c>PugMod.API.Authoring</c> holds no objects
        /// in the editor, so an id resolved at this point is <c>None</c> for anything but a vanilla
        /// name — which is exactly the bug that made every generated seed grow nothing. The name is
        /// carried through instead and the converters resolve it in the running game.
        /// </remarks>
        private static string ResolveReference(string itemId, DimensionNamingContext naming)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return string.Empty;
            }

            return naming.QualifyReference(itemId);
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>
        /// Adds the root-plant component without the exception its own <c>OnValidate</c> throws.
        /// </summary>
        /// <remarks>
        /// The same trap as <c>InventoryAuthoring</c>: <c>RootPlantAuthoring.canGrowOnTilesets</c> has
        /// no field initialiser and <c>OnValidate</c> walks it, and Unity runs <c>OnValidate</c>
        /// synchronously inside <c>AddComponent</c> — before we can assign the list. Unity logs rather
        /// than rethrows, so the prefab is fine either way; the quiet window is one call wide and only
        /// stops a console error appearing for every root plant a modder generates.
        /// </remarks>
        private static RootPlantAuthoring EnsureRootPlant(GameObject root)
        {
            RootPlantAuthoring existing = root.GetComponent<RootPlantAuthoring>();
            if (existing != null)
            {
                return existing;
            }

            bool logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            RootPlantAuthoring added;
            try
            {
                added = root.AddComponent<RootPlantAuthoring>();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
            }

            added.canGrowOnTilesets = new List<Tileset>();
            return added;
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }
    }
}
