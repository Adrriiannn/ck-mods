using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Recording the assets the runtime has to resolve later by name.
    /// </summary>
    public static partial class DimensionTemplateManifestBuilder
    {
        private static void AddAuthoredContentAssetReferences(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionAssetReferenceDefinition> assetReferences,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (!hasContentPack || template == null)
            {
                return;
            }

            Dictionary<string, bool> assetReferenceIds = new Dictionary<string, bool>();
            AddExistingAssetReferenceIds(assetReferences, assetReferenceIds);
            List<DimensionAssetReferenceDefinition> references =
                new List<DimensionAssetReferenceDefinition>();

            AddSceneAssetReferences(
                template.GlobalScenes,
                template.DimensionId,
                string.Empty,
                contentPackId,
                references);
            AddItemAssetReferences(template.GlobalItems, template.DimensionId, contentPackId, references);
            AddWorkbenchAssetReferences(template.GlobalWorkbenches, template.DimensionId, contentPackId, references);
            AddAnimalAssetReferences(template.GlobalAnimals, template.DimensionId, contentPackId, references);
            AddCritterAssetReferences(template.GlobalCritters, template.DimensionId, contentPackId, references);
            AddMobAssetReferences(template.GlobalMobs, template.DimensionId, contentPackId, references);
            AddBossAssetReferences(template.GlobalBosses, template.DimensionId, contentPackId, references);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome != null)
                {
                    AddSceneAssetReferences(
                        biome.ScenePool,
                        template.DimensionId,
                        biome.BiomeId,
                        contentPackId,
                        references);
                }
            }

            for (int i = 0; i < references.Count; i++)
            {
                DimensionAssetReferenceDefinition reference = references[i];
                if (!AddUnique(assetReferenceIds, reference.AssetId))
                {
                    continue;
                }

                assetReferences.Add(reference);
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.AssetReference,
                    reference.AssetId,
                    reference.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }

        private static void AddExistingAssetReferenceIds(
            List<DimensionAssetReferenceDefinition> assetReferences,
            Dictionary<string, bool> assetReferenceIds)
        {
            if (assetReferences == null || assetReferenceIds == null)
            {
                return;
            }

            for (int i = 0; i < assetReferences.Count; i++)
            {
                string assetId = assetReferences[i].AssetId;
                if (!string.IsNullOrEmpty(assetId) && !assetReferenceIds.ContainsKey(assetId))
                {
                    assetReferenceIds.Add(assetId, true);
                }
            }
        }

        private static void AddSceneAssetReferences(
            SceneTemplateAsset[] scenes,
            string dimensionId,
            string zoneId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    scene.AddAssetReferencesTo(contentPackId, dimensionId, zoneId, references);
                }
            }
        }

        private static void AddItemAssetReferences(
            DimensionItemAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionItemAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddWorkbenchAssetReferences(
            DimensionWorkbenchAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionWorkbenchAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddAnimalAssetReferences(
            DimensionAnimalAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionAnimalAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddCritterAssetReferences(
            DimensionCritterAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionCritterAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddMobAssetReferences(
            DimensionMobAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionMobAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }

        private static void AddBossAssetReferences(
            DimensionBossAsset[] assets,
            string dimensionId,
            string contentPackId,
            List<DimensionAssetReferenceDefinition> references)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionBossAsset asset = assets[i];
                if (asset != null)
                {
                    asset.AddAssetReferencesTo(contentPackId, dimensionId, string.Empty, references);
                }
            }
        }
    }
}
