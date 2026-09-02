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
    /// Reading the shipped source the roster is checked against.
    /// </summary>
    public sealed partial class DimensionSelfAuditTests
    {
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
