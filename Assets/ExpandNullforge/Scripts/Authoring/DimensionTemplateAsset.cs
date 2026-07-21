using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Dimension Asset")]
    public sealed class DimensionTemplateAsset : ScriptableObject
    {
        [SerializeField] private string dimensionId = "mod.dimension";
        [SerializeField] private string displayName = "Custom Dimension";
        [SerializeField] private string description = string.Empty;
        [SerializeField] private string contentPackId = string.Empty;
        [SerializeField] private string contentPackDisplayName = string.Empty;
        [SerializeField] private string contentPackVersion = "1.0.0";
        [SerializeField] private string contentPackAuthor = string.Empty;
        [SerializeField] private int minimumApiVersion = 1;
        [SerializeField] private string[] dependencyContentPackIds = new string[0];
        // Clears the protected ±5000 overworld band by the dimension's half-extent; an origin at
        // exactly 5000 straddles it and dimension registration is rejected.
        [SerializeField] private Vector2Int absoluteOrigin = new Vector2Int(0, 7000);
        [SerializeField] private Vector2Int reservedLocalMin = new Vector2Int(-4096, -4096);
        [SerializeField] private Vector2Int reservedLocalMaxExclusive = new Vector2Int(4096, 4096);
        [SerializeField] private int generationVersion = 1;
        [SerializeField] private DimensionSpaceKind spaceKind = DimensionSpaceKind.PocketWorld;
        [SerializeField] private DimensionCapabilityFlags capabilities =
            DimensionCapabilityFlags.LocalCoordinates |
            DimensionCapabilityFlags.AbsoluteCoordinates |
            DimensionCapabilityFlags.PlayerContext |
            DimensionCapabilityFlags.PlayerTravel |
            DimensionCapabilityFlags.Portals |
            DimensionCapabilityFlags.Map |
            DimensionCapabilityFlags.Minimap |
            DimensionCapabilityFlags.Generation |
            DimensionCapabilityFlags.AreaLoading |
            DimensionCapabilityFlags.SimulationLoading |
            DimensionCapabilityFlags.Persistence |
            DimensionCapabilityFlags.Multiplayer |
            DimensionCapabilityFlags.CoordinateInterop;
        [SerializeField] private DimensionLayoutTemplateAsset layoutTemplate;
        [SerializeField] private EnvironmentProfileTemplateAsset[] environmentProfiles = new EnvironmentProfileTemplateAsset[0];
        [SerializeField] private BiomeTemplateAsset[] biomes = new BiomeTemplateAsset[0];
        [SerializeField] private SceneTemplateAsset[] globalScenes = new SceneTemplateAsset[0];
        [SerializeField] private ResourceNodeTemplateAsset[] globalResourceNodes = new ResourceNodeTemplateAsset[0];
        [SerializeField] private DimensionItemAsset[] globalItems = new DimensionItemAsset[0];
        [SerializeField] private DimensionRecipeAsset[] globalRecipes = new DimensionRecipeAsset[0];
        [SerializeField] private DimensionWorkbenchAsset[] globalWorkbenches = new DimensionWorkbenchAsset[0];
        [SerializeField] private DimensionLootTableAsset[] globalLootTables = new DimensionLootTableAsset[0];
        [SerializeField] private DimensionAnimalAsset[] globalAnimals = new DimensionAnimalAsset[0];
        [SerializeField] private DimensionCritterAsset[] globalCritters = new DimensionCritterAsset[0];
        [SerializeField] private DimensionMobAsset[] globalMobs = new DimensionMobAsset[0];
        [SerializeField] private DimensionBossAsset[] globalBosses = new DimensionBossAsset[0];
        [SerializeField] private SpawnRuleTemplateAsset[] globalSpawnRules = new SpawnRuleTemplateAsset[0];
        [SerializeField] private GenerationPassTemplateAsset[] globalGenerationPasses = new GenerationPassTemplateAsset[0];
        [SerializeField] private GenerationTableTemplateAsset[] globalGenerationTables = new GenerationTableTemplateAsset[0];
        [SerializeField] private DimensionPortalAccessRuleAsset[] portalAccessRules = new DimensionPortalAccessRuleAsset[0];
        [SerializeField] private float portalActivationChargeSeconds = 30.0f;
        [SerializeField] private DimensionPortalVisualProfileAsset portalVisualProfile;

        public string DimensionId
        {
            get { return dimensionId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public string ContentPackId
        {
            get { return contentPackId ?? string.Empty; }
        }

        public string ContentPackDisplayName
        {
            get { return string.IsNullOrEmpty(contentPackDisplayName) ? DisplayName : contentPackDisplayName; }
        }

        public string ContentPackVersion
        {
            get { return contentPackVersion ?? string.Empty; }
        }

        public string ContentPackAuthor
        {
            get { return contentPackAuthor ?? string.Empty; }
        }

        public int MinimumApiVersion
        {
            get { return minimumApiVersion; }
        }

        public string[] DependencyContentPackIds
        {
            get { return dependencyContentPackIds ?? new string[0]; }
        }

        public DimensionLayoutTemplateAsset LayoutTemplate
        {
            get { return layoutTemplate; }
        }

        public EnvironmentProfileTemplateAsset[] EnvironmentProfiles
        {
            get { return environmentProfiles ?? new EnvironmentProfileTemplateAsset[0]; }
        }

        public BiomeTemplateAsset[] Biomes
        {
            get { return biomes ?? new BiomeTemplateAsset[0]; }
        }

        public SceneTemplateAsset[] GlobalScenes
        {
            get { return globalScenes ?? new SceneTemplateAsset[0]; }
        }

        public ResourceNodeTemplateAsset[] GlobalResourceNodes
        {
            get { return globalResourceNodes ?? new ResourceNodeTemplateAsset[0]; }
        }

        public DimensionItemAsset[] GlobalItems
        {
            get { return globalItems ?? new DimensionItemAsset[0]; }
        }

        public DimensionRecipeAsset[] GlobalRecipes
        {
            get { return globalRecipes ?? new DimensionRecipeAsset[0]; }
        }

        public DimensionWorkbenchAsset[] GlobalWorkbenches
        {
            get { return globalWorkbenches ?? new DimensionWorkbenchAsset[0]; }
        }

        public DimensionLootTableAsset[] GlobalLootTables
        {
            get { return globalLootTables ?? new DimensionLootTableAsset[0]; }
        }

        public DimensionAnimalAsset[] GlobalAnimals
        {
            get { return globalAnimals ?? new DimensionAnimalAsset[0]; }
        }

        public DimensionCritterAsset[] GlobalCritters
        {
            get { return globalCritters ?? new DimensionCritterAsset[0]; }
        }

        public DimensionMobAsset[] GlobalMobs
        {
            get { return globalMobs ?? new DimensionMobAsset[0]; }
        }

        public DimensionBossAsset[] GlobalBosses
        {
            get { return globalBosses ?? new DimensionBossAsset[0]; }
        }

        public SpawnRuleTemplateAsset[] GlobalSpawnRules
        {
            get { return globalSpawnRules ?? new SpawnRuleTemplateAsset[0]; }
        }

        public GenerationPassTemplateAsset[] GlobalGenerationPasses
        {
            get { return globalGenerationPasses ?? new GenerationPassTemplateAsset[0]; }
        }

        public GenerationTableTemplateAsset[] GlobalGenerationTables
        {
            get { return globalGenerationTables ?? new GenerationTableTemplateAsset[0]; }
        }

        public DimensionPortalAccessRuleAsset[] PortalAccessRules
        {
            get { return portalAccessRules ?? new DimensionPortalAccessRuleAsset[0]; }
        }

        public float PortalActivationChargeSeconds
        {
            get { return Mathf.Max(0.0f, portalActivationChargeSeconds); }
        }

        public DimensionPortalVisualProfileAsset PortalVisualProfile
        {
            get { return portalVisualProfile; }
        }

        public DimensionBounds ReservedLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(reservedLocalMin.x, reservedLocalMin.y),
                    new int2(reservedLocalMaxExclusive.x, reservedLocalMaxExclusive.y));
            }
        }

        public void ConfigureIdentity(
            string newDimensionId,
            string newDisplayName,
            string newDescription,
            string newContentPackId,
            string newContentPackDisplayName,
            string newContentPackVersion,
            string newContentPackAuthor,
            int newMinimumApiVersion,
            DimensionSpaceKind newSpaceKind,
            DimensionCapabilityFlags newCapabilities)
        {
            dimensionId = newDimensionId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            description = newDescription ?? string.Empty;
            contentPackId = newContentPackId ?? string.Empty;
            contentPackDisplayName = newContentPackDisplayName ?? string.Empty;
            contentPackVersion = newContentPackVersion ?? string.Empty;
            contentPackAuthor = newContentPackAuthor ?? string.Empty;
            minimumApiVersion = Mathf.Max(1, newMinimumApiVersion);
            spaceKind = newSpaceKind;
            capabilities = newCapabilities;
        }

        public void ApplyCustomizerMetadata(
            string newDimensionId,
            string newDisplayName,
            string newDescription,
            string newContentPackId,
            string newContentPackDisplayName,
            string newContentPackVersion,
            string newContentPackAuthor,
            int newMinimumApiVersion)
        {
            dimensionId = newDimensionId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            description = newDescription ?? string.Empty;
            contentPackId = newContentPackId ?? string.Empty;
            contentPackDisplayName = newContentPackDisplayName ?? string.Empty;
            contentPackVersion = newContentPackVersion ?? string.Empty;
            contentPackAuthor = newContentPackAuthor ?? string.Empty;
            minimumApiVersion = Mathf.Max(1, newMinimumApiVersion);
        }

        public void SetDependencyContentPackIds(IReadOnlyList<string> dependencies)
        {
            dependencyContentPackIds = CopyUniqueStrings(dependencies);
        }

        public void ConfigurePlacement(Vector2Int newAbsoluteOrigin, int newGenerationVersion)
        {
            absoluteOrigin = newAbsoluteOrigin;
            generationVersion = Mathf.Max(1, newGenerationVersion);
        }

        public void ApplyReservedLocalBounds(DimensionBounds bounds)
        {
            reservedLocalMin = new Vector2Int(bounds.Min.x, bounds.Min.y);
            reservedLocalMaxExclusive = EnsureExclusiveMax(
                reservedLocalMin,
                new Vector2Int(bounds.MaxExclusive.x, bounds.MaxExclusive.y));
        }

        public void SetLayoutTemplate(DimensionLayoutTemplateAsset template)
        {
            layoutTemplate = template;
        }

        public void SetEnvironmentProfiles(IReadOnlyList<EnvironmentProfileTemplateAsset> assets)
        {
            environmentProfiles = CopyObjects(assets);
        }

        public void SetBiomes(IReadOnlyList<BiomeTemplateAsset> assets)
        {
            biomes = CopyObjects(assets);
        }

        public void SetGlobalScenes(IReadOnlyList<SceneTemplateAsset> assets)
        {
            globalScenes = CopyObjects(assets);
        }

        public void SetGlobalResourceNodes(IReadOnlyList<ResourceNodeTemplateAsset> assets)
        {
            globalResourceNodes = CopyObjects(assets);
        }

        public void SetGlobalItems(IReadOnlyList<DimensionItemAsset> assets)
        {
            globalItems = CopyObjects(assets);
        }

        public void SetGlobalRecipes(IReadOnlyList<DimensionRecipeAsset> assets)
        {
            globalRecipes = CopyObjects(assets);
        }

        public void SetGlobalWorkbenches(IReadOnlyList<DimensionWorkbenchAsset> assets)
        {
            globalWorkbenches = CopyObjects(assets);
        }

        public void SetGlobalLootTables(IReadOnlyList<DimensionLootTableAsset> assets)
        {
            globalLootTables = CopyObjects(assets);
        }

        public void SetGlobalAnimals(IReadOnlyList<DimensionAnimalAsset> assets)
        {
            globalAnimals = CopyObjects(assets);
        }

        public void SetGlobalCritters(IReadOnlyList<DimensionCritterAsset> assets)
        {
            globalCritters = CopyObjects(assets);
        }

        public void SetGlobalMobs(IReadOnlyList<DimensionMobAsset> assets)
        {
            globalMobs = CopyObjects(assets);
        }

        public void SetGlobalBosses(IReadOnlyList<DimensionBossAsset> assets)
        {
            globalBosses = CopyObjects(assets);
        }

        public void SetGlobalSpawnRules(IReadOnlyList<SpawnRuleTemplateAsset> assets)
        {
            globalSpawnRules = CopyObjects(assets);
        }

        public void SetGlobalGenerationPasses(IReadOnlyList<GenerationPassTemplateAsset> assets)
        {
            globalGenerationPasses = CopyObjects(assets);
        }

        public void SetGlobalGenerationTables(IReadOnlyList<GenerationTableTemplateAsset> assets)
        {
            globalGenerationTables = CopyObjects(assets);
        }

        public void SetPortalAccessRules(IReadOnlyList<DimensionPortalAccessRuleAsset> assets)
        {
            portalAccessRules = CopyObjects(assets);
        }

        public void ConfigurePortalAppearance(
            float activationChargeSeconds,
            DimensionPortalVisualProfileAsset visualProfile)
        {
            portalActivationChargeSeconds = Mathf.Max(0.0f, activationChargeSeconds);
            portalVisualProfile = visualProfile;
        }

        public void SetPortalVisualProfile(DimensionPortalVisualProfileAsset visualProfile)
        {
            portalVisualProfile = visualProfile;
        }

        public DimensionDefinition ToDimensionDefinition()
        {
            return ToDimensionDefinition(ReservedLocalBounds);
        }

        public DimensionDefinition ToDimensionDefinition(DimensionBounds resolvedReservedLocalBounds)
        {
            return new DimensionDefinition(
                dimensionId,
                displayName,
                new int2(absoluteOrigin.x, absoluteOrigin.y),
                resolvedReservedLocalBounds,
                generationVersion,
                spaceKind,
                NormalizeRuntimeCapabilities(capabilities),
                DimensionLifecycleState.Registered);
        }

        private static DimensionCapabilityFlags NormalizeRuntimeCapabilities(
            DimensionCapabilityFlags source)
        {
            if ((source & DimensionCapabilityFlags.PlayerTravel) == DimensionCapabilityFlags.PlayerTravel)
            {
                source |= DimensionCapabilityFlags.AreaLoading;
                source |= DimensionCapabilityFlags.SimulationLoading;
            }

            return source;
        }

        private static Vector2Int EnsureExclusiveMax(Vector2Int localMin, Vector2Int localMaxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(localMin.x + 1, localMaxExclusive.x),
                Mathf.Max(localMin.y + 1, localMaxExclusive.y));
        }

        private static string[] CopyUniqueStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return new string[0];
            }

            List<string> destination = new List<string>();
            for (int i = 0; i < source.Count; i++)
            {
                string value = source[i];
                if (!string.IsNullOrEmpty(value) && !ContainsString(destination, value))
                {
                    destination.Add(value);
                }
            }

            return destination.ToArray();
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

        private static bool ContainsString(List<string> values, string value)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
