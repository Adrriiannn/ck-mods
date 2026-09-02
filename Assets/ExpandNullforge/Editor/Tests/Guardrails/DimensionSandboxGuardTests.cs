#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Fails the build when a runtime source file references something Core Keeper's mod sandbox
    /// rejects.
    /// </summary>
    /// <remarks>
    /// This is a test rather than a menu item on purpose. The failure it guards against — the game
    /// refusing the entire mod, and telling the player only that compilation failed — is invisible to
    /// <c>dotnet build</c> and to Unity's compiler alike, so the only thing that catches it early is
    /// something that runs without being asked. A validation nobody remembers to click would have
    /// caught this exactly as often as no validation at all.
    /// </remarks>
    internal sealed class DimensionSandboxGuardTests
    {
        [Test]
        public void RuntimeSourcesUseNothingTheSandboxDenies()
        {
            List<DimensionSandboxGuard.Finding> findings =
                DimensionSandboxGuard.Scan(Application.dataPath);

            if (findings.Count == 0)
            {
                Assert.Pass();
                return;
            }

            Assert.Fail(
                findings.Count + " runtime reference(s) to something the mod sandbox denies. The " +
                "mod will fail to load in-game — the player is told only that compilation failed — " +
                "even though it compiles cleanly here. Move the work to an Editor-only assembly, or " +
                "do it through a seam the sandbox permits." + Detail(findings));
        }

        /// <summary>
        /// The C# this framework writes into a consumer's project has to clear the same bar, because
        /// it is loaded by the same sandbox in the same way.
        /// </summary>
        [Test]
        public void TheGeneratedConsumerBootstrapUsesNothingTheSandboxDenies()
        {
            DimensionSandboxGuard.DenyList denyList =
                DimensionSandboxGuard.ReadDenyList(Application.dataPath);
            Assert.That(
                denyList,
                Is.Not.Null,
                "The transcribed deny list is missing, so this test would have proved nothing.");

            List<DimensionSandboxGuard.Finding> findings =
                DimensionSandboxGuard.ScanGeneratedSources(Application.dataPath, denyList);

            Assert.That(
                findings,
                Is.Empty,
                "A bootstrap emitter writes a denied reference into the consumer's mod, which fails " +
                "their mod the same way it would fail this one." + Detail(findings));
        }

        /// <summary>
        /// And the mod folder the framework actually wrote that C# into, as it sits on disk.
        /// </summary>
        /// <remarks>
        /// The emitter scan above reads the literals the emitters are built from; this reads the
        /// file that came out, together with every other runtime script in the creator's mod. The
        /// generated bootstrap is an <c>IMod</c> in THEIR assembly, and the game security-checks
        /// that assembly by the same rules — so a denied reference there fails their mod with the
        /// same unexplained "Compilation failed" ours would have failed with.
        /// </remarks>
        [Test]
        public void TheGeneratedConsumerModsUseNothingTheSandboxDenies()
        {
            DimensionSandboxGuard.DenyList denyList =
                DimensionSandboxGuard.ReadDenyList(Application.dataPath);
            Assert.That(
                denyList,
                Is.Not.Null,
                "The transcribed deny list is missing, so this test would have proved nothing.");

            List<string> roots = DimensionSandboxGuard.ConsumerModRoots(Application.dataPath);
            string problem =
                DimensionSandboxGuard.ConsumerSetProblem(Application.dataPath, roots.Count);
            Assert.That(problem, Is.Null, problem);

            List<DimensionSandboxGuard.Finding> findings =
                DimensionSandboxGuard.ScanConsumers(Application.dataPath, denyList);

            Assert.That(
                findings,
                Is.Empty,
                "A generated consumer mod references something the mod sandbox denies, so the " +
                "game will refuse THAT mod and tell the player only that compilation failed." +
                Detail(findings));
        }

        /// <summary>
        /// A consumer mod is found by where the generator puts its output, and the framework's own
        /// folder is not mistaken for one.
        /// </summary>
        /// <remarks>
        /// Written against files made for the purpose rather than against the project, because the
        /// project is expected to be clean and a finder that had stopped finding would look exactly
        /// the same from the test above.
        /// </remarks>
        [Test]
        public void AConsumerModIsFoundByItsGeneratedBootstrapAndScanned()
        {
            string assets = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_consumer");
            string generated = Path.Combine(
                Path.Combine(assets, "TheirMod"),
                DimensionSandboxGuard.GeneratedConsumerScriptFolder
                    .Replace('/', Path.DirectorySeparatorChar));
            string frameworkGenerated = Path.Combine(
                Path.Combine(assets, DimensionSandboxGuard.ShippedRoot),
                DimensionSandboxGuard.GeneratedConsumerScriptFolder
                    .Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(generated);
            Directory.CreateDirectory(frameworkGenerated);

            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");

            try
            {
                File.WriteAllText(
                    Path.Combine(assets, "TheirMod", "TheirMod.asmdef"),
                    "{\n  \"name\": \"TheirMod\",\n  \"includePlatforms\": []\n}\n");
                File.WriteAllText(
                    Path.Combine(generated, "TheirModWhiskey" + DimensionSandboxGuard.GeneratedConsumerScriptSuffix),
                    "using System.IO;\npublic class A { }\n");

                // A hand-written runtime script beside the generated one is compiled into the same
                // assembly and checked with it, so it is scanned too.
                File.WriteAllText(
                    Path.Combine(assets, "TheirMod", "Theirs.cs"),
                    "class B { void M() { System.IO.File.Delete(\"x\"); } }\n");

                // The framework's own folder is the other scan's job, and the generator refuses to
                // write into it; a file that looks like one there must not turn it into a consumer.
                File.WriteAllText(
                    Path.Combine(frameworkGenerated, "Ours" + DimensionSandboxGuard.GeneratedConsumerScriptSuffix),
                    "using System.IO;\npublic class C { }\n");

                List<string> roots = DimensionSandboxGuard.ConsumerModRoots(assets);

                Assert.That(roots.Count, Is.EqualTo(1), string.Join(", ", roots.ToArray()));
                Assert.That(roots[0], Does.EndWith("TheirMod"));
                Assert.That(
                    DimensionSandboxGuard.ConsumerSetProblem(assets, roots.Count),
                    Is.Null);
                Assert.That(
                    DimensionSandboxGuard.ShippedAssemblyNameOf(roots[0]),
                    Is.EqualTo("TheirMod"));

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.ScanConsumers(assets, denyList);

                Assert.That(findings.Count, Is.EqualTo(2), Detail(findings));
                Assert.That(
                    Has(findings, "Whiskey" + DimensionSandboxGuard.GeneratedConsumerScriptSuffix, "System.IO.*", 1),
                    Is.True,
                    Detail(findings));
                Assert.That(Has(findings, "Theirs.cs", "System.IO.*", 1), Is.True, Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(assets, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// No consumer mod at all is reported, because it reads exactly like a clean result.
        /// </summary>
        [Test]
        public void NoConsumerModFoundIsAProblemAndNotAPass()
        {
            string nowhere = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_no_consumer");

            List<string> roots = DimensionSandboxGuard.ConsumerModRoots(nowhere);

            Assert.That(roots, Is.Empty);
            Assert.That(
                DimensionSandboxGuard.ConsumerSetProblem(nowhere, roots.Count),
                Does.Contain("generated consumer mod"));
        }

        /// <summary>
        /// The list is read from the transcript beside the docs, so a read that comes back short is
        /// a check that has quietly stopped checking.
        /// </summary>
        [Test]
        public void TheDenyListIsReadFromTheTranscript()
        {
            DimensionSandboxGuard.DenyList denyList =
                DimensionSandboxGuard.ReadDenyList(Application.dataPath);

            Assert.That(
                denyList,
                Is.Not.Null,
                "Assets/" + DimensionSandboxGuard.DenyListPath + " could not be read. It is the " +
                "only copy of the rule the code sees; restore it from " +
                "ck-research/sandbox-compliance.md.");
            Assert.That(denyList.Namespaces, Is.Not.Empty);
            Assert.That(denyList.Types, Contains.Item("HarmonyLib.AccessTools"));
            Assert.That(denyList.Namespaces, Contains.Item("System.Reflection"));
            Assert.That(denyList.AllowPInvoke, Is.False);
            Assert.That(denyList.AllowUnsafeCode, Is.False);
        }

        /// <summary>
        /// A missing transcript must fail rather than pass. Everything above reads the same file, so
        /// without this the whole guard could go silent and every test would still be green.
        /// </summary>
        [Test]
        public void AMissingDenyListIsAFailureAndNotAPass()
        {
            string nowhere = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_no_list");

            List<DimensionSandboxGuard.Finding> findings = DimensionSandboxGuard.Scan(nowhere);

            Assert.That(findings, Is.Not.Empty);
            Assert.That(findings[0].Rule, Does.Contain("deny list"));
        }

        /// <summary>
        /// The set of files the scan walks has to be a real set. An empty one and a clean one are
        /// the same thing to every loop that reads it.
        /// </summary>
        /// <remarks>
        /// This is the third hole of the same shape in this class: the deny list and the emitter
        /// list each already fail rather than pass when they come back empty, and the ship set —
        /// the largest of the three — did not. One <c>"includePlatforms": ["Editor"]</c> line in
        /// <c>ExpandNullforge.asmdef</c> takes the count from 743 to 0, and before this test both
        /// this fixture and <c>DimensionBurstBudgetTests</c> read that as nothing to report.
        /// </remarks>
        [Test]
        public void TheShipSetIsBigEnoughToHaveCheckedAnything()
        {
            int shipped = DimensionSandboxGuard.ShippedSourceFiles(Application.dataPath).Count;
            string problem = DimensionSandboxGuard.ShipSetProblem(Application.dataPath, shipped);

            Assert.That(problem, Is.Null, problem);
        }

        /// <summary>
        /// And the scan itself says so, rather than reporting a clean tree, when the ship set is
        /// empty.
        /// </summary>
        /// <remarks>
        /// The fixture reproduces the exact mistake: a whole project whose root asmdef is marked
        /// Editor-only. Nothing is denied anywhere in it, which is precisely why a scan that came
        /// back with no findings would be believed.
        /// </remarks>
        [Test]
        public void AnEmptyShipSetIsAFailureAndNotAPass()
        {
            string root = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_no_ships");
            string mod = Path.Combine(root, DimensionSandboxGuard.ShippedRoot);
            string scripts = Path.Combine(mod, "Scripts");
            Directory.CreateDirectory(scripts);
            Directory.CreateDirectory(Path.Combine(mod, "Docs"));

            try
            {
                File.WriteAllText(
                    Path.Combine(root, DimensionSandboxGuard.DenyListPath.Replace('/', Path.DirectorySeparatorChar)),
                    "namespace: System.IO.*\ntype: HarmonyLib.AccessTools\n");
                File.WriteAllText(
                    Path.Combine(mod, "ExpandNullforge.asmdef"),
                    "{\n  \"name\": \"ExpandNullforge\",\n  \"includePlatforms\": [\"Editor\"]\n}\n");
                File.WriteAllText(
                    Path.Combine(scripts, "Clean.cs"),
                    "class A { }\n");

                Assert.That(
                    DimensionSandboxGuard.ShippedSourceFiles(root),
                    Is.Empty,
                    "The fixture is meant to produce an empty ship set; it did not, so what " +
                    "follows would not be testing the empty case.");

                List<DimensionSandboxGuard.Finding> findings = DimensionSandboxGuard.Scan(root);

                Assert.That(findings, Is.Not.Empty);
                Assert.That(findings[0].Rule, Does.Contain("ship set"), Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// The scanner is only worth trusting if it would actually fire, so prove it against files
        /// written for the purpose rather than against the real tree, which is expected to be clean.
        /// </summary>
        [Test]
        public void TheScannerDetectsDeniedCodeAndIgnoresProseAboutIt()
        {
            string root = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_test");
            string folder = Path.Combine(root, DimensionSandboxGuard.ShippedRoot, "Scripts");
            Directory.CreateDirectory(folder);

            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");
            denyList.Types.Add("HarmonyLib.AccessTools");
            denyList.Types.Add("HarmonyLib.Code");

            try
            {
                File.WriteAllText(
                    Path.Combine(folder, "Offender.cs"),
                    "using System.IO;\nclass A { }\n");

                File.WriteAllText(
                    Path.Combine(folder, "Reacher.cs"),
                    "using HarmonyLib;\n" +
                    "class B { static void M() { AccessTools.Field(null, \"x\"); } }\n");

                // Explaining the ban must not itself trip the ban, or the guard becomes unusable in
                // exactly the files that most need to explain it. Nor may a short name shared with
                // an ordinary property — HarmonyLib.Code against our own Code — fire on that
                // property, or the guard drowns in its own noise and gets switched off.
                File.WriteAllText(
                    Path.Combine(folder, "Innocent.cs"),
                    "// System.IO is banned in runtime code, and so is AccessTools.Field.\n" +
                    "/* also System.IO here */\n" +
                    "using HarmonyLib;\n" +
                    "class C { public string Code { get; } // System.IO\n" +
                    "  int L() { return this.Code.Length; } }\n");

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.Scan(root, denyList);

                Assert.That(findings.Count, Is.EqualTo(2), Detail(findings));
                Assert.That(
                    Has(findings, "Offender.cs", "System.IO.*", 1),
                    Is.True,
                    Detail(findings));
                Assert.That(
                    Has(findings, "Reacher.cs", "HarmonyLib.AccessTools", 2),
                    Is.True,
                    Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// Six ways a scanner can read a denied reference as clean text, one of which was
        /// live in the shipped tree when they were found.
        /// </summary>
        /// <remarks>
        /// Each fixture is the smallest file that reproduces one of them. They are written out
        /// rather than described because the reason all six survived a review is that every one of
        /// them looks like perfectly ordinary code — a wrapped line, a note after a using, a path
        /// with a wildcard in it.
        /// </remarks>
        [Test]
        public void TheScannerIsNotFooledByHowTheTextIsLaidOut()
        {
            string root = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_evasions");
            string folder = Path.Combine(root, DimensionSandboxGuard.ShippedRoot, "Scripts");
            Directory.CreateDirectory(folder);

            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");
            denyList.Namespaces.Add("System.Reflection");
            denyList.Types.Add("HarmonyLib.AccessTools");

            try
            {
                // The live one. A path with a wildcard in it opened a block comment that never
                // closed, and the rest of the file went unread.
                File.WriteAllText(
                    Path.Combine(folder, "PhantomComment.cs"),
                    "class A {\n" +
                    "  string p = \"Assets/<X>/Generation/Passes/*.asset\";\n" +
                    "  void M() { System.IO.File.Delete(\"x\"); }\n" +
                    "}\n");

                // The same hole one line wide: a "//" inside a literal ended the line early.
                File.WriteAllText(
                    Path.Combine(folder, "PhantomLine.cs"),
                    "class B {\n" +
                    "  void M() { string u = \"http://example\"; System.IO.File.Delete(\"x\"); }\n" +
                    "}\n");

                // A wrapped expression. Matching one physical line at a time saw neither half.
                File.WriteAllText(
                    Path.Combine(folder, "Wrapped.cs"),
                    "class C {\n" +
                    "  System.\n" +
                    "    IO.FileInfo f;\n" +
                    "}\n");

                // A note after the using stopped the file's imports from being read at all, and
                // with them every short name in it.
                File.WriteAllText(
                    Path.Combine(folder, "UsingNote.cs"),
                    "using HarmonyLib; // the patcher\n" +
                    "class D { void M() { AccessTools.Field(null, \"x\"); } }\n");

                // An alias for the PARENT namespace spells nothing denied anywhere in the file.
                File.WriteAllText(
                    Path.Combine(folder, "Alias.cs"),
                    "using S = System;\n" +
                    "class E { S.Reflection.FieldInfo f; }\n");

                // The other half of the wrapped case. A break beside a dot was closed up and a
                // space beside one was not, so the same expression written on one line went
                // through.
                File.WriteAllText(
                    Path.Combine(folder, "Spaced.cs"),
                    "class G { void M() { System . IO . File.Delete(\"x\"); } }\n");

                // A shipped script outside the folders a narrower scanner would be told to look in.
                string elsewhere = Path.Combine(root, DimensionSandboxGuard.ShippedRoot, "Runtime");
                Directory.CreateDirectory(elsewhere);
                File.WriteAllText(
                    Path.Combine(elsewhere, "Elsewhere.cs"),
                    "using System.IO;\nclass F { }\n");

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.Scan(root, denyList);

                Assert.That(Has(findings, "PhantomComment.cs", "System.IO.*", 3), Is.True, Detail(findings));
                Assert.That(Has(findings, "PhantomLine.cs", "System.IO.*", 2), Is.True, Detail(findings));
                Assert.That(Has(findings, "Wrapped.cs", "System.IO.*", 2), Is.True, Detail(findings));
                Assert.That(Has(findings, "UsingNote.cs", "HarmonyLib.AccessTools", 2), Is.True, Detail(findings));
                Assert.That(Has(findings, "Alias.cs", "System.Reflection.*", 2), Is.True, Detail(findings));
                Assert.That(Has(findings, "Spaced.cs", "System.IO.*", 1), Is.True, Detail(findings));
                Assert.That(Has(findings, "Elsewhere.cs", "System.IO.*", 1), Is.True, Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// A folder whose asmdef says Editor-only is skipped, and one that says nothing is not.
        /// </summary>
        /// <remarks>
        /// This is the whole reason the scanner no longer carries a list of folders: Unity decides
        /// what ships from the asmdefs, so the scanner reads the same thing Unity does. Get it
        /// wrong in one direction and Editor code floods the report; wrong in the other and shipped
        /// code goes unchecked.
        /// </remarks>
        [Test]
        public void EditorOnlyFoldersAreSkippedAndTheRestAreNot()
        {
            string root = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_asmdefs");
            string editor = Path.Combine(root, DimensionSandboxGuard.ShippedRoot, "Editor");
            string shipped = Path.Combine(root, DimensionSandboxGuard.ShippedRoot, "Scripts");
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(shipped);

            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");

            try
            {
                File.WriteAllText(
                    Path.Combine(editor, "Tools.asmdef"),
                    "{\n  \"name\": \"Tools\",\n  \"includePlatforms\": [\n    \"Editor\"\n  ]\n}\n");
                File.WriteAllText(Path.Combine(editor, "Tool.cs"), "using System.IO;\nclass A { }\n");
                File.WriteAllText(Path.Combine(shipped, "Ships.cs"), "using System.IO;\nclass B { }\n");

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.Scan(root, denyList);

                Assert.That(findings.Count, Is.EqualTo(1), Detail(findings));
                Assert.That(Has(findings, "Ships.cs", "System.IO.*", 1), Is.True, Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// Finding no bootstrap emitters at all is reported, because it reads exactly like a clean
        /// result and is not one.
        /// </summary>
        [Test]
        public void NoEmittersFoundIsAFindingAndNotAPass()
        {
            string nowhere = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_no_emitters");
            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");

            List<DimensionSandboxGuard.Finding> findings =
                DimensionSandboxGuard.ScanGeneratedSources(nowhere, denyList);

            Assert.That(findings, Is.Not.Empty);
            Assert.That(findings[0].Rule, Does.Contain("emitters"));
        }

        /// <summary>Whether one expected finding is somewhere in the list, in any order.</summary>
        /// <summary>
        /// A consumer beside a generated one is scanned even though it has never been generated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A SILENT PASS, AND THE WORST SHAPE ONE CAN HAVE: it looked like a clean run. Consumers
        /// were found only by the generated bootstrap, and the "did this find anything" question
        /// was asked of the whole project — so one generated mod satisfied it for every mod, and a
        /// second mod holding hand-written C# that ships and is security-checked was never opened.
        /// </para>
        /// <para>
        /// The second signature is an asmdef whose <c>references</c> name this framework. It is
        /// narrow on purpose: the SDK ships sample mods with asmdefs of their own, and a folder
        /// that is not built against this framework is not this framework's business. The third mod
        /// below is one of those, and it must not be scanned.
        /// </para>
        /// </remarks>
        [Test]
        public void AConsumerThatHasNeverBeenGeneratedIsStillScanned()
        {
            string assets = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_stale_consumer");
            if (Directory.Exists(assets))
            {
                Directory.Delete(assets, true);
            }

            DimensionSandboxGuard.DenyList denyList = new DimensionSandboxGuard.DenyList();
            denyList.Namespaces.Add("System.IO");

            try
            {
                string generated = Path.Combine(
                    Path.Combine(assets, "GeneratedMod"),
                    DimensionSandboxGuard.GeneratedConsumerScriptFolder
                        .Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(generated);
                File.WriteAllText(
                    Path.Combine(assets, "GeneratedMod", "GeneratedMod.asmdef"),
                    "{\n  \"name\": \"GeneratedMod\",\n  \"references\": [ \"ExpandNullforge\" ]\n}\n");
                File.WriteAllText(
                    Path.Combine(
                        generated,
                        "GeneratedMod" + DimensionSandboxGuard.GeneratedConsumerScriptSuffix),
                    "public class A { }\n");

                // Built against the framework, never generated, and shipping a denied reference.
                string stale = Path.Combine(assets, "StaleMod");
                Directory.CreateDirectory(stale);
                File.WriteAllText(
                    Path.Combine(stale, "StaleMod.asmdef"),
                    "{\n  \"name\": \"StaleMod\",\n  \"references\": [ \"ExpandNullforge.API\" ]\n}\n");
                File.WriteAllText(
                    Path.Combine(stale, "HandWritten.cs"),
                    "class B { void M() { System.IO.File.Delete(\"x\"); } }\n");

                // Not built against this framework at all: an SDK sample, and none of our business.
                string sample = Path.Combine(assets, "SomeSampleMod");
                Directory.CreateDirectory(sample);
                File.WriteAllText(
                    Path.Combine(sample, "SomeSampleMod.asmdef"),
                    "{\n  \"name\": \"SomeSampleMod\",\n  \"references\": [ \"PugMod.SDK\" ]\n}\n");
                File.WriteAllText(
                    Path.Combine(sample, "Whatever.cs"),
                    "class C { void M() { System.IO.File.Delete(\"x\"); } }\n");

                List<string> roots = DimensionSandboxGuard.ConsumerModRoots(assets);

                Assert.That(
                    roots.Count,
                    Is.EqualTo(2),
                    "Expected the generated mod and the stale one and nothing else. Found: "
                    + string.Join(", ", roots.ToArray()));
                Assert.That(
                    roots.Exists(r => r.EndsWith("StaleMod", System.StringComparison.Ordinal)),
                    Is.True,
                    "A mod built against this framework that has never been generated is invisible "
                    + "to the scan, so its shipped C# is never read and the run reads as clean.");
                Assert.That(
                    roots.Exists(r => r.EndsWith("SomeSampleMod", System.StringComparison.Ordinal)),
                    Is.False,
                    "A mod that is not built against this framework was scanned. Widening the "
                    + "search that far turns every sample in the SDK into a finding of ours.");

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.ScanConsumers(assets, denyList);

                Assert.That(
                    Has(findings, "HandWritten.cs", "System.IO.*", 1),
                    Is.True,
                    "The stale consumer's violation was not reported." + Detail(findings));
                Assert.That(
                    Has(findings, "Whatever.cs", "System.IO.*", 1),
                    Is.False,
                    "An unrelated mod's code was reported as ours." + Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(assets, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        /// <summary>
        /// A consumer with no assembly definition is said so, rather than quietly half-checked.
        /// </summary>
        /// <remarks>
        /// Its code compiles into <c>Assembly-CSharp</c>, which ships and is security-checked, and
        /// <c>ShippedAssemblyNameOf</c> has no name to hand the assembly guard — so the source half
        /// looked at it, the assembly half could not, and nothing said which. Reachable: this
        /// framework never writes an asmdef, and <c>EnsureConsumerAssemblyReferences</c> only edits
        /// one that already exists.
        /// </remarks>
        [Test]
        public void AConsumerWithNoAssemblyDefinitionIsReportedRatherThanHalfChecked()
        {
            string assets = Path.Combine(Path.GetTempPath(), "nf_sandbox_guard_no_asmdef");
            if (Directory.Exists(assets))
            {
                Directory.Delete(assets, true);
            }

            try
            {
                string generated = Path.Combine(
                    Path.Combine(assets, "NoAsmdefMod"),
                    DimensionSandboxGuard.GeneratedConsumerScriptFolder
                        .Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(generated);
                File.WriteAllText(
                    Path.Combine(
                        generated,
                        "NoAsmdefMod" + DimensionSandboxGuard.GeneratedConsumerScriptSuffix),
                    "public class A { }\n");

                List<string> roots = DimensionSandboxGuard.ConsumerModRoots(assets);
                Assert.That(roots.Count, Is.EqualTo(1), string.Join(", ", roots.ToArray()));
                Assert.That(
                    DimensionSandboxGuard.ShippedAssemblyNameOf(roots[0]),
                    Is.Empty,
                    "This fixture has no asmdef, so there is nothing for this test to be about.");

                List<DimensionSandboxGuard.Finding> findings =
                    DimensionSandboxGuard.ScanConsumers(
                        assets, new DimensionSandboxGuard.DenyList());

                Assert.That(
                    findings.Exists(f => f.Rule == "the consumer's assembly definition"),
                    Is.True,
                    "A consumer mod with no assembly definition was scanned for source and left "
                    + "out of the assembly scan with nothing said, so a clean result here means "
                    + "less than it reads." + Detail(findings));
            }
            finally
            {
                try
                {
                    Directory.Delete(assets, true);
                }
                catch (IOException)
                {
                    // A leftover temp folder is not worth failing a passing test over.
                }
            }
        }

        private static bool Has(
            List<DimensionSandboxGuard.Finding> findings,
            string fileEnding,
            string rule,
            int line)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].File.EndsWith(fileEnding, System.StringComparison.Ordinal) &&
                    findings[i].Rule == rule &&
                    findings[i].Line == line)
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
