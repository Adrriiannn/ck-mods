using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>How one patch of placed things is shaped.</summary>
    /// <remarks>The order mirrors Core Keeper's own <c>SpawnAlgorithm</c> and must stay aligned.</remarks>
    public enum DimensionFillingPatchShape
    {
        /// <summary>One thing at one spot. <c>Spot</c>.</summary>
        OneSpot = 0,

        /// <summary>A round patch. <c>Circle</c>.</summary>
        RoundPatch = 1,

        /// <summary>A square patch. <c>Rect</c>.</summary>
        SquarePatch = 2,

        /// <summary>A blob that flows to fill the space. <c>Cluster</c>.</summary>
        FlowingBlob = 3
    }

    /// <summary>
    /// One kind of thing a room filling places: what, how many patches, and where it may sit.
    /// </summary>
    [Serializable]
    public sealed class DimensionRoomFillingEntry
    {
        [Tooltip("What to place: one of your objects by id, or a vanilla object by its game name.")]
        [SerializeField] private string objectId = string.Empty;

        [Tooltip("Which look of it, when the object has several.")]
        [Min(0)]
        [SerializeField] private int look;

        [Tooltip("Fewest and most patches of this per room.")]
        [SerializeField] private Vector2Int howManyPatches = new Vector2Int(1, 1);

        [Tooltip("The shape each patch takes.")]
        [SerializeField] private DimensionFillingPatchShape patchShape = DimensionFillingPatchShape.RoundPatch;

        [Tooltip("How much of the room a patch covers, from a speck to the whole floor.")]
        [Range(0f, 1f)]
        [SerializeField] private float patchSize = 0.3f;

        [Tooltip("Chance this entry appears in a room at all.")]
        [Range(0f, 1f)]
        [SerializeField] private float chanceToAppear = 1f;

        [Tooltip("Within a patch, the chance each cell actually gets one. 1 is a solid patch; " +
                 "lower scatters it.")]
        [Range(0f, 1f)]
        [SerializeField] private float density = 1f;

        [Tooltip("What it may sit on: earlier entries' objects by id. Empty means bare ground — " +
                 "this is how layers chain: floor first, then things that sit on the floor.")]
        [SerializeField] private string[] mayLandOn = new string[0];

        [Tooltip("What it must never sit on.")]
        [SerializeField] private string[] neverOn = new string[0];

        [Tooltip("How many the placed thing holds — ore in a boulder, items in a stack.")]
        [Min(0)]
        [SerializeField] private int stackCount = 1;

        [Tooltip("For a chest: the loot table it opens with. A vanilla table by its game name, " +
                 "or one of your own loot tables by its id.")]
        [SerializeField] private string chestLoot = string.Empty;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Look { get { return look < 0 ? 0 : look; } }

        public Vector2Int HowManyPatches
        {
            get
            {
                int min = Mathf.Max(1, howManyPatches.x);
                return new Vector2Int(min, Mathf.Max(min, howManyPatches.y));
            }
        }

        public DimensionFillingPatchShape PatchShape { get { return patchShape; } }

        public float PatchSize { get { return Mathf.Clamp01(patchSize); } }

        public float ChanceToAppear { get { return Mathf.Clamp01(chanceToAppear); } }

        public float Density { get { return Mathf.Clamp01(density); } }

        public string[] MayLandOn { get { return mayLandOn ?? new string[0]; } }

        public string[] NeverOn { get { return neverOn ?? new string[0]; } }

        public int StackCount { get { return stackCount < 0 ? 0 : stackCount; } }

        public string ChestLoot { get { return chestLoot ?? string.Empty; } }
    }

    /// <summary>
    /// What fills a kind of dungeon room — the procedural half of a dungeon's character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the layer where vanilla's dungeons actually live: their 145 spawn templates
    /// paint floors, guaranteed chests, vein carpets, destructibles and decoration in entry
    /// order, each entry naming what it may sit on. A dungeon of handmade rooms alone covers a
    /// fraction of what the game ships; this asset covers the rest.
    /// </para>
    /// <para>
    /// ENTRY ORDER IS PAINT ORDER. The first entry is the bottom layer; later entries name
    /// earlier objects in "may land on" to stack — the exact chaining the game's own
    /// templates use.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Room Filling")]
    public sealed class DimensionRoomFillingAsset : ScriptableObject
    {
        [SerializeField] private string fillingId = "filling";
        [SerializeField] private string displayName = "Room Filling";

        [Tooltip("Which kinds of room this fills. Kinds are flags; a room matching ANY of them uses it.")]
        [SerializeField] private DimensionRoomKind fillsRooms = DimensionRoomKind.Main;

        [Tooltip("It fills corridors instead of rooms.")]
        [SerializeField] private bool fillsCorridors;

        [Tooltip("Only rooms at least this big, in tiles across. 0 fills any.")]
        [Min(0)]
        [SerializeField] private int onlyIfAtLeastThisBig;

        [Tooltip("What it places, in paint order: the first entry is the bottom layer.")]
        [SerializeField] private DimensionRoomFillingEntry[] entries = new DimensionRoomFillingEntry[0];

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string FillingId { get { return fillingId ?? string.Empty; } }

        public string DisplayName { get { return displayName ?? string.Empty; } }

        public DimensionRoomKind FillsRooms { get { return fillsRooms; } }

        public bool FillsCorridors { get { return fillsCorridors; } }

        public int OnlyIfAtLeastThisBig
        {
            get { return onlyIfAtLeastThisBig < 0 ? 0 : onlyIfAtLeastThisBig; }
        }

        public DimensionRoomFillingEntry[] Entries
        {
            get { return entries ?? new DimensionRoomFillingEntry[0]; }
        }

        public bool Enabled { get { return enabled; } }

        public string Notes { get { return notes ?? string.Empty; } }
    }
}
