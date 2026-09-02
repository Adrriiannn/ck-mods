#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Guards the capability-maturity registry: stable, unique ids; described maturities; and
    /// a conservative alpha-readiness rule so the dashboard/docs can trust it as the single
    /// source of truth for what is actually proven.
    /// </summary>
    /// <remarks>
    /// <c>DimensionCapabilityRegistry.All()</c> has no production caller today — these four
    /// readings are the only ones in the tree. It had one, a menu item that printed a maturity
    /// report, and that menu item was deleted as redundant with the dashboard. <c>TryGet</c> and
    /// <c>Describe</c> are still live — <c>DimensionFrameworkAuthoringWindow.Navigation.cs</c>,
    /// <c>DimensionStageMaturity</c> and <c>DimensionContentValidation</c> all call both — so the
    /// registry itself
    /// is not dead. Said here so the coverage below is not mistaken for evidence that anything
    /// asks for the whole list.
    /// </remarks>
    internal sealed class DimensionCapabilityRegistryTests
    {
        [Test]
        public void All_ReturnsUniqueWellFormedCapabilities()
        {
            DimensionCapability[] capabilities = DimensionCapabilityRegistry.All();
            Assert.That(capabilities, Is.Not.Null.And.Length.GreaterThan(0));

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < capabilities.Length; i++)
            {
                DimensionCapability capability = capabilities[i];
                Assert.That(capability.Id, Is.Not.Null.And.Not.Empty);
                Assert.That(capability.Title, Is.Not.Null.And.Not.Empty);
                Assert.That(capability.Note, Is.Not.Null.And.Not.Empty);
                Assert.That(
                    ids.Add(capability.Id),
                    Is.True,
                    "Duplicate capability id: " + capability.Id);
            }
        }

        [Test]
        public void All_ReturnsIndependentCopies()
        {
            // Callers must not be able to mutate the shared registry through the returned array.
            DimensionCapability[] first = DimensionCapabilityRegistry.All();
            DimensionCapability[] second = DimensionCapabilityRegistry.All();
            Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(first.Length, Is.EqualTo(second.Length));
        }

        [Test]
        public void TryGet_ResolvesKnownIdAndRejectsUnknown()
        {
            Assert.That(
                DimensionCapabilityRegistry.TryGet("portal-studio", out DimensionCapability portal),
                Is.True);
            Assert.That(portal.Title, Is.Not.Empty);

            Assert.That(
                DimensionCapabilityRegistry.TryGet("does-not-exist", out _),
                Is.False);
        }

        [Test]
        public void IsAlphaReady_IsTrueOnlyForEvidencedCapabilities()
        {
            Assert.That(DimensionCapabilityRegistry.IsAlphaReady("does-not-exist"), Is.False);

            DimensionCapability[] capabilities = DimensionCapabilityRegistry.All();
            for (int i = 0; i < capabilities.Length; i++)
            {
                bool expected = capabilities[i].Maturity ==
                    DimensionCapabilityMaturity.ImplementedAndEvidenced;
                Assert.That(
                    DimensionCapabilityRegistry.IsAlphaReady(capabilities[i].Id),
                    Is.EqualTo(expected),
                    capabilities[i].Id);
            }
        }

        [Test]
        public void Describe_ReturnsALabelForEveryMaturity()
        {
            foreach (DimensionCapabilityMaturity maturity in
                (DimensionCapabilityMaturity[])Enum.GetValues(
                    typeof(DimensionCapabilityMaturity)))
            {
                Assert.That(
                    DimensionCapabilityRegistry.Describe(maturity),
                    Is.Not.Null.And.Not.Empty,
                    maturity.ToString());
            }
        }
    }
}
#endif
