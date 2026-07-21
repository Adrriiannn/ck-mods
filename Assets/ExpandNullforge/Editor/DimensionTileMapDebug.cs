using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// A developer aid for proving the tile-map generation path in-game before the painting
    /// dashboard exists: it fills a dimension's runtime manifest with a deliberately
    /// non-rectangular shape that exercises every tile role — a ground disc, a wall ring around
    /// it, a water pool, and a void hole. If the whole disc (not a square) appears in-game with
    /// the water and hole in place, arbitrary-shape generation on Core Keeper's own tile path is
    /// working end to end.
    /// </summary>
    internal static class DimensionTileMapDebug
    {
        [MenuItem("Dimensions API/Debug/Fill Test Tile Map (Selected Manifest)")]
        private static void FillSelectedManifest()
        {
            DimensionRuntimeManifestAsset manifest =
                Selection.activeObject as DimensionRuntimeManifestAsset;
            if (manifest == null)
            {
                EditorUtility.DisplayDialog(
                    "Fill Test Tile Map",
                    "Select a Dimension Runtime Manifest asset first " +
                    "(the RuntimeManifest.asset in your generated dimension folder).",
                    "OK");
                return;
            }

            DimensionTileMapModel map = BuildProofShape();
            manifest.SetTileMap(map);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Fill Test Tile Map",
                "Filled a proof shape (" + map.PaintedTileCount() +
                " tiles: ground disc, wall ring, water pool, void hole) into '" +
                manifest.name + "'.\n\n" +
                "Build + install the mod, then enter the dimension. If the disc shape — not a " +
                "square — generates with the water and hole in place, the tile-map path works.\n\n" +
                "Do not re-export the dimension between now and building, or this test map is " +
                "overwritten.",
                "OK");
        }

        /// <summary>
        /// Builds the proof shape: a 32×32 region centred on the dimension's local origin holding
        /// a ground disc, a one-tile wall ring on its rim, an offset water pool, and an offset
        /// void hole. Internal so a test can assert it covers all four roles.
        /// </summary>
        internal static DimensionTileMapModel BuildProofShape()
        {
            const int size = 32;
            const float radius = 14f;
            int2 origin = new int2(-size / 2, -size / 2);

            DimensionTileMapModel map = new DimensionTileMapModel(origin, size, size);
            int ground = map.AddBlock(MakeBlock("test_ground", "Test Ground", DimensionTileRole.Ground));
            int wall = map.AddBlock(MakeBlock("test_wall", "Test Wall", DimensionTileRole.Wall));
            int water = map.AddBlock(MakeBlock("test_water", "Test Water", DimensionTileRole.Liquid));
            int pit = map.AddBlock(MakeBlock("test_void", "Test Void", DimensionTileRole.Pit));

            float2 center = new float2(0f, 0f);
            for (int y = origin.y; y < origin.y + size; y++)
            {
                for (int x = origin.x; x < origin.x + size; x++)
                {
                    float distance = math.length(new float2(x + 0.5f, y + 0.5f) - center);
                    if (distance > radius)
                    {
                        continue; // outside the disc — left empty (the arbitrary outline)
                    }

                    int2 cell = new int2(x, y);
                    map.SetBlock(cell, ground);
                    if (distance > radius - 1.5f)
                    {
                        map.SetBlock(cell, wall); // wall ring on the rim, over the ground
                    }
                }
            }

            FillDisc(map, new float2(-6f, -6f), 4f, water); // water pool
            FillDisc(map, new float2(7f, 7f), 3.5f, pit);   // void hole
            return map;
        }

        private static void FillDisc(DimensionTileMapModel map, float2 center, float radius, int block)
        {
            int minX = (int)math.floor(center.x - radius) - 1;
            int maxX = (int)math.ceil(center.x + radius) + 1;
            int minY = (int)math.floor(center.y - radius) - 1;
            int maxY = (int)math.ceil(center.y + radius) + 1;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (math.length(new float2(x + 0.5f, y + 0.5f) - center) <= radius)
                    {
                        map.SetBlock(new int2(x, y), block);
                    }
                }
            }
        }

        private static DimensionMapBlock MakeBlock(string id, string name, DimensionTileRole role)
        {
            return new DimensionMapBlock(
                id, name, role, DimensionBlockTilesetSource.Vanilla, 0, string.Empty);
        }
    }
}
