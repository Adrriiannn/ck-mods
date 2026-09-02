using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{

    /// <summary>
    /// Turns validated <see cref="DimensionItemAsset"/> definitions into consumer-owned Core
    /// Keeper prefabs. The archetype decides which authoring components are attached, so the
    /// generated object matches what the dashboard promised the creator it would build.
    ///
    /// Field names are applied through <see cref="TrySetProperty"/>: fields proven by the working
    /// portal bootstrap are set directly, and anything the installed SDK does not expose under the
    /// expected name is reported as a warning instead of silently doing nothing.
    /// </summary>
    internal static partial class DimensionItemGenerator
    {
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder)
        {
            return Generate(items, outputFolder, null, null);
        }

        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes)
        {
            return Generate(items, outputFolder, recipes, null);
        }

        /// <summary>
        /// Generates items and, where a recipe produces one of them, writes that recipe's
        /// ingredients and craft time onto the item. Core Keeper keeps a recipe's ingredient list
        /// on the produced item rather than on the crafting station, so this is where a recipe
        /// becomes real. When <paramref name="tilesets"/> is supplied, any generated item that is a
        /// tileset's ground or wall block additionally receives its tile-behaviour components so it
        /// places, digs/mines, and drops as a real custom block.
        /// </summary>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            return Generate(items, outputFolder, recipes, tilesets, null);
        }

        /// <summary>
        /// As above, and also writes the localization rows biomes need for their title cards.
        /// </summary>
        /// <remarks>
        /// Biomes ride along here rather than getting their own writer because the mod has ONE
        /// localization table, and two writers merging into one file would each drop the other's rows.
        /// Nothing about a biome is generated as a prefab — only its title text is written.
        /// </remarks>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets,
            IEnumerable<BiomeTemplateAsset> biomes)
        {
            return Generate(items, outputFolder, recipes, tilesets, biomes, null);
        }

        /// <summary>
        /// As above, and also rides the bosses along: their name localization rows and the
        /// summoning stamp on their idol items.
        /// </summary>
        /// <remarks>
        /// Bosses ride the item generate for the same reason biomes do — the mod has ONE
        /// localization table with one writer, and a second writer would drop the first's rows.
        /// The summoning stamp has to happen here too, because the idol is an ITEM: only this
        /// generator holds its prefab open.
        ///
        /// <paramref name="localization"/> carries the names of everything the OTHER generators
        /// build — stations, chests, decorations, seeds, creatures, and the lines of the mod's own
        /// stat effects. They ride here for the same one-writer reason: a second pass merging the
        /// same file would treat these rows as somebody else's leftovers and never clean them up.
        /// </remarks>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets,
            IEnumerable<BiomeTemplateAsset> biomes,
            IEnumerable<DimensionBossAsset> bosses,
            IEnumerable<DimensionNamedAreaAsset> namedAreas = null,
            DimensionLocalizationPlan localization = null,
            IEnumerable<string> otherOwnedObjectIds = null,
            IEnumerable<DimensionExplosionAsset> blasts = null,
            IEnumerable<string> switchedOffObjectIds = null,
            IEnumerable<string> objectNamesThisRunWrites = null)
        {
            DimensionItemGenerationReport report = new DimensionItemGenerationReport();
            if (items == null)
            {
                report.Errors.Add("No items were supplied.");
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder) || !outputFolder.StartsWith("Assets"))
            {
                report.Errors.Add(
                    "The output folder must be a project-relative path under Assets; got '" +
                    (outputFolder ?? "<null>") + "'.");
                return report;
            }

            if (!DimensionAssetFolders.EnsureExists(outputFolder))
            {
                report.Errors.Add("Could not create the output folder '" + outputFolder + "'.");
                return report;
            }

            Dictionary<string, TilesetBlockBinding> blockIndex = IndexTilesetBlocks(tilesets);
            List<TilesetOreBinding> oreBindings = ResolveTilesetOres(tilesets, report);
            Dictionary<string, DimensionTilesetAsset> oreIndex = IndexCustomOres(oreBindings);
            Dictionary<string, DimensionRecipeAsset> recipesByOutput = IndexRecipes(recipes, report);

            // Every generated object name is qualified with the owning mod, because Core Keeper keys
            // object properties by name and a collision between two mods makes a world that loads
            // once and then never again. The set of our own ids is collected first so that a
            // reference to a VANILLA item (a recipe asking for IronBar) is left alone — see
            // DimensionNamingContext.QualifyReference.
            List<string> ownItemIds = new List<string>();
            List<DimensionItemAsset> itemList = new List<DimensionItemAsset>();
            foreach (DimensionItemAsset candidate in items)
            {
                itemList.Add(candidate);
                if (candidate != null && !string.IsNullOrEmpty(candidate.ItemId))
                {
                    ownItemIds.Add(candidate.ItemId);
                }
            }

            // A cooked dish takes its two palettes from the ingredients it names, so the mod's own
            // ingredients are indexed once here rather than searched per dish.
            Dictionary<string, DimensionCookingTemplate> ingredientsById =
                new Dictionary<string, DimensionCookingTemplate>();
            for (int i = 0; i < itemList.Count; i++)
            {
                DimensionItemAsset candidate = itemList[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ItemId))
                {
                    continue;
                }

                DimensionCookingTemplate cooking = candidate.Cooking;
                if (cooking.IsAnIngredient && !ingredientsById.ContainsKey(candidate.ItemId))
                {
                    ingredientsById[candidate.ItemId] = cooking;
                }
            }

            CheckIngredientsCanFitInADish(itemList, ingredientsById.Count, report);

            // The mod's OTHER objects count as ours too. An item that fires the mod's own projectile,
            // or leaves the mod's own chest, names something no item asset defines — and with only
            // item ids in the ownership set the reference read as "the game does not have that" and
            // the component carrying it was stripped.
            if (otherOwnedObjectIds != null)
            {
                foreach (string otherId in otherOwnedObjectIds)
                {
                    if (!string.IsNullOrEmpty(otherId))
                    {
                        ownItemIds.Add(otherId);
                    }
                }
            }

            DimensionNamingContext naming = new DimensionNamingContext(
                ResolveModName(outputFolder), ownItemIds, switchedOffObjectIds);
            binder = new DimensionObjectBinder(naming);
            ownBlastIds = new HashSet<string>(StringComparer.Ordinal);
            if (blasts != null)
            {
                foreach (DimensionExplosionAsset blast in blasts)
                {
                    if (blast != null && blast.Enabled && !string.IsNullOrEmpty(blast.ExplosionId))
                    {
                        ownBlastIds.Add(blast.ExplosionId);
                    }
                }
            }

            if (!naming.CanQualify)
            {
                report.Warnings.Add(
                    "The owning mod could not be resolved from '" + outputFolder +
                    "', so generated object names are NOT namespaced. Installing this mod alongside " +
                    "another that uses the same item id will break world loading. Check that the " +
                    "output folder sits under a mod with a ModBuilderSettings asset.");
            }

            List<string> generatedIds = new List<string>();
            List<DimensionLocalizationCsv.Row> localizationRows =
                new List<DimensionLocalizationCsv.Row>();
            List<string> retiredLocalizationKeys = new List<string>();

            // Which items summon which bosses, visible to Configure while the batch runs.
            summoningBossesByItemId = BuildSummoningMap(bosses, itemList, naming, report);

            // Folders have to exist BEFORE the batch opens: AssetDatabase.CreateFolder inside a
            // StartAssetEditing block does not take effect until the batch closes, so an asset
            // written into a folder made in the same batch is written nowhere.
            DimensionEquipmentSkinGenerator.EnsureSkinFolder(
                outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionItemAsset item in itemList)
                {
                    if (GenerateOne(item, outputFolder, recipesByOutput, blockIndex, oreIndex, naming, report, ingredientsById) && item != null)
                    {
                        // The recorded id and the localization key must both use the qualified
                        // name, or the display name will not resolve for the object that now
                        // carries it.
                        string qualified = naming.QualifyGenerated(item.ItemId);
                        generatedIds.Add(qualified);

                        // A bomb's blast is a second object with its own id. It is recorded here
                        // alongside the item so the orphan sweep treats the pair as one thing:
                        // deleting the bomb takes its blast with it, and keeping the bomb keeps it.
                        string blastId = DimensionExplosiveBlast.Write(
                            item,
                            outputFolder,
                            naming,
                            message => report.Warnings.Add(message),
                            message => report.Errors.Add(message),
                            path => report.Created.Add(path),
                            path => report.Updated.Add(path));
                        if (!string.IsNullOrEmpty(blastId))
                        {
                            generatedIds.Add(naming.QualifyGenerated(blastId));
                        }
                        DimensionLocalizationCsv.AddItemRows(
                            localizationRows,
                            qualified,
                            item.DisplayName,
                            item.Description,
                            retiredLocalizationKeys);

                        // An ingredient also needs the two words a dish's name is built from.
                        // Without them every dish cooked with it reads with a raw object id where
                        // the ingredient's name belongs — and nothing says so until somebody cooks.
                        if (item.Cooking.IsAnIngredient)
                        {
                            DimensionLocalizationCsv.AddFoodIngredientNameRows(
                                localizationRows, qualified, item.DisplayName);
                        }
                    }
                }

                // Vanilla-ore veins are standalone objects rather than a stamp on one of our item
                // prefabs, so they are written after the items — nothing above depends on them, and
                // they contribute no localization (the ore already has vanilla's name).
                for (int i = 0; i < oreBindings.Count; i++)
                {
                    if (!oreBindings[i].IsCustomItem)
                    {
                        DimensionTilesetOreAuthoring.CreateVanillaVeinPrefab(
                            outputFolder,
                            oreBindings[i].Tileset,
                            oreBindings[i].OreItemId,
                            report);
                    }
                }

                AppendFarmingInfrastructure(outputFolder, tilesets, report);
                AppendBiomeTitleLocalization(localizationRows, naming, biomes);
                AppendBossNameLocalization(localizationRows, naming, bosses);
                AppendNamedAreaTitleLocalization(localizationRows, naming, namedAreas);

                if (localization != null)
                {
                    localizationRows.AddRange(localization.Rows);
                    retiredLocalizationKeys.AddRange(localization.RetiredKeys);
                    report.Warnings.AddRange(localization.Warnings);
                }
            }
            finally
            {
                summoningBossesByItemId = null;
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string modRoot = ResolveModRoot(outputFolder);

            // Before the manifest is overwritten with this run's ids, use its previous contents to
            // find prefabs we generated last time and no longer want. PugMod ships a mod by scanning
            // folders, so an orphan left here still registers an object in game.
            PruneOrphanedItemPrefabs(modRoot, outputFolder, generatedIds, report);

            // The whole set of NAMES this pack's prefabs are stamped with, qualified the same way
            // the prefabs are, which is exactly the subject list the world-load check needs and
            // could not have. Recorded beside the item ids rather than instead of them: the item
            // list is the promise the runtime holds the game to, and this one is only a list of
            // what to look at.
            //
            // IT IS NOT ownItemIds WHEN THE CALLER KNOWS BETTER. ownItemIds is the reference
            // vocabulary — the ids a creator TYPES — and three kinds of object are stamped with
            // something else: a plant becomes "<id>Plant" and "<id>Seed", a boss also builds
            // "<id>-summon-circle" and "<id>-map-marker". Fed the typed ids, the ledger declared a
            // plant id nothing answers to and never contained the summoning circle the companion
            // table was written for. DimensionGeneratedObjectIds.ObjectsMade answers the other
            // question; the fallback below keeps a caller that does not pass one behaving as before.
            List<string> ownedObjectIds = new List<string>();
            HashSet<string> seenOwnedObjectIds = new HashSet<string>(StringComparer.Ordinal);
            List<string> namesToQualify = new List<string>();
            if (objectNamesThisRunWrites != null)
            {
                foreach (string written in objectNamesThisRunWrites)
                {
                    if (!string.IsNullOrEmpty(written))
                    {
                        namesToQualify.Add(written);
                    }
                }
            }

            if (namesToQualify.Count == 0)
            {
                namesToQualify.AddRange(ownItemIds);
            }

            for (int i = 0; i < namesToQualify.Count; i++)
            {
                string qualifiedOwned = naming.QualifyGenerated(namesToQualify[i]);
                if (!string.IsNullOrEmpty(qualifiedOwned) && seenOwnedObjectIds.Add(qualifiedOwned))
                {
                    ownedObjectIds.Add(qualifiedOwned);
                }
            }

            for (int i = 0; i < generatedIds.Count; i++)
            {
                if (seenOwnedObjectIds.Add(generatedIds[i]))
                {
                    ownedObjectIds.Add(generatedIds[i]);
                }
            }

            RecordGeneratedItemIds(modRoot, generatedIds, ownedObjectIds, report);
            WriteLocalization(modRoot, localizationRows, retiredLocalizationKeys, report);

            // Two rows under one key is one name silently winning over another — the table has no
            // notion of ownership, so the loser simply never appears. It can only happen when two
            // authored things resolve to the same object name, which the game would also refuse to
            // load, so naming the key here is the earliest anyone hears about it.
            for (int i = 0; i < localizationRows.Count; i++)
            {
                if (!report.LocalizationKeys.Add(localizationRows[i].Key) &&
                    !string.IsNullOrEmpty(localizationRows[i].Key))
                {
                    report.Warnings.Add(
                        "Two things were named under '" + localizationRows[i].Key +
                        "'. Only one of the names can show. Give them different ids.");
                }
            }

            return report;
        }

        /// <returns>True when a prefab was written for this item.</returns>
        private static bool GenerateOne(
            DimensionItemAsset item,
            string outputFolder,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            Dictionary<string, TilesetBlockBinding> blockIndex,
            Dictionary<string, DimensionTilesetAsset> oreIndex,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report,
            Dictionary<string, DimensionCookingTemplate> ingredientsById)
        {
            if (item == null)
            {
                return false;
            }

            if (!item.Enabled)
            {
                report.Skipped.Add(
                    Describe(item) + " is disabled and was not generated.");
                return false;
            }

            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            bool blocked = false;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity == DimensionItemArchetypeValidator.Severity.Error)
                {
                    report.Errors.Add(Describe(item) + ": " + findings[i].Message);
                    blocked = true;
                }
            }

            if (blocked)
            {
                report.Skipped.Add(Describe(item) + " was skipped until its errors are fixed.");
                return false;
            }

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(item.ItemId, "Item") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(item.ItemId);

            bool written = false;
            try
            {
                Configure(
                    root,
                    item,
                    recipesByOutput,
                    blockIndex,
                    oreIndex,
                    naming,
                    report,
                    outputFolder,
                    ingredientsById);

                // A body, but only for an item that is actually put down. Core Keeper draws this
                // line sharply: a bomb, a barrel and a placed prop carry a collider, and a sword,
                // a helmet and a lump of ore carry none at all — a loose item is picked up through
                // its own pickup pass and is never cast against. So the answer is read off the
                // placement the author already gave: no placement, no body.
                //
                // For the ones that DO get placed it is not decoration. Being breakable is written
                // here, and every one of those answers is a lookup inside the loop that walks what
                // a swing hit; an item with no collider is not in the collision world and is never
                // in that loop, so a bomb could not be struck and could not be caught in another
                // bomb's chain.
                if (root.GetComponent<PlaceableObjectAuthoring>() != null)
                {
                    DimensionQueryCompanions.GiveItTheBodyItsFootprintAsksFor(
                        root,
                        Describe(item),
                        delegate(string message) { report.Warnings.Add(message); });
                }

                // And the sweep, last, the way every other generator ends. It was not wired in
                // here, which meant an item carrying an answer whose system needs something beside
                // it — a trap that attacks, a summoning circle, a bomb in a chain — never had that
                // filled in and was never told about it either.
                DimensionQueryCompanions.CloseTheGaps(
                    root,
                    Describe(item),
                    delegate(string message) { report.Warnings.Add(message); });

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                written = true;
                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    Describe(item) + " failed to generate: " + exception.Message);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            return written;
        }

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        /// <remarks>
        /// Set once at the top of <c>Generate</c> and read by every reference below. It replaced a
        /// bare enum parse that could only answer "the game has it" or "None" — and every caller
        /// treated the second as a mistake, which stripped the authoring component off items whose
        /// only crime was pointing at another of the mod's own objects.
        /// </remarks>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        /// <summary>
        /// Resolves an item name at generation time to one of the GAME's own numbers.
        /// </summary>
        /// <remarks>
        /// Vanilla names go through the enum, because the runtime lookup is an empty dictionary
        /// outside a running game and quietly returns None for every one of them. A name that is
        /// one of this mod's own comes back as None here on purpose — see
        /// <see cref="DimensionObjectBinder.IsDeferred"/>, which is what the callers ask next.
        /// </remarks>
        private static ObjectID ResolveObjectByName(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        /// <summary>True when the name is one of this mod's own and the runtime will fill it in.</summary>
        private static bool IsDeferred(string itemId)
        {
            return binder.IsDeferred(itemId);
        }

        /// <summary>The full name the game registers one of this mod's own objects under.</summary>
        private static string QualifyReference(string itemId)
        {
            return binder.Qualify(itemId);
        }

        /// <summary>The blasts this mod makes, by their authored ids.</summary>
        /// <remarks>
        /// Kept apart from the general ownership set because a bomb's explosion field is narrower
        /// than "one of ours": <c>ExplosiveAuthoring.explosionID</c> has to name an explosion, and
        /// the bootstrap only registers the pair for a blast this mod actually builds. Suppressing
        /// the warning for every one of the mod's ids would let a bomb point at, say, its own sword
        /// and say nothing at all.
        /// </remarks>
        private static HashSet<string> ownBlastIds = new HashSet<string>(StringComparer.Ordinal);

        private static bool NamesOneOfOurBlasts(string objectId)
        {
            return !string.IsNullOrEmpty(objectId) &&
                DimensionObjectBinder.Vanilla(objectId) == ObjectID.None &&
                ownBlastIds.Contains(DimensionObjectNamespace.LocalIdOf(objectId));
        }

        private static void Configure(
            GameObject root,
            DimensionItemAsset item,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            Dictionary<string, TilesetBlockBinding> blockIndex,
            Dictionary<string, DimensionTilesetAsset> oreIndex,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report,
            string outputFolder,
            Dictionary<string, DimensionCookingTemplate> ingredientsById)
        {
            DimensionItemArchetype archetype = item.Archetype;
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(archetype);

            root.name = item.ItemId;

            // What the game thinks this thing is. Resolved once, before anything else: the type
            // decides the slot the item lands in, and the durability pass at the end of this method
            // reads it back off the object to work out what the game's own formula makes of it.
            DimensionWhatItIs kind = ResolveWhatItIs(item, report);
            ReportKindProblems(item, kind, required, report);

            // Whether this one ends up with a durability pool, worked out here because two separate
            // passes need the same answer: the stacking question below, and the durability pass at
            // the end. The only pool the archetype asks for and does not get is a throwing weapon's
            // with nothing typed, which sheds it the way all seven of the game's own do.
            bool keepsADurabilityPool = item.KeepsADurabilityPool(kind);
            bool stacks = ResolveStacking(item, kind, keepsADurabilityPool, report);

            ConfigureObject(root, item, kind, naming, report);
            ConfigureLocalization(root, item, naming, report);

            // Portal items (V2 instant portals) are framework-defined: rare rarity so the item
            // reads as the special tool it is. Applied on every generate so existing consumer
            // items pick it up without manual prefab edits.
            if (item.Kind == DimensionItemKind.PortalItem)
            {
                ObjectAuthoring portalItemObject = root.GetComponent<ObjectAuthoring>();
                if (portalItemObject != null)
                {
                    portalItemObject.rarity = Rarity.Rare;
                }
            }

            recipesByOutput.TryGetValue(item.ItemId, out DimensionRecipeAsset recipe);
            ApplyComponent<InventoryItemAuthoring>(
                root, required, DimensionItemAuthoringComponents.InventoryItem,
                component => ConfigureInventory(component, item, stacks, recipe, naming, report));

            if (recipe != null &&
                !Requires(required, DimensionItemAuthoringComponents.InventoryItem))
            {
                report.Warnings.Add(
                    Describe(item) + ": recipe '" + recipe.RecipeId +
                    "' produces it, but a " + DimensionItemArchetypeRules.Describe(item.Archetype) +
                    " has no inventory representation, so the ingredients cannot be attached.");
            }

            ApplyComponent<PlaceableObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Placement,
                component => ConfigurePlaceable(component, item, report));

            // Durability is finished at the end of this method, not here.
            //
            // The framework used to say a written durability "reads back as 1" and warn that the
            // author's number had been thrown away. It had not been: DurabilityAuthoring recomputes
            // durability on prefab import from the object's OWN type and initialAmount, and both of
            // those were wrong — the type was the enum default, which matches no case in the game's
            // formula, and initialAmount was a hardcoded 1, which the formula then returns
            // unchanged. That is the whole of the 1. With the type now written, the same formula
            // gives the game's own number, so the multipliers go on here and
            // ApplyDurabilityTheGameWay runs the formula once the cooldown is in place (a melee
            // weapon's durability is divided by it).
            ApplyComponent<DurabilityAuthoring>(
                root, required, DimensionItemAuthoringComponents.Durability,
                component =>
                {
                    // The multipliers are the half that survives the import. maxDurability is
                    // recomputed from the item type times durabilityMultiplier, which is why a raw
                    // number never stuck and this one does.
                    component.durabilityMultiplier = item.DurabilityMultiplier;
                    component.repairMultiplier = item.RepairMultiplier;
                    component.reinforceCostMultiplier = item.ReinforceCostMultiplier;

                    // The author's own number is NOT written here. It goes into initialAmount in
                    // ApplyDurabilityTheGameWay, because that is the only field it survives in:
                    // DurabilityAuthoring.OnValidate recomputes durability and maxDurability from
                    // the object's type and initialAmount every time the prefab is imported, so a
                    // number written straight into either of them lasts until the next import and
                    // no longer.
                });

            ApplyComponent<WeaponDamageAuthoring>(
                root, required, DimensionItemAuthoringComponents.WeaponDamage,
                component =>
                {
                    component.damage = item.DamageAmount;
                    component.damageMultiplier = item.DamageMultiplierForItsTier;
                    component.isMagic = item.DamageIsMagic;
                    component.isRange = item.DamageIsRanged;
                });

            // Resolved once, and the one number every later pass uses. It has to be worked out
            // before the component is attached because the effects pass further down owns the same
            // component and would otherwise settle it a second time — which is exactly what used
            // to happen: the effects block carried a cooldown of its own, ran last, and destroyed
            // the component whenever that second field was blank. Whatever a creator typed on the
            // item was deleted before the prefab was written, so it set neither the swing rate nor
            // the durability divisor and every melee weapon came out at the game's default.
            float cooldownSeconds = ResolveCooldownSeconds(item, kind, required, report);

            ApplyComponent<CooldownAuthoring>(
                root, cooldownSeconds > 0f || item.Effects.SharesACooldown,
                component =>
                {
                    // The number goes into CooldownCD verbatim, and the slots that read it —
                    // MeleeWeaponSlot, RangeWeaponSlot, SummoningWeaponSlot, BeamWeaponSlot,
                    // EatableSlot — start at the game's own default and then OVERWRITE it with the
                    // item's number whenever the component is there. A component left holding zero
                    // is therefore not "nothing set", it is a weapon that swings with no delay at
                    // all, which is why zero is never what is written.
                    if (cooldownSeconds > 0f)
                    {
                        component.cooldown = cooldownSeconds;
                    }

                    component.casualCharacterIgnoresCustomCooldown = item.CasualIgnoresItsCooldown;
                });

            // Health backs destructible props, creatures, and bombs. A bomb needs it for a reason
            // the other two do not share: every way of setting an explosive off runs through its
            // health reaching zero, so a bomb without a health pool can never be broken by hand and
            // can never be caught in another bomb's chain reaction.
            // The archetype OR the answer: a barrel that happens to explode is a Placeable, and it
            // still needs the health pool every trigger runs through.
            bool isExplosive =
                Requires(required, DimensionItemAuthoringComponents.Explosive) ||
                item.Explosive.Explodes;
            // Asked of the item rather than recomputed here, because the bootstrap emitter has to
            // reach the same answer to decide whether a shed-loot row can ever land.
            bool needsHealth = item.GetsAHealthPool;
            ApplyComponent<HealthAuthoring>(
                root, needsHealth,
                component => component.maxHealth =
                    isExplosive ? item.Explosive.HowToughItIs : item.HealthPoints);

            ApplyComponent<DamageableObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);
            ApplyComponent<DestructibleObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);

            // lootTableID is a game enum, not free text: an id the game does not define would
            // otherwise silently resolve to whatever sits at index 0, so it is matched by name
            // and reported when it does not exist.
            DimensionObjectSpine.ApplyWorldRoles(
                root,
                item.WorldRoles,
                ResolveObjectByName,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                },
                IsDeferred,
                QualifyReference);

            // AFTER the world roles, and that order is load-bearing. Both passes write the
            // summoning-by-name component: the world-roles pass owns the list the item itself
            // names, writing it whole so a removed boss goes, and this one unions in the bosses
            // that named this item as their offering. The other way round, whichever ran second
            // would erase the first.
            ConfigureSummoning(root, item, report);

            DimensionObjectSpine.ApplyInstrument(
                root,
                item.Instrument,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplyExtraLoot(
                root,
                item.ExtraLoot,
                ResolveObjectByName,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                },
                IsDeferred);

            ApplyComponent<DropLootAuthoring>(
                root, required, DimensionItemAuthoringComponents.Loot,
                component =>
                {
                    // The shared resolver, not a bare enum parse: a vanilla table by its game
                    // name OR one of this mod's own tables by its id — this was the one loot
                    // field that refused the mod's tables while its siblings accepted them.
                    LootTableID resolved;
                    if (!string.IsNullOrEmpty(item.LootTableId) &&
                        DimensionEditorLootTables.TryResolve(item.LootTableId, out resolved))
                    {
                        DropLootAuthoring loot = (DropLootAuthoring)component;
                        loot.lootTableID = resolved;
                    }
                    else
                    {
                        TrySetEnumProperty(
                            component, "lootTableID", item.LootTableId, item, report);
                    }
                });

            // The three things an item does BEYOND being an item: what it grants you, what
            // right-clicking does, and what it looks like on your character. All three were
            // attached as bare components with nothing written into them, so a custom set of
            // armour equipped, granted nothing, did nothing on right-click, and left the character
            // on screen bare.
            DimensionObjectSpine.ApplyItemEffects(
                root,
                item.Effects,
                cooldownSeconds,
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        Describe(item) + ": '" + unknown + "' is not a condition the game has.");
                },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                Requires(required, DimensionItemAuthoringComponents.EquipmentConditions));

            DimensionObjectSpine.ApplyAttackSounds(
                root,
                item.AttackSounds,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                item.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplyBasics(
                root,
                item.Basics,
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });
            DimensionObjectSpine.ApplyInitialConditions(
                root,
                item.Conditions,
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });

            DimensionObjectSpine.ApplyOffHandAndCost(
                root,
                item.OffHand,
                item.PolishesInto,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplyItemKinds(
                root,
                item.IsAPotion,
                item.ScansForObjectId,
                item.SummonsInsteadOfScanning,
                item.ScannerOnlyInBiome,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplyExplosive(
                root,
                item.Explosive,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                NamesOneOfOurBlasts);

            // The DIRECTLY ASSIGNED icon, not the one an icon id might find. Reading colours out
            // of a sprite located by a fuzzy name search would sample whatever asset happened to
            // match, and a palette taken from the wrong picture is a mistake nobody would think to
            // look for. An item with only an icon id keeps the four colours typed on it.
            DimensionObjectSpine.ApplyCooking(
                root,
                item.Cooking,
                item.IconSprite,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string ingredientId)
                {
                    DimensionCookingTemplate found;
                    return ingredientsById != null &&
                        ingredientsById.TryGetValue(ingredientId, out found)
                            ? found
                            : null;
                },
                delegate(string referencedId) { return naming.Owns(referencedId); },
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        Describe(item) + ": '" + unknown + "' is not a condition the game has.");
                },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });

            DimensionObjectSpine.ApplyWeapon(
                root,
                item.Weapon,
                delegate(string projectileId) { return ResolveObjectByName(projectileId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + ": " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplySecondaryUse(
                root,
                item.SecondaryUse,
                delegate(string itemId) { return ResolveObjectByName(itemId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + ": " + message); },
                IsDeferred);

            ScriptableDataBlock skin = DimensionEquipmentSkinGenerator.EnsureSkin(
                item.EquipmentSkin,
                item.ItemId,
                outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName,
                delegate(string message) { report.Warnings.Add(message); });
            DimensionObjectSpine.ApplyEquipmentSkin(root, skin);
            if (skin == null)
            {
                DimensionEquipmentSkinGenerator.RemoveSkinIfPresent(
                    item.ItemId,
                    outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName);
            }

            if (Requires(required, DimensionItemAuthoringComponents.BossEncounter))
            {
                report.Warnings.Add(
                    Describe(item) +
                    ": boss encounter wiring (phases, arena, music) has no framework path yet; " +
                    "the prefab is generated without it.");
            }

            // A block item that belongs to a tileset becomes a real custom tile: stamp the tileset
            // identity and attach the ground/wall dig/mine/spawn/crack behaviour on top of the
            // placeable object the Block archetype already produced.
            if (blockIndex != null &&
                blockIndex.TryGetValue(item.ItemId, out TilesetBlockBinding binding))
            {
                DimensionTilesetBlockAuthoring.Apply(root, binding.Tileset, binding.IsWall, report);
            }

            // An item a tileset lists as its ore becomes the vein itself — same object, same
            // ObjectID, exactly as CopperOreEntity IS ObjectID.CopperOre. Applied after the block
            // stamp so a block item mistakenly also listed as an ore keeps its block identity and
            // the conflict shows up as the duplicate TileAuthoring it is, rather than silently
            // turning a wall into a vein.
            if (oreIndex != null &&
                oreIndex.TryGetValue(item.ItemId, out DimensionTilesetAsset oreTileset))
            {
                DimensionTilesetOreAuthoring.Apply(root, oreTileset, report);
            }
            else
            {
                // Taking an ore off a block's list has to actually stop it being a vein. Prefabs are
                // updated in place, so without this the stamp from an earlier run would survive and
                // the item would keep generating in walls no tileset claims it for.
                DimensionTilesetOreAuthoring.ClearIfOre(root);
            }

            // Last, because it reads the finished object. The cooking pass above can still change
            // the type (food is decided there and nowhere else), the block and ore stamps above can
            // still change it, and the durability formula divides a melee weapon's number by the
            // cooldown component this method attached earlier.
            ApplyDurabilityTheGameWay(root, item, report);
        }
    }
}
