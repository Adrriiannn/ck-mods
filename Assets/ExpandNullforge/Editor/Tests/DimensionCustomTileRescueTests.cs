using System.Collections.Generic;
using ExpandNullforge.Tilesets;
using NUnit.Framework;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Locks the per-world bucketing that makes custom tiles survive on a host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the fix for a bug that only appeared when one process ran two worlds — a host, which is
    /// every single-player session too. The pending records were keyed by submap position alone, and a
    /// host's server and client worlds see the SAME positions, so three things went wrong at once: the
    /// second world's capture overwrote the first's, a claim REMOVES what it returns so whichever
    /// restore ticked first consumed the other's record, and ageing ran once per world per frame and
    /// halved how long a record survived.
    /// </para>
    /// <para>
    /// Every one of those is invisible in a normal run — the symptom is a chunk of custom blocks
    /// quietly turning to dirt, in one world, sometimes, depending on system order. That is worth a
    /// test rather than an in-game session, so what remains to be verified live is the streaming, not
    /// the bookkeeping.
    /// </para>
    /// </remarks>
    public sealed class DimensionCustomTileRescueTests
    {
        private World alpha;
        private World beta;

        [SetUp]
        public void CreateWorlds()
        {
            alpha = new World("nullforge-rescue-alpha");
            beta = new World("nullforge-rescue-beta");
        }

        [TearDown]
        public void DisposeWorlds()
        {
            DimensionCustomTileRescue.Clear(alpha);
            DimensionCustomTileRescue.Clear(beta);
            if (alpha != null && alpha.IsCreated) alpha.Dispose();
            if (beta != null && beta.IsCreated) beta.Dispose();
        }

        private static List<SubMapLayer> Layers(int tileset)
        {
            return new List<SubMapLayer> { new SubMapLayer { layer = new TileCD { tileset = tileset } } };
        }

        [Test]
        public void OneWorldsRecordDoesNotOverwriteAnothersAtTheSamePosition()
        {
            int2 shared = new int2(4, 7);
            DimensionCustomTileRescue.Record(alpha, shared, Layers(1000));
            DimensionCustomTileRescue.Record(beta, shared, Layers(2000));

            List<SubMapLayer> fromAlpha;
            List<SubMapLayer> fromBeta;
            Assert.IsTrue(DimensionCustomTileRescue.TryClaim(alpha, shared, out fromAlpha));
            Assert.IsTrue(DimensionCustomTileRescue.TryClaim(beta, shared, out fromBeta));

            Assert.AreEqual(1000, fromAlpha[0].layer.tileset);
            Assert.AreEqual(
                2000,
                fromBeta[0].layer.tileset,
                "A host's two worlds see the same submap positions; keying on position alone let one " +
                "capture destroy the other's.");
        }

        [Test]
        public void ClaimingInOneWorldLeavesTheOthersRecordIntact()
        {
            int2 shared = new int2(-3, 12);
            DimensionCustomTileRescue.Record(alpha, shared, Layers(1000));
            DimensionCustomTileRescue.Record(beta, shared, Layers(2000));

            List<SubMapLayer> claimed;
            DimensionCustomTileRescue.TryClaim(alpha, shared, out claimed);

            Assert.IsTrue(
                DimensionCustomTileRescue.HasPendingFor(beta),
                "A claim removes what it returns, so a shared store meant whichever world restored " +
                "first consumed the other's tiles and the other silently restored nothing.");
        }

        [Test]
        public void AClaimIsOnceOnly()
        {
            int2 position = new int2(1, 1);
            DimensionCustomTileRescue.Record(alpha, position, Layers(1000));

            List<SubMapLayer> first;
            List<SubMapLayer> second;
            Assert.IsTrue(DimensionCustomTileRescue.TryClaim(alpha, position, out first));
            Assert.IsFalse(
                DimensionCustomTileRescue.TryClaim(alpha, position, out second),
                "The record is consumed on restore; handing it out twice would double-write tiles.");
        }

        [Test]
        public void ClaimingSomethingNeverRecordedIsNotAnError()
        {
            List<SubMapLayer> nothing;
            Assert.IsFalse(DimensionCustomTileRescue.TryClaim(alpha, new int2(99, 99), out nothing));
            Assert.IsNull(nothing);
        }

        [Test]
        public void ClearingOneWorldDoesNotWipeTheOther()
        {
            DimensionCustomTileRescue.Record(alpha, new int2(0, 0), Layers(1000));
            DimensionCustomTileRescue.Record(beta, new int2(0, 0), Layers(2000));

            DimensionCustomTileRescue.Clear(alpha);

            Assert.IsFalse(DimensionCustomTileRescue.HasPendingFor(alpha));
            Assert.IsTrue(
                DimensionCustomTileRescue.HasPendingFor(beta),
                "Tearing down one of a host's worlds must not discard the other's held tiles.");
        }

        [Test]
        public void AgeingOneWorldDoesNotAgeTheOther()
        {
            int2 position = new int2(5, 5);
            DimensionCustomTileRescue.Record(alpha, position, Layers(1000));
            DimensionCustomTileRescue.Record(beta, position, Layers(2000));

            // Ageing used to run once per world against one shared store, so a record aged twice per
            // frame and expired in half the passes it was given.
            for (int i = 0; i < 4; i++)
            {
                DimensionCustomTileRescue.AgePending(alpha);
            }

            Assert.IsTrue(
                DimensionCustomTileRescue.HasPendingFor(beta),
                "One world's ageing must not consume another world's grace period.");
        }

        [Test]
        public void AnEmptyCaptureIsNotRecorded()
        {
            DimensionCustomTileRescue.Record(alpha, new int2(2, 2), new List<SubMapLayer>());
            Assert.IsFalse(
                DimensionCustomTileRescue.HasPendingFor(alpha),
                "A submap with no custom layers has nothing to rescue; holding a record for it would " +
                "occupy the store and age out for nothing.");
        }

        [Test]
        public void RecordingTwiceForOnePositionKeepsTheLatest()
        {
            int2 position = new int2(8, 8);
            DimensionCustomTileRescue.Record(alpha, position, Layers(1000));
            DimensionCustomTileRescue.Record(alpha, position, Layers(1234));

            List<SubMapLayer> claimed;
            DimensionCustomTileRescue.TryClaim(alpha, position, out claimed);

            Assert.AreEqual(
                1234,
                claimed[0].layer.tileset,
                "A re-capture of the same submap describes its current state; keeping the older one " +
                "would restore tiles the player already changed.");
        }
    }
}
