#if UNITY_INCLUDE_TESTS
using System;
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pure-logic coverage for generation-pass planning defaults: every pass phase must map to
    /// a concrete generation table kind, so a provider that trusts the default never resolves
    /// against the neutral 'Any' table by accident.
    /// </summary>
    internal sealed class DimensionGenerationPassPlanningTests
    {
        [TestCase(DimensionGenerationPassPhase.Terrain, DimensionGenerationTableKind.Terrain)]
        [TestCase(DimensionGenerationPassPhase.Liquid, DimensionGenerationTableKind.Terrain)]
        [TestCase(DimensionGenerationPassPhase.Structures, DimensionGenerationTableKind.Scene)]
        [TestCase(DimensionGenerationPassPhase.Scenes, DimensionGenerationTableKind.Scene)]
        [TestCase(DimensionGenerationPassPhase.Ore, DimensionGenerationTableKind.Resource)]
        [TestCase(DimensionGenerationPassPhase.Objects, DimensionGenerationTableKind.Object)]
        [TestCase(DimensionGenerationPassPhase.Mobs, DimensionGenerationTableKind.Spawn)]
        [TestCase(DimensionGenerationPassPhase.Bosses, DimensionGenerationTableKind.Spawn)]
        [TestCase(DimensionGenerationPassPhase.Events, DimensionGenerationTableKind.WorldEvent)]
        [TestCase(DimensionGenerationPassPhase.Polish, DimensionGenerationTableKind.Custom)]
        [TestCase(DimensionGenerationPassPhase.Custom, DimensionGenerationTableKind.Custom)]
        public void GetDefaultTableKind_MapsEveryPhaseToItsTable(
            DimensionGenerationPassPhase phase,
            DimensionGenerationTableKind expected)
        {
            Assert.That(
                DimensionGenerationPassWorkPlanUtility.GetDefaultTableKind(phase),
                Is.EqualTo(expected));
        }

        [Test]
        public void GetDefaultTableKind_IsTotalAndNeverTheNeutralTable()
        {
            foreach (DimensionGenerationPassPhase phase in
                (DimensionGenerationPassPhase[])Enum.GetValues(
                    typeof(DimensionGenerationPassPhase)))
            {
                DimensionGenerationTableKind kind =
                    DimensionGenerationPassWorkPlanUtility.GetDefaultTableKind(phase);
                Assert.That(
                    kind,
                    Is.Not.EqualTo(DimensionGenerationTableKind.Any),
                    "Phase " + phase + " defaulted to the neutral 'Any' table.");
            }
        }
    }
}
#endif
