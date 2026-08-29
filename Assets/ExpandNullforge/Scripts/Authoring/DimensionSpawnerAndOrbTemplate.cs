using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Simple spawners, drifting orbs, followers, statues, and the last of the world's odds and ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ORB IS THE ONE WORTH READING. An electric orb does not chase — it drifts through a list
    /// of movement patterns, each for a random length of time at a random speed, optionally weaving
    /// as it goes. That is why it feels like weather rather than an enemy, and it is a genuinely
    /// different way of building a hazard.
    /// </para>
    /// <para>
    /// A PLAIN SPAWNER IS NOT A NEST. The nest template produces things around itself forever. This
    /// one keeps a count, forgets about what it made once a player is far enough away, and can stop
    /// entirely when nothing is moving — it is the quiet background populating the world does.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSpawnerAndOrbTemplate
    {
        [Header("A plain spawner")]
        [Tooltip("It quietly produces things nearby and keeps a count.")]
        [SerializeField] private bool isAPlainSpawner;

        [Tooltip("Closest to itself it will put one.")]
        [Min(0f)]
        [SerializeField] private float spawnsNoCloserThan = 5f;

        [Tooltip("Furthest out it will put one.")]
        [Min(0f)]
        [SerializeField] private float spawnsNoFurtherThan = 20f;

        [Tooltip("How many it keeps track of at once.")]
        [Min(0)]
        [SerializeField] private int keepsTrackOf = 10;

        [Tooltip("How far a player has to get before it forgets what it made.")]
        [Min(0f)]
        [SerializeField] private float forgetsBeyond = 60f;

        [Tooltip("It stops producing while nothing is moving.")]
        [SerializeField] private bool stopsWhenStill;

        [Header("Territory spawners")]
        [Tooltip("It claims a larva territory of this size. 0 for none.")]
        [Min(0)]
        [SerializeField] private int larvaTerritorySize;

        [Tooltip("It claims a slime territory of this size. 0 for none.")]
        [Min(0)]
        [SerializeField] private int slimeTerritorySize;

        [Tooltip("Chance a slime blob appears in that territory.")]
        [Range(0f, 1f)]
        [SerializeField] private float slimeBlobChance;

        [Header("A drifting orb")]
        [Tooltip("It is a drifting orb — it wanders through movement patterns rather than chasing.")]
        [SerializeField] private bool isADriftingOrb;

        [Tooltip("How long it takes to appear.")]
        [Min(0f)]
        [SerializeField] private float orbAppearSeconds = 0.5f;

        [Tooltip("How long it drifts for.")]
        [Min(0f)]
        [SerializeField] private float orbDriftSeconds = 5f;

        [Tooltip("How long it takes to fade.")]
        [Min(0f)]
        [SerializeField] private float orbFadeSeconds = 0.5f;

        [Tooltip("How long it stays gone before it can come back.")]
        [Min(0f)]
        [SerializeField] private float orbHiddenSeconds = 2f;

        [Tooltip("It bounces off walls rather than stopping at them.")]
        [SerializeField] private bool orbBouncesOffWalls = true;

        [Tooltip("The patterns it drifts through, in order.")]
        [SerializeField] private DimensionOrbDrift[] orbPatterns = new DimensionOrbDrift[0];

        [Header("Following something")]
        [Tooltip("It wanders near a particular object rather than roaming freely.")]
        [SerializeField] private bool wandersNearSomething;

        [Tooltip("What it stays near.")]
        [SerializeField] private string staysNearObjectId = string.Empty;

        [Tooltip("Closest it comes to that.")]
        [Min(0f)]
        [SerializeField] private float staysNoCloserThan = 0.5f;

        [Tooltip("Furthest it strays from it.")]
        [Min(0f)]
        [SerializeField] private float straysNoFurtherThan = 2f;

        [Tooltip("Longest it walks in one go.")]
        [Min(0f)]
        [SerializeField] private float walksForAtMost = 3f;

        [Tooltip("Shortest it stands still between walks.")]
        [Min(0f)]
        [SerializeField] private float restsAtLeast = 0.5f;

        [Tooltip("Longest it stands still.")]
        [Min(0f)]
        [SerializeField] private float restsAtMost = 1f;

        [Header("A boss statue")]
        [Tooltip("It is a statue that lights when the right crystal is brought to it.")]
        [SerializeField] private bool isABossStatue;

        [Tooltip("Which crystal it accepts.")]
        [SerializeField] private string acceptsCrystalId = string.Empty;

        [Tooltip("How long it takes to charge once the crystal is in.")]
        [Min(0f)]
        [SerializeField] private float statueChargeSeconds = 3f;

        [Header("Chewing terrain as it roams")]
        [Tooltip("It breaks the ground it roams over.")]
        [SerializeField] private bool chewsGroundAsItRoams;

        [Tooltip("How wide it chews.")]
        [Min(0f)]
        [SerializeField] private float chewRadius = 1f;

        [Tooltip("How far in front of it that chewing reaches.")]
        [Min(0f)]
        [SerializeField] private float chewReach = 1f;

        [Tooltip("Objects it will never break while roaming.")]
        [SerializeField] private string[] neverBreaks = new string[0];

        [Header("Pulling things toward it")]
        [Tooltip("It is a gravity well — it pulls wandering things toward itself.")]
        [SerializeField] private bool pullsThingsIn;

        [Tooltip("How far that pull reaches.")]
        [Min(0f)]
        [SerializeField] private float pullReaches = 8f;

        [Tooltip("Whether it pulls at all. 0 pulls nothing; anything above 0 pulls the same " +
            "things, because the game only checks the number is not 0.")]
        [Min(0)]
        [SerializeField] private int pullsOnLayers;

        public bool IsAPlainSpawner { get { return isAPlainSpawner; } }

        public float SpawnsNoCloserThan { get { return Floor(spawnsNoCloserThan); } }

        public float SpawnsNoFurtherThan
        {
            get { return AtLeast(spawnsNoFurtherThan, SpawnsNoCloserThan); }
        }

        public int KeepsTrackOf { get { return keepsTrackOf < 0 ? 0 : keepsTrackOf; } }

        public float ForgetsBeyond { get { return Floor(forgetsBeyond); } }

        public bool StopsWhenStill { get { return stopsWhenStill; } }

        public int LarvaTerritorySize
        {
            get { return larvaTerritorySize < 0 ? 0 : larvaTerritorySize; }
        }

        public int SlimeTerritorySize
        {
            get { return slimeTerritorySize < 0 ? 0 : slimeTerritorySize; }
        }

        public float SlimeBlobChance { get { return Clamp01(slimeBlobChance); } }

        public bool IsADriftingOrb { get { return isADriftingOrb; } }

        public float OrbAppearSeconds { get { return Floor(orbAppearSeconds); } }

        public float OrbDriftSeconds { get { return Floor(orbDriftSeconds); } }

        public float OrbFadeSeconds { get { return Floor(orbFadeSeconds); } }

        public float OrbHiddenSeconds { get { return Floor(orbHiddenSeconds); } }

        public bool OrbBouncesOffWalls { get { return orbBouncesOffWalls; } }

        public DimensionOrbDrift[] OrbPatterns
        {
            get { return orbPatterns ?? new DimensionOrbDrift[0]; }
        }

        public bool WandersNearSomething { get { return wandersNearSomething; } }

        public string StaysNearObjectId { get { return staysNearObjectId ?? string.Empty; } }

        public float StaysNoCloserThan { get { return Floor(staysNoCloserThan); } }

        public float StraysNoFurtherThan
        {
            get { return AtLeast(straysNoFurtherThan, StaysNoCloserThan); }
        }

        public float WalksForAtMost { get { return Floor(walksForAtMost); } }

        public float RestsAtLeast { get { return Floor(restsAtLeast); } }

        public float RestsAtMost { get { return AtLeast(restsAtMost, RestsAtLeast); } }

        public bool IsABossStatue { get { return isABossStatue; } }

        public string AcceptsCrystalId { get { return acceptsCrystalId ?? string.Empty; } }

        public float StatueChargeSeconds { get { return Floor(statueChargeSeconds); } }

        public bool ChewsGroundAsItRoams { get { return chewsGroundAsItRoams; } }

        public float ChewRadius { get { return Floor(chewRadius); } }

        public float ChewReach { get { return Floor(chewReach); } }

        public string[] NeverBreaks { get { return neverBreaks ?? new string[0]; } }

        public bool PullsThingsIn { get { return pullsThingsIn; } }

        public float PullReaches { get { return Floor(pullReaches); } }

        public int PullsOnLayers { get { return pullsOnLayers < 0 ? 0 : pullsOnLayers; } }

        /// <summary>Whether the orb drifts with no pattern to drift through.</summary>
        public bool OrbHasNoPattern
        {
            get { return isADriftingOrb && OrbPatterns.Length == 0; }
        }

        /// <summary>Whether it wanders near nothing in particular.</summary>
        public bool WandersNearNothing
        {
            get { return wandersNearSomething && string.IsNullOrEmpty(StaysNearObjectId); }
        }

        /// <summary>Whether it is a statue that accepts no crystal.</summary>
        public bool StatueAcceptsNothing
        {
            get { return isABossStatue && string.IsNullOrEmpty(AcceptsCrystalId); }
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

    /// <summary>One stretch of an orb's drift.</summary>
    [Serializable]
    public struct DimensionOrbDrift
    {
        [Tooltip("How it moves during this stretch, by the game's own pattern name.")]
        [SerializeField] private string pattern;

        [Tooltip("Shortest and longest this stretch lasts.")]
        [SerializeField] private Vector2 secondsRange;

        [Tooltip("Slowest and fastest it travels during it.")]
        [SerializeField] private Vector2 speedRange;

        [Tooltip("It weaves side to side through this stretch.")]
        [SerializeField] private bool weaves;

        [Tooltip("How far the weave turns it, in degrees.")]
        [Min(0f)]
        [SerializeField] private float weaveAngle;

        [Tooltip("How many times a second the weave repeats.")]
        [Min(0f)]
        [SerializeField] private float weaveRate;

        public string Pattern { get { return pattern ?? string.Empty; } }

        public Vector2 SecondsRange { get { return Ordered(secondsRange); } }

        public Vector2 SpeedRange { get { return Ordered(speedRange); } }

        public bool Weaves { get { return weaves; } }

        public float WeaveAngle { get { return weaveAngle < 0f ? 0f : weaveAngle; } }

        public float WeaveRate { get { return weaveRate < 0f ? 0f : weaveRate; } }

        private static Vector2 Ordered(Vector2 range)
        {
            float low = range.x < 0f ? 0f : range.x;
            float high = range.y < 0f ? 0f : range.y;
            return new Vector2(low, high < low ? low : high);
        }
    }
}
