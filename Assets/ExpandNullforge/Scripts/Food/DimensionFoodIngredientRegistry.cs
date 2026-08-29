using System;
using System.Collections.Generic;

namespace ExpandNullforge.Food
{
    /// <summary>
    /// One ingredient a mod added, named rather than numbered.
    /// </summary>
    /// <remarks>
    /// Names, not object ids, because object ids do not exist while a mod is being built. The
    /// generator bakes a VANILLA dish target straight onto the prefab and leaves
    /// <see cref="DishObjectName"/> empty; it fills the name in only when the ingredient makes one
    /// of the mod's OWN dishes, which nothing offline can turn into a number.
    /// </remarks>
    public sealed class DimensionFoodIngredientDefinition
    {
        public DimensionFoodIngredientDefinition(
            string ingredientObjectName,
            string displayName,
            string dishObjectName)
        {
            IngredientObjectName = ingredientObjectName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            DishObjectName = dishObjectName ?? string.Empty;
        }

        /// <summary>The qualified object name of the ingredient itself.</summary>
        public string IngredientObjectName { get; }

        /// <summary>What a player calls it, used when something has to be said about it.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// The mod's own dish this ingredient makes when it leads, or empty for a vanilla dish.
        /// </summary>
        public string DishObjectName { get; }
    }

    /// <summary>
    /// Every ingredient this mod adds, filled by the generated bootstrap at load.
    /// </summary>
    /// <remarks>
    /// Two jobs, both of which need the object ids that only exist once the mod has loaded, which is
    /// why they are a registry read by a system rather than something baked into a prefab:
    /// pointing an ingredient at one of the mod's own dishes, and refusing to let an ingredient with
    /// an unpackable object id ship silently.
    /// </remarks>
    public static class DimensionFoodIngredientRegistry
    {
        private static readonly List<DimensionFoodIngredientDefinition> Definitions =
            new List<DimensionFoodIngredientDefinition>();

        public static IReadOnlyList<DimensionFoodIngredientDefinition> All
        {
            get { return Definitions; }
        }

        /// <summary>Adds one ingredient, replacing an earlier registration of the same name.</summary>
        public static void Register(DimensionFoodIngredientDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.IngredientObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].IngredientObjectName,
                        definition.IngredientObjectName,
                        StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        /// <summary>Convenience for the generated bootstrap, which writes one call per ingredient.</summary>
        public static void Register(
            string ingredientObjectName,
            string displayName,
            string dishObjectName)
        {
            Register(new DimensionFoodIngredientDefinition(
                ingredientObjectName, displayName, dishObjectName));
        }
    }

    /// <summary>
    /// One dish family a mod added, and the two better versions of it.
    /// </summary>
    /// <remarks>
    /// The links between the three qualities are ObjectIDs on the ordinary version, and every one of
    /// them belongs to this mod — so, like an ingredient's dish, they can only be filled in once the
    /// game has handed the mod its numbers.
    /// </remarks>
    public sealed class DimensionFoodDishDefinition
    {
        public DimensionFoodDishDefinition(
            string dishObjectName,
            string rareObjectName,
            string epicObjectName)
        {
            DishObjectName = dishObjectName ?? string.Empty;
            RareObjectName = rareObjectName ?? string.Empty;
            EpicObjectName = epicObjectName ?? string.Empty;
        }

        /// <summary>The qualified object name of any one of the three qualities.</summary>
        public string DishObjectName { get; }

        /// <summary>The rare version of the family this object belongs to.</summary>
        public string RareObjectName { get; }

        /// <summary>The epic version of the family this object belongs to.</summary>
        public string EpicObjectName { get; }
    }

    /// <summary>Every dish family this mod adds, filled by the generated bootstrap at load.</summary>
    public static class DimensionFoodDishRegistry
    {
        private static readonly List<DimensionFoodDishDefinition> Definitions =
            new List<DimensionFoodDishDefinition>();

        public static IReadOnlyList<DimensionFoodDishDefinition> All
        {
            get { return Definitions; }
        }

        public static void Register(DimensionFoodDishDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.DishObjectName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(
                        Definitions[i].DishObjectName,
                        definition.DishObjectName,
                        StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        /// <summary>Convenience for the generated bootstrap, one call per quality of each dish.</summary>
        public static void Register(
            string dishObjectName,
            string rareObjectName,
            string epicObjectName)
        {
            Register(new DimensionFoodDishDefinition(
                dishObjectName, rareObjectName, epicObjectName));
        }
    }
}
