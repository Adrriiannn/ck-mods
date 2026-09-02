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
    /// Working out which frames the preview should show for the state it is in.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        private void EnsurePreviewComposition(SerializedObject profile)
        {
            DimensionPortalVisualProfileAsset target = profile == null
                ? null
                : profile.targetObject as DimensionPortalVisualProfileAsset;
            int targetDirtyCount = target == null
                ? -1
                : EditorUtility.GetDirtyCount(target);
            if (!hasPreviewComposition ||
                previewCompositionProfile != target ||
                previewCompositionProfileDirtyCount != targetDirtyCount)
            {
                previewCompositionDirty = true;
            }

            if (!previewCompositionDirty)
            {
                return;
            }

            Event current = Event.current;
            if (current != null && current.type != EventType.Layout)
            {
                // One immutable composition is shared by the Layout/Repaint pair. Input
                // events merely mark it dirty; the next Layout samples serialized state
                // and animation clocks once, preventing expensive asset inspection from
                // running for every mouse/key/repaint event.
                return;
            }

            BuildVisibleFrames(profile);
            hasPreviewComposition = true;
            previewCompositionProfile = target;
            previewCompositionProfileDirtyCount = target == null
                ? -1
                : EditorUtility.GetDirtyCount(target);
            previewCompositionDirty = false;
        }

        private void BuildVisibleFrames(SerializedObject profile)
        {
            visibleFrames.Clear();
            previewWarnings.Clear();
            previewErrors.Clear();

            SerializedProperty frameOverride = profile.FindProperty("portalFrameSpriteAsset");
            DimensionPortalArtworkReferenceKind frameReferenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                frameOverride,
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.Frame,
                out SpriteAsset frameAsset);
            bool hasCustomFrameArtwork =
                frameReferenceKind == DimensionPortalArtworkReferenceKind.Managed ||
                frameReferenceKind == DimensionPortalArtworkReferenceKind.External;
            if (frameReferenceKind == DimensionPortalArtworkReferenceKind.Unresolved)
            {
                AddPreviewError("Frame override address could not be resolved in Scriptable Data.");
            }

            SpriteAsset effectiveFrameAsset = hasCustomFrameArtwork && frameAsset != null
                ? frameAsset
                : DimensionPortalArtworkEditorUtility.GetFrameworkAsset(
                    DimensionPortalArtworkLayer.Frame);
            SpriteData frameData = effectiveFrameAsset == null
                ? null
                : effectiveFrameAsset.staticSpriteData;
            Texture2D frameTexture = frameData == null ? null : frameData.texture;
            Texture2D frameEmissiveTexture = frameData == null
                ? null
                : frameData.emissiveTexture;
            if (TryGetPendingTextureSlot(
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.Frame,
                    0,
                    out Texture2D pendingFrameTexture,
                    out Texture2D pendingFrameEmissiveTexture))
            {
                frameTexture = pendingFrameTexture;
                frameEmissiveTexture = pendingFrameEmissiveTexture;
            }

            if (hasCustomFrameArtwork && frameTexture == null)
            {
                AddPreviewError("Frame override must contain a static portal-frame texture.");
            }

            PreviewSheet frameSheet = frameTexture != null
                ? GetSourceSheet(frameTexture, 1)
                : GetSourceSheet(FrameTexturePath, 1);
            PreviewSheet frameDisplaySheet = GetFrameCompositeSheet(
                frameTexture,
                frameEmissiveTexture,
                GetColor(profile, "frameEmissiveColor", Color.clear));
            if (frameDisplaySheet == null)
            {
                frameDisplaySheet = frameSheet;
            }
            bodyPivot = frameData != null
                ? frameData.pivot
                : new Vector2(0.5f, 0.5f);
            canvasPixelWidth = frameSheet == null
                ? CanonicalCanvasPixels
                : Mathf.Max(1, frameSheet.FrameWidth);
            canvasPixelHeight = frameSheet == null
                ? CanonicalCanvasPixels
                : Mathf.Max(1, frameSheet.Height);
            bodyScreenOrigin = DimensionPortalVisualContract.GetBodyScreenOrigin(
                new Vector2Int(canvasPixelWidth, canvasPixelHeight),
                bodyPivot);
            if (canvasPixelWidth != CanonicalCanvasPixels ||
                canvasPixelHeight != CanonicalCanvasPixels)
            {
                AddPreviewWarning(
                    "Frame override is " + canvasPixelWidth + " x " + canvasPixelHeight +
                    "; vanilla portal overlays are authored for 48 x 48. The preview keeps native size and pivot instead of stretching it.");
            }

            bool customSwirlPreview =
                previewPhase != PreviewPhase.Charging &&
                !previewCenterClosingActive &&
                GetBool(profile, "centerSwirlVisible", true) &&
                GetBool(profile, "centerSwirlOverrideVanilla", false);
            if (customSwirlPreview)
            {
                // Custom swirl artwork belongs behind the physical portal and its center
                // ring. Drawing it first lets those opaque pixels form the natural inner
                // aperture instead of allowing the swirl to paint across the frame.
                BuildFleckPreview(profile);
            }

            if (GetBool(profile, "frameVisible", true))
            {
                Vector2 frameOffset = GetVector2(
                    profile,
                    "frameOffsetPixels",
                    Vector2.zero);
                AddVisibleFrame(
                    StudioLayer.Frame,
                    frameDisplaySheet,
                    frameSheet,
                    new Rect(
                        frameOffset.x,
                        frameOffset.y,
                        canvasPixelWidth,
                        canvasPixelHeight),
                    0,
                    GetColor(profile, "frameTint", Color.white),
                    null,
                    false,
                    bodyPivot,
                    GetVector2(profile, "frameScale", Vector2.one),
                    GetFloat(profile, "frameRotationDegrees", 0f),
                    GetBool(profile, "frameFlipX", false),
                    GetBool(profile, "frameFlipY", false),
                    hasCustomFrameArtwork
                        ? frameAsset == null ? "Unresolved override" : frameAsset.name
                        : "Framework vanilla frame",
                    frameReferenceKind == DimensionPortalArtworkReferenceKind.External);
            }

            if (previewPhase == PreviewPhase.Charging)
            {
                AddChargingFrame(profile);
                AddMilestoneFrame(profile, false);
            }
            else
            {
                AddMilestoneFrame(profile, true);
                AddCenterFrame(profile);
                // The in-game close hides the swirls the moment it starts, so the closing
                // replay does too. AddCenterFrame clears the flag when the one-shot ends,
                // which lets the flecks return on the same repaint.
                if (!customSwirlPreview && !previewCenterClosingActive)
                {
                    BuildFleckPreview(profile);
                }
            }
        }

        private void BuildFleckPreview(SerializedObject profile)
        {
            fleckPreviewState = default(FleckPreviewState);
            if (profile == null)
            {
                return;
            }

            if (!GetBool(profile, "centerSwirlVisible", true))
            {
                return;
            }

            if (GetBool(profile, "centerSwirlOverrideVanilla", false))
            {
                BuildCustomSwirlPreview(profile);
                return;
            }

            fleckPreviewState = new FleckPreviewState
            {
                Visible = true,
                Origin = new Vector2(
                    CanonicalCanvasPixels * 0.5f,
                    2f + DimensionPortalVisualContract.CanonicalCenterHeight * 0.5f),
                Scale = Vector2.one,
                EmissionMultiplier = 1f,
                SizeMultiplier = 1f,
                RadiusMultiplier = 1f
            };
        }

        private void BuildCustomSwirlPreview(SerializedObject profile)
        {
            if (!TryResolveCustomSwirlAsset(
                    profile,
                    out SpriteAsset swirlAsset,
                    out string resolveError))
            {
                AddPreviewError(resolveError);
                return;
            }

            if (!TryValidateCustomSwirlAsset(swirlAsset, out string validationError))
            {
                AddPreviewError(validationError);
                return;
            }

            AnimationSheet animation = GetAnimationSheet(swirlAsset, 0, 1, 10f);
            if (animation.Sheet == null || animation.Sheet.Texture == null)
            {
                // GetAnimationSheet records the specific SpriteAsset error.
                return;
            }

            float playbackSpeed = Mathf.Max(
                0.01f,
                GetFloat(profile, "centerSwirlPlaybackSpeed", 1f));
            int frame = GetAnimationFrame(animation, activatedClock * playbackSpeed);
            Vector2 offset = GetVector2(
                profile,
                "centerParticleOffsetPixels",
                Vector2.zero);
            FrameAnimation sourceAnimation = swirlAsset.GetAnimationAt(0);
            Texture2D albedoTexture = sourceAnimation == null || sourceAnimation.spriteData == null
                ? null
                : sourceAnimation.spriteData.texture;
            Texture2D emissiveTexture = sourceAnimation == null || sourceAnimation.spriteData == null
                ? null
                : sourceAnimation.spriteData.emissiveTexture;
            Color previewEmission = GetColor(
                profile,
                "centerSwirlEmissiveColor",
                Color.white);
            // A profile-owned swirl bakes the color into its pixels, so preview it neutral.
            // The shared white framework starter is still tinted live so a color drag reads
            // immediately, before the debounced bake materializes the owned asset.
            Color previewTint =
                DimensionPortalSwirlArtworkEditorUtility.IsFrameworkAsset(swirlAsset)
                    ? GetColor(profile, "centerParticleTint", Color.white)
                    : Color.white;
            float emissionMultiplier = Mathf.Max(
                0f,
                GetFloat(profile, "centerParticleEmissionMultiplier", 1f));
            previewEmission.r *= emissionMultiplier;
            previewEmission.g *= emissionMultiplier;
            previewEmission.b *= emissionMultiplier;
            Color compositeEmission = new Color(
                previewEmission.r * previewTint.r,
                previewEmission.g * previewTint.g,
                previewEmission.b * previewTint.b,
                1f);
            PreviewSheet display = GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                previewTint,
                compositeEmission,
                animation.Sheet.FrameCount);
            if (display == null)
            {
                display = animation.Sheet;
            }

            // The runtime multiplies the untinted swirl pixels by CenterParticleTint and
            // adds the emissive at draw time. When GetFrameCompositeSheet has already
            // composited that tint into the preview sheet, draw it neutral; otherwise apply
            // the tint here so the Studio preview matches the in-game material tint exactly.
            Color drawTint = ReferenceEquals(display, animation.Sheet)
                ? previewTint
                : Color.white;

            // Centre the full-canvas swirl sheet on the aperture (the same anchor the vanilla
            // fleck preview and the runtime SpriteObject use), not the outer 48x48 frame
            // centre, so its flecks land inside the inner circle by default.
            AddVisibleFrame(
                StudioLayer.InnerFlecks,
                display,
                animation.Sheet,
                new Rect(
                    offset.x,
                    offset.y + 2f +
                        DimensionPortalVisualContract.CanonicalCenterHeight * 0.5f -
                        CanonicalCanvasPixels * 0.5f,
                    CanonicalCanvasPixels,
                    CanonicalCanvasPixels),
                frame,
                drawTint,
                null,
                false,
                animation.Pivot,
                GetVector2(
                    profile,
                    "centerParticleScale",
                    Vector2.one),
                GetFloat(profile, "centerParticleRotationDegrees", 0f),
                GetBool(profile, "centerSwirlFlipX", false),
                GetBool(profile, "centerSwirlFlipY", false),
                animation.SourceLabel,
                true);
        }

        private static bool TryResolveCustomSwirlAsset(
            SerializedObject profile,
            out SpriteAsset asset,
            out string error)
        {
            asset = null;
            error = string.Empty;
            DimensionPortalVisualProfileAsset visualProfile = profile == null
                ? null
                : profile.targetObject as DimensionPortalVisualProfileAsset;
            if (visualProfile == null)
            {
                error = "The active portal profile is unavailable.";
                return false;
            }

            SerializedProperty reference = profile.FindProperty("centerSwirlSpriteAsset");
            if (reference == null)
            {
                error =
                    "The active portal profile does not expose an Artwork override for Swirls.";
                return false;
            }

            DimensionPortalPackageEditorUtility.TryGetPackage(
                visualProfile,
                out _,
                out string packageFolder);
            if (!DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    visualProfile,
                    reference,
                    packageFolder,
                    out asset))
            {
                error =
                    "Override vanilla is enabled, but the Artwork override address could not " +
                    "be resolved in Scriptable Data.";
                return false;
            }

            if (asset == null)
            {
                error =
                    "Override vanilla is enabled, but Artwork override has no SpriteAsset. " +
                    "Select a looping 48 x 48 SpriteAsset before applying this profile.";
                return false;
            }

            return true;
        }

        private static bool TryValidateCustomSwirlAsset(
            SpriteAsset asset,
            out string error)
        {
            error = string.Empty;
            if (asset == null || asset.animationCount <= 0)
            {
                error = "Swirl Artwork override must contain animation 0.";
                return false;
            }

            FrameAnimation animation = asset.GetAnimationAt(0);
            if (animation == null || animation.spriteData == null)
            {
                error = "Swirl Artwork override animation 0 has no SpriteData.";
                return false;
            }

            Texture2D texture = animation.spriteData.GetSrcTexture();
            if (texture == null)
            {
                error = "Swirl Artwork override animation 0 has no source texture.";
                return false;
            }

            int frameCount = animation.srcFrameCount;
            if (frameCount <= 0)
            {
                error = "Swirl Artwork override animation 0 has no source frames.";
                return false;
            }

            if (texture.width % frameCount != 0)
            {
                error =
                    "Swirl Artwork override sheet width " + texture.width +
                    " is not divisible by its " + frameCount + " frames.";
                return false;
            }

            int frameWidth = texture.width / frameCount;
            if (frameWidth <= 0 ||
                frameWidth > CanonicalCanvasPixels ||
                texture.height > CanonicalCanvasPixels)
            {
                error =
                    "Swirl Artwork override animation 0 frames must fit within the 48 x 48 " +
                    "portal canvas. Current frames are " + frameWidth + " x " +
                    texture.height + ".";
                return false;
            }

            if (!animation.loop)
            {
                error = "Swirl Artwork override animation 0 must be configured to loop.";
                return false;
            }

            return true;
        }
    }
}
