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
    /// The sweep that finds every place a name is written, over real authoring assets.
    /// </summary>
    /// <remarks>
    /// These are the cases the sweep exists for, and every one of them fails on a walk built the
    /// obvious way. A walk over <c>NextVisible</c> misses the hidden ore list; a walk over the
    /// dimension's own arrays without following the typed references misses a biome's scene pool;
    /// a match on the raw string misses the unqualified half of every reference. Each is a place a
    /// rename would leave a name behind with nothing said.
    /// </remarks>
    internal sealed class DimensionIdReferencesTests
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

        private static void SetStringArray(Object asset, string path, params string[] values)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, path + " missing on " + asset.GetType().Name);
            Assert.That(property.isArray, Is.True, path + " is not a list.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }

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

        private static bool Names(
            List<DimensionIdReference> found,
            Object asset,
            string propertyPath)
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].Asset == asset &&
                    found[i].PropertyPath == propertyPath)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void BothTheQualifiedAndTheUnqualifiedFormAreFound()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Old");

            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "outputItemId", "MyMod:Old");

            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            SerializedObject lootSerialized = new SerializedObject(loot);
            SerializedProperty entries = lootSerialized.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("itemId").stringValue = "Old";
            lootSerialized.ApplyModifiedPropertiesWithoutUndo();

            template.SetGlobalItems(new[] { item });
            template.SetGlobalRecipes(new[] { recipe });
            template.SetGlobalLootTables(new[] { loot });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, recipe, "outputItemId"),
                Is.True,
                "A reference written as MyMod:Old must be found when renaming Old. Owns() makes " +
                "both forms reach the same asset, so a sweep that only matched the raw string " +
                "would leave every qualified reference behind.");
            Assert.That(
                Names(found, loot, "entries.Array.data[0].itemId"),
                Is.True,
                "The unqualified form must be found too.");

            DimensionIdForm recipeForm = DimensionIdForm.Local;
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].Asset == recipe)
                {
                    recipeForm = found[i].Form;
                }
            }

            Assert.That(
                recipeForm,
                Is.EqualTo(DimensionIdForm.Qualified),
                "Which form each place was written in has to be carried, or a rewrite cannot put " +
                "it back the same way and the project ends up half qualified.");
        }

        [Test]
        public void APointerThreeLevelsDownIsFound()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset weapon = Make<DimensionItemAsset>();
            SetString(weapon, "itemId", "Bow");
            SetStringArray(weapon, "weapon.randomProjectileIds", "Something", "Old");
            template.SetGlobalItems(new[] { weapon });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, weapon, "weapon.randomProjectileIds.Array.data[1]"),
                Is.True,
                "asset to block to list to element. Most pointers in this framework live inside a " +
                "[Serializable] block, and a walk that does not descend into them sees almost " +
                "none of the references there are.");
        }

        [Test]
        public void AHiddenFieldIsFound()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset ore = Make<DimensionItemAsset>();
            SetString(ore, "itemId", "Old");

            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");
            SetStringArray(block, "oreScatterItemIds", "Old");

            template.SetGlobalItems(new[] { ore });
            template.SetTilesets(new[] { block });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, block, "oreScatterItemIds.Array.data[0]"),
                Is.True,
                "oreScatterItemIds is [HideInInspector] and it is what a block scatters as ore. " +
                "NextVisible walks straight past it, so a rename driven by that walk leaves the " +
                "block scattering an item nobody makes any more, with nothing said.");
        }

        [Test]
        public void AProseFieldHoldingTheSameWordsIsNotAReference()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Old");

            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "displayName", "Old");
            SetString(recipe, "notes", "Old");
            SetString(recipe, "outputItemId", "Old");

            template.SetGlobalItems(new[] { item });
            template.SetGlobalRecipes(new[] { recipe });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, recipe, "displayName"),
                Is.False,
                "A name a player reads that happens to be the same word is not a pointer, and " +
                "rewriting it would put a mod's internal id in front of a player.");
            Assert.That(
                Names(found, recipe, "notes"),
                Is.False,
                "Nor is a note the creator wrote to themselves.");
            Assert.That(
                Names(found, recipe, "outputItemId"),
                Is.True,
                "The pointer on the same asset is still found, so the exclusion is by field and " +
                "not by asset.");
        }

        [Test]
        public void AnAssetReachedOnlyThroughAnotherAssetIsSwept()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Clearing");
            SetStringArray(scene, "allowedBiomeIds", "Old");

            BiomeTemplateAsset biome = Make<BiomeTemplateAsset>();
            SetString(biome, "biomeId", "Old");
            biome.SetScenePool(new[] { scene });

            // The scene is NOT in the dimension's own scene list. It hangs off the biome, which is
            // one of the eleven typed references between assets — and a sweep that walked only the
            // dimension's arrays would never open it.
            template.SetBiomes(new[] { biome });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, scene, "allowedBiomeIds.Array.data[0]"),
                Is.True,
                "A scene reached only through a biome's pool must be swept, or renaming that " +
                "biome leaves the scene allowed in a biome that no longer exists.");
        }

        [Test]
        public void AnEmptyFieldIsNeverAHit()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionRecipeAsset recipe = Make<DimensionRecipeAsset>();
            SetString(recipe, "outputItemId", string.Empty);
            template.SetGlobalRecipes(new[] { recipe });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, string.Empty, false);

            Assert.That(
                found.Count,
                Is.EqualTo(0),
                "Most pointer fields legally mean none. A sweep that could not tell empty from " +
                "misspelt would answer every question with a wall of rows.");
        }

        [Test]
        public void TheThingsOwnNameIsInTheListSoACallerCanTellItApart()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Old");
            template.SetGlobalItems(new[] { item });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            Assert.That(
                Names(found, item, "itemId"),
                Is.True,
                "The sweep reports the name itself as well. The rename writes it last and on " +
                "purpose, and the page drops it from the 'what uses this' list — both of which " +
                "need it to be in the answer.");
        }

        [Test]
        public void ASecondNameOnTheSameThingIsMarkedAsAName()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionMobAsset mob = Make<DimensionMobAsset>();
            SetString(mob, "mobId", "Old");
            SetString(mob, "objectId", "Old");
            template.SetGlobalMobs(new[] { mob });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Old", false);

            bool marked = false;
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].Asset == mob && found[i].PropertyPath == "objectId")
                {
                    marked = found[i].IsAName;
                }
            }

            Assert.That(
                marked,
                Is.True,
                "A creature carries two names. Rewriting the second one is a second rename, so " +
                "the row has to say what it is rather than be ticked along with the pointers.");
        }

        // ------------------------------------------------- the slots that hold a thing ---

        [Test]
        public void ALootTableACreatureHoldsIsFound()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionLootTableAsset loot = Make<DimensionLootTableAsset>();
            SetString(loot, "lootTableId", "Drops");

            DimensionMobAsset mob = Make<DimensionMobAsset>();
            SetString(mob, "mobId", "Slime");
            SetAsset(mob, "lootTable", loot);

            template.SetGlobalLootTables(new[] { loot });
            template.SetGlobalMobs(new[] { mob });

            List<DimensionIdReference> held =
                DimensionIdReferences.FindHolders(template, loot, false, "globalLootTables");

            Assert.That(
                Names(held, mob, "lootTable"),
                Is.True,
                "A creature points at its loot table by holding the asset, not by naming it — " +
                "grep lootTableId over the four creature assets returns nothing. A sweep that " +
                "only reads strings cannot see one, so the card said 'Nothing uses this yet' " +
                "about a table three monsters drop, and deleting it left three Missing slots.");
        }

        [Test]
        public void ASceneABiomesPoolHoldsIsFound()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            SceneTemplateAsset scene = Make<SceneTemplateAsset>();
            SetString(scene, "sceneId", "Clearing");

            BiomeTemplateAsset biome = Make<BiomeTemplateAsset>();
            SetString(biome, "biomeId", "Caverns");
            biome.SetScenePool(new[] { scene });

            template.SetBiomes(new[] { biome });
            template.SetGlobalScenes(new[] { scene });

            List<DimensionIdReference> held =
                DimensionIdReferences.FindHolders(template, scene, false, "globalScenes");

            Assert.That(
                Names(held, biome, "scenePool.Array.data[0]"),
                Is.True,
                "A slot inside a list points at it as much as a scalar one does, and a delete " +
                "that leaves it behind leaves a null in the pool that nothing validates for.");
        }

        [Test]
        public void TheListAThingLivesInIsNotAPlaceThatPointsAtIt()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionItemAsset item = Make<DimensionItemAsset>();
            SetString(item, "itemId", "Blade");
            template.SetGlobalItems(new[] { item });

            List<DimensionIdReference> held =
                DimensionIdReferences.FindHolders(template, item, false, "globalItems");

            Assert.That(
                held.Count,
                Is.EqualTo(0),
                "The dimension's own list is where the thing LIVES. Every delete takes it out of " +
                "there whichever answer is given, so counting it would put one unhelpful row at " +
                "the top of every answer and mean 'Nothing points at it' could never be said.");
        }

        [Test]
        public void EverySlotThatHoldsAnAuthoredAssetIsCounted()
        {
            List<string> slots = new List<string>();
            List<string> authored = new List<string>();
            System.Type[] types = typeof(DimensionTemplateAsset).Assembly.GetTypes();
            for (int t = 0; t < types.Length; t++)
            {
                System.Reflection.FieldInfo[] fields = types[t].GetFields(
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                for (int f = 0; f < fields.Length; f++)
                {
                    System.Type held = fields[f].FieldType.IsArray
                        ? fields[f].FieldType.GetElementType()
                        : fields[f].FieldType;
                    if (held == null || !typeof(ScriptableObject).IsAssignableFrom(held))
                    {
                        continue;
                    }

                    if (!fields[f].IsPublic &&
                        fields[f].GetCustomAttributes(typeof(SerializeField), true).Length == 0)
                    {
                        continue;
                    }

                    slots.Add(types[t].Name + "." + fields[f].Name);

                    // The same question DimensionIdReferences.IsAuthoredAsset asks: is the thing
                    // in this slot one of ours, or one of the game's?
                    string space = held.Namespace;
                    if (space != null &&
                        space.StartsWith("ExpandNullforge", System.StringComparison.Ordinal))
                    {
                        authored.Add(types[t].Name + "." + fields[f].Name);
                    }
                }
            }

            // MEASURED, and the numbers are the point of the test. 44 serialized slots in this
            // assembly hold a ScriptableObject. 5 of them hold one of the GAME's — a PugMapTileset,
            // a walk pattern, two content bundles, a gradient map — and nothing here renames those.
            // Of the 39 that hold an authored asset, 27 are on DimensionTemplateAsset: the 25
            // lists a thing lives in, the layout, and the two portal profiles. That leaves 12 that
            // are asset to asset — four creature loot tables, a biome's scene pool and generation
            // passes, a dungeon's room group and its room fillings, a named area's blocks, a
            // workbench's recipes, a portal package's profile, and the manifest's own dimension —
            // and eleven of those point at something with a name a creator can rename.
            //
            // NONE of the 44 is one of the 247 pointer STRINGS the plan counted. The two sets do
            // not overlap at all, which is exactly why a sweep built on string values saw none of
            // them and told a creator "Nothing uses this yet".
            int onTheDimension = 0;
            for (int i = 0; i < authored.Count; i++)
            {
                if (authored[i].StartsWith(
                        "DimensionTemplateAsset.", System.StringComparison.Ordinal))
                {
                    onTheDimension++;
                }
            }

            Assert.That(
                slots.Count,
                Is.EqualTo(44),
                "A slot added to this assembly and not counted here is a slot the sweep has never " +
                "been checked against. Found: " + string.Join(", ", slots.ToArray()));
            Assert.That(
                authored.Count,
                Is.EqualTo(39),
                "and 39 of them hold something this framework authors. Found: " +
                string.Join(", ", authored.ToArray()));
            Assert.That(
                authored.Count - onTheDimension,
                Is.EqualTo(12),
                "and twelve of those are asset to asset rather than the list a thing lives in.");
        }

        [Test]
        public void ABlocksOwnNameIsVisibleToTheSweep()
        {
            DimensionTemplateAsset template = Make<DimensionTemplateAsset>();
            DimensionTilesetAsset block = Make<DimensionTilesetAsset>();
            SetString(block, "blockName", "Ashen Rock");
            template.SetTilesets(new[] { block });

            List<DimensionIdReference> found =
                DimensionIdReferences.Find(template, "Ashen Rock", false);

            Assert.That(
                Names(found, block, "blockName"),
                Is.True,
                "blockName was on the prose list, and a field on that list is invisible in BOTH " +
                "directions — so the re-sweep that checks a rename finished could never see a " +
                "block's own name, and a half-written block rename would have read as a clean one.");
        }
    }
}
#endif
