using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Mob")]
    public sealed class DimensionMobAsset : ScriptableObject, IDimensionLootBearingAsset
    {
        [SerializeField] private string mobId = "mod:mob";

        [Tooltip("What this creature is called wherever the game has to name it — a slot it ends up in, the console that spawns it. Core Keeper draws no nameplate over an ordinary enemy, so this is not written over its head.")]
        [SerializeField] private string displayName = "Mob";

        [Tooltip("The line under the name in a tooltip. The game's own enemies leave this empty.")]
        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string[] allowedBiomeIds = new string[0];
        [Tooltip("Every number this creature has, used exactly as typed unless you ask for level scaling.")]
        [SerializeField] private DimensionCreatureStatsTemplate creatureStats = new DimensionCreatureStatsTemplate();

        [SerializeField] private DimensionCreatureCombatTemplate combat = new DimensionCreatureCombatTemplate();

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

        [Tooltip("Also generate a rarer, tougher version of this creature.")]
        [SerializeField] private DimensionEliteVariantTemplate eliteVariant = new DimensionEliteVariantTemplate();
        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();
        [SerializeField] private DimensionLootTableAsset lootTable;
        [Tooltip("What starts the fight.\n\n" +
                 "Hostile: it comes after any player it can see, and after whoever hits it from " +
                 "up to twenty tiles away.\n\n" +
                 "Defensive: it ignores players completely until one of them hits it, then it " +
                 "hunts that player for as long as they keep it interested.\n\n" +
                 "Passive: it never attacks anyone at all. It also stops counting as prey, so " +
                 "tamed pets and summoned minions leave it alone, the way they leave cows alone.\n\n" +
                 "Custom: leave the attack categories under Attacks exactly as you typed them.")]
        [SerializeField] private DimensionSpawnAggressionKind aggression = DimensionSpawnAggressionKind.Hostile;
        [SerializeField] private string behaviorScriptId = string.Empty;
        [Tooltip("Loot it drops other than on death: as it is hit, when it is used, or by season.")]
        [SerializeField] private DimensionExtraLootTemplate extraLoot = new DimensionExtraLootTemplate();

        [Tooltip("What killing it teaches: skill experience for whoever landed the last blow.")]
        [SerializeField] private DimensionSkillRewardTemplate skillReward =
            new DimensionSkillRewardTemplate();

        [Tooltip("It is an egg: a player coming near makes it hatch into something else, the " +
                 "way the Larva Hive's cocoons burst.")]
        [SerializeField] private DimensionHatchingTemplate hatching = new DimensionHatchingTemplate();

        [Tooltip("It keeps coming back: tiles of a chosen kind breed it over time, the way " +
                 "hive nests keep making larvae. This is what keeps a dungeon or a biome " +
                 "populated after the first spawns die.")]
        [SerializeField] private DimensionRespawnTemplate respawn = new DimensionRespawnTemplate();

        [Tooltip("It is a pet: it follows its owner, fights alongside them, and buffs them.")]
        [SerializeField] private DimensionPetTemplate pet = new DimensionPetTemplate();

        [Tooltip("The small things it is, or does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string MobId { get { return mobId ?? string.Empty; } }
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

        /// <summary>The tougher sibling generated alongside this creature, if any.</summary>
        public DimensionEliteVariantTemplate EliteVariant
        {
            get { return eliteVariant ?? new DimensionEliteVariantTemplate(); }
        }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
        public DimensionSpawnAggressionKind Aggression
        {
            get
            {
                // The retired Boss value (3): assets saved before the retirement still carry
                // the raw int, and OnValidate only fires when the asset is touched in-editor.
                return (int)aggression == 3 ? DimensionSpawnAggressionKind.Hostile : aggression;
            }
        }
        public string BehaviorScriptId { get { return behaviorScriptId ?? string.Empty; } }
        public DimensionExtraLootTemplate ExtraLoot
        {
            get { return extraLoot ?? (extraLoot = new DimensionExtraLootTemplate()); }
        }

        /// <summary>The skill experience killing it is worth, if any.</summary>
        public DimensionSkillRewardTemplate SkillReward
        {
            get { return skillReward ?? (skillReward = new DimensionSkillRewardTemplate()); }
        }

        public DimensionRespawnTemplate Respawn
        {
            get { return respawn ?? (respawn = new DimensionRespawnTemplate()); }
        }

        public DimensionHatchingTemplate Hatching
        {
            get { return hatching ?? (hatching = new DimensionHatchingTemplate()); }
        }

        public DimensionPetTemplate Pet
        {
            get { return pet ?? (pet = new DimensionPetTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

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
                Audio,
                references);
        }

        private void OnValidate()
        {
            // The retired Boss aggression (3): migrate the saved value back to Hostile so the
            // inspector popup never shows an out-of-range selection.
            if ((int)aggression == 3)
            {
                aggression = DimensionSpawnAggressionKind.Hostile;
            }
        }
    }
}
