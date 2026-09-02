using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A creature made of a body that follows its head — a worm, a serpent, a caterpillar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SnakeMovementStateAuthoring</c> is thirty-five settings on 51 vanilla prefabs, and it is
    /// the single biggest untouched behaviour in the game. Nothing else in Core Keeper moves like
    /// this: the head decides where to go and every segment behind it follows the path the head
    /// took, so the creature occupies a line rather than a point.
    /// </para>
    /// <para>
    /// THE TAIL IS A SEPARATE OBJECT. <c>tailObjectId</c> names what the last segment is, which is
    /// why a serpent can have a differently-shaped end. Leave it blank and the body simply stops.
    /// </para>
    /// <para>
    /// IT LAYS GROUND AS IT GOES. <c>tilePlacementType</c> is how the slime serpents leave slime
    /// behind them and the sea ones leave water — the trail is part of the creature, not an effect
    /// attached to it.
    /// </para>
    /// <para>
    /// CATERPILLAR MOVEMENT IS A DIFFERENT ANIMAL. Switched on, the body bunches and stretches
    /// instead of flowing, and the four stretch settings below are what shape that. They do nothing
    /// at all without it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSegmentedCreatureTemplate
    {
        [Tooltip("It is a segmented creature — a body that follows its head.")]
        [SerializeField] private bool isSegmented;

        [Header("Its body")]
        [Tooltip("How many segments long it starts.")]
        [Min(0)]
        [SerializeField] private int startingLength = 5;

        [Tooltip("What its last segment is. One of the game's objects, or one of yours. Blank means the body just stops.")]
        [SerializeField] private string tailObjectId = string.Empty;

        [Tooltip("How far apart the segments sit.")]
        [Min(0f)]
        [SerializeField] private float spacing = 1f;

        [Tooltip("Extra sideways spacing, which is what makes it read as thick rather than thin.")]
        [Min(0f)]
        [SerializeField] private float extraSidewaysSpacing = 1.6f;

        [Tooltip("Each segment can be hit and killed on its own.")]
        [SerializeField] private bool segmentsAreHitSeparately;

        [Header("How it moves")]
        [Tooltip("How long a full turn takes. Longer makes it sweep rather than snap.")]
        [Min(0f)]
        [SerializeField] private float turnSeconds = 3f;

        [Tooltip("How far it weaves side to side as it travels. 0 travels straight.")]
        [Min(0f)]
        [SerializeField] private float weaveWidth;

        [Tooltip("How long one weave takes.")]
        [Min(0f)]
        [SerializeField] private float weaveSeconds;

        [Tooltip("It moves unpredictably rather than making for its target.")]
        [SerializeField] private bool movesChaotically;

        [Tooltip("It slows down near walls rather than grinding along them.")]
        [SerializeField] private bool slowsNearWalls = true;

        [Tooltip("It will go down into pits.")]
        [SerializeField] private bool goesIntoPits;

        [Tooltip("It plays its movement animation as it travels.")]
        [SerializeField] private bool playsItsMoveAnimation;

        [Tooltip("It is moved by physics rather than being placed directly. Usually on.")]
        [SerializeField] private bool movedByPhysics = true;

        [Header("Bunching like a caterpillar")]
        [Tooltip("Its body bunches and stretches instead of flowing. The four below need this.")]
        [SerializeField] private bool bunchesLikeACaterpillar;

        [Tooltip("How hard it stretches out.")]
        [Min(0f)]
        [SerializeField] private float stretchOutStrength = 0.8f;

        [Tooltip("How hard it pulls back in.")]
        [Min(0f)]
        [SerializeField] private float pullBackStrength = 0.8f;

        [Tooltip("How fast it bunches and stretches.")]
        [Min(0f)]
        [SerializeField] private float bunchSpeed = 10f;

        [Tooltip("How far along the body the bunching spreads.")]
        [Min(0f)]
        [SerializeField] private float bunchSpread = 2.5f;

        [Header("Who it goes for")]
        [Tooltip("How it picks a target.")]
        [SerializeField] private DimensionSegmentedTargeting picksTarget = DimensionSegmentedTargeting.WhoeverHitItLast;

        [Tooltip("How far away it will start going for a player.")]
        [Min(0f)]
        [SerializeField] private float noticesPlayersWithin = 15f;

        [Tooltip("How close it has to get before it picks somebody else.")]
        [Min(0f)]
        [SerializeField] private float switchesTargetWithin = 5f;

        [Tooltip("Shortest it waits before changing its mind about a player.")]
        [Min(0f)]
        [SerializeField] private float minTargetCooldown;

        [Tooltip("Longest it waits before changing its mind.")]
        [Min(0f)]
        [SerializeField] private float maxTargetCooldown;

        [Tooltip("How far it will stray from where the fight started.")]
        [Min(0f)]
        [SerializeField] private float straysFromTheFightBy = 30f;

        [Header("What it does on contact")]
        [Tooltip("It deals no damage at all — a harmless serpent.")]
        [SerializeField] private bool dealsNoDamage;

        [Tooltip("Flat damage, used when there is no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int flatDamage;

        [Tooltip("How hard it hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("How wide its contact hit is.")]
        [Min(0f)]
        [SerializeField] private float hitRadius = 1f;

        [Tooltip("Nudges where that hit sits relative to the head.")]
        [SerializeField] private Vector3 hitOffset = Vector3.zero;

        [Tooltip("How hard it shoves what it hits.")]
        [Min(0f)]
        [SerializeField] private float shoveForce = 10f;

        [Tooltip("It will not attack something already this close — it needs room.")]
        [Min(0f)]
        [SerializeField] private float tooCloseToAttack = 1f;

        [Tooltip("One object it will never hit. One of the game's, or one of yours. Blank for none.")]
        [SerializeField] private string neverHits = string.Empty;

        [Tooltip("What it smashes through drops nothing.")]
        [SerializeField] private bool whatItSmashesDropsNothing;

        [Header("The trail it leaves")]
        [Tooltip("What it lays down behind it as it travels.")]
        [SerializeField] private DimensionSegmentedTrail leavesBehind = DimensionSegmentedTrail.Nothing;

        [Tooltip("How wide that trail is, as a multiplier of its body.")]
        [Min(0f)]
        [SerializeField] private float trailWidthMultiplier = 1f;

        public bool IsSegmented { get { return isSegmented; } }

        public int StartingLength { get { return startingLength < 0 ? 0 : startingLength; } }

        public string TailObjectId { get { return tailObjectId ?? string.Empty; } }

        public float Spacing { get { return spacing < 0f ? 0f : spacing; } }

        public float ExtraSidewaysSpacing
        {
            get { return extraSidewaysSpacing < 0f ? 0f : extraSidewaysSpacing; }
        }

        public bool SegmentsAreHitSeparately { get { return segmentsAreHitSeparately; } }

        public float TurnSeconds { get { return turnSeconds < 0f ? 0f : turnSeconds; } }

        public float WeaveWidth { get { return weaveWidth < 0f ? 0f : weaveWidth; } }

        public float WeaveSeconds { get { return weaveSeconds < 0f ? 0f : weaveSeconds; } }

        public bool MovesChaotically { get { return movesChaotically; } }

        public bool SlowsNearWalls { get { return slowsNearWalls; } }

        public bool GoesIntoPits { get { return goesIntoPits; } }

        public bool PlaysItsMoveAnimation { get { return playsItsMoveAnimation; } }

        public bool MovedByPhysics { get { return movedByPhysics; } }

        public bool BunchesLikeACaterpillar { get { return bunchesLikeACaterpillar; } }

        public float StretchOutStrength
        {
            get { return stretchOutStrength < 0f ? 0f : stretchOutStrength; }
        }

        public float PullBackStrength
        {
            get { return pullBackStrength < 0f ? 0f : pullBackStrength; }
        }

        public float BunchSpeed { get { return bunchSpeed < 0f ? 0f : bunchSpeed; } }

        public float BunchSpread { get { return bunchSpread < 0f ? 0f : bunchSpread; } }

        public DimensionSegmentedTargeting PicksTarget { get { return picksTarget; } }

        public float NoticesPlayersWithin
        {
            get { return noticesPlayersWithin < 0f ? 0f : noticesPlayersWithin; }
        }

        public float SwitchesTargetWithin
        {
            get { return switchesTargetWithin < 0f ? 0f : switchesTargetWithin; }
        }

        public float MinTargetCooldown
        {
            get { return minTargetCooldown < 0f ? 0f : minTargetCooldown; }
        }

        public float MaxTargetCooldown
        {
            get
            {
                float longest = maxTargetCooldown < 0f ? 0f : maxTargetCooldown;
                return longest < MinTargetCooldown ? MinTargetCooldown : longest;
            }
        }

        public float StraysFromTheFightBy
        {
            get { return straysFromTheFightBy < 0f ? 0f : straysFromTheFightBy; }
        }

        public bool DealsNoDamage { get { return dealsNoDamage; } }

        public int FlatDamage { get { return flatDamage < 0 ? 0 : flatDamage; } }

        public float HitsThisHardForItsTier
        {
            get { return hitsThisHardForItsTier < 0f ? 0f : hitsThisHardForItsTier; }
        }

        public float HitRadius { get { return hitRadius < 0f ? 0f : hitRadius; } }

        public Vector3 HitOffset { get { return hitOffset; } }

        public float ShoveForce { get { return shoveForce < 0f ? 0f : shoveForce; } }

        public float TooCloseToAttack
        {
            get { return tooCloseToAttack < 0f ? 0f : tooCloseToAttack; }
        }

        public string NeverHits { get { return neverHits ?? string.Empty; } }

        public bool WhatItSmashesDropsNothing { get { return whatItSmashesDropsNothing; } }

        public DimensionSegmentedTrail LeavesBehind { get { return leavesBehind; } }

        public float TrailWidthMultiplier
        {
            get { return trailWidthMultiplier < 0f ? 0f : trailWidthMultiplier; }
        }

        /// <summary>Whether it is segmented with no segments.</summary>
        public bool HasNoBody
        {
            get { return isSegmented && startingLength <= 0; }
        }

        /// <summary>Whether the bunching settings were shaped without bunching switched on.</summary>
        public bool BunchingWillBeIgnored
        {
            get
            {
                return !bunchesLikeACaterpillar
                    && (stretchOutStrength != 0.8f || pullBackStrength != 0.8f
                        || bunchSpeed != 10f || bunchSpread != 2.5f);
            }
        }

        /// <summary>Whether a trail width was set on something that leaves no trail.</summary>
        public bool TrailWidthWillBeIgnored
        {
            get { return leavesBehind == DimensionSegmentedTrail.Nothing && trailWidthMultiplier != 1f; }
        }

        /// <summary>Whether it weaves in width but takes no time to do it, or the reverse.</summary>
        /// <remarks>
        /// The two are read together — a weave needs both a width and a period. One without the
        /// other produces a creature that travels dead straight while looking configured to weave.
        /// </remarks>
        public bool WeaveIsHalfSpecified
        {
            get { return (weaveWidth > 0f) != (weaveSeconds > 0f); }
        }
    }

    /// <summary>How a segmented creature picks who to go for. Core Keeper's <c>SnakeTargetingType</c>.</summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionSegmentedTargeting
    {
        /// <summary>It goes for whoever hurt it most recently. <c>LastAttacker</c>.</summary>
        WhoeverHitItLast = 0,

        /// <summary>It goes for whoever is nearest. <c>ClosestPlayer</c>.</summary>
        WhoeverIsNearest = 1
    }

    /// <summary>
    /// What a segmented creature lays down behind it. Core Keeper's
    /// <c>SnakeMovementTilePlacementType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionSegmentedTrail
    {
        /// <summary>It leaves the ground as it was. <c>None</c>.</summary>
        Nothing = 0,

        /// <summary>It leaves slime, the way the slime serpents do. <c>Slime</c>.</summary>
        Slime = 1,

        /// <summary>It leaves water behind it. <c>SeaWater</c>.</summary>
        Water = 2,

        /// <summary>It lays plain ground, filling in what it crosses. <c>Ground</c>.</summary>
        Ground = 3
    }
}
