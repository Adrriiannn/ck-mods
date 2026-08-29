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
    /// Owns the durable on-disk contract for self-contained Portal Studio packages.
    /// Runtime/generated portal output deliberately remains outside this tree.
    /// </summary>
    internal static class DimensionPortalPackageEditorUtility
    {
        public const string PackageFileName = "PortalPackage.asset";
        public const string ProfileFileName = "PortalProfile.asset";
        private const string LibrarySegment = "/Data/Portals/";

        public static bool TryResolveLibraryFolder(
            DimensionTemplateAsset template,
            out string ownerRoot,
            out string dimensionFolder,
            out string message)
        {
            ownerRoot = string.Empty;
            dimensionFolder = string.Empty;
            message = string.Empty;
            string templatePath = NormalizeAssetPath(
                template == null ? string.Empty : AssetDatabase.GetAssetPath(template));
            if (template == null || string.IsNullOrEmpty(templatePath))
            {
                message = "Save and select a Dimension Asset before managing portal packages.";
                return false;
            }

            ownerRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            if (string.IsNullOrEmpty(ownerRoot) ||
                AssetPathsEqual(ownerRoot, "Assets/ExpandNullforge"))
            {
                message = "Portal packages must belong to a saved consumer mod Dimension Asset; framework assets are read-only.";
                return false;
            }

            string dimensionKey = SanitizePathSegment(
                string.IsNullOrWhiteSpace(template.DimensionId)
                    ? template.name
                    : template.DimensionId,
                "Dimension");
            dimensionFolder = ownerRoot + LibrarySegment + dimensionKey;
            return true;
        }

        public static bool TryCreatePackageFolder(
            DimensionTemplateAsset template,
            string displayName,
            out string packageId,
            out string packageFolder,
            out string message)
        {
            packageId = Guid.NewGuid().ToString("N");
            packageFolder = string.Empty;
            if (!TryResolveLibraryFolder(
                    template,
                    out _,
                    out string dimensionFolder,
                    out message) ||
                !EnsureFolder(dimensionFolder, out message))
            {
                return false;
            }

            string baseName = SanitizePathSegment(displayName, "Portal");
            string candidate = dimensionFolder + "/" + baseName;
            int suffix = 2;
            while (AssetDatabase.IsValidFolder(candidate) ||
                   AssetDatabase.LoadMainAssetAtPath(candidate) != null)
            {
                candidate = dimensionFolder + "/" + baseName + " " + suffix;
                suffix++;
            }

            if (!EnsureFolder(candidate, out message))
            {
                return false;
            }

            packageFolder = candidate;
            return true;
        }

        public static bool TryGetPackage(
            DimensionPortalVisualProfileAsset profile,
            out DimensionPortalPackageAsset package,
            out string packageFolder)
        {
            package = null;
            packageFolder = string.Empty;
            string profilePath = NormalizeAssetPath(
                profile == null ? string.Empty : AssetDatabase.GetAssetPath(profile));
            if (string.IsNullOrEmpty(profilePath) ||
                !string.Equals(
                    Path.GetFileName(profilePath),
                    ProfileFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            packageFolder = NormalizeAssetPath(Path.GetDirectoryName(profilePath));
            if (string.IsNullOrEmpty(packageFolder) ||
                packageFolder.IndexOf(LibrarySegment, StringComparison.OrdinalIgnoreCase) < 0)
            {
                packageFolder = string.Empty;
                return false;
            }

            package = AssetDatabase.LoadAssetAtPath<DimensionPortalPackageAsset>(
                packageFolder + "/" + PackageFileName);
            if (package == null || package.Profile != profile ||
                string.IsNullOrEmpty(package.PackageId))
            {
                package = null;
                packageFolder = string.Empty;
                return false;
            }

            return true;
        }

        public static string GetArtworkFolder(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            if (!TryGetPackage(profile, out _, out string packageFolder))
            {
                return string.Empty;
            }

            return packageFolder + "/Artwork/" + GetLayerFolderName(layer);
        }

        public static IReadOnlyList<DimensionPortalVisualProfileAsset> FindPackageProfiles(
            DimensionTemplateAsset template)
        {
            List<DimensionPortalVisualProfileAsset> profiles =
                new List<DimensionPortalVisualProfileAsset>();
            if (!TryResolveLibraryFolder(
                    template,
                    out _,
                    out string dimensionFolder,
                    out _) ||
                !AssetDatabase.IsValidFolder(dimensionFolder))
            {
                return profiles.AsReadOnly();
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:DimensionPortalPackageAsset",
                new[] { dimensionFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                DimensionPortalPackageAsset package =
                    AssetDatabase.LoadAssetAtPath<DimensionPortalPackageAsset>(path);
                if (package != null && package.Profile != null &&
                    IsPackageOwnedByTemplate(template, package) &&
                    !profiles.Contains(package.Profile))
                {
                    profiles.Add(package.Profile);
                }
            }

            return profiles.AsReadOnly();
        }

        public static bool IsPackageOwnedByTemplate(
            DimensionTemplateAsset template,
            DimensionPortalPackageAsset package)
        {
            if (template == null || package == null || package.Profile == null)
            {
                return false;
            }

            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            string packagePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(package));
            string templateRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            string packageRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(packagePath));
            return !string.IsNullOrEmpty(templateRoot) &&
                   AssetPathsEqual(templateRoot, packageRoot) &&
                   string.Equals(
                       package.DimensionId,
                       template.DimensionId ?? string.Empty,
                       StringComparison.Ordinal);
        }

        public static bool RefreshArtworkInventory(
            DimensionPortalPackageAsset package,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (package == null || profile == null || package.Profile != profile ||
                !TryGetPackage(profile, out DimensionPortalPackageAsset resolved, out string root) ||
                resolved != package)
            {
                message = "The portal package and profile do not form a valid package root.";
                return false;
            }

            List<DimensionPortalPackageAsset.ArtworkEntry> entries =
                new List<DimensionPortalPackageAsset.ArtworkEntry>();
            List<DimensionPortalPackageAsset.DependencyEntry> dependencies =
                new List<DimensionPortalPackageAsset.DependencyEntry>();
            HashSet<string> dependencyPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            DimensionPortalArtworkLayer[] layers =
            {
                DimensionPortalArtworkLayer.Frame,
                DimensionPortalArtworkLayer.ChargeSweep,
                DimensionPortalArtworkLayer.Milestones,
                ResolveCenterInventoryLayer(profile, serialized)
            };
            for (int i = 0; i < layers.Length; i++)
            {
                DimensionPortalArtworkLayer layer = layers[i];
                SerializedProperty reference = serialized.FindProperty(
                    GetReferencePropertyName(layer));
                DimensionPortalArtworkReferenceKind kind =
                    DimensionPortalArtworkEditorUtility.ClassifyReference(
                        reference,
                        profile,
                        layer,
                        out SpriteAsset asset);
                if (kind != DimensionPortalArtworkReferenceKind.Managed || asset == null)
                {
                    message = "The " + GetLayerFolderName(layer) +
                              " layer is not a package-owned SpriteAsset.";
                    return false;
                }

                string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                string expectedFolder = GetArtworkFolder(profile, layer);
                if (!AssetPathIsWithin(assetPath, expectedFolder))
                {
                    message = "The " + GetLayerFolderName(layer) +
                              " SpriteAsset is outside its portal package.";
                    return false;
                }

                ReadSpriteAssetAddress(asset, out long low, out long high);
                if (low == 0L && high == 0L)
                {
                    message = "The " + GetLayerFolderName(layer) +
                              " SpriteAsset has no stable Scriptable Data address.";
                    return false;
                }

                entries.Add(new DimensionPortalPackageAsset.ArtworkEntry(
                    GetLayerFolderName(layer),
                    AssetDatabase.AssetPathToGUID(assetPath),
                    assetPath.Substring(root.Length + 1),
                    low,
                    high));
            }

            if (!TryAddCustomSwirlArtwork(
                    profile,
                    serialized,
                    root,
                    entries,
                    dependencies,
                    dependencyPaths,
                    out message))
            {
                return false;
            }

            string[] dependencyProperties =
            {
                "centerParticleTexture",
                "readyFlashTexture",
                "portalShadowSprite",
                "portalShadowCasterSprite"
            };
            string[] dependencyRoles =
            {
                "CenterParticles",
                "ReadyFlash",
                "FloorShadow",
                "ShadowCaster"
            };
            for (int i = 0; i < dependencyProperties.Length; i++)
            {
                SerializedProperty dependency = serialized.FindProperty(
                    dependencyProperties[i]);
                UnityEngine.Object asset = dependency == null
                    ? null
                    : dependency.objectReferenceValue;
                if (asset == null)
                {
                    continue;
                }

                if (!TryAddPackageDependency(
                        asset,
                        dependencyRoles[i],
                        root,
                        dependencies,
                        dependencyPaths,
                        out message))
                {
                    return false;
                }
            }

            SerializedProperty centerParticleSprite = serialized.FindProperty(
                "centerParticleSprite");
            if (centerParticleSprite != null &&
                centerParticleSprite.objectReferenceValue != null)
            {
                Sprite sprite = centerParticleSprite.objectReferenceValue as Sprite;
                if (sprite == null)
                {
                    message = "The CenterParticleSprite dependency is not a Sprite.";
                    return false;
                }

                if (!TryAddPackageSpriteDependency(
                        sprite,
                        "CenterParticleSprite",
                        root,
                        dependencies,
                        dependencyPaths,
                        out message))
                {
                    return false;
                }
            }

            SerializedProperty readyFlashSprites = serialized.FindProperty(
                "readyFlashSprites");
            if (readyFlashSprites != null)
            {
                if (!readyFlashSprites.isArray)
                {
                    message = "The ReadyFlashSprites dependency is not a Sprite array.";
                    return false;
                }

                for (int i = 0; i < readyFlashSprites.arraySize; i++)
                {
                    UnityEngine.Object element = readyFlashSprites
                        .GetArrayElementAtIndex(i)
                        .objectReferenceValue;
                    if (element == null)
                    {
                        continue;
                    }

                    Sprite sprite = element as Sprite;
                    if (sprite == null)
                    {
                        message = "ReadyFlashSprites element " + i +
                                  " is not a Sprite.";
                        return false;
                    }

                    if (!TryAddPackageSpriteDependency(
                            sprite,
                            "ReadyFlashSprite[" + i + "]",
                            root,
                            dependencies,
                            dependencyPaths,
                            out message))
                    {
                        return false;
                    }
                }
            }

            package.SetArtwork(entries);
            package.SetDependencies(dependencies);
            EditorUtility.SetDirty(package);
            return true;
        }

        private static bool TryAddCustomSwirlArtwork(
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serialized,
            string packageRoot,
            List<DimensionPortalPackageAsset.ArtworkEntry> artwork,
            List<DimensionPortalPackageAsset.DependencyEntry> dependencies,
            HashSet<string> dependencyPaths,
            out string message)
        {
            message = string.Empty;
            SerializedProperty overrideProperty = serialized.FindProperty(
                DimensionPortalSwirlArtworkEditorUtility.OverridePropertyName);
            SerializedProperty reference = serialized.FindProperty(
                DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName);
            bool overrideVanilla = overrideProperty != null && overrideProperty.boolValue;
            if (reference == null)
            {
                // Packages created before the Swirls schema remain valid in vanilla mode.
                if (overrideVanilla)
                {
                    message = "Custom Swirls are enabled, but this portal profile has no Swirls SpriteAsset reference.";
                    return false;
                }

                return true;
            }

            if (!DimensionPortalSwirlArtworkEditorUtility.HasAddress(reference))
            {
                if (overrideVanilla)
                {
                    message = "Custom Swirls are enabled, but no Swirls SpriteAsset is selected.";
                    return false;
                }

                return true;
            }

            if (!DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    profile,
                    reference,
                    packageRoot,
                    out SpriteAsset swirlAsset) ||
                swirlAsset == null)
            {
                message = overrideVanilla
                    ? "Custom Swirls are enabled, but their SpriteAsset could not be resolved in Scriptable Data."
                    : "The dormant custom Swirls SpriteAsset could not be resolved and cannot be preserved by this package.";
                return false;
            }

            if (DimensionPortalSwirlArtworkEditorUtility.IsFrameworkAsset(swirlAsset))
            {
                if (overrideVanilla)
                {
                    message = "Custom Swirls are enabled, but the selected starter artwork has not been localized into this portal package.";
                    return false;
                }

                // The populated framework starter is a read-only editing affordance while
                // exact-vanilla mode is active; it is not a consumer package dependency.
                return true;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(swirlAsset));
            string expectedFolder = NormalizeAssetPath(
                packageRoot + "/" +
                DimensionPortalSwirlArtworkEditorUtility.PackageRelativeFolder);
            if (!AssetPathIsWithin(assetPath, expectedFolder))
            {
                message = "The Swirls SpriteAsset is outside its portal package.";
                return false;
            }

            DimensionPortalSwirlArtworkEditorUtility.ReadSpriteAssetAddress(
                swirlAsset,
                out long low,
                out long high);
            if (low == 0L && high == 0L)
            {
                message = "The Swirls SpriteAsset has no stable Scriptable Data address.";
                return false;
            }

            if (!ManifestContains(profile, swirlAsset))
            {
                message = "The Swirls SpriteAsset is not registered in the consumer mod's SpriteAssetManifest.";
                return false;
            }

            if (!DimensionPortalSwirlArtworkEditorUtility.TryValidateAnimationContract(
                    swirlAsset,
                    true,
                    out List<DimensionPortalSwirlArtworkEditorUtility.TextureDependency>
                        textureDependencies,
                    out message))
            {
                return false;
            }

            for (int i = 0; i < textureDependencies.Count; i++)
            {
                DimensionPortalSwirlArtworkEditorUtility.TextureDependency dependency =
                    textureDependencies[i];
                if (!TryAddPackageDependency(
                        dependency.Texture,
                        DimensionPortalSwirlArtworkEditorUtility.PackageRole + "." +
                        dependency.Role,
                        packageRoot,
                        dependencies,
                        dependencyPaths,
                        out message))
                {
                    return false;
                }
            }

            artwork.Add(new DimensionPortalPackageAsset.ArtworkEntry(
                DimensionPortalSwirlArtworkEditorUtility.PackageRole,
                AssetDatabase.AssetPathToGUID(assetPath),
                assetPath.Substring(packageRoot.Length + 1),
                low,
                high));
            return true;
        }

        private static bool ManifestContains(
            DimensionPortalVisualProfileAsset profile,
            SpriteAsset asset)
        {
            string profilePath = NormalizeAssetPath(
                profile == null ? string.Empty : AssetDatabase.GetAssetPath(profile));
            string modRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(profilePath));
            if (string.IsNullOrEmpty(modRoot) || asset == null)
            {
                return false;
            }

            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                    modRoot + "/SpriteAssetManifest.asset");
            return manifest != null && manifest.spriteAssets != null &&
                   manifest.spriteAssets.Contains(asset);
        }

        private static bool TryAddPackageSpriteDependency(
            Sprite sprite,
            string role,
            string packageRoot,
            List<DimensionPortalPackageAsset.DependencyEntry> dependencies,
            HashSet<string> dependencyPaths,
            out string message)
        {
            message = string.Empty;
            if (sprite == null)
            {
                return true;
            }

            if (!TryAddPackageDependency(
                    sprite,
                    role,
                    packageRoot,
                    dependencies,
                    dependencyPaths,
                    out message))
            {
                return false;
            }

            Texture2D sourceTexture = sprite.texture;
            if (sourceTexture == null)
            {
                message = "The " + role +
                          " Sprite has no backing source texture.";
                return false;
            }

            return TryAddPackageDependency(
                sourceTexture,
                role + ".Source",
                packageRoot,
                dependencies,
                dependencyPaths,
                out message);
        }

        private static bool TryAddPackageDependency(
            UnityEngine.Object asset,
            string role,
            string packageRoot,
            List<DimensionPortalPackageAsset.DependencyEntry> dependencies,
            HashSet<string> dependencyPaths,
            out string message)
        {
            message = string.Empty;
            if (asset == null)
            {
                return true;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrEmpty(path))
            {
                message = "The " + role +
                          " dependency is not a saved project asset.";
                return false;
            }

            if (!AssetPathIsWithin(path, packageRoot))
            {
                message = "The " + role +
                          " dependency is outside its portal package.";
                return false;
            }

            if (!dependencyPaths.Add(path))
            {
                return true;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                message = "The " + role +
                          " dependency has no stable asset GUID.";
                return false;
            }

            dependencies.Add(new DimensionPortalPackageAsset.DependencyEntry(
                role,
                guid,
                path.Substring(packageRoot.Length + 1)));
            return true;
        }

        /// <summary>Makes the portal package folder exist, or says why it could not.</summary>
        /// <remarks>
        /// The body is <see cref="DimensionAssetFolders.TryEnsure"/>, with every other folder this
        /// framework makes. Kept as a name here because six call sites across the portal editors
        /// ask for it by this name.
        /// </remarks>
        public static bool EnsureFolder(string path, out string message)
        {
            return DimensionAssetFolders.TryEnsure(path, "portal package", out message);
        }

        private static void ReadSpriteAssetAddress(
            SpriteAsset asset,
            out long low,
            out long high)
        {
            low = 0L;
            high = 0L;
            if (asset == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty lowProperty = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty highProperty = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (lowProperty != null)
            {
                low = lowProperty.longValue;
            }

            if (highProperty != null)
            {
                high = highProperty.longValue;
            }
        }

        /// <summary>
        /// The instant item portal shares the placed portal's center reference property but
        /// follows the three-animation instant contract. Resolve which contract this profile's
        /// center reference actually satisfies, so packaged instant profiles inventory their
        /// center artwork instead of failing the placed-contract ownership check.
        /// </summary>
        private static DimensionPortalArtworkLayer ResolveCenterInventoryLayer(
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serialized)
        {
            SerializedProperty reference = serialized.FindProperty(
                GetReferencePropertyName(DimensionPortalArtworkLayer.Center));
            DimensionPortalArtworkReferenceKind kind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    profile,
                    DimensionPortalArtworkLayer.CenterInstant,
                    out _);
            return kind == DimensionPortalArtworkReferenceKind.Managed ||
                   kind == DimensionPortalArtworkReferenceKind.Framework
                ? DimensionPortalArtworkLayer.CenterInstant
                : DimensionPortalArtworkLayer.Center;
        }

        /// <summary>The serialized field a layer's sprite asset is written to.</summary>
        /// <remarks>
        /// ONE COPY, AND IT ALREADY MATTERED. There were three — here, in the preset utility and
        /// in the test — and the other two had no case for the instant centre, so the same layer
        /// answered with a field name here and threw there. The test having its own copy also meant
        /// it could not fail when this changed, which is the whole reason a test reads production.
        /// </remarks>
        internal static string GetReferencePropertyName(DimensionPortalArtworkLayer layer)
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
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "centerEffectSpriteAsset";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            }
        }

        private static string GetLayerFolderName(DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "Frame";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "Charge";
                case DimensionPortalArtworkLayer.Milestones:
                    return "Milestones";
                // The instant center shares the placed center's package folder and inventory
                // role: both contracts occupy the same reference property, so a profile only
                // ever has one of them.
                case DimensionPortalArtworkLayer.Center:
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "Center";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            }
        }

        private static string SanitizePathSegment(string value, string fallback)
        {
            string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                result = result.Replace(invalid[i], '_');
            }

            result = result.Replace('/', '_').Replace('\\', '_').Trim();
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static bool AssetPathIsWithin(string path, string folder)
        {
            return AssetPathsEqual(path, folder) ||
                   NormalizeAssetPath(path).StartsWith(
                       NormalizeAssetPath(folder) + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool AssetPathsEqual(string left, string right)
        {
            return string.Equals(
                NormalizeAssetPath(left),
                NormalizeAssetPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
