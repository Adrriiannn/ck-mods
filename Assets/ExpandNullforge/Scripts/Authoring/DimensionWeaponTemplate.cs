using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What kind of weapon something is, in the terms Core Keeper actually distinguishes.
    /// </summary>
    /// <remarks>
    /// Three separate components, not one with a mode: <c>MeleeWeaponAuthoring</c> (68 vanilla
    /// prefabs), <c>RangeWeaponAuthoring</c> (42) and <c>CastItemAuthoring</c> (63). A weapon carries
    /// exactly one of them, so this is a choice rather than a set of ticks.
    /// </remarks>
    public enum DimensionWeaponKind
    {
        /// <summary>Not a weapon. Most items.</summary>
        NotAWeapon = 0,

        /// <summary>Swung. Swords, spears, axes. (68 vanilla prefabs)</summary>
        Melee = 1,

        /// <summary>Fires something. Bows, guns, staffs that shoot. (42)</summary>
        Ranged = 2,

        /// <summary>Cast and held, doing a job rather than damage. Scanners, cages, wands. (63)</summary>
        Cast = 3
    }

    /// <summary>How wide a melee swing reaches around the player.</summary>
    /// <remarks>
    /// Measured across the game: 30 weapons swing the full circle, 26 swing a quarter, 10 swing
    /// three quarters. The rest are almost unused, and are kept because a weapon that swings a
    /// narrow 45 degrees is a perfectly good idea nobody has shipped yet.
    /// </remarks>
    public enum DimensionSwingArc
    {
        Narrow45 = 0,
        Quarter90 = 1,
        Wide135 = 2,
        Half180 = 3,
        ThreeQuarter270 = 4,
        FullCircle360 = 5
    }

    /// <summary>The flourish drawn when a melee weapon connects.</summary>
    public enum DimensionAttackFlourish
    {
        /// <summary>Nothing. What 36 of the game's 68 melee weapons do.</summary>
        None = 0,

        /// <summary>A straight line. A thrust.</summary>
        Line = 1,

        /// <summary>An arc. What most weapons that draw anything use.</summary>
        Arc = 2,

        /// <summary>A shockwave.</summary>
        Shockwave = 3
    }

    /// <summary>What a cast item is for.</summary>
    /// <remarks>
    /// These are Core Keeper's own jobs, not damage. Inventing more would be pointless: the game
    /// only knows how to do these nine, and the runtime switch is on this enum.
    /// </remarks>
    public enum DimensionCastPurpose
    {
        Nothing = 0,
        CombineMaterials = 1,
        OpenContainer = 2,
        TeleportToCore = 3,
        ScanWorld = 4,
        LeashCattle = 5,
        CageCattle = 6,
        OpenCrackedEgg = 7,
        SummonEntity = 8
    }

    /// <summary>
    /// What makes a weapon a weapon.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The item archetype has had a <c>Weapon</c> option all along, and generating one wrote
    /// <c>WeaponDamageAuthoring</c> and nothing else — a number with no swing behind it. This is the
    /// component that actually makes the player swing, fire or cast.
    /// </para>
    /// <para>
    /// FIELDS CHOSEN FROM MEASURED USE. <c>RangeWeaponAuthoring</c> alone has 22 public fields, and
    /// across the 42 vanilla ranged weapons nearly all of them are zero: <c>extraProjectiles</c> is
    /// zero on 40, <c>explosionSize</c> on 41, <c>recoilForce</c> on 39. What a ranged weapon really
    /// is, is the thing it fires. The mortar fields are a whole sub-system used by a handful of boss
    /// weapons and are deliberately left out rather than shipped as dead knobs.
    /// </para>
    /// <para>
    /// Two melee fields are used by <b>zero</b> vanilla weapons — <c>tileDamageAOE</c> and
    /// <c>disable</c> — and are not offered, on the same principle.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionWeaponTemplate
    {
        /// <summary>The reach 47 of the game's 68 melee weapons use.</summary>
        public const float OrdinaryReach = 1.5f;

        [Tooltip("What kind of weapon this is. Core Keeper keeps these three separate.")]
        [SerializeField] private DimensionWeaponKind kind = DimensionWeaponKind.NotAWeapon;

        [Header("Swung")]
        [Tooltip("How far the swing reaches. 1.5 is ordinary; 1 is short, 1.8 is long.")]
        [Min(0f)]
        [SerializeField] private float reach = OrdinaryReach;

        [Tooltip("Extra reach on top, for something with a long head.")]
        [Min(0f)]
        [SerializeField] private float extraReach;

        [Tooltip("How wide the swing goes around the player.")]
        [SerializeField] private DimensionSwingArc swingArc = DimensionSwingArc.Quarter90;

        [Tooltip("The flourish drawn when it connects.")]
        [SerializeField] private DimensionAttackFlourish flourish = DimensionAttackFlourish.None;

        [Tooltip("How hard it pulls the player forward as it swings.")]
        [Min(0f)]
        [SerializeField] private float lunge;

        [Tooltip("A short, fast hit rather than a full swing.")]
        [SerializeField] private bool quickHit;

        [Tooltip("It thrusts like a big spear.")]
        [SerializeField] private bool thrustsLikeASpear;

        [Tooltip("It swings like a big two-hander.")]
        [SerializeField] private bool swingsLikeATwoHander;

        [Tooltip("It skips the wind-up animation and goes straight to the hit.")]
        [SerializeField] private bool skipsTheWindUpAnimation;

        [Header("Fired")]
        [Tooltip("What it fires. A projectile from this mod, or a vanilla projectile object name.")]
        [SerializeField] private string firesProjectileId = string.Empty;

        [Tooltip("How many extra shots go out alongside the first.")]
        [Min(0)]
        [SerializeField] private int extraShots;

        [Tooltip("How far apart the shots spread, in degrees.")]
        [Min(0f)]
        [SerializeField] private float spreadAngle;

        [Tooltip("How far in front of the player the shot appears.")]
        [Min(0f)]
        [SerializeField] private float muzzleDistance;

        [Tooltip("The shot points wherever the player aims rather than snapping to a facing.")]
        [SerializeField] private bool aimsFreely;

        [Tooltip("How hard firing pushes the player back.")]
        [Min(0f)]
        [SerializeField] private float recoil;

        [Header("Cast")]
        [Tooltip("What casting it does.")]
        [SerializeField] private DimensionCastPurpose castPurpose = DimensionCastPurpose.Nothing;

        [Tooltip("How long the cast takes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float castSeconds = 1f;

        [Tooltip("Holding the button casts it again rather than casting once.")]
        [SerializeField] private bool holdingRepeatsIt;

        [Header("Ranged: what else comes out")]
        [Tooltip("It fires a random one of these instead of its single projectile.")]
        [SerializeField] private bool firesARandomProjectile;

        [Tooltip("The projectiles it picks from, when it fires a random one. The game's, or your own.")]
        [SerializeField] private string[] randomProjectileIds = new string[0];

        [Tooltip("The shot it fires instead once its wind-up is full. One of the game's projectiles, or one of yours. Empty for none.")]
        [SerializeField] private string secondProjectileId = string.Empty;

        [Tooltip("Its shots pass through what they hit once the wind-up is full.")]
        [SerializeField] private bool piercesAtFullWindUp;

        [Tooltip("Its shots bounce once the wind-up is full.")]
        [SerializeField] private bool bouncesAtFullWindUp;

        [Tooltip("An animation name to play instead of the usual one.")]
        [SerializeField] private string overrideAnimation = string.Empty;

        [Header("Ranged: the blast it makes")]
        [Tooltip("How big the explosion is. 0 for a weapon that does not explode.")]
        [Min(0)]
        [SerializeField] private int explosionSize;

        [Tooltip("The explosion does the weapon's own damage rather than the explosion's.")]
        [SerializeField] private bool explosionUsesWeaponDamage;

        [Header("Ranged: lobbing it like a mortar")]
        [Tooltip("It arcs onto where the player aimed rather than flying straight.")]
        [SerializeField] private bool lobsOntoTheAimPoint;

        [Tooltip("How far out it can be lobbed. Vanilla uses 7.")]
        [Min(0f)]
        [SerializeField] private float lobRange;

        [Tooltip("Nearest and furthest a lobbed shot scatters from the aim point.")]
        [SerializeField] private Vector2 lobScatterNearAndFar = Vector2.zero;

        [Tooltip("And the same for the second projectile.")]
        [SerializeField] private Vector2 secondLobScatterNearAndFar = Vector2.zero;

        [Tooltip("A lobbed shot hangs in the air longer the further it travels.")]
        [SerializeField] private bool lobHangsLongerWhenFurther;

        [Tooltip("And the same for the second projectile.")]
        [SerializeField] private bool secondLobHangsLongerWhenFurther;

        [Tooltip("The shortest share of the usual hang time a close lob keeps.")]
        [Range(0f, 1f)]
        [SerializeField] private float shortestLobHangShare;

        [Tooltip("Gap between where the second projectile's repeated hits land.")]
        [Min(0f)]
        [SerializeField] private float secondProjectileDistanceBetweenHits;

        [Header("Melee: the rest")]
        [Tooltip("Its swing breaks terrain across the whole arc, not just straight ahead.")]
        [SerializeField] private bool breaksTerrainAcrossTheWholeArc;

        [Tooltip("An animation name for the swing, instead of the usual one.")]
        [SerializeField] private string meleeOverrideAnimation = string.Empty;

        [Tooltip("The weapon is switched off — it carries its stats but will not swing.")]
        [SerializeField] private bool meleeDisabled;

        [Tooltip("An achievement unlocked by casting it. Blank for none.")]
        [SerializeField] private string castAchievementId = string.Empty;

        [Tooltip("An effect played when the cast finishes. Blank for none.")]
        [SerializeField] private string castCompleteEffectId = string.Empty;

        [Header("If it fires a beam")]
        [Tooltip("A held beam rather than a projectile — reach, growth, latching, mana cost.")]
        [SerializeField] private DimensionBeamWeaponTemplate beam = new DimensionBeamWeaponTemplate();

        [Header("Skill")]
        [Tooltip("How fast using it raises the matching skill. 1 is ordinary.")]
        [Min(0f)]
        [SerializeField] private float skillGainMultiplier = 1f;

        public DimensionWeaponKind Kind
        {
            get { return kind; }
        }

        public bool IsAWeapon
        {
            get { return kind != DimensionWeaponKind.NotAWeapon; }
        }

        public bool IsMelee
        {
            get { return kind == DimensionWeaponKind.Melee; }
        }

        public bool IsRanged
        {
            get { return kind == DimensionWeaponKind.Ranged; }
        }

        public bool IsCast
        {
            get { return kind == DimensionWeaponKind.Cast; }
        }

        public float Reach
        {
            get { return reach < 0f ? 0f : reach; }
        }

        public float ExtraReach
        {
            get { return extraReach < 0f ? 0f : extraReach; }
        }

        public DimensionSwingArc SwingArc
        {
            get { return swingArc; }
        }

        public DimensionAttackFlourish Flourish
        {
            get { return flourish; }
        }

        public float Lunge
        {
            get { return lunge < 0f ? 0f : lunge; }
        }

        public bool QuickHit
        {
            get { return quickHit; }
        }

        public bool ThrustsLikeASpear
        {
            get { return thrustsLikeASpear; }
        }

        public bool SwingsLikeATwoHander
        {
            get { return swingsLikeATwoHander; }
        }

        public bool SkipsTheWindUpAnimation
        {
            get { return skipsTheWindUpAnimation; }
        }

        /// <summary>What it fires, or empty when it is not a ranged weapon.</summary>
        public string FiresProjectileId
        {
            get { return IsRanged ? (firesProjectileId ?? string.Empty) : string.Empty; }
        }

        public int ExtraShots
        {
            get { return extraShots < 0 ? 0 : extraShots; }
        }

        public float SpreadAngle
        {
            get { return spreadAngle < 0f ? 0f : spreadAngle; }
        }

        public float MuzzleDistance
        {
            get { return muzzleDistance < 0f ? 0f : muzzleDistance; }
        }

        public bool AimsFreely
        {
            get { return aimsFreely; }
        }

        public float Recoil
        {
            get { return recoil < 0f ? 0f : recoil; }
        }

        public DimensionCastPurpose CastPurpose
        {
            get { return IsCast ? castPurpose : DimensionCastPurpose.Nothing; }
        }

        public float CastSeconds
        {
            get { return castSeconds < 0f ? 0f : castSeconds; }
        }

        public bool HoldingRepeatsIt
        {
            get { return holdingRepeatsIt; }
        }

        public bool FiresARandomProjectile { get { return firesARandomProjectile; } }

        public string[] RandomProjectileIds
        {
            get { return randomProjectileIds ?? new string[0]; }
        }

        public string SecondProjectileId { get { return secondProjectileId ?? string.Empty; } }

        public bool PiercesAtFullWindUp { get { return piercesAtFullWindUp; } }

        public bool BouncesAtFullWindUp { get { return bouncesAtFullWindUp; } }

        public string OverrideAnimation { get { return overrideAnimation ?? string.Empty; } }

        public int ExplosionSize { get { return explosionSize < 0 ? 0 : explosionSize; } }

        public bool ExplosionUsesWeaponDamage { get { return explosionUsesWeaponDamage; } }

        public bool LobsOntoTheAimPoint { get { return lobsOntoTheAimPoint; } }

        public float LobRange { get { return lobRange < 0f ? 0f : lobRange; } }

        public Vector2 LobScatterNearAndFar { get { return lobScatterNearAndFar; } }

        public Vector2 SecondLobScatterNearAndFar { get { return secondLobScatterNearAndFar; } }

        public bool LobHangsLongerWhenFurther { get { return lobHangsLongerWhenFurther; } }

        public bool SecondLobHangsLongerWhenFurther
        {
            get { return secondLobHangsLongerWhenFurther; }
        }

        public float ShortestLobHangShare
        {
            get
            {
                if (shortestLobHangShare < 0f)
                {
                    return 0f;
                }

                return shortestLobHangShare > 1f ? 1f : shortestLobHangShare;
            }
        }

        public float SecondProjectileDistanceBetweenHits
        {
            get
            {
                return secondProjectileDistanceBetweenHits < 0f
                    ? 0f
                    : secondProjectileDistanceBetweenHits;
            }
        }

        public bool BreaksTerrainAcrossTheWholeArc
        {
            get { return breaksTerrainAcrossTheWholeArc; }
        }

        public string MeleeOverrideAnimation
        {
            get { return meleeOverrideAnimation ?? string.Empty; }
        }

        public bool MeleeDisabled { get { return meleeDisabled; } }

        /// <summary>Whether a random-projectile list was given without switching it on.</summary>
        public bool RandomProjectilesWillBeIgnored
        {
            get { return !firesARandomProjectile && RandomProjectileIds.Length > 0; }
        }

        /// <summary>Whether it was told to fire randomly with nothing to pick from.</summary>
        public bool RandomProjectilesAreEmpty
        {
            get { return firesARandomProjectile && RandomProjectileIds.Length == 0; }
        }

        public DimensionBeamWeaponTemplate Beam
        {
            get { return beam ?? (beam = new DimensionBeamWeaponTemplate()); }
        }

        public string CastAchievementId { get { return castAchievementId ?? string.Empty; } }

        public string CastCompleteEffectId { get { return castCompleteEffectId ?? string.Empty; } }

        public float SkillGainMultiplier
        {
            get { return skillGainMultiplier < 0f ? 0f : skillGainMultiplier; }
        }

        /// <summary>Whether it is a ranged weapon with nothing to fire.</summary>
        /// <remarks>
        /// The failure that makes a bow useless while looking finished: it equips, it swings its
        /// animation, and nothing comes out.
        /// </remarks>
        public bool FiresNothing
        {
            get { return IsRanged && string.IsNullOrEmpty(FiresProjectileId); }
        }

        /// <summary>Whether it is a cast item that does nothing when cast.</summary>
        public bool CastsForNothing
        {
            get { return IsCast && castPurpose == DimensionCastPurpose.Nothing; }
        }

        /// <summary>Whether extra shots were asked for with no spread between them.</summary>
        /// <remarks>
        /// Legal, and occasionally wanted for a tight burst, but usually a surprise: every shot
        /// leaves on the same line and they read as one thicker projectile.
        /// </remarks>
        public bool FiresEveryShotDownTheSameLine
        {
            get { return IsRanged && ExtraShots > 0 && SpreadAngle <= 0f; }
        }

        /// <summary>Whether it swings with no reach at all.</summary>
        public bool SwingsAtNothing
        {
            get { return IsMelee && Reach <= 0f; }
        }
    }
}
