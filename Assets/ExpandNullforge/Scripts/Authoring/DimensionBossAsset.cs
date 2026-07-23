using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Boss")]
    public sealed class DimensionBossAsset : ScriptableObject
    {
        [SerializeField] private string bossId = "mod:boss";
        [SerializeField] private string displayName = "Boss";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string arenaSceneId = string.Empty;
        [SerializeField] private string summoningItemId = string.Empty;
        [SerializeField] private DimensionSpawnableStatsTemplate stats = new DimensionSpawnableStatsTemplate();
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();
        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private DimensionBossActivationKind activation = DimensionBossActivationKind.ArenaEntry;
        [SerializeField] private DimensionBossPhaseTemplate[] phases = new DimensionBossPhaseTemplate[0];
        [SerializeField] private float respawnCooldownMinutes;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string BossId { get { return bossId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string ArenaSceneId { get { return arenaSceneId ?? string.Empty; } }
        public string SummoningItemId { get { return summoningItemId ?? string.Empty; } }
        public DimensionSpawnableStatsTemplate Stats { get { return stats ?? new DimensionSpawnableStatsTemplate(); } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
        public DimensionBossActivationKind Activation { get { return activation; } }
        public DimensionBossPhaseTemplate[] Phases { get { return phases ?? new DimensionBossPhaseTemplate[0]; } }
        public float RespawnCooldownMinutes { get { return Mathf.Max(0f, respawnCooldownMinutes); } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionSpawnableAssetReferenceUtility.AddSpawnableAssetReferences(
                contentPackId,
                dimensionId,
                zoneId,
                BossId,
                DisplayName,
                ObjectId,
                Enabled,
                Visual,
                Audio,
                references);
            DimensionBossPhaseTemplate[] phaseValues = Phases;
            for (int i = 0; i < phaseValues.Length; i++)
            {
                DimensionBossPhaseTemplate phase = phaseValues[i];
                if (phase == null || !phase.Enabled)
                {
                    continue;
                }

                DimensionAuthoringAssetReferenceUtility.AddReference(
                    references,
                    contentPackId,
                    dimensionId,
                    zoneId,
                    BossId,
                    "phase-" + i.ToString() + "-music",
                    DisplayName + " " + phase.DisplayName + " Music",
                    DimensionAssetReferenceKind.Music,
                    phase.MusicCueId,
                    string.Empty,
                    70 + i,
                    Enabled,
                    phase.Notes);
            }
        }
    }
}
