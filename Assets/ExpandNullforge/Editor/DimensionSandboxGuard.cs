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
    public static class DimensionSandboxGuard
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
        /// The ship set is 687 files, and the largest planned change to it — moving the authoring
        /// engine behind the Editor boundary — takes out about forty. One
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
        /// found by name rather than listed one by one: the list that used to be here named ten
        /// partials, and an eleventh would have been scanned by nobody.
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

        /// <summary>
        /// Says why a ship set of <paramref name="shippedCount"/> files cannot be trusted, or null
        /// when the count is plausible.
        /// </summary>
        /// <remarks>
        /// Every check that walks the ship set has to ask this, because the set coming back empty
        /// and the set coming back clean look identical from inside a loop that never runs. The
        /// three ways it comes back empty are a missing <c>ExpandNullforge</c> folder, an
        /// enumeration that threw, and the root asmdef being marked Editor-only — none of which is
        /// visible to a caller holding an empty list.
        /// </remarks>
        public static string ShipSetProblem(string rootPath, int shippedCount)
        {
            if (shippedCount >= FewestPlausibleShippedFiles)
            {
                return null;
            }

            return "The shipped source set came back with " + shippedCount + " file(s), and " +
                   FewestPlausibleShippedFiles + " is the fewest this framework can plausibly " +
                   "have, so whatever walked it read almost nothing and cannot have found " +
                   "anything. Check that " + rootPath + "/" + ShippedRoot + " exists, that " +
                   ShippedRoot + "/ExpandNullforge.asmdef does not say " +
                   "\"includePlatforms\": [\"Editor\"], and that the folder is readable.";
        }

        /// <summary>
        /// Every C# file under <paramref name="rootPath"/> that is compiled into an assembly the
        /// game loads.
        /// </summary>
        /// <remarks>
        /// Public because it is the answer to "what ships", and more than one check needs it — the
        /// Burst budget test asks the same question. Two places working it out separately is how
        /// one of them ends up looking at a folder the other does not.
        /// </remarks>
        public static List<string> ShippedSourceFiles(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return new List<string>();
            }

            return ShippedSourceFilesUnder(Path.Combine(rootPath, ShippedRoot));
        }

        /// <summary>
        /// The same walk, over any one folder rather than over the framework's own.
        /// </summary>
        /// <remarks>
        /// Split out so a consumer's mod folder gets the identical treatment: the same asmdef
        /// reading, the same nearest-match rule, the same silence on an unreadable folder. A second
        /// walk written beside it would be the way the two end up disagreeing about what ships.
        /// </remarks>
        public static List<string> ShippedSourceFilesUnder(string folder)
        {
            List<string> shipped = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return shipped;
            }

            List<string> editorOnly = EditorOnlyFolders(folder);
            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return shipped;
            }

            for (int i = 0; i < files.Length; i++)
            {
                if (IsShipped(files[i], editorOnly))
                {
                    shipped.Add(files[i]);
                }
            }

            return shipped;
        }

        /// <summary>
        /// The mod folders under <paramref name="assetsPath"/> that this framework has generated
        /// runtime C# into.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY THE CONSUMER IS SCANNED AT ALL. The generated bootstrap is an <c>IMod</c> compiled
        /// into the CONSUMER's assembly, not into ours, and the game security-checks that assembly
        /// exactly the way it checks this one. Everything the framework's own scan is for applies
        /// to it word for word, and until this existed nothing asserted it: a denied reference in
        /// a creator's mod folder reached them as "Compilation failed" with no line number.
        /// </para>
        /// <para>
        /// FOUND, NOT LISTED. A creator names their own mod folder, so no constant here could name
        /// it. What is constant is where the generator puts its output —
        /// <c>&lt;modRoot&gt;/Scripts/Generated/…RuntimeBootstrap.cs</c> — so that is what is
        /// looked for, and the mod root is the folder two above it. The framework's own folder is
        /// excluded: the generator refuses to write into it, and its scan is the other one.
        /// </para>
        /// </remarks>
        public static List<string> ConsumerModRoots(string assetsPath)
        {
            List<string> roots = new List<string>();
            if (string.IsNullOrEmpty(assetsPath) || !Directory.Exists(assetsPath))
            {
                return roots;
            }

            string[] bootstraps;
            try
            {
                bootstraps = Directory.GetFiles(
                    assetsPath, "*" + GeneratedConsumerScriptSuffix, SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return roots;
            }

            string frameworkRoot = Path.Combine(assetsPath, ShippedRoot);
            string[] parts = GeneratedConsumerScriptFolder.Split('/');
            for (int i = 0; i < bootstraps.Length; i++)
            {
                string folder = Path.GetDirectoryName(bootstraps[i]);
                string root = folder;
                bool matches = true;
                for (int p = parts.Length - 1; p >= 0 && matches; p--)
                {
                    matches = root != null &&
                        string.Equals(
                            Path.GetFileName(root), parts[p], StringComparison.OrdinalIgnoreCase);
                    if (matches)
                    {
                        root = Path.GetDirectoryName(root);
                    }
                }

                if (!matches || string.IsNullOrEmpty(root) || IsWithin(root, frameworkRoot))
                {
                    continue;
                }

                if (!roots.Contains(root))
                {
                    roots.Add(root);
                }
            }

            return roots;
        }

        /// <summary>
        /// Says why a run that found <paramref name="rootCount"/> consumer mods checked nothing, or
        /// null when it found at least one.
        /// </summary>
        /// <remarks>
        /// Same shape as <see cref="ShipSetProblem"/>, and for the same reason: zero consumers and
        /// zero denied references look identical to a loop, and the one that reads as a clean run
        /// is the one that checked nothing. A project with no generated dimension in it is a real
        /// state, which is why this returns a sentence explaining that rather than pretending a
        /// violation was found.
        /// </remarks>
        public static string ConsumerSetProblem(string assetsPath, int rootCount)
        {
            if (rootCount > 0)
            {
                return null;
            }

            return "No generated consumer mod was found under " + assetsPath + ", so the C# this " +
                   "framework writes into somebody else's project was not checked against the " +
                   "sandbox. A consumer mod is recognised by " + GeneratedConsumerScriptFolder +
                   "/*" + GeneratedConsumerScriptSuffix + " inside it. Open a Dimension Asset and " +
                   "generate its runtime output, or move this check to a project that has one.";
        }

        /// <summary>
        /// Scans every generated consumer mod's shipped sources against the deny list.
        /// </summary>
        /// <remarks>
        /// This reads the file on disk, which is the generator's actual output — stricter than
        /// <see cref="ScanGeneratedSources"/>, which can only read the literals the emitters are
        /// built from and never sees a name assembled at run time. The two are worth having
        /// together: the emitter scan runs in a project that has never generated anything, and this
        /// one runs on what was really written.
        /// </remarks>
        public static List<Finding> ScanConsumers(string assetsPath, DenyList denyList)
        {
            List<Finding> findings = new List<Finding>();
            if (string.IsNullOrEmpty(assetsPath) || denyList == null)
            {
                return findings;
            }

            List<string> roots = ConsumerModRoots(assetsPath);
            for (int i = 0; i < roots.Count; i++)
            {
                List<string> files = ShippedSourceFilesUnder(roots[i]);
                if (files.Count == 0)
                {
                    // The bootstrap that identified this folder is itself a shipped file, so an
                    // empty set here means the mod's asmdef marks it Editor-only — in which case
                    // its IMod never loads, and saying so is more use than reporting it clean.
                    findings.Add(new Finding(
                        roots[i],
                        0,
                        "the consumer's ship set",
                        "This mod holds a generated runtime bootstrap and yet nothing in it is " +
                        "compiled into an assembly the game loads, so nothing was checked. Check " +
                        "that its .asmdef does not say \"includePlatforms\": [\"Editor\"]."));
                    continue;
                }

                for (int f = 0; f < files.Count; f++)
                {
                    ScanFile(files[f], denyList, findings);
                }
            }

            return findings;
        }

        /// <summary>
        /// The name of the assembly a consumer mod's shipped code is compiled into, or empty when
        /// its nearest asmdef is Editor-only or unreadable.
        /// </summary>
        /// <remarks>
        /// Read from the asmdef rather than from the folder name, because Unity names the assembly
        /// from the asmdef and the two need not match.
        /// </remarks>
        public static string ShippedAssemblyNameOf(string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot))
            {
                return string.Empty;
            }

            string[] asmdefs;
            try
            {
                asmdefs = Directory.GetFiles(modRoot, "*.asmdef", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return string.Empty;
            }

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string text;
                try
                {
                    text = System.IO.File.ReadAllText(asmdefs[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                if (SaysEditorOnly(text))
                {
                    continue;
                }

                string name = NameIn(text);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            return string.Empty;
        }

        /// <summary>An asmdef's <c>name</c>, which is what Unity calls the assembly it builds.</summary>
        private static string NameIn(string asmdef)
        {
            int at = asmdef.IndexOf("\"name\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return string.Empty;
            }

            int colon = asmdef.IndexOf(':', at);
            int open = colon < 0 ? -1 : asmdef.IndexOf('"', colon);
            int close = open < 0 ? -1 : asmdef.IndexOf('"', open + 1);
            if (open < 0 || close < 0)
            {
                return string.Empty;
            }

            return asmdef.Substring(open + 1, close - open - 1);
        }

        /// <summary>Whether one folder is <paramref name="ancestor"/> or sits inside it.</summary>
        private static bool IsWithin(string folder, string ancestor)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(ancestor))
            {
                return false;
            }

            if (folder.Length < ancestor.Length ||
                !folder.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return folder.Length == ancestor.Length ||
                folder[ancestor.Length] == Path.DirectorySeparatorChar ||
                folder[ancestor.Length] == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// The folders under <paramref name="root"/> whose asmdef says the assembly is Editor-only.
        /// </summary>
        /// <remarks>
        /// Unity compiles a script into the assembly of the NEAREST asmdef above it, so the check a
        /// file gets is the check of its closest enclosing asmdef folder — which is why the longest
        /// match wins in <see cref="IsShipped"/> rather than any match.
        /// </remarks>
        private static List<string> EditorOnlyFolders(string root)
        {
            List<string> folders = new List<string>();
            string[] asmdefs;
            try
            {
                asmdefs = Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return folders;
            }

            for (int i = 0; i < asmdefs.Length; i++)
            {
                string text;
                try
                {
                    text = System.IO.File.ReadAllText(asmdefs[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                if (SaysEditorOnly(text))
                {
                    folders.Add(Path.GetDirectoryName(asmdefs[i]));
                }
            }

            return folders;
        }

        /// <summary>
        /// Whether an asmdef's <c>includePlatforms</c> names Editor, which is Unity's own way of
        /// saying the assembly never reaches a build and so never reaches the sandbox.
        /// </summary>
        private static bool SaysEditorOnly(string asmdef)
        {
            int at = asmdef.IndexOf("\"includePlatforms\"", StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            int open = asmdef.IndexOf('[', at);
            int close = open < 0 ? -1 : asmdef.IndexOf(']', open);
            if (open < 0 || close < 0)
            {
                return false;
            }

            return asmdef.IndexOf("\"Editor\"", open, close - open, StringComparison.Ordinal) >= 0;
        }

        /// <summary>Whether one file is compiled into an assembly the game loads.</summary>
        private static bool IsShipped(string file, List<string> editorOnly)
        {
            string folder = Path.GetDirectoryName(file);
            if (folder == null)
            {
                return true;
            }

            for (int i = 0; i < editorOnly.Count; i++)
            {
                if (IsWithin(folder, editorOnly[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Scans the C# this framework WRITES into a consumer's project, by reading the string
        /// literals its emitters are built from.
        /// </summary>
        /// <remarks>
        /// The emitted file is not on disk at test time, so what is scanned is the text the emitters
        /// hold. That sees every line they spell out — the fourteen <c>using</c> directives among
        /// them — and does not see a name assembled from parts at run time. Worth having for what it
        /// covers; not worth mistaking for proof of the whole output. Finding no emitters at all is
        /// reported, because that reads exactly like a clean result and is not one.
        /// </remarks>
        public static List<Finding> ScanGeneratedSources(string assetsPath, DenyList denyList)
        {
            List<Finding> findings = new List<Finding>();
            if (string.IsNullOrEmpty(assetsPath) || denyList == null)
            {
                return findings;
            }

            string folder = Path.Combine(assetsPath, GeneratedSourceEmitterFolder);
            string[] emitters;
            try
            {
                emitters = Directory.Exists(folder)
                    ? Directory.GetFiles(folder, GeneratedSourceEmitterPrefix + "*.cs", SearchOption.TopDirectoryOnly)
                    : new string[0];
            }
            catch (Exception)
            {
                emitters = new string[0];
            }

            if (emitters.Length == 0)
            {
                findings.Add(new Finding(
                    GeneratedSourceEmitterFolder,
                    0,
                    "the emitters themselves",
                    "No " + GeneratedSourceEmitterPrefix + "*.cs was found, so the C# this " +
                    "framework writes into a consumer's project was not checked at all."));
                return findings;
            }

            for (int i = 0; i < emitters.Length; i++)
            {
                string[] lines;
                try
                {
                    lines = System.IO.File.ReadAllLines(emitters[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                List<string> emitted = new List<string>();
                List<int> emittedLines = new List<int>();
                bool inBlockComment = false;
                bool inVerbatimString = false;
                for (int n = 0; n < lines.Length; n++)
                {
                    string code = StripComments(lines[n], ref inBlockComment, ref inVerbatimString);
                    CollectStringLiterals(code, n + 1, emitted, emittedLines);
                }

                HashSet<string> usings = UsingsIn(emitted);
                Dictionary<string, string> aliases = AliasesIn(emitted);
                for (int n = 0; n < emitted.Count; n++)
                {
                    Source one = Source.FromSingleLine(emitted[n], emittedLines[n], usings, aliases);
                    Match(emitters[i], one, denyList, findings);
                }
            }

            return findings;
        }

        private static void ScanFile(string path, DenyList denyList, List<Finding> findings)
        {
            string[] lines;
            try
            {
                lines = System.IO.File.ReadAllLines(path);
            }
            catch (Exception)
            {
                // A file we cannot read is not a finding; the compiler will complain long before us.
                return;
            }

            Match(path, Source.Read(lines), denyList, findings);
        }

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
