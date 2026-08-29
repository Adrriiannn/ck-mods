using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// More ways a creature can fight: a sweeping ray, hurting on contact, a shield it holds up,
    /// and placing things down mid-fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SWEEPING RAY IS A WHOLE ATTACK TYPE, not a projectile. It grows out, turns, and shrinks
    /// back, and everything it passes over is hit — so a player dodges by reading its rotation
    /// rather than by stepping aside. Seventeen settings on it, none of them previously reachable.
    /// </para>
    /// <para>
    /// TOUCH DAMAGE IS SEPARATE FROM AN ATTACK. A creature that hurts you for standing next to it
    /// has no wind-up and no swing; it simply hurts. That is why it is its own thing rather than a
    /// melee attack with a zero wind-up.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMoreCombatTemplate
    {
        [Header("A sweeping ray")]
        [Tooltip("It fires a ray that grows out and sweeps around it.")]
        [SerializeField] private bool sweepsARay;

        [Tooltip("How long the ray reaches.")]
        [Min(0f)]
        [SerializeField] private float rayLength = 8f;

        [Tooltip("How thick the ray is.")]
        [Min(0f)]
        [SerializeField] private float rayThickness = 1f;

        [Tooltip("How fast it turns, in degrees per second. Negative turns the other way.")]
        [SerializeField] private float raySpinSpeed = 45f;

        [Tooltip("It starts pointing a random way rather than always the same way.")]
        [SerializeField] private bool rayStartsAtARandomAngle = true;

        [Tooltip("It does not turn at all — a fixed beam rather than a sweep.")]
        [SerializeField] private bool rayDoesNotTurn;

        [Tooltip("How far from its centre the ray begins.")]
        [Min(0f)]
        [SerializeField] private float rayStartsOutAt;

        [Tooltip("How long the ray takes to grow out.")]
        [Min(0f)]
        [SerializeField] private float rayGrowSeconds = 0.5f;

        [Tooltip("How long it takes to pull back in.")]
        [Min(0f)]
        [SerializeField] private float rayShrinkSeconds = 0.5f;

        [Tooltip("How long the wind-up before it appears.")]
        [Min(0f)]
        [SerializeField] private float rayWindUpSeconds = 1f;

        [Tooltip("How long it stays out and sweeping.")]
        [Min(0f)]
        [SerializeField] private float raySweepSeconds = 3f;

        [Tooltip("How long the recovery afterwards.")]
        [Min(0f)]
        [SerializeField] private float rayRecoverySeconds = 1f;

        [Tooltip("How long the whole attack takes.")]
        [Min(0f)]
        [SerializeField] private float rayTotalSeconds = 5f;

        [Tooltip("Flat ray damage.")]
        [Min(0)]
        [SerializeField] private int rayDamage;

        [Tooltip("How hard the ray hits for its tier.")]
        [Min(0f)]
        [SerializeField] private float rayMultiplier = 1f;

        [Tooltip("The ray counts as ranged damage.")]
        [SerializeField] private bool rayIsRanged = true;

        [Tooltip("The ray counts as magic damage.")]
        [SerializeField] private bool rayIsMagic;

        [Header("Hurting on contact")]
        [Tooltip("It hurts anything that touches it, with no attack at all.")]
        [SerializeField] private bool hurtsOnTouch;

        [Tooltip("How far that reaches.")]
        [Min(0f)]
        [SerializeField] private float touchRadius = 0.5f;

        [Tooltip("How hard it shoves what it touches.")]
        [Min(0f)]
        [SerializeField] private float touchShove;

        [Tooltip("How long before it can hurt the same thing again.")]
        [Min(0f)]
        [SerializeField] private float touchCooldown = 1f;

        [Tooltip("Touch damage goes through armour and resistances untouched.")]
        [SerializeField] private bool touchIgnoresArmour;

        [Tooltip("The animation it plays on touching something.")]
        [SerializeField] private string touchAnimation = "attack";

        [Header("A shield")]
        [Tooltip("It holds a shield that blocks from one direction.")]
        [SerializeField] private bool holdsAShield;

        [Tooltip("How wide the shield covers, in degrees. 90 is a quarter turn.")]
        [Range(0, 360)]
        [SerializeField] private int shieldWidthDegrees = 90;

        [Tooltip("The shield is already up when it appears.")]
        [SerializeField] private bool shieldStartsUp;

        [Header("Placing things down")]
        [Tooltip("It places objects mid-fight — mines, eggs, obstacles.")]
        [SerializeField] private bool placesObjects;

        [Tooltip("What it places.")]
        [SerializeField] private string placesObjectId = string.Empty;

        [Tooltip("How long placing one takes.")]
        [Min(0f)]
        [SerializeField] private float placeSeconds = 1f;

        [Tooltip("Shortest wait between placements.")]
        [Min(0f)]
        [SerializeField] private float minPlaceCooldown = 3f;

        [Tooltip("Longest wait between placements.")]
        [Min(0f)]
        [SerializeField] private float maxPlaceCooldown = 6f;

        [Tooltip("How many it will have out at once. 0 for no limit.")]
        [Min(0)]
        [SerializeField] private int atMostPlaced;

        [Tooltip("It only places while fighting a player.")]
        [SerializeField] private bool placesOnlyInCombat = true;

        [Tooltip("It will place on any ground rather than one particular kind.")]
        [SerializeField] private bool placesOnAnyGround = true;

        [Tooltip("Which ground it needs, when it is fussy.")]
        [SerializeField] private string placesOnTilesetId = string.Empty;

        [Tooltip("Which kind of tile it needs.")]
        [SerializeField] private PugTilemap.TileType placesOnTileKind = PugTilemap.TileType.ground;

        [Header("Orbiting its owner")]
        [Tooltip("It circles whoever owns it, the way an orbiting minion does.")]
        [SerializeField] private bool orbitsItsOwner;

        [Tooltip("How far out it circles.")]
        [Min(0f)]
        [SerializeField] private float orbitRadius = 2f;

        [Tooltip("How fast it circles.")]
        [Min(0f)]
        [SerializeField] private float orbitSpeed = 2f;

        public bool SweepsARay { get { return sweepsARay; } }

        public float RayLength { get { return Floor(rayLength); } }

        public float RayThickness { get { return Floor(rayThickness); } }

        public float RaySpinSpeed { get { return raySpinSpeed; } }

        public bool RayStartsAtARandomAngle { get { return rayStartsAtARandomAngle; } }

        public bool RayDoesNotTurn { get { return rayDoesNotTurn; } }

        public float RayStartsOutAt { get { return Floor(rayStartsOutAt); } }

        public float RayGrowSeconds { get { return Floor(rayGrowSeconds); } }

        public float RayShrinkSeconds { get { return Floor(rayShrinkSeconds); } }

        public float RayWindUpSeconds { get { return Floor(rayWindUpSeconds); } }

        public float RaySweepSeconds { get { return Floor(raySweepSeconds); } }

        public float RayRecoverySeconds { get { return Floor(rayRecoverySeconds); } }

        public float RayTotalSeconds { get { return Floor(rayTotalSeconds); } }

        public int RayDamage { get { return rayDamage < 0 ? 0 : rayDamage; } }

        public float RayMultiplier { get { return Floor(rayMultiplier); } }

        public bool RayIsRanged { get { return rayIsRanged; } }

        public bool RayIsMagic { get { return rayIsMagic; } }

        public bool HurtsOnTouch { get { return hurtsOnTouch; } }

        public float TouchRadius { get { return Floor(touchRadius); } }

        public float TouchShove { get { return Floor(touchShove); } }

        public float TouchCooldown { get { return Floor(touchCooldown); } }

        public bool TouchIgnoresArmour { get { return touchIgnoresArmour; } }

        public string TouchAnimation { get { return touchAnimation ?? string.Empty; } }

        public bool HoldsAShield { get { return holdsAShield; } }

        public int ShieldWidthDegrees
        {
            get
            {
                if (shieldWidthDegrees < 0)
                {
                    return 0;
                }

                return shieldWidthDegrees > 360 ? 360 : shieldWidthDegrees;
            }
        }

        public bool ShieldStartsUp { get { return shieldStartsUp; } }

        public bool PlacesObjects { get { return placesObjects; } }

        public string PlacesObjectId { get { return placesObjectId ?? string.Empty; } }

        public float PlaceSeconds { get { return Floor(placeSeconds); } }

        public float MinPlaceCooldown { get { return Floor(minPlaceCooldown); } }

        public float MaxPlaceCooldown
        {
            get
            {
                float longest = Floor(maxPlaceCooldown);
                return longest < MinPlaceCooldown ? MinPlaceCooldown : longest;
            }
        }

        public int AtMostPlaced { get { return atMostPlaced < 0 ? 0 : atMostPlaced; } }

        public bool PlacesOnlyInCombat { get { return placesOnlyInCombat; } }

        public bool PlacesOnAnyGround { get { return placesOnAnyGround; } }

        public string PlacesOnTilesetId { get { return placesOnTilesetId ?? string.Empty; } }

        public PugTilemap.TileType PlacesOnTileKind { get { return placesOnTileKind; } }

        public bool OrbitsItsOwner { get { return orbitsItsOwner; } }

        public float OrbitRadius { get { return Floor(orbitRadius); } }

        public float OrbitSpeed { get { return Floor(orbitSpeed); } }

        /// <summary>
        /// Whether the ray sweeps but never turns, which makes it a fixed beam.
        /// </summary>
        /// <remarks>
        /// Legal and occasionally wanted — a stationary hazard beam — but easy to reach by accident
        /// when someone leaves the spin speed at zero and expects a sweep.
        /// </remarks>
        public bool RayNeverSweeps
        {
            get { return sweepsARay && (rayDoesNotTurn || Mathf.Approximately(raySpinSpeed, 0f)); }
        }

        /// <summary>Whether it places something without saying what.</summary>
        public bool PlacesNothing
        {
            get { return placesObjects && string.IsNullOrEmpty(PlacesObjectId); }
        }

        /// <summary>Whether it is fussy about ground without naming any.</summary>
        public bool FussyAboutGroundWithoutNamingIt
        {
            get { return placesObjects && !placesOnAnyGround && string.IsNullOrEmpty(PlacesOnTilesetId); }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }
    }
}
