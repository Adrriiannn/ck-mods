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
    /// The panel beside the canvas, and the portal sounds in it.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void DrawContextInspector(
            DimensionTemplateAsset template,
            SerializedObject profile,
            ref DrawResult result,
            float minimumHeight = 0f,
            float fixedWidth = 0f)
        {
            if (minimumHeight > 0f && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(minimumHeight));
            }
            else if (minimumHeight > 0f)
            {
                EditorGUILayout.BeginVertical(GUILayout.Height(minimumHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical();
            }

            DimensionPortalArtworkReferenceKind artworkReferenceKind =
                DimensionPortalArtworkReferenceKind.Empty;
            SpriteAsset resolvedArtworkAsset = null;
            Rect mainPanelRect;
            if (fixedWidth > 0f)
            {
                mainPanelRect = EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                mainPanelRect = EditorGUILayout.BeginVertical(
                    DimensionsApiImguiTheme.CardBox,
                    GUILayout.MinWidth(InspectorMinWidth),
                    GUILayout.ExpandWidth(true));
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLayerTitle(selectedLayer), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Restore vanilla", GUILayout.Width(104f)))
            {
                CancelPendingTextureUpdate();
                CancelPendingArtworkVariantCreation(selectedLayer);
                profileEditSession.MarkChanged();

                DimensionPortalVisualProfileAsset restoreProfile =
                    profile.targetObject as DimensionPortalVisualProfileAsset;
                if (TryGetArtworkLayer(
                        selectedLayer,
                        out DimensionPortalArtworkLayer restoreArtworkLayer,
                        instantPortalMode))
                {
                    DimensionPortalArtworkEditorUtility.CancelPending(
                        restoreProfile,
                        restoreArtworkLayer);
                }
                else if (selectedLayer == StudioLayer.InnerFlecks)
                {
                    DimensionPortalSwirlArtworkEditorUtility.CancelSwirlBake(restoreProfile);
                }

                if (!RestoreLayerToVanilla(
                        profile,
                        selectedLayer,
                        out string restoreMessage,
                        instantPortalMode))
                {
                    result.Message = restoreMessage;
                    result.MessageType = MessageType.Error;
                }

                InvalidateTextureSlotCache();
                ClearPaletteFocus();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);

            bool directOverride = false;
            long artworkReferenceLow = 0L;
            long artworkReferenceHigh = 0L;
            string overridePropertyName = GetOverridePropertyName(selectedLayer);
            if (!string.IsNullOrEmpty(overridePropertyName))
            {
                SerializedProperty overrideProperty = profile.FindProperty(overridePropertyName);
                TryReadReferenceAddress(
                    overrideProperty,
                    out artworkReferenceLow,
                    out artworkReferenceHigh);
                DrawArtworkOverride(
                    template,
                    profile,
                    selectedLayer,
                    overrideProperty,
                    ref result);
                if (TryGetArtworkLayer(
                        selectedLayer,
                        out DimensionPortalArtworkLayer artworkLayer,
                        instantPortalMode))
                {
                    artworkReferenceKind =
                        DimensionPortalArtworkEditorUtility.ClassifyReference(
                            overrideProperty,
                            profile.targetObject as DimensionPortalVisualProfileAsset,
                            artworkLayer,
                            out resolvedArtworkAsset);
                    directOverride = IsDirectTextureReference(
                        profile.targetObject as DimensionPortalVisualProfileAsset,
                        artworkLayer,
                        artworkReferenceKind,
                        resolvedArtworkAsset,
                        overrideProperty);

                    if (artworkReferenceKind == DimensionPortalArtworkReferenceKind.Unresolved)
                    {
                        EditorGUILayout.HelpBox(
                            "This SpriteAsset address is not available in the live Scriptable Data lookup. The Studio is showing the framework artwork with the current palette until the lookup refreshes; Apply remains blocked.",
                            MessageType.Error);
                    }
                }
                if (directOverride &&
                    (selectedLayer == StudioLayer.ChargeSweep ||
                     selectedLayer == StudioLayer.Milestones ||
                     selectedLayer == StudioLayer.Center))
                {
                    EditorGUILayout.HelpBox(
                        "This SpriteAsset supplies the final pixels, so its built-in palette colors are bypassed. Tint, emission, timing, and playback settings still apply.",
                        MessageType.None);
                }
            }

            ColorRole[] roles = GetColorRoles(selectedLayer);
            if (selectedLayer == StudioLayer.InnerFlecks)
            {
                DrawFlecksPaletteControls(profile);
                GUILayout.Space(4f);
                DrawArtworkOverride(
                    template,
                    profile,
                    StudioLayer.InnerFlecks,
                    profile.FindProperty("centerSwirlSpriteAsset"),
                    ref result);
                if (GetBool(profile, "centerSwirlOverrideVanilla", false) &&
                    (!TryResolveCustomSwirlAsset(
                         profile,
                         out SpriteAsset swirlArtwork,
                         out string swirlArtworkError) ||
                     !TryValidateCustomSwirlAsset(
                         swirlArtwork,
                         out swirlArtworkError)))
                {
                    EditorGUILayout.HelpBox(swirlArtworkError, MessageType.Error);
                }
                GUILayout.Space(4f);
            }
            else if (selectedLayer == StudioLayer.ReadyBurst)
            {
                DrawReadyBurstPaletteControls(profile);
                GUILayout.Space(4f);
                DrawProperty(profile, "readyFlashSprites", "Animation frames");
                GUILayout.Space(4f);
            }

            if (roles.Length > 0)
            {
                DrawColorRoleStrip(profile, roles, directOverride);
                int selectedRoleIndex = Mathf.Clamp(
                    selectedColorRoleByLayer[(int)selectedLayer],
                    0,
                    roles.Length - 1);
                ColorRole selectedRole = roles[selectedRoleIndex];
                bool paletteDisabled = directOverride && selectedRole.IsPaletteColor;
                bool followsCenterPalette =
                    IsSelectedParticleTintFollowingCenter(
                        profile,
                        selectedLayer,
                        selectedRoleIndex);
                EditorGUI.BeginDisabledGroup(paletteDisabled || followsCenterPalette);
                EditorGUI.BeginChangeCheck();
                DrawModernColorEditor(profile.FindProperty(selectedRole.PropertyName), selectedRole);
                bool colorChanged = EditorGUI.EndChangeCheck();
                EditorGUI.EndDisabledGroup();
                if (colorChanged && selectedRole.IsPaletteColor)
                {
                    paletteBakeRequested = true;
                    paletteBakeLayer = selectedLayer;
                }

                if (followsCenterPalette)
                {
                    EditorGUILayout.HelpBox(
                        "Following the activated center palette. Vanilla center colors preserve the exact vanilla particle gradient; disable the matching follow option to use this independent color.",
                        MessageType.None);
                }
            }

            GUILayout.Space(6f);
            if (selectedLayer != StudioLayer.InnerFlecks ||
                GetBool(profile, "centerSwirlOverrideVanilla", false))
            {
                DrawLayerTransformSettings(profile, selectedLayer);
            }
            DrawLayerSpecificSettings(profile);
            EditorGUILayout.EndVertical();
            if (TryGetArtworkLayer(
                    selectedLayer,
                    out DimensionPortalArtworkLayer selectedArtworkLayer,
                    instantPortalMode) &&
                Event.current.type == EventType.Repaint &&
                mainPanelRect.height > 1f)
            {
                measuredArtworkMainPanelHeights[(int)selectedArtworkLayer] =
                    mainPanelRect.height;
            }
            else if (selectedLayer == StudioLayer.InnerFlecks &&
                     Event.current.type == EventType.Repaint &&
                     mainPanelRect.height > 1f)
            {
                measuredSwirlMainPanelHeight = mainPanelRect.height;
            }

            if (TryGetArtworkLayer(
                    selectedLayer,
                    out selectedArtworkLayer,
                    instantPortalMode))
            {
                GUILayout.Space(4f);
                float minimumTexturePanelHeight = GetTexturePanelMinimumHeight(
                    selectedArtworkLayer);
                float texturePanelHeight = minimumHeight > 0f
                    ? Mathf.Max(
                        minimumTexturePanelHeight,
                        minimumHeight -
                        measuredArtworkMainPanelHeights[(int)selectedArtworkLayer] -
                        measuredSoundsPanelHeight -
                        8f -
                        EditorGUIUtility.standardVerticalSpacing * 3f)
                    : 0f;
                DrawTexturePanel(
                    template,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    selectedArtworkLayer,
                    artworkReferenceKind,
                    resolvedArtworkAsset,
                    artworkReferenceLow,
                    artworkReferenceHigh,
                    texturePanelHeight,
                    fixedWidth);
            }
            else if (selectedLayer == StudioLayer.InnerFlecks)
            {
                GUILayout.Space(4f);
                const float minimumSwirlTexturePanelHeight = 64f;
                float texturePanelHeight = minimumHeight > 0f
                    ? Mathf.Max(
                        minimumSwirlTexturePanelHeight,
                        minimumHeight -
                        measuredSwirlMainPanelHeight -
                        measuredSoundsPanelHeight -
                        8f -
                        EditorGUIUtility.standardVerticalSpacing * 3f)
                    : 0f;
                DrawSwirlTexturePanel(
                    template,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    GetBool(profile, "centerSwirlOverrideVanilla", false),
                    texturePanelHeight,
                    fixedWidth);
            }

            GUILayout.Space(4f);
            DrawPortalSoundsPanel(template, fixedWidth);

            EditorGUILayout.EndVertical();
        }

        // Measured on repaint so the texture panel above can budget its height around the
        // sounds panel; seeded with a sensible estimate for the first frame.
        private float measuredSoundsPanelHeight = 92f;

        /// <summary>
        /// Portal sound configuration, living under the Textures panel. The placed portal only
        /// offers an activation sound; the instant portal picks ONE exclusive mode — Peak
        /// (activation/deactivation one-shots) or Loop (a bed while it stands open). Each field
        /// takes an SfxID name or a Sound Library key; the Pick button browses and previews.
        /// </summary>
        private void DrawPortalSoundsPanel(DimensionTemplateAsset template, float fixedWidth)
        {
            if (template == null)
            {
                return;
            }

            if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox, GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
            }

            EditorGUILayout.LabelField("Sounds", EditorStyles.boldLabel);

            string placedActivation = template.PlacedPortalActivationSound;
            int instantMode = template.InstantPortalSoundMode;
            string instantActivation = template.InstantPortalActivationSound;
            string instantDeactivation = template.InstantPortalDeactivationSound;
            string instantLoop = template.InstantPortalLoopSound;

            EditorGUI.BeginChangeCheck();
            if (!instantPortalMode)
            {
                placedActivation = DrawSoundKeyField(
                    template,
                    "Activation",
                    "Plays once when the portal finishes charging and lights up. SfxID name or " +
                    "a Sound Library key. Heard within 8 tiles. Empty = silent.",
                    DimensionSoundPickerWindow.FieldPlacedActivation,
                    placedActivation,
                    false);
            }
            else
            {
                instantMode = GUILayout.Toolbar(
                    instantMode,
                    new[]
                    {
                        new GUIContent(
                            "Peak",
                            "One-shots at the portal's edges: an activation sound when it opens " +
                            "and a deactivation sound as it closes."),
                        new GUIContent(
                            "Loop",
                            "A looping bed that plays the whole time the portal stands open.")
                    },
                    GUILayout.Width(160f));
                GUILayout.Space(2f);

                if (instantMode == 0)
                {
                    instantActivation = DrawSoundKeyField(
                        template,
                        "Activation",
                        "Plays once as the portal tears open. SfxID name or a Sound Library " +
                        "key. Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantActivation,
                        instantActivation,
                        false);
                    instantDeactivation = DrawSoundKeyField(
                        template,
                        "Deactivation",
                        "Plays once as the portal winks out. SfxID name or a Sound Library " +
                        "key. Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantDeactivation,
                        instantDeactivation,
                        false);
                }
                else
                {
                    instantLoop = DrawSoundKeyField(
                        template,
                        "Loop",
                        "Loops while the portal stands open and stops as it closes. Needs a " +
                        "Sound Library clip (SfxID names cannot loop). Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantLoop,
                        instantLoop,
                        true);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(template, "Portal Sounds");
                template.SetPortalSoundSettings(
                    placedActivation,
                    instantMode,
                    instantActivation,
                    instantDeactivation,
                    instantLoop);
                EditorUtility.SetDirty(template);
            }

            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                measuredSoundsPanelHeight = GUILayoutUtility.GetLastRect().height;
            }
        }

        private static string DrawSoundKeyField(
            DimensionTemplateAsset template,
            string label,
            string tooltip,
            string fieldId,
            string value,
            bool clipOnly)
        {
            EditorGUILayout.BeginHorizontal();
            string result = EditorGUILayout.TextField(new GUIContent(label, tooltip), value);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(result)))
            {
                if (GUILayout.Button(
                    new GUIContent(
                        "▶",
                        "Play this sound without opening the picker. Sound Library keys " +
                        "audition here; an SfxID name only plays inside the game."),
                    GUILayout.Width(24f)))
                {
                    PlaySoundKey(result);
                }
            }

            if (GUILayout.Button(
                new GUIContent("Pick", "Browse the game's sounds, listen, and select."),
                GUILayout.Width(40f)))
            {
                DimensionSoundPickerWindow.Open(template, fieldId, clipOnly);
            }

            EditorGUILayout.EndHorizontal();
            return result;
        }

        /// <summary>
        /// Auditions the sound a field names, resolving the key the same way the picker's
        /// preselect does. SfxID names have no clip in the bundles, so they stay silent here.
        /// </summary>
        private static void PlaySoundKey(string soundKey)
        {
            if (string.IsNullOrEmpty(soundKey))
            {
                return;
            }

            string gamePath = DimensionGameSoundCatalog.ResolveGamePath();
            System.Collections.Generic.IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries =
                DimensionGameSoundCatalog.GetEntries(gamePath, out string scanError);
            if (!string.IsNullOrEmpty(scanError))
            {
                return;
            }

            string normalizedKey = soundKey.Trim().Replace('\\', '/');
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(
                        entries[i].AssetPath,
                        normalizedKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AudioClip clip = DimensionGameSoundCatalog.LoadClip(entries[i], out _);
                if (clip != null)
                {
                    DimensionGameSoundCatalog.StopPreview();
                    DimensionGameSoundCatalog.PlayPreview(clip);
                }

                return;
            }
        }
    }
}
