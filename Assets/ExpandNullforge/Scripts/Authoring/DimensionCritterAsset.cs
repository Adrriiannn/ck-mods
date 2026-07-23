using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Critter")]
    public sealed class DimensionCritterAsset : ScriptableObject
    {
        [SerializeField] private string critterId = "mod:critter";
        [SerializeField] private string displayName = "Critter";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();
        [SerializeField] private int spawnWeight = 1;
        [SerializeField] private bool scatterOnApproach = true;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string CritterId { get { return critterId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string[] AllowedBiomeIds { get { return allowedBiomeIds ?? new string[0]; } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }
        public int SpawnWeight { get { return Mathf.Max(1, spawnWeight); } }
        public bool ScatterOnApproach { get { return scatterOnApproach; } }
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
                CritterId,
                DisplayName,
                ObjectId,
                Enabled,
                Visual,
                Audio,
                references);
        }
    }
}
