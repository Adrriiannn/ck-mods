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
    internal sealed class DimensionPortalProfileEditSession : IDisposable
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

        private static Dictionary<string, byte[]> CaptureFileSet(string absoluteRoot)
        {
            Dictionary<string, byte[]> files =
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(absoluteRoot) || !Directory.Exists(absoluteRoot))
            {
                return files;
            }

            string root = EnsureTrailingSeparator(Path.GetFullPath(absoluteRoot));
            Stack<string> pending = new Stack<string>();
            pending.Push(Path.GetFullPath(absoluteRoot));
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        "Portal package snapshots do not follow reparse-point directories: " +
                        directory + ".");
                }

                string[] childDirectories = Directory.GetDirectories(directory);
                for (int i = 0; i < childDirectories.Length; i++)
                {
                    string child = Path.GetFullPath(childDirectories[i]);
                    EnsureAbsolutePathWithinRoot(child, root);
                    pending.Push(child);
                }

                string[] childFiles = Directory.GetFiles(directory);
                for (int i = 0; i < childFiles.Length; i++)
                {
                    string file = Path.GetFullPath(childFiles[i]);
                    EnsureAbsolutePathWithinRoot(file, root);
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException(
                            "Portal package snapshots do not follow reparse-point files: " +
                            file + ".");
                    }

                    string relative = NormalizeRelativePath(file.Substring(root.Length));
                    files.Add(relative, File.ReadAllBytes(file));
                }
            }

            return files;
        }

        private static void RestoreFileSet(
            string absoluteRoot,
            Dictionary<string, byte[]> desiredFiles)
        {
            if (desiredFiles == null)
            {
                throw new ArgumentNullException(nameof(desiredFiles));
            }

            Directory.CreateDirectory(absoluteRoot);
            string root = EnsureTrailingSeparator(Path.GetFullPath(absoluteRoot));
            Dictionary<string, byte[]> currentFiles = CaptureFileSet(absoluteRoot);
            foreach (string relativePath in currentFiles.Keys)
            {
                if (desiredFiles.ContainsKey(relativePath))
                {
                    continue;
                }

                string absolutePath = ResolveSnapshotPath(root, relativePath);
                File.Delete(absolutePath);
            }

            foreach (KeyValuePair<string, byte[]> pair in desiredFiles)
            {
                string absolutePath = ResolveSnapshotPath(root, pair.Key);
                string directory = Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(absolutePath) ||
                    !ByteArraysEqual(File.ReadAllBytes(absolutePath), pair.Value))
                {
                    File.WriteAllBytes(absolutePath, pair.Value ?? Array.Empty<byte>());
                }
            }

            RemoveEmptyChildDirectories(absoluteRoot);
        }

        private static void RemoveEmptyChildDirectories(string absoluteRoot)
        {
            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }

            string root = EnsureTrailingSeparator(Path.GetFullPath(absoluteRoot));
            List<string> directories = new List<string>(
                Directory.GetDirectories(absoluteRoot, "*", SearchOption.AllDirectories));
            directories.Sort((left, right) => right.Length.CompareTo(left.Length));
            for (int i = 0; i < directories.Count; i++)
            {
                string directory = Path.GetFullPath(directories[i]);
                EnsureAbsolutePathWithinRoot(directory, root);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        "Portal package cleanup does not follow reparse points: " +
                        directory + ".");
                }

                if (Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
        }

        private static string ResolveSnapshotPath(string root, string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            if (string.IsNullOrEmpty(normalized) ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.IndexOf("/../", StringComparison.Ordinal) >= 0 ||
                Path.IsPathRooted(normalized))
            {
                throw new IOException("Portal package snapshot contains an unsafe path.");
            }

            string absolute = Path.GetFullPath(
                Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            EnsureAbsolutePathWithinRoot(absolute, root);
            return absolute;
        }

        private static void EnsureAbsolutePathWithinRoot(string path, string root)
        {
            string fullPath = Path.GetFullPath(path);
            string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Portal package operation escaped its owned folder: " + fullPath + ".");
            }
        }

        private static bool TryAssetPathToAbsolutePath(
            string assetPath,
            out string absolutePath,
            out string message)
        {
            absolutePath = string.Empty;
            message = string.Empty;
            string normalized = NormalizeAssetPath(assetPath);
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                message = "Portal packages must be stored inside the Unity Assets folder.";
                return false;
            }

            string projectRoot = Path.GetFullPath(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty);
            string candidate = Path.GetFullPath(
                Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRoot = EnsureTrailingSeparator(Path.GetFullPath(Application.dataPath));
            if (!candidate.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                message = "The portal package path escapes the Unity Assets folder.";
                return false;
            }

            absolutePath = candidate;
            return true;
        }

        private static byte[] ReadRequiredAssetBytes(string assetPath)
        {
            if (!TryAssetPathToAbsolutePath(assetPath, out string absolutePath, out string message))
            {
                throw new IOException(message);
            }

            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "The tracked portal asset is missing.",
                    absolutePath);
            }

            return File.ReadAllBytes(absolutePath);
        }

        private static void WriteAssetBytes(string assetPath, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new IOException("The portal asset baseline is missing.");
            }

            if (!TryAssetPathToAbsolutePath(
                    assetPath,
                    out string absolutePath,
                    out string message))
            {
                throw new IOException(message);
            }

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absolutePath, bytes);
        }

        private static bool FileSetsEqual(
            Dictionary<string, byte[]> left,
            Dictionary<string, byte[]> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, byte[]> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out byte[] other) ||
                    !ByteArraysEqual(pair.Value, other))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AssetIdentityMatches(UnityEngine.Object asset, string expectedGuid)
        {
            return asset != null &&
                   !string.IsNullOrEmpty(expectedGuid) &&
                   string.Equals(GetAssetGuid(asset), expectedGuid, StringComparison.Ordinal);
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            string path = NormalizeAssetPath(
                asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset));
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
        }

        private static bool AssetPathIsWithin(string path, string folder)
        {
            string normalizedPath = NormalizeAssetPath(path);
            string normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/');
            return !string.IsNullOrEmpty(normalizedPath) &&
                   !string.IsNullOrEmpty(normalizedFolder) &&
                   (AssetPathsEqual(normalizedPath, normalizedFolder) ||
                    normalizedPath.StartsWith(
                        normalizedFolder + "/",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static bool AssetPathsEqual(string left, string right)
        {
            return string.Equals(
                NormalizeAssetPath(left).TrimEnd('/'),
                NormalizeAssetPath(right).TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineAssetPath(string folder, string relativePath)
        {
            return NormalizeAssetPath(folder).TrimEnd('/') + "/" +
                   NormalizeRelativePath(relativePath);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static string NormalizeRelativePath(string path)
        {
            return NormalizeAssetPath(path).TrimStart('/');
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Path.DirectorySeparatorChar.ToString();
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
