using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionTemplateAssetEditorSaveResult
    {
        public DimensionTemplateAssetEditorSaveResult(
            bool executed,
            int savedCount,
            int skippedCount,
            Object primaryAsset,
            string message)
        {
            Executed = executed;
            SavedCount = savedCount < 0 ? 0 : savedCount;
            SkippedCount = skippedCount < 0 ? 0 : skippedCount;
            PrimaryAsset = primaryAsset;
            Message = message ?? string.Empty;
        }

        public bool Executed { get; private set; }

        public int SavedCount { get; private set; }

        public int SkippedCount { get; private set; }

        public Object PrimaryAsset { get; private set; }

        public string Message { get; private set; }
    }

    internal static class DimensionTemplateAssetEditorSaveUtility
    {
        public static DimensionTemplateAssetEditorSaveResult SaveWorkspace(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            if (workspace == null)
            {
                return Failure("No Dimension Asset workspace exists. Create the workspace first.");
            }

            DimensionTemplateAssetSavePlan savePlan = workspace.SavePlan;
            if (savePlan == null || savePlan.Entries == null || savePlan.Entries.Count == 0)
            {
                return Failure("The workspace has no save plan.");
            }

            string rootFolder = NormalizeAssetPath(savePlan.RootFolder);
            if (!IsAssetFolderPath(rootFolder))
            {
                return Failure("The save root must be inside Assets. Current root: " + rootFolder);
            }

            if (!DimensionAssetFolders.EnsureExists(rootFolder))
            {
                return Failure("Could not create save root folder: " + rootFolder);
            }

            int savedCount = 0;
            int skippedCount = 0;
            Object primaryAsset = null;
            string primaryAssetPath = string.Empty;
            List<string> failures = new List<string>();
            List<string> requiredFolders = CollectRequiredFolders(savePlan);
            for (int i = 0; i < requiredFolders.Count; i++)
            {
                string requiredFolder = requiredFolders[i];
                if (!DimensionAssetFolders.EnsureExists(requiredFolder))
                {
                    return Failure("Dimension folder could not be created: " + requiredFolder);
                }
            }

            AssetDatabase.Refresh();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < savePlan.Entries.Count; i++)
                {
                    DimensionTemplateAssetSavePlanEntry entry = savePlan.Entries[i];
                    Object asset = entry.Asset;
                    if (asset == null)
                    {
                        if (entry.Required)
                        {
                            failures.Add(entry.Role + " is required but missing.");
                        }

                        continue;
                    }

                    if (entry.Role == "Dimension")
                    {
                        primaryAsset = asset;
                        primaryAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                    }

                    if (IsPersistentAsset(asset))
                    {
                        skippedCount++;
                        continue;
                    }

                    string path = NormalizeAssetPath(entry.SuggestedPath);
                    if (!IsAssetFilePath(path))
                    {
                        failures.Add(entry.Role + " has an invalid asset path: " + path);
                        continue;
                    }

                    string folder = GetFolder(path);
                    if (!AssetDatabase.IsValidFolder(folder))
                    {
                        failures.Add(entry.Role + " folder is unavailable: " + folder);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(entry.SuggestedName))
                    {
                        asset.name = entry.SuggestedName;
                    }

                    string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
                    AssetDatabase.CreateAsset(asset, uniquePath);
                    EditorUtility.SetDirty(asset);
                    if (entry.Role == "Dimension")
                    {
                        primaryAssetPath = uniquePath;
                    }

                    savedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failures.Count > 0)
            {
                return new DimensionTemplateAssetEditorSaveResult(
                    false,
                    savedCount,
                    skippedCount,
                    primaryAsset,
                    "Saved " + savedCount + " assets, but " + failures.Count + " save issues remain. " +
                    failures[0]);
            }

            Object resolvedPrimaryAsset = ResolveSavedPrimaryAsset(primaryAsset, primaryAssetPath);
            if (resolvedPrimaryAsset != null)
            {
                primaryAsset = resolvedPrimaryAsset;
                Selection.activeObject = resolvedPrimaryAsset;
                EditorGUIUtility.PingObject(resolvedPrimaryAsset);
            }

            DimensionTemplateAsset template = resolvedPrimaryAsset as DimensionTemplateAsset;
            DimensionRuntimeConsumerBootstrapResult runtimeResult = null;
            if (template != null)
            {
                bool createdPortalVisualProfile;
                string portalVisualProfileMessage;
                DimensionPortalVisualProfileAsset portalVisualProfile =
                    DimensionPortalVisualProfileEditorUtility.EnsureAssigned(
                        template,
                        true,
                        false,
                        out createdPortalVisualProfile,
                        out portalVisualProfileMessage);
                if (portalVisualProfile == null)
                {
                    return new DimensionTemplateAssetEditorSaveResult(
                        false,
                        savedCount,
                        skippedCount,
                        primaryAsset,
                        "Saved " + savedCount + " Dimension Asset files into " + rootFolder +
                        ", but the portal visual profile could not be assigned. " +
                        portalVisualProfileMessage);
                }

                if (createdPortalVisualProfile)
                {
                    savedCount++;
                }

                DimensionTemplateManifestExportPreview preview =
                    DimensionTemplateManifestExportPreviewBuilder.Build(
                        template,
                        true,
                        false,
                        "Generate dimension runtime consumer output.");
                runtimeResult =
                    DimensionRuntimeConsumerBootstrapUtility.EnsureGeneratedRuntime(
                        template,
                        preview);
                if (!runtimeResult.Executed)
                {
                    return new DimensionTemplateAssetEditorSaveResult(
                        false,
                        savedCount,
                        skippedCount,
                        primaryAsset,
                        "Saved " + savedCount + " Dimension Asset files into " + rootFolder +
                        ", but runtime output was not generated. " +
                        runtimeResult.Message);
                }
            }

            return new DimensionTemplateAssetEditorSaveResult(
                true,
                savedCount,
                skippedCount,
                resolvedPrimaryAsset,
                runtimeResult == null
                    ? "Saved " + savedCount + " Dimension Asset files into " + rootFolder + "."
                    : "Saved " + savedCount + " Dimension Asset files into " + rootFolder + ". " +
                      runtimeResult.Message);
        }

        private static Object ResolveSavedPrimaryAsset(Object primaryAsset, string primaryAssetPath)
        {
            string normalizedPath = NormalizeAssetPath(primaryAssetPath);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                Object loadedAsset = AssetDatabase.LoadAssetAtPath<Object>(normalizedPath);
                if (loadedAsset != null)
                {
                    return loadedAsset;
                }
            }

            return primaryAsset;
        }

        private static DimensionTemplateAssetEditorSaveResult Failure(string message)
        {
            return new DimensionTemplateAssetEditorSaveResult(
                false,
                0,
                0,
                null,
                message);
        }

        private static List<string> CollectRequiredFolders(DimensionTemplateAssetSavePlan savePlan)
        {
            List<string> folders = new List<string>();
            if (savePlan == null || savePlan.Entries == null)
            {
                return folders;
            }

            AddUniqueFolder(folders, savePlan.RootFolder);

            for (int i = 0; i < savePlan.Entries.Count; i++)
            {
                DimensionTemplateAssetSavePlanEntry entry = savePlan.Entries[i];
                Object asset = entry.Asset;
                if (asset == null || IsPersistentAsset(asset))
                {
                    continue;
                }

                string path = NormalizeAssetPath(entry.SuggestedPath);
                if (!IsAssetFilePath(path))
                {
                    continue;
                }

                AddUniqueFolder(folders, GetFolder(path));
            }

            return folders;
        }

        private static void AddUniqueFolder(List<string> folders, string folder)
        {
            if (folders == null)
            {
                return;
            }

            string normalized = NormalizeAssetPath(folder);
            if (!IsAssetFolderPath(normalized))
            {
                return;
            }

            for (int i = 0; i < folders.Count; i++)
            {
                if (folders[i] == normalized)
                {
                    return;
                }
            }

            folders.Add(normalized);
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/').Trim();
            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private static string GetFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? string.Empty : normalized.Substring(0, slash);
        }

        private static bool IsAssetFolderPath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            return normalized == "Assets" || normalized.StartsWith("Assets/");
        }

        private static bool IsAssetFilePath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            return normalized.StartsWith("Assets/") && normalized.EndsWith(".asset");
        }

        private static bool IsPersistentAsset(Object asset)
        {
            return asset != null &&
                !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));
        }
    }

    internal static class DimensionPortalVisualProfileEditorUtility
    {
        private const string DefaultProfileFileName = "PortalVisualProfile.asset";

        public static DimensionPortalVisualProfileAsset EnsureAssigned(
            DimensionTemplateAsset template,
            bool createIfMissing,
            bool recordUndo,
            out bool created,
            out string message)
        {
            created = false;
            message = string.Empty;

            if (template == null)
            {
                message = "No Dimension Asset is selected.";
                return null;
            }

            if (template.PortalVisualProfile != null)
            {
                DimensionPortalVisualProfileAsset assignedProfile =
                    template.PortalVisualProfile;
                DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    template,
                    assignedProfile,
                    out string existingInitializationMessage);
                message = string.IsNullOrEmpty(existingInitializationMessage)
                    ? "The Dimension Asset already has a portal visual profile."
                    : existingInitializationMessage;
                return assignedProfile;
            }

            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            string templateFolder = GetFolder(templatePath);
            if (!IsAssetFolderPath(templateFolder))
            {
                message =
                    "Save the Dimension Asset inside Assets before assigning its portal visual profile.";
                return null;
            }

            string preferredProfilePath = templateFolder + "/" + DefaultProfileFileName;
            DimensionPortalVisualProfileAsset profile =
                AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(
                    preferredProfilePath);

            if (profile == null)
            {
                List<DimensionPortalVisualProfileAsset> candidates =
                    FindOwnedProfiles(templateFolder);
                IReadOnlyList<DimensionPortalVisualProfileAsset> packaged =
                    DimensionPortalPackageEditorUtility.FindPackageProfiles(template);
                for (int i = 0; i < packaged.Count; i++)
                {
                    if (packaged[i] != null && !candidates.Contains(packaged[i]))
                    {
                        candidates.Add(packaged[i]);
                    }
                }

                // The instant item portal's profile must never be scavenged as the placed
                // portal's profile — the two slots are configured independently.
                if (template.ItemPortalVisualProfile != null)
                {
                    candidates.Remove(template.ItemPortalVisualProfile);
                }

                if (candidates.Count == 1)
                {
                    profile = candidates[0];
                }
                else if (candidates.Count > 1)
                {
                    message =
                        "More than one portal visual profile exists under " +
                        templateFolder +
                        ". Select the intended profile manually so the dashboard does not guess.";
                    return null;
                }
            }

            if (profile == null && createIfMissing)
            {
                if (!DimensionPortalPresetEditorUtility.CreateVanilla(
                        template,
                        "Default Portal",
                        true,
                        out profile,
                        out string packageMessage))
                {
                    message = "Could not create the dimension's default portal package. " +
                              packageMessage;
                    return null;
                }

                created = true;
            }

            if (profile == null)
            {
                message =
                    "No portal visual profile exists under " + templateFolder + ".";
                return null;
            }

            if (recordUndo)
            {
                Undo.RecordObject(template, "Assign portal visual profile");
            }

            template.SetPortalVisualProfile(profile);
            DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                template,
                profile,
                out string initializationMessage);
            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
            message = created
                ? "Created and assigned a vanilla-default portal visual profile at " +
                  profilePath + "."
                : "Automatically assigned the portal visual profile at " +
                  profilePath + ".";
            if (!string.IsNullOrEmpty(initializationMessage))
            {
                message += " " + initializationMessage;
            }
            return profile;
        }

        /// <summary>
        /// Ensures the template has a dedicated instant item-portal (V2) profile. A freshly
        /// created profile starts frameless with the three-animation instant center sheets;
        /// unlike <see cref="EnsureAssigned"/> there is no candidate scavenging — an instant
        /// profile is only ever the one explicitly bound to the template.
        /// </summary>
        public static DimensionPortalVisualProfileAsset EnsureItemAssigned(
            DimensionTemplateAsset template,
            bool createIfMissing,
            bool recordUndo,
            out bool created,
            out string message)
        {
            created = false;
            message = string.Empty;

            if (template == null)
            {
                message = "No Dimension Asset is selected.";
                return null;
            }

            if (template.ItemPortalVisualProfile != null)
            {
                DimensionPortalVisualProfileAsset assignedProfile =
                    template.ItemPortalVisualProfile;
                DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    template,
                    assignedProfile,
                    out string existingInitializationMessage);

                // Heals profiles whose center still points at the shared framework instant
                // asset: the package invariant requires a package-owned clone.
                DimensionPortalInstantArtworkEditorUtility.EnsurePackagedInstantCenter(
                    template,
                    assignedProfile,
                    out string healMessage);

                message = string.IsNullOrEmpty(existingInitializationMessage)
                    ? "The Dimension Asset already has an instant portal profile."
                    : existingInitializationMessage;
                if (!string.IsNullOrEmpty(healMessage))
                {
                    message += " " + healMessage;
                }

                return assignedProfile;
            }

            if (!createIfMissing)
            {
                message = "The Dimension Asset has no instant portal profile.";
                return null;
            }

            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            if (!IsAssetFolderPath(GetFolder(templatePath)))
            {
                message =
                    "Save the Dimension Asset inside Assets before assigning its instant portal profile.";
                return null;
            }

            if (!DimensionPortalPresetEditorUtility.CreateVanilla(
                    template,
                    "Instant Portal",
                    false,
                    out DimensionPortalVisualProfileAsset profile,
                    out string packageMessage))
            {
                message = "Could not create the dimension's instant portal package. " +
                          packageMessage;
                return null;
            }

            created = true;
            if (recordUndo)
            {
                Undo.RecordObject(template, "Assign instant portal profile");
            }

            template.SetItemPortalVisualProfile(profile);

            // Applied after CreateVanilla's EnsureProfileInitialized so the schema migration
            // cannot turn the frameless layers back on or repoint the center at the
            // placed-portal contract.
            string defaultsMessage = string.Empty;
            DimensionPortalInstantArtworkEditorUtility.ApplyInstantProfileDefaults(
                template,
                profile,
                out defaultsMessage);

            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
            message = "Created and assigned the frameless instant portal profile at " +
                      profilePath + ".";
            if (!string.IsNullOrEmpty(defaultsMessage))
            {
                message += " " + defaultsMessage;
            }

            return profile;
        }

        private static List<DimensionPortalVisualProfileAsset> FindOwnedProfiles(
            string templateFolder)
        {
            List<DimensionPortalVisualProfileAsset> profiles =
                new List<DimensionPortalVisualProfileAsset>();
            string[] guids = AssetDatabase.FindAssets(
                "t:DimensionPortalVisualProfileAsset",
                new[] { templateFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (!IsPathInsideFolder(path, templateFolder) ||
                    path.IndexOf(
                        "/Generated/",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                DimensionPortalVisualProfileAsset profile =
                    AssetDatabase.LoadAssetAtPath<DimensionPortalVisualProfileAsset>(path);
                if (profile != null && !profiles.Contains(profile))
                {
                    profiles.Add(profile);
                }
            }

            return profiles;
        }

        private static bool IsPathInsideFolder(string assetPath, string folder)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            string normalizedFolder = NormalizeAssetPath(folder);
            return normalizedPath.StartsWith(
                normalizedFolder + "/",
                System.StringComparison.Ordinal);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetFolder(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            int slash = normalized.LastIndexOf('/');
            return slash <= 0 ? string.Empty : normalized.Substring(0, slash);
        }

        private static bool IsAssetFolderPath(string path)
        {
            string normalized = NormalizeAssetPath(path);
            return normalized == "Assets" || normalized.StartsWith("Assets/");
        }
    }
}
