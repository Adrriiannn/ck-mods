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

        /// <summary>
        /// Adds a radial ring, placed just outside whatever the layout already reaches.
        /// </summary>
        /// <remarks>
        /// Appended beyond the current outermost ring rather than at a fixed radius, because a new ring
        /// dropped on top of an existing one is invisible on the map — the author sees nothing happen
        /// and clicks again. Starting outside means the ring you just added is the ring you can see.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult AddLayoutRing(
            DimensionLayoutTemplateAsset layout,
            BiomeTemplateAsset biome)
        {
            if (layout == null)
            {
                return Failure("Assign or create a layout template before adding rings.");
            }

            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty rings = serialized.FindProperty("radialRings");
            if (rings == null || !rings.isArray)
            {
                return Failure("The layout template does not expose editable radial rings.");
            }

            int outermost = 0;
            DimensionLayoutRadialRingDefinition[] existing = layout.RadialRings;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    outermost = Mathf.Max(outermost, existing[i].MaxRadiusTiles);
                }
            }

            int index = rings.arraySize;
            rings.InsertArrayElementAtIndex(index);
            SerializedProperty element = rings.GetArrayElementAtIndex(index);
            string biomeId = biome == null ? string.Empty : biome.BiomeId;

            SetRelativeString(element, "ringId", "ring-" + (index + 1).ToString());
            SetRelativeString(element, "biomeId", biomeId);
            SetRelativeString(element, "zoneId", biomeId);
            SetRelativeString(
                element,
                "displayName",
                string.IsNullOrEmpty(biomeId) ? "Ring" : biome.DisplayName + " Ring");
            SetRelativeInt(element, "minRadiusTiles", outermost);
            SetRelativeInt(element, "maxRadiusTiles", outermost + 128);
            SetRelativeBool(element, "limitToAngleRange", false);
            SetRelativeFloat(element, "startAngleDegrees", 0f);
            SetRelativeFloat(element, "endAngleDegrees", 360f);
            SetRelativeInt(element, "priority", index);
            SetRelativeBool(element, "enabled", true);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(
                layout,
                "Added a ring from " + outermost + " to " + (outermost + 128) + " tiles.");
        }

        /// <summary>
        /// Deletes one ring or rectangle, addressed the way the Studio reported it.
        /// </summary>
        /// <remarks>
        /// The id carries which array it came from ("ring:2", "region:0") rather than a bare index,
        /// because both lists are on screen at once in Hybrid mode and an index alone would happily
        /// delete the wrong one.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult RemoveLayoutEntry(
            DimensionLayoutTemplateAsset layout,
            string entryId)
        {
            if (layout == null || string.IsNullOrEmpty(entryId))
            {
                return Failure("Nothing to remove.");
            }

            int separator = entryId.IndexOf(':');
            if (separator <= 0 || separator >= entryId.Length - 1)
            {
                return Failure("Could not work out which layout entry to remove.");
            }

            string kind = entryId.Substring(0, separator);
            int index;
            if (!int.TryParse(entryId.Substring(separator + 1), out index) || index < 0)
            {
                return Failure("Could not work out which layout entry to remove.");
            }

            string propertyName = kind == "ring" ? "radialRings" : "regions";
            SerializedObject serialized = new SerializedObject(layout);
            SerializedProperty array = serialized.FindProperty(propertyName);
            if (array == null || !array.isArray || index >= array.arraySize)
            {
                return Failure("That layout entry no longer exists.");
            }

            array.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(layout, kind == "ring" ? "Removed a ring." : "Removed a rectangle.");
        }

        /// <summary>
        /// Records the layout's current shape as a new published version.
        /// </summary>
        /// <remarks>
        /// <para>
        /// What is stored is the COMPILED region list, produced here by the same compiler the build
        /// runs. That is the whole point: a save pinned to this version can be regenerated from these
        /// rectangles even after the author rebuilds the layout out of entirely different rings.
        /// </para>
        /// <para>
        /// A layout that currently compiles to nothing is refused rather than published as an empty
        /// version — a world pinned to an empty layout would generate no biomes at all, which is a far
        /// worse outcome than being told to fix the layout first.
        /// </para>
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult PublishLayoutVersion(
            DimensionTemplateAsset template,
            DimensionLayoutTemplateAsset layout)
        {
            if (template == null || layout == null)
            {
                return Failure("Select a Dimension Asset with a layout before publishing.");
            }

            DimensionCompiledGenerationPlan plan = DimensionTemplateCompiler.Compile(template);
            if (plan.BiomeRegions == null || plan.BiomeRegions.Count == 0)
            {
                return Failure(
                    "This layout does not currently produce any biome regions, so there is nothing to " +
                    "publish. Fix the problems listed under the map first.");
            }

            DimensionLayoutArchivedRegion[] archived =
                new DimensionLayoutArchivedRegion[plan.BiomeRegions.Count];
            for (int i = 0; i < plan.BiomeRegions.Count; i++)
            {
                DimensionCompiledBiomeRegion region = plan.BiomeRegions[i];
                archived[i] = new DimensionLayoutArchivedRegion(
                    region.SourceTemplateId,
                    region.BiomeId,
                    region.ZoneId,
                    region.DisplayName,
                    region.LocalBounds,
                    region.Priority);
            }

            Undo.RecordObject(layout, "Publish layout version");

            string error;
            if (!layout.TryPublishVersion(
                    archived,
                    System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    string.Empty,
                    out error))
            {
                return Failure(error);
            }

            EditorUtility.SetDirty(layout);
            SaveAndSelect(layout);
            return Success(
                layout,
                "Published layout v" + layout.LayoutVersion + " with " + archived.Length +
                " regions. Worlds generated from now on remember this version.");
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

        /// <summary>
        /// Makes sure a dimension has a rule for one kind of portal, and returns the rule that
        /// owns it either way.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The repair button behind the Portal Studio's Access card, and deliberately NOT an "add
        /// a rule" list operation. A dimension has exactly one effective rule per kind, because the
        /// generator takes the first enabled match; a second rule of the same kind produces a
        /// service record with no portal in the world and nothing else. So this heals a gap and
        /// refuses to widen one: if a rule of that kind already exists it is handed back untouched,
        /// disabled or not.
        /// </para>
        /// <para>
        /// The defaults come from the same starter factory that seeds a brand-new dimension, so a
        /// repaired portal behaves exactly like a fresh one.
        /// </para>
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult EnsurePortalAccessRule(
            DimensionTemplateAsset template,
            DimensionPortalAccessKind accessKind)
        {
            if (template == null)
            {
                return Failure("Open a dimension before preparing its portal.");
            }

            DimensionPortalAccessRuleAsset[] rules = template.PortalAccessRules;
            for (int i = 0; rules != null && i < rules.Length; i++)
            {
                if (rules[i] != null && rules[i].AccessKind == accessKind)
                {
                    return Success(rules[i], "This portal already has its rule.");
                }
            }

            DimensionPortalAccessRuleAsset created = DimensionTemplateStarterFactory
                .CreatePortalAccessRule(accessKind, template.DimensionId, template.DisplayName);
            if (created == null)
            {
                return Failure("This portal's rule could not be prepared.");
            }

            DimensionPortalAccessRuleAsset saved = SaveNewAsset(
                template,
                "Portals",
                string.IsNullOrEmpty(created.name) ? "PortalAccessRule" : created.name,
                created);
            AppendObjectReference(template, "portalAccessRules", saved);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
            return Success(saved, "Prepared this portal's rule.");
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
            // Ground and Walls start EMPTY. They used to be pre-filled with ids of the shape
            // "<mod>:GroundBiome1Block", which no block a modder can make ever produces — a
            // generated block's id is "<mod>:<block name>.ground.block" — so a new biome opened
            // with two entries that could never resolve, and the world built dirt while the page
            // showed a full list. An empty list says "you have not picked yet", which is true.
            biome.ApplySemanticTerrainPreset(
                new string[0],
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

        /// <summary>
        /// Creates a chest, stash or display stand.
        /// </summary>
        /// <remarks>
        /// These four creators exist because the assets and the generators both did, and there was
        /// no way to reach either from the window — a creator could only make one by right-clicking
        /// in the Project view and hand-dragging it into the template, which is exactly the
        /// technical detour this framework is for avoiding.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateContainer(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a container.");
            }

            int index = template.GlobalContainers.Length + 1;
            DimensionContainerAsset container =
                CreateAsset<DimensionContainerAsset>(template, "Resources", "Container" + index.ToString());
            SetSerializedString(container, "containerId", ResolveScopedId(template, "Container" + index.ToString()));
            SetSerializedString(container, "displayName", "Container " + index.ToString());
            SetSerializedBool(container, "enabled", true);
            AppendObjectReference(template, "globalContainers", container);
            SaveAndSelect(container);
            return Success(container, "Created a container definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateDungeon(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a dungeon.");
            }

            int index = template.GlobalDungeons.Length + 1;
            DimensionDungeonAsset dungeon =
                CreateAsset<DimensionDungeonAsset>(template, "Scenes", "Dungeon" + index.ToString());
            SetSerializedString(dungeon, "dungeonId", ResolveScopedId(template, "Dungeon" + index.ToString()));
            SetSerializedString(dungeon, "displayName", "Dungeon " + index.ToString());
            SetSerializedBool(dungeon, "enabled", true);
            AppendObjectReference(template, "globalDungeons", dungeon);
            SaveAndSelect(dungeon);
            return Success(dungeon, "Created a dungeon.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateGenerationPass(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a generation pass.");
            }

            int index = template.GlobalGenerationPasses.Length + 1;
            GenerationPassTemplateAsset pass =
                CreateAsset<GenerationPassTemplateAsset>(
                    template, "Generation", "GenerationPass" + index.ToString());
            SetSerializedString(pass, "passId", ResolveScopedId(template, "Pass" + index.ToString()));
            SetSerializedString(pass, "displayName", "Pass " + index.ToString());
            SetSerializedBool(pass, "enabled", true);
            AppendObjectReference(template, "globalGenerationPasses", pass);
            SaveAndSelect(pass);
            return Success(pass, "Created a generation pass.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateNamedArea(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a named area.");
            }

            int index = template.NamedAreas.Length + 1;
            DimensionNamedAreaAsset area =
                CreateAsset<DimensionNamedAreaAsset>(template, "Biomes", "NamedArea" + index.ToString());
            SetSerializedString(area, "areaId", ResolveScopedId(template, "NamedArea" + index.ToString()));
            SetSerializedString(area, "displayName", "Named Area " + index.ToString());
            SetSerializedBool(area, "enabled", true);
            AppendObjectReference(template, "namedAreas", area);
            SaveAndSelect(area);
            return Success(area, "Created a named area.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateRoomFilling(
            DimensionTemplateAsset template,
            DimensionDungeonAsset dungeon)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a room filling.");
            }

            // With a dungeon in hand the filling attaches itself. Without one — which is what the
            // page's create button can offer, since it hands over the dimension rather than the
            // selection — attach it anyway when there is only one dungeon it could belong to. A
            // filling that belongs to nothing is a file the creator has to find and drag before it
            // has any effect, and nothing on screen said so.
            if (dungeon == null)
            {
                DimensionDungeonAsset[] dungeons = template.GlobalDungeons;
                int found = 0;
                for (int i = 0; i < dungeons.Length; i++)
                {
                    if (dungeons[i] != null)
                    {
                        found++;
                        dungeon = dungeons[i];
                    }
                }

                if (found != 1)
                {
                    dungeon = null;
                }
            }

            string stem = dungeon == null ? "RoomFilling" : NormalizeIdToken(dungeon.DungeonId, "Asset") + "Filling";
            int index = (dungeon == null ? 0 : dungeon.RoomFillings.Length) + 1;
            DimensionRoomFillingAsset filling =
                CreateAsset<DimensionRoomFillingAsset>(template, "Scenes", stem + index.ToString());
            SetSerializedString(filling, "fillingId",
                ResolveScopedId(
                    template,
                    (dungeon == null ? "filling" : dungeon.DungeonId + "-filling") + index.ToString()));
            SetSerializedString(filling, "displayName", "Room Filling " + index.ToString());
            SetSerializedBool(filling, "enabled", true);
            if (dungeon != null)
            {
                AppendObjectReference(dungeon, "roomFillings", filling);
            }

            SaveAndSelect(filling);
            return Success(
                filling,
                dungeon == null
                    ? "Created a room filling. Open a dungeon and add it under Room Fillings, then " +
                      "open it there to say what it places."
                    : "Created a room filling on '" + dungeon.DisplayName +
                      "'. Open it under that dungeon's Room Fillings to say what it places.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreatePlant(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a plant.");
            }

            int index = template.GlobalPlants.Length + 1;
            DimensionPlantAsset plant =
                CreateAsset<DimensionPlantAsset>(template, "Resources", "Plant" + index.ToString());
            SetSerializedString(plant, "plantId", ResolveScopedId(template, "Plant" + index.ToString()));
            SetSerializedString(plant, "displayName", "Plant " + index.ToString());
            SetSerializedBool(plant, "enabled", true);
            AppendObjectReference(template, "globalPlants", plant);
            SaveAndSelect(plant);
            return Success(plant, "Created a plant. Generating emits its seed, its plant and its ripe plant.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateWorldObject(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an object.");
            }

            int index = template.GlobalWorldObjects.Length + 1;
            DimensionWorldObjectAsset worldObject =
                CreateAsset<DimensionWorldObjectAsset>(template, "Resources", "Object" + index.ToString());
            SetSerializedString(worldObject, "objectIdentifier", ResolveScopedId(template, "Object" + index.ToString()));
            SetSerializedString(worldObject, "displayName", "Object " + index.ToString());
            SetSerializedBool(worldObject, "enabled", true);
            AppendObjectReference(template, "globalWorldObjects", worldObject);
            SaveAndSelect(worldObject);
            return Success(worldObject, "Created a placed object definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateVehicle(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a vehicle.");
            }

            int index = template.GlobalVehicles.Length + 1;
            DimensionVehicleAsset vehicle =
                CreateAsset<DimensionVehicleAsset>(template, "Resources", "Vehicle" + index.ToString());
            SetSerializedString(vehicle, "vehicleId", ResolveScopedId(template, "Vehicle" + index.ToString()));
            SetSerializedString(vehicle, "displayName", "Vehicle " + index.ToString());
            SetSerializedBool(vehicle, "enabled", true);
            AppendObjectReference(template, "globalVehicles", vehicle);
            SaveAndSelect(vehicle);
            return Success(vehicle, "Created a vehicle definition.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateProjectile(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a projectile.");
            }

            int index = template.GlobalProjectiles.Length + 1;
            DimensionProjectileAsset projectile =
                CreateAsset<DimensionProjectileAsset>(template, "Resources", "Projectile" + index.ToString());
            SetSerializedString(projectile, "projectileId", ResolveScopedId(template, "Projectile" + index.ToString()));
            SetSerializedString(projectile, "displayName", "Projectile " + index.ToString());
            SetSerializedBool(projectile, "enabled", true);
            AppendObjectReference(template, "globalProjectiles", projectile);
            SaveAndSelect(projectile);
            return Success(projectile, "Created a projectile. Name it from a weapon or a creature to fire it.");
        }

        public static DimensionFrameworkAuthoringAssetActionResult CreateExplosion(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an explosion.");
            }

            int index = template.GlobalExplosions.Length + 1;
            DimensionExplosionAsset explosion =
                CreateAsset<DimensionExplosionAsset>(template, "Resources", "Explosion" + index.ToString());
            SetSerializedString(explosion, "explosionId", ResolveScopedId(template, "Explosion" + index.ToString()));
            SetSerializedString(explosion, "displayName", "Explosion " + index.ToString());
            SetSerializedBool(explosion, "enabled", true);
            AppendObjectReference(template, "globalExplosions", explosion);
            SaveAndSelect(explosion);
            return Success(explosion, "Created an explosion. Name it from a bomb to make that bomb use it.");
        }

        /// <summary>
        /// Creates a stat effect (a condition) and registers it on the dimension.
        /// </summary>
        /// <remarks>
        /// The runtime for these shipped long before this button existed: a registered condition
        /// claims its number by NAME ordering across the whole set, so the only thing that
        /// matters here is that the name is scoped and stable from birth. Effects was the one
        /// tab a creator could browse but never add to — every other kind had a creator, and
        /// the asset-menu route it relied on is not a door this framework counts.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateCondition(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding an effect.");
            }

            int index = template.GlobalConditions.Length + 1;
            DimensionConditionAsset condition =
                CreateAsset<DimensionConditionAsset>(template, "Resources", "Effect" + index.ToString());
            SetSerializedString(condition, "conditionName", ResolveScopedId(template, "Effect" + index.ToString()));
            SetSerializedString(condition, "displayName", "Effect " + index.ToString());
            SetSerializedBool(condition, "enabled", true);
            AppendObjectReference(template, "globalConditions", condition);
            SaveAndSelect(condition);
            return Success(condition, "Created an effect. Name it from an item or a food to hand it out.");
        }

        /// <summary>
        /// Adds a rule set — the four things a mod changes about the game rather than adds to.
        /// </summary>
        /// <remarks>
        /// The World Rules stage had no way of making one at all: a creator could see the list and
        /// never add to it, which is how the whole stage sat unreachable while everything under it
        /// was written and tested. One rule set is usually enough for a mod, but the list stays a
        /// list because two mods' rule sets merge and a creator may want theirs split by subject.
        /// </remarks>
        public static DimensionFrameworkAuthoringAssetActionResult CreateWorldRules(
            DimensionTemplateAsset template)
        {
            if (template == null)
            {
                return Failure("Select a Dimension Asset before adding a rule set.");
            }

            int index = template.GlobalGameSetups.Length + 1;
            DimensionGameSetupAsset rules =
                CreateAsset<DimensionGameSetupAsset>(template, "Resources", "WorldRules" + index.ToString());
            SetSerializedString(rules, "setupIdentifier", ResolveScopedId(template, "Rules" + index.ToString()));
            SetSerializedString(rules, "displayName", "World rules " + index.ToString());
            SetSerializedBool(rules, "enabled", true);
            AppendObjectReference(template, "globalGameSetups", rules);
            SaveAndSelect(rules);
            return Success(
                rules,
                "Created a rule set. Switch on the block you want — upgrading, fishing, talents " +
                "or the player — and nothing else changes.");
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
                // The migration flag goes first, or the item's own OnValidate would still be
                // deciding stacking from the old number and would overwrite the line below.
                SetSerializedBool(item, "stackableWasMigrated", true);
                SetSerializedBool(item, "stackable", false);
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
            return SaveNewAsset(template, sectionFolder, stem, ScriptableObject.CreateInstance<T>());
        }

        /// <summary>
        /// Puts an object that has already been built onto disk beside its dimension.
        /// </summary>
        /// <remarks>
        /// Split out of <see cref="CreateAsset{T}"/> for the case where the defaults live somewhere
        /// else — a portal access rule is born fully configured by the starter factory, so the
        /// alternative would be a second copy of those defaults here.
        /// </remarks>
        private static T SaveNewAsset<T>(
            DimensionTemplateAsset template,
            string sectionFolder,
            string stem,
            T asset)
            where T : ScriptableObject
        {
            string folder = ResolveSectionFolder(template, sectionFolder);
            DimensionAssetFolders.Ensure(folder);
            string safeStem = NormalizeIdToken(stem, "Asset");
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
            DimensionAssetFolders.Ensure(folder);
            string safeStem = NormalizeIdToken(stem, "Asset");
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
            string safeSection = NormalizeIdToken(sectionFolder, "Asset");
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

        private static void SetRelativeFloat(SerializedProperty element, string propertyName, float value)
        {
            SerializedProperty property = element == null ? null : element.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.floatValue = value;
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
