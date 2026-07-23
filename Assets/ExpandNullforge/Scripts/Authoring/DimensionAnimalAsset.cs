using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Animal")]
    public sealed class DimensionAnimalAsset : ScriptableObject
    {
        [SerializeField] private string animalId = "mod:animal";
        [SerializeField] private string displayName = "Animal";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionSpawnableStatsTemplate stats = new DimensionSpawnableStatsTemplate();
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();
        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private int spawnWeight = 1;
        [SerializeField] private int herdMin = 1;
        [SerializeField] private int herdMax = 1;
        [SerializeField] private bool friendly = true;
        [SerializeField] private bool tameable;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string AnimalId { get { return animalId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string[] AllowedBiomeIds { get { return allowedBiomeIds ?? new string[0]; } }
        public DimensionSpawnableStatsTemplate Stats { get { return stats ?? new DimensionSpawnableStatsTemplate(); } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
        public int SpawnWeight { get { return Mathf.Max(1, spawnWeight); } }
        public int HerdMin { get { return Mathf.Max(1, herdMin); } }
        public int HerdMax { get { return Mathf.Max(HerdMin, herdMax); } }
        public bool Friendly { get { return friendly; } }
        public bool Tameable { get { return tameable; } }
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
                AnimalId,
                DisplayName,
                ObjectId,
                Enabled,
                Visual,
                Audio,
                references);
        }
    }
}
