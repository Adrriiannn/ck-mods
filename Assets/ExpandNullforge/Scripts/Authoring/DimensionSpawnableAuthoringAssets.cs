using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

// The four spawnable ScriptableObject asset classes (Animal/Critter/Mob/Boss) moved to their
// own same-named files: Unity only binds .asset files to the MonoScript matching the filename.
// Only enums, plain serializable templates, and utilities may stay here.
namespace ExpandNullforge.Authoring
{
    public enum DimensionSpawnableKind
    {
        Animal = 0,
        Critter = 1,
        Mob = 2,
        Boss = 3
    }

    public enum DimensionSpawnAggressionKind
    {
        Passive = 0,
        Defensive = 1,
        Hostile = 2,
        Boss = 3,
        Custom = 100
    }

    public enum DimensionBossActivationKind
    {
        EncounterStart = 0,
        ArenaEntry = 1,
        SummonItem = 2,
        WorldEvent = 3,
        Custom = 100
    }

    [Serializable]
    public sealed class DimensionSpawnableStatsTemplate
    {
        [SerializeField] private int maxHealth = 1;
        [SerializeField] private int contactDamage;
        [SerializeField] private int defense;
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float detectionRadius = 8f;

        public int MaxHealth
        {
            get { return Mathf.Max(1, maxHealth); }
        }

        public int ContactDamage
        {
            get { return Mathf.Max(0, contactDamage); }
        }

        public int Defense
        {
            get { return Mathf.Max(0, defense); }
        }

        public float MoveSpeed
        {
            get { return Mathf.Max(0f, moveSpeed); }
        }

        public float DetectionRadius
        {
            get { return Mathf.Max(0f, detectionRadius); }
        }
    }

    [Serializable]
    public sealed class DimensionSpawnableVisualTemplate
    {
        [SerializeField] private string prefabId = string.Empty;
        [SerializeField] private string spriteId = string.Empty;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private string materialId = string.Empty;
        [SerializeField] private string animationSetId = string.Empty;
        [SerializeField] private string variantId = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string PrefabId
        {
            get { return prefabId ?? string.Empty; }
        }

        public string SpriteId
        {
            get { return spriteId ?? string.Empty; }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public string MaterialId
        {
            get { return materialId ?? string.Empty; }
        }

        public string AnimationSetId
        {
            get { return animationSetId ?? string.Empty; }
        }

        public string VariantId
        {
            get { return variantId ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string displayName,
            bool enabled,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "prefab",
                displayName + " Prefab",
                DimensionAssetReferenceKind.Prefab,
                PrefabId,
                VariantId,
                0,
                enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "sprite",
                displayName + " Sprite",
                DimensionAssetReferenceKind.Sprite,
                SpriteId,
                VariantId,
                10,
                enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "icon",
                displayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                VariantId,
                20,
                enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "material",
                displayName + " Material",
                DimensionAssetReferenceKind.Material,
                MaterialId,
                VariantId,
                30,
                enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "animation",
                displayName + " Animation",
                DimensionAssetReferenceKind.Custom,
                AnimationSetId,
                VariantId,
                40,
                enabled,
                Notes);
        }
    }

    [Serializable]
    public sealed class DimensionSpawnableAudioTemplate
    {
        [SerializeField] private string spawnSoundId = string.Empty;
        [SerializeField] private string idleSoundId = string.Empty;
        [SerializeField] private string aggroSoundId = string.Empty;
        [SerializeField] private string hitSoundId = string.Empty;
        [SerializeField] private string deathSoundId = string.Empty;
        [SerializeField] private string musicCueId = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string SpawnSoundId
        {
            get { return spawnSoundId ?? string.Empty; }
        }

        public string IdleSoundId
        {
            get { return idleSoundId ?? string.Empty; }
        }

        public string AggroSoundId
        {
            get { return aggroSoundId ?? string.Empty; }
        }

        public string HitSoundId
        {
            get { return hitSoundId ?? string.Empty; }
        }

        public string DeathSoundId
        {
            get { return deathSoundId ?? string.Empty; }
        }

        public string MusicCueId
        {
            get { return musicCueId ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string displayName,
            bool enabled,
            List<DimensionAssetReferenceDefinition> references)
        {
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "spawn-sound", "Spawn Sound", SpawnSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "idle-sound", "Idle Sound", IdleSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "aggro-sound", "Aggro Sound", AggroSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "hit-sound", "Hit Sound", HitSoundId, enabled);
            AddAudioReference(references, contentPackId, dimensionId, zoneId, ownerId, displayName, "death-sound", "Death Sound", DeathSoundId, enabled);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                "music",
                displayName + " Music",
                DimensionAssetReferenceKind.Music,
                MusicCueId,
                string.Empty,
                60,
                enabled,
                Notes);
        }

        private void AddAudioReference(
            List<DimensionAssetReferenceDefinition> references,
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string displayName,
            string suffix,
            string label,
            string resourceKey,
            bool enabled)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ownerId,
                suffix,
                displayName + " " + label,
                DimensionAssetReferenceKind.Audio,
                resourceKey,
                string.Empty,
                50,
                enabled,
                Notes);
        }
    }

    [Serializable]
    public sealed class DimensionBossPhaseTemplate
    {
        [SerializeField] private string phaseId = "phase";
        [SerializeField] private string displayName = "Phase";
        [SerializeField] private float healthThreshold = 0.5f;
        [SerializeField] private string spawnRuleId = string.Empty;
        [SerializeField] private string musicCueId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string PhaseId
        {
            get { return phaseId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public float HealthThreshold
        {
            get { return Mathf.Clamp01(healthThreshold); }
        }

        public string SpawnRuleId
        {
            get { return spawnRuleId ?? string.Empty; }
        }

        public string MusicCueId
        {
            get { return musicCueId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }

    internal static class DimensionSpawnableAssetReferenceUtility
    {
        public static void AddSpawnableAssetReferences(
            string contentPackId,
            string dimensionId,
            string zoneId,
            string spawnableId,
            string displayName,
            string objectId,
            bool enabled,
            DimensionSpawnableVisualTemplate visual,
            DimensionSpawnableAudioTemplate audio,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                spawnableId,
                "object",
                displayName + " Object",
                DimensionAssetReferenceKind.Object,
                objectId,
                string.Empty,
                0,
                enabled,
                string.Empty);

            if (visual != null)
            {
                visual.AddAssetReferencesTo(
                    contentPackId,
                    dimensionId,
                    zoneId,
                    spawnableId,
                    displayName,
                    enabled,
                    references);
            }

            if (audio != null)
            {
                audio.AddAssetReferencesTo(
                    contentPackId,
                    dimensionId,
                    zoneId,
                    spawnableId,
                    displayName,
                    enabled,
                    references);
            }
        }
    }

    internal static class DimensionAuthoringAssetReferenceUtility
    {
        public static void AddReference(
            List<DimensionAssetReferenceDefinition> references,
            string contentPackId,
            string dimensionId,
            string zoneId,
            string ownerId,
            string suffix,
            string displayName,
            DimensionAssetReferenceKind kind,
            string resourceKey,
            string variantId,
            int priority,
            bool enabled,
            string notes)
        {
            if (references == null ||
                string.IsNullOrEmpty(contentPackId) ||
                string.IsNullOrEmpty(ownerId) ||
                string.IsNullOrEmpty(resourceKey))
            {
                return;
            }

            references.Add(new DimensionAssetReferenceDefinition(
                ownerId + "." + suffix,
                contentPackId,
                displayName,
                kind,
                resourceKey,
                dimensionId,
                zoneId,
                variantId,
                priority,
                enabled,
                notes));
        }
    }

    public static class DimensionSpawnableAuthoringUtility
    {
        public static string ResolveId(DimensionAnimalAsset asset)
        {
            return asset == null ? string.Empty : asset.AnimalId;
        }

        public static string ResolveId(DimensionCritterAsset asset)
        {
            return asset == null ? string.Empty : asset.CritterId;
        }

        public static string ResolveId(DimensionMobAsset asset)
        {
            return asset == null ? string.Empty : asset.MobId;
        }

        public static string ResolveId(DimensionBossAsset asset)
        {
            return asset == null ? string.Empty : asset.BossId;
        }

        public static string ResolveLootTableId(DimensionLootTableAsset asset)
        {
            return asset == null ? string.Empty : asset.LootTableId;
        }
    }
}
