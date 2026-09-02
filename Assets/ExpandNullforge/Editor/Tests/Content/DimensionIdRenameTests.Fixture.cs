#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The assets each test renames, and the small helpers that read them back.
    /// </summary>
    internal sealed partial class DimensionIdRenameTests
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

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetString(Object asset, string path, string value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object asset, string path, bool value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAsset(Object asset, string path, Object value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAssetArray(Object asset, string path, params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ReadString(Object asset, string path)
        {
            SerializedProperty property = new SerializedObject(asset).FindProperty(path);
            return property == null ? null : property.stringValue;
        }

        private static DimensionIdentityField NameOn<T>(Object asset, string property)
        {
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(asset);
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].Property == property)
                {
                    return names[i];
                }
            }

            Assert.Fail("No renameable name '" + property + "' on " + typeof(T).Name);
            return null;
        }

        /// <summary>An item called Old, a recipe that makes it, and a loot table that drops it.</summary>
        private DimensionTemplateAsset BuildPack(
            out DimensionItemAsset item,
            out DimensionRecipeAsset recipe,
            out DimensionLootTableAsset loot)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Old");
            SetString(item, "displayName", "Ember Blade");

            recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "MakeOld");
            SetString(recipe, "outputItemId", "MyMod:Old");

            loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "DropsOld");
            SerializedObject lootSerialized = new SerializedObject(loot);
            SerializedProperty entries = lootSerialized.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "Old";
            lootSerialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalItems(new[] { item });
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalLootTables(new[] { loot });
            return template;
        }

        // ------------------------------------------------- one word, two kinds of thing ---

        /// <summary>
        /// A pack where an item and a loot table are both called Ember, a recipe outputs the item,
        /// and a scene rolls on the table. Legal: they are different namespaces and nothing
        /// collides.
        /// </summary>
        private DimensionTemplateAsset BuildPackWithOneWordTwice(
            out DimensionItemAsset item,
            out DimensionLootTableAsset loot,
            out DimensionRecipeAsset recipe,
            out SceneTemplateAsset scene)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Ember");
            SetString(item, "displayName", "Ember");

            loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Ember");

            recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "EmberRecipe");
            SetString(recipe, "outputItemId", "Ember");

            scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Vault");
            SerializedObject sceneSerialized = new SerializedObject(scene);
            SerializedProperty objects = sceneSerialized.FindProperty("sceneObjects");
            objects.arraySize = 1;
            objects.GetArrayElementAtIndex(0)
                .FindPropertyRelative("lootTableId").stringValue = "Ember";
            sceneSerialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalItems(new[] { item });
            template.SetGlobalLootTables(new[] { loot });
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalScenes(new[] { scene });
            return template;
        }

        private static bool Ticked(
            DimensionIdRenamePlan plan,
            Object asset,
            string propertyPath,
            out string because)
        {
            because = null;
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].Asset == asset && plan.Edges[i].PropertyPath == propertyPath)
                {
                    string why;
                    bool ticked = plan.StartsTicked(plan.Edges[i], out why);
                    because = why;
                    return ticked;
                }
            }

            Assert.Fail("No row for " + propertyPath + " on " + asset.name);
            return false;
        }
    }
}
#endif
