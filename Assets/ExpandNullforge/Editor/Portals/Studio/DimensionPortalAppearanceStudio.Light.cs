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
    /// The pool of light a portal throws on the ground, and its gizmo.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        // Measured from the template PugLight (local y 1.244, ground z -0.3625 once the
        // sprite pivot chain is removed) — identical for the placed and the instant portal.
        private const float GroundLightHeightUnits = 1.244f;

        private const float GroundLightGroundZUnits = -0.3625f;

        private const string GroundGlowShaderPath =
            "Assets/ExpandNullforge/Editor/Shaders/PortalStudioGroundGlow.shader";

        private Texture2D groundGlowTexture;

        private Material groundGlowMaterial;

        private string groundGlowSignature;

        /// <summary>
        /// The pool of light the portal throws on the floor, rendered the way the game renders
        /// it rather than hinted at.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything here is the measured runtime, not an artistic impression. The lamp hangs
        /// <see cref="GroundLightHeightUnits"/> above the floor, so a floor point at radius r
        /// is d = sqrt(r² + h²) from it. The game attenuates by a curve authored in the shipped
        /// render-pipeline asset — a two-key Hermite that evaluates to (1 − d/range)² within
        /// ±0.007 everywhere — and multiplies by the light's colour and intensity, added over
        /// zero ambient. The intensity is the flicker midpoint, because LightFlickerEffect
        /// overwrites the authored value with (min + max) / 2 on Awake.
        /// </para>
        /// <para>
        /// The pool needs no tonemap or bloom emulation: at vanilla values its peak sits near
        /// 0.17, far under both the tonemap knee (0.5) and the bloom threshold (1.9). One texel
        /// per game pixel keeps the gradient exactly as chunky as the 270p chain draws it.
        /// </para>
        /// </remarks>
        private void DrawGroundLightPool(Rect canvasScreenRect)
        {
            if (Event.current.type != EventType.Repaint ||
                activeProfile == null ||
                !activeProfile.GroundLightEnabled)
            {
                return;
            }

            float range = Mathf.Max(0f, activeProfile.GroundLightRange);
            if (range <= GroundLightHeightUnits)
            {
                // No pool exists; the gizmo path owns the warning for this case.
                return;
            }

            float radiusPixels = Mathf.Sqrt(
                range * range - GroundLightHeightUnits * GroundLightHeightUnits) *
                DimensionPortalVisualContract.PixelsPerUnit;

            EnsureGroundGlowTexture(range, radiusPixels);
            if (groundGlowTexture == null)
            {
                return;
            }

            EnsureGroundGlowMaterial();
            if (groundGlowMaterial == null)
            {
                return;
            }

            Vector2 offsetPixels = activeProfile.GroundLightOffsetPixels;
            Vector2 groundGamePixels = new Vector2(
                offsetPixels.x,
                GroundLightGroundZUnits * DimensionPortalVisualContract.PixelsPerUnit +
                    offsetPixels.y);
            Vector2 discCenter = groundGamePixels - bodyScreenOrigin;

            Rect poolCanvasRect = new Rect(
                discCenter.x - radiusPixels,
                discCenter.y - radiusPixels,
                radiusPixels * 2f,
                radiusPixels * 2f);
            Rect poolLocal = CanvasRectToGuiRect(poolCanvasRect);
            Rect poolScreen = new Rect(
                canvasScreenRect.x + poolLocal.x,
                canvasScreenRect.y + poolLocal.y,
                poolLocal.width,
                poolLocal.height);

            // Manual clip: only the slice inside the canvas is drawn, with texture coordinates
            // narrowed to match, so the blit cannot reach the page even though it answers to no
            // GUI clip stack. The pool is radially symmetric, so the V origin needs no flip.
            Rect visible = Rect.MinMaxRect(
                Mathf.Max(poolScreen.xMin, canvasScreenRect.xMin),
                Mathf.Max(poolScreen.yMin, canvasScreenRect.yMin),
                Mathf.Min(poolScreen.xMax, canvasScreenRect.xMax),
                Mathf.Min(poolScreen.yMax, canvasScreenRect.yMax));
            if (visible.width <= 0f || visible.height <= 0f)
            {
                return;
            }

            Rect sourceUv = new Rect(
                (visible.xMin - poolScreen.xMin) / poolScreen.width,
                (poolScreen.yMax - visible.yMax) / poolScreen.height,
                visible.width / poolScreen.width,
                visible.height / poolScreen.height);
            Graphics.DrawTexture(
                visible,
                groundGlowTexture,
                sourceUv,
                0,
                0,
                0,
                0,
                Color.white,
                groundGlowMaterial);
        }

        /// <summary>Bakes the pool texture, one texel per game pixel, cached until a field moves.</summary>
        private void EnsureGroundGlowTexture(float range, float radiusPixels)
        {
            Color colour = activeProfile.GroundLightColor;
            float intensity = activeProfile.GroundLightIntensity;
            string signature = string.Concat(
                colour.r.ToString("0.####"), ",",
                colour.g.ToString("0.####"), ",",
                colour.b.ToString("0.####"), ",",
                range.ToString("0.####"), ",",
                intensity.ToString("0.####"));
            if (groundGlowTexture != null && signature == groundGlowSignature)
            {
                return;
            }

            DestroyTexture(ref groundGlowTexture);
            groundGlowSignature = signature;

            int side = Mathf.Max(2, Mathf.CeilToInt(radiusPixels) * 2);
            Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color linear = colour.linear;
            float half = side * 0.5f;
            float pixelsPerUnit = DimensionPortalVisualContract.PixelsPerUnit;
            Color32[] pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float dxUnits = (x + 0.5f - half) / pixelsPerUnit;
                    float dyUnits = (y + 0.5f - half) / pixelsPerUnit;
                    float floorDistance = Mathf.Sqrt(dxUnits * dxUnits + dyUnits * dyUnits);
                    float d = Mathf.Sqrt(
                        floorDistance * floorDistance +
                        GroundLightHeightUnits * GroundLightHeightUnits);
                    float normalized = Mathf.Clamp01(d / range);
                    float attenuation = (1f - normalized) * (1f - normalized);
                    Color texel = new Color(
                        linear.r * intensity * attenuation,
                        linear.g * intensity * attenuation,
                        linear.b * intensity * attenuation,
                        1f).gamma;
                    pixels[y * side + x] = texel;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            groundGlowTexture = texture;
        }

        private void EnsureGroundGlowMaterial()
        {
            if (groundGlowMaterial != null)
            {
                return;
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GroundGlowShaderPath);
            if (shader == null)
            {
                return;
            }

            groundGlowMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private void DrawGroundLightGizmo(Rect localCanvas)
        {
            if (selectedLayer != StudioLayer.GroundLight ||
                activeProfile == null ||
                !activeProfile.GroundLightEnabled)
            {
                return;
            }

            Vector2 offsetPixels = activeProfile.GroundLightOffsetPixels;
            // The offset moves the lamp across the floor: world X and world Z, each of which
            // projects 1:1 into canvas pixels.
            Vector2 groundGamePixels = new Vector2(
                offsetPixels.x,
                GroundLightGroundZUnits * DimensionPortalVisualContract.PixelsPerUnit +
                    offsetPixels.y);
            Vector2 discCenter = groundGamePixels - bodyScreenOrigin;

            float range = Mathf.Max(0f, activeProfile.GroundLightRange);
            if (range <= GroundLightHeightUnits)
            {
                // The light sits above the floor, so a range shorter than that height never
                // reaches the ground at all. Drawing a collapsed ring would claim a pool that
                // does not exist.
                AddPreviewWarning(
                    "The ground light's range (" + range.ToString("0.##") +
                    ") is shorter than its height above the floor (" +
                    GroundLightHeightUnits.ToString("0.###") +
                    "), so it casts no pool on the ground.");
                return;
            }

            float discRadiusPixels = Mathf.Sqrt(
                range * range - GroundLightHeightUnits * GroundLightHeightUnits) *
                DimensionPortalVisualContract.PixelsPerUnit;

            Color ringColor = activeProfile.GroundLightColor;
            ringColor.a = 0.9f;
            float dotSize = Mathf.Max(1.5f, 1f / EditorGUIUtility.pixelsPerPoint * 2f);
            int dotCount = Mathf.Clamp(
                Mathf.RoundToInt(discRadiusPixels * activeZoom * 0.2f),
                24,
                180);
            for (int i = 0; i < dotCount; i++)
            {
                float angle = i * (Mathf.PI * 2f / dotCount);
                Vector2 canvasPoint = discCenter + new Vector2(
                    Mathf.Cos(angle) * discRadiusPixels,
                    Mathf.Sin(angle) * discRadiusPixels);
                Vector2 guiPoint = CanvasPointToGuiPoint(canvasPoint);
                if (guiPoint.x < -dotSize || guiPoint.y < -dotSize ||
                    guiPoint.x > localCanvas.width + dotSize ||
                    guiPoint.y > localCanvas.height + dotSize)
                {
                    continue;
                }

                EditorGUI.DrawRect(
                    new Rect(
                        guiPoint.x - dotSize * 0.5f,
                        guiPoint.y - dotSize * 0.5f,
                        dotSize,
                        dotSize),
                    ringColor);
            }

            // The lamp itself, at its projected height above the disc.
            Vector2 sourceGamePixels = new Vector2(
                offsetPixels.x,
                (GroundLightHeightUnits + GroundLightGroundZUnits) *
                    DimensionPortalVisualContract.PixelsPerUnit + offsetPixels.y);
            Vector2 sourceGui = CanvasPointToGuiPoint(sourceGamePixels - bodyScreenOrigin);
            float markerThickness = Mathf.Max(1f, 1f / EditorGUIUtility.pixelsPerPoint);
            EditorGUI.DrawRect(
                new Rect(sourceGui.x - 5f, sourceGui.y, 10f, markerThickness),
                ringColor);
            EditorGUI.DrawRect(
                new Rect(sourceGui.x, sourceGui.y - 5f, markerThickness, 10f),
                ringColor);
        }
    }
}
