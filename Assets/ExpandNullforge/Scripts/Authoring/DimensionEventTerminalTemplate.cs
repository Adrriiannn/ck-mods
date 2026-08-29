using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// An event terminal that runs a timed sequence, the Cicada's kit, and the last few odds and
    /// ends the framework had not reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AN EVENT TERMINAL IS A SCRIPTED SEQUENCE, which is the only thing of its kind in the game. It
    /// runs down a list of steps — switch this connection on, hold, switch it off — each with its
    /// own duration, and can loop back to a step partway through. That is a puzzle written as a
    /// timeline rather than as wiring, and nothing else in Core Keeper offers it.
    /// </para>
    /// <para>
    /// THE CICADA STAGES DOWN RATHER THAN PHASING. It has a number of stages and a multiplier for
    /// the weakest one; every stage between is interpolated. So a five-stage Cicada with a 0.3
    /// floor gets steadily weaker as it loses stages, which is a different fight shape from a boss
    /// that flips gear at half health.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionEventTerminalTemplate
    {
        [Header("An event terminal")]
        [Tooltip("It runs a timed sequence of switches — a puzzle written as a timeline.")]
        [SerializeField] private bool isAnEventTerminal;

        [Tooltip("How far its influence reaches.")]
        [Min(0f)]
        [SerializeField] private float reaches = 10f;

        [Tooltip("How long the whole event runs before giving up.")]
        [Min(0f)]
        [SerializeField] private float runsFor = 60f;

        [Tooltip("A loot table it rolls when the event is completed. Blank for none.")]
        [SerializeField] private string rewardTableId = string.Empty;

        [Tooltip("Which step it loops back to. 0 restarts from the beginning.")]
        [Min(0)]
        [SerializeField] private int loopsBackToStep;

        [Tooltip("Connections that stay on for the whole event, by the game's own names.")]
        [SerializeField] private string[] alwaysOnConnections = new string[0];

        [Tooltip("The steps it runs through, in order.")]
        [SerializeField] private DimensionTerminalStep[] steps = new DimensionTerminalStep[0];

        [Header("The Cicada's kit — stages that wear down")]
        [Tooltip("It fights like the Giant Cicada: stages that weaken as they are stripped away.")]
        [SerializeField] private bool fightsLikeTheCicada;

        [Tooltip("How many stages it has.")]
        [Min(1)]
        [SerializeField] private int stages = 5;

        [Tooltip("How weak its last stage is, as a share of the first. 0.3 in vanilla.")]
        [Range(0f, 1f)]
        [SerializeField] private float weakestStage = 0.3f;

        [Tooltip("How long changing stage takes.")]
        [Min(0f)]
        [SerializeField] private float stageChangeSeconds = 3f;

        [Tooltip("Flat damage from its arm slam.")]
        [Min(0)]
        [SerializeField] private int armSlamDamage = 500;

        [Tooltip("How hard the slam hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float armSlamMultiplier = 1f;

        [Tooltip("Wind-up before the slam.")]
        [Min(0f)]
        [SerializeField] private float armSlamWindUp = 1f;

        [Tooltip("How long the slam animation runs.")]
        [Min(0f)]
        [SerializeField] private float armSlamSeconds = 5f;

        [Tooltip("How long between slams.")]
        [Min(0f)]
        [SerializeField] private float armSlamCooldown = 7f;

        [Tooltip("How long it takes to release nymphs.")]
        [Min(0f)]
        [SerializeField] private float nymphSpawnSeconds = 9f;

        [Tooltip("Shortest wait between nymph releases.")]
        [Min(0f)]
        [SerializeField] private float nymphMinCooldown = 12f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float nymphMaxCooldown = 16f;

        [Tooltip("It also summons void creatures.")]
        [SerializeField] private bool cicadaSummonsVoid = true;

        [Tooltip("How long that summon lasts.")]
        [Min(0f)]
        [SerializeField] private float cicadaVoidSeconds = 1f;

        [Tooltip("Wind-up before it.")]
        [Min(0f)]
        [SerializeField] private float cicadaVoidWindUp;

        [Tooltip("Recovery after it.")]
        [Min(0f)]
        [SerializeField] private float cicadaVoidRecovery;

        [Tooltip("Shortest wait between void summons.")]
        [Min(0f)]
        [SerializeField] private float cicadaVoidMinCooldown = 8f;

        [Tooltip("Longest wait between them.")]
        [Min(0f)]
        [SerializeField] private float cicadaVoidMaxCooldown = 14f;

        [Tooltip("It is a Cicada nymph — one of the small ones the boss releases.")]
        [SerializeField] private bool isACicadaNymph;

        [Header("A tank or terrarium")]
        [Tooltip("It holds a little contained world — a tank of fish, a terrarium of critters.")]
        [SerializeField] private bool holdsAMiniWorld;

        [Tooltip("How many things live in it at once.")]
        [Min(0)]
        [SerializeField] private int miniWorldPopulation = 3;

        [Tooltip("What lives in it — the fish or critter prefab it is populated with.")]
        [SerializeField] private ContainedMiniSim.Authoring.ContainedMiniSimElementAuthoring miniWorldInhabitant;

        [Tooltip("Narrowest and widest that space is.")]
        [SerializeField] private Vector2 miniWorldWidth = new Vector2(1f, 2f);

        [Tooltip("Shallowest and deepest.")]
        [SerializeField] private Vector2 miniWorldHeight = new Vector2(1f, 2f);

        [Tooltip("Shortest and longest.")]
        [SerializeField] private Vector2 miniWorldLength = new Vector2(1f, 2f);

        [Header("A fishing net")]
        [Tooltip("It is a fishing net, with splashes as it fills.")]
        [SerializeField] private bool isAFishingNet;

        [Tooltip("Shortest and longest between splashes with one fish in it.")]
        [SerializeField] private Vector2 splashTimerOneFish = new Vector2(2f, 5f);

        [Tooltip("And when the net is full.")]
        [SerializeField] private Vector2 splashTimerFull = new Vector2(0.5f, 1.5f);

        [Tooltip("Where each caught fish shows in the net.")]
        [SerializeField] private Vector2[] fishPositions = new Vector2[0];

        [Header("A nature caveling territory")]
        [Tooltip("It claims a nature caveling territory of this size. 0 for none.")]
        [Min(0)]
        [SerializeField] private int natureTerritorySize;

        [Tooltip("Chance a farmer appears in it.")]
        [Range(0f, 1f)]
        [SerializeField] private float farmerChance;

        [Tooltip("Chance a hunter appears.")]
        [Range(0f, 1f)]
        [SerializeField] private float hunterChance;

        [Header("Farming machines")]
        [Tooltip("It plants seeds and moves them along, as an automated planter does.")]
        [SerializeField] private bool isAnAutomatedPlanter;

        [Tooltip("It harvests crops and moves them along.")]
        [SerializeField] private bool isAnAutomatedHarvester;

        [Tooltip("Which tiles it works on, and which way it moves what it finds there.")]
        [SerializeField] private DimensionFarmReach[] worksOn = new DimensionFarmReach[0];

        [Header("Odds and ends")]
        [Tooltip("It spreads water to the ground around it.")]
        [SerializeField] private bool spreadsWater;

        [Tooltip("It is a recipe on its own rather than an ingredient list on an item.")]
        [SerializeField] private bool isAStandaloneRecipe;

        [Tooltip("What that recipe makes.")]
        [SerializeField] private string recipeMakesId = string.Empty;

        [Tooltip("Which look of it.")]
        [Min(0)]
        [SerializeField] private int recipeMakesVariation;

        [Tooltip("How many it makes.")]
        [Min(0)]
        [SerializeField] private int recipeMakesAmount = 1;

        [Tooltip("An object that must be standing nearby to use the recipe. Blank for none.")]
        [SerializeField] private string recipeNeedsNearbyId = string.Empty;

        [Tooltip("How the placement indicator moves as it is dragged about.")]
        [SerializeField] private AnimationCurve placementIndicatorSpeed =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("It uses that placement-indicator curve rather than the game's own.")]
        [SerializeField] private bool usesItsOwnPlacementIndicator;

        public bool IsAnEventTerminal { get { return isAnEventTerminal; } }

        public float Reaches { get { return Floor(reaches); } }

        public float RunsFor { get { return Floor(runsFor); } }

        public string RewardTableId { get { return rewardTableId ?? string.Empty; } }

        public int LoopsBackToStep { get { return loopsBackToStep < 0 ? 0 : loopsBackToStep; } }

        public string[] AlwaysOnConnections
        {
            get { return alwaysOnConnections ?? new string[0]; }
        }

        public DimensionTerminalStep[] Steps
        {
            get { return steps ?? new DimensionTerminalStep[0]; }
        }

        public bool FightsLikeTheCicada { get { return fightsLikeTheCicada; } }

        public int Stages { get { return stages < 1 ? 1 : stages; } }

        public float WeakestStage { get { return Clamp01(weakestStage); } }

        public float StageChangeSeconds { get { return Floor(stageChangeSeconds); } }

        public int ArmSlamDamage { get { return armSlamDamage < 0 ? 0 : armSlamDamage; } }

        public float ArmSlamMultiplier { get { return Floor(armSlamMultiplier); } }

        public float ArmSlamWindUp { get { return Floor(armSlamWindUp); } }

        public float ArmSlamSeconds { get { return Floor(armSlamSeconds); } }

        public float ArmSlamCooldown { get { return Floor(armSlamCooldown); } }

        public float NymphSpawnSeconds { get { return Floor(nymphSpawnSeconds); } }

        public float NymphMinCooldown { get { return Floor(nymphMinCooldown); } }

        public float NymphMaxCooldown { get { return AtLeast(nymphMaxCooldown, NymphMinCooldown); } }

        public bool CicadaSummonsVoid { get { return cicadaSummonsVoid; } }

        public float CicadaVoidSeconds { get { return Floor(cicadaVoidSeconds); } }

        public float CicadaVoidWindUp { get { return Floor(cicadaVoidWindUp); } }

        public float CicadaVoidRecovery { get { return Floor(cicadaVoidRecovery); } }

        public float CicadaVoidMinCooldown { get { return Floor(cicadaVoidMinCooldown); } }

        public float CicadaVoidMaxCooldown
        {
            get { return AtLeast(cicadaVoidMaxCooldown, CicadaVoidMinCooldown); }
        }

        public bool IsACicadaNymph { get { return isACicadaNymph; } }

        public bool HoldsAMiniWorld { get { return holdsAMiniWorld; } }

        public int MiniWorldPopulation
        {
            get { return miniWorldPopulation < 0 ? 0 : miniWorldPopulation; }
        }

        public ContainedMiniSim.Authoring.ContainedMiniSimElementAuthoring MiniWorldInhabitant
        {
            get { return miniWorldInhabitant; }
        }

        public Vector2 MiniWorldWidth { get { return Ordered(miniWorldWidth); } }

        public Vector2 MiniWorldHeight { get { return Ordered(miniWorldHeight); } }

        public Vector2 MiniWorldLength { get { return Ordered(miniWorldLength); } }

        public bool IsAFishingNet { get { return isAFishingNet; } }

        public Vector2 SplashTimerOneFish { get { return Ordered(splashTimerOneFish); } }

        public Vector2 SplashTimerFull { get { return Ordered(splashTimerFull); } }

        public Vector2[] FishPositions { get { return fishPositions ?? new Vector2[0]; } }

        public int NatureTerritorySize
        {
            get { return natureTerritorySize < 0 ? 0 : natureTerritorySize; }
        }

        public float FarmerChance { get { return Clamp01(farmerChance); } }

        public float HunterChance { get { return Clamp01(hunterChance); } }

        public bool IsAnAutomatedPlanter { get { return isAnAutomatedPlanter; } }

        public bool IsAnAutomatedHarvester { get { return isAnAutomatedHarvester; } }

        public DimensionFarmReach[] WorksOn
        {
            get { return worksOn ?? new DimensionFarmReach[0]; }
        }

        public bool SpreadsWater { get { return spreadsWater; } }

        public bool IsAStandaloneRecipe { get { return isAStandaloneRecipe; } }

        public string RecipeMakesId { get { return recipeMakesId ?? string.Empty; } }

        public int RecipeMakesVariation
        {
            get { return recipeMakesVariation < 0 ? 0 : recipeMakesVariation; }
        }

        public int RecipeMakesAmount
        {
            get { return recipeMakesAmount < 0 ? 0 : recipeMakesAmount; }
        }

        public string RecipeNeedsNearbyId
        {
            get { return recipeNeedsNearbyId ?? string.Empty; }
        }

        public AnimationCurve PlacementIndicatorSpeed { get { return placementIndicatorSpeed; } }

        public bool UsesItsOwnPlacementIndicator { get { return usesItsOwnPlacementIndicator; } }

        /// <summary>Whether it is a terminal with no sequence to run.</summary>
        public bool TerminalHasNoSteps
        {
            get { return isAnEventTerminal && Steps.Length == 0; }
        }

        /// <summary>
        /// Whether it loops back past the end of its own sequence.
        /// </summary>
        /// <remarks>
        /// The loop index points at a step that does not exist, so the event either restarts from
        /// nothing or stops dead depending which the game does first.
        /// </remarks>
        public bool LoopsPastTheEnd
        {
            get { return isAnEventTerminal && Steps.Length > 0 && loopsBackToStep >= Steps.Length; }
        }

        /// <summary>Whether a farming machine works on nowhere.</summary>
        public bool FarmMachineWorksOnNothing
        {
            get { return (isAnAutomatedPlanter || isAnAutomatedHarvester) && WorksOn.Length == 0; }
        }

        /// <summary>Whether a standalone recipe makes nothing.</summary>
        public bool RecipeMakesNothing
        {
            get { return isAStandaloneRecipe && string.IsNullOrEmpty(RecipeMakesId); }
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

        private static Vector2 Ordered(Vector2 range)
        {
            float low = range.x < 0f ? 0f : range.x;
            float high = range.y < 0f ? 0f : range.y;
            return new Vector2(low, high < low ? low : high);
        }
    }

    /// <summary>One step in an event terminal's sequence.</summary>
    [Serializable]
    public struct DimensionTerminalStep
    {
        [Tooltip("What this step does.")]
        [SerializeField] private DimensionTerminalAction action;

        [Tooltip("Which connection it acts on, by the game's own name.")]
        [SerializeField] private string connection;

        [Tooltip("How long this step lasts.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        public DimensionTerminalAction Action { get { return action; } }

        public string Connection { get { return connection ?? string.Empty; } }

        public float Seconds { get { return seconds < 0f ? 0f : seconds; } }
    }

    /// <summary>One tile a farming machine works on, and where it moves what it finds.</summary>
    [Serializable]
    public struct DimensionFarmReach
    {
        [Tooltip("Which tile relative to the machine. (1,0) is the tile to its right.")]
        [SerializeField] private Vector2Int tile;

        [Tooltip("Which way it moves what it finds there.")]
        [SerializeField] private Vector2Int movesItToward;

        public Vector2Int Tile { get { return tile; } }

        public Vector2Int MovesItToward { get { return movesItToward; } }
    }

    /// <summary>
    /// What one step of an event terminal does. Core Keeper's <c>EventTerminalAction</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionTerminalAction
    {
        /// <summary>Nothing happens; the step is a pause. <c>Idle</c>.</summary>
        Wait = 0,

        /// <summary>Switch the connection on. <c>ToggleOn</c>.</summary>
        SwitchOn = 1,

        /// <summary>Switch it off. <c>ToggleOff</c>.</summary>
        SwitchOff = 2,

        /// <summary>Hold it as it is. <c>Hold</c>.</summary>
        Hold = 3
    }
}
