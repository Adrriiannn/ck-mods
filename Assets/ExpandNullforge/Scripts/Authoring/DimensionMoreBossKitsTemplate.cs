using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The rest of the borrowable boss kits: the Bird, the Robot, the Octopus, the Larva, the
    /// Shaman and the Snake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six more fights a custom boss can take on whole. Between this and the other two kit files,
    /// every named boss in Core Keeper is now something a modder can borrow from — which was the
    /// point: a custom boss is usually a new sprite on a fight that already works, not an invention
    /// from nothing.
    /// </para>
    /// <para>
    /// THE ROBOT IS THE ODD ONE OUT AND THE MOST INTERESTING. It does not have attacks so much as
    /// LEGS: the whole kit is how far a leg reaches, how high it steps, how long a broken one stays
    /// broken, and how many attacks it chains. Breaking its legs is the fight.
    /// </para>
    /// <para>
    /// THE SPAWN CONFIGURATION REPEATS. Bird, Octopus and Core all use the same four-value shape for
    /// summoning something — a wind-up, a recovery and a cooldown range — because a summon is the
    /// same idea wherever it appears.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMoreBossKitsTemplate
    {
        [Header("The Bird's kit — landing and stones")]
        [Tooltip("It fights like the Bird: landing between passes and dropping stones. The game asks what the falling stone is, not what the bird is, so point the stone below at Core Keeper's own BirdBossStone for that part to land.")]
        [SerializeField] private bool fightsLikeTheBird;

        [Tooltip("How long it stays on the ground.")]
        [Min(0f)]
        [SerializeField] private float birdLandSeconds = 2f;

        [Tooltip("How long before it starts dropping stones.")]
        [Min(0f)]
        [SerializeField] private float birdSecondsBeforeStones;

        [Tooltip("How long before it stops dropping them.")]
        [Min(0f)]
        [SerializeField] private float birdSecondsBeforeStopping = 3f;

        [Tooltip("Wind-up before its beam.")]
        [Min(0f)]
        [SerializeField] private float birdBeamWindUp;

        [Tooltip("Recovery after its beam.")]
        [Min(0f)]
        [SerializeField] private float birdBeamRecovery;

        [Tooltip("Shortest wait between beams.")]
        [Min(0f)]
        [SerializeField] private float birdBeamMinCooldown = 5f;

        [Tooltip("Longest wait between beams.")]
        [Min(0f)]
        [SerializeField] private float birdBeamMaxCooldown = 9f;

        [Tooltip("Wind-up before a stone drop.")]
        [Min(0f)]
        [SerializeField] private float birdStoneWindUp;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float birdStoneRecovery;

        [Tooltip("Shortest wait between stone drops.")]
        [Min(0f)]
        [SerializeField] private float birdStoneMinCooldown = 4f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float birdStoneMaxCooldown = 8f;

        [Header("The Robot's kit — legs you break")]
        [Tooltip("It fights like the Robot: it walks on legs, and breaking them is the fight.")]
        [SerializeField] private bool fightsLikeTheRobot;

        [Tooltip("How long a broken leg stays broken, in seconds.")]
        [Min(0)]
        [SerializeField] private int brokenLegSeconds = 20;

        [Tooltip("How far out to the side its legs sit.")]
        [Min(0f)]
        [SerializeField] private float legSideOffset = 4f;

        [Tooltip("How far forward and back its legs sit.")]
        [Min(0f)]
        [SerializeField] private float legDepthOffset = 4f;

        [Tooltip("How high it lifts a leg as it steps.")]
        [Min(0f)]
        [SerializeField] private float stepHeight = 0.5f;

        [Tooltip("How sharply that lift peaks.")]
        [Min(0f)]
        [SerializeField] private float stepHeightCurve = 1f;

        [Tooltip("How far it must move before a leg follows.")]
        [Min(0f)]
        [SerializeField] private float distanceBeforeALegMoves = 1.5f;

        [Tooltip("How fast a leg swings.")]
        [Min(0f)]
        [SerializeField] private float legSpeed = 1f;

        [Tooltip("How far a leg reaches forward with each step.")]
        [Min(0f)]
        [SerializeField] private float stepLength = 0.8f;

        [Tooltip("Where a leg starts from.")]
        [Min(0f)]
        [SerializeField] private float legStartDistance = 0.5f;

        [Tooltip("How long a leg rests between steps.")]
        [Min(0f)]
        [SerializeField] private float legStepCooldown = 0.5f;

        [Tooltip("How many attacks it strings together in a row.")]
        [Min(0)]
        [SerializeField] private int attacksInAChain = 6;

        [Tooltip("How long between the attacks in that chain.")]
        [Min(0f)]
        [SerializeField] private float delayBetweenChainedAttacks = 1f;

        [Header("The Octopus's kit — tentacles")]
        [Tooltip("It fights like the Octopus: it surfaces and sends up tentacles. The game asks what the tentacle is, not what the octopus is, so use Core Keeper's own OctopusTentacle for the tentacles to behave.")]
        [SerializeField] private bool fightsLikeTheOctopus;

        [Tooltip("How long it takes to appear.")]
        [Min(0f)]
        [SerializeField] private float octopusAppearSeconds = 1f;

        [Tooltip("How long before it starts sending tentacles.")]
        [Min(0f)]
        [SerializeField] private float octopusSecondsBeforeTentacles;

        [Tooltip("How long before it stops.")]
        [Min(0f)]
        [SerializeField] private float octopusSecondsBeforeStopping = 3f;

        [Tooltip("Wind-up before a tentacle comes up.")]
        [Min(0f)]
        [SerializeField] private float tentacleWindUp;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float tentacleRecovery;

        [Tooltip("Shortest wait between tentacles.")]
        [Min(0f)]
        [SerializeField] private float tentacleMinCooldown = 3f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float tentacleMaxCooldown = 6f;

        [Tooltip("It is one of the spots the Octopus surfaces at, rather than the Octopus.")]
        [SerializeField] private bool isAnOctopusSurfacingSpot;

        [Header("The Larva's kit — a roaming grub")]
        [Tooltip("It fights like the boss Larva: a segmented grub that roams its territory.")]
        [SerializeField] private bool fightsLikeTheLarva;

        [Tooltip("How far it roams from home.")]
        [Min(0)]
        [SerializeField] private int larvaRoamDistance = 30;

        [Tooltip("How much that distance wanders.")]
        [Min(0)]
        [SerializeField] private int larvaRoamVariation = 10;

        [Tooltip("Flat contact damage.")]
        [Min(0)]
        [SerializeField] private int larvaDamage;

        [Tooltip("How hard it hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float larvaMultiplier = 1f;

        [Tooltip("The small body segment it is built from.")]
        [SerializeField] private GameObject larvaSmallSegment;

        [Tooltip("The medium body segment.")]
        [SerializeField] private GameObject larvaMediumSegment;

        [Tooltip("The large body segment.")]
        [SerializeField] private GameObject larvaLargeSegment;

        [Tooltip("It arrives the way the boss Larva does — a spawning entrance before it starts fighting.")]
        [SerializeField] private bool arrivesLikeTheBossLarva;

        [Header("The Shaman's kit — a phase change")]
        [Tooltip("It fights like the Shaman: one clean phase change partway down.")]
        [SerializeField] private bool fightsLikeTheShaman;

        [Tooltip("Health share where it changes gear. 0.5 is halfway, as vanilla uses.")]
        [Range(0f, 1f)]
        [SerializeField] private float shamanPhaseAtHealth = 0.5f;

        [Tooltip("How long that change takes.")]
        [Min(0f)]
        [SerializeField] private float shamanPhaseSeconds = 1f;

        [Tooltip("How long it cannot be hurt while changing.")]
        [Min(0f)]
        [SerializeField] private float shamanUntouchableSeconds = 1f;

        [Header("The Snake's kit")]
        [Tooltip("It fights like the boss Snake. The game asks what each body piece is, not what the head is, so use Core Keeper's own SnakeBossSegment for the body to follow properly.")]
        [SerializeField] private bool fightsLikeTheSnake;

        [Tooltip("How long after it dies before its defeat sound plays.")]
        [Min(0f)]
        [SerializeField] private float snakeDefeatSoundDelay = 2f;

        public bool FightsLikeTheBird { get { return fightsLikeTheBird; } }

        public float BirdLandSeconds { get { return Floor(birdLandSeconds); } }

        public float BirdSecondsBeforeStones { get { return Floor(birdSecondsBeforeStones); } }

        public float BirdSecondsBeforeStopping
        {
            get { return Floor(birdSecondsBeforeStopping); }
        }

        public float BirdBeamWindUp { get { return Floor(birdBeamWindUp); } }

        public float BirdBeamRecovery { get { return Floor(birdBeamRecovery); } }

        public float BirdBeamMinCooldown { get { return Floor(birdBeamMinCooldown); } }

        public float BirdBeamMaxCooldown
        {
            get { return AtLeast(birdBeamMaxCooldown, BirdBeamMinCooldown); }
        }

        public float BirdStoneWindUp { get { return Floor(birdStoneWindUp); } }

        public float BirdStoneRecovery { get { return Floor(birdStoneRecovery); } }

        public float BirdStoneMinCooldown { get { return Floor(birdStoneMinCooldown); } }

        public float BirdStoneMaxCooldown
        {
            get { return AtLeast(birdStoneMaxCooldown, BirdStoneMinCooldown); }
        }

        public bool FightsLikeTheRobot { get { return fightsLikeTheRobot; } }

        public int BrokenLegSeconds { get { return brokenLegSeconds < 0 ? 0 : brokenLegSeconds; } }

        public float LegSideOffset { get { return Floor(legSideOffset); } }

        public float LegDepthOffset { get { return Floor(legDepthOffset); } }

        public float StepHeight { get { return Floor(stepHeight); } }

        public float StepHeightCurve { get { return Floor(stepHeightCurve); } }

        public float DistanceBeforeALegMoves { get { return Floor(distanceBeforeALegMoves); } }

        public float LegSpeed { get { return Floor(legSpeed); } }

        public float StepLength { get { return Floor(stepLength); } }

        public float LegStartDistance { get { return Floor(legStartDistance); } }

        public float LegStepCooldown { get { return Floor(legStepCooldown); } }

        public int AttacksInAChain { get { return attacksInAChain < 0 ? 0 : attacksInAChain; } }

        public float DelayBetweenChainedAttacks
        {
            get { return Floor(delayBetweenChainedAttacks); }
        }

        public bool FightsLikeTheOctopus { get { return fightsLikeTheOctopus; } }

        public float OctopusAppearSeconds { get { return Floor(octopusAppearSeconds); } }

        public float OctopusSecondsBeforeTentacles
        {
            get { return Floor(octopusSecondsBeforeTentacles); }
        }

        public float OctopusSecondsBeforeStopping
        {
            get { return Floor(octopusSecondsBeforeStopping); }
        }

        public float TentacleWindUp { get { return Floor(tentacleWindUp); } }

        public float TentacleRecovery { get { return Floor(tentacleRecovery); } }

        public float TentacleMinCooldown { get { return Floor(tentacleMinCooldown); } }

        public float TentacleMaxCooldown
        {
            get { return AtLeast(tentacleMaxCooldown, TentacleMinCooldown); }
        }

        public bool IsAnOctopusSurfacingSpot { get { return isAnOctopusSurfacingSpot; } }

        public bool ArrivesLikeTheBossLarva { get { return arrivesLikeTheBossLarva; } }

        public bool FightsLikeTheLarva { get { return fightsLikeTheLarva; } }

        public int LarvaRoamDistance
        {
            get { return larvaRoamDistance < 0 ? 0 : larvaRoamDistance; }
        }

        public int LarvaRoamVariation
        {
            get { return larvaRoamVariation < 0 ? 0 : larvaRoamVariation; }
        }

        public int LarvaDamage { get { return larvaDamage < 0 ? 0 : larvaDamage; } }

        public float LarvaMultiplier { get { return Floor(larvaMultiplier); } }

        public GameObject LarvaSmallSegment { get { return larvaSmallSegment; } }

        public GameObject LarvaMediumSegment { get { return larvaMediumSegment; } }

        public GameObject LarvaLargeSegment { get { return larvaLargeSegment; } }

        public bool FightsLikeTheShaman { get { return fightsLikeTheShaman; } }

        public float ShamanPhaseAtHealth { get { return Clamp01(shamanPhaseAtHealth); } }

        public float ShamanPhaseSeconds { get { return Floor(shamanPhaseSeconds); } }

        public float ShamanUntouchableSeconds
        {
            get { return Floor(shamanUntouchableSeconds); }
        }

        public bool FightsLikeTheSnake { get { return fightsLikeTheSnake; } }

        public float SnakeDefeatSoundDelay { get { return Floor(snakeDefeatSoundDelay); } }

        /// <summary>
        /// Whether the Larva kit was taken without giving it a body to be made of.
        /// </summary>
        /// <remarks>
        /// The boss Larva is built from three segment prefabs. Without them the kit produces a head
        /// with nothing behind it.
        /// </remarks>
        public bool LarvaHasNoBody
        {
            get
            {
                return fightsLikeTheLarva
                    && larvaSmallSegment == null
                    && larvaMediumSegment == null
                    && larvaLargeSegment == null;
            }
        }

        /// <summary>Whether the Bird stops dropping stones before it starts.</summary>
        public bool BirdStopsBeforeItStarts
        {
            get
            {
                return fightsLikeTheBird
                    && birdSecondsBeforeStopping > 0f
                    && birdSecondsBeforeStopping < birdSecondsBeforeStones;
            }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float AtLeast(float value, float floor)
        {
            float clamped = Floor(value);
            return clamped < floor ? floor : clamped;
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
