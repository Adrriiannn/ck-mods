#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Guards the wiring that made eight of the framework's own passes reachable from a creature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THESE GUARD IS NOT A CRASH. Every one of the eight passes below was written,
    /// public, and correct for a long time before a creature could reach any of them: the
    /// world-object generator was the only caller of each, so a beam, a healing aura, mana, an
    /// owner, hiding in bushes, roaming a circuit, hurting whatever comes near and a shop were all
    /// buildable and none of them was reachable from the content type they belong to. The way that
    /// comes back is a call quietly moved, not deleted — past the temperament, where the sweep that
    /// closes their query gaps no longer sees what they added.
    /// </para>
    /// <para>
    /// SO THESE READ THE SOURCE. There is no Unity object to build here: the ordering rule is a
    /// fact about where two calls sit in one method, and the clobber rule is a fact about which
    /// component names appear inside eight others. Both are exactly what a text scan can answer,
    /// and neither needs a scene, a prefab or a play mode.
    /// </para>
    /// </remarks>
    public sealed class DimensionCreatureSpineTests
    {
        /// <summary>
        /// The eight passes the creature generator was taught to call, plus the two written for it.
        /// </summary>
        private static readonly string[] TheLiftedPasses =
        {
            "ApplyContinuousAttack",
            "ApplyBeamAndAmbience",
            "ApplyHidingAndHatching",
            "ApplyManaAndAura",
            "ApplyFinalTouches",
            "ApplySpawnerAndOrb",
            "ApplyTrader",
            "ApplyCreatureMinion",
            "ApplyCreatureLastStand",
        };

        /// <summary>
        /// Every lifted pass runs after the habits and before the temperament.
        /// </summary>
        /// <remarks>
        /// <para>
        /// AFTER THE HABITS, because <c>MinionConverter</c> reads <c>IsFlyingAuthoring</c> off the
        /// object to decide whether a minion flies, and the habits are what write it; and because
        /// the patrol route has to exist before the companion sweep is asked whether a roaming
        /// creature has one.
        /// </para>
        /// <para>
        /// BEFORE THE TEMPERAMENT, because the temperament overwrites the attack tags and the chase
        /// distance and can remove <c>BehaviourTagsAuthoring</c> outright, and because
        /// <c>FinishACreature</c> runs after THAT and is the thing that closes the query gaps these
        /// passes open. A call moved past the temperament generates cleanly and does nothing.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryLiftedPassRunsAfterTheHabitsAndBeforeTheTemperament()
        {
            // ANCHORED TO THE METHOD, NOT TO THE FILE. This asserts an order between three calls,
            // and an order only exists inside one body. Read over the whole file it survives only
            // as long as the file does: split the generator and all three IndexOf calls answer -1
            // in whichever half is read, so the assertions compare -1 with -1 and the guard stops
            // guarding without failing.
            string source = DimensionFrameworkSourceScanner.BodyOfMethodIn(TheGenerator(), "Configure");

            Assert.That(
                source,
                Is.Not.Null,
                "The creature generator has no Configure method any more, so the order these " +
                "passes run in is not written down anywhere this can read.");

            int habits = source.IndexOf(
                "DimensionObjectSpine.ApplyCreatureHabits(", StringComparison.Ordinal);
            int temperament = source.IndexOf(
                "ApplyTemperament(root, request, report);", StringComparison.Ordinal);
            int finish = source.IndexOf(
                "DimensionQueryCompanions.FinishACreature(", StringComparison.Ordinal);

            Assert.Greater(habits, -1, "The creature generator no longer calls ApplyCreatureHabits.");
            Assert.Greater(temperament, habits, "The temperament no longer runs after the habits.");
            Assert.Greater(
                finish,
                temperament,
                "FinishACreature no longer runs last. It is what closes the query gaps every pass " +
                "above it opens, and the sweep can only see what is already on the creature.");

            List<string> wrong = new List<string>();
            for (int i = 0; i < TheLiftedPasses.Length; i++)
            {
                int call = source.IndexOf(
                    "DimensionObjectSpine." + TheLiftedPasses[i] + "(",
                    StringComparison.Ordinal);
                if (call < 0)
                {
                    wrong.Add(TheLiftedPasses[i] + " is not called by the creature generator at all");
                    continue;
                }

                if (call < habits)
                {
                    wrong.Add(TheLiftedPasses[i] + " runs before the creature's habits");
                }

                if (call > temperament)
                {
                    wrong.Add(TheLiftedPasses[i] + " runs after the temperament");
                }
            }

            Assert.That(
                wrong,
                Is.Empty,
                "A pass the creature generator was taught to call has moved out of the window it " +
                "has to run in. Everything it writes would still be written, and the sweep that " +
                "makes it work would no longer see it:\n  " + string.Join("\n  ", wrong));
        }

        /// <summary>
        /// The two passes that share a component run in the order that keeps a boss beam working.
        /// </summary>
        /// <remarks>
        /// <c>ApplyContinuousAttack</c> owns <c>AttackContinuouslyAuthoring</c> and removes it when
        /// the answer is off. <c>ApplyFinalTouches</c> ADDS the same component for a boss beam,
        /// because a beam in Core Keeper is a thing that keeps hurting whatever stands in it and
        /// the beam's own system skips one without it. Continuous attack has to go first so the
        /// beam's add is the last word; swapped, a boss beam is built and then stripped.
        /// </remarks>
        [Test]
        public void TheContinuousAttackRunsBeforeTheBossBeamThatNeedsOne()
        {
            string source = TheGenerator();

            int continuous = source.IndexOf(
                "DimensionObjectSpine.ApplyContinuousAttack(", StringComparison.Ordinal);
            int finalTouches = source.IndexOf(
                "DimensionObjectSpine.ApplyFinalTouches(", StringComparison.Ordinal);

            Assert.Greater(continuous, -1, "ApplyContinuousAttack is no longer called.");
            Assert.Greater(
                finalTouches,
                continuous,
                "ApplyFinalTouches now runs before ApplyContinuousAttack, so the component it adds " +
                "for a boss beam is removed again in the same run and the beam never fires.");
        }

        /// <summary>
        /// The hiding pass is told that a creature answers being an egg somewhere else.
        /// </summary>
        /// <remarks>
        /// <c>ApplyHatching</c> owns <c>HatchWhenPlayerNearbyStateAuthoring</c> on a creature and is
        /// the only one of the two that can name one of the mod's own creatures to hatch into. The
        /// hiding pass runs later and its else-branch removes that component, so a call without the
        /// flag builds every generated egg and then strips its hatching in the same run.
        /// </remarks>
        [Test]
        public void TheHidingPassIsToldNotToOwnTheEgg()
        {
            string source = TheGenerator();

            int call = source.IndexOf(
                "DimensionObjectSpine.ApplyHidingAndHatching(", StringComparison.Ordinal);
            Assert.Greater(call, -1, "ApplyHidingAndHatching is no longer called.");

            // Everything up to the next spine call is this one's arguments. Reading to the first
            // ");" would stop inside the resolver lambda instead.
            int end = source.IndexOf("DimensionObjectSpine.", call + 1, StringComparison.Ordinal);
            Assert.Greater(end, call, "The call to ApplyHidingAndHatching could not be read.");

            StringAssert.Contains("true);", source.Substring(call, end - call));
        }

        /// <summary>
        /// No lifted pass touches the record of meals a creature has eaten.
        /// </summary>
        /// <remarks>
        /// The one component in this stretch with a live ordering trap.
        /// <c>ApplyCreatureHabits</c> toggles <c>MealsEatenAuthoring</c> from "remembers its meals",
        /// and <c>ApplyAbilities</c> force-adds it for a creature that grows up, because the game
        /// counts meals off that record. The force-add has to win. If a pass inserted between them
        /// ever toggled the same component, a creature that grows up would silently stop growing
        /// up — so this asserts that none of them mentions it.
        /// </remarks>
        [Test]
        public void NoLiftedPassTouchesTheMealsRecord()
        {
            string spine = TheSpine();

            List<string> wrong = new List<string>();
            for (int i = 0; i < TheLiftedPasses.Length; i++)
            {
                string body = BodyOf(spine, TheLiftedPasses[i]);
                if (body == null)
                {
                    wrong.Add(TheLiftedPasses[i] + " is no longer a method in the spine");
                    continue;
                }

                if (body.IndexOf("MealsEatenAuthoring", StringComparison.Ordinal) >= 0)
                {
                    wrong.Add(TheLiftedPasses[i] + " now writes MealsEatenAuthoring");
                }
            }

            Assert.That(
                wrong,
                Is.Empty,
                "A pass that runs between the creature's habits and its abilities has started " +
                "writing the meals record. The habits toggle it and growing up force-adds it, and " +
                "the force-add has to be the last word or a creature that grows up never does:\n  " +
                string.Join("\n  ", wrong));
        }

        /// <summary>
        /// An animal that can be tended is also given somewhere to keep the name it is given.
        /// </summary>
        /// <remarks>
        /// Everything in the game that names an animal uses <c>ecb.SetComponent&lt;NameCD&gt;</c>,
        /// which only writes a component that is already there, and <c>NameConverter</c> is the one
        /// thing that puts it there. Without both toggles the tending window offers to name an
        /// animal that has nowhere to keep the name, and <c>Cattle.GetName</c> answers null for ever.
        /// </remarks>
        [Test]
        public void BeingLivestockCarriesBothTheWindowAndTheName()
        {
            string body = BodyOf(TheSpine(), "ApplyCreatureHabits");
            Assert.IsNotNull(body, "ApplyCreatureHabits is no longer a method in the spine.");

            StringAssert.Contains("Toggle<CattleAuthoring>(root, habits.IsLivestock)", body);
            StringAssert.Contains("Toggle<NameAuthoring>(root, habits.IsLivestock)", body);
        }

        /// <summary>
        /// The beam's own buffer goes on with the beam and comes off with it.
        /// </summary>
        /// <remarks>
        /// <c>BeamAttackStateSystem</c>'s query names <c>BeamBuffer</c>, and Core Keeper's own
        /// <c>BeamAttackStateConverter</c> never ensures one. A marker left behind after the beam
        /// answer is unticked would keep ensuring a buffer and a cooldown on something with no beam,
        /// so both halves are asserted.
        /// </remarks>
        [Test]
        public void TheBeamCarriesItsOwnBufferOnAndOffTogether()
        {
            string body = BodyOf(TheSpine(), "ApplyBeamAndAmbience");
            Assert.IsNotNull(body, "ApplyBeamAndAmbience is no longer a method in the spine.");

            StringAssert.Contains(
                "EnsureComponent<ExpandNullforge.Creatures.DimensionBeamBufferAuthoring>(root)",
                body);
            StringAssert.Contains(
                "RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionBeamBufferAuthoring>(",
                body);
        }

        /// <summary>
        /// Every nested answer the creature template hands out is a real one, never null.
        /// </summary>
        /// <remarks>
        /// The generator passes each of these straight into a spine pass. Each pass returns early
        /// on a null template, so a null here would not throw — it would silently skip the whole
        /// answer, which is the exact shape of failure this work exists to remove.
        /// </remarks>
        [Test]
        public void TheCreatureTemplateNeverHandsOutNothing()
        {
            DimensionCreatureCombatTemplate combat = new DimensionCreatureCombatTemplate();

            Assert.IsNotNull(combat.BeamAndAmbience);
            Assert.IsNotNull(combat.HidingAndHatching);
            Assert.IsNotNull(combat.ManaAndAura);
            Assert.IsNotNull(combat.FinalTouches);
            Assert.IsNotNull(combat.SpawnerAndOrb);
            Assert.IsNotNull(combat.Trader);
            Assert.IsNotNull(combat.ContinuousAttack);
            Assert.IsNotNull(combat.Tending);
            Assert.IsNotNull(combat.Minion);
            Assert.IsNotNull(combat.LastStand);
        }

        /// <summary>
        /// The numbers a player walks up to are Core Keeper's own cow's.
        /// </summary>
        /// <remarks>
        /// Read off <c>Cow.prefab</c>: its <c>InteractableObject</c> has a radius of 1.3 and does
        /// not ignore the player's direction, and its <c>ObjectName</c> child sits two tiles up.
        /// The chest default of 0.375 would draw an animal's name inside the animal.
        /// </remarks>
        [Test]
        public void WalkingUpToACreatureStartsAtTheCowsOwnNumbers()
        {
            DimensionCreatureTendingTemplate tending = new DimensionCreatureTendingTemplate();

            Assert.AreEqual(1.3f, tending.HowCloseAPlayerMustBe, 0.0001f);
            Assert.AreEqual(2f, tending.HowHighItsNameFloats, 0.0001f);
            Assert.IsFalse(tending.WorksFromAnySide);
            Assert.IsFalse(tending.ItsNameWouldFloatInsideIt);
        }

        // ---- Scaffolding ---------------------------------------------------------------------

        private static string TheSpine()
        {
            return DimensionFrameworkSourceScanner.ReadPartials("DimensionObjectSpine");
        }

        private static string TheGenerator()
        {
            return DimensionFrameworkSourceScanner.ReadByName("DimensionCreatureGenerator.cs");
        }


        /// <summary>
        /// The text of one method, from its signature to the start of the next one.
        /// </summary>
        /// <remarks>
        /// Every method in the spine is declared at one indent inside the class, so the next
        /// declaration at that indent is the end of this one. Crude on purpose: it needs to be
        /// obviously right by reading rather than clever, and it only ever has to separate
        /// neighbours in one file whose shape is uniform.
        /// </remarks>
        private static string BodyOf(string source, string method)
        {
            int start = source.IndexOf(
                "public static void " + method + "(", StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            int next = source.IndexOf("\n        public static ", start + 1, StringComparison.Ordinal);
            int privateNext = source.IndexOf(
                "\n        private static ", start + 1, StringComparison.Ordinal);
            if (privateNext >= 0 && (next < 0 || privateNext < next))
            {
                next = privateNext;
            }

            return next < 0 ? source.Substring(start) : source.Substring(start, next - start);
        }
    }
}
#endif
