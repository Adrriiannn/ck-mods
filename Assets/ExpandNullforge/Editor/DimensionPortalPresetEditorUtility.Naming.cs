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
    /// What a preset is called, and making sure two of them are not called the same.
    /// </summary>
    internal static partial class DimensionPortalPresetEditorUtility
    {
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
    }
}
