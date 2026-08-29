#if UNITY_INCLUDE_TESTS
using ExpandNullforge.Authoring;
using ExpandNullforge.Scenes;
using NUnit.Framework;
using PugWorldGen;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The dungeon-parity layers' contracts: the shapes that cross from the Studio into the
    /// game must clamp and default the way their tooltips promise.
    /// </summary>
    internal sealed class DimensionDungeonParityTests
    {
        [Test]
        public void PatchShapesStayValueAlignedWithTheGamesAlgorithm()
        {
            // The blob writes the enum by value; a drift here silently reshapes every patch.
            Assert.That((int)DimensionFillingPatchShape.OneSpot, Is.EqualTo((int)SpawnAlgorithm.Spot));
            Assert.That((int)DimensionFillingPatchShape.RoundPatch, Is.EqualTo((int)SpawnAlgorithm.Circle));
            Assert.That((int)DimensionFillingPatchShape.SquarePatch, Is.EqualTo((int)SpawnAlgorithm.Rect));
            Assert.That((int)DimensionFillingPatchShape.FlowingBlob, Is.EqualTo((int)SpawnAlgorithm.Cluster));
        }

        [Test]
        public void AFillingRuleSurvivesNulls()
        {
            DimensionDungeonFillingRule rule =
                new DimensionDungeonFillingRule(null, DimensionRoomKind.Main, false, -5, null);
            Assert.That(rule.FillingId, Is.EqualTo(string.Empty));
            Assert.That(rule.OnlyIfAtLeastThisBig, Is.EqualTo(0));
            Assert.That(rule.Entries, Is.Not.Null);
            Assert.That(rule.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void RoomKindFlagsStayValueAlignedWithTheGames()
        {
            // The assembler casts these to RoomFlags by value; the Studio's whole room
            // vocabulary rides on the alignment.
            Assert.That((int)DimensionRoomKind.Main, Is.EqualTo((int)RoomFlags.Main));
            Assert.That((int)DimensionRoomKind.HandmadeScene, Is.EqualTo((int)RoomFlags.CustomScene));
            Assert.That((int)DimensionRoomKind.Entrance, Is.EqualTo((int)RoomFlags.Entrance));
            Assert.That((int)DimensionRoomKind.DeadEnd, Is.EqualTo((int)RoomFlags.End));
            Assert.That((int)DimensionRoomKind.Connecting, Is.EqualTo((int)RoomFlags.Connecting));
            Assert.That((int)DimensionRoomKind.Filler, Is.EqualTo((int)RoomFlags.Fill));
        }

        [Test]
        public void InDimensionPlacementMeasuresFromTheLocalCentreNotTheWorldCore()
        {
            // The whole point of local coordinates: a dimension sits thousands of tiles from
            // the world's origin, and an author's "keep 100 tiles clear" must mean 100 tiles
            // from THEIR local (0, 0) — the arrival spot — or the number is unusable.
            DimensionDungeonDefinition keepsDistance = new DimensionDungeonDefinition(
                "d", "", 40, 1f, 0.5f, 0.5f,
                100,   // minDistanceFromCentre, in LOCAL tiles
                true, null,
                dimensionId: "test.dim");

            Assert.That(
                ExpandNullforge.Generation.DimensionDungeonPlacementPassProvider.PassesPlacementBands(
                    new Unity.Mathematics.int2(30, 40), keepsDistance),
                Is.False,
                "50 tiles from local (0,0) is inside the 100-tile keep-out.");
            Assert.That(
                ExpandNullforge.Generation.DimensionDungeonPlacementPassProvider.PassesPlacementBands(
                    new Unity.Mathematics.int2(90, 120), keepsDistance),
                Is.True,
                "150 tiles from local (0,0) clears the keep-out. If this fails, the check is " +
                "measuring from somewhere other than the local centre.");
        }

        [Test]
        public void TheRingBandAndTheKeepOutCompose()
        {
            DimensionDungeonDefinition ringed = new DimensionDungeonDefinition(
                "d", "", 40, 1f, 0.5f, 0.5f, 200, true, null,
                dimensionId: "test.dim",
                dimensionPlacement: DimensionScenePlacementMode.RadialBand,
                minRadiusTiles: 100,
                maxRadiusTiles: 400);

            Assert.That(
                ExpandNullforge.Generation.DimensionDungeonPlacementPassProvider.PassesPlacementBands(
                    new Unity.Mathematics.int2(150, 0), ringed),
                Is.False,
                "Inside the ring but inside the keep-out: the stricter rule wins.");
            Assert.That(
                ExpandNullforge.Generation.DimensionDungeonPlacementPassProvider.PassesPlacementBands(
                    new Unity.Mathematics.int2(300, 0), ringed),
                Is.True);
            Assert.That(
                ExpandNullforge.Generation.DimensionDungeonPlacementPassProvider.PassesPlacementBands(
                    new Unity.Mathematics.int2(450, 0), ringed),
                Is.False,
                "Past the ring's outer edge.");
        }
    }
}
#endif
