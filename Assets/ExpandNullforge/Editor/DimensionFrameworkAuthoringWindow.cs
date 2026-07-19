using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    public sealed class DimensionFrameworkAuthoringWindow : EditorWindow
    {
        private const string WindowTitle = "Dimensions API";
        private const float SidebarWidth = 220f;
        private const float CanvasMinHeight = 260f;
        private DimensionTemplateAsset selectedTemplate;
        private DimensionTemplateAuthoringWorkspace workspace;
        private DimensionTemplateCustomizerViewModel viewModel;
        private string activeSectionId = "overview";
        private int selectedBiomeIndex;
        private Vector2 rootScroll;
        private Vector2 detailScroll;
        private Vector2 notesScroll;
        private Vector2 actionScroll;
        private Vector2 toolGuideScroll;
        private Vector2 setupGuideScroll;
        private Vector2 extractionReadinessScroll;
        private string selectedGuideModuleId = string.Empty;
        private string selectedActionId;
        private string commandInputValue = string.Empty;
        private bool commandConfirmed;
        private bool allowAssetMutation;
        private bool allowRuntimeMutation;
        private DimensionTemplateCustomizerPreparedCommand preparedCommand;
        private bool autoUseProjectSelection = true;
        private string lastEditorActionMessage = string.Empty;
        private MessageType lastEditorActionType = MessageType.Info;
        private string displayedEditorActionMessage = string.Empty;
        private MessageType displayedEditorActionType = MessageType.Info;
        private Object lastGeneratedManifestAsset;
        private bool portalVisualProfileSetupQueued;
        private DimensionPortalAppearanceStudio portalAppearanceStudio;
        private DimensionTemplateAsset initializedPortalTemplate;
        private DimensionPortalVisualProfileAsset initializedPortalProfile;
        private Object serializedAssetBindingTarget;
        private SerializedObject serializedAssetBinding;
        private readonly Dictionary<string, SerializedProperty> serializedAssetBindingProperties =
            new Dictionary<string, SerializedProperty>();
        private System.Action pendingPortalTransition;
        private bool portalTransitionQueued;

        [MenuItem("Dimensions API/Authoring Dashboard")]
        public static void Open()
        {
            DimensionFrameworkAuthoringWindow window =
                GetWindow<DimensionFrameworkAuthoringWindow>(WindowTitle);
            window.minSize = new Vector2(1080f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            wantsMouseMove = false;
            portalAppearanceStudio = new DimensionPortalAppearanceStudio();
            Undo.undoRedoPerformed += ResetPortalProfileInitializationCache;
            if (!TryUseProjectSelection())
            {
                RebuildWorkspace();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= ResetPortalProfileInitializationCache;
            EditorApplication.delayCall -= ProcessPendingPortalTransition;
            pendingPortalTransition = null;
            portalTransitionQueued = false;
            ReleaseSerializedAssetBinding();
            if (portalAppearanceStudio != null)
            {
                portalAppearanceStudio.Dispose();
                portalAppearanceStudio = null;
            }
        }

        private void Update()
        {
            bool portalSectionActive = activeSectionId == "portals";
            bool previewRepaintsContinuously =
                portalSectionActive &&
                portalAppearanceStudio != null &&
                portalAppearanceStudio.IsPlaying &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating;
            bool needsMouseMoveEvents = portalSectionActive && !previewRepaintsContinuously;
            if (wantsMouseMove != needsMouseMoveEvents)
            {
                wantsMouseMove = needsMouseMoveEvents;
            }

            if (portalAppearanceStudio != null &&
                portalAppearanceStudio.Tick(portalSectionActive))
            {
                Repaint();
            }
        }

        private void OnSelectionChange()
        {
            if (!autoUseProjectSelection)
            {
                return;
            }

            TryUseProjectSelection();
        }

        private void OnGUI()
        {
            // Keep the header's control tree stable for the complete Layout/Repaint
            // pair. Portal Studio can publish an action message later in the same
            // Layout pass; reading that mutable value directly from DrawHeader would
            // add a HelpBox only during Repaint and invalidate GUILayout's recorded
            // group state.
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type == EventType.Layout)
            {
                displayedEditorActionMessage = lastEditorActionMessage;
                displayedEditorActionType = lastEditorActionType;
            }

            DrawHeader();
            DrawTemplatePicker();

            if (selectedTemplate == null || workspace == null || viewModel == null)
            {
                DrawMissingTemplateState();
                return;
            }

            rootScroll = EditorGUILayout.BeginScrollView(rootScroll);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawNavigationSidebar();
            GUILayout.Space(8f);
            DrawCurrentSection();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Dimensions API", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Create a Core Keeper dimension from its root Dimension Asset to a playable world. Start by creating or selecting that asset, then follow the steps below.",
                EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(displayedEditorActionMessage))
            {
                EditorGUILayout.HelpBox(
                    displayedEditorActionMessage,
                    displayedEditorActionType);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTemplatePicker()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            DimensionTemplateAsset requestedTemplate =
                (DimensionTemplateAsset)EditorGUILayout.ObjectField(
                "Dimension Asset",
                selectedTemplate,
                typeof(DimensionTemplateAsset),
                false);
            bool templateChanged = EditorGUI.EndChangeCheck();
            autoUseProjectSelection = EditorGUILayout.ToggleLeft(
                "Follow Project selection",
                autoUseProjectSelection,
                GUILayout.Width(180f));
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
            {
                TryUseProjectSelection();
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            {
                RebuildWorkspace();
            }

            EditorGUILayout.EndHorizontal();
            if (templateChanged)
            {
                if (requestedTemplate != selectedTemplate)
                {
                    RequestTemplateChange(requestedTemplate);
                }
            }
        }

        private void DrawMissingTemplateState()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(310f), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("First step", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Create a Dimension Asset before using the dashboard. This asset becomes the home for the dimension identity, starter biome, layout, resources, scenes, access rules, and export settings.",
                EditorStyles.wordWrappedLabel);
            GUILayout.Space(8f);
            if (GUILayout.Button("Create Dimension Asset", GUILayout.Height(34f)))
            {
                DimensionTemplateCreationWizardWindow.Open();
            }

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Use the wizard first. When it finishes, select the created Dimension Asset in the field above, or select it in the Project window and press Use Selection.",
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

        private void DrawExtractionReadiness()
        {
            DimensionFrameworkExtractionReadinessReport report =
                workspace == null ? null : workspace.ExtractionReadiness;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Framework extraction readiness", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (report != null)
            {
                EditorGUILayout.LabelField(
                    "Ready " + report.ReadyCount +
                    "   Advisory " + report.AdvisoryCount +
                    "   Blocked " + report.BlockedCount,
                    EditorStyles.miniLabel,
                    GUILayout.Width(240f));
            }

            EditorGUILayout.EndHorizontal();

            if (report == null)
            {
                EditorGUILayout.HelpBox(
                    "No extraction-readiness report is available for this workspace.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox(
                report.Message,
                report.State == DimensionAuthoringReadinessState.Blocked
                    ? MessageType.Warning
                    : MessageType.Info);

            extractionReadinessScroll = EditorGUILayout.BeginScrollView(
                extractionReadinessScroll,
                GUILayout.MinHeight(110f),
                GUILayout.MaxHeight(180f));
            IReadOnlyList<DimensionFrameworkExtractionReadinessItem> items = report.Items;
            for (int i = 0; i < items.Count; i++)
            {
                DrawExtractionReadinessItem(items[i]);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawExtractionReadinessItem(DimensionFrameworkExtractionReadinessItem item)
        {
            if (item == null)
            {
                return;
            }

            Color oldColor = GUI.color;
            GUI.color = StateColor(item.State);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.color = oldColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(item.Title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(item.State.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(80f));
            EditorGUILayout.EndHorizontal();

            if (item.BlocksExtraction)
            {
                EditorGUILayout.LabelField("Blocks extraction if unresolved", EditorStyles.miniBoldLabel);
            }

            if (!string.IsNullOrEmpty(item.Evidence))
            {
                EditorGUILayout.LabelField("Evidence: " + item.Evidence, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(item.Guidance))
            {
                EditorGUILayout.LabelField(item.Guidance, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNavigationSidebar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(SidebarWidth));
            EditorGUILayout.LabelField("Sections", EditorStyles.boldLabel);
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                viewModel.Navigation == null ? null : viewModel.Navigation.Sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    DimensionTemplateCustomizerSectionItem section = sections[i];
                    GUIStyle style = section.SectionId == activeSectionId
                        ? EditorStyles.toolbarButton
                        : EditorStyles.miniButton;
                    Color oldColor = GUI.color;
                    GUI.color = StateColor(section.State);
                    if (GUILayout.Button(SectionButtonText(section), style))
                    {
                        RequestSectionChange(section.SectionId);
                    }

                    GUI.color = oldColor;
                }
            }

            GUILayout.Space(8f);
            DrawReadinessLegend();
            EditorGUILayout.EndVertical();
        }

        private void DrawReadinessLegend()
        {
            EditorGUILayout.LabelField("Legend", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Ready = green");
            EditorGUILayout.LabelField("Partial = yellow");
            EditorGUILayout.LabelField("Blocked = red");
            EditorGUILayout.LabelField("Missing = gray");
        }

        private void DrawCurrentSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));

            DrawSectionMaturityBadge(activeSectionId);

            if (activeSectionId == "dimension")
            {
                DrawDimensionEditor();
            }
            else if (activeSectionId == "portals")
            {
                DrawPortalEditor();
            }
            else if (activeSectionId == "layout")
            {
                DrawLayoutEditor();
            }
            else if (activeSectionId == "biomes")
            {
                DrawBiomeEditor();
            }
            else if (activeSectionId == "terrain")
            {
                DrawTerrainEditor();
            }
            else if (activeSectionId == "generation")
            {
                DrawGenerationEditor();
            }
            else if (activeSectionId == "scenes")
            {
                DrawScenesEditor();
            }
            else if (activeSectionId == "resources")
            {
                DrawResourcesEditor();
            }
            else if (activeSectionId == "spawns")
            {
                DrawSpawnsEditor();
            }
            else if (activeSectionId == "export")
            {
                DrawExportEditor();
            }
            else if (activeSectionId == "diagnostics")
            {
                DrawDiagnosticsEditor();
            }
            else
            {
                DrawOverviewEditor();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Shows an honest maturity label for the active section, read from the capability
        /// registry, so a creator can see at a glance whether the controls below are proven,
        /// preview-only, or experimental before investing time in them.
        /// </summary>
        private void DrawSectionMaturityBadge(string sectionId)
        {
            string capabilityId = MaturityCapabilityForSection(sectionId);
            if (string.IsNullOrEmpty(capabilityId) ||
                !DimensionCapabilityRegistry.TryGet(
                    capabilityId,
                    out DimensionCapability capability))
            {
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = MaturityColor(capability.Maturity);
            EditorGUILayout.LabelField(
                "● Maturity: " + DimensionCapabilityRegistry.Describe(capability.Maturity),
                EditorStyles.miniBoldLabel);
            GUI.color = previousColor;
            EditorGUILayout.LabelField(capability.Note, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);
        }

        private static string MaturityCapabilityForSection(string sectionId)
        {
            switch (sectionId)
            {
                case "dimension":
                    return "dimension-identity-registry";
                case "portals":
                    return "portal-studio";
                case "layout":
                    return "coordinate-translation";
                case "biomes":
                    return "biomes-zones";
                case "terrain":
                case "generation":
                    return "generation";
                case "scenes":
                case "resources":
                case "spawns":
                    return "scenes-resources-spawns-events";
                case "export":
                    return "manifest-ownership";
                case "diagnostics":
                    return "diagnostics-readiness";
                default:
                    return "dashboard-wizard";
            }
        }

        private static Color MaturityColor(DimensionCapabilityMaturity maturity)
        {
            switch (maturity)
            {
                case DimensionCapabilityMaturity.ImplementedAndEvidenced:
                    return new Color(0.45f, 1.0f, 0.55f);
                case DimensionCapabilityMaturity.ImplementedNotFullyProven:
                    return new Color(0.6f, 0.9f, 1.0f);
                case DimensionCapabilityMaturity.PartialVerticalSlice:
                    return new Color(1.0f, 0.92f, 0.5f);
                case DimensionCapabilityMaturity.ContractExtensionSeam:
                    return new Color(1.0f, 0.85f, 0.5f);
                case DimensionCapabilityMaturity.AuthoringModelOnly:
                    return new Color(1.0f, 0.7f, 0.4f);
                case DimensionCapabilityMaturity.ExperimentalUnstable:
                    return new Color(1.0f, 0.55f, 0.45f);
                case DimensionCapabilityMaturity.NotImplemented:
                    return new Color(0.72f, 0.72f, 0.72f);
                default:
                    return Color.white;
            }
        }

        private void DrawOverviewEditor()
        {
            DrawSectionIntro(
                "Dimension overview",
                "A clean dashboard for the selected Dimension Asset.");

            DimensionTemplateManifestExportPreview exportPreview =
                viewModel.SessionReport.ManifestExportPreview;

            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Biomes", CountBiomes().ToString(), "Biome definitions.");
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
            DrawNamedValue("Space kind", definition.SpaceKind.ToString());
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
                DrawNamedValue("Floor IDs", biome.GetFloorObjectIdsWithPresets().Length.ToString());
                DrawNamedValue("Wall IDs", biome.GetWallObjectIdsWithPresets().Length.ToString());
                DrawNamedValue("Ore IDs", biome.GetOreObjectIdsWithPresets().Length.ToString());
                DrawNamedValue("Water IDs", biome.GetWaterObjectIdsWithPresets().Length.ToString());
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Generation mini-preview", EditorStyles.boldLabel);
            DrawNamedValue("Passes", CountGenerationPasses().ToString());
            DrawNamedValue("Tables", CountGenerationTables().ToString());
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
            DrawNamedValue("Resource nodes", CountAssets(selectedTemplate.GlobalResourceNodes).ToString());
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
            DrawNamedValue("Spawn rules", CountAssets(selectedTemplate.GlobalSpawnRules).ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Selected biome content", EditorStyles.boldLabel);
            DrawNamedValue("Scenes", biome == null ? "0" : CountBiomeScenes(biome).ToString());
            DrawNamedValue("Resources", biome == null ? "0" : CountBiomeResources(biome).ToString());
            DrawNamedValue("Spawns", biome == null ? "0" : CountBiomeSpawns(biome).ToString());
            DrawNamedValue("Scene spawn points", biome == null ? "0" : CountSceneSpawnPoints(biome.ScenePool).ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDimensionEditor()
        {
            DrawSectionIntro(
                "Dimension identity and access",
                "Name the dimension, reserve its coordinate space, and define how players enter it.");

            DimensionDefinition definition = selectedTemplate.ToDimensionDefinition();

            DrawSerializedAsset(
                selectedTemplate,
                "Dimension Asset",
                Field("displayName", "Dimension name"),
                Field("dimensionId", "Generated Dimension ID"),
                Field("description", "Description"),
                Field("contentPackId", "Content pack ID"),
                Field("contentPackDisplayName", "Content pack name"),
                Field("contentPackVersion", "Content pack version"),
                Field("contentPackAuthor", "Content pack author"),
                Field("absoluteOrigin", "Absolute origin"),
                Field("spaceKind", "Space kind"),
                Field("reservedLocalMin", "Reserved local min"),
                Field("reservedLocalMaxExclusive", "Reserved local max exclusive"),
                Field("portalAccessRules", "Portal access rules"));

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            DrawNamedValue("Dimension name", selectedTemplate.DisplayName);
            DrawNamedValue("Dimension ID", selectedTemplate.DimensionId);
            DrawNamedValue("Content pack", selectedTemplate.ContentPackDisplayName);
            DrawNamedValue("Pack ID", selectedTemplate.ContentPackId);
            DrawNamedValue("API version", selectedTemplate.MinimumApiVersion.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Coordinates", EditorStyles.boldLabel);
            DrawNamedValue("Absolute origin", FormatInt2(definition.AbsoluteOrigin));
            DrawNamedValue("Local origin preview", "0, 0 at " + FormatInt2(definition.AbsoluteOrigin));
            DrawNamedValue("Reserved local bounds", FormatBounds(definition.LocalBounds));
            DrawNamedValue("Space kind", definition.SpaceKind.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Portal Access Rule", GUILayout.Width(180f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreatePortalAccessRule(selectedTemplate));
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            DrawPortalAccessRuleEditors();
        }

        private void DrawPortalEditor()
        {
            DimensionPortalVisualProfileAsset profile = selectedTemplate.PortalVisualProfile;
            if (profile == null)
            {
                QueuePortalVisualProfileSetup();
            }
            profile = selectedTemplate.PortalVisualProfile;

            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    portalVisualProfileSetupQueued
                        ? "Preparing and assigning this dimension's vanilla-default portal visual profile."
                        : "The dashboard could not create or assign this dimension's default portal.",
                    portalVisualProfileSetupQueued
                        ? MessageType.Info
                        : MessageType.Warning);
                if (GUILayout.Button("Retry Automatic Profile Setup", GUILayout.Width(260f)))
                {
                    CreatePortalVisualProfileAsset();
                }

                return;
            }

            EnsurePortalProfileInitializedOnce(profile);

            if (portalAppearanceStudio == null)
            {
                portalAppearanceStudio = new DimensionPortalAppearanceStudio();
            }

            DimensionPortalAppearanceStudio.DrawResult studioResult =
                portalAppearanceStudio.Draw(
                    selectedTemplate,
                    profile,
                    Mathf.Max(360f, position.width - SidebarWidth - 40f),
                    position.height);
            if (!string.IsNullOrEmpty(studioResult.Message))
            {
                lastEditorActionMessage = studioResult.Message;
                lastEditorActionType = studioResult.MessageType;
            }

            if (studioResult.TemplateChanged)
            {
                RebuildWorkspace();
                Repaint();
            }

            if (studioResult.UseProfileRequested != null)
            {
                DimensionPortalVisualProfileAsset requestedProfile =
                    studioResult.UseProfileRequested;
                bool switched = studioResult.CanApply &&
                    TryUsePortalVisualProfile(requestedProfile, out _);
                portalAppearanceStudio.NotifyRuntimeSyncResult(
                    requestedProfile,
                    switched);
            }
            else if (studioResult.RuntimeSyncRequested)
            {
                DimensionPortalVisualProfileAsset boundProfile =
                    selectedTemplate.PortalVisualProfile;
                bool synchronized = studioResult.CanApply &&
                    TryApplyPortalVisualChanges(out _);
                portalAppearanceStudio.NotifyRuntimeSyncResult(
                    boundProfile,
                    synchronized);
            }
            if (!studioResult.CanApply)
            {
                EditorGUILayout.HelpBox(
                    "Resolve the Portal Studio errors before applying generated portal assets.",
                    MessageType.Error);
            }

        }

        private void EnsurePortalProfileInitializedOnce(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null ||
                (initializedPortalTemplate == selectedTemplate &&
                 initializedPortalProfile == profile))
            {
                return;
            }

            initializedPortalTemplate = selectedTemplate;
            initializedPortalProfile = profile;
            if (DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    selectedTemplate,
                    profile,
                    out string artworkInitializationMessage) &&
                !string.IsNullOrEmpty(artworkInitializationMessage))
            {
                lastEditorActionMessage = artworkInitializationMessage;
                lastEditorActionType = MessageType.Info;
            }
        }

        private void ResetPortalProfileInitializationCache()
        {
            initializedPortalTemplate = null;
            initializedPortalProfile = null;
        }

        private void CreatePortalVisualProfileAsset()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            bool created;
            string message;
            DimensionPortalVisualProfileAsset profile =
                DimensionPortalVisualProfileEditorUtility.EnsureAssigned(
                    selectedTemplate,
                    true,
                    true,
                    out created,
                    out message);
            lastEditorActionMessage = message;
            lastEditorActionType = profile == null
                ? MessageType.Warning
                : MessageType.Info;
            if (profile != null)
            {
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
                RebuildWorkspace();
            }

            Repaint();
        }

        private void QueuePortalVisualProfileSetup()
        {
            if (selectedTemplate == null || portalVisualProfileSetupQueued)
            {
                return;
            }

            portalVisualProfileSetupQueued = true;
            DimensionTemplateAsset template = selectedTemplate;
            EditorApplication.delayCall += () =>
            {
                portalVisualProfileSetupQueued = false;
                if (this == null || template == null || selectedTemplate != template)
                {
                    return;
                }

                bool created;
                string message;
                DimensionPortalVisualProfileAsset profile =
                    DimensionPortalVisualProfileEditorUtility.EnsureAssigned(
                        template,
                        true,
                        true,
                        out created,
                        out message);
                if (this == null)
                {
                    return;
                }

                lastEditorActionMessage = message;
                lastEditorActionType = profile == null
                    ? MessageType.Warning
                    : MessageType.Info;
                if (profile != null && selectedTemplate == template)
                {
                    RebuildWorkspace();
                }

                Repaint();
            };
        }

        private bool TryUsePortalVisualProfile(
            DimensionPortalVisualProfileAsset requestedProfile,
            out string message)
        {
            message = string.Empty;
            if (selectedTemplate == null || requestedProfile == null)
            {
                message = "Select a Dimension Asset and portal profile before using it.";
                SetLastEditorAction(message, MessageType.Warning);
                return false;
            }

            DimensionTemplateAsset template = selectedTemplate;
            if (!DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    requestedProfile))
            {
                message =
                    "The selected portal profile does not belong to this Dimension Asset.";
                SetLastEditorAction(message, MessageType.Error);
                return false;
            }

            DimensionPortalVisualProfileAsset previousProfile =
                template.PortalVisualProfile;
            if (previousProfile == requestedProfile)
            {
                message = "This portal profile is already in use.";
                SetLastEditorAction(message, MessageType.Info);
                return true;
            }

            Undo.RecordObject(template, "Use Portal Profile");
            template.SetPortalVisualProfile(requestedProfile);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            ResetPortalProfileInitializationCache();

            if (TryApplyPortalVisualChanges(out message))
            {
                RebuildWorkspace();
                Repaint();
                return true;
            }

            template.SetPortalVisualProfile(previousProfile);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            ResetPortalProfileInitializationCache();
            RebuildWorkspace();
            message = string.IsNullOrEmpty(message)
                ? "The portal profile could not be applied. The previous profile remains in use."
                : message + " The previous portal profile remains in use.";
            SetLastEditorAction(message, MessageType.Error);
            Repaint();
            return false;
        }

        private bool TryApplyPortalVisualChanges(out string message)
        {
            message = string.Empty;
            if (selectedTemplate == null || selectedTemplate.PortalVisualProfile == null)
            {
                message =
                    "Assign a portal visual profile before regenerating portal assets.";
                SetLastEditorAction(message, MessageType.Warning);
                return false;
            }

            if (portalAppearanceStudio != null &&
                !portalAppearanceStudio.FlushPendingFrameTextureUpdate(
                    out string frameTextureError))
            {
                message = frameTextureError;
                SetLastEditorAction(message, MessageType.Error);
                return false;
            }

            if (!DimensionPortalArtworkEditorUtility.FlushPending(
                    selectedTemplate.PortalVisualProfile,
                    out string artworkError))
            {
                message = artworkError;
                SetLastEditorAction(message, MessageType.Error);
                return false;
            }

            AssetDatabase.SaveAssets();
            DimensionTemplateManifestExportPreview preview =
                DimensionTemplateManifestExportPreviewBuilder.Build(
                    selectedTemplate,
                    true,
                    false,
                    "Apply portal visual changes.");
            DimensionRuntimeConsumerBootstrapResult result =
                DimensionRuntimeConsumerBootstrapUtility.EnsureGeneratedRuntime(
                    selectedTemplate,
                    preview);
            message = result.Message;
            SetLastEditorAction(
                message,
                result.Executed
                ? MessageType.Info
                : MessageType.Warning);
            if (result.Executed)
            {
                lastGeneratedManifestAsset = result.RuntimeManifestAsset;
                RebuildWorkspace();
            }

            Repaint();
            return result.Executed;
        }

        private void DrawPortalAccessRuleEditors()
        {
            DimensionPortalAccessRuleAsset[] rules = selectedTemplate.PortalAccessRules;
            if (rules == null || rules.Length == 0)
            {
                DrawEditorCard(
                    "Portal access rules",
                    "No portal, generated entrance, return portal, or inventory-item access rule is configured yet.");
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                string title = rule == null || string.IsNullOrEmpty(rule.DisplayName)
                    ? "Portal access rule " + (i + 1).ToString()
                    : rule.DisplayName;
                DrawSerializedAsset(
                    rule,
                    title,
                    Field("displayName", "Display name"),
                    Field("ruleId", "Rule ID"),
                    Field("portalId", "Portal ID"),
                    Field("presentationId", "Presentation ID"),
                    Field("fromDimensionId", "From dimension"),
                    Field("fromLocalPosition", "From local position"),
                    Field("toDimensionId", "To dimension"),
                    Field("toLocalPosition", "To local position"),
                    Field("accessKind", "Access mode"),
                    Field("activationMode", "Activation mode"),
                    Field("activationCooldownSeconds", "Cooldown seconds"),
                    Field("requireGeneratedAreaOnUse", "Require generated area"),
                    Field("allowFallbackPositionOnUse", "Allow fallback position"),
                    Field("promptText", "Prompt"),
                    Field("lockedPromptText", "Locked prompt"),
                    Field("iconId", "Icon ID"),
                    Field("visualEffectId", "Visual effect ID"),
                    Field("audioCueId", "Audio cue ID"),
                    Field("priority", "Priority"),
                    Field("enabled", "Enabled"),
                    Field("interactable", "Interactable"),
                    Field("requiredItems", "Required activation items"));
            }
        }

        private void DrawLayoutEditor()
        {
            DrawSectionIntro(
                "World layout",
                "Shape where every biome lives before terrain, scenes, resources, and spawn rules are applied.");

            DrawContextualPreviewCanvas(viewModel.PreviewCanvas);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Layout Template", GUILayout.Width(180f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateLayoutTemplate(selectedTemplate));
            }

            using (new EditorGUI.DisabledScope(selectedTemplate.LayoutTemplate == null))
            {
                if (GUILayout.Button("Add Biome Region", GUILayout.Width(160f)))
                {
                    RunAssetAction(
                        DimensionFrameworkAuthoringAssetUtility.AddLayoutRegion(
                            selectedTemplate.LayoutTemplate,
                            GetSelectedBiomeOrFirst()));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
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
                Field("radialBandSizeTiles", "Radial band size"),
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

        private void DrawBiomeEditor()
        {
            DrawSectionIntro(
                "Biomes",
                "Create and maintain the biome definitions that later terrain, generation, resources, scenes, and spawns build on.");

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
                Field("biomeId", "Biome ID"),
                Field("mapColor", "Map color"),
                Field("priority", "Priority"),
                Field("enabled", "Enabled"),
                Field("environmentProfileId", "Environment profile ID"),
                Field("environmentProfileTemplate", "Environment profile"),
                Field("paletteAssetId", "Palette ID"),
                Field("paletteTemplate", "Palette"),
                Field("contentPresets", "Content presets"),
                Field("hasFallbackLocalBounds", "Has fallback bounds"),
                Field("fallbackLocalMin", "Fallback local min"),
                Field("fallbackLocalMaxExclusive", "Fallback local max"),
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
            DrawNamedValue("Floor IDs", biome.GetFloorObjectIdsWithPresets().Length.ToString());
            DrawNamedValue("Wall IDs", biome.GetWallObjectIdsWithPresets().Length.ToString());
            DrawNamedValue("Ore IDs", biome.GetOreObjectIdsWithPresets().Length.ToString());
            DrawNamedValue("Water IDs", biome.GetWaterObjectIdsWithPresets().Length.ToString());
            DrawNamedValue("Palette", biome.ResolvedPaletteAssetId);
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
            DrawDashboardMetric("Terrain tables", CountBiomeGenerationTables(biome).ToString(), "Tables that decide terrain objects.");
            DrawDashboardMetric("Generation passes", CountBiomeGenerationPasses(biome).ToString(), "Passes that apply terrain rules.");
            DrawDashboardMetric("Environment", string.IsNullOrEmpty(biome.ResolvedEnvironmentProfileId) ? "-" : biome.ResolvedEnvironmentProfileId, "Fog, lighting, music, and map color profile.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawSerializedAsset(
                biome,
                "Selected Biome Terrain",
                Field("floorObjectIds", "Floor object IDs"),
                Field("wallObjectIds", "Wall object IDs"),
                Field("oreObjectIds", "Ore object IDs"),
                Field("waterObjectIds", "Water object IDs"),
                Field("paletteAssetId", "Palette ID"),
                Field("paletteTemplate", "Palette"),
                Field("environmentProfileId", "Environment profile ID"),
                Field("environmentProfileTemplate", "Environment profile"),
                Field("generationProfile", "Generation profile"),
                Field("generationPasses", "Generation passes"),
                Field("generationTables", "Generation tables"));

            DrawSerializedAsset(
                biome.PaletteTemplate,
                "Selected Biome Palette",
                Field("paletteId", "Palette ID"),
                Field("displayName", "Display name"),
                Field("resourceKey", "Resource key"),
                Field("priority", "Priority"),
                Field("enabled", "Enabled"),
                Field("entries", "Palette entries"),
                Field("notes", "Notes"));

            DrawSerializedAsset(
                biome.EnvironmentProfileTemplate,
                "Selected Biome Environment",
                Field("profileId", "Profile ID"),
                Field("displayName", "Display name"),
                Field("zoneId", "Zone ID"),
                Field("mapColor", "Map color"),
                Field("ambientCueId", "Ambient cue ID"),
                Field("musicCueId", "Music cue ID"),
                Field("lightingProfileId", "Lighting profile ID"),
                Field("fogProfileId", "Fog profile ID"),
                Field("hasMapColor", "Has map color"),
                Field("priority", "Priority"),
                Field("enabled", "Enabled"));

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
            DrawDashboardMetric("Biomes", CountBiomes().ToString(), "Biome definitions participating in generation.");
            DrawDashboardMetric("Tables", CountGenerationTables().ToString(), "Terrain/object/liquid/ore tables.");
            DrawDashboardMetric("Passes", CountGenerationPasses().ToString(), "Ordered generation passes.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Generation Assets",
                Field("globalGenerationPasses", "Global generation passes"),
                Field("globalGenerationTables", "Global generation tables"));

            BiomeTemplateAsset biome = DrawBiomeSelectorHeader();
            if (biome != null)
            {
                DrawSerializedAsset(
                    biome,
                    "Selected Biome Generation",
                    Field("generationProfile", "Generation profile"),
                    Field("generationPasses", "Generation passes"),
                    Field("generationTables", "Generation tables"));

                DrawSerializedAsset(
                    biome.GenerationProfile,
                    "Selected Biome Generation Profile",
                    Field("profileId", "Profile ID"),
                    Field("displayName", "Display name"),
                    Field("generationPasses", "Generation passes"),
                    Field("terrainTables", "Terrain tables"),
                    Field("floorTables", "Floor tables"),
                    Field("wallTables", "Wall tables"),
                    Field("liquidTables", "Liquid tables"),
                    Field("oreTables", "Ore tables"),
                    Field("objectTables", "Object tables"),
                    Field("sceneTables", "Scene tables"),
                    Field("spawnTables", "Spawn tables"),
                    Field("resourceTables", "Resource tables"),
                    Field("worldEventTables", "World event tables"),
                    Field("customTables", "Custom tables"),
                    Field("notes", "Notes"));
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
            DrawDashboardMetric("Contents", CountSceneContents().ToString(), "Props, loot containers, spawn points, and triggers inside scenes.");
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

            if (GUILayout.Button("Add Recipe", GUILayout.Width(100f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateRecipe(selectedTemplate));
            }

            if (GUILayout.Button("Add Workbench", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateWorkbench(selectedTemplate));
            }

            if (GUILayout.Button("Add Loot Table", GUILayout.Width(120f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateLootTable(selectedTemplate));
            }

            if (GUILayout.Button("Add Global Node", GUILayout.Width(128f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateResourceNode(selectedTemplate, null));
            }

            using (new EditorGUI.DisabledScope(biome == null))
            {
                if (GUILayout.Button("Add Biome Node", GUILayout.Width(126f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateResourceNode(selectedTemplate, biome));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawItemGenerationBar();

            GUILayout.Space(6f);
            int biomeResources = biome == null ? 0 : CountBiomeResources(biome);
            int globalResourceNodes = CountAssets(selectedTemplate.GlobalResourceNodes);
            int globalItems = CountAssets(selectedTemplate.GlobalItems);
            int globalRecipes = CountAssets(selectedTemplate.GlobalRecipes);
            int globalWorkbenches = CountAssets(selectedTemplate.GlobalWorkbenches);
            int globalLootTables = CountAssets(selectedTemplate.GlobalLootTables);
            int globalResources = globalResourceNodes +
                globalItems +
                globalRecipes +
                globalWorkbenches +
                globalLootTables;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("This biome", biomeResources.ToString(), "Resource nodes attached to the selected biome.");
            DrawDashboardMetric("Global", globalResources.ToString(), "Global nodes, items, recipes, workbenches, and loot tables.");
            DrawDashboardMetric("Total", CountResources().ToString(), "All obtainable content currently declared.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Items", globalItems.ToString(), "Global item definitions.");
            DrawDashboardMetric("Recipes", globalRecipes.ToString(), "Global recipe definitions.");
            DrawDashboardMetric("Workbenches", globalWorkbenches.ToString(), "Global crafting station definitions.");
            DrawDashboardMetric("Loot", globalLootTables.ToString(), "Global loot table definitions.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Resource Assets",
                Field("globalResourceNodes", "Resource nodes"),
                Field("globalItems", "Items"),
                Field("globalRecipes", "Recipes"),
                Field("globalWorkbenches", "Workbenches"),
                Field("globalLootTables", "Loot tables"));

            if (biome != null)
            {
                DrawSerializedAsset(
                    biome,
                    "Selected Biome Resource Nodes",
                    Field("resourceNodes", "Resource nodes"));
            }

            DrawResourceAssetEditors(selectedTemplate.GlobalResourceNodes, "Global Resource Node");
            DrawItemAssetEditors(selectedTemplate.GlobalItems);
            DrawRecipeAssetEditors(selectedTemplate.GlobalRecipes);
            DrawWorkbenchAssetEditors(selectedTemplate.GlobalWorkbenches);
            DrawLootTableAssetEditors(selectedTemplate.GlobalLootTables);
            if (biome != null)
            {
                DrawResourceAssetEditors(biome.ResourceNodes, "Biome Resource Node");
            }

            GUILayout.Space(8f);
            if (biomeResources == 0 && globalResources == 0)
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

            if (GUILayout.Button("Add Global Rule", GUILayout.Width(124f)))
            {
                RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnRule(selectedTemplate, null));
            }

            using (new EditorGUI.DisabledScope(biome == null))
            {
                if (GUILayout.Button("Add Biome Rule", GUILayout.Width(120f)))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.CreateSpawnRule(selectedTemplate, biome));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            int biomeSpawns = biome == null ? 0 : CountBiomeSpawns(biome);
            int globalAnimals = CountAssets(selectedTemplate.GlobalAnimals);
            int globalCritters = CountAssets(selectedTemplate.GlobalCritters);
            int globalMobs = CountAssets(selectedTemplate.GlobalMobs);
            int globalBosses = CountAssets(selectedTemplate.GlobalBosses);
            int globalSpawnRules = CountAssets(selectedTemplate.GlobalSpawnRules);
            int globalSceneSpawnPoints = CountSceneSpawnPoints(selectedTemplate.GlobalScenes);
            int globalSpawns = globalAnimals +
                globalCritters +
                globalMobs +
                globalBosses +
                globalSpawnRules +
                globalSceneSpawnPoints;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("This biome", biomeSpawns.ToString(), "Spawn rules and scene spawn points attached to the selected biome.");
            DrawDashboardMetric("Global", globalSpawns.ToString(), "Global animals, critters, mobs, bosses, spawn rules, and scene spawn points.");
            DrawDashboardMetric("Total", CountSpawns().ToString(), "All spawn content currently declared.");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Animals", globalAnimals.ToString(), "Global passive animal definitions.");
            DrawDashboardMetric("Critters", globalCritters.ToString(), "Global critter definitions.");
            DrawDashboardMetric("Mobs", globalMobs.ToString(), "Global hostile or custom mob definitions.");
            DrawDashboardMetric("Bosses", globalBosses.ToString(), "Global boss definitions.");
            EditorGUILayout.EndHorizontal();

            DrawSerializedAsset(
                selectedTemplate,
                "Global Spawn Assets",
                Field("globalAnimals", "Animals"),
                Field("globalCritters", "Critters"),
                Field("globalMobs", "Mobs"),
                Field("globalBosses", "Bosses"),
                Field("globalSpawnRules", "Spawn rules"));

            if (biome != null)
            {
                DrawSerializedAsset(
                    biome,
                    "Selected Biome Spawn Rules",
                    Field("spawnRules", "Spawn rules"));
            }

            DrawAnimalAssetEditors(selectedTemplate.GlobalAnimals);
            DrawCritterAssetEditors(selectedTemplate.GlobalCritters);
            DrawMobAssetEditors(selectedTemplate.GlobalMobs);
            DrawBossAssetEditors(selectedTemplate.GlobalBosses);
            DrawSpawnRuleAssetEditors(selectedTemplate.GlobalSpawnRules, "Global Spawn Rule");
            if (biome != null)
            {
                DrawSpawnRuleAssetEditors(biome.SpawnRules, "Biome Spawn Rule");
            }

            if (biomeSpawns == 0 && globalSpawns == 0)
            {
                DrawEditorCard(
                    "No spawn rules yet",
                    "Add passive animals, critters, mobs, bosses, NPCs, and spawn rules once the biome habitat is designed.");
                return;
            }

            DrawEditorCard(
                "Habitat editor",
                "Configure creature and boss behavior by biome: spawn conditions, weights, scripts, visuals, sounds, boss arenas, summon items, and respawn rules.");
        }

        private void DrawSceneTemplateEditors(SceneTemplateAsset[] scenes, string titlePrefix)
        {
            if (scenes == null)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    scene,
                    BuildAssetEditorTitle(titlePrefix, scene.DisplayName, scene.SceneId, i),
                    Field("displayName", "Display name"),
                    Field("sceneId", "Scene ID"),
                    Field("templateId", "Template ID"),
                    Field("kind", "Kind"),
                    Field("providerId", "Provider ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("placementMode", "Placement mode"),
                    Field("footprintSize", "Footprint size"),
                    Field("exactLocalPosition", "Exact local position"),
                    Field("preferredLocalMin", "Preferred local min"),
                    Field("preferredLocalMaxExclusive", "Preferred local max"),
                    Field("weight", "Weight"),
                    Field("priority", "Priority"),
                    Field("enabled", "Enabled"),
                    Field("required", "Required"),
                    Field("unique", "Unique"),
                    Field("props", "Props"),
                    Field("lootContainers", "Loot containers"),
                    Field("spawnPoints", "Spawn points"),
                    Field("triggers", "Triggers"));
            }
        }

        private void DrawResourceAssetEditors(ResourceNodeTemplateAsset[] nodes, string titlePrefix)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                ResourceNodeTemplateAsset node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    node,
                    BuildAssetEditorTitle(titlePrefix, node.name, node.NodeId, i),
                    Field("displayName", "Display name"),
                    Field("nodeId", "Node ID"),
                    Field("zoneId", "Zone ID"),
                    Field("hasLocalBounds", "Has local bounds"),
                    Field("localMin", "Local min"),
                    Field("localMaxExclusive", "Local max"),
                    Field("resourceId", "Resource ID"),
                    Field("kind", "Kind"),
                    Field("providerId", "Provider ID"),
                    Field("generationPassId", "Generation pass ID"),
                    Field("weight", "Weight"),
                    Field("priority", "Priority"),
                    Field("enabled", "Enabled"));
            }
        }

        private void DrawItemAssetEditors(DimensionItemAsset[] items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    item,
                    BuildAssetEditorTitle("Item", item.DisplayName, item.ItemId, i),
                    BuildItemFields(item));

                DrawItemArchetypeSummary(item);
            }
        }

        /// <summary>
        /// Generation bar for items: reports how many are ready and how many are blocked, and
        /// only offers the action when there is something valid to build.
        /// </summary>
        private void DrawItemGenerationBar()
        {
            DimensionItemAsset[] items = selectedTemplate == null
                ? null
                : selectedTemplate.GlobalItems;

            int ready = 0;
            int blocked = 0;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    DimensionItemAsset item = items[i];
                    if (item == null || !item.Enabled)
                    {
                        continue;
                    }

                    if (DimensionItemArchetypeValidator.CanGenerate(item))
                    {
                        ready++;
                    }
                    else
                    {
                        blocked++;
                    }
                }
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(ready <= 0))
            {
                if (GUILayout.Button(
                    "Generate " + ready + " Item Prefab" + (ready == 1 ? string.Empty : "s"),
                    GUILayout.Width(210f)))
                {
                    GenerateItemPrefabs(items);
                }
            }

            if (blocked > 0)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.5f);
                EditorGUILayout.LabelField(
                    "● " + blocked + " item(s) blocked — see the errors below.",
                    EditorStyles.miniLabel);
                GUI.color = previousColor;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Generates the item prefabs into the consumer's own mod folder, then reports exactly
        /// what was written and anything that needs a manual pass.
        /// </summary>
        private void GenerateItemPrefabs(DimensionItemAsset[] items)
        {
            string templatePath = AssetDatabase.GetAssetPath(selectedTemplate);
            string modRoot =
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath);
            if (string.IsNullOrEmpty(modRoot))
            {
                EditorUtility.DisplayDialog(
                    "Generate Items",
                    "Could not resolve the mod folder that owns this template, so there is " +
                    "nowhere to put the generated prefabs. Save the template inside a mod " +
                    "folder first.",
                    "OK");
                return;
            }

            string outputFolder = modRoot + "/Items";
            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(items, outputFolder);

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + report.Errors[i]);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + report.Warnings[i]);
            }

            EditorUtility.DisplayDialog(
                "Generate Items",
                report.Summarize() + "\n\nOutput: " + outputFolder +
                (report.HasProblems
                    ? "\n\nDetails were written to the Console."
                    : string.Empty),
                "OK");
        }

        /// <summary>
        /// Builds the field list for an item from its archetype, so a creator only sees the values
        /// their chosen kind actually uses — a material does not ask for weapon damage, and a
        /// weapon does not hide it.
        /// </summary>
        private static SerializedFieldSpec[] BuildItemFields(DimensionItemAsset item)
        {
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(item.Archetype);

            List<SerializedFieldSpec> fields = new List<SerializedFieldSpec>
            {
                Field("displayName", "Display name"),
                Field("itemId", "Item ID"),
                Field("archetype", "Archetype"),
                Field("kind", "Kind"),
                Field("iconSprite", "Icon sprite"),
                Field("iconId", "Icon ID (fallback)"),
                Field("objectId", "Object ID")
            };

            if (RequiresComponent(required, DimensionItemAuthoringComponents.InventoryItem))
            {
                fields.Add(Field("maxStack", "Max stack"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Loot))
            {
                fields.Add(Field("lootTableId", "Loot table ID"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Breakable) ||
                RequiresComponent(required, DimensionItemAuthoringComponents.Creature))
            {
                fields.Add(Field("healthPoints", "Health"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("durabilityPoints", "Durability"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.WeaponDamage))
            {
                fields.Add(Field("damageAmount", "Damage"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Cooldown))
            {
                fields.Add(Field("cooldownSeconds", "Cooldown seconds"));
            }

            fields.Add(Field("rarityId", "Rarity ID"));
            fields.Add(Field("enabled", "Enabled"));
            fields.Add(Field("notes", "Notes"));
            return fields.ToArray();
        }

        /// <summary>
        /// Shows what the archetype will generate and anything still missing, so an incomplete
        /// item is caught here rather than discovered in-game.
        /// </summary>
        private void DrawItemArchetypeSummary(DimensionItemAsset item)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(
                "Generates: " + DimensionItemArchetypeRules.Describe(item.Archetype) + " — " +
                DimensionItemArchetypeRules.DescribeComponents(item.Archetype),
                EditorStyles.wordWrappedMiniLabel);

            List<DimensionItemArchetypeValidator.Finding> findings =
                DimensionItemArchetypeValidator.Validate(item);
            for (int i = 0; i < findings.Count; i++)
            {
                DimensionItemArchetypeValidator.Finding finding = findings[i];
                if (finding.Severity == DimensionItemArchetypeValidator.Severity.Ok)
                {
                    continue;
                }

                bool error = finding.Severity == DimensionItemArchetypeValidator.Severity.Error;
                Color previousColor = GUI.color;
                GUI.color = error
                    ? new Color(1f, 0.55f, 0.5f)
                    : new Color(1f, 0.85f, 0.45f);
                EditorGUILayout.LabelField(
                    (error ? "● " : "▲ ") + finding.Message,
                    EditorStyles.wordWrappedMiniLabel);
                GUI.color = previousColor;
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(4f);
        }

        private static bool RequiresComponent(
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component)
        {
            return (required & component) == component;
        }

        private void DrawRecipeAssetEditors(DimensionRecipeAsset[] recipes)
        {
            if (recipes == null)
            {
                return;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                DimensionRecipeAsset recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    recipe,
                    BuildAssetEditorTitle("Recipe", recipe.DisplayName, recipe.RecipeId, i),
                    Field("displayName", "Display name"),
                    Field("recipeId", "Recipe ID"),
                    Field("outputItemId", "Output item ID"),
                    Field("outputAmount", "Output amount"),
                    Field("craftingStationId", "Crafting station ID"),
                    Field("craftTimeSeconds", "Craft time seconds"),
                    Field("enabled", "Enabled"),
                    Field("ingredients", "Ingredients"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawWorkbenchAssetEditors(DimensionWorkbenchAsset[] workbenches)
        {
            if (workbenches == null)
            {
                return;
            }

            for (int i = 0; i < workbenches.Length; i++)
            {
                DimensionWorkbenchAsset workbench = workbenches[i];
                if (workbench == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    workbench,
                    BuildAssetEditorTitle("Workbench", workbench.DisplayName, workbench.WorkbenchId, i),
                    Field("displayName", "Display name"),
                    Field("workbenchId", "Workbench ID"),
                    Field("objectId", "Object ID"),
                    Field("iconId", "Icon ID"),
                    Field("recipes", "Recipes"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawLootTableAssetEditors(DimensionLootTableAsset[] lootTables)
        {
            if (lootTables == null)
            {
                return;
            }

            for (int i = 0; i < lootTables.Length; i++)
            {
                DimensionLootTableAsset lootTable = lootTables[i];
                if (lootTable == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    lootTable,
                    BuildAssetEditorTitle("Loot Table", lootTable.DisplayName, lootTable.LootTableId, i),
                    Field("displayName", "Display name"),
                    Field("lootTableId", "Loot table ID"),
                    Field("allowEmptyRoll", "Allow empty roll"),
                    Field("enabled", "Enabled"),
                    Field("entries", "Entries"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawSpawnRuleAssetEditors(SpawnRuleTemplateAsset[] rules, string titlePrefix)
        {
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                SpawnRuleTemplateAsset rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    rule,
                    BuildAssetEditorTitle(titlePrefix, rule.name, rule.RuleId, i),
                    Field("displayName", "Display name"),
                    Field("ruleId", "Rule ID"),
                    Field("zoneId", "Zone ID"),
                    Field("hasLocalBounds", "Has local bounds"),
                    Field("localMin", "Local min"),
                    Field("localMaxExclusive", "Local max"),
                    Field("subjectId", "Subject ID"),
                    Field("subjectKind", "Subject kind"),
                    Field("weight", "Weight"),
                    Field("priority", "Priority"),
                    Field("enabled", "Enabled"));
            }
        }

        private void DrawAnimalAssetEditors(DimensionAnimalAsset[] animals)
        {
            if (animals == null)
            {
                return;
            }

            for (int i = 0; i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    animal,
                    BuildAssetEditorTitle("Animal", animal.DisplayName, animal.AnimalId, i),
                    Field("displayName", "Display name"),
                    Field("animalId", "Animal ID"),
                    Field("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("stats", "Stats"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("spawnWeight", "Spawn weight"),
                    Field("herdMin", "Herd min"),
                    Field("herdMax", "Herd max"),
                    Field("friendly", "Friendly"),
                    Field("tameable", "Tameable"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawCritterAssetEditors(DimensionCritterAsset[] critters)
        {
            if (critters == null)
            {
                return;
            }

            for (int i = 0; i < critters.Length; i++)
            {
                DimensionCritterAsset critter = critters[i];
                if (critter == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    critter,
                    BuildAssetEditorTitle("Critter", critter.DisplayName, critter.CritterId, i),
                    Field("displayName", "Display name"),
                    Field("critterId", "Critter ID"),
                    Field("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("spawnWeight", "Spawn weight"),
                    Field("scatterOnApproach", "Scatter on approach"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawMobAssetEditors(DimensionMobAsset[] mobs)
        {
            if (mobs == null)
            {
                return;
            }

            for (int i = 0; i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    mob,
                    BuildAssetEditorTitle("Mob", mob.DisplayName, mob.MobId, i),
                    Field("displayName", "Display name"),
                    Field("mobId", "Mob ID"),
                    Field("objectId", "Object ID"),
                    Field("allowedBiomeIds", "Allowed biome IDs"),
                    Field("stats", "Stats"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("aggression", "Aggression"),
                    Field("spawnWeight", "Spawn weight"),
                    Field("behaviorScriptId", "Behavior script ID"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawBossAssetEditors(DimensionBossAsset[] bosses)
        {
            if (bosses == null)
            {
                return;
            }

            for (int i = 0; i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    boss,
                    BuildAssetEditorTitle("Boss", boss.DisplayName, boss.BossId, i),
                    Field("displayName", "Display name"),
                    Field("bossId", "Boss ID"),
                    Field("objectId", "Object ID"),
                    Field("arenaSceneId", "Arena scene ID"),
                    Field("summoningItemId", "Summoning item ID"),
                    Field("stats", "Stats"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("activation", "Activation"),
                    Field("phases", "Phases"),
                    Field("respawnCooldownMinutes", "Respawn cooldown minutes"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        private void DrawExportEditor()
        {
            DrawSectionIntro(
                "Export",
                "Preview the dimension manifest and export when required authoring data is valid.");

            DimensionTemplateCustomizerSessionReport session = viewModel.SessionReport;
            DimensionTemplateManifestExportPreview preview = session.ManifestExportPreview;
            EditorGUILayout.BeginHorizontal();
            DrawDashboardMetric("Manifest", preview.ManifestBuilt ? "Built" : "Pending", "Whether a runtime manifest preview could be generated.");
            DrawDashboardMetric("Blockers", preview.BlockingManifestExportCount.ToString(), "Required issues that block manifest export.");
            DrawDashboardMetric("Warnings", preview.WarningCount.ToString(), "Warnings to review before shipping.");
            DrawDashboardMetric("Authored", preview.AuthoredContentCount.ToString(), "Authored content records summarized for export.");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", GUILayout.Width(120f)))
            {
                ValidateExportPreview(preview);
            }

            using (new EditorGUI.DisabledScope(!preview.ReadyForManifestExport))
            {
                if (GUILayout.Button("Generate Runtime Manifest", GUILayout.Width(210f)))
                {
                    DimensionFrameworkAuthoringAssetActionResult result =
                        DimensionFrameworkAuthoringAssetUtility.CreateRuntimeManifestAsset(selectedTemplate, preview);
                    if (result != null && result.CreatedObject != null)
                    {
                        lastGeneratedManifestAsset = result.CreatedObject;
                    }

                    RunAssetAction(result);
                }
            }

            using (new EditorGUI.DisabledScope(lastGeneratedManifestAsset == null))
            {
                if (GUILayout.Button("Show Generated Assets", GUILayout.Width(170f)))
                {
                    Selection.activeObject = lastGeneratedManifestAsset;
                    EditorGUIUtility.PingObject(lastGeneratedManifestAsset);
                    lastEditorActionMessage = "Selected the last generated runtime manifest asset.";
                    lastEditorActionType = MessageType.Info;
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawContextualPreviewCanvas(viewModel.PreviewCanvas);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Readiness checklist", EditorStyles.boldLabel);
            DrawNamedValue("Manifest built", preview.ManifestBuilt ? "Yes" : "No");
            DrawNamedValue("Ready for export", preview.ReadyForManifestExport ? "Yes" : "No");
            DrawNamedValue("Runtime generation", preview.ReadyForRuntimeGeneration ? "Ready" : "Preparation pending");
            DrawNamedValue("Validation request", preview.ReadyForValidation ? "Ready" : "Blocked");
            DrawNamedValue("Apply request", preview.ReadyForApply ? "Ready" : "Blocked");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Runtime manifest", EditorStyles.boldLabel);
            DrawNamedValue("Dimensions", preview.DimensionCount.ToString());
            DrawNamedValue("Biomes", preview.BiomeCount.ToString());
            DrawNamedValue("Scenes", preview.SceneCount.ToString());
            DrawNamedValue("Resource nodes", preview.ResourceNodeCount.ToString());
            DrawNamedValue("Spawn rules", preview.SpawnRuleCount.ToString());
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Authoring content", EditorStyles.boldLabel);
            DrawNamedValue("Scenes", preview.AuthoredSceneContentCount.ToString());
            DrawNamedValue("Resources", preview.AuthoredResourceContentCount.ToString());
            DrawNamedValue("Spawns", preview.AuthoredSpawnableContentCount.ToString());
            DrawNamedValue("Ownership", preview.OwnershipBindingCount.ToString());
            DrawNamedValue("Asset references", preview.AssetReferenceCount.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawEditorCard(
                "Export gate",
                "Export stays unavailable only for true blockers: missing required identity, layout, biome, terrain, broken references, or out-of-bounds required content.");
            GUILayout.Space(8f);
            DrawDetailRows(viewModel.ActiveSectionDetail);
        }

        private void DrawDiagnosticsEditor()
        {
            DrawSectionIntro(
                "Diagnostics",
                "Review raw authoring warnings, blockers, prepared actions, visual asset checks, and setup-guide details. This page is intentionally technical.");

            DrawSummaryStrip();
            GUILayout.Space(8f);
            DrawActionPanel();
            GUILayout.Space(8f);
            DrawVisualAssetValidation();
            GUILayout.Space(8f);
            DrawSetupGuide();
            GUILayout.Space(8f);
            DrawDetailRows(viewModel.ActiveSectionDetail);
        }

        private void DrawSectionIntro(string title, string description)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }

            GUILayout.Space(8f);
        }

        private void DrawEditorCard(string title, string body)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(body))
            {
                EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void RunAssetAction(DimensionFrameworkAuthoringAssetActionResult result)
        {
            if (result == null)
            {
                lastEditorActionMessage = "The authoring action returned no result.";
                lastEditorActionType = MessageType.Warning;
            }
            else
            {
                lastEditorActionMessage = result.Message;
                lastEditorActionType = result.Executed ? MessageType.Info : MessageType.Warning;
                if (result.CreatedObject is DimensionRuntimeManifestAsset)
                {
                    lastGeneratedManifestAsset = result.CreatedObject;
                }
            }

            RebuildWorkspace();
            Repaint();
        }

        private void ValidateExportPreview(DimensionTemplateManifestExportPreview preview)
        {
            if (!preview.ManifestBuilt)
            {
                lastEditorActionMessage = "Manifest validation could not run because the manifest preview did not build: " + preview.Message;
                lastEditorActionType = MessageType.Warning;
            }
            else if (!preview.ReadyForManifestExport)
            {
                lastEditorActionMessage =
                    "Manifest validation found " +
                    preview.BlockingManifestExportCount.ToString() +
                    " export blocker(s): " +
                    preview.Message;
                lastEditorActionType = MessageType.Warning;
            }
            else
            {
                lastEditorActionMessage =
                    "Manifest validation passed. Runtime records: " +
                    preview.DimensionCount.ToString() +
                    " dimension(s), " +
                    preview.BiomeCount.ToString() +
                    " biome(s), " +
                    preview.ZoneCount.ToString() +
                    " zone(s).";
                lastEditorActionType = MessageType.Info;
            }

            RebuildWorkspace();
            Repaint();
        }

        private BiomeTemplateAsset GetSelectedBiomeOrFirst()
        {
            BiomeTemplateAsset[] biomes = GetBiomes();
            if (biomes.Length == 0)
            {
                return null;
            }

            if (selectedBiomeIndex < 0 || selectedBiomeIndex >= biomes.Length)
            {
                selectedBiomeIndex = 0;
            }

            return biomes[selectedBiomeIndex];
        }

        private struct SerializedFieldSpec
        {
            public string PropertyName;
            public string Label;
            public bool IncludeChildren;
            public bool ScopeToDimensionDataBlock;

            public SerializedFieldSpec(
                string propertyName,
                string label,
                bool includeChildren,
                bool scopeToDimensionDataBlock)
            {
                PropertyName = propertyName;
                Label = label;
                IncludeChildren = includeChildren;
                ScopeToDimensionDataBlock = scopeToDimensionDataBlock;
            }
        }

        private static SerializedFieldSpec Field(string propertyName)
        {
            return new SerializedFieldSpec(propertyName, null, true, false);
        }

        private static SerializedFieldSpec Field(string propertyName, string label)
        {
            return new SerializedFieldSpec(propertyName, label, true, false);
        }

        private static SerializedFieldSpec Field(string propertyName, string label, bool includeChildren)
        {
            return new SerializedFieldSpec(propertyName, label, includeChildren, false);
        }

        private static SerializedFieldSpec DimensionDataBlockField(string propertyName, string label)
        {
            return new SerializedFieldSpec(propertyName, label, true, true);
        }

        private bool DrawSerializedAsset(UnityEngine.Object target, string title, params SerializedFieldSpec[] fields)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (target != null && GUILayout.Button("Select", GUILayout.Width(72f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }

            EditorGUILayout.EndHorizontal();

            if (target == null)
            {
                EditorGUILayout.LabelField("Not assigned yet.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return false;
            }

            SerializedObject serializedObject = GetSerializedAssetBinding(target);
            serializedObject.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedFieldSpec field = fields[i];
                SerializedProperty property = GetSerializedAssetProperty(
                    serializedObject,
                    field.PropertyName);
                if (property == null)
                {
                    EditorGUILayout.LabelField(
                        field.Label ?? ObjectNames.NicifyVariableName(field.PropertyName),
                        "Missing serialized field: " + field.PropertyName,
                        EditorStyles.wordWrappedMiniLabel);
                    continue;
                }

                GUIContent label = string.IsNullOrEmpty(field.Label)
                    ? new GUIContent(ObjectNames.NicifyVariableName(field.PropertyName))
                    : new GUIContent(field.Label);
                if (field.ScopeToDimensionDataBlock)
                {
                    float propertyHeight = EditorGUI.GetPropertyHeight(
                        property,
                        label,
                        field.IncludeChildren);
                    Rect propertyRect = EditorGUILayout.GetControlRect(true, propertyHeight);
                    Event currentEvent = Event.current;
                    bool pointerInteraction =
                        currentEvent != null &&
                        (currentEvent.type == EventType.MouseDown ||
                         currentEvent.type == EventType.MouseUp) &&
                        currentEvent.button == 0 &&
                        propertyRect.Contains(currentEvent.mousePosition);
                    bool contextReady = true;
                    if (pointerInteraction &&
                        !TryScopeScriptableDataToSelectedDimension(out string contextError))
                    {
                        contextReady = false;
                        lastEditorActionMessage = contextError;
                        lastEditorActionType = MessageType.Warning;
                    }

                    bool previousGuiEnabled = GUI.enabled;
                    GUI.enabled = previousGuiEnabled && contextReady;
                    EditorGUI.PropertyField(
                        propertyRect,
                        property,
                        label,
                        field.IncludeChildren);
                    GUI.enabled = previousGuiEnabled;
                }
                else
                {
                    EditorGUILayout.PropertyField(property, label, field.IncludeChildren);
                }
            }

            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                RebuildWorkspace();
                Repaint();
            }

            EditorGUILayout.EndVertical();
            return changed;
        }

        private SerializedObject GetSerializedAssetBinding(Object target)
        {
            if (serializedAssetBinding == null || serializedAssetBindingTarget != target)
            {
                ReleaseSerializedAssetBinding();
                serializedAssetBindingTarget = target;
                serializedAssetBinding = new SerializedObject(target);
            }

            return serializedAssetBinding;
        }

        private SerializedProperty GetSerializedAssetProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            if (serializedObject == serializedAssetBinding &&
                serializedAssetBindingProperties.TryGetValue(
                    propertyName,
                    out SerializedProperty cachedProperty) &&
                cachedProperty != null)
            {
                return cachedProperty;
            }

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (serializedObject == serializedAssetBinding && property != null)
            {
                serializedAssetBindingProperties[propertyName] = property;
            }

            return property;
        }

        private void ReleaseSerializedAssetBinding()
        {
            if (serializedAssetBinding != null)
            {
                serializedAssetBinding.Dispose();
            }

            serializedAssetBinding = null;
            serializedAssetBindingTarget = null;
            serializedAssetBindingProperties.Clear();
        }

        private bool TryScopeScriptableDataToSelectedDimension(out string error)
        {
            return DimensionScriptableDataContextUtility.TryScopeToTemplate(
                selectedTemplate,
                out error);
        }

        private void DrawDashboardMetric(string title, string value, string tooltip)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(tooltip))
            {
                EditorGUILayout.LabelField(tooltip, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNamedValue(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "-" : value, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static string BuildAssetEditorTitle(
            string prefix,
            string displayName,
            string recordId,
            int index)
        {
            string resolvedPrefix = string.IsNullOrEmpty(prefix) ? "Asset" : prefix;
            string resolvedName = !string.IsNullOrEmpty(displayName)
                ? displayName
                : !string.IsNullOrEmpty(recordId)
                    ? recordId
                    : resolvedPrefix + " " + (index + 1).ToString();
            return resolvedPrefix + ": " + resolvedName;
        }

        private BiomeTemplateAsset DrawBiomeSelectorHeader()
        {
            BiomeTemplateAsset[] biomes = GetBiomes();
            if (biomes.Length == 0)
            {
                return null;
            }

            string[] names = new string[biomes.Length];
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                names[i] = biome == null || string.IsNullOrEmpty(biome.DisplayName)
                    ? "Biome " + (i + 1)
                    : biome.DisplayName;
            }

            selectedBiomeIndex = Mathf.Clamp(selectedBiomeIndex, 0, biomes.Length - 1);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Biome", EditorStyles.boldLabel, GUILayout.Width(80f));
            selectedBiomeIndex = EditorGUILayout.Popup(selectedBiomeIndex, names, GUILayout.MaxWidth(340f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                (selectedBiomeIndex + 1) + " / " + biomes.Length,
                EditorStyles.miniLabel,
                GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            return biomes[selectedBiomeIndex];
        }

        private BiomeTemplateAsset[] GetBiomes()
        {
            return selectedTemplate == null ? new BiomeTemplateAsset[0] : selectedTemplate.Biomes;
        }

        private int CountBiomes()
        {
            return GetBiomes().Length;
        }

        private int CountScenes()
        {
            int count = selectedTemplate == null ? 0 : CountAssets(selectedTemplate.GlobalScenes);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeScenes(biomes[i]);
            }

            return count;
        }

        private int CountBiomeScenes(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.ScenePool);
        }

        private int CountSceneContents()
        {
            int count = selectedTemplate == null ? 0 : CountSceneContents(selectedTemplate.GlobalScenes);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome != null)
                {
                    count += CountSceneContents(biome.ScenePool);
                }
            }

            return count;
        }

        private int CountResources()
        {
            int count = selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalResourceNodes) +
                    CountAssets(selectedTemplate.GlobalItems) +
                    CountAssets(selectedTemplate.GlobalRecipes) +
                    CountAssets(selectedTemplate.GlobalWorkbenches) +
                    CountAssets(selectedTemplate.GlobalLootTables);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeResources(biomes[i]);
            }

            return count;
        }

        private int CountBiomeResources(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.ResourceNodes);
        }

        private int CountSpawns()
        {
            int count = selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalAnimals) +
                    CountAssets(selectedTemplate.GlobalCritters) +
                    CountAssets(selectedTemplate.GlobalMobs) +
                    CountAssets(selectedTemplate.GlobalBosses) +
                    CountAssets(selectedTemplate.GlobalSpawnRules) +
                    CountSceneSpawnPoints(selectedTemplate.GlobalScenes);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeSpawns(biomes[i]);
            }

            return count;
        }

        private int CountBiomeSpawns(BiomeTemplateAsset biome)
        {
            return biome == null
                ? 0
                : CountAssets(biome.SpawnRules) +
                    CountSceneSpawnPoints(biome.ScenePool);
        }

        private int CountGenerationPasses()
        {
            int count = selectedTemplate == null ? 0 : CountAssets(selectedTemplate.GlobalGenerationPasses);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeGenerationPasses(biomes[i]);
            }

            return count;
        }

        private int CountBiomeGenerationPasses(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.GetGenerationPassesWithProfile());
        }

        private int CountGenerationTables()
        {
            int count = selectedTemplate == null ? 0 : CountAssets(selectedTemplate.GlobalGenerationTables);
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                count += CountBiomeGenerationTables(biomes[i]);
            }

            return count;
        }

        private int CountBiomeGenerationTables(BiomeTemplateAsset biome)
        {
            return biome == null ? 0 : CountAssets(biome.GetGenerationTablesWithProfile());
        }

        private int CountBiomeTerrainIds()
        {
            int count = 0;
            BiomeTemplateAsset[] biomes = GetBiomes();
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                count += biome.GetFloorObjectIdsWithPresets().Length;
                count += biome.GetWallObjectIdsWithPresets().Length;
                count += biome.GetOreObjectIdsWithPresets().Length;
                count += biome.GetWaterObjectIdsWithPresets().Length;
            }

            return count;
        }

        private static int CountAssets<T>(T[] values)
            where T : UnityEngine.Object
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSceneContents(SceneTemplateAsset[] scenes)
        {
            if (scenes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    count += scene.AuthoredContentCount;
                }
            }

            return count;
        }

        private static int CountSceneSpawnPoints(SceneTemplateAsset[] scenes)
        {
            if (scenes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    count += CountValues(scene.SpawnPoints);
                }
            }

            return count;
        }

        private static int CountValues<T>(T[] values)
            where T : class
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatInt2(Unity.Mathematics.int2 value)
        {
            return value.x + ", " + value.y;
        }

        private void DrawActionPanel()
        {
            DimensionTemplateCustomizerCommandPlan commandPlan =
                viewModel == null ? null : viewModel.CommandPlan;
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands =
                commandPlan == null ? null : commandPlan.Commands;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Authoring actions", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (commandPlan != null)
            {
                EditorGUILayout.LabelField(
                    "Ready " + commandPlan.ReadyCount +
                    "   Warn " + commandPlan.WarningCount +
                    "   Blocked " + commandPlan.BlockedCount +
                    "   Needs code " + commandPlan.ImplementationCount,
                    EditorStyles.miniLabel,
                    GUILayout.Width(270f));
            }

            EditorGUILayout.EndHorizontal();

            if (commands == null || commands.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No actions are available for this section yet.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            actionScroll = EditorGUILayout.BeginScrollView(actionScroll, GUILayout.MinHeight(92f), GUILayout.MaxHeight(140f));
            for (int i = 0; i < commands.Count; i++)
            {
                DrawCommandRow(commands[i]);
            }

            EditorGUILayout.EndScrollView();
            DrawSelectedCommandControls(commandPlan);
            EditorGUILayout.EndVertical();
        }

        private void DrawCommandRow(DimensionTemplateCustomizerCommand command)
        {
            if (command == null || command.Action.ActionId == string.Empty)
            {
                return;
            }

            bool selected = selectedActionId == command.CommandId;
            Color oldColor = GUI.color;
            GUI.color = selected ? new Color(0.74f, 0.9f, 1f, 1f) : CommandStateColor(command.State);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            string label = command.Action.Label;
            if (string.IsNullOrEmpty(label))
            {
                label = command.CommandId;
            }

            if (command.IsPrimary)
            {
                label = "* " + label;
            }

            if (GUILayout.Button(label, selected ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Width(190f)))
            {
                SelectCommand(command);
                PreviewSelectedCommand();
            }

            GUI.color = oldColor;
            EditorGUILayout.LabelField(command.State.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(120f));
            string summary = command.Preview == null ? command.BlockedReason : command.Preview.Summary;
            if (string.IsNullOrEmpty(summary))
            {
                summary = command.BlockedReason;
            }

            EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedCommandControls(DimensionTemplateCustomizerCommandPlan commandPlan)
        {
            DimensionTemplateCustomizerCommand selectedCommand =
                ResolveSelectedCommand(commandPlan);
            if (selectedCommand == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an action above to preview its safety requirements and expected result.",
                    MessageType.Info);
                return;
            }

            DimensionTemplateCustomizerActionDescriptor action = selectedCommand.Action;
            DimensionTemplateCustomizerActionPreview preview = selectedCommand.Preview;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(action.Label) ? selectedCommand.CommandId : action.Label,
                EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(action.Tooltip))
            {
                EditorGUILayout.LabelField(action.Tooltip, EditorStyles.wordWrappedMiniLabel);
            }

            if (preview != null)
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(preview.Summary, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(preview.ExpectedResult))
                {
                    EditorGUILayout.LabelField("Expected: " + preview.ExpectedResult, EditorStyles.wordWrappedMiniLabel);
                }

                if (!string.IsNullOrEmpty(preview.SafetyMessage))
                {
                    EditorGUILayout.HelpBox(preview.SafetyMessage, MessageType.Warning);
                }

                if (!string.IsNullOrEmpty(preview.InputHint))
                {
                    EditorGUILayout.LabelField(preview.InputHint, EditorStyles.miniBoldLabel);
                    commandInputValue = EditorGUILayout.TextField(commandInputValue);
                }
            }

            if (action.RequiresConfirmation)
            {
                commandConfirmed = EditorGUILayout.ToggleLeft(
                    "I understand this command's safety notes.",
                    commandConfirmed);
            }

            if (action.MutatesAssets)
            {
                allowAssetMutation = EditorGUILayout.ToggleLeft(
                    "Allow this command to mutate authoring assets when an executor is implemented.",
                    allowAssetMutation);
            }

            if (action.TouchesRuntimeState)
            {
                allowRuntimeMutation = EditorGUILayout.ToggleLeft(
                    "Allow this command to touch runtime state when an executor is implemented.",
                    allowRuntimeMutation);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview", GUILayout.Width(120f)))
            {
                PreviewSelectedCommand();
            }

            if (GUILayout.Button("Prepare", GUILayout.Width(120f)))
            {
                PrepareSelectedCommand();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            DrawPreparedCommand(preparedCommand);
            EditorGUILayout.EndVertical();
        }

        private DimensionTemplateCustomizerCommand ResolveSelectedCommand(
            DimensionTemplateCustomizerCommandPlan commandPlan)
        {
            IReadOnlyList<DimensionTemplateCustomizerCommand> commands =
                commandPlan == null ? null : commandPlan.Commands;
            if (commands == null || commands.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(selectedActionId))
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    if (commands[i].CommandId == selectedActionId)
                    {
                        return commands[i];
                    }
                }
            }

            return commandPlan.PrimaryCommand == null ? commands[0] : commandPlan.PrimaryCommand;
        }

        private void SelectCommand(DimensionTemplateCustomizerCommand command)
        {
            if (command == null)
            {
                selectedActionId = string.Empty;
                preparedCommand = null;
                return;
            }

            selectedActionId = command.CommandId;
            preparedCommand = null;
            commandInputValue = string.Empty;
            commandConfirmed = false;
            allowAssetMutation = false;
            allowRuntimeMutation = false;
        }

        private void PreviewSelectedCommand()
        {
            if (workspace == null)
            {
                preparedCommand = null;
                return;
            }

            DimensionTemplateCustomizerCommandRequest request =
                DimensionTemplateCustomizerCommandRequest.Preview(
                    activeSectionId,
                    selectedActionId,
                    DimensionTemplateCustomizerFocusRequest.ForAction(activeSectionId, selectedActionId));
            preparedCommand =
                DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, request);
        }

        private void PrepareSelectedCommand()
        {
            if (workspace == null)
            {
                preparedCommand = null;
                return;
            }

            DimensionTemplateCustomizerCommandRequest request =
                DimensionTemplateCustomizerCommandRequest.Prepare(
                    activeSectionId,
                    selectedActionId,
                    DimensionTemplateCustomizerFocusRequest.ForAction(activeSectionId, selectedActionId),
                    commandConfirmed,
                    allowAssetMutation,
                    allowRuntimeMutation,
                    commandInputValue);
            preparedCommand =
                DimensionTemplateCustomizerCommandRequestUtility.Prepare(workspace, request);
        }

        private void DrawPreparedCommand(DimensionTemplateCustomizerPreparedCommand prepared)
        {
            if (prepared == null)
            {
                return;
            }

            MessageType messageType = prepared.CanRun
                ? MessageType.Info
                : prepared.State == DimensionTemplateCustomizerPreparedCommandState.PreviewReady
                    ? MessageType.Info
                    : MessageType.Warning;
            EditorGUILayout.HelpBox(
                prepared.State + ": " + prepared.Message,
                messageType);

            string flags =
                "Can preview: " + prepared.CanPreview +
                "   Can run: " + prepared.CanRun +
                "   Assets: " + prepared.MutatesAssets +
                "   Runtime: " + prepared.TouchesRuntimeState;
            EditorGUILayout.LabelField(flags, EditorStyles.miniLabel);
        }

        private void DrawContextualPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            if (ShouldDrawPreviewCanvas(canvas))
            {
                DrawPreviewCanvas(canvas);
                GUILayout.Space(8f);
                return;
            }

            if (ShouldExplainHiddenPreview())
            {
                EditorGUILayout.HelpBox(
                    "No authored world preview exists yet. Use Layout to place biomes, then Generation to preview how the dimension will be assembled.",
                    MessageType.Info);
                GUILayout.Space(8f);
            }
        }

        private bool ShouldExplainHiddenPreview()
        {
            return activeSectionId == "layout" ||
                activeSectionId == "generation";
        }

        private static bool ShouldDrawPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            if (canvas.Layers == null)
            {
                return false;
            }

            for (int i = 0; i < canvas.Layers.Count; i++)
            {
                DimensionAuthoringCanvasLayer layer = canvas.Layers[i];
                if (!layer.VisibleByDefault ||
                    layer.Items == null ||
                    layer.Items.Count == 0 ||
                    !ShouldLayerCountAsPreviewContent(canvas.PlayableLocalBounds, layer))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool ShouldLayerCountAsPreviewContent(
            DimensionBounds playableBounds,
            DimensionAuthoringCanvasLayer layer)
        {
            if (layer.LayerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
            {
                return false;
            }

            if (layer.LayerKind != DimensionAuthoringPreviewLayerKind.BiomeRegion &&
                layer.LayerKind != DimensionAuthoringPreviewLayerKind.GenerationPassBounds)
            {
                return true;
            }

            if (layer.Items.Count > 1)
            {
                return true;
            }

            DimensionAuthoringCanvasItem item = layer.Items[0];
            return !BoundsEqual(playableBounds, item.LocalBounds);
        }

        private static bool BoundsEqual(DimensionBounds left, DimensionBounds right)
        {
            return left.Min.x == right.Min.x &&
                left.Min.y == right.Min.y &&
                left.MaxExclusive.x == right.MaxExclusive.x &&
                left.MaxExclusive.y == right.MaxExclusive.y;
        }

        private void DrawPreviewCanvas(DimensionAuthoringCanvasModel canvas)
        {
            Rect rect = GUILayoutUtility.GetRect(
                420f,
                CanvasMinHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.MinHeight(CanvasMinHeight));
            EditorGUI.DrawRect(rect, new Color(0.07f, 0.09f, 0.11f, 1f));
            DrawGrid(rect);

            if (canvas.Layers == null || canvas.Layers.Count == 0)
            {
                DrawCenteredLabel(rect, "No preview layers available.");
                return;
            }

            DimensionBounds bounds = canvas.PlayableLocalBounds;
            if (bounds.MaxExclusive.x <= bounds.Min.x || bounds.MaxExclusive.y <= bounds.Min.y)
            {
                DrawCenteredLabel(rect, "Preview bounds are empty.");
                return;
            }

            for (int i = 0; i < canvas.Layers.Count; i++)
            {
                DimensionAuthoringCanvasLayer layer = canvas.Layers[i];
                if (!layer.VisibleByDefault ||
                    layer.Items == null ||
                    layer.LayerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
                {
                    continue;
                }

                for (int j = 0; j < layer.Items.Count; j++)
                {
                    DrawCanvasItem(rect, bounds, layer, layer.Items[j]);
                }
            }
        }

        private void DrawGrid(Rect rect)
        {
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.06f);
            for (int i = 1; i < 4; i++)
            {
                float x = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
                float y = Mathf.Lerp(rect.yMin, rect.yMax, i / 4f);
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.color = oldColor;
        }

        private void DrawCanvasItem(
            Rect rect,
            DimensionBounds canvasBounds,
            DimensionAuthoringCanvasLayer layer,
            DimensionAuthoringCanvasItem item)
        {
            DimensionBounds itemBounds = item.LocalBounds;
            Rect itemRect = ToCanvasRect(rect, canvasBounds, itemBounds);
            if (itemRect.width < 1f || itemRect.height < 1f)
            {
                return;
            }

            if (layer.FillByDefault)
            {
                EditorGUI.DrawRect(itemRect, ToColor(item.FillColorRgba));
            }

            if (layer.OutlineByDefault)
            {
                DrawRectOutline(itemRect, ToColor(item.OutlineColorRgba), item.HasConflict ? 2f : 1f);
            }

            if (!string.IsNullOrEmpty(item.DisplayName) && itemRect.width > 48f && itemRect.height > 18f)
            {
                GUI.Label(itemRect, item.DisplayName, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private Rect ToCanvasRect(
            Rect rect,
            DimensionBounds canvasBounds,
            DimensionBounds itemBounds)
        {
            float width = canvasBounds.MaxExclusive.x - canvasBounds.Min.x;
            float height = canvasBounds.MaxExclusive.y - canvasBounds.Min.y;
            if (width <= 0f || height <= 0f)
            {
                return Rect.zero;
            }

            float xMin = rect.xMin + ((itemBounds.Min.x - canvasBounds.Min.x) / width) * rect.width;
            float xMax = rect.xMin + ((itemBounds.MaxExclusive.x - canvasBounds.Min.x) / width) * rect.width;
            float yMin = rect.yMax - ((itemBounds.MaxExclusive.y - canvasBounds.Min.y) / height) * rect.height;
            float yMax = rect.yMax - ((itemBounds.Min.y - canvasBounds.Min.y) / height) * rect.height;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void DrawDetailRows(DimensionTemplateCustomizerSectionDetail detail)
        {
            EditorGUILayout.LabelField("Guidance and diagnostics", EditorStyles.boldLabel);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MinHeight(170f));
            if (detail == null || detail.Rows == null || detail.Rows.Count == 0)
            {
                EditorGUILayout.LabelField("No rows for this section.");
            }
            else
            {
                for (int i = 0; i < detail.Rows.Count; i++)
                {
                    DrawDetailRow(detail.Rows[i]);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetailRow(DimensionTemplateCustomizerDetailRow row)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUIStyle titleStyle = row.Severity == DimensionAuthoringSeverity.Error
                ? EditorStyles.boldLabel
                : EditorStyles.label;
            EditorGUILayout.LabelField(row.Title, titleStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(row.Kind.ToString(), EditorStyles.miniLabel, GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(row.Message))
            {
                EditorGUILayout.LabelField(row.Message, EditorStyles.wordWrappedMiniLabel);
            }

            if (row.HasLocalBounds)
            {
                EditorGUILayout.LabelField(FormatBounds(row.LocalBounds), EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVisualAssetValidation()
        {
            DimensionVisualAssetValidationReport report =
                viewModel.SessionReport == null ? null : viewModel.SessionReport.VisualAssetValidation;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Visual asset preflight", EditorStyles.boldLabel);
            if (report == null)
            {
                EditorGUILayout.HelpBox("No visual asset validation report is available.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(
                "Ready: " + report.ReadyCount +
                "   Advisory: " + report.AdvisoryCount +
                "   Blocked: " + report.BlockedCount +
                "   Vanilla refs: " + report.VanillaReferenceCount +
                "   Mod assets: " + report.ModAssetCount +
                "   Packages: " + report.PackageAssetCount);
            EditorGUILayout.HelpBox(report.Message, report.BlockedCount > 0 ? MessageType.Error : MessageType.Info);

            notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.MinHeight(120f));
            IReadOnlyList<DimensionVisualAssetValidationEntry> entries = report.Entries;
            if (entries == null || entries.Count == 0)
            {
                EditorGUILayout.LabelField("No visual asset references found in the manifest.");
            }
            else
            {
                int maxRows = Mathf.Min(entries.Count, 16);
                for (int i = 0; i < maxRows; i++)
                {
                    DrawVisualAssetEntry(entries[i]);
                }

                if (entries.Count > maxRows)
                {
                    EditorGUILayout.LabelField("Showing first " + maxRows + " of " + entries.Count + " references.");
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawVisualAssetEntry(DimensionVisualAssetValidationEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DimensionAssetReferenceDefinition reference = entry.Reference;
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(reference.DisplayName) ? reference.AssetId : reference.DisplayName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                entry.State + " | " + entry.SourceKind + " | " + reference.Kind + " | " + reference.ResourceKey,
                EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(entry.Guidance))
            {
                EditorGUILayout.LabelField(entry.Guidance, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private void DrawCenteredLabel(Rect rect, string text)
        {
            GUI.Label(rect, text, EditorStyles.centeredGreyMiniLabel);
        }

        private void RequestSectionChange(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId) || sectionId == activeSectionId)
            {
                return;
            }

            RequestPortalTransition(() =>
            {
                activeSectionId = sectionId;
                RebuildViewModel();
                Repaint();
            });
        }

        private void RequestTemplateChange(DimensionTemplateAsset template)
        {
            if (template == selectedTemplate)
            {
                return;
            }

            RequestPortalTransition(() =>
            {
                selectedTemplate = template;
                activeSectionId = "overview";
                ResetPortalProfileInitializationCache();
                RebuildWorkspace();
                Repaint();
            });
        }

        private void RequestPortalTransition(System.Action transition)
        {
            if (transition == null)
            {
                return;
            }

            bool requiresGuard =
                activeSectionId == "portals" &&
                portalAppearanceStudio != null &&
                portalAppearanceStudio.HasPendingChanges;
            if (!requiresGuard)
            {
                transition();
                return;
            }

            if (portalTransitionQueued)
            {
                return;
            }

            pendingPortalTransition = transition;
            portalTransitionQueued = true;
            EditorApplication.delayCall += ProcessPendingPortalTransition;
        }

        private void ProcessPendingPortalTransition()
        {
            EditorApplication.delayCall -= ProcessPendingPortalTransition;
            System.Action transition = pendingPortalTransition;
            pendingPortalTransition = null;
            portalTransitionQueued = false;
            if (this == null || transition == null)
            {
                return;
            }

            if (portalAppearanceStudio == null ||
                !portalAppearanceStudio.HasPendingChanges)
            {
                transition();
                return;
            }

            bool applyAndLeave = EditorUtility.DisplayDialog(
                "Unsaved Portal Studio changes",
                "Leaving '" + portalAppearanceStudio.EditingProfileDisplayName +
                "' now will revert its unsaved changes.",
                "Apply and leave",
                "Leave without applying");
            if (applyAndLeave)
            {
                if (!portalAppearanceStudio.SavePendingChanges(
                        out bool runtimeUpdateRequired,
                        out string saveMessage))
                {
                    SetLastEditorAction(saveMessage, MessageType.Error);
                    Repaint();
                    return;
                }

                SetLastEditorAction(saveMessage, MessageType.Info);
                if (runtimeUpdateRequired)
                {
                    DimensionPortalVisualProfileAsset boundProfile =
                        selectedTemplate == null
                            ? null
                            : selectedTemplate.PortalVisualProfile;
                    bool synchronized = TryApplyPortalVisualChanges(out _);
                    portalAppearanceStudio.NotifyRuntimeSyncResult(
                        boundProfile,
                        synchronized);
                    if (!synchronized)
                    {
                        return;
                    }
                }
            }
            else
            {
                if (!portalAppearanceStudio.DiscardPendingChanges(
                        out string discardMessage))
                {
                    SetLastEditorAction(discardMessage, MessageType.Error);
                    Repaint();
                    return;
                }

                if (!string.IsNullOrEmpty(discardMessage))
                {
                    SetLastEditorAction(discardMessage, MessageType.Info);
                }
            }

            transition();
        }

        private void SetLastEditorAction(string message, MessageType type)
        {
            lastEditorActionMessage = message ?? string.Empty;
            lastEditorActionType = type;
        }

        private void RebuildWorkspace()
        {
            workspace = selectedTemplate == null
                ? null
                : DimensionTemplateAuthoringWorkspaceUtility.BuildWorkspaceFromTemplate(
                    selectedTemplate,
                    DimensionApiModFolderUtility.ResolvePreferredDimensionAssetFolder());
            RebuildViewModel();
        }

        private void RebuildViewModel()
        {
            viewModel = workspace == null
                ? null
                : workspace.GetCustomizerViewModel(activeSectionId);
            if (viewModel != null && viewModel.Navigation != null)
            {
                activeSectionId = string.IsNullOrEmpty(viewModel.ActiveSectionId)
                    ? viewModel.Navigation.ActiveSectionId
                    : viewModel.ActiveSectionId;
            }
        }

        private bool TryUseProjectSelection()
        {
            DimensionTemplateAsset selection = Selection.activeObject as DimensionTemplateAsset;
            if (selection == null || selection == selectedTemplate)
            {
                return false;
            }

            RequestTemplateChange(selection);
            return true;
        }

        private static string SectionButtonText(DimensionTemplateCustomizerSectionItem section)
        {
            string prefix = section.State == DimensionAuthoringReadinessState.Ready
                ? "✓ "
                : section.State == DimensionAuthoringReadinessState.Blocked
                    ? "! "
                    : section.State == DimensionAuthoringReadinessState.Partial
                        ? "~ "
                        : "- ";
            bool showContentCount =
                section.ContentCount > 0 &&
                (section.Kind == DimensionTemplateCustomizerSectionKind.Biomes ||
                    section.Kind == DimensionTemplateCustomizerSectionKind.Scenes ||
                    section.Kind == DimensionTemplateCustomizerSectionKind.Resources ||
                    section.Kind == DimensionTemplateCustomizerSectionKind.Spawns);

            return showContentCount
                ? section.DisplayName + " (" + section.ContentCount + ")"
                : section.DisplayName;
        }

        private static Color StateColor(DimensionAuthoringReadinessState state)
        {
            if (state == DimensionAuthoringReadinessState.Ready)
            {
                return new Color(0.75f, 1f, 0.75f, 1f);
            }

            if (state == DimensionAuthoringReadinessState.Partial)
            {
                return new Color(1f, 0.92f, 0.62f, 1f);
            }

            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                return new Color(1f, 0.65f, 0.65f, 1f);
            }

            return new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private static Color CommandStateColor(DimensionTemplateCustomizerCommandState state)
        {
            if (state == DimensionTemplateCustomizerCommandState.Ready)
            {
                return new Color(0.78f, 1f, 0.78f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.Warning ||
                state == DimensionTemplateCustomizerCommandState.NeedsConfirmation)
            {
                return new Color(1f, 0.92f, 0.62f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.NeedsImplementation)
            {
                return new Color(0.78f, 0.86f, 1f, 1f);
            }

            if (state == DimensionTemplateCustomizerCommandState.Blocked ||
                state == DimensionTemplateCustomizerCommandState.Disabled)
            {
                return new Color(1f, 0.65f, 0.65f, 1f);
            }

            return new Color(0.78f, 0.78f, 0.78f, 1f);
        }

        private static Color ToColor(uint rgba)
        {
            float r = ((rgba >> 24) & 0xFF) / 255f;
            float g = ((rgba >> 16) & 0xFF) / 255f;
            float b = ((rgba >> 8) & 0xFF) / 255f;
            float a = (rgba & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        private static string FormatBounds(DimensionBounds bounds)
        {
            return "Bounds: (" + bounds.Min.x + ", " + bounds.Min.y + ") to (" +
                bounds.MaxExclusive.x + ", " + bounds.MaxExclusive.y + ")";
        }
    }
}
