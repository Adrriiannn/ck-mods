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
    /// Runs the item generator for real against the AssetDatabase: prefabs are written, the
    /// archetype decides which authoring components exist, switching archetype cleans up the
    /// components that no longer apply, and blocked or disabled items never produce an object.
    ///
    /// These execute in Unity (not in the offline compile check), so they are what proves the
    /// generator actually works rather than merely compiling.
    /// </summary>
    internal sealed class DimensionItemGeneratorTests
    {
        private const string TestFolder = "Assets/ExpandNullforgeGeneratorTests";

        private readonly List<DimensionItemAsset> created = new List<DimensionItemAsset>();

        [SetUp]
        public void SetUp()
        {
            DeleteTestFolder();
        }

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
            DeleteTestFolder();
        }

        [Test]
        public void Material_ProducesAPrefabWithTheAlwaysOnComponents()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, "mod_copper_dust");

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Errors, Is.Empty, string.Join("; ", report.Errors));
            Assert.That(report.Created.Count, Is.EqualTo(1), report.Summarize());

            GameObject prefab = LoadPrefab("mod_copper_dust");
            Assert.That(prefab, Is.Not.Null, "The prefab was not written.");
            Assert.That(prefab.GetComponent<ObjectAuthoring>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LocalizationAuthoring>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<InventoryItemAuthoring>(), Is.Not.Null);

            // A material is not a weapon and must not carry combat data.
            Assert.That(prefab.GetComponent<WeaponDamageAuthoring>(), Is.Null);
            Assert.That(prefab.GetComponent<DurabilityAuthoring>(), Is.Null);
        }

        [Test]
        public void Weapon_CarriesItsDamageAndDurabilityValues()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_blade",
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 17;
                    serialized.FindProperty("durabilityPoints").intValue = 250;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);
            Assert.That(report.Errors, Is.Empty, string.Join("; ", report.Errors));

            GameObject prefab = LoadPrefab("mod_blade");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<WeaponDamageAuthoring>().damage, Is.EqualTo(17));
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().durability, Is.EqualTo(250));
        }

        [Test]
        public void SwitchingArchetype_RemovesTheComponentsThatNoLongerApply()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_shifter",
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 9;
                    serialized.FindProperty("durabilityPoints").intValue = 100;
                });

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);
            Assert.That(
                LoadPrefab("mod_shifter").GetComponent<WeaponDamageAuthoring>(),
                Is.Not.Null,
                "Precondition: the weapon should have damage.");

            // The creator changes their mind: it is just a crafting material now.
            SetArchetype(item, DimensionItemArchetype.Material);
            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Updated.Count, Is.EqualTo(1), report.Summarize());
            GameObject prefab = LoadPrefab("mod_shifter");
            Assert.That(
                prefab.GetComponent<WeaponDamageAuthoring>(),
                Is.Null,
                "Stale weapon data was left on the prefab after switching archetype.");
            Assert.That(prefab.GetComponent<DurabilityAuthoring>(), Is.Null);
            Assert.That(prefab.GetComponent<ObjectAuthoring>(), Is.Not.Null);
        }

        [Test]
        public void BlockedItem_IsSkippedRatherThanHalfBuilt()
        {
            // A weapon with no damage or durability cannot generate.
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Weapon, "mod_incomplete");

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Errors, Is.Not.Empty);
            Assert.That(report.Created, Is.Empty);
            Assert.That(LoadPrefab("mod_incomplete"), Is.Null, "A blocked item must not be written.");
        }

        [Test]
        public void DisabledItem_IsSkippedWithAReason()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Material,
                "mod_disabled",
                serialized => serialized.FindProperty("enabled").boolValue = false);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Skipped.Count, Is.EqualTo(1));
            Assert.That(report.Created, Is.Empty);
            Assert.That(LoadPrefab("mod_disabled"), Is.Null);
        }

        [Test]
        public void RegeneratingAnItem_UpdatesInPlaceInsteadOfDuplicating()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, "mod_stable");

            DimensionItemGenerationReport first =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);
            DimensionItemGenerationReport second =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(first.Created.Count, Is.EqualTo(1));
            Assert.That(second.Created, Is.Empty);
            Assert.That(second.Updated.Count, Is.EqualTo(1));
            Assert.That(first.Created[0], Is.EqualTo(second.Updated[0]));
        }

        [Test]
        public void AnUnknownLootTable_IsReportedRatherThanSilentlyWrong()
        {
            // lootTableID is a game enum: an id the game does not define must not quietly become
            // whatever sits at index 0.
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Ore,
                "mod_ore",
                serialized =>
                {
                    serialized.FindProperty("lootTableId").stringValue =
                        "definitely_not_a_real_loot_table";
                    serialized.FindProperty("healthPoints").intValue = 40;
                    serialized.FindProperty("objectId").stringValue = "object:mod_ore";
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Warnings, Is.Not.Empty, "An unknown loot table must be reported.");
            bool mentionsLootTable = false;
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                if (report.Warnings[i].Contains("lootTableID"))
                {
                    mentionsLootTable = true;
                }
            }

            Assert.That(mentionsLootTable, Is.True, string.Join("; ", report.Warnings));
        }

        [Test]
        public void AnInvalidOutputFolder_IsRefused()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, "mod_thing");

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, "C:/somewhere/else");

            Assert.That(report.Errors, Is.Not.Empty);
            Assert.That(report.Created, Is.Empty);
        }

        private static GameObject LoadPrefab(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                TestFolder + "/" + fileName + ".prefab");
        }

        private DimensionItemAsset MakeItem(
            DimensionItemArchetype archetype,
            string itemId,
            System.Action<SerializedObject> configure = null)
        {
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            created.Add(item);

            SerializedObject serialized = new SerializedObject(item);
            serialized.Update();
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("displayName").stringValue = itemId;
            serialized.FindProperty("iconId").stringValue = "icon:" + itemId;
            serialized.FindProperty("maxStack").intValue = 99;
            serialized.FindProperty("enabled").boolValue = true;
            SetArchetypeProperty(serialized, archetype);
            configure?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void SetArchetype(
            DimensionItemAsset item,
            DimensionItemArchetype archetype)
        {
            SerializedObject serialized = new SerializedObject(item);
            serialized.Update();
            SetArchetypeProperty(serialized, archetype);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArchetypeProperty(
            SerializedObject serialized,
            DimensionItemArchetype archetype)
        {
            serialized.FindProperty("archetype").enumValueIndex =
                System.Array.IndexOf(
                    System.Enum.GetValues(typeof(DimensionItemArchetype)),
                    archetype);
        }

        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
