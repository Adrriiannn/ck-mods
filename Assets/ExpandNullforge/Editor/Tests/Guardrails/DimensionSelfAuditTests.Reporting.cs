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
    /// What the audit is allowed to print, and what it must say it did not look at.
    /// </summary>
    public sealed partial class DimensionSelfAuditTests
    {
        [Test]
        public void TheObjectCheckSaysWhatItDidNotLookAt()
        {
            // The case is a content pack built before the framework kept a list of everything it
            // generated: its items resolve, the wider list is empty, and a summary that ends
            // "all of them carry what the systems that read them require" would be the failure.
            DimensionEntityAudit.Result itemsOnly = new DimensionEntityAudit.Result();
            itemsOnly.ResolvedItems = 2;
            itemsOnly.ItemSubjects = 2;
            itemsOnly.LedgerSubjects = 0;
            itemsOnly.Checked = 2;
            itemsOnly.RulesInTable = DimensionQueryCompanionTable.All.Length;
            itemsOnly.RulesApplied = 1;
            itemsOnly.WithoutAPrefab = new List<string>();
            itemsOnly.Findings = new List<DimensionEntityAudit.Finding>();

            Assert.GreaterOrEqual(
                itemsOnly.RulesInTable,
                FewestPlausibleCompanionRules,
                "The companion table is empty, so the sentence under test has nothing to be a "
                + "summary of.");

            string said = DimensionSelfAudit.WhatTheObjectCheckCoveredAndDidNot(
                itemsOnly,
                "Server world");

            StringAssert.Contains(
                "NOT LOOKED AT",
                said,
                "The summary of an items-only walk does not say that is what it was: " + said);
            StringAssert.Contains(
                "2 items and nothing else",
                said,
                "It does not say how narrow the subject list was: " + said);
            StringAssert.DoesNotContain(
                "all of them carry what the systems that read them require",
                said,
                "The old unqualified verdict is back: " + said);
            StringAssert.Contains(
                "1 of them applied",
                said,
                "It reports the table's length without saying how much of it was a test of "
                + "anything: " + said);
        }

        [Test]
        public void TheObjectCheckWillNotCallAWalkCleanWhenNoRuleApplied()
        {
            // Every rule passing because none of them asked anything is the same shape as an empty
            // subject list, and it produced the same congratulation.
            DimensionEntityAudit.Result nothingApplied = new DimensionEntityAudit.Result();
            nothingApplied.ResolvedItems = 4;
            nothingApplied.ItemSubjects = 1;
            nothingApplied.LedgerSubjects = 3;
            nothingApplied.Checked = 4;
            nothingApplied.RulesInTable = DimensionQueryCompanionTable.All.Length;
            nothingApplied.RulesApplied = 0;
            nothingApplied.WithoutAPrefab = new List<string>();
            nothingApplied.Findings = new List<DimensionEntityAudit.Finding>();

            Assert.GreaterOrEqual(
                nothingApplied.RulesInTable,
                FewestPlausibleCompanionRules,
                "The companion table is empty, so the sentence under test has nothing to be a "
                + "summary of.");

            string said = DimensionSelfAudit.WhatTheObjectCheckCoveredAndDidNot(
                nothingApplied,
                "Server world");

            StringAssert.Contains(
                "not as a clean result",
                said,
                "A walk where no rule applied to anything is still reported as a pass: " + said);
            StringAssert.DoesNotContain(
                "was there",
                said,
                "It claims the objects satisfied something when no rule looked at them: " + said);
        }

        [Test]
        public void TheObjectCheckKeepsQuietAboutScopeWhenTheListIsWhole()
        {
            // The caveat has to be worth reading, which means it cannot be on every line.
            DimensionEntityAudit.Result whole = new DimensionEntityAudit.Result();
            whole.ResolvedItems = 12;
            whole.ItemSubjects = 4;
            whole.LedgerSubjects = 8;
            whole.Checked = 12;
            whole.RulesInTable = DimensionQueryCompanionTable.All.Length;
            whole.RulesApplied = 6;
            whole.WithoutAPrefab = new List<string>();
            whole.Findings = new List<DimensionEntityAudit.Finding>();

            Assert.Greater(
                whole.LedgerSubjects,
                0,
                "This test is about a subject list that is not items-only, so an empty one would "
                + "make it assert nothing.");

            string said = DimensionSelfAudit.WhatTheObjectCheckCoveredAndDidNot(whole, "Server world");

            StringAssert.DoesNotContain(
                "NOT LOOKED AT",
                said,
                "A pack whose whole generated list was in front of the check is told it was not: "
                + said);
            StringAssert.Contains(
                "6 of them applied",
                said,
                "It does not say how much of the table was a test of anything: " + said);
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
                DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit"));
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
                DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit"));
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
                DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit"));
            Assert.AreEqual(
                2,
                CountOccurrences(audit, "WhatWasNotAskedOf(armed.IsClient)"),
                "The caveat is spliced into one of the two summary lines and not the other, so "
                + "whichever one it is missing from claims more than it checked.");
        }

        [Test]
        public void NoPrintedLineClaimsWhatThisProjectHasEverWatched()
        {
            // A constant cannot make a claim about the past. One saying the server-world reading
            // has never been confirmed by anybody would say it on every server world
            // in every session, and no run could retire it — including a run by the person who has
            // just watched one.
            string audit = WithoutComments(
                DimensionFrameworkSourceScanner.ReadPartials("DimensionSelfAudit"));
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
    }
}
#endif
