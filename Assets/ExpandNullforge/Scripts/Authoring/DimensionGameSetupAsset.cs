using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The rules of the game a mod changes, rather than adds to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERYTHING ELSE IN THE FRAMEWORK ADDS. A creature asset adds a creature; a loot table asset
    /// adds to what can drop. This one is different: it reaches into rules the game has exactly one
    /// of, and changes them for everyone playing with the mod on — what upgrading costs, what
    /// fishing catches, what talents give, and the numbers on the player.
    /// </para>
    /// <para>
    /// ONLY WHAT REACHES THE GAME IS OFFERED. An earlier version of this asset offered the whole
    /// shelf of the game's rule tables. It could not deliver them: a mod's copy of a table is
    /// thrown away before a world ever reads it, and the tables that CAN be changed are already
    /// changed from the assets that own them — loot from the loot tables, buffs from the
    /// conditions, what lives where from the spawn rules. What is here is what nothing else covers
    /// and what has a moment it can actually be written at.
    /// </para>
    /// <para>
    /// NOTHING IS GENERATED AS AN OBJECT. Two of these blocks become plain settings files inside
    /// the mod that the game reads while it starts; the rest become instructions that run as a
    /// world loads, or answers supplied where the game asks its question. No prefab is written,
    /// which is why switching a block off simply stops it.
    /// </para>
    /// <para>
    /// WHAT EACH BLOCK CHANGES IS THE WHOLE GAME, not one dimension. There is one upgrade table,
    /// one set-bonus table, one list of backgrounds, one world generator. A rule set applies
    /// wherever the mod is loaded, which is the point of it and also the reason to be sparing.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/World Rules")]
    public sealed class DimensionGameSetupAsset : ScriptableObject
    {
        [Tooltip("A short id for this rule set. Used for the settings file names inside the mod.")]
        [SerializeField] private string setupIdentifier = "rules";

        [Tooltip("What this rule set is called where you pick it.")]
        [SerializeField] private string displayName = "World rules";

        [Tooltip("Whether this rule set is applied at all.")]
        [SerializeField] private bool enabled = true;

        [Tooltip("What upgrading a piece of gear costs at each level.")]
        [SerializeField] private DimensionUpgradeCostTemplate upgrading =
            new DimensionUpgradeCostTemplate();

        [Tooltip("What fishing catches, and how hard each fish fights.")]
        [SerializeField] private DimensionFishingTemplate fishing = new DimensionFishingTemplate();

        [Tooltip("What each talent gives.")]
        [SerializeField] private DimensionTalentsTemplate talents = new DimensionTalentsTemplate();

        [Tooltip("The numbers this mod overrides on the game's own player.")]
        [SerializeField] private DimensionPlayerTemplate player = new DimensionPlayerTemplate();

        [Tooltip("Armour sets of your own, and what wearing enough of one gives.")]
        [SerializeField] private DimensionSetBonusTemplate armourSets = new DimensionSetBonusTemplate();

        [Tooltip("What the game's eleven backgrounds start a new character with.")]
        [SerializeField] private DimensionBackgroundTemplate backgrounds =
            new DimensionBackgroundTemplate();

        [Tooltip("Where and when the world acts on a player of its own accord: cave-ins, swarms, tentacles.")]
        [SerializeField] private DimensionEnvironmentEventTemplate worldEvents =
            new DimensionEnvironmentEventTemplate();

        [Tooltip("Where your blocks and ores appear in Core Keeper's own caves.")]
        [SerializeField] private DimensionWorldTerrainRuleTemplate gamesOwnTerrain =
            new DimensionWorldTerrainRuleTemplate();

        [Tooltip("Your own pictures on the game's twelve skills.")]
        [SerializeField] private DimensionSkillIconTemplate skillPictures =
            new DimensionSkillIconTemplate();

        public string SetupIdentifier
        {
            get { return string.IsNullOrEmpty(setupIdentifier) ? string.Empty : setupIdentifier; }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? SetupIdentifier : displayName; }
        }

        public bool Enabled { get { return enabled; } }

        /// <summary>What upgrading a piece of gear costs at each level.</summary>
        public DimensionUpgradeCostTemplate Upgrading
        {
            get { return upgrading ?? (upgrading = new DimensionUpgradeCostTemplate()); }
        }

        /// <summary>What fishing catches, and how hard each fish fights.</summary>
        public DimensionFishingTemplate Fishing
        {
            get { return fishing ?? (fishing = new DimensionFishingTemplate()); }
        }

        /// <summary>What each talent gives.</summary>
        public DimensionTalentsTemplate Talents
        {
            get { return talents ?? (talents = new DimensionTalentsTemplate()); }
        }

        /// <summary>The numbers this mod overrides on the game's own player.</summary>
        public DimensionPlayerTemplate Player
        {
            get { return player ?? (player = new DimensionPlayerTemplate()); }
        }

        /// <summary>Armour sets of a mod's own, and what wearing enough of one gives.</summary>
        public DimensionSetBonusTemplate ArmourSets
        {
            get { return armourSets ?? (armourSets = new DimensionSetBonusTemplate()); }
        }

        /// <summary>What the game's eleven backgrounds start a new character with.</summary>
        public DimensionBackgroundTemplate Backgrounds
        {
            get { return backgrounds ?? (backgrounds = new DimensionBackgroundTemplate()); }
        }

        /// <summary>Where and when the world acts on a player of its own accord.</summary>
        public DimensionEnvironmentEventTemplate WorldEvents
        {
            get { return worldEvents ?? (worldEvents = new DimensionEnvironmentEventTemplate()); }
        }

        /// <summary>Where a mod's blocks and ores appear in Core Keeper's own caves.</summary>
        public DimensionWorldTerrainRuleTemplate GamesOwnTerrain
        {
            get
            {
                return gamesOwnTerrain ??
                       (gamesOwnTerrain = new DimensionWorldTerrainRuleTemplate());
            }
        }

        /// <summary>A mod's own pictures on the game's twelve skills.</summary>
        public DimensionSkillIconTemplate SkillPictures
        {
            get { return skillPictures ?? (skillPictures = new DimensionSkillIconTemplate()); }
        }

        /// <summary>No block was switched on, so applying it would change nothing.</summary>
        public bool NothingIsSwitchedOn
        {
            get
            {
                return !Upgrading.ChangesWhatUpgradingCosts &&
                       !Fishing.ChangesWhatFishingCatches &&
                       !Talents.ChangesWhatTalentsGive &&
                       !Player.OverridesTheGamesPlayer &&
                       !ArmourSets.AddsArmourSets &&
                       !Backgrounds.ChangesWhatBackgroundsStartYouWith &&
                       !WorldEvents.ChangesWhenTheWorldActs &&
                       !GamesOwnTerrain.PutsBlocksInTheGamesWorld &&
                       !SkillPictures.ChangesTheSkillPictures;
            }
        }
    }
}
