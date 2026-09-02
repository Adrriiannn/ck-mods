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
        /// <para>
        /// THE SETTER DOES NOT TOUCH THE STRIP ITSELF, and that is the whole reason
        /// <see cref="ScheduleActionFeedbackRefresh"/> exists. Five of the twenty-one publish sites
        /// sit inside IMGUI draw methods, which run from the <c>IMGUIContainer</c> that hosts the
        /// legacy body — so writing here rewrote a UITK sibling's display and class list from
        /// inside <c>OnGUI</c>, which is the exact mid-pass mutation the comment on
        /// <c>DrawLegacyStageBodyInner</c> exists to prevent. The value is stored now and the strip
        /// is rebuilt on the next UITK frame instead.
        /// </para>
        /// </remarks>
        private string lastEditorActionMessage
        {
            get { return lastEditorActionMessageValue; }
            set
            {
                lastEditorActionMessageValue = value;
                ScheduleActionFeedbackRefresh();
            }
        }

        /// <summary>Whether that message is news, a warning, or a failure.</summary>
        private MessageType lastEditorActionType
        {
            get { return lastEditorActionTypeValue; }
            set
            {
                lastEditorActionTypeValue = value;
                ScheduleActionFeedbackRefresh();
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
    }
}
