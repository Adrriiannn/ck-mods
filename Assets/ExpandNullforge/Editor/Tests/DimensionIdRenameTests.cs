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
    internal sealed class DimensionIdRenameTests
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

        private T Make<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        private static void SetString(Object asset, string path, string value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object asset, string path, bool value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAsset(Object asset, string path, Object value)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAssetArray(Object asset, string path, params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ReadString(Object asset, string path)
        {
            SerializedProperty property = new SerializedObject(asset).FindProperty(path);
            return property == null ? null : property.stringValue;
        }

        private static DimensionIdentityField NameOn<T>(Object asset, string property)
        {
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(asset);
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].Property == property)
                {
                    return names[i];
                }
            }

            Assert.Fail("No renameable name '" + property + "' on " + typeof(T).Name);
            return null;
        }

        /// <summary>An item called Old, a recipe that makes it, and a loot table that drops it.</summary>
        private DimensionTemplateAsset BuildPack(
            out DimensionItemAsset item,
            out DimensionRecipeAsset recipe,
            out DimensionLootTableAsset loot)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Old");
            SetString(item, "displayName", "Ember Blade");

            recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "MakeOld");
            SetString(recipe, "outputItemId", "MyMod:Old");

            loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "DropsOld");
            SerializedObject lootSerialized = new SerializedObject(loot);
            SerializedProperty entries = lootSerialized.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "Old";
            lootSerialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalItems(new[] { item });
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalLootTables(new[] { loot });
            return template;
        }

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

        // -------------------------------------------------- the scene's two identities ---

        [Test]
        public void AScenesOwnIdIsRewrittenInWhatPointsAtIt()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");

            DimensionBossAsset boss = Make<DimensionBossAsset>();
            SetString(boss, "bossId", "Ashling");
            SetString(boss, "arenaSceneId", "Arena");

            template.SetGlobalScenes(new[] { scene });
            template.SetGlobalBosses(new[] { boss });

            DimensionIdentityField sceneId = NameOn<SceneTemplateAsset>(scene, "sceneId");
            Assert.That(
                sceneId.InboundNamesUseIt,
                Is.True,
                "Other authored content finds a scene by sceneId — that is what FindSceneById " +
                "matches on — so renaming it has to rewrite what points at it.");

            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, sceneId, "Hollow", false);
            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(ReadString(boss, "arenaSceneId"), Is.EqualTo("Hollow"));
            Assert.That(ReadString(scene, "sceneId"), Is.EqualTo("Hollow"));
            Assert.That(
                ReadString(scene, "templateId"),
                Is.EqualTo("arena-template"),
                "The scene's other name is a different identity and is not dragged along.");
        }

        [Test]
        public void AScenesNameToTheGameIsARenameOfItsOwn()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");
            template.SetGlobalScenes(new[] { scene });

            DimensionIdentityField templateId = NameOn<SceneTemplateAsset>(scene, "templateId");
            Assert.That(
                templateId.InboundNamesUseIt,
                Is.False,
                "templateId is the key the running service files the compiled scene under — " +
                "NullforgeDimensionService looks scenes up by it in a dictionary. Nothing a " +
                "creator authors points at it, so renaming it rewrites nothing and changes what " +
                "the game is handed.");

            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, templateId, "hollow-template", false);
            Assert.That(plan.CanGo, Is.True, string.Join(" / ", plan.Blockers));

            string report;
            Assert.That(DimensionIdRename.Apply(plan, plan.Edges, out report), Is.True, report);
            Assert.That(ReadString(scene, "templateId"), Is.EqualTo("hollow-template"));
            Assert.That(
                ReadString(scene, "sceneId"),
                Is.EqualTo("Arena"),
                "And the id other content points at is untouched.");
        }

        [Test]
        public void AScenesTwoNamesMayNotBecomeOneString()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Arena");
            SetString(scene, "templateId", "arena-template");
            template.SetGlobalScenes(new[] { scene });

            DimensionIdentityField sceneId = NameOn<SceneTemplateAsset>(scene, "sceneId");
            DimensionIdRenamePlan plan =
                DimensionIdRename.Plan(template, scene, sceneId, "arena-template", false);

            Assert.That(
                plan.CanGo,
                Is.False,
                "Two names on one asset becoming the same string makes it impossible to tell " +
                "afterwards which of them anything meant.");
        }

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

        // ------------------------------------------------- one word, two kinds of thing ---

        /// <summary>
        /// A pack where an item and a loot table are both called Ember, a recipe outputs the item,
        /// and a scene rolls on the table. Legal: they are different namespaces and nothing
        /// collides.
        /// </summary>
        private DimensionTemplateAsset BuildPackWithOneWordTwice(
            out DimensionItemAsset item,
            out DimensionLootTableAsset loot,
            out DimensionRecipeAsset recipe,
            out SceneTemplateAsset scene)
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();

            item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Ember");
            SetString(item, "displayName", "Ember");

            loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Ember");

            recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "recipeId", "EmberRecipe");
            SetString(recipe, "outputItemId", "Ember");

            scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Vault");
            SerializedObject sceneSerialized = new SerializedObject(scene);
            SerializedProperty objects = sceneSerialized.FindProperty("sceneObjects");
            objects.arraySize = 1;
            objects.GetArrayElementAtIndex(0)
                .FindPropertyRelative("lootTableId").stringValue = "Ember";
            sceneSerialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalItems(new[] { item });
            template.SetGlobalLootTables(new[] { loot });
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalScenes(new[] { scene });
            return template;
        }

        private static bool Ticked(
            DimensionIdRenamePlan plan,
            Object asset,
            string propertyPath,
            out string because)
        {
            because = null;
            for (int i = 0; i < plan.Edges.Count; i++)
            {
                if (plan.Edges[i].Asset == asset && plan.Edges[i].PropertyPath == propertyPath)
                {
                    string why;
                    bool ticked = plan.StartsTicked(plan.Edges[i], out why);
                    because = why;
                    return ticked;
                }
            }

            Assert.Fail("No row for " + propertyPath + " on " + asset.name);
            return false;
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

        [Test]
        public void AScenesSecondNameDoesNotAlsoOfferADelete()
        {
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            List<DimensionIdentityField> names = DimensionIdentityCatalog.Of(scene);

            int deletable = 0;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i].InboundNamesUseIt && names[i].ContainerProperty.Length > 0 &&
                    names[i].DeleteIsOfferedHere)
                {
                    deletable++;
                }
            }

            Assert.That(
                names.Count,
                Is.EqualTo(2),
                "A scene carries two names, and the card draws a section for each.");
            Assert.That(
                deletable,
                Is.EqualTo(1),
                "But only one of them is the name other content points at. The second planned " +
                "its delete against templateId — a string nothing points at — so it printed " +
                "'Nothing points at it.' and deleted a scene that boss arenas and biome pools do.");
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
