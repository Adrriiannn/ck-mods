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
    /// Consumer-owned persistence for Portal Studio profiles. Enumeration is cached and
    /// scoped to the selected Dimension Asset's PortalPresets folder; AssetDatabase scans
    /// happen only on the first request after a project change or an explicit mutation.
    /// </summary>
    internal static class DimensionPortalPresetEditorUtility
    {
        private const string LegacyPresetFolderName = "PortalPresets";

        private sealed class PresetCache
        {
            public string TemplatePath;
            public string OwnerRoot;
            public string PresetFolder;
            public IReadOnlyList<DimensionPortalVisualProfileAsset> Profiles;
        }

        private static readonly Dictionary<string, PresetCache> PresetCaches =
            new Dictionary<string, PresetCache>(StringComparer.OrdinalIgnoreCase);

        private static readonly DimensionPortalArtworkLayer[] ArtworkLayers =
        {
            DimensionPortalArtworkLayer.Frame,
            DimensionPortalArtworkLayer.ChargeSweep,
            DimensionPortalArtworkLayer.Milestones,
            DimensionPortalArtworkLayer.Center
        };

        static DimensionPortalPresetEditorUtility()
        {
            EditorApplication.projectChanged += InvalidateAll;
            Undo.undoRedoPerformed += InvalidateAll;
        }

        public static IReadOnlyList<DimensionPortalVisualProfileAsset> GetPresets(
            DimensionTemplateAsset template)
        {
            if (!TryResolveContext(
                    template,
                    null,
                    out string templatePath,
                    out string ownerRoot,
                    out string presetFolder,
                    out _))
            {
                return Array.Empty<DimensionPortalVisualProfileAsset>();
            }

            if (PresetCaches.TryGetValue(presetFolder, out PresetCache cached) &&
                cached != null &&
                string.Equals(cached.TemplatePath, templatePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cached.OwnerRoot, ownerRoot, StringComparison.OrdinalIgnoreCase) &&
                CacheIsAlive(cached.Profiles))
            {
                return cached.Profiles;
            }

            List<DimensionPortalVisualProfileAsset> profiles =
                new List<DimensionPortalVisualProfileAsset>();
            DimensionPortalVisualProfileAsset active = template.PortalVisualProfile;
            if (active != null && IsProfileOwnedBy(active, ownerRoot))
            {
                profiles.Add(active);
            }

            // Keep the template's original profile reachable after the author switches
            // to an alternative preset. It lives beside the Dimension Asset rather than
            // inside PortalPresets, so scanning only the preset folder would make it
            // disappear from the selector as soon as it became inactive.
            string templateFolder = NormalizeAssetPath(Path.GetDirectoryName(templatePath));
            DimensionPortalVisualProfileAsset defaultProfile =
                AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(
                    templateFolder + "/PortalVisualProfile.asset");
            if (defaultProfile != null &&
                IsProfileOwnedBy(defaultProfile, ownerRoot) &&
                !profiles.Contains(defaultProfile))
            {
                profiles.Add(defaultProfile);
            }

            IReadOnlyList<DimensionPortalVisualProfileAsset> packageProfiles =
                DimensionPortalPackageEditorUtility.FindPackageProfiles(template);
            for (int i = 0; i < packageProfiles.Count; i++)
            {
                DimensionPortalVisualProfileAsset profile = packageProfiles[i];
                if (profile != null && !profiles.Contains(profile))
                {
                    profiles.Add(profile);
                }
            }

            // Legacy loose presets stay discoverable until the author saves/migrates them
            // into a self-contained package. This bounded search is kept out of steady IMGUI.
            string legacyPresetFolder = templateFolder + "/" + LegacyPresetFolderName;
            if (AssetDatabase.IsValidFolder(legacyPresetFolder))
            {
                string[] guids = AssetDatabase.FindAssets(
                    "t:DimensionPortalVisualProfileAsset",
                    new[] { legacyPresetFolder });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                    DimensionPortalVisualProfileAsset profile =
                        AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(path);
                    if (profile != null &&
                        IsProfileOwnedBy(profile, ownerRoot) &&
                        !profiles.Contains(profile))
                    {
                        profiles.Add(profile);
                    }
                }
            }

            profiles.Sort(CompareProfiles);
            IReadOnlyList<DimensionPortalVisualProfileAsset> readOnly = profiles.AsReadOnly();
            PresetCaches[presetFolder] = new PresetCache
            {
                TemplatePath = templatePath,
                OwnerRoot = ownerRoot,
                PresetFolder = presetFolder,
                Profiles = readOnly
            };
            return readOnly;
        }

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
                : UnityEngine.Object.Instantiate(manifest);
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
                : UnityEngine.Object.Instantiate(manifest);
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

        public static bool SwitchPreset(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset target,
            out string message)
        {
            return SwitchPreset(template, target, true, out message);
        }

        public static string GetPresetFolder(DimensionTemplateAsset template)
        {
            return TryResolveContext(
                template,
                null,
                out _,
                out _,
                out string presetFolder,
                out _)
                ? presetFolder
                : string.Empty;
        }

        public static void Invalidate(DimensionTemplateAsset template)
        {
            string folder = GetPresetFolder(template);
            if (!string.IsNullOrEmpty(folder))
            {
                PresetCaches.Remove(folder);
            }
        }

        private static bool SwitchPreset(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset target,
            bool flushCurrent,
            out string message)
        {
            message = string.Empty;
            if (!IsPresetOwnedByTemplate(template, target))
            {
                message = "The selected portal preset does not belong to this Dimension Asset.";
                return false;
            }

            if (!TryResolveContext(
                    template,
                    target,
                    out _,
                    out _,
                    out _,
                    out message))
            {
                return false;
            }

            if (template.PortalVisualProfile == target)
            {
                message = "Portal preset '" + target.name + "' is already selected.";
                return true;
            }

            if (flushCurrent && template.PortalVisualProfile != null &&
                !SaveCurrent(template, out message))
            {
                return false;
            }

            DimensionPortalVisualProfileAsset previous = template.PortalVisualProfile;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Switch portal preset");
            try
            {
                Undo.RecordObject(template, "Switch portal preset");
                template.SetPortalVisualProfile(target);
                EditorUtility.SetDirty(template);
                AssetDatabase.SaveAssetIfDirty(template);
                Undo.CollapseUndoOperations(undoGroup);
                InvalidateAll();
                message = "Selected portal preset '" + target.name + "'.";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackMessage = string.Empty;
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    template.SetPortalVisualProfile(previous);
                    EditorUtility.SetDirty(template);
                    AssetDatabase.SaveAssetIfDirty(template);
                }
                catch (Exception rollbackException)
                {
                    rollbackMessage = rollbackException.Message;
                }

                InvalidateAll();
                message = "Could not select portal preset '" + target.name + "'. " +
                          exception.Message;
                AppendRollbackMessage(ref message, rollbackMessage);
                return false;
            }
        }

        internal static bool IsPresetOwnedByTemplate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out DimensionPortalPackageAsset package,
                    out _))
            {
                return DimensionPortalPackageEditorUtility.IsPackageOwnedByTemplate(
                    template,
                    package);
            }

            string templatePath = NormalizeAssetPath(
                template == null ? string.Empty : AssetDatabase.GetAssetPath(template));
            string profilePath = NormalizeAssetPath(
                profile == null ? string.Empty : AssetDatabase.GetAssetPath(profile));
            string templateFolder = NormalizeAssetPath(Path.GetDirectoryName(templatePath));
            if (string.IsNullOrEmpty(templateFolder) || string.IsNullOrEmpty(profilePath))
            {
                return false;
            }

            string defaultPath = templateFolder + "/PortalVisualProfile.asset";
            string presetFolder = templateFolder + "/" + LegacyPresetFolderName;
            return string.Equals(profilePath, defaultPath, StringComparison.OrdinalIgnoreCase) ||
                   AssetPathIsWithin(profilePath, presetFolder);
        }

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
                    GetReferencePropertyName(layer));
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
                    currentManifest.name = manifestSnapshot.name;
                    EditorUtility.SetDirty(currentManifest);
                    AssetDatabase.SaveAssetIfDirty(currentManifest);
                }
                else
                {
                    SpriteAssetManifest restored =
                        UnityEngine.Object.Instantiate(manifestSnapshot);
                    restored.name = manifestSnapshot.name;
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

        private static bool TryReadStaticFrameTextures(
            SpriteAsset asset,
            out Texture2D texture,
            out Texture2D emissive)
        {
            texture = null;
            emissive = null;
            if (asset == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty data = serialized.FindProperty("m_staticSpriteData");
            SerializedProperty textureProperty = data == null
                ? null
                : data.FindPropertyRelative("texture");
            SerializedProperty emissiveProperty = data == null
                ? null
                : data.FindPropertyRelative("emissiveTexture");
            texture = textureProperty == null
                ? null
                : textureProperty.objectReferenceValue as Texture2D;
            emissive = emissiveProperty == null
                ? null
                : emissiveProperty.objectReferenceValue as Texture2D;
            return texture != null;
        }

        private static bool TryResolveContext(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string templatePath,
            out string ownerRoot,
            out string presetFolder,
            out string message)
        {
            templatePath = NormalizeAssetPath(
                template == null ? string.Empty : AssetDatabase.GetAssetPath(template));
            ownerRoot = string.Empty;
            presetFolder = string.Empty;
            message = string.Empty;
            if (template == null || string.IsNullOrEmpty(templatePath))
            {
                message = "Save and select a Dimension Asset before managing portal presets.";
                return false;
            }

            ownerRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            if (string.IsNullOrEmpty(ownerRoot) ||
                string.Equals(ownerRoot, "Assets/ExpandNullforge", StringComparison.OrdinalIgnoreCase))
            {
                message = "Portal presets must belong to a saved consumer mod Dimension Asset; framework assets are read-only.";
                return false;
            }

            string templateFolder = NormalizeAssetPath(Path.GetDirectoryName(templatePath));
            if (string.IsNullOrEmpty(templateFolder) ||
                !AssetPathIsWithin(templateFolder, ownerRoot))
            {
                message = "Could not resolve a consumer-owned folder for the selected Dimension Asset.";
                return false;
            }

            if (!DimensionPortalPackageEditorUtility.TryResolveLibraryFolder(
                    template,
                    out string packageOwnerRoot,
                    out presetFolder,
                    out string packageMessage) ||
                !string.Equals(
                    packageOwnerRoot,
                    ownerRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = string.IsNullOrEmpty(packageMessage)
                    ? "Could not resolve the portal package library for this Dimension Asset."
                    : packageMessage;
                return false;
            }

            if (profile != null && !IsProfileOwnedBy(profile, ownerRoot))
            {
                message = "The selected portal profile must be saved inside the same consumer mod as the Dimension Asset.";
                return false;
            }

            return true;
        }

        private static bool IsProfileOwnedBy(
            DimensionPortalVisualProfileAsset profile,
            string ownerRoot)
        {
            string path = NormalizeAssetPath(
                profile == null ? string.Empty : AssetDatabase.GetAssetPath(profile));
            if (string.IsNullOrEmpty(path) || !AssetPathIsWithin(path, ownerRoot))
            {
                return false;
            }

            string profileRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(path));
            return string.Equals(profileRoot, ownerRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnsureFolder(string folder, out string message)
        {
            message = string.Empty;
            string normalized = NormalizeAssetPath(folder);
            if (string.IsNullOrEmpty(normalized) ||
                (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                 !normalized.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                message = "Portal preset folder is not a valid project asset path.";
                return false;
            }

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return true;
            }

            string[] parts = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    string created = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (!string.Equals(created, next, StringComparison.OrdinalIgnoreCase) &&
                        !AssetDatabase.IsValidFolder(next))
                    {
                        message = "Could not create portal preset folder '" + next + "'.";
                        return false;
                    }
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(normalized);
        }

        private static bool CacheIsAlive(
            IReadOnlyList<DimensionPortalVisualProfileAsset> profiles)
        {
            if (profiles == null)
            {
                return false;
            }

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareProfiles(
            DimensionPortalVisualProfileAsset left,
            DimensionPortalVisualProfileAsset right)
        {
            int nameOrder = string.Compare(
                left == null ? string.Empty : left.name,
                right == null ? string.Empty : right.name,
                StringComparison.OrdinalIgnoreCase);
            if (nameOrder != 0)
            {
                return nameOrder;
            }

            return string.Compare(
                NormalizeAssetPath(left == null ? string.Empty : AssetDatabase.GetAssetPath(left)),
                NormalizeAssetPath(right == null ? string.Empty : AssetDatabase.GetAssetPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetReferencePropertyName(DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "portalFrameSpriteAsset";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "chargeWaveSpriteAsset";
                case DimensionPortalArtworkLayer.Milestones:
                    return "milestoneSpriteAsset";
                case DimensionPortalArtworkLayer.Center:
                    return "centerEffectSpriteAsset";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            }
        }

        private static string GetLayerDisplayName(DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "Frame";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "Charge Sweep";
                case DimensionPortalArtworkLayer.Milestones:
                    return "Milestones";
                case DimensionPortalArtworkLayer.Center:
                    return "Center";
                default:
                    return layer.ToString();
            }
        }

        private static void AppendRollbackMessage(ref string message, string rollbackMessage)
        {
            if (string.IsNullOrEmpty(rollbackMessage))
            {
                return;
            }

            message += " Rollback warning: " + rollbackMessage + ".";
        }

        private static string GetProfileDisplayName(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out DimensionPortalPackageAsset package,
                    out _) &&
                package != null &&
                !string.IsNullOrWhiteSpace(package.DisplayName))
            {
                return package.DisplayName.Trim();
            }

            return string.IsNullOrWhiteSpace(profile.name)
                ? string.Empty
                : profile.name.Trim();
        }

        private static string GetUniqueDisplayName(
            DimensionTemplateAsset template,
            string requestedName,
            string fallback)
        {
            string baseName = SanitizeName(requestedName, fallback);
            HashSet<string> existingNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<DimensionPortalVisualProfileAsset> profiles =
                GetPresets(template);
            for (int i = 0; i < profiles.Count; i++)
            {
                string displayName = GetProfileDisplayName(profiles[i]);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    existingNames.Add(displayName.Trim());
                }
            }

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = baseName + " " + suffix;
                suffix++;
            }
            while (existingNames.Contains(candidate));

            return candidate;
        }

        private static string SanitizeName(string requestedName, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(requestedName)
                ? fallback
                : requestedName.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                value = value.Replace(invalid[i], '_');
            }

            value = value.Replace('/', '_').Replace('\\', '_').Trim();
            return string.IsNullOrEmpty(value) ? "Portal Preset" : value;
        }

        private static bool AssetPathIsWithin(string path, string folder)
        {
            string normalizedPath = NormalizeAssetPath(path);
            string normalizedFolder = NormalizeAssetPath(folder);
            return string.Equals(
                       normalizedPath,
                       normalizedFolder,
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(
                       normalizedFolder + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static void InvalidateAll()
        {
            PresetCaches.Clear();
        }
    }
}
