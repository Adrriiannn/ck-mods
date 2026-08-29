using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class - Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload.
    [CreateAssetMenu(menuName = "Dimensions API/Boss")]
    public sealed class DimensionBossAsset : ScriptableObject, IDimensionLootBearingAsset
    {
        [Tooltip("A short id for this boss. Core Keeper records a defeat in the world only for its own eight bosses, so anything you want to unlock off yours has to keep its own count — see Docs/IdentityGates.md.")]
        [SerializeField] private string bossId = "mod:boss";
        [SerializeField] private string displayName = "Boss";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string arenaSceneId = string.Empty;
        [SerializeField] private string summoningItemId = string.Empty;
        [Tooltip("Every number this creature has, used exactly as typed unless you ask for level scaling.")]
        [SerializeField] private DimensionCreatureStatsTemplate creatureStats = new DimensionCreatureStatsTemplate();

        [Header("How it fights")]
        [Tooltip("A fighting kit borrowed from one of the game's own bosses, then made yours.")]
        [SerializeField] private DimensionBorrowedBossKitTemplate borrowedKit = new DimensionBorrowedBossKitTemplate();

        [Tooltip("More borrowable kits: the Core's orbiting fight, the Wall's body, the Scarab's charge.")]
        [SerializeField] private DimensionCoreBossKitTemplate moreBorrowedKits = new DimensionCoreBossKitTemplate();

        [Tooltip("And the rest: the Bird, Robot, Octopus, Larva, Shaman and Snake fights.")]
        [SerializeField] private DimensionMoreBossKitsTemplate theRestOfTheKits = new DimensionMoreBossKitsTemplate();

        [Header("What it leaves behind")]
        [Tooltip("The treasure chest it drops where it died. The most recognisable thing a boss does.")]
        [SerializeField] private DimensionBossChestTemplate bossChest = new DimensionBossChestTemplate();

        [SerializeField] private DimensionCreatureCombatTemplate combat = new DimensionCreatureCombatTemplate();

        [Tooltip("What starts the fight.\n\n" +
                 "Hostile: it comes after any player it can see, and after whoever hits it from " +
                 "up to twenty tiles away.\n\n" +
                 "Defensive: it ignores players completely until one of them hits it, then it " +
                 "hunts that player for as long as they keep it interested. This is the boss " +
                 "asleep in its arena that only wakes when you swing first.\n\n" +
                 "Passive: it never attacks anyone at all, whatever they do to it.\n\n" +
                 "Custom: leave the attack categories under Attacks exactly as you typed them.")]
        [SerializeField] private DimensionSpawnAggressionKind aggression =
            DimensionSpawnAggressionKind.Hostile;

        [SerializeField] private DimensionSpawnableVisualTemplate visual = new DimensionSpawnableVisualTemplate();
        [SerializeField] private DimensionSpawnableAudioTemplate audio = new DimensionSpawnableAudioTemplate();

        [Tooltip("The pin this boss shows on the map, the way the game's own bosses do.")]
        [SerializeField] private DimensionBossMapPinTemplate mapPin = new DimensionBossMapPinTemplate();

        [Tooltip("The music of the fight. Starts as a player approaches; BOSS is the game's own boss roster.")]
        [SerializeField] private DimensionMusicAreaTemplate fightMusic =
            DimensionMusicAreaTemplate.BossFightDefaults();

        [Tooltip("The small things it simply is, or simply does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [SerializeField] private DimensionLootTableAsset lootTable;
        [SerializeField] private DimensionBossPhaseTemplate[] phases = new DimensionBossPhaseTemplate[0];
        [SerializeField] private float respawnCooldownMinutes;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string BossId { get { return bossId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public string ObjectId { get { return objectId ?? string.Empty; } }
        public string ArenaSceneId { get { return arenaSceneId ?? string.Empty; } }
        public string SummoningItemId { get { return summoningItemId ?? string.Empty; } }

        /// <summary>The full stat block, which is what generation actually writes onto the prefab.</summary>
        public DimensionCreatureStatsTemplate CreatureStats
        {
            get { return creatureStats ?? new DimensionCreatureStatsTemplate(); }
        }

        /// <summary>A fight borrowed from one of the game's own bosses, then made yours.</summary>
        public DimensionBorrowedBossKitTemplate BorrowedKit
        {
            get { return borrowedKit ?? (borrowedKit = new DimensionBorrowedBossKitTemplate()); }
        }

        /// <summary>The Core's, the Wall's and the Scarab's fights.</summary>
        public DimensionCoreBossKitTemplate MoreBorrowedKits
        {
            get { return moreBorrowedKits ?? (moreBorrowedKits = new DimensionCoreBossKitTemplate()); }
        }

        /// <summary>The Bird, Robot, Octopus, Larva, Shaman and Snake fights.</summary>
        public DimensionMoreBossKitsTemplate TheRestOfTheKits
        {
            get { return theRestOfTheKits ?? (theRestOfTheKits = new DimensionMoreBossKitsTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        /// <summary>The treasure chest it leaves where it died.</summary>
        public DimensionBossChestTemplate BossChest
        {
            get { return bossChest ?? new DimensionBossChestTemplate(); }
        }

        public DimensionCreatureCombatTemplate Combat
        {
            get { return combat ?? new DimensionCreatureCombatTemplate(); }
        }

        /// <summary>Whether it ignores players, waits to be provoked, or attacks on sight.</summary>
        /// <remarks>
        /// A boss used to be pinned to Hostile by the generator on the reasoning that "a fight
        /// nobody can start is not a boss fight". That is a fight design opinion, not a technical
        /// limit, and it is wrong about a real and common shape: a boss asleep in its arena that
        /// only wakes for whoever swings first. Nothing in <c>BossAuthoring</c>, the health bar,
        /// the fight music or the map pin reads the temperament, so all three tempers work on a
        /// boss exactly as they work on a mob. Hostile stays the default because it is what almost
        /// every boss is.
        /// </remarks>
        public DimensionSpawnAggressionKind Aggression { get { return aggression; } }
        public DimensionSpawnableVisualTemplate Visual { get { return visual ?? new DimensionSpawnableVisualTemplate(); } }
        public DimensionSpawnableAudioTemplate Audio { get { return audio ?? new DimensionSpawnableAudioTemplate(); } }

        public DimensionBossMapPinTemplate MapPin
        {
            get { return mapPin ?? (mapPin = new DimensionBossMapPinTemplate()); }
        }

        public DimensionMusicAreaTemplate FightMusic
        {
            get { return fightMusic ?? (fightMusic = DimensionMusicAreaTemplate.BossFightDefaults()); }
        }
        public DimensionLootTableAsset LootTable { get { return lootTable; } }
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
