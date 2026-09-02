using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>One idle animation a creature plays while nothing is happening.</summary>
    /// <remarks>
    /// The pre-idle pair is the interesting part: the creature waits a random time inside that
    /// range before starting, so a group of the same creature does not emote in lockstep.
    /// </remarks>
    [Serializable]
    public sealed class DimensionIdleEmote
    {
        [Tooltip("The animation to play.")]
        [SerializeField] private string animation = string.Empty;

        [Tooltip("How long it plays for, in seconds.")]
        [Min(0f)]
        [SerializeField] private float seconds = 1f;

        [Tooltip("Shortest wait before it starts. Randomised so a group does not emote in step.")]
        [Min(0f)]
        [SerializeField] private float minimumWait;

        [Tooltip("Longest wait before it starts.")]
        [Min(0f)]
        [SerializeField] private float maximumWait = 1f;

        [Tooltip("It only plays on ground the creature could walk on.")]
        [SerializeField] private bool onlyOnWalkableGround;

        public string Animation
        {
            get { return animation ?? string.Empty; }
        }

        public float Seconds
        {
            get { return seconds < 0f ? 0f : seconds; }
        }

        public float MinimumWait
        {
            get { return minimumWait < 0f ? 0f : minimumWait; }
        }

        /// <summary>The longest wait, never shorter than the shortest.</summary>
        public float MaximumWait
        {
            get
            {
                float longest = maximumWait < 0f ? 0f : maximumWait;
                return longest < MinimumWait ? MinimumWait : longest;
            }
        }

        public bool OnlyOnWalkableGround
        {
            get { return onlyOnWalkableGround; }
        }

        public bool NamesAnAnimation
        {
            get { return !string.IsNullOrEmpty(Animation); }
        }
    }

    /// <summary>
    /// How a creature arrives, idles, scales with the party, and leaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seven of Core Keeper's components, kept together because they are one arc rather than seven
    /// settings: what it does on the way in, what it does with nothing to do, how it answers a
    /// bigger group of players, what it takes with it when it dies, and when it gives up and
    /// despawns.
    /// </para>
    /// <para>
    /// THE PARTY SCALING IS THE ONE WORTH KNOWING ABOUT. <c>ScaleHealthByPlayerCount</c> is on 31
    /// vanilla prefabs and every one measured is a boss or a serious enemy — Bird Boss, Boss Larva,
    /// the cicadas. A factor of 1 (26 of them) means full scaling; the handful below that, 0.45 to
    /// 0.75, scale gently. A custom boss without it has the same health for one player as for four,
    /// which is the single most common complaint about modded bosses.
    /// </para>
    /// <para>
    /// The arrival is the other one with real presence: <c>SpawnState</c> can clear the tiles around
    /// it as the creature appears, which is how a boss bursts out of a wall rather than standing
    /// politely in a corridor.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureLifecycleTemplate
    {
        [Tooltip("Which way it faces as it appears.")]
        [SerializeField] private Vector2 facesOnEntrance = Vector2.zero;

        [Tooltip("Nudges where it clears tiles as it appears.")]
        [SerializeField] private Vector2 clearsTilesOffsetBy = Vector2.zero;

        [Tooltip("What it kills on death also takes things that normally survive zero health.")]
        [SerializeField] private bool deathTakesEvenTheUnkillable;

        public Vector2 FacesOnEntrance { get { return facesOnEntrance; } }

        public Vector2 ClearsTilesOffsetBy { get { return clearsTilesOffsetBy; } }

        public bool DeathTakesEvenTheUnkillable { get { return deathTakesEvenTheUnkillable; } }

        [Header("Arriving")]
        [Tooltip("It has an arrival animation rather than simply appearing.")]
        [SerializeField] private bool makesAnEntrance;

        [Tooltip("How long the arrival takes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float entranceSeconds = 1f;

        [Tooltip("The animation it plays as it arrives.")]
        [SerializeField] private string entranceAnimation = string.Empty;

        [Tooltip("It clears the ground around it as it arrives — bursting out rather than appearing.")]
        [SerializeField] private bool clearsTheGroundAsItArrives;

        [Tooltip("How far the ground is cleared.")]
        [Min(0f)]
        [SerializeField] private float clearedRadius = 2f;

        [Header("Idling")]
        [Tooltip("What it does with nothing else to do.")]
        [SerializeField] private DimensionIdleEmote[] idleEmotes = new DimensionIdleEmote[0];

        [Tooltip("Shortest gap between idle animations, in seconds.")]
        [Min(0f)]
        [SerializeField] private float minimumIdleGap = 1f;

        [Tooltip("Longest gap between idle animations, in seconds.")]
        [Min(0f)]
        [SerializeField] private float maximumIdleGap = 3f;

        [Header("The party")]
        [Tooltip("Its health rises with the number of players. 1 is full scaling; a boss wants this.")]
        [Range(0f, 1f)]
        [SerializeField] private float healthScalesWithPlayers;

        [Header("Other things")]
        [Tooltip("It follows a player carrying a lure.")]
        [SerializeField] private bool followsALure;

        [Tooltip("Something else can take control of it.")]
        [SerializeField] private bool somethingElseCanControlIt;

        [Header("Leaving")]
        [Tooltip("It despawns when no player is within this far. 0 means it never does.")]
        [Min(0f)]
        [SerializeField] private float despawnsWhenNobodyIsWithin;

        [Tooltip("How long it waits before despawning, in seconds.")]
        [Min(0f)]
        [SerializeField] private float despawnDelaySeconds = 5f;

        [Tooltip("When it dies it clears things around it. For a boss taking its minions with it.")]
        [SerializeField] private bool deathClearsThingsNearby;

        [Tooltip("How far that reaches.")]
        [Min(0f)]
        [SerializeField] private float deathClearRadius = 10f;

        [Tooltip("Anything temporary it summoned goes with it.")]
        [SerializeField] private bool itsSummonsGoWithIt = true;

        public bool MakesAnEntrance
        {
            get { return makesAnEntrance; }
        }

        public float EntranceSeconds
        {
            get { return entranceSeconds < 0f ? 0f : entranceSeconds; }
        }

        public string EntranceAnimation
        {
            get { return entranceAnimation ?? string.Empty; }
        }

        public bool ClearsTheGroundAsItArrives
        {
            get { return makesAnEntrance && clearsTheGroundAsItArrives; }
        }

        public float ClearedRadius
        {
            get { return clearedRadius < 0f ? 0f : clearedRadius; }
        }

        /// <summary>The idle animations, with the entries naming none dropped.</summary>
        public DimensionIdleEmote[] IdleEmotes
        {
            get
            {
                DimensionIdleEmote[] all = idleEmotes ?? new DimensionIdleEmote[0];
                System.Collections.Generic.List<DimensionIdleEmote> kept =
                    new System.Collections.Generic.List<DimensionIdleEmote>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].NamesAnAnimation)
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        public bool HasIdleEmotes
        {
            get { return IdleEmotes.Length > 0; }
        }

        public float MinimumIdleGap
        {
            get { return minimumIdleGap < 0f ? 0f : minimumIdleGap; }
        }

        /// <summary>The longest gap, never shorter than the shortest.</summary>
        public float MaximumIdleGap
        {
            get
            {
                float longest = maximumIdleGap < 0f ? 0f : maximumIdleGap;
                return longest < MinimumIdleGap ? MinimumIdleGap : longest;
            }
        }

        public float HealthScalesWithPlayers
        {
            get
            {
                if (healthScalesWithPlayers < 0f)
                {
                    return 0f;
                }

                return healthScalesWithPlayers > 1f ? 1f : healthScalesWithPlayers;
            }
        }

        public bool ScalesWithTheParty
        {
            get { return HealthScalesWithPlayers > 0f; }
        }

        public bool FollowsALure
        {
            get { return followsALure; }
        }

        public bool SomethingElseCanControlIt
        {
            get { return somethingElseCanControlIt; }
        }

        public float DespawnsWhenNobodyIsWithin
        {
            get { return despawnsWhenNobodyIsWithin < 0f ? 0f : despawnsWhenNobodyIsWithin; }
        }

        public bool EverDespawns
        {
            get { return DespawnsWhenNobodyIsWithin > 0f; }
        }

        public float DespawnDelaySeconds
        {
            get { return despawnDelaySeconds < 0f ? 0f : despawnDelaySeconds; }
        }

        public bool DeathClearsThingsNearby
        {
            get { return deathClearsThingsNearby; }
        }

        public float DeathClearRadius
        {
            get { return deathClearRadius < 0f ? 0f : deathClearRadius; }
        }

        public bool ItsSummonsGoWithIt
        {
            get { return itsSummonsGoWithIt; }
        }

        /// <summary>Whether it makes an entrance with no animation to make it with.</summary>
        public bool MakesAnEntranceWithNoAnimation
        {
            get { return makesAnEntrance && string.IsNullOrEmpty(EntranceAnimation); }
        }

        /// <summary>Whether its death clears an area of nothing.</summary>
        public bool DeathClearsNothing
        {
            get { return deathClearsThingsNearby && DeathClearRadius <= 0f; }
        }
    }
}
