#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Finds and reads this framework's own source, for the tests that assert on what it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TEST THAT NAMES A PATH BREAKS WHEN A FILE MOVES, and the way it breaks is the problem. It
    /// either throws with a message about a missing file — which reads as a broken test rather than
    /// a moved file — or, worse, reads an empty string and passes over nothing, which is the
    /// drawn-but-dead bug class arriving in the test suite. Twelve test files named production paths
    /// by hand, and the two they pinned hardest are the two largest files in the project.
    /// </para>
    /// <para>
    /// So nothing here takes a path. A file is found by its NAME across the whole framework, and a
    /// name that matches nothing, or matches twice, fails immediately and says which. Moving a file
    /// then costs nothing; renaming one fails loudly, which is right, because a test that names a
    /// file has an opinion about that file.
    /// </para>
    /// <para>
    /// ORDERING ASSERTIONS ARE ANCHORED TO A METHOD, not to a file. An assertion that one call comes
    /// before another is about one method's body; asserting it over a whole file means that
    /// splitting the file makes the assertion pass vacuously, which is exactly the failure this
    /// suite is meant to catch rather than to commit.
    /// </para>
    /// </remarks>
    internal static class DimensionFrameworkSourceScanner
    {
        /// <summary>The folder that holds everything this framework owns.</summary>
        public static string Root
        {
            get { return Path.Combine(Application.dataPath, DimensionSandboxGuard.ShippedRoot); }
        }

        /// <summary>
        /// Every source file the framework owns, except its own tests.
        /// </summary>
        /// <remarks>
        /// Tests are excluded because a test naming a component, a call or a path is talking ABOUT
        /// the framework rather than being part of it, and counting it would let a test satisfy a
        /// check on production code.
        /// </remarks>
        public static List<string> SourceFiles()
        {
            List<string> files = new List<string>();
            string root = Root;
            if (!Directory.Exists(root))
            {
                return files;
            }

            string tests = Path.Combine(root, "Editor" + Path.DirectorySeparatorChar + "Tests");
            string[] all = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].StartsWith(tests, StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(all[i]);
                }
            }

            return files;
        }

        /// <summary>
        /// The full path of the one framework file with this name.
        /// </summary>
        /// <remarks>
        /// Fails rather than returning null on both misses. Zero matches means the file was renamed
        /// or deleted and the caller's assertion no longer has a subject; two matches means the
        /// caller cannot know which one it read, and a name-keyed check would silently answer for
        /// the wrong one.
        /// </remarks>
        public static string FindByName(string fileName)
        {
            List<string> matches = new List<string>();
            List<string> files = SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(
                        Path.GetFileName(files[i]),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(files[i].Replace('\\', '/'));
                }
            }

            Assert.That(
                matches.Count,
                Is.Not.EqualTo(0),
                "There is no file called " + fileName + " anywhere under " + Root + ", so this " +
                "test has no subject. It was renamed or deleted; find out which before changing " +
                "this name.");

            Assert.That(
                matches.Count,
                Is.EqualTo(1),
                "There are " + matches.Count + " files called " + fileName + " under " + Root +
                ", so this test cannot know which one it read:\n  " +
                string.Join("\n  ", matches.ToArray()));

            return matches[0];
        }

        /// <summary>The text of the one framework file with this name.</summary>
        public static string ReadByName(string fileName)
        {
            return File.ReadAllText(FindByName(fileName));
        }

        /// <summary>Whether a file with this name exists, without failing when it does not.</summary>
        public static bool Exists(string fileName)
        {
            List<string> files = SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                if (string.Equals(
                        Path.GetFileName(files[i]),
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The body of one method, wherever in the framework it is written, or null when no method
        /// of that name exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what an ordering assertion should be anchored to. Reading a whole file and
        /// asserting that one call's position is below another's is an assertion about the file's
        /// layout, and it stops meaning anything the moment the file is split — both calls end up in
        /// different files, both <c>IndexOf</c> calls answer -1, and -1 &lt; -1 is false, so the test
        /// throws or passes depending on how it was written. Anchored to the method, the same
        /// assertion survives any move and any split.
        /// </para>
        /// <para>
        /// It matches braces rather than parsing C#, which is enough here because it is looking at
        /// this framework's own source: a brace inside a string literal or a comment inside the
        /// method body would confuse it, and there is none in the methods anything asserts on. It
        /// returns the FIRST method of that name; a name declared twice is caught by
        /// <see cref="BodyOfMethodIsUnique"/>.
        /// </para>
        /// </remarks>
        public static string BodyOfMethod(string methodName)
        {
            List<string> files = SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                string body = BodyOfMethodIn(File.ReadAllText(files[i]), methodName);
                if (body != null)
                {
                    return body;
                }
            }

            return null;
        }

        /// <summary>How many framework files declare a method with this name.</summary>
        public static int BodyOfMethodIsUnique(string methodName)
        {
            int count = 0;
            List<string> files = SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                if (BodyOfMethodIn(File.ReadAllText(files[i]), methodName) != null)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The body of a named method inside one piece of text, or null.</summary>
        public static string BodyOfMethodIn(string source, string methodName)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            int at = 0;
            while (true)
            {
                at = source.IndexOf(methodName, at, StringComparison.Ordinal);
                if (at < 0)
                {
                    return null;
                }

                int after = at + methodName.Length;
                bool looksLikeADeclaration =
                    (at == 0 || !IsNameCharacter(source[at - 1])) &&
                    after < source.Length &&
                    (source[after] == '(' || source[after] == '<');

                if (looksLikeADeclaration)
                {
                    int open = source.IndexOf('{', after);
                    int semicolon = source.IndexOf(';', after);

                    // A call is followed by a semicolon before any brace; a declaration is not. An
                    // abstract or interface member has a semicolon and no body at all.
                    if (open >= 0 && (semicolon < 0 || open < semicolon))
                    {
                        int close = MatchingBrace(source, open);
                        if (close > open)
                        {
                            return source.Substring(open, close - open + 1);
                        }
                    }
                }

                at = after;
            }
        }

        private static bool IsNameCharacter(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static int MatchingBrace(string source, int open)
        {
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
#endif
