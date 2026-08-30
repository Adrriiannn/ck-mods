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
    /// <para>
    /// ONE OF THE TWO WAS LIVE AND THE OTHER IS NOT, AND THE DIFFERENCE IS RECORDED HERE BECAUSE
    /// THIS FIXTURE USED TO SAY "both were live". A dotted card path put its whole block back in
    /// the catch-all fold, so twenty-two values had two live editors on one page: measured on the
    /// catalog, and the first three tests below are that rule.
    /// </para>
    /// <para>
    /// The second is a rule about a card nobody has written. The control layer builds a plain
    /// <c>TextField</c> for a curated string, which would drop the drawer on a field this framework
    /// draws itself — but not one of the twenty-five fields carrying those marks is named by a card
    /// anywhere, so no picker has ever disappeared. The last two tests hold the rule to the real
    /// marked fields rather than to a story about them, and they are worth running because the day
    /// somebody writes that card is the day it stops being theory.
    /// </para>
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
        /// <c>DimensionsApiControls.FrameworkDrawsField</c> decides "does this framework draw this
        /// field" by asking whether any of its <c>PropertyAttribute</c>s comes from the assembly
        /// the authoring marks live in. That is the whole rule, and it is only correct while every
        /// mark with a drawer is declared there. A mark declared in the editor assembly instead
        /// would compile, draw perfectly everywhere else, and be dropped from a curated row.
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
        /// <summary>
        /// The rule answers yes for every field this framework marks, and no for a plain one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS THE TEST THAT GIVES THE RULE A SUBJECT. No curated card names a marked field, so
        /// nothing in the studio exercises <c>FrameworkDrawsField</c> — a check written, shipped and
        /// reached by nothing is this project's own recurring failure, and a guard for a card
        /// somebody may write one day is only worth keeping if the rule itself is known to work.
        /// The subjects are the real fields in the real assembly, found the same way the control
        /// layer finds them.
        /// </para>
        /// <para>
        /// It walks the assembly rather than a list of names, so a mark added to a new field is
        /// covered without touching this, and the floor below fails the run if the walk ever stops
        /// finding any.
        /// </para>
        /// </remarks>
        [Test]
        public void TheDrawerRuleAnswersYesForEveryFieldThisFrameworkMarks()
        {
            System.Reflection.Assembly authoring =
                typeof(ExpandNullforge.Authoring.DimensionSoundNameAttribute).Assembly;

            List<string> answeredNo = new List<string>();
            List<string> unmarkedAnsweredYes = new List<string>();
            int marked = 0;
            int unmarked = 0;

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly;

            Type[] types;
            try
            {
                types = authoring.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException loadFailure)
            {
                List<Type> loaded = new List<Type>();
                for (int i = 0; i < loadFailure.Types.Length; i++)
                {
                    if (loadFailure.Types[i] != null)
                    {
                        loaded.Add(loadFailure.Types[i]);
                    }
                }

                types = loaded.ToArray();
            }

            for (int t = 0; t < types.Length; t++)
            {
                System.Reflection.FieldInfo[] fields = types[t].GetFields(Flags);
                for (int f = 0; f < fields.Length; f++)
                {
                    if (!CarriesAMarkFrom(fields[f], authoring))
                    {
                        // One plain field per type is enough to prove the rule says no as well as
                        // yes; walking every field in the assembly would say the same thing slower.
                        if (unmarked < 200 &&
                            fields[f].GetCustomAttributes(typeof(SerializeField), true).Length > 0)
                        {
                            unmarked++;
                            if (DimensionsApiControls.FrameworkDrawsField(types[t], fields[f].Name))
                            {
                                unmarkedAnsweredYes.Add(types[t].Name + "." + fields[f].Name);
                            }
                        }

                        continue;
                    }

                    marked++;
                    if (!DimensionsApiControls.FrameworkDrawsField(types[t], fields[f].Name))
                    {
                        answeredNo.Add(types[t].Name + "." + fields[f].Name);
                    }
                }
            }

            Assert.That(
                marked,
                Is.GreaterThan(0),
                "No field in " + authoring.GetName().Name + " carries one of this framework's own " +
                "field marks, so this test checked nothing. Either the marks were removed — in " +
                "which case DimensionsApiControls.FrameworkDrawsField and this test can go — or " +
                "the walk is broken.");
            Assert.That(
                unmarked,
                Is.GreaterThan(0),
                "No unmarked serialized field was found to check the other direction against, so " +
                "a rule that answered yes to everything would pass this.");
            Assert.That(
                answeredNo,
                Is.Empty,
                "These fields carry a mark this framework draws and the control layer says it does " +
                "not draw them, so a curated card for one would show a plain box with no Browse " +
                "button: " + string.Join(", ", answeredNo.ToArray()));
            Assert.That(
                unmarkedAnsweredYes,
                Is.Empty,
                "These fields carry no mark of this framework's and the control layer says it " +
                "draws them, so every one of them would be pushed through PropertyField and lose " +
                "the studio's own control: " + string.Join(", ", unmarkedAnsweredYes.ToArray()));
        }

        private static bool CarriesAMarkFrom(
            System.Reflection.FieldInfo field, System.Reflection.Assembly authoring)
        {
            // Fully qualified: NUnit ships a PropertyAttribute of its own and this fixture has both
            // namespaces open.
            object[] marks =
                field.GetCustomAttributes(typeof(UnityEngine.PropertyAttribute), true);
            for (int i = 0; i < marks.Length; i++)
            {
                if (marks[i].GetType().Assembly == authoring)
                {
                    return true;
                }
            }

            return false;
        }

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
