#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using PugMod;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Covers the "did my items actually register?" feedback loop. Object ids only exist once the
    /// game has loaded mod content, so the registry must tolerate resolving late, cache what it
    /// finds, and be able to name the items that never arrived instead of leaving a creator with
    /// a silently missing object.
    /// </summary>
    internal sealed class DimensionItemObjectRegistryTests
    {
        private readonly Dictionary<string, ObjectID> gameObjects =
            new Dictionary<string, ObjectID>();

        private int resolveCalls;

        [SetUp]
        public void SetUp()
        {
            DimensionItemObjectRegistry.Clear();
            gameObjects.Clear();
            resolveCalls = 0;
            DimensionItemObjectRegistry.SetResolverForTesting(name =>
            {
                resolveCalls++;
                return gameObjects.TryGetValue(name, out ObjectID id) ? id : ObjectID.None;
            });
        }

        [TearDown]
        public void TearDown()
        {
            DimensionItemObjectRegistry.SetResolverForTesting(null);
            DimensionItemObjectRegistry.Clear();
        }

        [Test]
        public void AnItemThatRegistersLate_ResolvesOnARetry()
        {
            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:blade" });

            // Content has not loaded yet.
            Assert.That(
                DimensionItemObjectRegistry.TryResolve("mod:blade", out _), Is.False);
            Assert.That(DimensionItemObjectRegistry.IsComplete, Is.False);
            Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(1));

            // The game finishes loading and the object appears.
            gameObjects["mod:blade"] = (ObjectID)1234;

            Assert.That(
                DimensionItemObjectRegistry.TryResolve("mod:blade", out ObjectID resolved),
                Is.True);
            Assert.That(resolved, Is.EqualTo((ObjectID)1234));
            Assert.That(DimensionItemObjectRegistry.IsComplete, Is.True);
            Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void AResolvedItem_IsCachedRatherThanLookedUpEveryCall()
        {
            gameObjects["mod:blade"] = (ObjectID)7;
            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:blade" });

            DimensionItemObjectRegistry.TryResolve("mod:blade", out _);
            int afterFirst = resolveCalls;
            DimensionItemObjectRegistry.TryResolve("mod:blade", out _);
            DimensionItemObjectRegistry.TryResolve("mod:blade", out _);

            Assert.That(resolveCalls, Is.EqualTo(afterFirst), "The lookup should be cached.");
        }

        [Test]
        public void RefreshAll_ReportsHowManyItemsAreStillMissing()
        {
            DimensionItemObjectRegistry.Declare(
                "pack", new[] { "mod:a", "mod:b", "mod:c" });
            gameObjects["mod:a"] = (ObjectID)1;
            gameObjects["mod:c"] = (ObjectID)3;

            int stillMissing = DimensionItemObjectRegistry.RefreshAll();

            Assert.That(stillMissing, Is.EqualTo(1));
            Assert.That(DimensionItemObjectRegistry.ResolvedCount, Is.EqualTo(2));
            Assert.That(DimensionItemObjectRegistry.GetPending(), Is.EqualTo(new[] { "mod:b" }));
        }

        /// <summary>
        /// Calling the reporter over and over leaves the pending list exactly as it was.
        /// </summary>
        /// <remarks>
        /// RENAMED FROM ReportMissing_LogsEachItemOnlyOnce, which this never checked. There is no
        /// <c>LogAssert</c> here and no assertion about the console at all — what it holds is that
        /// the reporter does not mutate. Whether the once-only console rule really holds is
        /// untested; the name now stops implying it does.
        /// </remarks>
        [Test]
        public void RepeatedReportMissingCallsLeaveThePendingListAlone()
        {
            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:ghost" });

            // A per-frame retry must not spam the console, and must not quietly drop the item
            // while trying not to.
            DimensionItemObjectRegistry.ReportMissing();
            DimensionItemObjectRegistry.ReportMissing();
            DimensionItemObjectRegistry.ReportMissing();

            Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(1));
            Assert.That(DimensionItemObjectRegistry.GetPending(), Is.EqualTo(new[] { "mod:ghost" }));
        }

        [Test]
        public void ReDeclaringAnItem_KeepsItsExistingResolution()
        {
            gameObjects["mod:blade"] = (ObjectID)42;
            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:blade" });
            DimensionItemObjectRegistry.TryResolve("mod:blade", out _);

            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:blade" });

            Assert.That(DimensionItemObjectRegistry.ResolvedCount, Is.EqualTo(1));
            Assert.That(DimensionItemObjectRegistry.IsComplete, Is.True);
        }

        [Test]
        public void AnUndeclaredItem_StillResolvesIfTheGameHasIt()
        {
            // Consumers may resolve ids they never declared; declaration only drives reporting.
            gameObjects["mod:surprise"] = (ObjectID)5;

            Assert.That(
                DimensionItemObjectRegistry.TryResolve("mod:surprise", out ObjectID id), Is.True);
            Assert.That(id, Is.EqualTo((ObjectID)5));
        }

        [Test]
        public void EmptyAndNullInputs_AreRejectedNotCrashed()
        {
            DimensionItemObjectRegistry.Declare("pack", null);
            DimensionItemObjectRegistry.Declare(null, new[] { string.Empty, null });

            Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(0));
            Assert.That(DimensionItemObjectRegistry.TryResolve(null, out _), Is.False);
            Assert.That(DimensionItemObjectRegistry.TryResolve(string.Empty, out _), Is.False);
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            gameObjects["mod:blade"] = (ObjectID)1;
            DimensionItemObjectRegistry.Declare("pack", new[] { "mod:blade" });
            DimensionItemObjectRegistry.TryResolve("mod:blade", out _);

            DimensionItemObjectRegistry.Clear();

            Assert.That(DimensionItemObjectRegistry.ResolvedCount, Is.EqualTo(0));
            Assert.That(DimensionItemObjectRegistry.PendingCount, Is.EqualTo(0));
        }
    }
}
#endif
