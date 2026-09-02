using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The design system, for the panels that are still drawn in IMGUI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Block Studio and the Portal Studio are canvases: they hit-test pixels, drag layers and
    /// paint previews every frame. Rewriting that machinery would risk the very thing that makes
    /// them worth keeping, so instead they borrow the same palette and the same typefaces as the
    /// rest of the product. Same colours, same fonts, same words — different drawing engine.
    /// </para>
    /// <para>
    /// Every colour here has a twin in <c>DimensionsApi.uss</c>. Change one and change the other.
    /// </para>
    /// </remarks>
    internal static class DimensionsApiImguiTheme
    {
        internal static readonly Color Cavern = new Color32(0x09, 0x10, 0x18, 0xFF);
        internal static readonly Color Depth = new Color32(0x0E, 0x1C, 0x2C, 0xFF);
        // The accent is the title's own light blue, sampled from the game's logo gradient —
        // the old mint green had no kinship with Core Keeper's palette and is gone everywhere.
        internal static readonly Color CoreBlue = new Color32(0x4A, 0xCC, 0xF7, 0xFF);
        internal static readonly Color CoreBluePale = new Color32(0x8C, 0xE8, 0xFF, 0xFF);
        internal static readonly Color Amber = new Color32(0xFF, 0x94, 0x00, 0xFF);
        internal static readonly Color Ember = new Color32(0xE4, 0x50, 0x01, 0xFF);
        internal static readonly Color Lavender = new Color32(0xD7, 0xDC, 0xFB, 0xFF);
        internal static readonly Color Periwinkle = new Color32(0xA1, 0xAE, 0xDD, 0xFF);
        internal static readonly Color Slate = new Color32(0x67, 0x7F, 0xAE, 0xFF);
        internal static readonly Color Seam = new Color(0.404f, 0.498f, 0.682f, 0.22f);

        private const string FontFolder = "Assets/ExpandNullforge/Editor/UI/Fonts/";

        private static Font display;
        private static Font body;
        private static Font bodyStrong;
        private static Font mono;
        private static Texture2D cavernFill;
        private static Texture2D cardFill;
        private static Texture2D sunkenFill;

        /// <summary>Bricolage Grotesque, for titles.</summary>
        internal static Font Display
        {
            get { return display != null ? display : (display = Load("Bricolage-700.ttf")); }
        }

        /// <summary>Hanken Grotesk, for everything read.</summary>
        internal static Font Body
        {
            get { return body != null ? body : (body = Load("Hanken-400.ttf")); }
        }

        internal static Font BodyStrong
        {
            get { return bodyStrong != null ? bodyStrong : (bodyStrong = Load("Hanken-700.ttf")); }
        }

        /// <summary>Spline Sans Mono, for ids and counts.</summary>
        internal static Font Mono
        {
            get { return mono != null ? mono : (mono = Load("SplineMono-400.ttf")); }
        }

        private static Font Load(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Font>(FontFolder + file);
        }

        internal static Texture2D CavernFill
        {
            get { return cavernFill != null ? cavernFill : (cavernFill = Solid(Cavern)); }
        }

        internal static Texture2D CardFill
        {
            get
            {
                return cardFill != null
                    ? cardFill
                    : (cardFill = Solid(new Color(0.035f, 0.090f, 0.125f, 0.55f)));
            }
        }

        internal static Texture2D SunkenFill
        {
            get
            {
                return sunkenFill != null
                    ? sunkenFill
                    : (sunkenFill = Solid(new Color(0.020f, 0.050f, 0.075f, 0.9f)));
            }
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "DimensionsApiThemeFill",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Applies the framework's typeface and colour to a style, keeping its layout.</summary>
        internal static GUIStyle Text(
            GUIStyle source,
            Font font,
            int fontSize,
            Color color)
        {
            GUIStyle style = new GUIStyle(source);
            if (font != null)
            {
                style.font = font;
            }

            if (fontSize > 0)
            {
                style.fontSize = fontSize;
                // A bundled face carries its own weight, so leaving the style bold as well
                // double-bolds it into something heavier than the design ever asks for.
                style.fontStyle = FontStyle.Normal;
            }

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            return style;
        }

        private static GUIStyle cardBox;
        private static GUIStyle sectionLabel;
        private static GUIStyle panelTitle;
        private static GUIStyle caption;

        /// <summary>The container the rebuilt pages call a group card.</summary>
        internal static GUIStyle CardBox
        {
            get
            {
                if (cardBox != null)
                {
                    return cardBox;
                }

                cardBox = new GUIStyle(GUIStyle.none)
                {
                    padding = new RectOffset(12, 12, 10, 12),
                    margin = new RectOffset(0, 0, 4, 8)
                };
                cardBox.normal.background = CardFill;
                return cardBox;
            }
        }

        /// <summary>The small spaced caps that title a group on the rebuilt pages.</summary>
        internal static GUIStyle SectionLabel
        {
            get
            {
                if (sectionLabel != null)
                {
                    return sectionLabel;
                }

                sectionLabel = Text(EditorStyles.miniBoldLabel, Mono, 10, Slate);
                return sectionLabel;
            }
        }

        internal static GUIStyle PanelTitle
        {
            get
            {
                if (panelTitle != null)
                {
                    return panelTitle;
                }

                panelTitle = Text(EditorStyles.boldLabel, BodyStrong, 13, Lavender);
                return panelTitle;
            }
        }

        internal static GUIStyle Caption
        {
            get
            {
                if (caption != null)
                {
                    return caption;
                }

                caption = Text(EditorStyles.miniLabel, Body, 11, Periwinkle);
                return caption;
            }
        }

        /// <summary>A card: the IMGUI twin of the group cards on the rebuilt pages.</summary>
        internal static GUIStyle Card()
        {
            GUIStyle style = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(12, 12, 10, 12),
                margin = new RectOffset(0, 0, 4, 8)
            };
            style.normal.background = CardFill;
            return style;
        }

        /// <summary>
        /// Tints every box, button and field the legacy panels draw toward the framework's own
        /// surfaces, then hands back what was there so the caller can put it back.
        /// </summary>
        internal static void PushTint(out Color previousBackground, out Color previousContent)
        {
            previousBackground = GUI.backgroundColor;
            previousContent = GUI.contentColor;
            // Editor styles multiply their grey plates by this, so a desaturated teal pulls the
            // whole panel onto the cavern palette without repainting a single call site.
            GUI.backgroundColor = new Color(0.55f, 0.80f, 0.93f, 1f);
            GUI.contentColor = new Color(0.93f, 0.95f, 1f, 1f);
        }

        internal static void PopTint(Color previousBackground, Color previousContent)
        {
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
        }

        /// <summary>Fills a rect with the cavern ground the rest of the product sits on.</summary>
        internal static void PaintBackground(Rect rect)
        {
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Cavern);
            }
        }
    }
}
