#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ExpandNullforge.Diagnostics;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Holds the world-load self-audit to what it claims: it names every system and every patch the
    /// framework has, and everything it prints is something a person can act on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY ASSERTION HERE FAILS ON AN EMPTY SUBJECT LIST, and that is the point rather than a
    /// nicety. An audit is a thing that walks a list and reports what is wrong with it; a list that
    /// has gone empty makes it report nothing wrong having looked at nothing, which is the exact
    /// shape of failure this framework has shipped three times. So each test that walks a roster
    /// asserts a floor on its size first, and each cross-check asserts that the source scan it
    /// compares against found something.
    /// </para>
    /// <para>
    /// THE CROSS-CHECKS ARE WHAT KEEPS THE ROSTERS HONEST. A table somebody has to keep up to date
    /// is only worth having if forgetting to update it is loud. Adding a class that derives from
    /// <c>SystemBase</c>, or a class carrying <c>[HarmonyPatch]</c>, without adding its row fails
    /// here, offline, before anybody launches the game.
    /// </para>
    /// </remarks>
    public sealed partial class DimensionSelfAuditTests
    {

        [Test]
        public void OnlyPatchesWithSomethingWaitingOnThemCanBeReported()
        {
            // A patch that never ran and has nothing registered for it is not a fault, and saying
            // so is how a scheme like this gets switched off. So every reportable row must have a
            // way to count what is waiting; a row with neither a count nor the player-action mark
            // would be reported on a session where nothing was wrong.
            DimensionPatchRoster.Row[] rows = DimensionPatchRoster.All;
            List<string> unaccountable = new List<string>();
            int reportable = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].OnlyOnPlayerAction)
                {
                    continue;
                }

                reportable++;
                if (rows[i].CountWork == null)
                {
                    unaccountable.Add(rows[i].PatchClass);
                }
            }

            Assert.Greater(
                reportable,
                0,
                "No patch row can be reported at all, so the patch half of the audit would always "
                + "be silent.");
            Assert.IsEmpty(
                unaccountable,
                "These rows can be reported as failures and nothing can count what is waiting on "
                + "them, so they would be reported on a session where nothing was registered: "
                + string.Join(", ", unaccountable.ToArray()));
        }

        [Test]
        public void OnlySystemsWithSomethingWaitingOnThemAreReportedForNeverRunning()
        {
            // THE SAME RULE AS THE PATCH TEST ABOVE, WHICH IS THE POINT. That invariant was written
            // down once and applied to one of the two tables; the system side reported every row
            // whose registry keeps no total as a failure — eighteen of thirty-one — in a sentence
            // that then said it might not matter. This calls the real decision the liveness pass
            // makes, on the real rows, so the two tables are held to one standard.
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            Assert.GreaterOrEqual(
                rows.Length,
                FewestPlausibleSystems,
                "The system roster holds " + rows.Length + " rows, so this checked almost nothing.");

            List<string> wrong = new List<string>();
            int countable = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].CountWork != null)
                {
                    countable++;
                    continue;
                }

                if (DimensionSelfAudit.WorthReportingAsNeverRan(rows[i]))
                {
                    wrong.Add(rows[i].Name);
                }
            }

            Assert.IsEmpty(
                wrong,
                "Nothing can count what is waiting on these, and the liveness pass would still call "
                + "them failures on a session where nothing was wrong: "
                + string.Join(", ", wrong.ToArray()));

            // A FLOOR IN THE OTHER DIRECTION. With no countable row at all the liveness pass could
            // never report anything, which reads from the log exactly like a healthy session.
            Assert.Greater(
                countable,
                0,
                "No system row can count what is waiting on it, so the liveness pass is silent "
                + "whatever happens.");
            Assert.IsFalse(
                DimensionSelfAudit.WorthReportingAsNeverRan(null),
                "A missing row is reported as a failure, so the pass can fail on nothing.");
        }

        /// <summary>
        /// A row counts what would have WOKEN the system, or it counts nothing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE FALSE-ALARM RULE, WRITTEN DOWN. <see cref="DimensionSelfAudit.WorthReportingAsNeverRan"/>
        /// turns "this system has never updated" into a Problem as soon as its row can count
        /// something, and that is only sound while the thing counted is the thing that makes it
        /// update. Two rows counted content instead:
        /// </para>
        /// <list type="bullet">
        /// <item><c>DimensionBlastFireSystem</c> waits on a live blast and counted the explosives a
        /// pack DECLARED, so any pack with one bomb in it produced a failure line five seconds into
        /// every world where nobody had set one off — which is every ordinary session;</item>
        /// <item><c>DimensionCustomTileCaptureSystem</c> waits on a SERIALIZED submap and counted
        /// the tileset registry, so a freshly generated world — which has nothing serialized yet —
        /// produced a failure line about terrain that was never at risk.</item>
        /// </list>
        /// <para>
        /// Their siblings keep their counts and are named here beside them, because the difference
        /// is the whole point: <c>DimensionCustomTileRestoreSystem</c> has no
        /// <c>RequireForUpdate</c> at all and <c>DimensionExplosiveHydrationSystem</c> waits on the
        /// object database, so both tick from the first frame and a zero on either really is a
        /// fault.
        /// </para>
        /// </remarks>
        [Test]
        public void ARowOnlyCountsWhatWouldHaveWokenItsSystem()
        {
            string[] wokenByAnEvent =
            {
                "DimensionBlastFireSystem",
                "DimensionCustomTileCaptureSystem",
            };
            string[] tickEveryFrame =
            {
                "DimensionCustomTileRestoreSystem",
                "DimensionExplosiveHydrationSystem",
            };

            Dictionary<string, DimensionSystemRoster.Row> byName =
                new Dictionary<string, DimensionSystemRoster.Row>(StringComparer.Ordinal);
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                byName[rows[i].Name] = rows[i];
            }

            for (int i = 0; i < wokenByAnEvent.Length; i++)
            {
                DimensionSystemRoster.Row row;
                Assert.IsTrue(
                    byName.TryGetValue(wokenByAnEvent[i], out row),
                    wokenByAnEvent[i] + " has no roster row, so this test has no subject.");
                Assert.IsFalse(
                    DimensionSelfAudit.WorthReportingAsNeverRan(row),
                    wokenByAnEvent[i] + " can be reported for never having run. It waits on "
                    + "something that happens in play, not on something a pack registers, so on an "
                    + "ordinary session it correctly never runs and this would be a failure line "
                    + "about nothing. Leave its CountWork null.");
            }

            int countable = 0;
            for (int i = 0; i < tickEveryFrame.Length; i++)
            {
                DimensionSystemRoster.Row row;
                Assert.IsTrue(
                    byName.TryGetValue(tickEveryFrame[i], out row),
                    tickEveryFrame[i] + " has no roster row, so this test has no subject.");
                Assert.IsNotNull(
                    row.CountWork,
                    tickEveryFrame[i] + " no longer counts what is waiting on it. It updates from "
                    + "the first frame, so a zero really is a fault and giving that up loses a real "
                    + "signal.");
                countable++;
            }

            Assert.Greater(
                countable,
                0,
                "No row was checked in the other direction, so a roster with every count removed "
                + "would pass this.");
        }

        /// <summary>
        /// The tile-rescue bracket is asked about in the world that captures, and nowhere else.
        /// </summary>
        /// <remarks>
        /// THE ONE FALSE ALARM THE DIAGNOSTICS WAVE INTRODUCED. The capture system was made
        /// <c>[WorldSystemFilter(ServerSimulation)]</c> and the client-side creation call was
        /// removed, both correctly — but this check was still run for every armed world, and
        /// <c>SerializationSystemGroup</c> exists in a client world. So on every client world it
        /// found the group, failed to find the capture system, and printed that custom blocks would
        /// be gone on the next load and that <c>EnsureSystemOrdering</c> had failed to run. Nothing
        /// was lost, and the fix it named had been deliberately deleted.
        /// </remarks>
        [Test]
        public void TheTileRescueBracketIsOnlyCheckedInTheWorldThatCapturesTiles()
        {
            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit"));
            Assert.IsFalse(
                string.IsNullOrEmpty(audit),
                "DimensionSelfAudit.cs is not in the shipped sources, so this proved nothing.");

            int method = audit.IndexOf(
                "private static void CheckTileRescueBracket", StringComparison.Ordinal);
            Assert.Greater(
                method,
                0,
                "CheckTileRescueBracket is gone, so this test has no subject.");

            int peer = audit.IndexOf("armed.IsClient", method, StringComparison.Ordinal);
            int group = audit.IndexOf(
                "GetExistingSystemManaged<SerializationSystemGroup>", method, StringComparison.Ordinal);

            Assert.Greater(
                peer,
                0,
                "The bracket check never asks which world it is in. The capture system is declared "
                + "for the server simulation only and SerializationSystemGroup exists in a client "
                + "world, so on every client world this reports that custom terrain will be lost "
                + "and names a repair call that was deliberately removed.");
            Assert.Less(
                peer,
                group,
                "The bracket check asks which world it is in only after it has already looked for "
                + "the group and the capture system, so the client-world finding is produced before "
                + "anything can stop it.");
        }

        [Test]
        public void EveryCompanionRuleCanActuallyFail()
        {
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            Assert.GreaterOrEqual(
                rules.Length,
                FewestPlausibleCompanionRules,
                "The companion table holds " + rules.Length + " rules. The entity check would walk "
                + "every object against almost nothing and report them all clean.");

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionQueryCompanionTable.Rule rule = rules[i];
                Assert.IsNotNull(rule.Trigger, "A rule has no trigger component.");
                Assert.IsNotNull(rule.Trigger.Present, rule.Trigger.Name + " has no presence check.");
                Assert.IsTrue(
                    seen.Add(rule.Trigger.Name + "" + rule.ReadingSystem),
                    "Two rules say " + rule.Trigger.Name + " is read by " + rule.ReadingSystem
                    + ", so one of them reports the same gap twice.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(rule.ReadingSystem),
                    rule.Trigger.Name + " names no system, so its message could not say what is "
                    + "not looking at the object.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(rule.VanillaExample) &&
                        !RulesWithNoVanillaExample.Contains(rule.Trigger.Name),
                    rule.Trigger.Name + " names no object of Core Keeper's own carrying the whole "
                    + "set, so a modder has nothing to compare against. If the game genuinely "
                    + "ships none, add it to RulesWithNoVanillaExample with the reason.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(rule.WhatBreaks),
                    rule.Trigger.Name + " does not say what a player would notice.");
                Assert.IsNotNull(rule.AlsoNeeds, rule.Trigger.Name + " requires nothing.");
                Assert.Greater(
                    rule.AlsoNeeds.Length,
                    0,
                    rule.Trigger.Name + " requires nothing beside itself, so the rule can never "
                    + "fail and its row is dead weight.");

                HashSet<string> inThisRule = new HashSet<string>(StringComparer.Ordinal);
                for (int n = 0; n < rule.AlsoNeeds.Length; n++)
                {
                    DimensionQueryCompanionTable.Need need = rule.AlsoNeeds[n];
                    Assert.IsNotNull(need.Present, rule.Trigger.Name + " -> " + need.Name
                        + " has no presence check.");
                    Assert.AreNotEqual(
                        rule.Trigger.Name,
                        need.Name,
                        rule.Trigger.Name + " lists itself as its own companion, so the rule can "
                        + "never fail.");
                    Assert.IsTrue(
                        inThisRule.Add(need.Name),
                        rule.Trigger.Name + " lists " + need.Name + " twice, so its message would "
                        + "name it twice.");
                }
            }
        }

        [Test]
        public void TheExemplarRuleIsInTheTable()
        {
            // The summoning circle is the case that exposed this whole bug class. If the rule for
            // it is ever dropped, the table has lost the thing it was written for.
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            DimensionQueryCompanionTable.Rule found = null;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].Trigger.Name == "SummonAreaCD")
                {
                    found = rules[i];
                    break;
                }
            }

            Assert.IsNotNull(
                found,
                "SummonAreaCD has no rule. BossSummoningSystem's query is the case this table was "
                + "written for.");
            Assert.AreEqual("BossSummoningSystem", found.ReadingSystem);

            List<string> needs = new List<string>();
            for (int i = 0; i < found.AlsoNeeds.Length; i++)
            {
                needs.Add(found.AlsoNeeds[i].Name);
            }

            CollectionAssert.Contains(needs, "NearbyEntitiesBufferCD");
            CollectionAssert.Contains(needs, "AnimationBuffer");
            CollectionAssert.Contains(needs, "AnimationBufferPointer");
        }

        /// <summary>
        /// Components a converter puts on essentially every object this framework generates.
        /// </summary>
        /// <remarks>
        /// A rule triggered on one of these asks its question of the whole subject list, so its
        /// consequence has to be true of the whole subject list — and the consequences in this table
        /// belong to one query each. The one that was written this way is the reason this list
        /// exists: <c>ObjectDataCD</c> comes off <c>ObjectConverter</c>, so a rule triggered on it
        /// looked at map pins and summoning circles and told their authors that fire does not touch
        /// them, on content that was exactly right, every session.
        /// </remarks>
        private static readonly HashSet<string> ComponentsEveryConvertedPrefabCarries =
            new HashSet<string>(StringComparer.Ordinal) { "ObjectDataCD", "LocalTransform" };

        [Test]
        public void NoRuleIsTriggeredByAComponentEveryConvertedPrefabCarries()
        {
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            Assert.GreaterOrEqual(
                rules.Length,
                FewestPlausibleCompanionRules,
                "The companion table is empty or nearly so, so this test walked nothing.");

            List<string> aimedAtEverything = new List<string>();
            for (int i = 0; i < rules.Length; i++)
            {
                if (ComponentsEveryConvertedPrefabCarries.Contains(rules[i].Trigger.Name))
                {
                    aimedAtEverything.Add(rules[i].Trigger.Name + " -> " + rules[i].ReadingSystem);
                }
            }

            Assert.IsEmpty(
                aimedAtEverything,
                "These rules are triggered by a component every generated object has, so they are "
                + "asked of every object the audit is handed and answered with a consequence that "
                + "belongs to one system's query: " + string.Join(", ", aimedAtEverything.ToArray())
                + ". Narrow the trigger to something that says the object was in that query's "
                + "scope in the first place.");
        }

        /// <summary>
        /// The kinds whose <c>ObjectTypeCD</c> arrives from the generator's finishing pass rather
        /// than from conditions support, by the component that says which kind it is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY THESE FOUR AND NOT THE OTHER FOUR. Eight kinds carry <c>ObjectTypeCD</c>. The
        /// environmental rule is triggered by <c>BurningConditionCD</c>, which only arrives with
        /// <c>SupportsConditionsAuthoring</c>, and only the container, creature, item and
        /// world-object generators add that. So the rule cannot fire on a crop, a critter, a
        /// station or a vehicle no matter what is wrong with one, and narrowing it — which was
        /// right — left those four with nothing watching the pairing at all.
        /// </para>
        /// <para>
        /// The names are the trigger names in the table, so a rule that is deleted, or one whose
        /// trigger is quietly swapped for something the kind does not carry, fails here.
        /// </para>
        /// </remarks>
        private static readonly string[] KindsWhoseTypeComesFromTheFinisher =
        {
            "GrowingCD", "CritterCD", "CraftingCD", "BoatCD, MinecartCD or VehicleCD"
        };

        [Test]
        public void EveryKindWhoseTypeComesFromTheFinisherIsGuarded()
        {
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            Assert.GreaterOrEqual(
                rules.Length,
                FewestPlausibleCompanionRules,
                "The companion table is empty or nearly so, so this test walked nothing.");

            List<string> unguarded = new List<string>();
            for (int k = 0; k < KindsWhoseTypeComesFromTheFinisher.Length; k++)
            {
                string kind = KindsWhoseTypeComesFromTheFinisher[k];
                bool guarded = false;
                for (int i = 0; i < rules.Length && !guarded; i++)
                {
                    if (rules[i].Trigger.Name != kind)
                    {
                        continue;
                    }

                    for (int n = 0; n < rules[i].AlsoNeeds.Length; n++)
                    {
                        if (rules[i].AlsoNeeds[n].Name == "ObjectTypeCD")
                        {
                            guarded = true;
                            break;
                        }
                    }
                }

                if (!guarded)
                {
                    unguarded.Add(kind);
                }
            }

            Assert.IsEmpty(
                unguarded,
                "Nothing in the table would notice if these kinds stopped carrying ObjectTypeCD: "
                + string.Join(", ", unguarded.ToArray())
                + ". They get it from their generator's finishing pass and never get a condition "
                + "buffer, so the BurningConditionCD rule cannot reach them and their own rows are "
                + "the only thing watching.");
        }

        [Test]
        public void TheAuditIsArmedRunAndForgottenByTheModEntry()
        {
            // A file that compiles and nothing calls is this project's recurring failure, and an
            // audit nothing calls is the worst instance of it: everything below would still pass.
            //
            // THE COMMENTS ARE STRIPPED FIRST, and that is not tidiness. Every assertion here is a
            // substring search, so a commented-out Arm call satisfied all of them — the test that
            // exists to prove the audit is wired up passed on an audit nothing called. Measured on
            // the tree, not supposed.
            string entry = WithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("ExpandNullforgeModEntry.cs"));
            StringAssert.Contains(
                "DimensionSelfAudit.Arm(world, false)",
                entry,
                "The server world is never armed, so the audit never runs on it.");
            StringAssert.Contains(
                "DimensionSelfAudit.Arm(world, true)",
                entry,
                "The client world is never armed, so the audit never runs on it.");
            StringAssert.Contains(
                "DimensionSelfAudit.Update()",
                entry,
                "Nothing drives the audit, so an armed world is never audited.");
            StringAssert.Contains(
                "DimensionSelfAudit.Forget(",
                entry,
                "A destroyed world is never forgotten, so the audit would keep a dead world in "
                + "its list.");
            StringAssert.Contains(
                "DimensionSelfAudit.Reset()",
                entry,
                "Shutdown does not reset the audit, so a reloaded mod would inherit the previous "
                + "session's verdict.");
        }

        [Test]
        public void TheBundleCheckIsDrivenByTheAuditAndDoesNotDependOnTheAuditSwitch()
        {
            string audit = DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit");
            StringAssert.Contains(
                "DimensionModBundleDiagnostics.ReportMultiBundleModsOnce()",
                audit,
                "Nothing asks the bundle question at all now, so a mod that shipped two asset "
                + "bundles would lose the second one's data silently.");

            // IT MUST SIT ABOVE THE AUDIT'S OWN EARLY RETURN. Two mods each shipping two asset
            // bundles is a packaging fault whether or not somebody passed -nfnoaudit; behind the
            // switch, a diagnostics setting silently took a correctness check away with it. The
            // call latches after its first successful run, so asking it from a per-frame method
            // costs two null checks.
            int callAt = audit.IndexOf(
                "DimensionModBundleDiagnostics.ReportMultiBundleModsOnce()",
                StringComparison.Ordinal);
            int switchAt = audit.IndexOf("!DimensionLogConfig.Audit", StringComparison.Ordinal);
            Assert.Greater(
                switchAt,
                0,
                "The audit no longer reads its own switch, so this comparison proves nothing.");
            Assert.Less(
                callAt,
                switchAt,
                "The bundle check sits below the audit's switch, so -nfnoaudit takes an unrelated "
                + "correctness check away with it.");
        }

        [Test]
        public void TheAuditDoesNotDriveTheHarmonyPatcherOrReadTypesByName()
        {
            // The sandbox denies HarmonyLib.Harmony and all of System.Reflection. The two guards
            // already scan for that across the shipped set; this says it about the audit in
            // particular, because the diagnostics plan proposed Harmony.GetPatchInfo and this is
            // the file somebody would put it in.
            //
            // COMMENTS ARE STRIPPED FIRST, and this one was failing without it: DimensionPatchRoster
            // explains in its own remarks WHY it cannot call Harmony.GetPatchInfo, and naming the
            // thing you are not doing is exactly what a doc comment is for. The question is whether
            // the code reaches for it, not whether the file says the words.
            string[] files =
            {
                "DimensionSelfAudit.cs",
                "DimensionSystemRoster.cs",
                "DimensionPatchRoster.cs",
                "DimensionQueryCompanionTable.cs",
                "DimensionEntityAudit.cs",
            };

            for (int i = 0; i < files.Length; i++)
            {
                string source = DimensionFrameworkSourceScanner.ReadPartials(
                    System.IO.Path.GetFileNameWithoutExtension(files[i]));
                Assert.IsNotNull(source, files[i] + " is not in the shipped sources.");
                source = WithoutComments(source);
                StringAssert.DoesNotContain(
                    "using System.Reflection",
                    source,
                    files[i] + " uses System.Reflection, which the mod sandbox rejects. The whole "
                    + "mod fails to load with \"Compilation failed\" and no line number.");
                StringAssert.DoesNotContain(
                    "Harmony.GetPatchInfo",
                    source,
                    files[i] + " asks Harmony what it bound. HarmonyLib.Harmony is a denied type: "
                    + "declaring a patch is allowed, driving the patcher is not.");
                StringAssert.DoesNotContain(
                    "AccessTools",
                    source,
                    files[i] + " uses HarmonyLib.AccessTools, which is a denied type.");
            }
        }
    }
}
#endif
