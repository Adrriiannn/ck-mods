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
    /// The overlay that shows where one palette colour is used.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void DrawPaletteFocusOverlay(Rect localCanvas)
        {
            if (!TryGetPaletteFocusFrame(out VisibleFrame frame, out int paletteIndex) ||
                !EnsurePaletteFocusTexture(frame, paletteIndex))
            {
                return;
            }

            EditorGUI.DrawRect(
                localCanvas,
                new Color(0.025f, 0.028f, 0.035f, 0.82f));
            DrawFrameTexture(frame, paletteFocusTexture);
        }

        private bool TryGetPaletteFocusFrame(
            out VisibleFrame focusFrame,
            out int paletteIndex)
        {
            focusFrame = default(VisibleFrame);
            paletteIndex = -1;
            if (!paletteFocusActive ||
                paletteFocusLayer != selectedLayer ||
                paletteFocusRoleIndex < 0)
            {
                return false;
            }

            ColorRole[] roles = GetColorRoles(paletteFocusLayer);
            if (paletteFocusRoleIndex >= roles.Length ||
                !roles[paletteFocusRoleIndex].IsPaletteColor)
            {
                return false;
            }

            int rolePaletteIndex = GetPaletteOrdinal(roles, paletteFocusRoleIndex);
            if (rolePaletteIndex < 0)
            {
                return false;
            }

            if (!TryFindPaletteFocusFrame(
                    paletteFocusLayer,
                    rolePaletteIndex,
                    out focusFrame))
            {
                return false;
            }

            paletteIndex = rolePaletteIndex;
            return true;
        }

        private static int GetPaletteOrdinal(ColorRole[] roles, int roleIndex)
        {
            if (roles == null || roleIndex < 0 || roleIndex >= roles.Length)
            {
                return -1;
            }

            int paletteIndex = 0;
            for (int i = 0; i < roleIndex; i++)
            {
                if (roles[i].IsPaletteColor)
                {
                    paletteIndex++;
                }
            }

            return roles[roleIndex].IsPaletteColor ? paletteIndex : -1;
        }

        private bool CanFocusPaletteRole(
            StudioLayer layer,
            ColorRole[] roles,
            int roleIndex)
        {
            int paletteIndex = GetPaletteOrdinal(roles, roleIndex);
            return paletteIndex >= 0 &&
                   TryFindPaletteFocusFrame(
                       layer,
                       paletteIndex,
                       out VisibleFrame unused);
        }

        private bool TryFindPaletteFocusFrame(
            StudioLayer layer,
            int paletteIndex,
            out VisibleFrame focusFrame)
        {
            focusFrame = default(VisibleFrame);
            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame candidate = visibleFrames[i];
                if (!IsFramePreviewVisible(candidate) ||
                    candidate.Layer != layer ||
                    !candidate.PalettePickingEnabled ||
                    candidate.DirectOverride ||
                    candidate.DisplaySheet == null ||
                    candidate.DisplaySheet.Pixels == null ||
                    candidate.HitSheet == null ||
                    candidate.HitSheet.Pixels == null ||
                    candidate.SourcePalette == null ||
                    paletteIndex < 0 ||
                    paletteIndex >= candidate.SourcePalette.Length)
                {
                    continue;
                }

                focusFrame = candidate;
                return true;
            }

            return false;
        }

        private bool EnsurePaletteFocusTexture(VisibleFrame frame, int paletteIndex)
        {
            int width = frame.DisplaySheet.Width;
            int height = frame.DisplaySheet.Height;
            if (width <= 0 ||
                height <= 0 ||
                frame.HitSheet.Width != width ||
                frame.HitSheet.Height != height ||
                frame.DisplaySheet.Pixels.Length != width * height ||
                frame.HitSheet.Pixels.Length != width * height)
            {
                return false;
            }

            bool cacheMatches =
                paletteFocusTexture != null &&
                paletteFocusTexture.width == width &&
                paletteFocusTexture.height == height &&
                paletteFocusDisplaySheet == frame.DisplaySheet &&
                paletteFocusHitSheet == frame.HitSheet &&
                paletteFocusSourcePalette == frame.SourcePalette &&
                paletteFocusTextureLayer == frame.Layer &&
                paletteFocusTextureRole == paletteIndex;
            if (cacheMatches)
            {
                return true;
            }

            if (paletteFocusTexture == null ||
                paletteFocusTexture.width != width ||
                paletteFocusTexture.height != height)
            {
                DestroyTexture(ref paletteFocusTexture);
                paletteFocusTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "Portal Studio Palette Focus",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            int pixelCount = width * height;
            if (paletteFocusPixels == null ||
                paletteFocusPixels.Length != pixelCount)
            {
                paletteFocusPixels = new Color32[pixelCount];
            }

            Color32 transparent = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixelCount; i++)
            {
                Color32 sourcePixel = frame.HitSheet.Pixels[i];
                if (sourcePixel.a == 0 ||
                    FindClosestPaletteIndex(
                        sourcePixel,
                        frame.SourcePalette) != paletteIndex)
                {
                    paletteFocusPixels[i] = transparent;
                    continue;
                }

                paletteFocusPixels[i] = frame.DisplaySheet.Pixels[i];
            }

            paletteFocusTexture.SetPixels32(paletteFocusPixels);
            paletteFocusTexture.Apply(false, false);
            paletteFocusDisplaySheet = frame.DisplaySheet;
            paletteFocusHitSheet = frame.HitSheet;
            paletteFocusSourcePalette = frame.SourcePalette;
            paletteFocusTextureLayer = frame.Layer;
            paletteFocusTextureRole = paletteIndex;
            return true;
        }
    }
}
