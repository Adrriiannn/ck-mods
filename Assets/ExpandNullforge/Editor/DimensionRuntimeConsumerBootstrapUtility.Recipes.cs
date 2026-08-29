using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using PugMod;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the crafting registrations that put an authored recipe on a station in the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHERE A RECIPE ACTUALLY LIVES. Core Keeper splits a recipe in two, and the split is the
    /// reason this file is careful. The COST — what goes in — is stored on the produced item
    /// (<c>ObjectInfo.requiredObjectsToCraft</c>, baked into the object database blob), so the item
    /// generator stamps it while it writes the item's own prefab. The AVAILABILITY — which bench
    /// offers it — is a row in that bench's <c>CanCraftObjectsBuffer</c>, which only exists once
    /// the world is converting. This emitter writes the availability half; the injector
    /// (<c>DimensionPortalRecipeInjector</c>) places it when the bench's entity appears.
    /// </para>
    /// <para>
    /// BY HAND IS A STATION. Core Keeper has no separate hand-crafting list: the player entity is
    /// itself a crafting station (<c>ObjectID.Player</c>, six recipes on the game's own player
    /// prefab — Wood Pickaxe and friends). So a recipe that names no bench is registered against
    /// the player, and the same injector that fills a workbench fills the player. Before this, a
    /// blank bench meant the recipe was quietly registered nowhere at all.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>
        /// Makes an authored recipe's output appear at the crafting station the recipe names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The ingredients half of a recipe was already generated — <c>DimensionItemGenerator</c>
        /// writes them onto the item's own <c>InventoryItemAuthoring</c>, exactly like vanilla. What
        /// had no runtime at all was the STATION: <c>craftingStationId</c> was authored in the
        /// dashboard and read by nothing, so a recipe could be fully filled in and still never appear
        /// anywhere a player could reach it.
        /// </para>
        /// <para>
        /// The station is resolved HERE, at generation time, rather than at runtime. It names a
        /// vanilla object, so it is an <c>ObjectID</c> enum member that the editor assembly can parse
        /// and emit as a compile-time constant — which means a typo is reported to the creator while
        /// they are still in the dashboard, instead of becoming a recipe that silently never shows
        /// up in game.
        /// </para>
        /// <para>
        /// The crafted object name must be the QUALIFIED one, because that is what the generated
        /// object carries and what <c>API.Authoring.GetObjectID</c> resolves. Qualification is
        /// idempotent, so an id that already contains the mod prefix — every tileset block id does,
        /// since it is built from the tileset's own <c>{mod}:{name}</c> identity — passes through
        /// unchanged.
        /// </para>
        /// </remarks>
        internal static void AppendRecipeCraftingRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionRecipeAsset[] recipes = template == null ? null : template.GlobalRecipes;
            if (recipes == null)
            {
                return;
            }

            int handCraftedCount = 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null || !recipe.Enabled || string.IsNullOrEmpty(recipe.OutputItemId))
                {
                    continue;
                }

                bool ownOutput = IsModOwnedObjectName(template, recipe.OutputItemId);

                // A NAME THAT IS NEITHER IS NOT "one of the game's own items". The check below used
                // to treat everything that was not ours as vanilla, so a misspelled output was told
                // its cost could not be changed because it belonged to the game — sending the
                // author to look at the wrong thing entirely.
                if (!ownOutput &&
                    DimensionObjectBinder.Vanilla(recipe.OutputItemId) == ObjectID.None)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Recipe '" + recipe.RecipeId + "' makes '" +
                        recipe.OutputItemId + "', which is neither one of this mod's items nor one " +
                        "the game has, so it can never be crafted. Check the spelling, or that " +
                        "the item is still switched on. This recipe was not registered.");
                    continue;
                }

                if (!ownOutput && HasAnyIngredient(recipe))
                {
                    // The cost lives on the produced item, and this recipe produces one of the
                    // game's. Registering it anyway would put the recipe on the bench at Core
                    // Keeper's price while the dashboard showed the author's — a lie that only
                    // surfaces when a player counts their materials.
                    Debug.LogWarning(
                        "[ExpandNullforge] Recipe '" + recipe.RecipeId + "' makes '" +
                        recipe.OutputItemId + "', which is one of the game's own items, and also " +
                        "lists ingredients. Core Keeper keeps what a craft costs on the item " +
                        "itself, so the cost of one of its items cannot be changed. Clear the " +
                        "ingredients to offer it at the game's own price, or make your own item " +
                        "and put the ingredients on that. This recipe was not registered.");
                    continue;
                }

                string station = recipe.CraftingStationId;
                string stationArgument;
                if (!TryResolveRecipeStationArgument(template, modName, recipe, station, out stationArgument))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(station))
                {
                    handCraftedCount++;
                    if (handCraftedCount == DimensionRecipeAsset.HandCraftingRecipeLimit + 1)
                    {
                        Debug.LogWarning(
                            "[ExpandNullforge] More than " + DimensionRecipeAsset.HandCraftingRecipeLimit +
                            " recipes are made by hand. The game's by-hand list only has room for " +
                            DimensionRecipeAsset.HandCraftingRecipeLimit + " more than its own six, and it cannot be " +
                            "split into pages the way a Workbench can, so the ones past that show " +
                            "up as nothing. Name a Workbench on the extra recipes.");
                    }
                }

                // Only the mod's own outputs are qualified. A recipe may name one of the game's
                // items — offering IronBar at your own bench is an ordinary thing to want — and
                // qualifying that name points the registration at an item that does not exist.
                string craftedName = ownOutput
                    ? DimensionObjectNamespace.Qualify(modName, recipe.OutputItemId)
                    : recipe.OutputItemId;

                builder.AppendLine("    DimensionCraftingRegistry.Register(");
                builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                builder.Append("            ").Append(ToCSharpString(craftedName)).AppendLine(",");
                builder.Append("            ").Append(stationArgument).AppendLine(",");
                builder.Append("            ")
                    .Append(recipe.OutputAmount.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(",");
                builder.Append("            ")
                    .Append(recipe.CraftTimeSeconds.ToString(CultureInfo.InvariantCulture))
                    .AppendLine("f,");
                builder.Append("            ").Append(ToCSharpString(recipe.DisplayName)).AppendLine("));");
            }

            AppendWorkbenchOwnRecipeRegistrations(builder, template, modName);
            AppendCraftingBenchLookRegistrations(builder, template, modName);
        }

        /// <summary>
        /// Registers the recipes a Workbench lists on itself, by name, at that Workbench.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY A SECOND WALK, when the loop above already covers a recipe that names its station.
        /// A Workbench also carries its own list, and a recipe can be in that list without naming
        /// the bench back — so the walk above sees it as hand-crafted, or not at all.
        /// </para>
        /// <para>
        /// AND WHY BY NAME RATHER THAN BAKED. The Workbench generator used to write the output into
        /// <c>CraftingAuthoring.moddedObjectID</c>, which Core Keeper resolves DURING conversion
        /// (<c>ck-db\Pug.ECS.Conversion\InventoryConverter.cs</c> <c>AddRecipe</c>) through a lookup
        /// filled one object at a time. Mod prefabs convert in <c>ExtraAuthoring</c> order, which
        /// <c>ModAPIAuthoring</c> keeps sorted by a hash of the prefab name — so whether a bench
        /// converted before or after the item it makes was a coin flip that could change when a
        /// prefab was renamed. Lose it and the bench baked <c>ObjectID.None</c> into its recipe
        /// list, permanently, with no log. Registered here instead, the injector adds the row when
        /// the bench's entity appears, by which time every name resolves.
        /// </para>
        /// <para>
        /// Registering the same station and output twice is harmless: the registry replaces on that
        /// pair, and the injector skips a recipe the buffer already holds.
        /// </para>
        /// </remarks>
        private static void AppendWorkbenchOwnRecipeRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionWorkbenchAsset[] workbenches =
                template == null ? null : template.GlobalWorkbenches;
            for (int w = 0; workbenches != null && w < workbenches.Length; w++)
            {
                DimensionWorkbenchAsset workbench = workbenches[w];
                if (workbench == null || !workbench.Enabled ||
                    string.IsNullOrEmpty(workbench.WorkbenchId))
                {
                    continue;
                }

                // A BENCH THAT BORROWS A STATION HAS NO PREFAB TO BAKE ITS RECIPES ONTO, and it used
                // to be skipped here as well — so a workbench that named an existing station, which
                // DimensionWorkbenchAsset documents as supported, registered its recipes nowhere,
                // was warned about nowhere, and appeared nowhere. Its station is the object it
                // named, resolved at load like any other name.
                bool makesItsOwnStation = workbench.GeneratesItsOwnObject;
                string stationName = makesItsOwnStation
                    ? DimensionObjectNamespace.Qualify(modName, workbench.WorkbenchId)
                    : (IsModOwnedObjectName(template, workbench.ObjectId)
                        ? DimensionObjectNamespace.Qualify(modName, workbench.ObjectId)
                        : workbench.ObjectId);

                if (string.IsNullOrEmpty(stationName))
                {
                    // Nowhere to show them. The generator says so.
                    continue;
                }

                string stationArgument = ToCSharpString(stationName);

                DimensionRecipeAsset[] recipes = workbench.Recipes;
                for (int i = 0; recipes != null && i < recipes.Length; i++)
                {
                    DimensionRecipeAsset recipe = recipes[i];
                    if (recipe == null || !recipe.Enabled ||
                        string.IsNullOrEmpty(recipe.OutputItemId))
                    {
                        continue;
                    }

                    // On a station of this mod's own, one of the game's outputs is a plain ObjectID
                    // the bench bakes itself — there is no name to resolve and nothing to race. On
                    // a borrowed station there is no bake at all, so every output has to come this
                    // way, the game's own included.
                    if (makesItsOwnStation && !IsModOwnedObjectName(template, recipe.OutputItemId))
                    {
                        continue;
                    }

                    // Qualified only when it IS one of this mod's. A borrowed station may offer a
                    // recipe for one of the game's own items, and a mod prefix on that name would
                    // resolve to nothing at load.
                    string outputName = IsModOwnedObjectName(template, recipe.OutputItemId)
                        ? DimensionObjectNamespace.Qualify(modName, recipe.OutputItemId)
                        : recipe.OutputItemId;

                    builder.AppendLine("    DimensionCraftingRegistry.Register(");
                    builder.AppendLine("        new DimensionCraftingRecipeDefinition(");
                    builder.Append("            ")
                        .Append(ToCSharpString(outputName))
                        .AppendLine(",");
                    builder.Append("            ").Append(stationArgument).AppendLine(",");
                    builder.Append("            ")
                        .Append(recipe.OutputAmount.ToString(CultureInfo.InvariantCulture))
                        .AppendLine(",");
                    builder.Append("            ")
                        .Append(recipe.CraftTimeSeconds.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("f,");
                    builder.Append("            ")
                        .Append(ToCSharpString(recipe.DisplayName)).AppendLine("));");
                }
            }
        }

        /// <summary>
        /// Writes down which crafting window each generated station opens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A LOOK IS PART OF A STATION, WHICH IS WHY IT SHIPS HERE. The rest of this file emits the
        /// half of a crafting station that only exists once the game is running — which recipes it
        /// offers — and its window's look is the same kind of fact for the same reason: the object
        /// has no number in the editor, so the answer travels as a name and is matched up in the
        /// running game. Emitting it from the recipe pass also means it is reachable the moment
        /// recipes are, rather than waiting on a call line in a file this domain must not edit.
        /// </para>
        /// <para>
        /// ONLY STATIONS THAT ACTUALLY OPEN A WINDOW ARE WRITTEN. A Workbench that generates no
        /// object of its own is a grouping device for recipes on somebody else's bench, and a world
        /// object that is not used as a crafting bench never builds a
        /// <c>DimensionCraftingBenchView</c> at all — a row for either would sit in the table
        /// forever, matching nothing.
        /// </para>
        /// <para>
        /// The default look is skipped rather than written. Wood is what both the registry and the
        /// game itself fall back to, so a row saying "wood" is a row that changes nothing, and the
        /// generated bootstrap is easier to read without one per station.
        /// </para>
        /// </remarks>
        private static void AppendCraftingBenchLookRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            DimensionWorkbenchAsset[] workbenches = template.GlobalWorkbenches;
            if (workbenches != null)
            {
                for (int i = 0; i < workbenches.Length; i++)
                {
                    DimensionWorkbenchAsset workbench = workbenches[i];
                    if (workbench == null || !workbench.Enabled ||
                        !workbench.GeneratesItsOwnObject ||
                        string.IsNullOrEmpty(workbench.WorkbenchId))
                    {
                        continue;
                    }

                    AppendOneCraftingBenchLook(
                        builder,
                        DimensionObjectNamespace.Qualify(modName, workbench.WorkbenchId),
                        workbench.Interaction.CraftingWindowLook);
                }
            }

            DimensionWorldObjectAsset[] worldObjects = template.GlobalWorldObjects;
            if (worldObjects == null)
            {
                return;
            }

            for (int i = 0; i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null || !worldObject.Enabled ||
                    string.IsNullOrEmpty(worldObject.ObjectIdentifier) ||
                    worldObject.Interaction.WhatUsingItDoes !=
                        DimensionUseBehaviour.OpensACraftingBench)
                {
                    continue;
                }

                AppendOneCraftingBenchLook(
                    builder,
                    DimensionObjectNamespace.Qualify(modName, worldObject.ObjectIdentifier),
                    worldObject.Interaction.CraftingWindowLook);
            }
        }

        /// <summary>Writes one station's window look, when it is not the one nobody has to ask for.</summary>
        private static void AppendOneCraftingBenchLook(
            StringBuilder builder,
            string objectName,
            DimensionCraftingWindowLook look)
        {
            if (look == DimensionCraftingWindowLook.Wooden || string.IsNullOrEmpty(objectName))
            {
                return;
            }

            // Written out in full rather than short: the generated file's using list is fixed by
            // the writer in the main partial, which this domain must not edit, and it does not
            // carry ExpandNullforge.Objects. A qualified call needs nothing from it.
            builder.Append("    ExpandNullforge.Objects.DimensionCraftingBenchLookRegistry.Register(")
                .Append(ToCSharpString(objectName))
                .Append(", ")
                .Append(((int)look).ToString(CultureInfo.InvariantCulture))
                .AppendLine(");");
        }

        /// <summary>
        /// The station argument for one recipe's registration, or false when the recipe names a
        /// station nothing can resolve and must therefore not be registered at all.
        /// </summary>
        /// <remarks>
        /// Three shapes, because a station is not always one of the game's. Blank is the player,
        /// which is what "made by hand" is in Core Keeper. A name the game's object list knows is
        /// baked to that number here, where it can be proven. One of this mod's own Workbenches is
        /// emitted as its QUALIFIED OBJECT NAME for the runtime to resolve — a mod object has no
        /// number until the game hands it one while the world converts, so there is nothing to bake.
        /// Anything else is a typo, and is refused rather than dropped onto some default bench:
        /// a recipe that quietly appeared at the wrong station is far harder to notice than one
        /// that has not appeared yet.
        /// </remarks>
        private static bool TryResolveRecipeStationArgument(
            DimensionTemplateAsset template,
            string modName,
            DimensionRecipeAsset recipe,
            string station,
            out string stationArgument)
        {
            if (string.IsNullOrEmpty(station))
            {
                stationArgument = "ObjectID.Player";
                return true;
            }

            ObjectID vanillaStation;
            if (System.Enum.TryParse(station, false, out vanillaStation) &&
                vanillaStation != ObjectID.None)
            {
                stationArgument = "ObjectID." + vanillaStation;
                return true;
            }

            DimensionWorkbenchAsset workbench = DimensionWorkbenchAsset.FindByStationId(
                template == null ? null : template.GlobalWorkbenches,
                station);
            if (workbench != null)
            {
                if (!workbench.GeneratesItsOwnObject)
                {
                    Debug.LogWarning(
                        "[ExpandNullforge] Recipe '" + recipe.RecipeId + "' is made at your " +
                        "Workbench '" + station + "', which is set not to generate its own object, " +
                        "so there is nothing in the world to craft at. Turn on \"Generates its own " +
                        "object\" on that Workbench, or name one of the game's stations instead.");
                    stationArgument = null;
                    return false;
                }

                stationArgument = ToCSharpString(
                    DimensionObjectNamespace.Qualify(modName, workbench.WorkbenchId));
                return true;
            }

            Debug.LogWarning(
                "[ExpandNullforge] Recipe '" + recipe.RecipeId + "' is made at '" + station +
                "', which is neither one of the game's stations nor one of your Workbenches, so it " +
                "was not registered anywhere. Leave the Workbench empty to have it made by hand, " +
                "name one of your own Workbenches, or use one of the game's object names such as " +
                "WoodenWorkBench or CopperWorkBench.");
            stationArgument = null;
            return false;
        }

        /// <summary>True when the recipe names at least one ingredient the creator meant to cost.</summary>
        private static bool HasAnyIngredient(DimensionRecipeAsset recipe)
        {
            DimensionRecipeIngredientTemplate[] ingredients =
                recipe == null ? null : recipe.Ingredients;
            if (ingredients == null)
            {
                return false;
            }

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (ingredients[i] != null && !string.IsNullOrEmpty(ingredients[i].ItemId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
