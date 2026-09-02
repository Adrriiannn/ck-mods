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
    /// Copying a swirl asset and everything it points at into a package a creator owns.
    /// </summary>
    internal static partial class DimensionPortalSwirlArtworkEditorUtility
    {
        public static bool TryLocalizeIntoPackage(
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            string modRoot,
            out string message)
        {
            message = string.Empty;
            if (source == null || target == null)
            {
                message = "The source and destination portal profiles are required.";
                return false;
            }

            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceReference = serializedSource.FindProperty(
                ReferencePropertyName);
            SerializedProperty sourceOverride = serializedSource.FindProperty(
                OverridePropertyName);
            if (sourceReference == null)
            {
                // Backward compatibility while an older profile schema is imported.
                return true;
            }

            bool overrideVanilla = sourceOverride != null && sourceOverride.boolValue;
            string sourcePackageFolder = string.Empty;
            DimensionPortalPackageEditorUtility.TryGetPackage(
                source,
                out _,
                out sourcePackageFolder);
            if (!TryResolveReference(
                    source,
                    sourceReference,
                    sourcePackageFolder,
                    out SpriteAsset sourceAsset))
            {
                if (!HasAddress(sourceReference) && !overrideVanilla)
                {
                    return true;
                }

                message = overrideVanilla
                    ? "Custom Swirls are enabled, but their SpriteAsset could not be resolved in Scriptable Data."
                    : "The dormant custom Swirls SpriteAsset could not be resolved, so it could not be preserved in this portal package.";
                return false;
            }

            if (sourceAsset == null)
            {
                if (!overrideVanilla)
                {
                    return true;
                }

                message = "Custom Swirls are enabled, but no Swirls SpriteAsset is selected.";
                return false;
            }

            // The framework starter is intentionally shared while vanilla mode is active.
            // Enabling the custom mode closes over it like any other custom selection, so
            // subsequent edits never mutate framework-owned artwork.
            if (IsFrameworkAsset(sourceAsset) && !overrideVanilla)
            {
                return AssignReference(target, sourceAsset, out message);
            }

            string expectedFolder = NormalizeAssetPath(
                packageFolder + "/" + PackageRelativeFolder);
            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
            if (ReferenceEquals(source, target) &&
                AssetPathIsWithin(sourcePath, expectedFolder))
            {
                // The package already owns this profile's pristine Swirls artwork. The tint
                // is applied at runtime from the profile colors, so an in-place Save & Update
                // needs no texture rewrite or import here.
                return true;
            }

            if (!TryValidateAnimationContract(
                    sourceAsset,
                    false,
                    out _,
                    out message))
            {
                message = "The selected Swirls SpriteAsset is not packageable. " + message;
                return false;
            }

            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    expectedFolder,
                    out message))
            {
                return false;
            }

            return TryCloneGraph(
                sourceAsset,
                target,
                expectedFolder,
                modRoot,
                out message);
        }

        private static bool TryCloneGraph(
            SpriteAsset sourceAsset,
            DimensionPortalVisualProfileAsset target,
            string destinationFolder,
            string modRoot,
            out string message)
        {
            message = string.Empty;
            List<string> createdPaths = new List<string>();
            DimensionPortalVisualProfileAsset targetSnapshot =
                DimensionPortalArtworkEditorUtility.InstantiateSnapshot(target);
            string manifestPath = NormalizeAssetPath(modRoot) + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            SpriteAssetManifest manifestSnapshot = manifest == null
                ? null
                : DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifest);
            try
            {
                string assetStem = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(target.name, "Portal") + "_Swirls";
                string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                    destinationFolder + "/" + assetStem + ".asset");
                SpriteAsset clone = UnityEngine.Object.Instantiate(sourceAsset);
                clone.name = Path.GetFileNameWithoutExtension(destinationPath);
                SetFreshAddress(clone, destinationPath);
                AssetDatabase.CreateAsset(clone, destinationPath);
                createdPaths.Add(destinationPath);

                SerializedObject serializedSource = new SerializedObject(sourceAsset);
                serializedSource.Update();
                SerializedObject serializedClone = new SerializedObject(clone);
                serializedClone.Update();
                Dictionary<string, Texture2D> localizedTextures =
                    new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

                if (!CopySpriteDataTextures(
                        serializedSource.FindProperty("m_staticSpriteData"),
                        serializedClone.FindProperty("m_staticSpriteData"),
                        destinationFolder,
                        assetStem + "_Static",
                        localizedTextures,
                        createdPaths,
                        null,
                        null,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                SerializedProperty sourceAnimations =
                    serializedSource.FindProperty("m_animations");
                SerializedProperty cloneAnimations =
                    serializedClone.FindProperty("m_animations");
                if (sourceAnimations == null || cloneAnimations == null ||
                    sourceAnimations.arraySize != cloneAnimations.arraySize)
                {
                    throw new InvalidOperationException(
                        "The cloned Swirls SpriteAsset did not preserve its animation slots.");
                }

                for (int i = 0; i < sourceAnimations.arraySize; i++)
                {
                    if (!CopySpriteDataTextures(
                            sourceAnimations.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("m_spriteData"),
                            cloneAnimations.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("m_spriteData"),
                            destinationFolder,
                            assetStem + "_Anim" + i,
                            localizedTextures,
                            createdPaths,
                            null,
                            null,
                            out message))
                    {
                        throw new InvalidOperationException(message);
                    }
                }

                serializedClone.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(clone);
                AssetDatabase.SaveAssetIfDirty(clone);
                AssetDatabase.ImportAsset(
                    destinationPath,
                    ImportAssetOptions.ForceSynchronousImport);
                clone = AssetDatabase.LoadAssetAtPath<SpriteAsset>(destinationPath);
                if (clone == null)
                {
                    throw new InvalidOperationException(
                        "The localized Swirls SpriteAsset could not be reloaded.");
                }

                string packageSuffix = "/" + PackageRelativeFolder;
                string clonePackageFolder = destinationFolder.EndsWith(
                        packageSuffix,
                        StringComparison.OrdinalIgnoreCase)
                    ? destinationFolder.Substring(
                        0,
                        destinationFolder.Length - packageSuffix.Length)
                    : NormalizeAssetPath(Path.GetDirectoryName(destinationFolder));
                if (!TryBuildAnimationZeroTextureSlot(
                        clone,
                        clonePackageFolder,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                // Carry the pristine untinted source sheet across the clone so the duplicated
                // profile can be recolored without re-selecting artwork. Otherwise the clone
                // would hold only the already-baked (tinted) output and the next color bake
                // would have no clean source to start from.
                string sourceAssetPath =
                    NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
                string sourceSourceSheet =
                    NormalizeAssetPath(Path.GetDirectoryName(sourceAssetPath)) + "/" +
                    Path.GetFileNameWithoutExtension(sourceAssetPath) + SwirlSourceSuffix;
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourceSourceSheet)))
                {
                    string cloneSourceSheet = NormalizeAssetPath(destinationFolder) + "/" +
                        Path.GetFileNameWithoutExtension(destinationPath) + SwirlSourceSuffix;
                    if (AssetDatabase.CopyAsset(sourceSourceSheet, cloneSourceSheet))
                    {
                        createdPaths.Add(cloneSourceSheet);
                    }
                }

                EnsureManifestContains(modRoot, destinationPath);
                if (!AssignReference(target, clone, out message))
                {
                    throw new InvalidOperationException(message);
                }

                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.SaveAssets();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not localize the custom Swirls artwork. " + exception.Message;
                try
                {
                    EditorUtility.CopySerialized(targetSnapshot, target);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                }
                catch (Exception restoreException)
                {
                    message += " Could not restore the profile: " + restoreException.Message;
                }

                try
                {
                    RestoreManifest(manifestPath, manifestSnapshot);
                }
                catch (Exception restoreException)
                {
                    message += " Could not restore the SpriteAsset manifest: " +
                               restoreException.Message;
                }

                for (int i = createdPaths.Count - 1; i >= 0; i--)
                {
                    string path = createdPaths[i];
                    if (!string.IsNullOrEmpty(path) &&
                        AssetDatabase.LoadMainAssetAtPath(path) != null)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetSnapshot);
                if (manifestSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifestSnapshot);
                }
            }
        }

        private static bool CopySpriteDataTextures(
            SerializedProperty sourceData,
            SerializedProperty targetData,
            string destinationFolder,
            string fileStem,
            Dictionary<string, Texture2D> localizedTextures,
            List<string> createdPaths,
            Texture2D sourceColorOverride,
            Texture2D sourceEmissiveOverride,
            out string message)
        {
            message = string.Empty;
            if (sourceData == null || targetData == null)
            {
                return true;
            }

            string[] properties = { "texture", "emissiveTexture", "normalTexture" };
            string[] suffixes = { string.Empty, "_Emissive", "_Normal" };
            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty sourceProperty =
                    sourceData.FindPropertyRelative(properties[i]);
                SerializedProperty targetProperty =
                    targetData.FindPropertyRelative(properties[i]);
                if (targetProperty == null)
                {
                    message = "The Swirls SpriteAsset does not expose its " +
                              properties[i] + " texture slot.";
                    return false;
                }

                Texture2D sourceTexture;
                if (i == 0 && sourceColorOverride != null)
                {
                    sourceTexture = sourceColorOverride;
                }
                else if (i == 1 && sourceEmissiveOverride != null)
                {
                    sourceTexture = sourceEmissiveOverride;
                }
                else
                {
                    sourceTexture = sourceProperty == null
                        ? null
                        : sourceProperty.objectReferenceValue as Texture2D;
                }
                if (sourceTexture == null)
                {
                    targetProperty.objectReferenceValue = null;
                    continue;
                }

                string sourcePath = NormalizeAssetPath(
                    AssetDatabase.GetAssetPath(sourceTexture));
                if (string.IsNullOrEmpty(sourcePath) ||
                    !sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    message = "Swirls texture '" + sourceTexture.name +
                              "' must be a saved project asset before it can be packaged.";
                    return false;
                }

                if (!localizedTextures.TryGetValue(sourcePath, out Texture2D copiedTexture) ||
                    copiedTexture == null)
                {
                    string extension = Path.GetExtension(sourcePath);
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = ".png";
                    }

                    string copiedPath = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + fileStem + suffixes[i] + extension);
                    if (!AssetDatabase.CopyAsset(sourcePath, copiedPath))
                    {
                        message = "Unity could not copy Swirls texture '" + sourcePath + "'.";
                        return false;
                    }

                    AssetDatabase.ImportAsset(
                        copiedPath,
                        ImportAssetOptions.ForceSynchronousImport);
                    copiedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(copiedPath);
                    if (copiedTexture == null)
                    {
                        message = "The copied Swirls texture could not be reloaded from '" +
                                  copiedPath + "'.";
                        return false;
                    }

                    localizedTextures[sourcePath] = copiedTexture;
                    createdPaths.Add(copiedPath);
                }

                targetProperty.objectReferenceValue = copiedTexture;
            }

            return true;
        }

        private static bool AssignReference(
            DimensionPortalVisualProfileAsset profile,
            SpriteAsset asset,
            out string message)
        {
            message = string.Empty;
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (reference == null)
            {
                message = "The portal profile no longer contains the custom Swirls reference.";
                return false;
            }

            ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, asset);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static Texture2D GetTexture(
            SerializedProperty spriteData,
            string propertyName)
        {
            SerializedProperty property = spriteData == null
                ? null
                : spriteData.FindPropertyRelative(propertyName);
            return property == null ? null : property.objectReferenceValue as Texture2D;
        }

        private static bool ChannelsMatch(Texture2D color, Texture2D optional)
        {
            return optional == null ||
                   (color != null && optional.width == color.width &&
                    optional.height == color.height);
        }

        private static void AddDependency(
            List<TextureDependency> dependencies,
            HashSet<Texture2D> seen,
            string role,
            Texture2D texture)
        {
            if (texture == null || !seen.Add(texture))
            {
                return;
            }

            dependencies.Add(new TextureDependency { Role = role, Texture = texture });
        }

        private static void SetFreshAddress(SpriteAsset asset, string assetPath)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                throw new InvalidOperationException(
                    "The Swirls SpriteAsset address could not be assigned.");
            }

            low.longValue = DimensionSpriteAssetAddress.Part(
                assetPath,
                0x737769726C617274UL);
            high.longValue = DimensionSpriteAssetAddress.Part(
                assetPath,
                0x706F7274616C7377UL);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The address is the lookup index's key, so re-addressing invalidates it.
            DimensionPortalArtworkEditorUtility.InvalidateSpriteAssetAddressIndex();
        }

        private static void EnsureManifestContains(string modRoot, string spriteAssetPath)
        {
            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(spriteAssetPath);
            if (spriteAsset == null)
            {
                throw new InvalidOperationException(
                    "The localized Swirls SpriteAsset could not be loaded for manifest registration.");
            }

            string manifestPath = NormalizeAssetPath(modRoot) + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                manifest.name = "SpriteAssetManifest";
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                if (manifest.spriteAssets[i] == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                }
            }

            if (!manifest.spriteAssets.Contains(spriteAsset))
            {
                manifest.spriteAssets.Add(spriteAsset);
            }

            manifest.name = Path.GetFileNameWithoutExtension(manifestPath);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }

        private static void RestoreManifest(
            string manifestPath,
            SpriteAssetManifest snapshot)
        {
            SpriteAssetManifest current =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (snapshot == null)
            {
                if (current != null)
                {
                    AssetDatabase.DeleteAsset(manifestPath);
                }
                return;
            }

            // The asset's own filename is the only name Unity's importer will accept, so it wins
            // over whatever name the in-memory snapshot happens to carry.
            string manifestName = Path.GetFileNameWithoutExtension(manifestPath);

            if (current == null)
            {
                SpriteAssetManifest restored =
                    DimensionPortalArtworkEditorUtility.InstantiateSnapshot(snapshot);
                restored.name = manifestName;
                AssetDatabase.CreateAsset(restored, manifestPath);
                return;
            }

            EditorUtility.CopySerialized(snapshot, current);
            current.name = manifestName;
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssetIfDirty(current);
        }
    }
}
