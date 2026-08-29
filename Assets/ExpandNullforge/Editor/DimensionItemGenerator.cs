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
    /// What a generation run did and what it could not do. Nothing is reported as generated
    /// unless the prefab was actually written, and any value the framework could not apply is
    /// surfaced as a warning rather than being dropped silently.
    /// </summary>
    internal sealed class DimensionItemGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();

        /// <summary>
        /// Prefabs deleted because the item that produced them no longer exists. Its own list
        /// because a deletion is neither a failure nor a routine write — it is the one outcome a
        /// modder should be able to scan for and object to.
        /// </summary>
        public readonly List<string> Removed = new List<string>();

        public readonly List<string> Skipped = new List<string>();

        /// <summary>
        /// Answers the generator worked out for an item that did not give one, and what it settled
        /// on: what the item is, the time between uses when none was set, and whether several of it
        /// share a slot when what it is settles that on its own.
        /// </summary>
        /// <remarks>
        /// Its own list for the same reason <see cref="Removed"/> has one: it is neither a failure
        /// nor a routine write. An item that never answered "what is this" gets an answer chosen for
        /// it, and that answer decides which slot it lands in and how much use it takes before it
        /// breaks — so it has to be readable somewhere rather than happening quietly. Warnings are
        /// the wrong home: on a project of two hundred items every one of them would be a warning,
        /// and a list nobody can finish reading is a list nobody reads. The item card's own
        /// validator already warns where a warning is the right shape.
        /// </remarks>
        public readonly List<string> Derived = new List<string>();

        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>
        /// Every localization key this run wrote, so the coverage check can ask the table itself
        /// what is named rather than re-deriving it and drifting from what was written.
        /// </summary>
        public readonly HashSet<string> LocalizationKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public int GeneratedCount => Created.Count + Updated.Count;

        public bool HasProblems => Warnings.Count > 0 || Errors.Count > 0;

        public string Summarize()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(Created.Count).Append(" created, ")
                .Append(Updated.Count).Append(" updated, ")
                .Append(Skipped.Count).Append(" skipped");
            if (Derived.Count > 0)
            {
                builder.Append(", ").Append(Derived.Count)
                    .Append(" had answers filled in");
            }

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
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder)
        {
            return Generate(items, outputFolder, null, null);
        }

        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes)
        {
            return Generate(items, outputFolder, recipes, null);
        }

        /// <summary>
        /// Generates items and, where a recipe produces one of them, writes that recipe's
        /// ingredients and craft time onto the item. Core Keeper keeps a recipe's ingredient list
        /// on the produced item rather than on the crafting station, so this is where a recipe
        /// becomes real. When <paramref name="tilesets"/> is supplied, any generated item that is a
        /// tileset's ground or wall block additionally receives its tile-behaviour components so it
        /// places, digs/mines, and drops as a real custom block.
        /// </summary>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            return Generate(items, outputFolder, recipes, tilesets, null);
        }

        /// <summary>
        /// As above, and also writes the localization rows biomes need for their title cards.
        /// </summary>
        /// <remarks>
        /// Biomes ride along here rather than getting their own writer because the mod has ONE
        /// localization table, and two writers merging into one file would each drop the other's rows.
        /// Nothing about a biome is generated as a prefab — only its title text is written.
        /// </remarks>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets,
            IEnumerable<BiomeTemplateAsset> biomes)
        {
            return Generate(items, outputFolder, recipes, tilesets, biomes, null);
        }

        /// <summary>
        /// As above, and also rides the bosses along: their name localization rows and the
        /// summoning stamp on their idol items.
        /// </summary>
        /// <remarks>
        /// Bosses ride the item generate for the same reason biomes do — the mod has ONE
        /// localization table with one writer, and a second writer would drop the first's rows.
        /// The summoning stamp has to happen here too, because the idol is an ITEM: only this
        /// generator holds its prefab open.
        ///
        /// <paramref name="localization"/> carries the names of everything the OTHER generators
        /// build — stations, chests, decorations, seeds, creatures, and the lines of the mod's own
        /// stat effects. They ride here for the same one-writer reason: a second pass merging the
        /// same file would treat these rows as somebody else's leftovers and never clean them up.
        /// </remarks>
        public static DimensionItemGenerationReport Generate(
            IEnumerable<DimensionItemAsset> items,
            string outputFolder,
            IEnumerable<DimensionRecipeAsset> recipes,
            IEnumerable<DimensionTilesetAsset> tilesets,
            IEnumerable<BiomeTemplateAsset> biomes,
            IEnumerable<DimensionBossAsset> bosses,
            IEnumerable<DimensionNamedAreaAsset> namedAreas = null,
            DimensionLocalizationPlan localization = null,
            IEnumerable<string> otherOwnedObjectIds = null,
            IEnumerable<DimensionExplosionAsset> blasts = null,
            IEnumerable<string> switchedOffObjectIds = null)
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

            if (!DimensionAssetFolders.EnsureExists(outputFolder))
            {
                report.Errors.Add("Could not create the output folder '" + outputFolder + "'.");
                return report;
            }

            Dictionary<string, TilesetBlockBinding> blockIndex = IndexTilesetBlocks(tilesets);
            List<TilesetOreBinding> oreBindings = ResolveTilesetOres(tilesets, report);
            Dictionary<string, DimensionTilesetAsset> oreIndex = IndexCustomOres(oreBindings);
            Dictionary<string, DimensionRecipeAsset> recipesByOutput = IndexRecipes(recipes, report);

            // Every generated object name is qualified with the owning mod, because Core Keeper keys
            // object properties by name and a collision between two mods makes a world that loads
            // once and then never again. The set of our own ids is collected first so that a
            // reference to a VANILLA item (a recipe asking for IronBar) is left alone — see
            // DimensionNamingContext.QualifyReference.
            List<string> ownItemIds = new List<string>();
            List<DimensionItemAsset> itemList = new List<DimensionItemAsset>();
            foreach (DimensionItemAsset candidate in items)
            {
                itemList.Add(candidate);
                if (candidate != null && !string.IsNullOrEmpty(candidate.ItemId))
                {
                    ownItemIds.Add(candidate.ItemId);
                }
            }

            // A cooked dish takes its two palettes from the ingredients it names, so the mod's own
            // ingredients are indexed once here rather than searched per dish.
            Dictionary<string, DimensionCookingTemplate> ingredientsById =
                new Dictionary<string, DimensionCookingTemplate>();
            for (int i = 0; i < itemList.Count; i++)
            {
                DimensionItemAsset candidate = itemList[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.ItemId))
                {
                    continue;
                }

                DimensionCookingTemplate cooking = candidate.Cooking;
                if (cooking.IsAnIngredient && !ingredientsById.ContainsKey(candidate.ItemId))
                {
                    ingredientsById[candidate.ItemId] = cooking;
                }
            }

            CheckIngredientsCanFitInADish(itemList, ingredientsById.Count, report);

            // The mod's OTHER objects count as ours too. An item that fires the mod's own projectile,
            // or leaves the mod's own chest, names something no item asset defines — and with only
            // item ids in the ownership set the reference read as "the game does not have that" and
            // the component carrying it was stripped.
            if (otherOwnedObjectIds != null)
            {
                foreach (string otherId in otherOwnedObjectIds)
                {
                    if (!string.IsNullOrEmpty(otherId))
                    {
                        ownItemIds.Add(otherId);
                    }
                }
            }

            DimensionNamingContext naming = new DimensionNamingContext(
                ResolveModName(outputFolder), ownItemIds, switchedOffObjectIds);
            binder = new DimensionObjectBinder(naming);
            ownBlastIds = new HashSet<string>(StringComparer.Ordinal);
            if (blasts != null)
            {
                foreach (DimensionExplosionAsset blast in blasts)
                {
                    if (blast != null && blast.Enabled && !string.IsNullOrEmpty(blast.ExplosionId))
                    {
                        ownBlastIds.Add(blast.ExplosionId);
                    }
                }
            }

            if (!naming.CanQualify)
            {
                report.Warnings.Add(
                    "The owning mod could not be resolved from '" + outputFolder +
                    "', so generated object names are NOT namespaced. Installing this mod alongside " +
                    "another that uses the same item id will break world loading. Check that the " +
                    "output folder sits under a mod with a ModBuilderSettings asset.");
            }

            List<string> generatedIds = new List<string>();
            List<DimensionLocalizationCsv.Row> localizationRows =
                new List<DimensionLocalizationCsv.Row>();
            List<string> retiredLocalizationKeys = new List<string>();

            // Which items summon which bosses, visible to Configure while the batch runs.
            summoningBossesByItemId = BuildSummoningMap(bosses, itemList, naming, report);

            // Folders have to exist BEFORE the batch opens: AssetDatabase.CreateFolder inside a
            // StartAssetEditing block does not take effect until the batch closes, so an asset
            // written into a folder made in the same batch is written nowhere.
            DimensionEquipmentSkinGenerator.EnsureSkinFolder(
                outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionItemAsset item in itemList)
                {
                    if (GenerateOne(item, outputFolder, recipesByOutput, blockIndex, oreIndex, naming, report, ingredientsById) && item != null)
                    {
                        // The recorded id and the localization key must both use the qualified
                        // name, or the display name will not resolve for the object that now
                        // carries it.
                        string qualified = naming.QualifyGenerated(item.ItemId);
                        generatedIds.Add(qualified);

                        // A bomb's blast is a second object with its own id. It is recorded here
                        // alongside the item so the orphan sweep treats the pair as one thing:
                        // deleting the bomb takes its blast with it, and keeping the bomb keeps it.
                        string blastId = DimensionExplosiveBlast.Write(
                            item,
                            outputFolder,
                            naming,
                            message => report.Warnings.Add(message),
                            message => report.Errors.Add(message),
                            path => report.Created.Add(path),
                            path => report.Updated.Add(path));
                        if (!string.IsNullOrEmpty(blastId))
                        {
                            generatedIds.Add(naming.QualifyGenerated(blastId));
                        }
                        DimensionLocalizationCsv.AddItemRows(
                            localizationRows,
                            qualified,
                            item.DisplayName,
                            item.Description,
                            retiredLocalizationKeys);

                        // An ingredient also needs the two words a dish's name is built from.
                        // Without them every dish cooked with it reads with a raw object id where
                        // the ingredient's name belongs — and nothing says so until somebody cooks.
                        if (item.Cooking.IsAnIngredient)
                        {
                            DimensionLocalizationCsv.AddFoodIngredientNameRows(
                                localizationRows, qualified, item.DisplayName);
                        }
                    }
                }

                // Vanilla-ore veins are standalone objects rather than a stamp on one of our item
                // prefabs, so they are written after the items — nothing above depends on them, and
                // they contribute no localization (the ore already has vanilla's name).
                for (int i = 0; i < oreBindings.Count; i++)
                {
                    if (!oreBindings[i].IsCustomItem)
                    {
                        DimensionTilesetOreAuthoring.CreateVanillaVeinPrefab(
                            outputFolder,
                            oreBindings[i].Tileset,
                            oreBindings[i].OreItemId,
                            report);
                    }
                }

                AppendFarmingInfrastructure(outputFolder, tilesets, report);
                AppendBiomeTitleLocalization(localizationRows, naming, biomes);
                AppendBossNameLocalization(localizationRows, naming, bosses);
                AppendNamedAreaTitleLocalization(localizationRows, naming, namedAreas);

                if (localization != null)
                {
                    localizationRows.AddRange(localization.Rows);
                    retiredLocalizationKeys.AddRange(localization.RetiredKeys);
                    report.Warnings.AddRange(localization.Warnings);
                }
            }
            finally
            {
                summoningBossesByItemId = null;
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string modRoot = ResolveModRoot(outputFolder);

            // Before the manifest is overwritten with this run's ids, use its previous contents to
            // find prefabs we generated last time and no longer want. PugMod ships a mod by scanning
            // folders, so an orphan left here still registers an object in game.
            PruneOrphanedItemPrefabs(modRoot, outputFolder, generatedIds, report);
            RecordGeneratedItemIds(modRoot, generatedIds, report);
            WriteLocalization(modRoot, localizationRows, retiredLocalizationKeys, report);

            // Two rows under one key is one name silently winning over another — the table has no
            // notion of ownership, so the loser simply never appears. It can only happen when two
            // authored things resolve to the same object name, which the game would also refuse to
            // load, so naming the key here is the earliest anyone hears about it.
            for (int i = 0; i < localizationRows.Count; i++)
            {
                if (!report.LocalizationKeys.Add(localizationRows[i].Key) &&
                    !string.IsNullOrEmpty(localizationRows[i].Key))
                {
                    report.Warnings.Add(
                        "Two things were named under '" + localizationRows[i].Key +
                        "'. Only one of the names can show. Give them different ids.");
                }
            }

            return report;
        }

        /// <summary>
        /// Refuses to build a mod so large that an ingredient cannot be remembered by a dish.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cooked dish stores the two ingredients that made it packed into one integer, sixteen
        /// bits each. An ingredient whose object id is above 65535 loses its high bits on the way
        /// back out, and the dish is then tinted, buffed and named after whatever object happens to
        /// sit at the truncated number. Nothing anywhere reports it — not the pot, not the cook
        /// book, not the save. This is the identity-gate failure the framework exists to catch.
        /// </para>
        /// <para>
        /// Only half of it is knowable here. A mod's object ids are handed out at load, starting at
        /// 32768 and continuing past whatever loaded before it, so the FINAL number depends on the
        /// player's load order and the runtime check is the one that sees it. What IS knowable is
        /// the best case: a mod alone at the front of the load order gets 32768 upwards, so a mod
        /// carrying more objects than the room between 32768 and 65535 has ingredients that cannot
        /// fit under ANY load order. That is a build error, not a warning, because there is no
        /// version of the shipped mod in which it works.
        /// </para>
        /// </remarks>
        private static void CheckIngredientsCanFitInADish(
            List<DimensionItemAsset> items,
            int ingredientCount,
            DimensionItemGenerationReport report)
        {
            if (ingredientCount == 0)
            {
                return;
            }

            const int FirstModObjectId = 32768;
            int room = ExpandNullforge.Food.DimensionFoodPairing.MaximumPackableObjectId
                - FirstModObjectId + 1;
            if (items.Count <= room)
            {
                return;
            }

            report.Errors.Add(
                "This mod defines " + items.Count + " objects and " + ingredientCount +
                " of them are cooking ingredients. A dish can only remember an ingredient whose " +
                "object number is " +
                ExpandNullforge.Food.DimensionFoodPairing.MaximumPackableObjectId +
                " or below, and a mod is numbered from " + FirstModObjectId + " upwards, so at " +
                "most " + room + " objects can come before an ingredient. Past that a cooked dish " +
                "quietly remembers the wrong ingredient — wrong colours, wrong effects, wrong " +
                "name. Split this mod in two, or move the ingredients earlier in it.");
        }

        /// <summary>
        /// Writes the item names and tooltips into the mod's localization table. Without this an
        /// item shows its raw id in-game, so generation is not complete until it runs.
        /// </summary>
        private static void WriteLocalization(
            string modRoot,
            List<DimensionLocalizationCsv.Row> rows,
            List<string> retiredKeys,
            DimensionItemGenerationReport report)
        {
            if (rows.Count == 0)
            {
                return;
            }

            string folder = modRoot + "/Localization";
            if (!DimensionAssetFolders.EnsureExists(folder))
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
                string merged = DimensionLocalizationCsv.Merge(existing, rows, retiredKeys);
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

        /// <summary>
        /// The name of the mod that owns <paramref name="outputFolder"/>, used to namespace every
        /// object this run generates. Empty when no owning mod can be found, in which case the
        /// caller warns rather than inventing a prefix — a wrong namespace is worse than none,
        /// because it silently renames content the player may already have in a save.
        /// </summary>
        private static string ResolveModName(string outputFolder)
        {
            ModBuilderSettings settings =
                DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(outputFolder);
            if (settings == null)
            {
                return string.Empty;
            }

            // ModMetadata.name is the mod's canonical id; displayName is a label and may change
            // without the content changing, so it must not decide object identity.
            return settings.metadata.name ?? string.Empty;
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
        /// <summary>
        /// Deletes prefabs a previous generation produced for items that no longer exist.
        /// </summary>
        /// <remarks>
        /// Only files this framework created are ever considered: the candidate set comes from the
        /// manifest's record of the last run, not from listing the folder, so a hand-authored prefab,
        /// a portal object or an ore vein cannot be caught by it. Deletion failures are reported
        /// rather than thrown — a locked file should not fail a whole generation, and the leftover is
        /// still named so it can be removed by hand.
        /// </remarks>
        private static void PruneOrphanedItemPrefabs(
            string modRoot,
            string outputFolder,
            List<string> generatedIds,
            DimensionItemGenerationReport report)
        {
            DimensionRuntimeManifestAsset manifest = FindRuntimeManifest(modRoot);
            if (manifest == null)
            {
                return;
            }

            List<string> orphans = DimensionGeneratedArtifactPruner.FindOrphanedItemIds(
                manifest.GeneratedItemIds, generatedIds);

            for (int i = 0; i < orphans.Count; i++)
            {
                string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(orphans[i], "Item") + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(prefabPath))
                {
                    report.Removed.Add(prefabPath);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + prefabPath + "' is left over from an item that no longer exists and " +
                        "could not be deleted. It will still be shipped and registered as an object " +
                        "until it is removed by hand.");
                }
            }
        }

        /// <summary>The mod's runtime manifest, or null when none has been exported yet.</summary>
        private static DimensionRuntimeManifestAsset FindRuntimeManifest(string modRoot)
        {
            if (!AssetDatabase.IsValidFolder(modRoot))
            {
                return null;
            }

            string[] guids =
                AssetDatabase.FindAssets("t:DimensionRuntimeManifestAsset", new[] { modRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                DimensionRuntimeManifestAsset manifest =
                    AssetDatabase.LoadAssetAtPath<DimensionRuntimeManifestAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                if (manifest != null)
                {
                    return manifest;
                }
            }

            return null;
        }

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
                    // The same asset can arrive twice — once from the mod's recipe list and once
                    // from the Workbench that lists it — and that is not two recipes clashing.
                    if (ReferenceEquals(existing, recipe))
                    {
                        continue;
                    }

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

        /// <summary>Which tileset a generated block item belongs to, and whether it is the wall kind.</summary>
        private readonly struct TilesetBlockBinding
        {
            public readonly DimensionTilesetAsset Tileset;
            public readonly bool IsWall;

            public TilesetBlockBinding(DimensionTilesetAsset tileset, bool isWall)
            {
                Tileset = tileset;
                IsWall = isWall;
            }
        }

        /// <summary>
        /// Maps each enabled tileset's toggled-on block item ids to their tileset + kind, so a block
        /// item can be recognised during generation and given its tile-behaviour components. A
        /// duplicate id (two tilesets colliding on a block id) keeps the first and is left for the
        /// tileset registry's own collision reporting.
        /// </summary>
        /// <summary>
        /// Writes (or removes) the hidden tilled-ground object for each tileset, so a farmable block
        /// tills into its own soil instead of dirt.
        /// </summary>
        /// <remarks>
        /// Both directions matter. The hoe keeps a block's tileset only when an object exists for
        /// <c>(tileset, dugUpGround)</c>, so switching farming ON needs the object written — and
        /// switching it OFF needs it deleted, or the capability could be granted but never revoked.
        /// A tileset with no ground block is skipped: there is nothing to till.
        /// </remarks>
        /// <summary>
        /// Writes each biome's title text under the term its generated runtime looks up.
        /// </summary>
        /// <remarks>
        /// Without this row the title card still appears — Core Keeper renders whatever the term
        /// resolves to — but it renders the raw key, so the player sees "Biomes/MyMod_frostlands"
        /// across the middle of their screen. That failure is invisible everywhere except in game.
        /// </remarks>
        /// <summary>Which bosses each summoning item calls, while a generate is running.</summary>
        private static Dictionary<string, List<string>> summoningBossesByItemId;

        /// <summary>
        /// Maps each authored summoning-item id to the qualified names of the bosses it calls,
        /// warning for a boss whose idol names no authored item — that boss would simply be
        /// unsummonable, with nothing else saying so.
        /// </summary>
        private static Dictionary<string, List<string>> BuildSummoningMap(
            IEnumerable<DimensionBossAsset> bosses,
            List<DimensionItemAsset> itemList,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report)
        {
            if (bosses == null)
            {
                return null;
            }

            HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < itemList.Count; i++)
            {
                if (itemList[i] != null)
                {
                    itemIds.Add(itemList[i].ItemId);
                }
            }

            Dictionary<string, List<string>> map = null;
            foreach (DimensionBossAsset boss in bosses)
            {
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.SummoningItemId))
                {
                    continue;
                }

                if (!itemIds.Contains(boss.SummoningItemId))
                {
                    report.Warnings.Add(
                        "Boss '" + boss.DisplayName + "' is summoned by item '" +
                        boss.SummoningItemId + "', which is not one of this mod's items. " +
                        "Nothing will summon it.");
                    continue;
                }

                map = map ?? new Dictionary<string, List<string>>(StringComparer.Ordinal);
                List<string> bossNames;
                if (!map.TryGetValue(boss.SummoningItemId, out bossNames))
                {
                    bossNames = new List<string>();
                    map[boss.SummoningItemId] = bossNames;
                }

                bossNames.Add(naming.QualifyGenerated(boss.BossId));
            }

            return map;
        }

        /// <summary>
        /// Stamps or clears the summoning link on an item, so an idol summons its boss and a
        /// re-generated ex-idol stops.
        /// </summary>
        /// <remarks>
        /// UNIONS, IT DOES NOT REPLACE. The item's own "it summons any of these" list is written by
        /// <c>DimensionObjectSpine.ApplyWorldRoles</c> into the same component just before this
        /// runs; replacing the list here would silently throw that away, and removing the component
        /// when no boss asset names this item would throw it away twice over.
        /// </remarks>
        private static void ConfigureSummoning(
            GameObject root,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            List<string> bossNames = null;
            bool summons =
                summoningBossesByItemId != null &&
                summoningBossesByItemId.TryGetValue(item.ItemId, out bossNames);

            ExpandNullforge.Creatures.DimensionSummoningItemAuthoring existing =
                root.GetComponent<ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>();

            if (!summons)
            {
                if (existing == null ||
                    existing.bossObjectNames == null ||
                    existing.bossObjectNames.Count == 0)
                {
                    RemoveComponentIfPresent<
                        ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
                }

                return;
            }

            ExpandNullforge.Creatures.DimensionSummoningItemAuthoring summoning = existing != null
                ? existing
                : EnsureComponent<ExpandNullforge.Creatures.DimensionSummoningItemAuthoring>(root);
            if (summoning.bossObjectNames == null)
            {
                summoning.bossObjectNames = new List<string>();
            }

            for (int i = 0; i < bossNames.Count; i++)
            {
                if (!summoning.bossObjectNames.Contains(bossNames[i]))
                {
                    summoning.bossObjectNames.Add(bossNames[i]);
                }
            }

            // AN OFFERING HAS TO BE SOMETHING YOU CAN PUT DOWN. The game's summoning circle finds
            // its offering by looking at what is lying on it, so the item has to become a thing in
            // the world — and only a placed object does. All six of the game's own summoning items
            // are placeable prefabs and carry the placement answer. In this framework the kind of
            // thing an item is comes solely from what the author picked it to be, and three of
            // those kinds — a material, a pickup and a custom one — are never placed.
            //
            // So an "Ember Idol" made as a Material, ticked as a boss's offering, with the circle
            // set down in the arena exactly as the validator asks, generates completely clean and
            // can never be put on the ground at all. The circle never sees it and the boss can
            // never be summoned. Nothing said so anywhere.
            if (root.GetComponent<PlaceableObjectAuthoring>() == null && report != null)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is a boss's offering, but it is not something a player " +
                    "can put down on the ground — and the summoning circle only notices what is " +
                    "lying on it. Make it a kind of thing that can be placed, or the boss can " +
                    "never be summoned.");
            }
        }

        /// <summary>
        /// Every boss's floating-name row, and the pin's hover row when it differs.
        /// </summary>
        /// <remarks>
        /// Without the row, the nameplate and the map hover both render the raw term —
        /// "Names/mod_boss" floating over the fight. Visible and debuggable, but not a name.
        /// </remarks>
        private static void AppendBossNameLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<DimensionBossAsset> bosses)
        {
            if (rows == null || bosses == null)
            {
                return;
            }

            foreach (DimensionBossAsset boss in bosses)
            {
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string qualified = naming.QualifyGenerated(boss.BossId);
                DimensionLocalizationCsv.AddNameRow(
                    rows,
                    qualified,
                    string.IsNullOrEmpty(boss.DisplayName) ? boss.BossId : boss.DisplayName);

                if (!string.IsNullOrEmpty(boss.MapPin.HoverName))
                {
                    DimensionLocalizationCsv.AddNameRow(rows, qualified + "-pin", boss.MapPin.HoverName);
                }
            }
        }

        /// <summary>The named areas' title rows, under the same synthetic ids the registries use.</summary>
        private static void AppendNamedAreaTitleLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<DimensionNamedAreaAsset> namedAreas)
        {
            if (rows == null || namedAreas == null)
            {
                return;
            }

            foreach (DimensionNamedAreaAsset area in namedAreas)
            {
                if (area == null || !area.Enabled || !area.ShowTitleOnDiscovery ||
                    string.IsNullOrEmpty(area.AreaId))
                {
                    continue;
                }

                DimensionLocalizationCsv.AddBiomeTitleRow(
                    rows,
                    DimensionBiomeTitleTerms.ForBiome(naming.ModName, "area:" + area.AreaId),
                    string.IsNullOrEmpty(area.DisplayName) ? area.AreaId : area.DisplayName);
            }
        }

        private static void AppendBiomeTitleLocalization(
            List<DimensionLocalizationCsv.Row> rows,
            DimensionNamingContext naming,
            IEnumerable<BiomeTemplateAsset> biomes)
        {
            if (rows == null || biomes == null)
            {
                return;
            }

            foreach (BiomeTemplateAsset biome in biomes)
            {
                if (biome == null || !biome.Enabled || !biome.ShowTitleOnDiscovery)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(biome.BiomeId))
                {
                    continue;
                }

                DimensionLocalizationCsv.AddBiomeTitleRow(
                    rows,
                    DimensionBiomeTitleTerms.ForBiome(naming.ModName, biome.BiomeId),
                    string.IsNullOrEmpty(biome.DisplayName) ? biome.BiomeId : biome.DisplayName);
            }
        }

        private static void AppendFarmingInfrastructure(
            string outputFolder,
            IEnumerable<DimensionTilesetAsset> tilesets,
            DimensionItemGenerationReport report)
        {
            if (tilesets == null)
            {
                return;
            }

            foreach (DimensionTilesetAsset tileset in tilesets)
            {
                if (tileset == null || string.IsNullOrEmpty(tileset.TilesetName))
                {
                    continue;
                }

                bool farmable =
                    tileset.Enabled &&
                    tileset.GenerateGroundBlock &&
                    tileset.IsStateEnabled("tilled");

                if (farmable)
                {
                    DimensionTilesetFarmingAuthoring.CreateTilledGroundPrefab(
                        outputFolder, tileset, report);
                }
                else
                {
                    DimensionTilesetFarmingAuthoring.RemoveTilledGroundPrefab(
                        outputFolder, tileset, report);
                }
            }
        }

        /// <summary>One tileset's resolved ore vein.</summary>
        private readonly struct TilesetOreBinding
        {
            public TilesetOreBinding(DimensionTilesetAsset tileset, string oreItemId, bool isCustomItem)
            {
                Tileset = tileset;
                OreItemId = oreItemId;
                IsCustomItem = isCustomItem;
            }

            public DimensionTilesetAsset Tileset { get; }
            public string OreItemId { get; }
            public bool IsCustomItem { get; }
        }

        /// <summary>
        /// Decides which single ore each tileset actually yields, and reports the ones that lost.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ONE VEIN PER TILESET — the rule lives here so both ore paths obey it. Mining resolves a
        /// vein through <c>GetObjectData(tileset, ore)</c>, a FIRST-MATCH linear scan over the
        /// database's object infos, so a tileset's second ore could never be reached no matter how
        /// it was authored. This is a vanilla limit, not ours: Solarite and Pandorium are both
        /// registered on Crystal, and the first one found wins there too.
        /// </para>
        /// <para>
        /// A second constraint stacks on top for custom ores: the item's own prefab carries the
        /// vein's <c>TileAuthoring</c>, and a prefab has one of those — so one custom item cannot be
        /// the ore of two tilesets either. Both losing cases are reported by name. Silence would
        /// teach the rule the expensive way, by someone mining a wall for an hour.
        /// </para>
        /// </remarks>
        private static List<TilesetOreBinding> ResolveTilesetOres(
            IEnumerable<DimensionTilesetAsset> tilesets,
            DimensionItemGenerationReport report)
        {
            List<TilesetOreBinding> bindings = new List<TilesetOreBinding>();
            if (tilesets == null)
            {
                return bindings;
            }

            Dictionary<string, DimensionTilesetAsset> customOwners =
                new Dictionary<string, DimensionTilesetAsset>(StringComparer.Ordinal);

            foreach (DimensionTilesetAsset tileset in tilesets)
            {
                if (tileset == null || !tileset.Enabled || tileset.Ores == null)
                {
                    continue;
                }

                // The ground-cover mirror rides the same loop and the same contract as the ore
                // mirror: written once per Generate, unconditionally, so removing the last cover
                // layer clears the arrays instead of leaving the previous build's grass behind.
                // It is independent of whether this tileset binds an ore.
                tileset.EditorSetGroundCover(tileset.Layers);
                EditorUtility.SetDirty(tileset);

                bool bound = false;
                foreach (DimensionTilesetOreConfig ore in tileset.Ores)
                {
                    if (ore == null || string.IsNullOrEmpty(ore.oreItemId))
                    {
                        continue;
                    }

                    if (bound)
                    {
                        report.Warnings.Add(
                            "'" + tileset.TilesetName + "' lists more than one ore; only the first is " +
                            "reachable, so '" + ore.oreItemId + "' was not generated. The game finds a " +
                            "vein by first match on the tileset, and vanilla has the same limit.");
                        continue;
                    }

                    if (ore.isCustomItem)
                    {
                        if (customOwners.TryGetValue(ore.oreItemId, out DimensionTilesetAsset owner))
                        {
                            report.Warnings.Add(
                                "Ore item '" + ore.oreItemId + "' is already the vein for '" +
                                owner.TilesetName + "', so '" + tileset.TilesetName + "' cannot also " +
                                "use it — the item's own prefab carries the vein and can only name one " +
                                "tileset. Duplicate the item if both blocks need it.");
                            continue;
                        }

                        customOwners.Add(ore.oreItemId, tileset);
                    }

                    bindings.Add(new TilesetOreBinding(tileset, ore.oreItemId, ore.isCustomItem));

                    // Mirror the winning config into the runtime arrays — the shape that
                    // survives into the game, where the vein scatter reads it. Generate-time,
                    // like the GEN sheets: panel edits take effect when the mod is generated.
                    tileset.EditorSetOreScatter(
                        new List<DimensionTilesetOreConfig> { ore });
                    EditorUtility.SetDirty(tileset);
                    bound = true;
                }

                if (!bound && tileset.OreScatterCount > 0)
                {
                    // The author removed the ore; the arrays must forget it too.
                    tileset.EditorSetOreScatter(null);
                    EditorUtility.SetDirty(tileset);
                }
            }

            return bindings;
        }

        /// <summary>Custom-ore bindings keyed by the item whose prefab gets the vein stamp.</summary>
        private static Dictionary<string, DimensionTilesetAsset> IndexCustomOres(
            List<TilesetOreBinding> bindings)
        {
            Dictionary<string, DimensionTilesetAsset> byItemId =
                new Dictionary<string, DimensionTilesetAsset>(StringComparer.Ordinal);
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].IsCustomItem && !byItemId.ContainsKey(bindings[i].OreItemId))
                {
                    byItemId.Add(bindings[i].OreItemId, bindings[i].Tileset);
                }
            }

            return byItemId;
        }

        private static Dictionary<string, TilesetBlockBinding> IndexTilesetBlocks(
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            Dictionary<string, TilesetBlockBinding> byId =
                new Dictionary<string, TilesetBlockBinding>(StringComparer.Ordinal);
            if (tilesets == null)
            {
                return byId;
            }

            foreach (DimensionTilesetAsset tileset in tilesets)
            {
                if (tileset == null || !tileset.Enabled || string.IsNullOrEmpty(tileset.TilesetName))
                {
                    continue;
                }

                if (tileset.GenerateGroundBlock && !byId.ContainsKey(tileset.GroundBlockItemId))
                {
                    byId.Add(tileset.GroundBlockItemId, new TilesetBlockBinding(tileset, false));
                }

                if (tileset.GenerateWallBlock && !byId.ContainsKey(tileset.WallBlockItemId))
                {
                    byId.Add(tileset.WallBlockItemId, new TilesetBlockBinding(tileset, true));
                }
            }

            return byId;
        }

        /// <returns>True when a prefab was written for this item.</returns>
        private static bool GenerateOne(
            DimensionItemAsset item,
            string outputFolder,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            Dictionary<string, TilesetBlockBinding> blockIndex,
            Dictionary<string, DimensionTilesetAsset> oreIndex,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report,
            Dictionary<string, DimensionCookingTemplate> ingredientsById)
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

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(item.ItemId, "Item") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(item.ItemId);

            bool written = false;
            try
            {
                Configure(
                    root,
                    item,
                    recipesByOutput,
                    blockIndex,
                    oreIndex,
                    naming,
                    report,
                    outputFolder,
                    ingredientsById);

                // A body, but only for an item that is actually put down. Core Keeper draws this
                // line sharply: a bomb, a barrel and a placed prop carry a collider, and a sword,
                // a helmet and a lump of ore carry none at all — a loose item is picked up through
                // its own pickup pass and is never cast against. So the answer is read off the
                // placement the author already gave: no placement, no body.
                //
                // For the ones that DO get placed it is not decoration. Being breakable is written
                // here, and every one of those answers is a lookup inside the loop that walks what
                // a swing hit; an item with no collider is not in the collision world and is never
                // in that loop, so a bomb could not be struck and could not be caught in another
                // bomb's chain.
                if (root.GetComponent<PlaceableObjectAuthoring>() != null)
                {
                    DimensionQueryCompanions.GiveItTheBodyItsFootprintAsksFor(
                        root,
                        Describe(item),
                        delegate(string message) { report.Warnings.Add(message); });
                }

                // And the sweep, last, the way every other generator ends. It was not wired in
                // here, which meant an item carrying an answer whose system needs something beside
                // it — a trap that attacks, a summoning circle, a bomb in a chain — never had that
                // filled in and was never told about it either.
                DimensionQueryCompanions.CloseTheGaps(
                    root,
                    Describe(item),
                    delegate(string message) { report.Warnings.Add(message); });

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

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        /// <remarks>
        /// Set once at the top of <c>Generate</c> and read by every reference below. It replaced a
        /// bare enum parse that could only answer "the game has it" or "None" — and every caller
        /// treated the second as a mistake, which stripped the authoring component off items whose
        /// only crime was pointing at another of the mod's own objects.
        /// </remarks>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        /// <summary>
        /// Resolves an item name at generation time to one of the GAME's own numbers.
        /// </summary>
        /// <remarks>
        /// Vanilla names go through the enum, because the runtime lookup is an empty dictionary
        /// outside a running game and quietly returns None for every one of them. A name that is
        /// one of this mod's own comes back as None here on purpose — see
        /// <see cref="DimensionObjectBinder.IsDeferred"/>, which is what the callers ask next.
        /// </remarks>
        private static ObjectID ResolveObjectByName(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        /// <summary>True when the name is one of this mod's own and the runtime will fill it in.</summary>
        private static bool IsDeferred(string itemId)
        {
            return binder.IsDeferred(itemId);
        }

        /// <summary>The full name the game registers one of this mod's own objects under.</summary>
        private static string QualifyReference(string itemId)
        {
            return binder.Qualify(itemId);
        }

        /// <summary>The blasts this mod makes, by their authored ids.</summary>
        /// <remarks>
        /// Kept apart from the general ownership set because a bomb's explosion field is narrower
        /// than "one of ours": <c>ExplosiveAuthoring.explosionID</c> has to name an explosion, and
        /// the bootstrap only registers the pair for a blast this mod actually builds. Suppressing
        /// the warning for every one of the mod's ids would let a bomb point at, say, its own sword
        /// and say nothing at all.
        /// </remarks>
        private static HashSet<string> ownBlastIds = new HashSet<string>(StringComparer.Ordinal);

        private static bool NamesOneOfOurBlasts(string objectId)
        {
            return !string.IsNullOrEmpty(objectId) &&
                DimensionObjectBinder.Vanilla(objectId) == ObjectID.None &&
                ownBlastIds.Contains(DimensionObjectNamespace.LocalIdOf(objectId));
        }

        private static void Configure(
            GameObject root,
            DimensionItemAsset item,
            Dictionary<string, DimensionRecipeAsset> recipesByOutput,
            Dictionary<string, TilesetBlockBinding> blockIndex,
            Dictionary<string, DimensionTilesetAsset> oreIndex,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report,
            string outputFolder,
            Dictionary<string, DimensionCookingTemplate> ingredientsById)
        {
            DimensionItemArchetype archetype = item.Archetype;
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(archetype);

            root.name = item.ItemId;

            // What the game thinks this thing is. Resolved once, before anything else: the type
            // decides the slot the item lands in, and the durability pass at the end of this method
            // reads it back off the object to work out what the game's own formula makes of it.
            DimensionWhatItIs kind = ResolveWhatItIs(item, report);
            ReportKindProblems(item, kind, required, report);

            // Whether this one ends up with a durability pool, worked out here because two separate
            // passes need the same answer: the stacking question below, and the durability pass at
            // the end. The only pool the archetype asks for and does not get is a throwing weapon's
            // with nothing typed, which sheds it the way all seven of the game's own do.
            bool keepsADurabilityPool = item.KeepsADurabilityPool(kind);
            bool stacks = ResolveStacking(item, kind, keepsADurabilityPool, report);

            ConfigureObject(root, item, kind, naming, report);
            ConfigureLocalization(root, item, naming, report);

            // Portal items (V2 instant portals) are framework-defined: rare rarity so the item
            // reads as the special tool it is. Applied on every generate so existing consumer
            // items pick it up without manual prefab edits.
            if (item.Kind == DimensionItemKind.PortalItem)
            {
                ObjectAuthoring portalItemObject = root.GetComponent<ObjectAuthoring>();
                if (portalItemObject != null)
                {
                    portalItemObject.rarity = Rarity.Rare;
                }
            }

            recipesByOutput.TryGetValue(item.ItemId, out DimensionRecipeAsset recipe);
            ApplyComponent<InventoryItemAuthoring>(
                root, required, DimensionItemAuthoringComponents.InventoryItem,
                component => ConfigureInventory(component, item, stacks, recipe, naming, report));

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

            // Durability is finished at the end of this method, not here.
            //
            // The framework used to say a written durability "reads back as 1" and warn that the
            // author's number had been thrown away. It had not been: DurabilityAuthoring recomputes
            // durability on prefab import from the object's OWN type and initialAmount, and both of
            // those were wrong — the type was the enum default, which matches no case in the game's
            // formula, and initialAmount was a hardcoded 1, which the formula then returns
            // unchanged. That is the whole of the 1. With the type now written, the same formula
            // gives the game's own number, so the multipliers go on here and
            // ApplyDurabilityTheGameWay runs the formula once the cooldown is in place (a melee
            // weapon's durability is divided by it).
            ApplyComponent<DurabilityAuthoring>(
                root, required, DimensionItemAuthoringComponents.Durability,
                component =>
                {
                    // The multipliers are the half that survives the import. maxDurability is
                    // recomputed from the item type times durabilityMultiplier, which is why a raw
                    // number never stuck and this one does.
                    component.durabilityMultiplier = item.DurabilityMultiplier;
                    component.repairMultiplier = item.RepairMultiplier;
                    component.reinforceCostMultiplier = item.ReinforceCostMultiplier;

                    // The author's own number is NOT written here. It goes into initialAmount in
                    // ApplyDurabilityTheGameWay, because that is the only field it survives in:
                    // DurabilityAuthoring.OnValidate recomputes durability and maxDurability from
                    // the object's type and initialAmount every time the prefab is imported, so a
                    // number written straight into either of them lasts until the next import and
                    // no longer.
                });

            ApplyComponent<WeaponDamageAuthoring>(
                root, required, DimensionItemAuthoringComponents.WeaponDamage,
                component =>
                {
                    component.damage = item.DamageAmount;
                    component.damageMultiplier = item.DamageMultiplierForItsTier;
                    component.isMagic = item.DamageIsMagic;
                    component.isRange = item.DamageIsRanged;
                });

            // Resolved once, and the one number every later pass uses. It has to be worked out
            // before the component is attached because the effects pass further down owns the same
            // component and would otherwise settle it a second time — which is exactly what used
            // to happen: the effects block carried a cooldown of its own, ran last, and destroyed
            // the component whenever that second field was blank. Whatever a creator typed on the
            // item was deleted before the prefab was written, so it set neither the swing rate nor
            // the durability divisor and every melee weapon came out at the game's default.
            float cooldownSeconds = ResolveCooldownSeconds(item, kind, required, report);

            ApplyComponent<CooldownAuthoring>(
                root, cooldownSeconds > 0f || item.Effects.SharesACooldown,
                component =>
                {
                    // The number goes into CooldownCD verbatim, and the slots that read it —
                    // MeleeWeaponSlot, RangeWeaponSlot, SummoningWeaponSlot, BeamWeaponSlot,
                    // EatableSlot — start at the game's own default and then OVERWRITE it with the
                    // item's number whenever the component is there. A component left holding zero
                    // is therefore not "nothing set", it is a weapon that swings with no delay at
                    // all, which is why zero is never what is written.
                    if (cooldownSeconds > 0f)
                    {
                        component.cooldown = cooldownSeconds;
                    }

                    component.casualCharacterIgnoresCustomCooldown = item.CasualIgnoresItsCooldown;
                });

            // Health backs destructible props, creatures, and bombs. A bomb needs it for a reason
            // the other two do not share: every way of setting an explosive off runs through its
            // health reaching zero, so a bomb without a health pool can never be broken by hand and
            // can never be caught in another bomb's chain reaction.
            // The archetype OR the answer: a barrel that happens to explode is a Placeable, and it
            // still needs the health pool every trigger runs through.
            bool isExplosive =
                Requires(required, DimensionItemAuthoringComponents.Explosive) ||
                item.Explosive.Explodes;
            // Asked of the item rather than recomputed here, because the bootstrap emitter has to
            // reach the same answer to decide whether a shed-loot row can ever land.
            bool needsHealth = item.GetsAHealthPool;
            ApplyComponent<HealthAuthoring>(
                root, needsHealth,
                component => component.maxHealth =
                    isExplosive ? item.Explosive.HowToughItIs : item.HealthPoints);

            ApplyComponent<DamageableObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);
            ApplyComponent<DestructibleObjectAuthoring>(
                root, required, DimensionItemAuthoringComponents.Breakable, null);

            // lootTableID is a game enum, not free text: an id the game does not define would
            // otherwise silently resolve to whatever sits at index 0, so it is matched by name
            // and reported when it does not exist.
            DimensionObjectSpine.ApplyWorldRoles(
                root,
                item.WorldRoles,
                ResolveObjectByName,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                },
                IsDeferred,
                QualifyReference);

            // AFTER the world roles, and that order is load-bearing. Both passes write the
            // summoning-by-name component: the world-roles pass owns the list the item itself
            // names, writing it whole so a removed boss goes, and this one unions in the bosses
            // that named this item as their offering. The other way round, whichever ran second
            // would erase the first.
            ConfigureSummoning(root, item, report);

            DimensionObjectSpine.ApplyInstrument(
                root,
                item.Instrument,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplyExtraLoot(
                root,
                item.ExtraLoot,
                ResolveObjectByName,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                },
                IsDeferred);

            ApplyComponent<DropLootAuthoring>(
                root, required, DimensionItemAuthoringComponents.Loot,
                component =>
                {
                    // The shared resolver, not a bare enum parse: a vanilla table by its game
                    // name OR one of this mod's own tables by its id — this was the one loot
                    // field that refused the mod's tables while its siblings accepted them.
                    LootTableID resolved;
                    if (!string.IsNullOrEmpty(item.LootTableId) &&
                        DimensionEditorLootTables.TryResolve(item.LootTableId, out resolved))
                    {
                        DropLootAuthoring loot = (DropLootAuthoring)component;
                        loot.lootTableID = resolved;
                    }
                    else
                    {
                        TrySetEnumProperty(
                            component, "lootTableID", item.LootTableId, item, report);
                    }
                });

            // The three things an item does BEYOND being an item: what it grants you, what
            // right-clicking does, and what it looks like on your character. All three were
            // attached as bare components with nothing written into them, so a custom set of
            // armour equipped, granted nothing, did nothing on right-click, and left the character
            // on screen bare.
            DimensionObjectSpine.ApplyItemEffects(
                root,
                item.Effects,
                cooldownSeconds,
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        Describe(item) + ": '" + unknown + "' is not a condition the game has.");
                },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                Requires(required, DimensionItemAuthoringComponents.EquipmentConditions));

            DimensionObjectSpine.ApplyAttackSounds(
                root,
                item.AttackSounds,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplySimpleTraits(
                root,
                item.SimpleTraits,
                delegate(string message)
                {
                    report.Warnings.Add(Describe(item) + ": " + message);
                });

            DimensionObjectSpine.ApplyBasics(
                root,
                item.Basics,
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });
            DimensionObjectSpine.ApplyInitialConditions(
                root,
                item.Conditions,
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });

            DimensionObjectSpine.ApplyOffHandAndCost(
                root,
                item.OffHand,
                item.PolishesInto,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplyItemKinds(
                root,
                item.IsAPotion,
                item.ScansForObjectId,
                item.SummonsInsteadOfScanning,
                item.ScannerOnlyInBiome,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplyExplosive(
                root,
                item.Explosive,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); },
                NamesOneOfOurBlasts);

            // The DIRECTLY ASSIGNED icon, not the one an icon id might find. Reading colours out
            // of a sprite located by a fuzzy name search would sample whatever asset happened to
            // match, and a palette taken from the wrong picture is a mistake nobody would think to
            // look for. An item with only an icon id keeps the four colours typed on it.
            DimensionObjectSpine.ApplyCooking(
                root,
                item.Cooking,
                item.IconSprite,
                delegate(string objectId) { return ResolveObjectByName(objectId); },
                delegate(string ingredientId)
                {
                    DimensionCookingTemplate found;
                    return ingredientsById != null &&
                        ingredientsById.TryGetValue(ingredientId, out found)
                            ? found
                            : null;
                },
                delegate(string referencedId) { return naming.Owns(referencedId); },
                delegate(string unknown)
                {
                    report.Warnings.Add(
                        Describe(item) + ": '" + unknown + "' is not a condition the game has.");
                },
                delegate(string message) { report.Warnings.Add(Describe(item) + " " + message); });

            DimensionObjectSpine.ApplyWeapon(
                root,
                item.Weapon,
                delegate(string projectileId) { return ResolveObjectByName(projectileId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + ": " + message); },
                IsDeferred);

            DimensionObjectSpine.ApplySecondaryUse(
                root,
                item.SecondaryUse,
                delegate(string itemId) { return ResolveObjectByName(itemId); },
                delegate(string message) { report.Warnings.Add(Describe(item) + ": " + message); },
                IsDeferred);

            ScriptableDataBlock skin = DimensionEquipmentSkinGenerator.EnsureSkin(
                item.EquipmentSkin,
                item.ItemId,
                outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName,
                delegate(string message) { report.Warnings.Add(message); });
            DimensionObjectSpine.ApplyEquipmentSkin(root, skin);
            if (skin == null)
            {
                DimensionEquipmentSkinGenerator.RemoveSkinIfPresent(
                    item.ItemId,
                    outputFolder + "/" + DimensionEquipmentSkinGenerator.FolderName);
            }

            if (Requires(required, DimensionItemAuthoringComponents.BossEncounter))
            {
                report.Warnings.Add(
                    Describe(item) +
                    ": boss encounter wiring (phases, arena, music) has no framework path yet; " +
                    "the prefab is generated without it.");
            }

            // A block item that belongs to a tileset becomes a real custom tile: stamp the tileset
            // identity and attach the ground/wall dig/mine/spawn/crack behaviour on top of the
            // placeable object the Block archetype already produced.
            if (blockIndex != null &&
                blockIndex.TryGetValue(item.ItemId, out TilesetBlockBinding binding))
            {
                DimensionTilesetBlockAuthoring.Apply(root, binding.Tileset, binding.IsWall, report);
            }

            // An item a tileset lists as its ore becomes the vein itself — same object, same
            // ObjectID, exactly as CopperOreEntity IS ObjectID.CopperOre. Applied after the block
            // stamp so a block item mistakenly also listed as an ore keeps its block identity and
            // the conflict shows up as the duplicate TileAuthoring it is, rather than silently
            // turning a wall into a vein.
            if (oreIndex != null &&
                oreIndex.TryGetValue(item.ItemId, out DimensionTilesetAsset oreTileset))
            {
                DimensionTilesetOreAuthoring.Apply(root, oreTileset, report);
            }
            else
            {
                // Taking an ore off a block's list has to actually stop it being a vein. Prefabs are
                // updated in place, so without this the stamp from an earlier run would survive and
                // the item would keep generating in walls no tileset claims it for.
                DimensionTilesetOreAuthoring.ClearIfOre(root);
            }

            // Last, because it reads the finished object. The cooking pass above can still change
            // the type (food is decided there and nowhere else), the block and ore stamps above can
            // still change it, and the durability formula divides a melee weapon's number by the
            // cooldown component this method attached earlier.
            ApplyDurabilityTheGameWay(root, item, report);
        }

        /// <summary>
        /// The one time-between-uses this item ships with, or zero for "it does not have one".
        /// </summary>
        /// <remarks>
        /// <para>
        /// There is one field now. There used to be two — the item's own, and a second inside the
        /// effects block — and the effects pass runs last, so it won: whenever its own field was
        /// blank it deleted the cooldown component and took the creator's number with it. The item's
        /// field is the one that is drawn and the one three archetypes ask for, so it is the one
        /// that stays; <c>DimensionItemAsset.CooldownSeconds</c> still reads the old place when the
        /// new one is blank so no asset loses a number it already held.
        /// </para>
        /// <para>
        /// A blank field on a template that asks for a cooldown gets the game's own number rather
        /// than a zero. A present <c>CooldownCD</c> holding zero is not "nothing set": every slot
        /// that reads one starts at the game's default and then overwrites it with the item's,
        /// so zero is a weapon that swings with no delay at all.
        /// </para>
        /// </remarks>
        private static float ResolveCooldownSeconds(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            DimensionItemAuthoringComponents required,
            DimensionItemGenerationReport report)
        {
            if (item.CooldownSeconds > 0f)
            {
                return item.CooldownSeconds;
            }

            if (!Requires(required, DimensionItemAuthoringComponents.Cooldown) &&
                !item.Effects.SharesACooldown)
            {
                return 0f;
            }

            float vanilla = DimensionItemObjectTypes.VanillaCooldownFor(
                DimensionItemObjectTypes.ToObjectType(kind));

            // Derived rather than a warning: the item card already says a blank one takes the
            // game's default, and saying it twice for every unhurried item would bury the lines
            // that matter.
            report.Derived.Add(
                Describe(item) + ": no time between uses was set, so the game's own " + vanilla +
                " seconds went in. Left at zero it would have had no delay at all, not the default.");
            return vanilla;
        }

        /// <summary>
        /// Whether several of these share one inventory slot, decided by what the thing is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The kind decides because <c>initialAmount</c> means two things and the game picks between
        /// them by this one bool: on anything that wears out it is the durability the item starts
        /// with, and on anything that stacks it is the number a player is handed. Counted over the
        /// 3,073 vanilla prefabs, not one of the 343 carrying a durability pool is stackable — so a
        /// generated helmet answering "yes, it stacks" is handed out ninety at a time.
        /// </para>
        /// <para>
        /// Where the game itself goes both ways the creator's answer stands: off-hands (8 of 38
        /// stack), ranged weapons (1 of 35), cast items (45 of 60), placeables (1,260 of 1,281) and
        /// things that are nothing in particular (123 of 139). Where it never goes both ways the
        /// kind wins and the report says it did.
        /// </para>
        /// </remarks>
        private static bool ResolveStacking(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            bool keepsADurabilityPool,
            DimensionItemGenerationReport report)
        {
            bool stacks = DimensionWhatItIsRules.StacksGiven(kind, item.Stackable);
            DimensionStacking rule = DimensionWhatItIsRules.StackingFor(kind);
            string it = DimensionWhatItIsRules.Describe(kind);

            // The shared-timer check used to live here, where a world object could never reach it.
            // It is now beside the write, in DimensionObjectSpine.ApplyItemEffects, which both this
            // generator and the world-object generator go through.

            if (rule == DimensionStacking.NeverStacks && item.Stackable)
            {
                report.Derived.Add(
                    Describe(item) + " was generated as one per slot. It is " + it +
                    ", and none of the game's own stack.");
            }
            else if (rule == DimensionStacking.AlwaysStacks && !item.Stackable)
            {
                report.Derived.Add(
                    Describe(item) + " was generated as a stack. It is " + it +
                    ", and all of the game's own stack.");
            }

            if (stacks && keepsADurabilityPool)
            {
                stacks = false;
                report.Warnings.Add(
                    Describe(item) + ": it wears out AND it was set to stack, and it cannot do " +
                    "both — the same number is the uses left on one of them and the size of the " +
                    "pile. It was generated as one per slot. Untick stacking, or take the " +
                    "durability off it.");
            }

            return stacks;
        }

        /// <summary>
        /// Works out the durability and the starting amount the way Core Keeper works them out, and
        /// writes both.
        /// </summary>
        /// <remarks>
        /// <para>
        /// No new mechanism: this calls the game's own <c>CalculateObjectDurability</c> on the
        /// component that is already on the object, which is the same method the SDK runs on prefab
        /// import. Doing it here means the prefab on disk already holds the number, so nothing
        /// depends on an import having happened.
        /// </para>
        /// <para>
        /// <c>initialAmount</c> is the same field twice over. On anything carrying a durability pool
        /// the game reads it as the durability the item starts with — the crafting preview reads it
        /// verbatim, and the durability bar is this over maxDurability. On everything else it is the
        /// stack the player is handed. Vanilla always keeps the first equal to the computed maximum,
        /// and writes 1 for the second on all but a handful of objects.
        /// </para>
        /// <para>
        /// THE AUTHOR'S OWN NUMBER GOES INTO <c>initialAmount</c>, NOT INTO <c>durability</c>. That
        /// is not a preference; it is the only field it survives in.
        /// <c>DurabilityAuthoring.OnValidate</c> recomputes <c>durability</c> and
        /// <c>maxDurability</c> from the object's type and <c>initialAmount</c> on every prefab
        /// import, so a number written straight into either of them lasts until the next import and
        /// no longer. Fed in as the amount, it is the value the game's own formula hands back
        /// unchanged for every kind the formula has no case for — a seeder, a fishing rod, a bag, a
        /// lantern, anything cast — which is exactly the set where the typed number is the only
        /// durability there is. On a kind the formula does have a case for, the formula wins, which
        /// is what a helmet reading 90 and a pickaxe reading 800 means.
        /// </para>
        /// </remarks>
        private static void ApplyDurabilityTheGameWay(
            GameObject root,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring == null)
            {
                return;
            }

            ObjectType objectType = objectAuthoring.objectType;
            int authored = item.AuthoredDurability;

            DurabilityAuthoring durability = root.GetComponent<DurabilityAuthoring>();
            if (durability == null)
            {
                // Nothing here wears out, so initialAmount is the stack the player is handed, which
                // vanilla writes as 1 on all but a handful of objects.
                objectAuthoring.initialAmount = 1;

                // AND SAY SO IF THEY FILLED THE BOXES IN. Wear, repair and reinforce are drawn on
                // every item, and thirteen of the sixteen templates never get a durability pool at
                // all — so those four numbers were typed, saved, and thrown away here without a
                // word. They still are thrown away; what changes is that the person is told.
                if (item.HasWearSettingsThatWillBeIgnored)
                {
                    report.Warnings.Add(
                        Describe(item) +
                        ": wear, repair or reinforce numbers are filled in, but a " +
                        DimensionItemArchetypeRules.Describe(item.Archetype) +
                        " never wears out, so none of them are read. Only a tool, a weapon and a " +
                        "piece of armour have durability — change the template, or clear those " +
                        "numbers.");
                }

                return;
            }

            // A thrown weapon is a stack, not a pool. Not one of the game's seven carries a
            // durability component, and on a stackable item initialAmount IS the number handed
            // over — so running the formula here would deal out 250 knives at a time and hang a
            // durability bar off a stack that has no maximum to fill. An author who typed a
            // durability anyway is taken at their word and keeps the pool; ResolveStacking above
            // has already taken the stacking off that one, because nothing in the game does both.
            if (objectType == ObjectType.ThrowingWeapon && authored <= 0)
            {
                RemoveComponentIfPresent<DurabilityAuthoring>(root);
                objectAuthoring.initialAmount = 1;
                report.Derived.Add(
                    Describe(item) + " was generated without a durability pool and as a stack of " +
                    "one, the way all seven of the game's throwing weapons are. Type a number " +
                    "under Durability points if you want one that wears out instead.");
                return;
            }

            int computed = DurabilityTheGameWouldGive(
                root, durability, authored > 0 ? authored : 1, objectType, item, report);
            durability.durability = computed;
            durability.maxDurability = computed;
            objectAuthoring.initialAmount = computed;
        }

        /// <summary>
        /// The number the game's own formula gives for this object, without changing the object to
        /// get it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Melee, ranged and thrown are the three types whose durability is divided by the time
        /// between uses. A cooldown component sitting at zero would divide by nothing and round
        /// infinity, which lands on int.MinValue — a weapon that reads as permanently reinforced,
        /// draws a negative bar, and breaks on its first swing.
        /// </para>
        /// <para>
        /// The cooldown site in <c>Configure</c> never leaves a zero behind any more, so the guard
        /// below is insurance rather than the everyday path. It is worth having because the cost of
        /// being wrong is silent and total. It closes the hole by writing the game's own number
        /// into the field: switching the component off would leave a <c>CooldownCD</c> of zero
        /// behind, because the converter reads the component through <c>GetComponent</c> and never
        /// asks whether it is enabled, and a present zero is what a slot reads as no delay at all.
        /// </para>
        /// </remarks>
        private static int DurabilityTheGameWouldGive(
            GameObject root,
            DurabilityAuthoring durability,
            int amount,
            ObjectType objectType,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            CooldownAuthoring cooldown = root.GetComponent<CooldownAuthoring>();
            bool wouldDivideByNothing =
                DimensionItemObjectTypes.DurabilityDividesByCooldown(objectType) &&
                cooldown != null &&
                cooldown.enabled &&
                cooldown.cooldown <= 0f;

            if (!wouldDivideByNothing)
            {
                return durability.CalculateObjectDurability(amount, objectType);
            }

            // Written into the field, not switched off. A disabled cooldown component still bakes a
            // CooldownCD of zero — the converter reads it through GetComponent and never asks
            // whether it is enabled — and every slot honours a present zero as no delay at all. The
            // game's own number is the only thing that leaves both the durability sum and the swing
            // rate right.
            float vanilla = DimensionItemObjectTypes.VanillaCooldownFor(objectType);
            cooldown.cooldown = vanilla;
            report.Warnings.Add(
                Describe(item) + ": its time between uses came out at zero, which its durability " +
                "would have been divided by. The game's own " + vanilla + " seconds went in " +
                "instead. Set a time between uses on the item.");

            // The fallback divisor and the numerator's own constant are the same number, so the
            // base comes back undivided: 350 for a swing, 250 for a shot or a throw.
            float baseDurability = objectType == ObjectType.MeleeWeapon ? 350f : 250f;
            return Mathf.RoundToInt(baseDurability * durability.durabilityMultiplier);
        }

        /// <summary>
        /// The kind the item said it was, or the one its other answers imply when it never said.
        /// </summary>
        /// <remarks>
        /// The item's own blocks are asked before the template is, because they are more specific:
        /// an item whose weapon block already says "swung" has said it is a melee weapon in
        /// everything but name, and a piece of armour that says it is drawn on the head has said it
        /// is a helmet. Only when neither has anything to say does the template decide.
        /// </remarks>
        private static DimensionWhatItIs ResolveWhatItIs(
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            DimensionWhatItIs kind = item.ResolveWhatItIs(out DimensionWhatItIsSource source);
            string it = DimensionWhatItIsRules.Describe(kind);

            switch (source)
            {
                case DimensionWhatItIsSource.HowItAttacks:
                    report.Derived.Add(
                        Describe(item) + " was generated as " + it +
                        ", from how it says it attacks.");
                    break;

                case DimensionWhatItIsSource.WhereItIsWorn:
                    report.Derived.Add(
                        Describe(item) + " was generated as " + it +
                        ", from where it says it is drawn on the character.");
                    break;

                case DimensionWhatItIsSource.ItsTemplate:
                    if (DimensionWhatItIsRules.ArchetypeCannotSayWhatItIs(item.Archetype))
                    {
                        report.Warnings.Add(
                            Describe(item) + ": nothing on it says what it is, and a " +
                            DimensionItemArchetypeRules.Describe(item.Archetype).ToLowerInvariant() +
                            " covers several kinds at once, so it was generated as " + it +
                            ". Set What It Is to the one you meant.");
                    }
                    else
                    {
                        report.Derived.Add(
                            Describe(item) + " was generated as " + it + ", from its template.");
                    }

                    break;
            }

            return kind;
        }

        /// <summary>
        /// The kinds where "it starts at one use" is a problem rather than the right answer.
        /// </summary>
        /// <remarks>
        /// A necklace, a ring and a thrown weapon are meant not to wear out — the first two reach an
        /// empty case in the game's formula on purpose, and no vanilla throwing weapon carries a
        /// pool at all. The kinds that never said what they are, or said nothing in particular, are
        /// left out because they already get a louder line of their own.
        /// </remarks>
        private static bool WearingOutIsMeaningfulFor(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.NotSaid:
                case DimensionWhatItIs.NothingInParticular:
                case DimensionWhatItIs.SomethingYouPlace:
                case DimensionWhatItIs.Critter:
                case DimensionWhatItIs.Food:
                case DimensionWhatItIs.Necklace:
                case DimensionWhatItIs.Ring:
                case DimensionWhatItIs.ThrowingWeapon:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Says where the kind and the rest of the item disagree, in the terms the author answered
        /// in. Nothing here stops generation: each of these produces an object that exists, sits in
        /// the right slot, and quietly does nothing, which is exactly the failure a creator cannot
        /// see without being told.
        /// </summary>
        private static void ReportKindProblems(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            DimensionItemAuthoringComponents required,
            DimensionItemGenerationReport report)
        {
            string it = DimensionWhatItIsRules.Describe(kind);

            // PlaceObjectSlot refuses to put anything down that is not a placeable prefab, a critter
            // or cattle. Both halves of that are worth saying, because both produce an object that
            // looks finished.
            bool placedInTheWorld =
                Requires(required, DimensionItemAuthoringComponents.Placement) ||
                Requires(required, DimensionItemAuthoringComponents.Creature);
            if (kind == DimensionWhatItIs.SomethingYouPlace && !placedInTheWorld)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", but its template attaches nothing that " +
                    "places it, so the game has no footprint to put down. Use the Placeable " +
                    "object template, or say what it is some other way.");
            }
            else if (placedInTheWorld &&
                     kind != DimensionWhatItIs.SomethingYouPlace &&
                     kind != DimensionWhatItIs.Critter)
            {
                // Pet and Food used to be exempt here, against the rule the line itself prints. The
                // game routes a pet to the gear slots and food to the eating slot, and
                // PlaceObjectSlot turns both away — so a pet or a dish on a placing template is
                // exactly the case worth saying out loud, not one to skip.
                report.Warnings.Add(
                    Describe(item) + ": its template places it in the world, but it is " + it +
                    ". The game only lets a player set down something you place or a critter, so " +
                    "this one can be carried and never put anywhere.");
            }

            if (DimensionWhatItIsRules.IsAWeapon(kind))
            {
                if (!item.Weapon.IsAWeapon)
                {
                    report.Warnings.Add(
                        Describe(item) + ": it is " + it + ", but its 'As a weapon' block says it " +
                        "is not a weapon, so nothing on it makes the player swing, fire or cast.");
                }

                // A summoning weapon is the exception on purpose: not one of the game's seven
                // carries weapon damage, because the thing it summons does the hitting.
                if (kind != DimensionWhatItIs.SummoningWeapon &&
                    !Requires(required, DimensionItemAuthoringComponents.WeaponDamage))
                {
                    report.Warnings.Add(
                        Describe(item) + ": it is " + it + ", but its template carries no damage, " +
                        "so it equips to a weapon slot and hits for nothing.");
                }
            }

            // A DIGGING TOOL HITS FOR ITS MULTIPLIER, and that number can be typed to zero. The
            // block above only covers the five kinds a player fights with, so the six that dig,
            // smash and drill fell through it — and a pick with the multiplier zeroed carries a
            // damage component that computes nothing per level, mines nothing, and said nothing.
            // The multiplier is what the game's per-level curve reads, so it is the number named.
            if (Requires(required, DimensionItemAuthoringComponents.WeaponDamage) &&
                !DimensionWhatItIsRules.IsAWeapon(kind) &&
                item.DamageAmount <= 0 &&
                item.DamageMultiplierForItsTier <= 0f)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and both its damage and its damage " +
                    "multiplier are zero, so it hits for nothing at every level and breaks nothing " +
                    "it is swung at. Put the multiplier back to 1, or type a damage.");
            }

            if (DimensionWhatItIsRules.IsWornArmour(kind) && !item.EquipmentSkin.IsWorn)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and it will equip and grant its stats, " +
                    "but nothing under 'Worn on the character' says how to draw it, so the " +
                    "character on screen stays bare.");
            }

            if (DimensionWhatItIsRules.AlwaysWearsOut(kind) &&
                !Requires(required, DimensionItemAuthoringComponents.Durability))
            {
                // Counted rather than assumed. Melee 35 of 36, ranged 32 of 35, pickaxe 9 of 10 and
                // beam 2 of 4 wear out; the ones that do not are legendary gear, the snowball and
                // the lightning gun. Telling somebody building an unbreakable legendary that
                // vanilla has none of those would be a lie they could check in ten seconds.
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and " +
                    (DimensionWhatItIsRules.SomeVanillaOnesNeverWearOut(kind)
                        ? "every vanilla one but the legendary gear wears out"
                        : "every vanilla one of those wears out") +
                    ", but its template carries no durability, so this one never will.");
            }

            // The kinds the game keeps no durability number for. A hoe works its 250 out on its
            // own; a seeder, which a player would call the same sort of tool, does not — both of
            // vanilla's carry 250 because their prefabs say 250. Nothing about the item shows the
            // difference, so it has to be said.
            if (Requires(required, DimensionItemAuthoringComponents.Durability) &&
                item.AuthoredDurability <= 0 &&
                DimensionWhatItIsRules.TheGameHasNoDurabilityNumberFor(kind) &&
                WearingOutIsMeaningfulFor(kind))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and the game has no durability of its " +
                    "own for one — the ones it ships carry a number written on them. Nothing is " +
                    "typed under Durability points, so this starts at 1 use and breaks the first " +
                    "time it is used.");
            }

            string missing = DimensionWhatItIsRules.MissingPieceFor(kind);
            if (!string.IsNullOrEmpty(missing))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", which needs " + missing +
                    " to do anything. The framework has no answer for that yet, so it will take " +
                    "its slot and sit there.");
            }

            if (kind == DimensionWhatItIs.Instrument &&
                (item.Instrument == null || !item.Instrument.IsAnInstrument))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", but its music block does not say it is " +
                    "one, so it takes the instrument slot and plays nothing.");
            }

            // Food is the one kind the item does not get to assert on its own: the cooking pass sets
            // the type from the cooking answer and clears it again when that answer says no.
            bool takesPartInCooking = item.Cooking != null && item.Cooking.TakesPartInCooking;
            if (kind == DimensionWhatItIs.Food && !takesPartInCooking)
            {
                report.Warnings.Add(
                    Describe(item) + ": it says it is food, but its cooking block says it plays no " +
                    "part in cooking, and that is the answer the game reads. It was generated as " +
                    "nothing in particular. Set 'Part in Cooking' under 'As food'.");
            }
            else if (takesPartInCooking &&
                     item.WhatItIs != DimensionWhatItIs.NotSaid &&
                     item.WhatItIs != DimensionWhatItIs.Food)
            {
                // Only when the author picked something else by hand. A worked-out kind is not a
                // disagreement: every ordinary food item is a Material with a cooking block, and
                // saying so on each of them would bury the lines that matter.
                report.Warnings.Add(
                    Describe(item) + ": its cooking block makes it food, and the game decides " +
                    "eating from the same answer that decides a slot, so it was generated as food " +
                    "rather than " + it + ".");
            }
        }

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

        private static void ConfigureLocalization(
            GameObject root,
            DimensionItemAsset item,
            DimensionNamingContext naming,
            DimensionItemGenerationReport report)
        {
            LocalizationAuthoring localization = EnsureComponent<LocalizationAuthoring>(root);

            // Must be the QUALIFIED name: the CSV rows this run writes are keyed by the qualified
            // object name, so an unqualified term key would look up a row that is not there and the
            // item would show its raw id in game.
            localization.termKey = naming.QualifyGenerated(item.ItemId);
            // LanguageGender is a PAIR, not an enum: a SystemLanguage and a Gender. Authors write it
            // as "English:Neutral" so one plain text field covers both halves, which is why this
            // does not go through the usual single-enum parse.
            localization.languageGenders = new List<LanguageGender>();
            string[] genders = item.NameGendersPerLanguage;
            for (int g = 0; g < genders.Length; g++)
            {
                if (string.IsNullOrEmpty(genders[g]))
                {
                    continue;
                }

                string[] halves = genders[g].Split(':');
                SystemLanguage language;
                Gender gender;
                if (halves.Length == 2 &&
                    System.Enum.TryParse(halves[0].Trim(), false, out language) &&
                    System.Enum.TryParse(halves[1].Trim(), false, out gender))
                {
                    localization.languageGenders.Add(
                        new LanguageGender { language = language, gender = gender });
                }
                else
                {
                    report.Warnings.Add(
                        Describe(item) + ": cannot read '" + genders[g] + "' as a language and a " +
                        "gender. Write it as English:Neutral.");
                }
            }

            if (localization.languageGenders.Count == 0)
            {
                TrySetArraySize(localization, "languageGenders", 0);
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
