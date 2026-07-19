using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Biome Content Preset")]
    public sealed class BiomeContentPresetAsset : ScriptableObject
    {
        [SerializeField] private string presetId = "biome-content-preset";
        [SerializeField] private string displayName = "Biome Content Preset";
        [SerializeField] private bool enabled = true;
        [SerializeField] private string environmentProfileId = string.Empty;
        [SerializeField] private EnvironmentProfileTemplateAsset environmentProfileTemplate;
        [SerializeField] private string paletteAssetId = string.Empty;
        [SerializeField] private BiomePaletteTemplateAsset paletteTemplate;
        [SerializeField] private BiomeGenerationProfileAsset generationProfile;
        [SerializeField] private GenerationPassTemplateAsset[] generationPasses = new GenerationPassTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] generationTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private SceneTemplateAsset[] scenePool = new SceneTemplateAsset[0];
        [SerializeField] private ResourceNodeTemplateAsset[] resourceNodes = new ResourceNodeTemplateAsset[0];
        [SerializeField] private SpawnRuleTemplateAsset[] spawnRules = new SpawnRuleTemplateAsset[0];
        [SerializeField] private string[] floorObjectIds = new string[0];
        [SerializeField] private string[] wallObjectIds = new string[0];
        [SerializeField] private string[] oreObjectIds = new string[0];
        [SerializeField] private string[] waterObjectIds = new string[0];
        [SerializeField] private string notes = string.Empty;

        private void OnValidate()
        {
            generationPasses = CopyObjects(generationPasses);
            generationTables = CopyObjects(generationTables);
            scenePool = CopyObjects(scenePool);
            resourceNodes = CopyObjects(resourceNodes);
            spawnRules = CopyObjects(spawnRules);
        }

        public string PresetId
        {
            get { return presetId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string EnvironmentProfileId
        {
            get { return environmentProfileId ?? string.Empty; }
        }

        public EnvironmentProfileTemplateAsset EnvironmentProfileTemplate
        {
            get { return environmentProfileTemplate; }
        }

        public string ResolvedEnvironmentProfileId
        {
            get
            {
                if (!string.IsNullOrEmpty(EnvironmentProfileId))
                {
                    return EnvironmentProfileId;
                }

                if (environmentProfileTemplate != null && environmentProfileTemplate.Enabled)
                {
                    return environmentProfileTemplate.ProfileId;
                }

                return string.Empty;
            }
        }

        public string PaletteAssetId
        {
            get { return paletteAssetId ?? string.Empty; }
        }

        public BiomePaletteTemplateAsset PaletteTemplate
        {
            get { return paletteTemplate; }
        }

        public BiomeGenerationProfileAsset GenerationProfile
        {
            get { return generationProfile; }
        }

        public string ResolvedPaletteAssetId
        {
            get
            {
                if (!string.IsNullOrEmpty(PaletteAssetId))
                {
                    return PaletteAssetId;
                }

                if (paletteTemplate != null && paletteTemplate.Enabled)
                {
                    return paletteTemplate.PaletteId;
                }

                return string.Empty;
            }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public bool HasAnyContent
        {
            get
            {
                return !string.IsNullOrEmpty(ResolvedEnvironmentProfileId) ||
                    !string.IsNullOrEmpty(ResolvedPaletteAssetId) ||
                    generationProfile != null ||
                    HasAny(generationPasses) ||
                    HasAny(generationTables) ||
                    HasAny(scenePool) ||
                    HasAny(resourceNodes) ||
                    HasAny(spawnRules) ||
                    HasAny(floorObjectIds) ||
                    HasAny(wallObjectIds) ||
                    HasAny(oreObjectIds) ||
                    HasAny(waterObjectIds);
            }
        }

        public void ConfigureIdentity(
            string newPresetId,
            string newDisplayName,
            bool newEnabled)
        {
            presetId = newPresetId ?? string.Empty;
            displayName = string.IsNullOrEmpty(newDisplayName)
                ? presetId
                : newDisplayName;
            enabled = newEnabled;
        }

        public void ConfigureEnvironmentProfile(
            string profileId,
            EnvironmentProfileTemplateAsset template)
        {
            environmentProfileId = profileId ?? string.Empty;
            environmentProfileTemplate = template;
        }

        public void ConfigurePalette(
            string newPaletteAssetId,
            BiomePaletteTemplateAsset template)
        {
            paletteAssetId = newPaletteAssetId ?? string.Empty;
            paletteTemplate = template;
        }

        public void ConfigureGenerationProfile(BiomeGenerationProfileAsset profile)
        {
            generationProfile = profile;
        }

        public void ApplySemanticTerrainPreset(
            string[] floorIds,
            string[] wallIds,
            string[] oreIds,
            string[] waterIds,
            bool replaceExisting)
        {
            floorObjectIds = replaceExisting
                ? CopyNonEmptyStrings(floorIds)
                : MergeStringArrays(floorObjectIds, floorIds);
            wallObjectIds = replaceExisting
                ? CopyNonEmptyStrings(wallIds)
                : MergeStringArrays(wallObjectIds, wallIds);
            oreObjectIds = replaceExisting
                ? CopyNonEmptyStrings(oreIds)
                : MergeStringArrays(oreObjectIds, oreIds);
            waterObjectIds = replaceExisting
                ? CopyNonEmptyStrings(waterIds)
                : MergeStringArrays(waterObjectIds, waterIds);
            notes = "Semantic terrain preset applied.";
        }

        public void ClearContent(bool keepIdentity)
        {
            if (!keepIdentity)
            {
                presetId = "biome-content-preset";
                displayName = "Biome Content Preset";
                enabled = true;
            }

            environmentProfileId = string.Empty;
            environmentProfileTemplate = null;
            paletteAssetId = string.Empty;
            paletteTemplate = null;
            generationProfile = null;
            generationPasses = new GenerationPassTemplateAsset[0];
            generationTables = new GenerationTableTemplateAsset[0];
            scenePool = new SceneTemplateAsset[0];
            resourceNodes = new ResourceNodeTemplateAsset[0];
            spawnRules = new SpawnRuleTemplateAsset[0];
            floorObjectIds = new string[0];
            wallObjectIds = new string[0];
            oreObjectIds = new string[0];
            waterObjectIds = new string[0];
            notes = "Content preset cleared.";
        }

        public void AddGenerationPassesTo(List<GenerationPassTemplateAsset> destination)
        {
            if (!enabled || destination == null)
            {
                return;
            }

            if (generationProfile != null)
            {
                generationProfile.AddGenerationPassesTo(destination);
            }

            AddRange(generationPasses, destination);
        }

        public void AddGenerationTablesTo(List<GenerationTableTemplateAsset> destination)
        {
            if (!enabled || destination == null)
            {
                return;
            }

            if (generationProfile != null)
            {
                generationProfile.AddGenerationTablesTo(destination);
            }

            AddRange(generationTables, destination);
        }

        public void AddScenePoolTo(List<SceneTemplateAsset> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddRange(scenePool, destination);
        }

        public void AddResourceNodesTo(List<ResourceNodeTemplateAsset> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddRange(resourceNodes, destination);
        }

        public void AddSpawnRulesTo(List<SpawnRuleTemplateAsset> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddRange(spawnRules, destination);
        }

        public void AddEnvironmentProfileTemplatesTo(List<EnvironmentProfileTemplateAsset> destination)
        {
            if (!enabled || destination == null || environmentProfileTemplate == null)
            {
                return;
            }

            destination.Add(environmentProfileTemplate);
        }

        public void AddPaletteTemplatesTo(List<BiomePaletteTemplateAsset> destination)
        {
            if (!enabled || destination == null || paletteTemplate == null)
            {
                return;
            }

            destination.Add(paletteTemplate);
        }

        public void AddFloorObjectIdsTo(List<string> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddStringRange(floorObjectIds, destination);
        }

        public void AddWallObjectIdsTo(List<string> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddStringRange(wallObjectIds, destination);
        }

        public void AddOreObjectIdsTo(List<string> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddStringRange(oreObjectIds, destination);
        }

        public void AddWaterObjectIdsTo(List<string> destination)
        {
            if (!enabled)
            {
                return;
            }

            AddStringRange(waterObjectIds, destination);
        }

        private static void AddRange<T>(T[] source, List<T> destination)
            where T : Object
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                T item = source[i];
                if (item == null)
                {
                    continue;
                }

                destination.Add(item);
            }
        }

        private static void AddStringRange(string[] source, List<string> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                string item = source[i] ?? string.Empty;
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                destination.Add(item);
            }
        }

        private static string[] CopyNonEmptyStrings(string[] source)
        {
            List<string> values = new List<string>();
            AddStringRange(source, values);
            return values.ToArray();
        }

        private static string[] MergeStringArrays(string[] existing, string[] incoming)
        {
            List<string> values = new List<string>();
            AddStringRange(existing, values);
            AddUniqueStringRange(incoming, values);
            return values.ToArray();
        }

        private static T[] CopyObjects<T>(IReadOnlyList<T> source)
            where T : Object
        {
            if (source == null || source.Count == 0)
            {
                return new T[0];
            }

            List<T> destination = new List<T>();
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item != null)
                {
                    destination.Add(item);
                }
            }

            return destination.ToArray();
        }

        private static void AddUniqueStringRange(string[] source, List<string> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                string item = source[i] ?? string.Empty;
                if (string.IsNullOrEmpty(item) || ContainsString(destination, item))
                {
                    continue;
                }

                destination.Add(item);
            }
        }

        private static bool ContainsString(IReadOnlyList<string> values, string item)
        {
            if (values == null || string.IsNullOrEmpty(item))
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == item)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAny<T>(T[] source)
            where T : Object
        {
            if (source == null)
            {
                return false;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAny(string[] source)
        {
            if (source == null)
            {
                return false;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (!string.IsNullOrEmpty(source[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
