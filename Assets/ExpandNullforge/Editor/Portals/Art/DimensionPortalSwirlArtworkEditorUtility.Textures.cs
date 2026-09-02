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
    /// The one texture slot a swirl has, and whether what was chosen fits it.
    /// </summary>
    internal static partial class DimensionPortalSwirlArtworkEditorUtility
    {
        /// <summary>
        /// Resolves the profile's current custom Swirls reference and reads animation zero.
        /// An empty reference uses the framework starter; a non-zero unresolved address is
        /// surfaced as an error rather than silently replacing author intent.
        /// </summary>
        public static bool TryGetAnimationZeroTextureSlot(
            DimensionPortalVisualProfileAsset profile,
            out AnimationZeroTextureSlot slot,
            out string message)
        {
            slot = null;
            message = string.Empty;
            string packageFolder = string.Empty;
            if (profile != null)
            {
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out _,
                    out packageFolder);
            }

            SpriteAsset selected = null;
            if (profile != null)
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.Update();
                SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
                if (reference == null)
                {
                    message = "The portal profile no longer contains the custom Swirls reference.";
                    return false;
                }

                if (HasAddress(reference) &&
                    !TryResolveReference(profile, reference, packageFolder, out selected))
                {
                    message =
                        "The selected Swirls SpriteAsset address could not be resolved in Scriptable Data.";
                    return false;
                }
            }

            if (selected == null)
            {
                selected = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath);
            }

            if (selected == null)
            {
                message = "The framework Swirls starter SpriteAsset could not be loaded.";
                return false;
            }

            return TryBuildAnimationZeroTextureSlot(
                selected,
                packageFolder,
                out slot,
                out message);
        }

        private static bool TryResolveManagedAssetForTextureEdit(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            string packageFolder,
            string expectedFolder,
            out SpriteAsset managed,
            out string message)
        {
            managed = null;
            message = string.Empty;
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (reference == null)
            {
                message = "The portal profile no longer contains the custom Swirls reference.";
                return false;
            }

            if (!TryResolveReference(
                    profile,
                    reference,
                    packageFolder,
                    out SpriteAsset selected))
            {
                message =
                    "The selected Swirls SpriteAsset could not be resolved in Scriptable Data.";
                return false;
            }

            string selectedPath = selected == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            if (selected == null ||
                IsFrameworkAsset(selected) ||
                !AssetPathIsWithin(selectedPath, expectedFolder))
            {
                if (!CreateEditableCopy(template, profile, out message))
                {
                    return false;
                }

                serialized = new SerializedObject(profile);
                serialized.Update();
                reference = serialized.FindProperty(ReferencePropertyName);
                if (reference == null ||
                    !TryResolveReference(profile, reference, packageFolder, out selected) ||
                    selected == null)
                {
                    message =
                        "The newly-created Swirls SpriteAsset could not be resolved from its portal package.";
                    return false;
                }

                selectedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            }

            if (IsFrameworkAsset(selected) ||
                string.IsNullOrEmpty(selectedPath) ||
                !AssetPathIsWithin(selectedPath, expectedFolder))
            {
                message =
                    "Swirls textures may only update the selected SpriteAsset inside this portal's Artwork/Swirls folder.";
                return false;
            }

            managed = selected;
            return true;
        }

        private static bool TryBuildAnimationZeroTextureSlot(
            SpriteAsset asset,
            string packageFolder,
            out AnimationZeroTextureSlot slot,
            out string message)
        {
            slot = null;
            message = string.Empty;
            if (asset == null)
            {
                message = "The Swirls SpriteAsset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            if (!TryGetAnimationZeroSpriteData(
                    serialized,
                    out SerializedProperty animation,
                    out SerializedProperty spriteData,
                    out message))
            {
                return false;
            }

            SerializedProperty frameCountProperty =
                animation.FindPropertyRelative("srcFrameCount");
            int frameCount = frameCountProperty == null
                ? 0
                : frameCountProperty.intValue;
            if (frameCount <= 0)
            {
                message = "Swirls animation zero has no source frames.";
                return false;
            }

            SerializedProperty loopProperty = animation.FindPropertyRelative("loop");
            if (loopProperty == null || !loopProperty.boolValue)
            {
                message = "Swirls animation zero must loop.";
                return false;
            }

            Texture2D color = GetTexture(spriteData, "texture");
            Texture2D emissive = GetTexture(spriteData, "emissiveTexture");
            Texture2D normal = GetTexture(spriteData, "normalTexture");
            if (color == null || color.width % frameCount != 0)
            {
                message =
                    "Swirls animation zero must have an evenly-divided color sheet.";
                return false;
            }

            int frameWidth = color.width / frameCount;
            if (frameWidth <= 0 ||
                frameWidth > NativeFrameSize ||
                color.height > NativeFrameSize)
            {
                message = "Swirls animation zero frames must fit within the " +
                          NativeFrameSize + " x " + NativeFrameSize +
                          " portal canvas. Found " + frameWidth + " x " + color.height + ".";
                return false;
            }

            if (!ChannelsMatch(color, emissive) || !ChannelsMatch(color, normal))
            {
                message =
                    "Swirls animation zero color, emissive, and normal sheets must have matching dimensions.";
                return false;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            string expectedFolder = string.IsNullOrEmpty(packageFolder)
                ? string.Empty
                : NormalizeAssetPath(packageFolder + "/" + PackageRelativeFolder);
            slot = new AnimationZeroTextureSlot
            {
                SpriteAsset = asset,
                ColorTexture = color,
                EmissiveTexture = emissive,
                NormalTexture = normal,
                FrameCount = frameCount,
                SheetWidth = color.width,
                SheetHeight = color.height,
                FrameWidth = frameWidth,
                FrameHeight = color.height,
                IsFramework = IsFrameworkAsset(asset),
                IsManaged = !string.IsNullOrEmpty(expectedFolder) &&
                            AssetPathIsWithin(assetPath, expectedFolder)
            };
            return true;
        }

        private static bool TryGetAnimationZeroSpriteData(
            SerializedObject serialized,
            out SerializedProperty animation,
            out SerializedProperty spriteData,
            out string message)
        {
            animation = null;
            spriteData = null;
            message = string.Empty;
            SerializedProperty animations = serialized == null
                ? null
                : serialized.FindProperty("m_animations");
            if (animations == null || !animations.isArray || animations.arraySize == 0)
            {
                message = "The Swirls SpriteAsset must contain animation zero.";
                return false;
            }

            animation = animations.GetArrayElementAtIndex(0);
            spriteData = animation == null
                ? null
                : animation.FindPropertyRelative("m_spriteData");
            if (animation == null || spriteData == null)
            {
                message = "Swirls animation zero has no SpriteData.";
                return false;
            }

            return true;
        }

        private static bool TryValidateSelectedTexture(
            Texture2D texture,
            string label,
            int requiredWidth,
            int requiredHeight,
            bool optional,
            out string message)
        {
            message = string.Empty;
            if (texture == null)
            {
                if (optional)
                {
                    return true;
                }

                message = label + " is required.";
                return false;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                message = label +
                          " must be a saved PNG inside the Unity Assets folder.";
                return false;
            }

            string absolutePath =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(path);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                message = label + " bytes could not be read from " + path + ".";
                return false;
            }

            if (texture.width != requiredWidth || texture.height != requiredHeight)
            {
                message = label + " must be " + requiredWidth + " x " +
                          requiredHeight + " pixels. Selected: " + texture.width + " x " +
                          texture.height + ".";
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.filterMode != FilterMode.Point)
            {
                message = label +
                          " must use Point filtering so its portal pixels remain exact.";
                return false;
            }

            return true;
        }

        private static bool TryMaterializeSelectedTexture(
            Texture2D selected,
            Texture2D current,
            string destinationFolder,
            string fileStem,
            string suffix,
            int requiredWidth,
            int requiredHeight,
            bool normalMap,
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction,
            out Texture2D managedTexture,
            out string message)
        {
            managedTexture = null;
            message = string.Empty;
            if (selected == null)
            {
                return true;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            if (AssetPathIsWithin(sourcePath, destinationFolder))
            {
                // The package owns this texture, so normalize its importer in place.
                // This is particularly important for Normal, where merely assigning a
                // point-filtered PNG does not establish Unity's normal-map semantics.
                DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                    sourcePath,
                    requiredWidth,
                    requiredHeight,
                    normalMap);
                managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (managedTexture == null)
                {
                    message = "The package-owned Swirls texture could not be reloaded from " +
                              sourcePath + ".";
                    return false;
                }

                return true;
            }

            string targetBaseName = fileStem + suffix;
            string currentPath = current == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(current));
            string targetPath = IsEditableAnimationZeroTexture(
                    currentPath,
                    destinationFolder,
                    targetBaseName)
                ? currentPath
                : NormalizeAssetPath(
                    AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + targetBaseName + ".png"));
            string absoluteSource =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(sourcePath);
            if (transaction == null ||
                string.IsNullOrEmpty(absoluteSource) ||
                !File.Exists(absoluteSource))
            {
                message = "The selected Swirls texture bytes could not be read from " +
                          sourcePath + ".";
                return false;
            }

            transaction.ReplaceAssetBytes(targetPath, File.ReadAllBytes(absoluteSource));
            DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                targetPath,
                requiredWidth,
                requiredHeight,
                normalMap);
            managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (managedTexture == null ||
                managedTexture.width != requiredWidth ||
                managedTexture.height != requiredHeight)
            {
                message = "The managed Swirls texture could not be imported at " +
                          targetPath + ".";
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
            if (importer == null ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled)
            {
                message = "The managed Swirls texture did not retain its point-sampled import contract.";
                return false;
            }

            return true;
        }

        private static bool IsEditableAnimationZeroTexture(
            string assetPath,
            string destinationFolder,
            string expectedBaseName)
        {
            if (!AssetPathIsWithin(assetPath, destinationFolder) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return string.Equals(
                       fileName,
                       expectedBaseName,
                       StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith(
                       expectedBaseName + " ",
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool CreateEditableCopy(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (template == null || profile == null)
            {
                message = "The active Dimension Asset and portal profile are required.";
                return false;
            }

            if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out message))
            {
                return false;
            }

            if (!DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out _,
                    out string packageFolder) ||
                string.IsNullOrEmpty(packageFolder))
            {
                message =
                    "Save this portal as a managed profile before creating editable Swirls artwork.";
                return false;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string modRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                    templatePath));
            if (string.IsNullOrEmpty(modRoot) ||
                !AssetPathIsWithin(packageFolder, modRoot))
            {
                message =
                    "The portal profile package does not belong to the selected Dimension Asset's mod.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (!TryResolveReference(
                    profile,
                    reference,
                    packageFolder,
                    out SpriteAsset sourceAsset))
            {
                message =
                    "The selected Swirls SpriteAsset could not be resolved in Scriptable Data.";
                return false;
            }

            if (sourceAsset == null)
            {
                sourceAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath);
            }

            if (!TryValidateAnimationContract(
                    sourceAsset,
                    true,
                    out _,
                    out message))
            {
                message = "The selected Swirls artwork cannot be copied. " + message;
                return false;
            }

            string destinationFolder = NormalizeAssetPath(
                packageFolder + "/" + PackageRelativeFolder);
            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    destinationFolder,
                    out message))
            {
                return false;
            }

            if (!TryCloneGraph(
                    sourceAsset,
                    profile,
                    destinationFolder,
                    modRoot,
                    out message))
            {
                return false;
            }

            message =
                "Created and selected an editable Swirls SpriteAsset inside this portal profile.";
            return true;
        }

        public static bool TryValidateAnimationContract(
            SpriteAsset asset,
            bool requirePackageFrameSize,
            out List<TextureDependency> dependencies,
            out string message)
        {
            dependencies = new List<TextureDependency>();
            message = string.Empty;
            if (asset == null)
            {
                message = "The Swirls SpriteAsset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty animations = serialized.FindProperty("m_animations");
            if (animations == null || !animations.isArray || animations.arraySize == 0)
            {
                message = "The Swirls SpriteAsset must contain animation 0.";
                return false;
            }

            HashSet<Texture2D> seen = new HashSet<Texture2D>();
            for (int i = 0; i < animations.arraySize; i++)
            {
                SerializedProperty animation = animations.GetArrayElementAtIndex(i);
                SerializedProperty spriteData = animation.FindPropertyRelative("m_spriteData");
                SerializedProperty frameCountProperty =
                    animation.FindPropertyRelative("srcFrameCount");
                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                if (frameCount <= 0)
                {
                    message = "Swirls animation " + i + " has no source frames.";
                    return false;
                }

                Texture2D color = GetTexture(spriteData, "texture");
                Texture2D emissive = GetTexture(spriteData, "emissiveTexture");
                Texture2D normal = GetTexture(spriteData, "normalTexture");
                if (color == null)
                {
                    message = "Swirls animation " + i + " has no color sheet.";
                    return false;
                }

                if (color.width % frameCount != 0)
                {
                    message = "Swirls animation " + i +
                              " is not evenly divided into its " + frameCount + " frames.";
                    return false;
                }

                int frameWidth = color.width / frameCount;
                if (requirePackageFrameSize &&
                    (frameWidth <= 0 ||
                     frameWidth > NativeFrameSize ||
                     color.height > NativeFrameSize))
                {
                    message = "Swirls animation " + i + " frames must fit within the " +
                              NativeFrameSize + " x " + NativeFrameSize +
                              " portal canvas. Found " + frameWidth + " x " +
                              color.height + ".";
                    return false;
                }

                if (!ChannelsMatch(color, emissive) || !ChannelsMatch(color, normal))
                {
                    message = "Swirls animation " + i +
                              " color, emissive, and normal sheets must have matching dimensions.";
                    return false;
                }

                AddDependency(dependencies, seen, "Animation[" + i + "].Color", color);
                AddDependency(dependencies, seen, "Animation[" + i + "].Emissive", emissive);
                AddDependency(dependencies, seen, "Animation[" + i + "].Normal", normal);
            }

            // Preserve any authored static fallback data too, even though custom Swirls
            // render animation 0. This keeps duplication graph-complete.
            SerializedProperty staticData = serialized.FindProperty("m_staticSpriteData");
            AddDependency(
                dependencies,
                seen,
                "Static.Color",
                GetTexture(staticData, "texture"));
            AddDependency(
                dependencies,
                seen,
                "Static.Emissive",
                GetTexture(staticData, "emissiveTexture"));
            AddDependency(
                dependencies,
                seen,
                "Static.Normal",
                GetTexture(staticData, "normalTexture"));
            return true;
        }
    }
}
