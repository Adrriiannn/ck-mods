using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Finds references in the framework's RUNTIME sources that Core Keeper's mod sandbox rejects at
    /// load time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A SCANNER EXISTS AT ALL. A mod's runtime assemblies are recompiled by the game through
    /// RoslynCSharp and then security-checked before anything runs. A rejected reference fails the
    /// WHOLE mod — the player is told "A mod failed to load: … (Compilation failed)" with no way to
    /// override — and an offline <c>dotnet build</c> is perfectly green, because the restriction has
    /// nothing to do with C#. That is the worst shape a bug can have: invisible until the game loads
    /// it, and total when it lands. This turns it into a test failure instead.
    /// </para>
    /// <para>
    /// WHERE THE LIST COMES FROM. The rules live in the game's serialized
    /// <c>RoslynCSharp.Settings.SecurityRestrictions</c>, read out of
    /// <c>Resources/RoslynCSharpSettings.asset</c> in the ripped game files. This class carries no
    /// copy of them. It reads <c>ExpandNullforge/Docs/SandboxDenyList.txt</c>, which is that asset's
    /// deny lists transcribed, so the guard and the rule cannot drift apart — and it reports the
    /// missing file as a finding rather than passing, because a guard that quietly checks nothing is
    /// worse than none.
    /// </para>
    /// <para>
    /// WHAT IT SCANS is worked out from the asmdefs rather than from a list written here. Everything
    /// under <c>Assets/ExpandNullforge</c> is compiled into an assembly the game loads unless the
    /// nearest asmdef above it says <c>includePlatforms: ["Editor"]</c>, which is exactly the rule
    /// Unity itself applies. A hard-coded folder list would have gone stale the first time a runtime
    /// script was written outside the two folders it named.
    /// </para>
    /// <para>
    /// AND IT SCANS THE CONSUMER'S MOD TOO. <see cref="ScanConsumers"/> walks every folder this
    /// framework has generated runtime C# into, by the same rules. The generated bootstrap is an
    /// <c>IMod</c> in the CREATOR's assembly, security-checked exactly the way ours is, and until
    /// that was scanned a violation there reached them as "Compilation failed" with no line number
    /// and nothing asserting otherwise anywhere.
    /// </para>
    /// <para>
    /// WHAT IT READS IS SOURCE TEXT, and the sandbox reads compiled IL. The two agree for code
    /// written the ordinary way and part company where a name never appears in the file: a reference
    /// introduced by a source generator, or an extension method whose defining namespace is never
    /// written down. <see cref="DimensionSandboxAssemblyGuard"/> reads the built assembly for
    /// exactly that reason and is the stricter of the two; this one runs before there is anything
    /// built, and on the C# this framework writes into somebody else's project, where there is no
    /// assembly to read at all.
    /// </para>
    /// </remarks>
    public static partial class DimensionSandboxGuard
    {
        /// <summary>
        /// The project-relative folder that holds everything this framework ships. Scanning starts
        /// here and steps around whatever the asmdefs mark Editor-only.
        /// </summary>
        public const string ShippedRoot = "ExpandNullforge";

        /// <summary>
        /// The fewest shipped source files this framework can plausibly have. Anything below it
        /// means the scan found nothing to look at, not that there is nothing wrong.
        /// </summary>
        /// <remarks>
        /// The ship set is 743 files — measured by walking it, not remembered. It said 687, then
        /// 662, and neither number had ever been re-walked after the tree grew. The largest
        /// planned change to the set, moving the
        /// authoring engine behind the Editor boundary, takes out about forty. One
        /// <c>"includePlatforms": ["Editor"]</c> line in <c>ExpandNullforge.asmdef</c> takes it to
        /// zero instead, and every check that walks the set then reports clean having read nothing.
        /// The floor sits far below the real count so ordinary work never trips it, and far above
        /// zero so that mistake cannot pass. Raise it when the tree genuinely grows; never lower it
        /// to make a run go green.
        /// </remarks>
        public const int FewestPlausibleShippedFiles = 400;

        /// <summary>
        /// The transcribed deny list, project-relative. Kept beside the docs rather than in code so
        /// that the record and the check are the same text.
        /// </summary>
        public const string DenyListPath = "ExpandNullforge/Docs/SandboxDenyList.txt";

        /// <summary>
        /// Where the emitters that WRITE runtime C# into a consumer's project live, and what their
        /// files are called.
        /// </summary>
        /// <remarks>
        /// Their string literals are scanned as well, because a denied reference in emitted source
        /// fails the consumer's mod exactly the way one in ours would fail this one. The files are
        /// found by name rather than listed one by one: a hand-written list names the ten partials
        /// that exist when it is written, and an eleventh is scanned by nobody.
        /// </remarks>
        /// <remarks>
        /// THIS IS A FOLDER TO SEARCH THROUGH, not the folder the partials sit in. They sat
        /// directly here until the restructure put all thirty-two in
        /// <c>ExpandNullforge/Editor/Bootstrap</c>; the search steps through subfolders so that
        /// move — and the next one — costs nothing. Narrowing this to the folder they are in today
        /// would put the same trap back one level down.
        /// </remarks>
        public const string GeneratedSourceEmitterFolder = "ExpandNullforge/Editor";

        /// <summary>The filename prefix every consumer-bootstrap emitter partial shares.</summary>
        public const string GeneratedSourceEmitterPrefix = "DimensionRuntimeConsumerBootstrapUtility";

        /// <summary>
        /// The folder, relative to a consumer's mod root, that generated runtime C# is written to.
        /// </summary>
        /// <remarks>
        /// <c>DimensionRuntimeConsumerBootstrapUtility.EnsureGeneratedRuntime</c> writes
        /// <c>modRoot + "/Scripts/Generated"</c>, and the class it writes there is always named
        /// <c>…RuntimeBootstrap</c>. Those two facts are how a consumer mod is recognised on disk
        /// without asking Unity, which is what lets this run in a plain console as well as in the
        /// editor.
        /// </remarks>
        public const string GeneratedConsumerScriptFolder = "Scripts/Generated";

        /// <summary>The filename ending every generated consumer bootstrap shares.</summary>
        public const string GeneratedConsumerScriptSuffix = "RuntimeBootstrap.cs";

        /// <summary>
        /// One reading of <c>SandboxDenyList.txt</c>.
        /// </summary>
        public sealed class DenyList
        {
            /// <summary>Namespace prefixes, with the asset's trailing ".*" removed.</summary>
            public List<string> Namespaces { get; } = new List<string>();

            /// <summary>Denied types, by full name.</summary>
            public List<string> Types { get; } = new List<string>();

            /// <summary>Denied members, by full name.</summary>
            public List<string> Members { get; } = new List<string>();

            /// <summary>
            /// Full type names the C# compiler puts in every assembly on its own, which the source
            /// never mentions and the game's own checker never walks.
            /// </summary>
            /// <remarks>
            /// Only <see cref="DimensionSandboxAssemblyGuard"/> uses this, because only it reads the
            /// built assembly, where they appear. The transcript beside the docs records, per entry,
            /// where each was measured and why the game does not see it. Anything NOT on this list
            /// is reported, so a new one fails the build and a person decides.
            /// </remarks>
            public List<string> CompilerEmitted { get; } = new List<string>();

            /// <summary>Whether the sandbox permits P/Invoke. It does not.</summary>
            public bool AllowPInvoke { get; set; } = true;

            /// <summary>Whether the sandbox permits unsafe code. It does not.</summary>
            public bool AllowUnsafeCode { get; set; } = true;

            /// <summary>
            /// A list with no entries at all, which would let anything through and so is treated as
            /// a broken read rather than as permission.
            /// </summary>
            public bool IsEmpty
            {
                get { return Namespaces.Count == 0 && Types.Count == 0 && Members.Count == 0; }
            }
        }

        public readonly struct Finding
        {
            public Finding(string file, int line, string rule, string text)
            {
                File = file;
                Line = line;
                Rule = rule;
                Text = text;
            }

            public string File { get; }
            public int Line { get; }

            /// <summary>The deny entry that matched, written as the game's own asset writes it.</summary>
            public string Rule { get; }

            public string Text { get; }

            public override string ToString()
            {
                return File + ":" + Line + " references " + Rule + " — " + Text.Trim();
            }
        }

        /// <summary>
        /// Reads the transcribed deny list. Returns null when the file is missing or unreadable.
        /// </summary>
        public static DenyList ReadDenyList(string assetsPath)
        {
            if (string.IsNullOrEmpty(assetsPath))
            {
                return null;
            }

            string path = Path.Combine(assetsPath, DenyListPath);
            string[] lines;
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    return null;
                }

                lines = System.IO.File.ReadAllLines(path);
            }
            catch (Exception)
            {
                return null;
            }

            DenyList list = new DenyList();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf('#');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string kind = line.Substring(0, colon).Trim();
                string entry = line.Substring(colon + 1).Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                if (string.Equals(kind, "namespace", StringComparison.Ordinal))
                {
                    if (entry.EndsWith(".*", StringComparison.Ordinal))
                    {
                        entry = entry.Substring(0, entry.Length - 2);
                    }

                    list.Namespaces.Add(entry);
                }
                else if (string.Equals(kind, "type", StringComparison.Ordinal))
                {
                    list.Types.Add(entry);
                }
                else if (string.Equals(kind, "member", StringComparison.Ordinal))
                {
                    list.Members.Add(entry);
                }
                else if (string.Equals(kind, "compiler-emitted", StringComparison.Ordinal))
                {
                    list.CompilerEmitted.Add(entry);
                }
                else if (string.Equals(kind, "setting", StringComparison.Ordinal))
                {
                    ApplySetting(list, entry);
                }
            }

            return list;
        }

        private static void ApplySetting(DenyList list, string entry)
        {
            int equals = entry.IndexOf('=');
            if (equals <= 0)
            {
                return;
            }

            string name = entry.Substring(0, equals).Trim();
            bool value = string.Equals(
                entry.Substring(equals + 1).Trim(),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (string.Equals(name, "allowPInvoke", StringComparison.Ordinal))
            {
                list.AllowPInvoke = value;
            }
            else if (string.Equals(name, "allowUnsafeCode", StringComparison.Ordinal))
            {
                list.AllowUnsafeCode = value;
            }
        }

        /// <summary>
        /// Scans every shipped source file under <paramref name="assetsPath"/> against the deny list
        /// transcribed beside it, and returns each denied reference. Empty means the runtime
        /// assemblies are clean.
        /// </summary>
        /// <remarks>
        /// A missing or empty deny list comes back as a finding of its own. The alternative is a
        /// test that passes because it checked nothing, which is the failure this whole class was
        /// written to prevent.
        /// </remarks>
        public static List<Finding> Scan(string assetsPath)
        {
            DenyList denyList = ReadDenyList(assetsPath);
            if (denyList == null || denyList.IsEmpty)
            {
                return new List<Finding>
                {
                    new Finding(
                        DenyListPath,
                        0,
                        "the deny list itself",
                        "The transcribed deny list is missing or has no entries, so nothing was " +
                        "checked. Restore Assets/" + DenyListPath + " from " +
                        "ck-research/sandbox-compliance.md."),
                };
            }

            string shipSetProblem = ShipSetProblem(assetsPath, ShippedSourceFiles(assetsPath).Count);
            if (shipSetProblem != null)
            {
                return new List<Finding>
                {
                    new Finding(ShippedRoot, 0, "the ship set itself", shipSetProblem),
                };
            }

            return Scan(assetsPath, denyList);
        }

        /// <summary>Scans the shipped sources under a root against a supplied deny list.</summary>
        public static List<Finding> Scan(string rootPath, DenyList denyList)
        {
            List<Finding> findings = new List<Finding>();
            if (string.IsNullOrEmpty(rootPath) || denyList == null)
            {
                return findings;
            }

            List<string> files = ShippedSourceFiles(rootPath);
            for (int i = 0; i < files.Count; i++)
            {
                ScanFile(files[i], denyList, findings);
            }

            return findings;
        }
    }
}
