using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The header, the template picker, and the tool guide with its checklists.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private void DrawHeader()
        {
            // Feedback only — the old title/description block was removed for a cleaner top.
            if (!string.IsNullOrEmpty(displayedEditorActionMessage))
            {
                EditorGUILayout.HelpBox(displayedEditorActionMessage, displayedEditorActionType);
            }
        }

        private void DrawTemplatePicker()
        {
            // No background box — just the label and field.
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Dimension Asset", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            DimensionTemplateAsset requestedTemplate =
                (DimensionTemplateAsset)EditorGUILayout.ObjectField(
                    selectedTemplate,
                    typeof(DimensionTemplateAsset),
                    false,
                    GUILayout.Width(SidebarWidth - 8f));
            bool templateChanged = EditorGUI.EndChangeCheck();
            EditorGUILayout.EndVertical();

            if (templateChanged && requestedTemplate != selectedTemplate)
            {
                RequestTemplateChange(requestedTemplate);
            }
        }

        private void DrawMissingTemplateState()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(310f), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("First step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Create a Dimension Asset before using the dashboard. Identity, starter biome, layout, resources, scenes, access rules and export settings all live on it.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Space(8f);
            if (GUILayout.Button("Create Dimension Asset", GUILayout.Height(34f)))
            {
                DimensionTemplateCreationWizardWindow.Open();
            }

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Use the wizard first. When it finishes, drop the created Dimension Asset into the field above, or select it in the Project window.",
                MessageType.Info);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.Space(8f);
            DrawToolGuide(true);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummaryStrip()
        {
            DimensionTemplateCustomizerSessionReport session = viewModel.SessionReport;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            DrawMetric("Dimension", session.DisplayName, 1.7f);
            DrawMetric("ID", session.DimensionId, 1.7f);
            DrawMetric("Biomes", session.BiomeCount.ToString(), 0.7f);
            DrawMetric("Visual refs", session.VisualAssetReferenceCount.ToString(), 0.8f);
            DrawMetric("Errors", session.ErrorCount.ToString(), 0.7f);
            DrawMetric("Warnings", session.WarningCount.ToString(), 0.8f);
            EditorGUILayout.EndHorizontal();

            MessageType messageType = session.ErrorCount > 0
                ? MessageType.Error
                : session.WarningCount > 0
                    ? MessageType.Warning
                    : MessageType.Info;
            EditorGUILayout.HelpBox(session.Message, messageType);
            DrawPrimaryActionCard(session);
            EditorGUILayout.EndVertical();
        }

        private void DrawMetric(string label, string value, float widthWeight)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(90f * widthWeight));
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawPrimaryActionCard(DimensionTemplateCustomizerSessionReport session)
        {
            if (session == null || string.IsNullOrEmpty(session.PrimaryActionId))
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Recommended next action", EditorStyles.miniBoldLabel, GUILayout.Width(160f));
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(session.PrimaryActionTitle)
                    ? session.PrimaryActionId
                    : session.PrimaryActionTitle,
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            string sectionId = ResolvePrimaryActionSectionId(session);
            bool canOpenSection = HasNavigationSection(sectionId);
            using (new EditorGUI.DisabledScope(!canOpenSection))
            {
                string buttonText = canOpenSection
                    ? "Open " + SectionDisplayName(sectionId)
                    : "No section available";
                if (GUILayout.Button(buttonText, GUILayout.Width(170f)))
                {
                    RequestSectionChange(sectionId);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(session.PrimaryActionMessage))
            {
                EditorGUILayout.LabelField(session.PrimaryActionMessage, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawToolGuide(bool expandHeight)
        {
            DimensionFrameworkToolGuide guide = workspace == null
                ? DimensionFrameworkToolGuideUtility.Build(null)
                : workspace.ToolGuide;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(expandHeight));
            if (guide == null)
            {
                EditorGUILayout.HelpBox(
                    "No creation guide is available for this workspace.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(
                "Start at the top and move downward. The first row creates the Dimension Asset; the rest become editable once that asset exists.",
                guide.State == DimensionAuthoringReadinessState.Blocked
                    ? MessageType.Warning
                    : MessageType.Info);

            DrawToolGuideLegend();

            IReadOnlyList<DimensionFrameworkToolModule> modules =
                BuildVisibleToolModules(guide.Modules);
            EnsureSelectedGuideModule(modules);

            GUILayoutOption[] guideOptions = expandHeight
                ? new GUILayoutOption[] { GUILayout.ExpandHeight(true), GUILayout.MinHeight(500f) }
                : new GUILayoutOption[] { GUILayout.ExpandHeight(true), GUILayout.MinHeight(430f) };

            EditorGUILayout.BeginHorizontal(guideOptions);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(390f), GUILayout.ExpandHeight(true));
            toolGuideScroll = EditorGUILayout.BeginScrollView(toolGuideScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < modules.Count; i++)
            {
                DrawToolModuleRow(modules[i]);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            GUILayout.Space(6f);
            DrawSelectedToolModuleDetail(FindGuideModule(modules, selectedGuideModuleId));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static IReadOnlyList<DimensionFrameworkToolModule> BuildVisibleToolModules(
            IReadOnlyList<DimensionFrameworkToolModule> modules)
        {
            List<DimensionFrameworkToolModule> visibleModules =
                new List<DimensionFrameworkToolModule>();
            if (modules == null)
            {
                return visibleModules;
            }

            for (int i = 0; i < modules.Count; i++)
            {
                DimensionFrameworkToolModule module = modules[i];
                if (module == null || IsInternalGuideModule(module.ModuleId))
                {
                    continue;
                }

                visibleModules.Add(module);
            }

            return visibleModules;
        }

        private static bool IsInternalGuideModule(string moduleId)
        {
            return moduleId == "framework-extraction" ||
                moduleId == "diagnostics-and-test-fixtures";
        }

        private void DrawToolGuideLegend()
        {
            EditorGUILayout.BeginHorizontal();
            DrawToolGuideLegendItem("Complete", DimensionAuthoringReadinessState.Ready);
            DrawToolGuideLegendItem("Needs details", DimensionAuthoringReadinessState.Partial);
            DrawToolGuideLegendItem("Not started", DimensionAuthoringReadinessState.Missing);
            DrawToolGuideLegendItem("Blocked", DimensionAuthoringReadinessState.Blocked);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolGuideLegendItem(
            string label,
            DimensionAuthoringReadinessState state)
        {
            DrawToolGuideStateSwatch(state, 14f);
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(86f));
        }

        private static void DrawToolGuideStateSwatch(
            DimensionAuthoringReadinessState state,
            float width)
        {
            Rect rect = GUILayoutUtility.GetRect(
                width,
                14f,
                GUILayout.Width(width),
                GUILayout.Height(14f));
            Rect swatch = new Rect(rect.x + 2f, rect.y + 4f, 8f, 8f);
            EditorGUI.DrawRect(swatch, StateColor(state));
        }

        private void EnsureSelectedGuideModule(IReadOnlyList<DimensionFrameworkToolModule> modules)
        {
            if (modules == null || modules.Count == 0)
            {
                selectedGuideModuleId = string.Empty;
                return;
            }

            if (FindGuideModule(modules, selectedGuideModuleId) != null)
            {
                return;
            }

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null &&
                    modules[i].State != DimensionAuthoringReadinessState.Ready)
                {
                    selectedGuideModuleId = modules[i].ModuleId;
                    return;
                }
            }

            selectedGuideModuleId = modules[0] == null ? string.Empty : modules[0].ModuleId;
        }

        private DimensionFrameworkToolModule FindGuideModule(
            IReadOnlyList<DimensionFrameworkToolModule> modules,
            string moduleId)
        {
            if (modules == null || string.IsNullOrEmpty(moduleId))
            {
                return null;
            }

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] != null && modules[i].ModuleId == moduleId)
                {
                    return modules[i];
                }
            }

            return null;
        }

        private void DrawToolModuleRow(DimensionFrameworkToolModule module)
        {
            if (module == null)
            {
                return;
            }

            bool selected = module.ModuleId == selectedGuideModuleId;
            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(0.82f, 0.92f, 1f, 1f) : new Color(0.9f, 0.9f, 0.9f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = oldColor;

            EditorGUILayout.BeginHorizontal();
            DrawToolGuideStateSwatch(module.State, 16f);

            if (GUILayout.Button(
                module.Title,
                selected ? EditorStyles.toolbarButton : EditorStyles.miniButtonLeft,
                GUILayout.Height(24f)))
            {
                selectedGuideModuleId = module.ModuleId;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedToolModuleDetail(DimensionFrameworkToolModule module)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
            if (module == null)
            {
                EditorGUILayout.LabelField("Select a step", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("No creation stage is selected.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawToolModuleLine("What this step is", module.Summary);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Do this next", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(module.NextAction, EditorStyles.wordWrappedMiniLabel);
            DrawToolModuleOpenControls(module);

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Completion checks", EditorStyles.miniBoldLabel);
            DrawChecklist(ResolveToolModuleChecklist(module.ModuleId));

            GUILayout.FlexibleSpace();
            DrawDimensionAssetQuickActions();
            EditorGUILayout.EndVertical();
        }

        private void DrawChecklist(string[] items)
        {
            if (items == null || items.Length == 0)
            {
                EditorGUILayout.LabelField("- No checklist registered yet.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                EditorGUILayout.LabelField("- " + items[i], EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawToolModuleOpenControls(DimensionFrameworkToolModule module)
        {
            string sectionId = ResolveToolModuleSectionId(module);
            bool opensWizard = module.ActionId == "open-new-dimension-wizard";
            if (string.IsNullOrEmpty(sectionId) && !opensWizard)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            bool drewAction = false;

            if (opensWizard)
            {
                drewAction = true;
                if (GUILayout.Button("Create Dimension Asset", GUILayout.Width(210f), GUILayout.Height(24f)))
                {
                    DimensionTemplateCreationWizardWindow.Open();
                }
            }

            if (!string.IsNullOrEmpty(sectionId))
            {
                bool canOpenSection = workspace != null &&
                    viewModel != null &&
                    HasNavigationSection(sectionId);
                string buttonText = canOpenSection
                    ? SectionDisplayName(sectionId) + " section"
                    : "Create/select a Dimension Asset first";
                using (new EditorGUI.DisabledScope(!canOpenSection))
                {
                    drewAction = true;
                    if (GUILayout.Button(buttonText, GUILayout.Width(230f), GUILayout.Height(24f)))
                    {
                        RequestSectionChange(sectionId);
                    }
                }
            }

            if (!drewAction)
            {
                EditorGUILayout.LabelField(
                    "This step runs from the dashboard when the previous steps are ready.",
                    EditorStyles.wordWrappedMiniLabel);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDimensionAssetQuickActions()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Show Dimension Asset in Project", GUILayout.Width(230f), GUILayout.Height(22f)))
            {
                Selection.activeObject = selectedTemplate;
                EditorGUIUtility.PingObject(selectedTemplate);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string[] ResolveToolModuleChecklist(string moduleId)
        {
            if (moduleId == "start-template")
            {
                return new string[]
                {
                    "Press Create Dimension Asset to open the wizard.",
                    "Choose the starter layout preset and generated asset folder.",
                    "Create the first biome/resource graph so the dimension is not an empty shell."
                };
            }

            if (moduleId == "dimension-domain")
            {
                return new string[]
                {
                    "Dimension ID, display name, owner mod, absolute reservation, and local 0,0.",
                    "Playable bounds and automatic coordinate-shell reservation.",
                    "Overlap warnings against vanilla and other registered dimensions."
                };
            }

            if (moduleId == "access-and-travel")
            {
                return new string[]
                {
                    "Placed portal, usable inventory item, generated portal, or mixed access.",
                    "Craftable/generated/consumed/permanent flags and recipe source.",
                    "Entry anchor, return anchor, failure text, cooldown, and multiplayer authority."
                };
            }

            if (moduleId == "layout-and-bounds")
            {
                return new string[]
                {
                    "Radial, grid, mask-import, exact-region, and layered-dungeon templates.",
                    "Biome ownership preview, playable-bounds preview, and shell growth.",
                    "Warnings for unreachable or overlapping regions."
                };
            }

            if (moduleId == "biome-environment")
            {
                return new string[]
                {
                    "Floors, walls, liquids, destructibility, density tables, decals, and ore rules.",
                    "Fog, lighting, ambience, music, weather-like overlays, and palette references.",
                    "Vanilla asset references or custom UGC assets with visual preflight."
                };
            }

            if (moduleId == "scenes-and-structures")
            {
                return new string[]
                {
                    "Random scene pools, exact coordinate placements, waypoints, locked entrances, and chest pools.",
                    "Dungeon layer rooms, return portals, boss arenas, and scene-specific loot.",
                    "Conflict checks for biome mismatch, walls, overlap, and reserved locations."
                };
            }

            if (moduleId == "mobs-bosses-npcs")
            {
                return new string[]
                {
                    "Enemy, passive animal, boss, and NPC spawn tables per biome/region.",
                    "Boss triggers, summoning items, arenas, phases, skills, sounds, and drops.",
                    "Spawn-budget warnings and multiplayer-safe encounter state."
                };
            }

            if (moduleId == "resources-items-loot")
            {
                return new string[]
                {
                    "Ores, bars, blocks, fish, valuables, materials, equipment, and scene exclusives.",
                    "Drop sources from mobs, bosses, chests, fishing, mining, and generated props.",
                    "Vanilla item references by key where copying an asset would be fragile."
                };
            }

            if (moduleId == "crafting-workbenches")
            {
                return new string[]
                {
                    "Recipe ingredients and quantities from vanilla or custom item catalogs.",
                    "Vanilla or custom workbench placement, page/category icons, and unlock paths.",
                    "Craftable/generated/not-craftable decisions that define progression."
                };
            }

            if (moduleId == "visual-asset-readiness")
            {
                return new string[]
                {
                    "SpriteObject material compatibility, UGC lit swaps, shadows, lights, inventory icons, and map icons.",
                    "Vanilla-reference reuse where possible and custom asset warnings where needed.",
                    "Preview checks before you enter the game."
                };
            }

            if (moduleId == "generation-validation")
            {
                return new string[]
                {
                    "Generation passes, provider budgets, table weights, exact placements, and deterministic seeds.",
                    "Collision/conflict checks across scenes, bosses, regions, and unbreakable borders.",
                    "Dry-run report before writing or applying runtime manifests."
                };
            }

            if (moduleId == "manifest-export")
            {
                return new string[]
                {
                    "Final manifest preview, dependencies, ownership, schema version, and migration notes.",
                    "Explicit confirmation before asset mutation or runtime export.",
                    "Packaging contract consumed by the dimension runtime."
                };
            }

            if (moduleId == "framework-extraction")
            {
                return new string[]
                {
                    "No hardcoded Nullforge IDs in the reusable framework path.",
                    "Fixture assets clearly isolated from framework assets.",
                    "Rename/package/dependency blockers visible before extraction."
                };
            }

            if (moduleId == "diagnostics-and-test-fixtures")
            {
                return new string[]
                {
                    "Portal proof loop, coordinate preview, fixture generation, and debug logs.",
                    "All diagnostics opt-in and removable from a consuming dimension mod.",
                    "No test content silently required by the framework."
                };
            }

            return new string[0];
        }

        private void DrawToolModuleLine(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(value, EditorStyles.wordWrappedMiniLabel);
        }

        private bool HasNavigationSection(string sectionId)
        {
            if (viewModel == null ||
                viewModel.Navigation == null ||
                viewModel.Navigation.Sections == null ||
                string.IsNullOrEmpty(sectionId))
            {
                return false;
            }

            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                viewModel.Navigation.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].SectionId == sectionId)
                {
                    return true;
                }
            }

            return false;
        }

        private string SectionDisplayName(string sectionId)
        {
            if (viewModel != null &&
                viewModel.Navigation != null &&
                viewModel.Navigation.Sections != null)
            {
                IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                    viewModel.Navigation.Sections;
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i].SectionId == sectionId)
                    {
                        return sections[i].DisplayName;
                    }
                }
            }

            if (sectionId == "dimension")
            {
                return "Dimension";
            }

            if (sectionId == "tilesets")
            {
                return "Tileset Studio";
            }

            if (sectionId == "layout")
            {
                return "Layout";
            }

            if (sectionId == "biomes")
            {
                return "Biomes";
            }

            if (sectionId == "terrain")
            {
                return "Terrain";
            }

            if (sectionId == "generation")
            {
                return "Generation";
            }

            if (sectionId == "scenes")
            {
                return "Scenes";
            }

            if (sectionId == "resources")
            {
                return "Resources";
            }

            if (sectionId == "spawns")
            {
                return "Spawns";
            }

            if (sectionId == "export")
            {
                return "Export";
            }

            if (sectionId == "diagnostics")
            {
                return "Diagnostics";
            }

            return "Overview";
        }

        private static string ResolveToolModuleSectionId(DimensionFrameworkToolModule module)
        {
            if (module == null)
            {
                return string.Empty;
            }

            string actionId = module.ActionId;
            if (actionId == "edit-identity-coordinates" ||
                actionId == "open-portal-contract" ||
                actionId == "open-travel-contract")
            {
                return "dimension";
            }

            if (actionId == "open-layout-designer")
            {
                return "layout";
            }

            if (actionId == "open-biome-customizer" ||
                actionId == "open-biome-environment")
            {
                return "biomes";
            }

            if (actionId == "open-visual-preflight")
            {
                return "terrain";
            }

            if (actionId == "open-generation-planner")
            {
                return "generation";
            }

            if (actionId == "open-content-placement")
            {
                return "scenes";
            }

            if (actionId == "open-spawn-encounter-planner")
            {
                return "spawns";
            }

            if (actionId == "open-resource-loot-planner" ||
                actionId == "open-crafting-progression")
            {
                return "resources";
            }

            if (actionId == "open-manifest-export")
            {
                return "export";
            }

            if (actionId == "open-extraction-readiness" ||
                actionId == "open-diagnostics")
            {
                return "diagnostics";
            }

            return "overview";
        }

        private static string ResolvePrimaryActionSectionId(
            DimensionTemplateCustomizerSessionReport session)
        {
            if (session == null)
            {
                return "overview";
            }

            string stageId = session.StageId;
            if (stageId == "fix-export-blocker" ||
                stageId == "fix-authoring")
            {
                return "diagnostics";
            }

            if (stageId == "configure-runtime-generation")
            {
                return "generation";
            }

            if (stageId == "preview-manifest" ||
                stageId == "validate-manifest" ||
                stageId == "ready")
            {
                return "export";
            }

            string actionId = session.PrimaryActionId;
            if (actionId == "validate-manifest" ||
                actionId == "preview-manifest" ||
                actionId == "apply-manifest" ||
                actionId == "export-manifest")
            {
                return "export";
            }

            if (actionId == "configure-runtime-generation" ||
                actionId == "prepare-runtime-generation")
            {
                return "generation";
            }

            if (actionId == "fix-authoring" ||
                actionId == "fix-issues" ||
                actionId == "fix-readiness-blocker" ||
                actionId == "review-diagnostics" ||
                actionId == "inspect-blockers")
            {
                return "diagnostics";
            }

            if (actionId == "configure-biomes" ||
                actionId == "fix-biomes")
            {
                return "biomes";
            }

            if (actionId == "configure-layout" ||
                actionId == "fix-layout")
            {
                return "layout";
            }

            if (actionId == "configure-generation" ||
                actionId == "fix-generation")
            {
                return "generation";
            }

            return "overview";
        }

        private void DrawSetupGuide()
        {
            DimensionFrameworkSetupGuide guide = workspace == null ? null : workspace.SetupGuide;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Setup guide", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (guide != null)
            {
                EditorGUILayout.LabelField(
                    "Ready " + guide.ReadyCount +
                    "   Advisory " + guide.WarningCount +
                    "   Blocked " + guide.BlockedCount,
                    EditorStyles.miniLabel,
                    GUILayout.Width(220f));
            }

            EditorGUILayout.EndHorizontal();

            if (guide == null)
            {
                EditorGUILayout.HelpBox("No setup guide is available for this workspace.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(
                guide.Message,
                guide.State == DimensionAuthoringReadinessState.Blocked
                    ? MessageType.Warning
                    : MessageType.Info);

            setupGuideScroll = EditorGUILayout.BeginScrollView(
                setupGuideScroll,
                GUILayout.MinHeight(120f),
                GUILayout.MaxHeight(190f));
            IReadOnlyList<DimensionFrameworkSetupGuideStep> steps = guide.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                DrawSetupGuideStep(steps[i]);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSetupGuideStep(DimensionFrameworkSetupGuideStep step)
        {
            Color oldColor = GUI.color;
            GUI.color = StateColor(step.State);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = oldColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(step.Title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(step.State.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(80f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(step.AssetKind, EditorStyles.miniBoldLabel);
            if (!string.IsNullOrEmpty(step.RecommendedLocation))
            {
                EditorGUILayout.LabelField("Suggested path: " + step.RecommendedLocation, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(step.Guidance))
            {
                EditorGUILayout.LabelField(step.Guidance, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(step.Verification))
            {
                EditorGUILayout.LabelField("Verify: " + step.Verification, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
