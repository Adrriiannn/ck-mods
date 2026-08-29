using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>Which part of a dungeon a group of rooms fills.</summary>
    /// <remarks>
    /// Mirrors Core Keeper's own room roles rather than inventing categories, and mirrored here rather
    /// than shared with the runtime so the authoring asset does not depend on a runtime enum whose
    /// numbering could shift. A test checks the two agree.
    /// </remarks>
    public enum DimensionDungeonRoomRoleKind
    {
        Main = 0,
        Entrance = 1,
        End = 2,
        Connecting = 3
    }

    /// <summary>A set of interchangeable rooms for one part of a dungeon.</summary>
    [Serializable]
    public sealed class DimensionDungeonRoomGroupTemplate
    {
        [Tooltip("Which part of the dungeon these rooms fill.")]
        [SerializeField] private DimensionDungeonRoomRoleKind role = DimensionDungeonRoomRoleKind.Main;

        [Tooltip("How few of this kind of room the dungeon must have.")]
        [Min(0)]
        [SerializeField] private int minRooms = 1;

        [Tooltip("How many at most.")]
        [Min(1)]
        [SerializeField] private int maxRooms = 3;

        [Tooltip("The scenes that may fill this role. More than one keeps the dungeon from repeating.")]
        [SerializeField] private SceneTemplateAsset[] rooms = new SceneTemplateAsset[0];

        public DimensionDungeonRoomRoleKind Role
        {
            get { return role; }
        }

        public int MinRooms
        {
            get { return minRooms < 0 ? 0 : minRooms; }
        }

        public int MaxRooms
        {
            get { return maxRooms < MinRooms ? MinRooms : maxRooms; }
        }

        public SceneTemplateAsset[] Rooms
        {
            get { return rooms ?? new SceneTemplateAsset[0]; }
        }
    }

    /// <summary>
    /// A dungeon the world grows inside one of this dimension's biomes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dungeon is an arrangement, not a new kind of content: its rooms are ordinary scenes, already
    /// authored elsewhere in this dimension. What this asset adds is how many of each kind there are,
    /// how big the whole thing is, and where it may appear — everything after that is Core Keeper's
    /// own generator choosing a spot, laying out rooms, carving paths and filling the gaps.
    /// </para>
    /// <para>
    /// The rooms are referenced as scene assets rather than by name so that renaming a scene cannot
    /// silently empty a dungeon.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Dungeon")]
    public sealed class DimensionDungeonAsset : ScriptableObject
    {
        [SerializeField] private string dungeonId = "dungeon";
        [SerializeField] private string displayName = "Dungeon";

        [Tooltip("The biome it grows in: one of your own biomes by id, or a vanilla biome by " +
                 "its game name (Slime, Larva, Stone, Nature, Sea, Desert, Crystal, Passage, " +
                 "Excavation) for Overworld spawning. Empty means anywhere.")]
        [SerializeField] private string biomeId = string.Empty;

        [Tooltip("How far across the dungeon reaches, in tiles.")]
        [Min(8)]
        [SerializeField] private int radius = 48;

        [Tooltip("How large its rooms are relative to the dungeon.")]
        [Min(0.01f)]
        [SerializeField] private float roomSize = 1f;

        [Tooltip("How wide the corridors between rooms are.")]
        [Min(0.01f)]
        [SerializeField] private float pathSize = 0.5f;

        [Tooltip("How likely it is to be chosen when the world places a dungeon here.")]
        [Range(0f, 1f)]
        [SerializeField] private float spawnChance = 0.5f;

        [Tooltip("How close to the CENTRE it may come, in tiles. In your dimension that centre " +
                 "is the local 0, 0 — the spot players arrive at — never the far-away world " +
                 "Core, so 300 here means 300 tiles from your own centre. (The Overworld's " +
                 "classic-world table reads the same number as world-core distance.)")]
        [Min(0)]
        [SerializeField] private int minDistanceFromCentre;

        [Tooltip("Keep wandering creatures out of it, the way vanilla dungeons do. This is the " +
                 "only control over that, and it applies whether the dungeon is a generated " +
                 "layout or one handmade room.")]
        [SerializeField] private bool blockOtherSpawns = true;

        [Tooltip("It grows inside this dimension. Off keeps it out of your dimension — it can " +
                 "then only appear in the Overworld through its biome binding.")]
        [SerializeField] private bool growsInThisDimension = true;

        [Tooltip("Where in the dimension it grows: anywhere it fits, at one exact spot, or on " +
                 "a ring at a distance from the centre. Every coordinate here is the " +
                 "dimension's OWN — local 0, 0 is its centre, exactly the numbers the " +
                 "coordinate readout shows in play.")]
        [SerializeField] private DimensionScenePlacementMode dimensionPlacement =
            DimensionScenePlacementMode.Automatic;

        [Tooltip("The exact spot, in the dimension's own coordinates — 0, 0 is its centre, " +
                 "where players arrive.")]
        [SerializeField] private Vector2Int dimensionExactPosition;

        [Tooltip("The ring's inner edge: nearest to the dimension's centre (local 0, 0) it " +
                 "may grow.")]
        [Min(0)]
        [SerializeField] private int dimensionMinRadius;

        [Tooltip("The ring's outer edge, from the same local centre. 0 means no outer limit.")]
        [Min(0)]
        [SerializeField] private int dimensionMaxRadius;

        [Tooltip("How many of it one generated area may grow.")]
        [Min(1)]
        [SerializeField] private int dimensionCount = 1;

        [Tooltip("Pin one guaranteed copy into the vanilla Overworld, the way the game pins " +
                 "its own bosses and temples. Rides the game's own placement: scored spot, " +
                 "saved position, old saves included.")]
        [SerializeField] private bool pinnedInOverworld;

        [Tooltip("Pinned at an exact world position rather than a scored distance.")]
        [SerializeField] private bool pinnedAtExactSpot;

        [Tooltip("The exact world position, when pinned at a spot.")]
        [SerializeField] private Vector2Int pinnedPosition;

        [Tooltip("How far from the Core it aims for, when scored. The game's own bosses run " +
                 "65 to 750.")]
        [Min(0)]
        [SerializeField] private int pinnedDistanceFromCore = 400;

        [Tooltip("A vanilla biome name (Slime, Larva, Stone, Nature, Sea, Desert, Crystal, " +
                 "Passage, Excavation) to keep it inside. Empty means anywhere.")]
        [SerializeField] private string pinnedBiomeName = string.Empty;

        [Tooltip("It spawns the moment the world loads rather than when a player nears.")]
        [SerializeField] private bool pinnedSpawnsImmediately;

        [SerializeField] private DimensionDungeonRoomGroupTemplate[] roomGroups =
            new DimensionDungeonRoomGroupTemplate[0];

        [Tooltip("The game's own dungeon generator: a shaped outline grown from room and path rules.")]
        [SerializeField] private DimensionDungeonShapeTemplate generatedShape = new DimensionDungeonShapeTemplate();

        [Tooltip("What fills the generated rooms: floors, chests, veins, decoration — placed " +
                 "procedurally, the way the game's own dungeons get their character.")]
        [SerializeField] private DimensionRoomFillingAsset[] roomFillings =
            new DimensionRoomFillingAsset[0];

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string DungeonId
        {
            get { return dungeonId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string BiomeId
        {
            get { return biomeId ?? string.Empty; }
        }

        public int Radius
        {
            get { return radius < 8 ? 8 : radius; }
        }

        public float RoomSize
        {
            get { return roomSize < 0.01f ? 0.01f : roomSize; }
        }

        public float PathSize
        {
            get { return pathSize < 0.01f ? 0.01f : pathSize; }
        }

        public float SpawnChance
        {
            get { return Mathf.Clamp01(spawnChance); }
        }

        public int MinDistanceFromCentre
        {
            get { return minDistanceFromCentre < 0 ? 0 : minDistanceFromCentre; }
        }

        public bool BlockOtherSpawns
        {
            get { return blockOtherSpawns; }
        }

        public DimensionDungeonRoomGroupTemplate[] RoomGroups
        {
            get { return roomGroups ?? new DimensionDungeonRoomGroupTemplate[0]; }
        }

        public DimensionDungeonShapeTemplate GeneratedShape
        {
            get { return generatedShape ?? (generatedShape = new DimensionDungeonShapeTemplate()); }
        }

        public DimensionRoomFillingAsset[] RoomFillings
        {
            get { return roomFillings ?? new DimensionRoomFillingAsset[0]; }
        }

        public bool GrowsInThisDimension { get { return growsInThisDimension; } }

        public DimensionScenePlacementMode DimensionPlacement { get { return dimensionPlacement; } }

        public Vector2Int DimensionExactPosition { get { return dimensionExactPosition; } }

        public int DimensionMinRadius { get { return dimensionMinRadius < 0 ? 0 : dimensionMinRadius; } }

        public int DimensionMaxRadius
        {
            get
            {
                return dimensionMaxRadius > 0 && dimensionMaxRadius < DimensionMinRadius
                    ? DimensionMinRadius
                    : (dimensionMaxRadius < 0 ? 0 : dimensionMaxRadius);
            }
        }

        public int DimensionCount { get { return dimensionCount < 1 ? 1 : dimensionCount; } }

        public bool PinnedInOverworld { get { return pinnedInOverworld; } }

        public bool PinnedAtExactSpot { get { return pinnedAtExactSpot; } }

        public Vector2Int PinnedPosition { get { return pinnedPosition; } }

        public int PinnedDistanceFromCore
        {
            get { return pinnedDistanceFromCore < 0 ? 0 : pinnedDistanceFromCore; }
        }

        public string PinnedBiomeName { get { return pinnedBiomeName ?? string.Empty; } }

        public bool PinnedSpawnsImmediately { get { return pinnedSpawnsImmediately; } }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>
        /// Whether this dungeon has an entrance group.
        /// </summary>
        /// <remarks>
        /// Worth surfacing before a build: a dungeon with no entrance still generates, and the player
        /// simply finds a sealed pocket of rooms with no way in.
        /// </remarks>
        public bool HasEntrance
        {
            get
            {
                DimensionDungeonRoomGroupTemplate[] groups = RoomGroups;
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i] != null &&
                        groups[i].Role == DimensionDungeonRoomRoleKind.Entrance &&
                        groups[i].Rooms.Length > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
