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
    internal static partial class DimensionPortalPresetEditorUtility
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

        private static void InvalidateAll()
        {
            PresetCaches.Clear();
        }
    }
}
