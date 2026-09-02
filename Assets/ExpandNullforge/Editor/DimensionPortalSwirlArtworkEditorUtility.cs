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
    /// Owns the optional full-canvas Swirls SpriteAsset without folding it into the four
    /// framework-derived palette layers. Vanilla mode still renders the extracted
    /// GatherEnergy ParticleSystem; this helper only materializes the author-selected
    /// animated SpriteObject alternative into a self-contained portal package.
    /// </summary>
    internal static partial class DimensionPortalSwirlArtworkEditorUtility
    {
        internal const string ReferencePropertyName = "centerSwirlSpriteAsset";
        internal const string OverridePropertyName = "centerSwirlOverrideVanilla";
        internal const string PackageRelativeFolder = "Artwork/Swirls";
        internal const string PackageRole = "Swirls";
        internal const int NativeFrameSize = 48;

        internal sealed class TextureDependency
        {
            public string Role;
            public Texture2D Texture;
        }

        /// <summary>
        /// Bounded authoring view of the only custom Swirls animation consumed at runtime.
        /// The selected SpriteAsset is reported so callers can distinguish the shared
        /// framework starter from a package-owned editable copy without re-resolving it.
        /// </summary>
        internal sealed class AnimationZeroTextureSlot
        {
            public SpriteAsset SpriteAsset;
            public Texture2D ColorTexture;
            public Texture2D EmissiveTexture;
            public Texture2D NormalTexture;
            public int FrameCount;
            public int SheetWidth;
            public int SheetHeight;
            public int FrameWidth;
            public int FrameHeight;
            public bool IsFramework;
            public bool IsManaged;
        }

        /// <summary>
        /// Returns the visible artwork selector to the framework starter and selects exact
        /// vanilla GatherEnergy behavior. Existing package-owned alternatives remain intact.
        /// </summary>
        public static bool AssignFrameworkReference(
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                message = "The active portal profile is required.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            if (!AssignFrameworkReference(serialized, out message))
            {
                return false;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
            message = "Restored the framework Swirls starter and exact vanilla inner particles.";
            return true;
        }

        /// <summary>
        /// Stages the framework Swirls starter and vanilla-particle mode on the caller's
        /// existing SerializedObject. The caller owns the single Apply/Undo transaction.
        /// </summary>
        internal static bool AssignFrameworkReference(
            SerializedObject serializedProfile,
            out string message)
        {
            message = string.Empty;
            if (serializedProfile == null ||
                !(serializedProfile.targetObject is DimensionPortalVisualProfileAsset))
            {
                message = "The active portal profile is required.";
                return false;
            }

            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath);
            if (framework == null)
            {
                message = "The framework Swirls starter SpriteAsset could not be loaded.";
                return false;
            }

            SerializedProperty reference = serializedProfile.FindProperty(
                ReferencePropertyName);
            SerializedProperty overrideVanilla = serializedProfile.FindProperty(
                OverridePropertyName);
            if (reference == null || overrideVanilla == null)
            {
                message = "The portal profile no longer exposes the custom Swirls settings.";
                return false;
            }

            ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, framework);
            overrideVanilla.boolValue = false;
            message = "Restored the framework Swirls starter and exact vanilla inner particles.";
            return true;
        }

        /// <summary>
        /// Replaces animation zero's color/emissive/normal sheets on the selected
        /// package-owned Swirls SpriteAsset. Framework and arbitrary external assets are
        /// first deep-copied into Artwork/Swirls and are never edited in place. Color is
        /// required; null emissive or normal values intentionally clear that channel.
        /// </summary>
        public static bool CreateOrUpdateAnimationZeroTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            Texture2D colorTexture,
            Texture2D emissiveTexture,
            Texture2D normalTexture,
            out string message)
        {
            message = string.Empty;
            if (template == null || profile == null)
            {
                message = "The active Dimension Asset and portal profile are required.";
                return false;
            }

            if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out message))
            {
                return false;
            }

            if (!DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out _,
                    out string packageFolder) ||
                string.IsNullOrEmpty(packageFolder))
            {
                message =
                    "Save this portal as a managed profile before editing Swirls textures.";
                return false;
            }

            packageFolder = NormalizeAssetPath(packageFolder);
            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            string modRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            if (string.IsNullOrEmpty(modRoot) ||
                !AssetPathIsWithin(packageFolder, modRoot))
            {
                message =
                    "The portal profile package does not belong to the selected Dimension Asset's mod.";
                return false;
            }

            string expectedFolder = NormalizeAssetPath(
                packageFolder + "/" + PackageRelativeFolder);
            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    expectedFolder,
                    out message))
            {
                return false;
            }

            if (!TryResolveManagedAssetForTextureEdit(
                    template,
                    profile,
                    packageFolder,
                    expectedFolder,
                    out SpriteAsset managed,
                    out message))
            {
                return false;
            }

            if (!TryBuildAnimationZeroTextureSlot(
                    managed,
                    packageFolder,
                    out AnimationZeroTextureSlot current,
                    out message))
            {
                return false;
            }

            if (colorTexture == null ||
                current.FrameCount <= 0 ||
                colorTexture.width <= 0 ||
                colorTexture.width % current.FrameCount != 0)
            {
                message = "A Swirls color sheet that divides evenly into its " +
                          current.FrameCount + " animation frames is required.";
                return false;
            }

            int selectedFrameWidth = colorTexture.width / current.FrameCount;
            if (selectedFrameWidth > NativeFrameSize ||
                colorTexture.height > NativeFrameSize)
            {
                message = "Swirls frames must fit within the " + NativeFrameSize + " x " +
                          NativeFrameSize + " portal canvas. Found " + selectedFrameWidth +
                          " x " + colorTexture.height + ".";
                return false;
            }

            // The emissive and normal sheets must match the chosen color sheet's dimensions.
            int requiredWidth = colorTexture.width;
            int requiredHeight = colorTexture.height;
            if (!TryValidateSelectedTexture(
                    colorTexture,
                    "Swirls color sheet",
                    requiredWidth,
                    requiredHeight,
                    false,
                    out message) ||
                !TryValidateSelectedTexture(
                    emissiveTexture,
                    "Swirls emissive sheet",
                    requiredWidth,
                    requiredHeight,
                    true,
                    out message) ||
                !TryValidateSelectedTexture(
                    normalTexture,
                    "Swirls normal sheet",
                    requiredWidth,
                    requiredHeight,
                    true,
                    out message))
            {
                return false;
            }

            // The swirl is a plain tinted SpriteObject at runtime, so the package-owned
            // sheets stay pristine: the selected artwork is deep-copied verbatim and the
            // creator's CenterParticleTint/CenterSwirlEmissiveColor are applied by the game
            // shader at draw time. No per-color pixel bake or untinted sidecar is written.
            string managedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(managed));
            Texture2D colorSource = colorTexture;
            Texture2D emissiveSource = emissiveTexture;

            SpriteAsset snapshot = DimensionPortalArtworkEditorUtility.InstantiateSnapshot(managed);
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction =
                new DimensionPortalArtworkEditorUtility.ArtworkFileTransaction(
                    managedPath,
                    false);
            try
            {
                string fileStem = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(managed.name, "Portal") + "_Editable_Anim0";
                if (!TryMaterializeSelectedTexture(
                        colorSource,
                        current.ColorTexture,
                        expectedFolder,
                        fileStem,
                        string.Empty,
                        requiredWidth,
                        requiredHeight,
                        false,
                        transaction,
                        out Texture2D managedColor,
                        out message) ||
                    !TryMaterializeSelectedTexture(
                        emissiveSource,
                        current.EmissiveTexture,
                        expectedFolder,
                        fileStem,
                        "_Emissive",
                        requiredWidth,
                        requiredHeight,
                        false,
                        transaction,
                        out Texture2D managedEmissive,
                        out message) ||
                    !TryMaterializeSelectedTexture(
                        normalTexture,
                        current.NormalTexture,
                        expectedFolder,
                        fileStem,
                        "_Normal",
                        requiredWidth,
                        requiredHeight,
                        true,
                        transaction,
                        out Texture2D managedNormal,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                SerializedObject serializedManaged = new SerializedObject(managed);
                serializedManaged.Update();
                if (!TryGetAnimationZeroSpriteData(
                        serializedManaged,
                        out _,
                        out SerializedProperty spriteData,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                SerializedProperty colorProperty =
                    spriteData.FindPropertyRelative("texture");
                SerializedProperty emissiveProperty =
                    spriteData.FindPropertyRelative("emissiveTexture");
                SerializedProperty normalProperty =
                    spriteData.FindPropertyRelative("normalTexture");
                if (colorProperty == null || emissiveProperty == null || normalProperty == null)
                {
                    throw new InvalidOperationException(
                        "The managed Swirls SpriteAsset does not expose all animation-zero texture slots.");
                }

                colorProperty.objectReferenceValue = managedColor;
                emissiveProperty.objectReferenceValue = managedEmissive;
                normalProperty.objectReferenceValue = managedNormal;
                serializedManaged.ApplyModifiedPropertiesWithoutUndo();

                if (!TryBuildAnimationZeroTextureSlot(
                        managed,
                        packageFolder,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                EditorUtility.SetDirty(managed);
                AssetDatabase.SaveAssetIfDirty(managed);
                EnsureManifestContains(modRoot, managedPath);
                AssetDatabase.SaveAssets();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                transaction.Commit();
                // The shape changed, so any previously preserved untinted source is stale.
                // Drop it so the next color bake recaptures from the new pristine artwork.
                DeleteSwirlSourceSheet(managedPath, expectedFolder);
                message = "Updated animation zero on the package-owned Swirls SpriteAsset.";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = transaction.Rollback(managed, snapshot);
                message = "Could not update the custom Swirls textures. " + exception.Message;
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    message += " Rollback issue: " + rollbackError + ".";
                }

                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        public static bool TryResolveReference(
            DimensionPortalVisualProfileAsset profile,
            SerializedProperty reference,
            string packageFolder,
            out SpriteAsset asset)
        {
            asset = null;
            if (!TryReadReferenceAddress(reference, out long low, out long high) ||
                (low == 0L && high == 0L))
            {
                return true;
            }

            ScriptableDataEditorUtility.GetDataBlock(reference, out asset);
            if (asset != null)
            {
                return true;
            }

            // Shared with ClassifyReference so a layer is never called unresolved in one place
            // and resolved in another: framework asset, then the owning package, then the project.
            string folder = string.IsNullOrEmpty(packageFolder)
                ? string.Empty
                : NormalizeAssetPath(packageFolder + "/" + PackageRelativeFolder);
            asset = DimensionPortalArtworkEditorUtility.FindSpriteAssetByAddress(
                low, high, folder);
            return asset != null;
        }

        public static bool IsFrameworkAsset(SpriteAsset asset)
        {
            return asset != null && string.Equals(
                NormalizeAssetPath(AssetDatabase.GetAssetPath(asset)),
                DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasAddress(SerializedProperty reference)
        {
            return TryReadReferenceAddress(reference, out long low, out long high) &&
                   (low != 0L || high != 0L);
        }

        public static bool TryReadReferenceAddress(
            SerializedProperty reference,
            out long low,
            out long high)
        {
            low = 0L;
            high = 0L;
            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            SerializedProperty lowProperty = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty highProperty = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (lowProperty == null || highProperty == null)
            {
                return false;
            }

            low = lowProperty.longValue;
            high = highProperty.longValue;
            return true;
        }

        public static void ReadSpriteAssetAddress(
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
            if (lowProperty != null && highProperty != null)
            {
                low = lowProperty.longValue;
                high = highProperty.longValue;
            }
        }

        private static bool AssetPathIsWithin(string assetPath, string folder)
        {
            string normalizedAsset = NormalizeAssetPath(assetPath);
            string normalizedFolder = NormalizeAssetPath(folder).TrimEnd('/');
            return !string.IsNullOrEmpty(normalizedAsset) &&
                   !string.IsNullOrEmpty(normalizedFolder) &&
                   (string.Equals(
                        normalizedAsset,
                        normalizedFolder,
                        StringComparison.OrdinalIgnoreCase) ||
                    normalizedAsset.StartsWith(
                        normalizedFolder + "/",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
