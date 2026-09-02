using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A boss kit borrowed from one of the game's own — the Hydra's burrow-and-surface fight, or the
    /// Slime's slam.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE BORROW DOOR. A custom boss does not have to invent everything: giving it the
    /// Hydra's kit means it burrows, surfaces, and fires the same six attacks, and every one of
    /// those attacks is then a number you can change. Take it as it is and it fights exactly like a
    /// Hydra with your sprite on it; change the beam's damage and only the beam changes.
    /// </para>
    /// <para>
    /// EVERY ATTACK HAS A FLAT NUMBER AND A MULTIPLIER, and the multiplier is the one that survives.
    /// On any boss carrying a tier the flat number is recomputed away and the multiplier is what
    /// actually scales it — the same trap as every other attack in the game.
    /// </para>
    /// <para>
    /// THE HYDRA TYPE PICKS ITS ELEMENT. Nature, Sea, Desert and Void are the four vanilla ones, and
    /// the type decides which of the six attacks it actually uses — a Sea Hydra throws ice shards
    /// where a Desert one does not.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBorrowedBossKitTemplate
    {
        [Header("The Hydra's kit")]
        [Tooltip("It fights like one of the Hydras — burrowing, surfacing, and its family of attacks. A custom one gets all of that; the four variant flourishes (the Sea's ice shards, the Nature's stalactites, the Void's fourth head, the Desert's timing) are the game's own and only run for its own Hydras.")]
        [SerializeField] private bool fightsLikeAHydra;

        [Tooltip("Which Hydra. The type decides which of its attacks it actually uses.")]
        [SerializeField] private DimensionHydraKind hydraKind = DimensionHydraKind.Nature;

        [Tooltip("The weak point that appears while it is vulnerable. None for no weak point.")]
        [SerializeField] private GameObject weakPointPrefab;

        [Header("Burrowing")]
        [Tooltip("How long it takes to bury itself.")]
        [Min(0f)]
        [SerializeField] private float buryingSeconds = 1f;

        [Tooltip("How long it takes to come back up.")]
        [Min(0f)]
        [SerializeField] private float surfacingSeconds = 1f;

        [Tooltip("Shortest it stays under.")]
        [Min(0f)]
        [SerializeField] private float minSecondsUnderground = 3f;

        [Tooltip("Longest it stays under.")]
        [Min(0f)]
        [SerializeField] private float maxSecondsUnderground = 6f;

        [Header("Its attacks — flat damage, then how hard for its tier")]
        [Tooltip("Damage from bursting out of the ground.")]
        [Min(0)]
        [SerializeField] private int surfacingSlamDamage;

        [Tooltip("How hard that slam hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float surfacingSlamMultiplier = 1f;

        [Tooltip("Damage from its beam.")]
        [Min(0)]
        [SerializeField] private int beamDamage;

        [Tooltip("How hard the beam hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float beamMultiplier = 1f;

        [Tooltip("Damage from the stalactites it drops.")]
        [Min(0)]
        [SerializeField] private int stalactiteDamage;

        [Tooltip("How hard stalactites hit for its tier.")]
        [Min(0f)]
        [SerializeField] private float stalactiteMultiplier = 1f;

        [Tooltip("Damage from its shockwave.")]
        [Min(0)]
        [SerializeField] private int shockwaveDamage;

        [Tooltip("How hard the shockwave hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float shockwaveMultiplier = 1f;

        [Tooltip("Damage from the ice shards it throws. Sea Hydra.")]
        [Min(0)]
        [SerializeField] private int iceShardDamage;

        [Tooltip("How hard ice shards hit for its tier.")]
        [Min(0f)]
        [SerializeField] private float iceShardMultiplier = 1f;

        [Tooltip("Damage from the lava it throws. Desert Hydra.")]
        [Min(0)]
        [SerializeField] private int lavaDamage;

        [Tooltip("How hard the lava hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float lavaMultiplier = 1f;

        [Tooltip("Damage from the nilipedes it lobs. Nature Hydra.")]
        [Min(0)]
        [SerializeField] private int nilipedeDamage;

        [Tooltip("How hard nilipedes hit for its tier.")]
        [Min(0f)]
        [SerializeField] private float nilipedeMultiplier = 1f;

        [Header("The Slime's shot cycling")]
        [Tooltip("It varies its shots the way the Slime King does — every fourth one is different, and it fires faster when enraged. The game only does this for the Lava Slime Boss itself, so on anything else the setting is carried but nothing acts on it.")]
        [SerializeField] private bool cyclesItsShotsLikeTheSlimeKing;

        [Header("The Slime's slam")]
        [Tooltip("It slams like the Slime boss — a leap that lands hard and leaves slime.")]
        [SerializeField] private bool slamsLikeTheSlime;

        [Tooltip("Wind-up before the leap.")]
        [Min(0f)]
        [SerializeField] private float slamWindUp = 0.5f;

        [Tooltip("How long it hangs in the air.")]
        [Min(0f)]
        [SerializeField] private float slamAirTime = 1f;

        [Tooltip("How long the landing takes.")]
        [Min(0f)]
        [SerializeField] private float slamLandTime = 0.5f;

        [Tooltip("How fast it travels while leaping.")]
        [Min(0f)]
        [SerializeField] private float slamSpeed = 5f;

        [Tooltip("Wind-up once it is enraged — usually shorter.")]
        [Min(0f)]
        [SerializeField] private float enragedSlamWindUp = 0.3f;

        [Tooltip("Air time once enraged.")]
        [Min(0f)]
        [SerializeField] private float enragedSlamAirTime = 0.7f;

        [Tooltip("Leap speed once enraged.")]
        [Min(0f)]
        [SerializeField] private float enragedSlamSpeed = 8f;

        [Tooltip("Which ground it leaves where it lands. Blank leaves the ground as it was.")]
        [SerializeField] private string slamLeavesTilesetId = string.Empty;

        [Tooltip("Flat slam damage.")]
        [Min(0)]
        [SerializeField] private int slamDamage;

        [Tooltip("How hard the slam hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float slamMultiplier = 1f;

        public bool FightsLikeAHydra { get { return fightsLikeAHydra; } }

        public DimensionHydraKind HydraKind { get { return hydraKind; } }

        public GameObject WeakPointPrefab { get { return weakPointPrefab; } }

        public float BuryingSeconds { get { return buryingSeconds < 0f ? 0f : buryingSeconds; } }

        public float SurfacingSeconds
        {
            get { return surfacingSeconds < 0f ? 0f : surfacingSeconds; }
        }

        public float MinSecondsUnderground
        {
            get { return minSecondsUnderground < 0f ? 0f : minSecondsUnderground; }
        }

        public float MaxSecondsUnderground
        {
            get
            {
                float longest = maxSecondsUnderground < 0f ? 0f : maxSecondsUnderground;
                return longest < MinSecondsUnderground ? MinSecondsUnderground : longest;
            }
        }

        public int SurfacingSlamDamage
        {
            get { return surfacingSlamDamage < 0 ? 0 : surfacingSlamDamage; }
        }

        public float SurfacingSlamMultiplier { get { return Clamp(surfacingSlamMultiplier); } }

        public int BeamDamage { get { return beamDamage < 0 ? 0 : beamDamage; } }

        public float BeamMultiplier { get { return Clamp(beamMultiplier); } }

        public int StalactiteDamage
        {
            get { return stalactiteDamage < 0 ? 0 : stalactiteDamage; }
        }

        public float StalactiteMultiplier { get { return Clamp(stalactiteMultiplier); } }

        public int ShockwaveDamage { get { return shockwaveDamage < 0 ? 0 : shockwaveDamage; } }

        public float ShockwaveMultiplier { get { return Clamp(shockwaveMultiplier); } }

        public int IceShardDamage { get { return iceShardDamage < 0 ? 0 : iceShardDamage; } }

        public float IceShardMultiplier { get { return Clamp(iceShardMultiplier); } }

        public int LavaDamage { get { return lavaDamage < 0 ? 0 : lavaDamage; } }

        public float LavaMultiplier { get { return Clamp(lavaMultiplier); } }

        public int NilipedeDamage { get { return nilipedeDamage < 0 ? 0 : nilipedeDamage; } }

        public float NilipedeMultiplier { get { return Clamp(nilipedeMultiplier); } }

        public bool CyclesItsShotsLikeTheSlimeKing
        {
            get { return cyclesItsShotsLikeTheSlimeKing; }
        }

        public bool SlamsLikeTheSlime { get { return slamsLikeTheSlime; } }

        public float SlamWindUp { get { return slamWindUp < 0f ? 0f : slamWindUp; } }

        public float SlamAirTime { get { return slamAirTime < 0f ? 0f : slamAirTime; } }

        public float SlamLandTime { get { return slamLandTime < 0f ? 0f : slamLandTime; } }

        public float SlamSpeed { get { return slamSpeed < 0f ? 0f : slamSpeed; } }

        public float EnragedSlamWindUp
        {
            get { return enragedSlamWindUp < 0f ? 0f : enragedSlamWindUp; }
        }

        public float EnragedSlamAirTime
        {
            get { return enragedSlamAirTime < 0f ? 0f : enragedSlamAirTime; }
        }

        public float EnragedSlamSpeed
        {
            get { return enragedSlamSpeed < 0f ? 0f : enragedSlamSpeed; }
        }

        public string SlamLeavesTilesetId
        {
            get { return slamLeavesTilesetId ?? string.Empty; }
        }

        public int SlamDamage { get { return slamDamage < 0 ? 0 : slamDamage; } }

        public float SlamMultiplier { get { return Clamp(slamMultiplier); } }

        /// <summary>
        /// Whether its enraged slam is slower or lazier than its ordinary one.
        /// </summary>
        /// <remarks>
        /// Enraging is supposed to make a boss more dangerous. A longer wind-up or a slower leap
        /// once enraged makes the fight get EASIER when the music says it should get harder, which
        /// reads as a bug to everyone who plays it.
        /// </remarks>
        public bool EnragingMakesItLessDangerous
        {
            get
            {
                return slamsLikeTheSlime
                    && (enragedSlamWindUp > slamWindUp || enragedSlamSpeed < slamSpeed);
            }
        }

        private static float Clamp(float value)
        {
            return value < 0f ? 0f : value;
        }
    }

    /// <summary>
    /// Which Hydra a borrowed kit fights like. Core Keeper's <c>HydraBossType</c>.
    /// </summary>
    /// <remarks>
    /// The order matches the game's enum and must stay that way. The type decides which of the six
    /// attacks are actually used, so it is not only a skin.
    /// </remarks>
    public enum DimensionHydraKind
    {
        Nature = 0,
        Sea = 1,
        Desert = 2,
        Void = 3
    }
}
