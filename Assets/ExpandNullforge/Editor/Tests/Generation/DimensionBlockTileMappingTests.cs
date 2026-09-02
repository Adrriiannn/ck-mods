#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Generation;
using ExpandNullforge.Tilesets;
using NUnit.Framework;
using PugTilemap;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pins the block → Core Keeper tile bridge. The role → <see cref="TileType"/> mapping is the
    /// thing that silently produces the wrong terrain in-game if a name drifts, so it is asserted
    /// against the real enum here rather than trusted.
    /// </summary>
    internal sealed class DimensionBlockTileMappingTests
    {
        [Test]
        public void CoreRoles_MapToTheExpectedTileTypes()
        {
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Ground),
                Is.EqualTo(TileType.ground));
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Wall),
                Is.EqualTo(TileType.wall));
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Pit),
                Is.EqualTo(TileType.pit));
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Liquid),
                Is.EqualTo(TileType.water));
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Vein),
                Is.EqualTo(TileType.ore));
            // The engine's solid roof is implicit; the ceiling layer's paintable tile is the
            // skylight opening. TileType.roof is obsolete and must stay out of the mapping.
            Assert.That(DimensionBlockTileMapping.ToTileType(DimensionTileRole.Ceiling),
                Is.EqualTo(TileType.roofHole));
        }

        [Test]
        public void EveryRole_MapsToSomething_NoUnhandledFallThrough()
        {
            foreach (DimensionTileRole role in
                System.Enum.GetValues(typeof(DimensionTileRole)))
            {
                // Must not throw for any role, including Custom/Decoration.
                TileType tile = DimensionBlockTileMapping.ToTileType(role);
                Assert.That(System.Enum.IsDefined(typeof(TileType), tile), Is.True, role.ToString());
            }
        }

        [Test]
        public void VanillaBlock_ResolvesItsTilesetIndexDirectly()
        {
            DimensionCompiledBlock block = new DimensionCompiledBlock(
                DimensionTileRole.Wall, DimensionBlockTilesetSource.Vanilla, 7, string.Empty);

            Assert.That(DimensionBlockTileMapping.TryResolveTileset(block, out int tileset), Is.True);
            Assert.That(tileset, Is.EqualTo(7));
        }

        /// <summary>
        /// A named custom block resolves to the id its NAME derives to. The id is a pure function
        /// of the name, so it resolves without
        /// the tileset being installed or even existing.
        /// </summary>
        [Test]
        public void CustomBlock_ResolvesToTheIdItsNameDerivesTo()
        {
            DimensionCompiledBlock block = new DimensionCompiledBlock(
                DimensionTileRole.Ground, DimensionBlockTilesetSource.Custom, 0, "mod:crystal");

            Assert.That(DimensionBlockTileMapping.TryResolveTileset(block, out int tileset), Is.True);

            Assert.That(
                tileset,
                Is.EqualTo(DimensionTilesetRegistry.ComputeTilesetId("mod:crystal")));
            Assert.That(tileset, Is.Not.EqualTo(0), "Never silently vanilla dirt.");
        }

        /// <summary>
        /// The half of the original test that still matters: a block flagged custom that names NO
        /// tileset has no identity to be correct about, so it must fail loudly rather than defaulting
        /// to tileset 0 and quietly generating dirt where the author asked for something else.
        /// </summary>
        [Test]
        public void CustomBlockNamingNothing_FailsRatherThanFallingBackToTileset0()
        {
            DimensionCompiledBlock block = new DimensionCompiledBlock(
                DimensionTileRole.Ground, DimensionBlockTilesetSource.Custom, 0, string.Empty);

            Assert.That(DimensionBlockTileMapping.TryResolveTileset(block, out int tileset), Is.False);
            Assert.That(tileset, Is.EqualTo(0));
        }
    }
}
#endif
