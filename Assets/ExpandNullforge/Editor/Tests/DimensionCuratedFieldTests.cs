#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The two ways a curated row could be worse than an uncurated one.
    /// </summary>
    /// <remarks>
    /// Both were live. A dotted path put its block back in the catch-all fold, so twenty-two values
    /// had two live editors on one page; and the control the page built for a string, an int or a
    /// bool asked nothing about that field's drawer, so the four name pickers appeared only on the
    /// rows nobody had written a card for. Neither is visible from a screenshot of a working page.
    /// </remarks>
    internal sealed class DimensionCuratedFieldTests
    {
        /// <summary>The dotted paths in the catalog, which is what makes this fixture non-empty.</summary>
        private static List<string> DottedCatalogPaths()
        {
            string catalog = DimensionFrameworkSourceScanner.ReadByName("DimensionStageCatalog.cs");
            List<string> paths = new List<string>();
            int at = 0;
            while (true)
            {
                at = catalog.IndexOf("F(\"", at, StringComparison.Ordinal);
                if (at < 0)
                {
                    break;
                }

                at += 3;
                int close = catalog.IndexOf('"', at);
                if (close < 0)
                {
                    break;
                }

                string path = catalog.Substring(at, close - at);
                if (path.IndexOf('.') > 0 && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Every block a card reaches inside is named, so the fold at the foot of the page can stop
        /// drawing that block whole.
        /// </summary>
        [Test]
        public void EveryDottedCatalogPathNamesItsBlockAsCurated()
        {
            List<string> dotted = DottedCatalogPaths();

            Assert.That(
                dotted,
                Is.Not.Empty,
                "No dotted field path was found in DimensionStageCatalog.cs, so this test has no " +
                "subject. Either the catalog stopped curating values inside blocks — in which case " +
                "delete this test — or the way a field is written in the catalog changed and this " +
                "no longer reads it.");

            HashSet<string> parents = DimensionCuratedPaths.ParentsOf(dotted);

            for (int i = 0; i < dotted.Count; i++)
            {
                string path = dotted[i];
                string block = path.Substring(0, path.IndexOf('.'));
                Assert.That(
                    parents,
                    Contains.Item(block),
                    "The catalog curates '" + path + "', so the page must know that the block '" +
                    block + "' is spoken for. Without that the block is drawn whole in " +
                    "\"Everything else\" as well, and '" + path + "' has two live editors on one " +
                    "page.");
            }
        }

        /// <summary>A path that names a value rather than a block claims no block.</summary>
        /// <remarks>
        /// The other half of the rule, and the one that would go wrong quietly: a rule that claimed
        /// a parent for every path would hide every uncurated top-level value on every page.
        /// </remarks>
        [Test]
        public void AFlatPathClaimsNothing()
        {
            HashSet<string> parents = DimensionCuratedPaths.ParentsOf(
                new[] { "displayName", "itemId", string.Empty, null });

            Assert.That(parents, Is.Empty);
        }

        /// <summary>A path two blocks deep claims both of them.</summary>
        [Test]
        public void ADeepPathClaimsEveryBlockOnTheWayDown()
        {
            HashSet<string> parents = DimensionCuratedPaths.ParentsOf(new[] { "a.b.c" });

            Assert.That(parents, Is.EquivalentTo(new[] { "a", "a.b" }));
        }

        /// <summary>
        /// Every mark this framework draws is declared in the assembly the control layer tests
        /// against, so no drawer is stripped from a curated row.
        /// </summary>
        /// <remarks>
        /// <c>DimensionsApiControls.HasFrameworkDrawer</c> decides "does this framework draw this
        /// field" by asking whether any of its <c>PropertyAttribute</c>s comes from the assembly
        /// the authoring marks live in. That is the whole rule, and it is only correct while every
        /// mark with a drawer is declared there. A mark declared in the editor assembly instead
        /// would compile, draw perfectly everywhere else, and silently vanish from curated rows —
        /// which is exactly the bug this was written to close.
        /// </remarks>
        [Test]
        public void EveryAttributeThisFrameworkDrawsLivesWithTheAuthoringMarks()
        {
            List<string> drawnAttributes = new List<string>();
            List<string> files = DimensionFrameworkSourceScanner.SourceFiles();
            for (int i = 0; i < files.Count; i++)
            {
                string source = System.IO.File.ReadAllText(files[i]);
                int at = 0;
                while (true)
                {
                    at = source.IndexOf("[CustomPropertyDrawer(typeof(", at, StringComparison.Ordinal);
                    if (at < 0)
                    {
                        break;
                    }

                    at += "[CustomPropertyDrawer(typeof(".Length;
                    int close = source.IndexOf(')', at);
                    if (close < 0)
                    {
                        break;
                    }

                    string name = source.Substring(at, close - at).Trim();
                    if (name.EndsWith("Attribute", StringComparison.Ordinal) &&
                        !drawnAttributes.Contains(name))
                    {
                        drawnAttributes.Add(name);
                    }
                }
            }

            Assert.That(
                drawnAttributes,
                Is.Not.Empty,
                "No [CustomPropertyDrawer(typeof(...Attribute))] was found anywhere in the " +
                "framework, so this test has no subject and the drawer rule it guards is guarding " +
                "nothing.");

            System.Reflection.Assembly authoring =
                typeof(ExpandNullforge.Authoring.DimensionSoundNameAttribute).Assembly;

            for (int i = 0; i < drawnAttributes.Count; i++)
            {
                Type mark = authoring.GetType(
                    "ExpandNullforge.Authoring." + drawnAttributes[i], false);

                Assert.That(
                    mark,
                    Is.Not.Null,
                    "This framework draws '" + drawnAttributes[i] + "', and it is not declared in " +
                    authoring.GetName().Name + " beside the other authoring marks. A curated " +
                    "string, int or bool row will not show its drawer, because the control layer " +
                    "recognises a mark by that assembly. Move it there.");

                Assert.That(
                    typeof(UnityEngine.PropertyAttribute).IsAssignableFrom(mark),
                    Is.True,
                    drawnAttributes[i] + " is drawn as a field mark but is not a PropertyAttribute.");
            }
        }

        /// <summary>
        /// Unity's own field marks are NOT in that assembly, which is what keeps the rule narrow.
        /// </summary>
        /// <remarks>
        /// <c>[Tooltip]</c> is a <c>PropertyAttribute</c> and has no drawer. Were it to count, all
        /// but a handful of rows on every page would be pushed through <c>PropertyField</c> and the
        /// studio's own controls would disappear from the whole window.
        /// </remarks>
        [Test]
        public void UnitysOwnMarksAreNotMistakenForThisFrameworksMarks()
        {
            System.Reflection.Assembly authoring =
                typeof(ExpandNullforge.Authoring.DimensionSoundNameAttribute).Assembly;

            Assert.That(typeof(TooltipAttribute).Assembly, Is.Not.EqualTo(authoring));
            Assert.That(typeof(UnityEngine.RangeAttribute).Assembly, Is.Not.EqualTo(authoring));
            Assert.That(typeof(HeaderAttribute).Assembly, Is.Not.EqualTo(authoring));
        }
    }
}
#endif
