using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The dimension and portal editors, their version tabs and their profiles.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private GUIStyle portalHeaderStyle;

        private GUIStyle portalTabNameStyle;

        private GUIStyle portalTabNameSelStyle;

        private GUIStyle portalTickStyle;

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
                    NameField("ruleId", "Rule ID"),
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
    }
}
