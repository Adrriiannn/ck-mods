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
    /// The helpers that were merged into one implementation stay at one implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THESE AND NOT EVERY HELPER. Each name below answers a question where two answers is a
    /// bug rather than a tidiness problem: an id that is written on one path and compared on
    /// another, a hash that is saved into a world, an address a generated asset is found by, a
    /// filename two generators have to agree on. When those drifted, nothing failed — the id, the
    /// hash, the address or the path simply stopped matching itself, silently.
    /// </para>
    /// <para>
    /// THE COUNTS ARE THE POINT. A copy pasted back in is caught because the count goes up; a merge
    /// undone by deleting the shared one is caught because it goes to zero. The exceptions are
    /// named rather than pattern-matched, so a new file that happens to be called something similar
    /// still has to be argued for.
    /// </para>
    /// </remarks>
    internal sealed class DimensionOneImplementationTests
    {
        /// <summary>One rule: what to look for, how many there should be, and where.</summary>
        private struct Rule
        {
            public string What;
            public string Pattern;
            public int Expected;
            public string Why;
        }

        private static readonly Rule[] Rules =
        {
            new Rule
            {
                What = "the fixed-string conversion",
                // The declaration, not the forwarder: a body, not a one-line delegate.
                Pattern = @"int count = Math\.Min\(value\.Length, MaxCharsIn",
                Expected = 2,
                Why = "ToFixed64 and ToFixed128 in DimensionFixedStrings. Eighteen copies existed " +
                      "and one of them truncated by bytes and kept control characters, so a portal " +
                      "id encoded one way on the item-portal path and another way everywhere else.",
            },
            new Rule
            {
                What = "the FNV walk that names are turned into ids with",
                Pattern = @"hash \^= \(ulong\)\(c & 0xFF\)",
                Expected = 1,
                Why = "DimensionFnv.Hash. The tileset id, the biome id and the layout fingerprint " +
                      "are all this hash and all three are saved into worlds.",
            },
            new Rule
            {
                What = "the sprite-asset address",
                Pattern = @"private static long ComputeStableAddressPart\(",
                Expected = 0,
                Why = "DimensionSpriteAssetAddress.Part is the one implementation. Four copies " +
                      "existed, two of them in the same file twelve hundred lines apart.",
            },
            new Rule
            {
                What = "the authored-name-to-filename rule",
                Pattern = @"private static string SanitizeFileName\(",
                Expected = 0,
                Why = "DimensionGeneratedPrefabUtility.SanitizeAuthoredName is the one " +
                      "implementation. Fifteen copies existed in two families that disagreed, so " +
                      "two generators wrote companion assets for one object to two paths and the " +
                      "pruner recognised only one of them.",
            },
            new Rule
            {
                What = "the walk that makes a folder and every folder above it",
                // The walk, not the name: several callers keep a one-line door of their own, and a
                // test fixture making one known folder under Assets is not this.
                Pattern = @"AssetDatabase\.CreateFolder\(current,",
                Expected = 1,
                Why = "DimensionAssetFolders is the one implementation. Twenty copies existed in " +
                      "five shapes, and the shape that made folders behind Unity's back is why " +
                      "some sites needed a retry block that the others did not.",
            },
            new Rule
            {
                What = "the id-scoping rule",
                Pattern = @"static string BuildScopedId\(",
                Expected = 1,
                Why = "DimensionTemplateCompiler owns it. Three copies existed, and if two of them " +
                      "ever disagreed a compiled zone and its manifest entry would sit at " +
                      "different ids with nothing said.",
            },
            new Rule
            {
                What = "which zone a compiled region belongs to",
                Pattern = @"static string ResolveCompiledZoneId\(",
                Expected = 1,
                Why = "Same rule, same reason.",
            },
            new Rule
            {
                What = "the serialized field a portal layer's artwork is written to",
                Pattern = @"static string GetReferencePropertyName\(",
                Expected = 1,
                Why = "DimensionPortalPackageEditorUtility owns it. Three copies existed and two " +
                      "of them had no case for the instant centre, so the same layer answered " +
                      "with a field name in one and threw in the others — and one of the two was " +
                      "in the test, which therefore could not fail when production changed.",
            },
        };

        [Test]
        public void EachMergedHelperStillHasExactlyOneImplementation()
        {
            List<string> sources = AllFrameworkSources();

            Assert.That(
                sources.Count,
                Is.GreaterThan(400),
                "Almost no source files were read, so every count below is a count of nothing.");

            List<string> wrong = new List<string>();

            for (int r = 0; r < Rules.Length; r++)
            {
                Rule rule = Rules[r];
                Regex pattern = new Regex(rule.Pattern);
                List<string> found = new List<string>();

                for (int i = 0; i < sources.Count; i++)
                {
                    MatchCollection matches = pattern.Matches(File.ReadAllText(sources[i]));
                    for (int m = 0; m < matches.Count; m++)
                    {
                        found.Add(Path.GetFileName(sources[i]));
                    }
                }

                if (found.Count == rule.Expected)
                {
                    continue;
                }

                found.Sort(StringComparer.Ordinal);
                wrong.Add(
                    rule.What + ": expected " + rule.Expected + " and found " + found.Count +
                    (found.Count == 0 ? string.Empty : " (" + string.Join(", ", found) + "). ") +
                    rule.Why);
            }

            Assert.That(
                wrong,
                Is.Empty,
                "A helper that was merged into one implementation has more than one again, or the " +
                "one it was merged into is gone. Neither shows up as a failure anywhere else — " +
                "the two copies simply start answering differently:\n  " +
                string.Join("\n  ", wrong));
        }

        /// <summary>Every C# file this framework owns, shipped and editor alike.</summary>
        /// <remarks>
        /// Not just the ship set: half of these helpers live in the editor assembly, and a copy
        /// pasted into a generator is exactly the case this is watching for.
        /// </remarks>
        private static List<string> AllFrameworkSources()
        {
            string root = Path.Combine(
                Application.dataPath,
                DimensionSandboxGuard.ShippedRoot);

            List<string> files = new List<string>();
            if (!Directory.Exists(root))
            {
                return files;
            }

            files.AddRange(Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories));
            return files;
        }
    }
}
#endif
