using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a generation run did and what it could not do. Nothing is reported as generated
    /// unless the prefab was actually written, and any value the framework could not apply is
    /// surfaced as a warning rather than being dropped silently.
    /// </summary>
    internal sealed class DimensionItemGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public int GeneratedCount => Created.Count + Updated.Count;

        public bool HasProblems => Warnings.Count > 0 || Errors.Count > 0;

        public string Summarize()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(Created.Count).Append(" created, ")
                .Append(Updated.Count).Append(" updated, ")
                .Append(Skipped.Count).Append(" skipped");
            if (Warnings.Count > 0)
            {
                builder.Append(", ").Append(Warnings.Count).Append(" warning(s)");
            }

            if (Errors.Count > 0)
            {
                builder.Append(", ").Append(Errors.Count).Append(" error(s)");
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Turns validated <see cref="DimensionItemAsset"/> definitions into consumer-owned Core
    /// Keeper prefabs. The archetype decides which authoring components are attached, so the
    /// generated object matches what the dashboard promised the creator it would build.
    ///
    /// Field names are applied through <see cref="TrySetProperty"/>: fields proven by the working
    /// portal bootstrap are set directly, and anything the installed SDK does not expose under the
    /// expected name is reported as a warning instead of silently doing nothing.
    /// </summary>
    internal static class DimensionItemGenerator
    {
        /// <summary>
        /// The only ObjectType value proven in this repository (the portal bootstrap uses it).
        /// Any other type must be named explicitly by the creator via the item's object type
        /// override, so the framework never guesses at the game's enum.
        /// </summary>
        private const string DefaultPlaceableObjectType = "PlaceablePrefab";

        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder)
        {
            return Generate(items, outputFolder, null);
        }

        /// <summary>
        /// Generates items and, where a recipe produces one of them, writes that recipe's
        /// ingredients and craft time onto the item. Core Keeper keeps a recipe's ingredient list
        /// on the produced item rather than on the crafting station, so this is where a recipe
        /// becomes real.
        /// </summary>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes)
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

            if (!EnsureAssetFolder(outputFolder))
            {
                report.Errors.Add("Could not create the output folder '" + outputFolder + "'.");
                return report;
            }

            Dictionary<string, DimensionRecipeAsset> recipesByOutput = IndexRecipes(recipes, report);
            List<string> generatedIds = new List<string>();
            List<DimensionLocalizationCsv.Row> localizationRows =
                new List<DimensionLocalizationCsv.Row>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionItemAsset item in items)
                {
                    if (GenerateOne(item, outputFolder, recipesByOutput, report) && item != null)
                    {
                        generatedIds.Add(item.ItemId);
                        DimensionLocalizationCsv.AddItemRows(
                            localizationRows,
                            item.ItemId,
                            item.DisplayName,
                            item.Description);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string modRoot = ResolveModRoot(outputFolder);
            RecordGeneratedItemIds(modRoot, generatedIds, report);
            WriteLocalization(modRoot, localizationRows, report);
            return report;
        }

        /// <summary>
        /// Writes the item names and tooltips into the mod's localization table. Without this an
        /// item shows its raw id in-game, so generation is not complete until it runs.
        /// </summary>
        private static void WriteLocalization(
            string modRoot,
            List<DimensionLocalizationCsv.Row> rows,
            DimensionItemGenerationReport report)
        {
            if (rows.Count == 0)
            {
                return;
            }

            string folder = modRoot + "/Localization";
            if (!EnsureAssetFolder(folder))
            {
                report.Warnings.Add(
                    "Could not create '" + folder +
                    "', so item names were not added to the localization table. Items will " +
                    "show their raw ids in-game.");
                return;
            }

            string path = folder + "/Localization.csv";
            string absolutePath = ToAbsolutePath(path);
            try
            {
                string existing = System.IO.File.Exists(absolutePath)
                    ? System.IO.File.ReadAllText(absolutePath)
                    : null;
                string merged = DimensionLocalizationCsv.Merge(existing, rows);
                if (!string.Equals(existing, merged, StringComparison.Ordinal))
                {
                    System.IO.File.WriteAllText(absolutePath, merged);
                    AssetDatabase.ImportAsset(path);
                }
            }
            catch (Exception exception)
            {
                report.Warnings.Add(
                    "Could not update '" + path + "': " + exception.Message);
            }
        }

        private static string ResolveModRoot(string outputFolder)
        {
            return outputFolder.EndsWith("/Items", StringComparison.Ordinal)
                ? outputFolder.Substring(0, outputFolder.Length - "/Items".Length)
                : outputFolder;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            return System.IO.Path.Combine(projectRoot ?? string.Empty, assetPath)
                .Replace('\\', '/');
        }

        /// <summary>
        /// Writes the generated ids into the mod's runtime manifest so the runtime can declare
        /// them and name any prefab that fails to register in-game. Without this the manifest and
        /// the generated prefabs would drift apart silently.
        /// </summary>
        private static void RecordGeneratedItemIds(
            string modRoot,
            List<string> generatedIds,
            DimensionItemGenerationReport report)
        {
            string[] guids = AssetDatabase.IsValidFolder(modRoot)
                ? AssetDatabase.FindAssets("t:DimensionRuntimeManifestAsset", new[] { modRoot })
                : null;
            if (guids == null || guids.Length == 0)
            {
                if (generatedIds.Count > 0)
                {
                    report.Warnings.Add(
                        "No runtime manifest was found under '" + modRoot +
                        "', so the generated items cannot be declared at runtime. Export the " +
                        "dimension manifest, then generate again.");
                }

                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                DimensionRuntimeManifestAsset manifest =
                    AssetDatabase.LoadAssetAtPath<DimensionRuntimeManifestAsset>(path);
                if (manifest == null)
                {
                    continue;
                }

                manifest.SetGeneratedItemIds(generatedIds.ToArray());
                EditorUtility.SetDirty(manifest);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Indexes recipes by the item they produce. Two enabled recipes producing the same item
        /// would each want to own that item's ingredient list, so the clash is reported rather
        /// than resolved by whichever happened to come last.
        /// </summary>
        private static Dictionary<string, DimensionRecipeAsset> IndexRecipes(
            IEnumerable<DimensionRecipeAsset> recipes,
            DimensionItemGenerationReport report)
        {
            Dictionary<string, DimensionRecipeAsset> byOutput =
                new Dictionary<string, DimensionRecipeAsset>(StringComparer.Ordinal);
            if (recipes == null)
            {
                return byOutput;
            }

            foreach (DimensionRecipeAsset recipe in recipes)
            {
                if (recipe == null || !recipe.Enabled || string.IsNullOrEmpty(recipe.OutputItemId))
                {
                    continue;
                }

                if (byOutput.TryGetValue(recipe.OutputItemId, out DimensionRecipeAsset existing))
                {
                    report.Warnings.Add(
                        "Recipes '" + existing.RecipeId + "' and '" + recipe.RecipeId +
                        "' both produce '" + recipe.OutputItemId +
                        "'. Core Keeper stores ingredients on the produced item, so only '" +
                        existing.RecipeId + "' was applied. Disable one of them.");
                    continue;
                }

                byOutput.Add(recipe.OutputItemId, recipe);
            }

            return byOutput;
        }

        /// <returns>True when a prefab was written for this item.</returns>
        private static bool GenerateOne(
            DimensionItemAsset item,
            string outputFolder,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            DimensionItemGenerationReport report)
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

            string prefabPath = outputFolder + "/" + SanitizeFileName(item.ItemId) + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(item.ItemId);

            bool written = false;
            try
            {
                Configure(root, item, recipesByOutput, report);
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

        private static void Configure(
            GameObject root,
            DimensionItemAsset item,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            DimensionItemGenerationReport report)
        {
            DimensionItemArchetype archetype = item.Archetype;
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(archetype);

            root.name = item.ItemId;

            ConfigureObject(root, item, report);
            ConfigureLocalization(root, item, report);

            recipesByOutput.TryGetValue(item.ItemId, out DimensionRecipeAsset recipe);
            ApplyComponent<InventoryItemAuthoring>(
                root, required, DimensionItemAuthoringComponents.InventoryItem,
                component => ConfigureInventory(component, item, recipe, report));

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

            // maxDurability is the authored ceiling and the only one that survives serialization;
            // the sibling `durability` field is runtime state that resets to its initializer when
            // the prefab is reloaded, so writing it here would be a no-op that reads as intent.
            ApplyComponent<DurabilityAuthoring>(
                root, required, DimensionItemAuthoringComponents.Durability,
                component => component.maxDurability = item.DurabilityPoints);

            ApplyComponent<WeaponDamageAuthoring>(
                root, required, DimensionItemAuthoringComponents.WeaponDamage,
                component => component.damage = item.DamageAmount);

            ApplyComponent<CooldownAuthoring>(
                root, required, DimensionItemAuthoringComponents.Cooldown,
                component =>
                {
                    // Zero means "use the vanilla default", which the validator already warned about.
                    if (item.CooldownSeconds > 0f)
                    {
                        component.cooldown = item.CooldownSeconds;
                    }
                });

            // Health backs both destructible props and creatures.
            bool needsHealth =
                Requires(required, DimensionItemAuthoringComponents.Breakable) ||
                Requires(required, DimensionItemAuthoringComponents.Creature);
            ApplyComponent<HealthAuthoring>(
                root, needsHealth,
                component => component.maxHealth = item.HealthPoints);

            ApplyComponent<DamageableObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);
            ApplyComponent<DestructibleObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);

            // lootTableID is a game enum, not free text: an id the game does not define would
            // otherwise silently resolve to whatever sits at index 0, so it is matched by name
            // and reported when it does not exist.
            ApplyComponent<DropLootAuthoring>(
                root, required, DimensionItemAuthoringComponents.Loot,
                component => TrySetEnumProperty(
                    component, "lootTableID", item.LootTableId, item, report));

            ApplyComponent<GivesConditionsWhenEquippedAuthoring>(
                root, required, DimensionItemAuthoringComponents.EquipmentConditions, null);
            ApplyComponent<SecondaryUseAuthoring>(
                root, required, DimensionItemAuthoringComponents.SecondaryUse, null);

            if (Requires(required, DimensionItemAuthoringComponents.BossEncounter))
            {
                report.Warnings.Add(
                    Describe(item) +
                    ": boss encounter wiring (phases, arena, music) has no framework path yet; " +
                    "the prefab is generated without it.");
            }
        }

        private static void ConfigureObject(
            GameObject root,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            ObjectAuthoring objectAuthoring = EnsureComponent<ObjectAuthoring>(root);
            objectAuthoring.objectName = item.ItemId;
            objectAuthoring.initialAmount = 1;
            objectAuthoring.additionalSprites = new List<Sprite>();

            if (DimensionItemArchetypeRules.IsWorldPlaced(item.Archetype))
            {
                TrySetEnumProperty(
                    objectAuthoring,
                    "objectType",
                    DefaultPlaceableObjectType,
                    item,
                    report);
            }

            if (ResolveSprite(item) == null && !string.IsNullOrEmpty(item.IconId))
            {
                report.Warnings.Add(
                    Describe(item) + ": no sprite found for icon id '" + item.IconId +
                    "'. Drag the sprite into the item's Icon sprite field instead.");
            }
        }

        private static void ConfigureLocalization(
            GameObject root,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            LocalizationAuthoring localization = EnsureComponent<LocalizationAuthoring>(root);
            localization.termKey = item.ItemId;
            TrySetArraySize(localization, "languageGenders", 0);
        }

        private static void ConfigureInventory(
            InventoryItemAuthoring inventory,
            DimensionItemAsset item,
            DimensionRecipeAsset recipe,
            DimensionItemGenerationReport report)
        {
            inventory.isStackable = item.MaxStack > 1;
            inventory.requiredObjectsToCraft = BuildIngredients(item, recipe, report);
            if (recipe != null && recipe.CraftTimeSeconds > 0f)
            {
                inventory.craftingTime = recipe.CraftTimeSeconds;
            }

            Sprite icon = ResolveSprite(item);
            if (icon != null)
            {
                inventory.icon = icon;
                if (inventory.smallIcon == null)
                {
                    inventory.smallIcon = icon;
                }
            }
        }

        /// <summary>
        /// Turns a recipe's ingredient list into the crafting requirements on the produced item.
        /// An ingredient with no item id is dropped and reported, because a blank entry would
        /// silently make the item craftable from nothing.
        ///
        /// Amounts are not checked here: <c>DimensionRecipeIngredientTemplate.Amount</c> clamps to
        /// at least one, so a zero cannot reach this point and a guard against it would be dead
        /// code that reads as protection it does not provide.
        /// </summary>
        private static List<InventoryItemAuthoring.CraftingObject> BuildIngredients(
            DimensionItemAsset item,
            DimensionRecipeAsset recipe,
            DimensionItemGenerationReport report)
        {
            List<InventoryItemAuthoring.CraftingObject> ingredients =
                new List<InventoryItemAuthoring.CraftingObject>();
            if (recipe == null)
            {
                return ingredients;
            }

            DimensionRecipeIngredientTemplate[] templates = recipe.Ingredients;
            if (templates == null)
            {
                return ingredients;
            }

            for (int i = 0; i < templates.Length; i++)
            {
                DimensionRecipeIngredientTemplate template = templates[i];
                if (template == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(template.ItemId))
                {
                    report.Warnings.Add(
                        Describe(item) + ": recipe '" + recipe.RecipeId +
                        "' has an ingredient with no item id; it was skipped.");
                    continue;
                }

                ingredients.Add(new InventoryItemAuthoring.CraftingObject
                {
                    objectName = template.ItemId,
                    amount = template.Amount
                });
            }

            if (ingredients.Count == 0)
            {
                report.Warnings.Add(
                    Describe(item) + ": recipe '" + recipe.RecipeId +
                    "' has no usable ingredients, so it can be crafted from nothing.");
            }

            return ingredients;
        }

        private static void ConfigurePlaceable(
            PlaceableObjectAuthoring placeable,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            // A single-tile footprint on walkable ground is the safe default; anything larger is
            // the creator's decision and is left alone once they change it.
            if (placeable.prefabTileSize == Vector2Int.zero)
            {
                placeable.prefabTileSize = Vector2Int.one;
            }

            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canBePlacedOnObjects = new List<ObjectID>();
            placeable.canNotBePlacedOnObjects = new List<ObjectID>();
        }

        private static void ApplyComponent<T>(
            GameObject root,
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component,
            Action<T> configure)
            where T : Component
        {
            ApplyComponent(root, Requires(required, component), configure);
        }

        /// <summary>
        /// Attaches and configures a component the archetype needs, or removes it when the
        /// archetype no longer needs it — so switching an item from weapon to material does not
        /// leave orphaned damage data on the prefab.
        /// </summary>
        private static void ApplyComponent<T>(
            GameObject root,
            bool needed,
            Action<T> configure)
            where T : Component
        {
            if (!needed)
            {
                RemoveComponentIfPresent<T>(root);
                return;
            }

            T component = EnsureComponent<T>(root);
            configure?.Invoke(component);
        }

        private static void TrySetEnumProperty(
            Object target,
            string propertyName,
            string enumMemberName,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            TrySetProperty(
                target,
                propertyName,
                item,
                report,
                property =>
                {
                    if (property.propertyType != SerializedPropertyType.Enum ||
                        property.enumNames == null)
                    {
                        return false;
                    }

                    for (int i = 0; i < property.enumNames.Length; i++)
                    {
                        if (string.Equals(
                            property.enumNames[i], enumMemberName, StringComparison.Ordinal))
                        {
                            property.enumValueIndex = i;
                            return true;
                        }
                    }

                    return false;
                });
        }

        /// <summary>
        /// Applies a serialized value, reporting rather than failing silently when the installed
        /// SDK does not expose the expected field. The creator gets one precise line naming the
        /// component and field to check instead of a prefab that is quietly wrong.
        /// </summary>
        private static bool TrySetProperty(
            Object target,
            string propertyName,
            DimensionItemAsset item,
            DimensionItemGenerationReport report,
            Func<SerializedProperty, bool> apply)
        {
            if (target == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !apply(property))
            {
                report.Warnings.Add(
                    Describe(item) + ": could not set '" + propertyName + "' on " +
                    target.GetType().Name +
                    " (this SDK build names it differently). Set it on the prefab by hand.");
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void TrySetArraySize(Object target, string propertyName, int size)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                property.arraySize = Mathf.Max(0, size);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Resolves the item's art. A directly assigned sprite always wins; otherwise the icon id
        /// is treated as a project path or an asset name. Returns null rather than substituting a
        /// placeholder, so a missing icon is reported instead of shipping the wrong art.
        /// </summary>
        private static Sprite ResolveSprite(DimensionItemAsset item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.IconSprite != null)
            {
                return item.IconSprite;
            }

            string iconId = item.IconId;
            if (string.IsNullOrEmpty(iconId))
            {
                return null;
            }

            if (iconId.StartsWith("Assets", StringComparison.Ordinal))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(iconId);
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite " + iconId);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null &&
                    string.Equals(sprite.name, iconId, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }

            return null;
        }

        private static bool EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return true;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(folder);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Item";
            }

            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char character = characters[i];
                bool safe =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_' || character == '-';
                if (!safe)
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }

        private static string Describe(DimensionItemAsset item)
        {
            if (item == null)
            {
                return "<missing item>";
            }

            return string.IsNullOrEmpty(item.DisplayName)
                ? item.ItemId
                : item.DisplayName + " (" + item.ItemId + ")";
        }

        private static bool Requires(
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component)
        {
            return (required & component) == component;
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            if (root == null)
            {
                return;
            }

            T component = root.GetComponent<T>();
            if (component != null)
            {
                Object.DestroyImmediate(component, true);
            }
        }
    }
}
