using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Mob")]
    public sealed class DimensionMobAsset : ScriptableObject
    {
        [SerializeField] private string mobId = "mod:mob";
        [SerializeField] private string displayName = "Mob";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionSpawnableStatsTemplate stats = new DimensionSpawnableStatsTemplate();
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();
        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private DimensionSpawnAggressionKind aggression = DimensionSpawnAggressionKind.Hostile;
        [SerializeField] private int spawnWeight = 1;
        [SerializeField] private string behaviorScriptId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string MobId { get { return mobId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string[] AllowedBiomeIds { get { return allowedBiomeIds ?? new string[0]; } }
        public DimensionSpawnableStatsTemplate Stats { get { return stats ?? new DimensionSpawnableStatsTemplate(); } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
        public DimensionSpawnAggressionKind Aggression { get { return aggression; } }
        public int SpawnWeight { get { return Mathf.Max(1, spawnWeight); } }
        public string BehaviorScriptId { get { return behaviorScriptId ?? string.Empty; } }
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
                MobId,
                DisplayName,
                ObjectId,
                Enabled,
                Visual,
                Audio,
                references);
        }
    }
}
