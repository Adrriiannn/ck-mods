using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionFrameworkAuthoringAssetActionResult
    {
        public DimensionFrameworkAuthoringAssetActionResult(
            bool executed,
            Object createdObject,
            string message)
        {
            Executed = executed;
            CreatedObject = createdObject;
            Message = message ?? string.Empty;
        }

        public bool Executed { get; private set; }

        public Object CreatedObject { get; private set; }

        public string Message { get; private set; }
    }

    internal static class DimensionFrameworkAuthoringAssetUtility
    {
        public static DimensionFrameworkAuthoringAssetActionResult CreateLayoutTemplate(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating a layout.");
            }

            DimensionLayoutTemplateAsset layout =
                CreateAsset<DimensionLayoutTemplateAsset>(
                    template,
                    "Layout",
                    "Layout");
            string biomeId = ResolvePrimaryBiomeId(template);
            layout.ApplySingleBiomeSquarePreset(
                biomeId,
                biomeId,
                64);
            SetObjectReference(template, "layoutTemplate", layout);
            SaveAndSelect(layout);
            return Success(layout, "Created and assigned a layout template.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult AddLayoutRegion(
            DimensionLayoutTemplateAsset layout,
            BiomeTemplateAsset biome)
        {
            if (layout == null)
            {
                return Failure("Assign or create a layout template before adding regions.");
            }

            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty regions = serialized.FindProperty("regions");
            if (regions == null || !regions.isArray)
            {
                return Failure("The layout template does not expose editable manual regions.");
            }

            int index = regions.arraySize;
            regions.InsertArrayElementAtIndex(index);
            SerializedProperty element = regions.GetArrayElementAtIndex(index);
            string biomeId = biome == null ? string.Empty : biome.BiomeId;
            string regionId = "region-" + (index + 1).ToString();
            SetRelativeString(element, "regionId", regionId);
            SetRelativeString(element, "biomeId", biomeId);
            SetRelativeString(element, "zoneId", biomeId);
            SetRelativeString(element, "displayName", string.IsNullOrEmpty(biomeId) ? "Region" : biome.DisplayName + " Region");
            SetRelativeVector2Int(element, "localMin", new Vector2Int(-64 + index * 128, -64));
            SetRelativeVector2Int(element, "localMaxExclusive", new Vector2Int(64 + index * 128, 64));
            SetRelativeInt(element, "priority", index);
            SetRelativeBool(element, "enabled", true);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(layout, "Added a layout region for " + (string.IsNullOrEmpty(biomeId) ? "the selected layout" : biomeId) + ".");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreatePortalAccessRule(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding portal access.");
            }

            int index = template.PortalAccessRules.Length + 1;
            string baseId = ResolveScopedId(template, "Portal" + index.ToString());
            DimensionPortalAccessRuleAsset rule =
                CreateAsset<DimensionPortalAccessRuleAsset>(
                    template,
                    "Portals",
                    "PortalAccessRule" + index.ToString());
            rule.Configure(
                baseId + ".rule",
                baseId,
                baseId + ".presentation",
                template.DisplayName + " Portal",
                DimensionIds.Overworld,
                Vector2.zero,
                template.DimensionId,
                Vector2.zero,
                DimensionPortalAccessKind.PlacedPortal,
                DimensionPortalActivationMode.VanillaCooldown,
                2.0f,
                true,
                true,
                "Enter " + template.DisplayName,
                "The portal is not active yet.",
                string.Empty,
                string.Empty,
                string.Empty,
                index,
                true,
                new DimensionPortalRequiredItemTemplate[0]);
            AppendObjectReference(template, "portalAccessRules", rule);
            SaveAndSelect(rule);
            return Success(rule, "Created a placed-portal access rule.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateBiome(
            DimensionTemplateAsset template,
            BiomeTemplateAsset source)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a biome.");
            }

            int index = template.Biomes.Length + 1;
            string biomeId = ResolveScopedId(template, "Biome" + index.ToString());
            BiomeTemplateAsset biome = source == null
                ? CreateAsset<BiomeTemplateAsset>(template, "Biomes", "Biome" + index.ToString())
                : DuplicateAsset(template, "Biomes", "Biome" + index.ToString(), source);
            biome.ConfigureIdentity(
                biomeId,
                "Biome " + index.ToString(),
                ResolveMapColor(index),
                index,
                true);
            biome.ApplyFallbackLocalBounds(
                new Vector2Int(-64 + index * 128, -64),
                new Vector2Int(64 + index * 128, 64));
            biome.ApplySemanticTerrainPreset(
                new[] { ResolveScopedId(template, "GroundBiome" + index.ToString() + "Block") },
                new[] { ResolveScopedId(template, "WallBiome" + index.ToString() + "Block") },
                new string[0],
                new string[0],
                source == null);
            AppendObjectReference(template, "biomes", biome);
            SaveAndSelect(biome);
            return Success(biome, source == null ? "Created a biome." : "Duplicated the selected biome.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult RemoveBiomeReference(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null || biome == null)
            {
                return Failure("Select a biome reference before removing it.");
            }

            bool removed = RemoveObjectReference(template, "biomes", biome);
            if (!removed)
            {
                return Failure("The selected biome is not referenced by this Dimension Asset.");
            }

            SaveAndSelect(template);
            return Success(template, "Removed the biome reference. The biome asset file was left on disk.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateScene(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a scene.");
            }

            int index = CountScenes(template, biome) + 1;
            string sceneId = ResolveScopedId(template, "Scene" + index.ToString());
            SceneTemplateAsset scene =
                CreateAsset<SceneTemplateAsset>(
                    template,
                    "Scenes",
                    "Scene" + index.ToString());
            scene.ConfigureIdentity(
                sceneId,
                sceneId + ".template",
                "Scene " + index.ToString(),
                "scene",
                string.Empty,
                index,
                true);
            scene.ConfigureSpawnPolicy(1, false, true);
            scene.ApplyAutomaticPlacement(new Vector2Int(16, 16));
            if (biome != null)
            {
                scene.SetAllowedBiomeIds(new[] { biome.BiomeId });
                AppendObjectReference(biome, "scenePool", scene);
            }
            else
            {
                AppendObjectReference(template, "globalScenes", scene);
            }

            SaveAndSelect(scene);
            return Success(scene, biome == null ? "Created a global scene." : "Created a biome scene.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateResourceNode(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a resource node.");
            }

            int index = CountResourceNodes(template, biome) + 1;
            string nodeId = ResolveScopedId(template, "ResourceNode" + index.ToString());
            ResourceNodeTemplateAsset node =
                CreateAsset<ResourceNodeTemplateAsset>(
                    template,
                    "Resources",
                    "ResourceNode" + index.ToString());
            string zoneId = biome == null ? string.Empty : biome.BiomeId;
            node.ConfigureIdentity(
                nodeId,
                "Resource Node " + index.ToString(),
                zoneId,
                ResolveScopedId(template, "Resource" + index.ToString()),
                DimensionResourceNodeKind.Custom,
                string.Empty,
                string.Empty,
                1,
                index,
                true);
            node.ApplyLocalBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32));
            AppendObjectReference(biome == null ? (Object)template : biome, biome == null ? "globalResourceNodes" : "resourceNodes", node);
            SaveAndSelect(node);
            return Success(node, biome == null ? "Created a global resource node." : "Created a biome resource node.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateItem(
            DimensionTemplateAsset template,
            DimensionItemKind kind)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an item.");
            }

            int index = template.GlobalItems.Length + 1;
            DimensionItemAsset item =
                CreateAsset<DimensionItemAsset>(
                    template,
                    "Resources",
                    kind.ToString() + index.ToString());
            SetSerializedString(item, "itemId", ResolveScopedId(template, kind.ToString() + index.ToString()));
            SetSerializedString(item, "displayName", ObjectNames.NicifyVariableName(kind.ToString()) + " " + index.ToString());
            SetSerializedEnum(item, "kind", (int)kind);
            SetSerializedBool(item, "enabled", true);
            AppendObjectReference(template, "globalItems", item);
            SaveAndSelect(item);
            return Success(item, "Created an item definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateTileset(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a tileset.");
            }

            int index = template.Tilesets.Length + 1;
            DimensionTilesetAsset tileset =
                CreateAsset<DimensionTilesetAsset>(
                    template,
                    "Tilesets",
                    "Tileset" + index.ToString());
            // The modder edits only the friendly block name; the mod prefix is stamped once here so
            // the identity ("{mod}:{name}") is self-contained on the asset at runtime.
            SetSerializedString(tileset, "blockName", "New Block " + index.ToString());
            SetSerializedString(tileset, "modPrefix", ResolveModPrefix(template));
            SetSerializedBool(tileset, "enabled", true);
            AppendObjectReference(template, "tilesets", tileset);
            SaveAndSelect(tileset);
            return Success(tileset, "Created a tileset definition.");
        }

        /// <summary>
        /// Creates a block from the Add-block wizard's choices: a tileset asset with the chosen type,
        /// states and item mode — and deliberately NO sheet, so the modder authors their own next.
        /// In create-item mode the "{name} Block" item is created immediately, seeded with the
        /// wizard's description and rarity.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult CreateWizardBlock(
            DimensionTemplateAsset template,
            DimensionTilesetWizardRequest request)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a block.");
            }

            if (request == null || string.IsNullOrEmpty(request.BlockName))
            {
                return Failure("The block needs a name.");
            }

            DimensionTilesetAsset tileset =
                CreateAsset<DimensionTilesetAsset>(template, "Tilesets", SanitizeAssetName(request.BlockName));
            SetSerializedString(tileset, "blockName", request.BlockName);
            SetSerializedString(tileset, "modPrefix", ResolveModPrefix(template));
            SetSerializedBool(tileset, "enabled", true);
            SetSerializedString(tileset, "blockType", request.TypeKey ?? "terrain");
            SetSerializedInt(tileset, "itemMode", (int)request.ItemMode);
            SetSerializedInt(tileset, "reskinTilesetIndex", request.ReskinIndex);
            EnableTilesetStates(tileset, request.StateKeys);
            AppendObjectReference(template, "tilesets", tileset);

            // Create-item mode: make the item now, seeded with the wizard's description/rarity, so
            // the modder's next stop is just dropping their two icons on it.
            if (request.ItemMode == DimensionTilesetItemMode.CreateItem)
            {
                EnsureTilesetBlockItems(template);
                DimensionItemAsset item = FindGlobalItem(template, tileset.WallBlockItemId);
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(request.Description))
                    {
                        SetSerializedString(item, "description", request.Description);
                    }

                    if (!string.IsNullOrEmpty(request.RarityId))
                    {
                        SetSerializedString(item, "rarityId", request.RarityId);
                    }
                }
            }

            // Compose the starter sheet (donor regions for exactly the chosen layers) and bake its
            // tileset data on the spot — the block leaves the wizard already rendering and playable;
            // repainting the sheet is the modder's own pace.
            string starterNote = ComposeStarterSheet(template, tileset, request);

            SaveAndSelect(tileset);
            return Success(
                tileset,
                "Created \"" + request.BlockName + "\" (" +
                DimensionTilesetTypeCatalog.Resolve(request.TypeKey).DisplayName + "). " + starterNote);
        }

        // Builds and assigns the wizard block's starter sheet, then bakes its GEN data. Returns the
        // human-readable tail of the creation message; failures degrade to guidance, never abort the
        // block (the asset is still valid without a sheet).
        private static string ComposeStarterSheet(
            DimensionTemplateAsset template,
            DimensionTilesetAsset tileset,
            DimensionTilesetWizardRequest request)
        {
            DimensionTilesetType type = DimensionTilesetTypeCatalog.Resolve(request.TypeKey);
            if (!EditorTools.Generation.DimensionTilesetStarterSheet.TryCompose(
                    type,
                    request.StateKeys,
                    out Color32[] pixels,
                    out int width,
                    out int height,
                    out System.Collections.Generic.List<PugTilemap.LayerName> missing,
                    out string composeError))
            {
                return "No starter sheet yet: " + composeError;
            }

            string folder = ResolveSectionFolder(template, "Tilesets");
            EnsureFolder(folder);
            string sheetPath = AssetDatabase.GenerateUniqueAssetPath(
                folder + "/" + SanitizeAssetName(request.BlockName) + "Sheet.png");
            Texture2D sheet = EditorTools.Generation.DimensionTilesetPixelIo.SaveImageOrderPng(
                pixels, width, height, sheetPath);
            if (sheet == null)
            {
                return "No starter sheet yet: failed to write " + sheetPath + ".";
            }

            SetSerializedObjectReference(tileset, "tilesetTexture", sheet);

            if (!EditorTools.Generation.DimensionTilesetGenerator.Generate(tileset, out string genError))
            {
                return "Starter sheet ready (" + System.IO.Path.GetFileName(sheetPath) +
                       "), but baking failed: " + genError;
            }

            string missingNote = missing.Count > 0
                ? " (no donor art yet for: " + string.Join(", ", missing) + ")"
                : string.Empty;
            return "Starter sheet composed and baked — build the mod and it plays as-is; repaint " +
                   System.IO.Path.GetFileName(sheetPath) + " to make it yours." + missingNote;
        }

        // Only the wizard's chosen states are switched on; everything else stays off until the modder
        // ticks it later on the block's page.
        private static void EnableTilesetStates(
            DimensionTilesetAsset tileset,
            System.Collections.Generic.List<string> stateKeys)
        {
            SerializedObject serialized = new SerializedObject(tileset);
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.ClearArray();
            int i = 0;
            foreach (DimensionTilesetState state in DimensionTilesetStateCatalog.All)
            {
                bool on = state.Required ||
                          (stateKeys != null && stateKeys.Contains(state.Key));
                if (!on)
                {
                    continue;
                }

                layers.InsertArrayElementAtIndex(i);
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = state.Key;
                element.FindPropertyRelative("enabled").boolValue = true;
                element.FindPropertyRelative("texture").objectReferenceValue = null;
                i++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DimensionItemAsset FindGlobalItem(DimensionTemplateAsset template, string itemId)
        {
            DimensionItemAsset[] items = template.GlobalItems;
            if (items == null)
            {
                return null;
            }

            foreach (DimensionItemAsset item in items)
            {
                if (item != null && string.Equals(item.ItemId, itemId, System.StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static string SanitizeAssetName(string raw)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
            }

            return builder.Length == 0 ? "Block" : builder.ToString();
        }

        // Locates the vanilla dirt_tileset.png (the canonical source layout) in the project or the
        // package cache, so a starter tileset can be seeded with an editable copy of it.
        private static string FindDirtSheetPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("dirt_tileset t:Texture2D"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("dirt_tileset.png", System.StringComparison.OrdinalIgnoreCase))
                {
                    string full = Path.GetFullPath(assetPath);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
            }

            string cache = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));
            if (Directory.Exists(cache))
            {
                string[] hits = Directory.GetFiles(cache, "dirt_tileset.png", SearchOption.AllDirectories);
                if (hits.Length > 0)
                {
                    return hits[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Bakes (or re-bakes) the selected tileset's adaptive "GEN" sheets and stores them on the
        /// asset, so it renders from its own data in-game. Surfaced by the Tileset Studio's
        /// "Generate tileset data" button; the heavy lifting lives in the generator.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult GenerateTilesetData(DimensionTilesetAsset tileset)
        {
            if (tileset == null)
            {
                return Failure("Select a tileset before generating its data.");
            }

            if (ExpandNullforge.EditorTools.Generation.DimensionTilesetGenerator.Generate(tileset, out string error))
            {
                return Success(
                    tileset,
                    "Baked tileset data for \"" + tileset.BlockName + "\" — it now renders from its own adaptive sheets.");
            }

            return Failure(error ?? "Could not generate the tileset data.");
        }

        private static void EnableAllTilesetStates(DimensionTilesetAsset tileset)
        {
            SerializedObject serialized = new SerializedObject(tileset);
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.ClearArray();
            int i = 0;
            foreach (DimensionTilesetState state in DimensionTilesetStateCatalog.All)
            {
                layers.InsertArrayElementAtIndex(i);
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = state.Key;
                element.FindPropertyRelative("enabled").boolValue = true;
                element.FindPropertyRelative("texture").objectReferenceValue = null;
                i++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static DimensionFrameworkAuthoringAssetActionResult DeleteTileset(
            DimensionTemplateAsset template,
            DimensionTilesetAsset tileset)
        {
            if (template == null || tileset == null)
            {
                return Failure("Nothing to delete.");
            }

            string label = tileset.BlockName;
            string path = AssetDatabase.GetAssetPath(tileset);

            // Deleting a block removes its WHOLE footprint, not just the .asset — the baked
            // "_Generated" folder, its managed sheet(s) in the Tilesets section, and its block items.
            // Create and delete must stay symmetrical or the project fills with orphans.
            DeleteTilesetGeneratedFolder(path, tileset.name);
            DeleteManagedTilesetTexture(template, tileset.TilesetTexture);
            DeleteManagedTilesetTexture(template, ReadSerializedTexture(tileset, "emissiveTexture"));
            DeleteTilesetBlockItems(template, tileset);

            RemoveObjectReference(template, "tilesets", tileset);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            return Success(null, "Deleted tileset '" + label + "' and its generated files.");
        }

        // The baked sheets live in "<AssetName>_Generated" beside the asset; delete it wholesale.
        private static void DeleteTilesetGeneratedFolder(string assetPath, string assetName)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string genDir = dir + "/" + assetName + "_Generated";
            if (AssetDatabase.IsValidFolder(genDir))
            {
                AssetDatabase.DeleteAsset(genDir);
            }
        }

        // A sheet is deleted with its block only when it lives in the template's Tilesets section —
        // i.e. it is managed by the framework (wizard-composed or dropped in for this block). Art
        // referenced from anywhere else may be shared and is left alone.
        private static void DeleteManagedTilesetTexture(DimensionTemplateAsset template, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string tilesetsFolder = ResolveSectionFolder(template, "Tilesets").Replace('\\', '/');
            if (path.Replace('\\', '/').StartsWith(tilesetsFolder + "/", System.StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        // Reads a texture field directly from serialization (getters may gate on other flags, e.g.
        // EmissiveTexture returns null when the glow toggle is off, which would orphan the file).
        private static Texture2D ReadSerializedTexture(Object asset, string property)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty prop = serialized.FindProperty(property);
            return prop != null ? prop.objectReferenceValue as Texture2D : null;
        }

        // The block's two item assets (visible wall block + hidden ground infra) belong to the
        // tileset; they go with it — out of the item list, off the disk, generated prefab included.
        private static void DeleteTilesetBlockItems(DimensionTemplateAsset template, DimensionTilesetAsset tileset)
        {
            string templatePath = AssetDatabase.GetAssetPath(template);
            foreach (string itemId in new[] { tileset.GroundBlockItemId, tileset.WallBlockItemId })
            {
                DimensionItemGenerator.DeleteGeneratedArtifacts(templatePath, itemId);
                DimensionItemAsset item = FindGlobalItem(template, itemId);
                if (item == null)
                {
                    continue;
                }

                RemoveObjectReference(template, "globalItems", item);
                string itemPath = AssetDatabase.GetAssetPath(item);
                if (!string.IsNullOrEmpty(itemPath))
                {
                    AssetDatabase.DeleteAsset(itemPath);
                }
            }
        }

        /// <summary>
        /// Deletes an item and its whole footprint: the reference, the .asset, and the generated
        /// prefab. Items owned by a block (its wall/ground pair) are refused — deleting the block in
        /// the Tileset Studio removes them properly.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult DeleteItem(
            DimensionTemplateAsset template,
            DimensionItemAsset item)
        {
            if (template == null || item == null)
            {
                return Failure("Nothing to delete.");
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset != null &&
                        (string.Equals(item.ItemId, tileset.GroundBlockItemId, System.StringComparison.Ordinal) ||
                         string.Equals(item.ItemId, tileset.WallBlockItemId, System.StringComparison.Ordinal)))
                    {
                        return Failure(
                            "\"" + item.DisplayName + "\" belongs to the block \"" + tileset.BlockName +
                            "\" — delete that block in the Tileset Studio to remove it cleanly.");
                    }
                }
            }

            string label = item.DisplayName;
            DimensionItemGenerator.DeleteGeneratedArtifacts(AssetDatabase.GetAssetPath(template), item.ItemId);
            RemoveObjectReference(template, "globalItems", item);
            string path = AssetDatabase.GetAssetPath(item);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            return Success(null, "Deleted item \"" + label + "\" and its generated prefab.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRecipe(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a recipe.");
            }

            int index = template.GlobalRecipes.Length + 1;
            DimensionRecipeAsset recipe =
                CreateAsset<DimensionRecipeAsset>(template, "Resources", "Recipe" + index.ToString());
            SetSerializedString(recipe, "recipeId", ResolveScopedId(template, "Recipe" + index.ToString()));
            SetSerializedString(recipe, "displayName", "Recipe " + index.ToString());
            SetSerializedBool(recipe, "enabled", true);
            AppendObjectReference(template, "globalRecipes", recipe);
            SaveAndSelect(recipe);
            return Success(recipe, "Created a recipe definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateWorkbench(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a workbench.");
            }

            int index = template.GlobalWorkbenches.Length + 1;
            DimensionWorkbenchAsset workbench =
                CreateAsset<DimensionWorkbenchAsset>(template, "Resources", "Workbench" + index.ToString());
            SetSerializedString(workbench, "workbenchId", ResolveScopedId(template, "Workbench" + index.ToString()));
            SetSerializedString(workbench, "displayName", "Workbench " + index.ToString());
            SetSerializedBool(workbench, "enabled", true);
            AppendObjectReference(template, "globalWorkbenches", workbench);
            SaveAndSelect(workbench);
            return Success(workbench, "Created a workbench definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateLootTable(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a loot table.");
            }

            int index = template.GlobalLootTables.Length + 1;
            DimensionLootTableAsset lootTable =
                CreateAsset<DimensionLootTableAsset>(template, "Resources", "LootTable" + index.ToString());
            SetSerializedString(lootTable, "lootTableId", ResolveScopedId(template, "LootTable" + index.ToString()));
            SetSerializedString(lootTable, "displayName", "Loot Table " + index.ToString());
            SetSerializedBool(lootTable, "enabled", true);
            AppendObjectReference(template, "globalLootTables", lootTable);
            SaveAndSelect(lootTable);
            return Success(lootTable, "Created a loot table definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateSpawnRule(
            DimensionTemplateAsset template,
            BiomeTemplateAsset biome)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a spawn rule.");
            }

            int index = CountSpawnRules(template, biome) + 1;
            SpawnRuleTemplateAsset rule =
                CreateAsset<SpawnRuleTemplateAsset>(template, "Spawns", "SpawnRule" + index.ToString());
            string zoneId = biome == null ? string.Empty : biome.BiomeId;
            rule.ConfigureIdentity(
                ResolveScopedId(template, "SpawnRule" + index.ToString()),
                "Spawn Rule " + index.ToString(),
                zoneId,
                ResolveScopedId(template, "Mob" + index.ToString()),
                DimensionSpawnSubjectKind.Mob,
                1,
                index,
                true);
            rule.ApplyLocalBounds(new Vector2Int(-32, -32), new Vector2Int(32, 32));
            AppendObjectReference(biome == null ? (Object)template : biome, biome == null ? "globalSpawnRules" : "spawnRules", rule);
            SaveAndSelect(rule);
            return Success(rule, biome == null ? "Created a global spawn rule." : "Created a biome spawn rule.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateSpawnable(
            DimensionTemplateAsset template,
            DimensionSpawnableKind kind)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding spawnable content.");
            }

            if (kind == DimensionSpawnableKind.Animal)
            {
                return CreateSpawnableAsset<DimensionAnimalAsset>(template, "globalAnimals", "animalId", "Animal", template.GlobalAnimals.Length + 1);
            }

            if (kind == DimensionSpawnableKind.Critter)
            {
                return CreateSpawnableAsset<DimensionCritterAsset>(template, "globalCritters", "critterId", "Critter", template.GlobalCritters.Length + 1);
            }

            if (kind == DimensionSpawnableKind.Boss)
            {
                return CreateSpawnableAsset<DimensionBossAsset>(template, "globalBosses", "bossId", "Boss", template.GlobalBosses.Length + 1);
            }

            return CreateSpawnableAsset<DimensionMobAsset>(template, "globalMobs", "mobId", "Mob", template.GlobalMobs.Length + 1);
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRuntimeManifestAsset(
            DimensionTemplateAsset template,
            DimensionTemplateManifestExportPreview preview)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before generating a runtime manifest.");
            }

            if (!preview.ManifestBuilt || !preview.ReadyForManifestExport)
            {
                return Failure("The manifest still has blockers. Validate the Export section before generating runtime output.");
            }

            DimensionRuntimeConsumerBootstrapResult runtimeResult =
                DimensionRuntimeConsumerBootstrapUtility.EnsureGeneratedRuntime(
                    template,
                    preview);
            if (!runtimeResult.Executed)
            {
                return Failure(runtimeResult.Message);
            }

            SaveAndSelect(runtimeResult.RuntimeManifestAsset);
            return Success(runtimeResult.RuntimeManifestAsset, runtimeResult.Message);
        }

        private static DimensionFrameworkAuthoringAssetActionResult CreateSpawnableAsset<T>(
            DimensionTemplateAsset template,
            string collectionProperty,
            string idProperty,
            string label,
            int index)
            where T : ScriptableObject
        {
            T asset = CreateAsset<T>(template, "Spawns", label + index.ToString());
            SetSerializedString(asset, idProperty, ResolveScopedId(template, label + index.ToString()));
            SetSerializedString(asset, "displayName", label + " " + index.ToString());
            SetSerializedString(asset, "objectId", ResolveScopedId(template, label + index.ToString() + "Object"));
            SetSerializedInt(asset, "spawnWeight", 1);
            SetSerializedBool(asset, "enabled", true);
            AppendObjectReference(template, collectionProperty, asset);
            SaveAndSelect(asset);
            return Success(asset, "Created a " + label.ToLowerInvariant() + " definition.");
        }

        /// <summary>
        /// Counts enabled instantaneous item portals (V2) that do not yet have an item asset, without
        /// creating anything (safe to call from OnGUI).
        /// </summary>
        public static int CountMissingPortalItems(DimensionTemplateAsset template)
        {
            int count = 0;
            ForEachMissingPortalItem(template, (rule, itemId, label) => count++);
            return count;
        }

        /// <summary>
        /// Ensures every enabled item portal (V2) has a real, editable item asset in the template's
        /// items, creating a default for any that is missing. The item id is locked to the rule's
        /// <c>PortalItemObjectId</c> because the runtime portal registry matches on it; everything else
        /// (icon, name, recipe) is left for the creator to customize in the item editor.
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult EnsurePortalItems(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating portal items.");
            }

            // Broken or deleted entries (missing asset files, or assets whose script binding was
            // severed) read back as null, hide the real item from the duplicate check, and every
            // generate would then create "PortalItem 1", "PortalItem 2", ... — compact them away
            // before deciding anything is missing.
            CompactGlobalItems(template);

            int created = 0;
            DimensionItemAsset lastCreated = null;
            ForEachMissingPortalItem(template, (rule, itemId, label) =>
            {
                DimensionItemAsset item = CreateAsset<DimensionItemAsset>(template, "Resources", "PortalItem");
                SetSerializedString(item, "itemId", itemId);
                SetSerializedString(item, "displayName", label + " Portal");
                SetSerializedString(item, "description",
                    "Right-click to open a temporary portal to " + label + ".");
                SetSerializedEnum(item, "archetype", (int)DimensionItemArchetype.Material);
                SetSerializedEnum(item, "kind", (int)DimensionItemKind.PortalItem);
                // objectId only satisfies the generator's "has a visual" gate so the first generate is
                // valid; a real icon comes from the creator dragging a sprite into the Icon sprite field.
                SetSerializedString(item, "objectId", itemId);
                SetSerializedInt(item, "maxStack", 1);
                SetSerializedBool(item, "enabled", true);
                AssignDefaultPortalIcons(item);
                AppendObjectReference(template, "globalItems", item);
                lastCreated = item;
                created++;
            });

            if (created == 0)
            {
                return Success(null, "Every enabled item portal already has an item.");
            }

            AssetDatabase.SaveAssets();
            return Success(
                lastCreated,
                "Created " + created + " portal item" + (created == 1 ? string.Empty : "s") + ".");
        }

        /// <summary>
        /// Ensures every enabled tileset has an item asset per toggled-on block kind, then keeps the
        /// item's display name in step with the tileset's block name. The item id is locked to the
        /// tileset's derived block id because the runtime maps tiles back to items by it. Everything
        /// else about the block — icon, description — is owned by the item and edited in the item
        /// list; only the name flows from the tileset (its single "Block Name" is the source).
        /// </summary>
        public static DimensionFrameworkAuthoringAssetActionResult EnsureTilesetBlockItems(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before creating block items.");
            }

            CompactGlobalItems(template);

            int created = 0;
            DimensionItemAsset lastCreated = null;
            ForEachMissingTilesetBlockItem(template, (tileset, itemId, isWall) =>
            {
                DimensionItemAsset item = CreateAsset<DimensionItemAsset>(
                    template,
                    "Resources",
                    (isWall ? "WallBlock" : "GroundBlock"));
                SetSerializedString(item, "itemId", itemId);
                SetSerializedString(
                    item,
                    "displayName",
                    tileset.ResolveBlockDisplayName(isWall ? PugTilemap.TileType.wall : PugTilemap.TileType.ground));
                // Block archetype pulls in the Placement components (PlaceableObjectAuthoring); the
                // tile behaviour on top of that is added by DimensionTilesetBlockAuthoring at generate.
                SetSerializedEnum(item, "archetype", (int)DimensionItemArchetype.Block);
                SetSerializedEnum(item, "kind", (int)DimensionItemKind.Block);
                // objectId only satisfies the generator's "has a visual" gate so the first generate
                // is valid; the real icon is assigned by the creator on the item.
                SetSerializedString(item, "objectId", itemId);
                SetSerializedInt(item, "maxStack", 999);
                SetSerializedBool(item, "enabled", true);
                // The ground counterpart is hidden infrastructure — vanilla's non-obtainable "Dirt
                // Ground". It must exist so placement lays ground-then-wall and both tiles drop the one
                // block, but the modder only ever sees and edits the wall, which is the "{name} Block".
                SetSerializedBool(item, "hidden", !isWall);
                AppendObjectReference(template, "globalItems", item);
                lastCreated = item;
                created++;
            });

            int synced = SyncTilesetBlockItemNames(template);
            if (created == 0 && synced == 0)
            {
                return Success(null, "Every tileset block already has an item.");
            }

            AssetDatabase.SaveAssets();
            if (created == 0)
            {
                return Success(null, "Updated " + synced + " block name" + (synced == 1 ? string.Empty : "s") + ".");
            }

            return Success(
                lastCreated,
                "Created " + created + " block item" + (created == 1 ? string.Empty : "s") + ".");
        }

        /// <summary>
        /// Keeps each generated block item's display name in step with its tileset's block name
        /// (ground = the name, wall = the name + " Wall"). Icons and descriptions are the item's own
        /// and are never touched here. Returns the number of names changed.
        /// </summary>
        public static int SyncTilesetBlockItemNames(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return 0;
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            DimensionItemAsset[] items = template.GlobalItems;
            if (tilesets == null || items == null)
            {
                return 0;
            }

            System.Collections.Generic.Dictionary<string, DimensionItemAsset> byId =
                new System.Collections.Generic.Dictionary<string, DimensionItemAsset>(
                    System.StringComparer.Ordinal);
            foreach (DimensionItemAsset item in items)
            {
                if (item != null && !string.IsNullOrEmpty(item.ItemId) && !byId.ContainsKey(item.ItemId))
                {
                    byId.Add(item.ItemId, item);
                }
            }

            int changed = 0;
            foreach (DimensionTilesetAsset tileset in tilesets)
            {
                if (tileset == null)
                {
                    continue;
                }

                DimensionItemAsset groundItem;
                if (tileset.GenerateGroundBlock &&
                    byId.TryGetValue(tileset.GroundBlockItemId, out groundItem) &&
                    SyncTilesetBlockItemName(tileset, groundItem, false))
                {
                    changed++;
                }

                DimensionItemAsset wallItem;
                if (tileset.GenerateWallBlock &&
                    byId.TryGetValue(tileset.WallBlockItemId, out wallItem) &&
                    SyncTilesetBlockItemName(tileset, wallItem, true))
                {
                    changed++;
                }
            }

            return changed;
        }

        private static bool SyncTilesetBlockItemName(
            DimensionTilesetAsset tileset,
            DimensionItemAsset item,
            bool isWall)
        {
            SerializedObject serialized = new SerializedObject(item);
            bool changed = false;

            string displayName = tileset.ResolveBlockDisplayName(
                isWall ? PugTilemap.TileType.wall : PugTilemap.TileType.ground);
            SerializedProperty nameProperty = serialized.FindProperty("displayName");
            if (nameProperty != null && nameProperty.stringValue != displayName)
            {
                nameProperty.stringValue = displayName;
                changed = true;
            }

            // The ground counterpart is always hidden infrastructure; the wall is the visible
            // "{name} Block". Enforcing it here also migrates assets created before the hidden flag.
            SerializedProperty hiddenProperty = serialized.FindProperty("hidden");
            if (hiddenProperty != null && hiddenProperty.boolValue != !isWall)
            {
                hiddenProperty.boolValue = !isWall;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return true;
        }

        /// <summary>
        /// Ensures the block items for one tileset exist, then returns the item for the requested
        /// kind (ground/wall) so the caller can select it. Null if the kind is not generated.
        /// </summary>
        public static DimensionItemAsset EnsureAndGetTilesetBlockItem(
            DimensionTemplateAsset template,
            DimensionTilesetAsset tileset,
            bool isWall)
        {
            if (template == null || tileset == null)
            {
                return null;
            }

            if (isWall ? !tileset.GenerateWallBlock : !tileset.GenerateGroundBlock)
            {
                return null;
            }

            EnsureTilesetBlockItems(template);

            string wantedId = isWall ? tileset.WallBlockItemId : tileset.GroundBlockItemId;
            DimensionItemAsset[] items = template.GlobalItems;
            if (items == null)
            {
                return null;
            }

            foreach (DimensionItemAsset item in items)
            {
                if (item != null && string.Equals(item.ItemId, wantedId, System.StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static void ForEachMissingTilesetBlockItem(
            DimensionTemplateAsset template,
            System.Action<DimensionTilesetAsset, string, bool> onMissing)
        {
            if (template == null)
            {
                return;
            }

            DimensionTilesetAsset[] tilesets = template.Tilesets;
            if (tilesets == null)
            {
                return;
            }

            System.Collections.Generic.HashSet<string> seen =
                new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            DimensionItemAsset[] items = template.GlobalItems;
            if (items != null)
            {
                foreach (DimensionItemAsset existing in items)
                {
                    if (existing != null && !string.IsNullOrEmpty(existing.ItemId))
                    {
                        seen.Add(existing.ItemId);
                    }
                }
            }

            foreach (DimensionTilesetAsset tileset in tilesets)
            {
                if (tileset == null || !tileset.Enabled || string.IsNullOrEmpty(tileset.TilesetName))
                {
                    continue;
                }

                // seen.Add is false when the id already exists (an existing item or duplicate tileset).
                if (tileset.GenerateGroundBlock && seen.Add(tileset.GroundBlockItemId))
                {
                    onMissing(tileset, tileset.GroundBlockItemId, false);
                }

                if (tileset.GenerateWallBlock && seen.Add(tileset.WallBlockItemId))
                {
                    onMissing(tileset, tileset.WallBlockItemId, true);
                }
            }
        }

        // Optional framework-shipped default icons for auto-created portal items. Drop a 16x16
        // PortalItemIcon.png and a 10x10 PortalItemIcon_inHand.png here (Sprite, PPU 16, Point filter)
        // and every new portal item starts with them; absent, the item is created iconless.
        private const string DefaultPortalIconPath =
            "Assets/ExpandNullforge/DefaultArt/PortalItemIcon.png";
        private const string DefaultPortalSmallIconPath =
            "Assets/ExpandNullforge/DefaultArt/PortalItemIcon_inHand.png";

        /// <summary>
        /// Fills any empty portal-item icon slot across the template with the framework default icons,
        /// so a creator sees them applied without dragging. Never overwrites an icon the creator has
        /// already set. Returns the number of items changed.
        /// </summary>
        public static int ApplyDefaultPortalIcons(DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return 0;
            }

            DimensionItemAsset[] items = template.GlobalItems;
            if (items == null)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item != null &&
                    item.Kind == DimensionItemKind.PortalItem &&
                    AssignDefaultPortalIcons(item))
                {
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Assigns the framework default icons to a single item, filling only slots that are still
        /// empty. Returns true if anything changed. The default sprites are loaded by path, so the
        /// import post-processor must have turned the PNGs into Sprites first.
        /// </summary>
        /// <summary>True once at least one framework default portal icon has imported as a Sprite.</summary>
        public static bool DefaultPortalIconsExist()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalIconPath) != null ||
                   AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalSmallIconPath) != null;
        }

        private static bool AssignDefaultPortalIcons(DimensionItemAsset item)
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalIconPath);
            Sprite smallIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultPortalSmallIconPath);
            if (icon == null && smallIcon == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(item);
            bool changed = false;

            if (icon != null)
            {
                SerializedProperty iconProperty = serialized.FindProperty("iconSprite");
                if (iconProperty != null && iconProperty.objectReferenceValue == null)
                {
                    iconProperty.objectReferenceValue = icon;
                    changed = true;
                }
            }

            if (smallIcon != null)
            {
                SerializedProperty smallIconProperty = serialized.FindProperty("smallIconSprite");
                if (smallIconProperty != null && smallIconProperty.objectReferenceValue == null)
                {
                    smallIconProperty.objectReferenceValue = smallIcon;
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }

            return changed;
        }

        /// <summary>
        /// Removes null and wrong-typed entries from the template's global item list (dead
        /// references left by deleted files or script-binding loss). Keeps healthy entries in
        /// order; saves only when something was actually dropped.
        /// </summary>
        private static void CompactGlobalItems(DimensionTemplateAsset template)
        {
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty items = serialized.FindProperty("globalItems");
            if (items == null || !items.isArray)
            {
                return;
            }

            bool changed = false;
            for (int i = items.arraySize - 1; i >= 0; i--)
            {
                Object reference = items.GetArrayElementAtIndex(i).objectReferenceValue;
                if (reference == null || !(reference is DimensionItemAsset))
                {
                    items.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(template);
            }
        }

        private static void ForEachMissingPortalItem(
            DimensionTemplateAsset template,
            System.Action<DimensionPortalAccessRuleAsset, string, string> onMissing)
        {
            if (template == null)
            {
                return;
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            if (rules == null)
            {
                return;
            }

            System.Collections.Generic.HashSet<string> seen =
                new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            DimensionItemAsset[] items = template.GlobalItems;
            if (items != null)
            {
                foreach (DimensionItemAsset existing in items)
                {
                    if (existing != null && !string.IsNullOrEmpty(existing.ItemId))
                    {
                        seen.Add(existing.ItemId);
                    }
                }
            }

            string label = string.IsNullOrEmpty(template.DisplayName)
                ? template.DimensionId
                : template.DisplayName;
            if (string.IsNullOrEmpty(label))
            {
                label = "Dimension";
            }

            foreach (DimensionPortalAccessRuleAsset rule in rules)
            {
                if (rule == null || !rule.IsItemPortal || !rule.Enabled)
                {
                    continue;
                }

                string itemId = rule.PortalItemObjectId;
                // seen.Add is false when the id already exists (a creator item or a duplicate rule).
                if (string.IsNullOrEmpty(itemId) || !seen.Add(itemId))
                {
                    continue;
                }

                onMissing(rule, itemId, label);
            }
        }

        private static T CreateAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem)
            where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            string folder = ResolveSectionFolder(template, sectionFolder);
            EnsureFolder(folder);
            string safeStem = SanitizeFileName(stem);
            asset.name = safeStem;
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeStem + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T DuplicateAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem,
            T source)
            where T : ScriptableObject
        {
            T asset = Object.Instantiate(source);
            string folder = ResolveSectionFolder(template, sectionFolder);
            EnsureFolder(folder);
            string safeStem = SanitizeFileName(stem);
            asset.name = safeStem;
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safeStem + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string ResolveSectionFolder(
            DimensionTemplateAsset template,
            string sectionFolder)
        {
            string root = ResolveTemplateRootFolder(template);
            string safeSection = SanitizeFileName(sectionFolder);
            return string.IsNullOrEmpty(safeSection)
                ? root
                : root + "/" + safeSection;
        }

        private static string ResolveTemplateRootFolder(DimensionTemplateAsset template)
        {
            string assetPath = template == null ? string.Empty : AssetDatabase.GetAssetPath(template);
            assetPath = DimensionApiModFolderUtility.NormalizeFolder(assetPath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                int slash = assetPath.LastIndexOf('/');
                if (slash > 0)
                {
                    return assetPath.Substring(0, slash);
                }
            }

            return DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder();
        }

        private static void AppendObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || value == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static bool RemoveObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || value == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return false;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != value)
                {
                    continue;
                }

                property.DeleteArrayElementAtIndex(i);
                if (i < property.arraySize &&
                    property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    property.DeleteArrayElementAtIndex(i);
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(owner);
                return true;
            }

            return false;
        }

        private static void SetObjectReference(Object owner, string propertyName, Object value)
        {
            if (owner == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static void SetSerializedString(Object owner, string propertyName, string value)
        {
            SetSerialized(owner, propertyName, property => property.stringValue = value ?? string.Empty);
        }

        private static void SetSerializedInt(Object owner, string propertyName, int value)
        {
            SetSerialized(owner, propertyName, property => property.intValue = value);
        }

        private static void SetSerializedBool(Object owner, string propertyName, bool value)
        {
            SetSerialized(owner, propertyName, property => property.boolValue = value);
        }

        private static void SetSerializedEnum(Object owner, string propertyName, int value)
        {
            SetSerialized(owner, propertyName, property => property.enumValueIndex = value);
        }

        private static void SetSerializedObjectReference(Object owner, string propertyName, Object value)
        {
            SetSerialized(owner, propertyName, property => property.objectReferenceValue = value);
        }

        private static void SetSerialized(Object owner, string propertyName, System.Action<SerializedProperty> setter)
        {
            if (owner == null || setter == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            setter(property);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
        }

        private static void SetRelativeString(SerializedProperty element, string propertyName, string value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        private static void SetRelativeInt(SerializedProperty element, string propertyName, int value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetRelativeBool(SerializedProperty element, string propertyName, bool value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetRelativeVector2Int(SerializedProperty element, string propertyName, Vector2Int value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.vector2IntValue = value;
            }
        }

        private static int CountScenes(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalScenes.Length : biome.ScenePool.Length;
        }

        private static int CountResourceNodes(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalResourceNodes.Length : biome.ResourceNodes.Length;
        }

        private static int CountSpawnRules(DimensionTemplateAsset template, BiomeTemplateAsset biome)
        {
            return biome == null ? template.GlobalSpawnRules.Length : biome.SpawnRules.Length;
        }

        private static string ResolvePrimaryBiomeId(DimensionTemplateAsset template)
        {
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes != null && biomes.Length > 0 && biomes[0] != null)
            {
                return biomes[0].BiomeId;
            }

            return ResolveScopedId(template, "StarterBiome");
        }

        private static string ResolveScopedId(DimensionTemplateAsset template, string suffix)
        {
            string prefix = ResolveModPrefix(template);
            return prefix + ":" + NormalizeIdToken(suffix, "Content");
        }

        private static string ResolveModPrefix(DimensionTemplateAsset template)
        {
            string dimensionId = template == null ? string.Empty : template.DimensionId;
            int colon = dimensionId.IndexOf(':');
            if (colon > 0)
            {
                return NormalizeIdToken(dimensionId.Substring(0, colon), "ModName");
            }

            string contentPackId = template == null ? string.Empty : template.ContentPackId;
            colon = contentPackId.IndexOf(':');
            if (colon > 0)
            {
                return NormalizeIdToken(contentPackId.Substring(0, colon), "ModName");
            }

            return "ModName";
        }

        private static string NormalizeIdToken(string value, string fallback)
        {
            string source = string.IsNullOrEmpty(value) ? fallback : value;
            string result = string.Empty;
            bool makeUpper = true;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                bool valid = (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (valid)
                {
                    if (makeUpper && character >= 'a' && character <= 'z')
                    {
                        character = (char)(character - 32);
                    }

                    result += character;
                    makeUpper = false;
                }
                else
                {
                    makeUpper = true;
                }
            }

            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static string SanitizeFileName(string value)
        {
            return NormalizeIdToken(value, "Asset");
        }

        private static Color ResolveMapColor(int index)
        {
            Color[] colors =
            {
                new Color(0.25f, 0.45f, 0.55f, 1f),
                new Color(0.38f, 0.52f, 0.30f, 1f),
                new Color(0.53f, 0.42f, 0.28f, 1f),
                new Color(0.44f, 0.34f, 0.58f, 1f),
                new Color(0.55f, 0.36f, 0.42f, 1f)
            };

            return colors[Mathf.Abs(index) % colors.Length];
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = DimensionApiModFolderUtility.NormalizeFolder(folder);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SaveAndSelect(Object asset)
        {
            if (asset != null)
            {
                EditorUtility.SetDirty(asset);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static DimensionFrameworkAuthoringAssetActionResult Success(Object createdObject, string message)
        {
            return new DimensionFrameworkAuthoringAssetActionResult(true, createdObject, message);
        }

        private static DimensionFrameworkAuthoringAssetActionResult Failure(string message)
        {
            return new DimensionFrameworkAuthoringAssetActionResult(false, null, message);
        }
    }
}
