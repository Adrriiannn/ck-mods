using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The eight colours a dish is drawn in, and the four shades an ingredient lends it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CORE KEEPER RECOLOURS ONE DISH SPRITE RATHER THAN DRAWING ONE PER PAIR. The dish carries
    /// eight source colours; at draw time the game builds a two-row lookup texture whose top row is
    /// those eight and whose bottom row is the two ingredients' four-shade ramps, and the sprite
    /// shader swaps every pixel that matches. Four of the eight belong to the leading ingredient and
    /// four to the second.
    /// </para>
    /// <para>
    /// THE MATCH IS EXACT, PER CHANNEL, AT EIGHT BITS. A pixel one step off the template colour is
    /// not "close enough" — it is simply left as drawn, which reads as a stain on the plate that
    /// never changes colour. That is why the palette is a fixed, deliberately artificial set rather
    /// than something pretty: nobody paints these by accident, and the check below can therefore
    /// name every pixel that will not be recoloured.
    /// </para>
    /// <para>
    /// Vanilla dishes leave unused slots at a (254,0,0) placeholder, so a family whose sprite uses
    /// only some of the eight is normal and is not warned about.
    /// </para>
    /// </remarks>
    internal static class DimensionFoodPalette
    {
        /// <summary>How many shades one ingredient lends, brightest first.</summary>
        internal const int ShadesPerIngredient = 4;

        /// <summary>
        /// The eight colours a dish sprite must be painted in: four reds for the leading
        /// ingredient's region, four blues for the second's.
        /// </summary>
        /// <remarks>
        /// Pure primaries at four steps each. Chosen because they are trivial to pick in any pixel
        /// editor, impossible to hit by accident while shading, and obviously wrong if a creator
        /// forgets to check the result in game.
        /// </remarks>
        private static readonly Color32[] Template =
        {
            new Color32(255, 0, 0, 255),
            new Color32(200, 0, 0, 255),
            new Color32(145, 0, 0, 255),
            new Color32(90, 0, 0, 255),
            new Color32(0, 0, 255, 255),
            new Color32(0, 0, 200, 255),
            new Color32(0, 0, 145, 255),
            new Color32(0, 0, 90, 255),
        };

        /// <summary>The eight template colours, brightest to darkest within each ingredient.</summary>
        internal static IReadOnlyList<Color32> SourceColors
        {
            get { return Template; }
        }

        /// <summary>One template colour as the generator writes it onto the dish.</summary>
        internal static Color SourceColor(int index)
        {
            return Template[Mathf.Clamp(index, 0, Template.Length - 1)];
        }

        /// <summary>The template colours written the way a pixel editor asks for them.</summary>
        internal static string DescribeTemplate()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < Template.Length; i++)
            {
                if (i == 0)
                {
                    text.Append("Leading ingredient: ");
                }
                else if (i == ShadesPerIngredient)
                {
                    text.Append("  ·  Second ingredient: ");
                }
                else
                {
                    text.Append(", ");
                }

                text.Append('#').Append(ColorUtility.ToHtmlStringRGB(Template[i]));
            }

            return text.ToString();
        }

        /// <summary>
        /// The four shades an ingredient lends, read off its own picture brightest to darkest.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creator should not have to eyedropper their own artwork to fill in four colour fields
        /// that are already sitting in the file. Every opaque colour in the picture is collected,
        /// ordered by how light it is, and four are taken at even spacing across that order — so a
        /// two-colour sprite gives four shades that repeat rather than four that are wrong, and a
        /// richly shaded one gives a ramp that spans it.
        /// </para>
        /// <para>
        /// Nearly-transparent pixels are skipped. Anti-aliased edges would otherwise drag the ramp
        /// towards whatever happens to be behind the sprite in the sheet.
        /// </para>
        /// </remarks>
        internal static bool TryExtractRamp(Sprite sprite, out Color[] shades)
        {
            shades = null;
            Color32[] pixels;
            if (!TryReadPixels(sprite, out pixels) || pixels.Length == 0)
            {
                return false;
            }

            List<Color32> distinct = new List<Color32>();
            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < 128)
                {
                    continue;
                }

                Color32 opaque = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 255);
                if (seen.Add(Key(opaque)))
                {
                    distinct.Add(opaque);
                }
            }

            if (distinct.Count == 0)
            {
                return false;
            }

            distinct.Sort(delegate(Color32 a, Color32 b)
            {
                float difference = Luminance(b) - Luminance(a);
                if (difference > 0f)
                {
                    return 1;
                }

                return difference < 0f ? -1 : Key(a).CompareTo(Key(b));
            });

            shades = new Color[ShadesPerIngredient];
            for (int i = 0; i < ShadesPerIngredient; i++)
            {
                int index = distinct.Count == 1
                    ? 0
                    : Mathf.RoundToInt(i * (distinct.Count - 1) / (float)(ShadesPerIngredient - 1));
                shades[i] = distinct[Mathf.Clamp(index, 0, distinct.Count - 1)];
            }

            return true;
        }

        /// <summary>
        /// The colours in a dish sprite that the game will not recolour, in the order it finds them.
        /// </summary>
        /// <remarks>
        /// Empty is the good answer. Anything listed here stays exactly as painted no matter what
        /// went into the pot, which is correct for a plate or a fork and wrong for the food.
        /// </remarks>
        internal static List<Color32> FindColoursOutsideTheTemplate(Sprite sprite, int limit)
        {
            List<Color32> strangers = new List<Color32>();
            Color32[] pixels;
            if (!TryReadPixels(sprite, out pixels))
            {
                return strangers;
            }

            HashSet<int> known = new HashSet<int>();
            for (int i = 0; i < Template.Length; i++)
            {
                known.Add(Key(Template[i]));
            }

            for (int i = 0; i < pixels.Length && strangers.Count < limit; i++)
            {
                if (pixels[i].a < 128)
                {
                    continue;
                }

                Color32 opaque = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 255);
                if (known.Add(Key(opaque)))
                {
                    strangers.Add(opaque);
                }
            }

            return strangers;
        }

        /// <summary>
        /// The sprite's own pixels, decoded from the file on disk.
        /// </summary>
        /// <remarks>
        /// Straight from the PNG rather than through the imported texture, because an imported
        /// sprite is not readable unless somebody remembered to tick Read/Write and a colour tool
        /// that silently fails on most projects is worse than no tool. Only the sprite's own
        /// rectangle is returned, so one entry in a packed sheet does not pick up its neighbours.
        /// </remarks>
        internal static bool TryReadPixels(Sprite sprite, out Color32[] pixels)
        {
            pixels = null;
            if (sprite == null || sprite.texture == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            string absolutePath = System.IO.Path
                .Combine(projectRoot ?? string.Empty, assetPath)
                .Replace('\\', '/');
            if (!System.IO.File.Exists(absolutePath))
            {
                return false;
            }

            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                if (!decoded.LoadImage(System.IO.File.ReadAllBytes(absolutePath)))
                {
                    return false;
                }

                Rect rect = sprite.rect;
                int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, decoded.width);
                int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, decoded.height);
                int width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 0, decoded.width - x);
                int height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 0, decoded.height - y);
                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                Color[] region = decoded.GetPixels(x, y, width, height);
                pixels = new Color32[region.Length];
                for (int i = 0; i < region.Length; i++)
                {
                    pixels[i] = region[i];
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
            }
        }

        private static int Key(Color32 color)
        {
            return (color.r << 16) | (color.g << 8) | color.b;
        }

        private static float Luminance(Color32 color)
        {
            return (0.299f * color.r) + (0.587f * color.g) + (0.114f * color.b);
        }
    }
}
