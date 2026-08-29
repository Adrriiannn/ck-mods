#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using NUnit.Framework;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pure-logic coverage for the coordinate value types the whole framework relies on:
    /// bounds containment (inclusive-min / exclusive-max), absolute/local round-trips, and
    /// the coordinate-domain derivation. These invariants have no runtime dependency, so they
    /// belong in the automated suite the handoff flags as missing.
    /// </summary>
    internal sealed class DimensionCoordinateDomainTests
    {
        [Test]
        public void Bounds_ContainmentIsInclusiveMinExclusiveMax()
        {
            DimensionBounds bounds = new DimensionBounds(new int2(2, -3), new int2(6, 4));

            Assert.That(bounds.Contains(new int2(2, -3)), Is.True, "min corner is inclusive");
            Assert.That(bounds.Contains(new int2(5, 3)), Is.True, "last interior tile");
            Assert.That(bounds.Contains(new int2(6, 3)), Is.False, "max-x is exclusive");
            Assert.That(bounds.Contains(new int2(5, 4)), Is.False, "max-y is exclusive");
            Assert.That(bounds.Contains(new int2(1, 0)), Is.False, "below min-x");
            Assert.That(bounds.Size, Is.EqualTo(new int2(4, 7)));
        }

        [Test]
        public void Bounds_FloatAndIntContainmentAgreeOnGrid()
        {
            DimensionBounds bounds = new DimensionBounds(new int2(0, 0), new int2(4, 4));
            Assert.That(bounds.Contains(new float2(3.9f, 0.0f)), Is.True);
            Assert.That(bounds.Contains(new float2(4.0f, 0.0f)), Is.False, "exclusive max holds for floats");
            Assert.That(bounds.Contains(new float2(-0.01f, 2.0f)), Is.False);
        }

        [Test]
        public void Domain_AbsoluteLocalRoundTripsExactly()
        {
            DimensionCoordinateDomain domain = MakeDomain(new int2(10000, -8000), 0);

            float2 local = new float2(37.5f, -12.25f);
            float2 absolute = domain.ToAbsolute(local);
            Assert.That(absolute, Is.EqualTo(new float2(10037.5f, -8012.25f)));
            Assert.That(domain.ToLocal(absolute), Is.EqualTo(local));

            // And the reverse composition returns the original absolute point.
            float2 someAbsolute = new float2(10123.0f, -7999.0f);
            Assert.That(domain.ToAbsolute(domain.ToLocal(someAbsolute)), Is.EqualTo(someAbsolute));
        }

        [Test]
        public void Domain_AbsoluteBoundsAreOriginPlusLocalBounds()
        {
            int2 origin = new int2(500, 500);
            DimensionCoordinateDomain domain = MakeDomain(origin, 0);

            // Playable local bounds in MakeDomain are (0,0)..(64,64).
            Assert.That(domain.PlayableAbsoluteBounds.Min, Is.EqualTo(origin));
            Assert.That(domain.PlayableAbsoluteBounds.MaxExclusive, Is.EqualTo(origin + new int2(64, 64)));

            // A local origin maps to the absolute origin and is inside the playable area.
            Assert.That(domain.ContainsPlayableAbsolute(domain.ToAbsolute(float2.zero)), Is.True);
            // Just outside the playable width is not playable...
            Assert.That(domain.ContainsPlayableAbsolute(domain.ToAbsolute(new float2(64f, 0f))), Is.False);
        }

        [Test]
        public void Domain_CoordinateBoundsIncludePaddingAroundPlayable()
        {
            DimensionCoordinateDomain domain = MakeDomain(new int2(0, 0), 8);

            // A point one tile outside the playable area but within the padded shell is a
            // valid coordinate context even though it is not playable.
            float2 justOutside = new float2(-1f, 10f);
            Assert.That(domain.ContainsPlayableAbsolute(justOutside), Is.False);
            Assert.That(domain.ContainsCoordinateAbsolute(justOutside), Is.True);
        }

        [Test]
        public void Domain_NegativePaddingIsClampedAndEmptyIdIsInvalid()
        {
            DimensionCoordinateDomain padded = MakeDomain(new int2(0, 0), -25);
            Assert.That(padded.PaddingTiles, Is.EqualTo(0));

            DimensionCoordinateDomain invalid = new DimensionCoordinateDomain(
                string.Empty,
                int2.zero,
                new DimensionBounds(int2.zero, new int2(4, 4)),
                new DimensionBounds(int2.zero, new int2(4, 4)),
                0,
                DimensionType.World);
            Assert.That(invalid.IsValid, Is.False);
        }

        private static DimensionCoordinateDomain MakeDomain(int2 origin, int padding)
        {
            int clamped = padding < 0 ? 0 : padding;
            DimensionBounds playable = new DimensionBounds(int2.zero, new int2(64, 64));
            DimensionBounds coordinate = new DimensionBounds(
                new int2(-clamped, -clamped),
                new int2(64 + clamped, 64 + clamped));
            return new DimensionCoordinateDomain(
                "test-dimension",
                origin,
                playable,
                coordinate,
                padding,
                DimensionType.World);
        }
    }
}
#endif
