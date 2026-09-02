using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The shape a generated dungeon takes, and the pieces the game's own dungeon generator uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework already builds dungeons its own way — by feeding the game's scene table
    /// directly. This is the OTHER road: Core Keeper's own dungeon generator, which grows a dungeon
    /// from rules rather than from handmade rooms. Rooms of a kind, paths between them, a wobble on
    /// the outline, and a fill pattern for the space between.
    /// </para>
    /// <para>
    /// SHAPE AMPLITUDE AND FREQUENCY ARE THE OUTLINE'S WOBBLE. Frequency is how often the edge turns
    /// and amplitude is how far — together they are the difference between a dungeon that reads as
    /// carved and one that reads as a circle somebody drew.
    /// </para>
    /// <para>
    /// A ROOM KIND IS A FLAG, NOT A NAME. Rooms carry flags, and paths say which flagged rooms they
    /// join. That is how an entrance connects to a treasure room without either knowing about the
    /// other — and why the same dungeon rules produce a different layout every seed.
    /// </para>
    /// <para>
    /// WHAT IS DELIBERATELY NOT HERE. Which places fill which rooms lives on the dungeon itself
    /// (its room groups), and what gets scattered inside a room lives in its room fillings —
    /// both of which reach the game. This template once carried duplicates of both, plus a
    /// starting-contents list, all built out of Unity scene files and vanilla spawn-template
    /// assets; none of those can be looked up at runtime, because a dungeon room is found by the
    /// NAME this dimension registered it under and the fill blobs are built from authored values.
    /// They were removed rather than left to look configurable.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionDungeonShapeTemplate
    {
        [Header("The dungeon itself")]
        [Tooltip("It generates a dungeon from rules rather than from handmade rooms.")]
        [SerializeField] private bool generatesADungeon;

        [Tooltip("The seed it grows from. 0 lets the world decide.")]
        [Min(0)]
        [SerializeField] private int seed;

        [Tooltip("How far out it reaches, in tiles.")]
        [Min(0)]
        [SerializeField] private int radius = 50;

        [Header("Its outline")]
        [Tooltip("It has a shaped outline rather than a plain one.")]
        [SerializeField] private bool hasAShapedOutline;

        [Tooltip("How far the edge wobbles.")]
        [Min(0f)]
        [SerializeField] private float outlineWobble = 2f;

        [Tooltip("How often the edge turns. Higher is busier.")]
        [Min(0f)]
        [SerializeField] private float outlineBusyness = 1f;

        [Tooltip("The outline follows its rooms rather than being drawn around them.")]
        [SerializeField] private bool outlineFollowsTheRooms;

        [Tooltip("It is rectangular rather than round.")]
        [SerializeField] private bool isRectangular;

        [Tooltip("How much of a room the fill pattern covers.")]
        [Min(0f)]
        [SerializeField] private float roomFillSize;

        [Tooltip("How much of a path it covers.")]
        [Min(0f)]
        [SerializeField] private float pathFillSize;

        [Header("One handmade room instead")]
        [Tooltip("It is one handmade scene rather than a generated layout.")]
        [SerializeField] private bool isASingleHandmadeRoom;

        [Tooltip("Which of its own places it is, by id. Leave empty to use the first place " +
                 "named in its room groups.")]
        [SerializeField] private string singleRoomSceneId = string.Empty;

        [Tooltip("How much space around it is kept clear while other dungeons are placed.")]
        [Min(0)]
        [SerializeField] private int keepsClearRadius = 10;

        [Header("Its rooms")]
        [Tooltip("The kinds of room it grows. Each entry is a rule, not one room.")]
        [SerializeField] private DimensionDungeonRoom[] rooms = new DimensionDungeonRoom[0];

        [Tooltip("How its rooms are joined up.")]
        [SerializeField] private DimensionDungeonPath[] paths = new DimensionDungeonPath[0];

        [Header("What fills it")]
        [Tooltip("Which blocks its outline is drawn from.")]
        [SerializeField] private string[] outlineBlockIds = new string[0];

        [Header("Swapping things out")]
        [Tooltip("Objects swapped for others as the dungeon is built — how a theme is applied.")]
        [SerializeField] private DimensionDungeonSwap[] swaps = new DimensionDungeonSwap[0];

        public DimensionDungeonRoom[] Rooms { get { return rooms ?? new DimensionDungeonRoom[0]; } }

        public DimensionDungeonPath[] Paths { get { return paths ?? new DimensionDungeonPath[0]; } }

        public string[] OutlineBlockIds { get { return outlineBlockIds ?? new string[0]; } }

        /// <summary>
        /// Which of the dungeon's own places it is, when it is one handmade room. Empty means
        /// the first place its room groups name.
        /// </summary>
        /// <remarks>
        /// An id rather than a scene file, because a dungeon room is a place this dimension
        /// registered under a name — that name is what the game's dungeon generator looks up,
        /// and a Unity scene file has no name in that table at all.
        /// </remarks>
        public string SingleRoomSceneId
        {
            get { return singleRoomSceneId ?? string.Empty; }
        }

        public DimensionDungeonSwap[] Swaps { get { return swaps ?? new DimensionDungeonSwap[0]; } }

        public bool GeneratesADungeon { get { return generatesADungeon; } }

        public int Seed { get { return seed < 0 ? 0 : seed; } }

        public int Radius { get { return radius < 0 ? 0 : radius; } }

        public bool HasAShapedOutline { get { return hasAShapedOutline; } }

        public float OutlineWobble { get { return Floor(outlineWobble); } }

        public float OutlineBusyness { get { return Floor(outlineBusyness); } }

        public bool OutlineFollowsTheRooms { get { return outlineFollowsTheRooms; } }

        public bool IsRectangular { get { return isRectangular; } }

        public float RoomFillSize { get { return Floor(roomFillSize); } }

        public float PathFillSize { get { return Floor(pathFillSize); } }

        public bool IsASingleHandmadeRoom { get { return isASingleHandmadeRoom; } }

        public int KeepsClearRadius { get { return keepsClearRadius < 0 ? 0 : keepsClearRadius; } }

        /// <summary>
        /// Whether it is both a generated dungeon and a single handmade room.
        /// </summary>
        /// <remarks>
        /// The two are alternatives, not layers. Asking for both leaves the game deciding which
        /// wins, which is exactly the kind of thing that works in testing and not in a real world.
        /// </remarks>
        public bool IsBothGeneratedAndHandmade
        {
            get { return generatesADungeon && isASingleHandmadeRoom; }
        }

        /// <summary>Whether the outline is shaped with no wobble to shape it.</summary>
        public bool OutlineIsFlat
        {
            get { return hasAShapedOutline && outlineWobble <= 0f; }
        }

        /// <summary>Whether it generates a dungeon with no room to grow into.</summary>
        public bool HasNoRoomToGrow
        {
            get { return generatesADungeon && radius <= 0; }
        }

        private static float Floor(float value)
        {
            return value < 0f ? 0f : value;
        }
    }

    /// <summary>One rule about a kind of room a dungeon grows.</summary>
    /// <remarks>
    /// A rule, not a room: it says how many of this kind, how big, how far apart, and where they are
    /// allowed to sit. The generator then grows that many.
    /// </remarks>
    [Serializable]
    public struct DimensionDungeonRoom
    {
        [Tooltip("Where rooms of this kind are put.")]
        [SerializeField] private DimensionRoomPlacement placement;

        [Tooltip("What kind of room this is. Paths join rooms by their kind.")]
        [SerializeField] private DimensionRoomKind kind;

        [Tooltip("Fewest and most of them.")]
        [SerializeField] private Vector2Int howMany;

        [Tooltip("Smallest and largest they are.")]
        [SerializeField] private Vector2Int howBig;

        [Tooltip("Closest and furthest apart they sit.")]
        [SerializeField] private Vector2Int howFarApart;

        [Tooltip("Narrowest and widest angle they are placed at, in degrees.")]
        [SerializeField] private Vector2Int atWhatAngle;

        [Tooltip("Their angle lines up with the direction out from the Core.")]
        [SerializeField] private bool alignedWithTheCore;

        [Tooltip("Paths to them run straight rather than winding.")]
        [SerializeField] private bool straightPathsToThem;

        [Tooltip("They may overlap other rooms.")]
        [SerializeField] private bool mayOverlapOtherRooms;

        [Tooltip("Which kinds of room they may overlap.")]
        [SerializeField] private DimensionRoomKind mayOverlapKinds;

        [Tooltip("A note to yourself about what this rule is for.")]
        [SerializeField] private string note;

        public DimensionRoomPlacement Placement { get { return placement; } }

        public DimensionRoomKind Kind { get { return kind; } }

        public Vector2Int HowMany { get { return Ordered(howMany); } }

        public Vector2Int HowBig { get { return Ordered(howBig); } }

        public Vector2Int HowFarApart { get { return Ordered(howFarApart); } }

        public Vector2Int AtWhatAngle { get { return Ordered(atWhatAngle); } }

        public bool AlignedWithTheCore { get { return alignedWithTheCore; } }

        public bool StraightPathsToThem { get { return straightPathsToThem; } }

        public bool MayOverlapOtherRooms { get { return mayOverlapOtherRooms; } }

        public DimensionRoomKind MayOverlapKinds { get { return mayOverlapKinds; } }

        public string Note { get { return note ?? string.Empty; } }

        internal static Vector2Int Ordered(Vector2Int range)
        {
            return new Vector2Int(range.x, range.y < range.x ? range.x : range.y);
        }
    }

    /// <summary>One rule about how a dungeon's rooms are joined.</summary>
    [Serializable]
    public struct DimensionDungeonPath
    {
        [Tooltip("How the paths are chosen.")]
        [SerializeField] private DimensionPathPlacement placement;

        [Tooltip("Which kind of room these paths belong to.")]
        [SerializeField] private DimensionRoomKind kind;

        [Tooltip("Fewest and most of them.")]
        [SerializeField] private Vector2Int howMany;

        [Tooltip("How wide they are.")]
        [Min(0f)]
        [SerializeField] private float width;

        [Tooltip("They run straight rather than winding.")]
        [SerializeField] private bool straight;

        [Tooltip("They may cross other paths.")]
        [SerializeField] private bool mayCrossPaths;

        [Tooltip("Which paths they may cross.")]
        [SerializeField] private DimensionRoomKind mayCrossPathKinds;

        [Tooltip("They may cut through rooms.")]
        [SerializeField] private bool mayCrossRooms;

        [Tooltip("Which rooms they may cut through.")]
        [SerializeField] private DimensionRoomKind mayCrossRoomKinds;

        [Tooltip("Which kind of room they start from.")]
        [SerializeField] private DimensionRoomKind startsFrom;

        [Tooltip("Which kind they end at.")]
        [SerializeField] private DimensionRoomKind endsAt;

        [Tooltip("A note to yourself about what this rule is for.")]
        [SerializeField] private string note;

        public DimensionPathPlacement Placement { get { return placement; } }

        public DimensionRoomKind Kind { get { return kind; } }

        public Vector2Int HowMany { get { return DimensionDungeonRoom.Ordered(howMany); } }

        public float Width { get { return width < 0f ? 0f : width; } }

        public bool Straight { get { return straight; } }

        public bool MayCrossPaths { get { return mayCrossPaths; } }

        public DimensionRoomKind MayCrossPathKinds { get { return mayCrossPathKinds; } }

        public bool MayCrossRooms { get { return mayCrossRooms; } }

        public DimensionRoomKind MayCrossRoomKinds { get { return mayCrossRoomKinds; } }

        public DimensionRoomKind StartsFrom { get { return startsFrom; } }

        public DimensionRoomKind EndsAt { get { return endsAt; } }

        public string Note { get { return note ?? string.Empty; } }
    }

    /// <summary>One object swapped for another as a dungeon is built.</summary>
    /// <remarks>
    /// How a theme is applied without redrawing a dungeon: build it out of ordinary stone and then
    /// swap every stone block for the themed one.
    /// </remarks>
    [Serializable]
    public struct DimensionDungeonSwap
    {
        [Tooltip("What gets replaced.")]
        [SerializeField] private string replaceId;

        [Tooltip("What it becomes.")]
        [SerializeField] private string withId;

        [Tooltip("Only replace this one look of it. Leave off to replace every look.")]
        [SerializeField] private bool onlyOneLook;

        [Tooltip("Which look that is.")]
        [Min(0)]
        [SerializeField] private int theLook;

        [Tooltip("Which looks the replacement takes, as a range.")]
        [SerializeField] private Vector2Int replacementLooks;

        public string ReplaceId { get { return replaceId ?? string.Empty; } }

        public string WithId { get { return withId ?? string.Empty; } }

        public bool OnlyOneLook { get { return onlyOneLook; } }

        public int TheLook { get { return theLook < 0 ? 0 : theLook; } }

        public Vector2Int ReplacementLooks
        {
            get { return DimensionDungeonRoom.Ordered(replacementLooks); }
        }
    }

    /// <summary>Where a kind of room is put. Core Keeper's <c>RoomPlacementAlgorithm</c>.</summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionRoomPlacement
    {
        /// <summary>Not placed. <c>None</c>.</summary>
        NotPlaced = 0,

        /// <summary>At the middle of the dungeon. <c>Center</c>.</summary>
        AtTheCentre = 1,

        /// <summary>Anywhere. <c>Random</c>.</summary>
        Anywhere = 2,

        /// <summary>At the way in. <c>Entrance</c>.</summary>
        AtTheEntrance = 3,

        /// <summary>Grown outward from the rooms already placed. <c>FromOtherRooms</c>.</summary>
        OutwardFromOtherRooms = 4,

        /// <summary>At a spot decided in advance. <c>fixedPlacement</c>.</summary>
        AtAFixedSpot = 5
    }

    /// <summary>How paths are chosen. Core Keeper's <c>PathPlacementAlgorithm</c>.</summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionPathPlacement
    {
        /// <summary>No paths. <c>None</c>.</summary>
        NoPaths = 0,

        /// <summary>Joined at random. <c>Random</c>.</summary>
        AtRandom = 1,

        /// <summary>The shortest set of paths that still joins everything. <c>MinSpanningTree</c>.</summary>
        TheLeastPathsThatJoinEverything = 2,

        /// <summary>Everything joined to everything. <c>All</c>.</summary>
        EverythingToEverything = 3,

        /// <summary>The shortest way between them. <c>ShortestPath</c>.</summary>
        TheShortestWay = 4,

        /// <summary>The longest way, for a dungeon you wander. <c>LongestPath</c>.</summary>
        TheLongestWay = 5
    }

    /// <summary>
    /// What kind of room something is. Core Keeper's <c>RoomFlags</c>, which is a set of flags
    /// rather than one choice — a room can be both an entrance and a main room.
    /// </summary>
    /// <remarks>The values match the game's flags exactly and must stay that way.</remarks>
    [Flags]
    public enum DimensionRoomKind
    {
        None = 0,
        Main = 1,
        HandmadeScene = 2,
        Entrance = 4,
        DeadEnd = 8,
        Connecting = 0x10,
        Filler = 0x20,
        YourOwnKind1 = 0x40,
        YourOwnKind2 = 0x80,
        YourOwnKind3 = 0x100,
        YourOwnKind4 = 0x200,
        YourOwnKind5 = 0x400
    }
}
