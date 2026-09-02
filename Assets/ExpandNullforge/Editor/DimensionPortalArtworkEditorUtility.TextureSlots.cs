using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which textures a layer has, and whether the ones chosen fit what the layer needs.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        /// <summary>
        /// Reads the selected layer's texture selectors together with the immutable native
        /// framework dimensions. This performs bounded object inspection only; callers should
        /// cache the result for their IMGUI frame and refresh it after a selector changes.
        /// </summary>
        public static bool TryGetTextureSlots(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            out TextureSlot[] slots,
            out string message)
        {
            slots = Array.Empty<TextureSlot>();
            message = string.Empty;
            SpriteAsset framework = GetFrameworkAsset(layer);
            if (framework == null)
            {
                message = "Could not load the framework " +
                          GetDescriptor(layer).DisplayName + " SpriteAsset.";
                return false;
            }

            SpriteAsset selected = framework;
            if (profile != null)
            {
                LayerDescriptor descriptor = GetDescriptor(layer);
                SerializedObject serializedProfile = new SerializedObject(profile);
                serializedProfile.Update();
                SerializedProperty reference = serializedProfile.FindProperty(
                    descriptor.ReferenceProperty);
                DimensionPortalArtworkReferenceKind kind = ClassifyReference(
                    reference,
                    profile,
                    layer,
                    out SpriteAsset resolved);
                if (resolved != null && kind != DimensionPortalArtworkReferenceKind.Unresolved)
                {
                    selected = resolved;
                }
            }

            return TryBuildTextureSlots(framework, selected, layer, out slots, out message);
        }

        /// <summary>
        /// Replaces every native texture slot for a portal layer without ever mutating the
        /// framework or an externally-selected SpriteAsset. The first edit creates a stable,
        /// consumer-owned SpriteAsset and later edits update that same managed copy.
        ///
        /// Arrays must contain exactly one entry for Frame, ChargeSweep and Milestones, and
        /// exactly two entries for Center (mature loop, then opening). A null color entry
        /// restores that slot's framework color sheet. Emissive sheets are optional as an
        /// array; when supplied its length must match and a null entry restores the framework
        /// emissive contract. Normal sheets follow the same optional-array/null-restores-
        /// framework rules. Every non-null input must be a saved PNG inside the consumer mod
        /// (or the framework), at an allowed sheet size. Center accepts its native 16 x 25
        /// frames or full-canvas 48 x 48 frames; all channels in a slot must match. Selected
        /// PNG bytes are copied beside the managed SpriteAsset and imported point-filtered/
        /// uncompressed; normal copies use linear normal-map import semantics.
        /// </summary>
        public static bool CreateOrUpdateLayerTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            out string message)
        {
            Texture2D[] currentNormals = null;
            if (TryGetTextureSlots(
                    profile,
                    layer,
                    out TextureSlot[] currentSlots,
                    out _))
            {
                currentNormals = new Texture2D[currentSlots.Length];
                for (int i = 0; i < currentSlots.Length; i++)
                {
                    currentNormals[i] = currentSlots[i].NormalTexture;
                }
            }

            return CreateOrUpdateLayerTextures(
                template,
                profile,
                layer,
                textures,
                emissiveTextures,
                currentNormals,
                out message);
        }

        public static bool CreateOrUpdateLayerTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            Texture2D[] normalTextures,
            out string message)
        {
            message = string.Empty;
            SpriteAsset framework = GetFrameworkAsset(layer);
            if (framework == null ||
                !TryBuildTextureSlots(
                    framework,
                    framework,
                    layer,
                    out TextureSlot[] contract,
                    out message))
            {
                return false;
            }

            if (textures == null || textures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " color texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") + ".";
                return false;
            }

            if (emissiveTextures != null && emissiveTextures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " emissive texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") +
                          " when an emissive array is supplied.";
                return false;
            }

            if (normalTextures != null && normalTextures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " normal texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") +
                          " when a normal array is supplied.";
                return false;
            }

            Texture2D[] normalizedEmissive = emissiveTextures ??
                                             new Texture2D[contract.Length];
            Texture2D[] normalizedNormals = normalTextures ??
                                            new Texture2D[contract.Length];
            return CreateOrUpdateManagedArtwork(
                template,
                profile,
                layer,
                out message,
                false,
                new TextureReplacement
                {
                    Textures = (Texture2D[])textures.Clone(),
                    EmissiveTextures = (Texture2D[])normalizedEmissive.Clone(),
                    NormalTextures = (Texture2D[])normalizedNormals.Clone()
                });
        }

        /// <summary>
        /// Returns true only for a profile-owned SpriteAsset created by the direct texture
        /// API. Semantic palette bakes intentionally leave these exact authored sheets alone.
        /// </summary>
        public static bool UsesDirectTextureOverride(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            if (profile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty reference = serializedProfile.FindProperty(
                descriptor.ReferenceProperty);
            DimensionPortalArtworkReferenceKind kind = ClassifyReference(
                reference,
                profile,
                layer,
                out SpriteAsset asset);
            if (kind != DimensionPortalArtworkReferenceKind.Managed || asset == null)
            {
                return false;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return TryReadManagedMetadata(
                       assetPath,
                       profile,
                       descriptor,
                       out ManagedArtworkMetadata metadata) &&
                   metadata.directTextureOverride;
        }

        public static bool AssignFrameworkReference(
            SerializedObject serializedProfile,
            DimensionPortalArtworkLayer layer)
        {
            if (serializedProfile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            SerializedProperty reference = serializedProfile.FindProperty(descriptor.ReferenceProperty);
            if (reference == null)
            {
                return false;
            }

            SetFrameworkReference(reference, descriptor);
            SetPaletteToSource(serializedProfile, descriptor);
            InvalidateReferenceCache();
            return true;
        }

        private static bool TryBuildTextureSlots(
            SpriteAsset framework,
            SpriteAsset selected,
            DimensionPortalArtworkLayer layer,
            out TextureSlot[] slots,
            out string message)
        {
            slots = Array.Empty<TextureSlot>();
            message = string.Empty;
            if (framework == null || selected == null)
            {
                message = "The portal artwork SpriteAsset could not be resolved.";
                return false;
            }

            SerializedObject serializedFramework = new SerializedObject(framework);
            serializedFramework.Update();
            SerializedObject serializedSelected = new SerializedObject(selected);
            serializedSelected.Update();
            if (layer == DimensionPortalArtworkLayer.Frame)
            {
                SerializedProperty frameworkData = serializedFramework.FindProperty(
                    "m_staticSpriteData");
                SerializedProperty selectedData = serializedSelected.FindProperty(
                    "m_staticSpriteData");
                if (!TryReadSpriteDataTextures(
                        frameworkData,
                        out Texture2D frameworkTexture,
                        out _,
                        out _) ||
                    frameworkTexture == null ||
                    !TryReadSpriteDataTextures(
                        selectedData,
                        out Texture2D selectedTexture,
                        out Texture2D selectedEmissive,
                        out Texture2D selectedNormal))
                {
                    message = "The Frame SpriteAsset does not expose its static texture contract.";
                    return false;
                }

                slots = new[]
                {
                    new TextureSlot
                    {
                        DisplayName = "Frame",
                        AnimationIndex = -1,
                        FrameCount = 1,
                        RequiredWidth = frameworkTexture.width,
                        RequiredHeight = frameworkTexture.height,
                        NativeWidth = frameworkTexture.width,
                        NativeHeight = frameworkTexture.height,
                        Texture = selectedTexture,
                        EmissiveTexture = selectedEmissive,
                        NormalTexture = selectedNormal
                    }
                };
                return true;
            }

            SerializedProperty frameworkAnimations = serializedFramework.FindProperty(
                "m_animations");
            SerializedProperty selectedAnimations = serializedSelected.FindProperty(
                "m_animations");
            if (frameworkAnimations == null ||
                selectedAnimations == null ||
                frameworkAnimations.arraySize == 0 ||
                selectedAnimations.arraySize != frameworkAnimations.arraySize)
            {
                message = "The selected " + GetDescriptor(layer).DisplayName +
                          " SpriteAsset does not match the framework animation-slot contract.";
                return false;
            }

            slots = new TextureSlot[frameworkAnimations.arraySize];
            for (int i = 0; i < frameworkAnimations.arraySize; i++)
            {
                SerializedProperty frameworkAnimation =
                    frameworkAnimations.GetArrayElementAtIndex(i);
                SerializedProperty selectedAnimation =
                    selectedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty frameCountProperty =
                    frameworkAnimation.FindPropertyRelative("srcFrameCount");
                SerializedProperty selectedFrameCountProperty =
                    selectedAnimation.FindPropertyRelative("srcFrameCount");
                SerializedProperty frameworkData =
                    frameworkAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty selectedData =
                    selectedAnimation.FindPropertyRelative("m_spriteData");
                if (!TryReadSpriteDataTextures(
                        frameworkData,
                        out Texture2D frameworkTexture,
                        out _,
                        out _) ||
                    frameworkTexture == null ||
                    !TryReadSpriteDataTextures(
                        selectedData,
                        out Texture2D selectedTexture,
                        out Texture2D selectedEmissive,
                        out Texture2D selectedNormal))
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName +
                              " does not expose a complete texture contract.";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                int selectedFrameCount = selectedFrameCountProperty == null
                    ? 0
                    : selectedFrameCountProperty.intValue;
                if (frameCount <= 0 || frameworkTexture.width % frameCount != 0)
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName +
                              " is not an evenly-divided native sheet.";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                if (selectedFrameCount != frameCount)
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " must preserve its " +
                              frameCount + " source frames. Selected: " +
                              selectedFrameCount + ".";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                if (!TryValidateSelectedTextureContract(
                        layer,
                        frameCount,
                        frameworkTexture,
                        selectedTexture,
                        selectedEmissive,
                        selectedNormal,
                        GetTextureSlotDisplayName(layer, i),
                        out message))
                {
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                bool supportsFullCanvas =
                    layer == DimensionPortalArtworkLayer.Center ||
                    layer == DimensionPortalArtworkLayer.CenterInstant;

                slots[i] = new TextureSlot
                {
                    DisplayName = GetTextureSlotDisplayName(layer, i),
                    AnimationIndex = i,
                    FrameCount = frameCount,
                    RequiredWidth = selectedTexture.width,
                    RequiredHeight = selectedTexture.height,
                    NativeWidth = frameworkTexture.width,
                    NativeHeight = frameworkTexture.height,
                    AlternateWidth = supportsFullCanvas
                        ? frameCount * DimensionPortalVisualContract.CanonicalFramePixels
                        : 0,
                    AlternateHeight = supportsFullCanvas
                        ? DimensionPortalVisualContract.CanonicalFramePixels
                        : 0,
                    Texture = selectedTexture,
                    EmissiveTexture = selectedEmissive,
                    NormalTexture = selectedNormal
                };
            }

            return true;
        }

        private static bool TryValidateSelectedTextureContract(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            Texture2D frameworkTexture,
            Texture2D selectedTexture,
            Texture2D selectedEmissive,
            Texture2D selectedNormal,
            string label,
            out string message)
        {
            message = string.Empty;
            if (frameworkTexture == null || selectedTexture == null)
            {
                message = label + " has no usable color sheet.";
                return false;
            }

            if (!IsAllowedAnimatedSheetSize(
                    layer,
                    frameCount,
                    frameworkTexture.width,
                    frameworkTexture.height,
                    selectedTexture.width,
                    selectedTexture.height))
            {
                message = label + " color sheet must be " +
                          GetAllowedAnimatedSheetSizeText(
                              layer,
                              frameCount,
                              frameworkTexture.width,
                              frameworkTexture.height) +
                          ". Selected: " + selectedTexture.width + " x " +
                          selectedTexture.height + ".";
                return false;
            }

            if (!TextureMatchesSize(selectedEmissive, selectedTexture.width, selectedTexture.height))
            {
                message = label + " emissive sheet must match its color sheet at " +
                          selectedTexture.width + " x " + selectedTexture.height + ". Selected: " +
                          selectedEmissive.width + " x " + selectedEmissive.height + ".";
                return false;
            }

            if (!TextureMatchesSize(selectedNormal, selectedTexture.width, selectedTexture.height))
            {
                message = label + " normal sheet must match its color sheet at " +
                          selectedTexture.width + " x " + selectedTexture.height + ". Selected: " +
                          selectedNormal.width + " x " + selectedNormal.height + ".";
                return false;
            }

            return true;
        }

        private static bool IsAllowedAnimatedSheetSize(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            int nativeWidth,
            int nativeHeight,
            int candidateWidth,
            int candidateHeight)
        {
            if (candidateWidth == nativeWidth && candidateHeight == nativeHeight)
            {
                return true;
            }

            // Beyond the exact vanilla-native size, allow any evenly-framed custom sheet whose
            // frames fit within the 48x48 portal canvas. Creators can make an overlay larger,
            // smaller, or differently proportioned than vanilla as long as it stays inside the
            // artboard; the Studio and generated portal clip anything beyond it.
            int cap = DimensionPortalVisualContract.CanonicalFramePixels;
            return frameCount > 0 &&
                   candidateWidth > 0 &&
                   candidateHeight > 0 &&
                   candidateWidth % frameCount == 0 &&
                   candidateWidth / frameCount <= cap &&
                   candidateHeight <= cap;
        }

        private static bool TextureMatchesSize(Texture2D texture, int width, int height)
        {
            return texture == null || (texture.width == width && texture.height == height);
        }

        private static string GetAllowedAnimatedSheetSizeText(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            int nativeWidth,
            int nativeHeight)
        {
            int cap = DimensionPortalVisualContract.CanonicalFramePixels;
            return nativeWidth + " x " + nativeHeight +
                   " pixels (native), or any evenly-framed sheet whose frames fit within " +
                   cap + " x " + cap + " pixels";
        }

        private static bool TryReadSpriteDataTextures(
            SerializedProperty spriteData,
            out Texture2D texture,
            out Texture2D emissive,
            out Texture2D normal)
        {
            texture = null;
            emissive = null;
            normal = null;
            if (spriteData == null)
            {
                return false;
            }

            SerializedProperty textureProperty = spriteData.FindPropertyRelative("texture");
            SerializedProperty emissiveProperty =
                spriteData.FindPropertyRelative("emissiveTexture");
            SerializedProperty normalProperty =
                spriteData.FindPropertyRelative("normalTexture");
            if (textureProperty == null ||
                emissiveProperty == null ||
                normalProperty == null)
            {
                return false;
            }

            texture = textureProperty.objectReferenceValue as Texture2D;
            emissive = emissiveProperty.objectReferenceValue as Texture2D;
            normal = normalProperty.objectReferenceValue as Texture2D;
            return true;
        }

        private static string GetTextureSlotDisplayName(
            DimensionPortalArtworkLayer layer,
            int animationIndex)
        {
            if (layer == DimensionPortalArtworkLayer.CenterInstant)
            {
                return animationIndex == 0
                    ? "Loop"
                    : animationIndex == 1
                        ? "Opening"
                        : "Closing";
            }

            if (layer == DimensionPortalArtworkLayer.Center)
            {
                return animationIndex == 0 ? "Loop" : "Opening";
            }

            if (layer == DimensionPortalArtworkLayer.ChargeSweep)
            {
                return "Charging sweep";
            }

            if (layer == DimensionPortalArtworkLayer.Milestones)
            {
                return "Milestone sequence";
            }

            return "Frame";
        }
    }
}
