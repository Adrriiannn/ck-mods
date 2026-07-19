#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Determinism and collision coverage for the radial dimension-slot allocator: the first
    /// slot is (5000, 0) (north), candidates walk the fixed 8-direction ring order, occupied
    /// or overworld-conflicting slots are skipped to the next, and the result is stable for
    /// identical inputs. This is pure logic — no world/service is required.
    /// </summary>
    internal sealed class DimensionSlotAllocatorTests
    {
        private static readonly DimensionBounds SmallBounds =
            new DimensionBounds(int2.zero, new int2(200, 200));

        [Test]
        public void Allocate_FirstAutomaticSlotIsDueNorthAtFiveThousand()
        {
            // Core Keeper axes: +Y is north, so due north is (0, 5000).
            DimensionSlotAllocationResult result =
                DimensionSlotAllocator.Allocate(AutoRequest("dim-a"), null, null);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.AbsoluteOrigin, Is.EqualTo(new int2(0, 5000)));
            Assert.That(result.CandidateIndex, Is.EqualTo(0));
            Assert.That(result.UsedFixedOrigin, Is.False);
        }

        [Test]
        public void Allocate_SkipsOccupiedSlotsInRadialOrder()
        {
            DimensionSlotRecord north = Occupy("dim-north", new int2(0, 5000));
            DimensionSlotRecord northEast = Occupy("dim-ne", new int2(5000, 5000));

            // North taken -> next candidate is north-east (5000, 5000).
            DimensionSlotAllocationResult afterNorth = DimensionSlotAllocator.Allocate(
                AutoRequest("dim-b"),
                null,
                new[] { north });
            Assert.That(afterNorth.AbsoluteOrigin, Is.EqualTo(new int2(5000, 5000)), afterNorth.Message);

            // North and north-east taken -> next candidate is east (5000, 0).
            DimensionSlotAllocationResult afterNorthEast = DimensionSlotAllocator.Allocate(
                AutoRequest("dim-c"),
                null,
                new[] { north, northEast });
            Assert.That(afterNorthEast.AbsoluteOrigin, Is.EqualTo(new int2(5000, 0)), afterNorthEast.Message);
        }

        [Test]
        public void Allocate_IsDeterministicForIdenticalInputs()
        {
            DimensionSlotRecord north = Occupy("dim-north", new int2(0, 5000));
            DimensionSlotAllocationResult first = DimensionSlotAllocator.Allocate(
                AutoRequest("dim-b"), null, new[] { north });
            DimensionSlotAllocationResult second = DimensionSlotAllocator.Allocate(
                AutoRequest("dim-b"), null, new[] { north });

            Assert.That(second.AbsoluteOrigin, Is.EqualTo(first.AbsoluteOrigin));
            Assert.That(second.CandidateIndex, Is.EqualTo(first.CandidateIndex));
        }

        [Test]
        public void Allocate_RejectsFixedOriginInsideTheProtectedOverworld()
        {
            DimensionSlotAllocationRequest request = new DimensionSlotAllocationRequest(
                "dim-fixed",
                SmallBounds,
                true,
                int2.zero,
                0,
                0,
                0,
                64);
            DimensionSlotAllocationResult result =
                DimensionSlotAllocator.Allocate(request, null, null);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo("fixed-slot-conflict"));
        }

        [Test]
        public void Allocate_AcceptsAClearFixedOrigin()
        {
            DimensionSlotAllocationRequest request = new DimensionSlotAllocationRequest(
                "dim-fixed",
                SmallBounds,
                true,
                new int2(20000, 20000),
                0,
                0,
                0,
                64);
            DimensionSlotAllocationResult result =
                DimensionSlotAllocator.Allocate(request, null, null);
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.AbsoluteOrigin, Is.EqualTo(new int2(20000, 20000)));
            Assert.That(result.UsedFixedOrigin, Is.True);
        }

        [Test]
        public void Allocate_LargeDimensionSkipsSlotsThatWouldCollide()
        {
            // A 6000-wide playable area is larger than the 5000 ring spacing, so it cannot sit
            // in a ring slot adjacent to an existing same-size dimension. The allocator must
            // find a slot whose bounds do not overlap the occupant (your "next available slot"
            // rule when spacing is insufficient).
            DimensionBounds bigBounds = new DimensionBounds(int2.zero, new int2(6000, 6000));
            DimensionSlotRecord bigNorth = new DimensionSlotRecord(
                "dim-big-north", bigBounds, new int2(0, 5000), 0, false, 0L, "ok", string.Empty);

            DimensionSlotAllocationRequest request = new DimensionSlotAllocationRequest(
                "dim-big", bigBounds, false, int2.zero, 0, 0, 0, 64);
            DimensionSlotAllocationResult result =
                DimensionSlotAllocator.Allocate(request, null, new[] { bigNorth });

            Assert.That(result.Accepted, Is.True, result.Message);
            DimensionBounds placed = new DimensionBounds(
                result.AbsoluteOrigin + bigBounds.Min,
                result.AbsoluteOrigin + bigBounds.MaxExclusive);
            Assert.That(
                Overlaps(placed, bigNorth.AbsoluteBounds),
                Is.False,
                "Large dimension was placed overlapping the existing one.");
        }

        private static bool Overlaps(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x < b.MaxExclusive.x && a.MaxExclusive.x > b.Min.x &&
                   a.Min.y < b.MaxExclusive.y && a.MaxExclusive.y > b.Min.y;
        }

        private static DimensionSlotAllocationRequest AutoRequest(string id)
        {
            // Zero offset/step falls back to the 5000 radial defaults.
            return new DimensionSlotAllocationRequest(
                id, SmallBounds, false, int2.zero, 0, 0, 0, 64);
        }

        private static DimensionSlotRecord Occupy(string id, int2 origin)
        {
            return new DimensionSlotRecord(id, SmallBounds, origin, 0, false, 0L, "ok", string.Empty);
        }
    }
}
#endif
