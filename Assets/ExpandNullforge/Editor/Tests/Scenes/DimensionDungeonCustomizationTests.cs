using System.Collections.Generic;
using ExpandNullforge.Creatures;
using ExpandNullforge.Loot;
using ExpandNullforge.Scenes;
using NUnit.Framework;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The measured laws behind "can I fully customize a dungeon?", locked as tests: the scene
    /// pivot, the corridor band, the fit radius, the biome binding and the loot-table identity.
    /// Each law came from the decompile with a file:line; a change that breaks one of these is
    /// a change that re-introduces an off-centred room or a silently dead binding.
    /// </summary>
    public sealed class DimensionDungeonCustomizationTests
    {
        private static DimensionSceneTile Tile(int x, int y)
        {
            return new DimensionSceneTile(new int2(x, y), 0, TileType.ground);
        }

        // ---- the pivot ----

        [Test]
        public void AnOddSceneCentresExactly()
        {
            // 5x5 painted 0..4: the middle tile is (2,2), no ambiguity.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>();
            for (int x = 0; x <= 4; x++)
            {
                for (int y = 0; y <= 4; y++)
                {
                    tiles.Add(Tile(x, y));
                }
            }

            Assert.AreEqual(new int2(2, 2), DimensionSceneGeometry.CentreOf(tiles));
        }

        [Test]
        public void AnEvenSceneAlwaysBiasesTheSameWay()
        {
            // 4x4 painted 0..3: (min+max)/2 = 1.5, and the pivot must not depend on rounding
            // mood — floor picks 1, every time. Vanilla's ties-to-even would pick 2 here and 1
            // one tile over, which is exactly the data-dependent wobble the helper exists to
            // remove.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>
            {
                Tile(0, 0), Tile(3, 3)
            };

            Assert.AreEqual(new int2(1, 1), DimensionSceneGeometry.CentreOf(tiles));
        }

        [Test]
        public void ANegativeCornerCentresLikeAPositiveOne()
        {
            // The same 4x4 shifted to start at (-2,-2): min+max = -1, and integer division
            // would truncate toward zero (0) where floor gives -1. The pivot must be the same
            // RELATIVE tile wherever the scene sat in authoring coordinates.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>
            {
                Tile(-2, -2), Tile(1, 1)
            };

            Assert.AreEqual(new int2(-1, -1), DimensionSceneGeometry.CentreOf(tiles));
        }

        // ---- the fit radius ----

        [Test]
        public void FitRadiusCoversTheFarthestTile()
        {
            // A 5x5 centred on (2,2): the corner sits sqrt(8) ≈ 2.83 away, so 3 is the radius
            // that keeps every tile inside the room's circle.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>
            {
                Tile(0, 0), Tile(4, 4), Tile(2, 2)
            };

            Assert.AreEqual(3, DimensionSceneGeometry.FitRadius(tiles, new int2(2, 2)));
        }

        [Test]
        public void FitRadiusIsExactForAnLShape()
        {
            // One long arm: a bounding-box diagonal would call this bigger than it is; the
            // real farthest tile is 10 away, exactly.
            List<DimensionSceneTile> tiles = new List<DimensionSceneTile>
            {
                Tile(0, 0), Tile(10, 0), Tile(0, 3)
            };

            Assert.AreEqual(10, DimensionSceneGeometry.FitRadius(tiles, int2.zero));
        }

        // ---- the corridor band ----

        [Test]
        public void CorridorsAreAlwaysAnOddNumberOfTiles()
        {
            // Measured from the carve test |z| <= width/2 + 0.1 over integer offsets
            // (DungeonGenerateRoomsSystem): 1.5 carves 1, 2 carves 3, 4 carves 5. The table is
            // the law; an even request silently gets the next odd band.
            Assert.AreEqual(1, DimensionSceneGeometry.EffectiveCorridorTiles(1f));
            Assert.AreEqual(1, DimensionSceneGeometry.EffectiveCorridorTiles(1.5f));
            Assert.AreEqual(3, DimensionSceneGeometry.EffectiveCorridorTiles(2f));
            Assert.AreEqual(3, DimensionSceneGeometry.EffectiveCorridorTiles(3f));
            Assert.AreEqual(5, DimensionSceneGeometry.EffectiveCorridorTiles(4f));
            Assert.AreEqual(5, DimensionSceneGeometry.EffectiveCorridorTiles(5f));
            Assert.AreEqual(7, DimensionSceneGeometry.EffectiveCorridorTiles(6f));
        }

        [Test]
        public void NormalizingTwiceChangesNothing()
        {
            // The stored width is the effective count, so re-assembling a definition must not
            // walk the width upward.
            for (float width = 0.5f; width <= 9f; width += 0.5f)
            {
                int once = DimensionSceneGeometry.EffectiveCorridorTiles(width);
                Assert.AreEqual(once, DimensionSceneGeometry.EffectiveCorridorTiles(once));
            }
        }

        // ---- the biome binding ----

        [Test]
        public void AVanillaBiomeNameBindsTheVanillaBiome()
        {
            // The bug this locks out: minting a custom id for "Desert" produced a binding no
            // Overworld cell ever reports, so the dungeon never rolled there.
            Assert.AreEqual(Biome.Desert, DimensionDungeonAssembler.ResolveBindingBiome("Desert"));
            Assert.AreEqual(Biome.Slime, DimensionDungeonAssembler.ResolveBindingBiome("Slime"));
        }

        [Test]
        public void AnEmptyBiomeIsTheWildcard()
        {
            Assert.AreEqual(Biome.None, DimensionDungeonAssembler.ResolveBindingBiome(null));
            Assert.AreEqual(Biome.None, DimensionDungeonAssembler.ResolveBindingBiome(string.Empty));
        }

        [Test]
        public void ACustomBiomeMintsItsOwnIdentity()
        {
            Biome custom = DimensionDungeonAssembler.ResolveBindingBiome("mod:glimmerdeep");
            Assert.GreaterOrEqual((int)custom, 1000);
        }

        // ---- the corridor mask ----

        [Test]
        public void ACorridorFillingNeverStealsTheFillTwins()
        {
            // Fill twins carry ONLY the Fill flag, and paths match by ALL bits — so a corridor
            // mask that includes Fill claims the widened wall shell and replaces it with floor.
            // The broadening must cover every real flag and add Fill only when the author chose
            // the Filler kind.
            PugWorldGen.RoomFlags broadened =
                DimensionDungeonAssembler.BroadenCorridorMask(Authoring.DimensionRoomKind.None);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.None, broadened & PugWorldGen.RoomFlags.Fill);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.Main, broadened & PugWorldGen.RoomFlags.Main);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.Entrance, broadened & PugWorldGen.RoomFlags.Entrance);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.End, broadened & PugWorldGen.RoomFlags.End);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.Connecting, broadened & PugWorldGen.RoomFlags.Connecting);
            Assert.AreEqual(
                PugWorldGen.RoomFlags.CustomScene, broadened & PugWorldGen.RoomFlags.CustomScene);

            PugWorldGen.RoomFlags chosen =
                DimensionDungeonAssembler.BroadenCorridorMask(Authoring.DimensionRoomKind.Filler);
            Assert.AreEqual(PugWorldGen.RoomFlags.Fill, chosen & PugWorldGen.RoomFlags.Fill);
        }

        // ---- the loot-table identity ----

        [Test]
        public void LootTableIdsAreDeterministicAndClearVanilla()
        {
            int id = DimensionLootTableRegistry.ComputeLootTableId("mod:crypt-chest");
            Assert.AreEqual(id, DimensionLootTableRegistry.ComputeLootTableId("mod:crypt-chest"));
            Assert.GreaterOrEqual(id, DimensionLootTableRegistry.MinCustomLootTableId);

            // The floor must actually clear the enum, or a mint could shadow a vanilla table.
            foreach (LootTableID vanilla in System.Enum.GetValues(typeof(LootTableID)))
            {
                Assert.Less((int)vanilla, DimensionLootTableRegistry.MinCustomLootTableId);
            }
        }

        [Test]
        public void ResolvingPrefersVanillaAndThenTheModsOwn()
        {
            DimensionLootTableRegistry.Clear();
            try
            {
                DimensionLootTableRegistry.Register(
                    "mod:crypt-chest",
                    false,
                    new[]
                    {
                        new DimensionLootTableRegistry.Entry
                        {
                            ItemObjectName = "IronBar", Weight = 1f, DropChance = 0.5f,
                            MinAmount = 1, MaxAmount = 2
                        }
                    });

                LootTableID resolved;
                Assert.IsTrue(DimensionLootTableRegistry.TryResolve("Caveling", out resolved));
                Assert.AreEqual(LootTableID.Caveling, resolved);

                Assert.IsTrue(DimensionLootTableRegistry.TryResolve("mod:crypt-chest", out resolved));
                Assert.AreEqual(
                    DimensionLootTableRegistry.ComputeLootTableId("mod:crypt-chest"),
                    (int)resolved);

                Assert.IsFalse(DimensionLootTableRegistry.TryResolve("mod:typo", out resolved));
            }
            finally
            {
                DimensionLootTableRegistry.Clear();
            }
        }

        // ---- the respawn rules ----

        [Test]
        public void ARespawnRuleReplacesItsOwnNameInsteadOfStacking()
        {
            DimensionRespawnRegistry.Clear();
            try
            {
                DimensionRespawnRegistry.Register(new DimensionRespawnRegistry.RespawnRule
                {
                    RuleName = "mod:gloomling:respawn",
                    CreatureObjectName = "mod:gloomling",
                    TileType = TileType.ground,
                    Chance = 0.1f
                });
                DimensionRespawnRegistry.Register(new DimensionRespawnRegistry.RespawnRule
                {
                    RuleName = "mod:gloomling:respawn",
                    CreatureObjectName = "mod:gloomling",
                    TileType = TileType.ground,
                    Chance = 0.2f
                });

                Assert.AreEqual(1, DimensionRespawnRegistry.PendingCount);
            }
            finally
            {
                DimensionRespawnRegistry.Clear();
            }
        }
    }
}
