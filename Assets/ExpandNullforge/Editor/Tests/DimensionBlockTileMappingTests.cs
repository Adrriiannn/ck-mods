#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Generation;
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

        [Test]
        public void CustomBlock_DoesNotSilentlyResolveToTileset0()
        {
            DimensionCompiledBlock block = new DimensionCompiledBlock(
                DimensionTileRole.Ground, DimensionBlockTilesetSource.Custom, 0, "mod:crystal");

            // No tileset provider ships yet, so a custom block must fail resolution loudly rather
            // than defaulting to tileset 0 and generating the wrong material.
            Assert.That(DimensionBlockTileMapping.TryResolveTileset(block, out int tileset), Is.False);
            Assert.That(tileset, Is.EqualTo(0));
        }
    }
}
#endif
