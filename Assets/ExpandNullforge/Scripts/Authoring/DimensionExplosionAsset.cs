using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The blast itself — what a bomb turns into when it goes off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This closes a loop the explosive work left open. <see cref="DimensionExplosiveTemplate"/>
    /// lets an author say what their bomb explodes into, and until now the only answers were
    /// vanilla's own explosions: a mod could make a bomb and could not make its blast.
    /// </para>
    /// <para>
    /// MOST BOMBS DO NOT NEED THIS ASSET. A bomb generates its own blast from the answers on the
    /// item, which is the one-asset-per-thing-you-make rule. This exists for the case where several
    /// bombs should share one blast, and for authoring a blast with its own look and sound.
    /// </para>
    /// <para>
    /// <c>ExplosionAuthoring</c> is three numbers — damage, terrain damage and radius
    /// (<c>ck-db\Pug.ECS.Authoring\ExplosionAuthoring.cs:9-15</c>) — and only ONE of them survives.
    /// Every path that spawns a blast writes its own damage and terrain damage over the prefab's:
    /// a bomb at <c>ExplosiveSystem.cs:175-176</c>, a chained charge through the same funnel, a
    /// creature's death rattle at <c>ExplodeStateSystem.cs:170-175</c>. So those two are not
    /// offered here — they live on the thing that sets the blast off, where they are read. Radius is
    /// used exactly as authored, and is offered.
    /// </para>
    /// <para>
    /// VERIFIED by census of all 30 vanilla prefabs carrying <c>ExplosionAuthoring</c>: radius runs
    /// from 1 (SmallPoisonExplosion) to 4.5 (DesertExplosiveWallExplosion), and 2 is what an
    /// ordinary bomb's blast uses. 28 of the 30 carry the same placeholder damage of 100 and a
    /// terrain damage of 0 — placeholders precisely because nothing reads them.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Explosion")]
    public sealed class DimensionExplosionAsset : ScriptableObject
    {
        /// <summary>The radius an ordinary vanilla blast uses.</summary>
        /// <remarks>VERIFIED: <c>ExplosionEntity.prefab</c> authors <c>radius: 2</c>.</remarks>
        public const float OrdinaryRadius = 2f;

        /// <summary>How long an explosion lives before removing itself, in seconds.</summary>
        /// <remarks>VERIFIED: <c>ExplosionEntity.prefab</c> authors <c>lifetime.pc: 1</c>.</remarks>
        public const float DefaultLifetimeSeconds = 1f;

        [Header("Identity")]
        [SerializeField] private string explosionId = "explosion";

        [Tooltip("What you call it in your own lists. A blast is never held or hovered, so no player ever reads this.")]
        [SerializeField] private string displayName = "Explosion";

        [Header("Look")]
        [SerializeField] private Sprite sprite;

        [Tooltip("How long it lasts before removing itself, in seconds.")]
        [Min(0f)]
        [SerializeField] private float lifetimeSeconds = DefaultLifetimeSeconds;

        [Header("What it does")]
        [Tooltip("How far it reaches, in tiles. Vanilla runs from 1 to 4.5, and an ordinary bomb's " +
            "blast uses 2. Terrain only breaks within 4 tiles no matter what this says. How much it " +
            "hurts and how much terrain it breaks are set on whatever sets this blast off.")]
        [Min(0f)]
        [SerializeField] private float radius = OrdinaryRadius;

        [Tooltip("What it leaves burning where it went off.")]
        [SerializeField] private DimensionBlastLeavesBehind leavesBehind = DimensionBlastLeavesBehind.Nothing;

        [Header("What it sounds and looks like")]
        [SerializeField] private DimensionImpactFeedbackTemplate feedback = new DimensionImpactFeedbackTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string ExplosionId
        {
            get { return explosionId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public Sprite Sprite
        {
            get { return sprite; }
        }

        public float LifetimeSeconds
        {
            get { return lifetimeSeconds < 0f ? 0f : lifetimeSeconds; }
        }

        public DimensionBlastLeavesBehind LeavesBehind
        {
            get { return leavesBehind; }
        }

        /// <summary>
        /// The napalm variation the game spawns for <see cref="LeavesBehind"/>, or -1 for none.
        /// </summary>
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

        public float Radius
        {
            get { return radius < 0f ? 0f : radius; }
        }

        public DimensionImpactFeedbackTemplate Feedback
        {
            get { return feedback ?? new DimensionImpactFeedbackTemplate(); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>Whether it reaches nothing at all.</summary>
        public bool ReachesNothing
        {
            get { return Radius <= 0f; }
        }

        /// <summary>Whether it never goes away.</summary>
        /// <remarks>
        /// An explosion with no lifetime stays where it was, doing its damage, for as long as the
        /// world is loaded. It is the same failure as a projectile with no timer, and worse,
        /// because this one has an area.
        /// </remarks>
        public bool NeverGoesAway
        {
            get { return LifetimeSeconds <= 0f; }
        }
    }
}
