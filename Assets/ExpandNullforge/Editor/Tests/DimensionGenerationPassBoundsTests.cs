#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Generation;
using ExpandNullforge.Scenes;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// That a generation step scoped to a rectangle actually runs over that rectangle, in every
    /// provider that claims to run steps.
    /// </summary>
    /// <remarks>
    /// This coverage exists because three of the five providers took the pass context and then
    /// read the whole area out of it: scoping a step to one corner was authored, emitted,
    /// delivered to the provider, and dropped on the floor with nothing logged. The tests pin
    /// each provider's own resolution — the method its TickGenerationPass calls — rather than a
    /// copy of the arithmetic, so a provider that stops honouring bounds fails here.
    /// </remarks>
    internal sealed class DimensionGenerationPassBoundsTests
    {
        private const string Dim = "test.pass-bounds";

        /// <summary>Local (0,0) sits at world (1000,2000), so the two spaces cannot be confused.</summary>
        private static DimensionArea Area()
        {
            return new DimensionArea(
                Dim,
                new DimensionBounds(new int2(-100, -100), new int2(100, 100)),
                new DimensionBounds(new int2(900, 1900), new int2(1100, 2100)));
        }

        private static DimensionDefinition Definition()
        {
            return new DimensionDefinition(
                Dim,
                "Pass Bounds Test",
                new int2(1000, 2000),
                new DimensionBounds(new int2(-100, -100), new int2(100, 100)),
                1,
                DimensionType.World,
                DimensionCapabilityFlags.LocalCoordinates | DimensionCapabilityFlags.Generation,
                DimensionLifecycleState.Registered);
        }

        private static DimensionGenerationPassContext Context(
            bool scoped,
            DimensionBounds scope)
        {
            DimensionArea area = Area();
            DimensionGenerationContext generation = new DimensionGenerationContext(
                null,
                Definition(),
                area,
                new DimensionGenerationStatus(
                    Dim, area.LocalBounds, DimensionGenerationState.Populating, 0f, string.Empty),
                0d);

            DimensionGenerationPassDefinition pass = new DimensionGenerationPassDefinition(
                "step",
                "Step",
                Dim,
                string.Empty,
                scoped,
                scope,
                DimensionGenerationPassPhase.Terrain,
                0,
                "expandnullforge:tile-map",
                true);

            return new DimensionGenerationPassContext(generation, pass, 0, 1);
        }

        private static DimensionGenerationPassContext Unscoped()
        {
            return Context(false, default(DimensionBounds));
        }

        /// <summary>A corner of the area, well inside it on both axes.</summary>
        private static DimensionBounds Corner()
        {
            return new DimensionBounds(new int2(-80, -80), new int2(-20, -20));
        }

        // ------------------------------------------------------------ the shared resolution ---

        [Test]
        public void AStepWithNoRectangleCoversTheWholeArea()
        {
            DimensionBounds resolved = DimensionGenerationPassBounds.Resolve(Unscoped());

            Assert.AreEqual(new int2(-100, -100), resolved.Min);
            Assert.AreEqual(new int2(100, 100), resolved.MaxExclusive);
        }

        [Test]
        public void AStepsRectangleIsClippedToTheAreaBeingGenerated()
        {
            DimensionBounds resolved = DimensionGenerationPassBounds.Resolve(
                Context(true, new DimensionBounds(new int2(-500, -500), new int2(0, 0))));

            Assert.AreEqual(new int2(-100, -100), resolved.Min, "clipped to the area's corner");
            Assert.AreEqual(new int2(0, 0), resolved.MaxExclusive, "the authored edge survives");
        }

        [Test]
        public void ARectangleThatMissesTheAreaHasNoTiles()
        {
            DimensionBounds resolved = DimensionGenerationPassBounds.Resolve(
                Context(true, new DimensionBounds(new int2(500, 500), new int2(600, 600))));

            Assert.IsFalse(DimensionGenerationPassBounds.HasArea(resolved));
        }

        [Test]
        public void ALocalRectangleConvertsByTheAreasOwnOffset()
        {
            DimensionBounds absolute =
                DimensionGenerationPassBounds.ToAbsolute(Area(), Corner());

            // Local (-80,-80) is 20 tiles in from the area's local corner (-100,-100), so it is
            // 20 tiles in from the world corner (900,1900) too.
            Assert.AreEqual(new int2(920, 1920), absolute.Min);
            Assert.AreEqual(new int2(980, 1980), absolute.MaxExclusive);
        }

        [Test]
        public void TwoRectanglesInOneAreaAreTwoDifferentJobKeys()
        {
            string left = DimensionGenerationPassBounds.Key(
                new DimensionBounds(new int2(-100, -100), new int2(0, 100)));
            string right = DimensionGenerationPassBounds.Key(
                new DimensionBounds(new int2(0, -100), new int2(100, 100)));

            Assert.AreNotEqual(left, right);
        }

        // ------------------------------------------------------------------ tile map paint ---

        [Test]
        public void ScopedTerrainPaintsOnlyItsRectangle()
        {
            DimensionBounds local;
            DimensionBounds absolute;
            bool resolved = DimensionTileMapGenerationProvider.TryResolvePaintBounds(
                Context(true, Corner()), out local, out absolute);

            Assert.IsTrue(resolved);
            Assert.AreEqual(Corner().Min, local.Min);
            Assert.AreEqual(Corner().MaxExclusive, local.MaxExclusive);
            // Both halves clipped together, or every painted tile slides by the width of the clip.
            Assert.AreEqual(new int2(920, 1920), absolute.Min);
            Assert.AreEqual(new int2(980, 1980), absolute.MaxExclusive);
        }

        [Test]
        public void UnscopedTerrainStillPaintsTheWholeArea()
        {
            DimensionBounds local;
            DimensionBounds absolute;
            Assert.IsTrue(DimensionTileMapGenerationProvider.TryResolvePaintBounds(
                Unscoped(), out local, out absolute));

            Assert.AreEqual(new int2(-100, -100), local.Min);
            Assert.AreEqual(new int2(900, 1900), absolute.Min);
            Assert.AreEqual(new int2(1100, 2100), absolute.MaxExclusive);
        }

        [Test]
        public void TerrainScopedAwayFromTheAreaIsDoneWithoutPainting()
        {
            DimensionGenerationPassContext context =
                Context(true, new DimensionBounds(new int2(500, 500), new int2(600, 600)));

            DimensionBounds local;
            DimensionBounds absolute;
            Assert.IsFalse(DimensionTileMapGenerationProvider.TryResolvePaintBounds(
                context, out local, out absolute));

            // The early Ready has to come before the world lookup, or a step over nothing would
            // wedge the ladder waiting for a world it never needs.
            DimensionGenerationProviderResult result =
                new DimensionTileMapGenerationProvider().TickGenerationPass(context);
            Assert.AreEqual(DimensionGenerationState.Ready, result.State);
        }

        // -------------------------------------------------------------------- ore scatter ---

        [Test]
        public void ScopedOreReadsWallsOnlyInsideItsRectangle()
        {
            DimensionBounds scan;
            Assert.IsTrue(DimensionOreScatterPassProvider.TryResolveScanBounds(
                Context(true, Corner()), out scan));

            // Walls come back in world coordinates, so the scan rectangle must be there too.
            Assert.AreEqual(new int2(920, 1920), scan.Min);
            Assert.AreEqual(new int2(980, 1980), scan.MaxExclusive);
        }

        [Test]
        public void UnscopedOreStillReadsTheWholeArea()
        {
            DimensionBounds scan;
            Assert.IsTrue(DimensionOreScatterPassProvider.TryResolveScanBounds(Unscoped(), out scan));

            Assert.AreEqual(new int2(900, 1900), scan.Min);
            Assert.AreEqual(new int2(1100, 2100), scan.MaxExclusive);
        }

        [Test]
        public void OreScopedAwayFromTheAreaIsDoneWithoutScanning()
        {
            DimensionGenerationPassContext context =
                Context(true, new DimensionBounds(new int2(500, 500), new int2(600, 600)));

            DimensionBounds scan;
            Assert.IsFalse(DimensionOreScatterPassProvider.TryResolveScanBounds(context, out scan));

            DimensionGenerationProviderResult result =
                new DimensionOreScatterPassProvider().TickGenerationPass(context);
            Assert.AreEqual(DimensionGenerationState.Ready, result.State);
        }

        // --------------------------------------------------------------- dungeon placement ---

        [Test]
        public void ScopedDungeonsGrowInsideTheirRectangle()
        {
            DimensionBounds scope = Corner();
            DimensionDungeonPlacementPassProvider provider =
                new DimensionDungeonPlacementPassProvider(null);

            DimensionBounds resolved;
            Assert.IsTrue(DimensionDungeonPlacementPassProvider.TryResolvePlacementBounds(
                Context(true, scope), out resolved));
            Assert.AreEqual(scope.Min, resolved.Min);
            Assert.AreEqual(scope.MaxExclusive, resolved.MaxExclusive);

            // Every copy has to land inside the rectangle, not merely the first: the spacing
            // score pushes later copies away from earlier ones, and the area is the obvious
            // place for them to escape to.
            System.Collections.Generic.List<DimensionDungeonPlacementPassProvider.PlacedSpot> spots =
                new System.Collections.Generic.List<DimensionDungeonPlacementPassProvider.PlacedSpot>();
            for (int copy = 0; copy < 3; copy++)
            {
                int2 spot;
                Assert.IsTrue(
                    provider.TryFindSpot(
                        Context(true, scope).GenerationContext,
                        resolved,
                        1234UL,
                        Dungeon(),
                        4,
                        spots,
                        copy,
                        out spot),
                    "copy " + copy + " found no spot inside its rectangle");
                Assert.IsTrue(
                    resolved.Contains(spot),
                    "copy " + copy + " landed at " + spot + ", outside " + resolved.Min + " to " +
                    resolved.MaxExclusive);
                spots.Add(new DimensionDungeonPlacementPassProvider.PlacedSpot
                {
                    Local = spot,
                    Radius = 4
                });
            }
        }

        [Test]
        public void UnscopedDungeonsUseTheWholeArea()
        {
            DimensionBounds resolved;
            Assert.IsTrue(DimensionDungeonPlacementPassProvider.TryResolvePlacementBounds(
                Unscoped(), out resolved));

            Assert.AreEqual(new int2(-100, -100), resolved.Min);
            Assert.AreEqual(new int2(100, 100), resolved.MaxExclusive);
        }

        [Test]
        public void DungeonsScopedAwayFromTheAreaAreDoneWithoutPlacing()
        {
            DimensionGenerationPassContext context =
                Context(true, new DimensionBounds(new int2(500, 500), new int2(600, 600)));

            DimensionBounds resolved;
            Assert.IsFalse(DimensionDungeonPlacementPassProvider.TryResolvePlacementBounds(
                context, out resolved));

            DimensionGenerationProviderResult result =
                new DimensionDungeonPlacementPassProvider(null).TickGenerationPass(context);
            Assert.AreEqual(DimensionGenerationState.Ready, result.State);
        }

        /// <summary>A dungeon with no biome binding, so the spot search needs no zone service.</summary>
        private static DimensionDungeonDefinition Dungeon()
        {
            return new DimensionDungeonDefinition(
                "dungeon",
                string.Empty,
                24,
                1f,
                0.5f,
                1f,
                0,
                true,
                new DimensionDungeonRoomGroup[0],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Dim);
        }
    }
}
#endif
