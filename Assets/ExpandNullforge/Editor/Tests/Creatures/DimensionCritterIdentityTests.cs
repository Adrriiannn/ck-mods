using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Holds a generated critter to being a critter, in the two places the game asks.
    /// </summary>
    /// <remarks>
    /// Both of these were wrong and neither showed up in the prefab. The generator stamped
    /// <c>ObjectType.Creature</c>, which <c>ObjectConverter</c> treats identically — so nothing
    /// complained — but <c>PlaceObjectSlot</c> refuses to put anything back down that is not a
    /// critter, a placeable prefab or cattle (`ck-db\Pug.Other\PlaceObjectSlot.cs:91`), and the
    /// critter catcher draws a non-Critter as bait rather than as the animal in the tank
    /// (`ck-db\Pug.Objects\CritterCatcher.cs:50`). A critter with "it can be caught" ticked could
    /// therefore be caught and never released. It also carried no object category tag at all,
    /// where every vanilla critter prefab carries <c>Critter</c>.
    /// </remarks>
    public sealed class DimensionCritterIdentityTests
    {
        private const string TestRoot = "Assets/NullforgeCritterTests";

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeCritterTests");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private static GameObject Generate(string id, bool canBeCaught)
        {
            DimensionCritterAsset critter = ScriptableObject.CreateInstance<DimensionCritterAsset>();
            SerializedObject serialized = new SerializedObject(critter);
            serialized.FindProperty("critterId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("canBeCaught").boolValue = canBeCaught;
            SerializedProperty biomes = serialized.FindProperty("allowedBiomeIds");
            biomes.arraySize = 1;
            biomes.GetArrayElementAtIndex(0).stringValue = "mod:biome";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionCritterGenerator.Generate(
                new List<DimensionCritterAsset> { critter },
                TestRoot,
                default(DimensionNamingContext));

            Object.DestroyImmediate(critter);
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
        }

        [Test]
        public void ACaughtCritterCanBePutBackDown()
        {
            GameObject prefab = Generate("beetle", true);

            Assert.AreEqual(
                ObjectType.Critter,
                prefab.GetComponent<ObjectAuthoring>().objectType,
                "as Creature it can be caught and then never released, which reads as the catch " +
                "having eaten it");
        }

        [Test]
        public void ACritterSaysItIsACritter()
        {
            GameObject prefab = Generate("centipede", false);

            Assert.IsTrue(
                prefab.GetComponent<ObjectAuthoring>().tags.Contains(ObjectCategoryTag.Critter),
                "the tag list is how everything else tells a critter from a rock");
        }
    }
}
