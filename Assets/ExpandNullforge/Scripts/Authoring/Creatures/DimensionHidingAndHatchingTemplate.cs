using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Creatures that hide in bushes, eggs that hatch when you come near, territories that fill
    /// themselves, and the last of the small world behaviours.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HIDING IN A BUSH IS A FULL CYCLE, not a toggle. The creature runs to a bush, burrows in,
    /// waits, peaks out, and leaves — each of those a separate duration. Getting one of them wrong
    /// produces a creature that pops in and out too fast to react to, which is why they are all
    /// asked about rather than bundled into one "hides" number.
    /// </para>
    /// <para>
    /// AN EGG IS A TIMER PLUS A RANGE. It hatches when a player comes near, waits, and produces
    /// somewhere between a minimum and a maximum. That is the whole larva-hive loop and it was
    /// unreachable.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionHidingAndHatchingTemplate
    {
        [Header("Hiding in bushes")]
        [Tooltip("It hides in bushes and pops out to attack.")]
        [SerializeField] private bool hidesInBushes;

        [Tooltip("How long it takes to reach a bush.")]
        [Min(0f)]
        [SerializeField] private float secondsToReachABush = 1f;

        [Tooltip("How long it stays hidden at the least.")]
        [Min(0f)]
        [SerializeField] private float minSecondsHidden = 13f;

        [Tooltip("How long it stays hidden at the most.")]
        [Min(0f)]
        [SerializeField] private float maxSecondsHidden = 16f;

        [Tooltip("How long it peeks out for before committing.")]
        [Min(0f)]
        [SerializeField] private float peekSeconds = 0.5f;

        [Tooltip("How long it takes to leave the bush.")]
        [Min(0f)]
        [SerializeField] private float secondsToLeave = 0.5f;

        [Tooltip("How close a target has to be for it to come out early.")]
        [Min(0f)]
        [SerializeField] private float comesOutWithin = 3f;

        [Tooltip("How long after a fight ends before it burrows back in.")]
        [Min(0f)]
        [SerializeField] private float burrowsBackAfter = 2f;

        [Header("Hatching")]
        [Tooltip("It is an egg — it hatches when a player comes near.")]
        [SerializeField] private bool isAnEgg;

        [Tooltip("How long it takes to hatch once triggered.")]
        [Min(0f)]
        [SerializeField] private float secondsToHatch = 2f;

        [Tooltip("What comes out.")]
        [SerializeField] private string hatchesIntoId = string.Empty;

        [Tooltip("Fewest that come out.")]
        [Min(0)]
        [SerializeField] private int fewestHatched = 1;

        [Tooltip("Most that come out.")]
        [Min(0)]
        [SerializeField] private int mostHatched = 1;

        [Tooltip("It hatches its eggs the way the Larva Hive boss does, as a fighting move rather than a timer.")]
        [SerializeField] private bool hatchesEggsLikeTheLarvaHive;

        [Header("A caveling territory")]
        [Tooltip("It claims a caveling territory of this size. 0 for none.")]
        [Min(0)]
        [SerializeField] private int cavelingTerritorySize;

        [Tooltip("Chance an ordinary caveling appears in it.")]
        [Range(0f, 1f)]
        [SerializeField] private float cavelingChance;

        [Tooltip("Chance a caveling shaman appears.")]
        [Range(0f, 1f)]
        [SerializeField] private float cavelingShamanChance;

        [Tooltip("Chance a caveling brute appears.")]
        [Range(0f, 1f)]
        [SerializeField] private float cavelingBruteChance;

        [Header("A delayed shot")]
        [Tooltip("It fires a shot that waits before setting off.")]
        [SerializeField] private bool firesADelayedShot;

        [Tooltip("How long it waits before moving.")]
        [Min(0f)]
        [SerializeField] private float shotDelay = 1f;

        [Tooltip("How fast it goes once it moves.")]
        [Min(0f)]
        [SerializeField] private float shotSpeed = 5f;

        [Tooltip("It seeks its target rather than flying straight.")]
        [SerializeField] private bool shotSeeks;

        [Header("Reacting to someone coming close")]
        [Tooltip("It triggers when something comes within reach.")]
        [SerializeField] private bool triggersOnApproach;

        [Tooltip("How close that is.")]
        [Min(0f)]
        [SerializeField] private float triggersWithin = 2f;

        [Tooltip("How long after being triggered before it acts.")]
        [Min(0f)]
        [SerializeField] private float triggerDelay;

        [Header("How fast it animates")]
        [Tooltip("It animates faster or slower than usual. 1 is normal.")]
        [Min(0f)]
        [SerializeField] private float animationSpeed = 1f;

        [Tooltip("How much its walk animation leans sideways.")]
        [SerializeField] private float animationLeanX;

        [Tooltip("And how much it leans forward and back.")]
        [SerializeField] private float animationLeanY;

        [Header("Extra inventory slots")]
        [Tooltip("It has slots for selling things.")]
        [SerializeField] private bool hasSellSlots;

        [Tooltip("How many columns those have.")]
        [Min(0)]
        [SerializeField] private int sellColumns = 3;

        [Tooltip("How many rows.")]
        [Min(0)]
        [SerializeField] private int sellRows = 3;

        [Tooltip("It has an upgrade slot.")]
        [SerializeField] private bool hasAnUpgradeSlot;

        [Tooltip("It has vanity slots for cosmetics.")]
        [SerializeField] private bool hasVanitySlots;

        public bool HidesInBushes { get { return hidesInBushes; } }

        public float SecondsToReachABush { get { return Floor(secondsToReachABush); } }

        public float MinSecondsHidden { get { return Floor(minSecondsHidden); } }

        public float MaxSecondsHidden
        {
            get { return AtLeast(maxSecondsHidden, MinSecondsHidden); }
        }

        public float PeekSeconds { get { return Floor(peekSeconds); } }

        public float SecondsToLeave { get { return Floor(secondsToLeave); } }

        public float ComesOutWithin { get { return Floor(comesOutWithin); } }

        public float BurrowsBackAfter { get { return Floor(burrowsBackAfter); } }

        public bool HatchesEggsLikeTheLarvaHive { get { return hatchesEggsLikeTheLarvaHive; } }

        public bool IsAnEgg { get { return isAnEgg; } }

        public float SecondsToHatch { get { return Floor(secondsToHatch); } }

        public string HatchesIntoId { get { return hatchesIntoId ?? string.Empty; } }

        public int FewestHatched { get { return fewestHatched < 0 ? 0 : fewestHatched; } }

        public int MostHatched
        {
            get
            {
                int most = mostHatched < 0 ? 0 : mostHatched;
                return most < FewestHatched ? FewestHatched : most;
            }
        }

        public int CavelingTerritorySize
        {
            get { return cavelingTerritorySize < 0 ? 0 : cavelingTerritorySize; }
        }

        public float CavelingChance { get { return Clamp01(cavelingChance); } }

        public float CavelingShamanChance { get { return Clamp01(cavelingShamanChance); } }

        public float CavelingBruteChance { get { return Clamp01(cavelingBruteChance); } }

        public bool FiresADelayedShot { get { return firesADelayedShot; } }

        public float ShotDelay { get { return Floor(shotDelay); } }

        public float ShotSpeed { get { return Floor(shotSpeed); } }

        public bool ShotSeeks { get { return shotSeeks; } }

        public bool TriggersOnApproach { get { return triggersOnApproach; } }

        public float TriggersWithin { get { return Floor(triggersWithin); } }

        public float TriggerDelay { get { return Floor(triggerDelay); } }

        public float AnimationSpeed { get { return Floor(animationSpeed); } }

        public float AnimationLeanX { get { return animationLeanX; } }

        public float AnimationLeanY { get { return animationLeanY; } }

        public bool HasSellSlots { get { return hasSellSlots; } }

        public int SellColumns { get { return sellColumns < 0 ? 0 : sellColumns; } }

        public int SellRows { get { return sellRows < 0 ? 0 : sellRows; } }

        public bool HasAnUpgradeSlot { get { return hasAnUpgradeSlot; } }

        public bool HasVanitySlots { get { return hasVanitySlots; } }

        /// <summary>Whether it is an egg that hatches into nothing.</summary>
        public bool HatchesIntoNothing
        {
            get { return isAnEgg && string.IsNullOrEmpty(HatchesIntoId); }
        }

        /// <summary>Whether it is an egg that produces none of what it hatches.</summary>
        public bool HatchesNone
        {
            get { return isAnEgg && mostHatched <= 0; }
        }

        /// <summary>
        /// Whether a caveling territory was claimed with no chance of anything appearing in it.
        /// </summary>
        public bool CavelingTerritoryIsEmpty
        {
            get
            {
                return cavelingTerritorySize > 0
                    && cavelingChance <= 0f
                    && cavelingShamanChance <= 0f
                    && cavelingBruteChance <= 0f;
            }
        }

        /// <summary>Whether a delayed shot never moves because it has no speed.</summary>
        public bool DelayedShotNeverMoves
        {
            get { return firesADelayedShot && shotSpeed <= 0f; }
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
