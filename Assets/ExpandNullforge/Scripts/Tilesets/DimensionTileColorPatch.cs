using System.Collections.Generic;
using HarmonyLib;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Gives custom tilesets their world-map colors.
    ///
    /// The game's TileTypeColorLookupSystem loads the shared TileTypeColorTable from Resources
    /// in OnCreate and bakes it into native lookup maps; a tileset absent from the table renders
    /// colorless on the map. Mod registrations happen at script-load time — before the ECS
    /// worlds (and this system) are created — so appending our entries to the loaded table right
    /// before OnCreate builds its maps folds custom colors in through the vanilla path, with no
    /// native-map surgery. Idempotent per table: re-created worlds re-run OnCreate against the
    /// same shared table instance, so entries are only appended once per registered tileset.
    /// </summary>
    [HarmonyPatch(typeof(TileTypeColorLookupSystem), "OnCreate")]
    internal static class DimensionTileColorPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        private static readonly HashSet<int> AppendedTilesetIds = new HashSet<int>();

        [HarmonyPrefix]
        private static void Before()
        {
            Fired++;

            TileTypeColorTable table = Resources.Load<TileTypeColorTable>("TileTypeColorTable");
            if (table == null || table.tileSetColors == null)
            {
                return;
            }

            foreach (DimensionCustomTileset tileset in DimensionTilesetRegistry.All)
            {
                if (tileset.MapColors.Count == 0 || !AppendedTilesetIds.Add(tileset.Id))
                {
                    continue;
                }

                TileTypeColorTable.TileSetColors entry = new TileTypeColorTable.TileSetColors
                {
                    pugMapTileset = (Tileset)tileset.Id,
                    tileColors = new List<TileTypeColorTable.TileColor>()
                };
                foreach (DimensionTileMapColor color in tileset.MapColors)
                {
                    entry.tileColors.Add(new TileTypeColorTable.TileColor
                    {
                        tileType = color.TileType,
                        color = color.Color
                    });
                }

                table.tileSetColors.Add(entry);
            }
        }
    }
}
