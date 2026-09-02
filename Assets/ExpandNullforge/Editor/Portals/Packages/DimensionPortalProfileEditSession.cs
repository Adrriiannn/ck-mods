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
    /// Owns one Portal Studio editing baseline. Package snapshots are intentionally scoped
    /// to the selected consumer-owned portal folder; the mod-wide SpriteAsset manifest is
    /// reconciled entry-by-entry and is never restored wholesale.
    /// </summary>
    internal sealed partial class DimensionPortalProfileEditSession : IDisposable
    {
        private readonly Dictionary<string, byte[]> baselinePackageFiles =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private DimensionTemplateAsset template;
        private DimensionPortalVisualProfileAsset profile;
        private string templateGuid = string.Empty;
        private string profileGuid = string.Empty;
        private string profilePath = string.Empty;
        private string modRoot = string.Empty;
        private string packageFolder = string.Empty;
        private string packageAbsoluteFolder = string.Empty;
        private string baselineProfileJson = string.Empty;
        private byte[] baselineLegacyProfileBytes;
        private float baselineChargeDuration;
        private bool isPackageSession;
        private bool disposed;
        private int changeRevision;
        private int evaluatedRevision;
        private bool cachedHasChanges;

        public DimensionPortalVisualProfileAsset Profile
        {
            get { return profile; }
        }

        public bool HasChanges
        {
            get
            {
                if (disposed || template == null || profile == null)
                {
                    return false;
                }

                if (evaluatedRevision == changeRevision)
                {
                    return cachedHasChanges;
                }

                evaluatedRevision = changeRevision;
                try
                {
                    cachedHasChanges = EvaluateHasChanges();
                }
                catch (Exception)
                {
                    // A missing or unreadable tracked file must never make the Studio look
                    // clean. Discard can then surface the actionable filesystem error.
                    cachedHasChanges = true;
                }

                return cachedHasChanges;
            }
        }

        public bool IsBoundTo(
            DimensionTemplateAsset candidateTemplate,
            DimensionPortalVisualProfileAsset candidateProfile)
        {
            if (disposed || candidateTemplate == null || candidateProfile == null ||
                template == null || profile == null)
            {
                return false;
            }

            return AssetIdentityMatches(candidateTemplate, templateGuid) &&
                   AssetIdentityMatches(candidateProfile, profileGuid);
        }

        public bool Begin(
            DimensionTemplateAsset candidateTemplate,
            DimensionPortalVisualProfileAsset candidateProfile,
            out string message)
        {
            message = string.Empty;
            if (disposed)
            {
                message = "The portal profile editing session has already been disposed.";
                return false;
            }

            if (candidateTemplate == null || candidateProfile == null)
            {
                message = "Select a saved Dimension Asset and portal profile before editing.";
                return false;
            }

            if (IsBoundTo(candidateTemplate, candidateProfile))
            {
                return true;
            }

            if (template != null && profile != null && HasChanges)
            {
                message =
                    "Save or discard the current portal profile changes before editing another profile.";
                return false;
            }

            ClearBaseline();
            template = candidateTemplate;
            profile = candidateProfile;
            templateGuid = GetAssetGuid(candidateTemplate);
            profileGuid = GetAssetGuid(candidateProfile);
            profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(candidateProfile));
            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(candidateTemplate));
            if (string.IsNullOrEmpty(templateGuid) || string.IsNullOrEmpty(profileGuid) ||
                string.IsNullOrEmpty(templatePath) || string.IsNullOrEmpty(profilePath))
            {
                message = "The Dimension Asset and portal profile must both be saved project assets.";
                ClearBaseline();
                return false;
            }

            string templateRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            string profileRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(profilePath));
            if (string.IsNullOrEmpty(templateRoot) ||
                !AssetPathsEqual(templateRoot, profileRoot) ||
                AssetPathsEqual(templateRoot, "Assets/ExpandNullforge"))
            {
                message =
                    "Portal editing sessions require a profile owned by the selected consumer mod.";
                ClearBaseline();
                return false;
            }

            modRoot = templateRoot;
            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    candidateProfile,
                    out DimensionPortalPackageAsset package,
                    out string resolvedPackageFolder))
            {
                if (!DimensionPortalPackageEditorUtility.IsPackageOwnedByTemplate(
                        candidateTemplate,
                        package))
                {
                    message = "The selected portal package does not belong to this Dimension Asset.";
                    ClearBaseline();
                    return false;
                }

                isPackageSession = true;
                packageFolder = NormalizeAssetPath(resolvedPackageFolder);
                if (!TryAssetPathToAbsolutePath(
                        packageFolder,
                        out packageAbsoluteFolder,
                        out message))
                {
                    ClearBaseline();
                    return false;
                }
            }

            try
            {
                CaptureBaseline();
                ResetChangeTracking();
                message = isPackageSession
                    ? "Portal package editing baseline captured."
                    : "Legacy portal profile editing baseline captured.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not capture the portal editing baseline. " + exception.Message;
                ClearBaseline();
                return false;
            }
        }

        public void MarkChanged()
        {
            if (disposed || template == null || profile == null)
            {
                return;
            }

            unchecked
            {
                changeRevision++;
                if (changeRevision == int.MinValue)
                {
                    changeRevision = 1;
                    evaluatedRevision = 0;
                }
            }
        }

        public bool Commit(out string message)
        {
            message = string.Empty;
            if (!ValidateLiveSession(out message))
            {
                return false;
            }

            try
            {
                SaveTrackedAssets();
                CaptureBaseline();
                ResetChangeTracking();
                message = isPackageSession
                    ? "Portal package changes accepted as the new editing baseline."
                    : "Legacy portal profile changes accepted as the new editing baseline.";
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not commit the portal editing baseline. " + exception.Message;
                return false;
            }
        }

        public bool Discard(out string message)
        {
            message = string.Empty;
            if (!ValidateLiveSession(out message))
            {
                return false;
            }

            if (!HasChanges)
            {
                message = "The portal profile has no changes to discard.";
                return true;
            }

            Dictionary<string, byte[]> currentPackageFiles = null;
            byte[] currentLegacyProfileBytes = null;
            string currentProfileJson = EditorJsonUtility.ToJson(profile, false);
            float currentChargeDuration = template.PortalActivationChargeSeconds;
            try
            {
                if (isPackageSession)
                {
                    currentPackageFiles = CaptureFileSet(packageAbsoluteFolder);
                }
                else
                {
                    currentLegacyProfileBytes = ReadRequiredAssetBytes(profilePath);
                }

                RestoreBaselineFiles();
                RestoreChargeDuration(baselineChargeDuration);
                ReloadProfileFromDisk(baselineProfileJson, false);
                if (isPackageSession)
                {
                    AddPackageSpriteAssetsToManifest();
                }

                Undo.ClearUndo(profile);
                InvalidatePortalCaches();

                // Importers may normalize the profile YAML while restoring its semantic
                // JSON. Capture that normalized clean state so HasChanges remains false.
                CaptureBaseline();
                ResetChangeTracking();
                message = isPackageSession
                    ? "Discarded the portal changes and restored the complete package."
                    : "Discarded the legacy portal profile changes.";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = TryRestoreDiscardInput(
                    currentPackageFiles,
                    currentLegacyProfileBytes,
                    currentProfileJson,
                    currentChargeDuration);
                MarkChanged();
                cachedHasChanges = true;
                evaluatedRevision = changeRevision;
                message = "Could not discard the portal changes. " + exception.Message;
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    message += " Rollback warning: " + rollbackError + ".";
                }

                return false;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ClearBaseline();
        }

        private bool EvaluateHasChanges()
        {
            if (!string.Equals(
                    EditorJsonUtility.ToJson(profile, false),
                    baselineProfileJson,
                    StringComparison.Ordinal) ||
                !Mathf.Approximately(
                    template.PortalActivationChargeSeconds,
                    baselineChargeDuration))
            {
                return true;
            }

            if (isPackageSession)
            {
                Dictionary<string, byte[]> current = CaptureFileSet(packageAbsoluteFolder);
                return !FileSetsEqual(current, baselinePackageFiles);
            }

            return !ByteArraysEqual(
                ReadRequiredAssetBytes(profilePath),
                baselineLegacyProfileBytes);
        }

        private void CaptureBaseline()
        {
            baselineProfileJson = EditorJsonUtility.ToJson(profile, false);
            baselineChargeDuration = template.PortalActivationChargeSeconds;
            baselinePackageFiles.Clear();
            baselineLegacyProfileBytes = null;
            if (isPackageSession)
            {
                Dictionary<string, byte[]> files = CaptureFileSet(packageAbsoluteFolder);
                foreach (KeyValuePair<string, byte[]> pair in files)
                {
                    baselinePackageFiles.Add(pair.Key, pair.Value);
                }
            }
            else
            {
                baselineLegacyProfileBytes = ReadRequiredAssetBytes(profilePath);
            }
        }

        private void SaveTrackedAssets()
        {
            if (isPackageSession)
            {
                Dictionary<string, byte[]> files = CaptureFileSet(packageAbsoluteFolder);
                foreach (string relativePath in files.Keys)
                {
                    if (!relativePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(
                        CombineAssetPath(packageFolder, relativePath));
                    if (asset != null)
                    {
                        AssetDatabase.SaveAssetIfDirty(asset);
                    }
                }
            }
            else
            {
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            AssetDatabase.SaveAssetIfDirty(template);
            SpriteAssetManifest manifest = AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                modRoot + "/SpriteAssetManifest.asset");
            if (manifest != null)
            {
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
        }

        private void RestoreBaselineFiles()
        {
            if (!isPackageSession)
            {
                WriteAssetBytes(profilePath, baselineLegacyProfileBytes);
                AssetDatabase.ImportAsset(
                    profilePath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                return;
            }

            RemovePackageEntriesFromManifest();
            RestoreFileSet(packageAbsoluteFolder, baselinePackageFiles);
            RefreshRestoredPackage(baselinePackageFiles);
        }

        private string TryRestoreDiscardInput(
            Dictionary<string, byte[]> currentPackageFiles,
            byte[] currentLegacyProfileBytes,
            string currentProfileJson,
            float currentChargeDuration)
        {
            List<string> errors = new List<string>();
            try
            {
                if (isPackageSession && currentPackageFiles != null)
                {
                    RemovePackageEntriesFromManifest();
                    RestoreFileSet(packageAbsoluteFolder, currentPackageFiles);
                    RefreshRestoredPackage(currentPackageFiles);
                    AddPackageSpriteAssetsToManifest();
                }
                else if (!isPackageSession && currentLegacyProfileBytes != null)
                {
                    WriteAssetBytes(profilePath, currentLegacyProfileBytes);
                    AssetDatabase.ImportAsset(
                        profilePath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
            }
            catch (Exception exception)
            {
                errors.Add("restore edited package files: " + exception.Message);
            }

            try
            {
                RestoreChargeDuration(currentChargeDuration);
            }
            catch (Exception exception)
            {
                errors.Add("restore edited charge duration: " + exception.Message);
            }

            try
            {
                ReloadProfileFromDisk(currentProfileJson, true);
            }
            catch (Exception exception)
            {
                errors.Add("restore edited profile state: " + exception.Message);
            }

            try
            {
                InvalidatePortalCaches();
            }
            catch (Exception exception)
            {
                errors.Add("invalidate restored editor caches: " + exception.Message);
            }

            return errors.Count == 0 ? string.Empty : string.Join("; ", errors);
        }

        private void ReloadProfileFromDisk(string expectedJson, bool keepDirty)
        {
            AssetDatabase.ImportAsset(
                profilePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            DimensionPortalVisualProfileAsset reloaded =
                AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(profilePath);
            if (reloaded == null)
            {
                throw new InvalidOperationException(
                    "The restored portal profile could not be loaded at " + profilePath + ".");
            }

            string reloadedJson = EditorJsonUtility.ToJson(reloaded, false);
            if (!string.Equals(reloadedJson, expectedJson, StringComparison.Ordinal))
            {
                EditorJsonUtility.FromJsonOverwrite(expectedJson, reloaded);
                EditorUtility.SetDirty(reloaded);
                if (!keepDirty)
                {
                    AssetDatabase.SaveAssetIfDirty(reloaded);
                }
            }

            profile = reloaded;
        }

        private void RestoreChargeDuration(float value)
        {
            SerializedObject serializedTemplate = new SerializedObject(template);
            serializedTemplate.UpdateIfRequiredOrScript();
            SerializedProperty charge = serializedTemplate.FindProperty(
                "portalActivationChargeSeconds");
            if (charge == null)
            {
                throw new InvalidOperationException(
                    "The Dimension Asset no longer exposes portal activation duration.");
            }

            if (Mathf.Approximately(charge.floatValue, value))
            {
                return;
            }

            charge.floatValue = Mathf.Max(0f, value);
            serializedTemplate.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssetIfDirty(template);
        }

        private void RemovePackageEntriesFromManifest()
        {
            SpriteAssetManifest manifest = AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                modRoot + "/SpriteAssetManifest.asset");
            if (manifest == null || manifest.spriteAssets == null)
            {
                return;
            }

            bool changed = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase asset = manifest.spriteAssets[i];
                if (asset == null)
                {
                    // A null entry has no provable ownership path. Preserve it rather than
                    // accidentally rewriting another package's manifest state.
                    continue;
                }

                string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                if (AssetPathIsWithin(path, packageFolder))
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
        }

        private void AddPackageSpriteAssetsToManifest()
        {
            List<SpriteAssetBase> packageAssets = new List<SpriteAssetBase>();
            Dictionary<string, byte[]> files = CaptureFileSet(packageAbsoluteFolder);
            foreach (string relativePath in files.Keys)
            {
                if (!relativePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SpriteAssetBase asset = AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(
                    CombineAssetPath(packageFolder, relativePath));
                if (asset != null && !packageAssets.Contains(asset))
                {
                    packageAssets.Add(asset);
                }
            }

            if (packageAssets.Count == 0)
            {
                return;
            }

            string manifestPath = modRoot + "/SpriteAssetManifest.asset";
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

            bool changed = false;
            for (int i = 0; i < packageAssets.Count; i++)
            {
                if (!manifest.spriteAssets.Contains(packageAssets[i]))
                {
                    manifest.spriteAssets.Add(packageAssets[i]);
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
        }

        private void RefreshRestoredPackage(Dictionary<string, byte[]> fileSet)
        {
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            foreach (string relativePath in fileSet.Keys)
            {
                if (relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assetPath = CombineAssetPath(packageFolder, relativePath);
                if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private void InvalidatePortalCaches()
        {
            DimensionPortalPresetEditorUtility.Invalidate(template);
            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
        }

        private bool ValidateLiveSession(out string message)
        {
            message = string.Empty;
            if (disposed || template == null || profile == null)
            {
                message = "No portal profile editing session is active.";
                return false;
            }

            if (!AssetIdentityMatches(template, templateGuid) ||
                !AssetIdentityMatches(profile, profileGuid))
            {
                message = "The tracked Dimension Asset or portal profile was replaced.";
                return false;
            }

            if (isPackageSession &&
                (!AssetPathIsWithin(profilePath, packageFolder) ||
                 string.IsNullOrEmpty(packageAbsoluteFolder)))
            {
                message = "The tracked portal package no longer has a safe owned path.";
                return false;
            }

            return true;
        }

        private void ResetChangeTracking()
        {
            changeRevision = 0;
            evaluatedRevision = 0;
            cachedHasChanges = false;
        }

        private void ClearBaseline()
        {
            baselinePackageFiles.Clear();
            baselineLegacyProfileBytes = null;
            baselineProfileJson = string.Empty;
            baselineChargeDuration = 0f;
            template = null;
            profile = null;
            templateGuid = string.Empty;
            profileGuid = string.Empty;
            profilePath = string.Empty;
            modRoot = string.Empty;
            packageFolder = string.Empty;
            packageAbsoluteFolder = string.Empty;
            isPackageSession = false;
            ResetChangeTracking();
        }
    }
}
