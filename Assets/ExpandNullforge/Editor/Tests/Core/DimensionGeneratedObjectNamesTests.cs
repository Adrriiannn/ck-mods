#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The two lists a template answers with, and why they are not the same list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DimensionGeneratedObjectIds.Collect</c> answers "is this name one of ours" for a
    /// reference a creator TYPED. <c>ObjectsMade</c> answers "what <c>objectName</c> will the
    /// prefabs carry", which is what the game answers to and what the world-load check has to be
    /// aimed at. Three kinds of thing differ, and feeding the first list to the second job is what
    /// left the companion table's own exemplar — a boss summoning circle — permanently outside the
    /// subject list, and declared a plant id nothing could ever resolve.
    /// </para>
    /// <para>
    /// Every assertion here compares the two lists against each other on a template built in the
    /// test, so a walk that came back empty fails rather than passes: an empty <c>ObjectsMade</c>
    /// contains none of the names asserted below.
    /// </para>
    /// </remarks>
    public sealed class DimensionGeneratedObjectNamesTests
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
        public void APlantIsNamedByThePlantAndSeedItsPrefabsAreStampedWith()
        {
            DimensionTemplateAsset template = TemplateWithAPlant("brightcap");

            List<string> typed = DimensionGeneratedObjectIds.Collect(template);
            List<string> made = DimensionGeneratedObjectIds.ObjectsMade(template);

            CollectionAssert.Contains(
                typed,
                "brightcap",
                "A creator types the plant's own id, so the reference vocabulary must still hold "
                + "it. Nothing about the subject list changes that.");

            CollectionAssert.Contains(
                made,
                "brightcap" + DimensionPlantGenerator.PlantSuffix,
                "DimensionPlantGenerator stamps the growing crop with the plant id and this "
                + "suffix, and the world-load check looks the objects up by the name they carry.");
            CollectionAssert.Contains(
                made,
                "brightcap" + DimensionPlantGenerator.SeedSuffix,
                "The seed is the second object a crop is built as, and it was in no list either.");
            CollectionAssert.DoesNotContain(
                made,
                "brightcap",
                "No prefab is stamped with the bare plant id, so declaring it puts a name in the "
                + "ledger that can never resolve — and an unresolved name there is explained away "
                + "rather than reported, which is how a real mismatch stays dressed as normal.");
        }

        [Test]
        public void ABossThatCanBeSummonedIsNamedByItsCircleAndItsPin()
        {
            DimensionTemplateAsset template = TemplateWithABoss(
                "emberlarva", summoningItemId: "emberTotem", showsOnTheMap: true);

            List<string> made = DimensionGeneratedObjectIds.ObjectsMade(template);

            CollectionAssert.Contains(made, "emberlarva", "The boss itself is still one of ours.");
            CollectionAssert.Contains(
                made,
                "emberlarva" + DimensionGeneratedObjectIds.BossSummonCircleSuffix,
                "The circle is a separate object with its own name, and it is the case the "
                + "companion table was written for: a SummonAreaCD rule can only ever fire on an "
                + "object the check is handed.");
            CollectionAssert.Contains(
                made,
                "emberlarva" + DimensionGeneratedObjectIds.BossMapMarkerSuffix,
                "The pin is a separate object too.");
        }

        [Test]
        public void ABossThatBuildsNeitherIsNamedByNeither()
        {
            // The generator builds the pin only when the pin shows and the circle only when a
            // summoning item is named. Naming either one anyway would put an id in the ledger that
            // no run ever writes a prefab for.
            DimensionTemplateAsset template = TemplateWithABoss(
                "quietboss", summoningItemId: string.Empty, showsOnTheMap: false);

            List<string> made = DimensionGeneratedObjectIds.ObjectsMade(template);

            CollectionAssert.Contains(made, "quietboss");
            CollectionAssert.DoesNotContain(
                made, "quietboss" + DimensionGeneratedObjectIds.BossSummonCircleSuffix);
            CollectionAssert.DoesNotContain(
                made, "quietboss" + DimensionGeneratedObjectIds.BossMapMarkerSuffix);
        }

        [Test]
        public void ASwitchedOffPlantIsInNeitherList()
        {
            DimensionTemplateAsset template = TemplateWithAPlant("offcrop", enabled: false);

            List<string> made = DimensionGeneratedObjectIds.ObjectsMade(template);

            CollectionAssert.DoesNotContain(made, "offcrop" + DimensionPlantGenerator.PlantSuffix);
            CollectionAssert.DoesNotContain(made, "offcrop" + DimensionPlantGenerator.SeedSuffix);
            CollectionAssert.Contains(
                DimensionGeneratedObjectIds.SwitchedOff(template),
                "offcrop",
                "An unticked asset belongs in the switched-off walk, so a reference to it is told "
                + "apart from a misspelling.");
        }

        // ------------------------------------------------------------------------ fixtures ---

        private DimensionTemplateAsset TemplateWithAPlant(string plantId, bool enabled = true)
        {
            DimensionPlantAsset plant = Track(ScriptableObject.CreateInstance<DimensionPlantAsset>());
            SerializedObject serializedPlant = new SerializedObject(plant);
            serializedPlant.FindProperty("plantId").stringValue = plantId;
            serializedPlant.FindProperty("enabled").boolValue = enabled;
            serializedPlant.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template =
                Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty plants = serialized.FindProperty("globalPlants");
            plants.arraySize = 1;
            plants.GetArrayElementAtIndex(0).objectReferenceValue = plant;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private DimensionTemplateAsset TemplateWithABoss(
            string bossId, string summoningItemId, bool showsOnTheMap)
        {
            DimensionBossAsset boss = Track(ScriptableObject.CreateInstance<DimensionBossAsset>());
            SerializedObject serializedBoss = new SerializedObject(boss);
            serializedBoss.FindProperty("bossId").stringValue = bossId;
            serializedBoss.FindProperty("enabled").boolValue = true;
            serializedBoss.FindProperty("summoningItemId").stringValue = summoningItemId;
            serializedBoss.FindProperty("mapPin").FindPropertyRelative("showsOnTheMap").boolValue =
                showsOnTheMap;
            serializedBoss.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template =
                Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty bosses = serialized.FindProperty("globalBosses");
            bosses.arraySize = 1;
            bosses.GetArrayElementAtIndex(0).objectReferenceValue = boss;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private T Track<T>(T asset) where T : Object
        {
            created.Add(asset);
            return asset;
        }
    }
}
#endif
