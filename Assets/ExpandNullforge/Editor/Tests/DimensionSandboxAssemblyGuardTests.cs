#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Fails the build when the ASSEMBLY Unity just built carries a reference the mod sandbox
    /// rejects.
    /// </summary>
    /// <remarks>
    /// The text scanner beside this one reads source and can be fooled by how the source is laid
    /// out — six ways were found and closed, and closing six is not the same as there being six.
    /// This reads the compiled reference tables, which is what the game reads, so the layout of the
    /// source stops mattering. It runs second because it needs a build; the text scanner runs on
    /// text that has not been built yet and on the C# this framework writes into somebody else's
    /// project, where there is nothing to read.
    /// </remarks>
    internal sealed class DimensionSandboxAssemblyGuardTests
    {
        [Test]
        public void TheBuiltRuntimeAssembliesUseNothingTheSandboxDenies()
        {
            DimensionSandboxGuard.DenyList denyList =
                DimensionSandboxGuard.ReadDenyList(Application.dataPath);
            Assert.That(
                denyList,
                Is.Not.Null,
                "The transcribed deny list is missing, so this test would have proved nothing.");

            // The framework's two, plus the assembly of every generated consumer mod in the
            // project. A creator's mod carries the generated IMod and is security-checked exactly
            // the way ours is, so leaving it out asserted its compliance nowhere.
            List<string> assemblies =
                DimensionSandboxAssemblyGuard.AssembliesToScan(Application.dataPath);
            Assert.That(
                assemblies.Count,
                Is.GreaterThan(DimensionSandboxAssemblyGuard.ShippedAssemblies.Length),
                "No consumer mod assembly was added to the framework's own two, so the C# this " +
                "framework writes into somebody else's project was not read here at all. " +
                // WHY THE COUNT IS SPELLED OUT RATHER THAN LEFT TO ConsumerSetProblem. That
                // sentence only exists for the zero case and answers null otherwise, so with one
                // consumer found and no assembly name for it — a mod folder with no .asmdef — this
                // message ended mid-explanation and then said no consumer had been found, which was
                // the opposite of what happened.
                WhyNoConsumerAssembly(Application.dataPath));

            List<DimensionSandboxGuard.Finding> findings = new List<DimensionSandboxGuard.Finding>();
            int scanned = 0;

            for (int i = 0; i < assemblies.Count; i++)
            {
                string path = BuiltAssembly(assemblies[i]);
                if (!File.Exists(path))
                {
                    continue;
                }

                scanned++;
                findings.AddRange(DimensionSandboxAssemblyGuard.ScanAssembly(path, denyList));
            }

            Assert.That(
                scanned,
                Is.EqualTo(assemblies.Count),
                "Not every shipped assembly was found under " +
                DimensionSandboxAssemblyGuard.BuiltAssemblyFolder + ", so this checked less than " +
                "it claims to. Looked for: " + string.Join(", ", assemblies.ToArray()));

            Assert.That(
                findings,
                Is.Empty,
                "The built assembly references something the mod sandbox denies, so the game will " +
                "refuse the whole mod and tell the player only that compilation failed. If the " +
                "reference is the framework's own, take it out. If it is one the C# compiler or " +
                "Burst put there by itself, find out where it is actually used before believing " +
                "that: only then add it to the compiler-emitted list in Assets/" +
                DimensionSandboxGuard.DenyListPath + ", with the note that says where." +
                Detail(findings));
        }

        /// <summary>
        /// Why the framework's own two assemblies were the only ones to scan, in a whole sentence.
        /// </summary>
        /// <remarks>
        /// Three different states end here and they need three different answers: no consumer mod
        /// in the project at all, a consumer mod that ships nothing, and a consumer mod with no
        /// assembly definition — whose code goes into Assembly-CSharp and which the assembly half
        /// of this guard therefore cannot name. Reporting the first sentence for all three is what
        /// made this message say "no consumer found" about a project that had one.
        /// </remarks>
        private static string WhyNoConsumerAssembly(string assetsPath)
        {
            List<string> roots = DimensionSandboxGuard.ConsumerModRoots(assetsPath);
            string noneAtAll = DimensionSandboxGuard.ConsumerSetProblem(assetsPath, roots.Count);
            if (noneAtAll != null)
            {
                return noneAtAll;
            }

            List<string> unnamed = new List<string>();
            for (int i = 0; i < roots.Count; i++)
            {
                if (string.IsNullOrEmpty(DimensionSandboxGuard.ShippedAssemblyNameOf(roots[i])))
                {
                    unnamed.Add(roots[i]);
                }
            }

            if (unnamed.Count == roots.Count)
            {
                return roots.Count + " consumer mod(s) were found and not one of them names an " +
                       "assembly this check could read: " + string.Join(", ", unnamed.ToArray()) +
                       ". A mod folder with no .asmdef, or one marked \"includePlatforms\": " +
                       "[\"Editor\"], compiles into Assembly-CSharp or into nothing, and either " +
                       "way there is no DLL here to open.";
            }

            return roots.Count + " consumer mod(s) were found and AssembliesToScan added none of " +
                   "them, which is a fault in AssembliesToScan rather than in the project: " +
                   string.Join(", ", roots.ToArray());
        }

        /// <summary>
        /// The reader is only worth trusting if it would actually fire, so point it at an assembly
        /// that is FULL of denied references and check that it says so.
        /// </summary>
        /// <remarks>
        /// <c>ExpandNullforge.Editor</c> is the right target: it never ships, so it uses
        /// <c>System.IO</c> and <c>System.Reflection</c> freely and always will. A reader that had
        /// quietly stopped parsing — a metadata layout it did not expect, a stream it could not
        /// find — would return an empty list here, and the test above would go green on nothing.
        /// </remarks>
        [Test]
        public void TheReaderFindsWhatIsThereWhenSomethingIsThere()
        {
            DimensionSandboxGuard.DenyList denyList =
                DimensionSandboxGuard.ReadDenyList(Application.dataPath);
            Assert.That(denyList, Is.Not.Null);

            string path = BuiltAssembly("ExpandNullforge.Editor.dll");
            Assert.That(
                File.Exists(path),
                "ExpandNullforge.Editor.dll was not found; without it nothing proves the reader " +
                "still reads.");

            List<DimensionSandboxGuard.Finding> findings =
                DimensionSandboxAssemblyGuard.ScanAssembly(path, denyList);

            Assert.That(
                Mentions(findings, "System.IO.Path"),
                Is.True,
                "The editor assembly certainly uses System.IO.Path, and the reader did not see " +
                "it, so it is not reading this assembly at all." + Detail(findings));
        }

        /// <summary>A file that is not an assembly is reported, not passed over.</summary>
        [Test]
        public void SomethingThatIsNotAnAssemblyIsAFindingAndNotAPass()
        {
            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");

            string path = Path.Combine(Path.GetTempPath(), "nf_not_an_assembly.dll");
            File.WriteAllText(path, "this is not a PE file");

            try
            {
                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxAssemblyGuard.ScanAssembly(path, denyList);

                Assert.That(findings, Is.Not.Empty);
                Assert.That(findings[0].Rule, Does.Contain("assembly"));
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // A leftover temp file is not worth failing a passing test over.
                }
            }
        }

        private static string BuiltAssembly(string name)
        {
            return Path.Combine(
                Application.dataPath,
                "..",
                DimensionSandboxAssemblyGuard.BuiltAssemblyFolder,
                name);
        }

        private static bool Mentions(List<DimensionSandboxGuard.Finding> findings, string text)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Text.Contains(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Detail(List<DimensionSandboxGuard.Finding> findings)
        {
            string detail = string.Empty;
            for (int i = 0; i < findings.Count; i++)
            {
                detail += "\n  " + findings[i];
            }

            return detail;
        }
    }
}
#endif
