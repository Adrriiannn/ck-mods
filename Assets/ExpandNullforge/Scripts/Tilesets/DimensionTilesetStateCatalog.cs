using System;
using System.Collections.Generic;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>Which part of a block a state belongs to — drives the Studio's left rail.</summary>
    public enum DimensionBlockPart
    {
        Ground,
        Wall,
        Environment
    }

    /// <summary>
    /// One authorable "thing that can happen to a block" — a surface, an overlay, or a way the
    /// world acts on it — mapped to the game's own render layer. This is the vocabulary the Tileset
    /// Studio presents as tick-to-enable states; see the block-state research in the tileset plan.
    /// </summary>
    public sealed class DimensionTilesetState
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly LayerName Layer;
        public readonly DimensionBlockPart Part;
        public readonly bool Required;
        public readonly bool InDirtBaseline;
        public readonly bool IsLight;
        public readonly string Blurb;

        public DimensionTilesetState(
            string key,
            string displayName,
            LayerName layer,
            DimensionBlockPart part,
            bool required,
            bool inDirtBaseline,
            bool isLight,
            string blurb)
        {
            Key = key;
            DisplayName = displayName;
            Layer = layer;
            Part = part;
            Required = required;
            InDirtBaseline = inDirtBaseline;
            IsLight = isLight;
            Blurb = blurb;
        }
    }

    /// <summary>
    /// The fixed set of block states the Studio exposes, grounded in the engine's LayerName enum and
    /// verified against the decompiled dependency graph (TileTypeUtility.GetNeededTile): only ground
    /// and wall carry states; auto-derived companion passes (fronts, shadows, sun beam, indirect
    /// light) are never listed — the game generates those from the states themselves.
    /// </summary>
    public static class DimensionTilesetStateCatalog
    {
        public static readonly IReadOnlyList<DimensionTilesetState> All = new List<DimensionTilesetState>
        {
            // ---- Ground ----
            new DimensionTilesetState("surface", "Surface", LayerName.ground, DimensionBlockPart.Ground, true, true, false,
                "The base look of your ground, edge-blended into its neighbors."),
            new DimensionTilesetState("tilled", "Tilled", LayerName.dugUpGround, DimensionBlockPart.Ground, false, true, false,
                "Hoed soil ready for planting crops."),
            new DimensionTilesetState("watered", "Watered", LayerName.wateredGround, DimensionBlockPart.Ground, false, true, false,
                "Darkened wet soil after the watering can."),
            new DimensionTilesetState("flooded", "Flooded", LayerName.water, DimensionBlockPart.Ground, false, true, false,
                "How the tile reads when it holds water."),
            new DimensionTilesetState("slime", "Slime-covered", LayerName.groundSlime, DimensionBlockPart.Ground, false, true, false,
                "Slime spread over the ground by enemies or biome."),
            new DimensionTilesetState("pebbles", "Pebbles", LayerName.smallStones, DimensionBlockPart.Ground, false, true, false,
                "Loose small rocks scattered on top."),
            new DimensionTilesetState("grass", "Grass tufts", LayerName.smallGrass, DimensionBlockPart.Ground, false, true, false,
                "Grass and straws sprouting from the surface."),
            new DimensionTilesetState("roots", "Big roots", LayerName.bigRoot, DimensionBlockPart.Ground, false, true, false,
                "Thick destructible roots growing on your ground (their shadow and glow are drawn automatically)."),
            new DimensionTilesetState("chrysalis", "Chrysalis", LayerName.chrysalis, DimensionBlockPart.Ground, false, true, false,
                "A larva-hive membrane spread across the ground."),
            new DimensionTilesetState("debris", "Debris", LayerName.debris, DimensionBlockPart.Ground, false, true, false,
                "Rubble and scattered bits laid over the ground."),
            new DimensionTilesetState("rubble", "Rubble", LayerName.debris2, DimensionBlockPart.Ground, false, true, false,
                "A second, denser debris pass for variety."),
            new DimensionTilesetState("floorCracks", "Digging cracks", LayerName.floorCrack, DimensionBlockPart.Ground, false, true, false,
                "Cracks that deepen as the ground is dug."),

            // ---- Wall ----
            new DimensionTilesetState("wall", "Surface", LayerName.wall, DimensionBlockPart.Wall, true, true, false,
                "The wall's top, front face and cast shadow — drawn as one set."),
            new DimensionTilesetState("cracks", "Mining cracks", LayerName.wallCrack, DimensionBlockPart.Wall, false, true, false,
                "Cracks that deepen as the wall is mined."),
            new DimensionTilesetState("ore", "Ore veins", LayerName.ore, DimensionBlockPart.Wall, false, true, false,
                "A mineable resource embedded in the wall."),
            new DimensionTilesetState("crystal", "Ancient crystal", LayerName.ancientCrystal, DimensionBlockPart.Wall, false, true, false,
                "Ancient crystal formations embedded in the wall."),
            new DimensionTilesetState("vines", "Wall vines", LayerName.wallGrass, DimensionBlockPart.Wall, false, true, false,
                "Grass and creepers clinging to the wall."),

            // ---- Environment ----
            new DimensionTilesetState("roofHole", "Roof hole", LayerName.roofHole, DimensionBlockPart.Environment, false, true, false,
                "An opening broken in the roof above the tile — its sun beam is drawn automatically.")
        };

        private static readonly Dictionary<string, DimensionTilesetState> ByKey = BuildIndex();

        private static Dictionary<string, DimensionTilesetState> BuildIndex()
        {
            Dictionary<string, DimensionTilesetState> map =
                new Dictionary<string, DimensionTilesetState>(StringComparer.Ordinal);
            foreach (DimensionTilesetState state in All)
            {
                map[state.Key] = state;
            }

            return map;
        }

        public static bool TryGet(string key, out DimensionTilesetState state)
        {
            state = null;
            return !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out state);
        }

        public static IEnumerable<DimensionTilesetState> ForPart(DimensionBlockPart part)
        {
            foreach (DimensionTilesetState state in All)
            {
                if (state.Part == part)
                {
                    yield return state;
                }
            }
        }
    }

    /// <summary>
    /// Per-state authoring data stored on a tileset asset: whether the modder switched the state on,
    /// and an optional custom texture that overrides the main sheet for that one layer. A state left
    /// without a custom texture simply renders from the main dirt-layout sheet.
    /// </summary>
    [Serializable]
    public sealed class DimensionTilesetLayerConfig
    {
        public string key = string.Empty;
        public bool enabled;
        public Texture2D texture;

        /// <summary>
        /// For a scattered overlay (grass, pebbles, roots, slime): how much of the block's ground it
        /// covers, 0 to 1. Ignored by states the world drives itself, such as tilled or watered soil.
        /// </summary>
        /// <remarks>
        /// Defaults to a light dusting rather than zero. Ticking "grass tufts" and getting a bare
        /// world would read as the feature being broken, so the default has to be visible; a fifth of
        /// the ground is enough to read as decorated without looking like a lawn.
        /// </remarks>
        [Range(0f, 1f)]
        public float density = 0.2f;
    }
}
