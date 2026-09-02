using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Zones;
using NUnit.Framework;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the ordering and identity rules behind boss phases.
    /// </summary>
    /// <remarks>
    /// Phases are the one place this framework supplies structure rather than borrowing it — Core
    /// Keeper's own bosses each hardcode their own state machine, so there was nothing to append to.
    /// That makes the ordering rules worth pinning down: a boss dropped past three thresholds by one
    /// hit has to run them in the order the author wrote, not the order they were registered.
    /// </remarks>
    public sealed class DimensionBossPhaseTests
    {
        [SetUp]
        [TearDown]
        public void Reset()
        {
            DimensionBossPhaseRegistry.Clear();
        }

        private static void Register(string phaseId, float threshold)
        {
            DimensionBossPhaseRegistry.Register(
                phaseId,
                "mod:boss",
                threshold,
                DimensionBossPhaseAction.SummonAdds,
                "mod:add",
                2,
                0f,
                4f);
        }

        [Test]
        public void PhasesComeBackHighestThresholdFirstWhateverOrderTheyWereAdded()
        {
            // One big hit can cross several thresholds at once. Running them low-to-high would play
            // the boss's final beat before its opening one.
            Register("third", 0.25f);
            Register("first", 0.75f);
            Register("second", 0.5f);

            IReadOnlyList<DimensionBossPhaseDefinition> phases =
                DimensionBossPhaseRegistry.GetPhases("mod:boss");

            Assert.AreEqual("first", phases[0].PhaseId);
            Assert.AreEqual("second", phases[1].PhaseId);
            Assert.AreEqual("third", phases[2].PhaseId);
        }

        [Test]
        public void ReRegisteringAPhaseReplacesItRatherThanAddingASecond()
        {
            // A reload re-registers everything; two copies of one phase would summon twice.
            Register("first", 0.75f);
            Register("first", 0.5f);

            IReadOnlyList<DimensionBossPhaseDefinition> phases =
                DimensionBossPhaseRegistry.GetPhases("mod:boss");

            Assert.AreEqual(1, phases.Count);
            Assert.AreEqual(0.5f, phases[0].HealthThreshold, 0.0001f);
        }

        [Test]
        public void TwoBossesKeepTheirOwnPhases()
        {
            Register("first", 0.5f);
            DimensionBossPhaseRegistry.Register(
                "other", "mod:otherboss", 0.5f, DimensionBossPhaseAction.Heal, string.Empty, 100, 0f, 0f);

            Assert.AreEqual(1, DimensionBossPhaseRegistry.GetPhases("mod:boss").Count);
            Assert.AreEqual(1, DimensionBossPhaseRegistry.GetPhases("mod:otherboss").Count);
        }

        [Test]
        public void ABossWithNoPhasesReportsNothingRatherThanAnEmptyList()
        {
            // The runtime skips a boss whose lookup misses; an empty list would make it walk a loop
            // every tick for every boss in the world to conclude the same thing.
            Assert.IsNull(DimensionBossPhaseRegistry.GetPhases("mod:never-registered"));
        }

        [Test]
        public void ABossWithNoNameIsRefusedRatherThanStoredNameless()
        {
            DimensionBossPhaseRegistry.Register(
                "phase", string.Empty, 0.5f, DimensionBossPhaseAction.Heal, string.Empty, 1, 0f, 0f);

            Assert.IsFalse(DimensionBossPhaseRegistry.HasAny);
        }

        [Test]
        public void EveryBossWithPhasesIsListedSoTheRuntimeCanResolveItsIdOnce()
        {
            Register("first", 0.5f);
            DimensionBossPhaseRegistry.Register(
                "other", "mod:otherboss", 0.5f, DimensionBossPhaseAction.Heal, string.Empty, 1, 0f, 0f);

            IReadOnlyList<string> names = DimensionBossPhaseRegistry.BossNames;
            Assert.AreEqual(2, names.Count);
            CollectionAssert.Contains(names, "mod:boss");
            CollectionAssert.Contains(names, "mod:otherboss");
        }

        [Test]
        public void EverythingTheAuthorChoseSurvivesRegistration()
        {
            DimensionBossPhaseRegistry.Register(
                "enrage",
                "mod:boss",
                0.3f,
                DimensionBossPhaseAction.ApplyConditionToPlayers,
                "Poisoned",
                3,
                12f,
                16f);

            DimensionBossPhaseDefinition phase = DimensionBossPhaseRegistry.GetPhases("mod:boss")[0];
            Assert.AreEqual(DimensionBossPhaseAction.ApplyConditionToPlayers, phase.Action);
            Assert.AreEqual("Poisoned", phase.ActionTarget);
            Assert.AreEqual(3, phase.ActionAmount);
            Assert.AreEqual(12f, phase.ActionDuration, 0.0001f);
            Assert.AreEqual(16f, phase.Radius, 0.0001f);
        }

        [Test]
        public void TheAuthoringActionsMatchTheRuntimeActionsEntryForEntry()
        {
            // Two enums exist so the authoring asset does not depend on a runtime type whose numbering
            // could shift. The generator writes one as the other by name, so they have to agree — a
            // drift here would turn "summon adds" into "heal" at the loudest moment of a fight.
            Array authoring = Enum.GetValues(typeof(DimensionBossPhaseActionKind));
            Array runtime = Enum.GetValues(typeof(DimensionBossPhaseAction));
            Assert.AreEqual(runtime.Length, authoring.Length);

            for (int i = 0; i < authoring.Length; i++)
            {
                Assert.AreEqual(
                    ((DimensionBossPhaseAction)runtime.GetValue(i)).ToString(),
                    ((DimensionBossPhaseActionKind)authoring.GetValue(i)).ToString(),
                    "Phase action " + i + " differs between authoring and runtime.");
                Assert.AreEqual(
                    (int)runtime.GetValue(i),
                    (int)authoring.GetValue(i),
                    "Phase action " + i + " has a different number on each side.");
            }
        }
    }
}
