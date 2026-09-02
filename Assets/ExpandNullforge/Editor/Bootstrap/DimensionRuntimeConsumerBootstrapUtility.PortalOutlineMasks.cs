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
    /// The outline mask textures and sprite assets a portal needs, and the importer settings for them.
    /// </summary>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        private static GeneratedPortalOutlineSpriteAssets EnsureGeneratedPortalOutlineMaskAssets(
            DimensionRuntimePortalOutput portalOutput,
            string portalFolder,
            string modRoot,
            string sourceBodyTexturePath)
        {
            string assetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineMask",
                "DimensionPortalOutlineMask");
            string supportAssetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineSupportMask",
                "DimensionPortalOutlineSupportMask");
            string capAssetName = SanitizeAssetFileName(
                portalOutput.AssetStem + "PortalOutlineCap",
                "DimensionPortalOutlineCap");
            string texturePath = portalFolder + "/" + assetName + ".png";
            string supportTexturePath = portalFolder + "/" + supportAssetName + ".png";
            string capTexturePath = portalFolder + "/" + capAssetName + ".png";
            string spriteAssetPath = portalFolder + "/" + assetName + ".asset";
            string supportSpriteAssetPath = portalFolder + "/" + supportAssetName + ".asset";
            string capSpriteAssetPath = portalFolder + "/" + capAssetName + ".asset";
            string addressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-mask";
            string supportAddressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-support-mask";
            string capAddressSeed =
                portalOutput.PortalObjectName + ":generated-portal-outline-cap";
            long addressLow = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x706F7274616C6F75UL);
            long addressHigh = DimensionSpriteAssetAddress.Part(
                addressSeed,
                0x746C696E656D6173UL);
            long supportAddressLow = DimensionSpriteAssetAddress.Part(
                supportAddressSeed,
                0x737570706F72746DUL);
            long supportAddressHigh = DimensionSpriteAssetAddress.Part(
                supportAddressSeed,
                0x61736B706F727461UL);
            long capAddressLow = DimensionSpriteAssetAddress.Part(
                capAddressSeed,
                0x6361706F75746C69UL);
            long capAddressHigh = DimensionSpriteAssetAddress.Part(
                capAddressSeed,
                0x6E656361706F7274UL);

            EnsureGeneratedPortalOutlineMaskTextures(
                sourceBodyTexturePath,
                texturePath,
                supportTexturePath,
                capTexturePath);

            string textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
            if (string.IsNullOrEmpty(textureGuid))
            {
                AssetDatabase.ImportAsset(
                    texturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                textureGuid = AssetDatabase.AssetPathToGUID(texturePath);
            }

            if (string.IsNullOrEmpty(textureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline mask texture at " +
                    texturePath +
                    ".");
            }

            string supportTextureGuid = AssetDatabase.AssetPathToGUID(supportTexturePath);
            if (string.IsNullOrEmpty(supportTextureGuid))
            {
                AssetDatabase.ImportAsset(
                    supportTexturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                supportTextureGuid = AssetDatabase.AssetPathToGUID(supportTexturePath);
            }

            if (string.IsNullOrEmpty(supportTextureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline support mask texture at " +
                    supportTexturePath +
                    ".");
            }

            string capTextureGuid = AssetDatabase.AssetPathToGUID(capTexturePath);
            if (string.IsNullOrEmpty(capTextureGuid))
            {
                AssetDatabase.ImportAsset(
                    capTexturePath,
                    ImportAssetOptions.ForceSynchronousImport);
                capTextureGuid = AssetDatabase.AssetPathToGUID(capTexturePath);
            }

            if (string.IsNullOrEmpty(capTextureGuid))
            {
                throw new System.InvalidOperationException(
                    "Could not create generated portal outline cap texture at " +
                    capTexturePath +
                    ".");
            }

            string spriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                assetName,
                addressLow,
                addressHigh,
                textureGuid);
            WriteTextAssetIfChanged(spriteAssetPath, spriteAssetYaml);
            EnsureSpriteAssetImported(spriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, spriteAssetPath);

            string supportSpriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                supportAssetName,
                supportAddressLow,
                supportAddressHigh,
                supportTextureGuid);
            WriteTextAssetIfChanged(supportSpriteAssetPath, supportSpriteAssetYaml);
            EnsureSpriteAssetImported(supportSpriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, supportSpriteAssetPath);

            string capSpriteAssetYaml = BuildPortalOutlineMaskSpriteAssetYaml(
                capAssetName,
                capAddressLow,
                capAddressHigh,
                capTextureGuid);
            WriteTextAssetIfChanged(capSpriteAssetPath, capSpriteAssetYaml);
            EnsureSpriteAssetImported(capSpriteAssetPath);
            EnsureSpriteAssetManifestContains(modRoot, capSpriteAssetPath);

            return new GeneratedPortalOutlineSpriteAssets
            {
                Main = new GeneratedPortalSpriteAsset
                {
                    AddressLow = addressLow,
                    AddressHigh = addressHigh
                },
                Support = new GeneratedPortalSpriteAsset
                {
                    AddressLow = supportAddressLow,
                    AddressHigh = supportAddressHigh
                },
                Cap = new GeneratedPortalSpriteAsset
                {
                    AddressLow = capAddressLow,
                    AddressHigh = capAddressHigh
                }
            };
        }

        private static void EnsureGeneratedPortalOutlineMaskTextures(
            string sourceBodyTexturePath,
            string targetMaskTexturePath,
            string targetSupportMaskTexturePath,
            string targetCapTexturePath)
        {
            string sourceAbsolutePath = AssetPathToAbsolutePath(sourceBodyTexturePath);
            if (string.IsNullOrEmpty(sourceAbsolutePath) || !File.Exists(sourceAbsolutePath))
            {
                throw new System.InvalidOperationException(
                    "Could not read the source portal body texture at " +
                    sourceBodyTexturePath +
                    ".");
            }

            Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D maskTexture = null;
            Texture2D supportMaskTexture = null;
            Texture2D capTexture = null;
            try
            {
                if (!sourceTexture.LoadImage(File.ReadAllBytes(sourceAbsolutePath)))
                {
                    throw new System.InvalidOperationException(
                        "Unity could not decode the source portal body texture at " +
                        sourceBodyTexturePath +
                        ".");
                }

                int sourceWidth = sourceTexture.width;
                int sourceHeight = sourceTexture.height;
                int maskWidth = sourceWidth;
                int maskHeight = sourceHeight;
                Color32[] sourcePixels = sourceTexture.GetPixels32();
                Color32[] maskPixels = new Color32[maskWidth * maskHeight];
                Color32[] supportMaskPixels = new Color32[maskWidth * maskHeight];
                Color32[] capPixels = new Color32[maskWidth * maskHeight];
                Color32 opaqueMaskPixel = new Color32(255, 255, 255, 255);
                Color32 transparentMaskPixel = new Color32(255, 255, 255, 0);
                bool[] bodyPixels = new bool[maskWidth * maskHeight];
                bool[] desiredEdgePixels = new bool[maskWidth * maskHeight];
                bool[] mainMaskPixels = new bool[maskWidth * maskHeight];
                bool[] mainOutlinePixels = new bool[maskWidth * maskHeight];
                bool[] supportPixels = new bool[maskWidth * maskHeight];
                bool[] supportOutlinePixels = new bool[maskWidth * maskHeight];

                for (int i = 0; i < maskPixels.Length; i++)
                {
                    maskPixels[i] = transparentMaskPixel;
                    supportMaskPixels[i] = transparentMaskPixel;
                    capPixels[i] = transparentMaskPixel;
                }

                for (int y = 0; y < sourceHeight; y++)
                {
                    for (int x = 0; x < sourceWidth; x++)
                    {
                        int index = y * maskWidth + x;
                        bool bodyPixel =
                            IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y);
                        bodyPixels[index] = bodyPixel;
                        if (!bodyPixel)
                        {
                            continue;
                        }

                        bool desiredEdge =
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x - 1, y) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x + 1, y) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y - 1) ||
                            !IsOpaquePortalPixel(sourcePixels, sourceWidth, sourceHeight, x, y + 1);
                        desiredEdgePixels[index] = desiredEdge;
                        mainMaskPixels[index] = !desiredEdge;
                        if (!desiredEdge)
                        {
                            maskPixels[index] = opaqueMaskPixel;
                        }
                    }
                }

                BuildSpriteObjectOutlinePixels(
                    mainMaskPixels,
                    bodyPixels,
                    maskWidth,
                    maskHeight,
                    mainOutlinePixels);
                AddOutlineSupportPixels(
                    bodyPixels,
                    desiredEdgePixels,
                    mainOutlinePixels,
                    supportPixels,
                    maskWidth,
                    maskHeight);
                BuildSpriteObjectOutlinePixels(
                    supportPixels,
                    bodyPixels,
                    maskWidth,
                    maskHeight,
                    supportOutlinePixels);

                for (int i = 0; i < supportPixels.Length; i++)
                {
                    if (supportPixels[i])
                    {
                        supportMaskPixels[i] = opaqueMaskPixel;
                    }
                }

                for (int i = 0; i < capPixels.Length; i++)
                {
                    if (desiredEdgePixels[i] &&
                        !mainOutlinePixels[i] &&
                        !supportOutlinePixels[i])
                    {
                        capPixels[i] = opaqueMaskPixel;
                    }
                }

                maskTexture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false);
                maskTexture.SetPixels32(maskPixels);
                maskTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetMaskTexturePath, maskTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetMaskTexturePath,
                    maskWidth,
                    maskHeight);

                supportMaskTexture = new Texture2D(
                    maskWidth,
                    maskHeight,
                    TextureFormat.RGBA32,
                    false);
                supportMaskTexture.SetPixels32(supportMaskPixels);
                supportMaskTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(
                    targetSupportMaskTexturePath,
                    supportMaskTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetSupportMaskTexturePath,
                    maskWidth,
                    maskHeight);

                capTexture = new Texture2D(maskWidth, maskHeight, TextureFormat.RGBA32, false);
                capTexture.SetPixels32(capPixels);
                capTexture.Apply(false, false);
                WriteBinaryAssetIfChanged(targetCapTexturePath, capTexture.EncodeToPNG());
                ConfigureGeneratedSpriteTextureImporter(
                    targetCapTexturePath,
                    maskWidth,
                    maskHeight);
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
                if (maskTexture != null)
                {
                    Object.DestroyImmediate(maskTexture);
                }

                if (supportMaskTexture != null)
                {
                    Object.DestroyImmediate(supportMaskTexture);
                }

                if (capTexture != null)
                {
                    Object.DestroyImmediate(capTexture);
                }
            }
        }

        private static void BuildSpriteObjectOutlinePixels(
            bool[] maskPixels,
            bool[] bodyPixels,
            int width,
            int height,
            bool[] outlinePixels)
        {
            for (int i = 0; i < outlinePixels.Length; i++)
            {
                outlinePixels[i] = false;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!bodyPixels[index] || maskPixels[index])
                    {
                        continue;
                    }

                    outlinePixels[index] =
                        IsMaskPixelSet(maskPixels, width, height, x - 1, y) ||
                        IsMaskPixelSet(maskPixels, width, height, x + 1, y) ||
                        IsMaskPixelSet(maskPixels, width, height, x, y - 1) ||
                        IsMaskPixelSet(maskPixels, width, height, x, y + 1);
                }
            }
        }

        private static void AddOutlineSupportPixels(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] mainOutlinePixels,
            bool[] supportPixels,
            int width,
            int height)
        {
            bool[] supportOutlinePixels = new bool[bodyPixels.Length];
            bool[] coveredPixels = new bool[bodyPixels.Length];
            for (int i = 0; i < coveredPixels.Length; i++)
            {
                coveredPixels[i] = mainOutlinePixels[i];
            }

            bool addedPixel;
            do
            {
                addedPixel = false;
                BuildSpriteObjectOutlinePixels(
                    supportPixels,
                    bodyPixels,
                    width,
                    height,
                    supportOutlinePixels);
                for (int i = 0; i < coveredPixels.Length; i++)
                {
                    coveredPixels[i] = mainOutlinePixels[i] || supportOutlinePixels[i];
                }

                for (int y = 0; y < height && !addedPixel; y++)
                {
                    for (int x = 0; x < width && !addedPixel; x++)
                    {
                        int index = y * width + x;
                        if (!desiredEdgePixels[index] || coveredPixels[index])
                        {
                            continue;
                        }

                        int supportIndex;
                        if (TryFindOutlineSupportPixel(
                            bodyPixels,
                            desiredEdgePixels,
                            coveredPixels,
                            supportPixels,
                            width,
                            height,
                            x,
                            y,
                            out supportIndex))
                        {
                            supportPixels[supportIndex] = true;
                            addedPixel = true;
                        }
                    }
                }
            }
            while (addedPixel);
        }

        private static bool TryFindOutlineSupportPixel(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] coveredPixels,
            bool[] supportPixels,
            int width,
            int height,
            int x,
            int y,
            out int supportIndex)
        {
            int index;
            if (TryGetSafeOutlineSupportPixel(
                bodyPixels,
                desiredEdgePixels,
                coveredPixels,
                supportPixels,
                width,
                height,
                x - 1,
                y,
                out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x + 1,
                    y,
                    out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x,
                    y - 1,
                    out index) ||
                TryGetSafeOutlineSupportPixel(
                    bodyPixels,
                    desiredEdgePixels,
                    coveredPixels,
                    supportPixels,
                    width,
                    height,
                    x,
                    y + 1,
                    out index))
            {
                supportIndex = index;
                return true;
            }

            supportIndex = -1;
            return false;
        }

        private static bool TryGetSafeOutlineSupportPixel(
            bool[] bodyPixels,
            bool[] desiredEdgePixels,
            bool[] coveredPixels,
            bool[] supportPixels,
            int width,
            int height,
            int x,
            int y,
            out int index)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                index = -1;
                return false;
            }

            index = y * width + x;
            if (!bodyPixels[index] || supportPixels[index])
            {
                return false;
            }

            bool addsUsefulPixel = false;
            for (int direction = 0; direction < 4; direction++)
            {
                int neighborX = x;
                int neighborY = y;
                switch (direction)
                {
                    case 0:
                        neighborX--;
                        break;
                    case 1:
                        neighborX++;
                        break;
                    case 2:
                        neighborY--;
                        break;
                    default:
                        neighborY++;
                        break;
                }

                if (IsMaskPixelSet(supportPixels, width, height, neighborX, neighborY))
                {
                    continue;
                }

                if (neighborX < 0 ||
                    neighborY < 0 ||
                    neighborX >= width ||
                    neighborY >= height)
                {
                    return false;
                }

                int neighborIndex = neighborY * width + neighborX;
                if (!bodyPixels[neighborIndex] || !desiredEdgePixels[neighborIndex])
                {
                    return false;
                }

                if (!coveredPixels[neighborIndex])
                {
                    addsUsefulPixel = true;
                }
            }

            return addsUsefulPixel;
        }

        private static bool IsMaskPixelSet(
            bool[] pixels,
            int width,
            int height,
            int x,
            int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x < width &&
                   y < height &&
                   pixels[y * width + x];
        }

        private static bool IsOpaquePortalPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y)
        {
            return x >= 0 &&
                   y >= 0 &&
                   x < width &&
                   y < height &&
                   pixels[y * width + x].a > 127;
        }

        private static void ConfigureGeneratedSpriteTextureImporter(
            string textureAssetPath,
            int sourceWidth,
            int sourceHeight)
        {
            TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(
                    textureAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new System.InvalidOperationException(
                        "Could not configure the generated sprite texture importer at " +
                        textureAssetPath + ".");
                }
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16.0f))
            {
                importer.spritePixelsPerUnit = 16.0f;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            int largestSourceDimension = Mathf.Max(sourceWidth, sourceHeight);
            if (largestSourceDimension > 0)
            {
                int requiredMaximumSize = Mathf.NextPowerOfTwo(largestSourceDimension);
                if (requiredMaximumSize > importer.maxTextureSize)
                {
                    importer.maxTextureSize = requiredMaximumSize;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static string BuildPortalOutlineMaskSpriteAssetYaml(
            string assetName,
            long addressLow,
            long addressHigh,
            string textureGuid)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("%YAML 1.1");
            builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            builder.AppendLine("--- !u!114 &11400000");
            builder.AppendLine("MonoBehaviour:");
            builder.AppendLine("  m_ObjectHideFlags: 0");
            builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
            builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
            builder.AppendLine("  m_GameObject: {fileID: 0}");
            builder.AppendLine("  m_Enabled: 1");
            builder.AppendLine("  m_EditorHideFlags: 0");
            builder.AppendLine("  m_Script: {fileID: -217761678, guid: 292700ef68995bdb2163e35989fc7eb0, type: 3}");
            builder.Append("  m_Name: ").AppendLine(ToUnityYamlString(assetName));
            builder.AppendLine("  m_EditorClassIdentifier: ");
            builder.AppendLine("  m_overload:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_address:");
            builder.Append("    m_low: ").AppendLine(addressLow.ToString(CultureInfo.InvariantCulture));
            builder.Append("    m_high: ").AppendLine(addressHigh.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  m_dynamicCollections:");
            builder.AppendLine("    m_list: []");
            builder.AppendLine("  m_defaultPrimaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultSecondaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultTertiaryGradientMap: {fileID: 0}");
            builder.AppendLine("  m_defaultPrimaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_defaultSecondaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_defaultTertiaryGradientMapRef:");
            builder.AppendLine("    m_address:");
            builder.AppendLine("      m_low: 0");
            builder.AppendLine("      m_high: 0");
            builder.AppendLine("  m_editorHideEmissive: 0");
            builder.AppendLine("  m_staticSpriteData:");
            builder.Append("    texture: {fileID: 2800000, guid: ")
                .Append(textureGuid)
                .AppendLine(", type: 3}");
            builder.AppendLine("    emissiveTexture: {fileID: 0}");
            builder.AppendLine("    normalTexture: {fileID: 0}");
            builder.AppendLine("    pivot: {x: 0.5, y: 0.5}");
            builder.AppendLine("    positionalData: []");
            builder.AppendLine("    inheritPivot: 1");
            builder.AppendLine("  m_staticVariants: []");
            builder.AppendLine("  m_animations: []");
            builder.AppendLine("  references:");
            builder.AppendLine("    version: 2");
            builder.AppendLine("    RefIds: []");
            return builder.ToString();
        }

        private static void EnsureSpriteAssetManifestContains(
            string modRoot,
            string spriteAssetPath)
        {
            string normalizedRoot = NormalizeAssetPath(modRoot);
            string normalizedSpriteAssetPath = NormalizeAssetPath(spriteAssetPath);
            if (string.IsNullOrEmpty(normalizedRoot) ||
                string.IsNullOrEmpty(normalizedSpriteAssetPath))
            {
                return;
            }

            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedSpriteAssetPath);
            if (spriteAsset == null)
            {
                throw new System.InvalidOperationException(
                    "Could not load the generated SpriteAsset at " +
                    normalizedSpriteAssetPath + ".");
            }

            string manifestPath = normalizedRoot + "/SpriteAssetManifest.asset";
            string absoluteManifestPath = AssetPathToAbsolutePath(manifestPath);
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                if (!string.IsNullOrEmpty(absoluteManifestPath) &&
                    File.Exists(absoluteManifestPath))
                {
                    throw new System.InvalidOperationException(
                        "The SpriteAsset manifest at " + manifestPath +
                        " exists but Unity could not load it as a SpriteAssetManifest.");
                }

                manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                manifest.name = "SpriteAssetManifest";
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            bool changed = false;
            bool alreadyRegistered = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase registeredAsset = manifest.spriteAssets[i];
                if (registeredAsset == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
                else if (registeredAsset == spriteAsset)
                {
                    alreadyRegistered = true;
                }
            }

            if (!alreadyRegistered)
            {
                manifest.spriteAssets.Add(spriteAsset);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
        }

        private static void RemoveSpriteAssetManifestReference(
            string modRoot,
            SpriteAssetBase spriteAsset)
        {
            string normalizedRoot = NormalizeAssetPath(modRoot);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                return;
            }

            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                    normalizedRoot + "/SpriteAssetManifest.asset");
            if (manifest == null || manifest.spriteAssets == null)
            {
                return;
            }

            bool changed = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase registeredAsset = manifest.spriteAssets[i];
                if (registeredAsset == null || registeredAsset == spriteAsset)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
        }

        private static void EnsureSpriteAssetImported(string spriteAssetPath)
        {
            string normalizedPath = NormalizeAssetPath(spriteAssetPath);
            if (string.IsNullOrEmpty(normalizedPath) ||
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedPath) != null)
            {
                return;
            }

            AssetDatabase.ImportAsset(
                normalizedPath,
                ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(normalizedPath) == null)
            {
                throw new System.InvalidOperationException(
                    "Unity could not import the generated SpriteAsset at " +
                    normalizedPath + ".");
            }
        }
    }
}
