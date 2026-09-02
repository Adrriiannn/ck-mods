using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Finding the mods built with this framework, and scanning what the generator wrote.
    /// </summary>
    public static partial class DimensionSandboxGuard
    {
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
        /// <para>
        /// AND A MOD THAT HAS NOT BEEN GENERATED YET IS STILL A CONSUMER. Looking only for the
        /// bootstrap made a stale mod invisible: it holds hand-written C# that ships and is
        /// security-checked, and beside one generated mod it was reported as a clean run rather
        /// than as an unscanned one, because the "did this find anything" question was asked of the
        /// whole project. The second signature closes that — an <c>.asmdef</c> whose
        /// <c>references</c> name <see cref="ShippedRoot"/> or its API assembly is a mod built
        /// against this framework whether or not a generate has ever been run in it, and no mod
        /// that is not built against this framework carries that line. Both signatures find the two
        /// mods in this repository, which is why widening it changes no result here.
        /// </para>
        /// </remarks>
        public static List<string> ConsumerModRoots(string assetsPath)
        {
            List<string> roots = new List<string>();
            if (string.IsNullOrEmpty(assetsPath) || !Directory.Exists(assetsPath))
            {
                return roots;
            }

            AddRootsThatReferenceTheFramework(assetsPath, roots);

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
        /// Adds every mod folder whose asmdef is built against this framework.
        /// </summary>
        /// <remarks>
        /// The asmdef's own folder is the mod root, which is how Unity treats it and how the
        /// bootstrap emitter treats it. Editor-only asmdefs are kept here rather than filtered:
        /// <see cref="ScanConsumers"/> is the half that decides what a mod with no shipped source
        /// means, and it already has a sentence for it.
        /// </remarks>
        private static void AddRootsThatReferenceTheFramework(string assetsPath, List<string> roots)
        {
            string[] asmdefs;
            try
            {
                asmdefs = Directory.GetFiles(assetsPath, "*.asmdef", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                return;
            }

            string frameworkRoot = Path.Combine(assetsPath, ShippedRoot);
            for (int i = 0; i < asmdefs.Length; i++)
            {
                string root = Path.GetDirectoryName(asmdefs[i]);
                if (string.IsNullOrEmpty(root) || IsWithin(root, frameworkRoot) ||
                    roots.Contains(root))
                {
                    continue;
                }

                string text;
                try
                {
                    text = System.IO.File.ReadAllText(asmdefs[i]);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!ReferencesTheFramework(text))
                {
                    continue;
                }

                roots.Add(root);
            }
        }

        /// <summary>
        /// Whether an asmdef's <c>references</c> name this framework's runtime or API assembly.
        /// </summary>
        /// <remarks>
        /// A quoted whole-word match rather than a substring, so a mod named
        /// <c>ExpandNullforgeExtras</c> in somebody's own reference list is not mistaken for one of
        /// ours. GUID references are not matched and cannot be: the emitter writes names
        /// (<c>"useGUIDs": false</c> on every mod it touches), and a project that switched to GUIDs
        /// would be found by its generated bootstrap instead.
        /// </remarks>
        private static bool ReferencesTheFramework(string asmdef)
        {
            if (string.IsNullOrEmpty(asmdef))
            {
                return false;
            }

            return asmdef.IndexOf("\"" + ShippedRoot + "\"", StringComparison.Ordinal) >= 0 ||
                asmdef.IndexOf("\"" + ShippedRoot + ".API\"", StringComparison.Ordinal) >= 0;
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

                // NO ASMDEF IS NOT NO PROBLEM. Unity compiles a folder with no asmdef into
                // Assembly-CSharp, which ships and is security-checked like any other — but
                // ShippedAssemblyNameOf has no name to give, so AssembliesToScan cannot add it and
                // the assembly half of the guard never sees this mod at all. Saying it here is what
                // stops the source scan's clean result being read as a clean result for the mod.
                // Reachable: the framework never writes an asmdef, and EnsureConsumerAssemblyReferences
                // only edits one that already exists.
                if (string.IsNullOrEmpty(ShippedAssemblyNameOf(roots[i])))
                {
                    findings.Add(new Finding(
                        roots[i],
                        0,
                        "the consumer's assembly definition",
                        "This mod has no .asmdef of its own, so its code is compiled into " +
                        "Assembly-CSharp. Its source is checked below, and its compiled assembly " +
                        "is not: nothing here can name a DLL to read. Add an assembly definition " +
                        "to the mod folder, which is what the game expects a mod to ship as."));
                }

                for (int f = 0; f < files.Count; f++)
                {
                    ScanFile(files[f], denyList, findings);
                }
            }

            return findings;
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
    }
}
