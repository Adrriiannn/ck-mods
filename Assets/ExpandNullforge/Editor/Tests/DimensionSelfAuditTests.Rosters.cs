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
    /// That the roster names every system and patch the framework actually has.
    /// </summary>
    public sealed partial class DimensionSelfAuditTests
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
    }
}
#endif
