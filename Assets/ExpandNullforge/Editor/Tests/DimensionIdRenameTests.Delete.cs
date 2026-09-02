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
    /// Deleting a thing, and what is left pointing at it.
    /// </summary>
    internal sealed partial class DimensionIdRenameTests
    {
        // -------------------------------------------------------------------- delete ---

        [Test]
        public void DeleteListsWhatPointsAtItAndCanEmptyThoseFields()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdDeletePlan plan =
                DimensionIdDelete.Plan(template, item, field, false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));
            Assert.That(
                plan.InboundCount,
                Is.EqualTo(2),
                "The recipe that makes it and the loot table that drops it both point at it, and " +
                "both would be left naming nothing.");

            List<DimensionIdReference> inbound = new List<DimensionIdReference>();
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].Asset != item)
                {
                    inbound.Add(plan.Edges[i]);
                }
            }

            string report;
            Assert.That(DimensionIdDelete.Apply(plan, true, inbound, out report), Is.True, report);

            Assert.That(ReadString(recipe, "outputItemId"), Is.Empty);
            Assert.That(ReadString(loot, "entries.Array.data[0].itemId"), Is.Empty);
            Assert.That(
                DimensionIdReferences.Find(template, "Old", false).Count,
                Is.EqualTo(0),
                "Told to clear them, nothing anywhere is left saying the name.");
            Assert.That(
                template.GlobalItems.Length,
                Is.EqualTo(0),
                "And it comes out of the list it was in, rather than leaving a null nothing " +
                "validates for.");
        }

        [Test]
        public void DeleteCanLeaveThoseFieldsAloneAndSaysHowMany()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdDeletePlan plan = DimensionIdDelete.Plan(template, item, field, false);

            string report;
            Assert.That(DimensionIdDelete.Apply(plan, false, null, out report), Is.True, report);

            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("MyMod:Old"),
                "Leaving them is the right answer when something else is about to take the name, " +
                "so it has to be one of the answers.");
            Assert.That(
                report.Contains("2"),
                Is.True,
                "And how many were left is the fact that decides it, so it is said out loud: " +
                report);
        }

        [Test]
        public void ABlocksOwnItemsCannotBeDeletedOnTheirOwn()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");

            DimensionItemAsset wall = Make<DimensionItemAsset>();
            SetString(wall, "itemId", block.WallBlockItemId);
            SetString(wall, "displayName", "Ashen Rock Block");

            template.SetTilesets(new[] { block });
            template.SetGlobalItems(new[] { wall });

            DimensionIdentityField field = NameOn<DimensionItemAsset>(wall, "itemId");
            DimensionIdDeletePlan plan = DimensionIdDelete.Plan(template, wall, field, false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "A block's wall and ground items belong to the block. Deleting one on its own " +
                "leaves the block naming an item that is gone; the block's own delete takes them " +
                "with it.");
        }

        // ---------------------------------------------------- what holds it, not names it ---

        [Test]
        public void ASlotThatHoldsItIsListedAndNeverRewritten()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Drops");

            DimensionMobAsset mob = Make<DimensionMobAsset>();
            SetString(mob, "mobId", "Slime");
            SetAsset(mob, "lootTable", loot);

            template.SetGlobalLootTables(new[] { loot });
            template.SetGlobalMobs(new[] { mob });

            DimensionIdentityField field =
                NameOn<DimensionLootTableAsset>(loot, "lootTableId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, loot, field, "BetterDrops", false);

            bool listed = false;
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].Asset == mob && plan.Edges[i].PropertyPath == "lootTable")
                {
                    listed = true;
                    Assert.That(
                        plan.Edges[i].Hold,
                        Is.EqualTo(DimensionIdHold.TheThingItself),
                        "It holds the table rather than naming it.");
                    string why;
                    Assert.That(
                        plan.StartsTicked(plan.Edges[i], out why),
                        Is.False,
                        "There is no name in it to rewrite, so it cannot be ticked.");
                }
            }

            Assert.That(
                listed,
                Is.True,
                "It has to be in the answer to 'what points at this' even though a rename does " +
                "nothing to it — the creature goes on dropping this table whatever it is called.");

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(
                mob.LootTable,
                Is.EqualTo(loot),
                "and it still holds the same table afterwards.");
        }

        [Test]
        public void DeleteTakesItOutOfEverySlotThatHeldIt()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Clearing");

            BiomeTemplateAsset biome = Make<BiomeTemplateAsset>();
            SetString(biome, "biomeId", "Caverns");
            biome.SetScenePool(new[] { scene });

            template.SetBiomes(new[] { biome });
            template.SetGlobalScenes(new[] { scene });

            DimensionIdentityField field = NameOn<SceneTemplateAsset>(scene, "sceneId");
            DimensionIdDeletePlan plan = DimensionIdDelete.Plan(template, scene, field, false);

            Assert.That(
                plan.InboundCount,
                Is.EqualTo(1),
                "The biome's pool holds it, and the old sweep counted zero — so the dialogue said " +
                "'Nothing points at it.' and deleted a scene four dungeon rooms could be using.");

            string report;
            Assert.That(
                DimensionIdDelete.Apply(plan, true, plan.Edges, out report), Is.True, report);
            Assert.That(
                biome.ScenePool.Length,
                Is.EqualTo(0),
                "Emptied means the slot goes, not that it is set to null: a null in a scene pool " +
                "is the same invisible hole a Project-view delete leaves, and nothing checks " +
                "for one.");
        }

        [Test]
        public void ABlocksDeleteIsLeftToTheBlockPagesOwn()
        {
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(block);

            Assert.That(names.Count, Is.EqualTo(1));
            Assert.That(
                names[0].DeleteIsOfferedHere,
                Is.False,
                "A block owns two generated item assets and its own page has taken them with it " +
                "since long before this card. Two delete buttons on one page with different " +
                "consequences is worse than either.");
        }
    }
}
#endif
