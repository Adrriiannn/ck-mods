using ExpandNullforge.Tilesets;
using NUnit.Framework;
using PugTilemap;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Guards the translation between our tileset ids and Core Keeper's own behaviours.
    /// </summary>
    /// <remarks>
    /// The registry sits underneath a Harmony patch that writes into the player's state, so a mistake
    /// here does not fail loudly — it makes ordinary ground behave like acid, or makes a vanilla
    /// biome start acting like a mod's. Both are worth a test.
    /// </remarks>
    public sealed class DimensionTilesetBehaviourTests
    {
        private static int CustomId(string name)
        {
            return DimensionTilesetRegistry.ComputeTilesetId(name);
        }

        [SetUp]
        [TearDown]
        public void Reset()
        {
            DimensionTilesetBehaviourRegistry.Clear();
        }

        [Test]
        public void ARegisteredBlockReportsTheBehaviourItAskedFor()
        {
            int id = CustomId("MyMod:tar");
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.Oil);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Oil,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(id));
        }

        [Test]
        public void AnUnregisteredBlockBehavesLikeNothing()
        {
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(CustomId("MyMod:plain")));
        }

        [Test]
        public void VanillaTilesetsAreNeverAnsweredFor()
        {
            // The guard that stops us changing how the base game behaves on its own ground: even an
            // explicit attempt to register a vanilla id must not take.
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(
                (int)Tileset.Dirt, DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour((int)Tileset.Dirt),
                "Registering a vanilla tileset would change the base game's own ground, which is " +
                "never what a dimension mod should do.");
            Assert.IsFalse(DimensionTilesetBehaviourRegistry.HasAny);
        }

        [Test]
        public void RegisteringNoneRemovesTheEntryRatherThanStoringIt()
        {
            int id = CustomId("MyMod:tar");
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.Acid);
            Assert.IsTrue(DimensionTilesetBehaviourRegistry.HasAny);

            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.None);

            Assert.IsFalse(
                DimensionTilesetBehaviourRegistry.HasAny,
                "A block with no hazard should leave the table empty so the per-frame lookup can be " +
                "skipped entirely.");
        }

        [Test]
        public void ABehaviourRegisteredForSlimeStaysOnTheSlime()
        {
            int id = CustomId("MyMod:tar");
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Acid,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.groundSlime, id));
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.ground, id),
                "The block's ordinary ground must stay walkable and harmless even when its slime burns.");
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.wall, id));
        }

        [Test]
        public void ReRegisteringReplacesRatherThanStacks()
        {
            int id = CustomId("MyMod:tar");
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.Acid);
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(id, DimensionTilesetGroundBehaviour.Oil);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Oil,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(id),
                "A reload re-registers everything; it is not a request for two behaviours at once.");
        }

        [Test]
        public void TwoBlocksKeepTheirOwnBehaviours()
        {
            int acid = CustomId("MyMod:acidpool");
            int oil = CustomId("MyMod:tar");
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(acid, DimensionTilesetGroundBehaviour.Acid);
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(oil, DimensionTilesetGroundBehaviour.Oil);

            Assert.AreEqual(DimensionTilesetGroundBehaviour.Acid, DimensionTilesetBehaviourRegistry.GetGroundBehaviour(acid));
            Assert.AreEqual(DimensionTilesetGroundBehaviour.Oil, DimensionTilesetBehaviourRegistry.GetGroundBehaviour(oil));
        }

        [Test]
        public void EveryBehaviourMapsToTheVanillaTilesetThatCarriesIt()
        {
            // The inverse mapping is what lets us hand a vanilla id to a system we cannot reach
            // directly, so every real behaviour must resolve to one.
            Tileset tileset;
            Assert.IsTrue(DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.Slime, out tileset));
            Assert.AreEqual(Tileset.Dirt, tileset);

            Assert.IsTrue(DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.Acid, out tileset));
            Assert.AreEqual(Tileset.LarvaHive, tileset);

            Assert.IsTrue(DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.PoisonSlime, out tileset));
            Assert.AreEqual(Tileset.Nature, tileset);

            Assert.IsTrue(DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.SlipperySlime, out tileset));
            Assert.AreEqual(Tileset.Sea, tileset);

            Assert.IsTrue(DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.Oil, out tileset));
            Assert.AreEqual(Tileset.Excavation, tileset);
        }

        [Test]
        public void NoneResolvesToNoVanillaTileset()
        {
            Tileset tileset;
            Assert.IsFalse(
                DimensionTilesetBehaviourRegistry.TryGetVanillaTilesetFor(DimensionTilesetGroundBehaviour.None, out tileset),
                "There is no vanilla tileset meaning 'does nothing'; callers must handle the false.");
        }

        // ---- ground that is itself the hazard, the way mold is ----

        [Test]
        public void GroundRegisteredAsHazardousAffectsWhoeverWalksOnIt()
        {
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Acid,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.ground, CustomId("MyMod:mold")));
        }

        [Test]
        public void ABlockCanHaveSafeGroundAndDangerousSlimeAtOnce()
        {
            // The common shape by far: a puddle of something nasty on an ordinary floor. Folding the
            // two settings together would make this impossible to express.
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Acid,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.groundSlime, CustomId("MyMod:mold")));
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.ground, CustomId("MyMod:mold")),
                "Slime being dangerous must not make the floor under it dangerous.");
        }

        [Test]
        public void HazardousGroundAndHazardousSlimeCanDifferOnTheSameBlock()
        {
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.PoisonSlime);
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.PoisonSlime,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.ground, CustomId("MyMod:mold")));
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.Acid,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.groundSlime, CustomId("MyMod:mold")));
        }

        [Test]
        public void HazardousGroundDoesNotSpreadToTheBlocksOtherSurfaces()
        {
            // Tilled soil, water and dug-up dirt are different surfaces. A hazard authored for the
            // untouched ground must not follow a player onto a field they just hoed.
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.dugUpGround, CustomId("MyMod:mold")));
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.water, CustomId("MyMod:mold")));
            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.wall, CustomId("MyMod:mold")));
        }

        [Test]
        public void VanillaGroundIsNeverAnsweredForOnTheSurfaceAxisEither()
        {
            // The whole registry exists to add behaviour to ids the game has never heard of. It must
            // never change what the base game does on its own ground.
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                (int)Tileset.Dirt, DimensionTilesetGroundBehaviour.Acid);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetGroundBehaviour(TileType.ground, (int)Tileset.Dirt));
        }

        [Test]
        public void RegisteringNoneClearsHazardousGroundRatherThanStoringADoesNothingRow()
        {
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.None);

            Assert.AreEqual(
                DimensionTilesetGroundBehaviour.None,
                DimensionTilesetBehaviourRegistry.GetSurfaceBehaviour(CustomId("MyMod:mold")));
            Assert.IsFalse(
                DimensionTilesetBehaviourRegistry.HasAny,
                "With nothing registered the hazard systems must skip entirely, so a mod that ships " +
                "no hazard costs nothing at all.");
        }

        [Test]
        public void EitherAxisAloneIsEnoughToWakeTheHazardSystems()
        {
            Assert.IsFalse(DimensionTilesetBehaviourRegistry.HasAny);

            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);
            Assert.IsTrue(DimensionTilesetBehaviourRegistry.HasAny);

            DimensionTilesetBehaviourRegistry.Clear();
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(
                CustomId("MyMod:mold"), DimensionTilesetGroundBehaviour.Acid);
            Assert.IsTrue(DimensionTilesetBehaviourRegistry.HasAny);
        }
    }
}
