using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// How a creature comes at you, and the fine grain of how it hits when it arrives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field audit found <c>ChaseStateAuthoring</c> at 2 fields of 19 and
    /// <c>MeleeAttackStateAuthoring</c> at 14 of 29 — the worst pair in the framework, and the
    /// reason a custom boss could be given numbers but not a way of moving. This is the half that
    /// was out of reach.
    /// </para>
    /// <para>
    /// SHAPED BY MEASURED USE, as with projectiles and traps. Across the game's chasers:
    /// <c>skipVisibilityCheck</c> 28, <c>neverStopChasing</c> 27, <c>ignoreLowColliders</c> 23,
    /// <c>needPathToChase</c> 12, <c>preferPathFind</c> 11 — those are the real behaviour switches.
    /// The four timing fields (<c>preChaseDuration</c>, <c>endChaseDuration</c>,
    /// <c>idleDuration</c>, <c>idleCooldown</c>) are zero on roughly 80 of 83, so they are offered
    /// under "rarely used" rather than up front. <c>chaseHeldObjects</c> is set by <b>zero</b>
    /// prefabs and is not offered at all.
    /// </para>
    /// <para>
    /// THE STANDOFF RANGE IS THE INTERESTING PART. <c>minDistanceToKeep</c> and
    /// <c>maxDistanceToKeep</c> are how a creature hangs back rather than closing — an archer that
    /// keeps its distance, a caster that circles. With both at zero every custom creature runs
    /// straight at the player and stops, which is why they all felt the same.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPursuitTemplate
    {
        [Header("How it closes")]
        [Tooltip("Closest it will come. Above zero makes it hang back rather than run straight in.")]
        [Min(0f)]
        [SerializeField] private float keepsAtLeastThisFarAway;

        [Tooltip("Furthest it drifts before closing again. Together these are the standoff range.")]
        [Min(0f)]
        [SerializeField] private float andAtMostThisFarAway;

        [Tooltip("How close before it starts stepping sideways rather than straight on.")]
        [Min(0f)]
        [SerializeField] private float startsSideSteppingWithin;

        [Header("What stops it")]
        [Tooltip("It keeps chasing forever once started, rather than giving up.")]
        [SerializeField] private bool neverGivesUp;

        [Tooltip("It chases even when it cannot see its target.")]
        [SerializeField] private bool chasesWhatItCannotSee;

        [Tooltip("Low obstacles do not stop it.")]
        [SerializeField] private bool lowObstaclesDoNotStopIt;

        [Header("How it finds its way")]
        [Tooltip("It needs a real path before it will chase at all.")]
        [SerializeField] private bool needsAPathToChase;

        [Tooltip("It prefers pathfinding over walking straight at the target.")]
        [SerializeField] private bool prefersPathfinding;

        [Tooltip("How far ahead it looks to step around things. 0 leaves the game's own behaviour.")]
        [Min(0f)]
        [SerializeField] private float looksAheadToAvoidObstacles;

        [Tooltip("It starts switched off, for a chase something else turns on later — a boss phase, a trigger.")]
        [SerializeField] private bool startsSwitchedOff;

        [Header("Rarely used")]
        [Tooltip("It pauses this long before starting to chase. Zero on nearly every vanilla chaser.")]
        [Min(0f)]
        [SerializeField] private float pauseBeforeChasing;

        [Tooltip("It keeps going this long after losing its target.")]
        [Min(0f)]
        [SerializeField] private float keepsGoingAfterLosingIt;

        [Tooltip("It stops to idle for this long mid-chase.")]
        [Min(0f)]
        [SerializeField] private float idlesMidChaseFor;

        [Tooltip("How long between those idles.")]
        [Min(0f)]
        [SerializeField] private float betweenMidChaseIdles;

        [Tooltip("It makes no noise about keeping its distance.")]
        [SerializeField] private bool keepsQuietAboutItsDistance;

        /// <summary>Whether the author filled any of this in at all.</summary>
        /// <remarks>
        /// THE CONTRACT IS ALL-OR-NOTHING, and deliberately so. Several vanilla behaviours — the
        /// robot patroller and friends — bring their own <c>ChaseStateAuthoring</c> already filled
        /// in, and writing a blank template over one produces a creature that runs at the player and
        /// then does nothing. So an untouched pursuit section leaves the behaviour's own chase
        /// exactly as it was, and a touched one means the author is taking the chase over.
        /// </remarks>
        public bool HasAnySetting
        {
            get
            {
                return keepsAtLeastThisFarAway > 0f
                    || andAtMostThisFarAway > 0f
                    || startsSideSteppingWithin > 0f
                    || looksAheadToAvoidObstacles > 0f
                    || pauseBeforeChasing > 0f
                    || keepsGoingAfterLosingIt > 0f
                    || idlesMidChaseFor > 0f
                    || betweenMidChaseIdles > 0f
                    || neverGivesUp
                    || chasesWhatItCannotSee
                    || lowObstaclesDoNotStopIt
                    || needsAPathToChase
                    || prefersPathfinding
                    || keepsQuietAboutItsDistance
                    || startsSwitchedOff;
            }
        }

        public bool StartsSwitchedOff
        {
            get { return startsSwitchedOff; }
        }

        public float KeepsAtLeastThisFarAway
        {
            get { return keepsAtLeastThisFarAway < 0f ? 0f : keepsAtLeastThisFarAway; }
        }

        /// <summary>The outer standoff distance, never closer than the inner one.</summary>
        public float AndAtMostThisFarAway
        {
            get
            {
                float outer = andAtMostThisFarAway < 0f ? 0f : andAtMostThisFarAway;
                return outer < KeepsAtLeastThisFarAway ? KeepsAtLeastThisFarAway : outer;
            }
        }

        /// <summary>Whether it hangs back at all rather than closing all the way.</summary>
        public bool KeepsItsDistance
        {
            get { return KeepsAtLeastThisFarAway > 0f; }
        }

        public float StartsSideSteppingWithin
        {
            get { return startsSideSteppingWithin < 0f ? 0f : startsSideSteppingWithin; }
        }

        public bool NeverGivesUp
        {
            get { return neverGivesUp; }
        }

        public bool ChasesWhatItCannotSee
        {
            get { return chasesWhatItCannotSee; }
        }

        public bool LowObstaclesDoNotStopIt
        {
            get { return lowObstaclesDoNotStopIt; }
        }

        public bool NeedsAPathToChase
        {
            get { return needsAPathToChase; }
        }

        public bool PrefersPathfinding
        {
            get { return prefersPathfinding; }
        }

        public float LooksAheadToAvoidObstacles
        {
            get { return looksAheadToAvoidObstacles < 0f ? 0f : looksAheadToAvoidObstacles; }
        }

        public float PauseBeforeChasing
        {
            get { return pauseBeforeChasing < 0f ? 0f : pauseBeforeChasing; }
        }

        public float KeepsGoingAfterLosingIt
        {
            get { return keepsGoingAfterLosingIt < 0f ? 0f : keepsGoingAfterLosingIt; }
        }

        public float IdlesMidChaseFor
        {
            get { return idlesMidChaseFor < 0f ? 0f : idlesMidChaseFor; }
        }

        public float BetweenMidChaseIdles
        {
            get { return betweenMidChaseIdles < 0f ? 0f : betweenMidChaseIdles; }
        }

        public bool KeepsQuietAboutItsDistance
        {
            get { return keepsQuietAboutItsDistance; }
        }

        /// <summary>
        /// Whether it needs a path to chase and will also never stop chasing.
        /// </summary>
        /// <remarks>
        /// Worth saying because the two pull against each other: it refuses to start without a route
        /// and then refuses to give up once it has one, so a target that becomes unreachable is
        /// followed forever by something that cannot arrive.
        /// </remarks>
        public bool WillFollowForeverWithoutArriving
        {
            get { return needsAPathToChase && neverGivesUp; }
        }

        /// <summary>Whether it hangs back further than it will ever close.</summary>
        public bool StandoffRangeIsBackwards
        {
            get { return andAtMostThisFarAway > 0f && andAtMostThisFarAway < keepsAtLeastThisFarAway; }
        }
    }
}
