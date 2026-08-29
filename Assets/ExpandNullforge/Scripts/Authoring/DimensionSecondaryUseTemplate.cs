using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What right-clicking an item does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The raw component count is misleading and worth stating plainly: <c>SecondaryUseAuthoring</c>
    /// is on 509 vanilla prefabs, but <b>420 of those are just <c>Equip</c></b> — a marker saying
    /// "right-click puts this on", which is what nearly every piece of armour and clothing carries.
    /// A further 47 are <c>None</c>. The mechanism people actually mean by "secondary attack" is
    /// <see cref="ChargedAttack"/>, and that is on 35 prefabs; minions are on 7.
    /// </para>
    /// <para>
    /// So the easy path is a tick, and the interesting path is a small set of timing dials. Treating
    /// all 509 as one elaborate system would have produced a wall of fields that 420 items leave
    /// untouched.
    /// </para>
    /// </remarks>
    public enum DimensionSecondaryUseKind
    {
        /// <summary>Right-click does nothing special. (47 vanilla prefabs)</summary>
        Nothing = 0,

        /// <summary>Right-click puts it on. What armour and clothing do. (420)</summary>
        EquipIt = 1,

        /// <summary>Hold to charge, release for a stronger attack. (35)</summary>
        ChargedAttack = 2,

        /// <summary>Right-click summons a minion. (7)</summary>
        SummonsMinion = 3
    }

    /// <summary>
    /// The secondary use of an item — what the right mouse button is for.
    /// </summary>
    /// <remarks>
    /// Composable, like wiring and item effects, because a secondary use is something an item HAS
    /// rather than something it IS. A sword with a charged thrust is still a sword.
    /// </remarks>
    [Serializable]
    public sealed class DimensionSecondaryUseTemplate
    {
        [Tooltip("What right-clicking does.")]
        [SerializeField] private DimensionSecondaryUseKind kind = DimensionSecondaryUseKind.Nothing;

        [Header("Charged attack")]
        [Tooltip("How long a full charge takes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float chargeTime = 0.7f;

        [Tooltip("How many steps the charge passes through. More steps means more visible build-up.")]
        [Min(1)]
        [SerializeField] private int chargeSteps = 1;

        [Tooltip("Release below this step and the attack is cancelled. 0 lets any charge release.")]
        [Min(0)]
        [SerializeField] private int minimumStepToRelease;

        [Tooltip("Damage multiplier on the charged hit.")]
        [Min(0f)]
        [SerializeField] private float extraDamageMultiplier = 1f;

        [Tooltip("How much bigger the charged hit's area is.")]
        [Min(0f)]
        [SerializeField] private float areaSizeMultiplier = 1f;

        [Tooltip("How much faster a charged projectile flies.")]
        [Min(0f)]
        [SerializeField] private float projectileSpeedMultiplier = 1f;

        [Tooltip("Mana the charged use costs, relative to an ordinary one.")]
        [Min(0f)]
        [SerializeField] private float manaCostMultiplier = 2f;

        [Tooltip("The charged hit knocks things back.")]
        [SerializeField] private bool knockback;

        [Tooltip("Which named charged attack it is. A SecondaryUseTerm — ChargedShot, ArcSlash, PiercingShot, and so on.")]
        [SerializeField] private string chargedAttackName = string.Empty;

        [Tooltip("Its elemental look. A WeaponEffectType — Metal, Fire, Ancient, Octarine, Seaweed, Pandorium.")]
        [SerializeField] private string weaponEffect = string.Empty;

        [Header("Minion")]
        [Tooltip("What it summons. One of the game's creatures, or one of yours. Only used when it summons a minion.")]
        [SerializeField] private string minionObjectId = string.Empty;

        [Tooltip("How much longer or shorter its wind-up is than usual.")]
        [Min(0f)]
        [SerializeField] private float windUpTimeMultiplier = 1f;

        [Tooltip("Which explosion in the sequence a full wind-up starts at.")]
        [Min(0)]
        [SerializeField] private int windUpExplosionStartsAt;

        [Tooltip("How far along the sequence each further wind-up tier moves.")]
        [Min(0)]
        [SerializeField] private int windUpExplosionStepsBy;

        public float WindUpTimeMultiplier
        {
            get { return windUpTimeMultiplier < 0f ? 0f : windUpTimeMultiplier; }
        }

        public int WindUpExplosionStartsAt
        {
            get { return windUpExplosionStartsAt < 0 ? 0 : windUpExplosionStartsAt; }
        }

        public int WindUpExplosionStepsBy
        {
            get { return windUpExplosionStepsBy < 0 ? 0 : windUpExplosionStepsBy; }
        }

        public DimensionSecondaryUseKind Kind
        {
            get { return kind; }
        }

        /// <summary>Whether right-click does anything at all.</summary>
        public bool HasSecondaryUse
        {
            get { return kind != DimensionSecondaryUseKind.Nothing; }
        }

        public bool IsChargedAttack
        {
            get { return kind == DimensionSecondaryUseKind.ChargedAttack; }
        }

        public bool SummonsMinion
        {
            get { return kind == DimensionSecondaryUseKind.SummonsMinion; }
        }

        public float ChargeTime
        {
            get { return chargeTime < 0f ? 0f : chargeTime; }
        }

        public int ChargeSteps
        {
            get { return chargeSteps < 1 ? 1 : chargeSteps; }
        }

        /// <summary>
        /// The step a charge has to reach before releasing does anything.
        /// </summary>
        /// <remarks>
        /// Clamped to the number of steps that exist. Asking for step 5 of a 3-step charge would make
        /// the attack unreleasable — the player would hold the button and nothing would ever happen.
        /// </remarks>
        public int MinimumStepToRelease
        {
            get
            {
                if (minimumStepToRelease < 0)
                {
                    return 0;
                }

                return minimumStepToRelease > ChargeSteps ? ChargeSteps : minimumStepToRelease;
            }
        }

        public float ExtraDamageMultiplier
        {
            get { return extraDamageMultiplier < 0f ? 0f : extraDamageMultiplier; }
        }

        public float AreaSizeMultiplier
        {
            get { return areaSizeMultiplier < 0f ? 0f : areaSizeMultiplier; }
        }

        public float ProjectileSpeedMultiplier
        {
            get { return projectileSpeedMultiplier < 0f ? 0f : projectileSpeedMultiplier; }
        }

        public float ManaCostMultiplier
        {
            get { return manaCostMultiplier < 0f ? 0f : manaCostMultiplier; }
        }

        public bool Knockback
        {
            get { return knockback; }
        }

        public string ChargedAttackName
        {
            get { return chargedAttackName ?? string.Empty; }
        }

        public string WeaponEffect
        {
            get { return weaponEffect ?? string.Empty; }
        }

        public string MinionObjectId
        {
            get { return SummonsMinion ? (minionObjectId ?? string.Empty) : string.Empty; }
        }

        /// <summary>Whether it summons a minion but names none.</summary>
        public bool SummonsNothing
        {
            get { return SummonsMinion && string.IsNullOrEmpty(MinionObjectId); }
        }

        /// <summary>Whether a charged attack was set up to be no better than an ordinary one.</summary>
        /// <remarks>
        /// Charging costs the player time and mana. One that adds no damage, no reach and no knockback
        /// is strictly worse than not charging, which reads as the charge being broken.
        /// </remarks>
        public bool ChargingGainsNothing
        {
            get
            {
                return IsChargedAttack
                    && ExtraDamageMultiplier <= 1f
                    && AreaSizeMultiplier <= 1f
                    && ProjectileSpeedMultiplier <= 1f
                    && !Knockback;
            }
        }
    }
}
