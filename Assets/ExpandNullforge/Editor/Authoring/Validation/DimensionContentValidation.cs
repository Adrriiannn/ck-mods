using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The content half of Review and Build: every check that asks whether the THINGS a
    /// creator authored are real, resolvable and drawable, folded into the same issue stream
    /// the compiler feeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before this file, Review's five lights watched only the world's shape — identity,
    /// bounds, biomes, layout. Items, recipes, loot, creatures and artwork each had a
    /// validator, and every one of them was green, tested, and unreachable: a dimension full
    /// of danglings reported "Ready to ship". This is the single place all of them now feed,
    /// and <see cref="DimensionAuthoringPreviewBuilder"/> is the single caller — one seam
    /// moves the lights, the Diagnostics page and its jump chips together.
    /// </para>
    /// <para>
    /// The severity rule, stated once: an ERROR means the built mod would contain a lie (a
    /// reference that silently binds to nothing), a WARNING means it would contain a no-op
    /// (a thing that quietly does nothing), INFO is disclosure. Findings on disabled assets
    /// are always Info — a deliberate choice must never wedge the lights red.
    /// </para>
    /// </remarks>
    public static class DimensionContentValidationUtility
    {
        /// <summary>
        /// Checks only the editor can run (AssetDatabase, prefab files). Installed by the
        /// editor assembly on load; null in the game's sandbox, where those checks have no
        /// meaning and their APIs do not exist.
        /// </summary>
        public static System.Func<DimensionTemplateAsset, List<DimensionAuthoringIssue>>
            EditorChecks;

        private static long changeStamp;
        private static Object cachedTemplate;
        private static long cachedStamp = -1;
        private static IReadOnlyList<DimensionAuthoringIssue> cachedIssues;

        /// <summary>
        /// Invalidates the memo. The editor bumps this when anything changes; within one
        /// workspace rebuild the preview is built several times and every call after the
        /// first must be free, or Check would visibly lag on a large pack.
        /// </summary>
        public static void BumpChangeStamp()
        {
            changeStamp++;
        }

        public static IReadOnlyList<DimensionAuthoringIssue> Validate(
            DimensionTemplateAsset template,
            DimensionCompiledGenerationPlan compiledPlan)
        {
            if (template == null)
            {
                return System.Array.Empty<DimensionAuthoringIssue>();
            }

            if (ReferenceEquals(template, cachedTemplate) && cachedStamp == changeStamp &&
                cachedIssues != null)
            {
                return cachedIssues;
            }

            List<DimensionAuthoringIssue> issues = new List<DimensionAuthoringIssue>();
            DimensionContentIdUniverse universe = DimensionContentIdUniverse.Build(template);
            AddIdCollisionIssues(template, universe, issues);
            AddItemArchetypeIssues(template, issues);
            AddReferenceResolutionIssues(template, compiledPlan, universe, issues);
            AddVisualAssetIssues(template, issues);
            AddCapabilityMaturityIssues(template, issues);
            AddBossArenaIssues(template, issues);
            AddCustomWaterIssues(template, issues);
            if (EditorChecks != null)
            {
                List<DimensionAuthoringIssue> editorIssues = EditorChecks(template);
                if (editorIssues != null)
                {
                    issues.AddRange(editorIssues);
                }
            }

            cachedTemplate = template;
            cachedStamp = changeStamp;
            cachedIssues = issues;
            return issues;
        }

        // ------------------------------------------------------------------ items ---

        private static void AddItemArchetypeIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (!item.Enabled)
                {
                    // One line of disclosure, never a blocker: the generator skips disabled
                    // items by design, so their other findings are moot.
                    issues.Add(new DimensionAuthoringIssue(
                        DimensionAuthoringSeverity.Info,
                        "item-disabled",
                        "'" + item.DisplayName + "' is turned off and will not be generated.",
                        "Item",
                        item.ItemId,
                        false,
                        default));
                    continue;
                }

                List<DimensionItemArchetypeValidator.Finding> findings =
                    DimensionItemArchetypeValidator.Validate(item);
                for (int f = 0; f < findings.Count; f++)
                {
                    DimensionItemArchetypeValidator.Finding finding = findings[f];
                    if (finding.Severity == DimensionItemArchetypeValidator.Severity.Ok)
                    {
                        continue;
                    }

                    issues.Add(new DimensionAuthoringIssue(
                        finding.Severity == DimensionItemArchetypeValidator.Severity.Error
                            ? DimensionAuthoringSeverity.Error
                            : DimensionAuthoringSeverity.Warning,
                        "item-archetype-" +
                        (string.IsNullOrEmpty(finding.Field) ? "invalid" : finding.Field),
                        finding.Message,
                        "Item",
                        item.ItemId,
                        false,
                        default));
                }
            }
        }

        // ------------------------------------------------------------- references ---

        private static void AddReferenceResolutionIssues(
            DimensionTemplateAsset template,
            DimensionCompiledGenerationPlan plan,
            DimensionContentIdUniverse universe,
            List<DimensionAuthoringIssue> issues)
        {
            HashSet<string> referencedLootTables = new HashSet<string>(System.StringComparer.Ordinal);

            DimensionRecipeAsset[] recipes = template.GlobalRecipes;
            int handCraftedCount = 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null || !recipe.Enabled)
                {
                    continue;
                }

                if (!universe.ResolvesAsItem(recipe.OutputItemId))
                {
                    issues.Add(Error(
                        "recipe-output-unresolved",
                        "Recipe '" + recipe.DisplayName + "' makes '" + recipe.OutputItemId +
                        "', which nothing defines. The recipe would craft nothing.",
                        "Recipe",
                        recipe.RecipeId));
                }

                DimensionRecipeIngredientTemplate[] ingredients = recipe.Ingredients;
                bool namesAnyIngredient = false;
                for (int j = 0; j < ingredients.Length; j++)
                {
                    if (ingredients[j] == null || string.IsNullOrEmpty(ingredients[j].ItemId))
                    {
                        continue;
                    }

                    namesAnyIngredient = true;
                    if (!universe.ResolvesAsItem(ingredients[j].ItemId))
                    {
                        issues.Add(Error(
                            "recipe-ingredient-unresolved",
                            "Recipe '" + recipe.DisplayName + "' asks for '" +
                            ingredients[j].ItemId + "', which nothing defines.",
                            "Recipe",
                            recipe.RecipeId));
                    }
                }

                // Core Keeper keeps what a craft costs on the produced item, not on the recipe, so
                // ingredients can only be given to an item this mod makes. A recipe that both makes
                // one of the game's items and lists ingredients would ship at the game's price
                // while the dashboard showed the author's — the recipe is refused rather than
                // registered at a cost nobody wrote.
                if (!string.IsNullOrEmpty(recipe.OutputItemId) &&
                    namesAnyIngredient &&
                    !universe.IsOneOfYourOwn(recipe.OutputItemId))
                {
                    issues.Add(Error(
                        "recipe-vanilla-output-has-ingredients",
                        "Recipe '" + recipe.DisplayName + "' makes '" + recipe.OutputItemId +
                        "', one of the game's own items, and also lists ingredients. What a craft " +
                        "costs is stored on the item itself, so it cannot be changed for the " +
                        "game's items. Clear the ingredients to offer it at the game's own price, " +
                        "or make your own item and put the ingredients on that.",
                        "Recipe",
                        recipe.RecipeId));
                }

                string station = recipe.CraftingStationId;
                if (string.IsNullOrEmpty(station))
                {
                    handCraftedCount++;
                    if (handCraftedCount > DimensionRecipeAsset.HandCraftingRecipeLimit)
                    {
                        issues.Add(Warning(
                            "recipe-hand-crafting-full",
                            "Recipe '" + recipe.DisplayName + "' is the " +
                            handCraftedCount.ToString() + "th made by hand. The by-hand list has " +
                            "room for " + DimensionRecipeAsset.HandCraftingRecipeLimit.ToString() + " on top of the " +
                            "game's own six and cannot be split into pages the way a Workbench " +
                            "can, so this one will not show up. Name a Workbench for it.",
                            "Recipe",
                            recipe.RecipeId));
                    }
                }
                else if (!universe.IsVanillaObject(station))
                {
                    DimensionWorkbenchAsset workbench = DimensionWorkbenchAsset.FindByStationId(
                        template.GlobalWorkbenches,
                        station);
                    if (workbench == null)
                    {
                        issues.Add(Error(
                            "recipe-station-unresolved",
                            "Recipe '" + recipe.DisplayName + "' is made at '" + station +
                            "', which is neither one of the game's stations nor one of yours. " +
                            "Leave the Workbench empty to have it made by hand.",
                            "Recipe",
                            recipe.RecipeId));
                    }
                    else if (!workbench.GeneratesItsOwnObject)
                    {
                        issues.Add(Error(
                            "recipe-station-has-no-object",
                            "Recipe '" + recipe.DisplayName + "' is made at your Workbench '" +
                            station + "', which does not generate its own object, so there is " +
                            "nothing in the world to craft at. Turn on \"Generates its own " +
                            "object\" on that Workbench, or name one of the game's stations.",
                            "Recipe",
                            recipe.RecipeId));
                    }
                }
            }

            DimensionLootTableAsset[] lootTables = template.GlobalLootTables;
            for (int i = 0; i < lootTables.Length; i++)
            {
                DimensionLootTableAsset lootTable = lootTables[i];
                if (lootTable == null || !lootTable.Enabled)
                {
                    continue;
                }

                DimensionLootEntryTemplate[] entries = lootTable.Entries;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (entries[j] != null && entries[j].Enabled &&
                        !universe.ResolvesAsItem(entries[j].ItemId))
                    {
                        issues.Add(Error(
                            "loot-entry-item-unresolved",
                            "Loot table '" + lootTable.DisplayName + "' can drop '" +
                            entries[j].ItemId + "', which nothing defines.",
                            "LootTable",
                            lootTable.LootTableId));
                    }
                }
            }

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(item.LootTableId))
                {
                    referencedLootTables.Add(item.LootTableId);
                    if (!universe.ResolvesAsLootTable(item.LootTableId))
                    {
                        issues.Add(Error(
                            "item-loot-table-unresolved",
                            "'" + item.DisplayName + "' opens loot table '" + item.LootTableId +
                            "', which nothing defines.",
                            "Item",
                            item.ItemId));
                    }
                }

                DimensionDropSource[] drops = item.DropsFrom;
                for (int j = 0; j < drops.Length; j++)
                {
                    DimensionDropSource drop = drops[j];
                    if (drop == null)
                    {
                        continue;
                    }

                    if (drop.DropsNothing)
                    {
                        issues.Add(Warning(
                            "drop-source-empty",
                            "'" + item.DisplayName + "' names a drop source with no chance " +
                            "and no amount, so it quietly never drops.",
                            "Item",
                            item.ItemId));
                        continue;
                    }

                    if (!universe.ResolvesDropSource(drop.Kind, drop.SourceId))
                    {
                        issues.Add(Error(
                            "drop-source-unresolved",
                            "'" + item.DisplayName + "' drops from '" + drop.SourceId +
                            "', which nothing defines for that kind of source.",
                            "Item",
                            item.ItemId));
                    }

                    if (!string.IsNullOrEmpty(drop.OnlyInBiomeId) &&
                        !universe.ResolvesAsBiome(drop.OnlyInBiomeId))
                    {
                        issues.Add(Warning(
                            "drop-biome-unresolved",
                            "'" + item.DisplayName + "' restricts a drop to biome '" +
                            drop.OnlyInBiomeId + "', which nothing defines — the restriction " +
                            "silently never matches.",
                            "Item",
                            item.ItemId));
                    }
                }
            }

            foreach (SceneTemplateAsset scene in universe.AllScenes)
            {
                if (scene == null || !scene.Enabled)
                {
                    continue;
                }

                // Scene furniture travels through sceneObjects — the list Core Keeper's own scene
                // table stamps — so that is where loot-table references live.
                DimensionSceneObjectTemplate[] sceneObjects = scene.SceneObjects;
                for (int j = 0; j < sceneObjects.Length; j++)
                {
                    if (sceneObjects[j] == null || !sceneObjects[j].Enabled)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(sceneObjects[j].LootTableId))
                    {
                        referencedLootTables.Add(sceneObjects[j].LootTableId);
                        if (!universe.ResolvesAsLootTable(sceneObjects[j].LootTableId))
                        {
                            issues.Add(Error(
                                "scene-loot-table-unresolved",
                                "A chest in '" + scene.SceneId + "' rolls '" +
                                sceneObjects[j].LootTableId + "', which nothing defines.",
                                "SceneObject",
                                scene.SceneId));
                        }
                    }
                }
            }

            DimensionPortalAccessRuleAsset[] accessRules = template.PortalAccessRules;
            for (int i = 0; i < accessRules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = accessRules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.IsItemPortal &&
                    !string.IsNullOrEmpty(rule.PortalItemObjectId) &&
                    !universe.ResolvesAsItem(rule.PortalItemObjectId))
                {
                    issues.Add(Error(
                        "portal-item-unresolved",
                        "A portal rule opens from item '" + rule.PortalItemObjectId +
                        "', which nothing defines. That portal could never be used.",
                        "Portal",
                        rule.RuleId));
                }

                // WHAT THE PORTAL ASKS FOR, CHECKED BEFORE THE BUILD RATHER THAN AFTER IT.
                // The offering row was drawn as a bare text box and read by nothing until the
                // generate ran, so a misspelt name shipped a door with a slot no item on earth
                // fits. Gated on UsesRequiredItems the same way the row above is gated on
                // IsItemPortal: a rule that opens on a cooldown never asks for these, so a name
                // left behind in the list is not a defect. An empty name is the "asks for nothing
                // here yet" state and is skipped, exactly as the drop targets below are.
                if (rule.UsesRequiredItems)
                {
                    DimensionPortalRequiredItemTemplate[] offerings = rule.RequiredItems;
                    for (int j = 0; j < offerings.Length; j++)
                    {
                        if (string.IsNullOrEmpty(offerings[j].ItemId) ||
                            universe.ResolvesAsItem(offerings[j].ItemId))
                        {
                            continue;
                        }

                        issues.Add(Error(
                            "portal-required-item-unresolved",
                            "A portal rule asks for '" + offerings[j].ItemId +
                            "' before it opens, and nothing defines that. No player could ever " +
                            "fill the slot, so that portal stays shut for good.",
                            "Portal",
                            rule.RuleId));
                    }
                }

                DimensionPortalDropTarget[] dropTargets = rule.DropTargets;
                for (int j = 0; j < dropTargets.Length; j++)
                {
                    if (string.IsNullOrEmpty(dropTargets[j].TargetObjectId))
                    {
                        continue;
                    }

                    if (!universe.ResolvesDropSource(
                            DimensionDropSourceKind.Creature,
                            dropTargets[j].TargetObjectId))
                    {
                        issues.Add(Error(
                            "portal-drop-target-unresolved",
                            "A portal rule drops its item from '" +
                            dropTargets[j].TargetObjectId +
                            "', which nothing defines. The drop would never happen.",
                            "Portal",
                            rule.RuleId));
                    }
                }
            }

            for (int i = 0; i < lootTables.Length; i++)
            {
                DimensionLootTableAsset lootTable = lootTables[i];
                if (lootTable != null && lootTable.Enabled &&
                    !referencedLootTables.Contains(lootTable.LootTableId) &&
                    !universe.LootTableReferencedByCreature(template, lootTable))
                {
                    issues.Add(Warning(
                        "loot-table-unused",
                        "Loot table '" + lootTable.DisplayName +
                        "' is rolled by nothing, so it quietly does nothing.",
                        "LootTable",
                        lootTable.LootTableId));
                }
            }
        }

        // ------------------------------------------------------------- collisions ---

        private static void AddIdCollisionIssues(
            DimensionTemplateAsset template,
            DimensionContentIdUniverse universe,
            List<DimensionAuthoringIssue> issues)
        {
            foreach (KeyValuePair<string, List<string>> pair in universe.DefinitionsById)
            {
                if (pair.Value.Count > 1)
                {
                    issues.Add(Error(
                        "content-id-duplicate",
                        "The id '" + pair.Key + "' is defined " + pair.Value.Count +
                        " times (" + string.Join(", ", pair.Value) +
                        "). Every reference to it is a coin toss.",
                        "asset-reference",
                        pair.Key));
                }

                if (universe.IsVanillaObject(pair.Key))
                {
                    issues.Add(Error(
                        "content-id-shadows-vanilla",
                        "The id '" + pair.Key + "' is also one of the game's own object " +
                        "names. Every reference to it binds to the game's object, and the " +
                        "authored one becomes unreachable.",
                        "asset-reference",
                        pair.Key));
                }
            }
        }

        // ------------------------------------------------------------------ visual ---

        private static void AddVisualAssetIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            DimensionVisualAssetValidationReport report =
                DimensionVisualAssetValidationUtility.Build(template);
            if (report != null && report.Issues != null)
            {
                issues.AddRange(report.Issues);
            }
        }

        // ------------------------------------------------------------ capabilities ---

        // ------------------------------------------------------------------ tilesets ---

        /// <summary>
        /// The fishing hazard: the game's fishing table is a fixed 75-slot array indexed by the
        /// water tile's tileset, unchecked, inside Burst. A custom tileset id (always ≥1000)
        /// used as WATER reads out-of-bounds memory the moment someone casts a line into it.
        /// </summary>
        private static void AddCustomWaterIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            DimensionTilesetAsset[] tilesets = template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            for (int i = 0; i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null || !tileset.Enabled ||
                    !string.Equals(tileset.BlockTypeKey, "water", System.StringComparison.Ordinal))
                {
                    continue;
                }

                issues.Add(new DimensionAuthoringIssue(
                    DimensionAuthoringSeverity.Warning,
                    "custom-water-fishing",
                    "'" + tileset.BlockTypeKey + "' block '" + tileset.TilesetName + "' is a " +
                    "custom WATER tileset. The game's fishing table is a fixed array indexed " +
                    "by the water's tileset id, unchecked, inside Burst — fishing in this " +
                    "water reads memory out of bounds. Use one of the game's own waters for " +
                    "the liquid layer (Sea water fishes at Sea tier, and so on) and keep " +
                    "custom blocks on ground and walls.",
                    "Tileset",
                    tileset.TilesetName,
                    false,
                    default));
            }
        }

        // ------------------------------------------------------------------ bosses ---

        /// <summary>
        /// Whether each boss's arena actually contains the objects the boss build generates
        /// for it: the map marker and the summoning circle.
        /// </summary>
        /// <remarks>
        /// Both are companion prefabs the author must PLACE — the build makes them exist, the
        /// arena scene makes them real. A pin that is generated but never placed simply never
        /// appears on any map; a circle never placed means the summoning item is inert. Both
        /// are no-ops shipped, which is the definition of a Warning here.
        /// </remarks>
        private static void AddBossArenaIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            DimensionBossAsset[] bosses = template.GlobalBosses;
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled)
                {
                    continue;
                }

                bool wantsMarkerPlaced = boss.MapPin.ShowsOnTheMap;
                bool wantsCirclePlaced = !string.IsNullOrEmpty(boss.SummoningItemId);
                if (!wantsMarkerPlaced && !wantsCirclePlaced)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(boss.ArenaSceneId))
                {
                    if (wantsCirclePlaced)
                    {
                        issues.Add(new DimensionAuthoringIssue(
                            DimensionAuthoringSeverity.Warning,
                            "boss-arena-missing",
                            "'" + boss.DisplayName + "' is summoned by an item but names no " +
                            "arena. Its summoning circle is generated as a placeable object — " +
                            "put it in a place, or nothing anywhere accepts the item.",
                            "Boss",
                            boss.BossId,
                            false,
                            default));
                    }

                    continue;
                }

                SceneTemplateAsset arena = FindSceneById(template, boss.ArenaSceneId);
                if (arena == null)
                {
                    issues.Add(new DimensionAuthoringIssue(
                        DimensionAuthoringSeverity.Warning,
                        "boss-arena-unknown",
                        "'" + boss.DisplayName + "' names arena '" + boss.ArenaSceneId +
                        "', which is not one of this dimension's places.",
                        "Boss",
                        boss.BossId,
                        false,
                        default));
                    continue;
                }

                if (wantsMarkerPlaced &&
                    !SceneContainsObject(arena, boss.BossId + "-map-marker"))
                {
                    issues.Add(new DimensionAuthoringIssue(
                        DimensionAuthoringSeverity.Warning,
                        "boss-pin-not-placed",
                        "'" + boss.DisplayName + "' shows a map pin, but its arena '" +
                        arena.SceneId + "' does not contain the generated marker object ('" +
                        boss.BossId + "-map-marker'). The pin will never appear on the map.",
                        "Boss",
                        boss.BossId,
                        false,
                        default));
                }

                if (wantsCirclePlaced &&
                    !SceneContainsObject(arena, boss.BossId + "-summon-circle"))
                {
                    issues.Add(new DimensionAuthoringIssue(
                        DimensionAuthoringSeverity.Warning,
                        "boss-circle-not-placed",
                        "'" + boss.DisplayName + "' is summoned by an item, but its arena '" +
                        arena.SceneId + "' does not contain the generated summoning circle ('" +
                        boss.BossId + "-summon-circle'). The item will have nothing to wake.",
                        "Boss",
                        boss.BossId,
                        false,
                        default));
                }
            }
        }

        private static SceneTemplateAsset FindSceneById(
            DimensionTemplateAsset template,
            string sceneId)
        {
            SceneTemplateAsset[] scenes = template.GlobalScenes;
            if (scenes != null)
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    if (scenes[i] != null &&
                        string.Equals(scenes[i].SceneId, sceneId, System.StringComparison.Ordinal))
                    {
                        return scenes[i];
                    }
                }
            }

            BiomeTemplateAsset[] biomes = template.Biomes;
            if (biomes != null)
            {
                for (int b = 0; b < biomes.Length; b++)
                {
                    if (biomes[b] == null)
                    {
                        continue;
                    }

                    SceneTemplateAsset[] pool = biomes[b].ScenePool;
                    for (int i = 0; pool != null && i < pool.Length; i++)
                    {
                        if (pool[i] != null &&
                            string.Equals(pool[i].SceneId, sceneId, System.StringComparison.Ordinal))
                        {
                            return pool[i];
                        }
                    }
                }
            }

            return null;
        }

        private static bool SceneContainsObject(SceneTemplateAsset scene, string objectId)
        {
            DimensionSceneObjectTemplate[] objects = scene.SceneObjects;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null &&
                    objects[i].Enabled &&
                    string.Equals(objects[i].ObjectId, objectId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddCapabilityMaturityIssues(
            DimensionTemplateAsset template,
            List<DimensionAuthoringIssue> issues)
        {
            AddCapabilityIssue(issues, template.GlobalItems.Length > 0, "custom-items");
            AddCapabilityIssue(
                issues,
                template.GlobalRecipes.Length > 0 ||
                template.GlobalWorkbenches.Length > 0 ||
                template.GlobalLootTables.Length > 0,
                "recipes-workbenches-loot");
            AddCapabilityIssue(issues, template.Tilesets.Length > 0, "custom-tilesets");
            AddCapabilityIssue(issues, template.Biomes.Length > 0, "biomes-zones");
            AddCapabilityIssue(
                issues,
                template.PortalVisualProfile != null || template.ItemPortalVisualProfile != null,
                "portal-studio");
        }

        private static void AddCapabilityIssue(
            List<DimensionAuthoringIssue> issues,
            bool used,
            string capabilityId)
        {
            if (!used ||
                !DimensionCapabilityRegistry.TryGet(capabilityId, out DimensionCapability capability) ||
                capability.Maturity == DimensionCapabilityMaturity.ImplementedAndEvidenced)
            {
                return;
            }

            // Info, never Warning: while the registry is honest about maturity, a Warning here
            // would make "Nothing is silent" permanently red and teach creators to ignore it.
            issues.Add(new DimensionAuthoringIssue(
                DimensionAuthoringSeverity.Info,
                "capability-" + capability.Id + "-maturity",
                capability.Title + " is " +
                DimensionCapabilityRegistry.Describe(capability.Maturity) + ". " +
                capability.Note,
                "Capability",
                capability.Id,
                false,
                default));
        }

        private static DimensionAuthoringIssue Error(
            string code,
            string message,
            string recordKind,
            string recordId)
        {
            return new DimensionAuthoringIssue(
                DimensionAuthoringSeverity.Error,
                code,
                message,
                recordKind,
                recordId,
                false,
                default);
        }

        private static DimensionAuthoringIssue Warning(
            string code,
            string message,
            string recordKind,
            string recordId)
        {
            return new DimensionAuthoringIssue(
                DimensionAuthoringSeverity.Warning,
                code,
                message,
                recordKind,
                recordId,
                false,
                default);
        }
    }
}
