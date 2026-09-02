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
    /// Reading and writing one serialized field, and putting a layer back as it was.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        /// <summary>The studio's own view of the portal, so page and canvas never disagree.</summary>
        internal SerializedObject GetProfileSerializedObject(
            DimensionPortalVisualProfileAsset profile)
        {
            return profile == null ? null : GetSerializedProfile(profile);
        }

        /// <summary>The studio's own view of the dimension the portal belongs to.</summary>
        internal SerializedObject GetTemplateSerializedObject(
            DimensionTemplateAsset template)
        {
            return template == null ? null : GetSerializedTemplate(template);
        }

        private SerializedObject GetSerializedTemplate(
            DimensionTemplateAsset template)
        {
            if (serializedTemplateCache == null || serializedTemplateTarget != template)
            {
                serializedTemplateTarget = template;
                serializedTemplateCache = new SerializedObject(template);
            }

            return serializedTemplateCache;
        }

        private SerializedObject GetSerializedProfile(
            DimensionPortalVisualProfileAsset profile)
        {
            if (serializedProfileCache == null || serializedProfileTarget != profile)
            {
                serializedProfileTarget = profile;
                serializedProfileCache = new SerializedObject(profile);
                hasSelectedPixel = false;
                hasHexEditKey = false;
                hexEditWasFocused = false;
                ClearPaletteFocus();
                previewCompositionDirty = true;
                hasPreviewComposition = false;
                previewCompositionProfile = null;
                previewCompositionProfileDirtyCount = -1;
            }

            return serializedProfileCache;
        }

        private static Color GetColor(
            SerializedObject profile,
            string propertyName,
            Color fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.colorValue;
        }

        private static float GetFloat(
            SerializedObject profile,
            string propertyName,
            float fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.floatValue;
        }

        private static Vector2 GetVector2(
            SerializedObject profile,
            string propertyName,
            Vector2 fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.vector2Value;
        }

        private static bool GetBool(
            SerializedObject profile,
            string propertyName,
            bool fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.boolValue;
        }

        private static int GetInt(
            SerializedObject profile,
            string propertyName,
            int fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.intValue;
        }

        private static Color ToneMapForEditor(Color color, bool hdr)
        {
            if (!hdr)
            {
                return new Color(
                    Mathf.Clamp01(color.r),
                    Mathf.Clamp01(color.g),
                    Mathf.Clamp01(color.b),
                    1f);
            }

            return new Color(
                color.r / (1f + Mathf.Max(0f, color.r)),
                color.g / (1f + Mathf.Max(0f, color.g)),
                color.b / (1f + Mathf.Max(0f, color.b)),
                1f);
        }

        private static string GetOverridePropertyName(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return "portalFrameSpriteAsset";
                case StudioLayer.ChargeSweep:
                    return "chargeWaveSpriteAsset";
                case StudioLayer.Milestones:
                    return "milestoneSpriteAsset";
                case StudioLayer.Center:
                    return "centerEffectSpriteAsset";
                default:
                    return string.Empty;
            }
        }

        private static bool TryGetArtworkLayer(
            StudioLayer layer,
            out DimensionPortalArtworkLayer artworkLayer,
            bool instantPortal = false)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    artworkLayer = DimensionPortalArtworkLayer.Frame;
                    return true;
                case StudioLayer.ChargeSweep:
                    artworkLayer = DimensionPortalArtworkLayer.ChargeSweep;
                    return true;
                case StudioLayer.Milestones:
                    artworkLayer = DimensionPortalArtworkLayer.Milestones;
                    return true;
                case StudioLayer.Center:
                    artworkLayer = instantPortal
                        ? DimensionPortalArtworkLayer.CenterInstant
                        : DimensionPortalArtworkLayer.Center;
                    return true;
                default:
                    artworkLayer = default(DimensionPortalArtworkLayer);
                    return false;
            }
        }

        private static ColorRole[] GetColorRoles(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return FrameColorRoles;
                case StudioLayer.ChargeSweep:
                    return ChargeColorRoles;
                case StudioLayer.Milestones:
                    return MilestoneColorRoles;
                case StudioLayer.Center:
                    return CenterColorRoles;
                case StudioLayer.InnerFlecks:
                    return FlecksColorRoles;
                case StudioLayer.ReadyBurst:
                    return ReadyBurstColorRoles;
                case StudioLayer.GroundLight:
                    return LightColorRoles;
                default:
                    return new ColorRole[0];
            }
        }

        /// <summary>The name one part of the portal goes by, in the studio and on the page.</summary>
        internal static string GetLayerTitle(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return "Portal frame";
                case StudioLayer.ChargeSweep:
                    return "Continuous charging sweep";
                case StudioLayer.Milestones:
                    return "Persistent milestone blobs";
                case StudioLayer.Center:
                    return "Activated center ring";
                case StudioLayer.InnerFlecks:
                    return "Inner swirls";
                case StudioLayer.ReadyBurst:
                    return "Ready activation burst";
                case StudioLayer.GroundLight:
                    return "Projected portal light";
                default:
                    return "Portal layer";
            }
        }

        internal static bool RestoreLayerToVanilla(
            SerializedObject profile,
            StudioLayer layer,
            out string message,
            bool instantPortal = false)
        {
            message = string.Empty;
            if (profile == null ||
                !(profile.targetObject is DimensionPortalVisualProfileAsset))
            {
                message = "The active portal profile is required.";
                return false;
            }

            RestoreLayerDefaults(profile, layer);
            if (TryGetArtworkLayer(
                    layer,
                    out DimensionPortalArtworkLayer artworkLayer,
                    instantPortal))
            {
                if (!DimensionPortalArtworkEditorUtility.AssignFrameworkReference(
                        profile,
                        artworkLayer))
                {
                    message = "The framework " + GetLayerTitle(layer) +
                              " SpriteAsset could not be restored.";
                    return false;
                }
            }
            else if (layer == StudioLayer.InnerFlecks &&
                     !DimensionPortalSwirlArtworkEditorUtility.AssignFrameworkReference(
                         profile,
                         out message))
            {
                return false;
            }

            return true;
        }

        private static void RestoreLayerDefaults(
            SerializedObject profile,
            StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    SetColor(profile, "frameTint", Color.white);
                    SetColor(profile, "frameEmissiveColor", DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor);
                    SetBool(profile, "frameVisible", true);
                    SetVector2(profile, "frameOffsetPixels", Vector2.zero);
                    SetVector2(profile, "frameScale", Vector2.one);
                    SetFloat(profile, "frameRotationDegrees", 0f);
                    SetBool(profile, "frameFlipX", false);
                    SetBool(profile, "frameFlipY", false);
                    break;
                case StudioLayer.ChargeSweep:
                    SetEffectPalette(profile, "chargeWave");
                    SetColor(profile, "chargeWaveTint", Color.white);
                    SetColor(profile, "chargeWaveEmissiveColor", DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor);
                    SetFloat(profile, "chargeWaveSpeed", 1f);
                    SetBool(profile, "chargeWaveVisible", true);
                    SetVector2(profile, "chargeWaveOffsetPixels", Vector2.zero);
                    SetVector2(profile, "chargeWaveScale", Vector2.one);
                    SetFloat(profile, "chargeWaveRotationDegrees", 0f);
                    SetBool(profile, "chargeWaveFlipX", false);
                    SetBool(profile, "chargeWaveFlipY", false);
                    break;
                case StudioLayer.Milestones:
                    SetEffectPalette(profile, "milestone");
                    SetColor(profile, "milestoneTint", Color.white);
                    SetColor(profile, "milestoneEmissiveColor", DimensionPortalVisualProfileAsset.VanillaLoadPointEmissiveColor);
                    SetFloat(profile, "firstMilestone", 0.25f);
                    SetFloat(profile, "secondMilestone", 0.5f);
                    SetFloat(profile, "thirdMilestone", 0.75f);
                    SetBool(profile, "milestonesVisible", true);
                    SetVector2(profile, "milestoneOffsetPixels", Vector2.zero);
                    SetVector2(profile, "milestoneScale", Vector2.one);
                    SetFloat(profile, "milestoneRotationDegrees", 0f);
                    SetBool(profile, "milestoneFlipX", false);
                    SetBool(profile, "milestoneFlipY", false);
                    break;
                case StudioLayer.Center:
                    SetColor(profile, "centerDarkColor", DimensionPortalVisualProfileAsset.VanillaPaletteDark);
                    SetColor(profile, "centerDeepColor", DimensionPortalVisualProfileAsset.VanillaCenterPaletteDeep);
                    SetColor(profile, "centerMidColor", DimensionPortalVisualProfileAsset.VanillaPaletteDeep);
                    SetColor(profile, "centerBrightColor", DimensionPortalVisualProfileAsset.VanillaPaletteMid);
                    SetColor(profile, "centerCoreColor", DimensionPortalVisualProfileAsset.VanillaPaletteCore);
                    SetColor(profile, "centerHighlightColor", Color.white);
                    SetColor(profile, "centerTint", DimensionPortalVisualProfileAsset.VanillaCenterTint);
                    SetColor(profile, "centerEmissiveColor", DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor);
                    SetBool(profile, "centerVisible", true);
                    SetVector2(profile, "centerOffsetPixels", Vector2.zero);
                    SetVector2(profile, "centerScale", Vector2.one);
                    SetFloat(profile, "centerRotationDegrees", 0f);
                    SetBool(profile, "centerFlipX", false);
                    SetBool(profile, "centerFlipY", false);
                    SetFloat(profile, "centerGlowIntensity", DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity);
                    break;
                case StudioLayer.InnerFlecks:
                    SetBool(profile, "centerParticlesEnabled", true);
                    SetBool(profile, "centerSwirlOverrideVanilla", false);
                    SetBool(profile, "centerSwirlVisible", true);
                    SetBool(profile, "centerParticlesFollowCenterPalette", false);
                    SetColor(
                        profile,
                        "centerParticleTint",
                        DimensionPortalVisualProfileAsset.VanillaSwirlTint);
                    SetObjectReference(profile, "centerParticleSprite", null);
                    SetObjectReference(profile, "centerParticleTexture", null);
                    SetFloat(profile, "centerParticleEmissionMultiplier", 1f);
                    SetFloat(profile, "centerParticleSizeMultiplier", 1f);
                    SetFloat(profile, "centerParticleLifetimeMultiplier", 1f);
                    SetFloat(profile, "centerParticleOrbitSpeedMultiplier", 1f);
                    SetFloat(profile, "centerParticleRadialSpeedMultiplier", 1f);
                    SetFloat(profile, "centerParticleRadiusMultiplier", 1f);
                    SetBool(profile, "centerParticleTrailsEnabled", true);
                    SetFloat(profile, "centerParticleTrailLifetimeMultiplier", 1f);
                    SetVector2(profile, "centerParticleOffsetPixels", Vector2.zero);
                    SetVector2(profile, "centerParticleScale", Vector2.one);
                    SetFloat(profile, "centerParticleRotationDegrees", 0f);
                    SetBool(profile, "centerSwirlFlipX", false);
                    SetBool(profile, "centerSwirlFlipY", false);
                    SetFloat(profile, "centerSwirlPlaybackSpeed", 1f);
                    SetColor(profile, "centerSwirlEmissiveColor", Color.white);
                    break;
                case StudioLayer.ReadyBurst:
                    SetBool(profile, "playReadyFlash", true);
                    SetBool(profile, "readyFlashFollowsCenterPalette", true);
                    SetColor(profile, "readyFlashTint", Color.white);
                    SetArraySize(profile, "readyFlashSprites", 0);
                    SetObjectReference(profile, "readyFlashTexture", null);
                    SetFloat(profile, "readyFlashEmissionMultiplier", 1f);
                    SetFloat(profile, "readyFlashSizeMultiplier", 1f);
                    SetVector2(profile, "readyFlashOffsetPixels", Vector2.zero);
                    SetVector2(profile, "readyFlashScale", Vector2.one);
                    SetFloat(profile, "readyFlashRotationDegrees", 0f);
                    break;
                case StudioLayer.GroundLight:
                    SetBool(profile, "groundLightEnabled", true);
                    SetColor(profile, "groundLightColor", DimensionPortalVisualProfileAsset.VanillaGroundLightColor);
                    SetFloat(profile, "groundLightRange", 5f);
                    SetFloat(profile, "groundLightMinimumIntensity", 0.3f);
                    SetFloat(profile, "groundLightMaximumIntensity", 0.3f);
                    SetBool(profile, "groundLightMovement", true);
                    SetBool(profile, "groundLightCastsShadows", true);
                    SetVector2(profile, "groundLightOffsetPixels", Vector2.zero);
                    SetBool(profile, "portalShadowEnabled", true);
                    SetObjectReference(profile, "portalShadowSprite", null);
                    SetObjectReference(profile, "portalShadowCasterSprite", null);
                    SetVector2(profile, "portalShadowOffsetPixels", Vector2.zero);
                    SetVector2(profile, "portalShadowScale", Vector2.one);
                    SetFloat(profile, "portalShadowRotationDegrees", 0f);
                    SetBool(profile, "portalShadowFlipX", false);
                    SetBool(profile, "portalShadowFlipY", false);
                    break;
            }
        }

        private static void SetEffectPalette(SerializedObject profile, string prefix)
        {
            SetColor(profile, prefix + "DarkColor", DimensionPortalVisualProfileAsset.VanillaPaletteDark);
            SetColor(profile, prefix + "DeepColor", DimensionPortalVisualProfileAsset.VanillaPaletteDeep);
            SetColor(profile, prefix + "MidColor", DimensionPortalVisualProfileAsset.VanillaPaletteMid);
            SetColor(profile, prefix + "BrightColor", DimensionPortalVisualProfileAsset.VanillaPaletteBright);
            SetColor(profile, prefix + "CoreColor", DimensionPortalVisualProfileAsset.VanillaPaletteCore);
        }

        private static void SetColor(SerializedObject profile, string propertyName, Color value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetFloat(SerializedObject profile, string propertyName, float value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetVector2(SerializedObject profile, string propertyName, Vector2 value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        private static void SetInt(SerializedObject profile, string propertyName, int value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBool(SerializedObject profile, string propertyName, bool value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetObjectReference(
            SerializedObject profile,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetArraySize(
            SerializedObject profile,
            string propertyName,
            int size)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                property.arraySize = Mathf.Max(0, size);
            }
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
