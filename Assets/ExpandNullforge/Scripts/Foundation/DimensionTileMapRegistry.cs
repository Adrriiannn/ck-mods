using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Runtime lookup from a dimension id to its painted tile map. Populated when the mod loads
    /// its dimension content (the authored map travels with the dimension asset) and read by the
    /// tile-map generation provider when that dimension generates.
    ///
    /// Kept as a small in-memory registry rather than threaded through the content manifest,
    /// because a tile grid is bulky and only the server generator needs it — the same reason the
    /// item-object registry is separate from the manifest.
    /// </summary>
    public static class DimensionTileMapRegistry
    {
        private static readonly Dictionary<string, DimensionTileMapModel> Maps =
            new Dictionary<string, DimensionTileMapModel>(StringComparer.Ordinal);

        /// <summary>Number of dimensions with a registered map.</summary>
        public static int Count => Maps.Count;

        /// <summary>
        /// Registers (or replaces) the map for a dimension. A null map removes any existing
        /// entry, so a dimension whose map was cleared does not keep generating a stale one.
        /// </summary>
        public static void Register(string dimensionId, DimensionTileMapModel map)
        {
            if (string.IsNullOrEmpty(dimensionId))
            {
                return;
            }

            if (map == null)
            {
                Maps.Remove(dimensionId);
                return;
            }

            Maps[dimensionId] = map;
            DimensionFrameworkLog.Warning(
                "[ExpandNullforge][tilemap] registered map for '" + dimensionId + "' with " +
                map.PaintedTileCount() + " painted tiles.");
        }

        public static bool TryGet(string dimensionId, out DimensionTileMapModel map)
        {
            map = null;
            return !string.IsNullOrEmpty(dimensionId) && Maps.TryGetValue(dimensionId, out map);
        }

        public static bool Has(string dimensionId)
        {
            return !string.IsNullOrEmpty(dimensionId) && Maps.ContainsKey(dimensionId);
        }

        public static void Remove(string dimensionId)
        {
            if (!string.IsNullOrEmpty(dimensionId))
            {
                Maps.Remove(dimensionId);
            }
        }

        public static void Clear()
        {
            Maps.Clear();
        }
    }
}
