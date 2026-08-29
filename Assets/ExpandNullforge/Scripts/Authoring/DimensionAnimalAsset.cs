using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Animal")]
    public sealed class DimensionAnimalAsset : ScriptableObject, IDimensionLootBearingAsset
    {
        [SerializeField] private string animalId = "mod:animal";

        [Tooltip("What this animal is called wherever the game has to name it — a slot it ends up in, the console that spawns it. Core Keeper draws no nameplate over an animal, so this is not written over its head.")]
        [SerializeField] private string displayName = "Animal";

        [Tooltip("The line under the name in a tooltip. The game's own animals leave this empty.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [Tooltip("Every number this creature has, used exactly as typed unless you ask for level scaling.")]
        [SerializeField] private DimensionCreatureStatsTemplate creatureStats = new DimensionCreatureStatsTemplate();

        [SerializeField] private DimensionCreatureCombatTemplate combat = new DimensionCreatureCombatTemplate();

        [Tooltip("What starts the fight. Animals begin Passive, which is what the game's own " +
                 "cows, goats and roly polies are.\n\n" +
                 "Passive: it never attacks anyone at all, and tamed pets and summoned minions " +
                 "leave it alone, the way they leave cows alone.\n\n" +
                 "Defensive: it ignores players completely until one of them hits it, then it " +
                 "hunts that player for as long as they keep it interested.\n\n" +
                 "Hostile: it comes after any player it can see, and after whoever hits it from " +
                 "up to twenty tiles away.\n\n" +
                 "Custom: leave the attack categories under Attacks exactly as you typed them.")]
        [SerializeField] private DimensionSpawnAggressionKind aggression =
            DimensionSpawnAggressionKind.Passive;

        [Header("Where it lives")]
        [Tooltip("Let the world place this creature on its own, the way vanilla wildlife appears.")]
        [SerializeField] private bool spawnsInWorld;

        [Tooltip("Chance per candidate tile, on the same scale Core Keeper's own entries use.")]
        [SerializeField] private float spawnChance = 0.002f;

        [Tooltip("Most of them to place at once.")]
        [SerializeField] private int spawnAmount = 1;

        [Tooltip("Appear in groups rather than singly.")]
        [SerializeField] private bool spawnsInGroups;

        [Tooltip("May appear inside dungeons and placed scenes. Off matches vanilla wildlife.")]
        [SerializeField] private bool canSpawnInBlockedArea;
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();

        [Tooltip("The small things it simply is, or simply does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string AnimalId { get { return animalId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string Description { get { return description ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string[] AllowedBiomeIds { get { return allowedBiomeIds ?? new string[0]; } }

        /// <summary>The full stat block, which is what generation actually writes onto the prefab.</summary>
        public DimensionCreatureStatsTemplate CreatureStats
        {
            get { return creatureStats ?? new DimensionCreatureStatsTemplate(); }
        }

        /// <summary>How it notices things, wanders, and fights.</summary>
        public DimensionCreatureCombatTemplate Combat
        {
            get { return combat ?? new DimensionCreatureCombatTemplate(); }
        }

        /// <summary>Whether it ignores players, defends itself, or hunts them down.</summary>
        /// <remarks>
        /// Clamped away from the retired Boss value (3) for the same reason the mob asset clamps
        /// it: an asset saved before the retirement still holds the raw int, and a popup showing
        /// an out-of-range selection is how an author ends up with a temper nothing implements.
        /// </remarks>
        public DimensionSpawnAggressionKind Aggression
        {
            get
            {
                return (int)aggression == 3 ? DimensionSpawnAggressionKind.Hostile : aggression;
            }
        }

        /// <summary>Whether the world places this creature on its own.</summary>
        /// <remarks>
        /// Off by default. A creature that only appears where the author placed it — in a scene, from
        /// a summon, as part of an event — is a normal thing to want, and a creature that quietly
        /// started filling the world would be a surprise.
        /// </remarks>
        public bool SpawnsInWorld { get { return spawnsInWorld; } }

        public float SpawnChance { get { return spawnChance < 0f ? 0f : spawnChance; } }

        public int SpawnAmount { get { return spawnAmount < 1 ? 1 : spawnAmount; } }

        public bool SpawnsInGroups { get { return spawnsInGroups; } }

        public bool CanSpawnInBlockedArea { get { return canSpawnInBlockedArea; } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        public DimensionLootTableAsset LootTable { get { return lootTable; } }
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
                Audio,
                references);
        }
    }
}
