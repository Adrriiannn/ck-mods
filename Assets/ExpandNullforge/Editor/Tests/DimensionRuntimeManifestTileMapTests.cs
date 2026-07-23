#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The tile map is carried on the runtime manifest as JSON, not as a nested Unity object,
    /// because a nested [Serializable] class does not survive Core Keeper's load-time recompile
    /// and deserializes to null in-game. These tests prove the JSON round-trip keeps the whole
    /// map — palette, both layers, and the painted cells — so HasTileMap is true at runtime.
    /// </summary>
    internal sealed class DimensionRuntimeManifestTileMapTests
    {
        [Test]
        public void SetThenGet_RoundTripsThePaintedMapThroughJson()
        {
            DimensionTileMapModel source = BuildDiscLikeMap();
            int expectedPainted = source.PaintedTileCount();
            Assert.That(expectedPainted, Is.GreaterThan(0));

            DimensionRuntimeManifestAsset manifest =
                ScriptableObject.CreateInstance<DimensionRuntimeManifestAsset>();
            try
            {
                manifest.SetTileMap(source);

                // Force a fresh deserialize from the stored JSON, exactly like a runtime load:
                // serialize the manifest and read it back into a new instance so no in-memory
                // object reference is carried over.
                DimensionRuntimeManifestAsset reloaded = ReloadThroughSerialization(manifest);

                Assert.That(reloaded.HasTileMap, Is.True, "Map must survive as JSON.");
                DimensionTileMapModel restored = reloaded.TileMap;
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.PaintedTileCount(), Is.EqualTo(expectedPainted));
                Assert.That(restored.PaletteCount, Is.EqualTo(source.PaletteCount));
                Assert.That(restored.LocalBounds.Min, Is.EqualTo(source.LocalBounds.Min));
                Assert.That(restored.LocalBounds.MaxExclusive, Is.EqualTo(source.LocalBounds.MaxExclusive));

                Object.DestroyImmediate(reloaded);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void BothLayersAndTheirCellsSurviveTheSnapshotRoundTrip()
        {
            DimensionTileMapModel source = new DimensionTileMapModel(int2.zero, 3, 3);
            int ground = source.AddBlock(Block("g", DimensionTileRole.Ground));
            int wall = source.AddBlock(Block("w", DimensionTileRole.Wall));
            source.SetBlock(new int2(1, 1), ground);
            source.SetBlock(new int2(1, 1), wall); // ground + wall at the same cell

            // Serialize through the flat snapshot with Newtonsoft — the exact path the manifest
            // uses. Unity's JsonUtility cannot rebuild arrays of mod-defined classes in the
            // recompiled-mod runtime (they come back empty); Newtonsoft round-trips them.
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(source.ToSnapshot());
            DimensionTileMapModel restored = DimensionTileMapModel.FromSnapshot(
                Newtonsoft.Json.JsonConvert.DeserializeObject<DimensionTileMapSnapshot>(json));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.PaletteCount, Is.EqualTo(2));
            Assert.That(
                restored.GetBlockIndex(new int2(1, 1), DimensionMapLayer.Ground), Is.EqualTo(ground));
            Assert.That(
                restored.GetBlockIndex(new int2(1, 1), DimensionMapLayer.Wall), Is.EqualTo(wall));
        }

        [Test]
        public void SnapshotRoundTrip_PreservesBoundsPaletteMetadataAndPaintedCount()
        {
            DimensionTileMapModel source = new DimensionTileMapModel(new int2(-16, -16), 32, 32);
            int ground = source.AddBlock(new DimensionMapBlock(
                "ground", "Ground", DimensionTileRole.Ground,
                DimensionBlockTilesetSource.Vanilla, 7, string.Empty));
            source.AddBlock(new DimensionMapBlock(
                "wall", "Wall", DimensionTileRole.Wall,
                DimensionBlockTilesetSource.Custom, 0, "mod:my-tileset"));
            for (int x = -4; x < 4; x++)
            {
                source.SetBlock(new int2(x, 0), ground);
            }

            DimensionTileMapModel restored = DimensionTileMapModel.FromSnapshot(
                Newtonsoft.Json.JsonConvert.DeserializeObject<DimensionTileMapSnapshot>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(source.ToSnapshot())));

            Assert.That(restored.LocalBounds.Min, Is.EqualTo(new int2(-16, -16)));
            Assert.That(restored.LocalBounds.MaxExclusive, Is.EqualTo(new int2(16, 16)));
            Assert.That(restored.PaintedTileCount(), Is.EqualTo(source.PaintedTileCount()));
            Assert.That(restored.GetBlock(1).TilesetSource, Is.EqualTo(DimensionBlockTilesetSource.Custom));
            Assert.That(restored.GetBlock(1).CustomTilesetId, Is.EqualTo("mod:my-tileset"));
            Assert.That(restored.GetBlock(0).VanillaTilesetIndex, Is.EqualTo(7));
        }

        [Test]
        public void NoMap_MeansHasTileMapFalse()
        {
            DimensionRuntimeManifestAsset manifest =
                ScriptableObject.CreateInstance<DimensionRuntimeManifestAsset>();
            try
            {
                Assert.That(manifest.HasTileMap, Is.False);
                manifest.SetTileMap(null);
                Assert.That(manifest.HasTileMap, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        private static DimensionRuntimeManifestAsset ReloadThroughSerialization(
            DimensionRuntimeManifestAsset manifest)
        {
            // EditorJsonUtility captures the serialized (SerializeField) state; reading it into a
            // fresh instance mimics a bundle load without carrying the in-memory tile-map cache.
            string manifestJson = UnityEditor.EditorJsonUtility.ToJson(manifest);
            DimensionRuntimeManifestAsset reloaded =
                ScriptableObject.CreateInstance<DimensionRuntimeManifestAsset>();
            UnityEditor.EditorJsonUtility.FromJsonOverwrite(manifestJson, reloaded);
            return reloaded;
        }

        private static DimensionTileMapModel BuildDiscLikeMap()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(new int2(-8, -8), 16, 16);
            int ground = map.AddBlock(Block("ground", DimensionTileRole.Ground));
            int wall = map.AddBlock(Block("wall", DimensionTileRole.Wall));
            int water = map.AddBlock(Block("water", DimensionTileRole.Liquid));
            for (int y = -8; y < 8; y++)
            {
                for (int x = -8; x < 8; x++)
                {
                    if (math.length(new float2(x + 0.5f, y + 0.5f)) <= 7f)
                    {
                        map.SetBlock(new int2(x, y), ground);
                    }
                }
            }

            map.SetBlock(new int2(0, 0), wall);
            map.SetBlock(new int2(2, 2), water);
            return map;
        }

        private static DimensionMapBlock Block(string id, DimensionTileRole role)
        {
            return new DimensionMapBlock(
                id, id, role, DimensionBlockTilesetSource.Vanilla, 0, string.Empty);
        }
    }
}
#endif
