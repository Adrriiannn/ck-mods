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
    internal sealed class DimensionLocalizationCsvTests
    {
        [Test]
        public void AnEmptyTable_GetsAHeaderAndTheGeneratedRows()
        {
            string merged = DimensionLocalizationCsv.Merge(null, Rows("mod:blade", "Blade", "Sharp."));

            string[] lines = Lines(merged);
            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/mod:blade\tText\t\tBlade"));
            Assert.That(lines, Does.Contain("Items/mod:bladeDesc\tText\t\tSharp."));
        }

        [Test]
        public void RegeneratingReplacesOwnedRowsInsteadOfDuplicatingThem()
        {
            string first = DimensionLocalizationCsv.Merge(null, Rows("mod:blade", "Blade", "Sharp."));
            string second = DimensionLocalizationCsv.Merge(
                first, Rows("mod:blade", "Great Blade", "Very sharp."));

            string[] lines = Lines(second);
            Assert.That(CountStartingWith(lines, "Items/mod:blade\t"), Is.EqualTo(1));
            Assert.That(CountStartingWith(lines, "Items/mod:bladeDesc\t"), Is.EqualTo(1));
            Assert.That(lines, Does.Contain("Items/mod:blade\tText\t\tGreat Blade"));
            Assert.That(second, Does.Not.Contain("Sharp."));
        }

        [Test]
        public void ForeignRowsAndOtherLanguagesArePreserved()
        {
            // A creator's existing table with a fifth column of hand-written French.
            string existing =
                "Key\tType\tDesc\tEnglish\tFrench\n" +
                "Items/other_mod_thing\tText\t\tThing\tChose\n" +
                "Items/mod:blade\tText\t\tOld Blade\tVieille Lame\n";

            string merged = DimensionLocalizationCsv.Merge(
                existing, Rows("mod:blade", "Blade", "Sharp."));

            string[] lines = Lines(merged);
            Assert.That(
                lines,
                Does.Contain("Items/other_mod_thing\tText\t\tThing\tChose"),
                "Another mod's row must survive regeneration.");
            Assert.That(lines[0], Is.EqualTo("Key\tType\tDesc\tEnglish\tFrench"));

            // Our own row is rewritten, and keeps the table's column count.
            Assert.That(lines, Does.Contain("Items/mod:blade\tText\t\tBlade\t"));
        }

        [Test]
        public void ABomAndCrlfTableIsHandled()
        {
            string existing =
                "﻿Key\tType\tDesc\tEnglish\r\nItems/keep_me\tText\t\tKeep\r\n";

            string merged = DimensionLocalizationCsv.Merge(
                existing, Rows("mod:blade", "Blade", "Sharp."));

            string[] lines = Lines(merged);
            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/keep_me\tText\t\tKeep"));
        }

        [Test]
        public void ATableMissingItsHeaderGetsOne()
        {
            string existing = "Items/keep_me\tText\t\tKeep\n";

            string[] lines = Lines(DimensionLocalizationCsv.Merge(
                existing, Rows("mod:blade", "Blade", "Sharp.")));

            Assert.That(lines[0], Is.EqualTo(DimensionLocalizationCsv.Header));
            Assert.That(lines, Does.Contain("Items/keep_me\tText\t\tKeep"));
        }

        [Test]
        public void TabsAndNewlinesInTextCannotBreakTheRowLayout()
        {
            List<DimensionLocalizationCsv.Row> rows = new List<DimensionLocalizationCsv.Row>();
            DimensionLocalizationCsv.AddItemRows(
                rows, "mod:blade", "Bla\tde", "Line one\nLine two");

            string[] lines = Lines(DimensionLocalizationCsv.Merge(null, rows));

            for (int i = 0; i < lines.Length; i++)
            {
                Assert.That(
                    lines[i].Split('\t').Length,
                    Is.EqualTo(4),
                    "Row " + i + " has the wrong column count: " + lines[i]);
            }

            Assert.That(lines, Does.Contain("Items/mod:blade\tText\t\tBla de"));
            Assert.That(lines, Does.Contain("Items/mod:bladeDesc\tText\t\tLine one Line two"));
        }

        [Test]
        public void AnItemWithNoDescriptionStillGetsAWellFormedRow()
        {
            string[] lines = Lines(
                DimensionLocalizationCsv.Merge(null, Rows("mod:blade", "Blade", null)));

            Assert.That(lines, Does.Contain("Items/mod:bladeDesc\tText\t\t"));
        }

        [Test]
        public void BlankLinesAreDropped()
        {
            string existing = "Key\tType\tDesc\tEnglish\n\n\nItems/keep_me\tText\t\tKeep\n\n";

            string[] lines = Lines(DimensionLocalizationCsv.Merge(existing, Rows("a", "A", "d")));

            for (int i = 0; i < lines.Length; i++)
            {
                Assert.That(string.IsNullOrWhiteSpace(lines[i]), Is.False);
            }
        }

        private static List<DimensionLocalizationCsv.Row> Rows(
            string objectName,
            string displayName,
            string description)
        {
            List<DimensionLocalizationCsv.Row> rows = new List<DimensionLocalizationCsv.Row>();
            DimensionLocalizationCsv.AddItemRows(rows, objectName, displayName, description);
            return rows;
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
