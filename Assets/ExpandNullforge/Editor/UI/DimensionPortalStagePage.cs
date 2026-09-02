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

        /// <summary>
        /// Where this page says something to the creator. Building a portal rule can fail — no
        /// dimension selected, the asset could not be written — and that answer used to go to the
        /// Console, where a creator who has not opened it never sees it.
        /// </summary>
        private readonly System.Action<string, MessageType> report;

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
            System.Action<bool> toggleVersionEnabled,
            System.Action<string, MessageType> reportToCreator)
        {
            this.resolveStudio = resolveStudio;
            this.onStudioResult = onStudioResult;
            this.getVersionTab = getVersionTab;
            this.setVersionTab = setVersionTab;
            this.hasVersion = hasVersion;
            this.versionEnabled = versionEnabled;
            this.toggleVersionEnabled = toggleVersionEnabled;
            report = reportToCreator;
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
