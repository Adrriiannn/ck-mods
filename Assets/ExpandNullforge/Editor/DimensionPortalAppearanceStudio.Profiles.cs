using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which profile is being edited, saving or discarding it, and the preset bar above it.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        public bool SavePendingChanges(
            out bool runtimeUpdateRequired,
            out string message)
        {
            runtimeUpdateRequired = false;
            message = string.Empty;
            DimensionTemplateAsset template = activeTemplate ?? editingProfileTemplate;
            DimensionPortalVisualProfileAsset profile = activeProfile ?? editingProfile;
            if (template == null || profile == null ||
                !DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    profile))
            {
                message = "The Portal Studio no longer has a valid profile to save.";
                return false;
            }

            if (!FlushPendingFrameTextureUpdate(out message) ||
                !DimensionPortalArtworkEditorUtility.FlushPending(profile, out message) ||
                !DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    profile,
                    out message))
            {
                return false;
            }

            if (!profileEditSession.Commit(out string baselineMessage))
            {
                message = string.IsNullOrEmpty(baselineMessage)
                    ? "The portal was saved, but its editing baseline could not be refreshed."
                    : baselineMessage;
                return false;
            }

            DimensionPortalVisualProfileAsset boundProfile =
                GetBoundProfile(template);
            runtimeUpdateRequired =
                profile == boundProfile ||
                templateSettingsChanged ||
                runtimeOutOfDateProfile == boundProfile;
            templateSettingsChanged = false;
            if (runtimeUpdateRequired)
            {
                runtimeOutOfDateProfile = boundProfile;
            }

            return true;
        }

        public bool DiscardPendingChanges(out string message)
        {
            message = string.Empty;
            CancelPendingTextureUpdate();
            DimensionPortalArtworkEditorUtility.CancelPending(editingProfile);
            if (!profileEditSession.Discard(out message))
            {
                return false;
            }

            editingProfile = profileEditSession.Profile;
            activeProfile = editingProfile;
            templateSettingsChanged = false;
            serializedTemplateCache = null;
            serializedTemplateTarget = null;
            serializedProfileCache = null;
            serializedProfileTarget = null;
            hasPaletteSnapshot = false;
            InvalidateTextureSlotCache();
            ClearPreviewAssetCaches();
            ClearPaletteFocus();
            hasSelectedPixel = false;
            repaintRequested = true;
            return true;
        }

        public void NotifyRuntimeSyncResult(
            DimensionPortalVisualProfileAsset profile,
            bool succeeded)
        {
            if (succeeded)
            {
                runtimeOutOfDateProfile = null;
            }
            else if (profile != null)
            {
                runtimeOutOfDateProfile = profile;
            }

            repaintRequested = true;
        }

        /// <summary>
        /// Tells the studio that something outside its own drawing changed the portal, so the
        /// preview rebuilds and the unsaved-work tracking stays honest.
        /// </summary>
        internal void NotifyProfileEdited()
        {
            DimensionPortalVisualProfileAsset profile = ActiveProfile;
            if (profile != null)
            {
                EditorUtility.SetDirty(profile);
            }

            profileEditSession.MarkChanged();
            previewCompositionDirty = true;
            repaintRequested = true;
        }

        /// <summary>
        /// The same, for a colour that lives in the artwork's palette: those have to be baked
        /// back into the portal's own sprite sheet, exactly as editing them in place does.
        /// </summary>
        internal void NotifyPaletteColorEdited(StudioLayer layer)
        {
            NotifyProfileEdited();
            DimensionTemplateAsset template = ActiveTemplate;
            DimensionPortalVisualProfileAsset profile = ActiveProfile;
            if (template == null || profile == null)
            {
                return;
            }

            if (TryGetArtworkLayer(
                    layer,
                    out DimensionPortalArtworkLayer artworkLayer,
                    instantPortalMode))
            {
                DimensionPortalArtworkEditorUtility.QueuePaletteBake(
                    template,
                    profile,
                    artworkLayer);
            }
            else if (layer == StudioLayer.InnerFlecks)
            {
                DimensionPortalSwirlArtworkEditorUtility.QueueSwirlBake(template, profile);
            }
        }

        /// <summary>
        /// The same, for a setting that lives on a portal access rule.
        /// </summary>
        /// <remarks>
        /// A rule is its own asset, so marking the dimension dirty would not save it. It is still
        /// counted as a dimension-level change, because that is what decides whether saving has to
        /// rebuild the runtime output — and an access rule certainly does.
        /// </remarks>
        internal void NotifyRuleEdited(Authoring.DimensionPortalAccessRuleAsset rule)
        {
            if (rule != null)
            {
                EditorUtility.SetDirty(rule);
            }

            templateSettingsChanged = true;
            repaintRequested = true;
        }

        /// <summary>The same, for a setting that lives on the dimension rather than the portal.</summary>
        internal void NotifyTemplateEdited()
        {
            DimensionTemplateAsset template = ActiveTemplate;
            if (template != null)
            {
                EditorUtility.SetDirty(template);
            }

            templateSettingsChanged = true;
            repaintRequested = true;
        }

        private DimensionPortalVisualProfileAsset ResolveEditingProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset boundProfile)
        {
            bool selectionIsValid =
                template != null &&
                editingProfileTemplate == template &&
                editingProfile != null &&
                DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    editingProfile);
            if (selectionIsValid)
            {
                return editingProfile;
            }

            if (template == null || boundProfile == null)
            {
                editingProfileTemplate = template;
                editingProfile = null;
                return null;
            }

            SelectEditingProfile(template, boundProfile);
            return editingProfile;
        }

        private bool SelectEditingProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            if (template == null || profile == null ||
                !DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    profile))
            {
                pendingArtworkMessage =
                    "The selected portal profile does not belong to this Dimension Asset.";
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            // The host window initializes the profile currently bound to the Dimension
            // Asset, but Portal Studio can browse any package-owned profile without
            // binding it first. Migrate that selected profile before capturing the edit
            // session baseline; otherwise older profiles expose newly-added fields (most
            // visibly the Swirls artwork reference) as their serialized zero/default
            // values until they happen to become the bound profile.
            bool profileMigrated =
                DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    template,
                    profile,
                    out string initializationMessage);
            if (!profileMigrated && !string.IsNullOrEmpty(initializationMessage))
            {
                pendingArtworkMessage = initializationMessage;
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            if (profileMigrated)
            {
                // Migration is framework bookkeeping, not an author edit. Persist it
                // before Begin() snapshots the clean profile state so switching away does
                // not offer to discard the framework-assigned artwork defaults.
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            if (!profileEditSession.Begin(template, profile, out string message))
            {
                pendingArtworkMessage = message;
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            editingProfileTemplate = template;
            editingProfile = profile;
            activeTemplate = template;
            activeProfile = profile;

            serializedProfileCache = null;
            serializedProfileTarget = null;
            paletteSnapshotProfile = null;
            hasPaletteSnapshot = false;
            InvalidateTextureSlotCache();
            ClearPreviewAssetCaches();
            ClearPaletteFocus();
            hasSelectedPixel = false;
            repaintRequested = true;
            return true;
        }

        private void ProcessUndoRedoPaletteChanges(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile)
        {
            if (!hasPaletteSnapshot || paletteSnapshotProfile != profile)
            {
                CapturePaletteSnapshot(profile, serializedProfile);
                undoRedoPaletteCheckRequested = false;
                return;
            }

            if (!undoRedoPaletteCheckRequested)
            {
                return;
            }

            undoRedoPaletteCheckRequested = false;
            int chargeHash = GetPaletteHash(GetPalette(serializedProfile, ChargePaletteProperties));
            int milestoneHash = GetPaletteHash(GetPalette(serializedProfile, MilestonePaletteProperties));
            int centerHash = GetPaletteHash(GetPalette(serializedProfile, CenterPaletteProperties));

            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                DimensionPortalArtworkLayer.ChargeSweep,
                "chargeWaveSpriteAsset",
                chargeHash != chargePaletteSnapshotHash);
            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                DimensionPortalArtworkLayer.Milestones,
                "milestoneSpriteAsset",
                milestoneHash != milestonePaletteSnapshotHash);
            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                CenterArtworkLayer,
                "centerEffectSpriteAsset",
                centerHash != centerPaletteSnapshotHash);
        }

        private static void QueueManagedPaletteAfterUndoRedo(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile,
            DimensionPortalArtworkLayer layer,
            string referencePropertyName,
            bool paletteChanged)
        {
            if (!paletteChanged || template == null || profile == null || serializedProfile == null)
            {
                return;
            }

            SerializedProperty reference = serializedProfile.FindProperty(referencePropertyName);
            if (DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    profile,
                    layer,
                    out _) == DimensionPortalArtworkReferenceKind.Managed)
            {
                DimensionPortalArtworkEditorUtility.QueuePaletteBake(
                    template,
                    profile,
                    layer);
            }
        }

        private void CapturePaletteSnapshot(
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile)
        {
            if (profile == null || serializedProfile == null)
            {
                paletteSnapshotProfile = null;
                hasPaletteSnapshot = false;
                return;
            }

            paletteSnapshotProfile = profile;
            chargePaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, ChargePaletteProperties));
            milestonePaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, MilestonePaletteProperties));
            centerPaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, CenterPaletteProperties));
            hasPaletteSnapshot = true;
        }

        private void DrawPresetToolbar(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            bool canApply)
        {
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets =
                DimensionPortalPresetEditorUtility.GetPresets(template);
            EnsurePresetLabels(presets);

            int selectedPreset = FindPresetIndex(presets, profile);
            bool busy = presetActionQueued ||
                        EditorApplication.isCompiling ||
                        EditorApplication.isUpdating;
            EditorGUI.BeginDisabledGroup(busy);
            int nextPreset = Mathf.Max(0, selectedPreset);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginDisabledGroup(presets == null || presets.Count == 0);
            nextPreset = EditorGUILayout.Popup(
                Mathf.Max(0, selectedPreset),
                presetLabelCache,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(100f),
                GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            Color previousBackground = GUI.backgroundColor;
            bool saveNeedsAttention =
                profileEditSession.HasChanges ||
                (profile == GetBoundProfile(template) &&
                 runtimeOutOfDateProfile == profile);
            if (saveNeedsAttention)
            {
                // Blue attention tint keeps the whole Portal Studio in one palette (orange now
                // signals the Tileset Studio) while still standing out from the grey neighbours.
                GUI.backgroundColor = new Color(0.26f, 0.67f, 0.95f, 1f);
            }

            if (GUILayout.Button(
                    new GUIContent(
                        "Save & Update",
                        profile == GetBoundProfile(template)
                            ? "Save this profile and update the portal generated for the mod."
                            : "Save this profile without changing the profile used by the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(104f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Save,
                    template,
                    profile);
            }
            GUI.backgroundColor = previousBackground;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(
                    new GUIContent(
                        "New profile",
                        "Create and open a new vanilla-based portal profile without using it in the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(88f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Create,
                    template,
                    null);
            }
            if (GUILayout.Button(
                    new GUIContent(
                        "Duplicate profile",
                        "Create and open a complete copy of this profile without using it in the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(112f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Duplicate,
                    template,
                    profile);
            }
            GUILayout.FlexibleSpace();
            bool profileInUse = profile == GetBoundProfile(template);
            EditorGUI.BeginDisabledGroup(!canApply || profileInUse);
            if (GUILayout.Button(
                    new GUIContent(
                        profileInUse ? "Profile in use" : "Use profile",
                        profileInUse
                            ? "This is the profile currently used by the mod."
                            : "Save this profile, use it for the mod, and regenerate the portal output."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(94f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Use,
                    template,
                    profile);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (presets != null &&
                nextPreset >= 0 &&
                nextPreset < presets.Count &&
                nextPreset != selectedPreset)
            {
                QueuePresetAction(
                    PendingPresetAction.Browse,
                    template,
                    presets[nextPreset]);
            }
        }

        private static string GetPresetDisplayName(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return "Portal";
            }

            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out DimensionPortalPackageAsset package,
                    out _) &&
                package != null &&
                !string.IsNullOrWhiteSpace(package.DisplayName))
            {
                return package.DisplayName.Trim();
            }

            return string.IsNullOrWhiteSpace(profile.name) ? "Portal" : profile.name;
        }

        private void EnsurePresetLabels(
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets)
        {
            if (ReferenceEquals(presetListCache, presets) &&
                presetLabelCache != null &&
                presetLabelCache.Length == (presets == null ? 0 : presets.Count))
            {
                return;
            }

            presetListCache = presets;
            int count = presets == null ? 0 : presets.Count;
            presetLabelCache = new string[count];
            for (int i = 0; i < count; i++)
            {
                DimensionPortalVisualProfileAsset preset = presets[i];
                presetLabelCache[i] = preset == null
                    ? "Missing portal"
                    : GetPresetDisplayName(preset);
            }
        }

        private static int FindPresetIndex(
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets,
            DimensionPortalVisualProfileAsset profile)
        {
            if (presets == null)
            {
                return -1;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == profile)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Every saved look this dimension can choose between.</summary>
        internal IReadOnlyList<DimensionPortalVisualProfileAsset> GetProfiles(
            DimensionTemplateAsset template)
        {
            return DimensionPortalPresetEditorUtility.GetPresets(template);
        }

        /// <summary>True while an earlier request is still being carried out.</summary>
        internal bool IsProfileActionBusy
        {
            get
            {
                return presetActionQueued ||
                       EditorApplication.isCompiling ||
                       EditorApplication.isUpdating;
            }
        }

        internal void QueueSaveProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            QueuePresetAction(PendingPresetAction.Save, template, profile);
        }

        internal void QueueCreateProfile(DimensionTemplateAsset template)
        {
            QueuePresetAction(PendingPresetAction.Create, template, null);
        }

        internal void QueueDuplicateProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            QueuePresetAction(PendingPresetAction.Duplicate, template, profile);
        }

        /// <summary>Opens a profile for editing and makes it the one the mod builds with.</summary>
        internal void QueueSwitchProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            QueuePresetAction(PendingPresetAction.Switch, template, profile);
        }

        private void QueuePresetAction(
            PendingPresetAction action,
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset target)
        {
            if (presetActionQueued || template == null || action == PendingPresetAction.None)
            {
                return;
            }

            pendingPresetAction = action;
            pendingPresetTemplate = template;
            pendingPresetTarget = target;
            pendingPresetBindingProfile = activeProfile;
            pendingPresetTemplateIdentity = CaptureAssetIdentity(template);
            pendingPresetTargetIdentity = CaptureAssetIdentity(target);
            pendingPresetBindingProfileIdentity = CaptureAssetIdentity(activeProfile);
            presetActionQueued = true;
            EditorApplication.delayCall -= ProcessPendingPresetAction;
            EditorApplication.delayCall += ProcessPendingPresetAction;
        }

        private void ProcessPendingPresetAction()
        {
            EditorApplication.delayCall -= ProcessPendingPresetAction;
            PendingPresetAction action = pendingPresetAction;
            DimensionTemplateAsset template = pendingPresetTemplate;
            DimensionPortalVisualProfileAsset target = pendingPresetTarget;
            DimensionPortalVisualProfileAsset bindingProfile =
                pendingPresetBindingProfile;
            AssetIdentity templateIdentity = pendingPresetTemplateIdentity;
            AssetIdentity targetIdentity = pendingPresetTargetIdentity;
            AssetIdentity bindingProfileIdentity =
                pendingPresetBindingProfileIdentity;
            presetActionQueued = false;
            pendingPresetAction = PendingPresetAction.None;
            pendingPresetTemplate = null;
            pendingPresetTarget = null;
            pendingPresetBindingProfile = null;
            pendingPresetTemplateIdentity = default(AssetIdentity);
            pendingPresetTargetIdentity = default(AssetIdentity);
            pendingPresetBindingProfileIdentity = default(AssetIdentity);
            if (disposed || template == null || action == PendingPresetAction.None)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        template,
                        templateIdentity,
                        bindingProfile,
                        bindingProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending portal preset action was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (target != null && !MatchesAssetIdentity(target, targetIdentity))
                {
                    pendingArtworkMessage =
                        "The pending portal preset action was cancelled because its target preset changed or was replaced.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                bool leavesEditingProfile =
                    action == PendingPresetAction.Browse ||
                    action == PendingPresetAction.Create ||
                    action == PendingPresetAction.Duplicate;
                if (leavesEditingProfile &&
                    !ResolvePendingProfileChangesBeforeLeave(out string transitionMessage))
                {
                    pendingArtworkMessage = transitionMessage;
                    pendingArtworkMessageType = MessageType.Error;
                    repaintRequested = true;
                    return;
                }

                bool succeeded;
                string message;
                DimensionPortalVisualProfileAsset created = null;
                switch (action)
                {
                    case PendingPresetAction.Save:
                        succeeded = SavePendingChanges(
                            out bool saveRuntimeUpdate,
                            out message);
                        if (succeeded && saveRuntimeUpdate)
                        {
                            runtimeSyncRequested = true;
                        }
                        break;
                    case PendingPresetAction.Duplicate:
                        succeeded = DimensionPortalPresetEditorUtility.DuplicateProfile(
                            template,
                            target,
                            out created,
                            out message);
                        if (succeeded)
                        {
                            succeeded = SelectEditingProfile(template, created);
                        }
                        break;
                    case PendingPresetAction.Create:
                        succeeded = DimensionPortalPresetEditorUtility.CreateVanilla(
                            template,
                            out created,
                            out message);
                        if (succeeded)
                        {
                            succeeded = SelectEditingProfile(template, created);
                        }
                        break;
                    case PendingPresetAction.Browse:
                        succeeded = SelectEditingProfile(template, target);
                        message = succeeded
                            ? "Opened portal profile '" +
                              GetPresetDisplayName(target) + "'."
                            : pendingArtworkMessage;
                        break;
                    case PendingPresetAction.Use:
                        succeeded = SavePendingChanges(
                            out _,
                            out message);
                        if (succeeded)
                        {
                            runtimeOutOfDateProfile = target;
                            useProfileRequested = target;
                        }
                        break;
                    case PendingPresetAction.Switch:
                        // Work on the look being left behind is saved first, so switching away
                        // can never quietly discard an afternoon's edits.
                        succeeded = SavePendingChanges(
                            out _,
                            out message);
                        if (succeeded)
                        {
                            succeeded = SelectEditingProfile(template, target);
                        }

                        if (succeeded)
                        {
                            runtimeOutOfDateProfile = target;
                            useProfileRequested = target;
                            message = "Now editing and using portal profile '" +
                                      GetPresetDisplayName(target) + "'.";
                        }
                        break;
                    default:
                        succeeded = false;
                        message = "Unknown Portal Studio preset action.";
                        break;
                }

                pendingArtworkMessage = message;
                pendingArtworkMessageType = succeeded
                    ? MessageType.Info
                    : MessageType.Error;
                if (succeeded)
                {
                    DimensionPortalPresetEditorUtility.Invalidate(template);
                    presetListCache = null;
                    presetLabelCache = Array.Empty<string>();
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    hasPaletteSnapshot = false;
                    InvalidateTextureSlotCache();
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                    activeTemplate = template;
                    activeProfile = editingProfile;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage = "Portal preset operation failed: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private bool ResolvePendingProfileChangesBeforeLeave(out string message)
        {
            message = string.Empty;
            if (!profileEditSession.HasChanges)
            {
                return true;
            }

            bool applyAndLeave = EditorUtility.DisplayDialog(
                "Unsaved Portal Studio changes",
                "Leaving '" + GetPresetDisplayName(editingProfile) +
                "' now will revert its unsaved changes.",
                "Apply and leave",
                "Leave without applying");
            if (applyAndLeave)
            {
                if (!SavePendingChanges(
                        out bool runtimeUpdateRequired,
                        out message))
                {
                    return false;
                }

                if (runtimeUpdateRequired)
                {
                    runtimeSyncRequested = true;
                }

                return true;
            }

            return DiscardPendingChanges(out message);
        }
    }
}
