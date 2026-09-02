using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A chain of explosions that walks outward, and the other small behaviours an object can carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE CHAIN IS THE INTERESTING ONE. A sequence explosive is not one blast — it is a list of
    /// charges, each going off a moment after the last and a little further out, optionally turned
    /// a few degrees. That is how a firework fans, how a mine walks toward you, and how a boss
    /// carpets a line. Each charge can spawn several at once, and each can inherit the direction the
    /// thing was facing or pick its own.
    /// </para>
    /// <para>
    /// EVERY CHARGE AFTER THE FIRST CAN BE MADE IDENTICAL with one tick — the game reads only the
    /// first entry and repeats it. That is how a long chain is written without typing twenty
    /// identical rows.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionChainReactionTemplate
    {
        [Header("A chain of explosions")]
        [Tooltip("It goes off as a chain rather than a single blast.")]
        [SerializeField] private bool explodesInAChain;

        [Tooltip("How long before the first charge.")]
        [Min(0f)]
        [SerializeField] private float delayBeforeFirst;

        [Tooltip("How long before the animation starts, separately from the charges.")]
        [Min(0f)]
        [SerializeField] private float animationDelay;

        [Tooltip("The chain goes off when it dies rather than on a timer.")]
        [SerializeField] private bool chainsOnDeath;

        [Tooltip("The chain walks the way it is facing rather than outward in all directions.")]
        [SerializeField] private bool chainFollowsItsFacing;

        [Tooltip("Every charge copies the first, so a long chain needs only one row filled in.")]
        [SerializeField] private bool everyChargeCopiesTheFirst;

        [Tooltip("A condition that limits how many charges it gets. Blank for no limit.")]
        [SerializeField] private string chargesLimitedByCondition = string.Empty;

        [Tooltip("The charges, in order. Each goes off after the one before it.")]
        [SerializeField] private DimensionChainCharge[] charges = new DimensionChainCharge[0];

        [Header("Leaving a trail")]
        [Tooltip("It leaves something behind it as it moves.")]
        [SerializeField] private bool leavesATrail;

        [Tooltip("What it leaves. One of the game's objects, or one of yours.")]
        [SerializeField] private string trailObjectId = string.Empty;

        [Tooltip("How many trail pieces it leaves at a time.")]
        [Min(0)]
        [SerializeField] private int trailPieces = 1;

        [Header("Changing look on its own")]
        [Tooltip("It changes look after sitting at one for a while.")]
        [SerializeField] private bool changesLookOverTime;

        [Tooltip("Which look it has to be wearing for that to happen.")]
        [Min(0)]
        [SerializeField] private int fromLook;

        [Tooltip("Which look it changes to.")]
        [Min(0)]
        [SerializeField] private int toLook = 1;

        [Tooltip("How long it waits before changing.")]
        [Min(0f)]
        [SerializeField] private float afterSeconds = 5f;

        [Tooltip("It also changes look the moment it is hurt.")]
        [SerializeField] private bool changesLookWhenHurt;

        [Tooltip("Which look it wears once hurt.")]
        [Min(0)]
        [SerializeField] private int hurtLook = 1;

        [Header("Odds and ends")]
        [Tooltip("It is a water source — it fills the ground around it.")]
        [SerializeField] private bool isAWaterSource;

        [Tooltip("Which water it makes.")]
        [SerializeField] private string waterTilesetId = string.Empty;

        [Tooltip("Where its splash appears relative to it.")]
        [SerializeField] private Vector3 splashOffset = Vector3.zero;

        [Tooltip("It is pet candy — feeding it to a pet gives this much experience. 0 for none.")]
        [Min(0)]
        [SerializeField] private int petExperience;

        [Tooltip("Everything it holds spills out the moment it is hit.")]
        [SerializeField] private bool spillsEverythingWhenHit;

        [Tooltip("Where that spill lands relative to it.")]
        [SerializeField] private Vector3 spillOffset = Vector3.zero;

        [Header("A vending machine")]
        [Tooltip("It is a vending machine, offering a grid of things.")]
        [SerializeField] private bool isAVendingMachine;

        [Tooltip("How many columns its grid has.")]
        [Min(0)]
        [SerializeField] private int machineColumns = 3;

        [Tooltip("How many rows.")]
        [Min(0)]
        [SerializeField] private int machineRows = 3;

        [Tooltip("What it offers.")]
        [SerializeField] private DimensionMelodyReward[] machineStock = new DimensionMelodyReward[0];

        public bool ExplodesInAChain { get { return explodesInAChain; } }

        public float DelayBeforeFirst { get { return Floor(delayBeforeFirst); } }

        public float AnimationDelay { get { return Floor(animationDelay); } }

        public bool ChainsOnDeath { get { return chainsOnDeath; } }

        public bool ChainFollowsItsFacing { get { return chainFollowsItsFacing; } }

        public bool EveryChargeCopiesTheFirst { get { return everyChargeCopiesTheFirst; } }

        public string ChargesLimitedByCondition
        {
            get { return chargesLimitedByCondition ?? string.Empty; }
        }

        public DimensionChainCharge[] Charges
        {
            get { return charges ?? new DimensionChainCharge[0]; }
        }

        public bool LeavesATrail { get { return leavesATrail; } }

        public string TrailObjectId { get { return trailObjectId ?? string.Empty; } }

        public int TrailPieces { get { return trailPieces < 0 ? 0 : trailPieces; } }

        public bool ChangesLookOverTime { get { return changesLookOverTime; } }

        public int FromLook { get { return fromLook < 0 ? 0 : fromLook; } }

        public int ToLook { get { return toLook < 0 ? 0 : toLook; } }

        public float AfterSeconds { get { return Floor(afterSeconds); } }

        public bool ChangesLookWhenHurt { get { return changesLookWhenHurt; } }

        public int HurtLook { get { return hurtLook < 0 ? 0 : hurtLook; } }

        public bool IsAWaterSource { get { return isAWaterSource; } }

        public string WaterTilesetId { get { return waterTilesetId ?? string.Empty; } }

        public Vector3 SplashOffset { get { return splashOffset; } }

        public int PetExperience { get { return petExperience < 0 ? 0 : petExperience; } }

        public bool SpillsEverythingWhenHit { get { return spillsEverythingWhenHit; } }

        public Vector3 SpillOffset { get { return spillOffset; } }

        public bool IsAVendingMachine { get { return isAVendingMachine; } }

        public int MachineColumns { get { return machineColumns < 0 ? 0 : machineColumns; } }

        public int MachineRows { get { return machineRows < 0 ? 0 : machineRows; } }

        public DimensionMelodyReward[] MachineStock
        {
            get { return machineStock ?? new DimensionMelodyReward[0]; }
        }

        /// <summary>Whether it chains with nothing in the chain.</summary>
        public bool ChainHasNoCharges
        {
            get { return explodesInAChain && Charges.Length == 0; }
        }

        /// <summary>Whether it leaves a trail of nothing.</summary>
        public bool TrailOfNothing
        {
            get { return leavesATrail && string.IsNullOrEmpty(TrailObjectId); }
        }

        /// <summary>Whether it changes look to the one it already wears.</summary>
        /// <remarks>
        /// The change happens and nothing visibly moves, which reads as the timer being broken.
        /// </remarks>
        public bool ChangesLookToTheSameLook
        {
            get { return changesLookOverTime && fromLook == toLook; }
        }

        /// <summary>Whether it is a vending machine with a grid smaller than its stock.</summary>
        public bool MachineStockDoesNotFit
        {
            get
            {
                return isAVendingMachine
                    && MachineStock.Length > MachineColumns * MachineRows;
            }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }
    }

    /// <summary>One charge in a chain of explosions.</summary>
    [Serializable]
    public struct DimensionChainCharge
    {
        [Tooltip("Which explosion this charge is.")]
        [SerializeField] private string explosionId;

        [Tooltip("Which look of it.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("How long after the charge before it this one goes off.")]
        [Min(0f)]
        [SerializeField] private float delayFromPrevious;

        [Tooltip("How much further out than the last one it lands.")]
        [Min(0f)]
        [SerializeField] private float furtherOutBy;

        [Tooltip("How far round from the last one it is turned, in degrees.")]
        [SerializeField] private float turnedByDegrees;

        [Tooltip("How many go off at once for this charge.")]
        [Min(0)]
        [SerializeField] private int howMany;

        [Tooltip("Which way this charge goes.")]
        [SerializeField] private DimensionChargeDirection direction;

        public string ExplosionId { get { return explosionId ?? string.Empty; } }

        public int Variation { get { return variation < 0 ? 0 : variation; } }

        public float DelayFromPrevious
        {
            get { return delayFromPrevious < 0f ? 0f : delayFromPrevious; }
        }

        public float FurtherOutBy { get { return furtherOutBy < 0f ? 0f : furtherOutBy; } }

        public float TurnedByDegrees { get { return turnedByDegrees; } }

        public int HowMany { get { return howMany < 1 ? 1 : howMany; } }

        public DimensionChargeDirection Direction { get { return direction; } }
    }

    /// <summary>
    /// Which way a charge in a chain goes. Core Keeper's
    /// <c>SequenceExplosionChargeDirectionType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionChargeDirection
    {
        /// <summary>Straight out from the thing that went off. <c>Base</c>.</summary>
        StraightOut = 0,

        /// <summary>The way the thing was facing. <c>InheritDirection</c>.</summary>
        TheWayItWasFacing = 1,

        /// <summary>Any direction at all. <c>Random</c>.</summary>
        Anywhere = 2
    }
}
