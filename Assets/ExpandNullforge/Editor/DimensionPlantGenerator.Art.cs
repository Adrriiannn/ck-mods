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
    /// The pictures a crop needs at each stage, and clearing away the ones it no longer does.
    /// </summary>
    internal static partial class DimensionPlantGenerator
    {
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
    }
}
