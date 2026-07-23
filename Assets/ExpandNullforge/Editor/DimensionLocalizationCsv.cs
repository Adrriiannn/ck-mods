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
            AddItemRows(rows, objectName, displayName, description, null);
        }

        /// <summary>
        /// Localization keys Core Keeper reads for an object's name and tooltip.
        /// Keys are written with ':' replaced by '_': the game's LocalizationManager applies that
        /// replacement to every term before lookup (Term.Replace(':', '_')), so a key stored with a
        /// colon can never be found. When the object name contained a colon, the old colon-form keys
        /// are reported via <paramref name="retiredKeys"/> so the merge can drop stale rows.
        /// </summary>
        public static void AddItemRows(
            List<Row> rows,
            string objectName,
            string displayName,
            string description,
            List<string> retiredKeys)
        {
            if (rows == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            string lookupName = ToLookupKeyName(objectName);
            string itemKey = "Items/" + lookupName;
            rows.Add(new Row(itemKey, displayName));
            rows.Add(new Row(itemKey + "Desc", description));

            if (retiredKeys != null &&
                !string.Equals(lookupName, objectName, StringComparison.Ordinal))
            {
                retiredKeys.Add("Items/" + objectName);
                retiredKeys.Add("Items/" + objectName + "Desc");
            }
        }

        /// <summary>
        /// The form of an object name the game's localization lookup actually queries: Core Keeper's
        /// patched I2 LocalizationManager replaces every ':' with '_' before searching its sources.
        /// </summary>
        public static string ToLookupKeyName(string objectName)
        {
            return string.IsNullOrEmpty(objectName)
                ? string.Empty
                : objectName.Replace(':', '_');
        }

        /// <summary>
        /// Returns the new file content. <paramref name="existingContent"/> may be null or empty
        /// for a table that does not exist yet.
        /// </summary>
        public static string Merge(string existingContent, IReadOnlyList<Row> rows)
        {
            return Merge(existingContent, rows, null);
        }

        /// <summary>
        /// Returns the new file content. <paramref name="retiredKeys"/> lists keys that are owned
        /// but must no longer exist (e.g. the colon-form of a key the lookup can never resolve):
        /// matching existing rows are dropped and nothing is written back for them.
        /// </summary>
        public static string Merge(
            string existingContent,
            IReadOnlyList<Row> rows,
            IReadOnlyList<string> retiredKeys)
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

            if (retiredKeys != null)
            {
                for (int i = 0; i < retiredKeys.Count; i++)
                {
                    if (!string.IsNullOrEmpty(retiredKeys[i]))
                    {
                        ownedKeys.Add(retiredKeys[i]);
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
