using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal static class DimensionApiModFolderUtility
    {
        private const string ChosenModKey = "PugMod/Window/UploadChosenMod";
        private const string DimensionAssetFolderName = "DimensionAssets";
        private const string FallbackRootFolder = "Assets/DimensionAssets";
        private static List<ModBuilderSettings> cachedModSettings;

        static DimensionApiModFolderUtility()
        {
            EditorApplication.projectChanged += InvalidateModSettingsCache;
        }

        public static string ResolvePreferredDimensionAssetFolder()
        {
            string selectedModRoot = ResolveSelectedModRootFolder();
            if (!string.IsNullOrEmpty(selectedModRoot))
            {
                return NormalizeDimensionAssetFolder(selectedModRoot);
            }

            ModBuilderSettings settings = ResolveSettingsFromSelection();
            if (settings == null)
            {
                settings = ResolveSettingsFromChosenMod();
            }

            if (settings == null)
            {
                settings = ResolveSingleNonFrameworkSettings();
            }

            string modRoot = GetModRoot(settings);
            if (!string.IsNullOrEmpty(modRoot))
            {
                return NormalizeFolder(modRoot + "/" + DimensionAssetFolderName);
            }

            string chosenModFolder = ResolveChosenModAssetFolder();
            if (!string.IsNullOrEmpty(chosenModFolder))
            {
                return NormalizeDimensionAssetFolder(chosenModFolder);
            }

            return FallbackRootFolder;
        }

        public static string ResolveActiveModDisplayName()
        {
            string selectedModRoot = ResolveSelectedModRootFolder();
            string selectedModFolderName = ResolveModFolderNameFromAssetFolder(selectedModRoot);
            if (!string.IsNullOrEmpty(selectedModFolderName))
            {
                return selectedModFolderName;
            }

            ModBuilderSettings settings = ResolveSettingsFromSelection();
            if (settings == null)
            {
                settings = ResolveSettingsFromChosenMod();
            }

            if (settings != null &&
                !string.IsNullOrEmpty(settings.metadata.name))
            {
                return settings.metadata.name;
            }

            string chosenModFolder = ResolveChosenModAssetFolder();
            string chosenModFolderName = ResolveModFolderNameFromAssetFolder(chosenModFolder);
            if (!string.IsNullOrEmpty(chosenModFolderName))
            {
                return chosenModFolderName;
            }

            return string.Empty;
        }

        public static string ResolveModDisplayNameForAssetFolder(string assetFolder)
        {
            string normalizedFolder = NormalizeFolder(assetFolder);
            if (string.IsNullOrEmpty(normalizedFolder))
            {
                return string.Empty;
            }

            ModBuilderSettings bestMatch = null;
            int bestLength = -1;
            List<ModBuilderSettings> settings = FindAllModSettings();
            for (int i = 0; i < settings.Count; i++)
            {
                string modRoot = GetModRoot(settings[i]);
                if (string.IsNullOrEmpty(modRoot))
                {
                    continue;
                }

                bool matches =
                    normalizedFolder == modRoot ||
                    normalizedFolder.StartsWith(modRoot + "/");
                if (matches && modRoot.Length > bestLength)
                {
                    bestMatch = settings[i];
                    bestLength = modRoot.Length;
                }
            }

            if (bestMatch != null &&
                !string.IsNullOrEmpty(bestMatch.metadata.name))
            {
                return bestMatch.metadata.name;
            }

            return ResolveModFolderNameFromAssetFolder(normalizedFolder);
        }

        public static ModBuilderSettings ResolveModSettingsForAssetPath(string assetPath)
        {
            string normalizedPath = NormalizeFolder(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return null;
            }

            ModBuilderSettings bestMatch = null;
            int bestLength = -1;
            List<ModBuilderSettings> settings = FindAllModSettings();
            for (int i = 0; i < settings.Count; i++)
            {
                string modRoot = GetModRoot(settings[i]);
                if (string.IsNullOrEmpty(modRoot))
                {
                    continue;
                }

                bool matches =
                    normalizedPath == modRoot ||
                    normalizedPath.StartsWith(modRoot + "/");
                if (matches && modRoot.Length > bestLength)
                {
                    bestMatch = settings[i];
                    bestLength = modRoot.Length;
                }
            }

            return bestMatch;
        }

        public static string ResolveModRootFolderForAssetPath(string assetPath)
        {
            ModBuilderSettings settings = ResolveModSettingsForAssetPath(assetPath);
            string settingsRoot = ResolveModRootFolder(settings);
            if (!string.IsNullOrEmpty(settingsRoot))
            {
                return settingsRoot;
            }

            string normalizedPath = NormalizeFolder(assetPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return string.Empty;
            }

            if (!AssetDatabase.IsValidFolder(normalizedPath))
            {
                int slash = normalizedPath.LastIndexOf('/');
                normalizedPath = slash <= 0 ? string.Empty : normalizedPath.Substring(0, slash);
            }

            return ResolveModRootFolderFromAssetFolder(normalizedPath);
        }

        public static string ResolveModRootFolder(ModBuilderSettings settings)
        {
            return GetModRoot(settings);
        }

        public static string NormalizeDimensionAssetFolder(string folder)
        {
            string normalized = NormalizeFolder(folder);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            string nestedDimensionAssetFolder = "/" + DimensionAssetFolderName + "/";
            int nestedIndex = normalized.IndexOf(nestedDimensionAssetFolder);
            if (nestedIndex >= 0)
            {
                return normalized.Substring(
                    0,
                    nestedIndex + 1 + DimensionAssetFolderName.Length);
            }

            if (normalized.EndsWith("/" + DimensionAssetFolderName) ||
                normalized == "Assets/" + DimensionAssetFolderName)
            {
                return normalized;
            }

            return NormalizeFolder(normalized + "/" + DimensionAssetFolderName);
        }

        public static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            string normalized = folder.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        public static string ConvertAbsoluteFolderToAssetFolder(string absoluteFolder)
        {
            if (string.IsNullOrEmpty(absoluteFolder))
            {
                return string.Empty;
            }

            string normalizedFolder = NormalizeFolder(absoluteFolder);
            string assetsRoot = NormalizeFolder(Application.dataPath);
            if (normalizedFolder == assetsRoot)
            {
                return "Assets";
            }

            string assetsRootPrefix = assetsRoot + "/";
            if (normalizedFolder.StartsWith(assetsRootPrefix))
            {
                return "Assets/" + normalizedFolder.Substring(assetsRootPrefix.Length);
            }

            return string.Empty;
        }

        private static ModBuilderSettings ResolveSettingsFromSelection()
        {
            Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return null;
            }

            ModBuilderSettings selectedSettings = selectedObject as ModBuilderSettings;
            if (selectedSettings != null)
            {
                return selectedSettings;
            }

            string selectionPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrEmpty(selectionPath))
            {
                return null;
            }

            selectionPath = NormalizeFolder(selectionPath);
            ModBuilderSettings bestMatch = null;
            int bestLength = -1;
            List<ModBuilderSettings> settings = FindAllModSettings();
            for (int i = 0; i < settings.Count; i++)
            {
                string modRoot = GetModRoot(settings[i]);
                if (string.IsNullOrEmpty(modRoot))
                {
                    continue;
                }

                bool matches =
                    selectionPath == modRoot ||
                    selectionPath.StartsWith(modRoot + "/");
                if (matches && modRoot.Length > bestLength)
                {
                    bestMatch = settings[i];
                    bestLength = modRoot.Length;
                }
            }

            return bestMatch;
        }

        private static ModBuilderSettings ResolveSettingsFromChosenMod()
        {
            string chosenMod = EditorPrefs.GetString(ChosenModKey, string.Empty);
            if (string.IsNullOrEmpty(chosenMod))
            {
                return null;
            }

            List<ModBuilderSettings> settings = FindAllModSettings();
            for (int i = 0; i < settings.Count; i++)
            {
                ModBuilderSettings item = settings[i];
                if (item == null)
                {
                    continue;
                }

                if (item.metadata.name == chosenMod)
                {
                    return item;
                }
            }

            return null;
        }

        private static string ResolveChosenModAssetFolder()
        {
            string chosenMod = EditorPrefs.GetString(ChosenModKey, string.Empty);
            if (string.IsNullOrEmpty(chosenMod))
            {
                return string.Empty;
            }

            string sanitized = chosenMod.Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(sanitized) ||
                sanitized.IndexOf('/') >= 0 ||
                sanitized.IndexOf(':') >= 0)
            {
                return string.Empty;
            }

            string candidate = "Assets/" + sanitized;
            return AssetDatabase.IsValidFolder(candidate)
                ? candidate
                : string.Empty;
        }

        private static ModBuilderSettings ResolveSingleNonFrameworkSettings()
        {
            List<ModBuilderSettings> settings = FindAllModSettings();
            ModBuilderSettings single = null;
            for (int i = 0; i < settings.Count; i++)
            {
                ModBuilderSettings item = settings[i];
                if (item == null || IsFrameworkSettings(item))
                {
                    continue;
                }

                if (single != null)
                {
                    return null;
                }

                single = item;
            }

            return single;
        }

        private static bool IsFrameworkSettings(ModBuilderSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            string root = GetModRoot(settings);
            if (root.Contains("Assets/ExpandNullforge"))
            {
                return true;
            }

            return settings.metadata.name == "ExpandNullforge";
        }

        private static string ResolveSelectedAssetFolder()
        {
            string guidFolder = ResolveSelectedAssetFolderFromGuids();
            if (!string.IsNullOrEmpty(guidFolder))
            {
                return guidFolder;
            }

            Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return string.Empty;
            }

            string path = NormalizeFolder(AssetDatabase.GetAssetPath(selectedObject));
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            int lastSlash = path.LastIndexOf('/');
            return lastSlash <= 0 ? string.Empty : path.Substring(0, lastSlash);
        }

        private static string ResolveSelectedAssetFolderFromGuids()
        {
            string[] assetGuids = Selection.assetGUIDs;
            if (assetGuids == null || assetGuids.Length == 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < assetGuids.Length; i++)
            {
                string path = NormalizeFolder(AssetDatabase.GUIDToAssetPath(assetGuids[i]));
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }

                int lastSlash = path.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    return path.Substring(0, lastSlash);
                }
            }

            return string.Empty;
        }

        private static string ResolveSelectedModRootFolder()
        {
            string selectedFolder = ResolveSelectedAssetFolder();
            string root = ResolveModRootFolderFromAssetFolder(selectedFolder);
            if (string.IsNullOrEmpty(root) ||
                root == "Assets/ExpandNullforge")
            {
                return string.Empty;
            }

            return root;
        }

        private static string ResolveModRootFolderFromAssetFolder(string assetFolder)
        {
            string normalized = NormalizeFolder(assetFolder);
            const string assetsPrefix = "Assets/";
            if (!normalized.StartsWith(assetsPrefix))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(assetsPrefix.Length);
            int slash = relative.IndexOf('/');
            string rootFolder = slash < 0 ? relative : relative.Substring(0, slash);
            if (string.IsNullOrEmpty(rootFolder) ||
                rootFolder == DimensionAssetFolderName)
            {
                return string.Empty;
            }

            return "Assets/" + rootFolder;
        }

        private static string ResolveModFolderNameFromAssetFolder(string assetFolder)
        {
            string normalized = NormalizeFolder(assetFolder);
            const string assetsPrefix = "Assets/";
            if (!normalized.StartsWith(assetsPrefix))
            {
                return string.Empty;
            }

            string relative = normalized.Substring(assetsPrefix.Length);
            int slash = relative.IndexOf('/');
            string rootFolder = slash < 0 ? relative : relative.Substring(0, slash);
            if (string.IsNullOrEmpty(rootFolder) ||
                rootFolder == DimensionAssetFolderName)
            {
                return string.Empty;
            }

            return rootFolder;
        }

        private static string GetModRoot(ModBuilderSettings settings)
        {
            if (settings == null)
            {
                return string.Empty;
            }

            string root = NormalizeFolder(settings.modPath);
            if (!string.IsNullOrEmpty(root))
            {
                return root;
            }

            string assetPath = NormalizeFolder(AssetDatabase.GetAssetPath(settings));
            int lastSlash = assetPath.LastIndexOf('/');
            return lastSlash <= 0 ? string.Empty : assetPath.Substring(0, lastSlash);
        }

        private static List<ModBuilderSettings> FindAllModSettings()
        {
            if (cachedModSettings != null)
            {
                bool cacheIsValid = true;
                for (int i = 0; i < cachedModSettings.Count; i++)
                {
                    if (cachedModSettings[i] == null)
                    {
                        cacheIsValid = false;
                        break;
                    }
                }

                if (cacheIsValid)
                {
                    return cachedModSettings;
                }
            }

            List<string> guids = new List<string>();
            AddGuids(guids, AssetDatabase.FindAssets("t:ModBuilderSettings"));
            AddGuids(guids, AssetDatabase.FindAssets("t:PugMod.ModBuilderSettings"));

            List<ModBuilderSettings> settings = new List<ModBuilderSettings>();
            for (int i = 0; i < guids.Count; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ModBuilderSettings item =
                    AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(path);
                if (item != null && !settings.Contains(item))
                {
                    settings.Add(item);
                }
            }

            cachedModSettings = settings;
            return cachedModSettings;
        }

        private static void InvalidateModSettingsCache()
        {
            cachedModSettings = null;
        }

        private static void AddGuids(List<string> target, string[] source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                if (!string.IsNullOrEmpty(source[i]) &&
                    !target.Contains(source[i]))
                {
                    target.Add(source[i]);
                }
            }
        }
    }
}
