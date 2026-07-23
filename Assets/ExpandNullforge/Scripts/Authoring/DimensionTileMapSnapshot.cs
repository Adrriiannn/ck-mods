using System;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Flat, JsonUtility-safe serialization form of a <see cref="DimensionTileMapModel"/>. Core
    /// Keeper recompiles a mod's C# at load time, and Unity's <c>JsonUtility.FromJson</c> silently
    /// fails to rebuild a graph that contains a private nested <c>[Serializable]</c> class or a
    /// <c>byte[]</c> in that context — it returns an object with the offending collection left
    /// empty. Every field here is public, every collection is a top-level public array of a public
    /// type, and cells are <c>int[]</c>, which is the same shape the runtime manifest snapshot uses
    /// and which round-trips reliably in-game. The model converts to and from this via
    /// <see cref="DimensionTileMapModel.ToSnapshot"/> / <see cref="DimensionTileMapModel.FromSnapshot"/>.
    /// </summary>
    [Serializable]
    public sealed class DimensionTileMapSnapshot
    {
        public int originX;
        public int originY;
        public int width;
        public int height;
        public DimensionMapBlockSnapshot[] palette = new DimensionMapBlockSnapshot[0];
        public DimensionTileMapLayerSnapshot[] layers = new DimensionTileMapLayerSnapshot[0];
    }

    /// <summary>Flat, JsonUtility-safe form of one palette block.</summary>
    [Serializable]
    public sealed class DimensionMapBlockSnapshot
    {
        public string blockId = string.Empty;
        public string displayName = string.Empty;
        public int role;
        public int tilesetSource;
        public int vanillaTilesetIndex;
        public string customTilesetId = string.Empty;
        public float previewR = 0.5f;
        public float previewG = 0.5f;
        public float previewB = 0.5f;
        public float previewA = 1f;
    }

    /// <summary>Flat, JsonUtility-safe form of one layer's dense cell grid (palette index + 1, 0 = empty).</summary>
    [Serializable]
    public sealed class DimensionTileMapLayerSnapshot
    {
        public int layer;
        public int[] cells = new int[0];
    }
}
