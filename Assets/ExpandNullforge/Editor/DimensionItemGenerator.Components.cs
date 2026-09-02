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
    /// Putting one component on the item prefab and setting the fields on it.
    /// </summary>
    internal static partial class DimensionItemGenerator
    {
        private static void ConfigureObject(
            GameObject root,
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report)
        {
            ObjectAuthoring objectAuthoring = EnsureComponent<ObjectAuthoring>(root);

            // Qualified with the owning mod. This is the name Core Keeper keys object properties
            // by, and the one a colliding mod would fail against.
            objectAuthoring.objectName = naming.QualifyGenerated(item.ItemId);

            // A placeholder, and it is always overwritten: ApplyDurabilityTheGameWay at the end of
            // Configure sets the real one down every path it can take. On anything that wears out,
            // initialAmount IS the starting durability and the game reads it as such, and it is
            // also the field an author's own typed durability has to land in to survive an import.
            // On everything else it is the stack a player is handed, which vanilla writes as 1 for
            // all but a handful of objects.
            objectAuthoring.initialAmount = 1;
            objectAuthoring.additionalSprites = new List<Sprite>();
            objectAuthoring.variation = item.Variation;
            objectAuthoring.variationIsDynamic = item.VariationIsChosenAtRuntime;
            objectAuthoring.variationToToggleTo = item.VariationItTogglesTo;

            // Written for every item, not only the world-placed ones. This used to be the single
            // line "if it is placed in the world, say PlaceablePrefab", which left everything a
            // player holds at the enum's own default of NonUsable — and NonUsable is the value the
            // game maps to the non-usable slot, so a generated sword could not be swung and a
            // generated helmet could not be worn.
            objectAuthoring.objectType = DimensionItemObjectTypes.ToObjectType(kind);

            // And the same answer again, as a component the RUNNING game can read.
            //
            // Everything that asks what an item is through the object table — slots, cooldowns,
            // durability, equipping, which inventory slot will accept it — reads the line above.
            // Three things ask the live object instead, through ObjectTypeCD: whether a hit shows
            // sparks (AttackSystem), whether damage treats it as destructible (EntityUtility), and
            // environmental conditions — and that last one does not merely read a default, it
            // queries .WithAll<ObjectTypeCD>() and leaves out anything without the component, so a
            // mod creature standing in slime was never in the system at all.
            // (PlayerAttackRoutineSystem used to be named here as a fourth. It is not one: it reads
            // PlayerAttackCD.objectType, which comes off the database blob, not this component.)
            //
            // The game's own EntityMonoBehaviourDataConverter adds ObjectTypeCD; ObjectConverter,
            // which is the converter every mod object goes through, does not. So the marker below
            // carries it, and DimensionObjectTypeConverter copies the type off this same
            // ObjectAuthoring at conversion. Scoped to objects this generator writes rather than
            // patched onto ObjectConverter, because that would hand the component to every vanilla
            // ObjectAuthoring object as well and change what the base game does.
            EnsureComponent<ExpandNullforge.Authoring.DimensionObjectTypeAuthoring>(root);

            // Rarity is a Core Keeper enum on ObjectAuthoring. Honour the item's authored rarity so
            // any item — the tileset block included — can set its tier from the dashboard; empty
            // leaves the vanilla default. (Portal items re-assert Rare in Configure, keeping their
            // fixed framework identity.)
            if (!string.IsNullOrEmpty(item.RarityId))
            {
                TrySetEnumProperty(objectAuthoring, "rarity", item.RarityId, item, report);
            }

            if (ResolveSprite(item) == null && !string.IsNullOrEmpty(item.IconId))
            {
                report.Warnings.Add(
                    Describe(item) + ": no sprite found for icon id '" + item.IconId +
                    "'. Drag the sprite into the item's Icon sprite field instead.");
            }
        }

        private static void ConfigureInventory(
            InventoryItemAuthoring inventory,
            DimensionItemAsset item,
            bool stacks,
            DimensionRecipeAsset recipe,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report)
        {
            // Resolved from the kind before this, not read straight off the tick. The tick starts
            // life ticked, and initialAmount is durability on anything that wears out and the
            // number handed over on anything that stacks — so a helmet taking the tick at its word
            // is a stack of ninety helmets.
            inventory.isStackable = stacks;
            inventory.requiredObjectsToCraft = BuildIngredients(item, recipe, naming, report);
            if (recipe != null && recipe.CraftTimeSeconds > 0f)
            {
                inventory.craftingTime = recipe.CraftTimeSeconds;
            }

            Sprite icon = ResolveSprite(item);
            if (icon != null)
            {
                inventory.iconOffset = item.IconOffset;
            inventory.icon = icon;
            }

            // Prefer an explicit small (in-hand / on-cursor) icon; fall back to the inventory icon so
            // the item is never left without a held sprite.
            Sprite smallIcon = item.SmallIconSprite != null ? item.SmallIconSprite : icon;
            if (smallIcon != null)
            {
                inventory.smallIcon = smallIcon;
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
            DimensionNamingContext naming,
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

                // Qualified only when the ingredient names one of OUR items. A recipe may ask for a
                // vanilla item (IronBar, Wood), and qualifying that would point it at an item that
                // does not exist.
                ingredients.Add(new InventoryItemAuthoring.CraftingObject
                {
                    objectName = naming.QualifyReference(template.ItemId),
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

        /// <summary>
        /// Deletes the generated prefab for an item id (the "Generate Items" output). Called when the
        /// item — or the block that owns it — is deleted, so create and delete stay symmetrical.
        /// Localization rows are left in place: the CSV is fully rewritten by the next generate, and
        /// stale rows are inert until then.
        /// </summary>
        public static void DeleteGeneratedArtifacts(string templatePath, string itemId)
        {
            if (string.IsNullOrEmpty(templatePath) || string.IsNullOrEmpty(itemId))
            {
                return;
            }

            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            string prefabPath = modRoot + "/Items/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(itemId, "Item") + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
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
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }
    }
}
