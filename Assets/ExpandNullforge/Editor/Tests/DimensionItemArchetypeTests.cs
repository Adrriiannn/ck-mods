#if UNITY_INCLUDE_TESTS
using System;
using ExpandNullforge.Api;
using NUnit.Framework;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Pins the archetype → authoring-components contract. The generator and the dashboard both
    /// read this, so if it drifts a creator would see fields that never reach the game (or an
    /// object would be emitted missing a component it needs).
    /// </summary>
    internal sealed class DimensionItemArchetypeTests
    {
        [Test]
        public void EveryArchetypeIncludesTheAlwaysRequiredComponents()
        {
            foreach (DimensionItemArchetype archetype in AllArchetypes())
            {
                DimensionItemAuthoringComponents components =
                    DimensionItemArchetypeRules.GetRequiredComponents(archetype);
                Assert.That(
                    (components & DimensionItemArchetypeRules.Always),
                    Is.EqualTo(DimensionItemArchetypeRules.Always),
                    archetype + " must always emit object, localization and visual components.");
            }
        }

        [Test]
        public void WeaponRequiresCombatComponentsAndMaterialDoesNot()
        {
            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Weapon,
                    DimensionItemAuthoringComponents.WeaponDamage),
                Is.True);
            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Weapon,
                    DimensionItemAuthoringComponents.Durability),
                Is.True);

            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Material,
                    DimensionItemAuthoringComponents.WeaponDamage),
                Is.False);
            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Material,
                    DimensionItemAuthoringComponents.Placement),
                Is.False,
                "A plain material is not placed in the world.");
        }

        [Test]
        public void LootBearingArchetypesRequireBreakableAndLoot()
        {
            foreach (DimensionItemArchetype archetype in new[]
                     {
                         DimensionItemArchetype.Ore,
                         DimensionItemArchetype.Breakable,
                         DimensionItemArchetype.Mob,
                         DimensionItemArchetype.Boss
                     })
            {
                Assert.That(
                    DimensionItemArchetypeRules.Requires(
                        archetype,
                        DimensionItemAuthoringComponents.Loot),
                    Is.True,
                    archetype + " should drop loot.");
                Assert.That(
                    DimensionItemArchetypeRules.Requires(
                        archetype,
                        DimensionItemAuthoringComponents.Breakable),
                    Is.True,
                    archetype + " should be destructible.");
            }

            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Boss,
                    DimensionItemAuthoringComponents.BossEncounter),
                Is.True,
                "Only a boss gets encounter hooks.");
            Assert.That(
                DimensionItemArchetypeRules.Requires(
                    DimensionItemArchetype.Mob,
                    DimensionItemAuthoringComponents.BossEncounter),
                Is.False);
        }

        [Test]
        public void WorldPlacedAndInventoryClassificationsAreConsistent()
        {
            Assert.That(DimensionItemArchetypeRules.IsWorldPlaced(DimensionItemArchetype.Block), Is.True);
            Assert.That(DimensionItemArchetypeRules.IsWorldPlaced(DimensionItemArchetype.Mob), Is.True);
            Assert.That(DimensionItemArchetypeRules.IsWorldPlaced(DimensionItemArchetype.Material), Is.False);

            Assert.That(DimensionItemArchetypeRules.IsInventoryItem(DimensionItemArchetype.Material), Is.True);
            Assert.That(DimensionItemArchetypeRules.IsInventoryItem(DimensionItemArchetype.Weapon), Is.True);
            Assert.That(
                DimensionItemArchetypeRules.IsInventoryItem(DimensionItemArchetype.Ore),
                Is.False,
                "An ore node is a world object, not an inventory entry (its drops are items).");
        }

        [Test]
        public void CustomArchetypeStaysMinimal()
        {
            Assert.That(
                DimensionItemArchetypeRules.GetRequiredComponents(DimensionItemArchetype.Custom),
                Is.EqualTo(DimensionItemArchetypeRules.Always),
                "Custom must not force components the framework cannot validate.");
        }

        [Test]
        public void DescribeReturnsALabelForEveryArchetype()
        {
            foreach (DimensionItemArchetype archetype in AllArchetypes())
            {
                Assert.That(
                    DimensionItemArchetypeRules.Describe(archetype),
                    Is.Not.Null.And.Not.Empty,
                    archetype.ToString());
            }
        }

        [Test]
        public void DescribeComponentsNamesWhatTheArchetypeWillEmit()
        {
            // Every archetype emits at least the always-on trio, so the summary is never blank.
            foreach (DimensionItemArchetype archetype in AllArchetypes())
            {
                string description = DimensionItemArchetypeRules.DescribeComponents(archetype);
                Assert.That(description, Is.Not.Null.And.Not.Empty, archetype.ToString());
                Assert.That(description, Does.Contain("ObjectAuthoring"), archetype.ToString());
            }

            Assert.That(
                DimensionItemArchetypeRules.DescribeComponents(DimensionItemArchetype.Weapon),
                Does.Contain("WeaponDamage"));
            Assert.That(
                DimensionItemArchetypeRules.DescribeComponents(DimensionItemArchetype.Material),
                Does.Not.Contain("WeaponDamage"));
        }

        private static DimensionItemArchetype[] AllArchetypes()
        {
            return (DimensionItemArchetype[])Enum.GetValues(typeof(DimensionItemArchetype));
        }
    }
}
#endif
