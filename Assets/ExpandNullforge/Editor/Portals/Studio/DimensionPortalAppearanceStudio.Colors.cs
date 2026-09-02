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
    /// The colour roles a layer has, and the layout settings beside them.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void DrawColorRoleStrip(
            SerializedObject profile,
            ColorRole[] roles,
            bool directOverride)
        {
            if (paletteFocusActive &&
                paletteFocusLayer == selectedLayer &&
                (directOverride ||
                 !CanFocusPaletteRole(
                     selectedLayer,
                     roles,
                     paletteFocusRoleIndex)))
            {
                ClearPaletteFocus();
            }

            Rect strip = GUILayoutUtility.GetRect(100f, 44f, GUILayout.ExpandWidth(true));
            float gap = 3f;
            float width = Mathf.Max(28f, (strip.width - gap * (roles.Length - 1)) / roles.Length);
            int selectedIndex = Mathf.Clamp(
                selectedColorRoleByLayer[(int)selectedLayer],
                0,
                roles.Length - 1);
            for (int i = 0; i < roles.Length; i++)
            {
                Rect swatch = new Rect(strip.x + i * (width + gap), strip.y, width, 44f);
                SerializedProperty property = profile.FindProperty(roles[i].PropertyName);
                Color color = property == null ? Color.magenta : property.colorValue;
                Color display = ToneMapForEditor(color, roles[i].IsHdr);
                bool canFocusPixels = !directOverride &&
                                      CanFocusPaletteRole(selectedLayer, roles, i);
                bool focused = canFocusPixels &&
                               paletteFocusActive &&
                               paletteFocusLayer == selectedLayer &&
                               paletteFocusRoleIndex == i;
                Color borderColor = focused
                    ? new Color(0.1f, 0.88f, 1f, 1f)
                    : i == selectedIndex
                        ? new Color(0.22f, 0.56f, 0.72f, 1f)
                        : new Color(0.16f, 0.17f, 0.2f, 1f);
                EditorGUI.DrawRect(swatch, borderColor);
                Rect inside = new Rect(swatch.x + 2f, swatch.y + 2f, swatch.width - 4f, 25f);
                EditorGUI.DrawRect(inside, display);
                if (directOverride && roles[i].IsPaletteColor)
                {
                    EditorGUI.DrawRect(
                        new Rect(inside.x, inside.center.y, inside.width, 1f),
                        new Color(1f, 1f, 1f, 0.65f));
                }

                Rect labelRect = new Rect(swatch.x, swatch.y + 27f, swatch.width, 15f);
                bool emphasizeLabel = focused || i == selectedIndex;
                EditorGUI.DrawRect(
                    labelRect,
                    emphasizeLabel
                        ? new Color(0.035f, 0.055f, 0.075f, 0.96f)
                        : new Color(0.045f, 0.05f, 0.065f, 0.82f));
                GUI.Label(
                    labelRect,
                    new GUIContent(roles[i].ShortLabel, roles[i].Label),
                    GetSwatchLabelStyle(emphasizeLabel));
                string tooltip = roles[i].Label;
                if (canFocusPixels)
                {
                    tooltip += focused
                        ? "\nClick to clear pixel focus."
                        : "\nClick to highlight these pixels in the portal preview.";
                }

                if (GUI.Button(
                        swatch,
                        new GUIContent(string.Empty, tooltip),
                        GUIStyle.none))
                {
                    selectedColorRoleByLayer[(int)selectedLayer] = i;
                    if (focused)
                    {
                        ClearPaletteFocus();
                    }
                    else if (canFocusPixels)
                    {
                        SetPaletteFocus(selectedLayer, i);
                    }
                    else
                    {
                        ClearPaletteFocus();
                    }

                    GUI.changed = true;
                }
            }
        }

        private void DrawModernColorEditor(SerializedProperty property, ColorRole role)
        {
            if (property == null)
            {
                return;
            }

            GUILayout.Space(5f);
            // No repeated title here — the swatch itself already names the role.
            Color authoredColor = property.colorValue;
            float intensity = role.IsHdr
                ? Mathf.Max(1f, authoredColor.r, authoredColor.g, authoredColor.b)
                : 1f;
            Color normalized = role.IsHdr && intensity > 0f
                ? new Color(
                    authoredColor.r / intensity,
                    authoredColor.g / intensity,
                    authoredColor.b / intensity,
                    authoredColor.a)
                : authoredColor;
            Color.RGBToHSV(
                normalized,
                out float colorHue,
                out float saturation,
                out float value);
            ColorPickerKey pickerKey = GetColorPickerKey(property);
            float hue = ResolvePickerHue(pickerKey, colorHue, saturation);

            EnsureColorPickerTextures(hue);
            Rect pickerRow = GUILayoutUtility.GetRect(100f, 112f, GUILayout.ExpandWidth(true));
            float hueWidth = 18f;
            float previewWidth = 42f;
            Rect svRect = new Rect(
                pickerRow.x,
                pickerRow.y,
                Mathf.Max(100f, pickerRow.width - hueWidth - previewWidth - 14f),
                pickerRow.height);
            Rect hueRect = new Rect(svRect.xMax + 6f, pickerRow.y, hueWidth, pickerRow.height);
            Rect previewRect = new Rect(hueRect.xMax + 8f, pickerRow.y, previewWidth, pickerRow.height);

            GUI.DrawTexture(svRect, saturationValueTexture, ScaleMode.StretchToFill, false);
            GUI.DrawTexture(hueRect, hueTexture, ScaleMode.StretchToFill, false);
            EditorGUI.DrawRect(previewRect, ToneMapForEditor(authoredColor, role.IsHdr));
            DrawOutline(svRect, new Color(0f, 0f, 0f, 0.8f), 1f);
            DrawOutline(hueRect, new Color(0f, 0f, 0f, 0.8f), 1f);
            DrawOutline(previewRect, new Color(0f, 0f, 0f, 0.8f), 1f);

            float markerX = svRect.x + saturation * svRect.width;
            float markerY = svRect.y + (1f - value) * svRect.height;
            DrawCrosshair(new Vector2(markerX, markerY));
            float hueY = Mathf.Lerp(hueRect.yMax - 1f, hueRect.yMin + 1f, hue);
            EditorGUI.DrawRect(new Rect(hueRect.x - 2f, hueY - 1f, hueRect.width + 4f, 2f), Color.white);
            EditorGUI.DrawRect(new Rect(hueRect.x - 1f, hueY, hueRect.width + 2f, 1f), Color.black);

            bool hsvChanged = false;
            if (HandlePickerRect(svRect, 811, out Vector2 svPosition))
            {
                saturation = Mathf.Clamp01(svPosition.x);
                value = Mathf.Clamp01(1f - svPosition.y);
                hsvChanged = true;
            }

            if (HandlePickerRect(hueRect, 812, out Vector2 huePosition, 3f))
            {
                hue = Mathf.Clamp01(1f - huePosition.y);
                retainedHueByColor[pickerKey] = hue;
                hsvChanged = true;
            }

            if (hsvChanged)
            {
                Color changed = Color.HSVToRGB(hue, saturation, value, role.IsHdr);
                changed.r *= intensity;
                changed.g *= intensity;
                changed.b *= intensity;
                changed.a = authoredColor.a;
                if (!ColorsApproximatelyEqual(changed, authoredColor))
                {
                    property.colorValue = changed;
                    GUI.changed = true;
                }

                authoredColor = changed;
                if (saturation > 0.0001f)
                {
                    retainedHueByColor[pickerKey] = hue;
                }

                normalized = NormalizePickerColor(authoredColor, intensity, role.IsHdr);
                repaintRequested = true;
            }

            string currentHex = FormatPickerHex(normalized, authoredColor.a);
            DrawHexColorField(
                property,
                pickerKey,
                intensity,
                role.IsHdr,
                currentHex,
                ref authoredColor);

            float alpha = EditorGUILayout.Slider("Alpha", authoredColor.a, 0f, 1f);
            if (!Mathf.Approximately(alpha, authoredColor.a))
            {
                authoredColor.a = alpha;
                property.colorValue = authoredColor;
            }

            if (role.IsHdr)
            {
                float maxIntensity = Mathf.Max(16f, Mathf.Ceil(intensity));
                float changedIntensity = EditorGUILayout.Slider(
                    "HDR intensity",
                    intensity,
                    0f,
                    maxIntensity);
                if (!Mathf.Approximately(changedIntensity, intensity))
                {
                    Color baseColor = intensity <= 0f
                        ? Color.black
                        : new Color(
                            authoredColor.r / intensity,
                            authoredColor.g / intensity,
                            authoredColor.b / intensity,
                            authoredColor.a);
                    baseColor.r *= changedIntensity;
                    baseColor.g *= changedIntensity;
                    baseColor.b *= changedIntensity;
                    property.colorValue = baseColor;
                }
            }
        }

        private void DrawLayerSpecificSettings(SerializedObject profile)
        {
            switch (selectedLayer)
            {
                case StudioLayer.ChargeSweep:
                    DrawProperty(profile, "chargeWaveSpeed", "Playback speed");
                    break;
                case StudioLayer.Milestones:
                    EditorGUILayout.LabelField("Activation thresholds", DimensionsApiImguiTheme.SectionLabel);
                    DrawProperty(profile, "firstMilestone", "Bottom pair");
                    DrawProperty(profile, "secondMilestone", "Middle pair");
                    DrawProperty(profile, "thirdMilestone", "Upper pair");
                    break;
                case StudioLayer.Center:
                    DrawProperty(profile, "centerGlowIntensity", "Highlight brightness");
                    break;
                case StudioLayer.InnerFlecks:
                    if (GetBool(profile, "centerSwirlOverrideVanilla", false))
                    {
                        EditorGUILayout.LabelField(
                            "Animation and glow",
                            DimensionsApiImguiTheme.SectionLabel);
                        DrawProperty(profile, "centerSwirlPlaybackSpeed", "Playback speed");
                        DrawProperty(
                            profile,
                            "centerParticleEmissionMultiplier",
                            "Emission multiplier");
                    }
                    break;
                case StudioLayer.ReadyBurst:
                    EditorGUILayout.LabelField("Burst placement", DimensionsApiImguiTheme.SectionLabel);
                    DrawProperty(profile, "readyFlashOffsetPixels", "Offset (pixels)");
                    DrawProperty(profile, "readyFlashScale", "Scale");
                    DrawProperty(profile, "readyFlashRotationDegrees", "Rotation");
                    DrawProperty(profile, "readyFlashEmissionMultiplier", "Emission multiplier");
                    DrawProperty(profile, "readyFlashSizeMultiplier", "Size multiplier");
                    GUILayout.Space(2f);
                    DrawReplayBurstControl();
                    break;
                case StudioLayer.GroundLight:
                    EditorGUILayout.HelpBox(
                        "The coloured pool itself only exists in the game world. The dashed ring on the canvas shows exactly where it reaches; the swatch below shows its true steady brightness. Fog and responsive shadows stay world-only.",
                        MessageType.None);
                    EditorGUILayout.LabelField("Light placement", DimensionsApiImguiTheme.SectionLabel);
                    DrawProperty(profile, "groundLightEnabled", "Enabled");
                    DrawProperty(profile, "groundLightOffsetPixels", "Offset (pixels)");
                    DrawProperty(profile, "groundLightRange", "Range");
                    DrawProperty(profile, "groundLightMinimumIntensity", "Runtime minimum");
                    DrawProperty(profile, "groundLightMaximumIntensity", "Runtime maximum");
                    DrawGroundLightEffectiveIntensityRow(profile);
                    DrawProperty(profile, "groundLightMovement", "Vanilla movement");
                    DrawProperty(profile, "groundLightCastsShadows", "Responsive shadows");
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("Projected shadow", DimensionsApiImguiTheme.SectionLabel);
                    DrawProperty(profile, "portalShadowEnabled", "Enabled");
                    DrawProperty(profile, "portalShadowSprite", "Floor shadow sprite");
                    DrawProperty(profile, "portalShadowCasterSprite", "Responsive caster sprite");
                    DrawProperty(profile, "portalShadowScale", "Scale");
                    EditorGUILayout.BeginHorizontal();
                    // Flip X/Y are intentionally not exposed for the shadow either.
                    EditorGUILayout.EndHorizontal();
                    break;
            }
        }

        private void DrawGroundLightEffectiveIntensityRow(SerializedObject profile)
        {
            float minimum = GetFloat(profile, "groundLightMinimumIntensity", 0.3f);
            float maximum = Mathf.Max(
                minimum,
                GetFloat(profile, "groundLightMaximumIntensity", 0.3f));
            // Read only. The profile derives the effective intensity from this same range, so
            // there is nothing to write back — and writing during a GUI pass would record an
            // undo step every repaint, which no Ctrl+Z could ever get past.
            float effective = (minimum + maximum) * 0.5f;

            Color lightColor = GetColor(
                profile,
                "groundLightColor",
                DimensionPortalVisualProfileAsset.VanillaGroundLightColor);
            Rect row = EditorGUILayout.GetControlRect();
            Rect content = EditorGUI.PrefixLabel(
                row,
                new GUIContent(
                    "Brightness in game",
                    "The steady brightness the flicker settles around: the midpoint of the runtime minimum and maximum. There is no separate authored intensity any more, because the game overwrote it a moment after the portal appeared."));
            Rect swatch = new Rect(content.x, content.y + 1f, 36f, content.height - 2f);
            EditorGUI.DrawRect(swatch, Color.black);
            EditorGUI.DrawRect(
                new Rect(swatch.x + 1f, swatch.y + 1f, swatch.width - 2f, swatch.height - 2f),
                new Color(
                    Mathf.Clamp01(lightColor.r * effective),
                    Mathf.Clamp01(lightColor.g * effective),
                    Mathf.Clamp01(lightColor.b * effective),
                    1f));
            EditorGUI.LabelField(
                new Rect(swatch.xMax + 6f, content.y, 80f, content.height),
                effective.ToString("0.00"),
                DimensionsApiImguiTheme.Caption);
        }

        private void DrawLayerTransformSettings(
            SerializedObject profile,
            StudioLayer layer)
        {
            if (!TryGetLayerTransformPropertyNames(
                    layer,
                    out string visibleName,
                    out string offsetName,
                    out string scaleName,
                    out string rotationName,
                    out string flipXName,
                    out string flipYName))
            {
                return;
            }

            SerializedProperty visible = profile.FindProperty(visibleName);
            SerializedProperty offset = profile.FindProperty(offsetName);
            SerializedProperty scale = profile.FindProperty(scaleName);
            SerializedProperty rotation = profile.FindProperty(rotationName);
            SerializedProperty flipX = profile.FindProperty(flipXName);
            SerializedProperty flipY = profile.FindProperty(flipYName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Layout", DimensionsApiImguiTheme.SectionLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset layout", EditorStyles.miniButton, GUILayout.Width(82f)))
            {
                ResetLayerLayout(profile, layer);
                GUI.changed = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            if (visible != null && layer != StudioLayer.InnerFlecks)
            {
                EditorGUILayout.PropertyField(
                    visible,
                    new GUIContent(
                        "Visible",
                        "Include this visual layer in the generated portal."));
            }

            // Offset X/Y number fields and Rotation are intentionally not exposed: positioning is
            // done with the Nudge buttons / preview dragging below, and rotation is unused by this
            // framework's portal art. Existing serialized values still bake as-is.
            if (scale != null)
            {
                EditorGUILayout.PropertyField(
                    scale,
                    new GUIContent(
                        "Scale",
                        "Independent width and height scale around the SpriteAsset pivot."));
                Vector2 clampedScale = ClampPreviewScale(scale.vector2Value);
                if (scale.vector2Value != clampedScale)
                {
                    scale.vector2Value = clampedScale;
                }
            }

            // Flip X/Y are intentionally not exposed (unused by this framework's portal art);
            // existing serialized flip values still bake as-is.
            if (EditorGUI.EndChangeCheck())
            {
                MarkPreviewTransformChanged();
            }

            if (offset != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Nudge", GUILayout.Width(64f));
                DrawOffsetNudgeButton(offset, Vector2.left, "←");
                DrawOffsetNudgeButton(offset, Vector2.right, "→");
                DrawOffsetNudgeButton(offset, Vector2.down, "↓");
                DrawOffsetNudgeButton(offset, Vector2.up, "↑");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
        }

        /// <summary>
        /// Puts one layer back where the artwork was drawn: centred, unturned, full size and
        /// visible. One body, shared by the drawn-in-place button and by the rebuilt page.
        /// </summary>
        internal void ResetLayerLayout(SerializedObject profile, StudioLayer layer)
        {
            if (profile == null ||
                !TryGetLayerTransformPropertyNames(
                    layer,
                    out string visibleName,
                    out string offsetName,
                    out string scaleName,
                    out string rotationName,
                    out string flipXName,
                    out string flipYName))
            {
                return;
            }

            SerializedProperty visible = profile.FindProperty(visibleName);
            SerializedProperty offset = profile.FindProperty(offsetName);
            SerializedProperty scale = profile.FindProperty(scaleName);
            SerializedProperty rotation = profile.FindProperty(rotationName);
            SerializedProperty flipX = profile.FindProperty(flipXName);
            SerializedProperty flipY = profile.FindProperty(flipYName);

            Undo.RecordObject(
                profile.targetObject,
                "Reset " + GetLayerTitle(layer) + " layout");
            if (visible != null)
            {
                visible.boolValue = true;
            }

            if (offset != null)
            {
                offset.vector2Value = Vector2.zero;
            }

            if (scale != null)
            {
                scale.vector2Value = Vector2.one;
            }

            if (rotation != null)
            {
                rotation.floatValue = 0f;
            }

            if (flipX != null)
            {
                flipX.boolValue = false;
            }

            if (flipY != null)
            {
                flipY.boolValue = false;
            }

            previewCompositionDirty = true;
            repaintRequested = true;
        }

        private void DrawOffsetNudgeButton(
            SerializedProperty offset,
            Vector2 direction,
            string label)
        {
            if (!GUILayout.Button(label, GUILayout.Width(30f)))
            {
                return;
            }

            Undo.RecordObject(
                offset.serializedObject.targetObject,
                "Nudge " + GetLayerTitle(selectedLayer));
            offset.vector2Value += direction;
            MarkPreviewTransformChanged();
        }

        private static bool TryGetLayerTransformPropertyNames(
            StudioLayer layer,
            out string visible,
            out string offset,
            out string scale,
            out string rotation,
            out string flipX,
            out string flipY)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    visible = "frameVisible";
                    offset = "frameOffsetPixels";
                    scale = "frameScale";
                    rotation = "frameRotationDegrees";
                    flipX = "frameFlipX";
                    flipY = "frameFlipY";
                    return true;
                case StudioLayer.ChargeSweep:
                    visible = "chargeWaveVisible";
                    offset = "chargeWaveOffsetPixels";
                    scale = "chargeWaveScale";
                    rotation = "chargeWaveRotationDegrees";
                    flipX = "chargeWaveFlipX";
                    flipY = "chargeWaveFlipY";
                    return true;
                case StudioLayer.Milestones:
                    visible = "milestonesVisible";
                    offset = "milestoneOffsetPixels";
                    scale = "milestoneScale";
                    rotation = "milestoneRotationDegrees";
                    flipX = "milestoneFlipX";
                    flipY = "milestoneFlipY";
                    return true;
                case StudioLayer.Center:
                    visible = "centerVisible";
                    offset = "centerOffsetPixels";
                    scale = "centerScale";
                    rotation = "centerRotationDegrees";
                    flipX = "centerFlipX";
                    flipY = "centerFlipY";
                    return true;
                case StudioLayer.InnerFlecks:
                    visible = "centerSwirlVisible";
                    offset = "centerParticleOffsetPixels";
                    scale = "centerParticleScale";
                    rotation = "centerParticleRotationDegrees";
                    flipX = "centerSwirlFlipX";
                    flipY = "centerSwirlFlipY";
                    return true;
                default:
                    visible = string.Empty;
                    offset = string.Empty;
                    scale = string.Empty;
                    rotation = string.Empty;
                    flipX = string.Empty;
                    flipY = string.Empty;
                    return false;
            }
        }

        private static void DrawFlecksPaletteControls(SerializedObject profile)
        {
            EditorGUILayout.LabelField("Swirl mode", DimensionsApiImguiTheme.SectionLabel);
            DrawProperty(profile, "centerSwirlVisible", "Visible");
            DrawProperty(profile, "centerSwirlOverrideVanilla", "Override vanilla");
        }

        private static void DrawReadyBurstPaletteControls(SerializedObject profile)
        {
            EditorGUILayout.LabelField("Ready burst visibility and color", DimensionsApiImguiTheme.SectionLabel);
            DrawProperty(profile, "playReadyFlash", "Enabled");
            DrawProperty(
                profile,
                "readyFlashFollowsCenterPalette",
                "Follow center palette");
        }

        private static bool IsSelectedParticleTintFollowingCenter(
            SerializedObject profile,
            StudioLayer layer,
            int selectedRoleIndex)
        {
            if (profile == null || selectedRoleIndex != 0)
            {
                return false;
            }

            string propertyName;
            if (layer == StudioLayer.ReadyBurst)
            {
                propertyName = "readyFlashFollowsCenterPalette";
            }
            else
            {
                return false;
            }

            SerializedProperty property = profile.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static void DrawProperty(
            SerializedObject serializedObject,
            string propertyName,
            string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }
    }
}
