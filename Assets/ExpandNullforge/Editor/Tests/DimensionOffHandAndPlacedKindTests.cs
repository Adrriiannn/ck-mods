using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers off-hand items, mana costs, jewellery, fence gates, flowers and spawner platforms.
    /// </summary>
    /// <remarks>
    /// Mana is the one worth noting. It is its own component and applies to staffs and secondary
    /// uses as much as to off-hand items, but a creator asking "what does this cost to use" is
    /// asking one question — so it lives on the off-hand template and is written whether or not the
    /// item is an off-hand one.
    /// </remarks>
    public sealed class DimensionOffHandAndPlacedKindTests
    {
        private const string TestRoot = "Assets/NullforgeOffHandTests";

        private DimensionItemAsset item;
        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeOffHandTests");
            }

            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = "testshield";
            serialized.FindProperty("displayName").stringValue = "Test Shield";
            serialized.FindProperty("iconId").stringValue = "1";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject other = new SerializedObject(worldObject);
            other.FindProperty("objectIdentifier").stringValue = "testgate";
            other.FindProperty("displayName").stringValue = "Test Gate";
            other.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void Cleanup()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void SetItem(System.Action<SerializedObject> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetObject(System.Action<SerializedObject> write)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            write(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport RunItem()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private DimensionWorldObjectGenerationReport RunObject()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject LoadItem()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testshield.prefab");
        }

        private static GameObject LoadObject()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testgate.prefab");
        }

        [Test]
        public void AnOffHandItemGetsItsMechanicAndItsStrength()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                SerializedProperty offHand = serialized.FindProperty("offHand");
                offHand.FindPropertyRelative("kind").intValue = (int)DimensionOffHandKind.Dash;
                offHand.FindPropertyRelative("strength").floatValue = 4f;
            });
            RunItem();

            OffHandAuthoring authored = LoadItem().GetComponent<OffHandAuthoring>();
            Assert.IsNotNull(authored);
            Assert.AreEqual(OffHandMechanic.Dash, authored.mechanic);
            Assert.AreEqual(4f, authored.mechanicValue);
        }

        [Test]
        public void SomethingThatIsNotAnOffHandItemCarriesNoOffHand()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("offHand").FindPropertyRelative("kind").intValue =
                    (int)DimensionOffHandKind.Shield;
            });
            RunItem();
            Assert.IsNotNull(LoadItem().GetComponent<OffHandAuthoring>());

            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("offHand").FindPropertyRelative("kind").intValue =
                    (int)DimensionOffHandKind.NotAnOffHandItem;
            });
            RunItem();
            Assert.IsNull(LoadItem().GetComponent<OffHandAuthoring>());
        }

        [Test]
        public void ManaIsWrittenEvenWhenTheItemIsNotAnOffHandOne()
        {
            // A staff's secondary use costs mana too.
            SetItem(delegate(SerializedObject serialized)
            {
                SerializedProperty offHand = serialized.FindProperty("offHand");
                offHand.FindPropertyRelative("kind").intValue =
                    (int)DimensionOffHandKind.NotAnOffHandItem;
                offHand.FindPropertyRelative("manaCost").intValue = 12;
            });
            RunItem();

            ConsumesManaAuthoring mana = LoadItem().GetComponent<ConsumesManaAuthoring>();
            Assert.IsNotNull(mana);
            Assert.AreEqual(12, mana.manaCost);
            Assert.IsNull(LoadItem().GetComponent<OffHandAuthoring>());
        }

        [Test]
        public void AnOffHandItemWithNoStrengthIsReported()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                SerializedProperty offHand = serialized.FindProperty("offHand");
                offHand.FindPropertyRelative("kind").intValue = (int)DimensionOffHandKind.Shield;
                offHand.FindPropertyRelative("strength").floatValue = 0f;
            });

            DimensionItemGenerationReport report = RunItem();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("no strength")));
            Assert.IsTrue(item.OffHand.DoesNothingWhenUsed);
        }

        [Test]
        public void JewelleryPolishesIntoSomethingRealOrIsReported()
        {
            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("polishesInto").stringValue = "Wood";
            });
            RunItem();
            Assert.AreEqual(
                ObjectID.Wood,
                LoadItem().GetComponent<JewelryAuthoring>().polishedVersion);

            SetItem(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("polishesInto").stringValue = "NotAnObject";
            });
            DimensionItemGenerationReport report = RunItem();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotAnObject")));
            Assert.IsNull(LoadItem().GetComponent<JewelryAuthoring>());
        }

        [Test]
        public void AFenceGateIsAMarkerAddedAndRemovedWithItsTick()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("isAFenceGate").boolValue = true;
            });
            RunObject();
            Assert.IsNotNull(LoadObject().GetComponent<FenceGateAuthoring>());

            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("isAFenceGate").boolValue = false;
            });
            RunObject();
            Assert.IsNull(LoadObject().GetComponent<FenceGateAuthoring>());
        }

        [Test]
        public void ASpawnerPlatformProducesTheEnemyItNames()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("spawnsEnemyId").stringValue = "Wood";
            });
            RunObject();

            Assert.AreEqual(
                ObjectID.Wood,
                LoadObject().GetComponent<EnemySpawnerPlatformAuthoring>().enemyToSpawn);
        }

        [Test]
        public void ASpawnerNamingNothingRealIsReported()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("spawnsEnemyId").stringValue = "NotACreature";
            });

            DimensionWorldObjectGenerationReport report = RunObject();

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("NotACreature")));
            Assert.IsNull(LoadObject().GetComponent<EnemySpawnerPlatformAuthoring>());
        }

        [Test]
        public void AFlowerBelongsToThePlantItNames()
        {
            SetObject(delegate(SerializedObject serialized)
            {
                serialized.FindProperty("flowerOfPlantId").stringValue = "Wood";
                serialized.FindProperty("flowerVariation").intValue = 2;
            });
            RunObject();

            FlowerAuthoring flower = LoadObject().GetComponent<FlowerAuthoring>();
            Assert.IsNotNull(flower);
            Assert.AreEqual(ObjectID.Wood, flower.plantID);
            Assert.AreEqual(2, flower.plantVariation);
        }
    }
}
