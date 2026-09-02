using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Deciding whether a piece of text names something the sandbox denies.
    /// </summary>
    public static partial class DimensionSandboxGuard
    {
        private static void Match(
            string path,
            Source source,
            DenyList denyList,
            List<Finding> findings)
        {
            string code = source.Text;
            if (code.Length == 0)
            {
                return;
            }

            HashSet<string> already = new HashSet<string>(StringComparer.Ordinal);

            for (int n = 0; n < denyList.Namespaces.Count; n++)
            {
                string banned = denyList.Namespaces[n];
                int from = 0;
                int at;
                while ((at = code.IndexOf(banned, from, StringComparison.Ordinal)) >= 0)
                {
                    Add(path, source, at, banned + ".*", findings, already);
                    from = at + 1;
                }
            }

            for (int n = 0; n < denyList.Types.Count; n++)
            {
                string banned = denyList.Types[n];
                foreach (int at in TypeMentions(code, banned, source.Usings))
                {
                    Add(path, source, at, banned, findings, already);
                }
            }

            for (int n = 0; n < denyList.Members.Count; n++)
            {
                string banned = denyList.Members[n];
                foreach (int at in MemberMentions(code, banned))
                {
                    Add(path, source, at, banned, findings, already);
                }
            }

            if (!denyList.AllowPInvoke)
            {
                foreach (int at in WordMentions(code, "DllImport"))
                {
                    Add(path, source, at, "allowPInvoke = false", findings, already);
                }

                foreach (int at in WordMentions(code, "MonoPInvokeCallback"))
                {
                    Add(path, source, at, "allowPInvoke = false", findings, already);
                }

                foreach (int at in WordMentions(code, "extern"))
                {
                    Add(path, source, at, "allowPInvoke = false", findings, already);
                }
            }

            if (!denyList.AllowUnsafeCode)
            {
                foreach (int at in WordMentions(code, "unsafe"))
                {
                    Add(path, source, at, "allowUnsafeCode = false", findings, already);
                }

                foreach (int at in WordMentions(code, "stackalloc"))
                {
                    Add(path, source, at, "allowUnsafeCode = false", findings, already);
                }

                foreach (int at in FollowedByAnyOf(code, "fixed", "("))
                {
                    Add(path, source, at, "allowUnsafeCode = false", findings, already);
                }
            }
        }

        /// <summary>
        /// Records one finding, at most once per line per rule so that a long expression does not
        /// arrive as a wall of the same complaint.
        /// </summary>
        private static void Add(
            string path,
            Source source,
            int at,
            string rule,
            List<Finding> findings,
            HashSet<string> already)
        {
            int line = source.LineOf(at);
            string key = line + "|" + rule;
            if (!already.Add(key))
            {
                return;
            }

            findings.Add(new Finding(path, line, rule, source.RawLineOf(at)));
        }

        /// <summary>
        /// Where a line names a denied type, either in full or by its short name in a file that
        /// imported its namespace.
        /// </summary>
        /// <remarks>
        /// The short-name half only counts a name that is USED — followed by <c>.</c>, <c>&lt;</c>,
        /// <c>(</c> or <c>[</c>. Without that, entries like <c>HarmonyLib.Code</c>,
        /// <c>HarmonyLib.Patch</c> and <c>HarmonyLib.Patches</c> would fire on the framework's own
        /// <c>Code</c> properties and on every sentence with the word "patch" in it, and a guard
        /// with that much noise gets switched off. The cost is that a bare declaration
        /// (<c>private Harmony field;</c>) goes unseen here; the assembly guard sees it.
        /// </remarks>
        private static IEnumerable<int> TypeMentions(string code, string fullName, HashSet<string> usings)
        {
            List<int> found = new List<int>();

            int from = 0;
            int at;
            while ((at = code.IndexOf(fullName, from, StringComparison.Ordinal)) >= 0)
            {
                found.Add(at);
                from = at + 1;
            }

            int dot = fullName.LastIndexOf('.');
            if (dot <= 0 || dot == fullName.Length - 1)
            {
                return found;
            }

            if (!usings.Contains(fullName.Substring(0, dot)))
            {
                return found;
            }

            found.AddRange(FollowedByAnyOf(code, fullName.Substring(dot + 1), ".<(["));
            return found;
        }

        /// <summary>
        /// Where a line names a denied member, in full or as "DeclaringType.Member".
        /// </summary>
        private static IEnumerable<int> MemberMentions(string code, string fullName)
        {
            List<int> found = new List<int>();

            int from = 0;
            int at;
            while ((at = code.IndexOf(fullName, from, StringComparison.Ordinal)) >= 0)
            {
                found.Add(at);
                from = at + 1;
            }

            int member = fullName.LastIndexOf('.');
            if (member <= 0)
            {
                return found;
            }

            int declaring = fullName.LastIndexOf('.', member - 1);
            if (declaring < 0)
            {
                return found;
            }

            string shortForm = fullName.Substring(declaring + 1);
            from = 0;
            while ((at = code.IndexOf(shortForm, from, StringComparison.Ordinal)) >= 0)
            {
                found.Add(at);
                from = at + 1;
            }

            return found;
        }

        /// <summary>
        /// The namespaces a file imported, read from lines the comments have already been taken out
        /// of — a <c>using</c> with a note after it is still a <c>using</c>.
        /// </summary>
        private static HashSet<string> UsingsIn(IEnumerable<string> lines)
        {
            HashSet<string> usings = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                string name;
                string alias;
                if (!TryReadUsing(line, out name, out alias) || alias != null)
                {
                    continue;
                }

                usings.Add(name);
            }

            return usings;
        }

        /// <summary>The aliases a file declared, as alias name to what it stands for.</summary>
        private static Dictionary<string, string> AliasesIn(IEnumerable<string> lines)
        {
            Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                string name;
                string alias;
                if (!TryReadUsing(line, out name, out alias) || alias == null)
                {
                    continue;
                }

                aliases[alias] = name;
            }

            return aliases;
        }

        /// <summary>
        /// Reads one <c>using</c> directive. <paramref name="alias"/> comes back null unless the
        /// directive named one.
        /// </summary>
        private static bool TryReadUsing(string line, out string name, out string alias)
        {
            name = null;
            alias = null;

            string trimmed = line.Trim();
            if (!trimmed.StartsWith("using ", StringComparison.Ordinal))
            {
                return false;
            }

            int semicolon = trimmed.IndexOf(';');
            if (semicolon < 0)
            {
                return false;
            }

            string body = trimmed.Substring(6, semicolon - 6).Trim();
            if (body.Length == 0)
            {
                return false;
            }

            if (body.StartsWith("static ", StringComparison.Ordinal))
            {
                body = body.Substring(7).Trim();
            }

            int equals = body.IndexOf('=');
            if (equals > 0)
            {
                alias = body.Substring(0, equals).Trim();
                name = body.Substring(equals + 1).Trim();
                return alias.Length > 0 && name.Length > 0;
            }

            name = body;
            return true;
        }

        /// <summary>
        /// Pulls out the contents of every double-quoted literal on one line of code.
        /// </summary>
        /// <remarks>
        /// Escaped quotes are honoured so that <c>"a \" b"</c> reads as one literal. A literal split
        /// across lines is read as two.
        /// </remarks>
        private static void CollectStringLiterals(
            string code,
            int lineNumber,
            List<string> literals,
            List<int> lineNumbers)
        {
            int i = 0;
            while (i < code.Length)
            {
                if (code[i] != '"')
                {
                    i++;
                    continue;
                }

                int start = i + 1;
                int at = start;
                while (at < code.Length)
                {
                    if (code[at] == '\\')
                    {
                        at += 2;
                        continue;
                    }

                    if (code[at] == '"')
                    {
                        break;
                    }

                    at++;
                }

                if (at > code.Length)
                {
                    at = code.Length;
                }

                if (at > start)
                {
                    literals.Add(code.Substring(start, Math.Min(at, code.Length) - start));
                    lineNumbers.Add(lineNumber);
                }

                i = at + 1;
            }
        }

        private static IEnumerable<int> WordMentions(string code, string word)
        {
            List<int> found = new List<int>();
            int from = 0;
            int at;
            while (FindWord(code, word, from, out at))
            {
                found.Add(at);
                from = at + 1;
            }

            return found;
        }

        private static IEnumerable<int> FollowedByAnyOf(string code, string word, string openers)
        {
            List<int> found = new List<int>();
            int from = 0;
            int at;
            while (FindWord(code, word, from, out at))
            {
                from = at + 1;

                int after = at + word.Length;
                while (after < code.Length && (code[after] == ' ' || code[after] == '\t'))
                {
                    after++;
                }

                if (after < code.Length && openers.IndexOf(code[after]) >= 0)
                {
                    found.Add(at);
                }
            }

            return found;
        }

        /// <summary>
        /// Finds <paramref name="word"/> standing on its own — not part of a longer identifier and
        /// not the tail of a member access, so <c>x.Code</c> does not count as <c>Code</c>.
        /// </summary>
        private static bool FindWord(string code, string word, int from, out int at)
        {
            while (from <= code.Length - word.Length)
            {
                at = code.IndexOf(word, from, StringComparison.Ordinal);
                if (at < 0)
                {
                    break;
                }

                from = at + 1;

                if (at > 0)
                {
                    char before = code[at - 1];
                    if (before == '.' || before == '@' || before == '_' || char.IsLetterOrDigit(before))
                    {
                        continue;
                    }
                }

                int after = at + word.Length;
                if (after < code.Length &&
                    (code[after] == '_' || char.IsLetterOrDigit(code[after])))
                {
                    continue;
                }

                return true;
            }

            at = -1;
            return false;
        }

        /// <summary>
        /// Returns the line with comments removed, carrying block-comment and verbatim-string state
        /// across lines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Comments are stripped because these namespaces get discussed constantly in the very doc
        /// comments that explain why they are banned — a scanner that flagged its own explanations
        /// would be turned off within a day.
        /// </para>
        /// <para>
        /// STRING LITERALS ARE STEPPED OVER, not stripped: their text stays in the result, but a
        /// <c>/*</c> or <c>//</c> inside one no longer starts a comment. That was not a theoretical
        /// hole. One authoring path in the shipped tree reads
        /// <c>"Assets/&lt;YourDimension&gt;/Authoring/Generation/Passes/*.asset"</c>, and the
        /// <c>/*</c> in it opened a block comment that never closed, so the last 133 lines of that
        /// file were matched against nothing at all.
        /// </para>
        /// </remarks>
        private static string StripComments(string line, ref bool inBlockComment, ref bool inVerbatimString)
        {
            StringBuilder code = new StringBuilder(line.Length);
            for (int i = 0; i < line.Length; i++)
            {
                if (inVerbatimString)
                {
                    code.Append(line[i]);
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            code.Append(line[i + 1]);
                            i++;
                            continue;
                        }

                        inVerbatimString = false;
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (line[i] == '*' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (line[i] == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    code.Append(line[i]);
                    code.Append(line[i + 1]);
                    i++;
                    inVerbatimString = true;
                    continue;
                }

                if (line[i] == '"')
                {
                    i = CopyLiteral(line, i, '"', code) - 1;
                    continue;
                }

                if (line[i] == '\'')
                {
                    i = CopyLiteral(line, i, '\'', code) - 1;
                    continue;
                }

                if (line[i] == '/' && i + 1 < line.Length)
                {
                    if (line[i + 1] == '/')
                    {
                        break;
                    }

                    if (line[i + 1] == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                }

                code.Append(line[i]);
            }

            return code.ToString();
        }

        /// <summary>
        /// Copies an ordinary quoted literal through, honouring backslash escapes, and answers with
        /// the index just past its closing quote.
        /// </summary>
        private static int CopyLiteral(string line, int start, char quote, StringBuilder code)
        {
            code.Append(line[start]);
            int i = start + 1;
            while (i < line.Length)
            {
                if (line[i] == '\\' && i + 1 < line.Length)
                {
                    code.Append(line[i]);
                    code.Append(line[i + 1]);
                    i += 2;
                    continue;
                }

                code.Append(line[i]);
                i++;
                if (line[i - 1] == quote)
                {
                    return i;
                }
            }

            return i;
        }
    }
}
