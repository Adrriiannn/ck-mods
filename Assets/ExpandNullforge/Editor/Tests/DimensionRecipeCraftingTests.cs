#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves an authored recipe reaches a station in the game — including the two cases that used
    /// to reach nothing at all.
    /// </summary>
    /// <remarks>
    /// A recipe that named no Workbench was silently registered nowhere, and a recipe that named
    /// one of the mod's own Workbenches stopped at a warning saying injection was unfinished. Both
    /// looked correct in the dashboard. These assertions are on the emitted registration text
    /// because that string is the only thing that survives the editor: whatever is not written here
    /// cannot be placed on a station later, no matter what the asset says.
    /// </remarks>
    internal sealed class DimensionRecipeCraftingTests
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
        }

        [Test]
        public void ARecipeWithNoWorkbench_IsMadeByHandOnThePlayer()
        {
            DimensionTemplateAsset template = TemplateWith("Whistle", string.Empty, null);

            string emitted = Emit(template);

            Assert.That(
                emitted,
                Does.Contain("ObjectID.Player"),
                "Core Keeper has no separate hand-crafting list — the player IS the station — so a " +
                "recipe with no Workbench has to register against the player or it exists nowhere.");
            Assert.That(emitted, Does.Contain("\"TestMod:Whistle\""));
        }

        [Test]
        public void ARecipeAtTheModsOwnWorkbench_IsEmittedAsAQualifiedName()
        {
            DimensionWorkbenchAsset bench = Make<DimensionWorkbenchAsset>();
            SetString(bench, "workbenchId", "StoneBench");
            SetString(bench, "displayName", "Stone Bench");
            SetBool(bench, "generatesItsOwnObject", true);
            SetBool(bench, "enabled", true);

            DimensionTemplateAsset template = TemplateWith("Whistle", "StoneBench", null);
            template.SetGlobalWorkbenches(new[] { bench });

            string emitted = Emit(template);

            Assert.That(
                emitted,
                Does.Contain("\"TestMod:StoneBench\""),
                "A mod's bench has no ObjectID until the game hands it one while the world " +
                "converts, so the station has to travel as the qualified object name the runtime " +
                "resolves — baking a number here would produce None and drop the recipe.");
        }

        [Test]
        public void ARecipeAtANonexistentWorkbench_RegistersNothing()
        {
            DimensionTemplateAsset template = TemplateWith("Whistle", "NoSuchBench", null);

            Assert.That(
                Emit(template),
                Does.Not.Contain("DimensionCraftingRegistry.Register"),
                "A recipe pointed at a station nobody has is refused rather than dropped onto a " +
                "default bench, because a recipe at the wrong bench is harder to notice than one " +
                "that never appeared.");
        }

        [Test]
        public void OneOfTheGamesItems_IsOfferedUnderItsOwnNameRatherThanQualified()
        {
            DimensionTemplateAsset template = TemplateWith("IronBar", "CopperWorkBench", null);
            template.SetGlobalItems(new DimensionItemAsset[0]);

            string emitted = Emit(template);

            Assert.That(emitted, Does.Contain("\"IronBar\""));
            Assert.That(
                emitted,
                Does.Not.Contain("\"TestMod:IronBar\""),
                "Qualifying one of the game's names points the registration at an item that does " +
                "not exist, so the bench would offer a blank slot.");
        }

        [Test]
        public void IngredientsOnOneOfTheGamesItems_RefuseTheWholeRecipe()
        {
            DimensionTemplateAsset template =
                TemplateWith("IronBar", "CopperWorkBench", new[] { "Wood" });
            template.SetGlobalItems(new DimensionItemAsset[0]);

            Assert.That(
                Emit(template),
                Does.Not.Contain("DimensionCraftingRegistry.Register"),
                "What a craft costs is stored on the produced item, so ingredients cannot reach " +
                "one of the game's items. Registering anyway would ship the game's price behind " +
                "the author's ingredient list.");
        }

        [Test]
        public void IngredientsOnTheModsOwnItem_DoNotStopTheRecipe()
        {
            DimensionTemplateAsset template =
                TemplateWith("Whistle", "CopperWorkBench", new[] { "Wood" });

            Assert.That(
                Emit(template),
                Does.Contain("\"TestMod:Whistle\""),
                "The mod's own item carries the cost on its own prefab, so ingredients are fine " +
                "here and only the station half is emitted.");
        }

        // ------------------------------------------------------------------ helpers ---

        private static string Emit(DimensionTemplateAsset template)
        {
            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendRecipeCraftingRegistrations(
                builder, template, "TestMod");
            return builder.ToString();
        }

        /// <summary>A template holding one enabled recipe, and the item it makes when it is ours.</summary>
        private DimensionTemplateAsset TemplateWith(
            string outputItemId,
            string stationId,
            string[] ingredientItemIds)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "test:Recipe");
            SetString(recipe, "displayName", "A Recipe");
            SetString(recipe, "outputItemId", outputItemId);
            SetString(recipe, "craftingStationId", stationId);
            SetBool(recipe, "enabled", true);
            if (ingredientItemIds != null)
            {
                SetIngredients(recipe, ingredientItemIds);
            }

            template.SetGlobalRecipes(new[] { recipe });

            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetString(item, "itemId", outputItemId);
            SetString(item, "displayName", outputItemId);
            SetBool(item, "enabled", true);
            template.SetGlobalItems(new[] { item });
            return template;
        }

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetString(Object asset, string field, string value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field + " missing on " + asset.GetType().Name);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object asset, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(field);
            Assert.That(property, Is.Not.Null, field + " missing on " + asset.GetType().Name);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIngredients(Object recipe, string[] itemIds)
        {
            SerializedObject serialized = new SerializedObject(recipe);
            SerializedProperty ingredients = serialized.FindProperty("ingredients");
            Assert.That(ingredients, Is.Not.Null, "ingredients missing on the recipe asset.");
            ingredients.arraySize = itemIds.Length;
            for (int i = 0; i < itemIds.Length; i++)
            {
                SerializedProperty element = ingredients.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemId").stringValue = itemIds[i];
                element.FindPropertyRelative("amount").intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
