#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The runtime map registry is how a dimension's painted map reaches its generation provider,
    /// so its register/replace/remove behaviour is pinned — a stale entry would keep generating an
    /// old map after the creator changed it.
    /// </summary>
    internal sealed class DimensionTileMapRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            DimensionTileMapRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DimensionTileMapRegistry.Clear();
        }

        [Test]
        public void RegisterThenGet_ReturnsTheMap()
        {
            DimensionTileMapModel map = new DimensionTileMapModel(int2.zero, 4, 4);
            DimensionTileMapRegistry.Register("mod:cavern", map);

            Assert.That(DimensionTileMapRegistry.Has("mod:cavern"), Is.True);
            Assert.That(DimensionTileMapRegistry.TryGet("mod:cavern", out DimensionTileMapModel got), Is.True);
            Assert.That(got, Is.SameAs(map));
        }

        [Test]
        public void RegisteringNull_RemovesAStaleEntry()
        {
            DimensionTileMapRegistry.Register("mod:cavern", new DimensionTileMapModel(int2.zero, 2, 2));
            DimensionTileMapRegistry.Register("mod:cavern", null);

            Assert.That(DimensionTileMapRegistry.Has("mod:cavern"), Is.False);
            Assert.That(DimensionTileMapRegistry.TryGet("mod:cavern", out _), Is.False);
        }

        [Test]
        public void ReRegister_ReplacesRatherThanDuplicates()
        {
            DimensionTileMapModel first = new DimensionTileMapModel(int2.zero, 2, 2);
            DimensionTileMapModel second = new DimensionTileMapModel(int2.zero, 8, 8);
            DimensionTileMapRegistry.Register("mod:cavern", first);
            DimensionTileMapRegistry.Register("mod:cavern", second);

            Assert.That(DimensionTileMapRegistry.Count, Is.EqualTo(1));
            DimensionTileMapRegistry.TryGet("mod:cavern", out DimensionTileMapModel got);
            Assert.That(got, Is.SameAs(second));
        }

        [Test]
        public void NullAndEmptyIds_AreRejectedNotCrashed()
        {
            DimensionTileMapRegistry.Register(null, new DimensionTileMapModel(int2.zero, 1, 1));
            DimensionTileMapRegistry.Register(string.Empty, new DimensionTileMapModel(int2.zero, 1, 1));

            Assert.That(DimensionTileMapRegistry.Count, Is.EqualTo(0));
            Assert.That(DimensionTileMapRegistry.TryGet(null, out _), Is.False);
            Assert.That(DimensionTileMapRegistry.Has(string.Empty), Is.False);
        }

        /// <summary>
        /// The painted map lives on the layout asset and travels to the game as a JSON snapshot.
        /// The trip is where a map can quietly become empty — Unity's own JsonUtility does exactly
        /// that to a list of a mod-defined type — so what comes back out is checked, not assumed.
        /// </summary>
        [Test]
        public void ALayoutsPaintedMapSurvivesBeingWrittenAndReadBack()
        {
            DimensionLayoutTemplateAsset layout =
                UnityEngine.ScriptableObject.CreateInstance<DimensionLayoutTemplateAsset>();
            try
            {
                Assert.That(layout.HasPaintedTileMap, Is.False);
                Assert.That(layout.PaintedTileMap, Is.Null);

                DimensionTileMapModel painted = new DimensionTileMapModel(new int2(-4, -4), 8, 8);
                int ground = painted.AddBlock(new DimensionMapBlock(
                    "stone.ground",
                    "Stone",
                    ExpandNullforge.Api.DimensionTileRole.Ground,
                    ExpandNullforge.Api.DimensionBlockTilesetSource.Vanilla,
                    1,
                    string.Empty));
                painted.SetBlock(new int2(0, 0), ground);
                painted.SetBlock(new int2(1, 0), ground);
                painted.SetBlock(new int2(2, 0), ground);

                layout.SetPaintedTileMap(painted);
                Assert.That(layout.HasPaintedTileMap, Is.True);

                // Read it the way a fresh load does: through the snapshot, not the cached object.
                DimensionTileMapModel roundTripped = DimensionTileMapModel.FromSnapshot(
                    Newtonsoft.Json.JsonConvert.DeserializeObject<DimensionTileMapSnapshot>(
                        Newtonsoft.Json.JsonConvert.SerializeObject(painted.ToSnapshot())));

                Assert.That(roundTripped.PaintedTileCount(), Is.EqualTo(painted.PaintedTileCount()));
                Assert.That(roundTripped.PaletteCount, Is.EqualTo(1));
                Assert.That(roundTripped.Origin, Is.EqualTo(new int2(-4, -4)));
                Assert.That(
                    roundTripped.GetBlockIndex(new int2(1, 0), ExpandNullforge.Api.DimensionMapLayer.Ground),
                    Is.EqualTo(ground));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void ClearingALayoutsPaintedMapLeavesNothingBehind()
        {
            DimensionLayoutTemplateAsset layout =
                UnityEngine.ScriptableObject.CreateInstance<DimensionLayoutTemplateAsset>();
            try
            {
                DimensionTileMapModel painted = new DimensionTileMapModel(int2.zero, 4, 4);
                int ground = painted.AddBlock(new DimensionMapBlock(
                    "dirt.ground",
                    "Dirt",
                    ExpandNullforge.Api.DimensionTileRole.Ground,
                    ExpandNullforge.Api.DimensionBlockTilesetSource.Vanilla,
                    0,
                    string.Empty));
                painted.SetBlock(int2.zero, ground);
                layout.SetPaintedTileMap(painted);

                layout.SetPaintedTileMap(null);

                Assert.That(layout.PaintedTileMap, Is.Null);
                Assert.That(layout.HasPaintedTileMap, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }
        }
    }
}
#endif
