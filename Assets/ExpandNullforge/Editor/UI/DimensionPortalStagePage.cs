using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Portal: what a player sees when the way into your dimension opens, and the studio that
    /// draws it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three columns. The parts of the portal on the left, the portal itself in the middle, and
    /// the settings for whichever part is selected on the right. Only the picture is still drawn
    /// by the old code: it hit tests pixels, drags layers and paints a moving preview every frame,
    /// and there is nothing to gain from redrawing that in another engine. Everything around it
    /// is the product's own chrome, so the studio stops looking like a borrowed inspector.
    /// </para>
    /// <para>
    /// The page never opens a second view of the portal. It edits through the studio's own
    /// <see cref="SerializedObject"/>, so a value typed here and a layer dragged on the canvas
    /// are the same edit, land in the same undo step, and can never disagree.
    /// </para>
    /// </remarks>
    internal sealed partial class DimensionPortalStagePage
    {
        private readonly System.Func<DimensionPortalAppearanceStudio> resolveStudio;
        private readonly System.Action<DimensionPortalAppearanceStudio.DrawResult> onStudioResult;
        private readonly System.Func<int> getVersionTab;
        private readonly System.Action<int> setVersionTab;
        private readonly System.Func<bool, bool> hasVersion;
        private readonly System.Func<bool, bool> versionEnabled;
        private readonly System.Action<bool> toggleVersionEnabled;

        private VisualElement root;
        private VisualElement versionHost;
        private VisualElement listHost;
        private VisualElement toolbarHost;
        private VisualElement settingsHost;
        private IMGUIContainer canvasHost;
        private VisualElement profileHost;

        private Button chargingTab;
        private Button activatedTab;
        private Button replayOpeningButton;
        private Button replayBurstButton;
        private Button replayClosingButton;
        private Button gridToggle;
        private Button guidesToggle;
        private Button zoomOutButton;
        private Button zoomInButton;
        private Label zoomLabel;

        private DimensionTemplateAsset template;
        private DimensionPortalVisualProfileAsset profile;

        /// <summary>
        /// Runs a registration only after a freshly built element has finished binding.
        /// </summary>
        /// <remarks>
        /// Binding a field fires an initial change event as it adopts the property's value.
        /// Handlers registered at build time received those and treated them as edits, which
        /// put the page into a permanent loop: refresh, rebind, "edit", artwork bake,
        /// synchronous texture import, project change, refresh — the cursor flickered busy
        /// forever and clicks landed on controls that had just been replaced. Attaching the
        /// handler once, after the bind has settled, removes the loop structurally: nothing
        /// is suppressed, nothing polls a clock, and every event a handler sees is real.
        /// </remarks>
        private static void AfterBinding(VisualElement element, System.Action register)
        {
            DimensionsApiControls.AfterBinding(element, register);
        }

        /// <summary>
        /// The parts of a portal, in the order a creator meets them: the frame, then what happens
        /// while it charges, then what happens once it is open, then the light it throws.
        /// </summary>
        private static readonly DimensionPortalAppearanceStudio.StudioLayer[] LayerOrder =
        {
            DimensionPortalAppearanceStudio.StudioLayer.Frame,
            DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep,
            DimensionPortalAppearanceStudio.StudioLayer.Milestones,
            DimensionPortalAppearanceStudio.StudioLayer.Center,
            DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks,
            DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst,
            DimensionPortalAppearanceStudio.StudioLayer.GroundLight
        };

        internal DimensionPortalStagePage(
            System.Func<DimensionPortalAppearanceStudio> resolveStudio,
            System.Action<DimensionPortalAppearanceStudio.DrawResult> onStudioResult,
            System.Func<int> getVersionTab,
            System.Action<int> setVersionTab,
            System.Func<bool, bool> hasVersion,
            System.Func<bool, bool> versionEnabled,
            System.Action<bool> toggleVersionEnabled)
        {
            this.resolveStudio = resolveStudio;
            this.onStudioResult = onStudioResult;
            this.getVersionTab = getVersionTab;
            this.setVersionTab = setVersionTab;
            this.hasVersion = hasVersion;
            this.versionEnabled = versionEnabled;
            this.toggleVersionEnabled = toggleVersionEnabled;
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            versionHost = new VisualElement();
            versionHost.style.flexShrink = 0;
            root.Add(versionHost);

            VisualElement columns = new VisualElement();
            columns.AddToClassList("dim-stage-page");
            root.Add(columns);

            listHost = new VisualElement();
            listHost.AddToClassList("dim-list-column");
            columns.Add(listHost);

            VisualElement canvasColumn = new VisualElement();
            canvasColumn.AddToClassList("dim-portal-canvas-column");

            toolbarHost = new VisualElement();
            toolbarHost.style.flexShrink = 0;
            toolbarHost.AddToClassList("dim-portal-toolbar");
            canvasColumn.Add(toolbarHost);
            canvasColumn.Add(BuildCanvasCard());

            profileHost = new VisualElement();
            profileHost.style.flexShrink = 0;
            canvasColumn.Add(profileHost);
            columns.Add(canvasColumn);

            ScrollView settingsScroll = new ScrollView(ScrollViewMode.Vertical);
            settingsScroll.AddToClassList("dim-detail-column");
            settingsScroll.AddToClassList("dim-portal-settings-column");
            settingsHost = new VisualElement();
            settingsHost.AddToClassList("dim-detail");
            settingsScroll.Add(settingsHost);
            columns.Add(settingsScroll);

            RegisterEditNotifications();

            // The studio changes the phase by itself when a layer is chosen, so the toolbar is
            // kept in step with it rather than only redrawn when something is clicked here.
            root.schedule.Execute(SyncToolbarState).Every(200);
            return root;
        }

        /// <summary>Points the page at a dimension's portal and redraws all three columns.</summary>
        internal void Refresh(
            DimensionTemplateAsset dimensionTemplate,
            DimensionPortalVisualProfileAsset portalProfile)
        {
            template = dimensionTemplate;
            profile = portalProfile;

            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null && profile == null)
            {
                profile = studio.ActiveProfile;
            }

            RebuildVersionTabs();
            RebuildLayerList();
            RebuildToolbar();
            RebuildProfileCard();
            RebuildSettings();
        }

        /// <summary>
        /// The two portals a dimension can have: the placed one that charges up, and the one an
        /// item tears open. Each has its own studio, its own profile, and its own prefab, so
        /// this is a page-wide switch rather than another layer in the list.
        /// </summary>
        private void RebuildVersionTabs()
        {
            if (versionHost == null)
            {
                return;
            }

            versionHost.Clear();
            if (template == null || getVersionTab == null)
            {
                return;
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("dim-row");
            row.style.alignItems = Align.Center;

            int current = getVersionTab();
            row.Add(DimensionsApiControls.Tabs(
                new[] { "Placed Portal", "Item Portal" },
                current,
                index =>
                {
                    if (setVersionTab != null)
                    {
                        setVersionTab(index);
                    }
                }));

            bool instant = current == 1;
            if (hasVersion != null && hasVersion(instant))
            {
                bool enabled = versionEnabled != null && versionEnabled(instant);
                bool otherCarries = hasVersion(!instant) &&
                                    versionEnabled != null && versionEnabled(!instant);

                Button include = DimensionsApiControls.GhostButton(
                    enabled ? "Included in the mod" : "Not included",
                    () =>
                    {
                        if (toggleVersionEnabled != null)
                        {
                            toggleVersionEnabled(instant);
                        }

                        DeferredRefresh();
                    });
                include.tooltip = enabled
                    ? (otherCarries
                        ? "This portal ships with the mod. Click to leave it out of the next build."
                        : "This portal ships with the mod, and it is the only way in, so it " +
                          "cannot be left out.")
                    : "This portal stays in your project but out of the next build. Click to " +
                      "include it again.";
                include.SetEnabled(!enabled || otherCarries);
                include.style.marginLeft = 10;
                row.Add(include);
            }

            versionHost.Add(row);
        }

        private DimensionPortalAppearanceStudio Studio
        {
            get { return resolveStudio == null ? null : resolveStudio(); }
        }

        // ------------------------------------------------------------------ left column --

        private void RebuildLayerList()
        {
            if (listHost == null)
            {
                return;
            }

            listHost.Clear();
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null)
            {
                return;
            }

            Label heading = new Label("LAYERS");
            heading.AddToClassList("dim-index");
            heading.AddToClassList("dim-portal-list-heading");
            listHost.Add(heading);

            for (int i = 0; i < LayerOrder.Length; i++)
            {
                DimensionPortalAppearanceStudio.StudioLayer layer = LayerOrder[i];
                if (!studio.IsLayerAvailable(layer))
                {
                    continue;
                }

                listHost.Add(BuildLayerCard(studio, layer));
            }
        }

        private VisualElement BuildLayerCard(
            DimensionPortalAppearanceStudio studio,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");
            if (studio.SelectedLayer == layer)
            {
                card.AddToClassList("dim-item-card-selected");
            }

            Label name = new Label(NameLayer(layer));
            name.AddToClassList("dim-item-card-name");
            name.tooltip = DescribeLayer(layer);
            card.Add(name);

            card.RegisterCallback<MouseDownEvent>(evt =>
            {
                DimensionPortalAppearanceStudio target = Studio;
                if (target != null)
                {
                    target.SelectedLayer = layer;
                }

                evt.StopPropagation();
                DeferredRefresh();
            });

            return card;
        }

        /// <summary>
        /// What each part of the portal is called on this page. The studio has names of its own
        /// for these, but they were written for whoever was building the studio. A creator opens
        /// this page to change how a portal looks, so the parts are named the way that creator
        /// would point at them.
        /// </summary>
        private static string NameLayer(DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    return "Portal Frame";
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    return "Charging Sweep";
                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    return "Charging Nodes";
                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    return "Inner Portal";
                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    return "Inner Portal Swirls";
                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    return "Activation Flash";
                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    return "Portal Glow";
                default:
                    return "Layer";
            }
        }

        /// <summary>What each part of the portal is, said the way a player would say it.</summary>
        private static string DescribeLayer(DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    return "the part around the outside";
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    return "the band of light that circles it while it charges";
                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    return "the marks that fill in as it charges";
                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    return "the part a player steps into";
                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    return "the specks drifting inside it";
                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    return "the flash when it finishes charging";
                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    return "the pool it casts around itself";
                default:
                    return string.Empty;
            }
        }

        // ---------------------------------------------------------------- middle column --

        /// <summary>
        /// The strip under the picture: which portal profile is open, and what to do with it.
        /// </summary>
        /// <remarks>
        /// One dropdown, three buttons, no prose. Choosing a profile in the dropdown opens it AND
        /// makes it the one the mod builds with — there is deliberately no separate "use" step,
        /// because a picker that edits one thing while the build quietly uses another is a trap.
        /// </remarks>
        private void RebuildProfileCard()
        {
            if (profileHost == null)
            {
                return;
            }

            profileHost.Clear();
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || template == null || profile == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyList<DimensionPortalVisualProfileAsset> profiles =
                studio.GetProfiles(template);
            bool busy = studio.IsProfileActionBusy;

            VisualElement strip = new VisualElement();
            strip.AddToClassList("dim-build-bar");
            strip.style.marginBottom = 0;
            strip.style.marginTop = 8;

            System.Collections.Generic.List<string> names =
                new System.Collections.Generic.List<string>();
            int current = 0;
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    names.Add(profiles[i] == null ? "Missing" : profiles[i].name);
                    if (profiles[i] == profile)
                    {
                        current = i;
                    }
                }
            }

            if (names.Count == 0)
            {
                names.Add(profile.name);
            }

            PopupField<string> picker = new PopupField<string>(names, current);
            picker.tooltip = "Which portal this dimension uses. Choosing one opens it for " +
                             "editing and builds the mod with it.";
            picker.RegisterValueChangedCallback(evt =>
            {
                int index = names.IndexOf(evt.newValue);
                if (profiles != null && index >= 0 && index < profiles.Count &&
                    profiles[index] != profile)
                {
                    studio.QueueSwitchProfile(template, profiles[index]);
                }
            });
            picker.SetEnabled(!busy);
            picker.style.flexGrow = 1;
            strip.Add(picker);

            Button save = DimensionsApiControls.PrimaryButton(
                "Save",
                () => studio.QueueSaveProfile(template, profile));
            save.tooltip = "Save this portal and update what the mod builds.";
            save.SetEnabled(!busy);
            strip.Add(save);

            Button copy = DimensionsApiControls.GhostButton(
                "Make a copy",
                () => studio.QueueDuplicateProfile(template, profile));
            copy.tooltip = "Duplicate this portal, so you can try something without losing it.";
            copy.SetEnabled(!busy);
            strip.Add(copy);

            Button fresh = DimensionsApiControls.GhostButton(
                "Add new",
                () => studio.QueueCreateProfile(template));
            fresh.tooltip = "Add a new portal that starts from the game's own.";
            fresh.SetEnabled(!busy);
            strip.Add(fresh);

            profileHost.Add(strip);
        }

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

        // ----------------------------------------------------------------- right column --

        /// <summary>
        /// Anything changed on this page is an edit to the portal, so the studio is told: the
        /// preview rebuilds, and the unsaved work counter stays honest.
        /// </summary>
        private bool editNotificationsAttached;
        private EventCallback<ChangeEvent<bool>> onBoolEdited;
        private EventCallback<ChangeEvent<float>> onFloatEdited;
        private EventCallback<ChangeEvent<int>> onIntEdited;
        private EventCallback<ChangeEvent<string>> onStringEdited;
        private EventCallback<ChangeEvent<Color>> onColorEdited;
        private EventCallback<ChangeEvent<Vector2>> onVectorEdited;
        private EventCallback<ChangeEvent<Object>> onObjectEdited;

        private void RegisterEditNotifications()
        {
            onBoolEdited = evt => NotifyEdited(evt);
            onFloatEdited = evt => NotifyEdited(evt);
            onIntEdited = evt => NotifyEdited(evt);
            onStringEdited = evt => NotifyEdited(evt);
            onColorEdited = evt => NotifyEdited(evt);
            onVectorEdited = evt => NotifyEdited(evt);
            onObjectEdited = evt => NotifyEdited(evt);
            AttachEditNotifications();
        }

        private void AttachEditNotifications()
        {
            if (editNotificationsAttached || settingsHost == null)
            {
                return;
            }

            editNotificationsAttached = true;
            settingsHost.RegisterCallback(onBoolEdited);
            settingsHost.RegisterCallback(onFloatEdited);
            settingsHost.RegisterCallback(onIntEdited);
            settingsHost.RegisterCallback(onStringEdited);
            settingsHost.RegisterCallback(onColorEdited);
            settingsHost.RegisterCallback(onVectorEdited);
            settingsHost.RegisterCallback(onObjectEdited);
        }

        /// <summary>
        /// The bulk edit listeners step out while the column is rebuilt and rebind, and step
        /// back in once it has settled — so the binding chorus never reaches them at all.
        /// </summary>
        private void DetachEditNotificationsDuringRebuild()
        {
            if (!editNotificationsAttached || settingsHost == null)
            {
                return;
            }

            editNotificationsAttached = false;
            settingsHost.UnregisterCallback(onBoolEdited);
            settingsHost.UnregisterCallback(onFloatEdited);
            settingsHost.UnregisterCallback(onIntEdited);
            settingsHost.UnregisterCallback(onStringEdited);
            settingsHost.UnregisterCallback(onColorEdited);
            settingsHost.UnregisterCallback(onVectorEdited);
            settingsHost.UnregisterCallback(onObjectEdited);
            AfterBinding(settingsHost, AttachEditNotifications);
        }

        private void NotifyEdited(EventBase evt)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null)
            {
                return;
            }

            studio.NotifyProfileEdited();
            canvasHost?.MarkDirtyRepaint();
        }

        private void RebuildSettings()
        {
            if (settingsHost == null)
            {
                return;
            }

            DetachEditNotificationsDuringRebuild();
            settingsHost.Clear();
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || template == null)
            {
                settingsHost.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its portal appears here.",
                    null));
                return;
            }

            if (profile == null)
            {
                settingsHost.Add(DimensionsApiControls.EmptyState(
                    "This dimension has no portal yet",
                    "A portal is the way in. The framework prepares one that looks like the game's own, and everything about it can then be changed here.",
                    null));
                return;
            }

            SerializedObject serialized = studio.GetProfileSerializedObject(profile);
            if (serialized == null)
            {
                return;
            }

            DimensionPortalAppearanceStudio.StudioLayer layer = studio.SelectedLayer;
            settingsHost.Add(BuildLooksGroup(studio, serialized, layer));

            VisualElement placement = BuildPlacementGroup(studio, serialized, layer);
            if (placement != null)
            {
                settingsHost.Add(placement);
            }

            VisualElement behaviour = BuildBehaviourGroup(studio, serialized, layer);
            if (behaviour != null)
            {
                settingsHost.Add(behaviour);
            }

            if (layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight)
            {
                settingsHost.Add(BuildShadowGroup(serialized));
            }

            // Portal-wide settings live on the dimension, not on the look, so they sit under
            // the layer cards and never change when a different layer is picked. Both cards
            // were only reachable from the legacy panels before this.
            SerializedObject serializedTemplate = studio.GetTemplateSerializedObject(template);
            if (serializedTemplate != null)
            {
                settingsHost.Add(BuildSoundCard(studio, serializedTemplate));
            }

            AddAccessCards(studio);
        }

        /// <summary>What the portal sounds like: one card per version's own moments.</summary>
        private VisualElement BuildSoundCard(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serializedTemplate)
        {
            VisualElement group = DimensionsApiControls.Group("Sound", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!studio.InstantPortalMode)
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "placedPortalActivationSound",
                    "Activation",
                    "Plays once when the portal finishes charging and lights up. A game sound's " +
                    "name or a Sound Library key. Heard within 8 tiles. Empty is silent.",
                    DimensionSoundPickerWindow.FieldPlacedActivation,
                    false));
                return group;
            }

            SerializedProperty mode = serializedTemplate.FindProperty("instantPortalSoundMode");
            int currentMode = mode == null ? 0 : Mathf.Clamp(mode.intValue, 0, 1);
            body.Add(DimensionsApiControls.Field(
                "Mode",
                "Peak plays one sound as the portal opens and another as it closes. Loop plays " +
                "one sound the whole time it stands open.",
                DimensionsApiControls.Tabs(
                    new[] { "Peak", "Loop" },
                    currentMode,
                    index =>
                    {
                        if (mode != null && mode.intValue != index)
                        {
                            mode.intValue = index;
                            serializedTemplate.ApplyModifiedProperties();
                            NotifyTemplateSoundEdited();
                            DeferredRefresh();
                        }
                    })));

            if (currentMode == 0)
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalActivationSound",
                    "Activation",
                    "Plays once as the portal tears open. A game sound's name or a Sound " +
                    "Library key. Heard within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantActivation,
                    false));
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalDeactivationSound",
                    "Deactivation",
                    "Plays once as the portal winks out. A game sound's name or a Sound " +
                    "Library key. Heard within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantDeactivation,
                    false));
            }
            else
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalLoopSound",
                    "Loop",
                    "Loops while the portal stands open and stops as it closes. Needs a Sound " +
                    "Library clip, because the game's own one-shot sounds cannot loop. Heard " +
                    "within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantLoop,
                    true));
            }

            return group;
        }

        /// <summary>A sound key with a browse button beside it, bound to the dimension.</summary>
        private VisualElement BuildSoundKeyRow(
            SerializedObject serializedTemplate,
            string propertyPath,
            string label,
            string tooltip,
            string pickerFieldId,
            bool clipOnly)
        {
            SerializedProperty property = serializedTemplate.FindProperty(propertyPath);
            if (property == null)
            {
                return new VisualElement();
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            TextField key = new TextField();
            key.BindProperty(property);
            key.style.flexGrow = 1;
            row.Add(key);

            DimensionTemplateAsset pickerTemplate = template;
            Button browse = DimensionsApiControls.GhostButton(
                "Browse",
                () => DimensionSoundPickerWindow.Open(pickerTemplate, pickerFieldId, clipOnly));
            browse.tooltip = "Pick from every sound the game and the Sound Library carry, and " +
                             "hear each one before choosing.";
            browse.style.marginLeft = 6;
            row.Add(browse);

            VisualElement field = DimensionsApiControls.Field(label, tooltip, row);
            // A sound is a dimension edit, not a change to the look: it must not trip the
            // profile's unsaved-work counter, so the event stops here and the template is told.
            AfterBinding(field, () => field.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                NotifyTemplateSoundEdited();
                evt.StopPropagation();
            }));
            return field;
        }

        private void NotifyTemplateSoundEdited()
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null)
            {
                studio.NotifyTemplateEdited();
            }
        }

        private VisualElement BuildLooksGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            VisualElement group = DimensionsApiControls.Group("Appearance", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "frameVisible",
                        "Visible",
                        "Turn this off for a portal with nothing around its middle, the way the one an item opens is drawn."));
                    body.Add(Tint(serialized, "frameTint", "Tint", "A colour laid over the frame artwork. Leave it white to keep the art exactly as it was drawn."));
                    body.Add(Glow(serialized, "frameEmissiveColor", "Glow", "How brightly the frame shines in a dark cave. Brighter than white makes it give off light of its own."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "chargeWaveVisible",
                        "Visible",
                        "The band of light that travels around the portal while it charges up."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "chargeWaveDarkColor", "chargeWaveDeepColor", "chargeWaveMidColor", "chargeWaveBrightColor", "chargeWaveCoreColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core" });
                    body.Add(Tint(serialized, "chargeWaveTint", "Tint", "A colour laid over the whole sweep, on top of the five colours above."));
                    body.Add(Glow(serialized, "chargeWaveEmissiveColor", "Glow", "How brightly the sweep shines in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "milestonesVisible",
                        "Visible",
                        "The pairs of marks that light up one by one and stay lit as the portal charges."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "milestoneDarkColor", "milestoneDeepColor", "milestoneMidColor", "milestoneBrightColor", "milestoneCoreColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core" });
                    body.Add(Tint(serialized, "milestoneTint", "Tint", "A colour laid over every mark, on top of the five colours above."));
                    body.Add(Glow(serialized, "milestoneEmissiveColor", "Glow", "How brightly the marks shine in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerVisible",
                        "Visible",
                        "The ring a player actually steps into. Almost every portal wants this."));
                    AddPalette(
                        body,
                        studio,
                        serialized,
                        layer,
                        new[] { "centerDarkColor", "centerDeepColor", "centerMidColor", "centerBrightColor", "centerCoreColor", "centerHighlightColor" },
                        new[] { "Shadow", "Dark", "Base", "Bright", "Core", "Highlight" });
                    body.Add(Tint(serialized, "centerTint", "Tint", "A colour laid over the whole middle, on top of the colours above."));
                    body.Add(Glow(serialized, "centerEmissiveColor", "Glow", "How brightly the middle shines in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerSwirlVisible",
                        "Visible",
                        "The specks of light that drift around inside an open portal."));
                    body.Add(BuildSwirlOverrideField(serialized));
                    body.Add(Tint(serialized, "centerParticleTint", "Tint", "The colour of the drifting specks. It follows the middle's colours unless you change it."));
                    body.Add(Glow(serialized, "centerSwirlEmissiveColor", "Glow", "How brightly the specks shine in the dark."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "playReadyFlash",
                        "Enabled",
                        "The burst of light that goes off the instant the portal finishes charging."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashFollowsCenterPalette",
                        "Match Inner Portal",
                        "Take the flash colour from the middle of the portal, so the two always agree. Turn it off to pick a colour of your own."));
                    body.Add(Tint(serialized, "readyFlashTint", "Tint", "The colour of the burst, used when it is not matching the middle."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashSprites",
                        "Flash Frames",
                        "The pictures the burst plays through, in order. Leave this alone to use the framework's own burst."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightEnabled",
                        "Enabled",
                        "Whether the portal casts a pool of light onto the ground around it. The picture draws the pool exactly as the game lights it, and the dashed ring while this layer is selected marks where it ends."));
                    body.Add(Tint(serialized, "groundLightColor", "Colour", "The colour of the pool of light on the floor."));
                    body.Add(BuildGroundLightBrightnessRow(serialized));
                    break;
            }

            return group;
        }

        /// <summary>
        /// The swirls have two settings that decide whether the rest of the page applies at all,
        /// so changing this one redraws the column beneath it.
        /// </summary>
        private VisualElement BuildSwirlOverrideField(SerializedObject serialized)
        {
            VisualElement field = DimensionsApiControls.Bound(
                serialized,
                "centerSwirlOverrideVanilla",
                "Custom Artwork",
                "Off, the portal drifts the game's own specks. On, it uses your artwork, and you can move and time them yourself.");
            Toggle toggle = field.Q<Toggle>();
            if (toggle != null)
            {
                AfterBinding(toggle, () =>
                    toggle.RegisterValueChangedCallback(evt => DeferredRefresh()));
            }

            return field;
        }

        /// <summary>
        /// The steady brightness the game settles on: the middle of the dimmest and brightest it
        /// is allowed to go. Read only, because it is worked out rather than chosen.
        /// </summary>
        private VisualElement BuildGroundLightBrightnessRow(SerializedObject serialized)
        {
            Label value = new Label(string.Empty);
            value.AddToClassList("dim-readonly-value");
            SerializedProperty minimum = serialized.FindProperty("groundLightMinimumIntensity");
            SerializedProperty maximum = serialized.FindProperty("groundLightMaximumIntensity");
            System.Action recompute = () =>
            {
                if (minimum == null || maximum == null)
                {
                    return;
                }

                serialized.Update();
                float low = minimum.floatValue;
                float high = Mathf.Max(low, maximum.floatValue);
                value.text = ((low + high) * 0.5f).ToString("0.00");
            };
            recompute();
            // Recomputes only when either bound moves — the page costs nothing at rest.
            if (minimum != null)
            {
                value.TrackPropertyValue(minimum, changed => recompute());
            }

            if (maximum != null)
            {
                value.TrackPropertyValue(maximum, changed => recompute());
            }

            return DimensionsApiControls.Field(
                "Effective Brightness",
                "The steady brightness the flicker settles around, halfway between the dimmest and the brightest below. The game works this out itself, so there is nothing to type.",
                value);
        }

        private VisualElement BuildPlacementGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            if (layer == DimensionPortalAppearanceStudio.StudioLayer.GroundLight)
            {
                return BuildGroundLightPlacementGroup(serialized);
            }

            string offset;
            string scale;
            string rotation;
            if (!TryGetPlacementProperties(layer, out offset, out scale, out rotation))
            {
                return null;
            }

            if (layer == DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks &&
                !IsSwirlOverrideOn(serialized))
            {
                return null;
            }

            VisualElement group = DimensionsApiControls.Group(
                "Position", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                offset,
                "Offset",
                "How far this part sits from the middle of the portal, counted in single pixels. You can also drag it around on the picture."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                scale,
                "Scale",
                "Width and height, where one means the size it was drawn at. Two makes it twice as wide or twice as tall."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                rotation,
                "Rotation",
                "How far round to turn this part, in degrees. Most portal art is drawn facing the player, so this usually stays at zero."));

            if (studio.LayerHasLayout(layer))
            {
                DimensionPortalAppearanceStudio.StudioLayer resetLayer = layer;
                body.Add(DimensionsApiControls.GhostButton(
                    "Revert",
                    () => ResetPlacement(resetLayer)));
            }

            return group;
        }

        /// <summary>The pool of light has a place and a reach, but no size and no turning.</summary>
        private VisualElement BuildGroundLightPlacementGroup(SerializedObject serialized)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Position", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "groundLightOffsetPixels",
                "Offset",
                "How far the pool of light sits from the middle of the portal, counted in single pixels."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "groundLightRange",
                "Radius",
                "How many tiles out the light spreads across the floor. The dashed ring on the picture shows exactly where it stops."));
            return group;
        }

        private void ResetPlacement(DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio == null || profile == null)
            {
                return;
            }

            SerializedObject serialized = studio.GetProfileSerializedObject(profile);
            if (serialized == null)
            {
                return;
            }

            studio.ResetLayerLayout(serialized, layer);
            serialized.ApplyModifiedProperties();
            studio.NotifyProfileEdited();
            DeferredRefresh();
        }

        private VisualElement BuildBehaviourGroup(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Behaviour", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "chargeWaveSpeed",
                        "Sweep Speed",
                        "One is the speed the animation was drawn at. Two runs it twice as fast."));
                    VisualElement chargeTime = BuildChargeDurationField(studio);
                    if (chargeTime != null)
                    {
                        body.Add(chargeTime);
                    }

                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "firstMilestone",
                        "First Node",
                        "How far through charging the bottom pair of marks lights up. A quarter of the way is 0.25."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "secondMilestone",
                        "Second Node",
                        "How far through charging the middle pair lights up. Half way is 0.5."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "thirdMilestone",
                        "Third Node",
                        "How far through charging the top pair lights up. Three quarters of the way is 0.75."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerGlowIntensity",
                        "Highlight Intensity",
                        "How strongly the white highlight burns through the middle of the portal."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    if (!IsSwirlOverrideOn(serialized))
                    {
                        return null;
                    }

                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerSwirlPlaybackSpeed",
                        "Swirl Speed",
                        "One is the speed the animation was drawn at. Two runs it twice as fast."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "centerParticleEmissionMultiplier",
                        "Density",
                        "One is as many specks as the game's own portal drifts. Two is twice as many."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashEmissionMultiplier",
                        "Intensity",
                        "One is the brightness the burst was drawn at. Higher makes the whole cave flare."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "readyFlashSizeMultiplier",
                        "Size",
                        "One is the size the burst was drawn at. Two makes it twice as wide."));
                    break;

                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMinimumIntensity",
                        "Minimum Intensity",
                        "The lowest the pool of light drops to as it flickers."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMaximumIntensity",
                        "Maximum Intensity",
                        "The highest the pool of light rises to as it flickers."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightMovement",
                        "Drift",
                        "Give the pool of light the same restless drift the game's own portals have."));
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundLightCastsShadows",
                        "Cast Shadows",
                        "Let the light throw shadows from whatever stands near the portal. Costs more to draw."));
                    break;

                default:
                    return null;
            }

            return group;
        }

        /// <summary>
        /// How long the placed portal takes to charge lives on the dimension, not on the look, so
        /// it is told to the studio separately and never counted as a change to the artwork.
        /// </summary>
        private VisualElement BuildChargeDurationField(DimensionPortalAppearanceStudio studio)
        {
            if (studio.InstantPortalMode || template == null)
            {
                return null;
            }

            SerializedObject serializedTemplate = studio.GetTemplateSerializedObject(template);
            if (serializedTemplate == null ||
                serializedTemplate.FindProperty("portalActivationChargeSeconds") == null)
            {
                return null;
            }

            VisualElement host = new VisualElement();
            host.Add(DimensionsApiControls.Bound(
                serializedTemplate,
                "portalActivationChargeSeconds",
                "Charge Time",
                "How many seconds a placed portal takes to become ready, counted from the moment it is put down."));
            AfterBinding(host, () => host.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                DimensionPortalAppearanceStudio target = Studio;
                if (target != null)
                {
                    target.NotifyTemplateEdited();
                }

                evt.StopPropagation();
            }));

            return host;
        }

        private VisualElement BuildShadowGroup(SerializedObject serialized)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Shadow", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowEnabled",
                "Enabled",
                "Whether a shadow is drawn on the floor under the portal."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowSprite",
                "Sprite",
                "The picture used for the shadow lying flat on the floor. Leave it empty to use the framework's own."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowCasterSprite",
                "Caster Sprite",
                "The shape used when the shadow has to move with a nearby light. Leave it empty to use the framework's own."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "portalShadowScale",
                "Scale",
                "Width and height of the shadow, where one means the size it was drawn at."));
            return group;
        }

        // ------------------------------------------------------------------- ingredients --

        private VisualElement Tint(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return DimensionsApiControls.Bound(serialized, propertyPath, label, tooltip);
            }

            ColorField field = new ColorField();
            field.BindProperty(property);
            return DimensionsApiControls.Field(label, tooltip, field);
        }

        /// <summary>A glow can be brighter than white, so its picker has to allow that.</summary>
        private VisualElement Glow(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return DimensionsApiControls.Bound(serialized, propertyPath, label, tooltip);
            }

            ColorField field = new ColorField { hdr = true };
            field.BindProperty(property);
            return DimensionsApiControls.Field(label, tooltip, field);
        }

        /// <summary>
        /// The colours that live inside the artwork itself. Changing one has to be painted back
        /// into this portal's own copy of the picture, which is what the studio is told to do.
        /// </summary>
        private void AddPalette(
            VisualElement body,
            DimensionPortalAppearanceStudio studio,
            SerializedObject serialized,
            DimensionPortalAppearanceStudio.StudioLayer layer,
            string[] propertyPaths,
            string[] labels)
        {
            for (int i = 0; i < propertyPaths.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(propertyPaths[i]);
                if (property == null)
                {
                    continue;
                }

                ColorField field = new ColorField();
                field.BindProperty(property);
                DimensionPortalAppearanceStudio.StudioLayer bakeLayer = layer;
                AfterBinding(field, () => field.RegisterValueChangedCallback(evt =>
                {
                    DimensionPortalAppearanceStudio target = Studio;
                    if (target != null)
                    {
                        target.NotifyPaletteColorEdited(bakeLayer);
                    }
                }));
                body.Add(DimensionsApiControls.Field(
                    labels[i],
                    "One of the colours the artwork itself is painted in. Change it and this portal's own copy of the picture is repainted to match.",
                    field));
            }
        }

        private static bool IsSwirlOverrideOn(SerializedObject serialized)
        {
            SerializedProperty property = serialized.FindProperty("centerSwirlOverrideVanilla");
            return property != null && property.boolValue;
        }

        private static bool TryGetPlacementProperties(
            DimensionPortalAppearanceStudio.StudioLayer layer,
            out string offset,
            out string scale,
            out string rotation)
        {
            switch (layer)
            {
                case DimensionPortalAppearanceStudio.StudioLayer.Frame:
                    offset = "frameOffsetPixels";
                    scale = "frameScale";
                    rotation = "frameRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.ChargeSweep:
                    offset = "chargeWaveOffsetPixels";
                    scale = "chargeWaveScale";
                    rotation = "chargeWaveRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.Milestones:
                    offset = "milestoneOffsetPixels";
                    scale = "milestoneScale";
                    rotation = "milestoneRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.Center:
                    offset = "centerOffsetPixels";
                    scale = "centerScale";
                    rotation = "centerRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.InnerFlecks:
                    offset = "centerParticleOffsetPixels";
                    scale = "centerParticleScale";
                    rotation = "centerParticleRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.ReadyBurst:
                    offset = "readyFlashOffsetPixels";
                    scale = "readyFlashScale";
                    rotation = "readyFlashRotationDegrees";
                    return true;
                case DimensionPortalAppearanceStudio.StudioLayer.GroundLight:
                    offset = "groundLightOffsetPixels";
                    scale = string.Empty;
                    rotation = string.Empty;
                    return false;
                default:
                    offset = string.Empty;
                    scale = string.Empty;
                    rotation = string.Empty;
                    return false;
            }
        }

        private void DeferredRefresh()
        {
            if (root == null)
            {
                return;
            }

            root.schedule.Execute(() => Refresh(template, profile));
        }
    }
}
