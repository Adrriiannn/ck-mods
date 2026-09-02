#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Guards the decision of what to delete. Every test here is really the same question asked from
    /// a different angle: can this ever name a file the framework did not generate?
    /// </summary>
    internal sealed class DimensionGeneratedArtifactPrunerTests
    {
        [Test]
        public void ARenamedItem_LeavesItsOldIdBehind()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { "MyMod:blade" },
                new[] { "MyMod:sword" });

            Assert.That(orphans, Is.EqualTo(new[] { "blade" }));
        }

        [Test]
        public void AnItemThatStillExists_IsNeverOrphaned()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { "MyMod:blade", "MyMod:shield" },
                new[] { "MyMod:blade", "MyMod:shield" });

            Assert.That(orphans, Is.Empty);
        }

        /// <summary>
        /// The qualifier is an implementation detail of the object name, not of the file name, so a
        /// run that starts qualifying ids must not read as "everything was renamed" and delete the
        /// whole set.
        /// </summary>
        [Test]
        public void QualifiedAndBareFormsOfOneId_AreTheSameItem()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { "blade" },
                new[] { "MyMod:blade" });

            Assert.That(orphans, Is.Empty, "Adding the mod qualifier is not a rename.");
        }

        [Test]
        public void NothingGeneratedNow_OrphansEverythingFromBefore()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { "MyMod:blade", "MyMod:shield" },
                new string[0]);

            Assert.That(orphans, Is.EqualTo(new[] { "blade", "shield" }));
        }

        /// <summary>
        /// A first run has no previous list. Returning everything currently generated would delete
        /// the prefabs that run just wrote.
        /// </summary>
        [Test]
        public void NoPreviousRun_OrphansNothing()
        {
            Assert.That(
                DimensionGeneratedArtifactPruner.FindOrphanedItemIds(null, new[] { "MyMod:blade" }),
                Is.Empty);
            Assert.That(
                DimensionGeneratedArtifactPruner.FindOrphanedItemIds(new string[0], new[] { "MyMod:blade" }),
                Is.Empty);
        }

        [Test]
        public void EmptyAndNullEntries_AreIgnoredRatherThanNamingAFile()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { null, string.Empty, "MyMod:", "MyMod:blade" },
                new string[0]);

            Assert.That(
                orphans, Is.EqualTo(new[] { "blade" }),
                "A blank id must not resolve to a path and delete something unrelated.");
        }

        [Test]
        public void TheSameOrphanListedTwice_IsReportedOnce()
        {
            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                new[] { "MyMod:blade", "blade" },
                new string[0]);

            Assert.That(orphans, Is.EqualTo(new[] { "blade" }));
        }
    }
}
#endif
