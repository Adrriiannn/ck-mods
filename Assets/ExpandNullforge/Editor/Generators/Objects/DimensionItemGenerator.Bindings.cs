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
    /// Joining an item to the things it is bound to: a block, an ore, a boss, a recipe.
    /// </summary>
    internal static partial class DimensionItemGenerator
    {
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
            List<string> ownedObjectIds,
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
                manifest.SetGeneratedObjectIds(
                    ownedObjectIds == null ? new string[0] : ownedObjectIds.ToArray());
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
    }
}
