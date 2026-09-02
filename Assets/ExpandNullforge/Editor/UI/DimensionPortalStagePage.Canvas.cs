using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The canvas island and the toolbar above it.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
        private VisualElement BuildCanvasCard()
        {
            // The picture needs no caption: it is the largest thing on the page and it is a
            // portal. The card frame stays so the canvas sits in the same chrome as everything
            // else; only the words above it are gone.
            VisualElement group = new VisualElement();
            group.AddToClassList("dim-group");

            VisualElement groupBody = new VisualElement();
            groupBody.AddToClassList("dim-group-body");
            group.Add(groupBody);

            // No fixed height: the island reports its measured content height and the
            // container shrink-wraps it. Any fixed number here is either too small (content
            // clips) or too large (the container background shows as a dead band below the
            // playbar) — and it was the latter, twice, before this comment.
            canvasHost = new IMGUIContainer(DrawCanvas);
            canvasHost.AddToClassList("dim-canvas-island");
            groupBody.Add(canvasHost);
            return group;
        }

        private void DrawCanvas()
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || template == null)
            {
                return;
            }

            float width = canvasHost.resolvedStyle.width;
            // The canvas area is an INPUT to the island, never read back from the container —
            // deriving it from the container's own size is the circle that produced the band.
            const float canvasAreaHeight = 560f;
            Color previousBackground;
            Color previousContent;
            DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
            try
            {
                studio.DrawCanvasIsland(
                    template,
                    profile,
                    width > 1f ? width : 460f,
                    canvasAreaHeight);
            }
            finally
            {
                DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
            }

            DimensionPortalAppearanceStudio.DrawResult result = studio.ConsumeCanvasResult();
            if (onStudioResult != null)
            {
                onStudioResult(result);
            }

            // The preset strip under the canvas can switch which saved look is being edited. The
            // settings on the right have to follow it, or they would quietly edit the other one.
            if (result.EditingProfile != null && result.EditingProfile != profile)
            {
                profile = result.EditingProfile;
                DeferredRefresh();
            }
        }

        private void RebuildToolbar()
        {
            if (toolbarHost == null)
            {
                return;
            }

            toolbarHost.Clear();
            chargingTab = null;
            activatedTab = null;
            replayOpeningButton = null;
            replayBurstButton = null;
            replayClosingButton = null;
            gridToggle = null;
            guidesToggle = null;
            zoomInButton = null;
            zoomOutButton = null;
            zoomLabel = null;

            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || template == null || profile == null)
            {
                return;
            }

            // Left: what the picture is showing. Two columns — the phase in the first, the
            // moment to replay beside it — so Charging sits over Activated and each replay sits
            // beside the phase it belongs to.
            VisualElement left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;

            if (!studio.InstantPortalMode)
            {
                VisualElement phaseColumn = ToolbarColumn();
                chargingTab = ToolbarTab(
                    "Charging",
                    "Show the portal while it is still charging up.",
                    () => SetPhase(false));
                activatedTab = ToolbarTab(
                    "Activated",
                    "Show the portal once it has finished charging and a player can step through.",
                    () => SetPhase(true));
                phaseColumn.Add(chargingTab);
                phaseColumn.Add(activatedTab);
                left.Add(phaseColumn);

                VisualElement playColumn = ToolbarColumn();
                replayOpeningButton = ToolbarAction(
                    "Play the opening",
                    "Run the moment the portal opens again from its first frame.",
                    () => Act(s => s.ReplayOpening()));
                replayBurstButton = ToolbarAction(
                    "Play the flash",
                    "Fire the burst of light that goes off the instant the portal finishes charging.",
                    () => Act(s => s.ReplayBurst()));
                playColumn.Add(replayOpeningButton);
                playColumn.Add(replayBurstButton);
                left.Add(playColumn);
            }
            else
            {
                VisualElement playColumn = ToolbarColumn();
                replayOpeningButton = ToolbarAction(
                    "Play the opening",
                    "Run the moment the portal opens again from its first frame.",
                    () => Act(s => s.ReplayOpening()));
                replayClosingButton = ToolbarAction(
                    "Play the closing",
                    "Run the moment the portal closes again. Only a portal opened from an item closes.",
                    () => Act(s => s.ReplayClosing()));
                playColumn.Add(replayOpeningButton);
                playColumn.Add(replayClosingButton);
                left.Add(playColumn);
            }

            toolbarHost.Add(left);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbarHost.Add(spacer);

            // Right: how the picture is shown. View toggles above, zoom below.
            VisualElement right = new VisualElement();
            right.style.flexDirection = FlexDirection.Column;
            right.style.alignItems = Align.FlexEnd;

            VisualElement viewRow = new VisualElement();
            viewRow.style.flexDirection = FlexDirection.Row;
            gridToggle = ToolbarTab(
                "Grid",
                "Lay a grid of single pixels over the picture, so you can see exactly where art lands.",
                () => Act(s => s.ShowGrid = !s.ShowGrid));
            guidesToggle = ToolbarTab(
                "Guides",
                "Show the middle line and the pivot of the selected part, so you can line things up.",
                () => Act(s => s.ShowGuides = !s.ShowGuides));
            viewRow.Add(gridToggle);
            viewRow.Add(guidesToggle);
            right.Add(viewRow);

            VisualElement zoomRow = new VisualElement();
            zoomRow.style.flexDirection = FlexDirection.Row;
            zoomRow.style.alignItems = Align.Center;
            zoomRow.style.marginTop = 4;
            zoomOutButton = ToolbarTab(
                "−",
                "Look at the portal from further back.",
                () => Act(s => s.TryAdjustPreviewZoom(-1)));
            zoomLabel = new Label(string.Empty);
            zoomLabel.AddToClassList("dim-portal-zoom");
            zoomInButton = ToolbarTab(
                "+",
                "Look at the portal closer up, down to a single pixel. You can also scroll the " +
                "wheel over the picture, drag with the middle mouse button to move around, and " +
                "double middle click to come back to the portal.",
                () => Act(s => s.TryAdjustPreviewZoom(1)));
            zoomRow.Add(zoomOutButton);
            zoomRow.Add(zoomLabel);
            zoomRow.Add(zoomInButton);
            right.Add(zoomRow);

            toolbarHost.Add(right);

            SyncToolbarState();
        }

        /// <summary>A column of toolbar controls whose buttons stretch to one width.</summary>
        private static VisualElement ToolbarColumn()
        {
            VisualElement column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.marginRight = 6;
            return column;
        }

        /// <summary>
        /// A tab in the strip above the picture. Which one is current is decided by the studio
        /// rather than by the last click here, so it is set afterwards by
        /// <see cref="SyncToolbarState"/>.
        /// </summary>
        private static Button ToolbarTab(string text, string tooltip, System.Action action)
        {
            return DimensionsApiControls.Tab(text, tooltip, false, action);
        }

        private static Button ToolbarAction(string text, string tooltip, System.Action action)
        {
            Button button = DimensionsApiControls.GhostButton(text, action);
            button.AddToClassList("dim-portal-toolbar-action");
            button.tooltip = tooltip;
            return button;
        }

        private static VisualElement ToolbarGap()
        {
            VisualElement gap = new VisualElement();
            gap.AddToClassList("dim-portal-toolbar-gap");
            return gap;
        }

        private void SetPhase(bool activated)
        {
            Act(s => s.SetPreviewActivated(activated));
        }

        private void Act(System.Action<DimensionPortalAppearanceStudio> action)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null)
            {
                return;
            }

            action(studio);
            SyncToolbarState();
            canvasHost?.MarkDirtyRepaint();
        }

        /// <summary>Keeps the toolbar telling the truth about the studio behind it.</summary>
        private void SyncToolbarState()
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null)
            {
                return;
            }

            bool activated = studio.IsPreviewActivated;
            SetCurrent(chargingTab, !activated);
            SetCurrent(activatedTab, activated);
            SetCurrent(gridToggle, studio.ShowGrid);
            SetCurrent(guidesToggle, studio.ShowGuides);

            replayOpeningButton?.SetEnabled(studio.CanReplayOpening);
            replayBurstButton?.SetEnabled(studio.CanReplayBurst);
            replayClosingButton?.SetEnabled(studio.CanReplayClosing);
            zoomInButton?.SetEnabled(studio.CanZoomPreviewIn);
            zoomOutButton?.SetEnabled(studio.CanZoomPreviewOut);
            if (zoomLabel != null)
            {
                zoomLabel.text = studio.PreviewZoomPercent + "%";
            }
        }

        private static void SetCurrent(Button button, bool current)
        {
            if (button == null)
            {
                return;
            }

            if (current)
            {
                button.AddToClassList("dim-tab-current");
            }
            else
            {
                button.RemoveFromClassList("dim-tab-current");
            }
        }
    }
}
