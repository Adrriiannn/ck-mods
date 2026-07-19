using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [Serializable]
    public sealed class DimensionScenePropTemplate
    {
        [SerializeField] private string propId = "prop";
        [SerializeField] private string displayName = "Prop";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private Vector2Int localPosition;
        [SerializeField] private int rotationSteps;
        [SerializeField] private string variantId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string PropId { get { return propId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public Vector2Int LocalPosition { get { return localPosition; } }
        public int RotationSteps { get { return Mathf.Abs(rotationSteps % 4); } }
        public string VariantId { get { return variantId ?? string.Empty; } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string sceneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                BuildOwnerId(sceneId, PropId),
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                VariantId,
                0,
                Enabled,
                Notes);
        }

        private static string BuildOwnerId(string sceneId, string localId)
        {
            return (sceneId ?? string.Empty) + ".prop." + (localId ?? string.Empty);
        }
    }

    [Serializable]
    public sealed class DimensionSceneLootContainerTemplate
    {
        [SerializeField] private string containerId = "loot";
        [SerializeField] private string displayName = "Loot Container";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private Vector2Int localPosition;
        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private string lootTableId = string.Empty;
        [SerializeField] private string requiredItemId = string.Empty;
        [SerializeField] private bool locked;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string ContainerId { get { return containerId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public Vector2Int LocalPosition { get { return localPosition; } }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
        public string LootTableId
        {
            get
            {
                return lootTable != null
                    ? lootTable.LootTableId
                    : lootTableId ?? string.Empty;
            }
        }

        public string RequiredItemId { get { return requiredItemId ?? string.Empty; } }
        public bool Locked { get { return locked; } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string sceneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                (sceneId ?? string.Empty) + ".loot." + ContainerId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                0,
                Enabled,
                Notes);
        }
    }

    [Serializable]
    public sealed class DimensionSceneSpawnPointTemplate
    {
        [SerializeField] private string spawnPointId = "spawn";
        [SerializeField] private string displayName = "Spawn Point";
        [SerializeField] private Vector2Int localPosition;
        [SerializeField] private DimensionAnimalAsset animal;
        [SerializeField] private DimensionCritterAsset critter;
        [SerializeField] private DimensionMobAsset mob;
        [SerializeField] private DimensionBossAsset boss;
        [SerializeField] private string fallbackSubjectId = string.Empty;
        [SerializeField] private DimensionSpawnSubjectKind fallbackSubjectKind = DimensionSpawnSubjectKind.Mob;
        [SerializeField] private int minCount = 1;
        [SerializeField] private int maxCount = 1;
        [SerializeField] private int weight = 1;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string SpawnPointId { get { return spawnPointId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public Vector2Int LocalPosition { get { return localPosition; } }
        public DimensionAnimalAsset Animal { get { return animal; } }
        public DimensionCritterAsset Critter { get { return critter; } }
        public DimensionMobAsset Mob { get { return mob; } }
        public DimensionBossAsset Boss { get { return boss; } }
        public string FallbackSubjectId { get { return fallbackSubjectId ?? string.Empty; } }
        public DimensionSpawnSubjectKind FallbackSubjectKind { get { return fallbackSubjectKind; } }
        public int MinCount { get { return Mathf.Max(1, minCount); } }
        public int MaxCount { get { return Mathf.Max(MinCount, maxCount); } }
        public int Weight { get { return Mathf.Max(1, weight); } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        public string ResolvedSubjectId
        {
            get
            {
                if (boss != null)
                {
                    return boss.BossId;
                }

                if (mob != null)
                {
                    return mob.MobId;
                }

                if (critter != null)
                {
                    return critter.CritterId;
                }

                if (animal != null)
                {
                    return animal.AnimalId;
                }

                return FallbackSubjectId;
            }
        }

        public DimensionSpawnSubjectKind ResolvedSubjectKind
        {
            get
            {
                if (boss != null)
                {
                    return DimensionSpawnSubjectKind.Boss;
                }

                if (mob != null)
                {
                    return DimensionSpawnSubjectKind.Mob;
                }

                if (critter != null || animal != null)
                {
                    return DimensionSpawnSubjectKind.Critter;
                }

                return FallbackSubjectKind;
            }
        }
    }

    [Serializable]
    public sealed class DimensionSceneTriggerTemplate
    {
        [SerializeField] private string triggerId = "trigger";
        [SerializeField] private string displayName = "Trigger";
        [SerializeField] private Vector2Int localMin;
        [SerializeField] private Vector2Int localMaxExclusive = new Vector2Int(1, 1);
        [SerializeField] private string eventId = string.Empty;
        [SerializeField] private string promptText = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string TriggerId { get { return triggerId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public Vector2Int LocalMin { get { return localMin; } }
        public Vector2Int LocalMaxExclusive { get { return EnsureExclusiveMax(localMin, localMaxExclusive); } }
        public string EventId { get { return eventId ?? string.Empty; } }
        public string PromptText { get { return promptText ?? string.Empty; } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        private static Vector2Int EnsureExclusiveMax(Vector2Int min, Vector2Int maxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(min.x + 1, maxExclusive.x),
                Mathf.Max(min.y + 1, maxExclusive.y));
        }
    }
}
