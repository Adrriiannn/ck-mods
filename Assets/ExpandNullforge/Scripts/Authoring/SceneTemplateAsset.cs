using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Scene Template")]
    public sealed class SceneTemplateAsset : ScriptableObject
    {
        [SerializeField] private string sceneId = "scene";
        [SerializeField] private string templateId = "scene-template";
        [SerializeField] private string displayName = "Scene";
        [SerializeField] private string kind = "scene";
        [SerializeField] private string providerId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionScenePlacementMode placementMode = DimensionScenePlacementMode.Automatic;
        [SerializeField] private Vector2Int footprintSize = new Vector2Int(16, 16);
        [SerializeField] private Vector2Int exactLocalPosition;
        [SerializeField] private Vector2Int preferredLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int preferredLocalMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private int weight = 1;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool required;
        [SerializeField] private bool unique = true;
        [SerializeField] private DimensionScenePropTemplate[] props =
            new DimensionScenePropTemplate[0];
        [SerializeField] private DimensionSceneLootContainerTemplate[] lootContainers =
            new DimensionSceneLootContainerTemplate[0];
        [SerializeField] private DimensionSceneSpawnPointTemplate[] spawnPoints =
            new DimensionSceneSpawnPointTemplate[0];
        [SerializeField] private DimensionSceneTriggerTemplate[] triggers =
            new DimensionSceneTriggerTemplate[0];

        public string SceneId
        {
            get { return sceneId ?? string.Empty; }
        }

        public string TemplateId
        {
            get { return templateId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Kind
        {
            get { return kind ?? string.Empty; }
        }

        public string[] AllowedBiomeIds
        {
            get { return allowedBiomeIds ?? new string[0]; }
        }

        public DimensionScenePlacementMode PlacementMode
        {
            get { return placementMode; }
        }

        public Vector2Int FootprintSize
        {
            get { return footprintSize; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool Required
        {
            get { return required; }
        }

        public bool Unique
        {
            get { return unique; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public DimensionScenePropTemplate[] Props
        {
            get { return props ?? new DimensionScenePropTemplate[0]; }
        }

        public DimensionSceneLootContainerTemplate[] LootContainers
        {
            get { return lootContainers ?? new DimensionSceneLootContainerTemplate[0]; }
        }

        public DimensionSceneSpawnPointTemplate[] SpawnPoints
        {
            get { return spawnPoints ?? new DimensionSceneSpawnPointTemplate[0]; }
        }

        public DimensionSceneTriggerTemplate[] Triggers
        {
            get { return triggers ?? new DimensionSceneTriggerTemplate[0]; }
        }

        public int AuthoredContentCount
        {
            get
            {
                return CountEnabled(Props) +
                    CountEnabled(LootContainers) +
                    CountEnabled(SpawnPoints) +
                    CountEnabled(Triggers);
            }
        }

        public void ConfigureIdentity(
            string newSceneId,
            string newTemplateId,
            string newDisplayName,
            string newKind,
            string newProviderId,
            int newPriority,
            bool newEnabled)
        {
            sceneId = newSceneId ?? string.Empty;
            templateId = newTemplateId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            kind = string.IsNullOrEmpty(newKind) ? "scene" : newKind;
            providerId = newProviderId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ConfigureSpawnPolicy(int newWeight, bool newRequired, bool newUnique)
        {
            weight = Mathf.Max(1, newWeight);
            required = newRequired;
            unique = newUnique;
        }

        public void SetAllowedBiomeIds(IReadOnlyList<string> biomeIds)
        {
            if (biomeIds == null || biomeIds.Count == 0)
            {
                allowedBiomeIds = new string[0];
                return;
            }

            List<string> values = new List<string>();
            for (int i = 0; i < biomeIds.Count; i++)
            {
                string biomeId = biomeIds[i];
                if (!string.IsNullOrEmpty(biomeId) && !ContainsString(values, biomeId))
                {
                    values.Add(biomeId);
                }
            }

            allowedBiomeIds = values.ToArray();
        }

        public void SetProps(IReadOnlyList<DimensionScenePropTemplate> values)
        {
            props = CopyValues(values);
        }

        public void SetLootContainers(IReadOnlyList<DimensionSceneLootContainerTemplate> values)
        {
            lootContainers = CopyValues(values);
        }

        public void SetSpawnPoints(IReadOnlyList<DimensionSceneSpawnPointTemplate> values)
        {
            spawnPoints = CopyValues(values);
        }

        public void SetTriggers(IReadOnlyList<DimensionSceneTriggerTemplate> values)
        {
            triggers = CopyValues(values);
        }

        public void ApplyAutomaticPlacement(Vector2Int newFootprintSize)
        {
            placementMode = DimensionScenePlacementMode.Automatic;
            footprintSize = EnsurePositiveSize(newFootprintSize);
        }

        public void ApplyExactPlacement(Vector2Int newExactLocalPosition, Vector2Int newFootprintSize)
        {
            placementMode = DimensionScenePlacementMode.ExactLocalPosition;
            exactLocalPosition = newExactLocalPosition;
            footprintSize = EnsurePositiveSize(newFootprintSize);
        }

        public void ApplyPreferredPlacement(
            Vector2Int newPreferredLocalMin,
            Vector2Int newPreferredLocalMaxExclusive,
            Vector2Int newFootprintSize)
        {
            placementMode = DimensionScenePlacementMode.PreferredBounds;
            preferredLocalMin = newPreferredLocalMin;
            preferredLocalMaxExclusive = EnsureExclusiveMax(newPreferredLocalMin, newPreferredLocalMaxExclusive);
            footprintSize = EnsurePositiveSize(newFootprintSize);
        }

        public DimensionBounds ExactLocalBounds
        {
            get
            {
                return BoundsFromPositionAndSize(exactLocalPosition, footprintSize);
            }
        }

        public DimensionBounds PreferredLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(preferredLocalMin.x, preferredLocalMin.y),
                    new int2(preferredLocalMaxExclusive.x, preferredLocalMaxExclusive.y));
            }
        }

        public DimensionSceneTemplateDefinition ToTemplateDefinition(string dimensionId, string zoneId)
        {
            return new DimensionSceneTemplateDefinition(
                templateId,
                displayName,
                dimensionId,
                zoneId,
                kind,
                providerId,
                new int2(footprintSize.x, footprintSize.y),
                weight,
                priority,
                enabled);
        }

        public DimensionSceneDefinition ToSceneDefinition(string dimensionId, DimensionBounds localBounds)
        {
            return new DimensionSceneDefinition(
                sceneId,
                displayName,
                dimensionId,
                localBounds,
                kind,
                priority,
                enabled ? DimensionSceneState.Planned : DimensionSceneState.Disabled);
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                sceneId,
                "scene",
                displayName,
                DimensionAssetReferenceKind.Scene,
                string.IsNullOrEmpty(providerId) ? templateId : providerId,
                kind,
                priority,
                enabled,
                "Scene template source.");

            DimensionScenePropTemplate[] propValues = Props;
            for (int i = 0; i < propValues.Length; i++)
            {
                DimensionScenePropTemplate prop = propValues[i];
                if (prop != null && prop.Enabled)
                {
                    prop.AddAssetReferencesTo(contentPackId, dimensionId, zoneId, sceneId, references);
                }
            }

            DimensionSceneLootContainerTemplate[] containerValues = LootContainers;
            for (int i = 0; i < containerValues.Length; i++)
            {
                DimensionSceneLootContainerTemplate container = containerValues[i];
                if (container != null && container.Enabled)
                {
                    container.AddAssetReferencesTo(contentPackId, dimensionId, zoneId, sceneId, references);
                }
            }
        }

        private static DimensionBounds BoundsFromPositionAndSize(Vector2Int position, Vector2Int size)
        {
            return new DimensionBounds(
                new int2(position.x, position.y),
                new int2(position.x + size.x, position.y + size.y));
        }

        private static Vector2Int EnsurePositiveSize(Vector2Int size)
        {
            return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        private static Vector2Int EnsureExclusiveMax(Vector2Int localMin, Vector2Int localMaxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(localMin.x + 1, localMaxExclusive.x),
                Mathf.Max(localMin.y + 1, localMaxExclusive.y));
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

        private static T[] CopyValues<T>(IReadOnlyList<T> source)
            where T : class
        {
            if (source == null || source.Count == 0)
            {
                return new T[0];
            }

            List<T> values = new List<T>();
            for (int i = 0; i < source.Count; i++)
            {
                T value = source[i];
                if (value != null)
                {
                    values.Add(value);
                }
            }

            return values.ToArray();
        }

        private static int CountEnabled(DimensionScenePropTemplate[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEnabled(DimensionSceneLootContainerTemplate[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEnabled(DimensionSceneSpawnPointTemplate[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountEnabled(DimensionSceneTriggerTemplate[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null && values[i].Enabled)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
