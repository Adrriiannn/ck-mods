using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers cooking: ingredients, the dishes they make, and the colours that carry across.
    /// </summary>
    /// <remarks>
    /// The colour copy is the whole point of doing this in a framework. Core Keeper draws one dish
    /// sprite and recolours it from the two ingredients that went in, so a cooked dish stores a copy
    /// of each ingredient's four-tone palette. In vanilla those copies are typed in by hand on every
    /// dish; here the dish names its ingredients and the generator does it.
    /// </remarks>
    public sealed class DimensionCookingTests
    {
        private const string TestRoot = "Assets/NullforgeCookingTests";

        private DimensionItemAsset ingredient;
        private DimensionItemAsset dish;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            ingredient = Make("testberry", "Test Berry");
            dish = Make("testpie", "Test Pie");
        }

        [TearDown]
        public void Cleanup()
        {
            if (ingredient != null)
            {
                Object.DestroyImmediate(ingredient);
            }

            if (dish != null)
            {
                Object.DestroyImmediate(dish);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private static DimensionItemAsset Make(string id, string name)
        {
            DimensionItemAsset made = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject serialized = new SerializedObject(made);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("iconId").stringValue = "1";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return made;
        }

        private static void SetCooking(DimensionItemAsset item, System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized.FindProperty("cooking"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport Run()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { ingredient, dish },
                TestRoot);
        }

        private static GameObject Load(string id)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
        }

        [Test]
        public void AnIngredientCarriesItsKindAndItsColours()
        {
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                cooking.FindPropertyRelative("ingredientKind").intValue =
                    (int)DimensionIngredientKind.Fish;
                cooking.FindPropertyRelative("brightest").colorValue = Color.red;
            });
            Run();

            CookingIngredientAuthoring authored =
                Load("testberry").GetComponent<CookingIngredientAuthoring>();
            Assert.IsNotNull(authored);
            Assert.AreEqual(IngredientType.Fish, authored.ingredientType);
            Assert.AreEqual(Color.red, authored.brightestColor);
        }

        [Test]
        public void ADishTakesItsPalettesFromTheIngredientsItNames()
        {
            // The reason this is worth a framework: vanilla types these in twice.
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                cooking.FindPropertyRelative("brightest").colorValue = Color.green;
                cooking.FindPropertyRelative("darkest").colorValue = Color.blue;
            });

            SetCooking(dish, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.CookedDish;
                cooking.FindPropertyRelative("madeFrom").stringValue = "testberry";
            });
            Run();

            CookedFoodAuthoring cooked = Load("testpie").GetComponent<CookedFoodAuthoring>();
            Assert.IsNotNull(cooked);
            Assert.AreEqual(Color.green, cooked.ingredient1BrightestColor);
            Assert.AreEqual(Color.blue, cooked.ingredient1DarkestColor);
        }

        [Test]
        public void AnIngredientTheModDoesNotDefineIsReportedAndFallsBackToTheDish()
        {
            SetCooking(dish, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.CookedDish;
                cooking.FindPropertyRelative("madeFrom").stringValue = "notaningredient";
                cooking.FindPropertyRelative("brightest").colorValue = Color.magenta;
            });

            DimensionItemGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("notaningredient")));
            Assert.AreEqual(
                Color.magenta,
                Load("testpie").GetComponent<CookedFoodAuthoring>().ingredient1BrightestColor,
                "a dish built from vanilla ingredients still has to work");
        }

        [Test]
        public void AFishIsAnIngredientThatCanBeCaught()
        {
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                cooking.FindPropertyRelative("canBeFished").boolValue = true;
            });
            Run();

            Assert.IsNotNull(Load("testberry").GetComponent<FishAuthoring>());
        }

        [Test]
        public void SomethingThatStopsBeingFoodCarriesNoneOfIt()
        {
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                cooking.FindPropertyRelative("canBeFished").boolValue = true;
            });
            Run();
            Assert.IsNotNull(Load("testberry").GetComponent<CookingIngredientAuthoring>());

            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.NotFood;
            });
            Run();

            Assert.IsNull(Load("testberry").GetComponent<CookingIngredientAuthoring>());
            Assert.IsNull(Load("testberry").GetComponent<FishAuthoring>());
        }

        [Test]
        public void AnItemIsNeverBothAnIngredientAndADish()
        {
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
            });
            Run();
            Assert.IsNull(Load("testberry").GetComponent<CookedFoodAuthoring>());

            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.CookedDish;
            });
            Run();
            Assert.IsNull(Load("testberry").GetComponent<CookingIngredientAuthoring>());
            Assert.IsNotNull(Load("testberry").GetComponent<CookedFoodAuthoring>());
        }

        [Test]
        public void AnIngredientIsTaggedSoThePotWillTakeIt()
        {
            // The cooking pot's two input slots accept objects BY TAG. Without this an ingredient
            // with perfect cooking data simply cannot be dropped in, and the slot says nothing.
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
            });
            Run();

            ObjectAuthoring authored = Load("testberry").GetComponent<ObjectAuthoring>();
            Assert.IsNotNull(authored.tags);
            Assert.Contains(ObjectCategoryTag.CookingIngredient, authored.tags);

            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.NotFood;
            });
            Run();

            authored = Load("testberry").GetComponent<ObjectAuthoring>();
            Assert.IsFalse(
                authored.tags != null && authored.tags.Contains(ObjectCategoryTag.CookingIngredient),
                "something that stops being food stops being accepted by the pot");
        }

        [Test]
        public void WhatAnIngredientGivesRawAndCookedAreKeptApart()
        {
            // "Better when cooked" is the whole variety mechanic. The raw and cooked payloads sit
            // side by side in one entry, and writing one value into both halves throws it away.
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                SerializedProperty raw = cooking.FindPropertyRelative("givesRaw");
                raw.arraySize = 1;
                raw.GetArrayElementAtIndex(0).FindPropertyRelative("effectId").stringValue =
                    "HungerAddition";
                raw.GetArrayElementAtIndex(0).FindPropertyRelative("value").intValue = 9;

                SerializedProperty cooked = cooking.FindPropertyRelative("givesCooked");
                cooked.arraySize = 1;
                cooked.GetArrayElementAtIndex(0).FindPropertyRelative("effectId").stringValue =
                    "HungerAddition";
                cooked.GetArrayElementAtIndex(0).FindPropertyRelative("value").intValue = 19;
            });
            Run();

            GivesConditionsWhenConsumedAuthoring eaten =
                Load("testberry").GetComponent<GivesConditionsWhenConsumedAuthoring>();
            Assert.IsNotNull(eaten);
            Assert.AreEqual(1, eaten.Values.Count);
            Assert.AreEqual(9, eaten.Values[0].conditionData.value, "raw");
            Assert.AreEqual(19, eaten.Values[0].conditionDataWhenCooked.value, "cooked");
        }

        [Test]
        public void RegeneratingDoesNotStackAnIngredientsEffectsTwice()
        {
            // The cooking pass APPENDS to the same component the item's own eaten effects use, and
            // that is only safe because the effects pass rewrites the list first. If the two ever
            // swap order, this is the test that says so.
            SetCooking(ingredient, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.Ingredient;
                SerializedProperty raw = cooking.FindPropertyRelative("givesRaw");
                raw.arraySize = 1;
                raw.GetArrayElementAtIndex(0).FindPropertyRelative("effectId").stringValue =
                    "HungerAddition";
                raw.GetArrayElementAtIndex(0).FindPropertyRelative("value").intValue = 5;
            });
            Run();
            Run();

            GivesConditionsWhenConsumedAuthoring eaten =
                Load("testberry").GetComponent<GivesConditionsWhenConsumedAuthoring>();
            Assert.AreEqual(1, eaten.Values.Count);
        }

        [Test]
        public void TheFishTickIsRefusedOnSomethingThatIsNotAnIngredient()
        {
            SetCooking(dish, delegate(SerializedProperty cooking)
            {
                cooking.FindPropertyRelative("role").intValue = (int)DimensionFoodRole.CookedDish;
                cooking.FindPropertyRelative("canBeFished").boolValue = true;
            });

            Assert.IsTrue(dish.Cooking.FishTickWasRefused);
            Assert.IsFalse(dish.Cooking.CanBeFished, "a fish nothing can cook is not a fish");
        }
    }
}
