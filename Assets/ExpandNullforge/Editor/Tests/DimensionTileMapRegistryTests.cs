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
    }
}
#endif
