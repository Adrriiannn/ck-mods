using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// The shape-template half of a dungeon, mirrored into plain runtime values.
    /// </summary>
    /// <remarks>
    /// These mirrors exist because the Studio's shape template was consumed by nothing: every
    /// wobble, fill size and outline choice was authored and then never crossed into the built
    /// mod. The bootstrap emits literals into this class, and the assembler maps them onto the
    /// game's own dungeon components field for field.
    /// </remarks>
    public sealed class DimensionDungeonShape
    {
        public DimensionDungeonShape(
            int seed,
            int radius,
            bool hasAShapedOutline,
            float outlineWobble,
            float outlineBusyness,
            bool outlineFollowsTheRooms,
            bool isRectangular,
            float roomFillSize,
            float pathFillSize)
        {
            Seed = seed < 0 ? 0 : seed;
            Radius = radius < 0 ? 0 : radius;
            HasAShapedOutline = hasAShapedOutline;
            OutlineWobble = outlineWobble < 0f ? 0f : outlineWobble;
            OutlineBusyness = outlineBusyness < 0f ? 0f : outlineBusyness;
            OutlineFollowsTheRooms = outlineFollowsTheRooms;
            IsRectangular = isRectangular;
            RoomFillSize = roomFillSize < 0f ? 0f : roomFillSize;
            PathFillSize = pathFillSize < 0f ? 0f : pathFillSize;
        }

        /// <summary>0 keeps the id-derived seed; the game reseeds per placement either way.</summary>
        public readonly int Seed;

        /// <summary>Overrides the asset radius when positive.</summary>
        public readonly int Radius;

        public readonly bool HasAShapedOutline;

        public readonly float OutlineWobble;

        public readonly float OutlineBusyness;

        public readonly bool OutlineFollowsTheRooms;

        /// <summary>Rectangular FILL, not outline — the game has no rectangular outline.</summary>
        public readonly bool IsRectangular;

        public readonly float RoomFillSize;

        public readonly float PathFillSize;
    }

    /// <summary>One authored room rule, mirroring the Studio's fields into runtime values.</summary>
    public struct DimensionDungeonRoomRule
    {
        public DimensionRoomPlacement Placement;

        public DimensionRoomKind Kind;

        public int MinCount;

        public int MaxCount;

        /// <summary>Room radius in tiles, inclusive on both ends.</summary>
        public int MinRadius;

        public int MaxRadius;

        public int MinSpacing;

        public int MaxSpacing;

        /// <summary>Degrees, as authored; converted to radians at the game boundary.</summary>
        public float AngleMinDegrees;

        public float AngleMaxDegrees;

        public bool AlignedWithTheCore;

        public bool StraightPaths;

        public bool MayOverlapOtherRooms;

        public DimensionRoomKind MayOverlapKinds;
    }

    /// <summary>One authored path rule.</summary>
    public struct DimensionDungeonPathRule
    {
        public DimensionPathPlacement Placement;

        public DimensionRoomKind Kind;

        public int MinCount;

        public int MaxCount;

        public float Width;

        public bool Straight;

        public bool MayCrossPaths;

        public DimensionRoomKind MayCrossPathKinds;

        public bool MayCrossRooms;

        public DimensionRoomKind MayCrossRoomKinds;

        public DimensionRoomKind StartsFrom;

        public DimensionRoomKind EndsAt;
    }

    /// <summary>One object swapped for another as the dungeon builds — how a theme is applied.</summary>
    public struct DimensionDungeonSwapRule
    {
        public string ReplaceId;

        public string WithId;

        public bool OnlyOneLook;

        public int TheLook;

        public int MinReplacementLook;

        public int MaxReplacementLook;
    }

    /// <summary>One placed layer of a room filling, mirrored into plain runtime values.</summary>
    public struct DimensionDungeonFillingEntry
    {
        public string ObjectId;

        public int Look;

        public int MinPatches;

        public int MaxPatches;

        /// <summary>Value-aligned with the game's SpawnAlgorithm.</summary>
        public int PatchShape;

        public float PatchSize;

        public float ChanceToAppear;

        public float Density;

        public string[] MayLandOn;

        public string[] NeverOn;

        public int StackCount;

        public string ChestLoot;
    }

    /// <summary>One authored room or corridor filling: which rooms, and the paint-order layers.</summary>
    public sealed class DimensionDungeonFillingRule
    {
        public DimensionDungeonFillingRule(
            string fillingId,
            DimensionRoomKind fillsRooms,
            bool fillsCorridors,
            int onlyIfAtLeastThisBig,
            IReadOnlyList<DimensionDungeonFillingEntry> entries)
        {
            FillingId = fillingId ?? string.Empty;
            FillsRooms = fillsRooms;
            FillsCorridors = fillsCorridors;
            OnlyIfAtLeastThisBig = onlyIfAtLeastThisBig < 0 ? 0 : onlyIfAtLeastThisBig;
            Entries = entries ?? System.Array.Empty<DimensionDungeonFillingEntry>();
        }

        public readonly string FillingId;

        public readonly DimensionRoomKind FillsRooms;

        public readonly bool FillsCorridors;

        public readonly int OnlyIfAtLeastThisBig;

        public readonly IReadOnlyList<DimensionDungeonFillingEntry> Entries;
    }

    /// <summary>A dungeon that IS one handmade scene, the way vanilla's single-scene dungeons are.</summary>
    public sealed class DimensionDungeonSingleScene
    {
        public DimensionDungeonSingleScene(string sceneName, int reservedRadius)
        {
            SceneName = sceneName ?? string.Empty;
            ReservedRadius = reservedRadius < 0 ? 0 : reservedRadius;
        }

        public readonly string SceneName;

        /// <summary>Kept clear of other dungeons while the world lays them out.</summary>
        public readonly int ReservedRadius;
    }

    /// <summary>Which part of a dungeon a group of scenes is used for.</summary>
    /// <remarks>
    /// Mirrors Core Keeper's own room roles rather than inventing categories. The generator decides
    /// where each kind goes — the entrance where the player arrives, ends at the extremities, main
    /// rooms along the way — so an author picks a role and the layout follows the game's own logic.
    /// </remarks>
    public enum DimensionDungeonRoomRole
    {
        /// <summary>An ordinary room along the dungeon's length.</summary>
        Main = 0,

        /// <summary>Where the player comes in.</summary>
        Entrance = 1,

        /// <summary>A dead end — where a dungeon puts the thing worth walking to.</summary>
        End = 2,

        /// <summary>A corridor or junction between larger rooms.</summary>
        Connecting = 3
    }

    /// <summary>A set of interchangeable scenes the generator may place in one kind of room.</summary>
    public sealed class DimensionDungeonRoomGroup
    {
        public DimensionDungeonRoomGroup(
            DimensionDungeonRoomRole role,
            int minRooms,
            int maxRooms,
            IReadOnlyList<string> sceneNames)
        {
            Role = role;
            MinRooms = minRooms;
            MaxRooms = maxRooms;
            SceneNames = sceneNames ?? new string[0];
        }

        public readonly DimensionDungeonRoomRole Role;

        /// <summary>How few of this kind of room a dungeon must have.</summary>
        public readonly int MinRooms;

        /// <summary>How many at most.</summary>
        public readonly int MaxRooms;

        /// <summary>
        /// The scenes that may fill this role, chosen between at generation.
        /// </summary>
        /// <remarks>
        /// A list rather than one scene because repetition is what makes a generated dungeon feel
        /// generated. Three interchangeable rooms read as a place; one room three times reads as a
        /// copy-paste.
        /// </remarks>
        public readonly IReadOnlyList<string> SceneNames;
    }

    /// <summary>One dungeon a dimension can grow.</summary>
    public sealed class DimensionDungeonDefinition
    {
        public DimensionDungeonDefinition(
            string dungeonId,
            string biomeId,
            int radius,
            float roomSize,
            float pathSize,
            float spawnChance,
            int minDistanceFromCentre,
            bool blockOtherSpawns,
            IReadOnlyList<DimensionDungeonRoomGroup> roomGroups,
            DimensionDungeonShape shape = null,
            IReadOnlyList<DimensionDungeonRoomRule> roomRules = null,
            IReadOnlyList<DimensionDungeonPathRule> pathRules = null,
            IReadOnlyList<string> outlineBlockIds = null,
            IReadOnlyList<DimensionDungeonSwapRule> swaps = null,
            DimensionDungeonSingleScene singleScene = null,
            IReadOnlyList<DimensionDungeonFillingRule> fillings = null,
            string dimensionId = null,
            DimensionScenePlacementMode dimensionPlacement = DimensionScenePlacementMode.Automatic,
            Unity.Mathematics.int2 exactLocalPosition = default,
            int minRadiusTiles = 0,
            int maxRadiusTiles = 0,
            int countPerArea = 1)
        {
            DungeonId = dungeonId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            Radius = radius;
            RoomSize = roomSize;
            PathSize = pathSize;
            SpawnChance = spawnChance;
            MinDistanceFromCentre = minDistanceFromCentre;
            BlockOtherSpawns = blockOtherSpawns;
            RoomGroups = roomGroups ?? new DimensionDungeonRoomGroup[0];
            Shape = shape;
            RoomRules = roomRules ?? new DimensionDungeonRoomRule[0];
            PathRules = pathRules ?? new DimensionDungeonPathRule[0];
            OutlineBlockIds = outlineBlockIds ?? new string[0];
            Swaps = swaps ?? new DimensionDungeonSwapRule[0];
            SingleScene = singleScene;
            Fillings = fillings ?? new DimensionDungeonFillingRule[0];
            DimensionId = dimensionId ?? string.Empty;
            DimensionPlacement = dimensionPlacement;
            ExactLocalPosition = exactLocalPosition;
            MinRadiusTiles = minRadiusTiles < 0 ? 0 : minRadiusTiles;
            MaxRadiusTiles = maxRadiusTiles < MinRadiusTiles ? MinRadiusTiles : maxRadiusTiles;
            CountPerArea = countPerArea < 1 ? 1 : countPerArea;
        }

        public readonly string DungeonId;

        /// <summary>The biome it grows in.</summary>
        public readonly string BiomeId;

        /// <summary>How far across the dungeon reaches, in tiles.</summary>
        public readonly int Radius;

        public readonly float RoomSize;
        public readonly float PathSize;

        /// <summary>How likely it is to be chosen when the world places a dungeon here.</summary>
        public readonly float SpawnChance;

        /// <summary>
        /// How far from the world's centre it may first appear.
        /// </summary>
        /// <remarks>
        /// Core Keeper's own distance gate, and the honest way to keep a hard dungeon away from a new
        /// player without forbidding anything — they can still walk there, it is simply further.
        /// </remarks>
        public readonly int MinDistanceFromCentre;

        /// <summary>Whether wandering creatures are kept out of it, as vanilla dungeons do.</summary>
        public readonly bool BlockOtherSpawns;

        public readonly IReadOnlyList<DimensionDungeonRoomGroup> RoomGroups;

        /// <summary>The authored shape template, or null when the asset never opened one.</summary>
        public readonly DimensionDungeonShape Shape;

        /// <summary>Authored room rules. Empty means the assembler synthesizes sensible ones.</summary>
        public readonly IReadOnlyList<DimensionDungeonRoomRule> RoomRules;

        /// <summary>Authored path rules. Empty means one minimum-spanning-tree corridor pass.</summary>
        public readonly IReadOnlyList<DimensionDungeonPathRule> PathRules;

        /// <summary>
        /// The blocks its fill shell is drawn from, bottom layer first.
        /// </summary>
        /// <remarks>
        /// Empty means no shell at all: rooms stamp their scenes onto raw cave, which is the
        /// safe today-state. The binary rule matters — a present-but-empty shell would satisfy
        /// the game's template query and then stamp nothing, silently.
        /// </remarks>
        public readonly IReadOnlyList<string> OutlineBlockIds;

        public readonly IReadOnlyList<DimensionDungeonSwapRule> Swaps;

        /// <summary>When set, the dungeon is this one scene and nothing else generates.</summary>
        public readonly DimensionDungeonSingleScene SingleScene;

        /// <summary>
        /// The procedural room contents — where a dungeon's character actually lives. The
        /// authored rows come before the outline shell's, so a filling only feeds the shell's
        /// Fill rooms when its author explicitly includes the Filler kind.
        /// </summary>
        public readonly IReadOnlyList<DimensionDungeonFillingRule> Fillings;

        /// <summary>
        /// The custom dimension this dungeon grows in. Empty means Overworld tables only —
        /// vanilla's own placer never runs inside a custom dimension, so without this the
        /// authored dungeon could never appear in the very dimension it was made for.
        /// </summary>
        public readonly string DimensionId;

        public readonly DimensionScenePlacementMode DimensionPlacement;

        public readonly Unity.Mathematics.int2 ExactLocalPosition;

        public readonly int MinRadiusTiles;

        public readonly int MaxRadiusTiles;

        /// <summary>How many of it one generated area may grow.</summary>
        public readonly int CountPerArea;
    }

    /// <summary>
    /// Every dungeon this mod defines, waiting to be built into the world's own dungeon tables.
    /// </summary>
    /// <remarks>
    /// Filled by generated bootstrap code at load. The rooms are named scenes, which are already in
    /// Core Keeper's scene table by the time a dungeon is assembled — so a dungeon is a arrangement of
    /// things the game can already place, not a new kind of content.
    /// </remarks>
    public static class DimensionDungeonRegistry
    {
        private static readonly List<DimensionDungeonDefinition> Definitions =
            new List<DimensionDungeonDefinition>();

        public static bool HasAny
        {
            get { return Definitions.Count > 0; }
        }

        public static IReadOnlyList<DimensionDungeonDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>Registers a dungeon, replacing any earlier one with the same id.</summary>
        public static void Register(DimensionDungeonDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.DungeonId))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].DungeonId,
                        definition.DungeonId,
                        System.StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }
}
