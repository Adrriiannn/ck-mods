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
    /// The child objects a portal visual is built from, and the profile they are set up from.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static void ConfigurePortalVisualProfile(
            DimensionPortalVisual visual,
            DimensionPortalVisualProfileAsset profile)
        {
            if (visual == null)
            {
                return;
            }

            SerializedObject serializedVisual = new SerializedObject(visual);
            serializedVisual.Update();
            SetSerializedBool(
                serializedVisual,
                "portalBodyVisible",
                profile == null || profile.FrameVisible);
            SetSerializedBool(
                serializedVisual,
                "chargeWaveVisible",
                profile == null || profile.ChargeWaveVisible);
            SetSerializedBool(
                serializedVisual,
                "milestoneVisible",
                profile == null || profile.MilestonesVisible);
            SetSerializedBool(
                serializedVisual,
                "centerVisible",
                profile == null || profile.CenterVisible);
            SetSerializedBool(
                serializedVisual,
                "centerParticlesVisible",
                profile == null || profile.CenterSwirlVisible);
            SetSerializedFloat(
                serializedVisual,
                "customSwirlPlaybackSpeed",
                profile == null ? 1.0f : profile.CenterSwirlPlaybackSpeed);
            SetSerializedBool(
                serializedVisual,
                "projectedShadowVisible",
                profile == null || profile.PortalShadowEnabled);
            SetSerializedColor(
                serializedVisual,
                "portalBodyColor",
                profile == null ? Color.white : profile.FrameTint);
            SetSerializedColor(
                serializedVisual,
                "portalBodyEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.FrameEmissiveColor);
            SetSerializedColor(
                serializedVisual,
                "chargeWaveColor",
                profile == null ? Color.white : profile.ChargeWaveTint);
            SetSerializedColor(
                serializedVisual,
                "chargeWaveEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                    : profile.ChargeWaveEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "chargeWaveSpeed",
                profile == null ? 1.0f : profile.ChargeWaveSpeed);
            SetSerializedColor(
                serializedVisual,
                "milestoneColor",
                profile == null ? Color.white : profile.MilestoneTint);
            SetSerializedColor(
                serializedVisual,
                "milestoneEmissiveColor",
                profile == null
                    ? PortalLoadPointEmissiveColor
                    : profile.MilestoneEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "firstMilestone",
                profile == null ? 0.25f : profile.FirstMilestone);
            SetSerializedFloat(
                serializedVisual,
                "secondMilestone",
                profile == null ? 0.5f : profile.SecondMilestone);
            SetSerializedFloat(
                serializedVisual,
                "thirdMilestone",
                profile == null ? 0.75f : profile.ThirdMilestone);
            // Milestone stage->frame mapping is fixed in DimensionPortalVisual (vanilla sheet
            // order); nothing to bake for it.
            SetSerializedColor(
                serializedVisual,
                "centerColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterTint
                    : profile.CenterTint);
            SetSerializedColor(
                serializedVisual,
                "centerEmissiveColor",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor
                    : profile.CenterEmissiveColor);
            SetSerializedFloat(
                serializedVisual,
                "centerGlowIntensity",
                profile == null
                    ? DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity
                    : profile.CenterGlowIntensity);
            SetSerializedInt(
                serializedVisual,
                "centerIdleAnimationIndex",
                profile == null ? 0 : profile.CenterIdleAnimationIndex);
            SetSerializedInt(
                serializedVisual,
                "centerOpeningAnimationIndex",
                profile == null ? 1 : profile.CenterOpeningAnimationIndex);
            SetSerializedBool(
                serializedVisual,
                "playReadyFlash",
                profile == null || profile.PlayReadyFlash);
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform EnsurePortalXScaler(GameObject root)
        {
            Transform xScaler = FindDescendantTransform(root.transform, "XScaler");
            if (xScaler == null)
            {
                xScaler = new GameObject("XScaler").transform;
                xScaler.SetParent(root.transform, false);
            }

            xScaler.localPosition = Vector3.zero;
            xScaler.localRotation = Quaternion.identity;
            xScaler.localScale = Vector3.one;
            xScaler.gameObject.layer = root.layer;
            return xScaler;
        }

        private static PortalSpriteObjectSet EnsurePortalSpriteObjectHierarchy(
            GameObject root,
            GeneratedPortalOutlineSpriteAssets outlineMaskAssets,
            PortalVisualSpriteAssets visualAssets,
            DimensionPortalVisualProfileAsset visualProfile,
            bool itemPortal)
        {
            Transform xScaler = EnsurePortalXScaler(root);
            Transform animPositionRotation = EnsureChild(xScaler, "AnimPositionRotation");
            animPositionRotation.localPosition = Vector3.zero;
            animPositionRotation.localRotation = Quaternion.identity;
            animPositionRotation.localScale = Vector3.one;
            animPositionRotation.gameObject.layer = root.layer;

            Transform animScale = EnsureChild(animPositionRotation, "AnimScale");
            animScale.localPosition = Vector3.zero;
            animScale.localRotation = Quaternion.identity;
            animScale.localScale = Vector3.one;
            animScale.gameObject.layer = root.layer;

            Transform spritePivot = EnsureChild(animScale, "SRPivot");
            spritePivot.localPosition = itemPortal
                ? ItemPortalSpritePivotPosition
                : PortalSpritePivotPosition;
            spritePivot.localRotation = Quaternion.identity;
            spritePivot.localScale = Vector3.one;
            spritePivot.gameObject.layer = root.layer;

            Transform spriteRoot = EnsureChild(spritePivot, "PortalSpriteObjects");
            spriteRoot.localPosition = Vector3.zero;
            spriteRoot.localRotation = Quaternion.identity;
            spriteRoot.localScale = Vector3.one;
            spriteRoot.gameObject.layer = root.layer;
            Transform shadowRoot = EnsureChild(spriteRoot, "PortalShadowGroup");
            Vector2 shadowOffset = visualProfile == null
                ? Vector2.zero
                : visualProfile.PortalShadowOffsetPixels;
            shadowRoot.localPosition = new Vector3(
                -0.5f + shadowOffset.x / DimensionPortalVisualContract.PixelsPerUnit,
                0.0f,
                0.8125f + shadowOffset.y / DimensionPortalVisualContract.PixelsPerUnit);
            shadowRoot.localRotation = Quaternion.identity;
            shadowRoot.localScale = Vector3.one;
            shadowRoot.gameObject.layer = root.layer;

            Material litMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(UgcSpriteObjectLitMaterialPath);
            Material unlitMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(UgcSpriteObjectUnlitMaterialPath);
            Material floorShadowMaterial =
                RequirePortalMaterial(PortalFloorShadowMaterialPath);
            Material shadowCasterMaterial =
                RequirePortalMaterial(PortalShadowCasterMaterialPath);
            Material outlineMaterial = unlitMaterial != null ? unlitMaterial : litMaterial;
            bool customShadowSprite =
                visualProfile != null && visualProfile.PortalShadowSprite != null;
            bool customShadowCasterSprite =
                visualProfile != null && visualProfile.PortalShadowCasterSprite != null;
            Sprite shadowSprite = customShadowSprite
                ? visualProfile.PortalShadowSprite
                : RequirePortalSprite(PortalShadowSpritePath);
            Sprite shadowCasterSprite = customShadowCasterSprite
                ? visualProfile.PortalShadowCasterSprite
                : RequirePortalSprite(PortalShadowCasterSpritePath);

            Vector3 bodyPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Frame,
                visualProfile == null ? Vector2.zero : visualProfile.FrameOffsetPixels);
            Quaternion bodyRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.FrameRotationDegrees);
            Vector3 bodyScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.FrameScale,
                visualProfile != null && visualProfile.FrameFlipX,
                visualProfile != null && visualProfile.FrameFlipY);
            Vector3 chargePosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Milestones,
                visualProfile == null ? Vector2.zero : visualProfile.MilestoneOffsetPixels);
            Quaternion chargeRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.MilestoneRotationDegrees);
            Vector3 chargeScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.MilestoneScale,
                visualProfile != null && visualProfile.MilestoneFlipX,
                visualProfile != null && visualProfile.MilestoneFlipY);
            Vector3 wavePosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.ChargeSweep,
                visualProfile == null ? Vector2.zero : visualProfile.ChargeWaveOffsetPixels);
            Quaternion waveRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.ChargeWaveRotationDegrees);
            Vector3 waveScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.ChargeWaveScale,
                visualProfile != null && visualProfile.ChargeWaveFlipX,
                visualProfile != null && visualProfile.ChargeWaveFlipY);
            Vector3 centerPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Center,
                visualProfile == null ? Vector2.zero : visualProfile.CenterOffsetPixels);
            Quaternion centerRotation = DimensionPortalVisualContract.GetLocalRotation(
                visualProfile == null ? 0.0f : visualProfile.CenterRotationDegrees);
            Vector3 centerScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null ? Vector2.one : visualProfile.CenterScale,
                visualProfile != null && visualProfile.CenterFlipX,
                visualProfile != null && visualProfile.CenterFlipY);
            bool customSwirlEnabled = visualProfile != null &&
                visualProfile.CenterSwirlOverrideVanilla &&
                visualProfile.CenterSwirlVisible;
            // Anchor the swirl to the activated center/aperture (not the outer frame) so a
            // full-canvas sheet whose flecks are drawn about its own centre lands exactly in
            // the inner circle with no manual offset. CenterParticleOffsetPixels then nudges
            // from that natural anchor.
            Vector3 customSwirlPosition = DimensionPortalVisualContract.GetLocalPosition(
                DimensionPortalVisualContract.Layer.Center,
                visualProfile == null
                    ? Vector2.zero
                    : visualProfile.CenterParticleOffsetPixels);
            // Place the artwork just behind the activated ring at the same projected point.
            const float customSwirlDepthInset = 0.0002f;
            float customSwirlTargetDepth =
                DimensionPortalVisualContract.CenterLocalPosition.z +
                customSwirlDepthInset;
            float customSwirlDepthDelta =
                customSwirlTargetDepth - customSwirlPosition.z;
            customSwirlPosition.y -= customSwirlDepthDelta;
            customSwirlPosition.z += customSwirlDepthDelta;
            Quaternion customSwirlRotation =
                DimensionPortalVisualContract.GetLocalRotation(
                    visualProfile == null
                        ? 0.0f
                        : visualProfile.CenterParticleRotationDegrees);
            Vector3 customSwirlScale = DimensionPortalVisualContract.GetLocalScale(
                visualProfile == null
                    ? Vector2.one
                    : visualProfile.CenterParticleScale,
                visualProfile != null && visualProfile.CenterSwirlFlipX,
                visualProfile != null && visualProfile.CenterSwirlFlipY);
            // The swirl color is baked into the profile-owned sheet, so the SpriteObject is
            // drawn with a neutral tint. Only the emissive glow hue and intensity remain
            // runtime material properties (the game shader adds emissiveColor * pixels).
            Color customSwirlTint = Color.white;
            Color customSwirlEmission = visualProfile == null
                ? Color.white
                : ScalePortalColor(
                    visualProfile.CenterSwirlEmissiveColor,
                    visualProfile.CenterParticleEmissionMultiplier);
            Transform staleCustomSwirl = spriteRoot.Find("PortalCustomSwirlSO");
            if (!customSwirlEnabled && staleCustomSwirl != null)
            {
                Object.DestroyImmediate(staleCustomSwirl.gameObject, true);
            }
            Vector3 outlinePosition = bodyPosition +
                (DimensionPortalVisualContract.OutlineLocalPosition -
                 DimensionPortalVisualContract.FrameLocalPosition);
            Vector3 shadowPosition = new Vector3(0.5f, 0.0625f, -0.5f);
            Vector3 shadowCasterPosition = new Vector3(0.5f, 2.099f, -0.5f);
            Vector2 shadowScale2D = visualProfile == null
                ? Vector2.one
                : visualProfile.PortalShadowScale;
            Vector3 shadowScale = new Vector3(
                visualProfile != null && visualProfile.PortalShadowFlipX
                    ? -shadowScale2D.x
                    : shadowScale2D.x,
                visualProfile != null && visualProfile.PortalShadowFlipY
                    ? -shadowScale2D.y
                    : shadowScale2D.y,
                1.0f);
            Quaternion shadowRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f) *
                DimensionPortalVisualContract.GetLocalRotation(
                    visualProfile == null
                        ? 0.0f
                        : visualProfile.PortalShadowRotationDegrees);
            bool shadowVisible = visualProfile == null || visualProfile.PortalShadowEnabled;

            PortalSpriteObjectSet result = new PortalSpriteObjectSet
            {
                Body = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalBodySO",
                    visualAssets.Body.AddressLow,
                    visualAssets.Body.AddressHigh,
                    litMaterial,
                    bodyPosition,
                    bodyRotation,
                    bodyScale,
                    visualProfile == null ? Color.white : visualProfile.FrameTint,
                    ScalePortalColor(
                        MultiplyPortalColors(
                            visualProfile == null
                                ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                                : visualProfile.FrameEmissiveColor,
                            visualProfile == null ? Color.white : visualProfile.FrameTint),
                        visualAssets.BodyRendererSprite == null ? 0.0f : 5.0f),
                    (visualProfile == null || visualProfile.FrameVisible) &&
                    visualAssets.BodyRendererSprite == null),
                BodyRenderer = visualAssets.BodyRendererSprite == null ||
                    visualAssets.BodyRendererMaterial == null
                    ? null
                    : EnsurePortalBodyRenderer(
                        spriteRoot,
                        "PortalBodyRenderer",
                        visualAssets.BodyRendererSprite,
                        visualAssets.BodyRendererMaterial,
                        bodyPosition,
                        bodyRotation,
                        bodyScale,
                        visualProfile == null || visualProfile.FrameVisible),
                ChargeProgress = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalChargeProgressSO",
                    visualAssets.ChargeProgress.AddressLow,
                    visualAssets.ChargeProgress.AddressHigh,
                    litMaterial,
                    chargePosition,
                    chargeRotation,
                    chargeScale,
                    visualProfile == null ? Color.white : visualProfile.MilestoneTint,
                    visualProfile == null
                        ? PortalLoadPointEmissiveColor
                        : visualProfile.MilestoneEmissiveColor,
                    false),
                EmissiveWave = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalEmissiveWaveSO",
                    visualAssets.EmissiveWave.AddressLow,
                    visualAssets.EmissiveWave.AddressHigh,
                    litMaterial,
                    wavePosition,
                    waveRotation,
                    waveScale,
                    visualProfile == null ? Color.white : visualProfile.ChargeWaveTint,
                    ScalePortalColor(
                        MultiplyPortalColors(
                            visualProfile == null
                                ? DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor
                                : visualProfile.ChargeWaveEmissiveColor,
                            visualProfile == null ? Color.white : visualProfile.ChargeWaveTint),
                        3.5f),
                    false),
                CenterEffect = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalCenterEffectSO",
                    visualAssets.CenterEffect.AddressLow,
                    visualAssets.CenterEffect.AddressHigh,
                    litMaterial,
                    centerPosition,
                    centerRotation,
                    centerScale,
                    visualProfile == null
                        ? DimensionPortalVisualProfileAsset.VanillaCenterTint
                        : visualProfile.CenterTint,
                    ScalePortalColor(
                        visualProfile == null
                            ? MultiplyPortalColors(
                                DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor,
                                DimensionPortalVisualProfileAsset.VanillaCenterTint)
                            : MultiplyPortalColors(
                                visualProfile.CenterEmissiveColor,
                                visualProfile.CenterTint),
                        visualProfile == null
                            ? DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity
                            : visualProfile.CenterGlowIntensity),
                    false),
                CustomSwirl = customSwirlEnabled
                    ? EnsurePortalSpriteObject(
                        spriteRoot,
                        "PortalCustomSwirlSO",
                        visualAssets.CustomSwirl.AddressLow,
                        visualAssets.CustomSwirl.AddressHigh,
                        litMaterial,
                        customSwirlPosition,
                        customSwirlRotation,
                        customSwirlScale,
                        customSwirlTint,
                        customSwirlEmission,
                        false)
                    : null,
                OutlineMask = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineMaskSO",
                    outlineMaskAssets.Main.AddressLow,
                    outlineMaskAssets.Main.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    true),
                OutlineSupportMask = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineSupportMaskSO",
                    outlineMaskAssets.Support.AddressLow,
                    outlineMaskAssets.Support.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    true),
                OutlineCap = EnsurePortalSpriteObject(
                    spriteRoot,
                    "PortalOutlineCapSO",
                    outlineMaskAssets.Cap.AddressLow,
                    outlineMaskAssets.Cap.AddressHigh,
                    outlineMaterial,
                    outlinePosition,
                    bodyRotation,
                    bodyScale,
                    Color.clear,
                    Color.clear,
                    false),
                ShadowRoot = shadowRoot.gameObject,
                Shadow = EnsurePortalShadowRenderer(
                    shadowRoot,
                    "Shadow",
                    shadowSprite,
                    floorShadowMaterial,
                    shadowPosition,
                    shadowRotation,
                    shadowScale,
                    shadowVisible,
                    !customShadowSprite,
                    22,
                    -10),
                ShadowCaster = EnsurePortalShadowRenderer(
                    shadowRoot,
                    "ShadowCaster",
                    shadowCasterSprite,
                    shadowCasterMaterial,
                    shadowCasterPosition,
                    shadowRotation,
                    shadowScale,
                    shadowVisible,
                    !customShadowCasterSprite,
                    19,
                    0)
            };

            TrySetUnityTag(result.Shadow.gameObject, "ExcludeFromSpriteAutoSort");
            TrySetUnityTag(result.ShadowCaster.gameObject, "ExcludeFromSpriteAutoSort");
            PortalParticleRootSet particleRoots = EnsurePortalCenterParticleEffects(
                spriteRoot,
                visualProfile);
            result.CenterParticlesRoot = particleRoots.Persistent;
            result.ReadyFlashRoot = particleRoots.ReadyFlash;
            return result;
        }
    }
}
