using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The editors for the world itself: layout, biome, terrain, scenes, tilesets, spawns.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawOverviewEditor()
        {
            DrawSectionIntro(
                "Dimension overview",
                "The selected Dimension Asset at a glance.");

            DimensionTemplateManifestExportPreview exportPreview =
                viewModel.SessionReport.ManifestExportPreview;

            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Biomes", CountBiomes().ToString(), "Biomes so far.");
            DrawDashboardMetric("Scenes", CountScenes().ToString(), "Structures and scene pools.");
            DrawDashboardMetric("Resources", CountResources().ToString(), "Ores, resource nodes, and loot sources.");
            DrawDashboardMetric("Spawns", CountSpawns().ToString(), "Animals, critters, mobs, bosses, and spawn rules.");
            DrawDashboardMetric("Visuals", viewModel.SessionReport.VisualAssetReferenceCount.ToString(), "Sprites, palettes, environments, and art links.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawContextualPreviewCanvas(viewModel.PreviewCanvas);

            DimensionDefinition definition = selectedTemplate.ToDimensionDefinition();
            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Dimension identity", EditorStyles.boldLabel);
            DrawNamedValue("Name", selectedTemplate.DisplayName);
            DrawNamedValue("Dimension ID", selectedTemplate.DimensionId);
            DrawNamedValue("Absolute origin", FormatInt2(definition.AbsoluteOrigin));
            DrawNamedValue("Local 0,0", "At " + FormatInt2(definition.AbsoluteOrigin));
            DrawNamedValue("Dimension type", definition.Type.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Layout bounds", EditorStyles.boldLabel);
            DrawNamedValue("Reserved", FormatBounds(definition.LocalBounds));
            DrawNamedValue("Playable", FormatBounds(exportPreview.PlayableLocalBounds));
            DrawNamedValue("Shell padding", exportPreview.CoordinateShellPaddingTiles.ToString());
            DrawNamedValue("Biome count", CountBiomes().ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Selected biome", EditorStyles.boldLabel);
            if (biome == null)
            {
                DrawNamedValue("Biome", "None");
                DrawNamedValue("Terrain", "Not configured");
                DrawNamedValue("Content", "Not configured");
            }
            else
            {
                DrawNamedValue("Name", biome.DisplayName);
                DrawNamedValue("Biome ID", biome.BiomeId);
                DrawNamedValue("Enabled", biome.Enabled ? "Yes" : "No");
                DrawNamedValue("Bounds", biome.HasFallbackLocalBounds ? FormatBounds(biome.FallbackLocalBounds) : "Layout-driven");
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Terrain mini-preview", EditorStyles.boldLabel);
            if (biome == null)
            {
                DrawNamedValue("Terrain", "No biome selected");
            }
            else
            {
                DrawNamedValue("Floor IDs", biome.FloorObjectIds.Length.ToString());
                DrawNamedValue("Wall IDs", biome.WallObjectIds.Length.ToString());
                DrawNamedValue("Ore IDs", biome.OreObjectIds.Length.ToString());
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Generation mini-preview", EditorStyles.boldLabel);
            DrawNamedValue("Passes", CountGenerationPasses().ToString());
            DrawNamedValue("Terrain IDs", CountBiomeTerrainIds().ToString());
            DrawNamedValue("Runtime-ready", exportPreview.ReadyForRuntimeGeneration ? "Yes" : "Pending");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Scenes summary", EditorStyles.boldLabel);
            DrawNamedValue("Scene assets", CountScenes().ToString());
            DrawNamedValue("Scene contents", CountSceneContents().ToString());
            DrawNamedValue("Export records", exportPreview.SceneTemplateCount + " templates, " + exportPreview.SceneCount + " scenes");
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Resources summary", EditorStyles.boldLabel);
            DrawNamedValue("Items", CountAssets(selectedTemplate.GlobalItems).ToString());
            DrawNamedValue("Recipes", CountAssets(selectedTemplate.GlobalRecipes).ToString());
            DrawNamedValue("Workbenches", CountAssets(selectedTemplate.GlobalWorkbenches).ToString());
            DrawNamedValue("Loot tables", CountAssets(selectedTemplate.GlobalLootTables).ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Spawns summary", EditorStyles.boldLabel);
            DrawNamedValue("Animals", CountAssets(selectedTemplate.GlobalAnimals).ToString());
            DrawNamedValue("Critters", CountAssets(selectedTemplate.GlobalCritters).ToString());
            DrawNamedValue("Mobs", CountAssets(selectedTemplate.GlobalMobs).ToString());
            DrawNamedValue("Bosses", CountAssets(selectedTemplate.GlobalBosses).ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Selected biome content", EditorStyles.boldLabel);
            DrawNamedValue("Scenes", biome == null ? "0" : CountBiomeScenes(biome).ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLayoutEditor()
        {
            DrawSectionIntro(
                "World layout",
                "Shape where every biome lives before terrain, scenes, resources, and spawn rules are applied.");

            if (selectedTemplate.LayoutTemplate == null)
            {
                EditorGUILayout.HelpBox(
                    "This dimension has no layout yet. A layout is where every biome lives — create one " +
                    "and the Layout Studio opens here.",
                    MessageType.Info);
                if (GUILayout.Button("Create Layout Template", GUILayout.Width(180f), GUILayout.Height(26f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateLayoutTemplate(selectedTemplate));
                    GUIUtility.ExitGUI();
                }

                return;
            }

            DrawLayoutStudio();
            GUILayout.Space(8f);

            DrawSerializedAsset(
                selectedTemplate,
                "Dimension Layout Link",
                Field("layoutTemplate", "Layout template"));

            DimensionLayoutTemplateAsset layout = selectedTemplate.LayoutTemplate;
            DrawSerializedAsset(
                layout,
                "Layout Template",
                Field("layoutId", "Layout ID"),
                Field("displayName", "Display name"),
                Field("layoutKind", "Layout mode"),
                Field("regions", "Manual regions"),
                Field("gridLocalMin", "Grid local min"),
                Field("gridCellSize", "Grid cell size"),
                Field("gridCells", "Grid cells"),
                // "Radial band size" is gone from every surface: the compiler reads it, passes it
                // on and the method it lands in never uses it, so a ring is always written as an
                // exact circle. The field itself stays stored, because pinned layout fingerprints
                // include it.
                Field("radialRings", "Radial rings"),
                Field("biomeMask", "Biome mask"),
                Field("maskBiomeMappings", "Mask biome mappings"),
                Field("maskLocalMin", "Mask local min"),
                Field("tilesPerMaskPixel", "Tiles per mask pixel"),
                Field("maskAlphaThreshold", "Mask alpha threshold"),
                Field("notes", "Notes"));

            DrawEditorCard(
                "Current local bounds",
                FormatBounds(selectedTemplate.ReservedLocalBounds));
        }

        /// <summary>
        /// Draws the Layout Studio and carries out whatever the modder asked it for.
        /// </summary>
        /// <remarks>
        /// The Studio itself only reports intent — it never creates or deletes assets. Keeping the
        /// asset writes here means every one of them goes through <c>RunAssetAction</c>, which is what
        /// produces the undo entry and the saved asset; a studio that wrote assets directly would leave
        /// edits that survive until Unity feels like reloading and then quietly do not.
        /// </remarks>
        private void DrawLayoutStudio()
        {
            DimensionLayoutTemplateAsset layout = selectedTemplate.LayoutTemplate;

            // Compiled fresh every repaint, by the same compiler the build uses. It is not free, but it
            // is the only way the canvas can be trusted: a preview that reads the authoring data
            // directly would show rings the build might reject and hide the ones it silently drops.
            DimensionCompiledGenerationPlan plan = DimensionTemplateCompiler.Compile(selectedTemplate);

            DimensionLayoutStudio.DrawResult r = layoutStudio.Draw(
                selectedTemplate,
                layout,
                plan.BiomeRegions,
                plan.PlayableLocalBounds);

            if (r.Changed)
            {
                Repaint();
            }

            DrawLayoutCompileIssues(plan);

            if (r.AddRingRequested)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.AddLayoutRing(layout, GetSelectedBiomeOrFirst()));
                GUIUtility.ExitGUI();
            }

            if (r.AddRegionRequested)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.AddLayoutRegion(layout, GetSelectedBiomeOrFirst()));
                GUIUtility.ExitGUI();
            }

            if (!string.IsNullOrEmpty(r.RemoveEntryId))
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.RemoveLayoutEntry(layout, r.RemoveEntryId));
                GUIUtility.ExitGUI();
            }

            if (r.PublishRequested)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.PublishLayoutVersion(selectedTemplate, layout));
                GUIUtility.ExitGUI();
            }
        }

        /// <summary>
        /// Shows only the compile problems that are about the layout.
        /// </summary>
        /// <remarks>
        /// Filtered rather than showing everything, because the full issue list covers spawn rules,
        /// resources and scenes too — and a wall of unrelated warnings under a map is how an author
        /// learns to stop reading warnings.
        /// </remarks>
        private void DrawLayoutCompileIssues(DimensionCompiledGenerationPlan plan)
        {
            if (plan.Issues == null)
            {
                return;
            }

            for (int i = 0; i < plan.Issues.Count; i++)
            {
                DimensionAuthoringIssue issue = plan.Issues[i];
                if (issue.Code == null || !issue.Code.StartsWith("layout-", System.StringComparison.Ordinal))
                {
                    continue;
                }

                EditorGUILayout.HelpBox(
                    issue.Message,
                    issue.Severity == DimensionAuthoringSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        private void DrawBiomeEditor()
        {
            DrawSectionIntro(
                "Biomes",
                "The biomes that terrain, generation, resources, scenes and spawns all build on.");

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Biome", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateBiome(selectedTemplate, null));
            }

            using (new EditorGUI.DisabledScope(biome == null))
            {
                if (GUILayout.Button("Duplicate Selected", GUILayout.Width(150f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateBiome(selectedTemplate, biome));
                }

                if (GUILayout.Button("Remove Reference", GUILayout.Width(145f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.RemoveBiomeReference(selectedTemplate, biome));
                    selectedBiomeIndex = 0;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            if (biome == null)
            {
                DrawEditorCard(
                    "No biome exists yet",
                    "Create at least one biome before configuring terrain, generation, resources, or spawns.");
                return;
            }

            DrawSerializedAsset(
                selectedTemplate,
                "Biome Collections",
                Field("biomes", "Biomes"),
                Field("environmentProfiles", "Environment profiles"));

            GUILayout.Space(8f);
            DrawSerializedAsset(
                biome,
                "Selected Biome",
                Field("displayName", "Display name"),
                NameField("biomeId", "Biome ID"),
                Field("mapColor", "Map color"),
                Field("priority", "Priority"),
                Field("enabled", "Enabled"),
                Field("environmentProfileId", "Environment profile ID"),
                Field("paletteAssetId", "Palette ID"),
                Field("hasFallbackLocalBounds", "Has fallback bounds"),
                Field("fallbackLocalMin", "Fallback local min"),
                Field("fallbackLocalMaxExclusive", "Fallback local max"),
                Field("showTitleOnDiscovery", "Announce on discovery"),
                Field("overrideTitleColor", "Override title colour"),
                Field("titleColor", "Title colour"),
                Field("titleIconObjectId", "Title icon object"),
                Field("ambienceSoundKey", "Ambience loop"),
                Field("ambienceVolume", "Ambience volume"),
                Field("musicRosterName", "Music playlist"),
                Field("notes", "Notes"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Biome identity", EditorStyles.boldLabel);
            DrawNamedValue("Display name", biome.DisplayName);
            DrawNamedValue("Biome ID", biome.BiomeId);
            DrawNamedValue("Enabled", biome.Enabled ? "Yes" : "No");
            DrawNamedValue("Priority", biome.Priority.ToString());
            DrawNamedValue("Fallback bounds", biome.HasFallbackLocalBounds ? FormatBounds(biome.FallbackLocalBounds) : "Uses layout placement");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Biome assets", EditorStyles.boldLabel);
            DrawNamedValue("Floor IDs", biome.FloorObjectIds.Length.ToString());
            DrawNamedValue("Wall IDs", biome.WallObjectIds.Length.ToString());
            DrawNamedValue("Ore IDs", biome.OreObjectIds.Length.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawEditorCard(
                "Biome pack editor",
                "This is the asset home for the selected biome: floor, wall, ore, liquid, decals, breakables, optional crates/vases, and biome-only generation objects. Terrain tuning, loot, and spawns stay in their own tabs.");
        }

        private void DrawTerrainEditor()
        {
            DrawSectionIntro(
                "Terrain",
                "Tune how the selected biome feels on the ground: floors, walls, liquids, clutter, ore density, roughness, and visual ambience.");

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            if (biome == null)
            {
                DrawEditorCard("No biome selected", "Create or select a biome before editing terrain.");
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Generation passes", CountBiomeGenerationPasses(biome).ToString(), "Passes that apply terrain rules.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawSerializedAsset(
                biome,
                "Selected Biome Terrain",
                Field("floorObjectIds", "Floor object IDs"),
                Field("wallObjectIds", "Wall object IDs"),
                Field("oreObjectIds", "Ore object IDs"),
                Field("generationPasses", "Generation passes"));

            DrawEditorCard(
                "Terrain controls",
                "Tune this biome with vanilla-style generation knobs: density, wall breakup, water coverage, ore frequency, decoration amount, edge blending, and biome chaos. Changes here apply only to the selected biome.");
        }

        private void DrawGenerationEditor()
        {
            DrawSectionIntro(
                "Generation preview",
                "Preview how all configured biomes and their terrain settings combine into one generated dimension.");

            DrawContextualPreviewCanvas(viewModel.PreviewCanvas);
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Biomes", CountBiomes().ToString(), "Biomes taking part in generation.");
            DrawDashboardMetric("Passes", CountGenerationPasses().ToString(), "Ordered generation passes.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Generation Assets",
                Field("globalGenerationPasses", "Global generation passes"));

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            if (biome != null)
            {
                DrawSerializedAsset(
                    biome,
                    "Selected Biome Generation",
                    Field("generationPasses", "Generation passes"));
            }

            DrawEditorCard(
                "Edge blending",
                "Preview the final combined generation result and how biome borders meet. This page is global because it needs every biome and terrain rule at once.");
        }

        private void DrawScenesEditor()
        {
            DrawSectionIntro(
                "Scenes and structures",
                "Add placed structures, random scene pools, boss arenas, puzzle rooms, ancient waypoints, and other handcrafted content.");

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Global Scene", GUILayout.Width(150f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateScene(selectedTemplate, null));
            }

            using (new EditorGUI.DisabledScope(biome == null))
            {
                if (GUILayout.Button("Add Biome Scene", GUILayout.Width(150f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateScene(selectedTemplate, biome));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            int biomeScenes = biome == null ? 0 : CountBiomeScenes(biome);
            int globalScenes = CountAssets(selectedTemplate.GlobalScenes);
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("This biome", biomeScenes.ToString(), "Scenes attached to the selected biome.");
            DrawDashboardMetric("Global", globalScenes.ToString(), "Scenes attached to the whole dimension.");
            DrawDashboardMetric("Total", CountScenes().ToString(), "All scenes currently declared.");
            DrawDashboardMetric("Contents", CountSceneContents().ToString(), "Triggers inside scenes.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Scene Assets",
                Field("globalScenes", "Scenes"));

            if (biome != null)
            {
                DrawSerializedAsset(
                    biome,
                    "Selected Biome Scene Pool",
                    Field("scenePool", "Scenes"));
            }

            DrawSerializedAsset(
                selectedTemplate,
                "Dungeons",
                Field("globalDungeons", "Dungeons"));

            DrawDungeonAssetEditors(selectedTemplate.GlobalDungeons);

            DrawSceneTemplateEditors(selectedTemplate.GlobalScenes, "Global Scene");
            if (biome != null)
            {
                DrawSceneTemplateEditors(biome.ScenePool, "Biome Scene");
            }

            if (biomeScenes == 0 && globalScenes == 0)
            {
                DrawEditorCard(
                    "No scenes yet",
                    "Add structures, arenas, waypoints, locked entrances, puzzle rooms, or random scene pools when this dimension is ready for handcrafted content.");
                return;
            }

            DrawEditorCard(
                "Scene builder",
                "Scene placement belongs here: random pools, exact coordinates, collision warnings, required clearances, and optional SceneBuilder-style import/export.");
        }

        private void DrawTilesetsEditor()
        {
            DimensionTilesetAsset[] tilesets = selectedTemplate.Tilesets ?? new DimensionTilesetAsset[0];
            int count = CountNonNull(tilesets);

            if (count == 0 && !tilesetStudio.WizardActive)
            {
                GUILayout.Space(10f);
                DrawEditorCard(
                    "No blocks yet",
                    "Add your first block — a short wizard asks what kind of block it is, which states it supports, and how it reaches the game.");
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.90f, 0.58f, 0.26f);
                if (GUILayout.Button("+ Add block", GUILayout.Width(130f), GUILayout.Height(26f)))
                {
                    tilesetStudio.BeginWizard();
                    Repaint();
                }

                GUI.backgroundColor = previous;
                return;
            }

            // Keep the selection valid, then hand the whole thing to the Studio.
            ResolveSelectedTileset(tilesets);
            DimensionTilesetStudio.DrawResult r =
                tilesetStudio.Draw(selectedTemplate, tilesets, selectedTilesetIndex, position.width - SidebarWidth);

            if (r.WizardRequest != null)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.CreateWizardBlock(selectedTemplate, r.WizardRequest));
                selectedTilesetIndex = CountNonNull(selectedTemplate.Tilesets) - 1;
                GUIUtility.ExitGUI();
            }

            if (r.DeleteRequested)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.DeleteTileset(selectedTemplate, tilesets[selectedTilesetIndex]));
                selectedTilesetIndex = 0;
                GUIUtility.ExitGUI();
            }

            if (r.GenerateRequested)
            {
                RunAssetAction(
                    DimensionFrameworkAuthoringAssetUtility.GenerateTilesetData(tilesets[selectedTilesetIndex]));
                GUIUtility.ExitGUI();
            }

            if (r.FocusItem != null)
            {
                // The Studio's "Edit ▸" deep-link: jump to Resources with the item selected.
                activeSectionId = "resources";
                Selection.activeObject = r.FocusItem;
                EditorGUIUtility.PingObject(r.FocusItem);
                Repaint();
            }

            if (r.SelectIndex >= 0)
            {
                selectedTilesetIndex = r.SelectIndex;
                Repaint();
            }

            if (!string.IsNullOrEmpty(r.Message))
            {
                lastEditorActionMessage = r.Message;
                lastEditorActionType = r.MessageType;
            }

            if (r.Changed)
            {
                RebuildWorkspace();
                Repaint();
            }
        }

        private DimensionTilesetAsset ResolveSelectedTileset(DimensionTilesetAsset[] tilesets)
        {
            if (selectedTilesetIndex < 0 || selectedTilesetIndex >= tilesets.Length ||
                tilesets[selectedTilesetIndex] == null)
            {
                for (int i = 0; i < tilesets.Length; i++)
                {
                    if (tilesets[i] != null)
                    {
                        selectedTilesetIndex = i;
                        return tilesets[i];
                    }
                }

                return null;
            }

            return tilesets[selectedTilesetIndex];
        }

        private static int CountNonNull(DimensionTilesetAsset[] tilesets)
        {
            if (tilesets == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < tilesets.Length; i++)
            {
                if (tilesets[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void DrawResourcesEditor()
        {
            DrawSectionIntro(
                "Resources",
                "Define what players can obtain from this biome: ores, blocks, liquids, drops, chest loot, recipes, and craftable progression items.");

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Item", GUILayout.Width(92f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateItem(selectedTemplate, DimensionItemKind.BaseItem));
            }

            int missingPortalItems =
                DimensionFrameworkAuthoringAssetUtility.CountMissingPortalItems(selectedTemplate);
            if (missingPortalItems > 0 &&
                GUILayout.Button(
                    "Create Portal Item" + (missingPortalItems == 1 ? string.Empty : "s"),
                    GUILayout.Width(150f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.EnsurePortalItems(selectedTemplate));
            }

            if (GUILayout.Button("Add Recipe", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateRecipe(selectedTemplate));
            }

            if (GUILayout.Button("Add Workbench", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateWorkbench(selectedTemplate));
            }

            if (GUILayout.Button("Add Container", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateContainer(selectedTemplate));
            }

            if (GUILayout.Button("Add Plant", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreatePlant(selectedTemplate));
            }

            if (GUILayout.Button("Add Object", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateWorldObject(selectedTemplate));
            }

            if (GUILayout.Button("Add Vehicle", GUILayout.Width(110f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateVehicle(selectedTemplate));
            }

            if (GUILayout.Button("Add Projectile", GUILayout.Width(126f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateProjectile(selectedTemplate));
            }

            if (GUILayout.Button("Add Explosion", GUILayout.Width(126f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateExplosion(selectedTemplate));
            }

            if (GUILayout.Button("Add Loot Table", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateLootTable(selectedTemplate));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawItemGenerationBar();

            GUILayout.Space(6f);
            int globalItems = CountAssets(selectedTemplate.GlobalItems);
            int globalRecipes = CountAssets(selectedTemplate.GlobalRecipes);
            int globalWorkbenches = CountAssets(selectedTemplate.GlobalWorkbenches);
            int globalLootTables = CountAssets(selectedTemplate.GlobalLootTables);
            int globalContainers = CountAssets(selectedTemplate.GlobalContainers);
            int globalPlants = CountAssets(selectedTemplate.GlobalPlants);
            int globalWorldObjects = CountAssets(selectedTemplate.GlobalWorldObjects);
            int globalVehicles = CountAssets(selectedTemplate.GlobalVehicles);
            int globalProjectiles = CountAssets(selectedTemplate.GlobalProjectiles);
            int globalResources = globalItems +
                globalRecipes +
                globalWorkbenches +
                globalLootTables +
                globalContainers +
                globalPlants +
                globalWorldObjects +
                globalVehicles +
                globalProjectiles;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Global", globalResources.ToString(), "Global items, recipes, workbenches, and loot tables.");
            DrawDashboardMetric("Total", CountResources().ToString(), "All obtainable content currently declared.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Items", globalItems.ToString(), "Items the mod adds.");
            DrawDashboardMetric("Recipes", globalRecipes.ToString(), "How items are crafted.");
            DrawDashboardMetric("Workbenches", globalWorkbenches.ToString(), "Crafting stations of your own.");
            DrawDashboardMetric("Loot", globalLootTables.ToString(), "Tables that decide drops.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Containers", globalContainers.ToString(), "Chests, stashes and display stands.");
            DrawDashboardMetric("Plants", globalPlants.ToString(), "Crops. Each one emits a seed, a plant and a ripe plant.");
            DrawDashboardMetric("Objects", globalWorldObjects.ToString(), "Doors, lights, beds, trophies and decoration.");
            DrawDashboardMetric("Vehicles", globalVehicles.ToString(), "Things a player can ride.");
            DrawDashboardMetric("Projectiles", globalProjectiles.ToString(), "What weapons and creatures fire.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Resource Assets",
                Field("globalItems", "Items"),
                Field("globalRecipes", "Recipes"),
                Field("globalWorkbenches", "Workbenches"),
                Field("globalLootTables", "Loot tables"),
                Field("globalContainers", "Containers"),
                Field("globalPlants", "Plants"),
                Field("globalWorldObjects", "Objects"),
                Field("globalVehicles", "Vehicles"),
                Field("globalProjectiles", "Projectiles"),
                Field("globalExplosions", "Explosions"));

            DrawItemAssetEditors(selectedTemplate.GlobalItems);
            DrawRecipeAssetEditors(selectedTemplate.GlobalRecipes);
            DrawWorkbenchAssetEditors(selectedTemplate.GlobalWorkbenches);
            DrawLootTableAssetEditors(selectedTemplate.GlobalLootTables);
            DrawContainerAssetEditors(selectedTemplate.GlobalContainers);
            DrawPlantAssetEditors(selectedTemplate.GlobalPlants);
            DrawWorldObjectAssetEditors(selectedTemplate.GlobalWorldObjects);
            DrawVehicleAssetEditors(selectedTemplate.GlobalVehicles);
            DrawProjectileAssetEditors(selectedTemplate.GlobalProjectiles);
            DrawExplosionAssetEditors(selectedTemplate.GlobalExplosions);

            GUILayout.Space(8f);
            if (globalResources == 0)
            {
                DrawEditorCard(
                    "No resources yet",
                    "Add ores, resource nodes, item sources, chest pools, drops, and recipes when you are ready to build progression for this biome.");
                return;
            }

            DrawEditorCard(
                "Loot and recipe graph",
                "Define where each item comes from: drop chances, break yields, chest pools, boss/mob drops, crafting stations, recipes, and biome-specific progression.");
        }

        private void DrawSpawnsEditor()
        {
            DrawSectionIntro(
                "Spawns",
                "Define the life in each biome: passive animals, critters, mobs, bosses, spawn weights, spawn tiles, sounds, animations, and behavior hooks.");

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Animal", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(selectedTemplate, DimensionSpawnableKind.Animal));
            }

            if (GUILayout.Button("Add Critter", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(selectedTemplate, DimensionSpawnableKind.Critter));
            }

            if (GUILayout.Button("Add Mob", GUILayout.Width(90f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(selectedTemplate, DimensionSpawnableKind.Mob));
            }

            if (GUILayout.Button("Add Boss", GUILayout.Width(90f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(selectedTemplate, DimensionSpawnableKind.Boss));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            int globalAnimals = CountAssets(selectedTemplate.GlobalAnimals);
            int globalCritters = CountAssets(selectedTemplate.GlobalCritters);
            int globalMobs = CountAssets(selectedTemplate.GlobalMobs);
            int globalBosses = CountAssets(selectedTemplate.GlobalBosses);
            int globalSpawns = globalAnimals +
                globalCritters +
                globalMobs +
                globalBosses;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Global", globalSpawns.ToString(), "Global animals, critters, mobs, and bosses.");
            DrawDashboardMetric("Total", CountSpawns().ToString(), "All spawn content currently declared.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Animals", globalAnimals.ToString(), "Passive animals.");
            DrawDashboardMetric("Critters", globalCritters.ToString(), "Ambient critters.");
            DrawDashboardMetric("Mobs", globalMobs.ToString(), "Hostile or custom mobs.");
            DrawDashboardMetric("Bosses", globalBosses.ToString(), "Boss fights.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Spawn Assets",
                Field("globalAnimals", "Animals"),
                Field("globalCritters", "Critters"),
                Field("globalMobs", "Mobs"),
                Field("globalBosses", "Bosses"));

            DrawSerializedAsset(
                selectedTemplate,
                "Changing the game itself",
                Field("globalGameSetups", "World rules"));

            DrawSerializedAsset(
                selectedTemplate,
                "Stat effects of your own",
                Field("globalConditions", "Conditions"));

            DrawAnimalAssetEditors(selectedTemplate.GlobalAnimals);
            DrawCritterAssetEditors(selectedTemplate.GlobalCritters);
            DrawGameSetupAssetEditors(selectedTemplate.GlobalGameSetups);
            DrawConditionAssetEditors(selectedTemplate.GlobalConditions);
            DrawMobAssetEditors(selectedTemplate.GlobalMobs);
            DrawBossAssetEditors(selectedTemplate.GlobalBosses);

            if (globalSpawns == 0)
            {
                DrawEditorCard(
                    "No spawns yet",
                    "Add passive animals, critters, mobs, and bosses once the biome habitat is designed.");
                return;
            }

            DrawEditorCard(
                "Habitat editor",
                "Configure creature and boss behavior by biome: spawn conditions, weights, scripts, visuals, sounds, boss arenas, summon items, and respawn rules.");
        }
    }
}
