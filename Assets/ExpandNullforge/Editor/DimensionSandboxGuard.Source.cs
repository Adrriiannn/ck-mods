using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// One source file flattened into a run of code, with a way back to the real line.
    /// </summary>
    public static partial class DimensionSandboxGuard
    {
        /// <summary>
        /// One source file, flattened into a single run of code with the comments and the line
        /// breaks taken out and the aliases put back.
        /// </summary>
        /// <remarks>
        /// <para>
        /// FLATTENING IS THE POINT. Matching line by line meant a name written across two lines
        /// matched nothing — <c>System.</c> on one line and <c>IO.File</c> on the next is a perfectly
        /// ordinary way for a formatter to break a long expression, and it read as clean. Here a
        /// break next to a <c>.</c> closes up and every other break becomes one space, so the same
        /// text reads the same way whatever column it was wrapped at. Each character remembers the
        /// physical line it came from, so a finding still points at a line somebody can open.
        /// </para>
        /// <para>
        /// ALIASES ARE PUT BACK because <c>using S = System;</c> followed by <c>S.Reflection</c>
        /// spells nothing denied anywhere in the file. Each alias is substituted for its target and
        /// the pass repeats a few times so that an alias of an alias resolves too. An alias declared
        /// in a DIFFERENT file — a global using, or C#'s <c>extern alias</c> — is not seen; that one
        /// is left to the assembly guard.
        /// </para>
        /// </remarks>
        private sealed class Source
        {
            private string[] rawLines;
            private int[] lineOfCharacter;
            private bool isOneLiteral;

            /// <summary>The whole file's code, comments removed and line breaks closed up.</summary>
            public string Text { get; private set; }

            /// <summary>Every namespace the file imported.</summary>
            public HashSet<string> Usings { get; private set; }

            public int LineOf(int index)
            {
                if (lineOfCharacter.Length == 0)
                {
                    return 0;
                }

                if (index < 0)
                {
                    index = 0;
                }

                if (index >= lineOfCharacter.Length)
                {
                    index = lineOfCharacter.Length - 1;
                }

                return lineOfCharacter[index];
            }

            /// <summary>The untouched source line a match sits on, for the message.</summary>
            public string RawLineOf(int index)
            {
                if (isOneLiteral)
                {
                    return rawLines.Length > 0 ? rawLines[0] : string.Empty;
                }

                int line = LineOf(index);
                return line >= 1 && line <= rawLines.Length ? rawLines[line - 1] : string.Empty;
            }

            public static Source Read(string[] lines)
            {
                StringBuilder text = new StringBuilder();
                List<int> map = new List<int>();
                bool inBlockComment = false;
                bool inVerbatimString = false;
                string[] stripped = new string[lines.Length];

                for (int i = 0; i < lines.Length; i++)
                {
                    stripped[i] = StripComments(lines[i], ref inBlockComment, ref inVerbatimString);
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    string code = stripped[i].Trim();
                    if (code.Length == 0)
                    {
                        continue;
                    }

                    if (text.Length > 0 && text[text.Length - 1] != '.' && code[0] != '.')
                    {
                        text.Append(' ');
                        map.Add(i + 1);
                    }

                    for (int c = 0; c < code.Length; c++)
                    {
                        text.Append(code[c]);
                        map.Add(i + 1);
                    }
                }

                string flat = CloseSpacesAroundDots(text.ToString(), map);

                Source source = new Source();
                source.rawLines = lines;
                source.Usings = UsingsIn(stripped);
                Expand(flat, map, AliasesIn(stripped), source);
                return source;
            }

            /// <summary>
            /// Removes whitespace sitting directly beside a dot, so <c>System . IO</c> reads as the
            /// name it compiles to.
            /// </summary>
            /// <remarks>
            /// Two halves of one shape, and only one of them was closed. The flattener already
            /// joins a line break next to a dot; a space or a tab next to a dot was left alone, so
            /// <c>System . IO . File.Delete(...)</c> went through while the same expression split
            /// across two lines did not. Literal text is carried through the flattener rather than
            /// blanked, so a sentence inside a string that ends "System." and continues " IO" would
            /// now read as a reference; there is no such sentence in the tree, and trusting a space
            /// to hide a name from the game's own checker — which reads compiled IL, where no space
            /// survives — is the worse trade.
            /// </remarks>
            private static string CloseSpacesAroundDots(string text, List<int> map)
            {
                StringBuilder kept = new StringBuilder(text.Length);
                List<int> keptMap = new List<int>(map.Count);

                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == ' ' || text[i] == '\t')
                    {
                        int end = i;
                        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
                        {
                            end++;
                        }

                        char before = kept.Length > 0 ? kept[kept.Length - 1] : '\0';
                        char after = end < text.Length ? text[end] : '\0';
                        if (before == '.' || after == '.')
                        {
                            i = end - 1;
                            continue;
                        }
                    }

                    kept.Append(text[i]);
                    keptMap.Add(i < map.Count ? map[i] : 0);
                }

                map.Clear();
                map.AddRange(keptMap);
                return kept.ToString();
            }

            /// <summary>One literal out of an emitter, treated as a file of its own.</summary>
            public static Source FromSingleLine(
                string code,
                int line,
                HashSet<string> usings,
                Dictionary<string, string> aliases)
            {
                List<int> map = new List<int>(code.Length);
                for (int i = 0; i < code.Length; i++)
                {
                    map.Add(line);
                }

                string flat = CloseSpacesAroundDots(code, map);

                Source source = new Source();
                source.rawLines = new[] { code };
                source.isOneLiteral = true;
                source.Usings = usings;
                Expand(flat, map, aliases, source);
                return source;
            }

            /// <summary>
            /// Rewrites <c>alias.</c> as <c>target.</c>, repeatedly, so an alias chain resolves.
            /// </summary>
            private static void Expand(
                string text,
                List<int> map,
                Dictionary<string, string> aliases,
                Source into)
            {
                for (int round = 0; round < 4 && aliases.Count > 0; round++)
                {
                    StringBuilder next = new StringBuilder(text.Length);
                    List<int> nextMap = new List<int>(map.Count);
                    bool changed = false;

                    int i = 0;
                    while (i < text.Length)
                    {
                        bool replaced = false;
                        foreach (KeyValuePair<string, string> alias in aliases)
                        {
                            if (!StartsWithWord(text, i, alias.Key))
                            {
                                continue;
                            }

                            int after = i + alias.Key.Length;
                            if (after >= text.Length || text[after] != '.')
                            {
                                continue;
                            }

                            for (int c = 0; c < alias.Value.Length; c++)
                            {
                                next.Append(alias.Value[c]);
                                nextMap.Add(map[i]);
                            }

                            i = after;
                            replaced = true;
                            changed = true;
                            break;
                        }

                        if (replaced)
                        {
                            continue;
                        }

                        next.Append(text[i]);
                        nextMap.Add(map[i]);
                        i++;
                    }

                    text = next.ToString();
                    map = nextMap;
                    if (!changed)
                    {
                        break;
                    }
                }

                into.Text = text;
                into.lineOfCharacter = map.ToArray();
            }

            private static bool StartsWithWord(string text, int at, string word)
            {
                if (at + word.Length > text.Length)
                {
                    return false;
                }

                if (at > 0)
                {
                    char before = text[at - 1];
                    if (before == '.' || before == '_' || before == '@' || char.IsLetterOrDigit(before))
                    {
                        return false;
                    }
                }

                return string.CompareOrdinal(text, at, word, 0, word.Length) == 0;
            }
        }
    }
}
