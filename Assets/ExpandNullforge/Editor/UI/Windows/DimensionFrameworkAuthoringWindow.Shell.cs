using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The window's frame: the wordmark bar, the Home landing screen, and the journey rail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This half is UI Toolkit, which is what buys the rounded panels, the bundled typefaces,
    /// the hover transitions and the measured Core Keeper palette. The stage bodies underneath
    /// are still the original IMGUI panels, hosted unchanged inside an <see cref="IMGUIContainer"/>
    /// so that nothing a creator can reach today stops working while the panels are migrated one
    /// at a time.
    /// </para>
    /// <para>
    /// The frame owns navigation and the frame alone: the rail lists
    /// <see cref="DimensionJourney"/>'s fixed stages, so the order never shifts underfoot the way
    /// the old readiness-sorted sidebar did.
    /// </para>
    /// </remarks>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private const string StyleSheetPath =
            "Assets/ExpandNullforge/Editor/UI/DimensionsApi.uss";

        private VisualElement homeScreen;
        private VisualElement journeyScreen;
        private VisualElement railStageHost;
        private VisualElement listHost;
        private Label stageTitleLabel;
        private Label stageMaturityChip;
        private VisualElement diagnosticsRailRow;
        private Label diagnosticsAlertChip;
        private VisualElement actionFeedback;
        private Label actionFeedbackLabel;
        private bool showingHome = true;
        private DimensionBiomeStagePage biomePage;
        private VisualElement biomePageRoot;
        private IMGUIContainer legacyBodyHost;
        private VisualElement catalogPageHost;
        private readonly HashSet<string> showOriginalPanel = new HashSet<string>();
        private Button originalPanelToggle;
        private DimensionIdentityStagePage identityPage;
        private DimensionReviewStagePage reviewPage;
        private DimensionMapStagePage mapPage;
        private DimensionIssuesStagePage issuesPage;
        private DimensionPortalStagePage portalPage;
        private VisualElement portalPageRoot;
        private DimensionBlockStagePage blockPage;
        private VisualElement blockPageRoot;
        private VisualElement issuesPageRoot;
        private VisualElement mapPageRoot;
        private VisualElement reviewPageRoot;
        private VisualElement identityPageRoot;
        private Button continueButton;
        private readonly Dictionary<string, DimensionCollectionStagePage> catalogPages =
            new Dictionary<string, DimensionCollectionStagePage>();
        private readonly Dictionary<string, VisualElement> catalogPageRoots =
            new Dictionary<string, VisualElement>();

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("dim-root");

            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            root.Add(BuildTitleBar());

            homeScreen = BuildHomeScreen();
            journeyScreen = BuildJourneyScreen();
            root.Add(homeScreen);
            root.Add(journeyScreen);

            // A window reopened on a dimension it already had should land back in the journey,
            // not send the creator through the front door again.
            ShowHome(selectedTemplate == null);

            // A recompile rebuilds the frame while the last message is still held. Without this
            // the strip would come back blank and the creator would be told nothing about the
            // action they had just taken.
            RefreshActionFeedback();
        }

        private VisualElement BuildTitleBar()
        {
            VisualElement bar = new VisualElement();
            bar.AddToClassList("dim-titlebar");

            Label wordmark = new Label("DIMENSIONS");
            wordmark.AddToClassList("dim-wordmark");
            bar.Add(wordmark);

            Label accent = new Label("API");
            accent.AddToClassList("dim-wordmark-accent");
            bar.Add(accent);

            VisualElement spacer = new VisualElement();
            spacer.AddToClassList("dim-titlebar-spacer");
            bar.Add(spacer);
            return bar;
        }

        /// <summary>
        /// The strip under the stage's name where the window tells the creator what just happened.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Built once and hidden, rather than added and removed, so that publishing a message can
        /// never reorder the page under a creator's cursor. It sits directly beneath the header
        /// because that is where the eye already is after clicking a button in it, and above the
        /// body so it never scrolls out of sight with the fields.
        /// </para>
        /// <para>
        /// The message is dismissable and nothing dismisses it on a timer: a warning that
        /// disappears by itself is a warning the creator can miss entirely.
        /// </para>
        /// </remarks>
        private VisualElement BuildActionFeedback()
        {
            actionFeedback = new VisualElement();
            actionFeedback.AddToClassList("dim-feedback");
            actionFeedback.style.display = DisplayStyle.None;

            actionFeedbackLabel = new Label(string.Empty);
            actionFeedbackLabel.AddToClassList("dim-feedback-text");
            actionFeedback.Add(actionFeedbackLabel);

            Button dismiss = new Button(DismissActionFeedback) { text = "×" };
            dismiss.AddToClassList("dim-button");
            dismiss.AddToClassList("dim-button-ghost");
            dismiss.AddToClassList("dim-feedback-dismiss");
            dismiss.tooltip = "Put this message away.";
            actionFeedback.Add(dismiss);
            return actionFeedback;
        }

        /// <summary>
        /// Puts the last published message on screen, or takes the strip away when there is none.
        /// </summary>
        /// <remarks>
        /// Called from the setter of <c>lastEditorActionMessage</c>, so every one of the
        /// twenty-one places that publish one arrives here without knowing this exists. Guarded on
        /// the elements being built, because a message can be published while the window is still
        /// opening and before <c>CreateGUI</c> has run.
        /// </remarks>
        /// <summary>Whether a published message is waiting for the strip to be rebuilt.</summary>
        private bool actionFeedbackRefreshQueued;

        /// <summary>
        /// Asks for the strip to be rebuilt on the next UITK frame rather than here and now.
        /// </summary>
        /// <remarks>
        /// <para>
        /// BECAUSE FIVE OF THE PUBLISH SITES ARE INSIDE OnGUI. <c>DrawPortalEditor</c>,
        /// <c>DrawTilesetsEditor</c>, <c>DrawSerializedAsset</c> and the two portal-profile
        /// creators all run from the <c>IMGUIContainer</c> that hosts the legacy body, and the
        /// strip is that container's SIBLING in the UITK tree. Rebuilding it from inside an IMGUI
        /// pass changes an element's <c>display</c> and its class list between that pass's Layout
        /// and Repaint — the same class of mid-pass mutation <c>DrawLegacyStageBodyInner</c> already
        /// defends against by deferring its own read.
        /// </para>
        /// <para>
        /// The flag is not an optimisation: without it a message published on every draw would
        /// queue one callback per frame for as long as the window is open.
        /// </para>
        /// </remarks>
        private void ScheduleActionFeedbackRefresh()
        {
            if (actionFeedback == null || actionFeedbackRefreshQueued)
            {
                // No strip yet means the message was published before CreateGUI built the frame.
                // CreateGUI calls RefreshActionFeedback once at the end for exactly that case, so
                // nothing published this early is lost.
                return;
            }

            actionFeedbackRefreshQueued = true;
            actionFeedback.schedule.Execute(() =>
            {
                actionFeedbackRefreshQueued = false;
                RefreshActionFeedback();
            });
        }

        private void RefreshActionFeedback()
        {
            if (actionFeedback == null || actionFeedbackLabel == null)
            {
                return;
            }

            string message = lastEditorActionMessageValue;
            if (string.IsNullOrEmpty(message))
            {
                actionFeedback.style.display = DisplayStyle.None;
                return;
            }

            actionFeedbackLabel.text = message;
            actionFeedback.RemoveFromClassList("dim-feedback-warn");
            actionFeedback.RemoveFromClassList("dim-feedback-bad");
            if (lastEditorActionTypeValue == MessageType.Warning)
            {
                actionFeedback.AddToClassList("dim-feedback-warn");
            }
            else if (lastEditorActionTypeValue == MessageType.Error)
            {
                actionFeedback.AddToClassList("dim-feedback-bad");
            }

            actionFeedback.style.display = DisplayStyle.Flex;
        }

        private void DismissActionFeedback()
        {
            lastEditorActionMessage = string.Empty;
        }

        /// <summary>
        /// A message from a page that has no asset action to report, said in the same strip.
        /// </summary>
        /// <remarks>
        /// Two pages had nowhere to put a failure and wrote it to the Console instead — where a
        /// creator who has not opened the Console never sees it, and where it looks like a bug in
        /// the framework rather than an answer to what they just clicked.
        /// </remarks>
        private void ReportToCreator(string message, MessageType type)
        {
            lastEditorActionMessage = message ?? string.Empty;
            lastEditorActionType = type;
            Repaint();
        }

        /// <summary>Shows the landing screen, or the journey when <paramref name="home"/> is false.</summary>
        private void ShowHome(bool home)
        {
            showingHome = home;
            if (homeScreen == null || journeyScreen == null)
            {
                return;
            }

            homeScreen.style.display = home ? DisplayStyle.Flex : DisplayStyle.None;
            journeyScreen.style.display = home ? DisplayStyle.None : DisplayStyle.Flex;
            if (home)
            {
                RefreshDimensionList();
                return;
            }

            if (DimensionJourney.IndexOf(activeSectionId) < 0)
            {
                activeSectionId = DimensionJourney.Stages[0].SectionId;
            }

            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
        }

        /// <summary>Rebuilds the frame after the workspace behind it changed.</summary>
        private void RefreshShell()
        {
            if (showingHome)
            {
                RefreshDimensionList();
                return;
            }

            RefreshRail();
            RefreshStageHeader();
            RefreshStageBody();
        }
    }
}
