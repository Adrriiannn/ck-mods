#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Generation;
using ExpandNullforge.Scenes;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The scene placement pass's contract, at the seams that decide whether authored
    /// placement policy is real: the pool that carries it to runtime, and the provider
    /// gating that decides which dimensions get a scenes pass at all.
    /// </summary>
    /// <remarks>
    /// This coverage exists because the policy is easy to author and then throw away —
    /// mode, radial band, weight, unique and required reaching nothing at runtime, and
    /// <c>DimensionScenePlacement.TryPlace</c> with zero callers. These tests hold both halves
    /// down: the pool must carry every field, and the provider must be reachable from
    /// the generation ladder's own selection rules.
    /// </remarks>
    internal sealed class DimensionScenePlacementPassTests
    {
        private const string Dim = "test.scene-pass";

        [SetUp]
        public void ResetRegistry()
        {
            DimensionScenePoolRegistry.Clear();
        }

        [TearDown]
        public void ClearRegistry()
        {
            DimensionScenePoolRegistry.Clear();
        }

        private static DimensionScenePoolEntry Entry(
            string sceneId = "ruin",
            int weight = 1,
            bool unique = true,
            bool required = false,
            DimensionScenePlacementMode mode = DimensionScenePlacementMode.Automatic)
        {
            return new DimensionScenePoolEntry(
                "mod:" + sceneId,
                sceneId,
                "meadow",
                new[] { "swamp" },
                mode,
                new int2(4, 5),
                new DimensionBounds(new int2(-8, -8), new int2(8, 8)),
                mode == DimensionScenePlacementMode.PreferredBounds,
                10,
                40,
                string.Empty,
                new int2(6, 6),
                weight,
                unique,
                required,
                0);
        }

        private static DimensionDefinition Definition(
            string id = Dim,
            DimensionCapabilityFlags extra = DimensionCapabilityFlags.Generation)
        {
            return new DimensionDefinition(
                id,
                "Scene Pass Test",
                new int2(200000, 200000),
                new DimensionBounds(new int2(-64, -64), new int2(64, 64)),
                1,
                DimensionType.World,
                DimensionCapabilityFlags.LocalCoordinates | extra,
                DimensionLifecycleState.Registered);
        }

        [Test]
        public void ThePoolCarriesEveryAuthoredPolicyField()
        {
            DimensionScenePoolRegistry.Register(Dim, Entry("ruin", 7, false, true));
            var pool = DimensionScenePoolRegistry.For(Dim);
            Assert.That(pool.Count, Is.EqualTo(1));

            DimensionScenePoolEntry entry = pool[0];
            Assert.That(entry.RegisteredSceneName, Is.EqualTo("mod:ruin"));
            Assert.That(entry.SceneId, Is.EqualTo("ruin"));
            Assert.That(entry.BiomeId, Is.EqualTo("meadow"));
            Assert.That(entry.AllowedBiomeIds, Is.EqualTo(new[] { "swamp" }));
            Assert.That(entry.ExactLocalPosition, Is.EqualTo(new int2(4, 5)));
            Assert.That(entry.MinRadiusTiles, Is.EqualTo(10));
            Assert.That(entry.MaxRadiusTiles, Is.EqualTo(40));
            Assert.That(entry.FootprintSize, Is.EqualTo(new int2(6, 6)));
            Assert.That(entry.Weight, Is.EqualTo(7));
            Assert.That(entry.Unique, Is.False);
            Assert.That(entry.Required, Is.True);
        }

        [Test]
        public void ReRegisteringASceneReplacesItsEntryInsteadOfDuplicating()
        {
            DimensionScenePoolRegistry.Register(Dim, Entry("ruin", 1));
            DimensionScenePoolRegistry.Register(Dim, Entry("ruin", 9));

            var pool = DimensionScenePoolRegistry.For(Dim);
            Assert.That(pool.Count, Is.EqualTo(1), "Content reload feeds the same ids again.");
            Assert.That(pool[0].Weight, Is.EqualTo(9));
        }

        [Test]
        public void TheEntryClampsWhatWouldOtherwiseBreakTheRoulette()
        {
            DimensionScenePoolEntry entry = new DimensionScenePoolEntry(
                "mod:x", "x", "", null,
                DimensionScenePlacementMode.Automatic,
                default, default, false,
                // Inverted band, zero weight, zero footprint: every one of these would
                // divide-by-zero or dead-loop the search if it survived.
                50, 10, "", new int2(0, 0), 0, false, false, 0);

            Assert.That(entry.Weight, Is.GreaterThanOrEqualTo(1));
            Assert.That(entry.MaxRadiusTiles, Is.GreaterThanOrEqualTo(entry.MinRadiusTiles));
            Assert.That(entry.FootprintSize.x, Is.GreaterThanOrEqualTo(1));
            Assert.That(entry.FootprintSize.y, Is.GreaterThanOrEqualTo(1));
            Assert.That(entry.AllowedBiomeIds, Is.Not.Null);
        }

        [Test]
        public void ANamelessEntryIsRefusedBecauseNothingCouldEverStampIt()
        {
            DimensionScenePoolRegistry.Register(Dim, new DimensionScenePoolEntry(
                "", "orphan", "", null,
                DimensionScenePlacementMode.Automatic,
                default, default, false, 0, 0, "", new int2(1, 1), 1, true, false, 0));

            Assert.That(DimensionScenePoolRegistry.Has(Dim), Is.False);
        }

        [Test]
        public void TheProviderOnlyClaimsDimensionsWithSceneWork()
        {
            var provider = new DimensionScenePlacementPassProvider(new NullforgeDimensionService());
            DimensionBounds bounds = new DimensionBounds(new int2(-64, -64), new int2(64, 64));

            Assert.That(
                provider.CanGenerate(Definition(), bounds),
                Is.False,
                "No pool, no planned scenes: the plan synthesizer must not append a scenes pass.");

            DimensionScenePoolRegistry.Register(Dim, Entry());
            Assert.That(provider.CanGenerate(Definition(), bounds), Is.True);
        }

        [Test]
        public void TheProviderNeverClaimsTheOverworldOrAGenerationlessDimension()
        {
            var provider = new DimensionScenePlacementPassProvider(new NullforgeDimensionService());
            DimensionBounds bounds = new DimensionBounds(new int2(-64, -64), new int2(64, 64));

            DimensionScenePoolRegistry.Register(DimensionIds.Overworld, Entry());
            DimensionScenePoolRegistry.Register(Dim, Entry());

            Assert.That(
                provider.CanGenerate(Definition(DimensionIds.Overworld), bounds),
                Is.False,
                "The Overworld belongs to the game's own placer.");
            Assert.That(
                provider.CanGenerate(Definition(Dim, DimensionCapabilityFlags.None), bounds),
                Is.False,
                "A dimension without the Generation capability never generates anything.");
        }

        [Test]
        public void TheProviderAnswersToTheLadderUnderItsRegisteredId()
        {
            var provider = new DimensionScenePlacementPassProvider(new NullforgeDimensionService());

            Assert.That(
                provider.ProviderId,
                Is.EqualTo(DimensionGenerationProviderIds.ScenePlacement),
                "The plan synthesizer looks the provider up by this id; a drift here would " +
                "silently drop the auto-scenes pass.");
            Assert.That(
                provider,
                Is.InstanceOf<IDimensionGenerationPassProvider>(),
                "TryPreparePlannedGeneration drops any pass whose provider is not a pass " +
                "provider — silently.");
        }

        [Test]
        public void WithoutAServerWorldTheProviderReportsProgressNotFailure()
        {
            var provider = new DimensionScenePlacementPassProvider(new NullforgeDimensionService());
            DimensionScenePoolRegistry.Register(Dim, Entry());
            DimensionDefinition definition = Definition();

            DimensionGenerationContext context = new DimensionGenerationContext(
                null,
                definition,
                new DimensionArea(definition.Id, definition.LocalBounds, definition.AbsoluteBounds),
                default,
                0.0d);

            DimensionGenerationProviderResult result = provider.TickGeneration(context);
            Assert.That(
                result.State,
                Is.EqualTo(DimensionGenerationState.StampingScenes),
                "A world that has not arrived yet is a wait, never a verdict.");
            Assert.That(result.Progress01, Is.LessThan(1f));
        }
    }
}
#endif
