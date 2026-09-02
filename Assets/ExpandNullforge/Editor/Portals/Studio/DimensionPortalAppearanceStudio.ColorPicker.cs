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
    /// The colour picker itself: its hex field, its wheel and its swatches.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private static ColorPickerKey GetColorPickerKey(SerializedProperty property)
        {
            UnityEngine.Object target = property == null || property.serializedObject == null
                ? null
                : property.serializedObject.targetObject;
            return new ColorPickerKey
            {
                TargetInstanceId = target == null ? 0 : target.GetInstanceID(),
                PropertyPath = property == null ? string.Empty : property.propertyPath
            };
        }

        private float ResolvePickerHue(
            ColorPickerKey key,
            float colorHue,
            float saturation)
        {
            if (saturation > PickerHueSaturationEpsilon)
            {
                retainedHueByColor[key] = colorHue;
                return colorHue;
            }

            if (retainedHueByColor.TryGetValue(key, out float retainedHue))
            {
                return retainedHue;
            }

            retainedHueByColor[key] = colorHue;
            return colorHue;
        }

        private static Color NormalizePickerColor(
            Color authoredColor,
            float intensity,
            bool isHdr)
        {
            if (!isHdr || intensity <= 0f)
            {
                return authoredColor;
            }

            return new Color(
                authoredColor.r / intensity,
                authoredColor.g / intensity,
                authoredColor.b / intensity,
                authoredColor.a);
        }

        private static string FormatPickerHex(Color normalized, float alpha)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(new Color(
                Mathf.Clamp01(normalized.r),
                Mathf.Clamp01(normalized.g),
                Mathf.Clamp01(normalized.b),
                Mathf.Clamp01(alpha)));
        }

        private void DrawHexColorField(
            SerializedProperty property,
            ColorPickerKey pickerKey,
            float intensity,
            bool isHdr,
            string currentHex,
            ref Color authoredColor)
        {
            bool keyChanged = !hasHexEditKey || !hexEditKey.Equals(pickerKey);
            if (keyChanged)
            {
                hexEditKey = pickerKey;
                hasHexEditKey = true;
                hexEditWasFocused = false;
                hexEditBuffer = currentHex;
            }

            bool focusedBefore = string.Equals(
                GUI.GetNameOfFocusedControl(),
                HexColorFieldControlName,
                StringComparison.Ordinal);
            if (!focusedBefore && !hexEditWasFocused && !keyChanged)
            {
                hexEditBuffer = currentHex;
            }

            Event current = Event.current;
            bool submitKey = focusedBefore &&
                             current != null &&
                             current.type == EventType.KeyDown &&
                             (current.keyCode == KeyCode.Return ||
                              current.keyCode == KeyCode.KeypadEnter);

            GUI.SetNextControlName(HexColorFieldControlName);
            bool guiChangedBefore = GUI.changed;
            EditorGUI.BeginChangeCheck();
            string editedHex = EditorGUILayout.TextField("Hex", hexEditBuffer);
            bool textChanged = EditorGUI.EndChangeCheck();
            GUI.changed = guiChangedBefore;
            if (textChanged)
            {
                hexEditBuffer = editedHex;
            }

            bool focusedAfter = string.Equals(
                GUI.GetNameOfFocusedControl(),
                HexColorFieldControlName,
                StringComparison.Ordinal);
            bool lostFocus = hexEditWasFocused && !focusedAfter;
            bool commitImmediately = textChanged && IsCompletePickerHex(hexEditBuffer);
            bool shouldCommit = commitImmediately || submitKey || lostFocus;
            if (shouldCommit && TryParsePickerHex(hexEditBuffer, out Color parsed))
            {
                Color.RGBToHSV(
                    parsed,
                    out float parsedHue,
                    out float parsedSaturation,
                    out _);
                if (parsedSaturation > PickerHueSaturationEpsilon)
                {
                    retainedHueByColor[pickerKey] = parsedHue;
                }

                parsed.r *= intensity;
                parsed.g *= intensity;
                parsed.b *= intensity;
                if (!ColorsApproximatelyEqual(parsed, authoredColor))
                {
                    property.colorValue = parsed;
                    authoredColor = parsed;
                    GUI.changed = true;
                    repaintRequested = true;
                }

                if (!focusedAfter || submitKey)
                {
                    Color normalized = NormalizePickerColor(authoredColor, intensity, isHdr);
                    hexEditBuffer = FormatPickerHex(normalized, authoredColor.a);
                }
            }
            else if (lostFocus)
            {
                hexEditBuffer = currentHex;
            }

            if (submitKey)
            {
                GUI.FocusControl(null);
                focusedAfter = false;
                current.Use();
            }

            hexEditWasFocused = focusedAfter;
        }

        private static bool IsCompletePickerHex(string value)
        {
            string trimmed = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            int digitCount = trimmed.StartsWith("#", StringComparison.Ordinal)
                ? trimmed.Length - 1
                : trimmed.Length;
            return digitCount == 6 || digitCount == 8;
        }

        private static bool TryParsePickerHex(string value, out Color parsed)
        {
            string trimmed = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = "#" + trimmed;
            }

            return ColorUtility.TryParseHtmlString(trimmed, out parsed);
        }

        private static bool ColorsApproximatelyEqual(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= 0.000001f &&
                   Mathf.Abs(left.g - right.g) <= 0.000001f &&
                   Mathf.Abs(left.b - right.b) <= 0.000001f &&
                   Mathf.Abs(left.a - right.a) <= 0.000001f;
        }

        private void EnsureColorPickerTextures(float hue)
        {
            if (hueTexture == null)
            {
                hueTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false)
                {
                    name = "Portal Studio Hue",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color32[] huePixels = new Color32[128];
                for (int y = 0; y < huePixels.Length; y++)
                {
                    huePixels[y] = Color.HSVToRGB(y / 127f, 1f, 1f);
                }

                hueTexture.SetPixels32(huePixels);
                hueTexture.Apply(false, true);
            }

            if (saturationValueTexture != null &&
                Mathf.Abs(Mathf.DeltaAngle(saturationValueHue * 360f, hue * 360f)) < 0.5f)
            {
                return;
            }

            const int width = 128;
            const int height = 96;
            if (saturationValueTexture == null)
            {
                saturationValueTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "Portal Studio Saturation Value",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            int pixelCount = width * height;
            if (saturationValuePixels == null ||
                saturationValuePixels.Length != pixelCount)
            {
                saturationValuePixels = new Color32[pixelCount];
            }

            for (int y = 0; y < height; y++)
            {
                float value = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float saturation = x / (float)(width - 1);
                    saturationValuePixels[y * width + x] =
                        Color.HSVToRGB(hue, saturation, value);
                }
            }

            saturationValueTexture.SetPixels32(saturationValuePixels);
            saturationValueTexture.Apply(false, false);
            saturationValueHue = hue;
        }

        private bool HandlePickerRect(
            Rect rect,
            int hint,
            out Vector2 normalized,
            float hitPadding = 0f)
        {
            normalized = Vector2.zero;
            Rect hitRect = rect;
            if (hitPadding > 0f)
            {
                hitRect.xMin -= hitPadding;
                hitRect.xMax += hitPadding;
                hitRect.yMin -= hitPadding;
                hitRect.yMax += hitPadding;
            }

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, hitRect);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button != 0 ||
                        current.mousePosition.x < hitRect.xMin ||
                        current.mousePosition.x > hitRect.xMax ||
                        current.mousePosition.y < hitRect.yMin ||
                        current.mousePosition.y > hitRect.yMax)
                    {
                        return false;
                    }

                    Undo.IncrementCurrentGroup();
                    colorUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Edit portal color");
                    GUI.FocusControl(null);
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return false;
                    }

                    current.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return false;
                    }

                    GUIUtility.hotControl = 0;
                    collapseColorUndoAfterApply = true;
                    current.Use();
                    break;
                default:
                    return false;
            }

            normalized = new Vector2(
                Mathf.Clamp01((current.mousePosition.x - rect.x) / rect.width),
                Mathf.Clamp01((current.mousePosition.y - rect.y) / rect.height));
            return true;
        }

        private static void DrawCrosshair(Vector2 position)
        {
            EditorGUI.DrawRect(new Rect(position.x - 5f, position.y - 1f, 10f, 2f), Color.black);
            EditorGUI.DrawRect(new Rect(position.x - 1f, position.y - 5f, 2f, 10f), Color.black);
            EditorGUI.DrawRect(new Rect(position.x - 4f, position.y, 8f, 1f), Color.white);
            EditorGUI.DrawRect(new Rect(position.x, position.y - 4f, 1f, 8f), Color.white);
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private Rect CanvasRectToGuiRect(Rect canvasRect)
        {
            return new Rect(
                (canvasRect.x - activeViewBounds.x) * activeZoom,
                (activeViewBounds.yMax - canvasRect.y - canvasRect.height) * activeZoom,
                canvasRect.width * activeZoom,
                canvasRect.height * activeZoom);
        }

        private static int FindClosestPaletteIndex(Color32 color, Color32[] palette)
        {
            int closest = 0;
            int closestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int red = color.r - palette[i].r;
                int green = color.g - palette[i].g;
                int blue = color.b - palette[i].b;
                int distance = red * red + green * green + blue * blue;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }

        private static int GetPaletteHash(Color[] colors)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < colors.Length; i++)
                {
                    hash = hash * 31 + colors[i].GetHashCode();
                }

                return hash;
            }
        }

        private Color[] GetPalette(SerializedObject profile, string[] propertyNames)
        {
            Color[] colors = propertyNames.Length == centerPaletteBuffer.Length
                ? centerPaletteBuffer
                : effectPaletteBuffer;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                colors[i] = GetColor(profile, propertyNames[i], Color.white);
            }

            return colors;
        }
    }
}
