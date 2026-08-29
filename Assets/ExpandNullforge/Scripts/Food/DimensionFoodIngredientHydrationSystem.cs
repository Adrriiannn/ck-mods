using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugMod;
using Unity.Entities;

namespace ExpandNullforge.Food
{
    /// <summary>
    /// Points a mod's ingredients at the mod's own dishes, and refuses to let an unpackable
    /// ingredient ship in silence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT BAKE THIS. An ingredient's <c>turnsIntoFood</c> is an ObjectID, and
    /// a mod's object ids are handed out while the game loads — the editor has none of them. A
    /// vanilla dish target is a known number and IS baked; one of the mod's own dishes can only be
    /// resolved here, once.
    /// </para>
    /// <para>
    /// IT IS THE PREFAB ENTITY THAT MATTERS, not any spawned copy. The pot reads
    /// <c>ingredientLookup[primaryPrefabEntity].turnsIntoFood</c> straight off the database's prefab
    /// entity for the leading ingredient, so that is the entity this writes to. Writing to a
    /// spawned item instead would change nothing a player could ever see.
    /// </para>
    /// <para>
    /// THE ID GUARD IS THE SECOND HALF, and it lives here for the same reason: an ingredient's final
    /// object id is not knowable until now. A dish stores its two ingredients as
    /// <c>(primary &lt;&lt; 16) | secondary</c> and reads them back through a 16-bit mask, so an
    /// ingredient numbered above 65535 is silently truncated — the dish is tinted from the wrong
    /// ingredient, buffed by the wrong ingredient, and named after the wrong ingredient, with no
    /// error anywhere. Mod ids start at 32768, so this only happens in a load order carrying tens of
    /// thousands of modded objects; when it does happen it is reported as an error naming the
    /// ingredient and the fix, because a quiet wrong answer is worse than a loud one.
    /// </para>
    /// <para>
    /// Runs in every world with a database rather than server-only: the pot's ghost preview and the
    /// cook book are client-side and read the same prefab component, so an ingredient hydrated only
    /// on the server would show the player one dish and hand them another.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionFoodIngredientHydrationSystem : SystemBase
    {
        private EntityQuery databaseQuery;
        private readonly HashSet<string> settled = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> complained = new HashSet<string>(System.StringComparer.Ordinal);

        protected override void OnCreate()
        {
            databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            RequireForUpdate(databaseQuery);
        }

        protected override void OnUpdate()
        {
            IReadOnlyList<DimensionFoodIngredientDefinition> ingredients =
                DimensionFoodIngredientRegistry.All;
            IReadOnlyList<DimensionFoodDishDefinition> dishes = DimensionFoodDishRegistry.All;
            if (ingredients.Count + dishes.Count == 0 ||
                settled.Count >= ingredients.Count + dishes.Count)
            {
                return;
            }

            PugDatabase.DatabaseBankCD bank =
                databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();

            HydrateIngredients(ingredients, bank);
            HydrateDishes(dishes, bank);
        }

        private void HydrateIngredients(
            IReadOnlyList<DimensionFoodIngredientDefinition> definitions,
            PugDatabase.DatabaseBankCD bank)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                DimensionFoodIngredientDefinition definition = definitions[i];
                if (settled.Contains(definition.IngredientObjectName))
                {
                    continue;
                }

                ObjectID ingredientId = API.Authoring.GetObjectID(definition.IngredientObjectName);
                if (ingredientId == ObjectID.None)
                {
                    // The object has not registered yet. Say nothing and try again next tick —
                    // registration order is not something a mod can depend on.
                    continue;
                }

                CheckItFitsInAVariation(definition, (int)ingredientId);

                if (string.IsNullOrEmpty(definition.DishObjectName))
                {
                    // A vanilla dish target was baked onto the prefab at generate; nothing to do.
                    settled.Add(definition.IngredientObjectName);
                    continue;
                }

                ObjectID dishId = API.Authoring.GetObjectID(definition.DishObjectName);
                if (dishId == ObjectID.None)
                {
                    continue;
                }

                if (PointAtTheDish(ingredientId, dishId, bank))
                {
                    settled.Add(definition.IngredientObjectName);
                }
            }
        }

        /// <summary>
        /// Links each of a dish's three qualities to the better two, on the prefab the game reads.
        /// </summary>
        /// <remarks>
        /// All three qualities carry the same pair of links, which is how vanilla authors them:
        /// whichever one a player is holding, the cooking-skill bonus can find the version above it.
        /// A quality whose links are already right is left alone, so a save reloaded in the same
        /// session does not rewrite anything.
        /// </remarks>
        private void HydrateDishes(
            IReadOnlyList<DimensionFoodDishDefinition> definitions,
            PugDatabase.DatabaseBankCD bank)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                DimensionFoodDishDefinition definition = definitions[i];
                if (settled.Contains(definition.DishObjectName))
                {
                    continue;
                }

                ObjectID dishId = API.Authoring.GetObjectID(definition.DishObjectName);
                if (dishId == ObjectID.None)
                {
                    continue;
                }

                ObjectID rareId = API.Authoring.GetObjectID(definition.RareObjectName);
                ObjectID epicId = API.Authoring.GetObjectID(definition.EpicObjectName);
                if (rareId == ObjectID.None || epicId == ObjectID.None)
                {
                    continue;
                }

                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(dishId, bank.databaseBankBlob, 0);
                if (prefab == Entity.Null || !EntityManager.Exists(prefab))
                {
                    continue;
                }

                if (!EntityManager.HasComponent<CookedFoodCD>(prefab))
                {
                    Complain(
                        "'" + definition.DishObjectName + "' is registered as a dish but its " +
                        "object carries no cooked-food data, so the pot can never produce it and " +
                        "the cook book will never list it. Set its part in cooking to Cooked dish " +
                        "on the item and generate again.");
                    settled.Add(definition.DishObjectName);
                    continue;
                }

                CookedFoodCD cooked = EntityManager.GetComponentData<CookedFoodCD>(prefab);
                if (cooked.rareVersion != rareId || cooked.epicVersion != epicId)
                {
                    cooked.rareVersion = rareId;
                    cooked.epicVersion = epicId;
                    EntityManager.SetComponentData(prefab, cooked);
                }

                settled.Add(definition.DishObjectName);
            }
        }

        /// <summary>
        /// Writes the dish onto the ingredient's prefab entity, which is what the pot reads.
        /// </summary>
        /// <returns>
        /// True once the write has landed, or once it is certain it never can — either way the
        /// ingredient is done and must not be looked at again every tick for the rest of the run.
        /// </returns>
        private bool PointAtTheDish(
            ObjectID ingredientId,
            ObjectID dishId,
            PugDatabase.DatabaseBankCD bank)
        {
            Entity prefab = PugDatabase.GetPrimaryPrefabEntity(
                ingredientId, bank.databaseBankBlob, 0);
            if (prefab == Entity.Null || !EntityManager.Exists(prefab))
            {
                return false;
            }

            if (!EntityManager.HasComponent<CookingIngredientCD>(prefab))
            {
                // Generated without the ingredient component. Nothing here can repair that, and
                // repeating the complaint every tick would bury the console.
                Complain(
                    "'" + ingredientId + "' is registered as an ingredient but its object carries " +
                    "no cooking data, so the pot will refuse it. Set its part in cooking to " +
                    "Ingredient on the item and generate again.");
                return true;
            }

            CookingIngredientCD ingredient =
                EntityManager.GetComponentData<CookingIngredientCD>(prefab);
            if (ingredient.turnsIntoFood == dishId)
            {
                return true;
            }

            ingredient.turnsIntoFood = dishId;
            EntityManager.SetComponentData(prefab, ingredient);
            return true;
        }

        private void CheckItFitsInAVariation(
            DimensionFoodIngredientDefinition definition,
            int ingredientId)
        {
            if (DimensionFoodPairing.FitsInAVariation(ingredientId))
            {
                return;
            }

            string name = string.IsNullOrEmpty(definition.DisplayName)
                ? definition.IngredientObjectName
                : definition.DisplayName;
            Complain(
                "'" + name + "' was given object id " + ingredientId + ", which is above the " +
                DimensionFoodPairing.MaximumPackableObjectId + " a dish can remember. Any dish " +
                "cooked from it will be tinted, buffed and named after a different ingredient, " +
                "with nothing else to show for it. Remove some mods from this load order, or " +
                "move this one earlier in it, so that fewer than " +
                (DimensionFoodPairing.MaximumPackableObjectId - 32767) +
                " modded objects are registered before this ingredient.");
        }

        /// <summary>Says a thing once. The same complaint every tick would bury everything else.</summary>
        private void Complain(string message)
        {
            if (complained.Add(message))
            {
                DimensionFrameworkLog.Error(message);
            }
        }
    }
}
