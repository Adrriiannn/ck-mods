#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pins the public API version-compatibility contract so an accidental change to the
    /// supported range is caught. These are read-only checks — they never touch the global
    /// service registration, which the live editor may already own.
    /// </summary>
    internal sealed class DimensionApiCompatibilityTests
    {
        [Test]
        public void CurrentApiVersion_IsAtLeastTheMinimumSupported()
        {
            Assert.That(
                DimensionApi.CurrentApiVersion,
                Is.GreaterThanOrEqualTo(DimensionApiCompatibility.MinimumSupportedApiVersion));
            Assert.That(DimensionApiCompatibility.Policy, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IsApiVersionSupported_AcceptsTheSupportedRangeOnly()
        {
            Assert.That(
                DimensionApiCompatibility.IsApiVersionSupported(
                    DimensionApiCompatibility.MinimumSupportedApiVersion),
                Is.True);
            Assert.That(
                DimensionApiCompatibility.IsApiVersionSupported(DimensionApi.CurrentApiVersion),
                Is.True);
            Assert.That(
                DimensionApiCompatibility.IsApiVersionSupported(
                    DimensionApiCompatibility.MinimumSupportedApiVersion - 1),
                Is.False,
                "Versions below the minimum are unsupported.");
            Assert.That(
                DimensionApiCompatibility.IsApiVersionSupported(
                    DimensionApi.CurrentApiVersion + 1),
                Is.False,
                "Versions newer than the framework are unsupported.");
        }
    }
}
#endif
