using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One entry in a map's block palette: a block the creator can paint. A block is a role
    /// (which becomes a Core Keeper <c>TileType</c> — ground, wall, water, pit…) plus a tileset
    /// (its material/biome skin), chosen from vanilla Core Keeper tilesets or a custom one the
    /// creator registered. The preview colour is the editor swatch shown before real tile art is
    /// composited.
    /// </summary>
    [Serializable]
    public sealed class DimensionMapBlock
    {
        [SerializeField] private string blockId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [Tooltip("The structural role. Wall/Vein paint into the wall layer; everything else into the ground layer.")]
        [SerializeField] private DimensionTileRole role = DimensionTileRole.Ground;
        [SerializeField] private DimensionBlockTilesetSource tilesetSource =
            DimensionBlockTilesetSource.Vanilla;
        [Tooltip("Vanilla Core Keeper tileset index (used when the source is Vanilla).")]
        [SerializeField] private int vanillaTilesetIndex;
        [Tooltip("Custom tileset id from the Dimensions API tileset registry (used when the source is Custom).")]
        [SerializeField] private string customTilesetId = string.Empty;
        [Tooltip("Swatch colour shown in the editor before real tile art is composited.")]
        [SerializeField] private Color previewColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        public DimensionMapBlock()
        {
        }

        public DimensionMapBlock(
            string blockId,
            string displayName,
            DimensionTileRole role,
            DimensionBlockTilesetSource tilesetSource,
            int vanillaTilesetIndex,
            string customTilesetId)
        {
            this.blockId = blockId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.role = role;
            this.tilesetSource = tilesetSource;
            this.vanillaTilesetIndex = Mathf.Max(0, vanillaTilesetIndex);
            this.customTilesetId = customTilesetId ?? string.Empty;
        }

        public DimensionMapBlock(
            string blockId,
            string displayName,
            DimensionTileRole role,
            DimensionBlockTilesetSource tilesetSource,
            int vanillaTilesetIndex,
            string customTilesetId,
            Color previewColor)
            : this(blockId, displayName, role, tilesetSource, vanillaTilesetIndex, customTilesetId)
        {
            this.previewColor = previewColor;
        }

        public string BlockId => blockId ?? string.Empty;

        public string DisplayName =>
            string.IsNullOrEmpty(displayName) ? BlockId : displayName;

        public DimensionTileRole Role => role;

        public DimensionMapLayer Layer => DimensionMapLayerRules.LayerForRole(role);

        public DimensionBlockTilesetSource TilesetSource => tilesetSource;

        public int VanillaTilesetIndex => Mathf.Max(0, vanillaTilesetIndex);

        public string CustomTilesetId => customTilesetId ?? string.Empty;

        public Color PreviewColor => previewColor;

        /// <summary>Resolves this palette entry to the positionless block the generator consumes.</summary>
        public DimensionCompiledBlock ToCompiled()
        {
            return new DimensionCompiledBlock(
                role, tilesetSource, VanillaTilesetIndex, CustomTilesetId);
        }
    }

    /// <summary>
    /// A dimension's painted tile map: a palette of blocks plus, per layer, a dense grid of
    /// palette references over a bounded local-tile region. This is the authoring model the
    /// Biome/Map Creator paints into and the generation system reads; it is backend-neutral
    /// (it says <em>what</em> tiles exist and where, not how they are written into the world).
    ///
    /// Coordinates are dimension-local tiles. A cell stores a palette index + 1 (0 = empty), so
    /// up to 255 blocks per palette — ample for a biome. Ground and wall are separate layers so a
    /// cell can carry both, exactly as Core Keeper stores a wall over its underlying ground.
    /// </summary>
    [Serializable]
    public sealed class DimensionTileMapModel
    {
        [Serializable]
        private sealed class LayerGrid
        {
            [SerializeField] private DimensionMapLayer layer;
            [SerializeField] private byte[] cells = Array.Empty<byte>();

            public LayerGrid()
            {
            }

            public LayerGrid(DimensionMapLayer layer, int cellCount)
            {
                this.layer = layer;
                cells = new byte[Mathf.Max(0, cellCount)];
            }

            public DimensionMapLayer Layer => layer;

            public byte[] Cells => cells ?? (cells = Array.Empty<byte>());

            public void Resize(int cellCount)
            {
                cells = new byte[Mathf.Max(0, cellCount)];
            }
        }

        [SerializeField] private int originX;
        [SerializeField] private int originY;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private List<DimensionMapBlock> palette = new List<DimensionMapBlock>();
        [SerializeField] private List<LayerGrid> layers = new List<LayerGrid>();

        public DimensionTileMapModel()
        {
        }

        public DimensionTileMapModel(int2 origin, int width, int height)
        {
            SetBounds(origin, width, height);
        }

        public int2 Origin => new int2(originX, originY);

        public int Width => Mathf.Max(0, width);

        public int Height => Mathf.Max(0, height);

        /// <summary>Local tile bounds this map covers (inclusive-min / exclusive-max).</summary>
        public DimensionBounds LocalBounds =>
            new DimensionBounds(Origin, Origin + new int2(Width, Height));

        public int PaletteCount => palette?.Count ?? 0;

        public IReadOnlyList<DimensionMapBlock> Palette =>
            palette ?? (palette = new List<DimensionMapBlock>());

        /// <summary>
        /// Sets the covered region, clearing all painted tiles. Called when a map is first created
        /// or resized to a different origin/extent; use <see cref="Resize"/> to keep content.
        /// </summary>
        public void SetBounds(int2 origin, int newWidth, int newHeight)
        {
            originX = origin.x;
            originY = origin.y;
            width = Mathf.Max(0, newWidth);
            height = Mathf.Max(0, newHeight);
            layers = new List<LayerGrid>();
        }

        /// <summary>
        /// Resizes the covered region while preserving tiles whose local position still falls
        /// inside the new region. Tiles outside the new region are dropped.
        /// </summary>
        public void Resize(int2 newOrigin, int newWidth, int newHeight)
        {
            newWidth = Mathf.Max(0, newWidth);
            newHeight = Mathf.Max(0, newHeight);

            List<LayerGrid> previous = layers ?? new List<LayerGrid>();
            int oldOriginX = originX;
            int oldOriginY = originY;
            int oldWidth = Width;

            originX = newOrigin.x;
            originY = newOrigin.y;
            width = newWidth;
            height = newHeight;
            layers = new List<LayerGrid>();

            for (int i = 0; i < previous.Count; i++)
            {
                LayerGrid old = previous[i];
                byte[] oldCells = old.Cells;
                for (int index = 0; index < oldCells.Length; index++)
                {
                    byte value = oldCells[index];
                    if (value == 0)
                    {
                        continue;
                    }

                    int localX = oldOriginX + (index % oldWidth);
                    int localY = oldOriginY + (index / oldWidth);
                    if (TryCellIndex(new int2(localX, localY), out int _))
                    {
                        SetRaw(new int2(localX, localY), old.Layer, value);
                    }
                }
            }
        }

        /// <summary>Adds a block to the palette and returns its index.</summary>
        public int AddBlock(DimensionMapBlock block)
        {
            if (block == null)
            {
                return -1;
            }

            palette ??= new List<DimensionMapBlock>();
            if (palette.Count >= byte.MaxValue)
            {
                // 255 is the cell encoding ceiling (index + 1 into a byte); refuse rather than
                // silently overwrite an existing block's cells by wrapping.
                return -1;
            }

            palette.Add(block);
            return palette.Count - 1;
        }

        public DimensionMapBlock GetBlock(int paletteIndex)
        {
            return paletteIndex >= 0 && palette != null && paletteIndex < palette.Count
                ? palette[paletteIndex]
                : null;
        }

        public bool InBounds(int2 localPosition)
        {
            return TryCellIndex(localPosition, out int _);
        }

        /// <summary>
        /// Paints a palette block at a cell. The block's role decides the layer, so painting a
        /// wall does not erase the ground beneath it. Returns false for an out-of-bounds cell or
        /// an invalid palette index.
        /// </summary>
        public bool SetBlock(int2 localPosition, int paletteIndex)
        {
            DimensionMapBlock block = GetBlock(paletteIndex);
            if (block == null || !TryCellIndex(localPosition, out int _))
            {
                return false;
            }

            SetRaw(localPosition, block.Layer, (byte)(paletteIndex + 1));
            return true;
        }

        /// <summary>Clears a specific layer at a cell.</summary>
        public bool ClearBlock(int2 localPosition, DimensionMapLayer layer)
        {
            if (!TryCellIndex(localPosition, out int _))
            {
                return false;
            }

            SetRaw(localPosition, layer, 0);
            return true;
        }

        /// <summary>Returns the palette index painted at a cell/layer, or -1 if empty.</summary>
        public int GetBlockIndex(int2 localPosition, DimensionMapLayer layer)
        {
            if (!TryCellIndex(localPosition, out int index))
            {
                return -1;
            }

            LayerGrid grid = FindLayer(layer);
            if (grid == null)
            {
                return -1;
            }

            byte[] cells = grid.Cells;
            byte value = index < cells.Length ? cells[index] : (byte)0;
            return value == 0 ? -1 : value - 1;
        }

        /// <summary>Number of painted (non-empty) cells across all layers.</summary>
        public int PaintedTileCount()
        {
            int count = 0;
            if (layers == null)
            {
                return 0;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                byte[] cells = layers[i].Cells;
                for (int index = 0; index < cells.Length; index++)
                {
                    if (cells[index] != 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Yields every painted tile as a resolved placement, in ground-then-wall layer order so
        /// the generator writes floors before the walls that sit on them.
        /// </summary>
        public IEnumerable<DimensionTilePlacement> EnumeratePlacements()
        {
            if (layers == null)
            {
                yield break;
            }

            // Ground first, then Wall — deterministic regardless of layer insertion order.
            for (int layerValue = 0; layerValue < DimensionMapLayerRules.LayerCount; layerValue++)
            {
                DimensionMapLayer layer = (DimensionMapLayer)layerValue;
                LayerGrid grid = FindLayer(layer);
                if (grid == null)
                {
                    continue;
                }

                byte[] cells = grid.Cells;
                int gridWidth = Width;
                for (int index = 0; index < cells.Length; index++)
                {
                    byte value = cells[index];
                    if (value == 0)
                    {
                        continue;
                    }

                    DimensionMapBlock block = GetBlock(value - 1);
                    if (block == null)
                    {
                        continue;
                    }

                    int2 local = new int2(
                        originX + (index % gridWidth),
                        originY + (index / gridWidth));
                    yield return new DimensionTilePlacement(local, block.ToCompiled());
                }
            }
        }

        /// <summary>
        /// Converts this model to its flat, JsonUtility-safe snapshot. Used when writing the map
        /// into a runtime manifest so it survives Core Keeper's load-time recompile — a private
        /// nested class or byte[] does not (see <see cref="DimensionTileMapSnapshot"/>).
        /// </summary>
        public DimensionTileMapSnapshot ToSnapshot()
        {
            DimensionTileMapSnapshot snapshot = new DimensionTileMapSnapshot
            {
                originX = originX,
                originY = originY,
                width = width,
                height = height
            };

            List<DimensionMapBlock> pal = palette ?? new List<DimensionMapBlock>();
            snapshot.palette = new DimensionMapBlockSnapshot[pal.Count];
            for (int i = 0; i < pal.Count; i++)
            {
                DimensionMapBlock block = pal[i];
                Color color = block.PreviewColor;
                snapshot.palette[i] = new DimensionMapBlockSnapshot
                {
                    blockId = block.BlockId,
                    displayName = block.DisplayName,
                    role = (int)block.Role,
                    tilesetSource = (int)block.TilesetSource,
                    vanillaTilesetIndex = block.VanillaTilesetIndex,
                    customTilesetId = block.CustomTilesetId,
                    previewR = color.r,
                    previewG = color.g,
                    previewB = color.b,
                    previewA = color.a
                };
            }

            List<LayerGrid> lay = layers ?? new List<LayerGrid>();
            snapshot.layers = new DimensionTileMapLayerSnapshot[lay.Count];
            for (int i = 0; i < lay.Count; i++)
            {
                byte[] cells = lay[i].Cells;
                int[] intCells = new int[cells.Length];
                for (int c = 0; c < cells.Length; c++)
                {
                    intCells[c] = cells[c];
                }

                snapshot.layers[i] = new DimensionTileMapLayerSnapshot
                {
                    layer = (int)lay[i].Layer,
                    cells = intCells
                };
            }

            return snapshot;
        }

        /// <summary>Rebuilds a model from its flat snapshot, or null if the snapshot is null.</summary>
        public static DimensionTileMapModel FromSnapshot(DimensionTileMapSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            DimensionTileMapModel model = new DimensionTileMapModel();
            model.originX = snapshot.originX;
            model.originY = snapshot.originY;
            model.width = Mathf.Max(0, snapshot.width);
            model.height = Mathf.Max(0, snapshot.height);

            model.palette = new List<DimensionMapBlock>();
            if (snapshot.palette != null)
            {
                for (int i = 0; i < snapshot.palette.Length; i++)
                {
                    DimensionMapBlockSnapshot s = snapshot.palette[i];
                    if (s == null)
                    {
                        continue;
                    }

                    model.palette.Add(new DimensionMapBlock(
                        s.blockId,
                        s.displayName,
                        (DimensionTileRole)s.role,
                        (DimensionBlockTilesetSource)s.tilesetSource,
                        s.vanillaTilesetIndex,
                        s.customTilesetId,
                        new Color(s.previewR, s.previewG, s.previewB, s.previewA)));
                }
            }

            model.layers = new List<LayerGrid>();
            if (snapshot.layers != null)
            {
                for (int i = 0; i < snapshot.layers.Length; i++)
                {
                    DimensionTileMapLayerSnapshot s = snapshot.layers[i];
                    if (s == null)
                    {
                        continue;
                    }

                    int[] source = s.cells ?? Array.Empty<int>();
                    LayerGrid grid = new LayerGrid((DimensionMapLayer)s.layer, source.Length);
                    byte[] cells = grid.Cells;
                    int count = Mathf.Min(source.Length, cells.Length);
                    for (int c = 0; c < count; c++)
                    {
                        cells[c] = (byte)source[c];
                    }

                    model.layers.Add(grid);
                }
            }

            return model;
        }

        private void SetRaw(int2 localPosition, DimensionMapLayer layer, byte value)
        {
            if (!TryCellIndex(localPosition, out int index))
            {
                return;
            }

            LayerGrid grid = FindLayer(layer);
            if (grid == null)
            {
                grid = new LayerGrid(layer, Width * Height);
                layers ??= new List<LayerGrid>();
                layers.Add(grid);
            }

            byte[] cells = grid.Cells;
            if (index < cells.Length)
            {
                cells[index] = value;
            }
        }

        private LayerGrid FindLayer(DimensionMapLayer layer)
        {
            if (layers == null)
            {
                return null;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Layer == layer)
                {
                    return layers[i];
                }
            }

            return null;
        }

        private bool TryCellIndex(int2 localPosition, out int index)
        {
            index = -1;
            int localX = localPosition.x - originX;
            int localY = localPosition.y - originY;
            if (localX < 0 || localY < 0 || localX >= Width || localY >= Height)
            {
                return false;
            }

            index = localX + (localY * Width);
            return true;
        }
    }
}
