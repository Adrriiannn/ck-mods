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
    /// Turning chosen textures into the sheets the portal draws from.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        private static bool PrepareStaticFrameArtwork(
            SpriteAsset source,
            SpriteAsset managed,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceData = serializedSource.FindProperty("m_staticSpriteData");
            if (!TryReadSpriteDataTextures(
                    sourceData,
                    out Texture2D sourceTexture,
                    out Texture2D sourceEmissive,
                    out Texture2D sourceNormal) ||
                sourceTexture == null)
            {
                message = "The framework Frame SpriteAsset has no static portal texture.";
                return false;
            }

            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedData = serializedManaged.FindProperty("m_staticSpriteData");
            SerializedProperty managedTextureProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("texture");
            SerializedProperty managedEmissiveProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("emissiveTexture");
            SerializedProperty managedNormalProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("normalTexture");
            if (managedTextureProperty == null ||
                managedEmissiveProperty == null ||
                managedNormalProperty == null)
            {
                message = "The editable Frame SpriteAsset has no static texture fields.";
                return false;
            }

            managedTextureProperty.objectReferenceValue = sourceTexture;
            managedEmissiveProperty.objectReferenceValue = sourceEmissive;
            managedNormalProperty.objectReferenceValue = sourceNormal;
            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PrepareTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            DimensionPortalArtworkLayer layer,
            TextureReplacement replacement,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            if (replacement == null || replacement.Textures == null)
            {
                message = "No portal texture replacement was supplied.";
                return false;
            }

            if (!TryBuildTextureSlots(
                    source,
                    source,
                    layer,
                    out TextureSlot[] contract,
                    out message) ||
                replacement.Textures.Length != contract.Length ||
                replacement.EmissiveTextures == null ||
                replacement.EmissiveTextures.Length != contract.Length ||
                replacement.NormalTextures == null ||
                replacement.NormalTextures.Length != contract.Length)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The supplied portal sheets do not match the native " +
                              GetDescriptor(layer).DisplayName + " slot count.";
                }

                return false;
            }

            // Each committed edit receives immutable PNG assets. Unity Undo can then
            // restore the SpriteAsset's previous texture references without relying on
            // bytes that were overwritten in place; Redo remains valid for the same reason.
            string revision = Guid.NewGuid().ToString("N").Substring(0, 12);
            return layer == DimensionPortalArtworkLayer.Frame
                ? PrepareStaticTextureReplacement(
                    source,
                    managed,
                    managedPath,
                    modRoot,
                    replacement,
                    revision,
                    transaction,
                    out message)
                : PrepareAnimatedTextureReplacement(
                    source,
                    managed,
                    managedPath,
                    modRoot,
                    layer,
                    replacement,
                    revision,
                    transaction,
                    out message);
        }

        private static bool HasAuthoredTextureSelection(TextureReplacement replacement)
        {
            if (replacement == null)
            {
                return false;
            }

            for (int i = 0; replacement.Textures != null && i < replacement.Textures.Length; i++)
            {
                if (replacement.Textures[i] != null)
                {
                    return true;
                }
            }

            for (int i = 0;
                 replacement.EmissiveTextures != null && i < replacement.EmissiveTextures.Length;
                 i++)
            {
                if (replacement.EmissiveTextures[i] != null)
                {
                    return true;
                }
            }

            for (int i = 0;
                 replacement.NormalTextures != null && i < replacement.NormalTextures.Length;
                 i++)
            {
                if (replacement.NormalTextures[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PrepareStaticTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            TextureReplacement replacement,
            string revision,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceData = serializedSource.FindProperty("m_staticSpriteData");
            if (!TryReadSpriteDataTextures(
                    sourceData,
                    out Texture2D sourceTexture,
                    out Texture2D sourceEmissive,
                    out Texture2D sourceNormal) ||
                sourceTexture == null)
            {
                message = "The framework Frame SpriteAsset has no static texture contract.";
                return false;
            }

            Texture2D requestedTexture = replacement.Textures[0] ?? sourceTexture;
            Texture2D requestedEmissive = replacement.EmissiveTextures[0];
            if (requestedEmissive == null)
            {
                requestedEmissive = sourceEmissive == sourceTexture &&
                                    replacement.Textures[0] != null
                    ? requestedTexture
                    : sourceEmissive;
            }
            Texture2D requestedNormal = replacement.NormalTextures[0] ?? sourceNormal;

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            if (!CopyManagedPortalTexture(
                    requestedTexture,
                    folder + "/" + stem + "_Static_Custom_" + revision + ".png",
                    "Portal frame",
                    modRoot,
                    sourceTexture.width,
                    sourceTexture.height,
                    transaction,
                    out Texture2D targetTexture,
                    out message))
            {
                return false;
            }

            Texture2D targetEmissive = null;
            if (requestedEmissive == requestedTexture)
            {
                targetEmissive = targetTexture;
            }
            else if (requestedEmissive != null &&
                     !CopyManagedPortalTexture(
                         requestedEmissive,
                         folder + "/" + stem + "_Static_Custom_" + revision + "_Emissive.png",
                         "Portal frame emissive",
                         modRoot,
                         sourceTexture.width,
                         sourceTexture.height,
                         transaction,
                         out targetEmissive,
                         out message))
            {
                return false;
            }

            Texture2D targetNormal = null;
            if (requestedNormal != null &&
                !CopyManagedPortalTexture(
                    requestedNormal,
                    folder + "/" + stem + "_Static_Custom_" + revision + "_Normal.png",
                    "Portal frame normal",
                    modRoot,
                    sourceTexture.width,
                    sourceTexture.height,
                    transaction,
                    true,
                    out targetNormal,
                    out message))
            {
                return false;
            }

            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedData = serializedManaged.FindProperty("m_staticSpriteData");
            SerializedProperty managedTexture = managedData?.FindPropertyRelative("texture");
            SerializedProperty managedEmissive =
                managedData?.FindPropertyRelative("emissiveTexture");
            SerializedProperty managedNormal =
                managedData?.FindPropertyRelative("normalTexture");
            if (managedTexture == null ||
                managedEmissive == null ||
                managedNormal == null)
            {
                message = "The editable Frame SpriteAsset has no static texture fields.";
                return false;
            }

            managedTexture.objectReferenceValue = targetTexture;
            managedEmissive.objectReferenceValue = targetEmissive;
            managedNormal.objectReferenceValue = targetNormal;
            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PrepareAnimatedTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            DimensionPortalArtworkLayer layer,
            TextureReplacement replacement,
            string revision,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedAnimations = serializedManaged.FindProperty("m_animations");
            if (sourceAnimations == null ||
                managedAnimations == null ||
                sourceAnimations.arraySize == 0 ||
                sourceAnimations.arraySize != managedAnimations.arraySize ||
                sourceAnimations.arraySize != replacement.Textures.Length)
            {
                message = "The editable " + GetDescriptor(layer).DisplayName +
                          " SpriteAsset does not preserve its native animation contract.";
                return false;
            }

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation =
                    sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty sourceData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                if (!TryReadSpriteDataTextures(
                        sourceData,
                        out Texture2D sourceTexture,
                        out Texture2D sourceEmissive,
                        out Texture2D sourceNormal) ||
                    sourceTexture == null)
                {
                    message = "Framework animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " is incomplete.";
                    return false;
                }

                Texture2D requestedTexture = replacement.Textures[i] ?? sourceTexture;
                Texture2D requestedEmissive = replacement.EmissiveTextures[i];
                if (sourceEmissive == sourceTexture &&
                    replacement.Textures[i] != null &&
                    (requestedEmissive == null || requestedEmissive == sourceEmissive))
                {
                    // The framework Center sheets use one PNG for both color and emission.
                    // When an author replaces only Color, keep that relationship intact so
                    // switching to the 48 x 48 full-canvas mode is one deliberate selection
                    // rather than an immediate native/full-size mismatch.
                    requestedEmissive = requestedTexture;
                }
                else if (requestedEmissive == null)
                {
                    requestedEmissive = sourceEmissive == sourceTexture &&
                                        replacement.Textures[i] != null
                        ? requestedTexture
                        : sourceEmissive;
                }
                Texture2D requestedNormal = replacement.NormalTextures[i] ?? sourceNormal;

                SerializedProperty sourceFrameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                int frameCount = sourceFrameCountProperty == null
                    ? 0
                    : sourceFrameCountProperty.intValue;
                SerializedProperty managedAnimation =
                    managedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty managedFrameCountProperty =
                    managedAnimation.FindPropertyRelative("srcFrameCount");
                int managedFrameCount = managedFrameCountProperty == null
                    ? 0
                    : managedFrameCountProperty.intValue;
                string slotLabel = GetTextureSlotDisplayName(layer, i);
                if (frameCount <= 0 || managedFrameCount != frameCount)
                {
                    message = slotLabel + " must retain the framework's " + frameCount +
                              " source frames. The editable SpriteAsset currently has " +
                              managedFrameCount + ".";
                    return false;
                }

                if (
                    !TryValidateSelectedTextureContract(
                        layer,
                        frameCount,
                        sourceTexture,
                        requestedTexture,
                        requestedEmissive,
                        requestedNormal,
                        slotLabel,
                        out message))
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message = slotLabel + " has no valid source-frame contract.";
                    }

                    return false;
                }

                int targetWidth = requestedTexture.width;
                int targetHeight = requestedTexture.height;

                string slotStem = stem + "_Anim" + i + "_Custom_" + revision;
                if (!CopyManagedPortalTexture(
                        requestedTexture,
                        folder + "/" + slotStem + ".png",
                        slotLabel,
                        modRoot,
                        targetWidth,
                        targetHeight,
                        transaction,
                        out Texture2D targetTexture,
                        out message))
                {
                    return false;
                }

                Texture2D targetEmissive = null;
                if (requestedEmissive == requestedTexture)
                {
                    targetEmissive = targetTexture;
                }
                else if (requestedEmissive != null &&
                          !CopyManagedPortalTexture(
                              requestedEmissive,
                             folder + "/" + slotStem + "_Emissive.png",
                             slotLabel + " emissive",
                             modRoot,
                             targetWidth,
                             targetHeight,
                             transaction,
                             out targetEmissive,
                             out message))
                {
                    return false;
                }

                Texture2D targetNormal = null;
                if (requestedNormal != null &&
                    !CopyManagedPortalTexture(
                        requestedNormal,
                        folder + "/" + slotStem + "_Normal.png",
                        slotLabel + " normal",
                        modRoot,
                        targetWidth,
                        targetHeight,
                        transaction,
                        true,
                        out targetNormal,
                        out message))
                {
                    return false;
                }

                SerializedProperty managedData =
                    managedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty managedTexture =
                    managedData?.FindPropertyRelative("texture");
                SerializedProperty managedEmissive =
                    managedData?.FindPropertyRelative("emissiveTexture");
                SerializedProperty managedNormal =
                    managedData?.FindPropertyRelative("normalTexture");
                if (managedTexture == null ||
                    managedEmissive == null ||
                    managedNormal == null)
                {
                    message = "Editable animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " is incomplete.";
                    return false;
                }

                managedTexture.objectReferenceValue = targetTexture;
                managedEmissive.objectReferenceValue = targetEmissive;
                managedNormal.objectReferenceValue = targetNormal;
            }

            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool CopyManagedPortalTexture(
            Texture2D texture,
            string targetPath,
            string label,
            string modRoot,
            int requiredWidth,
            int requiredHeight,
            ArtworkFileTransaction transaction,
            out Texture2D managedTexture,
            out string message)
        {
            return CopyManagedPortalTexture(
                texture,
                targetPath,
                label,
                modRoot,
                requiredWidth,
                requiredHeight,
                transaction,
                false,
                out managedTexture,
                out message);
        }

        private static bool CopyManagedPortalTexture(
            Texture2D texture,
            string targetPath,
            string label,
            string modRoot,
            int requiredWidth,
            int requiredHeight,
            ArtworkFileTransaction transaction,
            bool normalMap,
            out Texture2D managedTexture,
            out string message)
        {
            managedTexture = null;
            message = string.Empty;
            if (texture == null)
            {
                message = label + " texture could not be resolved.";
                return false;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(path) ||
                (!AssetPathIsWithin(path, modRoot) &&
                 !AssetPathIsWithin(path, "Assets/ExpandNullforge")))
            {
                message = label + " must be saved inside this dimension mod or the Dimensions API framework.";
                return false;
            }

            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                message = label +
                          " must be a saved PNG so generated portal sprites preserve exact pixels and alpha.";
                return false;
            }

            if (texture.width != requiredWidth || texture.height != requiredHeight)
            {
                message = label + " must be " + requiredWidth + " x " + requiredHeight +
                          " pixels to match its native portal sheet. Selected: " +
                          texture.width + " x " + texture.height + ".";
                return false;
            }

            string absolutePath = AssetPathToAbsolutePath(path);
            if (transaction == null ||
                string.IsNullOrEmpty(absolutePath) ||
                !File.Exists(absolutePath))
            {
                message = label + " PNG bytes could not be read from " + path + ".";
                return false;
            }

            transaction.ReplaceAssetBytes(targetPath, File.ReadAllBytes(absolutePath));
            ConfigureSpriteTextureImporter(
                targetPath,
                requiredWidth,
                requiredHeight,
                normalMap);
            managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (managedTexture == null)
            {
                message = "Could not import the managed " + label + " PNG at " +
                          targetPath + ".";
                return false;
            }

            return true;
        }

        private static bool BakeAnimationTextures(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            LayerDescriptor descriptor,
            Color[] palette,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedAnimations = serializedManaged.FindProperty("m_animations");
            if (sourceAnimations == null ||
                managedAnimations == null ||
                sourceAnimations.arraySize == 0 ||
                managedAnimations.arraySize != sourceAnimations.arraySize)
            {
                message = "The " + descriptor.DisplayName +
                          " SpriteAsset animation contract could not be preserved.";
                return false;
            }

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation = sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty sourceSpriteData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty sourceTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("texture");
                SerializedProperty sourceEmissiveProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("emissiveTexture");
                SerializedProperty frameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                Texture2D sourceTexture = sourceTextureProperty == null
                    ? null
                    : sourceTextureProperty.objectReferenceValue as Texture2D;
                Texture2D sourceEmissive = sourceEmissiveProperty == null
                    ? null
                    : sourceEmissiveProperty.objectReferenceValue as Texture2D;
                int frameCount = frameCountProperty == null ? 0 : frameCountProperty.intValue;
                if (sourceTexture == null || frameCount <= 0)
                {
                    message = "Animation " + i + " in the framework " +
                              descriptor.DisplayName + " artwork is incomplete.";
                    return false;
                }

                string texturePath = folder + "/" + stem + "_Anim" + i + ".png";
                Texture2D bakedTexture = BakeTexture(
                    sourceTexture,
                    texturePath,
                    frameCount,
                    descriptor.SourcePalette,
                    palette,
                    transaction);
                Texture2D bakedEmissive = null;
                if (sourceEmissive == sourceTexture)
                {
                    bakedEmissive = bakedTexture;
                }
                else if (sourceEmissive != null)
                {
                    bakedEmissive = BakeTexture(
                        sourceEmissive,
                        folder + "/" + stem + "_Anim" + i + "_Emissive.png",
                        frameCount,
                        descriptor.SourcePalette,
                        palette,
                        transaction);
                }

                SerializedProperty managedAnimation = managedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty managedSpriteData =
                    managedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty managedTextureProperty = managedSpriteData == null
                    ? null
                    : managedSpriteData.FindPropertyRelative("texture");
                SerializedProperty managedEmissiveProperty = managedSpriteData == null
                    ? null
                    : managedSpriteData.FindPropertyRelative("emissiveTexture");
                if (managedTextureProperty == null || managedEmissiveProperty == null)
                {
                    message = "Animation " + i + " in the editable " +
                              descriptor.DisplayName + " artwork is incomplete.";
                    return false;
                }

                managedTextureProperty.objectReferenceValue = bakedTexture;
                managedEmissiveProperty.objectReferenceValue = bakedEmissive;
            }

            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static Texture2D BakeTexture(
            Texture2D source,
            string targetPath,
            int frameCount,
            Color32[] sourcePalette,
            Color[] targetPalette,
            ArtworkFileTransaction transaction)
        {
            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            string sourceAbsolute = AssetPathToAbsolutePath(sourcePath);
            if (string.IsNullOrEmpty(sourceAbsolute) ||
                !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourceAbsolute))
            {
                throw new InvalidOperationException(
                    "Portal palette source textures must be saved PNG files. Current source: " +
                    sourcePath + ".");
            }

            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D output = null;
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(sourceAbsolute)) ||
                    frameCount <= 0 ||
                    decoded.width % frameCount != 0)
                {
                    throw new InvalidOperationException(
                        "Could not decode an evenly divided portal animation sheet at " +
                        sourcePath + ".");
                }

                Color32[] sourcePixels = decoded.GetPixels32();
                Color32[] targetPixels = new Color32[sourcePixels.Length];
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color32 pixel = sourcePixels[i];
                    if (pixel.a == 0)
                    {
                        targetPixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (sourcePalette == null ||
                        targetPalette == null ||
                        sourcePalette.Length == 0 ||
                        targetPalette.Length != sourcePalette.Length)
                    {
                        // Frame artwork has no semantic recolor contract. An explicit
                        // editable copy preserves its source pixels exactly.
                        targetPixels[i] = pixel;
                    }
                    else
                    {
                        int paletteIndex = FindClosestPaletteIndex(pixel, sourcePalette);
                        Color target = targetPalette[paletteIndex];
                        Color32 recolored = target;
                        recolored.a = (byte)Mathf.Clamp(
                            Mathf.RoundToInt(pixel.a * Mathf.Clamp01(target.a)),
                            0,
                            255);
                        targetPixels[i] = recolored;
                    }
                }

                output = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
                output.SetPixels32(targetPixels);
                output.Apply(false, false);
                if (transaction == null)
                {
                    throw new InvalidOperationException(
                        "A portal artwork file transaction was not available.");
                }

                transaction.ReplaceAssetBytes(targetPath, output.EncodeToPNG());
                ConfigureSpriteTextureImporter(targetPath, decoded.width, decoded.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (output != null)
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }

            Texture2D baked = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (baked == null)
            {
                throw new InvalidOperationException(
                    "Could not import editable portal artwork texture at " + targetPath + ".");
            }

            return baked;
        }

        internal static void ConfigureSpriteTextureImporter(
            string textureAssetPath,
            int sourceWidth,
            int sourceHeight,
            bool normalMap = false)
        {
            TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(
                    textureAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            }

            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Could not configure editable portal texture " + textureAssetPath + ".");
            }

            importer.textureType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Sprite;
            if (!normalMap)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
            }

            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = !normalMap;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            int largest = Mathf.Max(sourceWidth, sourceHeight);
            if (largest > 0)
            {
                importer.maxTextureSize = Mathf.Max(
                    importer.maxTextureSize,
                    Mathf.NextPowerOfTwo(largest));
            }

            importer.SaveAndReimport();
        }
    }
}
