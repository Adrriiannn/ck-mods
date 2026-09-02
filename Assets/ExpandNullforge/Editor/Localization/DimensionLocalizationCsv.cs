using System;
using System.Collections.Generic;
using System.Globalization;

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
        /// The two terms a dish's name is built from, for one ingredient.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cooked dish is never named after itself. The game composes its name out of three terms
        /// — the second ingredient as an adjective, the leading ingredient as a noun, and the dish
        /// family — so an ingredient without these two rows turns every dish it appears in into a
        /// name with a raw object id sitting in the middle of it. This is the piece a mod that only
        /// wrote <c>Items/</c> rows would miss, and it is invisible until somebody cooks.
        /// </para>
        /// <para>
        /// A trailing "Rare" is stripped off the object's name before the lookup, which is how a
        /// golden ingredient shares its ordinary form's words. That means the golden version needs
        /// no rows of its own, and writing them would only leave two keys nothing reads.
        /// </para>
        /// <para>
        /// Gendered languages append Female or Male to the ADJECTIVE key only. English appends
        /// nothing, so the plain key is what is written here; a translator adding a gendered
        /// language adds the suffixed rows themselves, and the merge leaves their rows alone.
        /// </para>
        /// </remarks>
        public static void AddFoodIngredientNameRows(
            List<Row> rows,
            string objectName,
            string displayName)
        {
            if (rows == null || string.IsNullOrEmpty(objectName) ||
                objectName.EndsWith("Rare", StringComparison.Ordinal))
            {
                return;
            }

            string lookupName = ToLookupKeyName(objectName);
            rows.Add(new Row("FoodAdjectives/" + lookupName, displayName));
            rows.Add(new Row("FoodNouns/" + lookupName, displayName));
        }

        /// <summary>
        /// The localization key for a biome's title card, and the row that fills it.
        /// </summary>
        /// <remarks>
        /// Under <c>Biomes/</c> rather than <c>Items/</c> so a biome and an item of the same name
        /// cannot collide, and passed through the same colon replacement as everything else — Core
        /// Keeper's lookup rewrites the term before searching, so a key stored with a colon is a key
        /// that can never be found.
        /// </remarks>
        public static void AddBiomeTitleRow(List<Row> rows, string biomeTermName, string displayName)
        {
            if (rows == null || string.IsNullOrEmpty(biomeTermName))
            {
                return;
            }

            rows.Add(new Row(ToLookupKeyName(biomeTermName), displayName));
        }

        /// <summary>
        /// The row a creature's floating name and its map-pin hover both resolve: the game's
        /// named bosses render "Names/&lt;objectID&gt;", and ours render the same term with the
        /// qualified object name in the id's place.
        /// </summary>
        public static void AddNameRow(List<Row> rows, string objectName, string displayName)
        {
            if (rows == null || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            rows.Add(new Row("Names/" + ToLookupKeyName(objectName), displayName));
        }

        /// <summary>
        /// The one line a player ever reads about a custom stat effect.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Core Keeper asks for <c>"Conditions/" + conditionID.ToString()</c> — verified at
        /// <c>Pug.Other/ConditionUI.cs:191, 217, 223</c>, and the same string is built again by the
        /// item tooltip (<c>SlotUIBase.cs:1510</c>), the talent panel
        /// (<c>SkillTalentUIElement.cs:277</c>), the souls panel and the mouse hover. A number a mod
        /// minted has no name in the game's enum, so <c>ToString</c> returns its digits and the term
        /// really is <c>Conditions/358</c>. Nothing registers that term for a minted number, which is
        /// why a custom buff is mechanically live and nameless until this row exists.
        /// </para>
        /// <para>
        /// THE VALUE ARRIVES AS <c>{0}</c>. Every one of those call sites passes the formatted value
        /// as the single format field, so the line is written the way the game's own 269 condition
        /// lines are written — "{0}% damage", "{0} acid damage every sec, ignores armor". A line with
        /// no <c>{0}</c> still shows, it just never shows its number.
        /// </para>
        /// <para>
        /// THERE IS NO SECOND ROW. A condition has no description term: none of the call sites ever
        /// appends "Desc", and the description column is empty on all 269 of the game's own condition
        /// entries. The line below IS the description, which is why it reads as a sentence rather
        /// than as a title.
        /// </para>
        /// </remarks>
        public static void AddConditionRow(List<Row> rows, int conditionNumber, string line)
        {
            if (rows == null || conditionNumber < 0)
            {
                return;
            }

            rows.Add(new Row(
                "Conditions/" + conditionNumber.ToString(CultureInfo.InvariantCulture),
                line));
        }

        /// <summary>
        /// The words on a talent square, for a talent this mod invented.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A TALENT'S NAME IS A KEY AND NOT A WORD. The talent window renders
        /// <c>LocalizationManager.GetTranslation("SkillTalents/" + info.name)</c> — verified at
        /// <c>Pug.Other/SkillTalentUIElement.cs:154, 160</c> — so a mod that names its talent
        /// "Deep Digger" and writes no row shows the raw key on the square. Nothing in the
        /// framework wrote a <c>SkillTalents/</c> row until this.
        /// </para>
        /// <para>
        /// A ROW IS WRITTEN ONLY FOR A NAME THE GAME DOES NOT ALREADY KNOW. Reusing one of Core
        /// Keeper's own 96 talent names is the ordinary "I only want to change the numbers" case,
        /// and the game already has that name written in every language it ships; writing a row
        /// over it would replace twelve translations with one. So the caller checks, and the split
        /// is <see cref="ExpandNullforge.Authoring.DimensionTalent.KeepsTheGamesOwnWording"/>.
        /// </para>
        /// <para>
        /// Passed through the same colon replacement as everything else, because the game's lookup
        /// rewrites the term before searching — and the rewrite happens to the key the game builds
        /// from the talent's name too, so the two still meet.
        /// </para>
        /// </remarks>
        public static void AddTalentRow(List<Row> rows, string talentName, string shownAs)
        {
            if (rows == null || string.IsNullOrEmpty(talentName))
            {
                return;
            }

            rows.Add(new Row("SkillTalents/" + ToLookupKeyName(talentName), shownAs));
        }

        /// <summary>
        /// The key an object's name resolves to, so emitters and the coverage check cannot disagree
        /// about what counts as "this object has a name".
        /// </summary>
        public static string ItemKeyFor(string objectName)
        {
            return string.IsNullOrEmpty(objectName)
                ? string.Empty
                : "Items/" + ToLookupKeyName(objectName);
        }

        /// <summary>The key a boss's floating name resolves to. See <see cref="AddNameRow"/>.</summary>
        public static string NameKeyFor(string objectName)
        {
            return string.IsNullOrEmpty(objectName)
                ? string.Empty
                : "Names/" + ToLookupKeyName(objectName);
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
