using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Portals;
using ExpandNullforge.Scenes;
using Pug.Sprite;
using PugMod;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which sprite asset each portal layer draws from, and the material behind the body.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private struct PortalSpriteObjectSet
        {
            public SpriteObject Body;
            public SpriteRenderer BodyRenderer;
            public SpriteObject ChargeProgress;
            public SpriteObject EmissiveWave;
            public SpriteObject CenterEffect;
            public SpriteObject CustomSwirl;
            public GameObject CenterParticlesRoot;
            public GameObject ReadyFlashRoot;
            public SpriteObject OutlineMask;
            public SpriteObject OutlineSupportMask;
            public SpriteObject OutlineCap;
            public GameObject ShadowRoot;
            public Renderer Shadow;
            public Renderer ShadowCaster;
        }

        private struct GeneratedPortalSpriteAsset
        {
            public long AddressLow;
            public long AddressHigh;
            public SpriteAsset Asset;
        }

        private struct GeneratedPortalOutlineSpriteAssets
        {
            public GeneratedPortalSpriteAsset Main;
            public GeneratedPortalSpriteAsset Support;
            public GeneratedPortalSpriteAsset Cap;
        }

        private struct PortalVisualSpriteAssets
        {
            public GeneratedPortalSpriteAsset Body;
            public SpriteAsset BodySource;
            public Sprite BodyRendererSprite;
            public Material BodyRendererMaterial;
            public GeneratedPortalSpriteAsset ChargeProgress;
            public GeneratedPortalSpriteAsset EmissiveWave;
            public GeneratedPortalSpriteAsset CenterEffect;
            public GeneratedPortalSpriteAsset CustomSwirl;
        }

        private static PortalVisualSpriteAssets ResolvePortalVisualSpriteAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot)
        {
            DimensionPortalVisualProfileAsset profile = portalOutput.VisualProfile;
            SpriteAsset bodyOverride = profile == null
                ? null
                : ResolvePortalSpriteAssetReference(
                    profile.PortalFrameSpriteAsset,
                    "portal frame");
            SpriteAsset bodySource = bodyOverride != null
                ? bodyOverride
                : AssetDatabase.LoadAssetAtPath<SpriteAsset>(PortalBodySpriteAssetPath);
            if (bodySource == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the framework portal frame SpriteAsset at " +
                    PortalBodySpriteAssetPath + ".");
            }

            GeneratedPortalSpriteAsset body = ResolvePortalSpriteAssetOverride(
                bodyOverride,
                PortalBodySpriteAssetAddressLow,
                PortalBodySpriteAssetAddressHigh,
                modRoot,
                "portal frame");
            GeneratedPortalSpriteAsset customSwirl = ResolvePortalCustomSwirlSpriteAsset(
                profile,
                modRoot);

            // When the profile's center reference is the framework instant center (the item
            // portal's default three-animation contract), treat it as the framework baseline so
            // palette-only recolors bake from the instant sheets instead of the reference being
            // misread as an external override.
            bool instantCenterReference = profile != null &&
                profile.CenterEffectSpriteAsset.hasAddress &&
                profile.CenterEffectSpriteAsset.address.lowBits ==
                    DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressLow &&
                profile.CenterEffectSpriteAsset.address.highBits ==
                    DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressHigh;
            long centerFallbackAddressLow = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressLow
                : PortalCenterEffectSpriteAssetAddressLow;
            long centerFallbackAddressHigh = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressHigh
                : PortalCenterEffectSpriteAssetAddressHigh;
            string centerFallbackAssetPath = instantCenterReference
                ? DimensionPortalInstantArtworkEditorUtility.InstantCenterAssetPath
                : PortalCenterEffectSpriteAssetPath;

            GeneratedPortalSpriteAsset chargeProgress;
            GeneratedPortalSpriteAsset centerEffect;
            if (profile == null)
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalMilestonesPalette");
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalChargeWavePalette");
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalCenterPalette");
                chargeProgress = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalChargeProgressSpriteAssetAddressLow,
                    PortalChargeProgressSpriteAssetAddressHigh,
                    modRoot,
                    "portal milestones");
                centerEffect = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalCenterEffectSpriteAssetAddressLow,
                    PortalCenterEffectSpriteAssetAddressHigh,
                    modRoot,
                    "activated portal center");
            }
            else
            {
                chargeProgress = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.MilestoneSpriteAsset,
                    PortalChargeProgressSpriteAssetAddressLow,
                    PortalChargeProgressSpriteAssetAddressHigh,
                    PortalChargeProgressSpriteAssetPath,
                    "PortalMilestonesPalette",
                    "milestones",
                    "portal milestones",
                    PortalEffectSourcePalette,
                    new[]
                    {
                        profile.MilestoneDarkColor,
                        profile.MilestoneDeepColor,
                        profile.MilestoneMidColor,
                        profile.MilestoneBrightColor,
                        profile.MilestoneCoreColor
                    });
                centerEffect = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.CenterEffectSpriteAsset,
                    centerFallbackAddressLow,
                    centerFallbackAddressHigh,
                    centerFallbackAssetPath,
                    "PortalCenterPalette",
                    "center",
                    "activated portal center",
                    PortalCenterSourcePalette,
                    new[]
                    {
                        profile.CenterDarkColor,
                        profile.CenterDeepColor,
                        profile.CenterMidColor,
                        profile.CenterBrightColor,
                        profile.CenterCoreColor,
                        profile.CenterHighlightColor
                    });
            }

            bool frameUsesAuthoredNormal = bodySource.staticSpriteData != null &&
                bodySource.staticSpriteData.normalTexture != null;
            bool customFrameArtwork = profile != null &&
                profile.PortalFrameSpriteAsset.hasAddress &&
                !DimensionPortalArtworkEditorUtility.IsFrameworkReference(
                    profile.PortalFrameSpriteAsset,
                    DimensionPortalArtworkLayer.Frame);
            bool customAnimatedCharge = profile != null &&
                ((profile.ChargeWaveSpriteAsset.hasAddress &&
                  !DimensionPortalArtworkEditorUtility.IsFrameworkReference(
                      profile.ChargeWaveSpriteAsset,
                      DimensionPortalArtworkLayer.ChargeSweep)) ||
                 customFrameArtwork ||
                 RequiresIndependentChargeOverlay(profile) ||
                 frameUsesAuthoredNormal);
            DeleteLegacyIntegratedPortalChargingAssets(
                portalOutput,
                portalFolder,
                modRoot);
            GeneratedPortalSpriteAsset chargeLayer;
            Sprite bodyRendererSprite = null;
            Material bodyRendererMaterial = null;
            if (customAnimatedCharge)
            {
                DeletePortalShaderBodyArtifacts(portalOutput, portalFolder);
                GeneratedPortalSpriteAsset chargeWaveMask = ResolvePortalPaletteSpriteAsset(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    profile.ChargeWaveSpriteAsset,
                    PortalEmissiveWaveSpriteAssetAddressLow,
                    PortalEmissiveWaveSpriteAssetAddressHigh,
                    PortalEmissiveWaveSpriteAssetPath,
                    "PortalChargeWavePalette",
                    "charge-wave",
                    "portal charge sweep",
                    PortalEffectSourcePalette,
                    new[]
                    {
                        profile.ChargeWaveDarkColor,
                        profile.ChargeWaveDeepColor,
                        profile.ChargeWaveMidColor,
                        profile.ChargeWaveBrightColor,
                        profile.ChargeWaveCoreColor
                    });
                // Custom charging artwork is a true overlay. Keeping it separate from
                // the frame is what makes independent offsets, scale, rotation and
                // visibility deterministic in both Portal Studio and the built mod.
                chargeLayer = chargeWaveMask;
            }
            else
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    "PortalChargeWavePalette");
                chargeLayer = ResolvePortalSpriteAssetOverride(
                    null,
                    PortalEmissiveWaveSpriteAssetAddressLow,
                    PortalEmissiveWaveSpriteAssetAddressHigh,
                    modRoot,
                    "portal charge sweep");
                Texture2D shaderEmissiveTexture = ResolvePortalShaderEmissiveTexture(
                    portalOutput,
                    portalFolder,
                    bodySource,
                    profile);
                bodyRendererSprite = ResolvePortalBodyRendererSprite(
                    portalOutput,
                    portalFolder,
                    bodyOverride,
                    bodySource);
                bodyRendererMaterial = EnsurePortalBodyRendererMaterial(
                    portalOutput,
                    portalFolder,
                    shaderEmissiveTexture,
                    profile);
            }

            return new PortalVisualSpriteAssets
            {
                Body = body,
                BodySource = bodySource,
                BodyRendererSprite = bodyRendererSprite,
                BodyRendererMaterial = bodyRendererMaterial,
                ChargeProgress = chargeProgress,
                EmissiveWave = chargeLayer,
                CenterEffect = centerEffect,
                CustomSwirl = customSwirl
            };
        }

        private static GeneratedPortalSpriteAsset ResolvePortalCustomSwirlSpriteAsset(
            DimensionPortalVisualProfileAsset profile,
            string modRoot)
        {
            if (profile == null || !profile.CenterSwirlOverrideVanilla)
            {
                return default(GeneratedPortalSpriteAsset);
            }

            if (!profile.CenterSwirlSpriteAsset.hasAddress)
            {
                throw new System.InvalidOperationException(
                    "Custom portal swirl override is enabled, but Artwork override is empty. " +
                    "Assign a looping 48 x 48 SpriteAsset before applying this profile.");
            }

            SpriteAsset configured = ResolvePortalSpriteAssetReference(
                profile.CenterSwirlSpriteAsset,
                "custom portal center swirl");
            if (configured == null)
            {
                throw new System.InvalidOperationException(
                    "Custom portal swirl override is enabled, but its Artwork override " +
                    "address could not be resolved in Scriptable Data.");
            }

            FrameAnimation animation = configured.hasAnimations && configured.animationCount > 0
                ? configured.GetAnimationAt(0)
                : null;
            if (animation == null || animation.spriteData == null || animation.srcFrameCount <= 0)
            {
                throw new System.InvalidOperationException(
                    "The custom portal center swirl SpriteAsset must provide at least one " +
                    "frame in animation 0: " + AssetDatabase.GetAssetPath(configured) + ".");
            }

            if (!animation.loop)
            {
                throw new System.InvalidOperationException(
                    "Animation 0 of the custom portal center swirl SpriteAsset must loop: " +
                    AssetDatabase.GetAssetPath(configured) + ".");
            }

            Texture2D sourceTexture = animation.spriteData.GetSrcTexture();
            int frameCount = animation.srcFrameCount;
            if (sourceTexture == null ||
                frameCount <= 0 ||
                sourceTexture.width % frameCount != 0 ||
                sourceTexture.width / frameCount <= 0 ||
                sourceTexture.width / frameCount >
                    DimensionPortalVisualContract.CanonicalFramePixels ||
                sourceTexture.height > DimensionPortalVisualContract.CanonicalFramePixels)
            {
                throw new System.InvalidOperationException(
                    "Animation 0 of the custom portal center swirl must use horizontal " +
                    "frames that fit within the 48 x 48 portal canvas: " +
                    AssetDatabase.GetAssetPath(configured) + ".");
            }

            return ResolvePortalSpriteAssetOverride(
                configured,
                PortalCustomSwirlSpriteAssetAddressLow,
                PortalCustomSwirlSpriteAssetAddressHigh,
                modRoot,
                "custom portal center swirl");
        }

        private static bool UsesDefaultChargeLayout(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return true;
            }

            return profile.ChargeWaveVisible &&
                profile.ChargeWaveOffsetPixels.sqrMagnitude <= 0.0001f &&
                (profile.ChargeWaveScale - Vector2.one).sqrMagnitude <= 0.0001f &&
                Mathf.Abs(profile.ChargeWaveRotationDegrees) <= 0.0001f &&
                !profile.ChargeWaveFlipX &&
                !profile.ChargeWaveFlipY;
        }

        private static bool RequiresIndependentChargeOverlay(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return false;
            }

            // The vanilla Portal shader draws charging as part of the frame renderer.
            // Any independently-authored pose or hidden frame must therefore use the
            // separate charge SpriteObject that Portal Studio previews.
            return !UsesDefaultChargeLayout(profile) ||
                !profile.FrameVisible ||
                profile.FrameOffsetPixels.sqrMagnitude > 0.0001f ||
                (profile.FrameScale - Vector2.one).sqrMagnitude > 0.0001f ||
                Mathf.Abs(profile.FrameRotationDegrees) > 0.0001f ||
                profile.FrameFlipX ||
                profile.FrameFlipY;
        }

        private static Texture2D ResolvePortalShaderEmissiveTexture(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            SpriteAsset bodySource,
            DimensionPortalVisualProfileAsset profile)
        {
            Texture2D source = bodySource == null || bodySource.staticSpriteData == null
                ? null
                : bodySource.staticSpriteData.emissiveTexture;
            if (source == null)
            {
                SpriteAsset fallback =
                    AssetDatabase.LoadAssetAtPath<SpriteAsset>(PortalBodySpriteAssetPath);
                source = fallback == null || fallback.staticSpriteData == null
                    ? null
                    : fallback.staticSpriteData.emissiveTexture;
            }

            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "The selected portal frame needs an emissive texture for the continuous shader sweep.");
            }

            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargeShaderMask",
                "DimensionPortalChargeShaderMask");
            string generatedPath = portalFolder + "/" + assetName + ".png";
            bool useSource = profile == null || PortalPaletteMatchesSource(
                PortalEffectSourcePalette,
                new[]
                {
                    profile.ChargeWaveDarkColor,
                    profile.ChargeWaveDeepColor,
                    profile.ChargeWaveMidColor,
                    profile.ChargeWaveBrightColor,
                    profile.ChargeWaveCoreColor
                });
            if (useSource)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(generatedPath) != null)
                {
                    AssetDatabase.DeleteAsset(generatedPath);
                }

                return source;
            }

            return EnsurePaletteBakedPortalTexture(
                source,
                generatedPath,
                1,
                "portal shader charge mask",
                PortalEffectSourcePalette,
                new[]
                {
                    profile.ChargeWaveDarkColor,
                    profile.ChargeWaveDeepColor,
                    profile.ChargeWaveMidColor,
                    profile.ChargeWaveBrightColor,
                    profile.ChargeWaveCoreColor
                });
        }

        private static Sprite ResolvePortalBodyRendererSprite(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            SpriteAsset bodyOverride,
            SpriteAsset bodySource)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyRendererSprite",
                "DimensionPortalBodyRendererSprite");
            string outputPath = portalFolder + "/" + assetName + ".asset";
            string bodyOverridePath = NormalizeAssetPath(
                bodyOverride == null ? string.Empty : AssetDatabase.GetAssetPath(bodyOverride));
            if (bodyOverride == null ||
                string.Equals(
                    bodyOverridePath,
                    PortalBodySpriteAssetPath,
                    System.StringComparison.Ordinal))
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(outputPath) != null)
                {
                    AssetDatabase.DeleteAsset(outputPath);
                }

                return RequirePortalSprite(PortalBodySpritePath);
            }

            Texture2D texture = bodySource == null || bodySource.staticSpriteData == null
                ? null
                : bodySource.staticSpriteData.texture;
            if (texture == null)
            {
                throw new System.InvalidOperationException(
                    "The selected portal frame SpriteAsset needs a static source texture.");
            }

            Vector2 pivot = new Vector2(
                bodySource.staticSpriteData.pivot.x,
                bodySource.staticSpriteData.pivot.y);
            Sprite staged = Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, texture.width, texture.height),
                pivot,
                16.0f,
                1,
                SpriteMeshType.FullRect);
            staged.name = assetName;
            Sprite generated = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            if (generated == null)
            {
                AssetDatabase.CreateAsset(staged, outputPath);
                return AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            }

            try
            {
                EditorUtility.CopySerialized(staged, generated);
                generated.name = assetName;
                EditorUtility.SetDirty(generated);
                AssetDatabase.SaveAssetIfDirty(generated);
                return generated;
            }
            finally
            {
                Object.DestroyImmediate(staged);
            }
        }

        private static void DeletePortalShaderBodyArtifacts(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder)
        {
            string spriteName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyRendererSprite",
                "DimensionPortalBodyRendererSprite");
            string materialName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyShader",
                "DimensionPortalBodyShader");
            string maskName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalChargeShaderMask",
                "DimensionPortalChargeShaderMask");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + spriteName + ".asset");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + materialName + ".mat");
            DeleteGeneratedPortalAssetIfPresent(portalFolder + "/" + maskName + ".png");
        }

        private static void DeleteGeneratedPortalAssetIfPresent(string assetPath)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private static Material EnsurePortalBodyRendererMaterial(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            Texture2D emissiveTexture,
            DimensionPortalVisualProfileAsset profile)
        {
            Material source = RequirePortalMaterial(PortalBodyMaterialPath);
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalBodyShader",
                "DimensionPortalBodyShader");
            string outputPath = portalFolder + "/" + assetName + ".mat";
            Material generated = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (generated == null)
            {
                generated = new Material(source);
                generated.name = assetName;
                AssetDatabase.CreateAsset(generated, outputPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, generated);
                generated.name = assetName;
            }

            generated.SetTexture("_EmissiveTex", emissiveTexture);
            generated.SetFloat("_loadingMul", 1.0f);
            generated.SetFloat("_emissiveStrengthMul", 0.0f);
            generated.SetFloat("_activeStrength", 5.0f);
            generated.SetFloat("_activeHeight", 0.9f);
            generated.SetFloat(
                "_loadingSpeed",
                profile == null ? 1.0f : profile.ChargeWaveSpeed);
            generated.SetColor(
                "_emissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.ChargeWaveEmissiveColor);
            EditorUtility.SetDirty(generated);
            return generated;
        }
    }
}
