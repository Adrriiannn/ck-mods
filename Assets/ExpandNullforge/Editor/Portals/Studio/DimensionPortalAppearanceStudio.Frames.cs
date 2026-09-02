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
    /// Adding one frame to the preview, and checking the sheet it came from.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void AddChargingFrame(SerializedObject profile)
        {
            if (!GetBool(profile, "chargeWaveVisible", true))
            {
                return;
            }

            SerializedProperty overrideProperty = profile.FindProperty("chargeWaveSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.ChargeSweep,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.ChargeSweep,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Charge sweep override address could not be resolved in Scriptable Data.");
            }

            AnimationSheet animation = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(ChargeTexturePath, 42),
                    FrameCount = 42,
                    Fps = 27.3f,
                    Loop = true,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla charging sweep"
                }
                : GetAnimationSheet(overrideAsset, 0, 42, 27.3f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                display = GetRecoloredSheet(
                    "charge",
                    animation.Sheet,
                    EffectSourcePalette,
                    GetPalette(profile, ChargePaletteProperties),
                    GetColor(
                        profile,
                        "chargeWaveEmissiveColor",
                        DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor));
            }

            ValidateLayerSheet(
                StudioLayer.ChargeSweep,
                animation,
                CanonicalCanvasPixels,
                CanonicalCanvasPixels,
                "Charge sweep");
            float speed = Mathf.Max(0.01f, GetFloat(profile, "chargeWaveSpeed", 1f));
            int frame = GetAnimationFrame(animation, animationClock * speed);
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.ChargeSweep,
                animation,
                GetVector2(profile, "chargeWaveOffsetPixels", Vector2.zero));
            AddVisibleFrame(
                StudioLayer.ChargeSweep,
                display,
                animation.Sheet,
                layerRect,
                frame,
                GetColor(profile, "chargeWaveTint", Color.white),
                EffectSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "chargeWaveScale", Vector2.one),
                GetFloat(profile, "chargeWaveRotationDegrees", 0f),
                GetBool(profile, "chargeWaveFlipX", false),
                GetBool(profile, "chargeWaveFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddMilestoneFrame(SerializedObject profile, bool ready)
        {
            if (!GetBool(profile, "milestonesVisible", true))
            {
                return;
            }

            // Stage -> frame follows the fixed vanilla sheet order (empty 0, bottom 1,
            // middle 3, upper 4, ready 7) — matching the runtime visual. Frame zero is
            // transparent in the vanilla sheet, so the pre-threshold state draws nothing
            // unless custom artwork fills it in.
            int frame;
            if (ready)
            {
                frame = 7;
            }
            else
            {
                float first = Mathf.Clamp01(GetFloat(profile, "firstMilestone", 0.25f));
                float second = Mathf.Clamp(
                    GetFloat(profile, "secondMilestone", 0.5f),
                    first,
                    1f);
                float third = Mathf.Clamp(
                    GetFloat(profile, "thirdMilestone", 0.75f),
                    second,
                    1f);
                if (chargeProgress >= third)
                {
                    frame = 4;
                }
                else if (chargeProgress >= second)
                {
                    frame = 3;
                }
                else if (chargeProgress >= first)
                {
                    frame = 1;
                }
                else
                {
                    frame = 0;
                }
            }

            SerializedProperty overrideProperty = profile.FindProperty("milestoneSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.Milestones,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.Milestones,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Milestone override address could not be resolved in Scriptable Data.");
            }

            AnimationSheet animation = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(MilestoneTexturePath, 8),
                    FrameCount = 8,
                    Fps = 1f,
                    Loop = true,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla milestone states"
                }
                : GetAnimationSheet(overrideAsset, 0, 8, 1f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                display = GetRecoloredSheet(
                    "milestones",
                    animation.Sheet,
                    EffectSourcePalette,
                    GetPalette(profile, MilestonePaletteProperties),
                    GetColor(
                        profile,
                        "milestoneEmissiveColor",
                        DimensionPortalVisualProfileAsset.VanillaLoadPointEmissiveColor));
            }

            ValidateLayerSheet(
                StudioLayer.Milestones,
                animation,
                CanonicalCanvasPixels,
                CanonicalCanvasPixels,
                "Milestones");
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.Milestones,
                animation,
                GetVector2(profile, "milestoneOffsetPixels", Vector2.zero));
            AddVisibleFrame(
                StudioLayer.Milestones,
                display,
                animation.Sheet,
                layerRect,
                Mathf.Clamp(frame, 0, Mathf.Max(0, animation.FrameCount - 1)),
                GetColor(profile, "milestoneTint", Color.white),
                EffectSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "milestoneScale", Vector2.one),
                GetFloat(profile, "milestoneRotationDegrees", 0f),
                GetBool(profile, "milestoneFlipX", false),
                GetBool(profile, "milestoneFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddCenterFrame(SerializedObject profile)
        {
            if (!GetBool(profile, "centerVisible", true))
            {
                return;
            }

            SerializedProperty overrideProperty = profile.FindProperty("centerEffectSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    CenterArtworkLayer,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                CenterArtworkLayer,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Center override address could not be resolved in Scriptable Data.");
            }

            const int openingIndex = 1;
            const int idleIndex = 0;
            string fallbackOpeningPath = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantOpenTexturePath
                : CenterOpeningTexturePath;
            string fallbackIdlePath = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantIdleTexturePath
                : CenterIdleTexturePath;
            int fallbackOpeningFrames = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantFrameCount
                : CenterOpeningFrameCountVanilla;

            AnimationSheet opening = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(fallbackOpeningPath, fallbackOpeningFrames),
                    FrameCount = fallbackOpeningFrames,
                    Fps = 10f,
                    Loop = false,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla center opening"
                }
                : GetAnimationSheet(overrideAsset, openingIndex, fallbackOpeningFrames, 10f);
            float openingDuration = GetAnimationDuration(opening, 0.4f);
            if (skipCenterOpeningOnNextBuild)
            {
                // Selecting an activated layer should present the useful mature loop,
                // even when preview playback is paused. The explicit Replay opening
                // control remains the way to inspect the one-shot formation sequence.
                activatedClock = Mathf.Max(activatedClock, openingDuration);
                skipCenterOpeningOnNextBuild = false;
            }

            // The one-shot closing replay (instant portal only): the closing sheet runs on a
            // fresh clock, then the preview returns to the mature loop. The clock is clamped
            // past the opening on exit so the opening cannot restart afterwards.
            bool isClosing = false;
            AnimationSheet closing = default(AnimationSheet);
            if (previewCenterClosingActive && instantPortalMode)
            {
                closing = !hasDirectOverride
                    ? new AnimationSheet
                    {
                        Sheet = GetSourceSheet(
                            DimensionPortalInstantArtworkEditorUtility.InstantCloseTexturePath,
                            DimensionPortalInstantArtworkEditorUtility.InstantFrameCount),
                        FrameCount = DimensionPortalInstantArtworkEditorUtility.InstantFrameCount,
                        Fps = 10f,
                        Loop = false,
                        Pivot = new Vector2(0.5f, 0.5f),
                        SourceLabel = "Framework instant center closing"
                    }
                    : GetAnimationSheet(
                        overrideAsset,
                        DimensionPortalInstantArtworkEditorUtility.InstantClosingAnimationIndex,
                        DimensionPortalInstantArtworkEditorUtility.InstantFrameCount,
                        10f);
                float closingDuration = GetAnimationDuration(closing, 0.5f);
                if (closing.Sheet != null && activatedClock < closingDuration)
                {
                    isClosing = true;
                }
                else
                {
                    previewCenterClosingActive = false;
                    activatedClock = Mathf.Max(activatedClock, openingDuration);
                }
            }

            bool isOpening = !isClosing && opening.Sheet != null && activatedClock < openingDuration;
            AnimationSheet animation = isClosing
                ? closing
                : isOpening
                    ? opening
                    : !hasDirectOverride
                        ? new AnimationSheet
                        {
                            Sheet = GetSourceSheet(fallbackIdlePath, 5),
                            FrameCount = 5,
                            Fps = 10f,
                            Loop = true,
                            Pivot = new Vector2(0.5f, 0.5f),
                            SourceLabel = "Framework vanilla center idle"
                        }
                        : GetAnimationSheet(overrideAsset, idleIndex, 5, 10f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                // The runtime adds emissive x tint x glow intensity at draw time; routing the
                // same product through the shared emission approximation makes Highlight
                // brightness readable on the canvas like the charge and milestone layers.
                Color centerTintForEmission = GetColor(profile, "centerTint", Color.white);
                Color centerEmissive = GetColor(
                    profile,
                    "centerEmissiveColor",
                    DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor);
                float glowIntensity = Mathf.Clamp01(GetFloat(
                    profile,
                    "centerGlowIntensity",
                    DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity));
                display = GetRecoloredSheet(
                    isClosing ? "center-closing" : isOpening ? "center-opening" : "center-idle",
                    animation.Sheet,
                    CenterSourcePalette,
                    GetPalette(profile, CenterPaletteProperties),
                    new Color(
                        centerEmissive.r * centerTintForEmission.r * glowIntensity,
                        centerEmissive.g * centerTintForEmission.g * glowIntensity,
                        centerEmissive.b * centerTintForEmission.b * glowIntensity,
                        1f));
            }

            currentCenterOpeningDuration = openingDuration;
            currentCenterIsOpening = isOpening;
            ValidateLayerSheet(
                StudioLayer.Center,
                animation,
                DimensionPortalVisualContract.CanonicalCenterWidth,
                DimensionPortalVisualContract.CanonicalCenterHeight,
                isClosing ? "Center closing" : isOpening ? "Center opening" : "Center idle",
                true);
            float localClock = isOpening || isClosing
                ? activatedClock
                : Mathf.Max(0f, activatedClock - openingDuration);
            int frame = GetAnimationFrame(animation, localClock);
            Vector2 centerOffset = GetVector2(profile, "centerOffsetPixels", Vector2.zero);
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.Center,
                animation,
                centerOffset);
            AddVisibleFrame(
                StudioLayer.Center,
                display,
                animation.Sheet,
                layerRect,
                frame,
                GetColor(profile, "centerTint", Color.white),
                CenterSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "centerScale", Vector2.one),
                GetFloat(profile, "centerRotationDegrees", 0f),
                GetBool(profile, "centerFlipX", false),
                GetBool(profile, "centerFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddVisibleFrame(
            StudioLayer layer,
            PreviewSheet displaySheet,
            PreviewSheet hitSheet,
            Rect canvasRect,
            int frameIndex,
            Color tint,
            Color32[] sourcePalette,
            bool palettePickingEnabled,
            Vector2 pivot,
            Vector2 scale,
            float rotationDegrees,
            bool flipX,
            bool flipY,
            string sourceLabel,
            bool directOverride)
        {
            if (displaySheet == null || displaySheet.Texture == null)
            {
                return;
            }

            VisibleFrame visibleFrame = new VisibleFrame
            {
                Layer = layer,
                DisplaySheet = displaySheet,
                HitSheet = hitSheet,
                CanvasRect = canvasRect,
                FrameIndex = frameIndex,
                Tint = tint,
                SourcePalette = sourcePalette,
                PalettePickingEnabled = palettePickingEnabled,
                Pivot = pivot,
                Scale = ClampPreviewScale(scale),
                RotationDegrees = NormalizePreviewRotation(rotationDegrees),
                FlipX = flipX,
                FlipY = flipY,
                SourceLabel = sourceLabel ?? string.Empty,
                DirectOverride = directOverride
            };
            visibleFrames.Add(visibleFrame);
            ValidateTransformedFrameBounds(visibleFrame);
        }

        private Rect ResolveLayerRect(
            DimensionPortalVisualContract.Layer layer,
            AnimationSheet animation,
            Vector2 centerOffsetPixels)
        {
            Vector2Int bodySize = new Vector2Int(canvasPixelWidth, canvasPixelHeight);
            Vector2Int layerSize = new Vector2Int(
                animation.Sheet == null ? 1 : Mathf.Max(1, animation.Sheet.FrameWidth),
                animation.Sheet == null ? 1 : Mathf.Max(1, animation.Sheet.Height));
            Rect rect = DimensionPortalVisualContract.ResolveBodyLocalRect(
                bodySize,
                bodyPivot,
                layerSize,
                animation.Pivot,
                DimensionPortalVisualContract.GetLocalPosition(layer, centerOffsetPixels));
            rect.x = SnapNearPixel(rect.x);
            rect.y = SnapNearPixel(rect.y);
            if (!DimensionPortalVisualContract.IsPixelAligned(rect))
            {
                AddPreviewWarning(
                    GetLayerTitle(ToStudioLayer(layer)) + " lands between source pixels at " +
                    FormatRect(rect) + ". The preview preserves the runtime transform; use a pixel-aligned pivot or center offset for crisp point-filtered artwork.");
            }

            return rect;
        }

        private void ValidateTransformedFrameBounds(VisibleFrame frame)
        {
            Vector2 bottomLeft = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMin, frame.CanvasRect.yMin));
            Vector2 bottomRight = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMax, frame.CanvasRect.yMin));
            Vector2 topRight = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMax, frame.CanvasRect.yMax));
            Vector2 topLeft = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMin, frame.CanvasRect.yMax));
            float minimumX = Mathf.Min(
                Mathf.Min(bottomLeft.x, bottomRight.x),
                Mathf.Min(topRight.x, topLeft.x));
            float maximumX = Mathf.Max(
                Mathf.Max(bottomLeft.x, bottomRight.x),
                Mathf.Max(topRight.x, topLeft.x));
            float minimumY = Mathf.Min(
                Mathf.Min(bottomLeft.y, bottomRight.y),
                Mathf.Min(topRight.y, topLeft.y));
            float maximumY = Mathf.Max(
                Mathf.Max(bottomLeft.y, bottomRight.y),
                Mathf.Max(topRight.y, topLeft.y));
            if (minimumX >= -0.001f &&
                minimumY >= -0.001f &&
                maximumX <= canvasPixelWidth + 0.001f &&
                maximumY <= canvasPixelHeight + 0.001f)
            {
                return;
            }

            AddPreviewWarning(
                GetLayerTitle(frame.Layer) +
                " extends outside the 48 x 48 portal artboard after its layout transform. " +
                "The Studio and the generated portal both preserve the transform and clip any pixels beyond the authored canvas.");
        }

        private static float SnapNearPixel(float value)
        {
            float rounded = Mathf.Round(value);
            return Mathf.Abs(value - rounded) < 0.01f ? rounded : value;
        }

        private static StudioLayer ToStudioLayer(DimensionPortalVisualContract.Layer layer)
        {
            switch (layer)
            {
                case DimensionPortalVisualContract.Layer.ChargeSweep:
                    return StudioLayer.ChargeSweep;
                case DimensionPortalVisualContract.Layer.Milestones:
                    return StudioLayer.Milestones;
                case DimensionPortalVisualContract.Layer.Center:
                    return StudioLayer.Center;
                default:
                    return StudioLayer.Frame;
            }
        }

        private void ValidateLayerSheet(
            StudioLayer layer,
            AnimationSheet animation,
            int expectedWidth,
            int expectedHeight,
            string label,
            bool allowFullCanvasFrames = false)
        {
            if (animation.Sheet == null || animation.Sheet.Texture == null)
            {
                AddPreviewError(label + " has no usable source texture.");
                return;
            }

            if (animation.Sheet.Width % Mathf.Max(1, animation.FrameCount) != 0)
            {
                AddPreviewError(
                    label + " sheet width " + animation.Sheet.Width +
                    " is not divisible by its " + animation.FrameCount + " source frames.");
            }

            // Non-vanilla frame sizes are fully supported (shown at native size/pivot, never
            // stretched) — deliberately no advisory about them; creators chose their size.

            if (animation.Sheet.Pixels == null)
            {
                AddPreviewWarning(
                    label + " is not backed by a directly readable PNG. It can be displayed, but alpha-aware pixel picking is approximate.");
            }
        }

        private static int GetAnimationFrame(AnimationSheet animation, float time)
        {
            if (animation.FrameCount <= 1 || animation.Fps <= 0f)
            {
                return 0;
            }

            int runtimeFrameCount = animation.RuntimeFrameRemap != null &&
                                    animation.RuntimeFrameRemap.Length > 0
                ? animation.RuntimeFrameRemap.Length
                : animation.FrameCount;
            int runtimeFrame = Mathf.FloorToInt(Mathf.Max(0f, time) * animation.Fps);
            runtimeFrame = animation.Loop
                ? ((runtimeFrame % runtimeFrameCount) + runtimeFrameCount) % runtimeFrameCount
                : Mathf.Clamp(runtimeFrame, 0, runtimeFrameCount - 1);
            if (animation.RuntimeFrameRemap != null &&
                animation.RuntimeFrameRemap.Length == runtimeFrameCount)
            {
                return Mathf.Clamp(
                    animation.RuntimeFrameRemap[runtimeFrame],
                    0,
                    animation.FrameCount - 1);
            }

            return Mathf.Clamp(runtimeFrame, 0, animation.FrameCount - 1);
        }

        private static float GetAnimationDuration(AnimationSheet animation, float fallback)
        {
            if (animation.Fps <= 0f)
            {
                return fallback;
            }

            int runtimeFrames = animation.RuntimeFrameRemap != null &&
                                animation.RuntimeFrameRemap.Length > 0
                ? animation.RuntimeFrameRemap.Length
                : animation.FrameCount;
            return Mathf.Max(0.01f, runtimeFrames / animation.Fps);
        }

        private void AddPreviewWarning(string message)
        {
            if (!string.IsNullOrEmpty(message) && !previewWarnings.Contains(message))
            {
                previewWarnings.Add(message);
            }
        }

        private void AddPreviewError(string message)
        {
            if (!string.IsNullOrEmpty(message) && !previewErrors.Contains(message))
            {
                previewErrors.Add(message);
            }
        }
    }
}
