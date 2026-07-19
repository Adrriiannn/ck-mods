using System;
using System.Collections.Generic;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Merges generated rows into a Core Keeper mod's tab-separated <c>Localization.csv</c>.
    ///
    /// Without a row here an item shows its raw id in-game instead of its name, so generation is
    /// not finished until the table is updated. The merge is deliberately a pure string-to-string
    /// function: it can be unit-tested offline, and the only file work left to the caller is
    /// reading and writing.
    ///
    /// Rows the caller supplies are treated as owned — regenerating replaces them in place rather
    /// than appending duplicates — while every other line, including hand-written translations
    /// for other languages, is preserved untouched.
    /// </summary>
    internal static class DimensionLocalizationCsv
    {
        internal const string Header = "Key\tType\tDesc\tEnglish";

        internal readonly struct Row
        {
            public Row(string key, string englishText)
            {
                Key = key ?? string.Empty;
                EnglishText = englishText ?? string.Empty;
            }

            public string Key { get; }

            public string EnglishText { get; }
        }

        /// <summary>Localization keys Core Keeper reads for an object's name and tooltip.</summary>
        public static void AddItemRows(
            List<Row> rows,
            string objectName,
            string displayName,
            string description)
        {
            if (rows == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            string itemKey = "Items/" + objectName;
            rows.Add(new Row(itemKey, displayName));
            rows.Add(new Row(itemKey + "Desc", description));
        }

        /// <summary>
        /// Returns the new file content. <paramref name="existingContent"/> may be null or empty
        /// for a table that does not exist yet.
        /// </summary>
        public static string Merge(string existingContent, IReadOnlyList<Row> rows)
        {
            List<string> lines = new List<string>();
            HashSet<string> ownedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!string.IsNullOrEmpty(rows[i].Key))
                    {
                        ownedKeys.Add(rows[i].Key);
                    }
                }
            }

            if (!string.IsNullOrEmpty(existingContent))
            {
                string[] existingLines = existingContent
                    .Replace("\r\n", "\n")
                    .Replace('\r', '\n')
                    .Split('\n');

                for (int i = 0; i < existingLines.Length; i++)
                {
                    string line = existingLines[i];
                    if (i == 0)
                    {
                        // Strip a UTF-8 BOM so the header compares correctly.
                        line = line.TrimStart('﻿');
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    // A table that opens with data rather than a header still needs one.
                    if (lines.Count == 0 && !line.StartsWith("Key\t", StringComparison.Ordinal))
                    {
                        lines.Add(Header);
                    }

                    if (ownedKeys.Contains(ReadKey(line)))
                    {
                        continue;
                    }

                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                lines.Add(Header);
            }

            int columnCount = lines[0].Split('\t').Length;
            if (columnCount < 4)
            {
                lines[0] = Header;
                columnCount = 4;
            }

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    lines.Add(BuildRow(rows[i], columnCount));
                }
            }

            return string.Join("\n", lines.ToArray()) + "\n";
        }

        private static string ReadKey(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            int tabIndex = line.IndexOf('\t');
            return tabIndex < 0 ? line : line.Substring(0, tabIndex);
        }

        private static string BuildRow(Row row, int columnCount)
        {
            // Key, Type, Desc, English — with any extra language columns left blank so a table
            // that already has more languages keeps its shape.
            string[] columns = new string[Math.Max(4, columnCount)];
            for (int i = 0; i < columns.Length; i++)
            {
                columns[i] = string.Empty;
            }

            columns[0] = row.Key;
            columns[1] = "Text";
            columns[3] = Sanitize(row.EnglishText);
            return string.Join("\t", columns);
        }

        /// <summary>
        /// Tabs and newlines would split a row into the wrong columns, so they collapse to spaces.
        /// </summary>
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();
        }
    }
}
