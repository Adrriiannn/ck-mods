using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the drop-location inversion: authored from the item, resolved into sources.
    /// </summary>
    /// <remarks>
    /// The inversion is the whole point, so it is what these test. Core Keeper stores loot on the
    /// thing that gives it; a person designing an item thinks from the item's side. Getting the
    /// grouping wrong would either scatter one source's loot across several writes or merge two
    /// unrelated sources into one.
    /// </remarks>
    public sealed class DimensionDropSourceTests
    {
        private readonly List<Object> temporaries = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < temporaries.Count; i++)
            {
                if (temporaries[i] != null)
                {
                    Object.DestroyImmediate(temporaries[i]);
                }
            }

            temporaries.Clear();
        }

        /// <summary>
        /// Builds a drop source through a host asset, the way the authoring assets set templates.
        /// </summary>
        private DimensionDropSource Source(
            DimensionDropSourceKind kind,
            string sourceId,
            float chance = 1f,
            int weight = 1,
            int minAmount = 1,
            int maxAmount = 1,
            bool enabled = true,
            string biome = "")
        {
            // A world object is used purely as a serialization host; any asset with a serialized
            // array of the type would do.
            DimensionWorldObjectAsset host = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            temporaries.Add(host);

            SerializedObject holder = new SerializedObject(host);
            SerializedProperty list = holder.FindProperty("dropsFrom");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("sourceId").stringValue = sourceId;
            entry.FindPropertyRelative("chance").floatValue = chance;
            entry.FindPropertyRelative("weight").intValue = weight;
            entry.FindPropertyRelative("minAmount").intValue = minAmount;
            entry.FindPropertyRelative("maxAmount").intValue = maxAmount;
            entry.FindPropertyRelative("enabled").boolValue = enabled;
            entry.FindPropertyRelative("onlyInBiomeId").stringValue = biome;
            holder.ApplyModifiedPropertiesWithoutUndo();

            return host.DropsFrom.Length > 0 ? host.DropsFrom[0] : null;
        }

        private static List<DimensionDropsForSource> Group(
            params KeyValuePair<string, DimensionDropSource[]>[] items)
        {
            return DimensionDropCollector.GroupBySource(items);
        }

        private static KeyValuePair<string, DimensionDropSource[]> Item(
            string itemId,
            params DimensionDropSource[] sources)
        {
            return new KeyValuePair<string, DimensionDropSource[]>(itemId, sources);
        }

        // ---- the inversion ----

        [Test]
        public void TwoItemsNamingTheSameCreatureBecomeOneSourceWithBothDrops()
        {
            // This is the whole point: the author writes it per item, the game needs it per source.
            List<DimensionDropsForSource> grouped = Group(
                Item("EmberShard", Source(DimensionDropSourceKind.Creature, "Slime")),
                Item("AshDust", Source(DimensionDropSourceKind.Creature, "Slime")));

            Assert.AreEqual(1, grouped.Count, "one creature, not two");
            Assert.AreEqual(2, grouped[0].Drops.Count);
            Assert.AreEqual("Slime", grouped[0].SourceId);
        }

        [Test]
        public void OneItemNamingSeveralPlacesReachesAllOfThem()
        {
            List<DimensionDropsForSource> grouped = Group(
                Item(
                    "EmberShard",
                    Source(DimensionDropSourceKind.Creature, "Slime"),
                    Source(DimensionDropSourceKind.Container, "DesertChest"),
                    Source(DimensionDropSourceKind.Destructible, "Rubble")));

            Assert.AreEqual(3, grouped.Count);
        }

        [Test]
        public void ACreatureAndAContainerSharingANameStayApart()
        {
            // Merging them would put creature loot into a chest, which nothing would report.
            List<DimensionDropsForSource> grouped = Group(
                Item("EmberShard", Source(DimensionDropSourceKind.Creature, "Guardian")),
                Item("AshDust", Source(DimensionDropSourceKind.Container, "Guardian")));

            Assert.AreEqual(2, grouped.Count, "kind is part of the identity, not just the id");
        }

        [Test]
        public void TheItemTravelsWithItsOwnRatesRatherThanTheSources()
        {
            // Two items from one creature can be rare and common independently.
            List<DimensionDropsForSource> grouped = Group(
                Item("EmberShard", Source(DimensionDropSourceKind.Creature, "Slime", chance: 0.05f, weight: 1)),
                Item("Slimeball", Source(DimensionDropSourceKind.Creature, "Slime", chance: 1f, weight: 50)));

            Assert.AreEqual(1, grouped.Count);
            DimensionResolvedDrop rare = grouped[0].Drops.Find(d => d.ItemId == "EmberShard");
            DimensionResolvedDrop common = grouped[0].Drops.Find(d => d.ItemId == "Slimeball");

            Assert.AreEqual(0.05f, rare.Source.Chance, 0.001f);
            Assert.AreEqual(50, common.Source.Weight);
        }

        [Test]
        public void ABiomeConstraintSurvivesTheInversion()
        {
            // LootInfo.onlyDropsInBiome is Core Keeper's own field, so "only in the desert" is a
            // constraint the game already understands.
            List<DimensionDropsForSource> grouped = Group(
                Item("SandPearl", Source(DimensionDropSourceKind.Creature, "Scarab", biome: "Desert")));

            Assert.AreEqual("Desert", grouped[0].Drops[0].Source.OnlyInBiomeId);
        }

        // ---- entries that should not reach a source ----

        [Test]
        public void ADisabledDropIsLeftOut()
        {
            List<DimensionDropsForSource> grouped = Group(
                Item("EmberShard", Source(DimensionDropSourceKind.Creature, "Slime", enabled: false)));

            Assert.IsEmpty(grouped);
        }

        [Test]
        public void ADropNamingNoSourceIsLeftOut()
        {
            List<DimensionDropsForSource> grouped = Group(
                Item("EmberShard", Source(DimensionDropSourceKind.Creature, string.Empty)));

            Assert.IsEmpty(grouped, "It would otherwise become a source with a blank name.");
        }

        [Test]
        public void GroupingNothingIsSafe()
        {
            Assert.IsEmpty(DimensionDropCollector.GroupBySource(null));
            Assert.IsEmpty(Group());
        }

        // ---- the plan: one inversion per generate, handed out per source ----

        /// <summary>Builds an item asset carrying one drop source.</summary>
        private DimensionItemAsset ItemDroppingFrom(
            string itemId,
            DimensionDropSourceKind kind,
            string sourceId)
        {
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            temporaries.Add(item);

            SerializedObject holder = new SerializedObject(item);
            holder.FindProperty("itemId").stringValue = itemId;
            SerializedProperty list = holder.FindProperty("dropsFrom");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("sourceId").stringValue = sourceId;
            entry.FindPropertyRelative("chance").floatValue = 1f;
            entry.FindPropertyRelative("weight").intValue = 1;
            entry.FindPropertyRelative("minAmount").intValue = 1;
            entry.FindPropertyRelative("maxAmount").intValue = 1;
            entry.FindPropertyRelative("enabled").boolValue = true;
            holder.ApplyModifiedPropertiesWithoutUndo();

            return item;
        }

        [Test]
        public void ThePlanHandsEachSourceOnlyItsOwnDrops()
        {
            DimensionDropPlan plan = DimensionDropPlan.Build(
                new List<DimensionItemAsset>
                {
                    ItemDroppingFrom("EmberShard", DimensionDropSourceKind.Creature, "Slime"),
                    ItemDroppingFrom("SandPearl", DimensionDropSourceKind.Creature, "Scarab")
                },
                null);

            Assert.AreEqual(1, plan.For(DimensionDropSourceKind.Creature, "Slime").Drops.Count);
            Assert.AreEqual("EmberShard",
                plan.For(DimensionDropSourceKind.Creature, "Slime").Drops[0].ItemId);
            Assert.AreEqual("SandPearl",
                plan.For(DimensionDropSourceKind.Creature, "Scarab").Drops[0].ItemId);
        }

        [Test]
        public void ASourceNothingNamedComesBackNullRatherThanEmpty()
        {
            // Null lets a generator tell "nothing drops here" from "something does" without
            // inspecting a count, and costs no allocation in the common case.
            DimensionDropPlan plan = DimensionDropPlan.Build(
                new List<DimensionItemAsset>
                {
                    ItemDroppingFrom("EmberShard", DimensionDropSourceKind.Creature, "Slime")
                },
                null);

            Assert.IsNull(plan.For(DimensionDropSourceKind.Creature, "Nothing"));
            Assert.IsNull(plan.For(DimensionDropSourceKind.Container, "Slime"), "kind matters too");
        }

        [Test]
        public void ADropNamingASourceTheModNeverMakesIsCaught()
        {
            // Otherwise silent: no creature carries it, nothing errors, and the item never turns up.
            DimensionDropPlan plan = DimensionDropPlan.Build(
                new List<DimensionItemAsset>
                {
                    ItemDroppingFrom("EmberShard", DimensionDropSourceKind.Creature, "Slmie")
                },
                null);

            List<string> unknown = plan.SourcesNothingDefines(
                new HashSet<string> { "Slime", "Scarab" });

            Assert.AreEqual(1, unknown.Count);
            Assert.AreEqual("Slmie", unknown[0], "the typo, named back to the author");
        }

        [Test]
        public void AnEmptyPlanIsSafeAndSaysSo()
        {
            DimensionDropPlan plan = DimensionDropPlan.Build(null, null);
            Assert.IsTrue(plan.IsEmpty);
            Assert.IsNull(plan.For(DimensionDropSourceKind.Creature, "Slime"));
        }

        // ---- the emit half: routing between custom loot and a table ----

        [Test]
        public void AFixedAmountWithNoBiomeFitsInCustomLoot()
        {
            Assert.IsTrue(DimensionDropEmitter.FitsInCustomLoot(
                Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 3, maxAmount: 3),
                "Wood"));
        }

        [Test]
        public void AnAmountRangeCannotBeCustomLootBecauseLootDropHasNoRange()
        {
            // LootDrop carries a single amount; only a loot table LootInfo has a RangeInt. Writing a
            // range as custom loot would flatten it to one number with nothing reporting the loss.
            Assert.IsFalse(DimensionDropEmitter.FitsInCustomLoot(
                Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 2, maxAmount: 5),
                "Wood"));
        }

        [Test]
        public void ABiomeRestrictionCannotBeCustomLootEither()
        {
            // onlyDropsInBiome lives on LootInfo, not LootDrop.
            Assert.IsFalse(DimensionDropEmitter.FitsInCustomLoot(
                Source(DimensionDropSourceKind.Creature, "Scarab", biome: "Desert"),
                "Wood"));
        }

        [Test]
        public void DropsNeedingATableAreHandedBackRatherThanLost()
        {
            GameObject host = new GameObject("dropsource-probe");
            try
            {
                DimensionDropsForSource grouped = Group(
                    Item("Fixed", Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 1, maxAmount: 1)),
                    Item("Ranged", Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 2, maxAmount: 5)))[0];

                System.Collections.Generic.List<DimensionResolvedDrop> leftOver =
                    DimensionDropEmitter.ApplyCustomLoot(
                        host,
                        grouped,
                        delegate(string id) { return ObjectID.Wood; },
                        null);

                Assert.AreEqual(1, leftOver.Count, "the ranged one must come back, not vanish");
                Assert.AreEqual("Ranged", leftOver[0].ItemId);

                DropLootAuthoring loot = host.GetComponent<DropLootAuthoring>();
                Assert.IsTrue(loot.hasCustomLoot);
                Assert.AreEqual(1, loot.customLoot.Values.Count, "only the fixed one was carried");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ---- the quiet mistakes on a single entry ----

        [Test]
        public void ADropThatCanNeverHappenIsRecognised()
        {
            Assert.IsTrue(
                Source(DimensionDropSourceKind.Creature, "Slime", chance: 0f).DropsNothing,
                "never rolls");

            Assert.IsTrue(
                Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 0, maxAmount: 0).DropsNothing,
                "rolls and yields zero");
        }

        [Test]
        public void AMaximumBelowTheMinimumIsCorrectedRatherThanInverted()
        {
            DimensionDropSource source =
                Source(DimensionDropSourceKind.Creature, "Slime", minAmount: 5, maxAmount: 2);

            Assert.AreEqual(5, source.MinAmount);
            Assert.AreEqual(5, source.MaxAmount, "a backwards range would otherwise drop nothing");
        }
    }
}
