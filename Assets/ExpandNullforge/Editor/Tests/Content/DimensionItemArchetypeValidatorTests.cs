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
    /// Proves the archetype validator tells a creator exactly what their chosen archetype still
    /// needs — a plain material passes with minimal data, while a weapon or ore is blocked until
    /// its archetype-specific values exist.
    /// </summary>
    internal sealed class DimensionItemArchetypeValidatorTests
    {
        private readonly List<DimensionItemAsset> created = new List<DimensionItemAsset>();

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
        public void Material_WithBasicsOnly_Passes()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, i =>
            {
                i.FindProperty("itemId").stringValue = "mod:copper_dust";
                i.FindProperty("displayName").stringValue = "Copper Dust";
                i.FindProperty("iconId").stringValue = "icon:copper_dust";
                i.FindProperty("stackableWasMigrated").boolValue = true;
                i.FindProperty("stackable").boolValue = true;
            });

            Assert.That(DimensionItemArchetypeValidator.CanGenerate(item), Is.True, Describe(item));
        }

        [Test]
        public void Material_MissingDisplayName_IsBlocked()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, i =>
            {
                i.FindProperty("itemId").stringValue = "mod:thing";
                i.FindProperty("displayName").stringValue = string.Empty;
                i.FindProperty("iconId").stringValue = "icon:thing";
            });

            Assert.That(DimensionItemArchetypeValidator.CanGenerate(item), Is.False);
            Assert.That(FieldsWithErrors(item), Does.Contain("displayName"));
        }

        [Test]
        public void Weapon_RequiresDamage_AndADurabilityMultiplierThatIsNotZero()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Weapon, i =>
            {
                i.FindProperty("itemId").stringValue = "mod:blade";
                i.FindProperty("displayName").stringValue = "Blade";
                i.FindProperty("iconId").stringValue = "icon:blade";
                i.FindProperty("stackableWasMigrated").boolValue = true;
                i.FindProperty("stackable").boolValue = false;
            });

            // durabilityPoints IS DELIBERATELY NOT REQUIRED. Requiring it is a
            // contradiction: the validator would refuse to generate without a number the generator
            // then tells the author it discarded, because the game recomputes durability from the
            // item type times durabilityMultiplier. Equipment could not be made without filling in
            // a field that does nothing.
            List<string> errors = FieldsWithErrors(item);
            Assert.That(errors, Does.Contain("damageAmount"));
            Assert.That(
                errors,
                Does.Not.Contain("durabilityPoints"),
                "a number the generator throws away must not block generation");
            Assert.That(DimensionItemArchetypeValidator.CanGenerate(item), Is.False);

            // Damage alone clears it: the durability multiplier already defaults to 1.
            SerializedObject serialized = new SerializedObject(item);
            serialized.Update();
            serialized.FindProperty("damageAmount").intValue = 12;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(DimensionItemArchetypeValidator.CanGenerate(item), Is.True, Describe(item));

            // A zero multiplier is the real failure, and it does block.
            serialized = new SerializedObject(item);
            serialized.Update();
            serialized.FindProperty("durabilityMultiplier").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(FieldsWithErrors(item), Does.Contain("durabilityMultiplier"));
        }

        [Test]
        public void Ore_RequiresLootTableAndHealth()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Ore, i =>
            {
                i.FindProperty("itemId").stringValue = "mod:ore_node";
                i.FindProperty("displayName").stringValue = "Ore Node";
                i.FindProperty("objectId").stringValue = "object:ore_node";
            });

            List<string> errors = FieldsWithErrors(item);
            Assert.That(errors, Does.Contain("lootTableId"));
            Assert.That(errors, Does.Contain("healthPoints"));
        }

        [Test]
        public void DisabledItem_IsReportedAsAWarningNotAnError()
        {
            DimensionItemAsset item = MakeItem(DimensionItemArchetype.Material, i =>
            {
                i.FindProperty("itemId").stringValue = "mod:thing";
                i.FindProperty("displayName").stringValue = "Thing";
                i.FindProperty("iconId").stringValue = "icon:thing";
                i.FindProperty("enabled").boolValue = false;
            });

            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            bool hasDisabledWarning = false;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Field == "enabled")
                {
                    Assert.That(
                        findings[i].Severity,
                        Is.EqualTo(DimensionItemArchetypeValidator.Severity.Warning));
                    hasDisabledWarning = true;
                }
            }

            Assert.That(hasDisabledWarning, Is.True);
            Assert.That(DimensionItemArchetypeValidator.CanGenerate(item), Is.True);
        }

        private DimensionItemAsset MakeItem(
            DimensionItemArchetype archetype,
            System.Action<SerializedObject> configure)
        {
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            created.Add(item);
            SerializedObject serialized = new SerializedObject(item);
            serialized.Update();
            serialized.FindProperty("archetype").enumValueIndex =
                System.Array.IndexOf(
                    System.Enum.GetValues(typeof(DimensionItemArchetype)),
                    archetype);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static List<string> FieldsWithErrors(DimensionItemAsset item)
        {
            List<string> fields = new List<string>();
            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity == DimensionItemArchetypeValidator.Severity.Error)
                {
                    fields.Add(findings[i].Field);
                }
            }

            return fields;
        }

        private static string Describe(DimensionItemAsset item)
        {
            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < findings.Count; i++)
            {
                builder.Append('\n').Append(findings[i].Severity).Append(' ')
                    .Append(findings[i].Field).Append(": ").Append(findings[i].Message);
            }

            return builder.ToString();
        }
    }
}
#endif
