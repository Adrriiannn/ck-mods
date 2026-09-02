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
    /// Renaming and deleting an authored thing, and every refusal that stands between a creator and
    /// a project that looks fine and is broken.
    /// </summary>
    /// <remarks>
    /// Each of these fails on the tree as it was before this fixture existed, because before it
    /// there was no rename at all: an id was a bound text box, and changing it left every name
    /// pointing at it as it was. The refusals matter more than the rewrite — the rewrite is
    /// mechanical, and what a creator cannot see is the rename that succeeds and silently means
    /// something else afterwards.
    /// </remarks>
    internal sealed partial class DimensionIdRenameTests
    {

        // ------------------------------------------------------------------- rewrite ---

        [Test]
        public void EachPlaceIsRewrittenInTheFormItWasWrittenIn()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "New", false);

            Assert.That(
                plan.CanGo,
                Is.True,
                "Nothing should refuse this: " + string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(
                DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);

            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("MyMod:New"),
                "A reference written with its mod qualifier keeps it. Dropping it would leave " +
                "the recipe naming the game's object if one happens to share the name.");
            Assert.That(
                ReadString(loot, "entries.Array.data[0].itemId"),
                Is.EqualTo("New"),
                "A reference written without one stays without one. Adding one would point the " +
                "row at a name that only exists once qualification runs, which is not the same " +
                "thing at all.");
            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("New"),
                "And the name itself lands.");
        }

        [Test]
        public void ARenameLeftHalfDoneIsFinishedByRunningItAgain()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            // What an interrupted rename leaves: one place already rewritten, the name itself
            // still old, because the name is always written last.
            SetString(recipe, "outputItemId", "MyMod:New");
            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("Old"),
                "The half-done state under test is the one where the name is still the old one.");

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan again =
                DimensionIdRename.Plan(template, item, field, "New", false);
            Assert.That(again.CanGo, Is.True, string.Join(" / ", again.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(again, again.Edges, out report), Is.True, report);

            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("MyMod:New"),
                "The place that was already done stays done — each write is idempotent, which is " +
                "the whole reason the name is written last.");
            Assert.That(ReadString(loot, "entries.Array.data[0].itemId"), Is.EqualTo("New"));
            Assert.That(ReadString(item, "itemId"), Is.EqualTo("New"));
            Assert.That(
                DimensionIdRename.WhatStillSaysTheOldName(template, "Old", false).Count,
                Is.EqualTo(0),
                "And nothing anywhere still says the old name.");
        }

        [Test]
        public void OnlyTheTickedPlacesAreRewritten()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "New", false);

            List<DimensionIdReference> onlyTheRecipe = new List<DimensionIdReference>();
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].Asset == recipe)
                {
                    onlyTheRecipe.Add(plan.Edges[i]);
                }
            }

            string report;
            Assert.That(DimensionIdRename.Apply(plan, onlyTheRecipe, out report), Is.True, report);

            Assert.That(ReadString(recipe, "outputItemId"), Is.EqualTo("MyMod:New"));
            Assert.That(
                ReadString(loot, "entries.Array.data[0].itemId"),
                Is.EqualTo("Old"),
                "The sweep matches on the value, so it over-reports where two things share a " +
                "string. The list is a list to read and tick, and an unticked row must stay put.");
            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("New"),
                "The name itself is written whether or not it was ticked; it is the thing being " +
                "renamed.");
        }

        [Test]
        public void UndoPutsEveryTouchedAssetBackInOneStep()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "New", false);

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(ReadString(item, "itemId"), Is.EqualTo("New"));

            Undo.PerformUndo();

            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("Old"),
                "One rename is one thing a creator did, so it has to be one thing to undo.");
            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("MyMod:Old"),
                "Every asset the rename wrote is recorded into the same group, so a single undo " +
                "puts them all back. Undoing a rename asset by asset would leave the project in " +
                "states that never existed.");
            Assert.That(ReadString(loot, "entries.Array.data[0].itemId"), Is.EqualTo("Old"));
        }

        // ------------------------------------------------------------------ refusals ---

        [Test]
        public void RenamingOntoOneOfTheGamesOwnNamesIsRefused()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "IronBar", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "IronBar is one of the game's own objects. Owns() asks vanilla FIRST and answers " +
                "false, so every recipe, drop, shot and trader row naming it would quietly start " +
                "meaning the game's iron bar while the mod still shipped an object nothing can " +
                "reach — and nothing in the editor would say a word.");

            string report;
            Assert.That(
                DimensionIdRename.Apply(plan, plan.Edges, out report),
                Is.False,
                "A refused plan must not write anything.");
            Assert.That(ReadString(item, "itemId"), Is.EqualTo("Old"));
            Assert.That(ReadString(recipe, "outputItemId"), Is.EqualTo("MyMod:Old"));
        }

        [Test]
        public void RenamingOntoTheNameOfSomethingSwitchedOffIsRefused()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            DimensionItemAsset live = Make<DimensionItemAsset>();
            SetString(live, "itemId", "Old");
            SetBool(live, "enabled", true);

            DimensionItemAsset off = Make<DimensionItemAsset>();
            SetString(off, "itemId", "Taken");
            SetBool(off, "enabled", false);

            template.SetGlobalItems(new[] { live, off });

            DimensionIdentityField field = NameOn<DimensionItemAsset>(live, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, live, field, "Taken", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "A switched-off thing still owns its name. The collision would not break anything " +
                "today; it would break the day the creator ticks it back on, and by then nothing " +
                "connects the two events.");
        }

        [Test]
        public void TwoBlockNamesDifferingOnlyInPunctuationCollide()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            DimensionTilesetAsset first = Make<DimensionTilesetAsset>();
            SetString(first, "blockName", "Ashen Rock");

            DimensionTilesetAsset second = Make<DimensionTilesetAsset>();
            SetString(second, "blockName", "Eerie Stone");

            template.SetTilesets(new[] { first, second });

            DimensionIdentityField field = NameOn<DimensionTilesetAsset>(first, "blockName");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, first, field, "Eerie-Stone", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "A block's identity strips spacing and punctuation, so \"Eerie-Stone\" and " +
                "\"Eerie Stone\" are the same block as far as a saved world is concerned. A plain " +
                "string comparison would wave this through and the two would hash to one id.");
        }

        [Test]
        public void RenamingABlockSaysWhatItCostsInWorldsAlreadyPlayed()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");
            template.SetTilesets(new[] { block });

            DimensionIdentityField field = NameOn<DimensionTilesetAsset>(block, "blockName");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, block, field, "Cinder Rock", false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));
            Assert.That(
                plan.Warnings.Count,
                Is.GreaterThan(0),
                "A block's number is a hash of its name and a saved world writes that number into " +
                "every tile. Renaming it without saying so orphans ground people have already " +
                "built on, and nothing else in the editor mentions it.");
        }

        [Test]
        public void KeepingTheIdentityPinsItBeforeTheNameLands()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");
            template.SetTilesets(new[] { block });

            string wasKnownAs = block.IdentityToken;
            Assert.That(wasKnownAs, Is.Not.Empty, "A block starts with an identity derived from its name.");

            DimensionIdentityField field = NameOn<DimensionTilesetAsset>(block, "blockName");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, block, field, "Cinder Rock", false);

            DimensionIdRename.PinIdentityFirst(plan);
            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);

            Assert.That(ReadString(block, "blockName"), Is.EqualTo("Cinder Rock"));
            Assert.That(
                block.IdentityToken,
                Is.EqualTo(wasKnownAs),
                "The pin has to be written BEFORE the name, because the token it stores is " +
                "derived from the name it is replacing. Written after, it would pin the new " +
                "identity and orphan exactly the tiles it was meant to save.");
        }

        [Test]
        public void RenamingSomethingAPublishedLayoutRecordsIsRefused()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            BiomeTemplateAsset biome = Make<BiomeTemplateAsset>();
            SetString(biome, "biomeId", "Old");

            DimensionLayoutTemplateAsset layout = Make<DimensionLayoutTemplateAsset>();
            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty published = serialized.FindProperty("publishedVersions");
            Assert.That(published, Is.Not.Null, "publishedVersions missing on the layout.");
            published.arraySize = 1;
            SerializedProperty regions =
                published.GetArrayElementAtIndex(0).FindPropertyRelative("regions");
            regions.arraySize = 1;
            regions.GetArrayElementAtIndex(0).FindPropertyRelative("biomeId").stringValue = "Old";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetBiomes(new[] { biome });
            template.SetLayoutTemplate(layout);

            DimensionIdentityField field = NameOn<BiomeTemplateAsset>(biome, "biomeId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, biome, field, "New", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "A published layout is a record of what WAS published, kept so a world made under " +
                "it still generates. Rewriting it makes the project consistent and the record a " +
                "lie; leaving it makes the record honest and the name dangling. Nobody has ruled " +
                "on which, so the tool declines rather than picking one.");
        }

        [Test]
        public void ARowThatCouldMeanTheOtherThingDoesNotStartTicked()
        {
            DimensionItemAsset item;
            DimensionLootTableAsset loot;
            DimensionRecipeAsset recipe;
            SceneTemplateAsset scene;
            DimensionTemplateAsset template =
                BuildPackWithOneWordTwice(out item, out loot, out recipe, out scene);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "EmberBolt", false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string because;
            Assert.That(
                Ticked(plan, scene, "sceneObjects.Array.data[0].lootTableId", out because),
                Is.False,
                "The scene rolls on the LOOT TABLE called Ember, not on the item. Ticked by " +
                "default, pressing Rename breaks the scene's loot and orphans nothing anybody " +
                "would look for — a rename in one namespace silently rewriting another.");
            Assert.That(
                because,
                Does.Contain("loot table"),
                "and the reason has to be in the row, not only in the code: " + because);
        }

        [Test]
        public void ARowWhoseFieldReadsAsThisKindStillStartsTicked()
        {
            DimensionItemAsset item;
            DimensionLootTableAsset loot;
            DimensionRecipeAsset recipe;
            SceneTemplateAsset scene;
            DimensionTemplateAsset template =
                BuildPackWithOneWordTwice(out item, out loot, out recipe, out scene);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "EmberBolt", false);

            string because;
            Assert.That(
                Ticked(plan, recipe, "outputItemId", out because),
                Is.True,
                "Unticking everything the moment two things share a word would trade silent " +
                "corruption for a feature nobody can use. The recipe's output is an ITEM by the " +
                "name of the field, and an item is what is being renamed.");
        }

        [Test]
        public void RenamingTheLootTableLeavesTheRecipesOutputAlone()
        {
            DimensionItemAsset item;
            DimensionLootTableAsset loot;
            DimensionRecipeAsset recipe;
            SceneTemplateAsset scene;
            DimensionTemplateAsset template =
                BuildPackWithOneWordTwice(out item, out loot, out recipe, out scene);

            DimensionIdentityField field =
                NameOn<DimensionLootTableAsset>(loot, "lootTableId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, loot, field, "EmberDrops", false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string because;
            Assert.That(
                Ticked(plan, recipe, "outputItemId", out because),
                Is.False,
                "The other way round, and worse: rewriting it makes the recipe output an item " +
                "nothing defines and orphans the real Ember item, with no blocker and no warning.");
            Assert.That(
                Ticked(plan, scene, "sceneObjects.Array.data[0].lootTableId", out because),
                Is.True,
                "while the scene's roll, which reads as a loot table's name, is still ticked.");

            List<DimensionIdReference> chosen = new List<DimensionIdReference>();
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                string why;
                if (plan.StartsTicked(plan.Edges[i], out why))
                {
                    chosen.Add(plan.Edges[i]);
                }
            }

            string report;
            Assert.That(DimensionIdRename.Apply(plan, chosen, out report), Is.True, report);
            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("Ember"),
                "and the recipe still makes the item it always made.");
            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("Ember"),
                "and the item is still called what it was called.");
        }

        [Test]
        public void WithNothingElseSharingTheWordEveryPointerStartsTicked()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "New", false);

            Assert.That(
                plan.TheNameIsSharedInThisPack,
                Is.False,
                "Nothing else in this pack is called Old.");

            string because;
            Assert.That(
                Ticked(plan, recipe, "outputItemId", out because),
                Is.True,
                "so the ordinary case behaves exactly as it always did.");
            Assert.That(
                Ticked(plan, loot, "entries.Array.data[0].itemId", out because),
                Is.True,
                "including the rows whose field name says nothing about what kind they mean.");
        }

        // ---------------------------------------------------------- refusals and forms ---

        [Test]
        public void RenamingOntoAGameObjectNameIsRefusedInTheQualifiedFormToo()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "MyMod:CopperShovel", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "Ids are created qualified by default, so 'MyMod:CopperShovel' is the form a " +
                "creator actually types — and it walked straight past a refusal that only ever " +
                "looked at the bare string. Owns() keys on the local half and refuses anything " +
                "vanilla owns, so both forms break in exactly the same way.");
        }

        [Test]
        public void ALootTableMayBeCalledAfterOneOfTheGamesObjects()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Drops");
            template.SetGlobalLootTables(new[] { loot });

            DimensionIdentityField field =
                NameOn<DimensionLootTableAsset>(loot, "lootTableId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, loot, field, "IronBar", false);

            Assert.That(
                plan.CanGo,
                Is.True,
                "Nothing ever resolves a loot table's id against the game's object table, so the " +
                "redirect the refusal describes cannot happen to one. The enum holds Wood, Slime, " +
                "Bomb and Lantern, and a block delivered with a reason that is not true of it is " +
                "still a block: " + string.Join(" / ", plan.Blockers));
        }

        [Test]
        public void AQualifierTypedIntoTheDraftDoesNotSpreadToPlainFields()
        {
            DimensionItemAsset item;
            DimensionRecipeAsset recipe;
            DimensionLootTableAsset loot;
            DimensionTemplateAsset template = BuildPack(out item, out recipe, out loot);

            DimensionIdentityField field = NameOn<DimensionItemAsset>(item, "itemId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, item, field, "MyMod:Sword", false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));
            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);

            Assert.That(
                ReadString(loot, "entries.Array.data[0].itemId"),
                Is.EqualTo("Sword"),
                "A field written without a qualifier stays without one. It used to take the new " +
                "name exactly as typed, so a field that had never carried a qualifier grew one.");
            Assert.That(
                ReadString(item, "itemId"),
                Is.EqualTo("MyMod:Sword"),
                "while the name itself is what the creator typed.");
        }

        // -------------------------------------------------- the ids built out of an id ---

        [Test]
        public void APlantsSeedAndGrownFormAreRewrittenWithIt()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionPlantAsset plant = Make<DimensionPlantAsset>();
            SetString(plant, "plantId", "Ember");

            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "MakeSeed");
            SetString(recipe, "outputItemId", "EmberSeed");

            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Drops");
            SerializedObject lootSerialized = new SerializedObject(loot);
            SerializedProperty entries = lootSerialized.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue =
                "EmberPlant";
            lootSerialized.ApplyModifiedPropertiesWithoutUndo();

            SetAssetArray(template, "globalPlants", plant);
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalLootTables(new[] { loot });

            DimensionIdentityField field = NameOn<DimensionPlantAsset>(plant, "plantId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, plant, field, "Ashling", false);

            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);

            Assert.That(
                ReadString(recipe, "outputItemId"),
                Is.EqualTo("AshlingSeed"),
                "Nothing is ever built under the bare plant id — the seed and the grown plant are " +
                "the objects, and those are the names a creator types. A sweep that looked only " +
                "for 'Ember' reported 'nothing else pointed at it' and left the recipe making a " +
                "seed nobody builds.");
            Assert.That(
                ReadString(loot, "entries.Array.data[0].itemId"),
                Is.EqualTo("AshlingPlant"),
                "and the same for the grown form.");
        }

        [Test]
        public void StartingFreshLetsTheOldIdentityGo()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ember Rock");
            SetString(block, "identityToken", "AshenRock");
            template.SetTilesets(new[] { block });

            DimensionIdentityField field = NameOn<DimensionTilesetAsset>(block, "blockName");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, block, field, "Cinder Rock", false);

            string report;
            Assert.That(
                DimensionIdRename.Apply(
                    plan, plan.Edges, DimensionRenameIdentityAnswer.StartFresh, out report),
                Is.True,
                report);

            Assert.That(
                block.IdentityToken,
                Is.EqualTo("CinderRock"),
                "'Start fresh' promises a genuinely new block. It only skipped the pin write, " +
                "which does nothing at all to a block an earlier rename had already pinned — the " +
                "frozen token wins over the name, so the creator got the same block, same tileset " +
                "id and same block items, under a third label.");
        }

        [Test]
        public void KeepingTheIdentityThroughApplyIsOneUndoStep()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");
            template.SetTilesets(new[] { block });

            string wasKnownAs = block.IdentityToken;
            DimensionIdentityField field = NameOn<DimensionTilesetAsset>(block, "blockName");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, block, field, "Cinder Rock", false);

            string report;
            Assert.That(
                DimensionIdRename.Apply(
                    plan, plan.Edges, DimensionRenameIdentityAnswer.KeepTheIdentity, out report),
                Is.True,
                report);

            Assert.That(block.IdentityToken, Is.EqualTo(wasKnownAs));

            Undo.PerformUndo();
            Assert.That(
                ReadString(block, "identityToken"),
                Is.Empty,
                "The pin was written before Undo's group was even opened, so the one thing the " +
                "card promises about a rename — one Ctrl+Z puts every touched asset back — was " +
                "untrue of the identity token.");
            Assert.That(
                ReadString(block, "blockName"),
                Is.EqualTo("Ashen Rock"),
                "and the name comes back in the same step.");
        }

        // -------------------------------------------------- what the report may claim ---

        [Test]
        public void ARowLeftUntickedIsNotReportedAsARenameThatFailed()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionMobAsset mob = Make<DimensionMobAsset>();
            SetString(mob, "mobId", "Slime");
            SetString(mob, "objectId", "Slime");
            template.SetGlobalMobs(new[] { mob });

            DimensionIdentityField field = NameOn<DimensionMobAsset>(mob, "mobId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, mob, field, "Blobbin", false);

            // Exactly what the card sends: everything that starts ticked, which is not the
            // second identity.
            List<DimensionIdReference> chosen = new List<DimensionIdReference>();
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                string why;
                if (!plan.IsTheIdentityRow(plan.Edges[i]) &&
                    plan.StartsTicked(plan.Edges[i], out why))
                {
                    chosen.Add(plan.Edges[i]);
                }
            }

            string report;
            Assert.That(DimensionIdRename.Apply(plan, chosen, out report), Is.True, report);

            Assert.That(
                DimensionIdRename.WhatStillSaysTheOldName(template, "Slime", false).Count,
                Is.EqualTo(1),
                "The creature's objectId still says Slime, on purpose — it is a second name and " +
                "rewriting it is a second rename.");
            Assert.That(
                DimensionIdRename.WhatWasAskedForAndDidNotHappen(template, "Slime", chosen, false)
                    .Count,
                Is.EqualTo(0),
                "So the report must not say the rename failed and tell the creator to run it " +
                "again. Running it again cannot finish it: the identity already holds the new " +
                "name, so there is no old to new left to run.");
        }
    }
}
#endif
