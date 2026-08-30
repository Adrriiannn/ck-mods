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
    public sealed class DimensionSelfAuditTests
    {
        /// <summary>
        /// The fewest systems this framework can plausibly have.
        /// </summary>
        /// <remarks>
        /// Set below the real count so ordinary work never trips it and far above zero so an empty
        /// roster cannot pass. Raise it when the framework genuinely grows; never lower it to make
        /// a run go green.
        /// </remarks>
        private const int FewestPlausibleSystems = 20;

        /// <summary>The fewest Harmony patches this framework can plausibly have.</summary>
        private const int FewestPlausiblePatches = 20;

        /// <summary>The fewest companion rules the entity check can be worth running with.</summary>
        private const int FewestPlausibleCompanionRules = 20;

        /// <summary>
        /// Triggers for which Core Keeper genuinely ships no object carrying the whole set.
        /// </summary>
        /// <remarks>
        /// An entry costs a name and a reason, because an empty vanilla example is normally a row
        /// somebody did not finish. <c>BeamAttackStateCD</c> is here because the game ships no
        /// prefab with a beam attack at all and nothing in it fills <c>BeamBuffer</c>; the rule's
        /// own text says so. It used to carry the sentence "no vanilla prefab carries a beam
        /// attack" in the example field, which passed this assertion and made the message read
        /// "Core Keeper's own no vanilla prefab carries a beam attack carries the whole set."
        /// </remarks>
        private static readonly HashSet<string> RulesWithNoVanillaExample =
            new HashSet<string>(StringComparer.Ordinal) { "BeamAttackStateCD" };

        [Test]
        public void TheSystemRosterNamesEverySystemTheFrameworkHas()
        {
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            Assert.GreaterOrEqual(
                rows.Length,
                FewestPlausibleSystems,
                "The system roster holds " + rows.Length + " rows. Every check that walks it would "
                + "report nothing wrong having looked at almost nothing.");

            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.IsTrue(
                    named.Add(rows[i].Name),
                    "Two roster rows name " + rows[i].Name + ", so one of them is never reached.");
            }

            HashSet<string> inSource = SystemsInTheSources();
            Assert.GreaterOrEqual(
                inSource.Count,
                FewestPlausibleSystems,
                "The scan of the shipped sources found " + inSource.Count + " classes deriving "
                + "from SystemBase, so comparing the roster against it proves nothing.");

            List<string> missing = new List<string>();
            foreach (string name in inSource)
            {
                if (!named.Contains(name))
                {
                    missing.Add(name);
                }
            }

            Assert.IsEmpty(
                missing,
                "These derive from SystemBase and have no row in DimensionSystemRoster, so the "
                + "audit would never notice them missing from a world: " + string.Join(", ", missing.ToArray()));

            List<string> invented = new List<string>();
            foreach (string name in named)
            {
                if (!inSource.Contains(name))
                {
                    invented.Add(name);
                }
            }

            Assert.IsEmpty(
                invented,
                "These have a roster row and no such system in the sources, so the audit would "
                + "report them missing from every world: " + string.Join(", ", invented.ToArray()));
        }

        [Test]
        public void EverySystemRowSaysWhatStopsWorkingAndHowToFindIt()
        {
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            Assert.GreaterOrEqual(rows.Length, FewestPlausibleSystems, "The roster is too small to "
                + "be the real one.");

            for (int i = 0; i < rows.Length; i++)
            {
                DimensionSystemRoster.Row row = rows[i];
                Assert.IsNotNull(
                    row.Find,
                    row.Name + " has no lookup, so the audit cannot tell whether it exists.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(row.Capability),
                    row.Name + " has no capability sentence. Its failure line would say a system "
                    + "did not run and nothing about what a player would notice.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(row.WorkName),
                    row.Name + " does not name what would be waiting on it, so its failure line "
                    + "cannot say whether the failure matters to this pack.");
                Assert.IsFalse(
                    row.Capability.EndsWith(".", StringComparison.Ordinal),
                    row.Name + "'s capability reads as a sentence of its own; the audit puts it "
                    + "inside one, so it must not carry its own full stop.");
            }
        }

        [Test]
        public void ThePatchRosterNamesEveryHarmonyPatchTheFrameworkDeclares()
        {
            DimensionPatchRoster.Row[] rows = DimensionPatchRoster.All;
            Assert.GreaterOrEqual(
                rows.Length,
                FewestPlausiblePatches,
                "The patch roster holds " + rows.Length + " rows, which cannot be all of them.");

            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.IsTrue(
                    named.Add(rows[i].PatchClass),
                    "Two roster rows name " + rows[i].PatchClass + ".");
            }

            HashSet<string> inSource = HarmonyPatchClassesInTheSources();
            Assert.GreaterOrEqual(
                inSource.Count,
                FewestPlausiblePatches,
                "The scan of the shipped sources found " + inSource.Count + " classes carrying "
                + "[HarmonyPatch], so comparing the roster against it proves nothing.");

            List<string> missing = new List<string>();
            foreach (string name in inSource)
            {
                if (!named.Contains(name))
                {
                    missing.Add(name);
                }
            }

            Assert.IsEmpty(
                missing,
                "These declare a Harmony patch and have no row in DimensionPatchRoster, so nothing "
                + "would ever notice them never running: " + string.Join(", ", missing.ToArray()));

            List<string> invented = new List<string>();
            foreach (string name in named)
            {
                if (!inSource.Contains(name))
                {
                    invented.Add(name);
                }
            }

            Assert.IsEmpty(
                invented,
                "These have a patch roster row and declare no Harmony patch: "
                + string.Join(", ", invented.ToArray()));
        }

        [Test]
        public void EveryPatchCarriesTheCounterTheAuditReads()
        {
            // THE COUNTER IS THE ONLY EVIDENCE THERE IS. The mod sandbox denies HarmonyLib.Harmony,
            // so the framework cannot ask the patcher what it bound; a patch with no increment is a
            // patch the audit will always call dead.
            HashSet<string> classes = HarmonyPatchClassesInTheSources();
            Assert.GreaterOrEqual(
                classes.Count,
                FewestPlausiblePatches,
                "Found " + classes.Count + " patch classes, so this proves nothing.");

            List<string> withoutAField = new List<string>();
            List<string> withoutAnIncrement = new List<string>();
            foreach (string name in classes)
            {
                string body = BodyOfClass(name);
                Assert.IsNotNull(body, "Could not read the body of " + name + ".");
                if (body.IndexOf("internal static int Fired;", StringComparison.Ordinal) < 0)
                {
                    withoutAField.Add(name);
                }

                if (!Regex.IsMatch(body, @"^\s*Fired\+\+;\s*$", RegexOptions.Multiline))
                {
                    withoutAnIncrement.Add(name);
                }
            }

            Assert.IsEmpty(
                withoutAField,
                "These patch classes have no Fired counter, so the audit cannot tell whether they "
                + "ever ran: " + string.Join(", ", withoutAField.ToArray()));
            Assert.IsEmpty(
                withoutAnIncrement,
                "These patch classes declare a Fired counter and never increment it, so the audit "
                + "will report them dead in every session: "
                + string.Join(", ", withoutAnIncrement.ToArray()));
        }

        [Test]
        public void EveryPatchRowSaysWhatItPatchesAndWhatBreaks()
        {
            DimensionPatchRoster.Row[] rows = DimensionPatchRoster.All;
            Assert.GreaterOrEqual(rows.Length, FewestPlausiblePatches, "The roster is too small.");

            for (int i = 0; i < rows.Length; i++)
            {
                DimensionPatchRoster.Row row = rows[i];
                Assert.IsNotNull(row.Fired, row.PatchClass + " has no counter to read.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(row.Target),
                    row.PatchClass + " does not say which game method it is declared against.");
                Assert.IsFalse(
                    string.IsNullOrEmpty(row.WhatBreaks),
                    row.PatchClass + " does not say what breaks when it never runs, so its failure "
                    + "line would name a class and no consequence.");
                Assert.AreEqual(
                    0,
                    row.Fired(),
                    row.PatchClass + " reports " + row.Fired() + " runs outside a running game, so "
                    + "its counter is being incremented by something other than the patch.");
            }
        }

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
                DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs"));
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

        [Test]
        public void APatchTargetOnlyOneSideOfTheGameRunsSaysWhichSide()
        {
            // Harmony patching is process-wide and the targets are not. Without this column a
            // dedicated server reported the music, ambience and region-title patches as broken —
            // there is no GameMusicHandler on a headless server — and a player joined to somebody
            // else's game got the same about the dungeon and ambient-spawn patches.
            DimensionPatchRoster.Row[] rows = DimensionPatchRoster.All;
            Assert.GreaterOrEqual(
                rows.Length,
                FewestPlausiblePatches,
                "The patch roster is empty or nearly so, so this test walked nothing.");

            int oneSided = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Where != DimensionSystemRoster.Peer.Both)
                {
                    oneSided++;
                }

                Assert.IsFalse(
                    rows[i].Where != DimensionSystemRoster.Peer.Both && rows[i].OnlyOnPlayerAction,
                    rows[i].PatchClass + " is marked for one side of the game and is also marked "
                    + "player-action-only, so the side it names has no effect: a player-action row "
                    + "is never reported either way. One of the two marks is wrong.");
            }

            Assert.Greater(
                oneSided,
                0,
                "Not one patch row names a side of the game, so the column is decorative and the "
                + "patch pass reports every row in every kind of session again.");
        }

        [Test]
        public void ThePatchPassAsksWhichSideOfTheGameThisProcessRunsBeforeReporting()
        {
            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs"));
            Assert.IsFalse(
                string.IsNullOrEmpty(audit),
                "DimensionSelfAudit.cs is not in the shipped sources, so this proved nothing.");

            int method = audit.IndexOf(
                "private static void RunPatchPass", StringComparison.Ordinal);
            Assert.Greater(method, 0, "RunPatchPass is gone, so this test has no subject.");

            int peer = audit.IndexOf("ThisProcessHasAWorldFor(", method, StringComparison.Ordinal);
            int report = audit.IndexOf("DimensionLog.Problem(", method, StringComparison.Ordinal);

            Assert.Greater(
                peer,
                0,
                "The patch pass never asks which worlds this process has, so a patch whose target "
                + "lives on the side of the game this session does not run is reported as never "
                + "having run.");
            Assert.Less(
                peer,
                report,
                "The patch pass asks which worlds this process has only after it has already "
                + "composed a failure, so the finding is produced before anything can stop it.");
        }

        [Test]
        public void TheGrowthCheckLeavesOutARegistryThatPlayingRaises()
        {
            // The finding it produces says a content pack registered itself twice. That reading is
            // only available for a registry nothing but a declaration writes.
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            int countable = 0;
            int excluded = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].CountWork == null)
                {
                    continue;
                }

                countable++;
                if (rows[i].CountGrowsDuringPlay)
                {
                    excluded++;
                }
            }

            Assert.Greater(
                countable,
                0,
                "No roster row can be counted at all, so the growth check has nothing to compare "
                + "and this test walked nothing.");
            Assert.Greater(
                excluded,
                0,
                "Not one countable registry is marked as one that playing raises. At least one is: "
                + "the armed-trap registry is filled as a dimension generates and nothing clears "
                + "it between worlds, so the second world of a session sees a bigger number for an "
                + "ordinary reason.");
            Assert.Greater(
                countable - excluded,
                0,
                "Every countable registry is excluded, so the growth check compares nothing and "
                + "can never say anything.");

            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs"));
            StringAssert.Contains(
                "CountGrowsDuringPlay",
                audit,
                "The roster carries the answer and the growth check does not read it, so the "
                + "exclusion above changes nothing.");
        }

        [Test]
        public void AWorldSaysWhichOfTheseChecksWereNotAskedOfIt()
        {
            // "Found nothing wrong" is a claim about what was looked at. Two of the registration
            // pass's checks are asked of a server world only, both skip with a Trace, and traces
            // are off unless somebody turns their channel on — so the session a player actually
            // has, joining somebody else's server, read as clean with two checks unrun.
            Assert.IsEmpty(
                DimensionSelfAudit.WhatWasNotAskedOf(false),
                "A server world is asked everything, so its summary should carry no caveat.");

            string clause = DimensionSelfAudit.WhatWasNotAskedOf(true);
            Assert.IsNotEmpty(
                clause,
                "A client world's summary says nothing about the two checks that were not asked "
                + "of it, so it reads exactly like a world that passed both.");
            StringAssert.Contains(
                "server world only",
                clause,
                "The caveat does not say why those checks were not asked, so a reader cannot tell "
                + "it from a failure.");

            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs"));
            Assert.AreEqual(
                2,
                CountOccurrences(audit, "WhatWasNotAskedOf(armed.IsClient)"),
                "The caveat is spliced into one of the two summary lines and not the other, so "
                + "whichever one it is missing from claims more than it checked.");
        }

        [Test]
        public void NoPrintedLineClaimsWhatThisProjectHasEverWatched()
        {
            // A constant cannot make a claim about the past. The one that used to sit here said
            // the server-world reading had never been confirmed by anybody, on every server world
            // in every session, and no run could retire it — including a run by the person who had
            // just watched one.
            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs"));
            Assert.IsFalse(
                string.IsNullOrEmpty(audit),
                "DimensionSelfAudit.cs is not in the shipped sources, so this proved nothing.");

            string[] claims =
            {
                "first world of its kind",
                "the evidence that the game schedules",
                "nobody has yet watched",
                "has never been watched",
            };

            for (int i = 0; i < claims.Length; i++)
            {
                Assert.IsFalse(
                    audit.Contains(claims[i]),
                    "An emitted string says \"" + claims[i] + "\". That is a statement about how "
                    + "much of this framework anybody has tested, not about the world in front of "
                    + "the reader: it is identical on every run and nothing here can retire it. "
                    + "Record it in a comment instead.");
            }
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0;
            int at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }

            return count;
        }

        [Test]
        public void AFailureSentenceNamesTheThingTheGapAndTheFix()
        {
            DimensionQueryCompanionTable.Rule rule = null;
            DimensionQueryCompanionTable.Rule[] rules = DimensionQueryCompanionTable.All;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].Trigger.Name == "SummonAreaCD")
                {
                    rule = rules[i];
                    break;
                }
            }

            Assert.IsNotNull(rule, "The exemplar rule is missing, so this test has no subject.");

            DimensionEntityAudit.Finding finding = new DimensionEntityAudit.Finding();
            finding.ItemId = "EmberLarva";
            finding.ObjectId = 42117;
            finding.Rule = rule;
            finding.Missing = new List<string>
            {
                "NearbyEntitiesBufferCD", "AnimationBuffer", "AnimationBufferPointer",
            };
            finding.Present = new List<string>();

            string sentence = DimensionEntityAudit.Describe(finding);

            // The plan's own standard: name the thing, what is missing, and what to do about it.
            StringAssert.Contains("EmberLarva", sentence);
            StringAssert.Contains("42117", sentence);
            StringAssert.Contains("SummonAreaCD", sentence);
            StringAssert.Contains("BossSummoningSystem", sentence);
            StringAssert.Contains("NearbyEntitiesBufferCD", sentence);
            StringAssert.Contains("AnimationBuffer", sentence);
            StringAssert.Contains("AnimationBufferPointer", sentence);
            StringAssert.Contains("LarvaBossSummonAreaEntity", sentence);
            StringAssert.Contains("Fix:", sentence);
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
            string audit = DimensionFrameworkSourceScanner.ReadByName("DimensionSelfAudit.cs");
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
                string source = DimensionFrameworkSourceScanner.ReadByName(files[i]);
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

        // ------------------------------------------------------------------------- source scan ---

        /// <summary>
        /// The same source with every comment blanked out, so a substring search cannot match one.
        /// </summary>
        /// <remarks>
        /// Lines rather than a parser: it removes a <c>//</c> run to the end of its line and drops
        /// whole lines inside a block comment. It does not understand a <c>//</c> inside a string
        /// literal, which would blank the rest of that line — harmless here, because nothing this
        /// test looks for is written inside a string.
        /// </remarks>
        private static string WithoutComments(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            string[] lines = source.Split('\n');
            StringBuilder kept = new StringBuilder();
            bool inBlock = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (inBlock)
                {
                    int close = line.IndexOf("*/", StringComparison.Ordinal);
                    if (close < 0)
                    {
                        kept.Append('\n');
                        continue;
                    }

                    inBlock = false;
                    line = line.Substring(close + 2);
                }

                int open = line.IndexOf("/*", StringComparison.Ordinal);
                if (open >= 0 && line.IndexOf("*/", open, StringComparison.Ordinal) < 0)
                {
                    inBlock = true;
                    line = line.Substring(0, open);
                }

                int slashes = line.IndexOf("//", StringComparison.Ordinal);
                if (slashes >= 0)
                {
                    line = line.Substring(0, slashes);
                }

                kept.Append(line).Append('\n');
            }

            return kept.ToString();
        }

        private static HashSet<string> SystemsInTheSources()
        {
            return NamesMatching(
                new Regex(@"class\s+(Dimension\w+)\s*:\s*SystemBase"),
                onlyRuntime: true);
        }

        private static HashSet<string> HarmonyPatchClassesInTheSources()
        {
            return NamesMatching(
                new Regex(
                    @"\[HarmonyPatch\([^\r\n]*\)\]\s*(?:\r?\n\s*)*(?:public|internal)\s+static\s+class\s+(\w+)"),
                onlyRuntime: true);
        }

        private static HashSet<string> NamesMatching(Regex pattern, bool onlyRuntime)
        {
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            List<string> files = DimensionFrameworkSourceScanner.SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                if (onlyRuntime && !IsRuntimeFile(files[i]))
                {
                    continue;
                }

                string text = System.IO.File.ReadAllText(files[i]);
                foreach (Match match in pattern.Matches(text))
                {
                    found.Add(match.Groups[1].Value);
                }
            }

            return found;
        }

        private static bool IsRuntimeFile(string path)
        {
            // Scripts/ ships; Editor/ and CodeGen/ do not. The audit only ever sees the shipped set.
            return path.Replace('\\', '/').IndexOf("/Scripts/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BodyOfClass(string className)
        {
            List<string> files = DimensionFrameworkSourceScanner.SourceFiles();
            Regex declaration = new Regex(
                @"(?:public|internal)\s+static\s+class\s+" + Regex.Escape(className) + @"\s*$",
                RegexOptions.Multiline);

            for (int i = 0; i < files.Count; i++)
            {
                if (!IsRuntimeFile(files[i]))
                {
                    continue;
                }

                string text = System.IO.File.ReadAllText(files[i]);
                Match match = declaration.Match(text);
                if (!match.Success)
                {
                    continue;
                }

                return BracedBodyFrom(text, match.Index + match.Length);
            }

            return null;
        }

        /// <summary>
        /// The text between the first brace after <paramref name="from"/> and its partner.
        /// </summary>
        /// <remarks>
        /// Counting braces is enough here because the subjects are patch classes: small, with no
        /// string literal or comment in them that carries an unbalanced brace. A reader adding one
        /// would see this test start naming the wrong class rather than pass wrongly.
        /// </remarks>
        private static string BracedBodyFrom(string text, int from)
        {
            int open = text.IndexOf('{', from);
            if (open < 0)
            {
                return null;
            }

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(open, i - open + 1);
                    }
                }
            }

            return null;
        }
    }
}
#endif
