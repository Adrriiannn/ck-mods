using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Dishes, their tiers, their golden versions and what eating one does.
    /// </summary>
    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {
        // ------------------------------------------------------------------ food ---

        /// <summary>Adds a new kind of dish to the dimension.</summary>
        public static DimensionFrameworkAuthoringAssetActionResult CreateDish(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a dish.");
            }

            int index = template.GlobalDishes.Length + 1;
            string stem = "Dish" + index.ToString();
            DimensionDishAsset dish = CreateAsset<DimensionDishAsset>(template, "Resources", stem);
            SetSerializedString(dish, "dishId", ResolveScopedId(template, stem));
            SetSerializedString(dish, "displayName", "Dish " + index.ToString());
            SetSerializedBool(dish, "enabled", true);
            AppendObjectReference(template, "globalDishes", dish);
            EnsureFoodItems(template);
            SaveAndSelect(dish);
            return Success(
                dish,
                "Created a dish. Generating emits its ordinary, rare and epic versions.");
        }

        /// <summary>
        /// Creates and keeps in step every item a dish or a golden ingredient needs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A dish is three objects to the game and one asset to a creator, and a golden ingredient
        /// is a second object that shares almost everything with the first. Both are framework
        /// bookkeeping rather than decisions, so both are made here and hidden from the item lists —
        /// the same arrangement a tileset block's ground counterpart already uses.
        /// </para>
        /// <para>
        /// Run before every generate, not only when a dish is created, because the fields it copies
        /// are edited on the dish and on the ingredient. Without the re-sync a creator could rename
        /// a dish, generate, and get the old name in game with nothing to explain it.
        /// </para>
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult EnsureFoodItems(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating food items.");
            }

            CompactGlobalItems(template);

            int created = 0;
            DimensionItemAsset lastCreated = null;

            DimensionDishAsset[] dishes = template.GlobalDishes;
            for (int i = 0; i < dishes.Length; i++)
            {
                DimensionDishAsset dish = dishes[i];
                if (dish == null || !dish.Enabled || string.IsNullOrEmpty(dish.DishId))
                {
                    continue;
                }

                SyncDishTier(template, dish, 0, ref created, ref lastCreated);
                SyncDishTier(template, dish, 1, ref created, ref lastCreated);
                SyncDishTier(template, dish, 2, ref created, ref lastCreated);
            }

            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                if (item.Cooking.HasAGoldenVersion)
                {
                    SyncGoldenIngredient(template, item, ref created, ref lastCreated);
                }
            }

            AssetDatabase.SaveAssets();
            if (created == 0)
            {
                return Success(null, "Every dish and golden ingredient already has its items.");
            }

            return Success(
                lastCreated,
                "Created " + created + " food item" + (created == 1 ? string.Empty : "s") + ".");
        }

        /// <summary>
        /// One quality of one dish: created if missing, rewritten from the dish either way.
        /// </summary>
        /// <remarks>
        /// The rare and epic ids are the dish's id with "Rare" and "Epic" on the end, and that is
        /// not a naming habit — the game strips exactly those two suffixes off an object's name
        /// before looking up a dish's term, which is what lets all three qualities share one name.
        /// </remarks>
        private static void SyncDishTier(
            DimensionTemplateAsset template,
            DimensionDishAsset dish,
            int tier,
            ref int created,
            ref DimensionItemAsset lastCreated)
        {
            string itemId = tier == 0
                ? dish.DishId
                : (tier == 1 ? dish.RareItemId : dish.EpicItemId);
            DimensionItemAsset item = FindGlobalItem(template, itemId);
            if (item == null)
            {
                item = CreateAsset<DimensionItemAsset>(
                    template, "Resources", NormalizeIdToken(itemId, "Asset"));
                SetSerializedString(item, "itemId", itemId);
                AppendObjectReference(template, "globalItems", item);
                created++;
                lastCreated = item;
            }

            SetSerializedString(item, "displayName", dish.DisplayName);
            SetSerializedString(item, "description", dish.Description);
            SetSerializedEnum(item, "archetype", (int)DimensionItemArchetype.Consumable);
            SetSerializedEnum(item, "kind", (int)DimensionItemKind.BaseItem);
            // A dish stacks, like every other food. The ceiling is the game's own 9999, which every
            // stackable item shares.
            SetSerializedBool(item, "stackableWasMigrated", true);
            SetSerializedBool(item, "stackable", true);
            SetSerializedBool(item, "enabled", true);
            // Managed entirely from the dish asset, so it is kept out of the item lists — editing
            // it there would only be overwritten the next time the dish is synced.
            SetSerializedBool(item, "hidden", true);
            SetSerializedString(item, "rarityId", tier == 0 ? "Uncommon" : (tier == 1 ? "Rare" : "Epic"));
            SetSerializedObjectReference(
                item,
                "iconSprite",
                tier == 0 ? dish.BaseSprite : (tier == 1 ? dish.RareSprite : dish.EpicSprite));
            // Satisfies the generator's has-a-picture gate so a dish still being drawn generates
            // and can be walked through in game; the generator warns separately about the missing
            // sprite, which is the message that actually helps.
            SetSerializedString(item, "objectId", itemId);

            SetSerializedEnum(item, "cooking.role", (int)DimensionFoodRole.CookedDish);
            SetSerializedString(item, "cooking.rareVersion", dish.RareItemId);
            SetSerializedString(item, "cooking.epicVersion", dish.EpicItemId);

            int hunger = tier == 0 ? dish.Hunger : (tier == 1 ? dish.RareHunger : dish.EpicHunger);
            DimensionItemEffect[] extras = tier == 1
                ? dish.ExtraOnRare
                : (tier == 2 ? dish.ExtraOnEpic : new DimensionItemEffect[0]);
            WriteDishEatenEffects(item, hunger, extras);
        }

        /// <summary>
        /// The golden twin of an ingredient: everything the base has, one rarity up, aimed higher.
        /// </summary>
        /// <remarks>
        /// A GOLDEN INGREDIENT OF A MOD'S OWN CANNOT ALWAYS LEAD, and nothing here pretends
        /// otherwise. The game decides that from two hardcoded id ranges, inside Burst-compiled
        /// code no mod reaches. What a golden version DOES get is the two halves that are open: it
        /// aims at the better version of the same dish, and its Rare rarity plus a flower marker is
        /// what can push a cooked dish up to epic. Against another ingredient it leads half the
        /// time rather than always, which the combiner window says plainly.
        /// </remarks>
        private static void SyncGoldenIngredient(
            DimensionTemplateAsset template,
            DimensionItemAsset baseItem,
            ref int created,
            ref DimensionItemAsset lastCreated)
        {
            string goldenId = DimensionCookingTemplate.GoldenItemIdFor(baseItem.ItemId);
            DimensionItemAsset item = FindGlobalItem(template, goldenId);
            if (item == null)
            {
                item = CreateAsset<DimensionItemAsset>(
                    template, "Resources", NormalizeIdToken(goldenId, "Asset"));
                SetSerializedString(item, "itemId", goldenId);
                AppendObjectReference(template, "globalItems", item);
                created++;
                lastCreated = item;
            }

            DimensionCookingTemplate cooking = baseItem.Cooking;
            string name = string.IsNullOrEmpty(cooking.GoldenName)
                ? "Golden " + baseItem.DisplayName
                : cooking.GoldenName;
            SetSerializedString(item, "displayName", name);
            SetSerializedString(item, "description", baseItem.Description);
            SetSerializedEnum(item, "archetype", (int)DimensionItemArchetype.Consumable);
            SetSerializedEnum(item, "kind", (int)baseItem.Kind);
            SetSerializedBool(item, "stackableWasMigrated", true);
            SetSerializedBool(item, "stackable", baseItem.Stackable);
            SetSerializedBool(item, "enabled", true);
            SetSerializedBool(item, "hidden", true);
            SetSerializedString(item, "rarityId", "Rare");
            SetSerializedObjectReference(item, "iconSprite", baseItem.IconSprite);
            SetSerializedString(item, "objectId", goldenId);

            SetSerializedEnum(item, "cooking.role", (int)DimensionFoodRole.Ingredient);
            SetSerializedEnum(item, "cooking.ingredientKind", (int)cooking.IngredientKind);
            SetSerializedBool(item, "cooking.canBeFished", cooking.CanBeFished);
            SetSerializedBool(item, "cooking.countsAsAFlower", true);
            SetSerializedBool(
                item, "cooking.coloursFromItsOwnPicture", cooking.ColoursFromItsOwnPicture);
            SetSerializedString(item, "cooking.makesDish", ResolveGoldenDish(template, cooking));
            CopyColour(baseItem, item, "cooking.brightest");
            CopyColour(baseItem, item, "cooking.bright");
            CopyColour(baseItem, item, "cooking.dark");
            CopyColour(baseItem, item, "cooking.darkest");
            CopyEffectArray(baseItem, item, "cooking.givesRaw");
            CopyEffectArray(baseItem, item, "cooking.givesCooked");
        }

        /// <summary>
        /// Which dish a golden version aims at: the one it was told, or the better version of the
        /// dish its ordinary form makes.
        /// </summary>
        /// <remarks>
        /// The derivation only works for one of this mod's own dishes, where the better version's
        /// id is a suffix away. A golden version of an ingredient that makes one of the game's
        /// dishes has to be told which dish to aim at, because the game's rare tiers are not
        /// reachable from the ordinary one by name — CookedSoup's rare version is CookedSoupRare,
        /// which IS a suffix away, so that case works too and only an unusual pairing needs typing.
        /// </remarks>
        private static string ResolveGoldenDish(
            DimensionTemplateAsset template,
            DimensionCookingTemplate cooking)
        {
            if (!string.IsNullOrEmpty(cooking.GoldenMakesDish))
            {
                return cooking.GoldenMakesDish;
            }

            string ordinary = cooking.MakesDish;
            if (string.IsNullOrEmpty(ordinary))
            {
                return string.Empty;
            }

            return DimensionDishAsset.RareItemIdFor(ordinary);
        }

        private static void CopyColour(Object from, Object to, string path)
        {
            SerializedProperty source = new SerializedObject(from).FindProperty(path);
            if (source == null)
            {
                return;
            }

            Color value = source.colorValue;
            SetSerialized(to, path, property => property.colorValue = value);
        }

        /// <summary>
        /// Copies a list of effects between two assets, entry by entry.
        /// </summary>
        /// <remarks>
        /// Field by field rather than by copying the array wholesale: Unity's serialized-property
        /// copy shares the managed instances between the two assets, so editing one afterwards
        /// would silently edit the other.
        /// </remarks>
        private static void CopyEffectArray(Object from, Object to, string path)
        {
            SerializedProperty source = new SerializedObject(from).FindProperty(path);
            SerializedObject targetObject = new SerializedObject(to);
            SerializedProperty target = targetObject.FindProperty(path);
            if (source == null || target == null || !source.isArray || !target.isArray)
            {
                return;
            }

            target.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
            {
                SerializedProperty a = source.GetArrayElementAtIndex(i);
                SerializedProperty b = target.GetArrayElementAtIndex(i);
                b.FindPropertyRelative("effectId").stringValue =
                    a.FindPropertyRelative("effectId").stringValue;
                b.FindPropertyRelative("value").intValue =
                    a.FindPropertyRelative("value").intValue;
                b.FindPropertyRelative("valueMultiplier").floatValue =
                    a.FindPropertyRelative("valueMultiplier").floatValue;
                b.FindPropertyRelative("seconds").floatValue =
                    a.FindPropertyRelative("seconds").floatValue;
            }

            targetObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Writes a dish's own hunger and its tier bonuses onto the item, replacing what was there.
        /// </summary>
        /// <remarks>
        /// Replaced rather than appended, because this runs before every generate and appending
        /// would double the dish's hunger every time somebody pressed the button. Hunger comes
        /// first so a creator reading the item can see it without hunting.
        /// </remarks>
        private static void WriteDishEatenEffects(
            DimensionItemAsset item,
            int hunger,
            DimensionItemEffect[] extras)
        {
            SerializedObject serialized = new SerializedObject(item);
            SerializedProperty list = serialized.FindProperty("effects.whenEaten");
            if (list == null || !list.isArray)
            {
                return;
            }

            list.arraySize = 1 + extras.Length;
            WriteEffect(list.GetArrayElementAtIndex(0), "HungerAddition", hunger, 1f, 0f);
            for (int i = 0; i < extras.Length; i++)
            {
                WriteEffect(
                    list.GetArrayElementAtIndex(i + 1),
                    extras[i].EffectId,
                    extras[i].Value,
                    extras[i].ValueMultiplier,
                    extras[i].Seconds);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteEffect(
            SerializedProperty element,
            string effectId,
            int value,
            float multiplier,
            float seconds)
        {
            element.FindPropertyRelative("effectId").stringValue = effectId ?? string.Empty;
            element.FindPropertyRelative("value").intValue = value;
            element.FindPropertyRelative("valueMultiplier").floatValue = multiplier;
            element.FindPropertyRelative("seconds").floatValue = seconds;
        }
    }
}
