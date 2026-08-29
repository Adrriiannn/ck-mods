using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Mana, healing auras, ancient wiring, and the last of the world's connective tissue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MANA IS A RESOURCE THE FRAMEWORK HAD NEVER EXPOSED. An object with mana holds a pool that
    /// refills on its own, and a siphon drains it from something else at a distance. Between them
    /// they are how the game builds a power network that is not electricity — the ancient machinery
    /// that runs on drawn mana rather than on wire.
    /// </para>
    /// <para>
    /// ANCIENT WIRING IS ITS OWN NETWORK, separate from ordinary circuits. Each piece carries an
    /// amount, can be a source of its own, and can block the flow — which is how a puzzle is built
    /// out of it rather than a machine.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionManaAndAuraTemplate
    {
        [Header("Mana")]
        [Tooltip("It holds a pool of mana.")]
        [SerializeField] private bool holdsMana;

        [Tooltip("How much it starts with.")]
        [Min(0)]
        [SerializeField] private int startingMana;

        [Tooltip("The most it can hold.")]
        [Min(0)]
        [SerializeField] private int maximumMana = 100;

        [Tooltip("How fast it refills, in mana per second.")]
        [Min(0f)]
        [SerializeField] private float refillRate = 1f;

        [Tooltip("How long after being drained before it starts refilling.")]
        [Min(0f)]
        [SerializeField] private float refillDelay = 2f;

        [Header("Drawing mana from elsewhere")]
        [Tooltip("It siphons mana out of things around it.")]
        [SerializeField] private bool siphonsMana;

        [Tooltip("How much it draws per second.")]
        [Min(0f)]
        [SerializeField] private float siphonRate = 5f;

        [Tooltip("How long it rests between draws.")]
        [Min(0f)]
        [SerializeField] private float siphonCooldown = 1f;

        [Tooltip("How far it will reach to draw from something.")]
        [Min(0f)]
        [SerializeField] private float siphonReach = 10f;

        [Tooltip("How wide an area it draws from.")]
        [Min(0f)]
        [SerializeField] private float siphonRadius = 5f;

        [Header("Healing what is near it")]
        [Tooltip("It heals things around it.")]
        [SerializeField] private bool healsWhatIsNear;

        [Tooltip("It starts already healing rather than waiting to be switched on.")]
        [SerializeField] private bool healingStartsOn = true;

        [Tooltip("Whose side it heals, by the game's own faction name. Blank heals everyone.")]
        [SerializeField] private string healsFaction = string.Empty;

        [Tooltip("How much health it restores per second.")]
        [Min(0)]
        [SerializeField] private int healthPerSecond = 5;

        [Tooltip("How far that reaches.")]
        [Min(0f)]
        [SerializeField] private float healRadius = 5f;

        [Header("Ancient wiring")]
        [Tooltip("It is part of the ancient power network, which is separate from ordinary wiring.")]
        [SerializeField] private bool isAncientWiring;

        [Tooltip("How much power it carries.")]
        [Min(0)]
        [SerializeField] private int carriesPower;

        [Tooltip("How much power it produces of its own. 0 means it only carries.")]
        [Min(0)]
        [SerializeField] private int producesPower;

        [Tooltip("It blocks the flow rather than passing it on — how a puzzle gate is built.")]
        [SerializeField] private bool blocksTheFlow;

        [Header("Belonging to something else")]
        [Tooltip("It is a part of a larger thing rather than a thing in its own right.")]
        [SerializeField] private bool isPartOfSomethingElse;

        [Tooltip("The larger thing it belongs to.")]
        [SerializeField] private GameObject belongsTo;

        [Tooltip("An owner it answers to.")]
        [SerializeField] private GameObject ownedBy;

        [Header("Boss hooks")]
        [Tooltip("It is bait that draws a Hydra.")]
        [SerializeField] private bool isHydraBait;

        [Tooltip("Which Hydra it draws.")]
        [SerializeField] private DimensionHydraKind attractsHydra = DimensionHydraKind.Nature;

        [Tooltip("It marks where a boss appears.")]
        [SerializeField] private bool marksABossSpawn;

        [Tooltip("Which boss appears there.")]
        [SerializeField] private string bossThatAppearsId = string.Empty;

        [Header("Leaving ground behind a blast")]
        [Tooltip("An explosion here lays ground down.")]
        [SerializeField] private bool blastLaysGround;

        [Tooltip("Which ground it lays.")]
        [SerializeField] private string blastGroundTilesetId = string.Empty;

        [Tooltip("Which kind of tile.")]
        [SerializeField] private PugTilemap.TileType blastGroundKind = PugTilemap.TileType.ground;

        [Tooltip("How long that ground lasts. 0 for permanent.")]
        [Min(0f)]
        [SerializeField] private float blastGroundSeconds;

        [Tooltip("It only lays ground where something could already walk.")]
        [SerializeField] private bool blastNeedsWalkableGround = true;

        public bool HoldsMana { get { return holdsMana; } }

        public int StartingMana { get { return startingMana < 0 ? 0 : startingMana; } }

        public int MaximumMana
        {
            get
            {
                int most = maximumMana < 0 ? 0 : maximumMana;
                return most < StartingMana ? StartingMana : most;
            }
        }

        public float RefillRate { get { return Floor(refillRate); } }

        public float RefillDelay { get { return Floor(refillDelay); } }

        public bool SiphonsMana { get { return siphonsMana; } }

        public float SiphonRate { get { return Floor(siphonRate); } }

        public float SiphonCooldown { get { return Floor(siphonCooldown); } }

        public float SiphonReach { get { return Floor(siphonReach); } }

        public float SiphonRadius { get { return Floor(siphonRadius); } }

        public bool HealsWhatIsNear { get { return healsWhatIsNear; } }

        public bool HealingStartsOn { get { return healingStartsOn; } }

        public string HealsFaction { get { return healsFaction ?? string.Empty; } }

        public int HealthPerSecond { get { return healthPerSecond < 0 ? 0 : healthPerSecond; } }

        public float HealRadius { get { return Floor(healRadius); } }

        public bool IsAncientWiring { get { return isAncientWiring; } }

        public int CarriesPower { get { return carriesPower < 0 ? 0 : carriesPower; } }

        public int ProducesPower { get { return producesPower < 0 ? 0 : producesPower; } }

        public bool BlocksTheFlow { get { return blocksTheFlow; } }

        public bool IsPartOfSomethingElse { get { return isPartOfSomethingElse; } }

        public GameObject BelongsTo { get { return belongsTo; } }

        public GameObject OwnedBy { get { return ownedBy; } }

        public bool IsHydraBait { get { return isHydraBait; } }

        public DimensionHydraKind AttractsHydra { get { return attractsHydra; } }

        public bool MarksABossSpawn { get { return marksABossSpawn; } }

        public string BossThatAppearsId { get { return bossThatAppearsId ?? string.Empty; } }

        public bool BlastLaysGround { get { return blastLaysGround; } }

        public string BlastGroundTilesetId
        {
            get { return blastGroundTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType BlastGroundKind { get { return blastGroundKind; } }

        public float BlastGroundSeconds { get { return Floor(blastGroundSeconds); } }

        public bool BlastNeedsWalkableGround { get { return blastNeedsWalkableGround; } }

        /// <summary>Whether it siphons mana with no reach to siphon across.</summary>
        public bool SiphonsFromNowhere
        {
            get { return siphonsMana && siphonReach <= 0f && siphonRadius <= 0f; }
        }

        /// <summary>Whether it heals for nothing per second.</summary>
        public bool HealsNothing
        {
            get { return healsWhatIsNear && healthPerSecond <= 0; }
        }

        /// <summary>
        /// Whether it is ancient wiring that neither carries nor produces power.
        /// </summary>
        /// <remarks>
        /// A blocker is a legitimate use of exactly that — a piece whose whole job is to stop the
        /// flow — so this is only worth saying when it does not block either.
        /// </remarks>
        public bool AncientWiringDoesNothing
        {
            get
            {
                return isAncientWiring && carriesPower <= 0 && producesPower <= 0 && !blocksTheFlow;
            }
        }

        /// <summary>Whether it marks a boss spawn without naming a boss.</summary>
        public bool MarksNoBoss
        {
            get { return marksABossSpawn && string.IsNullOrEmpty(BossThatAppearsId); }
        }

        /// <summary>Whether a blast is told to lay ground without naming any.</summary>
        public bool BlastGroundIsMissing
        {
            get { return blastLaysGround && string.IsNullOrEmpty(BlastGroundTilesetId); }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }
    }
}
