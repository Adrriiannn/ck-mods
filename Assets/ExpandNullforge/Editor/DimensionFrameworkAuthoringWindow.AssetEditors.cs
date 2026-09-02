using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The editors for items and the things made from them.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawSceneTemplateEditors(SceneTemplateAsset[] scenes, string titlePrefix)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    scene,
                    BuildAssetEditorTitle(titlePrefix, scene.DisplayName, scene.SceneId, i),
                    Field("displayName", "Display name"),
                    NameField("sceneId", "Scene ID"),
                    NameField("templateId", "Template ID"),
                    Field("kind", "Kind"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("placementMode", "Placement mode"),
                    Field("footprintSize", "Footprint size"),
                    Field("exactLocalPosition", "Exact local position"),
                    Field("preferredLocalMin", "Preferred local min"),
                    Field("preferredLocalMaxExclusive", "Preferred local max"),
                    Field("weight", "Weight"),
                    Field("priority", "Priority"),
                    Field("enabled", "Enabled"),
                    Field("required", "Required"),
                    Field("unique", "Unique"),
                    Field("triggers", "Triggers"),
                    Field("tiles", "Terrain tiles"),
                    Field("sceneObjects", "Placed objects"));
            }
        }

        private void DrawItemAssetEditors(DimensionItemAsset[] items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null)
                {
                    continue;
                }

                // Hidden items are framework infrastructure the modder should never edit — e.g. a
                // tileset block's auto-created ground counterpart. They still generate; they just
                // don't clutter the item list.
                if (item.Hidden)
                {
                    continue;
                }

                DrawSerializedAsset(
                    item,
                    BuildAssetEditorTitle("Item", item.DisplayName, item.ItemId, i),
                    BuildItemFields(item));

                DrawItemArchetypeSummary(item);

                // Symmetry: an item leaves with its whole footprint (.asset + generated prefab).
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete item…", GUILayout.Width(100f)) &&
                    EditorUtility.DisplayDialog(
                        "Delete item",
                        "Delete \"" + item.DisplayName + "\" and its generated prefab?",
                        "Delete",
                        "Cancel"))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.DeleteItem(selectedTemplate, item));
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
        }

        /// <summary>
        /// Generation bar for items: reports how many are ready and how many are blocked, and
        /// only offers the action when there is something valid to build.
        /// </summary>
        private void DrawItemGenerationBar()
        {
            TryApplyDefaultPortalIcons();

            DimensionItemAsset[] items = selectedTemplate == null
                ? null
                : selectedTemplate.GlobalItems;

            int ready = 0;
            int blocked = 0;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    DimensionItemAsset item = items[i];
                    if (item == null || !item.Enabled)
                    {
                        continue;
                    }

                    if (DimensionItemArchetypeValidator.CanGenerate(item))
                    {
                        ready++;
                    }
                    else
                    {
                        blocked++;
                    }
                }
            }

            // Default items for enabled item portals (V2) are created on generate and are valid by
            // construction, so count them as ready — this also enables the button when the only item to
            // build is an item portal the creator has not hand-authored yet.
            ready += DimensionFrameworkAuthoringAssetUtility.CountMissingPortalItems(selectedTemplate);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(ready <= 0))
            {
                if (GUILayout.Button(
                    "Generate " + ready + " Item Prefab" + (ready == 1 ? string.Empty : "s"),
                    GUILayout.Width(210f)))
                {
                    GenerateItemPrefabs(items);
                }
            }

            if (blocked > 0)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.5f);
                EditorGUILayout.LabelField(
                    "● " + blocked + " item(s) blocked — see the errors below.",
                    EditorStyles.miniLabel);
                GUI.color = previousColor;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Builds the field list for an item from its archetype, so a creator only sees the values
        /// their chosen kind actually uses — a material does not ask for weapon damage, and a
        /// weapon does not hide it.
        /// </summary>
        private static SerializedFieldSpec[] BuildItemFields(DimensionItemAsset item)
        {
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(item.Archetype);

            List<SerializedFieldSpec> fields = new List<SerializedFieldSpec>
            {
                Field("displayName", "Display name"),
                NameField("itemId", "Item ID"),
                Field("archetype", "Archetype"),
                Field("description", "Description"),
                Field("iconSprite", "Icon sprite (16x16)"),
                Field("smallIconSprite", "Small icon (in-hand, 10x10)"),
                Field("iconId", "Icon ID (fallback)"),
                NameField("objectId", "Object ID")
            };

            if (RequiresComponent(required, DimensionItemAuthoringComponents.InventoryItem))
            {
                fields.Add(Field("stackable", "Stacks in one slot"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Loot))
            {
                fields.Add(Field("lootTableId", "Loot table ID"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Breakable) ||
                RequiresComponent(required, DimensionItemAuthoringComponents.Creature))
            {
                fields.Add(Field("healthPoints", "Health"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("durabilityPoints", "Durability"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.WeaponDamage))
            {
                fields.Add(Field("damageAmount", "Damage"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.WeaponDamage) ||
                RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("weapon", "As a weapon"));
                fields.Add(Field("attackSounds", "What it sounds like to swing"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Cooldown))
            {
                fields.Add(Field("cooldownSeconds", "Cooldown seconds"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.EquipmentConditions))
            {
                fields.Add(Field("effects", "What it does for you"));

                // Only armour is drawn on the character, and only three body parts read a skin at
                // all, so nothing else is asked this.
                fields.Add(Field("equipmentSkin", "Worn on the character"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.SecondaryUse))
            {
                fields.Add(Field("secondaryUse", "Right-click"));
            }

            // Cooking is offered on every item rather than gated by archetype: an ingredient is a
            // Material, a cooked dish is a Consumable, and a fish is whatever its author decided.
            fields.Add(Field("cooking", "As food"));

            // Explosives are offered on every item for the same reason: a bomb is the Bomb
            // archetype, but an explosive barrel is a Placeable that happens to go off. On the Bomb
            // archetype this is the headline rather than an extra, so it is named as one.
            fields.Add(RequiresComponent(required, DimensionItemAuthoringComponents.Explosive)
                ? Field("explosive", "What it does when it goes off")
                : Field("explosive", "If it goes off"));
            fields.Add(Field("basics", "Where it sits in the world"));
            if (RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("durabilityMultiplier", "How sturdy it is"));
                fields.Add(Field("repairMultiplier", "Repair cost"));
                fields.Add(Field("reinforceCostMultiplier", "Reinforce cost"));
            }
            fields.Add(Field("conditions", "Conditions"));
            fields.Add(Field("offHand", "In the off hand"));
            fields.Add(Field("polishesInto", "Polishes into"));
            fields.Add(Field("isAPotion", "Is a potion"));

            // ---- scanning ----
            fields.Add(Field("scansForObjectId", "Scans for"));
            fields.Add(Field("summonsInsteadOfScanning", "Summons it instead of scanning"));
            fields.Add(Field("scannerOnlyInBiome", "Scanner only works in this biome"));

            // ---- how it wears and swings ----
            fields.Add(Field("flatDurability", "Durability, exactly"));
            fields.Add(Field("flatMaxDurability", "Its ceiling, exactly"));
            fields.Add(Field("casualIgnoresItsCooldown", "Casual mode skips its cooldown"));
            fields.Add(Field("damageIsMagic", "Its damage counts as magic"));
            fields.Add(Field("damageIsRanged", "Its damage counts as ranged"));
            fields.Add(Field("damageMultiplierForItsTier", "How hard it hits for its tier"));

            // ---- its looks ----
            fields.Add(Field("iconOffset", "Icon nudge"));
            fields.Add(Field("variation", "Which look it is"));
            fields.Add(Field("variationIsChosenAtRuntime", "The game picks its look"));
            fields.Add(Field("variationItTogglesTo", "The look it flips to"));
            fields.Add(Field("nameGendersPerLanguage", "Its name's gender, per language", true));

            // ---- the rest of what it can be ----
            fields.Add(Field("instrument", "As an instrument", true));
            fields.Add(Field("extraLoot", "Loot besides its drops", true));
            fields.Add(Field("worldRoles", "Its roles in the world", true));
            fields.Add(Field("simpleTraits", "The small things it simply is", true));

            fields.Add(Field("dropsFrom", "Where it drops from"));
            fields.Add(Field("rarityId", "Rarity ID"));
            fields.Add(Field("enabled", "Enabled"));
            fields.Add(Field("notes", "Notes"));
            return fields.ToArray();
        }

        /// <summary>
        /// Shows what the archetype will generate and anything still missing, so an incomplete
        /// item is caught here rather than discovered in-game.
        /// </summary>
        private void DrawItemArchetypeSummary(DimensionItemAsset item)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "Generates: " + DimensionItemArchetypeRules.Describe(item.Archetype) + " — " +
                DimensionItemArchetypeRules.DescribeComponents(item.Archetype),
                EditorStyles.wordWrappedMiniLabel);

            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            for (int i = 0; i < findings.Count; i++)
            {
                DimensionItemArchetypeValidator.Finding finding = findings[i];
                if (finding.Severity == DimensionItemArchetypeValidator.Severity.Ok)
                {
                    continue;
                }

                bool error = finding.Severity == DimensionItemArchetypeValidator.Severity.Error;
                Color previousColor = GUI.color;
                GUI.color = error
                    ? new Color(1f, 0.55f, 0.5f)
                    : new Color(1f, 0.85f, 0.45f);
                EditorGUILayout.LabelField(
                    (error ? "● " : "▲ ") + finding.Message,
                    EditorStyles.wordWrappedMiniLabel);
                GUI.color = previousColor;
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(4f);
        }

        private static bool RequiresComponent(
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component)
        {
            return (required & component) == component;
        }

        private void DrawRecipeAssetEditors(DimensionRecipeAsset[] recipes)
        {
            if (recipes == null)
            {
                return;
            }

            // The station id is resolved at GENERATION time, not runtime, so a typo is worth calling
            // out here rather than leaving it to become a recipe that silently never appears.
            if (recipes.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    "Generating wires the whole recipe: ingredients and craft time go onto the item, and " +
                    "the output shows up at the crafting station you name.\n\n" +
                    "\"Crafting station ID\" is a Core Keeper object name — WoodenWorkBench, " +
                    "CopperWorkBench, and so on. A name the game does not know is reported when you " +
                    "generate, and that recipe appears at no station. Leave it empty to skip the " +
                    "station entirely; blocks can still be mined where your dimension generates them.",
                    MessageType.Info);
                GUILayout.Space(4f);
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    recipe,
                    BuildAssetEditorTitle("Recipe", recipe.DisplayName, recipe.RecipeId, i),
                    Field("displayName", "Display name"),
                    NameField("recipeId", "Recipe ID"),
                    Field("outputItemId", "Output item ID"),
                    Field("outputAmount", "Output amount"),
                    Field("craftingStationId", "Crafting station ID"),
                    Field("craftTimeSeconds", "Craft time seconds"),
                    Field("enabled", "Enabled"),
                    Field("ingredients", "Ingredients"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawWorkbenchAssetEditors(DimensionWorkbenchAsset[] workbenches)
        {
            if (workbenches == null)
            {
                return;
            }

            for (int i = 0; i < workbenches.Length; i++)
            {
                DimensionWorkbenchAsset workbench = workbenches[i];
                if (workbench == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    workbench,
                    BuildAssetEditorTitle("Workbench", workbench.DisplayName, workbench.WorkbenchId, i),
                    Field("displayName", "Display name"),
                    NameField("workbenchId", "Workbench ID"),
                    NameField("objectId", "Object ID"),
                    Field("iconId", "Icon ID"),
                    Field("recipes", "Recipes"),

                    // ---- how it works ----
                    Field("generatesItsOwnObject", "The framework builds its object"),
                    Field("wholeInventoryIsOneCraft", "Its whole inventory is one craft"),
                    Field("extractsCategoryTag", "What it draws out of things"),
                    Field("extractedAmountRange", "How much it draws at a time"),
                    Field("defaultCraftTimeRange", "How long a craft takes"),
                    Field("showsALoopingEffectWhileWorking", "It shows an effect while working"),

                    // ---- placing and using ----
                    Field("placementRules", "Where it may be placed", true),
                    Field("interaction", "When a player uses it", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawLootTableAssetEditors(DimensionLootTableAsset[] lootTables)
        {
            if (lootTables == null)
            {
                return;
            }

            for (int i = 0; i < lootTables.Length; i++)
            {
                DimensionLootTableAsset lootTable = lootTables[i];
                if (lootTable == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    lootTable,
                    BuildAssetEditorTitle("Loot Table", lootTable.DisplayName, lootTable.LootTableId, i),
                    Field("displayName", "Display name"),
                    NameField("lootTableId", "Loot table ID"),
                    Field("allowEmptyRoll", "Allow empty roll"),
                    Field("enabled", "Enabled"),
                    Field("entries", "Entries"),
                    Field("notes", "Notes"));
            }
        }

        /// <summary>
        /// The container editors.
        /// </summary>
        /// <remarks>
        /// The fields are grouped the way the questions actually come: what it is, how big, where it
        /// goes, how it breaks, and what happens when something is put in it. The indestructible tick
        /// only bites on a world-placed container, and the asset says so itself rather than the
        /// window having to explain it twice.
        /// </remarks>
        private void DrawContainerAssetEditors(DimensionContainerAsset[] containers)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                DimensionContainerAsset container = containers[i];
                if (container == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    container,
                    BuildAssetEditorTitle("Container", container.DisplayName, container.ContainerId, i),
                    Field("displayName", "Display name"),
                    NameField("containerId", "Container ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("labelItComesWith", "The label floating above it"),
                    Field("sprite", "Sprite"),
                    Field("icon", "Icon"),
                    Field("basics", "Where it sits in the world"),
                    Field("conditions", "Conditions"),
                    Field("size", "Size"),
                    Field("customSlotsAcross", "Custom slots across"),
                    Field("customSlotsDown", "Custom slots down"),
                    Field("upgradeableExtraSlots", "Upgradeable extra slots"),
                    Field("isAPouch", "Is a pouch"),
                    Field("sameSizeAtEveryLevel", "Same size at every level"),
                    Field("onlyAcceptsCategoryTags", "Only accepts these categories"),
                    Field("oneItemPerSlot", "One item per slot"),
                    Field("contentsAreLocked", "Contents are locked"),
                    Field("cannotAddItems", "Cannot add items"),
                    Field("autoTransfer", "Auto transfer"),
                    Field("slotRules", "Slot rules"),
                    Field("tileSize", "Tile size"),
                    Field("canBePlacedOnWater", "Can be placed on water"),
                    Field("facesPlacementDirection", "Faces placement direction"),
                    Field("origin", "Where it comes from"),
                    Field("indestructible", "Indestructible (world-placed only)"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("requiredMiningDamage", "Required mining damage"),
                    Field("requiresDrill", "Requires drill"),
                    Field("dropsContentsWhenBroken", "Drops contents when broken"),
                    Field("dropsItselfWhenBroken", "Drops itself when broken"),
                    // ---- reacting to an item ----
                    Field("reactsToItemId", "Reacts to item"),
                    Field("reactionVariation", "The look it takes when it reacts"),
                    Field("reactionEffectId", "The effect shown as it reacts"),
                    Field("removeColliderOnReaction", "Its collider goes when it reacts"),
                    Field("becomesContainerId", "Becomes container"),
                    Field("becomesLootTableId", "Becomes loot table"),
                    Field("becomesContents", "Becomes contents"),

                    // ---- what its slots accept ----
                    Field("appliesToAllSlots", "One rule for every slot"),
                    Field("acceptsCategoryTags", "Kinds of thing it accepts", true),
                    Field("acceptsItemIds", "Exact items it accepts", true),
                    Field("denyLegendary", "Legendary gear is refused"),
                    Field("showHint", "Its slots hint at what fits"),

                    // ---- appearance and placing ----
                    Field("sprite", "Its picture in the world"),
                    Field("icon", "Its icon in inventories"),
                    Field("placementRules", "Where it may be placed", true),
                    Field("melodyResponse", "It answers a tune", true),
                    Field("interaction", "When a player uses it", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (container.IndestructibleWasRefused)
                {
                    EditorGUILayout.HelpBox(
                        "Indestructible only applies to a container the world places. A container " +
                        "players craft has to be breakable, or they can never take it back.",
                        MessageType.Warning);
                }
            }
        }
    }
}
