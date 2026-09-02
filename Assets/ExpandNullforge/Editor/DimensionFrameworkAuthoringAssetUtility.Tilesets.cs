using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Making a tileset block, its sheets, its items, and deleting all of it again.
    /// </summary>
    internal static partial class DimensionFrameworkAuthoringAssetUtility
    {
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
            DimensionAssetFolders.Ensure(folder);
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
                SetSerializedBool(item, "stackableWasMigrated", true);
                SetSerializedBool(item, "stackable", true);
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
    }
}
