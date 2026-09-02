using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>Which of Core Keeper's ways of moving a vehicle uses.</summary>
    /// <remarks>
    /// <para>
    /// Not a style choice — each kind is a different set of rules the game already implements. A boat
    /// floats and needs water; a minecart follows rails and cannot leave them; a ground vehicle drives
    /// and drifts. Picking one is picking which of those the vehicle obeys.
    /// </para>
    /// <para>
    /// THERE IS NO FOURTH KIND AND THERE CANNOT BE ONE. Core Keeper's player has exactly three riding
    /// states (<c>BoatRiding</c>, <c>MinecartRiding</c>, <c>VehicleRiding</c>) and the dispatch between
    /// them is a compiled chain in <c>UpdatePlayerStateSystem</c>. Inventing a fourth would need code
    /// that does not exist in the running game, so this list is closed on purpose.
    /// </para>
    /// </remarks>
    public enum DimensionVehicleKind
    {
        /// <summary>Drives on land, with drift and acceleration. The game's go-karts.</summary>
        Ground = 0,

        /// <summary>Floats, and needs water under it.</summary>
        Boat = 1,

        /// <summary>Follows rails.</summary>
        Minecart = 2
    }

    /// <summary>
    /// Something a player rides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A vehicle here is a whole thing: an item a player can be given or craft, an object they put
    /// down, a seat they get on by walking up and using it, and something they can break to get back.
    /// The generator writes all four from the answers below.
    /// </para>
    /// <para>
    /// EVERY RULE ABOUT RIDING IS CORE KEEPER'S AND STAYS CORE KEEPER'S — mounting, dismounting,
    /// collision, what happens when it hits a wall, how the camera behaves. What an author owns here
    /// is the same thing they own for a creature: the art, the name, and the numbers. The multipliers
    /// are relative to the game's own vehicles, so 1 is "handles like a vanilla one". That is a more
    /// useful anchor than an absolute speed nobody can picture.
    /// </para>
    /// <para>
    /// WHAT A GENERATED VEHICLE DOES NOT HAVE, so nobody is surprised by it: dust, smoke, tyre tracks
    /// and the engine loop. Those live on the game's own <c>Boat</c> and <c>GoKart</c> components,
    /// which read a hand-built hierarchy of particle systems and outline controllers with no null
    /// check at all — putting one of them on a generated prefab is an error every frame rather than a
    /// nicer kart. A generated vehicle is a picture that moves and carries a player.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Vehicle")]
    public sealed class DimensionVehicleAsset : ScriptableObject
    {
        /// <summary>
        /// How many hits a vehicle takes to break in the game's own.
        /// </summary>
        /// <remarks>
        /// Measured, not chosen: all seven vanilla vehicles — both boats, both minecarts and all three
        /// go-karts — author <c>maxHealth: 2</c> with damage capped at one point per hit, which is
        /// where "two hits with anything" comes from.
        /// </remarks>
        public const int VanillaVehicleHitsToBreak = 2;

        [Tooltip("The name this vehicle is known by inside your project. Other things point at it with this.")]
        [SerializeField] private string vehicleId = "vehicle";

        [Tooltip("What a player sees on the vehicle and on the item that puts it down.")]
        [SerializeField] private string displayName = "Vehicle";

        [Tooltip("The line under the name in the tooltip.")]
        [SerializeField] private string description = string.Empty;

        [Tooltip("Which of the game's ways of moving this uses. There are only three, and the list cannot be added to.")]
        [SerializeField] private DimensionVehicleKind kind = DimensionVehicleKind.Ground;

        [Tooltip("The picture drawn where the vehicle stands. Falls back to the icon when empty.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("The picture in an inventory slot. Falls back to the world picture when empty.")]
        [SerializeField] private Sprite icon;

        [Tooltip("How rare it reads as in a tooltip. Leave blank for common.")]
        [SerializeField] private string rarityId = string.Empty;

        [Tooltip("How fast, relative to the game's own vehicles. 1 handles like a vanilla one.")]
        [Min(0.05f)]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("How much it slides through turns. Higher is looser. Go-karts only.")]
        [Min(0f)]
        [SerializeField] private float driftingMultiplier = 1f;

        [Tooltip("How quickly it gets up to speed. Go-karts only.")]
        [Min(0.05f)]
        [SerializeField] private float accelerationMultiplier = 1f;

        [Tooltip("The sound its horn makes, by its name. Blank keeps the game's own. Go-karts only.")]
        [DimensionSoundName]
        [SerializeField] private string honkSound = string.Empty;

        [Tooltip("Top speed for a minecart, which does not use multipliers. The game's own carts use 800 and 500.")]
        [Min(0f)]
        [SerializeField] private float minecartMaxSpeed = 800f;

        [Tooltip("How close a player has to be standing to get on.")]
        [Min(0.1f)]
        [SerializeField] private float howCloseToGetOn = 1.5f;

        [Tooltip("A player can get on from any side, rather than only the side they are facing.")]
        [SerializeField] private bool getOnFromAnySide = true;

        [Tooltip("How many tiles it takes up where it stands.")]
        [SerializeField] private Vector2Int tileSize = Vector2Int.one;

        [Tooltip("It turns to face whichever way the player was looking when they put it down.")]
        [SerializeField] private bool turnsToFacePlacement = true;

        [Tooltip("As well as the ground its kind needs, it may also be put down on these things, by name. The game's objects, or your own.")]
        [SerializeField] private string[] alsoGoesOnItemIds = new string[0];

        [Tooltip("Nothing can break it. A player who puts one down can never pick it up again.")]
        [SerializeField] private bool indestructible;

        [Tooltip("How many hits it takes to break. The game's own vehicles all take two.")]
        [Min(1)]
        [SerializeField] private int hitsToBreak = VanillaVehicleHitsToBreak;

        [Tooltip("How much mining power a tool needs before it damages this at all. 0 means anything works.")]
        [Min(0)]
        [SerializeField] private int requiredMiningDamage;

        [Tooltip("Breaking it gives the vehicle back as an item.")]
        [SerializeField] private bool dropsItselfWhenBroken = true;

        [SerializeField] private DimensionObjectBasicsTemplate basics = new DimensionObjectBasicsTemplate();

        [SerializeField] private DimensionPlacementRulesTemplate placementRules = new DimensionPlacementRulesTemplate();

        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits = new DimensionSimpleTraitsTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string VehicleId
        {
            get { return vehicleId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public DimensionVehicleKind Kind
        {
            get { return kind; }
        }

        /// <summary>The picture drawn in the world, falling back to the inventory icon.</summary>
        public Sprite Sprite
        {
            get { return sprite != null ? sprite : icon; }
        }

        /// <summary>The picture in a slot, falling back to the world picture.</summary>
        public Sprite Icon
        {
            get { return icon != null ? icon : sprite; }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        public float SpeedMultiplier
        {
            get { return speedMultiplier < 0.05f ? 0.05f : speedMultiplier; }
        }

        public float DriftingMultiplier
        {
            get { return driftingMultiplier < 0f ? 0f : driftingMultiplier; }
        }

        public string HonkSoundName { get { return honkSound ?? string.Empty; } }

        public int HonkSound { get { return DimensionSoundNames.Hash(HonkSoundName); } }

        public float AccelerationMultiplier
        {
            get { return accelerationMultiplier < 0.05f ? 0.05f : accelerationMultiplier; }
        }

        public float MinecartMaxSpeed
        {
            get { return minecartMaxSpeed < 0f ? 0f : minecartMaxSpeed; }
        }

        public float HowCloseToGetOn
        {
            get { return howCloseToGetOn < 0.1f ? 0.1f : howCloseToGetOn; }
        }

        public bool GetOnFromAnySide { get { return getOnFromAnySide; } }

        /// <summary>How many tiles it takes up, never smaller than one in either direction.</summary>
        /// <remarks>
        /// A zero here would give the game a placement footprint of no tiles, which reads as "goes
        /// anywhere and overlaps everything" rather than as the mistake it is.
        /// </remarks>
        public Vector2Int TileSize
        {
            get
            {
                return new Vector2Int(
                    tileSize.x < 1 ? 1 : tileSize.x,
                    tileSize.y < 1 ? 1 : tileSize.y);
            }
        }

        public bool TurnsToFacePlacement { get { return turnsToFacePlacement; } }

        public string[] AlsoGoesOnItemIds
        {
            get { return alsoGoesOnItemIds ?? new string[0]; }
        }

        public bool Indestructible { get { return indestructible; } }

        public int HitsToBreak { get { return hitsToBreak < 1 ? 1 : hitsToBreak; } }

        public int RequiredMiningDamage { get { return requiredMiningDamage < 0 ? 0 : requiredMiningDamage; } }

        public bool DropsItselfWhenBroken { get { return dropsItselfWhenBroken; } }

        public DimensionObjectBasicsTemplate Basics
        {
            get { return basics ?? (basics = new DimensionObjectBasicsTemplate()); }
        }

        public DimensionPlacementRulesTemplate PlacementRules
        {
            get { return placementRules ?? (placementRules = new DimensionPlacementRulesTemplate()); }
        }

        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>
        /// Whether an author has set numbers that this kind of vehicle never reads.
        /// </summary>
        /// <remarks>
        /// Drift and acceleration are go-kart fields — <c>BoatCD</c> carries a speed multiplier and
        /// nothing else, and <c>MinecartCD</c> carries a top speed and nothing else. Answering them on
        /// a boat is not an error, but it is a number the author will wait for and never see, so the
        /// generate says so.
        /// </remarks>
        public bool HasNumbersItsKindIgnores
        {
            get
            {
                if (kind == DimensionVehicleKind.Ground)
                {
                    return false;
                }

                bool touchedKartOnlyNumbers =
                    !Mathf.Approximately(DriftingMultiplier, 1f) ||
                    !Mathf.Approximately(AccelerationMultiplier, 1f) ||
                    !string.IsNullOrEmpty(HonkSoundName);

                if (kind == DimensionVehicleKind.Boat)
                {
                    return touchedKartOnlyNumbers;
                }

                // A minecart reads its top speed and nothing else, so even the speed multiplier is
                // one of the numbers it ignores.
                return touchedKartOnlyNumbers || !Mathf.Approximately(SpeedMultiplier, 1f);
            }
        }
    }
}
