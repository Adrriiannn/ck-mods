using System;
using System.Collections.Generic;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>One tile type's world-map color for a custom tileset.</summary>
    [Serializable]
    public struct DimensionTileMapColor
    {
        public TileType TileType;
        public Color32 Color;

        public DimensionTileMapColor(TileType tileType, Color32 color)
        {
            TileType = tileType;
            Color = color;
        }
    }

    /// <summary>
    /// A registered custom tileset: everything the framework's rendering, map and naming patches
    /// serve for one custom tileset id.
    ///
    /// Identity is the NAME ("Mod:TilesetName"); the numeric id is derived from it by
    /// <see cref="DimensionTilesetRegistry.ComputeTilesetId"/> — a pure function, so the id is the
    /// same on every install and never depends on load order or which other mods exist. World
    /// saves store the bare id inside their chunk data (TileCD), which stays valid forever.
    /// </summary>
    public sealed class DimensionCustomTileset
    {
        public DimensionCustomTileset(string name)
        {
            Name = name ?? string.Empty;
            Id = DimensionTilesetRegistry.ComputeTilesetId(Name);
        }

        /// <summary>The stable string identity, e.g. "Nullforge:EerieStone".</summary>
        public string Name { get; }

        /// <summary>The derived numeric id (>= <see cref="DimensionTilesetRegistry.MinCustomTilesetId"/>).</summary>
        public int Id { get; }

        /// <summary>Shown by GetFriendlyName (paint tool, diagnostics). Falls back to Name.</summary>
        public string FriendlyName;

        /// <summary>
        /// The quad-generation rule set (layer definitions). Optional: when null, the vanilla
        /// Dirt tileset's rules are reused, which renders ground/wall/roof correctly for any
        /// standard tileset — only the textures differ. Leave the rules' onlyAdaptToOwnTileset
        /// alone so tiles blend with vanilla neighbors (engine default behavior).
        /// </summary>
        public PugMapTileset LayersTemplate;


        /// <summary>Main sheet textures (regular/emissive/effect-mask/normals).</summary>
        public MapWorkshopTilesetBank.TilesetTextures Textures =
            new MapWorkshopTilesetBank.TilesetTextures();

        /// <summary>Optional per-layer adaptive (edge-blend) textures.</summary>
        public readonly Dictionary<LayerName, MapWorkshopTilesetBank.TilesetTextures> AdaptiveTextures =
            new Dictionary<LayerName, MapWorkshopTilesetBank.TilesetTextures>();

        /// <summary>Optional per-layer material overrides.</summary>
        public readonly Dictionary<LayerName, Material> OverrideMaterials =
            new Dictionary<LayerName, Material>();

        /// <summary>World-map colors per tile type (appended into the game's color table).</summary>
        public readonly List<DimensionTileMapColor> MapColors = new List<DimensionTileMapColor>();
    }
}
