using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The backdrops that give the window its depth. UI Toolkit has no declarative gradients, so
    /// the soft glows and the drafting grid are generated once as textures and reused.
    /// </summary>
    /// <remarks>
    /// The game's own rule decides what these look like: darkness is the default, and colour only
    /// exists where something emits. So the grid is barely there, and the glow is a single quiet
    /// pool of the Core's light rather than a lit background.
    /// </remarks>
    internal static class DimensionsApiTextures
    {
        private static Texture2D radialGlow;
        private static Texture2D draftingGrid;
        private static Texture2D verticalFade;

        /// <summary>A soft round glow, white with a falling alpha, tinted at the use site.</summary>
        internal static Texture2D RadialGlow
        {
            get
            {
                if (radialGlow != null)
                {
                    return radialGlow;
                }

                const int size = 128;
                radialGlow = CreateTexture(size, size, "DimensionsApiRadialGlow");
                Color[] pixels = new Color[size * size];
                float centre = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - centre) / centre;
                        float dy = (y - centre) / centre;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        // Squared falloff reads as light rather than as a drawn circle.
                        float alpha = Mathf.Clamp01(1.0f - distance);
                        alpha *= alpha;
                        pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                    }
                }

                radialGlow.SetPixels(pixels);
                radialGlow.Apply(false, false);
                return radialGlow;
            }
        }

        /// <summary>The faint engineering grid, one cell, meant to be tiled.</summary>
        internal static Texture2D DraftingGrid
        {
            get
            {
                if (draftingGrid != null)
                {
                    return draftingGrid;
                }

                const int cell = 44;
                draftingGrid = CreateTexture(cell, cell, "DimensionsApiDraftingGrid");
                draftingGrid.wrapMode = TextureWrapMode.Repeat;
                Color[] pixels = new Color[cell * cell];
                Color empty = new Color(1.0f, 1.0f, 1.0f, 0.0f);
                Color line = new Color(0.404f, 0.498f, 0.682f, 0.05f);
                for (int y = 0; y < cell; y++)
                {
                    for (int x = 0; x < cell; x++)
                    {
                        pixels[y * cell + x] = x == 0 || y == 0 ? line : empty;
                    }
                }

                draftingGrid.SetPixels(pixels);
                draftingGrid.Apply(false, false);
                return draftingGrid;
            }
        }

        /// <summary>A top-to-bottom darkening wash, stretched across a panel.</summary>
        internal static Texture2D VerticalFade
        {
            get
            {
                if (verticalFade != null)
                {
                    return verticalFade;
                }

                const int height = 64;
                verticalFade = CreateTexture(1, height, "DimensionsApiVerticalFade");
                verticalFade.wrapMode = TextureWrapMode.Clamp;
                Color[] pixels = new Color[height];
                for (int y = 0; y < height; y++)
                {
                    // Texture row 0 is the bottom, which is where the light pools.
                    float t = y / (float)(height - 1);
                    pixels[y] = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp01(1.0f - t) * 0.55f);
                }

                verticalFade.SetPixels(pixels);
                verticalFade.Apply(false, false);
                return verticalFade;
            }
        }

        private static Texture2D CreateTexture(int width, int height, string name)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return texture;
        }
    }
}
