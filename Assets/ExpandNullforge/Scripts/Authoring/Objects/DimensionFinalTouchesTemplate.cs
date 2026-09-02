using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The last small behaviours an object can carry, and the beams the game's bosses fire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE HURT-CHANCE CURVE IS THE MOST INTERESTING THING LEFT. A creature can be given a chance to
    /// apply a condition to ITSELF when hurt — and that chance is a curve read against how much
    /// health it has left, so a thing can panic only when it is nearly dead. That is a whole
    /// behaviour shape with no other way to express it.
    /// </para>
    /// <para>
    /// SITTING IS A SLOT WITH FOUR OFFSETS, one per facing, because a chair seats you differently
    /// depending which way it points. That is why a bench that looks right from the front sits you
    /// inside it from the side unless all four are given.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionFinalTouchesTemplate
    {
        [Header("Somewhere to sit or stand")]
        [Tooltip("Something can occupy it — sit on it, stand in it, be held by it.")]
        [SerializeField] private bool canBeOccupied;

        [Tooltip("Where the occupant sits when it faces forward.")]
        [SerializeField] private Vector3 occupantForward = Vector3.zero;

        [Tooltip("Facing right.")]
        [SerializeField] private Vector3 occupantRight = Vector3.zero;

        [Tooltip("Facing back.")]
        [SerializeField] private Vector3 occupantBack = Vector3.zero;

        [Tooltip("Facing left.")]
        [SerializeField] private Vector3 occupantLeft = Vector3.zero;

        [Header("Reacting to its own wounds")]
        [Tooltip("Being hurt can apply a condition to itself — panic, rage, a defensive shell.")]
        [SerializeField] private bool reactsToItsOwnWounds;

        [Tooltip("Which condition, by the game's own name.")]
        [SerializeField] private string reactionConditionId = string.Empty;

        [Tooltip("How long it lasts.")]
        [Min(0f)]
        [SerializeField] private float reactionSeconds = 5f;

        [Tooltip("How strong it is.")]
        [SerializeField] private int reactionStrength = 1;

        [Tooltip("How likely it is, read against how much health is left. Right of the curve is full health.")]
        [SerializeField] private AnimationCurve reactionChanceByHealth =
            AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Odds and ends")]
        [Tooltip("It counts as destructible only while its health is above this share.")]
        [Range(0f, 1f)]
        [SerializeField] private float destructibleAboveHealth;

        [Tooltip("It is destroyed when whatever owns it is.")]
        [SerializeField] private bool diesWithItsOwner;

        [Tooltip("The thing it dies with.")]
        [SerializeField] private GameObject diesWith;

        [Tooltip("Pheromones it gives off, by the game's own names.")]
        [SerializeField] private string[] givesOffPheromones = new string[0];

        [Tooltip("A condition shown as a bar while it is worn. Blank for none.")]
        [SerializeField] private string showsConditionAsBar = string.Empty;

        [Tooltip("A world event it sets off when it dies. Blank for none.")]
        [SerializeField] private string triggersEventOnDeath = string.Empty;

        [Tooltip("How smoothly its visual follows it. 0 leaves the game's own.")]
        [Min(0f)]
        [SerializeField] private float visualFollowSpeed;

        [Tooltip("How long it takes to warm up before it works. 0 for instant.")]
        [Min(0f)]
        [SerializeField] private float warmUpSeconds;

        [Header("A boss beam")]
        [Tooltip("It is a boss beam — the sweeping kind the Core and the Bird fire.")]
        [SerializeField] private bool isABossBeam;

        [Tooltip("How long it takes to appear.")]
        [Min(0f)]
        [SerializeField] private float beamStartSeconds = 0.5f;

        [Tooltip("How long it holds.")]
        [Min(0f)]
        [SerializeField] private float beamHoldSeconds = 3f;

        [Tooltip("How long it takes to fade.")]
        [Min(0f)]
        [SerializeField] private float beamEndSeconds = 0.5f;

        [Tooltip("How long it stays gone afterwards.")]
        [Min(0f)]
        [SerializeField] private float beamHiddenSeconds = 1f;

        [Tooltip("How long after appearing before it starts hurting. Bird beam only.")]
        [Min(0f)]
        [SerializeField] private float beamHarmlessFor;

        [Tooltip("Which way the beam travels. Bird beam only.")]
        [SerializeField] private Vector3 beamTravelDirection = Vector3.zero;

        [Tooltip("How fast it travels. Bird beam only.")]
        [Min(0f)]
        [SerializeField] private float beamTravelSpeed;

        [Tooltip("It sweeps sideways rather than forward. Bird beam only.")]
        [SerializeField] private bool beamSweepsSideways;

        [Header("A boss spawn point")]
        [Tooltip("It is a spawn point the Core uses to bring things in.")]
        [SerializeField] private bool isABossSpawnPoint;

        [Tooltip("How close a player has to be for it to wake.")]
        [Min(0f)]
        [SerializeField] private float wakesWithin = 30f;

        [Tooltip("How close before it actually spawns.")]
        [Min(0f)]
        [SerializeField] private float spawnsWithin = 5f;

        [Tooltip("How long the spawn takes.")]
        [Min(0f)]
        [SerializeField] private float spawnSeconds = 5f;

        [Tooltip("How long it takes to break down afterwards.")]
        [Min(0f)]
        [SerializeField] private float breakDownSeconds = 0.5f;

        [Tooltip("How high above the ground the spawn appears.")]
        [SerializeField] private float spawnHeight = 0.75f;

        [Header("A hatching egg in a hive")]
        [Tooltip("It is a hive egg — it opens on its own rather than when a player comes near.")]
        [SerializeField] private bool isAHiveEgg;

        [Tooltip("How long the change into hatching takes.")]
        [Min(0f)]
        [SerializeField] private float hiveEggChangeSeconds = 1f;

        [Tooltip("How long the hatch itself takes.")]
        [Min(0f)]
        [SerializeField] private float hiveEggHatchSeconds = 2f;

        public bool CanBeOccupied { get { return canBeOccupied; } }

        public Vector3 OccupantForward { get { return occupantForward; } }

        public Vector3 OccupantRight { get { return occupantRight; } }

        public Vector3 OccupantBack { get { return occupantBack; } }

        public Vector3 OccupantLeft { get { return occupantLeft; } }

        public bool ReactsToItsOwnWounds { get { return reactsToItsOwnWounds; } }

        public string ReactionConditionId { get { return reactionConditionId ?? string.Empty; } }

        public float ReactionSeconds { get { return Floor(reactionSeconds); } }

        public int ReactionStrength { get { return reactionStrength; } }

        public AnimationCurve ReactionChanceByHealth { get { return reactionChanceByHealth; } }

        public float DestructibleAboveHealth { get { return Clamp01(destructibleAboveHealth); } }

        public bool DiesWithItsOwner { get { return diesWithItsOwner; } }

        public GameObject DiesWith { get { return diesWith; } }

        public string[] GivesOffPheromones
        {
            get { return givesOffPheromones ?? new string[0]; }
        }

        public string ShowsConditionAsBar { get { return showsConditionAsBar ?? string.Empty; } }

        public string TriggersEventOnDeath
        {
            get { return triggersEventOnDeath ?? string.Empty; }
        }

        public float VisualFollowSpeed { get { return Floor(visualFollowSpeed); } }

        public float WarmUpSeconds { get { return Floor(warmUpSeconds); } }

        public bool IsABossBeam { get { return isABossBeam; } }

        public float BeamStartSeconds { get { return Floor(beamStartSeconds); } }

        public float BeamHoldSeconds { get { return Floor(beamHoldSeconds); } }

        public float BeamEndSeconds { get { return Floor(beamEndSeconds); } }

        public float BeamHiddenSeconds { get { return Floor(beamHiddenSeconds); } }

        public float BeamHarmlessFor { get { return Floor(beamHarmlessFor); } }

        public Vector3 BeamTravelDirection { get { return beamTravelDirection; } }

        public float BeamTravelSpeed { get { return Floor(beamTravelSpeed); } }

        public bool BeamSweepsSideways { get { return beamSweepsSideways; } }

        public bool IsABossSpawnPoint { get { return isABossSpawnPoint; } }

        public float WakesWithin { get { return Floor(wakesWithin); } }

        public float SpawnsWithin { get { return Floor(spawnsWithin); } }

        public float SpawnSeconds { get { return Floor(spawnSeconds); } }

        public float BreakDownSeconds { get { return Floor(breakDownSeconds); } }

        public float SpawnHeight { get { return spawnHeight; } }

        public bool IsAHiveEgg { get { return isAHiveEgg; } }

        public float HiveEggChangeSeconds { get { return Floor(hiveEggChangeSeconds); } }

        public float HiveEggHatchSeconds { get { return Floor(hiveEggHatchSeconds); } }

        /// <summary>Whether it can be occupied with every seat at the same spot.</summary>
        /// <remarks>
        /// All four offsets at zero seats the occupant dead centre whichever way it faces, which for
        /// anything wider than one tile puts them inside it.
        /// </remarks>
        public bool EverySeatIsTheSameSpot
        {
            get
            {
                return canBeOccupied
                    && occupantForward == Vector3.zero
                    && occupantRight == Vector3.zero
                    && occupantBack == Vector3.zero
                    && occupantLeft == Vector3.zero;
            }
        }

        /// <summary>Whether it reacts to its own wounds with no condition named.</summary>
        public bool ReactsWithNothing
        {
            get { return reactsToItsOwnWounds && string.IsNullOrEmpty(ReactionConditionId); }
        }

        /// <summary>Whether it wakes closer than it spawns, so it can never wake first.</summary>
        public bool WakesCloserThanItSpawns
        {
            get { return isABossSpawnPoint && wakesWithin < spawnsWithin; }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
