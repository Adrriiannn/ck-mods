#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Determinism and collision coverage for the radial dimension-slot allocator: the first
    /// slot is (0, 5000) (due north, since +Y is north), candidates walk the fixed 8-direction
    /// ring order, occupied or overworld-conflicting slots are skipped to the next, and the
    /// result is stable for identical inputs. Also covers the two lifecycle rules — a resize is
    /// allowed but watched for collisions, and removing a dimension frees its slot for reuse.
    /// This is pure logic — no world/service is required.
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

        [Test]
        public void Resize_GrowingIntoFreeSpaceIsAccepted()
        {
            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(int2.zero, new int2(400, 400)),
                0,
                null,
                null);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.DiscardsContent, Is.False);
            Assert.That(result.RequiresRelocation, Is.False);
        }

        [Test]
        public void Resize_GrowingIntoANeighbourIsBlockedAndNamesIt()
        {
            // Neighbour sits due north-east; growing 5000 tiles wide reaches it.
            DimensionSlotRecord neighbour = Occupy("dim-neighbour", new int2(5000, 5000));

            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(int2.zero, new int2(6000, 200)),
                0,
                null,
                new[] { neighbour });

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo("resize-conflict"));
            Assert.That(result.ConflictingDimensionId, Is.EqualTo("dim-neighbour"));
            Assert.That(result.RequiresRelocation, Is.True, "The creator should be offered a move.");
        }

        [Test]
        public void Resize_GrowingIntoTheProtectedOverworldBandIsBlocked()
        {
            // Origin sits just past the protected band; growing southwards re-enters it.
            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(new int2(0, -4000), new int2(200, 200)),
                0,
                null,
                null);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo("resize-overworld-protected"));
            Assert.That(result.ConflictingDimensionId, Is.Empty);
        }

        [Test]
        public void Resize_ShrinkingIsAllowedButFlagsLostTiles()
        {
            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(int2.zero, new int2(100, 100)),
                0,
                null,
                null);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.DiscardsContent, Is.True);
            Assert.That(result.Code, Is.EqualTo("resize-shrink-discards"));
        }

        [Test]
        public void Resize_ToTheSameBoundsIsANoOp()
        {
            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a", new int2(0, 5000), SmallBounds, SmallBounds, 0, null, null);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Code, Is.EqualTo("resize-noop"));
            Assert.That(result.DiscardsContent, Is.False);
        }

        [Test]
        public void Resize_IgnoresTheDimensionsOwnCurrentSlot()
        {
            // The dimension's own record must not count as a collision with itself.
            DimensionSlotRecord self = Occupy("dim-a", new int2(0, 5000));

            DimensionSlotResizeResult result = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(int2.zero, new int2(400, 400)),
                0,
                null,
                new[] { self });

            Assert.That(result.Accepted, Is.True, result.Message);
        }

        [Test]
        public void Resize_RejectsDegenerateBoundsAndTheOverworld()
        {
            DimensionSlotResizeResult degenerate = DimensionSlotAllocator.ValidateResize(
                "dim-a",
                new int2(0, 5000),
                SmallBounds,
                new DimensionBounds(int2.zero, int2.zero),
                0,
                null,
                null);
            Assert.That(degenerate.Accepted, Is.False);
            Assert.That(degenerate.Code, Is.EqualTo("local-bounds-invalid"));

            DimensionSlotResizeResult overworld = DimensionSlotAllocator.ValidateResize(
                DimensionIds.Overworld, int2.zero, SmallBounds, SmallBounds, 0, null, null);
            Assert.That(overworld.Accepted, Is.False);
            Assert.That(overworld.Code, Is.EqualTo("overworld-slot-not-resizable"));
        }

        [Test]
        public void Prune_FreesSlotsWhoseDimensionIsGone()
        {
            DimensionSlotRecord live = Occupy("dim-live", new int2(0, 5000));
            DimensionSlotRecord removed = Occupy("dim-removed", new int2(5000, 5000));

            System.Collections.Generic.List<DimensionSlotRecord> retained =
                new System.Collections.Generic.List<DimensionSlotRecord>();
            System.Collections.Generic.List<DimensionSlotRecord> freed =
                new System.Collections.Generic.List<DimensionSlotRecord>();

            int freedCount = DimensionSlotAllocator.PruneOrphanedSlots(
                new[] { live, removed },
                new[] { "dim-live" },
                retained,
                freed);

            Assert.That(freedCount, Is.EqualTo(1));
            Assert.That(retained.Count, Is.EqualTo(1));
            Assert.That(retained[0].DimensionId, Is.EqualTo("dim-live"));
            Assert.That(freed[0].DimensionId, Is.EqualTo("dim-removed"));
        }

        [Test]
        public void Prune_ReleasesTheCoordinateSpaceForReuse()
        {
            // A removed dimension held the north slot. After pruning, the next allocation must be
            // able to take north again rather than being pushed further out forever.
            DimensionSlotRecord removed = Occupy("dim-removed", new int2(0, 5000));
            System.Collections.Generic.List<DimensionSlotRecord> retained =
                new System.Collections.Generic.List<DimensionSlotRecord>();

            DimensionSlotAllocator.PruneOrphanedSlots(
                new[] { removed }, new string[0], retained, null);

            DimensionSlotAllocationResult result =
                DimensionSlotAllocator.Allocate(AutoRequest("dim-new"), null, retained);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(result.AbsoluteOrigin, Is.EqualTo(new int2(0, 5000)));
        }

        [Test]
        public void Prune_DropsBlankAndDuplicateRecords()
        {
            DimensionSlotRecord blank = new DimensionSlotRecord(
                string.Empty, SmallBounds, new int2(0, 5000), 0, false, 0L, "ok", string.Empty);
            DimensionSlotRecord first = Occupy("dim-live", new int2(5000, 5000));
            DimensionSlotRecord duplicate = Occupy("dim-live", new int2(5000, 0));

            System.Collections.Generic.List<DimensionSlotRecord> retained =
                new System.Collections.Generic.List<DimensionSlotRecord>();
            System.Collections.Generic.List<DimensionSlotRecord> freed =
                new System.Collections.Generic.List<DimensionSlotRecord>();

            int freedCount = DimensionSlotAllocator.PruneOrphanedSlots(
                new[] { blank, first, duplicate },
                new[] { "dim-live" },
                retained,
                freed);

            Assert.That(freedCount, Is.EqualTo(2), "Blank and duplicate records must not leak space.");
            Assert.That(retained.Count, Is.EqualTo(1));
            Assert.That(retained[0].AbsoluteOrigin, Is.EqualTo(new int2(5000, 5000)));
        }

        [Test]
        public void Prune_HandlesNullInputsWithoutThrowing()
        {
            Assert.That(
                DimensionSlotAllocator.PruneOrphanedSlots(null, null, null, null),
                Is.EqualTo(0));
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
