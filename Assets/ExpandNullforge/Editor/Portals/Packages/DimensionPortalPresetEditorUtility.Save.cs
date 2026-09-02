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
    /// Saving a profile, duplicating one, and making the vanilla one from scratch.
    /// </summary>
    internal static partial class DimensionPortalPresetEditorUtility
    {
        public static bool SaveCurrent(
            DimensionTemplateAsset template,
            out string message)
        {
            DimensionPortalVisualProfileAsset profile =
                template == null ? null : template.PortalVisualProfile;
            if (!SaveProfile(template, profile, out message))
            {
                return false;
            }

            AssetDatabase.SaveAssetIfDirty(template);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool SaveProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (!IsPresetOwnedByTemplate(template, profile))
            {
                message =
                    "The selected portal profile does not belong to this Dimension Asset.";
                return false;
            }

            if (!TryResolveContext(
                    template,
                    profile,
                    out _,
                    out _,
                    out _,
                    out message))
            {
                return false;
            }

            if (!DimensionPortalArtworkEditorUtility.FlushPending(profile, out message))
            {
                return false;
            }

            if (!DimensionPortalSwirlArtworkEditorUtility.FlushSwirlBake(
                    template,
                    profile,
                    out message))
            {
                return false;
            }

            DimensionPortalPackageAsset package = null;
            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out package,
                    out string packageFolder))
            {
                // Save & Update is also the import boundary for optional artwork. Authors
                // may select any saved project Sprite/Texture in the Studio, but a durable
                // portal package must never retain references outside its own folder.
                // Reusing the same source/target profile is intentional: the localization
                // helpers leave already-owned dependencies untouched and remap only newly
                // selected external assets, so repeated saves remain stable.
                if (!TryLocalizeOptionalDependencies(
                        template,
                        profile,
                        profile,
                        packageFolder,
                        out message))
                {
                    message = "Could not centralize the portal's optional artwork. " +
                              message;
                    return false;
                }

                if (!DimensionPortalPackageEditorUtility.RefreshArtworkInventory(
                        package,
                        profile,
                        out message))
                {
                    return false;
                }
            }

            AssetDatabase.SaveAssetIfDirty(profile);
            if (package != null)
            {
                AssetDatabase.SaveAssetIfDirty(package);
            }

            AssetDatabase.SaveAssets();
            message = "Saved portal preset '" + GetProfileDisplayName(profile) + "'.";
            return true;
        }

        public static bool SaveAs(
            DimensionTemplateAsset template,
            string requestedName,
            bool switchToCreated,
            out DimensionPortalVisualProfileAsset created,
            out string message)
        {
            DimensionPortalVisualProfileAsset source =
                template == null ? null : template.PortalVisualProfile;
            return DuplicateProfileInternal(
                template,
                source,
                requestedName,
                switchToCreated,
                out created,
                out message);
        }

        public static bool DuplicateProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset source,
            out DimensionPortalVisualProfileAsset created,
            out string message)
        {
            string sourceName = GetProfileDisplayName(source);
            string requestedName = string.IsNullOrWhiteSpace(sourceName)
                ? "Portal Copy"
                : sourceName + " Copy";
            return DuplicateProfileInternal(
                template,
                source,
                requestedName,
                false,
                out created,
                out message);
        }

        private static bool DuplicateProfileInternal(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset source,
            string requestedName,
            bool switchToCreated,
            out DimensionPortalVisualProfileAsset created,
            out string message)
        {
            created = null;
            message = string.Empty;
            if (!IsPresetOwnedByTemplate(template, source))
            {
                message =
                    "The selected portal profile does not belong to this Dimension Asset.";
                return false;
            }

            if (!TryResolveContext(
                    template,
                    source,
                    out _,
                    out string ownerRoot,
                    out _,
                    out message))
            {
                return false;
            }

            if (!DimensionPortalArtworkEditorUtility.FlushPending(source, out message))
            {
                return false;
            }

            string sourceDisplayName = GetProfileDisplayName(source);
            string fallbackName = string.IsNullOrWhiteSpace(sourceDisplayName)
                ? "Portal Copy"
                : sourceDisplayName + " Copy";
            string displayName = GetUniqueDisplayName(
                template,
                requestedName,
                fallbackName);
            string manifestPath = ownerRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            SpriteAssetManifest manifestSnapshot = manifest == null
                ? null
                : DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifest);
            DimensionPortalVisualProfileAsset duplicate = null;
            DimensionPortalPackageAsset package = null;
            string packageFolder = string.Empty;
            string targetPath = string.Empty;
            try
            {
                if (!DimensionPortalPackageEditorUtility.TryCreatePackageFolder(
                        template,
                        displayName,
                        out string packageId,
                        out packageFolder,
                        out message))
                {
                    return false;
                }

                targetPath = packageFolder + "/" +
                             DimensionPortalPackageEditorUtility.ProfileFileName;
                duplicate = UnityEngine.Object.Instantiate(source);
                duplicate.name = displayName;
                AssetDatabase.CreateAsset(duplicate, targetPath);
                package = ScriptableObject.CreateInstance<DimensionPortalPackageAsset>();
                package.name = "PortalPackage";
                package.Configure(
                    packageId,
                    displayName,
                    template.DimensionId,
                    duplicate);
                AssetDatabase.CreateAsset(
                    package,
                    packageFolder + "/" +
                    DimensionPortalPackageEditorUtility.PackageFileName);
                EditorUtility.SetDirty(package);
                AssetDatabase.SaveAssets();

                if (!TryCloneCompleteArtwork(
                        template,
                        source,
                        duplicate,
                        out string cloneMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        duplicate,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    created = null;
                    message = "Could not create portal preset because its private artwork " +
                              "could not be cloned. " + cloneMessage +
                              " The incomplete preset and all artwork created for it were removed.";
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                if (!TryLocalizeOptionalDependencies(
                        template,
                        source,
                        duplicate,
                        packageFolder,
                        out string dependencyMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        duplicate,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    created = null;
                    message = "Could not create portal preset because an effect or shadow " +
                              "asset could not be copied into its package. " +
                              dependencyMessage;
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                if (!DimensionPortalPackageEditorUtility.RefreshArtworkInventory(
                        package,
                        duplicate,
                        out string inventoryMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        duplicate,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    created = null;
                    message = "Could not create portal preset because its package inventory " +
                              "was incomplete. " + inventoryMessage;
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                EditorUtility.SetDirty(duplicate);
                EditorUtility.SetDirty(package);
                AssetDatabase.SaveAssetIfDirty(duplicate);
                AssetDatabase.SaveAssetIfDirty(package);
                AssetDatabase.SaveAssets();

                // Artwork creation records selector changes on the new profile so failures
                // can be reverted safely. Once every layer is durable, those internal Undo
                // records must not let a later Undo turn a private preset back into a mixed
                // shared/private profile while leaving its managed files behind.
                Undo.ClearUndo(duplicate);
                InvalidateAll();

                if (switchToCreated &&
                    !SwitchPreset(template, duplicate, false, out string switchMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        duplicate,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    created = null;
                    message = "Could not create and select the portal preset. " + switchMessage +
                              " The incomplete preset and all artwork created for it were removed.";
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                created = duplicate;
                message = "Created complete portal package '" + duplicate.name + "' at " +
                          packageFolder + ".";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackMessage = RollbackCreatedPreset(
                    duplicate,
                    targetPath,
                    packageFolder,
                    manifestPath,
                    manifestSnapshot);
                created = null;
                message = "Could not create portal preset. " + exception.Message +
                          " The incomplete preset and all artwork created for it were removed.";
                AppendRollbackMessage(ref message, rollbackMessage);
                return false;
            }
            finally
            {
                if (manifestSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifestSnapshot);
                }
            }
        }

        public static bool CreateVanilla(
            DimensionTemplateAsset template,
            out DimensionPortalVisualProfileAsset created,
            out string message)
        {
            return CreateVanilla(
                template,
                "New Portal",
                false,
                out created,
                out message);
        }

        public static bool CreateVanilla(
            DimensionTemplateAsset template,
            string requestedName,
            bool switchToCreated,
            out DimensionPortalVisualProfileAsset created,
            out string message)
        {
            created = null;
            message = string.Empty;
            if (!TryResolveContext(
                    template,
                    null,
                    out _,
                    out string ownerRoot,
                    out string presetFolder,
                    out message))
            {
                return false;
            }

            string displayName = GetUniqueDisplayName(
                template,
                requestedName,
                "New Portal");
            string manifestPath = ownerRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            SpriteAssetManifest manifestSnapshot = manifest == null
                ? null
                : DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifest);
            DimensionPortalVisualProfileAsset vanilla = null;
            DimensionPortalPackageAsset package = null;
            string packageFolder = string.Empty;
            string targetPath = string.Empty;
            try
            {
                if (!DimensionPortalPackageEditorUtility.TryCreatePackageFolder(
                        template,
                        displayName,
                        out string packageId,
                        out packageFolder,
                        out message))
                {
                    return false;
                }

                targetPath = packageFolder + "/" +
                             DimensionPortalPackageEditorUtility.ProfileFileName;
                vanilla = ScriptableObject.CreateInstance<DimensionPortalVisualProfileAsset>();
                vanilla.name = displayName;
                AssetDatabase.CreateAsset(vanilla, targetPath);
                package = ScriptableObject.CreateInstance<DimensionPortalPackageAsset>();
                package.name = "PortalPackage";
                package.Configure(
                    packageId,
                    displayName,
                    template.DimensionId,
                    vanilla);
                AssetDatabase.CreateAsset(
                    package,
                    packageFolder + "/" +
                    DimensionPortalPackageEditorUtility.PackageFileName);
                EditorUtility.SetDirty(package);
                AssetDatabase.SaveAssets();

                if (!DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                        template,
                        vanilla,
                        out string initializationMessage))
                {
                    SerializedObject serialized = new SerializedObject(vanilla);
                    SerializedProperty version = serialized.FindProperty(
                        "portalArtworkDefaultsVersion");
                    bool initialized = version != null && version.intValue > 0;
                    if (!initialized)
                    {
                        string rollbackMessage = RollbackCreatedPreset(
                            vanilla,
                            targetPath,
                            packageFolder,
                            manifestPath,
                            manifestSnapshot);
                        message = string.IsNullOrEmpty(initializationMessage)
                            ? "Could not initialize the vanilla portal preset."
                            : initializationMessage;
                        AppendRollbackMessage(ref message, rollbackMessage);
                        return false;
                    }
                }

                if (!TryCloneCompleteArtwork(
                        template,
                        vanilla,
                        vanilla,
                        out string cloneMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        vanilla,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    message = "Could not materialize the complete vanilla portal package. " +
                              cloneMessage;
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                if (!TryLocalizeOptionalDependencies(
                        template,
                        vanilla,
                        vanilla,
                        packageFolder,
                        out string dependencyMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        vanilla,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    message = "Could not localize the vanilla portal effect dependencies. " +
                              dependencyMessage;
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                if (!DimensionPortalPackageEditorUtility.RefreshArtworkInventory(
                        package,
                        vanilla,
                        out string inventoryMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        vanilla,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    message = "Could not validate the complete vanilla portal package. " +
                              inventoryMessage;
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                EditorUtility.SetDirty(vanilla);
                EditorUtility.SetDirty(package);
                AssetDatabase.SaveAssetIfDirty(vanilla);
                AssetDatabase.SaveAssetIfDirty(package);
                AssetDatabase.SaveAssets();
                Undo.ClearUndo(vanilla);
                InvalidateAll();

                if (switchToCreated &&
                    !SwitchPreset(template, vanilla, false, out string switchMessage))
                {
                    string rollbackMessage = RollbackCreatedPreset(
                        vanilla,
                        targetPath,
                        packageFolder,
                        manifestPath,
                        manifestSnapshot);
                    message = "Could not create and select the vanilla portal preset. " +
                              switchMessage + " The incomplete preset was removed.";
                    AppendRollbackMessage(ref message, rollbackMessage);
                    return false;
                }

                created = vanilla;
                message = "Created complete vanilla portal package '" + vanilla.name +
                          "' at " + packageFolder + ".";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackMessage = RollbackCreatedPreset(
                    vanilla,
                    targetPath,
                    packageFolder,
                    manifestPath,
                    manifestSnapshot);
                created = null;
                message = "Could not create the vanilla portal preset. " + exception.Message +
                          " The incomplete preset was removed.";
                AppendRollbackMessage(ref message, rollbackMessage);
                return false;
            }
            finally
            {
                if (manifestSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifestSnapshot);
                }
            }
        }
    }
}
