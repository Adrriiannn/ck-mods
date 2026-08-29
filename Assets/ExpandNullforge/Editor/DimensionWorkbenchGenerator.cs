using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating workbenches did.</summary>
    internal sealed class DimensionWorkbenchGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Workbenches: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns a workbench into a crafting station that actually crafts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT WAS MISSING. A workbench asset could already group recipes and point them at an object,
    /// but nothing ever wrote <c>CraftingAuthoring</c> — so a modder could describe a station and
    /// never make one. The recipes had to land on a vanilla bench, and a custom bench was a
    /// decoration.
    /// </para>
    /// <para>
    /// The component set is vanilla's own, read from <c>AlchemyTableEntity</c> and its 84 siblings:
    /// <c>CraftingAuthoring</c> on top of exactly the spine every placed object has — mineable,
    /// health, damage reduction, placement, animation, rotation and the four state components.
    /// </para>
    /// <para>
    /// The recipe list is <c>canCraftObjects</c>, and it names WHAT the station can make, not how.
    /// Ingredients live on the crafted object itself (<c>requiredObjectsToCraft</c>), which is why a
    /// recipe asset's ingredients are written elsewhere and only its output is needed here.
    /// </para>
    /// </remarks>
    internal static class DimensionWorkbenchGenerator
    {
        /// <summary>The folder inside a mod where generated station prefabs live.</summary>
        public const string FolderName = "Workbenches";

        /// <summary>Damage a single hit may do, matching the rest of the framework.</summary>
        private const int DamagePerHit = 1;

        public static DimensionWorkbenchGenerationReport Generate(
            IEnumerable<DimensionWorkbenchAsset> workbenches,
            string outputFolder,
            DimensionNamingContext naming)
        {
            DimensionWorkbenchGenerationReport report = new DimensionWorkbenchGenerationReport();
            if (workbenches == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no workbenches were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionWorkbenchAsset workbench in workbenches)
                {
                    GenerateOne(workbench, outputFolder, naming, report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static void GenerateOne(
            DimensionWorkbenchAsset workbench,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionWorkbenchGenerationReport report)
        {
            if (workbench == null || !workbench.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(workbench.WorkbenchId))
            {
                report.Skipped.Add("A workbench with no id was skipped.");
                return;
            }

            if (workbench.HasNowhereToShowRecipes)
            {
                report.Warnings.Add(
                    "'" + workbench.DisplayName + "' does not generate its own station and names no " +
                    "existing object either, so its recipes have nowhere to appear.");
            }

            // CHECKED BEFORE THE EARLY RETURN, and it was not. A bench that groups its recipes onto
            // a station somebody else made never reached this line, so a misspelled output on such
            // a bench was silent here as well as at the bootstrap — the one shape this generator
            // could not say anything about at all.
            int usable = CountUsableRecipes(workbench, naming, report);

            // Grouping recipes onto a station somebody else made is a legitimate answer, and it
            // needs no prefab of ours. Its recipes are registered against the named station at load
            // (AppendWorkbenchOwnRecipeRegistrations), which is the half that used to be missing.
            if (!workbench.GeneratesItsOwnObject)
            {
                if (usable == 0 && !workbench.HasNowhereToShowRecipes)
                {
                    report.Warnings.Add(
                        "'" + workbench.DisplayName + "' puts its recipes on '" + workbench.ObjectId +
                        "' but has none that can be made, so nothing new appears at that station.");
                }

                return;
            }

            if (usable == 0)
            {
                report.Warnings.Add(
                    "'" + workbench.DisplayName + "' is a crafting station that can craft nothing. It " +
                    "will place and open an empty window.");
            }

            // A station's window is opened by the body a player walks up to, and that body only
            // gets built when the interaction says so — DimensionInteractionVisualUtility.Apply
            // draws a picture for anything with a sprite but wires no use for "Nothing", and with
            // no use there is no LocalInteractableAuthoring and no crafting view. So a Workbench
            // left at the default places a picture that cannot be used, which reads exactly like a
            // bug in the game rather than an unanswered question in the editor.
            if (workbench.Interaction.WhatUsingItDoes != DimensionUseBehaviour.OpensACraftingBench)
            {
                report.Warnings.Add(
                    "'" + workbench.DisplayName + "' does not say that using it opens a crafting " +
                    "bench, so it will stand there and do nothing when a player uses it. Set What " +
                    "using it does to \"Opens a crafting bench\" on this station.");
            }

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(workbench.WorkbenchId, "Workbench") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(workbench.WorkbenchId);

            try
            {
                Configure(root, workbench, naming, report);
                // drawsItself: a station is a thing a player looks at as much as a thing they open,
                // and until this argument existed a generated station had no renderer anywhere —
                // it opened its recipe window from a square of nothing.
                DimensionInteractionVisualUtility.Apply(
                    root,
                    workbench.Interaction,
                    outputFolder,
                    DimensionGeneratedPrefabUtility.SanitizeAuthoredName(workbench.WorkbenchId, "Workbench"),
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + workbench.DisplayName + "' " + message);
                    },
                    true,
                    workbench.Sprite);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

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
                    workbench.WorkbenchId + " failed to generate: " + exception.Message + "\n" +
                    exception.StackTrace);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            DimensionWorkbenchAsset workbench,
            DimensionNamingContext naming,
            DimensionWorkbenchGenerationReport report)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(workbench.WorkbenchId);
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;

            Rarity rarity;
            if (!string.IsNullOrEmpty(workbench.RarityId) &&
                Enum.TryParse(workbench.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            ApplyArt(root, workbench, report);
            ApplyCrafting(root, workbench, naming, report);
            ApplyBreaking(root, workbench);

            EnsureComponent<PlaceableObjectAuthoring>(root).canBePlacedOnAnyWalkableTile = true;
            DimensionObjectSpine.ApplyPlacementRules(
                root,
                workbench.PlacementRules,
                delegate(string message)
                {
                    report.Warnings.Add("'" + workbench.DisplayName + "' " + message);
                });

            // The same spine every placed object gets. Stations are not paintable in vanilla, but they
            // do face the way you placed them.
            DimensionObjectSpine.ApplyUniversal(root, true);

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                workbench.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add("'" + workbench.DisplayName + "' " + message);
                });
            DimensionObjectSpine.ApplyDamageableStates(root);
            DimensionObjectSpine.ApplyPlacedObject(root, false, workbench.FacesPlacementDirection);
            DimensionObjectSpine.ApplyWiring(root, workbench.Wiring);

            GiveAMachineSomewhereToPutThings(root, workbench);

            // The sweep. A station is a placed object like any other, and the same answers on it —
            // wiring, reacting to what is nearby, a statue that takes a crystal — need the same
            // companions the game's own prefabs carry.
            DimensionQueryCompanions.FinishAWorldObject(
                root,
                workbench.DisplayName,
                delegate(string message)
                {
                    report.Warnings.Add(message);
                });
        }

        /// <summary>
        /// The slots a machine puts things into, without which its window opens empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A plain workbench needs none of this — you pick a recipe from a list and it appears in
        /// your own bag — which is why the game's own <c>CopperWorkBenchEntity</c> carries no
        /// inventory and why that kind was the only one that worked. Every OTHER kind is a machine
        /// you put something into and take something out of, and all of that runs off an inventory:
        /// with none, the window is built with zero slots, the crafting handler never gets an
        /// output slot to write into, and the timer buffer that tells the machine it is working is
        /// never created at all. The station generated cleanly and could never craft anything.
        /// </para>
        /// <para>
        /// The sizes are the game's own: the furnace and the seed extractor are one slot, the
        /// cooking pot is one by two, the critter catcher is two by two. The automated crafter
        /// component beside it is what creates the timer buffer, and it is on all four of those
        /// prefabs — it is also what lets a conveyor feed the machine, which is what a modder
        /// expects of anything that processes.
        /// </para>
        /// </remarks>
        private static void GiveAMachineSomewhereToPutThings(
            GameObject root,
            DimensionWorkbenchAsset workbench)
        {
            int across;
            int down;

            switch (workbench.Kind)
            {
                case DimensionWorkbenchKind.Cooking:
                    across = 1;
                    down = 2;
                    break;

                case DimensionWorkbenchKind.CritterCatching:
                    across = 2;
                    down = 2;
                    break;

                case DimensionWorkbenchKind.ProcessResources:
                case DimensionWorkbenchKind.Extract:
                case DimensionWorkbenchKind.Incinerate:
                case DimensionWorkbenchKind.Fishing:
                case DimensionWorkbenchKind.BossStatue:
                case DimensionWorkbenchKind.BiomeBossStatue:
                case DimensionWorkbenchKind.BiomeBossHydraStatue:
                    across = 1;
                    down = 1;
                    break;

                default:
                    // Workbench and Cattle. The game's own prefabs for both carry no inventory, and
                    // adding one would put an empty window in front of a recipe list.
                    return;
            }

            InventoryAuthoring inventory = EnsureComponent<InventoryAuthoring>(root);
            if (inventory.sizeX <= 0)
            {
                inventory.sizeX = across;
            }

            if (inventory.sizeY <= 0)
            {
                inventory.sizeY = down;
            }

            if (inventory.slotRequirements == null)
            {
                inventory.slotRequirements = new List<SlotRequirement>();
            }

            bool statue =
                workbench.Kind == DimensionWorkbenchKind.BossStatue ||
                workbench.Kind == DimensionWorkbenchKind.BiomeBossStatue ||
                workbench.Kind == DimensionWorkbenchKind.BiomeBossHydraStatue;
            if (!statue)
            {
                EnsureComponent<Pug.Automation.AutomatedCrafterAuthoring>(root);
                EnsureComponent<Pug.Automation.AutomatedStorageAuthoring>(root);
            }
        }

        /// <summary>
        /// The two pictures a station needs: the one in the world and the one in a slot.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>additionalSprites[0]</c> is where the world picture goes because that is where Core
        /// Keeper already keeps an object's own picture for whoever has to draw it outside the world
        /// — <c>PlayerController</c> reads it for a carried object — and it is what
        /// <see cref="ExpandNullforge.Objects.DimensionCraftingBenchView"/> reads back per entity.
        /// The list is rebuilt rather than appended to, so regenerating never leaves the previous
        /// picture sitting in front of the new one.
        /// </para>
        /// <para>
        /// The icon has to be on <c>InventoryItemAuthoring</c> and nowhere else, because that is the
        /// only component <c>ObjectAuthoring.ObjectAuthoringToObjectInfo</c> reads an icon from.
        /// </para>
        /// </remarks>
        private static void ApplyArt(
            GameObject root,
            DimensionWorkbenchAsset workbench,
            DimensionWorkbenchGenerationReport report)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.additionalSprites = new List<Sprite>();
            if (workbench.Sprite != null)
            {
                obj.additionalSprites.Add(workbench.Sprite);
            }

            InventoryItemAuthoring inventoryItem = EnsureComponent<InventoryItemAuthoring>(root);
            inventoryItem.icon = workbench.Icon;
            inventoryItem.smallIcon = workbench.Icon;
            inventoryItem.isStackable = false;

            if (workbench.Icon == null)
            {
                report.Warnings.Add(
                    "'" + workbench.DisplayName + "' has no icon, so it draws as an empty square " +
                    "in every slot it appears in. Put artwork in its Picture or Icon field.");
            }
        }

        /// <summary>
        /// The recipe list, which is what makes it a station at all.
        /// </summary>
        /// <remarks>
        /// <c>canCraftObjects</c> names outputs. Each entry carries the recipe's own crafting time, so
        /// a slow recipe stays slow wherever it is offered.
        /// </remarks>
        private static void ApplyCrafting(
            GameObject root,
            DimensionWorkbenchAsset workbench,
            DimensionNamingContext naming,
            DimensionWorkbenchGenerationReport report)
        {
            CraftingAuthoring crafting = EnsureComponent<CraftingAuthoring>(root);
            crafting.craftingType = ToCraftingType(workbench.Kind);
            crafting.showLoopEffectOnOutputSlot = workbench.ShowsALoopingEffectWhileWorking;
            crafting.allInventoryIsForSingleCraft = workbench.WholeInventoryIsOneCraft;
            crafting.minMaxRandomDefaultExtractedOutputAmount = workbench.ExtractedAmountRange;
            crafting.minMaxRandomDefaultCraftingTime = workbench.DefaultCraftTimeRange;

            ObjectCategoryTag extracts;
            if (!string.IsNullOrEmpty(workbench.ExtractsCategoryTag) &&
                System.Enum.TryParse(workbench.ExtractsCategoryTag, false, out extracts))
            {
                crafting.extractableType = extracts;
            }
            else if (!string.IsNullOrEmpty(workbench.ExtractsCategoryTag))
            {
                report.Warnings.Add(
                    "'" + workbench.DisplayName + "' extracts '" + workbench.ExtractsCategoryTag +
                    "', which is not a category the game has, so it extracts nothing.");
            }
            crafting.canCraftObjects = new List<CraftingAuthoring.CraftableObject>();

            if (crafting.includeCraftedObjectsFromBuildings == null)
            {
                crafting.includeCraftedObjectsFromBuildings = new List<CraftingAuthoring>();
            }

            DimensionRecipeAsset[] recipes = workbench.Recipes;
            if (recipes == null)
            {
                return;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null || !recipe.Enabled || string.IsNullOrEmpty(recipe.OutputItemId))
                {
                    continue;
                }

                CraftingAuthoring.CraftableObject craftable = new CraftingAuthoring.CraftableObject();
                craftable.amount = recipe.OutputAmount < 1 ? 1 : recipe.OutputAmount;
                craftable.craftingTime = recipe.CraftTimeSeconds;

                ObjectID output;
                if (!Enum.TryParse(recipe.OutputItemId, false, out output) ||
                    output == ObjectID.None)
                {
                    // NOT BAKED, AND THAT IS THE FIX. Core Keeper's own escape hatch for this is
                    // CraftingAuthoring.moddedObjectID — but it is resolved DURING conversion, by a
                    // lookup filled one object at a time, and mod prefabs convert in an order kept
                    // sorted by a hash of the prefab name. A bench that happened to convert before
                    // the item it makes baked ObjectID.None into its recipe list, permanently and
                    // silently, and a rename could flip it either way. The bootstrap registers
                    // these by name instead (AppendWorkbenchOwnRecipeRegistrations) and the recipe
                    // injector adds the row when the bench's entity appears, by which time every
                    // name resolves.
                    continue;
                }

                craftable.objectID = output;
                crafting.canCraftObjects.Add(craftable);
            }
        }

        /// <summary>How many recipes will actually reach the station.</summary>
        /// <remarks>
        /// IT ASKS WHETHER THE OUTPUT EXISTS, and it did not. A misspelled output was silent at
        /// both ends: the bake above skips anything the game's enum does not answer to, without a
        /// word, and the bootstrap's own walk skips anything this mod does not own — so a bench
        /// offering a recipe for "IronBarr" counted as usable, generated clean, and simply never
        /// showed the recipe. This is the one place that sees the output and knows both answers.
        /// </remarks>
        private static int CountUsableRecipes(
            DimensionWorkbenchAsset workbench,
            DimensionNamingContext naming,
            DimensionWorkbenchGenerationReport report)
        {
            DimensionRecipeAsset[] recipes = workbench.Recipes;
            if (recipes == null)
            {
                return 0;
            }

            int usable = 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null || !recipe.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(recipe.OutputItemId))
                {
                    report.Warnings.Add(
                        "'" + workbench.DisplayName + "' offers a recipe with no output, which will " +
                        "show as a blank entry the player cannot craft.");
                    continue;
                }

                if (DimensionObjectBinder.Vanilla(recipe.OutputItemId) == ObjectID.None &&
                    !naming.Owns(recipe.OutputItemId))
                {
                    report.Warnings.Add(
                        "'" + workbench.DisplayName + "' offers a recipe making '" +
                        recipe.OutputItemId + "', which is neither one of this mod's items nor one " +
                        "the game has, so that recipe never appears at the station. Check the " +
                        "spelling, or that the item is still switched on.");
                    continue;
                }

                usable++;
            }

            return usable;
        }

        private static void ApplyBreaking(GameObject root, DimensionWorkbenchAsset workbench)
        {
            EnsureComponent<MineableAuthoring>(root);

            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = true;
            health.maxHealth = workbench.HitsToBreak;
            health.startHealth = workbench.HitsToBreak;
            health.maxHealthMultiplier = 1f;

            DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
            reduction.calculateReductionFromLevel = false;
            reduction.reductionMultiplier = 1f;
            reduction.reduction = 0;
            reduction.maxDamagePerHit = DamagePerHit;
            reduction.minDamagePerHit = 0;

            // Vanilla stations return the station itself, not a stack sized by drop maths.
            EnsureComponent<AlwaysDropOneAuthoring>(root);
        }

        /// <summary>Maps our plain names onto Core Keeper's own <c>CraftingType</c>.</summary>
        private static CraftingType ToCraftingType(DimensionWorkbenchKind kind)
        {
            switch (kind)
            {
                case DimensionWorkbenchKind.ProcessResources:
                    return CraftingType.ProcessResources;
                case DimensionWorkbenchKind.BossStatue:
                    return CraftingType.BossStatue;
                case DimensionWorkbenchKind.Cooking:
                    return CraftingType.Cooking;
                case DimensionWorkbenchKind.Cattle:
                    return CraftingType.Cattle;
                case DimensionWorkbenchKind.Extract:
                    return CraftingType.Extract;
                case DimensionWorkbenchKind.Incinerate:
                    return CraftingType.Incinerate;
                case DimensionWorkbenchKind.BiomeBossStatue:
                    return CraftingType.BiomeBossStatue;
                case DimensionWorkbenchKind.Fishing:
                    return CraftingType.Fishing;
                case DimensionWorkbenchKind.BiomeBossHydraStatue:
                    return CraftingType.BiomeBossHydraStatue;
                case DimensionWorkbenchKind.CritterCatching:
                    return CraftingType.CritterCatching;
                default:
                    return CraftingType.Simple;
            }
        }

        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

    }
}
