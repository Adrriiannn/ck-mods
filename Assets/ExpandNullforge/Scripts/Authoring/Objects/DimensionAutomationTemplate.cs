using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How a mover hands out work when several are attached to one machine.</summary>
    public enum DimensionMoverCycling
    {
        /// <summary>They all run whenever the machine is idle.</summary>
        AllAtOnce = 0,

        /// <summary>They take turns, one per activation.</summary>
        TakeTurns = 1
    }

    /// <summary>
    /// What Core Keeper's automation can do with a thing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six components, five of them bare markers. They are one template because they are one
    /// question a builder asks — "will my conveyors and arms work with this" — and because the
    /// answers are otherwise scattered: a container that accepts automation, an ore a drill can
    /// mine, a seed a seeder can plant, and a machine that does the moving are four different
    /// components with nothing in common except the system that reads them.
    /// </para>
    /// <para>
    /// The mover half is the only one with settings, and its shape is worth knowing: a conveyor or
    /// a robot arm is <c>AutomatedMoverAuthoring</c> (a marker saying "this moves things") plus
    /// <c>AutomatedMoverSharedAuthoring</c> (how long a move takes, how long it rests, and what
    /// happens when several movers share one machine).
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionAutomationTemplate
    {
        [Tooltip("Conveyors and robot arms may act on it — pick it up, put it down, feed it.")]
        [SerializeField] private bool automationMayActOnIt;

        [Tooltip("A drill can mine it.")]
        [SerializeField] private bool aDrillCanMineIt;

        [Tooltip("A seeder can plant it. For seeds.")]
        [SerializeField] private bool aSeederCanPlantIt;

        [Tooltip("Automation can craft at it. For a station a crafter feeds.")]
        [SerializeField] private bool automationCanCraftAtIt;

        [Header("If it is the thing doing the moving")]
        [Tooltip("It moves things — a conveyor, a robot arm.")]
        [SerializeField] private bool itMovesThings;

        [Tooltip("How long one move takes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float moveSeconds = 0.5f;

        [Tooltip("How long it rests between moves, in seconds.")]
        [Min(0f)]
        [SerializeField] private float restSeconds = 0.5f;

        [Tooltip("It picks things up part-way through a move rather than only at the start.")]
        [SerializeField] private bool picksUpDuringTheMove;

        [Tooltip("It may take from inventories, not just from the ground.")]
        [SerializeField] private bool mayTakeFromInventories = true;

        [Tooltip("It splits a stack as it moves rather than carrying the whole thing.")]
        [SerializeField] private bool splitsStacks;

        [Tooltip("Only one attached mover runs at a time.")]
        [SerializeField] private bool onlyOneAtATime;

        [Tooltip("How attached movers share the work.")]
        [SerializeField] private DimensionMoverCycling cycling = DimensionMoverCycling.AllAtOnce;

        [Header("If it pushes what stands on it")]
        [Tooltip("Things standing on it are pushed along. This is what a conveyor belt does.")]
        [SerializeField] private bool pushesWhatStandsOnIt;

        [Tooltip("Which way it pushes, one entry per variation. A belt with four rotations has four.")]
        [SerializeField] private Vector2Int[] pushDirections = new Vector2Int[0];

        [Tooltip("Which push wins when two overlap. Higher wins.")]
        [SerializeField] private int pushPriority;

        [Tooltip("It only pushes while powered.")]
        [SerializeField] private bool pushNeedsPower = true;

        public bool AutomationMayActOnIt
        {
            get { return automationMayActOnIt; }
        }

        public bool ADrillCanMineIt
        {
            get { return aDrillCanMineIt; }
        }

        public bool ASeederCanPlantIt
        {
            get { return aSeederCanPlantIt; }
        }

        public bool AutomationCanCraftAtIt
        {
            get { return automationCanCraftAtIt; }
        }

        public bool ItMovesThings
        {
            get { return itMovesThings; }
        }

        public float MoveSeconds
        {
            get { return moveSeconds < 0f ? 0f : moveSeconds; }
        }

        public float RestSeconds
        {
            get { return restSeconds < 0f ? 0f : restSeconds; }
        }

        public bool PicksUpDuringTheMove
        {
            get { return picksUpDuringTheMove; }
        }

        public bool MayTakeFromInventories
        {
            get { return mayTakeFromInventories; }
        }

        public bool SplitsStacks
        {
            get { return splitsStacks; }
        }

        public bool OnlyOneAtATime
        {
            get { return onlyOneAtATime; }
        }

        public bool PushesWhatStandsOnIt
        {
            get { return pushesWhatStandsOnIt; }
        }

        /// <summary>Which way it pushes, one entry per variation.</summary>
        public Vector2Int[] PushDirections
        {
            get { return pushDirections ?? new Vector2Int[0]; }
        }

        public int PushPriority
        {
            get { return pushPriority; }
        }

        public bool PushNeedsPower
        {
            get { return pushNeedsPower; }
        }

        /// <summary>Whether it pushes and was never told which way.</summary>
        /// <remarks>
        /// A belt with no directions is a belt that runs and moves nothing. It looks switched on and
        /// does nothing, which is the same symptom as a move time of zero and a different cause.
        /// </remarks>
        public bool PushesNowhere
        {
            get { return pushesWhatStandsOnIt && PushDirections.Length == 0; }
        }

        public DimensionMoverCycling Cycling
        {
            get { return cycling; }
        }

        /// <summary>Whether automation has anything to do with it at all.</summary>
        public bool TakesPartInAutomation
        {
            get
            {
                return automationMayActOnIt
                    || aDrillCanMineIt
                    || aSeederCanPlantIt
                    || automationCanCraftAtIt
                    || itMovesThings
                    || pushesWhatStandsOnIt;
            }
        }

        /// <summary>Whether it moves things and never finishes a move.</summary>
        /// <remarks>
        /// A move time of zero is not "instant" — the mover has no interval to run over, so nothing
        /// completes. It looks like a conveyor that is switched on and does nothing.
        /// </remarks>
        public bool MovesNothingBecauseAMoveTakesNoTime
        {
            get { return itMovesThings && MoveSeconds <= 0f; }
        }
    }
}
