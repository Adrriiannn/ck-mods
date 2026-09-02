#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The localization table is shared with the creator: it holds their hand-written
    /// translations and rows from other generators. Regeneration must replace only what this
    /// generator owns and leave everything else exactly as it was.
    /// </summary>
    /// <remarks>
    /// KEYS ARE WRITTEN WITH '_', NEVER ':'. An object is named <c>mod:blade</c>, but Core Keeper's
    /// LocalizationManager applies <c>Term.Replace(':', '_')</c> to every term before it searches,
    /// so a row stored under <c>Items/mod:blade</c> can never be found. Every assertion below is
    /// written against the lookup form for that reason — an assertion against the colon
    /// form is one no amount of correct code could satisfy.
    /// </remarks>
    internal sealed class DimensionLocalizationCsvTests
    {
        [Test]
        public void AnEmptyTable_GetsAHeaderAndTheGeneratedRows()
        {
            string merged = Regenerate(null, "mod:blade", "Blade", "Sharp.");

            string[] lines = Lines(merged);
            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/mod_blade\tText\t\tBlade"));
            Assert.That(lines, Does.Contain("Items/mod_bladeDesc\tText\t\tSharp."));
        }

        [Test]
        public void RegeneratingReplacesOwnedRowsInsteadOfDuplicatingThem()
        {
            string first = Regenerate(null, "mod:blade", "Blade", "Sharp.");
            string second = Regenerate(first, "mod:blade", "Great Blade", "Very sharp.");

            string[] lines = Lines(second);
            Assert.That(CountStartingWith(lines, "Items/mod_blade\t"), Is.EqualTo(1));
            Assert.That(CountStartingWith(lines, "Items/mod_bladeDesc\t"), Is.EqualTo(1));
            Assert.That(lines, Does.Contain("Items/mod_blade\tText\t\tGreat Blade"));
            Assert.That(second, Does.Not.Contain("Sharp."));
        }

        /// <summary>
        /// A table generated before object names were namespaced holds colon-form keys. Those are
        /// ours, they are unreachable by the game's lookup, and regeneration must retire them —
        /// otherwise every rebuild leaves another dead row behind and the file grows forever.
        /// </summary>
        [Test]
        public void ColonFormKeysFromAnOlderGeneration_AreRetiredNotKeptAsForeign()
        {
            string existing =
                "Key\tType\tDesc\tEnglish\n" +
                "Items/mod:blade\tText\t\tOld Blade\n" +
                "Items/mod:bladeDesc\tText\t\tOld and blunt.\n";

            string merged = Regenerate(existing, "mod:blade", "Blade", "Sharp.");

            string[] lines = Lines(merged);
            Assert.That(
                CountStartingWith(lines, "Items/mod:"), Is.EqualTo(0),
                "A colon-form key the game can never resolve must not survive regeneration.");
            Assert.That(lines, Does.Contain("Items/mod_blade\tText\t\tBlade"));
            Assert.That(lines, Does.Contain("Items/mod_bladeDesc\tText\t\tSharp."));
        }

        [Test]
        public void ForeignRowsAndOtherLanguagesArePreserved()
        {
            // A creator's existing table with a fifth column of hand-written French.
            string existing =
                "Key\tType\tDesc\tEnglish\tFrench\n" +
                "Items/other_mod_thing\tText\t\tThing\tChose\n" +
                "Items/mod_blade\tText\t\tOld Blade\tVieille Lame\n";

            string merged = Regenerate(existing, "mod:blade", "Blade", "Sharp.");

            string[] lines = Lines(merged);
            Assert.That(
                lines,
                Does.Contain("Items/other_mod_thing\tText\t\tThing\tChose"),
                "Another mod's row must survive regeneration.");
            Assert.That(lines[0], Is.EqualTo("Key\tType\tDesc\tEnglish\tFrench"));

            // Our own row is rewritten, and keeps the table's column count.
            Assert.That(lines, Does.Contain("Items/mod_blade\tText\t\tBlade\t"));
        }

        [Test]
        public void ABomAndCrlfTableIsHandled()
        {
            string existing =
                "﻿Key\tType\tDesc\tEnglish\r\nItems/keep_me\tText\t\tKeep\r\n";

            string merged = Regenerate(existing, "mod:blade", "Blade", "Sharp.");

            string[] lines = Lines(merged);
            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/keep_me\tText\t\tKeep"));
        }

        [Test]
        public void ATableMissingItsHeaderGetsOne()
        {
            string existing = "Items/keep_me\tText\t\tKeep\n";

            string[] lines = Lines(Regenerate(existing, "mod:blade", "Blade", "Sharp."));

            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/keep_me\tText\t\tKeep"));
        }

        [Test]
        public void TabsAndNewlinesInTextCannotBreakTheRowLayout()
        {
            string[] lines = Lines(Regenerate(null, "mod:blade", "Bla\tde", "Line one\nLine two"));

            for (int i = 0; i < lines.Length; i++)
            {
                Assert.That(
                    lines[i].Split('\t').Length,
                    Is.EqualTo(4),
                    "Row " + i + " has the wrong column count: " + lines[i]);
            }

            Assert.That(lines, Does.Contain("Items/mod_blade\tText\t\tBla de"));
            Assert.That(lines, Does.Contain("Items/mod_bladeDesc\tText\t\tLine one Line two"));
        }

        [Test]
        public void AnItemWithNoDescriptionStillGetsAWellFormedRow()
        {
            string[] lines = Lines(Regenerate(null, "mod:blade", "Blade", null));

            Assert.That(lines, Does.Contain("Items/mod_bladeDesc\tText\t\t"));
        }

        [Test]
        public void BlankLinesAreDropped()
        {
            string existing = "Key\tType\tDesc\tEnglish\n\n\nItems/keep_me\tText\t\tKeep\n\n";

            string[] lines = Lines(Regenerate(existing, "a", "A", "d"));

            for (int i = 0; i < lines.Length; i++)
            {
                Assert.That(string.IsNullOrWhiteSpace(lines[i]), Is.False);
            }
        }

        /// <summary>
        /// One regeneration, shaped exactly like <c>DimensionItemGenerator</c>'s: rows AND the
        /// retirement list, threaded into the same <c>Merge</c> overload. Calling the shorter
        /// overloads here would test a path production never takes — which is how the colon-form
        /// rows went unnoticed.
        /// </summary>
        private static string Regenerate(
            string existing,
            string objectName,
            string displayName,
            string description)
        {
            List<DimensionLocalizationCsv.Row> rows = new List<DimensionLocalizationCsv.Row>();
            List<string> retired = new List<string>();
            DimensionLocalizationCsv.AddItemRows(rows, objectName, displayName, description, retired);
            return DimensionLocalizationCsv.Merge(existing, rows, retired);
        }

        private static string[] Lines(string content)
        {
            return content.TrimEnd('\n').Split('\n');
        }

        private static int CountStartingWith(string[] lines, string prefix)
        {
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
#endif
