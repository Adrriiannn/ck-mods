#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// No comment or document in this framework cites one of its own files by line number.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT WENT WRONG. Twenty-eight comments and documents pointed at a framework file by
    /// <c>Name.cs:1317</c>. Every one of them was right when it was typed. Then the god scripts
    /// were split, sixteen partials came out of one spine, and every number moved. Six of the
    /// twenty-eight then pointed past the end of the file they named and four more landed on an
    /// unrelated line, which is worse: a reader checks it, sees code, and believes a claim about
    /// something else. One of them — a note in the pet skin registry saying where a creature's
    /// object name is stamped — landed on a warnings delegate.
    /// </para>
    /// <para>
    /// The rot is silent by construction. A line number cannot be compiled, a rename cannot break
    /// it, and the file it names still exists, so nothing anywhere had an opinion until somebody
    /// read all twenty-eight by hand. Citing the MEMBER instead survives every move a split makes,
    /// and citing the partial by its own name survives the split that created it.
    /// </para>
    /// <para>
    /// CORE KEEPER'S OWN SOURCE IS DELIBERATELY EXEMPT. Roughly a hundred and eighty comments cite
    /// the decompiled game at <c>ck-db/Pug.Other/AttackSystem.cs:943</c> and the like. That tree is
    /// outside this repository, nothing here moves it, and the line number is the only way to find
    /// a call inside a four-thousand-line decompiled system. Those are the citations worth having;
    /// this test is careful not to touch them. The rule is only about files this repository can
    /// move, which is why it is keyed off the names of files that really are in it.
    /// </para>
    /// </remarks>
    internal sealed class DimensionCitationTests
    {
        /// <summary>A citation of the form <c>SomeFile.cs:123</c>, with any trailing range.</summary>
        private static readonly Regex CitesALine = new Regex(
            @"(?<file>[A-Za-z][A-Za-z0-9_.]*\.cs):(?<line>[0-9]+)",
            RegexOptions.Compiled);

        [Test]
        public void NoCommentCitesAFrameworkFileByLineNumber()
        {
            HashSet<string> ours = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> sources = DimensionFrameworkSourceScanner.SourceFiles();
            for (int i = 0; i < sources.Count; i++)
            {
                ours.Add(Path.GetFileName(sources[i]));
            }

            // The tests are read here as well as the shipped code. The scanner leaves them out
            // because a test naming a component is talking ABOUT the framework rather than being
            // part of it — but a test naming a line of the framework rots exactly like a comment
            // does, and three of the twenty-eight were in test files.
            List<string> everything = new List<string>(sources);
            everything.AddRange(EveryTestFile());
            everything.AddRange(EveryDocument());

            List<string> rotten = new List<string>();
            for (int i = 0; i < everything.Count; i++)
            {
                string text;
                try
                {
                    text = File.ReadAllText(everything[i]);
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (Match match in CitesALine.Matches(text))
                {
                    string named = match.Groups["file"].Value;
                    if (!ours.Contains(named))
                    {
                        continue;
                    }

                    rotten.Add(
                        Path.GetFileName(everything[i]) + " cites " + match.Value);
                }
            }

            rotten.Sort(StringComparer.Ordinal);

            Assert.That(
                rotten,
                Is.Empty,
                "A comment points at a file this repository owns by line number. The next split " +
                "moves the line and leaves the citation reading as freshly checked. Name the " +
                "member, or the partial, instead:\n  " +
                string.Join("\n  ", rotten.ToArray()));
        }

        /// <summary>The test files, which the scanner deliberately leaves out.</summary>
        private static List<string> EveryTestFile()
        {
            return FilesUnder("Editor/Tests", "*.cs");
        }

        /// <summary>The documents, which carry the same citations and rot the same way.</summary>
        private static List<string> EveryDocument()
        {
            return FilesUnder("Docs", "*.md");
        }

        private static List<string> FilesUnder(string relativeFolder, string pattern)
        {
            List<string> files = new List<string>();
            string folder = Path.Combine(
                DimensionFrameworkSourceScanner.Root,
                relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
            {
                return files;
            }

            files.AddRange(Directory.GetFiles(folder, pattern, SearchOption.AllDirectories));
            return files;
        }
    }
}
#endif
