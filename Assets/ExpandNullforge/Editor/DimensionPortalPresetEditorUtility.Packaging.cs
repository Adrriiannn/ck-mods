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
    /// Copying everything a preset points at into its own folder, and putting it back on failure.
    /// </summary>
    internal static partial class DimensionPortalPresetEditorUtility
    {
        private static bool TryCloneCompleteArtwork(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            out string message)
        {
            message = string.Empty;
            if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out string contextError))
            {
                message = contextError;
                return false;
            }

            SerializedObject serializedSource = new SerializedObject(source);
            for (int i = 0; i < ArtworkLayers.Length; i++)
            {
                serializedSource.Update();
                DimensionPortalArtworkLayer layer = ArtworkLayers[i];
                SerializedProperty reference = serializedSource.FindProperty(
                    DimensionPortalPackageEditorUtility.GetReferencePropertyName(layer));
                DimensionPortalArtworkReferenceKind kind =
                    DimensionPortalArtworkEditorUtility.ClassifyReference(
                        reference,
                        source,
                        layer,
                        out SpriteAsset sourceAsset);
                if (kind == DimensionPortalArtworkReferenceKind.Unresolved)
                {
                    message = "Could not resolve the source " + GetLayerDisplayName(layer) +
                              " SpriteAsset in Scriptable Data, so a complete private " +
                              "preset could not be guaranteed.";
                    return false;
                }

                bool cloned;
                string cloneMessage;
                // Frame has no semantic source palette. Even older managed frame copies can
                // lack the directTextureOverride metadata bit while already pointing at exact
                // custom color/emissive sheets; cloning them from the vanilla baseline is the
                // data-loss bug this package format is designed to prevent.
                bool exactTextures =
                    layer == DimensionPortalArtworkLayer.Frame ||
                    kind == DimensionPortalArtworkReferenceKind.External ||
                    (kind == DimensionPortalArtworkReferenceKind.Managed &&
                     DimensionPortalArtworkEditorUtility.UsesDirectTextureOverride(
                         source,
                         layer));
                if (exactTextures)
                {
                    if (!DimensionPortalArtworkEditorUtility.TryGetTextureSlots(
                        source,
                        layer,
                        out DimensionPortalArtworkEditorUtility.TextureSlot[] slots,
                        out cloneMessage))
                    {
                        message = "Could not read the exact " + GetLayerDisplayName(layer) +
                                  " texture sheets from the source preset. " + cloneMessage;
                        return false;
                    }

                    Texture2D[] textures = new Texture2D[slots.Length];
                    Texture2D[] emissiveTextures = new Texture2D[slots.Length];
                    Texture2D[] normalTextures = new Texture2D[slots.Length];
                    for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                    {
                        textures[slotIndex] = slots[slotIndex].Texture;
                        emissiveTextures[slotIndex] = slots[slotIndex].EmissiveTexture;
                        normalTextures[slotIndex] = slots[slotIndex].NormalTexture;
                    }

                    cloned = DimensionPortalArtworkEditorUtility.CreateOrUpdateLayerTextures(
                        template,
                        target,
                        layer,
                        textures,
                        emissiveTextures,
                        normalTextures,
                        out cloneMessage);
                }
                else
                {
                    // Empty/framework references and semantic-palette derivatives still get
                    // a private SpriteAsset. CreateEditableCopy recreates their exact visible
                    // palette while retaining future semantic color editing in this package.
                    cloned = DimensionPortalArtworkEditorUtility.CreateEditableCopy(
                        template,
                        target,
                        layer,
                        out cloneMessage);
                }

                if (!cloned)
                {
                    message = "Could not materialize " + GetLayerDisplayName(layer) +
                              " artwork inside the new portal package. " + cloneMessage;
                    return false;
                }
            }

            return true;
        }

        private static bool TryLocalizeOptionalDependencies(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            out string message)
        {
            message = string.Empty;
            if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out message))
            {
                return false;
            }

            string modRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                    packageFolder));
            if (string.IsNullOrEmpty(modRoot) ||
                string.Equals(
                    modRoot,
                    "Assets/ExpandNullforge",
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "The portal package does not belong to a consumer mod root.";
                return false;
            }

            Dictionary<string, string> localizedSpriteAssetPaths =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return TryCopyOptionalDependency(
                       source,
                       target,
                       packageFolder,
                       "centerParticleTexture",
                       "Artwork/Flecks",
                       "CenterParticles",
                       typeof(Texture2D),
                       out message) &&
                   TryCopyOptionalSpriteDependency(
                       source,
                       target,
                       packageFolder,
                       "centerParticleSprite",
                       "Artwork/Flecks",
                       "CenterParticleSprite",
                       localizedSpriteAssetPaths,
                       out message) &&
                   TryCopyOptionalDependency(
                       source,
                       target,
                       packageFolder,
                       "readyFlashTexture",
                       "Artwork/ReadyBurst",
                       "ReadyFlash",
                       typeof(Texture2D),
                       out message) &&
                   TryCopyOptionalSpriteArrayDependency(
                       source,
                       target,
                       packageFolder,
                       "readyFlashSprites",
                       "Artwork/ReadyBurst",
                       "ReadyFlashSprite",
                       localizedSpriteAssetPaths,
                       out message) &&
                   TryCopyOptionalDependency(
                       source,
                       target,
                       packageFolder,
                       "portalShadowSprite",
                       "Shadow/Floor",
                       "FloorShadow",
                       typeof(Sprite),
                       out message) &&
                   TryCopyOptionalDependency(
                       source,
                       target,
                       packageFolder,
                       "portalShadowCasterSprite",
                       "Shadow/Caster",
                       "ShadowCaster",
                       typeof(Sprite),
                       out message) &&
                   DimensionPortalSwirlArtworkEditorUtility.TryLocalizeIntoPackage(
                       source,
                       target,
                       packageFolder,
                       modRoot,
                       out message);
        }

        private static bool TryCopyOptionalSpriteDependency(
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            string propertyName,
            string relativeFolder,
            string fileStem,
            Dictionary<string, string> localizedAssetPaths,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceProperty = serializedSource.FindProperty(propertyName);
            if (sourceProperty == null || sourceProperty.objectReferenceValue == null)
            {
                // A null Sprite is the exact-vanilla fallback and has no package dependency.
                return true;
            }

            Sprite sourceSprite = sourceProperty.objectReferenceValue as Sprite;
            if (sourceSprite == null)
            {
                message = "The '" + propertyName + "' dependency is not a Sprite.";
                return false;
            }

            if (!TryLocalizeSpriteDependency(
                    sourceSprite,
                    packageFolder,
                    relativeFolder,
                    fileStem,
                    localizedAssetPaths,
                    out Sprite copied,
                    out message))
            {
                return false;
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            SerializedProperty targetProperty = serializedTarget.FindProperty(propertyName);
            if (targetProperty == null)
            {
                message = "The portal profile no longer contains '" + propertyName + "'.";
                return false;
            }

            targetProperty.objectReferenceValue = copied;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return true;
        }

        private static bool TryCopyOptionalSpriteArrayDependency(
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            string propertyName,
            string relativeFolder,
            string fileStem,
            Dictionary<string, string> localizedAssetPaths,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceProperty = serializedSource.FindProperty(propertyName);
            if (sourceProperty == null)
            {
                return true;
            }

            if (!sourceProperty.isArray)
            {
                message = "The '" + propertyName + "' dependency is not a Sprite array.";
                return false;
            }

            List<Sprite> localized = new List<Sprite>(sourceProperty.arraySize);
            for (int i = 0; i < sourceProperty.arraySize; i++)
            {
                UnityEngine.Object sourceObject = sourceProperty
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue;
                if (sourceObject == null)
                {
                    localized.Add(null);
                    continue;
                }

                Sprite sourceSprite = sourceObject as Sprite;
                if (sourceSprite == null)
                {
                    message = "The '" + propertyName + "' element " + i +
                              " is not a Sprite.";
                    return false;
                }

                if (!TryLocalizeSpriteDependency(
                        sourceSprite,
                        packageFolder,
                        relativeFolder,
                        fileStem + "_" + i.ToString("D2"),
                        localizedAssetPaths,
                        out Sprite copied,
                        out message))
                {
                    return false;
                }

                localized.Add(copied);
            }

            // An empty array is the exact-vanilla fallback. Preserve it without emitting
            // assets, while still copying the shape of arrays that contain null slots.
            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            SerializedProperty targetProperty = serializedTarget.FindProperty(propertyName);
            if (targetProperty == null || !targetProperty.isArray)
            {
                message = "The portal profile no longer contains the Sprite array '" +
                          propertyName + "'.";
                return false;
            }

            targetProperty.arraySize = localized.Count;
            for (int i = 0; i < localized.Count; i++)
            {
                targetProperty.GetArrayElementAtIndex(i).objectReferenceValue = localized[i];
            }

            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return true;
        }

        private static bool TryLocalizeSpriteDependency(
            Sprite source,
            string packageFolder,
            string relativeFolder,
            string fileStem,
            Dictionary<string, string> localizedAssetPaths,
            out Sprite localized,
            out string message)
        {
            localized = null;
            message = string.Empty;
            if (source == null)
            {
                return true;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            Texture2D sourceTexture = source.texture;
            string sourceTexturePath = NormalizeAssetPath(
                sourceTexture == null ? string.Empty : AssetDatabase.GetAssetPath(sourceTexture));
            if (!IsSavedProjectAssetPath(sourcePath) ||
                sourceTexture == null ||
                !IsSavedProjectAssetPath(sourceTexturePath))
            {
                message = "The Sprite dependency '" + source.name +
                          "' and its backing texture must be saved project assets before " +
                          "they can be packaged.";
                return false;
            }

            if (AssetPathIsWithin(sourcePath, packageFolder) &&
                AssetPathIsWithin(sourceTexturePath, packageFolder))
            {
                localized = source;
                return true;
            }

            string destinationFolder = packageFolder + "/" + relativeFolder;
            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    destinationFolder,
                    out message))
            {
                return false;
            }

            if (string.Equals(
                    sourcePath,
                    sourceTexturePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!TryCopyAssetOnce(
                        sourcePath,
                        destinationFolder,
                        fileStem,
                        localizedAssetPaths,
                        out string copiedPath,
                        out message))
                {
                    return false;
                }

                localized = FindSpriteAtPath(copiedPath, source.name);
                if (localized == null)
                {
                    message = "The copied Sprite '" + source.name +
                              "' could not be resolved from '" + copiedPath + "'.";
                    return false;
                }

                return true;
            }

            if (!TryCopyAssetOnce(
                    sourceTexturePath,
                    destinationFolder,
                    fileStem + "_Source",
                    localizedAssetPaths,
                    out string copiedTexturePath,
                    out message))
            {
                return false;
            }

            Texture2D copiedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                copiedTexturePath);
            if (copiedTexture == null)
            {
                message = "The copied backing texture for Sprite '" + source.name +
                          "' could not be reloaded.";
                return false;
            }

            Rect rect = source.rect;
            if (rect.width <= 0.0f || rect.height <= 0.0f)
            {
                message = "The Sprite dependency '" + source.name +
                          "' has an invalid source rectangle.";
                return false;
            }

            Vector2 normalizedPivot = new Vector2(
                source.pivot.x / rect.width,
                source.pivot.y / rect.height);
            Sprite spriteCopy = Sprite.Create(
                copiedTexture,
                rect,
                normalizedPivot,
                source.pixelsPerUnit,
                1u,
                SpriteMeshType.FullRect,
                source.border);
            if (spriteCopy == null)
            {
                message = "Unity could not recreate the Sprite dependency '" +
                          source.name + "' from its localized texture.";
                return false;
            }

            spriteCopy.name = source.name;
            string spritePath = AssetDatabase.GenerateUniqueAssetPath(
                destinationFolder + "/" + fileStem + ".asset");
            AssetDatabase.CreateAsset(spriteCopy, spritePath);
            AssetDatabase.ImportAsset(
                spritePath,
                ImportAssetOptions.ForceSynchronousImport);
            localized = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (localized == null)
            {
                message = "The localized Sprite dependency '" + source.name +
                          "' could not be reloaded.";
                return false;
            }

            return true;
        }

        private static bool TryCopyAssetOnce(
            string sourcePath,
            string destinationFolder,
            string fileStem,
            Dictionary<string, string> localizedAssetPaths,
            out string destinationPath,
            out string message)
        {
            destinationPath = string.Empty;
            message = string.Empty;
            string normalizedSource = NormalizeAssetPath(sourcePath);
            if (localizedAssetPaths.TryGetValue(normalizedSource, out destinationPath) &&
                !string.IsNullOrEmpty(destinationPath))
            {
                return true;
            }

            string extension = Path.GetExtension(normalizedSource);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".asset";
            }

            destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                destinationFolder + "/" + fileStem + extension);
            if (!AssetDatabase.CopyAsset(normalizedSource, destinationPath))
            {
                message = "Unity could not copy '" + normalizedSource + "' to '" +
                          destinationPath + "'.";
                destinationPath = string.Empty;
                return false;
            }

            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport);
            localizedAssetPaths[normalizedSource] = destinationPath;
            return true;
        }

        private static Sprite FindSpriteAtPath(string assetPath, string preferredName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            Sprite fallback = null;
            for (int i = 0; i < assets.Length; i++)
            {
                Sprite sprite = assets[i] as Sprite;
                if (sprite == null)
                {
                    continue;
                }

                if (string.Equals(sprite.name, preferredName, StringComparison.Ordinal))
                {
                    return sprite;
                }

                if (fallback == null)
                {
                    fallback = sprite;
                }
            }

            return fallback;
        }

        private static bool IsSavedProjectAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (string.Equals(path, "Assets", StringComparison.Ordinal) ||
                    path.StartsWith("Assets/", StringComparison.Ordinal));
        }

        private static bool TryCopyOptionalDependency(
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            string propertyName,
            string relativeFolder,
            string fileStem,
            Type expectedType,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceProperty = serializedSource.FindProperty(propertyName);
            UnityEngine.Object sourceObject = sourceProperty == null
                ? null
                : sourceProperty.objectReferenceValue;
            if (sourceObject == null)
            {
                return true;
            }

            if (!expectedType.IsInstanceOfType(sourceObject))
            {
                message = "The '" + propertyName + "' dependency is not a " +
                          expectedType.Name + ".";
                return false;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceObject));
            if (string.IsNullOrEmpty(sourcePath) ||
                (!string.Equals(sourcePath, "Assets", StringComparison.Ordinal) &&
                 !sourcePath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                message = "The '" + propertyName +
                          "' dependency must be a saved project asset before it can be packaged.";
                return false;
            }

            if (AssetPathIsWithin(sourcePath, packageFolder))
            {
                return true;
            }

            string destinationFolder = packageFolder + "/" + relativeFolder;
            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    destinationFolder,
                    out message))
            {
                return false;
            }

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".asset";
            }

            string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                destinationFolder + "/" + fileStem + extension);
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                message = "Unity could not copy '" + sourcePath + "' to '" +
                          destinationPath + "'.";
                return false;
            }

            AssetDatabase.ImportAsset(
                destinationPath,
                ImportAssetOptions.ForceSynchronousImport);
            UnityEngine.Object copied = null;
            if (expectedType == typeof(Texture2D))
            {
                copied = AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
            }
            else if (expectedType == typeof(Sprite))
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(destinationPath);
                for (int i = 0; i < assets.Length; i++)
                {
                    Sprite sprite = assets[i] as Sprite;
                    if (sprite != null && string.Equals(
                            sprite.name,
                            sourceObject.name,
                            StringComparison.Ordinal))
                    {
                        copied = sprite;
                        break;
                    }

                    if (copied == null && sprite != null)
                    {
                        copied = sprite;
                    }
                }
            }

            if (copied == null)
            {
                message = "The copied '" + propertyName + "' asset could not be reloaded.";
                return false;
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.Update();
            SerializedProperty targetProperty = serializedTarget.FindProperty(propertyName);
            if (targetProperty == null)
            {
                message = "The portal profile no longer contains '" + propertyName + "'.";
                return false;
            }

            targetProperty.objectReferenceValue = copied;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return true;
        }

        private static string RollbackCreatedPreset(
            DimensionPortalVisualProfileAsset profile,
            string profilePath,
            string packageFolder,
            string manifestPath,
            SpriteAssetManifest manifestSnapshot)
        {
            List<string> errors = new List<string>();
            try
            {
                if (profile != null)
                {
                    Undo.ClearUndo(profile);
                }
            }
            catch (Exception exception)
            {
                errors.Add("clear incomplete preset Undo records: " + exception.Message);
            }

            try
            {
                if (!string.IsNullOrEmpty(packageFolder) &&
                    AssetDatabase.IsValidFolder(packageFolder) &&
                    !AssetDatabase.DeleteAsset(packageFolder))
                {
                    errors.Add("remove incomplete portal package '" + packageFolder + "'");
                }
            }
            catch (Exception exception)
            {
                errors.Add("remove incomplete portal package: " + exception.Message);
            }

            try
            {
                if (!string.IsNullOrEmpty(profilePath) &&
                    AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(profilePath) != null &&
                    !AssetDatabase.DeleteAsset(profilePath))
                {
                    errors.Add("remove incomplete preset '" + profilePath + "'");
                }
            }
            catch (Exception exception)
            {
                errors.Add("remove incomplete preset: " + exception.Message);
            }

            try
            {
                SpriteAssetManifest currentManifest =
                    AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
                if (manifestSnapshot == null)
                {
                    if (currentManifest != null && !AssetDatabase.DeleteAsset(manifestPath))
                    {
                        errors.Add("remove newly-created SpriteAsset manifest");
                    }
                }
                else if (currentManifest != null)
                {
                    EditorUtility.CopySerialized(manifestSnapshot, currentManifest);
                    // The asset's filename is the only name Unity's importer accepts.
                    currentManifest.name = DimensionPortalArtworkEditorUtility
                        .ResolveAssetFileName(currentManifest, manifestSnapshot.name);
                    EditorUtility.SetDirty(currentManifest);
                    AssetDatabase.SaveAssetIfDirty(currentManifest);
                }
                else
                {
                    SpriteAssetManifest restored =
                        DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifestSnapshot);
                    restored.name = Path.GetFileNameWithoutExtension(manifestPath);
                    AssetDatabase.CreateAsset(restored, manifestPath);
                }
            }
            catch (Exception exception)
            {
                errors.Add("restore SpriteAsset manifest: " + exception.Message);
            }

            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                errors.Add("refresh rolled-back preset assets: " + exception.Message);
            }

            try
            {
                DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            }
            catch (Exception exception)
            {
                errors.Add("invalidate rolled-back SpriteAsset caches: " + exception.Message);
            }

            InvalidateAll();
            return errors.Count == 0 ? string.Empty : string.Join("; ", errors.ToArray());
        }

        private static void AppendRollbackMessage(ref string message, string rollbackMessage)
        {
            if (string.IsNullOrEmpty(rollbackMessage))
            {
                return;
            }

            message += " Rollback warning: " + rollbackMessage + ".";
        }
    }
}
