using ExpandNullforge.Api;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Biome Template")]
    public sealed class BiomeTemplateAsset : ScriptableObject
    {
        [SerializeField] private string biomeId = "biome";
        [SerializeField] private string displayName = "Biome";
        [SerializeField] private string environmentProfileId = string.Empty;
        [SerializeField] private EnvironmentProfileTemplateAsset environmentProfileTemplate;
        [SerializeField] private string paletteAssetId = string.Empty;
        [SerializeField] private BiomePaletteTemplateAsset paletteTemplate;
        [SerializeField] private string spawnTableId = string.Empty;
        [SerializeField] private string resourceTableId = string.Empty;
        [SerializeField] private string worldEventTableId = string.Empty;
        [SerializeField] private BiomeContentPresetAsset[] contentPresets = new BiomeContentPresetAsset[0];
        [SerializeField] private Color mapColor = new Color(0.25f, 0.45f, 0.55f, 1f);
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool hasFallbackLocalBounds;
        [SerializeField] private Vector2Int fallbackLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int fallbackLocalMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private string[] floorObjectIds = new string[0];
        [SerializeField] private string[] wallObjectIds = new string[0];
        [SerializeField] private string[] oreObjectIds = new string[0];
        [SerializeField] private string[] waterObjectIds = new string[0];
        [SerializeField] private SceneTemplateAsset[] scenePool = new SceneTemplateAsset[0];
        [SerializeField] private ResourceNodeTemplateAsset[] resourceNodes = new ResourceNodeTemplateAsset[0];
        [SerializeField] private SpawnRuleTemplateAsset[] spawnRules = new SpawnRuleTemplateAsset[0];
        [SerializeField] private BiomeGenerationProfileAsset generationProfile;
        [SerializeField] private GenerationPassTemplateAsset[] generationPasses = new GenerationPassTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] generationTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private string notes = string.Empty;

        private void OnValidate()
        {
            contentPresets = CopyObjects(contentPresets);
            scenePool = CopyObjects(scenePool);
            resourceNodes = CopyObjects(resourceNodes);
            spawnRules = CopyObjects(spawnRules);
            generationPasses = CopyObjects(generationPasses);
            generationTables = CopyObjects(generationTables);
        }

        public string BiomeId
        {
            get { return biomeId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool HasFallbackLocalBounds
        {
            get { return hasFallbackLocalBounds; }
        }

        public DimensionBounds FallbackLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(fallbackLocalMin.x, fallbackLocalMin.y),
                    new int2(fallbackLocalMaxExclusive.x, fallbackLocalMaxExclusive.y));
            }
        }

        public SceneTemplateAsset[] ScenePool
        {
            get { return scenePool ?? new SceneTemplateAsset[0]; }
        }

        public BiomeContentPresetAsset[] ContentPresets
        {
            get { return contentPresets ?? new BiomeContentPresetAsset[0]; }
        }

        public ResourceNodeTemplateAsset[] ResourceNodes
        {
            get { return resourceNodes ?? new ResourceNodeTemplateAsset[0]; }
        }

        public SpawnRuleTemplateAsset[] SpawnRules
        {
            get { return spawnRules ?? new SpawnRuleTemplateAsset[0]; }
        }

        public BiomeGenerationProfileAsset GenerationProfile
        {
            get { return generationProfile; }
        }

        public EnvironmentProfileTemplateAsset EnvironmentProfileTemplate
        {
            get { return environmentProfileTemplate; }
        }

        public string EnvironmentProfileId
        {
            get { return environmentProfileId ?? string.Empty; }
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

                BiomeContentPresetAsset[] presets = ContentPresets;
                for (int i = 0; i < presets.Length; i++)
                {
                    BiomeContentPresetAsset preset = presets[i];
                    if (preset == null || !preset.Enabled)
                    {
                        continue;
                    }

                    string presetProfileId = preset.ResolvedEnvironmentProfileId;
                    if (!string.IsNullOrEmpty(presetProfileId))
                    {
                        return presetProfileId;
                    }
                }

                return string.Empty;
            }
        }

        public BiomePaletteTemplateAsset PaletteTemplate
        {
            get { return paletteTemplate; }
        }

        public string PaletteAssetId
        {
            get { return paletteAssetId ?? string.Empty; }
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

                BiomeContentPresetAsset[] presets = ContentPresets;
                for (int i = 0; i < presets.Length; i++)
                {
                    BiomeContentPresetAsset preset = presets[i];
                    if (preset == null || !preset.Enabled)
                    {
                        continue;
                    }

                    string presetPaletteId = preset.ResolvedPaletteAssetId;
                    if (!string.IsNullOrEmpty(presetPaletteId))
                    {
                        return presetPaletteId;
                    }
                }

                return string.Empty;
            }
        }

        public GenerationPassTemplateAsset[] GenerationPasses
        {
            get { return generationPasses ?? new GenerationPassTemplateAsset[0]; }
        }

        public GenerationTableTemplateAsset[] GenerationTables
        {
            get { return generationTables ?? new GenerationTableTemplateAsset[0]; }
        }

        public GenerationPassTemplateAsset[] GetGenerationPassesWithProfile()
        {
            List<GenerationPassTemplateAsset> combined = new List<GenerationPassTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddGenerationPassesTo(combined);
            }

            if (generationProfile != null)
            {
                generationProfile.AddGenerationPassesTo(combined);
            }

            AddRange(GenerationPasses, combined);
            return combined.ToArray();
        }

        public GenerationTableTemplateAsset[] GetGenerationTablesWithProfile()
        {
            List<GenerationTableTemplateAsset> combined = new List<GenerationTableTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddGenerationTablesTo(combined);
            }

            if (generationProfile != null)
            {
                generationProfile.AddGenerationTablesTo(combined);
            }

            AddRange(GenerationTables, combined);
            return combined.ToArray();
        }

        public SceneTemplateAsset[] GetScenePoolWithPresets()
        {
            List<SceneTemplateAsset> combined = new List<SceneTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddScenePoolTo(combined);
            }

            AddRange(ScenePool, combined);
            return combined.ToArray();
        }

        public ResourceNodeTemplateAsset[] GetResourceNodesWithPresets()
        {
            List<ResourceNodeTemplateAsset> combined = new List<ResourceNodeTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddResourceNodesTo(combined);
            }

            AddRange(ResourceNodes, combined);
            return combined.ToArray();
        }

        public SpawnRuleTemplateAsset[] GetSpawnRulesWithPresets()
        {
            List<SpawnRuleTemplateAsset> combined = new List<SpawnRuleTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddSpawnRulesTo(combined);
            }

            AddRange(SpawnRules, combined);
            return combined.ToArray();
        }

        public EnvironmentProfileTemplateAsset[] GetEnvironmentProfileTemplatesWithPresets()
        {
            List<EnvironmentProfileTemplateAsset> combined = new List<EnvironmentProfileTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddEnvironmentProfileTemplatesTo(combined);
            }

            if (environmentProfileTemplate != null)
            {
                combined.Add(environmentProfileTemplate);
            }

            return combined.ToArray();
        }

        public BiomePaletteTemplateAsset[] GetPaletteTemplatesWithPresets()
        {
            List<BiomePaletteTemplateAsset> combined = new List<BiomePaletteTemplateAsset>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddPaletteTemplatesTo(combined);
            }

            if (paletteTemplate != null)
            {
                combined.Add(paletteTemplate);
            }

            return combined.ToArray();
        }

        public string[] GetFloorObjectIdsWithPresets()
        {
            List<string> combined = new List<string>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddFloorObjectIdsTo(combined);
            }

            AddStringRange(floorObjectIds, combined);
            return combined.ToArray();
        }

        public string[] GetWallObjectIdsWithPresets()
        {
            List<string> combined = new List<string>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddWallObjectIdsTo(combined);
            }

            AddStringRange(wallObjectIds, combined);
            return combined.ToArray();
        }

        public string[] GetOreObjectIdsWithPresets()
        {
            List<string> combined = new List<string>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddOreObjectIdsTo(combined);
            }

            AddStringRange(oreObjectIds, combined);
            return combined.ToArray();
        }

        public string[] GetWaterObjectIdsWithPresets()
        {
            List<string> combined = new List<string>();
            BiomeContentPresetAsset[] presets = ContentPresets;
            for (int i = 0; i < presets.Length; i++)
            {
                BiomeContentPresetAsset preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                preset.AddWaterObjectIdsTo(combined);
            }

            AddStringRange(waterObjectIds, combined);
            return combined.ToArray();
        }

        public DimensionBiomeDefinition ToBiomeDefinition(string dimensionId)
        {
            return new DimensionBiomeDefinition(
                biomeId,
                displayName,
                dimensionId,
                ResolvedEnvironmentProfileId,
                ResolvedPaletteAssetId,
                spawnTableId,
                resourceTableId,
                worldEventTableId,
                ToRgba(mapColor),
                priority,
                enabled,
                notes);
        }

        public void ConfigureIdentity(
            string newBiomeId,
            string newDisplayName,
            Color newMapColor,
            int newPriority,
            bool newEnabled)
        {
            biomeId = newBiomeId ?? string.Empty;
            displayName = string.IsNullOrEmpty(newDisplayName)
                ? biomeId
                : newDisplayName;
            mapColor = newMapColor;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ApplyCustomizerMetadata(
            string newBiomeId,
            string newDisplayName,
            int newPriority,
            bool newEnabled,
            string newEnvironmentProfileId,
            string newPaletteAssetId,
            string newNotes)
        {
            biomeId = newBiomeId ?? string.Empty;
            displayName = string.IsNullOrEmpty(newDisplayName)
                ? biomeId
                : newDisplayName;
            priority = newPriority;
            enabled = newEnabled;
            environmentProfileId = newEnvironmentProfileId ?? string.Empty;
            paletteAssetId = newPaletteAssetId ?? string.Empty;
            notes = newNotes ?? string.Empty;
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

        public void SetContentPresets(IReadOnlyList<BiomeContentPresetAsset> presets)
        {
            contentPresets = CopyObjects(presets);
        }

        public void SetScenePool(IReadOnlyList<SceneTemplateAsset> scenes)
        {
            scenePool = CopyObjects(scenes);
        }

        public void SetResourceNodes(IReadOnlyList<ResourceNodeTemplateAsset> nodes)
        {
            resourceNodes = CopyObjects(nodes);
        }

        public void SetSpawnRules(IReadOnlyList<SpawnRuleTemplateAsset> rules)
        {
            spawnRules = CopyObjects(rules);
        }

        public void SetGenerationPasses(IReadOnlyList<GenerationPassTemplateAsset> passes)
        {
            generationPasses = CopyObjects(passes);
        }

        public void SetGenerationTables(IReadOnlyList<GenerationTableTemplateAsset> tables)
        {
            generationTables = CopyObjects(tables);
        }

        public void ApplyFallbackLocalBounds(
            Vector2Int localMin,
            Vector2Int localMaxExclusive)
        {
            hasFallbackLocalBounds = true;
            fallbackLocalMin = localMin;
            fallbackLocalMaxExclusive = EnsureExclusiveMax(localMin, localMaxExclusive);
        }

        public void ClearFallbackLocalBounds()
        {
            hasFallbackLocalBounds = false;
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
            notes = "Semantic terrain preset applied. Runtime generation will expand these IDs into scoped generation tables.";
        }

        public void ApplyMinimalBiomePreset(
            string newBiomeId,
            string newDisplayName,
            Color newMapColor,
            Vector2Int localMin,
            Vector2Int localMaxExclusive,
            string[] floorIds,
            string[] wallIds)
        {
            ConfigureIdentity(newBiomeId, newDisplayName, newMapColor, priority, true);
            ApplyFallbackLocalBounds(localMin, localMaxExclusive);
            ApplySemanticTerrainPreset(
                floorIds,
                wallIds,
                new string[0],
                new string[0],
                true);
            notes = "Generated from the minimal biome preset.";
        }

        private static uint ToRgba(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static Vector2Int EnsureExclusiveMax(
            Vector2Int localMin,
            Vector2Int localMaxExclusive)
        {
            int maxX = localMaxExclusive.x <= localMin.x
                ? localMin.x + 1
                : localMaxExclusive.x;
            int maxY = localMaxExclusive.y <= localMin.y
                ? localMin.y + 1
                : localMaxExclusive.y;
            return new Vector2Int(maxX, maxY);
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
    }
}
