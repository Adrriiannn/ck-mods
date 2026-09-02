#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Reading the framework source these tests are asked about.
    /// </summary>
    internal sealed partial class DimensionQueryCompanionTests
    {
        /// <summary>
        /// Whether a resolved type argument names something Core Keeper owns.
        /// </summary>
        /// <remarks>
        /// The suffix test is applied AFTER the name has been resolved through the aliases and
        /// stripped of <c>global::</c>, the verbatim <c>@</c> and its namespace. The pattern this
        /// replaces was applied to the raw text, so <c>using NewThing = Pug.SomethingAuthoring;</c>
        /// followed by <c>Attach&lt;NewThing&gt;(root)</c> matched nothing at all and shipped
        /// silently — which is the hole the remark on that pattern claimed to have closed.
        /// </remarks>
        private static bool IsACoreKeeperSurfaceName(string resolved)
        {
            return resolved.EndsWith("Authoring", StringComparison.Ordinal) ||
                   resolved.EndsWith("AuthoringComponent", StringComparison.Ordinal) ||
                   resolved.EndsWith("CD", StringComparison.Ordinal) ||
                   resolved == "InteractableObject";
        }

        /// <summary>Every name in a type-argument list, one at a time.</summary>
        private static IEnumerable<string> EveryTypeArgumentIn(string typeArguments)
        {
            foreach (string part in typeArguments.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        /// <summary>
        /// One source file with its prose blanked out and its using-aliases read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY NOT LINE BY LINE. Every scan here used to be <c>ReadAllLines</c> plus a per-line
        /// regex, and a generic call split across two lines matched none of them —
        /// <c>EnsureComponent&lt;</c> on one line and the type on the next was invisible to all
        /// three. The tree already wraps generic calls at the margin, so one long type name on an
        /// add would have taken that component out of the guard permanently, and out of the backlog
        /// too, because the staleness check reads through the same regex.
        /// </para>
        /// <para>
        /// Prose is blanked rather than dropped so that positions still line up with the file, and
        /// so a commented-out add still cannot keep a dead name alive.
        /// </para>
        /// </remarks>
        private sealed class ScannedFile
        {
            public string Name;

            /// <summary>Where it sits, so two files with one name can be told apart.</summary>
            public string Where;

            public string Code;

            /// <summary>
            /// The same again with the string literals blanked as well as the comments.
            /// </summary>
            /// <remarks>
            /// Only the sweep check reads this. Blanking strings would hide a component name held
            /// in a literal from the add scans, and the tree does hold type names in strings, so
            /// the two readings are kept apart rather than merged.
            /// </remarks>
            public string CodeWithoutText;

            public Dictionary<string, string> Aliases;

            /// <summary>The 1-based line the character at that position is on.</summary>
            public int LineAt(int index)
            {
                int line = 1;
                for (int i = 0; i < index && i < Code.Length; i++)
                {
                    if (Code[i] == '\n')
                    {
                        line++;
                    }
                }

                return line;
            }

            /// <summary>
            /// The component a type argument names, following a using-alias if it is one.
            /// </summary>
            /// <remarks>
            /// <c>using Facing = Pug.X.SomeAuthoring;</c> and then <c>Ensure&lt;Facing&gt;</c> used
            /// to yield "Facing", which failed the old suffix test and was dropped in silence.
            /// The tree aliases a type in eight files, and all eight are <c>Object</c> aliased to
            /// dodge the <c>UnityEngine.Object</c> clash — so this is not yet precedent for
            /// aliasing a Core Keeper type, only proof that the next name clash will produce one.
            /// Nothing decides whether a name is a surface until it has been through here.
            /// </remarks>
            public string Resolve(string typeName)
            {
                string last = LastPartOf(typeName);
                string aliased;
                return Aliases.TryGetValue(last, out aliased) ? aliased : last;
            }
        }

        /// <summary>
        /// The file a name-keyed list is written against: the type, not the partial.
        /// </summary>
        /// <remarks>
        /// A partial of <c>Foo</c> is called <c>Foo.Something.cs</c> everywhere in this framework,
        /// so the first dot separates the type from the part. <c>Foo.cs</c> answers itself.
        /// </remarks>
        private static string SubjectOf(string fileName)
        {
            int dot = fileName.IndexOf('.');
            return dot < 0 ? fileName : fileName.Substring(0, dot) + ".cs";
        }

        /// <summary>Every framework source file, read once, prose blanked, aliases resolved.</summary>
        private static List<ScannedFile> ScanTheFramework()
        {
            List<ScannedFile> scanned = new List<ScannedFile>();

            foreach (string path in GeneratorSources())
            {
                ScannedFile file = new ScannedFile();
                file.Name = Path.GetFileName(path);
                file.Where = path.Replace('\\', '/');
                string source = File.ReadAllText(path);
                file.Code = CodeWithoutProse(source);
                file.CodeWithoutText = CodeWithoutProse(source, true);
                file.Aliases = AliasesIn(file.Code);
                scanned.Add(file);
            }

            return scanned;
        }

        /// <summary>
        /// The same text with every <c>#if</c> block turned into blank lines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The sweep check is a text match, and text inside a preprocessor block the compiler never
        /// takes is not a call. A generator whose only <c>CloseTheGaps</c> sat inside
        /// <c>#if NEVER_DEFINED</c> read as sweeping, and every companion row was inert for
        /// everything it built.
        /// </para>
        /// <para>
        /// It blanks the whole block whichever way the condition would go, because nothing here
        /// knows which symbols a build defines. That is the safe direction: it can only take a
        /// sweep away, which asks a person to look, and never invent one. The four framework files
        /// with a conditional block in them today write no Core Keeper surface, so none of them
        /// reaches this.
        /// </para>
        /// </remarks>
        private static string WithConditionalBlocksBlanked(string source)
        {
            string[] lines = source.Split('\n');
            int depth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                bool opens = trimmed.StartsWith("#if", StringComparison.Ordinal);
                bool closes = trimmed.StartsWith("#endif", StringComparison.Ordinal);

                if (closes && depth > 0)
                {
                    depth--;
                }

                if (depth > 0 || opens || closes)
                {
                    lines[i] = string.Empty;
                }

                if (opens)
                {
                    depth++;
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// The same text with every comment turned into spaces, newlines kept.
        /// </summary>
        private static string CodeWithoutProse(string source)
        {
            return CodeWithoutProse(source, false);
        }

        /// <summary>
        /// The same, optionally blanking the string literals as well.
        /// </summary>
        /// <remarks>
        /// The sweep check is a text match for a call, and it was reading a file whose comments
        /// were blanked and whose strings were not — so a warning sentence that mentioned the
        /// sweep by name satisfied it just as well as a call did.
        /// </remarks>
        private static string CodeWithoutProse(string source, bool alsoBlankText)
        {
            char[] code = source.ToCharArray();
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < code.Length; i++)
            {
                char here = code[i];
                char next = i + 1 < code.Length ? code[i + 1] : '\0';

                if (inLineComment)
                {
                    if (here == '\n')
                    {
                        inLineComment = false;
                    }
                    else
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (here == '*' && next == '/')
                    {
                        code[i] = ' ';
                        code[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else if (here != '\n')
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inString)
                {
                    if (here == '\\')
                    {
                        if (alsoBlankText)
                        {
                            code[i] = ' ';
                            if (i + 1 < code.Length && code[i + 1] != '\n')
                            {
                                code[i + 1] = ' ';
                            }
                        }

                        i++;
                    }
                    else if (here == '"')
                    {
                        inString = false;
                        if (alsoBlankText)
                        {
                            code[i] = ' ';
                        }
                    }
                    else if (alsoBlankText && here != '\n')
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inChar)
                {
                    if (here == '\\')
                    {
                        i++;
                    }
                    else if (here == '\'')
                    {
                        inChar = false;
                    }

                    continue;
                }

                if (here == '"')
                {
                    inString = true;
                    if (alsoBlankText)
                    {
                        code[i] = ' ';
                    }
                }
                else if (here == '\'')
                {
                    inChar = true;
                }
                else if (here == '/' && next == '/')
                {
                    code[i] = ' ';
                    inLineComment = true;
                }
                else if (here == '/' && next == '*')
                {
                    code[i] = ' ';
                    code[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                }
            }

            return new string(code);
        }

        /// <summary>Every <c>using Alias = Some.Type;</c> in the file, by alias.</summary>
        private static Dictionary<string, string> AliasesIn(string code)
        {
            Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in UsingAlias.Matches(code))
            {
                aliases[match.Groups[1].Value] = LastPartOf(match.Groups[2].Value);
            }

            return aliases;
        }

        private static readonly Regex UsingAlias = new Regex(
            @"^\s*using\s+(\w+)\s*=\s*([\w\.]+)\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Every source file in the framework that could put a component on an object.
        /// </summary>
        /// <remarks>
        /// EVERYTHING, not <c>Editor/Dimension*.cs</c> at the top level. That narrower scan read
        /// about a quarter of the framework: <c>Editor/Generation</c>, <c>Editor/UI</c>,
        /// <c>Scripts</c> and <c>API</c> were never opened, and a generator named anything but
        /// <c>Dimension*</c> was invisible. Four files in the widened part DO write Core Keeper
        /// components — <c>NullforgeDimensionService.RuntimeLoadingInternals.cs</c>,
        /// <c>DimensionItemPortalSpawnSystem.cs</c>, <c>DimensionPortalAuthoringConverter.cs</c>
        /// and <c>DimensionDungeonAssembler.cs</c> — so the remark that used to sit here saying
        /// nothing in those folders writes one was wrong, and widening the scan was not free: it
        /// is what put those four on the excused list with a reason. This test's own folder is
        /// left out, because the probes below add components on purpose.
        /// <para>
        /// WHERE IT STOPS. It roots at <c>Assets/ExpandNullforge</c>, so the two sibling folders
        /// under <c>Assets</c> — <c>Nullforge</c> and <c>MPTest</c>, each a built mod with its own
        /// assembly definition and one generated bootstrap in it — are never read. Both were
        /// checked: the only Core Keeper-looking text in either is a <c>using</c> line, and neither
        /// puts a component on anything. They are a mod's OUTPUT rather than this framework's
        /// source, which is why the root is where it is; a generator that ever moves out there
        /// would be invisible to every scan in this file.
        /// </para>
        /// </remarks>
        private static IEnumerable<string> GeneratorSources()
        {
            string root = Path.Combine(Application.dataPath, "ExpandNullforge");
            if (!Directory.Exists(root))
            {
                yield break;
            }

            string tests = Path.Combine(root, "Editor" + Path.DirectorySeparatorChar + "Tests");

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(tests, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return file;
            }
        }
    }
}
#endif
