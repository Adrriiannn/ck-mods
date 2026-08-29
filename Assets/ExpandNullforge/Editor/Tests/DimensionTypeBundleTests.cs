#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The dimension type bundles' contract: migration never loses a world, World is today's
    /// behavior under its honest name, and the old self-exemption footgun stays dead.
    /// </summary>
    internal sealed class DimensionTypeBundleTests
    {
        [TestCase(0, DimensionType.World)]  // legacy Overworld record
        [TestCase(1, DimensionType.World)]  // legacy PocketWorld — every real record
        [TestCase(2, DimensionType.Dungeon)]
        [TestCase(3, DimensionType.Arena)]
        [TestCase(4, DimensionType.Room)]
        [TestCase(5, DimensionType.World)]  // retired RuntimeOnly label
        [TestCase(-7, DimensionType.World)]
        [TestCase(9000, DimensionType.World)]
        public void EveryRawPersistedValueNormalizesToARealType(int raw, DimensionType expected)
        {
            Assert.That(
                DimensionTypeMigration.Normalize(raw),
                Is.EqualTo(expected),
                "A raw registry int must always land on a type with a policy — an old world " +
                "that loads with no bundle behavior is the failure this guards.");
        }

        [Test]
        public void WorldPolicyIsTodaysBehaviorAllOff()
        {
            DimensionTypePolicy world = DimensionTypePolicy.For(DimensionType.World);
            Assert.That(world.BlocksAmbientSpawns, Is.False);
            Assert.That(world.HasMusicOverride, Is.False);
            Assert.That(world.ResetsWhenEmpty, Is.False);
            Assert.That(world.ReturnPortalArmedByVictory, Is.False);
            Assert.That(world.RequiresReturnPortal, Is.False);
            Assert.That(world.WarnOnProceduralContent, Is.False);
        }

        [Test]
        public void EachTypeCarriesItsPromisedBundle()
        {
            DimensionTypePolicy dungeon = DimensionTypePolicy.For(DimensionType.Dungeon);
            Assert.That(dungeon.BlocksAmbientSpawns, Is.True);
            Assert.That(dungeon.HasMusicOverride, Is.True);
            Assert.That(dungeon.RequiresReturnPortal, Is.True);
            Assert.That(dungeon.ResetsWhenEmpty, Is.False, "A dungeon keeps its looted state.");

            DimensionTypePolicy arena = DimensionTypePolicy.For(DimensionType.Arena);
            Assert.That(arena.BlocksAmbientSpawns, Is.True);
            Assert.That(arena.ResetsWhenEmpty, Is.True);
            Assert.That(arena.ReturnPortalArmedByVictory, Is.True);
            Assert.That(arena.RequiresReturnPortal, Is.True);

            DimensionTypePolicy room = DimensionTypePolicy.For(DimensionType.Room);
            Assert.That(room.BlocksAmbientSpawns, Is.True);
            Assert.That(room.ResetsWhenEmpty, Is.False, "A room keeps what players did to it.");
            Assert.That(room.RequiresReturnPortal, Is.False, "A room may be one-way on purpose.");
        }

        [Test]
        public void ADimensionCanNoLongerExemptItselfFromOverlapConflicts()
        {
            // The retired footgun: claiming the Overworld SPACE KIND used to skip a dimension
            // in conflict checking, so later allocations could stack on top of it. The check
            // is by ID now, and no custom dimension can wear the Overworld's id.
            DimensionDefinition squatter = new DimensionDefinition(
                "mod.squatter",
                "Squatter",
                new int2(0, 5000),
                new DimensionBounds(new int2(-2048, -2048), new int2(2048, 2048)),
                1,
                DimensionType.World,
                DimensionCapabilityFlags.LocalCoordinates,
                DimensionLifecycleState.Registered);

            DimensionSlotAllocationResult result = DimensionSlotAllocator.Allocate(
                AutoRequest("mod.newcomer"),
                new[] { squatter });

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(
                result.AbsoluteOrigin,
                Is.Not.EqualTo(new int2(0, 5000)),
                "The newcomer was allocated on top of an existing dimension — the overlap " +
                "exemption is back.");
        }

        [Test]
        public void TheRealOverworldIsStillExemptByItsId()
        {
            // The Overworld genuinely spans everything; conflicting with it would make every
            // allocation impossible. Its exemption keys off the one id nothing else can have.
            DimensionDefinition overworld = new DimensionDefinition(
                DimensionIds.Overworld,
                "Overworld",
                int2.zero,
                new DimensionBounds(
                    new int2(-1000000000, -1000000000),
                    new int2(1000000000, 1000000000)),
                1,
                DimensionType.World,
                DimensionCapabilityFlags.LocalCoordinates,
                DimensionLifecycleState.Registered);

            DimensionSlotAllocationResult result = DimensionSlotAllocator.Allocate(
                AutoRequest("mod.first"),
                new[] { overworld });

            Assert.That(
                result.Accepted,
                Is.True,
                "Allocation must succeed inside the Overworld's endless bounds: " + result.Message);
        }

        private static DimensionSlotAllocationRequest AutoRequest(string id)
        {
            return new DimensionSlotAllocationRequest(
                id,
                new DimensionBounds(new int2(-2048, -2048), new int2(2048, 2048)),
                false,
                int2.zero,
                0,
                0,
                0,
                64);
        }
    }
}
#endif
