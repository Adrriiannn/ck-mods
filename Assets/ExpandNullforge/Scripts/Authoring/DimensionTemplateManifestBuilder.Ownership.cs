using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Recording which content pack owns each authored thing.
    /// </summary>
    public static partial class DimensionTemplateManifestBuilder
    {
        private static void AddAuthoredContentOwnership(
            DimensionTemplateAsset template,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (template == null)
            {
                return;
            }

            AddItemOwnership(template.GlobalItems, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddRecipeOwnership(template.GlobalRecipes, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddWorkbenchOwnership(template.GlobalWorkbenches, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddLootTableOwnership(template.GlobalLootTables, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddAnimalOwnership(template.GlobalAnimals, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddCritterOwnership(template.GlobalCritters, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddMobOwnership(template.GlobalMobs, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddBossOwnership(template.GlobalBosses, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            AddSceneContentOwnership(template.GlobalScenes, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);

            BiomeTemplateAsset[] biomeAssets = template.Biomes;
            for (int i = 0; i < biomeAssets.Length; i++)
            {
                BiomeTemplateAsset biome = biomeAssets[i];
                if (biome != null)
                {
                    AddSceneContentOwnership(biome.ScenePool, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddItemOwnership(
            DimensionItemAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Item, asset.ItemId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddRecipeOwnership(
            DimensionRecipeAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionRecipeAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Recipe, asset.RecipeId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddWorkbenchOwnership(
            DimensionWorkbenchAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Workbench, asset.WorkbenchId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddLootTableOwnership(
            DimensionLootTableAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (assets == null)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                DimensionLootTableAsset asset = assets[i];
                if (asset != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.LootTable, asset.LootTableId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddAnimalOwnership(
            DimensionAnimalAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Animal, asset.AnimalId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddCritterOwnership(
            DimensionCritterAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Critter, asset.CritterId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddMobOwnership(
            DimensionMobAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Mob, asset.MobId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddBossOwnership(
            DimensionBossAsset[] assets,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
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
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.Boss, asset.BossId, asset.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddSceneContentOwnership(
            SceneTemplateAsset[] scenes,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                AddSceneTriggerOwnership(scene, contentPackId, hasContentPack, ownershipBindings, ownershipKeys);
            }
        }

        private static void AddSceneTriggerOwnership(
            SceneTemplateAsset scene,
            string contentPackId,
            bool hasContentPack,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            DimensionSceneTriggerTemplate[] triggers = scene.Triggers;
            for (int i = 0; i < triggers.Length; i++)
            {
                DimensionSceneTriggerTemplate trigger = triggers[i];
                if (trigger != null)
                {
                    AddOwnership(hasContentPack, contentPackId, DimensionContentRecordKind.SceneTrigger, BuildSceneContentRecordId(scene.SceneId, "trigger", trigger.TriggerId, i), trigger.DisplayName, ownershipBindings, ownershipKeys);
                }
            }
        }

        private static void AddGenerationPassOwnership(
            string contentPackId,
            bool hasContentPack,
            List<DimensionGenerationPassDefinition> generationPasses,
            List<DimensionContentOwnershipBinding> ownershipBindings,
            Dictionary<string, bool> ownershipKeys)
        {
            for (int i = 0; i < generationPasses.Count; i++)
            {
                DimensionGenerationPassDefinition generationPass = generationPasses[i];
                AddOwnership(
                    hasContentPack,
                    contentPackId,
                    DimensionContentRecordKind.GenerationPass,
                    generationPass.PassId,
                    generationPass.DisplayName,
                    ownershipBindings,
                    ownershipKeys);
            }
        }
    }
}
