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
    /// The GUI styles the studio uses, and the pin icon it draws itself.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private GUIStyle GetLayerButtonStyle()
        {
            if (layerButtonStyle == null)
            {
                // Same typeface and text colour as the rest of the product; the button's shape
                // and behaviour are untouched.
                layerButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 42f,
                    richText = true,
                    padding = new RectOffset(6, 30, 2, 2)
                };
                if (DimensionsApiImguiTheme.BodyStrong != null)
                {
                    layerButtonStyle.font = DimensionsApiImguiTheme.BodyStrong;
                    layerButtonStyle.fontSize = 12;
                }
                SetStyleTextColor(layerButtonStyle, DimensionsApiImguiTheme.Lavender);
            }

            return layerButtonStyle;
        }

        private GUIStyle GetLayerChipStyle()
        {
            if (layerChipStyle == null)
            {
                GUIStyle source = EditorStyles.toolbarButton;
                layerChipStyle = new GUIStyle(source)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(
                        source.padding.left,
                        20,
                        source.padding.top,
                        source.padding.bottom)
                };
                if (DimensionsApiImguiTheme.Body != null)
                {
                    layerChipStyle.font = DimensionsApiImguiTheme.Body;
                    layerChipStyle.fontSize = 11;
                }
                SetStyleTextColor(layerChipStyle, DimensionsApiImguiTheme.Periwinkle);
            }

            return layerChipStyle;
        }

        private GUIStyle GetSwatchLabelStyle(bool emphasized)
        {
            GUIStyle style = emphasized ? selectedSwatchLabelStyle : swatchLabelStyle;
            if (style != null)
            {
                return style;
            }

            Color textColor = emphasized
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.82f, 0.86f, 0.9f, 1f);
            style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                font = DimensionsApiImguiTheme.Mono,
                fontSize = 8,
                fontStyle = emphasized ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            SetStyleTextColor(style, textColor);

            if (emphasized)
            {
                selectedSwatchLabelStyle = style;
            }
            else
            {
                swatchLabelStyle = style;
            }

            return style;
        }

        private static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private Texture2D GetLayerPinIcon(bool filled)
        {
            if (filled)
            {
                if (pinnedLayerIconTexture == null)
                {
                    pinnedLayerIconTexture = CreateLayerPinIcon(true);
                }

                return pinnedLayerIconTexture;
            }

            if (unpinnedLayerIconTexture == null)
            {
                unpinnedLayerIconTexture = CreateLayerPinIcon(false);
            }

            return unpinnedLayerIconTexture;
        }

        private static Texture2D CreateLayerPinIcon(bool filled)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = filled ? "PortalLayerPinFilled" : "PortalLayerPinOutline",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!IsLayerPinPixel(x, y))
                    {
                        continue;
                    }

                    bool border = !IsLayerPinPixel(x - 1, y) ||
                                  !IsLayerPinPixel(x + 1, y) ||
                                  !IsLayerPinPixel(x, y - 1) ||
                                  !IsLayerPinPixel(x, y + 1);
                    if (filled || border)
                    {
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool IsLayerPinPixel(int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
            {
                return false;
            }

            if (y >= 11 && y <= 13 && x >= 4 && x <= 11)
            {
                return true;
            }

            if (y >= 7 && y <= 11 && x >= 5 && x <= 10)
            {
                return true;
            }

            if (y >= 6 && y <= 7 && x >= 3 && x <= 12)
            {
                return true;
            }

            if (y >= 2 && y <= 5 && (x == 7 || x == 8))
            {
                return true;
            }

            return y == 1 && x == 7;
        }
    }
}
