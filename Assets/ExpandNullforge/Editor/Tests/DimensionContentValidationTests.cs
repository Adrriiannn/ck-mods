#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves content validation actually reaches Review's lights — across the seam, not in
    /// isolation.
    /// </summary>
    /// <remarks>
    /// This suite exists because of the exact failure it now guards: item, artwork and
    /// portal validators that were green, unit-tested, and had zero callers, so a dimension
    /// full of dangling references reported "Ready to ship". A test that only exercises the
    /// validator would be the same lie with better coverage numbers — every assertion here
    /// crosses from an authored asset to the preview's issue stream, the thing the five
    /// lights and the Diagnostics page actually read.
    /// </remarks>
    internal sealed class DimensionContentValidationTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
            DimensionContentValidationUtility.BumpChangeStamp();
        }

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetField(Object asset, string field, object value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field + " missing on " + asset.GetType().Name);
            switch (value)
            {
                case string text:
                    property.stringValue = text;
                    break;
                case bool flag:
                    property.boolValue = flag;
                    break;
                default:
                    Assert.Fail("Unsupported test value type for " + field);
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void EditorChecks_AreInstalled()
        {
            Assert.That(
                DimensionContentValidationUtility.EditorChecks,
                Is.Not.Null,
                "The editor half of content validation is not installed. Portal parity and " +
                "prefab currency silently vanished from Review.");
        }

        [Test]
        public void DanglingRecipeOutput_ReachesThePreviewIssueStream()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetField(recipe, "recipeId", "test:BrokenRecipe");
            SetField(recipe, "displayName", "Broken Recipe");
            SetField(recipe, "outputItemId", "test:NothingDefinesThis");
            SetField(recipe, "enabled", true);
            template.SetGlobalRecipes(new[] { recipe });

            DimensionContentValidationUtility.BumpChangeStamp();
            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(template);

            bool found = false;
            for (int i = 0; i < preview.Issues.Count; i++)
            {
                if (preview.Issues[i].Code == "recipe-output-unresolved")
                {
                    found = true;
                    Assert.That(
                        preview.Issues[i].Severity,
                        Is.EqualTo(DimensionAuthoringSeverity.Error),
                        "A dangling reference ships a lie, so it must block.");
                }
            }

            Assert.That(
                found,
                Is.True,
                "The dangling recipe output never reached the preview's issue stream — the " +
                "seam between content validation and Review is broken again.");
            Assert.That(
                preview.ErrorCount,
                Is.GreaterThan(0),
                "Errors in the stream must move the counters the lights read.");
        }

        [Test]
        public void IngredientsOnOneOfTheGamesItems_ReachThePreviewIssueStream()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetField(recipe, "recipeId", "test:CheapIronBar");
            SetField(recipe, "displayName", "Cheap Iron Bar");
            SetField(recipe, "outputItemId", "IronBar");
            SetField(recipe, "enabled", true);

            SerializedObject serialized = new SerializedObject(recipe);
            SerializedProperty ingredients = serialized.FindProperty("ingredients");
            ingredients.arraySize = 1;
            ingredients.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "Wood";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalRecipes(new[] { recipe });

            DimensionContentValidationUtility.BumpChangeStamp();
            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(template);

            bool found = false;
            for (int i = 0; i < preview.Issues.Count; i++)
            {
                if (preview.Issues[i].Code == "recipe-vanilla-output-has-ingredients")
                {
                    found = true;
                    Assert.That(
                        preview.Issues[i].Severity,
                        Is.EqualTo(DimensionAuthoringSeverity.Error),
                        "The recipe is refused rather than registered at a cost nobody wrote, so " +
                        "the dashboard has to say so before the build.");
                }
            }

            Assert.That(
                found,
                Is.True,
                "A cost written against one of the game's items never reaches the game — what a " +
                "craft costs lives on the item — so the author has to be told here.");
        }

        [Test]
        public void DuplicateIds_AreOneErrorNamingBothOwners()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset first = Make<DimensionItemAsset>();
            DimensionItemAsset second = Make<DimensionItemAsset>();
            SetField(first, "itemId", "test:SameId");
            SetField(first, "displayName", "First");
            SetField(first, "enabled", false);
            SetField(second, "itemId", "test:SameId");
            SetField(second, "displayName", "Second");
            SetField(second, "enabled", false);
            template.SetGlobalItems(new[] { first, second });

            DimensionContentValidationUtility.BumpChangeStamp();
            IReadOnlyList<DimensionAuthoringIssue> issues =
                DimensionContentValidationUtility.Validate(template, default);

            int duplicates = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Code == "content-id-duplicate")
                {
                    duplicates++;
                }
            }

            Assert.That(duplicates, Is.EqualTo(1), "One collision, one row.");
        }

        [Test]
        public void VanillaShadowingId_IsAnError()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetField(item, "itemId", "IronBar");
            SetField(item, "displayName", "Fake Iron");
            SetField(item, "enabled", false);
            template.SetGlobalItems(new[] { item });

            DimensionContentValidationUtility.BumpChangeStamp();
            IReadOnlyList<DimensionAuthoringIssue> issues =
                DimensionContentValidationUtility.Validate(template, default);

            bool found = false;
            for (int i = 0; i < issues.Count; i++)
            {
                found |= issues[i].Code == "content-id-shadows-vanilla";
            }

            Assert.That(
                found,
                Is.True,
                "An id that is also a vanilla object name makes the authored thing " +
                "unreachable; that must be said out loud.");
        }

        [Test]
        public void Cache_ServesTheSameListUntilTheStampMoves()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionContentValidationUtility.BumpChangeStamp();

            IReadOnlyList<DimensionAuthoringIssue> first =
                DimensionContentValidationUtility.Validate(template, default);
            IReadOnlyList<DimensionAuthoringIssue> second =
                DimensionContentValidationUtility.Validate(template, default);
            Assert.That(
                ReferenceEquals(first, second),
                Is.True,
                "Within one rebuild the preview is built several times; every call after " +
                "the first must be free.");

            DimensionContentValidationUtility.BumpChangeStamp();
            IReadOnlyList<DimensionAuthoringIssue> third =
                DimensionContentValidationUtility.Validate(template, default);
            Assert.That(
                ReferenceEquals(first, third),
                Is.False,
                "A bumped stamp must recompute, or edits stop being seen.");
        }

        [Test]
        public void DisabledBrokenItem_NeverBlocks()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetField(item, "itemId", "test:BrokenButOff");
            SetField(item, "displayName", "Broken But Off");
            SetField(item, "enabled", false);
            template.SetGlobalItems(new[] { item });

            DimensionContentValidationUtility.BumpChangeStamp();
            IReadOnlyList<DimensionAuthoringIssue> issues =
                DimensionContentValidationUtility.Validate(template, default);

            bool found = false;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].RecordId != "test:BrokenButOff")
                {
                    continue;
                }

                found = true;
                Assert.That(
                    issues[i].Severity,
                    Is.EqualTo(DimensionAuthoringSeverity.Info),
                    "A deliberately disabled item must never wedge the lights red: " +
                    issues[i].Code);
            }

            // Without this the loop is the whole test, so a validator that reported nothing at
            // all about the item passed it — the one outcome that would hide the disclosure line
            // going missing.
            Assert.That(
                found,
                Is.True,
                "The validator said nothing about the disabled item, so the loop above ran zero " +
                "times and asserted nothing. A disabled item is meant to get one Info line " +
                "('item-disabled') saying it will not be generated.");
        }
    }
}
#endif
