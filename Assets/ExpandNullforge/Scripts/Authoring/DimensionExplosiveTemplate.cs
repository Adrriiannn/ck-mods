using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How hard an explosion shoves what it catches.</summary>
    public enum DimensionExplosionPushback
    {
        /// <summary>Nothing moves.</summary>
        None = 0,

        /// <summary>A nudge.</summary>
        Small = 1,

        /// <summary>What a bomb does. The vanilla default.</summary>
        Normal = 2
    }

    /// <summary>What sets a placed bomb off.</summary>
    /// <remarks>
    /// Every one of these ends the same way: something zeroes the bomb's health, the game marks it
    /// destroyed, and <c>ExplosiveSystem.ExplodeJob</c> turns that into a blast
    /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:406-408</c> — the job's query requires
    /// <c>EntityDestroyedCD</c>). Breaking the bomb by hand does the same thing, on every setting,
    /// which is how a pile of bombs chain-detonates.
    /// </remarks>
    public enum DimensionExplosiveTrigger
    {
        /// <summary>A lit fuse: it goes off a few seconds after it is placed.</summary>
        ACountdown = 0,

        /// <summary>It goes off when something walks close enough.</summary>
        SomethingComingClose = 1,

        /// <summary>It sits there until a wire gives it power.</summary>
        GettingPower = 2
    }

    /// <summary>What a blast leaves burning on the ground.</summary>
    /// <remarks>
    /// Both patches are the game's own fire, <c>ObjectID.Napalm</c> — variation 0 is the long one
    /// and variation 1 the short one (VERIFIED: <c>NapalmEntity.prefab</c> and
    /// <c>NapalmShortEntity.prefab</c> both carry <c>objectID: 6630</c>, differing only in
    /// <c>variation</c>). Nothing else in the game is spawned this way, so there is no third answer
    /// to offer.
    /// </remarks>
    public enum DimensionBlastLeavesBehind
    {
        /// <summary>Nothing. The blast happens and the ground is bare.</summary>
        Nothing = 0,

        /// <summary>A short-lived patch of fire.</summary>
        AShortPatchOfFire = 1,

        /// <summary>A long-lasting patch of fire.</summary>
        ALongPatchOfFire = 2
    }

    /// <summary>
    /// A thing that goes off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A BOMB IS TWO OBJECTS, and this is the half you place. It carries <c>ExplosiveAuthoring</c>
    /// (VERIFIED on 38 vanilla prefabs) and names a second object — the blast — which is what
    /// actually reaches out and hurts things. The framework generates that second object from the
    /// answers below, so an author never makes two things to get one bomb.
    /// </para>
    /// <para>
    /// WHICH HALF EACH NUMBER LIVES ON is not a detail, because it decides which ones are real.
    /// Damage and terrain damage travel from the bomb onto the blast every time it goes off
    /// (<c>ExplosiveSystem.cs:175-176</c> overwrites the blast's own copies), so they belong here.
    /// Reach does not travel — the blast's radius is used as authored — so <see cref="BlastReach"/>
    /// is written onto the generated blast instead.
    /// </para>
    /// <para>
    /// FACTION IS THE ONE THAT BITES. A blast with no inherited faction hurts everything in range
    /// including whoever set it off. Nine of the thirty-eight vanilla explosives set
    /// <c>explosionInheritsFaction</c> (VERIFIED by census: the affix fire bomb and the eight
    /// minions), and every one of those is something a creature uses — a bomb a player places is
    /// meant to hurt them if they stand in it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionExplosiveTemplate
    {
        /// <summary>The fuse every vanilla placed bomb uses, in seconds.</summary>
        /// <remarks>
        /// VERIFIED by census of the ripped prefabs: Bomb, SmallBomb, LargeBomb and SulfurBomb all
        /// author <c>DestroyTimerAuthoring.lifetime.pc = 4</c>; VoidBomb uses 3 and BlunderBomb and
        /// SeekerBomb use 5. Four is the middle and the most common.
        /// </remarks>
        public const float OrdinaryFuseSeconds = 4f;

        /// <summary>The reach an ordinary vanilla blast has, in tiles.</summary>
        /// <remarks>
        /// VERIFIED by census of all 30 prefabs carrying <c>ExplosionAuthoring</c>: radius runs from
        /// 1 to 4.5, and 2 is what <c>ExplosionEntity</c> — the blast an ordinary Bomb makes — uses.
        /// </remarks>
        public const float OrdinaryBlastReach = 2f;

        /// <summary>How much health every vanilla placed bomb has.</summary>
        /// <remarks>VERIFIED: all seven placed bombs author <c>maxHealth: 10</c>.</remarks>
        public const int OrdinaryToughness = 10;

        /// <summary>
        /// The furthest a blast can break terrain, in tiles, no matter how far it reaches.
        /// </summary>
        /// <remarks>
        /// The tile loop is a hard-coded 9x9 block centred on the blast
        /// (<c>ck-db\Pug.Other\ExplosionDamageSystem.cs:383-385</c>, <c>for j in -4..4</c>). Reach
        /// beyond this still catches creatures; it just cannot dig.
        /// </remarks>
        public const float TerrainReachCap = 4f;

        [Tooltip("It explodes at all.")]
        [SerializeField] private bool explodes;

        [Header("What sets it off")]
        [Tooltip("What makes it go off once it is placed. Breaking it always works too.")]
        [SerializeField] private DimensionExplosiveTrigger setOffBy = DimensionExplosiveTrigger.ACountdown;

        [Tooltip("Seconds between placing it and the bang.")]
        [Min(0f)]
        [SerializeField] private float secondsBeforeItGoesOff = OrdinaryFuseSeconds;

        [Tooltip("How close something has to come, in tiles. The game's own proximity bomb uses 1.")]
        [Min(0f)]
        [SerializeField] private float howCloseSomethingHasToCome = 1f;

        [Tooltip("Seconds between being triggered and going off. The game's own proximity bomb uses 0.5.")]
        [Min(0f)]
        [SerializeField] private float secondsAfterItIsTriggered = 0.5f;

        [Tooltip("How much punishment it takes to break it by hand. Every vanilla bomb uses 10.")]
        [Min(1)]
        [SerializeField] private int howToughItIs = OrdinaryToughness;

        [Header("What the blast does")]
        [Tooltip("How much it hurts what it catches.")]
        [Min(0)]
        [SerializeField] private int hurtsCreaturesBy = 20;

        [Tooltip("How much of the terrain it breaks. Measured against the mining curve rather than " +
            "health, so it runs into the hundreds when it is used at all. 0 leaves the walls alone.")]
        [Min(0)]
        [SerializeField] private int breaksTerrainBy;

        [Tooltip("How far it reaches, in tiles. Vanilla runs from 1 to 4.5, and an ordinary bomb " +
            "uses 2. Terrain only breaks within 4 tiles no matter what this says.")]
        [Min(0f)]
        [SerializeField] private float blastReach = OrdinaryBlastReach;

        [Tooltip("What it leaves burning where it went off.")]
        [SerializeField] private DimensionBlastLeavesBehind leavesBehind = DimensionBlastLeavesBehind.Nothing;

        [Tooltip("How hard it shoves what it catches.")]
        [SerializeField] private DimensionExplosionPushback pushback = DimensionExplosionPushback.Normal;

        [Header("Who it spares")]
        [Tooltip("The blast takes the side of whoever set it off, so it spares them.")]
        [SerializeField] private bool sparesWhoeverSetItOff;

        [Tooltip("The bomb itself takes their side too.")]
        [SerializeField] private bool theBombTakesTheirSideToo;

        [Tooltip("Other explosions nearby do not set this one off. It is then spent for good.")]
        [SerializeField] private bool otherBlastsDoNotSetItOff;

        [Header("If it scales with its tier")]
        [Tooltip("Damage multiplier, used instead of the flat number when the item scales with its tier.")]
        [Min(0f)]
        [SerializeField] private float damageMultiplier = 1f;

        [Tooltip("Terrain-damage multiplier, used instead of the flat number when the item scales " +
            "with its tier.")]
        [Min(0f)]
        [SerializeField] private float terrainDamageMultiplier = 1f;

        [Header("Advanced")]
        [Tooltip("Use this blast object instead of generating one. Leave empty for the usual case.")]
        [SerializeField] private string explosionObjectId = string.Empty;

        [Tooltip("Which variation of that blast object.")]
        [Min(0)]
        [SerializeField] private int explosionVariation;

        public bool Explodes
        {
            get { return explodes; }
        }

        public DimensionExplosiveTrigger SetOffBy
        {
            get { return setOffBy; }
        }

        public float SecondsBeforeItGoesOff
        {
            get { return secondsBeforeItGoesOff < 0f ? 0f : secondsBeforeItGoesOff; }
        }

        public float HowCloseSomethingHasToCome
        {
            get { return howCloseSomethingHasToCome < 0f ? 0f : howCloseSomethingHasToCome; }
        }

        public float SecondsAfterItIsTriggered
        {
            get { return secondsAfterItIsTriggered < 0f ? 0f : secondsAfterItIsTriggered; }
        }

        public int HowToughItIs
        {
            get { return howToughItIs < 1 ? 1 : howToughItIs; }
        }

        public int HurtsCreaturesBy
        {
            get { return hurtsCreaturesBy < 0 ? 0 : hurtsCreaturesBy; }
        }

        public int BreaksTerrainBy
        {
            get { return breaksTerrainBy < 0 ? 0 : breaksTerrainBy; }
        }

        public float BlastReach
        {
            get { return blastReach < 0f ? 0f : blastReach; }
        }

        public DimensionBlastLeavesBehind LeavesBehind
        {
            get { return leavesBehind; }
        }

        public DimensionExplosionPushback Pushback
        {
            get { return pushback; }
        }

        public bool SparesWhoeverSetItOff
        {
            get { return sparesWhoeverSetItOff; }
        }

        public bool TheBombTakesTheirSideToo
        {
            get { return theBombTakesTheirSideToo; }
        }

        public bool OtherBlastsDoNotSetItOff
        {
            get { return otherBlastsDoNotSetItOff; }
        }

        public float DamageMultiplier
        {
            get { return damageMultiplier < 0f ? 0f : damageMultiplier; }
        }

        public float TerrainDamageMultiplier
        {
            get { return terrainDamageMultiplier < 0f ? 0f : terrainDamageMultiplier; }
        }

        /// <summary>A blast object named by hand, or empty when the framework should make one.</summary>
        public string ExplosionObjectId
        {
            get { return explodes ? (explosionObjectId ?? string.Empty) : string.Empty; }
        }

        public int ExplosionVariation
        {
            get { return explosionVariation < 0 ? 0 : explosionVariation; }
        }

        /// <summary>Whether the framework generates the blast rather than the author naming one.</summary>
        public bool MakesItsOwnBlast
        {
            get { return explodes && string.IsNullOrEmpty(ExplosionObjectId); }
        }

        /// <summary>
        /// The napalm variation the game spawns for <see cref="LeavesBehind"/>, or -1 for none.
        /// </summary>
        /// <remarks>
        /// The mapping is inverted on purpose: the game's variation 1 is the SHORT patch, so the
        /// long one — the more dramatic answer — is variation 0.
        /// </remarks>
        public int NapalmVariation
        {
            get
            {
                switch (leavesBehind)
                {
                    case DimensionBlastLeavesBehind.AShortPatchOfFire: return 1;
                    case DimensionBlastLeavesBehind.ALongPatchOfFire: return 0;
                    default: return -1;
                }
            }
        }

        /// <summary>Whether it explodes and does nothing when it does.</summary>
        public bool ExplodesHarmlessly
        {
            get { return explodes && HurtsCreaturesBy == 0 && BreaksTerrainBy == 0; }
        }

        /// <summary>Whether it explodes and catches nothing, because the blast has no reach.</summary>
        public bool ExplodesAndReachesNothing
        {
            get { return explodes && MakesItsOwnBlast && BlastReach <= 0f; }
        }

        /// <summary>
        /// Whether the reach asked for is further than terrain damage can ever travel.
        /// </summary>
        /// <remarks>
        /// Worth saying out loud only when the author actually asked for terrain damage — a bomb
        /// with a wide reach and no digging is an ordinary, correct thing to build.
        /// </remarks>
        public bool DigsLessFarThanItReaches
        {
            get { return explodes && BreaksTerrainBy > 0 && BlastReach > TerrainReachCap; }
        }

        /// <summary>Whether it can never be set off, because its fuse never runs out.</summary>
        public bool HasAFuseThatNeverBurnsDown
        {
            get
            {
                return explodes &&
                       setOffBy == DimensionExplosiveTrigger.ACountdown &&
                       SecondsBeforeItGoesOff <= 0f;
            }
        }

        /// <summary>Whether it waits for something to come close and nothing ever can.</summary>
        public bool WaitsForSomethingThatCannotArrive
        {
            get
            {
                return explodes &&
                       setOffBy == DimensionExplosiveTrigger.SomethingComingClose &&
                       HowCloseSomethingHasToCome <= 0f;
            }
        }
    }
}
