using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Plants;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating plants did.</summary>
    internal sealed class DimensionPlantGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Removed = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Plants: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

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
    internal static class DimensionPlantGenerator
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

        /// <summary>Where every picture the project's crops point at currently lives.</summary>
        /// <remarks>
        /// Passed to the sweep so a generated file that an author has since picked as a SOURCE is
        /// never deleted. Copying leaves a source that already sits at its destination alone, which
        /// makes that a supported thing to do, and deleting one would take away the only copy.
        /// </remarks>
        private static HashSet<string> CollectPlantPicturePaths(IEnumerable<DimensionPlantAsset> plants)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            if (plants == null)
            {
                return paths;
            }

            foreach (DimensionPlantAsset plant in plants)
            {
                if (plant == null)
                {
                    continue;
                }

                DimensionPlantArtTemplate art = plant.Art;
                if (art != null)
                {
                    AddPicturePaths(art.GrowthPictures, paths);
                    AddPicturePath(art.ShinePicture, paths);
                    AddPicturePath(art.SeedPicture, paths);
                    AddPicturePath(art.SeedInWetGroundPicture, paths);
                }

                DimensionCropVersionTemplate[] versions = plant.Versions;
                if (versions == null)
                {
                    continue;
                }

                for (int i = 0; i < versions.Length; i++)
                {
                    DimensionCropVersionLookTemplate look = versions[i] == null ? null : versions[i].Look;
                    if (look == null)
                    {
                        continue;
                    }

                    AddPicturePaths(look.GrowthPictures, paths);
                    AddPicturePath(look.ShinePicture, paths);
                    AddPicturePath(look.SeedPicture, paths);
                    AddPicturePath(look.SeedInWetGroundPicture, paths);
                }
            }

            return paths;
        }

        private static void AddPicturePaths(Texture2D[] textures, HashSet<string> paths)
        {
            if (textures == null)
            {
                return;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                AddPicturePath(textures[i], paths);
            }
        }

        private static void AddPicturePath(Texture2D texture, HashSet<string> paths)
        {
            if (texture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
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

        /// <summary>
        /// What was drawn for one crop, and which shared body each of its prefabs should point at.
        /// </summary>
        /// <remarks>
        /// A prefab with no art gets NO body rather than the shared one. That is not tidiness: the
        /// body is pooled, so a crop that draws nothing would be handed an instance still wearing
        /// the last crop's pictures and would grow disguised as a neighbour. An invisible crop is
        /// the honest result of drawing nothing, and it is what the warning says will happen.
        /// </remarks>
        private sealed class DimensionPlantArtSet
        {
            /// <summary>Whether the crop's own pictures made it into a sprite asset.</summary>
            public bool HasPlantArt;

            public bool HasSeedArt;

            /// <summary>Whether each version's OWN pictures made it into one, by version index.</summary>
            /// <remarks>
            /// Kept apart from the crop's because a version may draw itself while the crop draws
            /// nothing — an author part way through, or a crop that only exists as its rare form.
            /// Rolling the two together would leave that version's prefab pointing at no body while
            /// its art sat finished on disk.
            /// </remarks>
            public bool[] VersionPlantArt = new bool[0];

            public bool[] VersionSeedArt = new bool[0];

            public GameObject PlantVisual;

            public GameObject SeedVisual;

            /// <summary>The body for the crop itself, or for one version by its index.</summary>
            public GameObject PlantVisualFor(int versionIndex)
            {
                bool drawn = HasPlantArt ||
                    (versionIndex >= 0 && versionIndex < VersionPlantArt.Length &&
                        VersionPlantArt[versionIndex]);
                return drawn ? PlantVisual : null;
            }

            public GameObject SeedVisualFor(int versionIndex)
            {
                bool drawn = HasSeedArt ||
                    (versionIndex >= 0 && versionIndex < VersionSeedArt.Length &&
                        VersionSeedArt[versionIndex]);
                return drawn ? SeedVisual : null;
            }
        }

        /// <summary>
        /// Writes every sprite asset one crop needs: its own, and one per version that draws
        /// something of its own.
        /// </summary>
        /// <remarks>
        /// A version that only washes the ordinary pictures a colour gets no asset — it points at
        /// the crop's and the wash is applied where it is drawn, which is what makes a recoloured
        /// version cost nothing but a colour.
        /// </remarks>
        private static DimensionPlantArtSet BuildArt(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            string[] versionKeys,
            string plantName,
            string seedName,
            string outputFolder,
            HashSet<string> artFiles,
            DimensionPlantGenerationReport report,
            GameObject plantVisual,
            GameObject seedVisual)
        {
            DimensionPlantArtSet set = new DimensionPlantArtSet
            {
                PlantVisual = plantVisual,
                SeedVisual = seedVisual,
                VersionPlantArt = new bool[versions.Length],
                VersionSeedArt = new bool[versions.Length]
            };
            DimensionPlantArtTemplate art = plant.Art;
            Action<string> warn = delegate(string message)
            {
                report.Warnings.Add("'" + plant.DisplayName + "' " + message);
            };

            if (art.HasPlantPictures)
            {
                set.HasPlantArt = BuildOnePlantArt(
                    plant, art, null, null, plantName, outputFolder, artFiles, warn);
            }

            if (plant.IsPlanted && art.HasSeedPicture)
            {
                set.HasSeedArt = BuildOneSeedArt(
                    plant, art, null, null, seedName, outputFolder, artFiles, warn);
            }

            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionLookTemplate look = versions[i].Look;
                if (look.HasPlantPictures)
                {
                    set.VersionPlantArt[i] = BuildOnePlantArt(
                        plant, art, look, versionKeys[i], plantName, outputFolder, artFiles, warn);
                }

                if (plant.IsPlanted && look.HasSeedPicture)
                {
                    set.VersionSeedArt[i] = BuildOneSeedArt(
                        plant, art, look, versionKeys[i], seedName, outputFolder, artFiles, warn);
                }
            }

            return set;
        }

        private static bool BuildOnePlantArt(
            DimensionPlantAsset plant,
            DimensionPlantArtTemplate art,
            DimensionCropVersionLookTemplate look,
            string versionKey,
            string plantName,
            string outputFolder,
            HashSet<string> artFiles,
            Action<string> warn)
        {
            string assetName = PlantArtName(plant.PlantId, versionKey);
            artFiles.Add(assetName);

            int needed = plant.PicturesNeeded;
            Texture2D[] source = look != null && look.HasPlantPictures
                ? look.GrowthPictures
                : art.GrowthPictures;

            // Sized to exactly what the crop's stages need, whatever was drawn. A short list would
            // leave the last stages with no animation and a long one would write pictures the plant
            // can never reach — and the stage a plant plays is its own stage number, so the list
            // cannot simply be as long as it happens to be.
            Texture2D[] pictures = new Texture2D[needed];
            int[] counts = new int[needed];
            for (int i = 0; i < needed; i++)
            {
                pictures[i] = i < source.Length ? source[i] : null;
                counts[i] = art.PicturesInRow(i);
            }

            Texture2D shine = look != null && look.ShinePicture != null
                ? look.ShinePicture
                : art.ShinePicture;

            DimensionPlantSpriteAssetResult result = DimensionPlantSpriteAssetUtility.BuildPlant(
                assetName,
                PlantArtSeed(plantName, versionKey),
                pictures,
                counts,
                art.PictureSpeed,
                DimensionPlantSpriteAssetUtility.VanillaGroundLinePixels,
                shine,
                art.PicturesInTheShineRow,
                outputFolder,
                warn);
            return result.HasAsset;
        }

        private static bool BuildOneSeedArt(
            DimensionPlantAsset plant,
            DimensionPlantArtTemplate art,
            DimensionCropVersionLookTemplate look,
            string versionKey,
            string seedName,
            string outputFolder,
            HashSet<string> artFiles,
            Action<string> warn)
        {
            string assetName = SeedArtName(plant.PlantId, versionKey);
            artFiles.Add(assetName);

            bool ownSeed = look != null && look.HasSeedPicture;
            Texture2D seed = ownSeed ? look.SeedPicture : art.SeedPicture;
            Texture2D watered = ownSeed
                ? (look.SeedInWetGroundPicture != null
                    ? look.SeedInWetGroundPicture
                    : art.SeedInWetGroundPicture)
                : art.SeedInWetGroundPicture;

            DimensionPlantSpriteAssetResult result = DimensionPlantSpriteAssetUtility.BuildSeed(
                assetName,
                SeedArtSeed(seedName, versionKey),
                seed,
                watered,
                DimensionPlantSpriteAssetUtility.VanillaGroundLinePixels,
                outputFolder,
                warn);
            return result.HasAsset;
        }

        /// <summary>The seed variation the version at <paramref name="index"/> sits on.</summary>
        /// <remarks>
        /// One-based, so the first authored version lands on variation 1 — the slot Core Keeper's own
        /// golden roll places — and can therefore hand its roll back to the game.
        /// </remarks>
        public static int SeedVariationFor(int index)
        {
            return index + 1;
        }

        /// <summary>The plant variation the version at <paramref name="index"/> sits on.</summary>
        /// <remarks>
        /// Two-based: variation 0 is the growing plant and variation 1 is the already-ripe copy, so
        /// the first version lands on 2, which is again what the game's own golden plants use.
        /// </remarks>
        public static int PlantVariationFor(int index)
        {
            return index + 2;
        }

        /// <summary>The seed variation an ordinary planting lands on.</summary>
        public static int PlainSeedVariationFor(int versionCount)
        {
            return versionCount + 1;
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

        private static void Warn(List<string> warnings, string message)
        {
            if (warnings != null)
            {
                warnings.Add(message);
            }
        }

        /// <summary>
        /// The name a crop's pictures are addressed by, for one version or for the crop itself.
        /// </summary>
        /// <remarks>
        /// Built from the object NAME rather than from the plant id, so two mods that both call a
        /// crop "carrot" cannot land their art on the same address and draw each other's plants.
        /// The separator is one no object name can contain.
        /// </remarks>
        public static string PlantArtSeed(string plantObjectName, string versionKey)
        {
            return (plantObjectName ?? string.Empty) + "#plant#" + (versionKey ?? string.Empty);
        }

        /// <summary>The same, for the seed sitting in the soil.</summary>
        public static string SeedArtSeed(string seedObjectName, string versionKey)
        {
            return (seedObjectName ?? string.Empty) + "#seed#" + (versionKey ?? string.Empty);
        }

        /// <summary>The file a crop's pictures are written under, for one version or the crop.</summary>
        public static string PlantArtName(string plantId, string versionKey)
        {
            return DimensionGeneratedPrefabUtility.SanitizeAuthoredName(plantId, "Plant") + PlantSuffix +
                (string.IsNullOrEmpty(versionKey) ? string.Empty : versionKey);
        }

        /// <summary>The file a seed's picture is written under.</summary>
        public static string SeedArtName(string plantId, string versionKey)
        {
            return DimensionGeneratedPrefabUtility.SanitizeAuthoredName(plantId, "Plant") + SeedSuffix +
                (string.IsNullOrEmpty(versionKey) ? string.Empty : versionKey);
        }

        /// <summary>
        /// The plant in the ground, on one variation.
        /// </summary>
        /// <remarks>
        /// Typed <c>NonObtainable</c> because a growing plant is not a thing a player carries — vanilla
        /// types every <c>…PlantEntity</c> that way, and typing it as an item would put a half-grown
        /// crop in the crafting menus.
        /// </remarks>
        private static void ConfigurePlant(
            GameObject root,
            DimensionPlantAsset plant,
            string plantName,
            string seedName,
            DimensionCropVersionTemplate version,
            int variation,
            int currentStage,
            bool hasVersions,
            DimensionNamingContext naming,
            DimensionPlantGenerationReport report,
            GameObject visual)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = plantName;
            obj.objectType = ObjectType.NonObtainable;
            obj.initialAmount = 1;
            obj.variation = variation;

            // Assigned even when null, which is the case that matters: a crop whose art was removed
            // must stop pointing at a body, or it would be handed a pooled instance still wearing
            // whichever crop used it last and grow disguised as that one.
            obj.graphicalPrefab = visual;

            // Vanilla's PlantAuthoring bakes an ObjectID that cannot be resolved while this runs, and
            // its converter hardcodes the harvest count to one. The framework twin carries the name
            // and the count, and the two components must not both be present or which of them wrote
            // the entity's PlantCD would come down to converter registration order.
            RemoveComponentIfPresent<PlantAuthoring>(root);

            DimensionPlantProduceAuthoring produce =
                EnsureComponent<DimensionPlantProduceAuthoring>(root);
            produce.growingSettings = BuildGrowingSettings(plant, currentStage);
            produce.produceName = ResolveReference(
                version != null && !string.IsNullOrEmpty(version.ProduceItemId)
                    ? version.ProduceItemId
                    : plant.ProduceItemId,
                naming);
            produce.numberOfPlantsToDrop = version != null && version.HarvestAmount > 0
                ? version.HarvestAmount
                : plant.HarvestAmount;

            WarnAboutReference(plant, produce.produceName, "gives when picked", report);
            ConfigureGiveBacks(root, plant, seedName, version, naming, report);

            if (hasVersions)
            {
                EnsureComponent<DimensionCropTierPlantAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<DimensionCropTierPlantAuthoring>(root);
            }

            // The state machine: without these a plant never registers being hit and cannot be
            // removed, which is the same trap containers and creatures hit.
            DimensionObjectSpine.ApplyDamageableStates(root);
            EnsureComponent<MineableAuthoring>(root);
            EnsureComponent<PlaceableObjectAuthoring>(root);
            DimensionObjectSpine.ApplyPlacementRules(root, plant.PlacementRules, null);

            // What every vanilla plant carries and ours did not. Health is what makes it removable
            // at all; Diggable is what lets a shovel lift it rather than only a tool that mines.
            DimensionObjectSpine.ApplyUniversal(root, false);

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                plant.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + plant.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyAreaLevel(root, true);
            EnsureComponent<DiggableAuthoring>(root);
            EnsureComponent<Pug.Automation.AutomatedHarvestablePlantAuthoring>(root);

            // THREE HITS WHILE IT GROWS, ONE WHEN IT IS RIPE. That is the game's own shape and the
            // only shape in which "stays tough when ripe" means anything: the growing crop caps
            // what one blow can take off it, and ripening lifts the cap so a ripe crop harvests in
            // a single swing. The cap lives in DamageReductionCD, and the pass that lifts it does
            // nothing at all when the plant has no such component — so the answer the author ticked
            // was inert, and an unripe crop died to any stray swing on the way past.
            // CarrockPlantEntity is three health and one damage per hit.
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = true;
            health.maxHealth = 3;
            health.startHealth = 3;
            health.maxHealthMultiplier = 1f;

            DamageReductionAuthoring toughness = EnsureComponent<DamageReductionAuthoring>(root);
            toughness.calculateReductionFromLevel = false;
            toughness.reductionMultiplier = 1f;
            toughness.reduction = 0;
            toughness.maxDamagePerHit = 1;
            toughness.minDamagePerHit = 0;

            if (plant.WashedAwayByWater)
            {
                EnsureComponent<CanBeRemovedByWaterAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CanBeRemovedByWaterAuthoring>(root);
            }

            ApplySpreading(root, plant, report);
        }

        /// <summary>
        /// What picking the plant hands back besides the produce: its seed, and a version's extras.
        /// </summary>
        /// <remarks>
        /// ONE CHANCE COVERS THE WHOLE LIST because Core Keeper stores exactly one
        /// (<c>ChanceToDropLootCD</c>) per object and rolls it separately for each entry. So a version
        /// that always hands over a bonus item also always hands back its seed. That is worth knowing
        /// rather than working around, and the number is authored in one place per version.
        /// </remarks>
        private static void ConfigureGiveBacks(
            GameObject root,
            DimensionPlantAsset plant,
            string seedName,
            DimensionCropVersionTemplate version,
            DimensionNamingContext naming,
            DimensionPlantGenerationReport report)
        {
            List<string> names = new List<string>();
            List<int> amounts = new List<int>();

            if (plant.IsPlanted && !string.IsNullOrEmpty(seedName))
            {
                names.Add(seedName);
                amounts.Add(1);
            }

            if (version != null)
            {
                DimensionCropVersionDropTemplate[] extras = version.ExtraDrops;
                for (int i = 0; i < extras.Length; i++)
                {
                    if (extras[i] == null || extras[i].IsEmpty)
                    {
                        continue;
                    }

                    string extraName = ResolveReference(extras[i].ItemId, naming);
                    WarnAboutReference(plant, extraName, "gives as an extra", report);
                    names.Add(extraName);
                    amounts.Add(extras[i].Amount);
                }
            }

            if (names.Count == 0)
            {
                RemoveComponentIfPresent<DimensionPlantDropsAuthoring>(root);
                return;
            }

            float percent = version != null && version.ChanceToGetThingsBackPercent > 0f
                ? version.ChanceToGetThingsBackPercent
                : plant.ChanceToGetTheSeedBackPercent;

            DimensionPlantDropsAuthoring drops = EnsureComponent<DimensionPlantDropsAuthoring>(root);
            drops.itemNames = names.ToArray();
            drops.amounts = amounts.ToArray();
            drops.chance = Mathf.Clamp01(percent / 100f);
        }

        /// <summary>
        /// The seed a player plants, on one variation.
        /// </summary>
        /// <remarks>
        /// <c>turnsIntoPlantName</c> is the whole point of the seed, and it is the name this generator
        /// gave the plant a moment ago rather than anything the author typed — so the two can never
        /// drift apart, and the id behind it is looked up in the running game where it exists.
        /// </remarks>
        private static void ConfigureSeed(
            GameObject root,
            DimensionPlantAsset plant,
            string plantName,
            string seedName,
            int variation,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report,
            GameObject visual)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = seedName;
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;
            obj.variation = variation;

            // The seed's body, which is what a player looks at for the whole first stage. Null when
            // nothing was drawn, for the same pooling reason as the plant's.
            obj.graphicalPrefab = visual;

            Rarity rarity;
            if (!string.IsNullOrEmpty(plant.RarityId) && Enum.TryParse(plant.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            // The seed is the half of a crop a player carries, so it is the half with an icon.
            InventoryItemAuthoring inventory = EnsureComponent<InventoryItemAuthoring>(root);
            inventory.isStackable = true;

            // Never left null: ObjectAuthoring.ObjectInfo walks this list with a foreach the moment
            // anything asks the prefab what object it is, and a null there throws inside conversion
            // rather than reporting a missing recipe.
            if (inventory.requiredObjectsToCraft == null)
            {
                inventory.requiredObjectsToCraft = new List<InventoryItemAuthoring.CraftingObject>();
            }

            if (plant.SeedIcon != null)
            {
                inventory.icon = plant.SeedIcon;
                inventory.smallIcon = plant.SeedIcon;
            }

            RemoveComponentIfPresent<SeedAuthoring>(root);

            GiveItEverythingAVanillaSeedHas(root);

            DimensionSeedAuthoring seed = EnsureComponent<DimensionSeedAuthoring>(root);
            seed.growingSettings = BuildGrowingSettings(plant, 0);
            seed.turnsIntoPlantName = plantName;

            // The game's own golden roll only fires when this is above zero, and it only ever places
            // this one variation. Handing it the first version's slot is what keeps a plain golden
            // crop behaving exactly as it does in vanilla, gardening talents included.
            bool gameRollsFirstVersion =
                versions.Length > 0 && versions[0].UsesTheGamesGoldenChance;
            seed.rareSeedVariation = gameRollsFirstVersion ? DimensionPlantAsset.RareSeedVariation : 0;
            seed.rarePlantVariation = gameRollsFirstVersion ? DimensionPlantAsset.RarePlantVariation : 0;

            if (versions.Length == 0)
            {
                RemoveComponentIfPresent<DimensionCropTierSeedAuthoring>(root);
                return;
            }

            DimensionCropTierSeedAuthoring tiers = EnsureComponent<DimensionCropTierSeedAuthoring>(root);
            tiers.seedVariations = new int[versions.Length];
            tiers.plantVariations = new int[versions.Length];
            tiers.chancePercents = new float[versions.Length];
            tiers.usesTheGamesGoldenRoll = new bool[versions.Length];
            tiers.plainSeedVariation = PlainSeedVariationFor(versions.Length);

            for (int i = 0; i < versions.Length; i++)
            {
                tiers.seedVariations[i] = SeedVariationFor(i);
                tiers.plantVariations[i] = PlantVariationFor(i);
                tiers.chancePercents[i] = versions[i].ChancePercent;

                // Only the first version can be left to the game: it has one golden roll and one
                // golden variation, so a later version claiming the same thing would simply never be
                // rolled by anybody. Reported in WarnAboutPlant; taken over here.
                tiers.usesTheGamesGoldenRoll[i] = i == 0 && versions[i].UsesTheGamesGoldenChance;
            }
        }

        /// <summary>
        /// The tilled soils the game has, which is where a seed may be planted.
        /// </summary>
        /// <remarks>
        /// Ten grounds, each with a dug-up and a watered form, and the game's own crop seeds list
        /// exactly the two belonging to their own tileset. A mod's crop has no tileset of its own
        /// to belong to, so it takes all twenty and is plantable in any soil a player has hoed.
        /// A tilled soil that a MOD invented is not in this list; a seed cannot yet be restricted
        /// to one, or extended to one.
        /// </remarks>
        private static readonly ObjectID[] EverySoilACropCanBePlantedIn =
        {
            ObjectID.DugUpGround, ObjectID.WateredGround,
            ObjectID.DugUpStoneGround, ObjectID.WateredStoneGround,
            ObjectID.DugUpClayGround, ObjectID.WateredClayGround,
            ObjectID.DugUpCrystalGround, ObjectID.WateredCrystalGround,
            ObjectID.DugUpMeadowGround, ObjectID.WateredMeadowGround,
            ObjectID.DugUpMoldGround, ObjectID.WateredMoldGround,
            ObjectID.DugUpNatureGround, ObjectID.WateredNatureGround,
            ObjectID.DugUpOasisGround, ObjectID.WateredOasisGround,
            ObjectID.DugUpSeaGround, ObjectID.WateredSeaGround,
            ObjectID.DugUpTurfGround, ObjectID.WateredTurfGround
        };

        /// <summary>
        /// The five answers every one of the game's own crop seeds carries and ours carried none of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHERE IT MAY BE PLANTED was the worst of them. With no placement answer at all, the
        /// game falls back to "anywhere a player can walk", so a modded seed went into grass, into
        /// floor, onto a bridge and onto a rug. It then never grew, because the timer that advances
        /// a crop only ticks while the tile underneath is watered ground — so the crop sat at stage
        /// one forever with nothing in the log, and the author concluded the framework was broken.
        /// The game's own seeds cannot be planted anywhere but soil, which is what makes their
        /// growth reliable rather than what makes it restricted.
        /// </para>
        /// <para>
        /// DIGGING IT BACK UP is the second. The game only lets a placed thing drop itself when it
        /// is diggable or mineable, and the whole loot machinery — the start and finished markers,
        /// the two "do not drop" flags — is only attached to something that already is. A seed
        /// with none of that is permanent scenery: a shovel does nothing, a sword does nothing, and
        /// nothing says why. The game's own seeds are diggable and cannot be attacked.
        /// </para>
        /// <para>
        /// AUTOMATION is the third and fourth. A planter looks for the seed category tag and for
        /// the automation component, and refuses anything missing either — while the framework
        /// already wrote the harvest half on the plant, which is what made the omission look like
        /// an oversight rather than a decision.
        /// </para>
        /// </remarks>
        private static void GiveItEverythingAVanillaSeedHas(GameObject root)
        {
            PlaceableObjectAuthoring placement = EnsureComponent<PlaceableObjectAuthoring>(root);
            placement.canBePlacedOnAnyWalkableTile = false;
            placement.prefabTileSize = Vector2Int.one;
            placement.canBePlacedOnObjects = new List<ObjectID>(EverySoilACropCanBePlantedIn);

            EnsureComponent<DiggableAuthoring>(root);
            EnsureComponent<CantBeAttackedAuthoring>(root);

            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.Seed, true);
            EnsureComponent<Pug.Automation.AutomatedPlantableSeedAuthoring>(root);
        }

        /// <summary>
        /// A plant that spreads itself instead of being planted.
        /// </summary>
        private static void ApplySpreading(
            GameObject root,
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report)
        {
            if (!plant.SpreadsOnItsOwn)
            {
                RemoveComponentIfPresent<RootPlantAuthoring>(root);
                return;
            }

            RootPlantAuthoring rootPlant = EnsureRootPlant(root);
            rootPlant.minTimeBetweenSpread = plant.MinSpreadSeconds;
            rootPlant.maxTimeBetweenSpread = plant.MaxSpreadSeconds;
            rootPlant.canGrowOnTilesets = new List<Tileset>();

            string[] tilesets = plant.SpreadsOnTilesetIds;
            for (int i = 0; i < tilesets.Length; i++)
            {
                Tileset tileset;
                if (Enum.TryParse(tilesets[i], false, out tileset))
                {
                    rootPlant.canGrowOnTilesets.Add(tileset);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' spreads onto '" + tilesets[i] +
                        "', which is not a tileset the game has. It will not spread there.");
                }
            }

            // Its own tileset — the tiles it LAYS DOWN as it spreads, as opposed to the list above
            // of tilesets it may spread onto. Defaults to the first one it can grow on, because a
            // root that creeps across stone and leaves dirt behind reads as a bug.
            Tileset becomes;
            if (!string.IsNullOrEmpty(plant.BecomesTilesetId) &&
                Enum.TryParse(plant.BecomesTilesetId, false, out becomes))
            {
                rootPlant.tileset = becomes;
            }
            else if (!string.IsNullOrEmpty(plant.BecomesTilesetId))
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' lays down '" + plant.BecomesTilesetId +
                    "' as it spreads, which is not a tileset the game has.");
            }
            else if (rootPlant.canGrowOnTilesets.Count > 0)
            {
                rootPlant.tileset = rootPlant.canGrowOnTilesets[0];
            }
        }

        /// <summary>
        /// The growing curve, shared by the seed and every plant prefab.
        /// </summary>
        /// <remarks>
        /// Built in one place so the seed and the plant cannot disagree about how long growing takes —
        /// vanilla authors the settings on both, and a mismatch there would show up as a plant that
        /// changes pace the moment it is planted.
        /// </remarks>
        private static GrowingSettings BuildGrowingSettings(DimensionPlantAsset plant, int currentStage)
        {
            return new GrowingSettings
            {
                highestStage = plant.GrowthStages,
                timeBetweenStages = plant.SecondsBetweenStages,
                currentStage = currentStage,
                keepDamageReductionWhenRipe = plant.StaysToughWhenRipe
            };
        }

        /// <summary>
        /// Names every crop version that is turned off, because the first one anybody adds is
        /// turned off without their ever having said so.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE TRAP. <c>DimensionCropVersionTemplate.enabled</c> starts as <c>true</c> in C#, but
        /// Unity does not run field initialisers for a row added to an EMPTY list — it fills the
        /// new row with zeroes. So the very first version anybody adds arrives turned off, and
        /// <c>EnabledVersions</c> drops it: the author writes a golden variant, draws it, gives it
        /// a chance, generates, and nothing about it reaches the game. It is the same zero-fill
        /// that made the first version's colour wash a multiply-by-black.
        /// </para>
        /// <para>
        /// A WARNING RATHER THAN A QUIET REPAIR, deliberately. Turning a version off is a real
        /// thing to want — it is how you set one aside without deleting it — and no rule can tell
        /// a row that was zero-filled from one that was switched off on purpose once the author has
        /// begun filling it in. The honest fix for the field itself is to store "turned off"
        /// instead of "turned on", so a zero-filled row means the same as an untouched one; that is
        /// a change to what is already saved in every project, so it is written up rather than made
        /// here. Until then, nothing is silent.
        /// </para>
        /// </remarks>
        private static void WarnAboutVersionsThatAreTurnedOff(
            DimensionPlantAsset plant,
            DimensionPlantGenerationReport report)
        {
            DimensionCropVersionTemplate[] all = plant.Versions;
            if (all == null)
            {
                return;
            }

            for (int i = 0; i < all.Length; i++)
            {
                DimensionCropVersionTemplate version = all[i];
                if (version == null || version.Enabled)
                {
                    continue;
                }

                string name = string.IsNullOrEmpty(version.VersionName)
                    ? "version " + (i + 1)
                    : "'" + version.VersionName + "'";
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has a crop version, " + name + ", that is turned " +
                    "off, so nothing about it reaches the game. If you did not turn it off, tick " +
                    "its Enabled box: a version added to an empty list starts unticked whatever " +
                    "the field says it defaults to.");
            }
        }

        private static void WarnAboutPlant(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report)
        {
            if (plant.HarvestGivesNothing)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has nothing to harvest, so it will grow and then " +
                    "give the player nothing when picked. Name an item under Produce item.");
            }

            if (plant.SpreadsNowhere)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' spreads on its own but names no ground to spread " +
                    "onto, so it will never spread anywhere. Add a tileset it may spread across.");
            }

            if (plant.IsReadyImmediately && plant.IsPlanted)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' takes no time to grow, so it is harvestable the " +
                    "instant it is planted. Give it some minutes to grow.");
            }

            if (!plant.IsPlanted && plant.Versions.Length > 0)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' spreads on its own, so nothing ever plants it and " +
                    "its better versions can never come up. Set it to be planted, or remove them.");
            }

            WarnAboutVersionsThatAreTurnedOff(plant, report);

            float running = 0f;
            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionTemplate version = versions[i];
                if (i > 0 && version.UsesTheGamesGoldenChance)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' asks the game to roll '" + version.VersionName +
                        "', but the game only has one golden roll and the first version already has " +
                        "it. Give '" + version.VersionName + "' a chance of its own instead.");
                }

                if (version.NeverComesUp)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' has a version called '" + version.VersionName +
                        "' with no chance of coming up. Give it a chance above zero, or turn it off.");
                }

                bool ours = !(i == 0 && version.UsesTheGamesGoldenChance);
                if (!ours || version.ChancePercent <= 0f)
                {
                    continue;
                }

                if (running >= 100f)
                {
                    report.Warnings.Add(
                        "'" + plant.DisplayName + "' has versions above it adding up to a hundred " +
                        "already, so '" + version.VersionName + "' can never come up. Lower the " +
                        "chances of the versions listed before it.");
                }

                running += version.ChancePercent;
            }

            if (running > 100f)
            {
                report.Warnings.Add(
                    "'" + plant.DisplayName + "' has better versions adding up to " +
                    running.ToString("0.#") + " in a hundred, which is more plantings than there " +
                    "are. Lower them until they add up to a hundred or less.");
            }
        }

        /// <summary>
        /// Everything about a crop's pictures that will fail quietly at runtime.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE COUNT IS THE ONE THAT COSTS PEOPLE AN EVENING. Core Keeper counts a plant's stages
        /// from zero up to and including its top stage, so a crop with two growth stages shows
        /// three pictures. This is the same off-by-one that <c>ObjectInfo.additionalSprites</c> has
        /// to satisfy on every vanilla plant prefab — one entry per stage, ripe included — and a
        /// list one short leaves a ripe crop showing its half-grown picture with no error anywhere.
        /// </para>
        /// <para>
        /// WHAT IS DELIBERATELY NOT WARNED ABOUT: having more than three growth stages. Core
        /// Keeper's own plant renderer keeps a fixed array of four animations and plays number
        /// <c>stage + 1</c>, so a fourth look reads off the end of it — but that renderer is not
        /// the one a generated crop uses. <see cref="DimensionPlantView"/> indexes a list that is
        /// exactly as long as the crop has stages, so the limit is genuinely not there and a
        /// warning about it would be a warning about somebody else's code.
        /// </para>
        /// <para>
        /// A ROW THAT DOES NOT DIVIDE is the other silent one. The game slices a row by width
        /// alone, so a strip of three pictures 50 pixels wide plays sixteen-and-two-thirds pixels
        /// of each and drifts sideways as it goes.
        /// </para>
        /// </remarks>
        private static void WarnAboutArt(
            DimensionPlantAsset plant,
            DimensionCropVersionTemplate[] versions,
            DimensionPlantGenerationReport report)
        {
            DimensionPlantArtTemplate art = plant.Art;
            string who = "'" + plant.DisplayName + "' ";
            int needed = plant.PicturesNeeded;

            if (!art.HasPlantPictures)
            {
                report.Warnings.Add(
                    who + "has nothing drawn for it, so it will be invisible in the ground the " +
                    "whole time it grows. Give it " + needed + " pictures under Look, one for each " +
                    "stage from just sprouted to ripe.");
            }
            else
            {
                WarnAboutPictureList(
                    who, "grows through " + plant.GrowthStages + " stages",
                    art.GrowthPictures, needed, art, report);
            }

            if (art.ShinePicture != null &&
                art.ShinePicture.width % art.PicturesInTheShineRow != 0)
            {
                report.Warnings.Add(
                    who + "has a twinkle " + art.ShinePicture.width + " pixels wide, which does " +
                    "not divide into " + art.PicturesInTheShineRow + " pictures. Every picture in " +
                    "a row has to be the same width, so trim it or change the count.");
            }

            if (plant.IsPlanted && !art.HasSeedPicture)
            {
                report.Warnings.Add(
                    who + "has no picture for its seed, so a player will plant it and see bare " +
                    "soil until it sprouts. Draw the seed in the ground under Look.");
            }

            if (art.SeedInWetGroundPicture != null && !art.HasSeedPicture)
            {
                report.Warnings.Add(
                    who + "has a watered seed picture but no dry one. The dry picture is the one " +
                    "the game asks for by default, so nothing will be drawn either way.");
            }

            if (!string.IsNullOrEmpty(art.RipePuffId))
            {
                PuffID puff;
                if (!Enum.TryParse(art.RipePuffId, false, out puff))
                {
                    report.Warnings.Add(
                        who + "throws up '" + art.RipePuffId + "' when it ripens, which is not " +
                        "one of the game's own bursts. It will throw up leaves instead.");
                }
            }

            // The puff had this check and the sound beside it did not, so a mistyped ripening
            // sound generated cleanly and ripened in silence.
            DimensionSoundNames.WarnIfUnknown(
                art.RipeSoundId,
                "'" + plant.DisplayName + "'",
                message => report.Warnings.Add(message));

            for (int i = 0; i < versions.Length; i++)
            {
                DimensionCropVersionTemplate version = versions[i];
                DimensionCropVersionLookTemplate look = version.Look;
                string versionWho = who + "version '" + version.VersionName + "' ";
                if (!look.LooksDifferent)
                {
                    report.Warnings.Add(
                        versionWho + "draws nothing of its own, so it will look exactly like the " +
                        "ordinary crop and nobody will be able to tell they found one. Give it " +
                        "its own pictures, or at least a colour wash.");
                    continue;
                }

                if (look.HasPlantPictures)
                {
                    WarnAboutPictureList(
                        versionWho, "has to match the ordinary crop", look.GrowthPictures, needed,
                        art, report);
                }
            }
        }

        /// <summary>The checks a list of stage pictures has to pass, wherever it came from.</summary>
        private static void WarnAboutPictureList(
            string who,
            string why,
            Texture2D[] pictures,
            int needed,
            DimensionPlantArtTemplate art,
            DimensionPlantGenerationReport report)
        {
            if (pictures.Length != needed)
            {
                report.Warnings.Add(
                    who + why + ", so it needs " + needed + " pictures — one for each stage, and " +
                    "the last one is the ripe plant. It has " + pictures.Length + ". " +
                    (pictures.Length < needed
                        ? "The stages past the end will show nothing."
                        : "The extra ones will never be shown."));
            }

            for (int i = 0; i < pictures.Length && i < needed; i++)
            {
                if (pictures[i] == null)
                {
                    report.Warnings.Add(
                        who + "has no picture for stage " + (i + 1) + " of " + needed +
                        ", so the plant will be invisible for that whole stage.");
                    continue;
                }

                int count = art.PicturesInRow(i);
                if (pictures[i].width % count != 0)
                {
                    report.Warnings.Add(
                        who + "has a stage " + (i + 1) + " picture " + pictures[i].width +
                        " pixels wide, which does not divide into " + count + " pictures. Every " +
                        "picture in a row has to be the same width, so trim it or change the count.");
                }
            }
        }

        /// <summary>
        /// Takes away sprite assets and pictures this generator made for a crop and no longer wants.
        /// </summary>
        /// <remarks>
        /// Renaming a version leaves its art behind exactly as it leaves its prefab behind, and art
        /// nobody references is still listed in the mod's sprite manifest and still compiled into
        /// the game's atlas — for ever, growing with every rename. Deleting the asset is enough to
        /// clear the manifest too: the entry becomes null and the next generation drops it.
        /// </remarks>
        private static void RemoveStaleArt(
            string outputFolder,
            Dictionary<string, HashSet<string>> expected,
            HashSet<string> artFiles,
            DimensionPlantGenerationReport report)
        {
            string artFolder = outputFolder + "/" + DimensionPlantSpriteAssetUtility.ArtFolderName;
            if (expected.Count == 0 || !AssetDatabase.IsValidFolder(artFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { artFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string stem = StemOf(path);

                // A picture is named after the asset it belongs to plus the animation it is, so the
                // asset's own name is the part before the first underscore that follows it. Rather
                // than parse that, a file is kept when any wanted asset name is a prefix of it.
                if (IsWantedArt(stem, artFiles))
                {
                    continue;
                }

                bool ours = false;
                foreach (KeyValuePair<string, HashSet<string>> entry in expected)
                {
                    if (IsGeneratedNameFor(stem, entry.Key))
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

        private static bool IsWantedArt(string stem, HashSet<string> artFiles)
        {
            foreach (string wanted in artFiles)
            {
                if (string.Equals(stem, wanted, StringComparison.Ordinal) ||
                    stem.StartsWith(wanted + "_", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// Says so when a reference is neither a game item nor spelled like a mod item.
        /// </summary>
        /// <remarks>
        /// The one mistake that can still be caught from here. A mod's own items carry the mod's name
        /// in front of them, so a bare word that is not one of the game's own ObjectIDs resolves to
        /// nothing when the world loads — silently, since there is no id to be wrong about.
        /// </remarks>
        private static void WarnAboutReference(
            DimensionPlantAsset plant,
            string itemId,
            string what,
            DimensionPlantGenerationReport report)
        {
            if (string.IsNullOrEmpty(itemId) || itemId.IndexOf(':') >= 0)
            {
                return;
            }

            if (DimensionObjectBinder.Vanilla(itemId) != ObjectID.None)
            {
                return;
            }

            report.Warnings.Add(
                "'" + plant.DisplayName + "' " + what + " '" + itemId + "', which is not one of the " +
                "game's own items. If it is one of yours, write it with your mod in front of it, " +
                "like 'MyMod:" + itemId + "'. As it is, it will give nothing.");
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
