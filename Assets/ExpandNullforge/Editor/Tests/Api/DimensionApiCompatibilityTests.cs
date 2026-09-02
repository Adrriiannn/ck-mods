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
    /// <remarks>
    /// IT PINS SOMETHING THE RUNTIME NOW DOES. Until the D7 decision this file was pinning a
    /// helper nothing called: the two places the service really asks the question wrote the
    /// comparison out inline, as <c>MinimumApiVersion &gt; CurrentApiVersion</c> in the manifest
    /// validation and <c>&lt;=</c> in the readiness check, and neither looked at the bottom of the
    /// range at all — a content pack declaring version 0 was accepted as compatible by both. Both
    /// now call <see cref="DimensionApiCompatibility.IsApiVersionSupported"/>, so the third
    /// assertion below — that a version under the minimum is refused — is a claim about the
    /// running framework instead of about a helper with no callers.
    /// </remarks>
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
