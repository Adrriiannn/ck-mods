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
    /// Baking a portal sheet to a chosen palette, and finding the colour it should have.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static GeneratedPortalSpriteAsset ResolvePortalPaletteSpriteAsset(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            DataBlockRef<SpriteAsset> overrideReference,
            long fallbackAddressLow,
            long fallbackAddressHigh,
            string sourceSpriteAssetPath,
            string assetSuffix,
            string layerKey,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            bool frameworkReference =
                overrideReference.hasAddress &&
                overrideReference.address.lowBits == fallbackAddressLow &&
                overrideReference.address.highBits == fallbackAddressHigh;
            if (overrideReference.hasAddress && !frameworkReference)
            {
                SpriteAsset overrideAsset = ResolvePortalSpriteAssetReference(
                    overrideReference,
                    label);
                string generatedAssetPath = GetPaletteBakedPortalSpriteAssetPath(
                    portalOutput,
                    portalFolder,
                    assetSuffix);
                string overrideAssetPath = NormalizeAssetPath(
                    AssetDatabase.GetAssetPath(overrideAsset));
                string normalizedPortalFolder = NormalizeAssetPath(portalFolder);
                if (overrideAssetPath == generatedAssetPath ||
                    overrideAssetPath.StartsWith(
                        normalizedPortalFolder + "/",
                        System.StringComparison.Ordinal))
                {
                    throw new System.InvalidOperationException(
                        "The configured " + label +
                        " override points at a generator-owned portal asset. " +
                        "Choose an authored SpriteAsset outside the Generated/Portal folder instead.");
                }

                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    assetSuffix);
                return ResolvePortalSpriteAssetOverride(
                    overrideAsset,
                    fallbackAddressLow,
                    fallbackAddressHigh,
                    modRoot,
                    label);
            }

            if (PortalPaletteMatchesSource(sourcePalette, targetPalette))
            {
                DeletePaletteBakedPortalSpriteAssetIfPresent(
                    portalOutput,
                    portalFolder,
                    modRoot,
                    assetSuffix);
                return new GeneratedPortalSpriteAsset
                {
                    AddressLow = fallbackAddressLow,
                    AddressHigh = fallbackAddressHigh,
                    Asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(sourceSpriteAssetPath)
                };
            }

            return EnsurePaletteBakedPortalSpriteAsset(
                portalOutput,
                portalFolder,
                modRoot,
                sourceSpriteAssetPath,
                assetSuffix,
                layerKey,
                label,
                sourcePalette,
                targetPalette);
        }

        private static string GetPaletteBakedPortalSpriteAssetPath(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string assetSuffix)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + assetSuffix,
                "Dimension" + assetSuffix);
            return portalFolder + "/" + assetName + ".asset";
        }

        private static void DeletePaletteBakedPortalSpriteAssetIfPresent(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string assetSuffix)
        {
            string outputAssetPath = GetPaletteBakedPortalSpriteAssetPath(
                portalOutput,
                portalFolder,
                assetSuffix);
            string assetName = Path.GetFileNameWithoutExtension(outputAssetPath);
            SpriteAsset generated =
                AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
            if (generated != null)
            {
                RemoveSpriteAssetManifestReference(modRoot, generated);
                AssetDatabase.DeleteAsset(outputAssetPath);
            }

            if (!AssetDatabase.IsValidFolder(portalFolder))
            {
                return;
            }

            string texturePrefix = assetName + "Anim";
            string[] textureGuids = AssetDatabase.FindAssets(
                texturePrefix + " t:Texture2D",
                new[] { portalFolder });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string texturePath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(textureGuids[i]));
                if (!texturePath.StartsWith(portalFolder + "/", System.StringComparison.Ordinal) ||
                    !texturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileNameWithoutExtension(texturePath).StartsWith(
                        texturePrefix,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        private static bool PortalPaletteMatchesSource(
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            if (sourcePalette == null ||
                targetPalette == null ||
                sourcePalette.Length == 0 ||
                sourcePalette.Length != targetPalette.Length)
            {
                return false;
            }

            for (int i = 0; i < sourcePalette.Length; i++)
            {
                Color32 target = targetPalette[i];
                Color32 source = sourcePalette[i];
                if (source.r != target.r ||
                    source.g != target.g ||
                    source.b != target.b ||
                    source.a != target.a)
                {
                    return false;
                }
            }

            return true;
        }

        private static SpriteAsset ResolvePortalSpriteAssetReference(
            DataBlockRef<SpriteAsset> reference,
            string label)
        {
            if (!reference.hasAddress)
            {
                return null;
            }

            SpriteAsset resolved;
            if (reference.TryGet(out resolved) &&
                resolved != null &&
                !IsGeneratorOwnedPortalSpriteAsset(resolved))
            {
                return resolved;
            }

            SpriteAsset generatedFallback = resolved;
            string[] candidateGuids = AssetDatabase.FindAssets(
                "t:SpriteAsset",
                new[] { "Assets" });
            for (int i = 0; i < candidateGuids.Length; i++)
            {
                string candidatePath = NormalizeAssetPath(
                    AssetDatabase.GUIDToAssetPath(candidateGuids[i]));
                SpriteAsset candidate =
                    AssetDatabase.LoadAssetAtPath<SpriteAsset>(candidatePath);
                if (candidate != null && candidate.address == reference.address)
                {
                    if (!IsGeneratorOwnedPortalSpriteAsset(candidate))
                    {
                        return candidate;
                    }

                    if (generatedFallback == null)
                    {
                        generatedFallback = candidate;
                    }
                }
            }

            if (generatedFallback != null)
            {
                return generatedFallback;
            }

            throw new System.InvalidOperationException(
                "Could not resolve the configured " + label +
                " SpriteAsset data-block reference at address " +
                reference.address +
                ". Make sure the SpriteAsset is saved inside the target dimension mod or ExpandNullforge.");
        }

        private static bool IsGeneratorOwnedPortalSpriteAsset(SpriteAsset asset)
        {
            string path = asset == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return path.IndexOf(
                "/Generated/Portal/",
                System.StringComparison.Ordinal) >= 0;
        }

        private static GeneratedPortalSpriteAsset EnsurePaletteBakedPortalSpriteAsset(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string sourceSpriteAssetPath,
            string assetSuffix,
            string layerKey,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            SpriteAsset source = AssetDatabase.LoadAssetAtPath<SpriteAsset>(sourceSpriteAssetPath);
            if (source == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the framework " + label + " SpriteAsset at " +
                    sourceSpriteAssetPath + ".");
            }

            if (sourcePalette == null ||
                targetPalette == null ||
                sourcePalette.Length == 0 ||
                sourcePalette.Length != targetPalette.Length)
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " palette is incomplete.");
            }

            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + assetSuffix,
                "Dimension" + assetSuffix);
            string outputAssetPath = portalFolder + "/" + assetName + ".asset";
            string addressSeed =
                portalOutput.PortalObjectName + ":generated-portal-palette:" + layerKey;
            long addressLow = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x70616C657474656CUL);
            long addressHigh = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x706F7274616C7669UL);

            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            if (sourceAnimations == null || sourceAnimations.arraySize == 0)
            {
                throw new System.InvalidOperationException(
                    "The framework " + label + " SpriteAsset does not contain animations.");
            }

            Texture2D[] bakedTextures = new Texture2D[sourceAnimations.arraySize];
            Texture2D[] bakedEmissiveTextures = new Texture2D[sourceAnimations.arraySize];
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation = sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty frameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                SerializedProperty sourceSpriteData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty sourceTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("texture");
                SerializedProperty sourceEmissiveTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("emissiveTexture");
                Texture2D sourceTexture = sourceTextureProperty == null
                    ? null
                    : sourceTextureProperty.objectReferenceValue as Texture2D;
                Texture2D sourceEmissiveTexture = sourceEmissiveTextureProperty == null
                    ? null
                    : sourceEmissiveTextureProperty.objectReferenceValue as Texture2D;
                if (sourceTexture == null || frameCount <= 0)
                {
                    throw new System.InvalidOperationException(
                        "Animation " + i + " in the framework " + label +
                        " SpriteAsset has no valid source texture or frame count.");
                }

                string animationStem = assetName + "Anim" + i.ToString(CultureInfo.InvariantCulture);
                bakedTextures[i] = EnsurePaletteBakedPortalTexture(
                    sourceTexture,
                    portalFolder + "/" + animationStem + ".png",
                    frameCount,
                    label,
                    sourcePalette,
                    targetPalette);
                if (sourceEmissiveTexture == null)
                {
                    bakedEmissiveTextures[i] = null;
                }
                else if (sourceEmissiveTexture == sourceTexture)
                {
                    bakedEmissiveTextures[i] = bakedTextures[i];
                }
                else
                {
                    bakedEmissiveTextures[i] = EnsurePaletteBakedPortalTexture(
                        sourceEmissiveTexture,
                        portalFolder + "/" + animationStem + "Emissive.png",
                        frameCount,
                        label + " emissive",
                        sourcePalette,
                        targetPalette);
                }
            }

            SpriteAsset generated = AssetDatabase.LoadAssetAtPath<SpriteAsset>(outputAssetPath);
            if (generated == null)
            {
                generated = Object.Instantiate(source);
                generated.name = assetName;
                AssetDatabase.CreateAsset(generated, outputAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, generated);
                generated.name = assetName;
            }

            SerializedObject serializedGenerated = new SerializedObject(generated);
            serializedGenerated.Update();
            SetSerializedLong(serializedGenerated, "m_address.m_low", addressLow);
            SetSerializedLong(serializedGenerated, "m_address.m_high", addressHigh);
            SerializedProperty generatedAnimations = serializedGenerated.FindProperty("m_animations");
            if (generatedAnimations == null || generatedAnimations.arraySize != bakedTextures.Length)
            {
                throw new System.InvalidOperationException(
                    "Could not preserve the animation layout while generating the " + label +
                    " palette SpriteAsset.");
            }

            for (int i = 0; i < generatedAnimations.arraySize; i++)
            {
                SerializedProperty generatedAnimation = generatedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty generatedSpriteData =
                    generatedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty generatedTextureProperty = generatedSpriteData == null
                    ? null
                    : generatedSpriteData.FindPropertyRelative("texture");
                SerializedProperty generatedEmissiveTextureProperty = generatedSpriteData == null
                    ? null
                    : generatedSpriteData.FindPropertyRelative("emissiveTexture");
                if (generatedTextureProperty == null || generatedEmissiveTextureProperty == null)
                {
                    throw new System.InvalidOperationException(
                        "Could not assign generated textures to animation " + i +
                        " in the " + label + " SpriteAsset.");
                }

                generatedTextureProperty.objectReferenceValue = bakedTextures[i];
                generatedEmissiveTextureProperty.objectReferenceValue = bakedEmissiveTextures[i];
            }

            serializedGenerated.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generated);
            EnsureSpriteAssetManifestContains(modRoot, outputAssetPath);
            return new GeneratedPortalSpriteAsset
            {
                AddressLow = addressLow,
                AddressHigh = addressHigh,
                Asset = generated
            };
        }

        private static Texture2D EnsurePaletteBakedPortalTexture(
            Texture2D source,
            string targetTexturePath,
            int frameCount,
            string label,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            string sourceTexturePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            string sourceAbsolutePath = AssetPathToAbsolutePath(sourceTexturePath);
            if (string.IsNullOrEmpty(sourceTexturePath) ||
                !sourceTexturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(sourceAbsolutePath) ||
                !File.Exists(sourceAbsolutePath))
            {
                throw new System.InvalidOperationException(
                    "The " + label + " palette source must be a saved PNG texture.");
            }

            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D targetTexture = null;
            try
            {
                if (!sourceTexture.LoadImage(File.ReadAllBytes(sourceAbsolutePath)))
                {
                    throw new System.InvalidOperationException(
                        "Could not decode the " + label + " palette source at " +
                        sourceTexturePath + ".");
                }

                int nativeFrameWidth = frameCount <= 0
                    ? 0
                    : sourceTexture.width / frameCount;
                if (frameCount <= 0 ||
                    sourceTexture.width <= 0 ||
                    sourceTexture.height <= 0 ||
                    sourceTexture.width % frameCount != 0 ||
                    nativeFrameWidth <= 0)
                {
                    throw new System.InvalidOperationException(
                        "The " + label + " animation must be a horizontal strip of evenly sized frames. " +
                        "Current texture: " + sourceTexture.width + " x " +
                        sourceTexture.height + ", frames: " + frameCount + ".");
                }

                Color32[] sourcePixels = sourceTexture.GetPixels32();
                Color32[] targetPixels = new Color32[sourcePixels.Length];
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color32 sourcePixel = sourcePixels[i];
                    if (sourcePixel.a == 0)
                    {
                        targetPixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int paletteIndex = FindClosestPortalPaletteIndex(
                        sourcePixel,
                        sourcePalette);
                    Color targetColor = targetPalette[paletteIndex];
                    Color32 targetPixel = targetColor;
                    targetPixel.a = (byte)Mathf.Clamp(
                        Mathf.RoundToInt(sourcePixel.a * Mathf.Clamp01(targetColor.a)),
                        0,
                        255);
                    targetPixels[i] = targetPixel;
                }

                targetTexture = new Texture2D(
                    sourceTexture.width,
                    sourceTexture.height,
                    TextureFormat.RGBA32,
                    false);
                targetTexture.SetPixels32(targetPixels);
                targetTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetTexturePath, targetTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetTexturePath,
                    sourceTexture.width,
                    sourceTexture.height);
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
                if (targetTexture != null)
                {
                    Object.DestroyImmediate(targetTexture);
                }
            }

            Texture2D generated = AssetDatabase.LoadAssetAtPath<Texture2D>(targetTexturePath);
            if (generated == null)
            {
                throw new System.InvalidOperationException(
                    "Could not import the generated " + label + " texture at " +
                    targetTexturePath + ".");
            }

            return generated;
        }

        private static int FindClosestPortalPaletteIndex(
            Color32 color,
            Color32[] palette)
        {
            int closestIndex = 0;
            int closestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int red = color.r - palette[i].r;
                int green = color.g - palette[i].g;
                int blue = color.b - palette[i].b;
                int distance = red * red + green * green + blue * blue;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private static GeneratedPortalSpriteAsset ResolvePortalSpriteAssetOverride(
            SpriteAsset overrideAsset,
            long fallbackAddressLow,
            long fallbackAddressHigh,
            string modRoot,
            string label)
        {
            if (overrideAsset == null)
            {
                return new GeneratedPortalSpriteAsset
                {
                    AddressLow = fallbackAddressLow,
                    AddressHigh = fallbackAddressHigh,
                    Asset = null
                };
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(overrideAsset));
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset has not been saved as a project asset.");
            }

            bool frameworkAsset =
                assetPath == "Assets/ExpandNullforge" ||
                assetPath.StartsWith("Assets/ExpandNullforge/");
            bool consumerAsset =
                assetPath == modRoot ||
                assetPath.StartsWith(modRoot + "/");
            if (!frameworkAsset && !consumerAsset)
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset must belong to the target dimension mod or ExpandNullforge. " +
                    "Current asset: " + assetPath + ".");
            }

            SerializedObject serializedAsset = new SerializedObject(overrideAsset);
            serializedAsset.Update();
            SerializedProperty address = serializedAsset.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null || (low.longValue == 0L && high.longValue == 0L))
            {
                throw new System.InvalidOperationException(
                    "The configured " + label + " SpriteAsset needs a non-zero SpriteAsset address before portal generation: " +
                    assetPath + ".");
            }

            EnsureSpriteAssetManifestContains(
                frameworkAsset ? "Assets/ExpandNullforge" : modRoot,
                assetPath);

            return new GeneratedPortalSpriteAsset
            {
                AddressLow = low.longValue,
                AddressHigh = high.longValue,
                Asset = overrideAsset
            };
        }

        private static string ResolvePortalFrameTexturePath(SpriteAsset frameAsset)
        {
            if (frameAsset == null)
            {
                return PortalBodyTexturePath;
            }

            Texture2D texture = frameAsset.staticSpriteData == null
                ? null
                : frameAsset.staticSpriteData.texture;
            string texturePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (texture == null || string.IsNullOrEmpty(texturePath))
            {
                throw new System.InvalidOperationException(
                    "The configured portal frame SpriteAsset needs a static source texture so the interaction outline can be generated.");
            }

            if (!texturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException(
                    "The configured portal frame texture must be a PNG so the generated outline can use its alpha silhouette: " +
                    texturePath + ".");
            }

            return texturePath;
        }

        private static Color MultiplyPortalColors(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }

        private static Color ScalePortalColor(Color color, float scale)
        {
            return new Color(
                color.r * scale,
                color.g * scale,
                color.b * scale,
                color.a);
        }
    }
}
