using HarmonyLib;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Lets custom tileset ids through the game's tile-write helper.
    ///
    /// EntityUtility.AddTile hard-rejects any tileset id >= 75 — a guard calibrated to the
    /// vanilla enum being exactly full. Player placement (PlaceObjectSlot) and this framework's
    /// generation both write tiles through it, so custom ids need this lifted. The prefix
    /// replicates AddTile's exact semantics for our ids and skips the original:
    /// - the spawn-core protection (the four positions around 0,0 are only writable in
    ///   creative or for roofHole; the obsidian special-case can't apply to custom ids),
    /// - the layer-exclusivity bookkeeping (a wall removes roofHole at its position; ground
    ///   removes pit and water).
    /// Ids below <see cref="DimensionTilesetRegistry.MinCustomTilesetId"/> run vanilla untouched.
    ///
    /// Note: this managed patch reaches the placement path because the framework already runs
    /// EquipmentUpdateSystem un-Bursted (the portal item-use hook requires it).
    /// </summary>
    [HarmonyPatch(typeof(EntityUtility), nameof(EntityUtility.AddTile))]
    internal static class DimensionAddTilePatch
    {
        [HarmonyPrefix]
        private static bool Before(
            int tileSet,
            TileType tileType,
            int2 position,
            bool isWorldModeCreative,
            DynamicBuffer<TileUpdateBuffer> tileUpdateBuffer)
        {
            if (tileSet < DimensionTilesetRegistry.MinCustomTilesetId)
            {
                return true;
            }

            // Proof, once per tileset, that the guard lift ran on THIS peer. In multiplayer the tile
            // only survives if the authoritative side writes it too, so a client log carrying this line
            // while the host log does not means the host is missing the mod.
            DimensionTilesetRegistry.LogGuardLiftOnce(tileSet);

            TileCD tile = new TileCD
            {
                tileset = tileSet,
                tileType = tileType
            };

            bool atSpawnCore =
                math.all(position == new int2(0, 0)) ||
                math.all(position == new int2(0, 1)) ||
                math.all(position == new int2(-1, 1)) ||
                math.all(position == new int2(1, 1));
            if (!isWorldModeCreative && atSpawnCore && tile.tileType != TileType.roofHole)
            {
                return false;
            }

            tileUpdateBuffer.Add(new TileUpdateBuffer
            {
                command = TileUpdateBuffer.Command.Add,
                position = position,
                tile = tile
            });

            if (tile.tileType == TileType.wall)
            {
                tileUpdateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = position,
                    tile = new TileCD { tileType = TileType.roofHole }
                });
            }
            else if (tile.tileType == TileType.ground)
            {
                tileUpdateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = position,
                    tile = new TileCD { tileType = TileType.pit }
                });
                tileUpdateBuffer.Add(new TileUpdateBuffer
                {
                    command = TileUpdateBuffer.Command.Remove,
                    position = position,
                    tile = new TileCD { tileType = TileType.water }
                });
            }

            return false;
        }
    }
}
