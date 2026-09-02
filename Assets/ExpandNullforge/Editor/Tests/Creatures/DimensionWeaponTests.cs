using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what makes a weapon swing, fire or cast.
    /// </summary>
    /// <remarks>
    /// The item archetype has had a Weapon option all along and generating one wrote a damage number
    /// and nothing else — a value with no swing behind it. The rule worth guarding hardest is that
    /// Core Keeper keeps the three kinds in three separate components and an item carries exactly
    /// one: a sword that became a bow and kept both would swing as well as fire.
    /// </remarks>
    public sealed class DimensionWeaponTests
    {
        private const string TestRoot = "Assets/NullforgeWeaponTests";

        private DimensionItemAsset item;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeWeaponTests");
            }

            item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            Set("itemId", "testsword");
            Set("displayName", "Test Sword");
            Set("iconId", "1");
        }

        [TearDown]
        public void Cleanup()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetWeapon(DimensionWeaponKind kind)
        {
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("weapon").FindPropertyRelative("kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetWeaponField(string field, System.Action<SerializedProperty> write)
        {
            SerializedObject serialized = new SerializedObject(item);
            write(serialized.FindProperty("weapon").FindPropertyRelative(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionItemGenerationReport Run()
        {
            return DimensionItemGenerator.Generate(
                new List<DimensionItemAsset> { item },
                TestRoot);
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testsword.prefab");
        }

        [Test]
        public void AnItemCarriesExactlyOneOfTheThreeWeaponComponents()
        {
            SetWeapon(DimensionWeaponKind.Melee);
            Run();
            Assert.IsNotNull(Load().GetComponent<MeleeWeaponAuthoring>());
            Assert.IsNull(Load().GetComponent<RangeWeaponAuthoring>());
            Assert.IsNull(Load().GetComponent<CastItemAuthoring>());

            SetWeapon(DimensionWeaponKind.Ranged);
            Run();
            Assert.IsNull(
                Load().GetComponent<MeleeWeaponAuthoring>(),
                "a sword that became a bow must not still swing");
            Assert.IsNotNull(Load().GetComponent<RangeWeaponAuthoring>());

            SetWeapon(DimensionWeaponKind.Cast);
            Run();
            Assert.IsNull(Load().GetComponent<RangeWeaponAuthoring>());
            Assert.IsNotNull(Load().GetComponent<CastItemAuthoring>());
        }

        [Test]
        public void SomethingThatIsNotAWeaponCarriesNoneOfThem()
        {
            SetWeapon(DimensionWeaponKind.Melee);
            Run();
            Assert.IsNotNull(Load().GetComponent<MeleeWeaponAuthoring>());

            SetWeapon(DimensionWeaponKind.NotAWeapon);
            Run();
            Assert.IsNull(Load().GetComponent<MeleeWeaponAuthoring>());
            Assert.IsNull(Load().GetComponent<WeaponSkillGainedMultiplierAuthoring>());
        }

        [Test]
        public void TheSwingDialsReachTheGame()
        {
            SetWeapon(DimensionWeaponKind.Melee);
            SetWeaponField("reach", delegate(SerializedProperty p) { p.floatValue = 1.8f; });
            SetWeaponField("swingArc", delegate(SerializedProperty p)
            {
                p.intValue = (int)DimensionSwingArc.FullCircle360;
            });
            SetWeaponField("flourish", delegate(SerializedProperty p)
            {
                p.intValue = (int)DimensionAttackFlourish.Arc;
            });
            Run();

            MeleeWeaponAuthoring melee = Load().GetComponent<MeleeWeaponAuthoring>();
            Assert.AreEqual(1.8f, melee.baseHitColliderSize);
            Assert.AreEqual(ArcAngle.arc360, melee.arcAngle);
            Assert.AreEqual(AttackFXType.Arc, melee.attackFXType);
        }

        [Test]
        public void ABowNamingAProjectileTheGameDoesNotHaveIsReported()
        {
            // The failure that makes a bow useless while looking finished: it equips, it plays its
            // animation, and nothing comes out.
            SetWeapon(DimensionWeaponKind.Ranged);
            SetWeaponField("firesProjectileId", delegate(SerializedProperty p)
            {
                p.stringValue = "NotAProjectile";
            });

            DimensionItemGenerationReport report = Run();

            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("NotAProjectile")));
            Assert.AreEqual(ObjectID.None, Load().GetComponent<RangeWeaponAuthoring>().projectileID);
        }

        [Test]
        public void ACastItemGetsItsPurposeAndItsTiming()
        {
            SetWeapon(DimensionWeaponKind.Cast);
            SetWeaponField("castPurpose", delegate(SerializedProperty p)
            {
                p.intValue = (int)DimensionCastPurpose.ScanWorld;
            });
            SetWeaponField("castSeconds", delegate(SerializedProperty p) { p.floatValue = 2.5f; });
            Run();

            CastItemAuthoring cast = Load().GetComponent<CastItemAuthoring>();
            Assert.AreEqual(CastItemUseType.ScanWorld, cast.useType);
            Assert.AreEqual(2.5f, cast.castTime);
        }

        [Test]
        public void TheQuietWeaponMistakesAreDetectableWithoutGenerating()
        {
            SetWeapon(DimensionWeaponKind.Ranged);
            Assert.IsTrue(item.Weapon.FiresNothing, "a bow with nothing to fire");

            SetWeaponField("extraShots", delegate(SerializedProperty p) { p.intValue = 2; });
            Assert.IsTrue(
                item.Weapon.FiresEveryShotDownTheSameLine,
                "extra shots with no spread all leave on one line");

            SetWeapon(DimensionWeaponKind.Cast);
            Assert.IsTrue(item.Weapon.CastsForNothing);
            Assert.IsFalse(item.Weapon.FiresNothing, "not a ranged weapon any more");
        }

        [Test]
        public void HowFastItRaisesTheSkillIsWrittenForEveryKind()
        {
            SetWeapon(DimensionWeaponKind.Cast);
            SetWeaponField("skillGainMultiplier", delegate(SerializedProperty p)
            {
                p.floatValue = 2f;
            });
            Run();

            Assert.AreEqual(
                2f,
                Load().GetComponent<WeaponSkillGainedMultiplierAuthoring>().skillMultiplier);
        }
    }
}
