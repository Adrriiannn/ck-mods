using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// The object each test generates, and the small helpers that read it back.
    /// </summary>
    public sealed partial class DimensionEmittedLightTests
    {
        private const string TestRoot = "Assets/NullforgeEmittedLightTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SetString("objectIdentifier", "testlight");
            SetString("displayName", "Test Light");
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        /// <summary>Writes one answer onto the asset the way the inspector writes it.</summary>
        /// <remarks>
        /// <c>ApplyModifiedProperties</c> rather than the WithoutUndo variant, and the four setters
        /// below all use it: it is the call the inspector makes, and it is what runs the asset's
        /// <c>OnValidate</c> — which is where choosing one of the game's named lights fills the rest
        /// of the fold in. Writing the field by any other route would take the thing an author
        /// actually does out of the test.
        /// </remarks>
        private void SetString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            // intValue, not enumValueIndex — the same reason the container tests give: the index is
            // a position in the drawn list, and the value is what the field holds.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>Says what using the object does, the way the inspector says it.</summary>
        /// <remarks>
        /// This is the answer the whole pooling problem hangs off: the moment it is anything but
        /// Nothing, the prefab a player walks up to carries one of the framework's views and is
        /// shared with every other object of that use.
        /// </remarks>
        private void SetUse(DimensionUseBehaviour use)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized
                .FindProperty("interaction")
                .FindPropertyRelative("whatUsingItDoes")
                .intValue = (int)use;
            serialized.ApplyModifiedProperties();
        }

        private DimensionWorldObjectGenerationReport Run()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        /// <summary>The prefab a player walks up to, which is where a placed light lives.</summary>
        private static GameObject LoadVisual()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testlightVisual.prefab");
        }

        /// <summary>The object's own prefab — the one that points at the visual above.</summary>
        private static GameObject LoadEntity()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testlight.prefab");
        }

        private static ManagedLight[] EveryLightOn(GameObject prefab)
        {
            return prefab == null
                ? new ManagedLight[0]
                : prefab.GetComponentsInChildren<ManagedLight>(true);
        }

        /// <summary>Whether a node carries a component of this type name.</summary>
        private static bool CarriesAComponentCalled(GameObject node, string typeName)
        {
            Component[] components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a report says a left-over visual was removed.</summary>
        private static bool SomethingWasTakenAway(DimensionWorldObjectGenerationReport report)
        {
            for (int i = 0; report != null && i < report.Warnings.Count; i++)
            {
                if (report.Warnings[i].Contains("was taken away"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertPreset(
            DimensionLightLike light,
            float red,
            float green,
            float blue,
            float brightness,
            float range,
            float dimmest,
            float brightest,
            bool flameMoves,
            float height,
            float frontToBack,
            string measuredAt)
        {
            DimensionEmittedLightTemplate.Preset preset =
                DimensionEmittedLightTemplate.PresetFor(light);
            Assert.AreEqual(red, preset.Colour.r, 0.0001f, light + " red, " + measuredAt);
            Assert.AreEqual(green, preset.Colour.g, 0.0001f, light + " green, " + measuredAt);
            Assert.AreEqual(blue, preset.Colour.b, 0.0001f, light + " blue, " + measuredAt);
            Assert.AreEqual(brightness, preset.Brightness, 0.0001f, light + " brightness, " + measuredAt);
            Assert.AreEqual(range, preset.Range, 0.0001f, light + " range, " + measuredAt);
            Assert.AreEqual(dimmest, preset.Dimmest, 0.0001f, light + " dimmest, " + measuredAt);
            Assert.AreEqual(brightest, preset.Brightest, 0.0001f, light + " brightest, " + measuredAt);
            Assert.AreEqual(flameMoves, preset.FlameMoves, light + " flame movement, " + measuredAt);
            Assert.AreEqual(height, preset.HeightAboveTheFloor, 0.0001f, light + " height, " + measuredAt);
            Assert.AreEqual(
                frontToBack, preset.FrontToBack, 0.0001f, light + " front to back, " + measuredAt);
        }

        /// <summary>The bootstrap text this object's light would be written into.</summary>
        private string EmitLights()
        {
            DimensionTemplateAsset template =
                ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(template);
                SerializedProperty list = serialized.FindProperty("globalWorldObjects");
                Assert.IsNotNull(list, "globalWorldObjects is gone, so nothing was emitted.");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = worldObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                DimensionRuntimeConsumerBootstrapUtility.AppendEmittedLightRegistrations(
                    builder, template, "TestMod");
                return builder.ToString();
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }
    }
}
