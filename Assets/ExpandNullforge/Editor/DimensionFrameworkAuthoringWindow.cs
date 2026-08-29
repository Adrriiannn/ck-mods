using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    public sealed partial class DimensionFrameworkAuthoringWindow : EditorWindow
    {
        private const string WindowTitle = "Dimensions API";
        private const float SidebarWidth = 220f;
        private const float CanvasMinHeight = 260f;
        private DimensionTemplateAsset selectedTemplate;
        private readonly System.Collections.Generic.HashSet<int> portalIconsScannedTemplates =
            new System.Collections.Generic.HashSet<int>();
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
        private string selectedGuideModuleId = string.Empty;
        private string selectedActionId;
        private string commandInputValue = string.Empty;
        private bool commandConfirmed;
        private bool allowAssetMutation;
        private bool allowRuntimeMutation;
        private DimensionTemplateCustomizerPreparedCommand preparedCommand;
        private bool autoUseProjectSelection = true;
        private string lastEditorActionMessageValue = string.Empty;
        private MessageType lastEditorActionTypeValue = MessageType.Info;
        private string displayedEditorActionMessage = string.Empty;
        private MessageType displayedEditorActionType = MessageType.Info;

        /// <summary>
        /// What the last thing the creator did actually did, and how it went.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWENTY-ONE PLACES WRITE THIS AND FOR A LONG TIME NOTHING READ IT. The only reader was
        /// the original panel's header, and that panel became unreachable, so "Created X",
        /// "validation found 3 blockers" and "the portal's look could not be pushed into the
        /// prefab" were all worked out and then thrown away — on the creator's first click.
        /// <see cref="RefreshActionFeedback"/> puts them back on screen, in the shell.
        /// </para>
        /// <para>
        /// A PROPERTY RATHER THAN A FIELD, keeping the field's own name, so that the twenty-one
        /// places that publish a message stay exactly as they were and there is still only one
        /// place that knows the strip exists. Renaming them would have been twenty-one chances to
        /// miss one, and a missed one is a message that silently goes nowhere again.
        /// </para>
        /// </remarks>
        private string lastEditorActionMessage
        {
            get { return lastEditorActionMessageValue; }
            set
            {
                lastEditorActionMessageValue = value;
                RefreshActionFeedback();
            }
        }

        /// <summary>Whether that message is news, a warning, or a failure.</summary>
        private MessageType lastEditorActionType
        {
            get { return lastEditorActionTypeValue; }
            set
            {
                lastEditorActionTypeValue = value;
                RefreshActionFeedback();
            }
        }
        private Object lastGeneratedManifestAsset;
        private bool portalVisualProfileSetupQueued;
        private DimensionPortalAppearanceStudio portalAppearanceStudio;
        private readonly DimensionTilesetStudio tilesetStudio = new DimensionTilesetStudio();
        private readonly DimensionLayoutStudio layoutStudio = new DimensionLayoutStudio();
        private DimensionTemplateAsset initializedPortalTemplate;
        private DimensionPortalVisualProfileAsset initializedPortalProfile;
        private bool itemPortalVisualProfileSetupQueued;
        private DimensionPortalAppearanceStudio itemPortalAppearanceStudio;
        private DimensionTemplateAsset initializedItemPortalTemplate;
        private DimensionPortalVisualProfileAsset initializedItemPortalProfile;
        private int portalStudioTab;
        private int selectedTilesetIndex;
        private Object serializedAssetBindingTarget;
        /// <summary>Foldout state for the uncurated fields each panel draws below its own list.</summary>
        private readonly Dictionary<string, bool> uncuratedFieldFoldouts =
            new Dictionary<string, bool>();

        /// <summary>Which borrowed attack each creature has highlighted in the picker.</summary>
        private readonly Dictionary<string, int> borrowedAttackChoices =
            new Dictionary<string, int>();

        private SerializedObject serializedAssetBinding;
        private readonly Dictionary<string, SerializedProperty> serializedAssetBindingProperties =
            new Dictionary<string, SerializedProperty>();
        private System.Action pendingPortalTransition;
        private bool portalTransitionQueued;

        [MenuItem("Dimensions API/Dimension Dashboard")]
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
            tilesetStudio.Cleanup();
            if (portalAppearanceStudio != null)
            {
                portalAppearanceStudio.Dispose();
                portalAppearanceStudio = null;
            }

            if (itemPortalAppearanceStudio != null)
            {
                itemPortalAppearanceStudio.Dispose();
                itemPortalAppearanceStudio = null;
            }
        }

        private void Update()
        {
            bool portalSectionActive = activeSectionId == "portals";
            DimensionPortalAppearanceStudio activeStudio = portalStudioTab == 1
                ? itemPortalAppearanceStudio
                : portalAppearanceStudio;
            bool previewRepaintsContinuously =
                portalSectionActive &&
                activeStudio != null &&
                activeStudio.IsPlaying &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating;
            // The Tileset Studio's scene preview needs mouse-move events too, so its add/remove
            // placement outline can follow the cursor.
            bool tilesetSectionActive = activeSectionId == "tilesets";
            bool needsMouseMoveEvents = (portalSectionActive && !previewRepaintsContinuously) || tilesetSectionActive;
            if (wantsMouseMove != needsMouseMoveEvents)
            {
                wantsMouseMove = needsMouseMoveEvents;
            }

            bool repaint = false;
            if (portalAppearanceStudio != null &&
                portalAppearanceStudio.Tick(portalSectionActive && portalStudioTab == 0))
            {
                repaint = true;
            }

            if (itemPortalAppearanceStudio != null &&
                itemPortalAppearanceStudio.Tick(portalSectionActive && portalStudioTab == 1))
            {
                repaint = true;
            }

            if (repaint)
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

        /// <summary>
        /// Draws the active stage's body: the original IMGUI panel, hosted by the UI Toolkit
        /// frame in <c>DimensionFrameworkAuthoringWindow.Shell.cs</c>. The frame owns the
        /// wordmark bar, the Home screen, the rail and the stage heading; everything below the
        /// heading is still the panel a creator uses today, unchanged.
        /// </summary>
        private void DrawLegacyStageBody()
        {
            // The panels below are IMGUI and draw Unity's own grey plates. Painting the cavern
            // ground first and tinting their backgrounds pulls them onto the same surface as the
            // rebuilt pages without touching a single control.
            DimensionsApiImguiTheme.PaintBackground(
                new Rect(0f, 0f, position.width, position.height));
            Color previousBackground;
            Color previousContent;
            DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
            try
            {
                DrawLegacyStageBodyInner();
            }
            finally
            {
                DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
            }
        }

        private void DrawLegacyStageBodyInner()
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

            // Redraw as the cursor moves over the Tileset Studio so its placement outline tracks it.
            if (currentEvent != null && currentEvent.type == EventType.MouseMove && activeSectionId == "tilesets")
            {
                Repaint();
            }

            DrawHeader();

            if (selectedTemplate == null || workspace == null || viewModel == null)
            {
                DrawTemplatePicker();
                DrawMissingTemplateState();
                return;
            }

            rootScroll = EditorGUILayout.BeginScrollView(rootScroll, GUILayout.ExpandHeight(true));
            DrawCurrentSection();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Selects a dimension on behalf of the Home screen, using the same path the old
        /// template picker used so the workspace, view model and portal caches all rebuild.
        /// </summary>
        /// <summary>Builds the runtime manifest, the same action the old Export page ran.</summary>
        /// <summary>Draws just the Layout Studio, for the rebuilt Map page to host.</summary>
        private void DrawLayoutStudioForShell()
        {
            if (selectedTemplate == null || selectedTemplate.LayoutTemplate == null)
            {
                return;
            }

            DrawLayoutStudio();
        }

        /// <summary>
        /// Which Portal Studio the rebuilt page drives. The placed portal and the instant item
        /// portal are two studios sharing one page, chosen by the tab the creator picked.
        /// </summary>
        private DimensionPortalAppearanceStudio ResolvePortalStudioForShell()
        {
            if (portalStudioTab == 1)
            {
                // The item-portal studio used to be created by the legacy instant-portal panel,
                // which nothing reaches any more — without this, switching the page to the item
                // portal resolved a null studio and showed an empty room.
                if (itemPortalAppearanceStudio == null)
                {
                    itemPortalAppearanceStudio = new DimensionPortalAppearanceStudio();
                    itemPortalAppearanceStudio.ConfigureInstantPortalMode();
                }

                return itemPortalAppearanceStudio;
            }

            return portalAppearanceStudio;
        }

        /// <summary>The portal profile the page should edit for the active version tab.</summary>
        private DimensionPortalVisualProfileAsset ResolvePortalProfileForShell()
        {
            if (selectedTemplate == null)
            {
                return null;
            }

            return portalStudioTab == 1
                ? selectedTemplate.ItemPortalVisualProfile
                : selectedTemplate.PortalVisualProfile;
        }

        /// <summary>
        /// Prepares the active version's profile the way the legacy panels used to: a missing
        /// profile is created and assigned on the next editor tick, an existing one gets its
        /// artwork initialized once. Without this, a fresh dimension's portal page showed its
        /// empty state forever, waiting for a setup step that no longer had a caller.
        /// </summary>
        private void EnsurePortalProfileReadyForShell()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            if (portalStudioTab == 1)
            {
                DimensionPortalVisualProfileAsset itemProfile =
                    selectedTemplate.ItemPortalVisualProfile;
                if (itemProfile == null)
                {
                    QueueItemPortalVisualProfileSetup();
                }
                else
                {
                    EnsureItemPortalProfileInitializedOnce(itemProfile);
                }

                return;
            }

            DimensionPortalVisualProfileAsset profile = selectedTemplate.PortalVisualProfile;
            if (profile == null)
            {
                QueuePortalVisualProfileSetup();
            }
            else
            {
                EnsurePortalProfileInitializedOnce(profile);
            }
        }

        /// <summary>
        /// Handles what a Portal Studio pass reported, exactly as the legacy panels did: a
        /// message to show, a template edit to rebuild from, a profile to start building with,
        /// or a request to push the saved look into the generated prefab.
        /// </summary>
        /// <remarks>
        /// The use and sync requests are the half that was silently dropped when the legacy
        /// panels died: Save and the profile picker queue their work inside the studio, the
        /// studio reports it out through this result, and only the window can finish the job —
        /// so without these branches the button clicked, the message said saved, and the built
        /// portal never changed.
        /// </remarks>
        private void HandlePortalStudioResultFromShell(
            DimensionPortalAppearanceStudio.DrawResult result)
        {
            if (!string.IsNullOrEmpty(result.Message))
            {
                lastEditorActionMessage = result.Message;
                lastEditorActionType = result.MessageType;
            }

            if (result.TemplateChanged)
            {
                RebuildWorkspace();
                Repaint();
            }

            bool instant = portalStudioTab == 1;
            DimensionPortalAppearanceStudio studio = instant
                ? itemPortalAppearanceStudio
                : portalAppearanceStudio;
            if (studio == null)
            {
                return;
            }

            if (result.UseProfileRequested != null)
            {
                DimensionPortalVisualProfileAsset requestedProfile = result.UseProfileRequested;
                bool switched = result.CanApply &&
                    (instant
                        ? TryUseItemPortalVisualProfile(requestedProfile, out _)
                        : TryUsePortalVisualProfile(requestedProfile, out _));
                studio.NotifyRuntimeSyncResult(requestedProfile, switched);
            }
            else if (result.RuntimeSyncRequested)
            {
                DimensionPortalVisualProfileAsset boundProfile = ResolvePortalProfileForShell();
                bool synchronized = result.CanApply && TryApplyPortalVisualChanges(out _);
                studio.NotifyRuntimeSyncResult(boundProfile, synchronized);
            }
        }

        // ------------------------------------------------- portal versions, for the page ---

        private int GetPortalVersionTabForShell()
        {
            return portalStudioTab;
        }

        private void SetPortalVersionTabFromShell(int tab)
        {
            int next = tab == 1 ? 1 : 0;
            if (portalStudioTab == next)
            {
                return;
            }

            portalStudioTab = next;
            RefreshStageBody();
            Repaint();
        }

        private bool HasPortalVersionForShell(bool instant)
        {
            return HasPortalVersion(instant);
        }

        private bool IsPortalVersionEnabledForShell(bool instant)
        {
            return IsPortalVersionEnabled(instant);
        }

        private void TogglePortalVersionEnabledFromShell(bool instant)
        {
            TogglePortalVersionEnabled(instant);
            RebuildWorkspace();
            Repaint();
        }

        /// <summary>Shows a block's generated item on the stage that owns items.</summary>
        private void FocusBlockItemFromShell(DimensionItemAsset item)
        {
            if (item == null)
            {
                return;
            }

            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
            GoToStage("resources");
        }

        private void BuildDimensionFromShell()
        {
            if (selectedTemplate == null || viewModel == null || viewModel.SessionReport == null)
            {
                return;
            }

            // The whole content pipeline runs FIRST: items, tileset block items, portal items,
            // creatures, plants, workbenches, chests, objects, vehicles, projectiles, critters,
            // explosions, fog, and the drop plan. It used to be a separate button on a page that
            // no longer exists, which meant "Build the Dimension" shipped a dimension with zero
            // content prefabs — every generator green and unreachable. The manifest and bootstrap
            // are built after, so they see every prefab this pass produced.
            GenerateItemPrefabs(selectedTemplate.GlobalItems);

            DimensionTemplateManifestExportPreview preview =
                viewModel.SessionReport.ManifestExportPreview;
            DimensionFrameworkAuthoringAssetActionResult result =
                DimensionFrameworkAuthoringAssetUtility.CreateRuntimeManifestAsset(
                    selectedTemplate,
                    preview);
            if (result != null && result.CreatedObject != null)
            {
                lastGeneratedManifestAsset = result.CreatedObject;
            }

            RunAssetAction(result);
        }

        private void ShowGeneratedAssetsFromShell()
        {
            if (lastGeneratedManifestAsset == null)
            {
                return;
            }

            Selection.activeObject = lastGeneratedManifestAsset;
            EditorGUIUtility.PingObject(lastGeneratedManifestAsset);
            lastEditorActionMessage = "Selected the last generated runtime manifest asset.";
            lastEditorActionType = MessageType.Info;
        }

        private void SelectTemplateFromShell(DimensionTemplateAsset template)
        {
            if (template == selectedTemplate)
            {
                return;
            }

            selectedTemplate = template;
            autoUseProjectSelection = false;
            RebuildWorkspace();
        }

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

        // The dashboard sections are colour-coded BLUE so a creator can tell "I'm in the section
        // list" at a peripheral glance — distinct from the orange Tileset Studio and (soon) the
        // blue-accented Portal Studio interior.
        private static readonly Color NavBlue = new Color(0.34f, 0.62f, 0.92f);
        private static readonly Color NavBlueSoft = new Color(0.34f, 0.62f, 0.92f, 0.14f);
        private static readonly Color NavIcon = new Color(0.5f, 0.74f, 1f);
        private GUIStyle navTitleStyle;
        private GUIStyle navTitleSelStyle;
        private GUIStyle navDescStyle;
        private GUIStyle portalHeaderStyle;
        private GUIStyle portalTabNameStyle;
        private GUIStyle portalTabNameSelStyle;
        private GUIStyle portalTickStyle;

        private void DrawNavigationSidebar()
        {
            EnsureNavStyles();
            // Width comes from the shared left column, so this vertical just fills it.
            EditorGUILayout.BeginVertical();
            GUILayout.Label("SECTIONS", EditorStyles.miniBoldLabel);
            GUILayout.Space(2f);

            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                viewModel.Navigation == null ? null : viewModel.Navigation.Sections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    DrawNavItem(sections[i]);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNavItem(DimensionTemplateCustomizerSectionItem section)
        {
            bool selected = section.SectionId == activeSectionId;
            Rect r = EditorGUILayout.GetControlRect(false, 40f, GUILayout.Width(SidebarWidth - 18f));

            if (selected)
            {
                EditorGUI.DrawRect(r, NavBlueSoft);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), NavBlue);
            }
            else if (r.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.04f));
            }

            Rect icon = new Rect(r.x + 12f, r.y + 10f, 20f, 20f);
            DrawSectionIcon(icon, section.SectionId, selected ? new Color(0.66f, 0.84f, 1f) : NavIcon);

            float textX = icon.xMax + 12f;
            float textW = r.xMax - textX - 8f;
            GUI.Label(new Rect(textX, r.y + 5f, textW, 16f), section.DisplayName,
                selected ? navTitleSelStyle : navTitleStyle);
            GUI.Label(new Rect(textX, r.y + 21f, textW, 13f), SectionDescription(section.SectionId), navDescStyle);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                RequestSectionChange(section.SectionId);
            }
        }

        // Section icons: a real PNG dropped in Assets/ExpandNullforge/Editor/Icons/<id>.png wins
        // (drawn at full colour); otherwise a smooth code-generated icon is drawn, tinted to state.
        private static Dictionary<string, Texture2D> sectionIconPngCache;

        private static void DrawSectionIcon(Rect box, string sectionId, Color tint)
        {
            Texture2D png = SectionIconPng(sectionId);
            if (png != null)
            {
                GUI.DrawTexture(box, png, ScaleMode.ScaleToFit);
                return;
            }

            Texture2D generated = DimensionSectionIcons.Generated(sectionId);
            if (generated != null)
            {
                GUI.DrawTexture(box, generated, ScaleMode.ScaleToFit, true, 0f, tint, Vector4.zero, Vector4.zero);
            }
        }

        private static Texture2D SectionIconPng(string sectionId)
        {
            if (sectionIconPngCache == null)
            {
                sectionIconPngCache = new Dictionary<string, Texture2D>();
            }

            if (!sectionIconPngCache.TryGetValue(sectionId, out Texture2D tex))
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ExpandNullforge/Editor/Icons/" + sectionId + ".png");
                sectionIconPngCache[sectionId] = tex;
            }

            return tex;
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

        private static string SectionDescription(string sectionId)
        {
            switch (sectionId)
            {
                case "overview": return "Everything at a glance";
                case "dimension": return "Identity & coordinates";
                case "portals": return "Design your portals";
                case "tilesets": return "Custom blocks & tiles";
                case "layout": return "Shape of the world";
                case "biomes": return "Zones & palettes";
                case "terrain": return "Ground, walls, liquids";
                case "generation": return "How the world builds";
                case "scenes": return "Handcrafted structures";
                case "resources": return "Items, recipes, loot";
                case "spawns": return "Creatures & mobs";
                case "export": return "Build the manifest";
                case "diagnostics": return "Readiness & issues";
                default: return string.Empty;
            }
        }

        private void EnsureNavStyles()
        {
            if (navTitleStyle != null)
            {
                return;
            }

            navTitleStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 13, alignment = TextAnchor.MiddleLeft };
            navTitleSelStyle = new GUIStyle(navTitleStyle);
            navTitleSelStyle.normal.textColor = new Color(0.72f, 0.85f, 1f);
            navDescStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            navDescStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
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
            else if (activeSectionId == "tilesets")
            {
                DrawTilesetsEditor();
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
                new GUIContent(
                    "● " + DimensionCapabilityRegistry.Describe(capability.Maturity),
                    capability.Note),
                EditorStyles.miniBoldLabel);
            GUI.color = previousColor;
            GUILayout.Space(4f);
        }

        /// <summary>
        /// One map, shared with the live shell. Two copies of "which capability is this section"
        /// is two answers waiting to disagree, and the one a creator reads would be whichever
        /// panel they happened to open.
        /// </summary>
        private static string MaturityCapabilityForSection(string sectionId)
        {
            return DimensionStageMaturity.CapabilityForSection(sectionId);
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
                Field("dimensionType", "Dimension type"),
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
            DrawNamedValue("Dimension type", definition.Type.ToString());
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

        private void ComputePortalVersion(bool instant, out bool has, out bool enabled, out bool otherEnabled)
        {
            has = false;
            enabled = false;
            otherEnabled = false;
            DimensionPortalAccessRuleAsset[] rules =
                selectedTemplate == null ? null : selectedTemplate.PortalAccessRules;
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                bool isInstant = DimensionPortalVersions.IsInstantaneousItem(rule.AccessKind);
                bool isPlaced = DimensionPortalVersions.IsUserAccessible(rule.AccessKind);
                if (instant ? isInstant : isPlaced)
                {
                    has = true;
                    enabled |= rule.Enabled;
                }
                else if (instant ? isPlaced : isInstant)
                {
                    otherEnabled |= rule.Enabled;
                }
            }
        }

        private bool HasPortalVersion(bool instant)
        {
            ComputePortalVersion(instant, out bool has, out _, out _);
            return has;
        }

        private bool IsPortalVersionEnabled(bool instant)
        {
            ComputePortalVersion(instant, out _, out bool enabled, out _);
            return enabled;
        }

        /// <summary>
        /// Toggles whether a portal version exists in-game. Both versions can be enabled together;
        /// the last enabled one is locked on so a dimension never loses its only way in. Applies on
        /// the next Generate Runtime Manifest.
        /// </summary>
        private void TogglePortalVersionEnabled(bool instant)
        {
            ComputePortalVersion(instant, out bool has, out bool enabled, out bool otherEnabled);
            if (!has || (enabled && !otherEnabled))
            {
                return;
            }

            bool next = !enabled;
            DimensionPortalAccessRuleAsset[] rules = selectedTemplate.PortalAccessRules;
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                bool isTabRule = instant
                    ? DimensionPortalVersions.IsInstantaneousItem(rule.AccessKind)
                    : DimensionPortalVersions.IsUserAccessible(rule.AccessKind);
                if (isTabRule)
                {
                    Undo.RecordObject(rule, "Portal Version Enabled");
                    rule.SetEnabled(next);
                    EditorUtility.SetDirty(rule);
                }
            }
        }

        /// <summary>
        /// The instant portal is spawned by an item — show which one, with a jump to its editor
        /// in the Resources section.
        /// </summary>
        private void DrawInstantPortalLinkedItemRow()
        {
            DimensionPortalAccessRuleAsset[] rules = selectedTemplate == null
                ? null
                : selectedTemplate.PortalAccessRules;
            if (rules == null)
            {
                return;
            }

            string itemId = null;
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule != null &&
                    DimensionPortalVersions.IsInstantaneousItem(rule.AccessKind) &&
                    !string.IsNullOrEmpty(rule.PortalItemObjectId))
                {
                    itemId = rule.PortalItemObjectId;
                    break;
                }
            }

            if (itemId == null)
            {
                return;
            }

            DimensionItemAsset linkedItem = null;
            DimensionItemAsset[] items = selectedTemplate.GlobalItems;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null &&
                        string.Equals(items[i].ItemId, itemId, System.StringComparison.Ordinal))
                    {
                        linkedItem = items[i];
                        break;
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Linked item", "The item that spawns this portal when used."),
                GUILayout.Width(70f));
            string displayText = linkedItem != null && !string.IsNullOrEmpty(linkedItem.DisplayName)
                ? linkedItem.DisplayName + "  (" + itemId + ")"
                : itemId;
            EditorGUILayout.SelectableLabel(
                displayText,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUI.DisabledScope(linkedItem == null))
            {
                if (GUILayout.Button(
                    new GUIContent(
                        "Go",
                        linkedItem == null
                            ? "The item asset does not exist yet — run Generate Item Prefab in Resources first."
                            : "Open this item in the Resources section."),
                    GUILayout.Width(36f)))
                {
                    Selection.activeObject = linkedItem;
                    EditorGUIUtility.PingObject(linkedItem);
                    RequestSectionChange("resources");
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        // The Portal Studio's blue identity strip, now carrying the mode tabs on its right. The tabs
        // are borderless so the strip shows through; the active one gets a darker-blue wash, and a
        // tiny tick by each name shows (and toggles) whether that version ships.
        private static readonly Color PortalBlue = new Color(0.26f, 0.67f, 0.95f);

        private void DrawPortalHeaderStrip()
        {
            EnsurePortalHeaderStyles();
            Rect bar = EditorGUILayout.GetControlRect(false, 40f);
            EditorGUI.DrawRect(bar, new Color(PortalBlue.r, PortalBlue.g, PortalBlue.b, 0.15f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), PortalBlue);
            GUI.Label(new Rect(bar.x + 12f, bar.y, 200f, bar.height), "Portal Studio", portalHeaderStyle);

            float placedW = PortalTabWidth("Placed Portal");
            float instantW = PortalTabWidth("Instant Portal");
            const float gap = 4f;
            float startX = bar.xMax - placedW - gap - instantW - 6f;
            DrawPortalTab(new Rect(startX, bar.y, placedW, bar.height), false, "Placed Portal");
            DrawPortalTab(new Rect(startX + placedW + gap, bar.y, instantW, bar.height), true, "Instant Portal");
        }

        private float PortalTabWidth(string name)
        {
            // Room for the tick (~25px lead-in) + the name + trailing breathing space.
            return portalTabNameStyle.CalcSize(new GUIContent(name)).x + 34f;
        }

        private void DrawPortalTab(Rect rect, bool instant, string name)
        {
            bool selected = (portalStudioTab == 1) == instant;
            if (selected)
            {
                // A slight darker-blue wash marks the active tab; the strip shows through.
                EditorGUI.DrawRect(rect, new Color(0.10f, 0.30f, 0.52f, 0.42f));
            }
            else if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f));
            }

            bool hasVersion = HasPortalVersion(instant);
            bool enabled = IsPortalVersionEnabled(instant);

            // A genuinely tiny tick beside the name: filled + check when the version ships, a faint
            // hollow square when it does not.
            const float ts = 10f;
            Rect tick = new Rect(rect.x + 9f, rect.y + (rect.height - ts) / 2f, ts, ts);
            if (hasVersion)
            {
                if (enabled)
                {
                    EditorGUI.DrawRect(tick, PortalBlue);
                    GUI.Label(new Rect(tick.x - 1f, tick.y - 2f, tick.width + 2f, tick.height + 2f), "✓", portalTickStyle);
                }
                else
                {
                    DrawThinBorder(tick, new Color(1f, 1f, 1f, 0.35f));
                }
            }

            Rect nameRect = new Rect(tick.xMax + 6f, rect.y, rect.xMax - (tick.xMax + 6f), rect.height);
            GUI.Label(nameRect, name, selected ? portalTabNameSelStyle : portalTabNameStyle);

            if (hasVersion && GUI.Button(tick, GUIContent.none, GUIStyle.none))
            {
                TogglePortalVersionEnabled(instant);
            }

            if (GUI.Button(nameRect, GUIContent.none, GUIStyle.none))
            {
                portalStudioTab = instant ? 1 : 0;
                GUI.FocusControl(null);
            }
        }

        private static void DrawThinBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        private void EnsurePortalHeaderStyles()
        {
            if (portalHeaderStyle != null)
            {
                return;
            }

            portalHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            portalTabNameStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 12, alignment = TextAnchor.MiddleLeft };
            portalTabNameStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            portalTabNameSelStyle = new GUIStyle(portalTabNameStyle);
            portalTabNameSelStyle.normal.textColor = Color.white;
            portalTickStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            portalTickStyle.normal.textColor = Color.white;
        }

        private void DrawPortalEditor()
        {
            DrawPortalHeaderStrip();
            GUILayout.Space(4f);

            if (portalStudioTab == 1)
            {
                DrawInstantPortalLinkedItemRow();
                DrawInstantPortalEditor();
                return;
            }

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
            initializedItemPortalTemplate = null;
            initializedItemPortalProfile = null;
        }

        private void DrawInstantPortalEditor()
        {
            DimensionPortalVisualProfileAsset profile = selectedTemplate.ItemPortalVisualProfile;
            if (profile == null)
            {
                QueueItemPortalVisualProfileSetup();
            }
            profile = selectedTemplate.ItemPortalVisualProfile;

            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    itemPortalVisualProfileSetupQueued
                        ? "Preparing and assigning this dimension's frameless instant-portal visual profile."
                        : "The dashboard could not create or assign this dimension's instant portal profile.",
                    itemPortalVisualProfileSetupQueued
                        ? MessageType.Info
                        : MessageType.Warning);
                if (GUILayout.Button("Retry Automatic Profile Setup", GUILayout.Width(260f)))
                {
                    CreateItemPortalVisualProfileAsset();
                }

                return;
            }

            EnsureItemPortalProfileInitializedOnce(profile);

            if (itemPortalAppearanceStudio == null)
            {
                itemPortalAppearanceStudio = new DimensionPortalAppearanceStudio();
                itemPortalAppearanceStudio.ConfigureInstantPortalMode();
            }

            DimensionPortalAppearanceStudio.DrawResult studioResult =
                itemPortalAppearanceStudio.Draw(
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
                    TryUseItemPortalVisualProfile(requestedProfile, out _);
                itemPortalAppearanceStudio.NotifyRuntimeSyncResult(
                    requestedProfile,
                    switched);
            }
            else if (studioResult.RuntimeSyncRequested)
            {
                DimensionPortalVisualProfileAsset boundProfile =
                    selectedTemplate.ItemPortalVisualProfile;
                bool synchronized = studioResult.CanApply &&
                    TryApplyPortalVisualChanges(out _);
                itemPortalAppearanceStudio.NotifyRuntimeSyncResult(
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

        private void EnsureItemPortalProfileInitializedOnce(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null ||
                (initializedItemPortalTemplate == selectedTemplate &&
                 initializedItemPortalProfile == profile))
            {
                return;
            }

            initializedItemPortalTemplate = selectedTemplate;
            initializedItemPortalProfile = profile;
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

        private void CreateItemPortalVisualProfileAsset()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            bool created;
            string message;
            DimensionPortalVisualProfileAsset profile =
                DimensionPortalVisualProfileEditorUtility.EnsureItemAssigned(
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

        private void QueueItemPortalVisualProfileSetup()
        {
            if (selectedTemplate == null || itemPortalVisualProfileSetupQueued)
            {
                return;
            }

            itemPortalVisualProfileSetupQueued = true;
            DimensionTemplateAsset template = selectedTemplate;
            EditorApplication.delayCall += () =>
            {
                itemPortalVisualProfileSetupQueued = false;
                if (this == null || template == null || selectedTemplate != template)
                {
                    return;
                }

                bool created;
                string message;
                DimensionPortalVisualProfileAsset profile =
                    DimensionPortalVisualProfileEditorUtility.EnsureItemAssigned(
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

        private bool TryUseItemPortalVisualProfile(
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
                template.ItemPortalVisualProfile;
            if (previousProfile == requestedProfile)
            {
                message = "This portal profile is already in use.";
                SetLastEditorAction(message, MessageType.Info);
                return true;
            }

            Undo.RecordObject(template, "Use Instant Portal Profile");
            template.SetItemPortalVisualProfile(requestedProfile);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            ResetPortalProfileInitializationCache();

            if (TryApplyPortalVisualChanges(out message))
            {
                RebuildWorkspace();
                Repaint();
                return true;
            }

            template.SetItemPortalVisualProfile(previousProfile);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            ResetPortalProfileInitializationCache();
            RebuildWorkspace();
            message = string.IsNullOrEmpty(message)
                ? "The instant portal profile could not be applied. The previous profile remains in use."
                : message + " The previous instant portal profile remains in use.";
            SetLastEditorAction(message, MessageType.Error);
            Repaint();
            return false;
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

            if (itemPortalAppearanceStudio != null &&
                !itemPortalAppearanceStudio.FlushPendingFrameTextureUpdate(
                    out string itemFrameTextureError))
            {
                message = itemFrameTextureError;
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

            if (selectedTemplate.ItemPortalVisualProfile != null &&
                !DimensionPortalArtworkEditorUtility.FlushPending(
                    selectedTemplate.ItemPortalVisualProfile,
                    out string itemArtworkError))
            {
                message = itemArtworkError;
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
                    Field("interactable", "Interactable"),
                    Field("requiredItems", "Required activation items"),
                    Field("craftable", "Craftable"),
                    Field("craftingStationObjectId", "Crafting station object id (blank = Wooden Workbench)"),
                    Field("generatedInWorld", "Generated in world (placed portal only)"),
                    Field("droppable", "Droppable from mobs/bosses"),
                    Field("dropTargets", "Drop targets"),
                    Field("portalItemObjectId", "Portal item object id (item portal only)"),
                    Field("itemPortalDurationSeconds", "Item portal open duration seconds (item portal only)"));
            }

            EditorGUILayout.HelpBox(
                "Portal versions: the placed portal (V1) and the instantaneous item portal (V2) can " +
                "both be enabled and co-exist in the same world — players then choose freely. Each is " +
                "optional, but at least one must stay enabled — the generator re-enables the placed " +
                "portal if you turn both off. The generated return portal (V3) is always present.",
                MessageType.Info);
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
                Field("biomeId", "Biome ID"),
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
                    Field("triggers", "Triggers"),
                    Field("tiles", "Terrain tiles"),
                    Field("sceneObjects", "Placed objects"));
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

                // Hidden items are framework infrastructure the modder should never edit — e.g. a
                // tileset block's auto-created ground counterpart. They still generate; they just
                // don't clutter the item list.
                if (item.Hidden)
                {
                    continue;
                }

                DrawSerializedAsset(
                    item,
                    BuildAssetEditorTitle("Item", item.DisplayName, item.ItemId, i),
                    BuildItemFields(item));

                DrawItemArchetypeSummary(item);

                // Symmetry: an item leaves with its whole footprint (.asset + generated prefab).
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Delete item…", GUILayout.Width(100f)) &&
                    EditorUtility.DisplayDialog(
                        "Delete item",
                        "Delete \"" + item.DisplayName + "\" and its generated prefab?",
                        "Delete",
                        "Cancel"))
                {
                    RunAssetAction(DimensionFrameworkAuthoringAssetUtility.DeleteItem(selectedTemplate, item));
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }
        }

        /// <summary>
        /// Fills empty portal-item icon slots with the framework default icons once, per template, so a
        /// creator sees them applied on view without dragging. Keeps retrying until the default sprites
        /// have imported (so it self-heals right after the PNGs are dropped in), then stops.
        /// </summary>
        private void TryApplyDefaultPortalIcons()
        {
            if (selectedTemplate == null)
            {
                return;
            }

            int id = selectedTemplate.GetInstanceID();
            if (portalIconsScannedTemplates.Contains(id))
            {
                return;
            }

            DimensionFrameworkAuthoringAssetUtility.ApplyDefaultPortalIcons(selectedTemplate);
            if (DimensionFrameworkAuthoringAssetUtility.DefaultPortalIconsExist())
            {
                portalIconsScannedTemplates.Add(id);
            }
        }

        /// <summary>
        /// Generation bar for items: reports how many are ready and how many are blocked, and
        /// only offers the action when there is something valid to build.
        /// </summary>
        private void DrawItemGenerationBar()
        {
            TryApplyDefaultPortalIcons();

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

            // Default items for enabled item portals (V2) are created on generate and are valid by
            // construction, so count them as ready — this also enables the button when the only item to
            // build is an item portal the creator has not hand-authored yet.
            ready += DimensionFrameworkAuthoringAssetUtility.CountMissingPortalItems(selectedTemplate);

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

            // Make sure every enabled item portal (V2) has a real, editable item asset before we build,
            // creating a default for any that is missing. Persisting it (rather than synthesizing a
            // throwaway) is what lets the creator open it in the item list and assign an icon/recipe.
            DimensionFrameworkAuthoringAssetActionResult portalItemResult =
                DimensionFrameworkAuthoringAssetUtility.EnsurePortalItems(selectedTemplate);
            DimensionFrameworkAuthoringAssetUtility.ApplyDefaultPortalIcons(selectedTemplate);

            // Same guarantee for tileset blocks: every enabled tileset's toggled-on block kinds get
            // a real item asset (id locked to the tileset's derived block id) before generation.
            DimensionFrameworkAuthoringAssetUtility.EnsureTilesetBlockItems(selectedTemplate);

            // And for food: every dish becomes three items and every golden ingredient a fourth,
            // re-synced from the dish and the ingredient each time so a renamed dish cannot ship
            // under its old name. Must run before the item list is read below.
            DimensionFrameworkAuthoringAssetUtility.EnsureFoodItems(selectedTemplate);
            int portalItemsCreated =
                portalItemResult != null && portalItemResult.CreatedObject != null ? 1 : 0;

            // Re-read after the ensure step so any freshly created portal items are included.
            DimensionItemAsset[] itemsToGenerate = selectedTemplate.GlobalItems;

            // The drop-location inversion, done ONCE for the whole generate. Every generator below
            // is handed the same plan, so a chest and a slime can never disagree about what drops
            // from them, and the walk over every authored item happens once rather than per source.
            DimensionDropPlan drops = DimensionDropPlan.Build(
                itemsToGenerate,
                selectedTemplate.GlobalWorldObjects);

            // The mod's own loot tables, answerable by name for the whole generate — the same
            // law as conditions below. Anything stamping a LootTableID (a creature's drops, a
            // container's becomes-loot) resolves vanilla names first and this mod's tables
            // second, to the exact minted id the runtime registry will carry.
            DimensionEditorLootTables.Seed(selectedTemplate);

            // This mod's own conditions have to be answerable by name for the whole generate, and
            // their numbers depend on the whole set — so they are claimed once, here, and put back
            // afterwards. Without it a creature asking for a custom buff silently gets nothing.
            using (DimensionConditionScope conditions =
                new DimensionConditionScope(selectedTemplate.GlobalConditions))
            {
            if (conditions.Count > 0)
            {
                Debug.Log(
                    "[Dimensions API] " + conditions.Count +
                    " condition(s) of this mod's own are available by name for this generate.");
            }

            // Every name the OTHER generators owe the player, gathered before anything is written.
            // It is built here, inside the condition scope, because a custom stat effect's line is
            // keyed by the number the scope hands it; and it is handed to the item generator
            // because the mod has one localization table and one pass may write it.
            DimensionLocalizationPlan localization = DimensionLocalizationPlan.Build(
                selectedTemplate,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionItemGenerationReport report = DimensionItemGenerator.Generate(
                itemsToGenerate,
                outputFolder,
                EveryRecipeInTheMod(),
                selectedTemplate.Tilesets,
                selectedTemplate.Biomes,
                selectedTemplate.GlobalBosses,
                selectedTemplate.NamedAreas,
                localization,
                OwnedObjectIds(),
                selectedTemplate.GlobalExplosions,
                SwitchedOffObjectIds());

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + report.Errors[i]);
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + report.Warnings[i]);
            }

            // An item that never said what it is has one chosen for it, and that choice decides
            // which slot it lands in and how much use it takes before it breaks. Plain log lines
            // rather than warnings: on a project that predates the question every item is on this
            // list, and a hundred warnings would bury the ones that need doing something about.
            for (int i = 0; i < report.Derived.Count; i++)
            {
                Debug.Log("[Dimensions API] " + report.Derived[i]);
            }

            // Ground fog rides the same action because it is generated FROM the blocks, and a mod
            // whose blocks and whose fog were generated at different moments would ship a fog block
            // describing a tileset id that no longer exists.
            DimensionGroundFogReport fogReport =
                DimensionGroundFogGenerator.Generate(selectedTemplate.Tilesets, modRoot);
            for (int i = 0; i < fogReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + fogReport.Warnings[i]);
            }

            string fogSummary = DescribeGroundFog(fogReport);

            DimensionContainerGenerationReport containerReport = DimensionContainerGenerator.Generate(
                selectedTemplate.GlobalContainers,
                outputFolder + "/" + DimensionContainerGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                drops);

            for (int i = 0; i < containerReport.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + containerReport.Errors[i]);
            }

            for (int i = 0; i < containerReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + containerReport.Warnings[i]);
            }

            string containerSummary =
                containerReport.Created.Count + containerReport.Updated.Count +
                containerReport.Skipped.Count > 0
                    ? "\n\n" + containerReport.Summarize()
                    : string.Empty;

            DimensionCreatureGenerationReport creatureReport = GenerateCreatures(outputFolder, drops);
            for (int i = 0; i < creatureReport.Errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + creatureReport.Errors[i]);
            }

            for (int i = 0; i < creatureReport.Warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + creatureReport.Warnings[i]);
            }

            string creatureSummary =
                creatureReport.Created.Count + creatureReport.Updated.Count + creatureReport.Skipped.Count > 0
                    ? "\n\n" + creatureReport.Summarize()
                    : string.Empty;

            // Everything else a mod can define. These went unreached for a while — the assets and
            // the generators both existed, and nothing called them — which is exactly the shape of
            // failure that leaves an author staring at a crop they authored and never see in game.
            DimensionWorkbenchGenerationReport workbenchReport = DimensionWorkbenchGenerator.Generate(
                selectedTemplate.GlobalWorkbenches,
                outputFolder + "/" + DimensionWorkbenchGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionPlantGenerationReport plantReport = DimensionPlantGenerator.Generate(
                selectedTemplate.GlobalPlants,
                outputFolder + "/" + DimensionPlantGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionWorldObjectGenerationReport worldObjectReport =
                DimensionWorldObjectGenerator.Generate(
                    selectedTemplate.GlobalWorldObjects,
                    outputFolder + "/" + DimensionWorldObjectGenerator.FolderName,
                    DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                    drops,
                    selectedTemplate.Tilesets);

            DimensionExplosionGenerationReport explosionReport = DimensionExplosionGenerator.Generate(
                selectedTemplate.GlobalExplosions,
                outputFolder + "/" + DimensionExplosionGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            DimensionCritterGenerationReport critterReport = DimensionCritterGenerator.Generate(
                selectedTemplate.GlobalCritters,
                outputFolder + "/" + DimensionCritterGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                selectedTemplate.Tilesets);

            // World rules are the one thing here that does not become a prefab. Fishing and talents
            // are written as settings files into the mod's own Conf folder, which the game reads
            // while it starts; upgrade prices and the player's numbers ride the generated bootstrap
            // instead. They go to the MOD ROOT rather than the items folder, because Conf is a
            // folder the game itself looks for and it only looks beside the mod, never inside it.
            DimensionWorldRulesGenerationReport gameSetupReport =
                DimensionWorldRulesGenerator.Generate(selectedTemplate.GlobalGameSetups, modRoot);

            DimensionProjectileGenerationReport projectileReport = DimensionProjectileGenerator.Generate(
                selectedTemplate.GlobalProjectiles,
                outputFolder + "/" + DimensionProjectileGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()),
                selectedTemplate.Tilesets);

            DimensionCreatureGenerationReport vehicleReport =
                DimensionVehicleGenerator.Generate(
                    selectedTemplate.GlobalVehicles,
                    outputFolder + "/" + DimensionVehicleGenerator.FolderName,
                    DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            ReportProblems(workbenchReport.Errors, workbenchReport.Warnings);
            ReportProblems(plantReport.Errors, plantReport.Warnings);
            ReportProblems(worldObjectReport.Errors, worldObjectReport.Warnings);
            ReportProblems(vehicleReport.Errors, vehicleReport.Warnings);
            ReportProblems(projectileReport.Errors, projectileReport.Warnings);
            ReportProblems(critterReport.Errors, critterReport.Warnings);
            ReportProblems(explosionReport.Errors, explosionReport.Warnings);
            ReportProblems(gameSetupReport.Errors, gameSetupReport.Warnings);

            string otherSummary =
                Describe("Crafting stations", workbenchReport.Created.Count, workbenchReport.Updated.Count, workbenchReport.Skipped.Count) +
                Describe("Plants", plantReport.Created.Count, plantReport.Updated.Count, plantReport.Skipped.Count) +
                Describe("Objects", worldObjectReport.Created.Count, worldObjectReport.Updated.Count, worldObjectReport.Skipped.Count) +
                Describe("Vehicles", vehicleReport.Created.Count, vehicleReport.Updated.Count, vehicleReport.Skipped.Count) +
                Describe("Projectiles", projectileReport.Created.Count, projectileReport.Updated.Count, projectileReport.Skipped.Count) +
                Describe("Critters", critterReport.Created.Count, critterReport.Updated.Count, critterReport.Skipped.Count) +
                Describe("Explosions", explosionReport.Created.Count, explosionReport.Updated.Count, explosionReport.Skipped.Count) +
                Describe("World rules", gameSetupReport.Created.Count, gameSetupReport.Updated.Count, gameSetupReport.Skipped.Count);

            // A drop naming a source that nothing generates is completely silent: no error, no
            // creature carrying it, and an item that simply never turns up. This is the only moment
            // the two halves are both known, so it is the only place the typo can be caught.
            WarnAboutDropsFromNowhere(drops);
            WarnAboutIdsThatShadowTheGame();
            WarnAboutPortalCostsFromNowhere(outputFolder);

            // And the same shape of silence for names: an object built by a generator nobody
            // remembered to give a name to reaches the player showing its own key. Every generator
            // above has closed its asset batch by now, which is what makes the prefabs readable.
            WarnAboutObjectsWithNoName(
                report.LocalizationKeys,
                report,
                containerReport,
                creatureReport,
                workbenchReport,
                plantReport,
                worldObjectReport,
                explosionReport,
                critterReport,
                projectileReport,
                vehicleReport);

            // A mod that ships a pooled-prefab bank pools ONLY what that bank lists, and a body
            // with no pool is a KeyNotFoundException thrown from inside the game's own draw loop
            // the first time that object comes on screen — not a missing picture. Every body these
            // generators just wrote is exposed to it, so the bank is brought back into step here,
            // once, after every generator has closed its asset batch and the prefabs are readable.
            DimensionPooledPrefabBankUtility.Result pooledPrefabs =
                DimensionPooledPrefabBankUtility.EnsureGeneratedPrefabsArePooled(
                    modRoot,
                    delegate(string message) { Debug.LogWarning("[Dimensions API] " + message); });

            string portalItemSummary = portalItemsCreated > 0
                ? "\n\n" + portalItemResult.Message +
                  " Open it under Resources ▸ Items to set its Icon sprite, then generate again."
                : string.Empty;

            EditorUtility.DisplayDialog(
                "Generate Items",
                report.Summarize() + creatureSummary + containerSummary + otherSummary +
                fogSummary + pooledPrefabs.Summarize() + portalItemSummary +
                "\n\nOutput: " + outputFolder +
                (report.HasProblems
                    ? "\n\nDetails were written to the Console."
                    : string.Empty),
                "OK");
            }

            DimensionEditorLootTables.Clear();
        }

        /// <summary>
        /// Turns the dimension's authored mobs, bosses and animals into real prefabs.
        /// </summary>
        /// <remarks>
        /// All three shapes go through one generator because a prefab does not care which authoring
        /// asset it came from — what differs is what an author is offered, not what the game needs.
        /// Bosses are marked as such because Core Keeper treats them differently in several places.
        /// Every one of them is an enemy in the game's sense of the word — the tag is what carries
        /// <c>LastAttackerCD</c> — and what separates a cow from a caveling is the Temperament each
        /// asset carries, which the generator turns into attack tags and a chase distance.
        /// </remarks>
        /// <summary>
        /// Every id this template turns into an object, so a reference to one of them is qualified.
        /// </summary>
        /// <remarks>
        /// Handed to every generator that is not the item generator. Without it those runs owned
        /// nothing, and <c>QualifyReference</c> — which is "qualify it only if it is ours" — was the
        /// identity function: a workbench recipe outputting the mod's own item shipped the bare id
        /// while the item had registered under the qualified one, and the station crafted nothing.
        /// </remarks>
        private List<string> OwnedObjectIds()
        {
            return DimensionGeneratedObjectIds.Collect(selectedTemplate);
        }

        /// <summary>
        /// The ids of this mod's own assets that are unticked, so a reference to one can be told
        /// apart from a misspelling.
        /// </summary>
        /// <remarks>
        /// The bootstrap emitter's binder has always been given this. The generators' were not, so
        /// the same unticked projectile got "one of yours but is switched off" from one half of a
        /// generate and "neither one of this mod's nor one the game has" from the other.
        /// </remarks>
        private List<string> SwitchedOffObjectIds()
        {
            return DimensionGeneratedObjectIds.SwitchedOff(selectedTemplate);
        }

        /// <summary>
        /// Every recipe the mod has, whether it sits in the mod's recipe list or only on a
        /// Workbench.
        /// </summary>
        /// <remarks>
        /// WHAT A CRAFT COSTS IS STORED ON THE ITEM IT MAKES, so the item generator is the only
        /// pass that can write it — and it was only being shown the mod's own recipe list. A
        /// Workbench also carries a list of its own, and the bootstrap already registers from that
        /// one. So a recipe added straight to a Workbench appeared at the bench and cost nothing at
        /// all, with no word said. Both lists are handed over now; the same asset in both is
        /// recognised as one recipe.
        /// </remarks>
        private List<DimensionRecipeAsset> EveryRecipeInTheMod()
        {
            List<DimensionRecipeAsset> all = new List<DimensionRecipeAsset>();
            if (selectedTemplate == null)
            {
                return all;
            }

            DimensionRecipeAsset[] global = selectedTemplate.GlobalRecipes;
            for (int i = 0; global != null && i < global.Length; i++)
            {
                if (global[i] != null && !all.Contains(global[i]))
                {
                    all.Add(global[i]);
                }
            }

            DimensionWorkbenchAsset[] benches = selectedTemplate.GlobalWorkbenches;
            for (int b = 0; benches != null && b < benches.Length; b++)
            {
                if (benches[b] == null || !benches[b].Enabled)
                {
                    continue;
                }

                DimensionRecipeAsset[] benchRecipes = benches[b].Recipes;
                for (int i = 0; benchRecipes != null && i < benchRecipes.Length; i++)
                {
                    if (benchRecipes[i] != null && !all.Contains(benchRecipes[i]))
                    {
                        all.Add(benchRecipes[i]);
                    }
                }
            }

            return all;
        }

        private DimensionCreatureGenerationReport GenerateCreatures(
            string outputFolder,
            DimensionDropPlan drops)
        {
            List<DimensionCreatureGenerator.Request> requests =
                new List<DimensionCreatureGenerator.Request>();

            DimensionMobAsset[] mobs = selectedTemplate.GlobalMobs;
            if (mobs != null)
            {
                for (int i = 0; i < mobs.Length; i++)
                {
                    DimensionMobAsset mob = mobs[i];
                    if (mob == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = mob.MobId,
                        DisplayName = mob.DisplayName,
                        Stats = mob.CreatureStats,
                        Combat = mob.Combat,
                        SimpleTraits = mob.SimpleTraits,
                        ExtraLoot = mob.ExtraLoot,
                        Pet = mob.Pet,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, mob.MobId),
                        LootTable = mob.LootTable,
                        BehaviourName = mob.BehaviorScriptId,
                        IsEnemy = true,
                        Aggression = mob.Aggression,
                        IsBoss = false,
                        Hatching = mob.Hatching,
                        Visual = mob.Visual,
                        Audio = mob.Audio,
                        Enabled = mob.Enabled
                    });

                    // An elite is a second creature, not a mode of the first. It borrows the same
                    // behaviour and art and differs only in the numbers, which is exactly what the
                    // game can already express without any new machinery.
                    DimensionEliteVariantTemplate elite = mob.EliteVariant;
                    if (elite.Enabled)
                    {
                        // The two halves of the elite do not both work on both kinds of stat
                        // block, and which half is doing nothing is invisible in the prefab. On a
                        // level-scaled creature the game recomputes health, damage reduction and
                        // attack damage from the level every time, so only the level bump lands.
                        if (!mob.CreatureStats.UsesAuthoredNumbers &&
                            elite.ExtraLevels == 0)
                        {
                            Debug.LogWarning(
                                "[Dimensions API] '" + mob.DisplayName + "' takes its numbers from " +
                                "the area level curve, and its elite is set to no extra levels. The " +
                                "health, damage and armour multipliers are recomputed from the " +
                                "level on a creature like this, so the elite will be identical to " +
                                "the ordinary one. Give it at least one extra level, or type the " +
                                "mob's numbers under Stats.");
                        }

                        requests.Add(new DimensionCreatureGenerator.Request
                        {
                            CreatureId = DimensionEliteVariantTemplate.IdFor(mob.MobId),
                            DisplayName = elite.DisplayNameFor(mob.DisplayName),
                            Stats = mob.CreatureStats.ScaledForElite(elite, 1, true, false),

                            // The level bump is what makes a level-scaled elite actually harder;
                            // on a creature with typed numbers the multipliers have already done
                            // the work and the rarity is only the colour of its name.
                            Rarity = (Rarity)elite.ExtraLevels,
                            Combat = mob.Combat,
                            SimpleTraits = mob.SimpleTraits,
                            ExtraLoot = mob.ExtraLoot,
                            Pet = mob.Pet,
                            DropsFromItems = drops.For(
                                DimensionDropSourceKind.Creature,
                                DimensionEliteVariantTemplate.IdFor(mob.MobId)),
                            LootTable = elite.LootTable != null ? elite.LootTable : mob.LootTable,
                            BehaviourName = mob.BehaviorScriptId,
                            IsEnemy = true,
                            Aggression = mob.Aggression,
                            IsBoss = false,

                            // Same art and same voice as the mob it is a harder copy of: an elite
                            // is the same creature with different numbers, and giving it its own
                            // clip list would mean drawing every elite twice.
                            Visual = mob.Visual,
                            Audio = mob.Audio,
                            Enabled = mob.Enabled
                        });
                    }
                }
            }

            DimensionBossAsset[] bosses = selectedTemplate.GlobalBosses;
            if (bosses != null)
            {
                for (int i = 0; i < bosses.Length; i++)
                {
                    DimensionBossAsset boss = bosses[i];
                    if (boss == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = boss.BossId,
                        DisplayName = boss.DisplayName,
                        Stats = boss.CreatureStats,
                        Combat = boss.Combat,
                        SimpleTraits = boss.SimpleTraits,
                        BorrowedKit = boss.BorrowedKit,
                        MoreBorrowedKits = boss.MoreBorrowedKits,
                        TheRestOfTheKits = boss.TheRestOfTheKits,
                        BossChest = boss.BossChest,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, boss.BossId),
                        LootTable = boss.LootTable,
                        IsEnemy = true,

                        // Bosses were pinned to Hostile here, on the reasoning that a fight
                        // nobody can start is not a boss fight. A boss that ignores you until you
                        // touch it is a real shape and the framework was refusing to build it, so
                        // the boss now answers the same question a mob does. Its own default is
                        // Hostile, which is what almost every boss is.
                        Aggression = boss.Aggression,
                        IsBoss = true,
                        MapPin = boss.MapPin,
                        FightMusic = boss.FightMusic,
                        BodySprite = boss.Visual.BodySprite,
                        Visual = boss.Visual,
                        Audio = boss.Audio,
                        SummoningItemId = boss.SummoningItemId,
                        Enabled = boss.Enabled
                    });
                }
            }

            DimensionAnimalAsset[] animals = selectedTemplate.GlobalAnimals;
            if (animals != null)
            {
                for (int i = 0; i < animals.Length; i++)
                {
                    DimensionAnimalAsset animal = animals[i];
                    if (animal == null)
                    {
                        continue;
                    }

                    requests.Add(new DimensionCreatureGenerator.Request
                    {
                        CreatureId = animal.AnimalId,
                        DisplayName = animal.DisplayName,
                        Stats = animal.CreatureStats,
                        Combat = animal.Combat,
                        SimpleTraits = animal.SimpleTraits,
                        DropsFromItems = drops.For(DimensionDropSourceKind.Creature, animal.AnimalId),
                        LootTable = animal.LootTable,

                        Visual = animal.Visual,
                        Audio = animal.Audio,

                        // The game's own Cow and Roly Poly BOTH carry EnemyAuthoring, and they are
                        // as harmless as animals get — what makes them harmless is their empty
                        // attack tags, which is what Temperament now writes. Withholding the tag
                        // instead cost the animal its LastAttackerCD (so a Defensive animal could
                        // never hit back) and made it invisible to explosions and pushback.
                        IsEnemy = true,
                        Aggression = animal.Aggression,
                        IsBoss = false,
                        Enabled = animal.Enabled
                    });
                }
            }

            if (requests.Count == 0)
            {
                return new DimensionCreatureGenerationReport();
            }

            return DimensionCreatureGenerator.Generate(
                requests,
                outputFolder + "/" + DimensionCreatureGenerator.FolderName,
                DimensionNamingContext.ForOutputFolder(outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));
        }

        /// <summary>
        /// Reports anything this run built that a player would meet without a name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each generator is named explicitly rather than collected through a shared interface,
        /// because there is no shared interface and inventing one to make this line shorter would
        /// touch ten files to save four. The cost of forgetting a generator here is one missed
        /// warning; the cost of the check not existing at all was a whole class of content shipping
        /// with its key printed in the tooltip.
        /// </para>
        /// <para>
        /// Reports carry prefab PATHS, so the objects are read back off disk. That is deliberate:
        /// asking the prefabs what they are called is the only question that keeps working when
        /// somebody adds a generator, or a second object inside an existing one.
        /// </para>
        /// </remarks>
        private static void WarnAboutObjectsWithNoName(
            ICollection<string> writtenKeys,
            DimensionItemGenerationReport items,
            DimensionContainerGenerationReport containers,
            DimensionCreatureGenerationReport creatures,
            DimensionWorkbenchGenerationReport workbenches,
            DimensionPlantGenerationReport plants,
            DimensionWorldObjectGenerationReport worldObjects,
            DimensionExplosionGenerationReport explosions,
            DimensionCritterGenerationReport critters,
            DimensionProjectileGenerationReport projectiles,
            DimensionCreatureGenerationReport vehicles)
        {
            List<string> paths = new List<string>();
            AddPaths(paths, items == null ? null : items.Created, items == null ? null : items.Updated);
            AddPaths(paths, containers == null ? null : containers.Created, containers == null ? null : containers.Updated);
            AddPaths(paths, creatures == null ? null : creatures.Created, creatures == null ? null : creatures.Updated);
            AddPaths(paths, workbenches == null ? null : workbenches.Created, workbenches == null ? null : workbenches.Updated);
            AddPaths(paths, plants == null ? null : plants.Created, plants == null ? null : plants.Updated);
            AddPaths(paths, worldObjects == null ? null : worldObjects.Created, worldObjects == null ? null : worldObjects.Updated);
            AddPaths(paths, explosions == null ? null : explosions.Created, explosions == null ? null : explosions.Updated);
            AddPaths(paths, critters == null ? null : critters.Created, critters == null ? null : critters.Updated);
            AddPaths(paths, projectiles == null ? null : projectiles.Created, projectiles == null ? null : projectiles.Updated);
            AddPaths(paths, vehicles == null ? null : vehicles.Created, vehicles == null ? null : vehicles.Updated);

            List<string> warnings = new List<string>();
            DimensionLocalizationCoverage.Check(
                DimensionLocalizationCoverage.ReadGeneratedObjects(paths),
                writtenKeys,
                warnings);

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + warnings[i]);
            }
        }

        private static void AddPaths(List<string> paths, List<string> created, List<string> updated)
        {
            if (created != null)
            {
                paths.AddRange(created);
            }

            if (updated != null)
            {
                paths.AddRange(updated);
            }
        }

        /// <summary>Sends a generator's problems to the Console the same way for every generator.</summary>
        private static void ReportProblems(List<string> errors, List<string> warnings)
        {
            for (int i = 0; errors != null && i < errors.Count; i++)
            {
                Debug.LogError("[Dimensions API] " + errors[i]);
            }

            for (int i = 0; warnings != null && i < warnings.Count; i++)
            {
                Debug.LogWarning("[Dimensions API] " + warnings[i]);
            }
        }

        /// <summary>
        /// One summary line, or nothing when a creator has never used that kind of thing.
        /// </summary>
        /// <remarks>
        /// Silent when there is nothing to say, for the same reason ground fog is: somebody who has
        /// never authored a plant should not read "Plants: 0 created" on every generate.
        /// </remarks>
        private static string Describe(string label, int created, int updated, int skipped)
        {
            if (created + updated + skipped == 0)
            {
                return string.Empty;
            }

            return "\n\n" + label + ": " + created + " created, " + updated + " updated, " +
                skipped + " skipped.";
        }

        /// <summary>
        /// Warns about drops that name a source nothing in the mod defines.
        /// </summary>
        /// <remarks>
        /// This failure is completely silent otherwise. An item that says it drops from "gaint_slime"
        /// generates without complaint, no creature carries it, and the author plays their own mod
        /// hunting for something that can never appear. Generation is the only moment both halves —
        /// what drops, and what exists to drop it — are known at once, so it is the only place the
        /// typo can be caught.
        /// </remarks>
        /// <summary>
        /// Warns when one of the mod's own ids is also the name of one of the game's objects.
        /// </summary>
        /// <remarks>
        /// THE GAME'S NAME WINS EVERYWHERE, and that has to be said out loud. Calling one of your
        /// objects Torch is allowed and the object generates perfectly well — but every reference
        /// anywhere in the mod that types "Torch" means the game's torch, because the binder asks
        /// the ObjectID enum first and bakes the number it finds. Without this line the only way to
        /// discover that is to notice a recipe quietly making the wrong thing.
        /// </remarks>
        private void WarnAboutIdsThatShadowTheGame()
        {
            List<string> owned = OwnedObjectIds();
            for (int i = 0; i < owned.Count; i++)
            {
                string local = owned[i];
                if (DimensionObjectBinder.Vanilla(local) == ObjectID.None)
                {
                    continue;
                }

                Debug.LogWarning(
                    "[Dimensions API] One of your objects is called '" + local + "', which is also " +
                    "the name of one of the game's. Your object is still made, but anywhere in this " +
                    "mod that types '" + local + "' — a recipe ingredient, a drop, a shot, a trader's " +
                    "stock — means the GAME'S '" + local + "', not yours. Rename yours if you meant " +
                    "to point at it.");
            }
        }

        /// <summary>
        /// Warns when a portal asks for an item nothing answers to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS ONE SHIPS A DOOR THAT CAN NEVER OPEN. The item a portal asks for is typed into a
        /// text box and was checked nowhere: not while authoring, not at generate, not at run time.
        /// A misspelling generates cleanly, the portal is built, the slot is drawn, and no item in
        /// the game will ever go into it — with nothing said anywhere.
        /// </para>
        /// <para>
        /// Asked through <see cref="DimensionObjectBinder"/>, which is the one predicate in the
        /// framework that can tell the game's own names, this mod's own names and a typo apart, and
        /// which the generators and the bootstrap emitter already decide references with. Asking it
        /// here is what keeps this answer and theirs the same answer.
        /// </para>
        /// </remarks>
        private void WarnAboutPortalCostsFromNowhere(string outputFolder)
        {
            DimensionPortalAccessRuleAsset[] rules = selectedTemplate == null
                ? null
                : selectedTemplate.PortalAccessRules;
            if (rules == null || rules.Length == 0)
            {
                return;
            }

            DimensionObjectBinder binder = new DimensionObjectBinder(
                DimensionNamingContext.ForOutputFolder(
                    outputFolder, OwnedObjectIds(), SwitchedOffObjectIds()));

            for (int i = 0; i < rules.Length; i++)
            {
                DimensionPortalAccessRuleAsset rule = rules[i];
                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                string which = string.IsNullOrEmpty(rule.DisplayName)
                    ? (string.IsNullOrEmpty(rule.RuleId) ? rule.name : rule.RuleId)
                    : rule.DisplayName;

                DimensionPortalRequiredItemTemplate[] items = rule.RequiredItems;
                for (int n = 0; n < items.Length; n++)
                {
                    string itemId = items[n].ItemId;
                    if (string.IsNullOrEmpty(itemId))
                    {
                        Debug.LogWarning(
                            "[Dimensions API] The portal '" + which + "' has an offering slot with " +
                            "no item in it, so nothing can ever be put there and the portal stays " +
                            "shut. Name the item, or take the slot out.");
                        continue;
                    }

                    ObjectID baked;
                    if (binder.TryBind(itemId, out baked))
                    {
                        continue;
                    }

                    string switchedOff = binder.ExplainIfSwitchedOff(
                        "The portal '" + which + "' asks for", itemId);
                    if (switchedOff != null)
                    {
                        Debug.LogWarning("[Dimensions API] " + switchedOff);
                        continue;
                    }

                    Debug.LogWarning(
                        "[Dimensions API] The portal '" + which + "' asks for '" + itemId +
                        "', and nothing in the game or in this mod is called that. The portal is " +
                        "still built and its slot is still drawn, and no item will ever go into " +
                        "it, so no player can open the door — check the spelling against the " +
                        "item you meant.");
                }
            }
        }

        private void WarnAboutDropsFromNowhere(DimensionDropPlan drops)
        {
            if (drops == null || drops.IsEmpty)
            {
                return;
            }

            HashSet<string> defined = new HashSet<string>();
            AddIds(defined, selectedTemplate.GlobalMobs, delegate(DimensionMobAsset a) { return a.MobId; });
            AddIds(defined, selectedTemplate.GlobalMobs, delegate(DimensionMobAsset a)
            {
                return a.EliteVariant.Enabled ? DimensionEliteVariantTemplate.IdFor(a.MobId) : null;
            });
            AddIds(defined, selectedTemplate.GlobalBosses, delegate(DimensionBossAsset a) { return a.BossId; });
            AddIds(defined, selectedTemplate.GlobalAnimals, delegate(DimensionAnimalAsset a) { return a.AnimalId; });
            AddIds(defined, selectedTemplate.GlobalCritters, delegate(DimensionCritterAsset a) { return a.CritterId; });
            AddIds(defined, selectedTemplate.GlobalContainers, delegate(DimensionContainerAsset a) { return a.ContainerId; });
            AddIds(defined, selectedTemplate.GlobalWorldObjects, delegate(DimensionWorldObjectAsset a) { return a.ObjectIdentifier; });
            AddIds(defined, selectedTemplate.GlobalScenes, delegate(SceneTemplateAsset a) { return a.SceneId; });

            List<string> unknown = drops.SourcesNothingDefines(defined);
            for (int i = 0; i < unknown.Count; i++)
            {
                Debug.LogWarning(
                    "[Dimensions API] Something is set to drop from '" + unknown[i] +
                    "', but nothing in this dimension is called that. Nothing will drop, and " +
                    "nothing else will say so — check the spelling against the creature, chest, " +
                    "object or scene you meant.");
            }
        }

        private static void AddIds<T>(HashSet<string> into, T[] assets, System.Func<T, string> idOf)
            where T : UnityEngine.Object
        {
            for (int i = 0; assets != null && i < assets.Length; i++)
            {
                if (assets[i] == null)
                {
                    continue;
                }

                string id = idOf(assets[i]);
                if (!string.IsNullOrEmpty(id))
                {
                    into.Add(id);
                }
            }
        }

        /// <summary>
        /// One line about ground fog, or nothing when no block uses it.
        /// </summary>
        /// <remarks>
        /// Silent when there is nothing to say. A creator who has never touched fog should not have to
        /// read "0 ground fog blocks" every time they generate items.
        /// </remarks>
        private static string DescribeGroundFog(DimensionGroundFogReport fogReport)
        {
            int touched = fogReport.Created.Count + fogReport.Updated.Count + fogReport.Removed.Count;
            if (touched == 0)
            {
                return string.Empty;
            }

            return "\n\nGround fog: " + fogReport.Created.Count + " created, " +
                fogReport.Updated.Count + " updated, " + fogReport.Removed.Count + " removed.";
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
                Field("description", "Description"),
                Field("iconSprite", "Icon sprite (16x16)"),
                Field("smallIconSprite", "Small icon (in-hand, 10x10)"),
                Field("iconId", "Icon ID (fallback)"),
                Field("objectId", "Object ID")
            };

            if (RequiresComponent(required, DimensionItemAuthoringComponents.InventoryItem))
            {
                fields.Add(Field("stackable", "Stacks in one slot"));
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

            if (RequiresComponent(required, DimensionItemAuthoringComponents.WeaponDamage) ||
                RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("weapon", "As a weapon"));
                fields.Add(Field("attackSounds", "What it sounds like to swing"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.Cooldown))
            {
                fields.Add(Field("cooldownSeconds", "Cooldown seconds"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.EquipmentConditions))
            {
                fields.Add(Field("effects", "What it does for you"));

                // Only armour is drawn on the character, and only three body parts read a skin at
                // all, so nothing else is asked this.
                fields.Add(Field("equipmentSkin", "Worn on the character"));
            }

            if (RequiresComponent(required, DimensionItemAuthoringComponents.SecondaryUse))
            {
                fields.Add(Field("secondaryUse", "Right-click"));
            }

            // Cooking is offered on every item rather than gated by archetype: an ingredient is a
            // Material, a cooked dish is a Consumable, and a fish is whatever its author decided.
            fields.Add(Field("cooking", "As food"));

            // Explosives are offered on every item for the same reason: a bomb is the Bomb
            // archetype, but an explosive barrel is a Placeable that happens to go off. On the Bomb
            // archetype this is the headline rather than an extra, so it is named as one.
            fields.Add(RequiresComponent(required, DimensionItemAuthoringComponents.Explosive)
                ? Field("explosive", "What it does when it goes off")
                : Field("explosive", "If it goes off"));
            fields.Add(Field("basics", "Where it sits in the world"));
            if (RequiresComponent(required, DimensionItemAuthoringComponents.Durability))
            {
                fields.Add(Field("durabilityMultiplier", "How sturdy it is"));
                fields.Add(Field("repairMultiplier", "Repair cost"));
                fields.Add(Field("reinforceCostMultiplier", "Reinforce cost"));
            }
            fields.Add(Field("conditions", "Conditions"));
            fields.Add(Field("offHand", "In the off hand"));
            fields.Add(Field("polishesInto", "Polishes into"));
            fields.Add(Field("isAPotion", "Is a potion"));

            // ---- scanning ----
            fields.Add(Field("scansForObjectId", "Scans for"));
            fields.Add(Field("summonsInsteadOfScanning", "Summons it instead of scanning"));
            fields.Add(Field("scannerOnlyInBiome", "Scanner only works in this biome"));

            // ---- how it wears and swings ----
            fields.Add(Field("flatDurability", "Durability, exactly"));
            fields.Add(Field("flatMaxDurability", "Its ceiling, exactly"));
            fields.Add(Field("casualIgnoresItsCooldown", "Casual mode skips its cooldown"));
            fields.Add(Field("damageIsMagic", "Its damage counts as magic"));
            fields.Add(Field("damageIsRanged", "Its damage counts as ranged"));
            fields.Add(Field("damageMultiplierForItsTier", "How hard it hits for its tier"));


            // ---- its looks ----
            fields.Add(Field("iconOffset", "Icon nudge"));
            fields.Add(Field("variation", "Which look it is"));
            fields.Add(Field("variationIsChosenAtRuntime", "The game picks its look"));
            fields.Add(Field("variationItTogglesTo", "The look it flips to"));
            fields.Add(Field("nameGendersPerLanguage", "Its name's gender, per language", true));

            // ---- the rest of what it can be ----
            fields.Add(Field("instrument", "As an instrument", true));
            fields.Add(Field("extraLoot", "Loot besides its drops", true));
            fields.Add(Field("worldRoles", "Its roles in the world", true));
            fields.Add(Field("simpleTraits", "The small things it simply is", true));

            fields.Add(Field("dropsFrom", "Where it drops from"));
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

            // The station id is resolved at GENERATION time, not runtime, so a typo is worth calling
            // out here rather than leaving it to become a recipe that silently never appears.
            if (recipes.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    "Generating wires the whole recipe: ingredients and craft time go onto the item, and " +
                    "the output shows up at the crafting station you name.\n\n" +
                    "\"Crafting station ID\" is a Core Keeper object name — WoodenWorkBench, " +
                    "CopperWorkBench, and so on. A name the game does not know is reported when you " +
                    "generate, and that recipe appears at no station. Leave it empty to skip the " +
                    "station entirely; blocks can still be mined where your dimension generates them.",
                    MessageType.Info);
                GUILayout.Space(4f);
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

                    // ---- how it works ----
                    Field("generatesItsOwnObject", "The framework builds its object"),
                    Field("wholeInventoryIsOneCraft", "Its whole inventory is one craft"),
                    Field("extractsCategoryTag", "What it draws out of things"),
                    Field("extractedAmountRange", "How much it draws at a time"),
                    Field("defaultCraftTimeRange", "How long a craft takes"),
                    Field("showsALoopingEffectWhileWorking", "It shows an effect while working"),

                    // ---- placing and using ----
                    Field("placementRules", "Where it may be placed", true),
                    Field("interaction", "When a player uses it", true),
                    Field("simpleTraits", "The small things it simply is", true),

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
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("aggression", "Temperament"),
                    Field("spawnsInWorld", "Spawns in the world"),
                    Field("spawnChance", "Spawn chance"),
                    Field("spawnAmount", "Spawn amount"),
                    Field("spawnsInGroups", "Spawns in groups"),
                    Field("canSpawnInBlockedArea", "May spawn in blocked areas"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }


        /// <summary>
        /// The container editors.
        /// </summary>
        /// <remarks>
        /// The fields are grouped the way the questions actually come: what it is, how big, where it
        /// goes, how it breaks, and what happens when something is put in it. The indestructible tick
        /// only bites on a world-placed container, and the asset says so itself rather than the
        /// window having to explain it twice.
        /// </remarks>
        private void DrawContainerAssetEditors(DimensionContainerAsset[] containers)
        {
            if (containers == null)
            {
                return;
            }

            for (int i = 0; i < containers.Length; i++)
            {
                DimensionContainerAsset container = containers[i];
                if (container == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    container,
                    BuildAssetEditorTitle("Container", container.DisplayName, container.ContainerId, i),
                    Field("displayName", "Display name"),
                    Field("containerId", "Container ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("labelItComesWith", "The label floating above it"),
                    Field("sprite", "Sprite"),
                    Field("icon", "Icon"),
                    Field("basics", "Where it sits in the world"),
                    Field("conditions", "Conditions"),
                    Field("size", "Size"),
                    Field("customSlotsAcross", "Custom slots across"),
                    Field("customSlotsDown", "Custom slots down"),
                    Field("upgradeableExtraSlots", "Upgradeable extra slots"),
                    Field("isAPouch", "Is a pouch"),
                    Field("sameSizeAtEveryLevel", "Same size at every level"),
                    Field("onlyAcceptsCategoryTags", "Only accepts these categories"),
                    Field("oneItemPerSlot", "One item per slot"),
                    Field("contentsAreLocked", "Contents are locked"),
                    Field("cannotAddItems", "Cannot add items"),
                    Field("autoTransfer", "Auto transfer"),
                    Field("slotRules", "Slot rules"),
                    Field("tileSize", "Tile size"),
                    Field("canBePlacedOnWater", "Can be placed on water"),
                    Field("facesPlacementDirection", "Faces placement direction"),
                    Field("origin", "Where it comes from"),
                    Field("indestructible", "Indestructible (world-placed only)"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("requiredMiningDamage", "Required mining damage"),
                    Field("requiresDrill", "Requires drill"),
                    Field("dropsContentsWhenBroken", "Drops contents when broken"),
                    Field("dropsItselfWhenBroken", "Drops itself when broken"),
                    // ---- reacting to an item ----
                    Field("reactsToItemId", "Reacts to item"),
                    Field("reactionVariation", "The look it takes when it reacts"),
                    Field("reactionEffectId", "The effect shown as it reacts"),
                    Field("removeColliderOnReaction", "Its collider goes when it reacts"),
                    Field("becomesContainerId", "Becomes container"),
                    Field("becomesLootTableId", "Becomes loot table"),
                    Field("becomesContents", "Becomes contents"),

                    // ---- what its slots accept ----
                    Field("appliesToAllSlots", "One rule for every slot"),
                    Field("acceptsCategoryTags", "Kinds of thing it accepts", true),
                    Field("acceptsItemIds", "Exact items it accepts", true),
                    Field("denyLegendary", "Legendary gear is refused"),
                    Field("showHint", "Its slots hint at what fits"),

                    // ---- appearance and placing ----
                    Field("sprite", "Its picture in the world"),
                    Field("icon", "Its icon in inventories"),
                    Field("placementRules", "Where it may be placed", true),
                    Field("melodyResponse", "It answers a tune", true),
                    Field("interaction", "When a player uses it", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (container.IndestructibleWasRefused)
                {
                    EditorGUILayout.HelpBox(
                        "Indestructible only applies to a container the world places. A container " +
                        "players craft has to be breakable, or they can never take it back.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawPlantAssetEditors(DimensionPlantAsset[] plants)
        {
            if (plants == null)
            {
                return;
            }

            for (int i = 0; i < plants.Length; i++)
            {
                DimensionPlantAsset plant = plants[i];
                if (plant == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    plant,
                    BuildAssetEditorTitle("Plant", plant.DisplayName, plant.PlantId, i),
                    Field("displayName", "Display name"),
                    Field("plantId", "Plant ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("seedIcon", "Seed icon"),
                    Field("art", "What it looks like in the ground", true),
                    Field("growthStages", "Growth stages"),
                    Field("minutesToGrow", "Minutes to grow"),
                    Field("staysToughWhenRipe", "Stays tough when ripe"),
                    Field("washedAwayByWater", "Washed away by water"),
                    Field("produceItemId", "Produce item"),
                    Field("harvestAmount", "How many per harvest"),
                    Field("chanceToGetTheSeedBackPercent", "Chance to get the seed back"),
                    Field("versions", "Better versions", true),
                    Field("ground", "Ground it grows on"),
                    Field("spreadsOnTilesetIds", "Spreads on tilesets"),
                    Field("minSpreadSeconds", "Min spread seconds"),
                    Field("maxSpreadSeconds", "Max spread seconds"),
                    Field("becomesTilesetId", "The ground it turns into"),
                    Field("placementRules", "Where it may be planted", true),
                    Field("simpleTraits", "The small things it simply is", true),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                EditorGUILayout.HelpBox(
                    "Generating this writes the seed, the growing plant and the ripe plant, plus a " +
                    "seed and a plant for every better version. They are all two objects — a seed " +
                    "and a plant — the way Core Keeper builds its own crops.\n\n" +
                    "A better version's place in the list decides which variation it sits on, so " +
                    "reordering the list moves crops already planted in an existing world onto a " +
                    "different version. Add new ones at the end.\n\n" +
                    "It needs " + plant.PicturesNeeded + " pictures to be visible: one for each of " +
                    "its " + plant.GrowthStages + " growth stages and one for the ripe plant. A " +
                    "better version with pictures of its own is what makes a golden crop look " +
                    "golden; one without looks exactly like the ordinary one.",
                    MessageType.Info);
            }
        }

        private void DrawWorldObjectAssetEditors(DimensionWorldObjectAsset[] worldObjects)
        {
            if (worldObjects == null)
            {
                return;
            }

            for (int i = 0; i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    worldObject,
                    BuildAssetEditorTitle("Object", worldObject.DisplayName, worldObject.ObjectIdentifier, i),
                    Field("displayName", "Display name"),
                    Field("objectIdentifier", "Object ID"),
                    Field("description", "Description"),
                    Field("rarityId", "Rarity"),
                    Field("sprite", "Sprite"),
                    Field("icon", "Icon"),
                    Field("kind", "What it is"),
                    Field("summonsEnemyId", "Trophy summons"),
                    Field("tileSize", "Tile size"),
                    Field("facesPlacementDirection", "Faces placement direction"),
                    Field("canBePlacedOnWater", "Can be placed on water"),
                    Field("paintable", "Paintable"),
                    Field("surfacePriority", "Surface priority"),
                    Field("lightsTheRoomWhenPlaced", "Lights the room when placed"),
                    Field("lightsTheRoomWhenHeld", "Lights the room when held"),
                    Field("heldLightColor", "Held light colour"),
                    Field("heldLightRange", "Held light range"),
                    Field("objectItselfGlows", "The object itself glows"),
                    Field("glowColor", "Glow colour"),
                    Field("glowIntensity", "Glow intensity"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("cannotBeAttacked", "Cannot be attacked"),
                    Field("disappearsAfterSeconds", "Disappears after (seconds)"),
                    Field("effects", "What it does for you"),
                    Field("impactFeedback", "Hitting and breaking it"),
                    Field("tileOutcome", "What it leaves on the tile"),
                    Field("leavesBehind", "What it leaves standing"),
                    Field("continuousAttack", "If it hurts things"),
                    Field("keepsThingsSafeNearby", "Keeps things safe nearby"),
                    Field("safeRadius", "How far the safety reaches"),
                    Field("basics", "Where it sits in the world"),
                    Field("conditions", "Conditions"),
                    Field("initialFacing", "Faces this way when placed"),
                    Field("itsColliderTurnsToo", "Its collider turns too"),
                    Field("textItComesWith", "Text it comes with"),
                    Field("untouchableForOneFrameOnly", "Untouchable for one frame only"),
                    Field("isAFenceGate", "Is a fence gate"),
                    Field("flowerOfPlantId", "Flower of plant"),
                    Field("spawnsEnemyId", "Spawner platform produces"),
                    Field("playersCanTravelToIt", "Players can travel to it"),
                    Field("activateWithin", "Activate within"),
                    Field("isTheCoreWaypoint", "Is the core waypoint"),
                    Field("music", "Music near it"),
                    Field("automation", "Automation"),
                    Field("rules", "How the world treats it"),
                    Field("alwaysDropsLoot", "Creative mode still gives its drops"),
                    Field("secondaryUse", "Right-click"),
                    Field("dropsFrom", "Where it drops from"),
                    Field("wiring", "Wiring"),

                    // ---- what a player does with it ----
                    Field("interaction", "When a player uses it", true),
                    Field("roles", "Its small roles in a base", true),
                    Field("simpleTraits", "The small things it simply is", true),

                    // ---- placing and appearance details ----
                    Field("placementRules", "Where it may be placed", true),
                    Field("rotationIconOffset", "Icon nudge per rotation"),
                    Field("flowerVariation", "Which look its flower is"),
                    Field("adaptsToSurroundings", "It changes with its surroundings", true),

                    // ---- the safety zone's shape ----
                    Field("safeAreaIsRectangular", "The safe area is a rectangle"),
                    Field("safeWidth", "Safe area width"),
                    Field("safeHeight", "Safe area height"),

                    // ---- what it gives and holds ----
                    Field("extraLoot", "Loot besides its drops", true),
                    Field("extractable", "What machines can draw from it", true),
                    Field("trader", "If it buys and sells", true),
                    Field("nest", "If it is a nest", true),

                    // ---- how it behaves ----
                    Field("melodyResponse", "It answers a tune", true),
                    Field("reactsToNearby", "It reacts to someone coming close", true),
                    Field("summoningCircle", "If it summons something", true),
                    Field("poweredMachine", "If power drives it", true),
                    Field("keepsItsFloor", "It lays its own floor", true),
                    Field("machineRoles", "Its machine roles", true),
                    Field("terrainEffects", "How it changes the ground", true),
                    Field("chainReaction", "If it sets off its neighbours", true),
                    Field("spawnerAndOrb", "If it spawns things", true),
                    Field("manaAndAura", "Mana and auras", true),
                    Field("hidingAndHatching", "Hiding and hatching", true),
                    Field("beamAndAmbience", "Beams and ambience", true),
                    Field("eventTerminal", "If it runs an event", true),
                    Field("finalTouches", "Final touches", true),

                    // ---- the wider world ----
                    Field("worldRoles", "Its roles in the world", true),
                    Field("nativeWorldPlacement", "Placed once, when a world is made", true),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (worldObject.GlowsButLightsNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This glows but lights nothing. Core Keeper keeps those separate - a torch " +
                        "carries the two lighting components and not the glow - so as authored it " +
                        "will shine in a pitch-black room.",
                        MessageType.Warning);
                }

                if (worldObject.IsATrophyThatSummonsNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This is a trophy with nothing to summon, so using it will do nothing.",
                        MessageType.Warning);
                }

                if (worldObject.CanNeverBeRemoved)
                {
                    EditorGUILayout.HelpBox(
                        "Nothing can attack this and it never disappears, so a player who places " +
                        "one can never take it back.",
                        MessageType.Warning);
                }
            }
        }


        /// <summary>
        /// The projectile editors.
        /// </summary>
        /// <remarks>
        /// The ordinary questions first and the exotica after, because that is what vanilla use
        /// looks like: 41 of the game's 71 projectiles are a plain shot with a 0.2 hit radius and
        /// nothing else ticked. Putting all 22 of the component's fields on one flat list would bury
        /// the one that matters.
        /// </remarks>

        /// <summary>
        /// The explosion editors.
        /// </summary>
        /// <remarks>
        /// Three numbers and the ordinary object spine — that really is all an explosion is. The
        /// terrain damage is worth its label: it is measured against the mining curve rather than
        /// health, so the vanilla values that dig are 165 and 210 rather than anything health-like.
        /// </remarks>
        private void DrawExplosionAssetEditors(DimensionExplosionAsset[] explosions)
        {
            if (explosions == null)
            {
                return;
            }

            for (int i = 0; i < explosions.Length; i++)
            {
                DimensionExplosionAsset explosion = explosions[i];
                if (explosion == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    explosion,
                    BuildAssetEditorTitle("Explosion", explosion.DisplayName, explosion.ExplosionId, i),
                    Field("displayName", "Display name"),
                    Field("explosionId", "Explosion ID"),
                    Field("sprite", "Sprite"),
                    Field("lifetimeSeconds", "Lasts (seconds)"),
                    Field("radius", "How far it reaches"),
                    Field("leavesBehind", "What it leaves burning"),
                    Field("feedback", "What it sounds and looks like"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (explosion.ReachesNothing)
                {
                    EditorGUILayout.HelpBox(
                        "The reach is zero, so this catches nothing. Vanilla runs from 1 to 4.5, " +
                        "and an ordinary bomb's blast reaches 2.",
                        MessageType.Warning);
                }

                if (explosion.NeverGoesAway)
                {
                    EditorGUILayout.HelpBox(
                        "This has no lifetime, so it stays where it went off, doing its damage, " +
                        "for as long as the world is loaded.",
                        MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    "How much a blast hurts and how much terrain it breaks are set on whatever " +
                    "sets it off, not here: the game writes those two numbers over the blast's " +
                    "own every time one goes off.",
                    MessageType.Info);
            }
        }

        private void DrawProjectileAssetEditors(DimensionProjectileAsset[] projectiles)
        {
            if (projectiles == null)
            {
                return;
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                DimensionProjectileAsset projectile = projectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    projectile,
                    BuildAssetEditorTitle("Projectile", projectile.DisplayName, projectile.ProjectileId, i),
                    Field("displayName", "Display name"),
                    Field("projectileId", "Projectile ID"),
                    Field("sprite", "Sprite"),
                    Field("speed", "Speed"),
                    Field("lifetimeSeconds", "Lifetime (seconds)"),
                    Field("hitRadius", "Hit radius"),
                    Field("goesThroughEnemies", "Goes through enemies"),
                    Field("explodesOnEnemies", "Bursts on enemies"),
                    Field("damagesTerrain", "Damages terrain"),
                    Field("terrainHitRadius", "Terrain hit radius"),
                    Field("bounces", "Bounces off walls"),
                    Field("flight", "How it travels"),
                    Field("useTheGamesOwnTimings", "Use the game's own arc timings"),
                    Field("goUpSeconds", "Seconds going up"),
                    Field("airSeconds", "Seconds in the air"),
                    Field("goDownSeconds", "Seconds coming down"),
                    Field("explodeSeconds", "Seconds before it goes off"),
                    Field("breaksTerrainWhereItLands", "Breaks terrain where it lands"),
                    Field("isMagic", "Counts as magic"),
                    Field("ignoresTheDamageCap", "Ignores the damage cap"),
                    Field("onlyLandsWhereItCanSee", "Only lands in sight"),
                    Field("scattersTilesOnTheWayDown", "Scatters tiles on the way down"),
                    Field("scatteredTilesetId", "Tileset it scatters"),
                    Field("scatterExtraRadius", "Extra scatter radius"),
                    Field("leavesATileWhereItLands", "Leaves a tile where it lands"),
                    Field("landedTilesetId", "Tileset it leaves"),
                    Field("sounds", "What it sounds like"),
                    Field("survivesCollision", "Survives collision"),
                    Field("canBeShotDown", "Can be shot down"),
                    Field("weaves", "Weaves as it flies"),
                    Field("stopsOnUnwalkableTiles", "Stops on unwalkable tiles"),
                    Field("dodgingDoesNotSaveYou", "A dodge still counts as a hit"),
                    Field("shards", "Breaks into"),
                    Field("shardObjectId", "Breaks into what"),
                    Field("shattersOnCollision", "It shatters when it hits"),

                    // ---- flight details ----
                    Field("outAndBackSeconds", "Seconds out before it comes back"),
                    Field("speedFollowsACurve", "Its speed follows a curve"),
                    Field("speedCurve", "That curve"),
                    Field("secondSpeedCurve", "A second curve, blended in"),
                    Field("mayExplodeOnAPartialWindUp", "May go off on a partial wind-up"),
                    Field("onlyHitsTheSameThingEvery", "Only hits the same thing every (seconds)"),
                    Field("fliesThroughWallTypes", "Wall types it flies through", true),
                    Field("clientPredictsIt", "The player's own game predicts it"),

                    // ---- what it does to the ground ----
                    Field("scatteredTileType", "What kind of tile it scatters"),
                    Field("landedTileType", "What kind of tile it leaves"),
                    Field("canPlaceTilesOnWaterAndPits", "Its tiles may land on water and pits"),
                    Field("removesTilesWhereItLands", "It removes tiles where it lands"),
                    Field("removedTilesetId", "Which tileset it removes"),
                    Field("removedTileType", "Which kind of tile it removes"),
                    Field("raggedEdges", "Its craters have ragged edges"),
                    Field("wallsItBreaksDropNothing", "Walls it breaks drop nothing"),

                    // ---- what it does to whoever it hits ----
                    Field("pushesWhatItHits", "How hard it pushes what it hits"),
                    Field("leavesBehindObjectId", "What it leaves behind"),
                    Field("leavesBehindVariation", "That object's look"),

                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));

                if (projectile.NeverGoesAnywhere)
                {
                    EditorGUILayout.HelpBox(
                        "This has no speed, so it appears where it was fired from and expires " +
                        "without travelling.",
                        MessageType.Warning);
                }

                if (projectile.CannotHitAnything)
                {
                    EditorGUILayout.HelpBox(
                        "The hit radius is zero, so this passes through everything. Most of the " +
                        "game uses " + DimensionProjectileAsset.OrdinaryHitRadius + ".",
                        MessageType.Warning);
                }

                if (projectile.ShattersIntoNothing)
                {
                    EditorGUILayout.HelpBox(
                        "This is set to break into pieces without saying what of.",
                        MessageType.Warning);
                }

                if (projectile.DamagesTerrainOverNoArea)
                {
                    EditorGUILayout.HelpBox(
                        "This damages terrain over an area of zero, so the terrain damage can " +
                        "never land.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawVehicleAssetEditors(DimensionVehicleAsset[] vehicles)
        {
            if (vehicles == null)
            {
                return;
            }

            for (int i = 0; i < vehicles.Length; i++)
            {
                DimensionVehicleAsset vehicle = vehicles[i];
                if (vehicle == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    vehicle,
                    BuildAssetEditorTitle("Vehicle", vehicle.DisplayName, vehicle.VehicleId, i),
                    Field("displayName", "Display name"),
                    Field("vehicleId", "Vehicle ID"),
                    Field("description", "Description"),
                    Field("kind", "How it moves"),
                    Field("sprite", "Picture"),
                    Field("icon", "Icon"),
                    Field("speedMultiplier", "Speed"),
                    Field("accelerationMultiplier", "Acceleration"),
                    Field("driftingMultiplier", "Drifting"),
                    Field("minecartMaxSpeed", "Minecart top speed"),
                    Field("howCloseToGetOn", "How close to get on"),
                    Field("hitsToBreak", "Hits to break"),
                    Field("dropsItselfWhenBroken", "Breaking gives it back"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }


        /// <summary>
        /// Draws the setups that change the game itself, rather than adding to it.
        /// </summary>
        /// <remarks>
        /// The field list is short on purpose. Everything else the asset holds is drawn below it by
        /// the catch-all, so nothing here can quietly become unreachable the way whole features
        /// have before.
        /// </remarks>

        /// <summary>Draws the stat effects a mod invented.</summary>
        private void DrawConditionAssetEditors(DimensionConditionAsset[] conditions)
        {
            if (conditions == null)
            {
                return;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                DimensionConditionAsset condition = conditions[i];
                if (condition == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    condition,
                    "Condition: " + condition.DisplayName,
                    Field("conditionName", "Id"),
                    Field("displayName", "Called"),
                    Field("enabled", "Generated"),
                    Field("effect", "What it does"));
            }
        }
        private void DrawGameSetupAssetEditors(DimensionGameSetupAsset[] setups)
        {
            if (setups == null)
            {
                return;
            }

            for (int i = 0; i < setups.Length; i++)
            {
                DimensionGameSetupAsset setup = setups[i];
                if (setup == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    setup,
                    "World rules: " + setup.DisplayName,
                    Field("setupIdentifier", "Id"),
                    Field("displayName", "Called"),
                    Field("enabled", "Applied"),
                    Field("upgrading", "What upgrading costs", true),
                    Field("fishing", "What fishing catches", true),
                    Field("talents", "What talents give", true),
                    Field("player", "Overrides on the player", true));
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
                    Field("isFlying", "Flies"),
                    Field("spawnContinuously", "Keeps appearing"),
                    Field("isPersistent", "Survives being left behind"),
                    Field("allowLargerAmount", "More may gather than usual"),
                    Field("canBeCaught", "Can be caught"),
                    Field("home", "Picks its home by"),
                    Field("tilesetIds", "Grounds it lives on"),
                    Field("enabled", "Enabled"),
                    Field("notes", "Notes"));
            }
        }

        /// <summary>
        /// Draws every authored dungeon.
        /// </summary>
        /// <remarks>
        /// Dungeons and quests were both built, generated and tested, and neither had a panel — the
        /// authoring-surface audit found them with no editor at all. Everything a creator could set
        /// on one was reachable only by selecting the raw asset in the Project window, which is the
        /// exact thing this framework exists to avoid.
        /// </remarks>
        private void DrawDungeonAssetEditors(DimensionDungeonAsset[] dungeons)
        {
            if (dungeons == null)
            {
                return;
            }

            for (int i = 0; i < dungeons.Length; i++)
            {
                DimensionDungeonAsset dungeon = dungeons[i];
                if (dungeon == null)
                {
                    continue;
                }

                DrawSerializedAsset(
                    dungeon,
                    BuildAssetEditorTitle("Dungeon", dungeon.DisplayName, dungeon.DungeonId, i),
                    Field("displayName", "Display name"),
                    Field("dungeonId", "Dungeon ID"),
                    Field("biomeId", "Biome it appears in"),
                    Field("radius", "How far out it can appear"),
                    Field("minDistanceFromCentre", "Never closer to the centre than"),
                    Field("spawnChance", "Chance it appears"),
                    Field("roomGroups", "Rooms it is built from"),
                    Field("roomSize", "Room size"),
                    Field("pathSize", "Corridor width"),
                    Field("blockOtherSpawns", "Nothing else spawns inside it"),
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
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("eliteVariant", "Elite variant"),
                    Field("spawnsInWorld", "Spawns in the world"),
                    Field("spawnChance", "Spawn chance"),
                    Field("spawnAmount", "Spawn amount"),
                    Field("spawnsInGroups", "Spawns in groups"),
                    Field("canSpawnInBlockedArea", "May spawn in blocked areas"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("lootTable", "Loot table"),
                    Field("extraLoot", "Loot besides its drops", true),
                    Field("pet", "As a pet", true),
                    Field("simpleTraits", "The small things it simply is", true),
                    Field("aggression", "Aggression"),
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
                    Field("creatureStats", "Stats (every number, exactly as typed)"),
                    Field("combat", "Combat and behaviour"),
                    Field("aggression", "Temperament"),
                    Field("bossChest", "Chest it leaves behind"),
                    Field("visual", "Visual"),
                    Field("audio", "Audio"),
                    Field("mapPin", "Map pin"),
                    Field("fightMusic", "Fight music"),
                    Field("lootTable", "Loot table"),
                    Field("phases", "Phases"),
                    Field("respawnCooldownMinutes", "Respawn cooldown minutes"),

                    // ---- kits borrowed from the game's own bosses ----
                    Field("borrowedKit", "The Hydra and Slime kits", true),
                    Field("moreBorrowedKits", "The Core, Wall and Scarab kits", true),
                    Field("theRestOfTheKits", "The Bird, Robot, Octopus, Larva, Shaman and Snake kits", true),
                    Field("simpleTraits", "The small things it simply is", true),

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


        /// <summary>
        /// Offers every attack the game itself authored, for a creature to take whole.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS DOOR ONE. An author building a creature should never have to invent a swing from
        /// nothing just because they wanted the Hydra's. Picking one here writes its measured
        /// numbers into the ordinary fields below, where they stay editable — the creature does not
        /// remember it borrowed anything, so nothing is ever silently reverted.
        /// </para>
        /// <para>
        /// The list only appears on assets that have a combat block, which is what makes it a
        /// creature. Drawing it on a workbench would be noise.
        /// </para>
        /// </remarks>
        private void DrawBorrowedAttackPickers(
            UnityEngine.Object target,
            SerializedObject serializedObject)
        {
            SerializedProperty combat = serializedObject.FindProperty("combat");
            if (combat == null || target == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Take an attack from something in the game",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "It arrives complete, with the game's own numbers. Change as much or as little of " +
                "it as you like afterwards — or nothing at all.",
                EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button(
                    "Browse all " + DimensionBorrowedAttacks.All.Length + " with a search box"))
            {
                DimensionBorrowedAttackPickerWindow.Open(target, null, null);
            }

            for (int i = 0; i < DimensionBorrowedAttacks.Kinds.Length; i++)
            {
                string kind = DimensionBorrowedAttacks.Kinds[i];
                DrawOneBorrowedAttackPicker(target, serializedObject, combat, kind, LabelFor(kind));
            }

            EditorGUILayout.EndVertical();
        }


        /// <summary>What a row of the picker is called, in the words an author thinks in.</summary>
        /// <remarks>
        /// The kinds themselves come from the generated table, so a new sort of borrowable thing
        /// appears in the picker on its own. Anything without a phrase here falls back to its own
        /// name, which is readable enough to ship with while a better one is chosen.
        /// </remarks>
        private static string LabelFor(string kind)
        {
            switch (kind)
            {
                case "Melee":
                    return "A close-up swing";
                case "Ranged":
                    return "A ranged shot";
                case "Chase":
                    return "The way it chases";
                case "Wander":
                    return "The way it wanders";
                case "Sounds":
                    return "The sounds it makes fighting";
                case "Charge":
                    return "The way it charges";
                case "Jump":
                    return "Its leaping attack";
                case "Explode":
                    return "The way it explodes";
                case "Ray":
                    return "Its sweeping ray";
                default:
                    return kind;
            }
        }
        /// <summary>One row of the picker: a list of attacks of one kind, and a button.</summary>
        private void DrawOneBorrowedAttackPicker(
            UnityEngine.Object target,
            SerializedObject serializedObject,
            SerializedProperty combat,
            string kind,
            string label)
        {
            DimensionBorrowedAttacks.Preset[] presets = DimensionBorrowedAttacks.OfKind(kind);
            if (presets.Length == 0)
            {
                return;
            }

            string key = target.GetInstanceID() + "/" + kind;
            int chosen;
            if (!borrowedAttackChoices.TryGetValue(key, out chosen))
            {
                chosen = 0;
            }

            // The label a creator reads, not the file name the prefab was saved under. The preset's
            // own Name stays its identity everywhere else; only this list is renamed.
            string[] names = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                names[i] = DimensionBorrowedAttackCatalog.Label(presets[i]);
            }

            EditorGUILayout.BeginHorizontal();
            chosen = EditorGUILayout.Popup(label, Mathf.Clamp(chosen, 0, presets.Length - 1), names);
            borrowedAttackChoices[key] = chosen;

            if (GUILayout.Button("Take it", GUILayout.Width(72f)))
            {
                List<string> lost = new List<string>();
                int written = DimensionBorrowedAttackUtility.Apply(combat, presets[chosen], lost.Add);
                serializedObject.ApplyModifiedProperties();

                if (lost.Count > 0)
                {
                    Debug.LogWarning(
                        "Some of '" + names[chosen] + "' could not be written:\n" +
                        string.Join("\n", lost.ToArray()),
                        target);
                }
                else
                {
                    Debug.Log(
                        "Took '" + names[chosen] + "' — " + written +
                        " values, all of them still editable below.",
                        target);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// A field's label with its help riding along, plus the little circled question mark that
        /// tells an author there IS help before they think to hover.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY AUTHORING FIELD ALREADY CARRIES ITS EXPLANATION — the templates were written with a
        /// plain-words tooltip on every single field, and Unity threads that text onto the
        /// serialized property. What was missing was any visible sign of it: a tooltip nobody knows
        /// exists is documentation nobody reads. So the label itself gains a "?" suffix whenever
        /// help exists, and hovering anywhere on the label shows it.
        /// </para>
        /// <para>
        /// Fields with no tooltip get no mark, deliberately: a "?" that reveals nothing teaches an
        /// author to stop hovering.
        /// </para>
        /// </remarks>
        private static GUIContent LabelWithHelp(string text, SerializedProperty property)
        {
            string help = property == null ? string.Empty : property.tooltip;
            if (string.IsNullOrEmpty(help))
            {
                return new GUIContent(text);
            }

            return new GUIContent(text + "  ⍰", help);
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

            // ---- the first door: borrow one of the game's own attacks ----
            // Only creatures have a combat block, so only creatures are offered this.
            DrawBorrowedAttackPickers(target, serializedObject);

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

                GUIContent label = LabelWithHelp(
                    string.IsNullOrEmpty(field.Label)
                        ? ObjectNames.NicifyVariableName(field.PropertyName)
                        : field.Label,
                    property);
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

            // EVERYTHING THE CURATED LIST DID NOT MENTION.
            //
            // The panels are a first draft: their field lists were written early, from guesses about
            // what an asset would need, and the authoring layer has grown a long way past them. An
            // audit of this found whole features — creature combat, dungeons, quests — built,
            // generated and tested with no way to reach them from the dashboard at all. Maintaining
            // every list by hand against every asset would just reintroduce that gap the next time
            // the authoring layer moves.
            //
            // So a curated list means ORDERING, not permission. Whatever it leaves out is still
            // drawn, below, under its own foldout. Fields get promoted into the lists as the UI is
            // designed properly, and nothing is unreachable in the meantime.
            SerializedProperty remaining = serializedObject.GetIterator();
            bool enterChildren = true;
            List<SerializedProperty> extras = new List<SerializedProperty>();
            while (remaining.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (remaining.propertyPath == "m_Script")
                {
                    continue;
                }

                bool alreadyDrawn = false;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].PropertyName == remaining.propertyPath)
                    {
                        alreadyDrawn = true;
                        break;
                    }
                }

                if (!alreadyDrawn)
                {
                    extras.Add(remaining.Copy());
                }
            }

            if (extras.Count > 0)
            {
                string foldoutKey = target.GetInstanceID() + "/" + title;
                bool expanded;
                if (!uncuratedFieldFoldouts.TryGetValue(foldoutKey, out expanded))
                {
                    expanded = false;
                }

                expanded = EditorGUILayout.Foldout(
                    expanded,
                    "Everything else (" + extras.Count + ")",
                    true);
                uncuratedFieldFoldouts[foldoutKey] = expanded;

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < extras.Count; i++)
                    {
                        EditorGUILayout.PropertyField(
                            extras[i],
                            LabelWithHelp(extras[i].displayName, extras[i]),
                            true);
                    }

                    EditorGUI.indentLevel--;
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
            return selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalItems) +
                    CountAssets(selectedTemplate.GlobalRecipes) +
                    CountAssets(selectedTemplate.GlobalWorkbenches) +
                    CountAssets(selectedTemplate.GlobalLootTables);
        }

        private int CountSpawns()
        {
            return selectedTemplate == null
                ? 0
                : CountAssets(selectedTemplate.GlobalAnimals) +
                    CountAssets(selectedTemplate.GlobalCritters) +
                    CountAssets(selectedTemplate.GlobalMobs) +
                    CountAssets(selectedTemplate.GlobalBosses);
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
            return biome == null ? 0 : CountAssets(biome.GenerationPasses);
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

                count += biome.FloorObjectIds.Length;
                count += biome.WallObjectIds.Length;
                count += biome.OreObjectIds.Length;
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
                    count += CountValues(scene.Triggers);
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
                ((portalAppearanceStudio != null &&
                  portalAppearanceStudio.HasPendingChanges) ||
                 (itemPortalAppearanceStudio != null &&
                  itemPortalAppearanceStudio.HasPendingChanges));
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

            if (!TryResolvePendingStudioChangesForTransition(
                    portalAppearanceStudio,
                    selectedTemplate == null
                        ? null
                        : selectedTemplate.PortalVisualProfile) ||
                !TryResolvePendingStudioChangesForTransition(
                    itemPortalAppearanceStudio,
                    selectedTemplate == null
                        ? null
                        : selectedTemplate.ItemPortalVisualProfile))
            {
                return;
            }

            transition();
        }

        private bool TryResolvePendingStudioChangesForTransition(
            DimensionPortalAppearanceStudio studio,
            DimensionPortalVisualProfileAsset boundProfile)
        {
            if (studio == null || !studio.HasPendingChanges)
            {
                return true;
            }

            bool applyAndLeave = EditorUtility.DisplayDialog(
                "Unsaved Portal Studio changes",
                "Leaving '" + studio.EditingProfileDisplayName +
                "' now will revert its unsaved changes.",
                "Apply and leave",
                "Leave without applying");
            if (applyAndLeave)
            {
                if (!studio.SavePendingChanges(
                        out bool runtimeUpdateRequired,
                        out string saveMessage))
                {
                    SetLastEditorAction(saveMessage, MessageType.Error);
                    Repaint();
                    return false;
                }

                SetLastEditorAction(saveMessage, MessageType.Info);
                if (runtimeUpdateRequired)
                {
                    bool synchronized = TryApplyPortalVisualChanges(out _);
                    studio.NotifyRuntimeSyncResult(
                        boundProfile,
                        synchronized);
                    if (!synchronized)
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!studio.DiscardPendingChanges(
                        out string discardMessage))
                {
                    SetLastEditorAction(discardMessage, MessageType.Error);
                    Repaint();
                    return false;
                }

                if (!string.IsNullOrEmpty(discardMessage))
                {
                    SetLastEditorAction(discardMessage, MessageType.Info);
                }
            }

            return true;
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

            // Readiness feeds the rail's ticks, so the frame is rebuilt from the same pass that
            // recomputes it rather than waiting for the next interaction.
            RefreshShell();
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
