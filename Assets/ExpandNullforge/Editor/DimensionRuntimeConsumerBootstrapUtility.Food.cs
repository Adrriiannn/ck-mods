using System.Text;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of cooking that only exists once the game has handed the mod its numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY ANY OF THIS IS NEEDED, given that a custom ingredient otherwise needs no runtime code at
    /// all. Two of the values cooking reads are ObjectIDs pointing at the mod's OWN objects — the
    /// dish an ingredient makes, and the two better versions a dish upgrades into — and a mod's
    /// object ids are handed out while the game loads. The generator bakes those pointers when they
    /// name one of the game's own objects and leaves them empty otherwise; these registrations are
    /// what the runtime uses to fill the empty ones in.
    /// </para>
    /// <para>
    /// The ingredient rows carry one more job. An ingredient whose final object id lands above
    /// 65535 is silently corrupted — a dish only has sixteen bits to remember each of its two
    /// ingredients — so the same list is what the runtime walks to check the ids it was actually
    /// given and complain by name if one of them will not fit.
    /// </para>
    /// <para>
    /// Golden versions are registered like any other ingredient. They are separate objects with
    /// separate ids and a dish target of their own, and the framework has no way to make the game
    /// treat them as always-leading — that answer is two hardcoded id ranges inside Burst-compiled
    /// code. Registering them is the whole of what can honestly be done.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes one row per ingredient and per dish this mod adds.</summary>
        internal static void AppendFoodRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            AppendIngredientRegistrations(builder, template, modName);
            AppendDishRegistrations(builder, template, modName);
        }

        private static void AppendIngredientRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                DimensionCookingTemplate cooking = item.Cooking;
                if (!cooking.IsAnIngredient)
                {
                    continue;
                }

                AppendOneIngredient(
                    builder,
                    modName,
                    item.ItemId,
                    item.DisplayName,
                    cooking.MakesDish,
                    template);

                // The golden twin is a second object with a second id and a dish of its own, so it
                // needs a row of its own — the runtime looks these up by name, and the twin's name
                // is not the base ingredient's.
                if (cooking.HasAGoldenVersion)
                {
                    string goldenDish = string.IsNullOrEmpty(cooking.GoldenMakesDish)
                        ? DimensionDishAsset.RareItemIdFor(cooking.MakesDish)
                        : cooking.GoldenMakesDish;
                    AppendOneIngredient(
                        builder,
                        modName,
                        DimensionCookingTemplate.GoldenItemIdFor(item.ItemId),
                        string.IsNullOrEmpty(cooking.GoldenName)
                            ? "Golden " + item.DisplayName
                            : cooking.GoldenName,
                        goldenDish,
                        template);
                }
            }
        }

        /// <summary>
        /// One ingredient's row. The dish name is left empty when the generator already baked it.
        /// </summary>
        /// <remarks>
        /// An empty dish name is not "no dish" — it is "the dish is one of the game's own and is
        /// already sitting on the prefab as a number". Writing the name anyway would make the
        /// runtime look up something it does not need and would hide, behind a busy log, the case
        /// where a name really is unresolvable.
        /// </remarks>
        private static void AppendOneIngredient(
            StringBuilder builder,
            string modName,
            string ingredientId,
            string displayName,
            string dishId,
            DimensionTemplateAsset template)
        {
            string dishObjectName = NamesOneOfOurDishes(template, dishId)
                ? DimensionObjectNamespace.Qualify(modName, dishId)
                : string.Empty;

            builder.AppendLine("    DimensionFoodIngredientRegistry.Register(");
            builder.Append("        ")
                .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, ingredientId)))
                .AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(displayName)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(dishObjectName)).AppendLine(");");
        }

        private static void AppendDishRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionDishAsset[] dishes = template.GlobalDishes;
            for (int i = 0; dishes != null && i < dishes.Length; i++)
            {
                DimensionDishAsset dish = dishes[i];
                if (dish == null || !dish.Enabled || string.IsNullOrEmpty(dish.DishId))
                {
                    continue;
                }

                string rare = DimensionObjectNamespace.Qualify(modName, dish.RareItemId);
                string epic = DimensionObjectNamespace.Qualify(modName, dish.EpicItemId);

                // All three qualities carry the same pair of links, which is how the game authors
                // its own: whichever quality a player is holding, the cooking-skill bonus has to be
                // able to find the one above it.
                AppendOneDish(builder, DimensionObjectNamespace.Qualify(modName, dish.DishId), rare, epic);
                AppendOneDish(builder, rare, rare, epic);
                AppendOneDish(builder, epic, rare, epic);
            }
        }

        private static void AppendOneDish(
            StringBuilder builder,
            string dishObjectName,
            string rareObjectName,
            string epicObjectName)
        {
            builder.AppendLine("    DimensionFoodDishRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(dishObjectName)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(rareObjectName)).AppendLine(",");
            builder.Append("        ").Append(ToCSharpString(epicObjectName)).AppendLine(");");
        }

        /// <summary>Whether a dish id names one of this mod's dishes rather than one of the game's.</summary>
        private static bool NamesOneOfOurDishes(DimensionTemplateAsset template, string dishId)
        {
            if (template == null || string.IsNullOrEmpty(dishId))
            {
                return false;
            }

            DimensionDishAsset[] dishes = template.GlobalDishes;
            for (int i = 0; dishes != null && i < dishes.Length; i++)
            {
                DimensionDishAsset dish = dishes[i];
                if (dish == null)
                {
                    continue;
                }

                if (string.Equals(dish.DishId, dishId, System.StringComparison.Ordinal) ||
                    string.Equals(dish.RareItemId, dishId, System.StringComparison.Ordinal) ||
                    string.Equals(dish.EpicItemId, dishId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
