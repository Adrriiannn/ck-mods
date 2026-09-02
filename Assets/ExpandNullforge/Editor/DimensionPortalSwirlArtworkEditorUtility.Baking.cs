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
    /// Baking the swirl sheet a profile asks for, and the queue that debounces it.
    /// </summary>
    internal static partial class DimensionPortalSwirlArtworkEditorUtility
    {
        // ------------------------------------------------------------------
        // Profile-owned Swirls color bake.
        //
        // Mirrors the palette layers: a customized swirl becomes a profile-owned
        // SpriteAsset whose pixels carry the chosen color, so it is saved and inspectable
        // like every other layer instead of pointing at the shared framework starter.
        // The bake is deliberately sidecar-light because the shipped starter sheet is
        // neutral white: a plain per-pixel multiply by the tint reproduces any hue, so a
        // red tint no longer collapses a blue source to black. One immutable untinted
        // source sheet ("_Source_Anim0.png") is preserved beside the generated output so
        // repeated recolors always start from the pristine shape, never a prior tint.
        // ------------------------------------------------------------------
        private const string SwirlSourceSuffix = "_Source_Anim0.png";

        private const string SwirlOutputSuffix = "_Anim0.png";

        private const double SwirlBakeDebounceSeconds = 0.28d;

        private sealed class PendingSwirlBake
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public double DueTime;
        }

        private static readonly Dictionary<int, PendingSwirlBake> PendingSwirlBakes =
            new Dictionary<int, PendingSwirlBake>();

        private static bool swirlBakeHookInstalled;

        /// <summary>
        /// Debounces a profile-owned Swirls recolor. Called from the color-picker hot path,
        /// so it only replaces one small pending record; the due callback (or an explicit
        /// Save &amp; Update flush) performs the package/asset/texture work once interaction
        /// settles. Vanilla mode clears any pending bake.
        /// </summary>
        internal static void QueueSwirlBake(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            bool immediate = false)
        {
            if (template == null || profile == null)
            {
                return;
            }

            int profileInstanceId = profile.GetInstanceID();
            if (!profile.CenterSwirlOverrideVanilla)
            {
                PendingSwirlBakes.Remove(profileInstanceId);
                RemoveSwirlBakeHookIfIdle();
                return;
            }

            PendingSwirlBakes[profileInstanceId] = new PendingSwirlBake
            {
                Template = template,
                Profile = profile,
                DueTime = immediate
                    ? EditorApplication.timeSinceStartup
                    : EditorApplication.timeSinceStartup + SwirlBakeDebounceSeconds
            };
            InstallSwirlBakeHook();
        }

        /// <summary>
        /// Synchronously runs any pending Swirls bake for this profile. Used by Save &amp;
        /// Update so the profile-owned artwork is always current before the package is saved.
        /// </summary>
        internal static bool FlushSwirlBake(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (template == null || profile == null)
            {
                return true;
            }

            PendingSwirlBakes.Remove(profile.GetInstanceID());
            RemoveSwirlBakeHookIfIdle();
            return BakeSwirlProfileAsset(template, profile, out message);
        }

        internal static void CancelSwirlBake(DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return;
            }

            PendingSwirlBakes.Remove(profile.GetInstanceID());
            RemoveSwirlBakeHookIfIdle();
        }

        private static void InstallSwirlBakeHook()
        {
            if (!swirlBakeHookInstalled)
            {
                EditorApplication.update += ProcessPendingSwirlBakes;
                swirlBakeHookInstalled = true;
            }
        }

        private static void RemoveSwirlBakeHookIfIdle()
        {
            if (swirlBakeHookInstalled && PendingSwirlBakes.Count == 0)
            {
                EditorApplication.update -= ProcessPendingSwirlBakes;
                swirlBakeHookInstalled = false;
            }
        }

        private static void ProcessPendingSwirlBakes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            List<int> ready = new List<int>();
            foreach (KeyValuePair<int, PendingSwirlBake> pair in PendingSwirlBakes)
            {
                if (pair.Value == null || pair.Value.DueTime <= now)
                {
                    ready.Add(pair.Key);
                }
            }

            for (int i = 0; i < ready.Count; i++)
            {
                if (!PendingSwirlBakes.TryGetValue(ready[i], out PendingSwirlBake pending))
                {
                    continue;
                }

                PendingSwirlBakes.Remove(ready[i]);
                if (pending == null || pending.Template == null || pending.Profile == null)
                {
                    continue;
                }

                if (!BakeSwirlProfileAsset(pending.Template, pending.Profile, out string error))
                {
                    Debug.LogError(
                        "Dimensions API could not update the custom Swirls colors: " + error,
                        pending.Profile);
                }
            }

            RemoveSwirlBakeHookIfIdle();
        }

        private static void DeleteSwirlSourceSheet(string managedPath, string expectedFolder)
        {
            if (string.IsNullOrEmpty(managedPath) || string.IsNullOrEmpty(expectedFolder))
            {
                return;
            }

            string stem = Path.GetFileNameWithoutExtension(managedPath);
            string sourcePath =
                NormalizeAssetPath(expectedFolder) + "/" + stem + SwirlSourceSuffix;
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourcePath)))
            {
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        internal static bool BakeSwirlProfileAsset(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (template == null || profile == null)
            {
                message = "The active Dimension Asset and portal profile are required.";
                return false;
            }

            if (!profile.CenterSwirlOverrideVanilla)
            {
                // The exact vanilla particles are in use; there is no custom sheet to bake.
                return true;
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
                message = "Save this portal as a managed profile before customizing Swirls.";
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
                    out message) ||
                !TryResolveManagedAssetForTextureEdit(
                    template,
                    profile,
                    packageFolder,
                    expectedFolder,
                    out SpriteAsset managed,
                    out message) ||
                !TryBuildAnimationZeroTextureSlot(
                    managed,
                    packageFolder,
                    out AnimationZeroTextureSlot current,
                    out message))
            {
                return false;
            }

            string managedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(managed));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            string sourcePath = expectedFolder + "/" + stem + SwirlSourceSuffix;
            string outputPath = expectedFolder + "/" + stem + SwirlOutputSuffix;

            SpriteAsset snapshot = DimensionPortalArtworkEditorUtility.InstantiateSnapshot(managed);
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction =
                new DimensionPortalArtworkEditorUtility.ArtworkFileTransaction(
                    managedPath,
                    false);
            try
            {
                Texture2D source = EnsureSwirlSourceSheet(
                    current,
                    sourcePath,
                    outputPath,
                    transaction);
                Texture2D baked = BakeSwirlColorTexture(
                    source,
                    outputPath,
                    profile.CenterParticleTint,
                    true,
                    current.SheetWidth,
                    current.SheetHeight,
                    transaction);

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

                // Color now lives in the pixels; the runtime draws the swirl with a neutral
                // material tint and re-applies only the emissive glow/intensity, so the
                // emissive slot shares the baked color sheet.
                spriteData.FindPropertyRelative("texture").objectReferenceValue = baked;
                spriteData.FindPropertyRelative("emissiveTexture").objectReferenceValue = baked;
                serializedManaged.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(managed);
                AssetDatabase.SaveAssetIfDirty(managed);
                EnsureManifestContains(modRoot, managedPath);
                AssetDatabase.SaveAssets();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                transaction.Commit();
                message = "Updated the profile-owned Swirls artwork from the saved colors.";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = transaction.Rollback(managed, snapshot);
                message = "Could not bake the custom Swirls colors. " + exception.Message;
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

        /// <summary>
        /// Returns the immutable untinted source sheet, materializing it once from the
        /// managed asset's current pristine artwork. Once a bake has run, the active color
        /// texture is the generated output, so the preserved source stays authoritative and
        /// recolors never compound. Selecting new custom artwork clears the source so it is
        /// recaptured from the new shape.
        /// </summary>
        private static Texture2D EnsureSwirlSourceSheet(
            AnimationZeroTextureSlot current,
            string sourcePath,
            string outputPath,
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction)
        {
            string normalizedSource = NormalizeAssetPath(sourcePath);
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedSource);
            if (existing != null &&
                existing.width == current.SheetWidth &&
                existing.height == current.SheetHeight)
            {
                return existing;
            }

            Texture2D pristine = current.ColorTexture;
            string pristinePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(pristine));
            string pristineAbsolute =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(pristinePath);

            // The active sheet sitting at the output path is only dangerous to capture once a
            // bake has produced it. Before the first bake there is no sidecar at all and the
            // sheet is still the untinted copy the package was created with — refusing it there
            // would make the very first recolor of a freshly saved package impossible. A sidecar
            // that exists but does not match the sheet size is a genuine mismatch and still
            // refuses, because that sheet may already carry a tint from an earlier bake.
            bool sidecarExists = existing != null ||
                File.Exists(
                    DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(normalizedSource));
            bool pristineIsBakeOutput = string.Equals(
                pristinePath,
                NormalizeAssetPath(outputPath),
                StringComparison.OrdinalIgnoreCase);

            if (pristine == null ||
                (pristineIsBakeOutput && sidecarExists) ||
                string.IsNullOrEmpty(pristineAbsolute) ||
                !pristinePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(pristineAbsolute))
            {
                throw new InvalidOperationException(
                    "The untinted Swirls source sheet is missing. Re-select the intended " +
                    "Swirls artwork before recoloring so an already-tinted sheet is never " +
                    "used as the source.");
            }

            transaction.ReplaceAssetBytes(
                normalizedSource,
                File.ReadAllBytes(pristineAbsolute));
            DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                normalizedSource,
                current.SheetWidth,
                current.SheetHeight);
            Texture2D created = AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedSource);
            if (created == null)
            {
                throw new InvalidOperationException(
                    "Could not preserve the untinted Swirls source sheet at " +
                    normalizedSource + ".");
            }

            return created;
        }

        private static Texture2D BakeSwirlColorTexture(
            Texture2D source,
            string targetPath,
            Color factor,
            bool multiplyAlpha,
            int requiredWidth,
            int requiredHeight,
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction)
        {
            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            string sourceAbsolute =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(sourcePath);
            if (source == null || transaction == null ||
                string.IsNullOrEmpty(sourceAbsolute) ||
                !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourceAbsolute))
            {
                throw new InvalidOperationException(
                    "The preserved Swirls source sheet is not a readable PNG asset.");
            }

            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D output = null;
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(sourceAbsolute)) ||
                    decoded.width != requiredWidth ||
                    decoded.height != requiredHeight)
                {
                    throw new InvalidOperationException(
                        "The preserved Swirls source sheet does not match the animation-zero dimensions.");
                }

                Color32[] sourcePixels = decoded.GetPixels32();
                Color32[] outputPixels = new Color32[sourcePixels.Length];
                float red = Mathf.Max(0f, factor.r);
                float green = Mathf.Max(0f, factor.g);
                float blue = Mathf.Max(0f, factor.b);
                float alpha = multiplyAlpha ? Mathf.Clamp01(factor.a) : 1f;
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color32 pixel = sourcePixels[i];
                    outputPixels[i] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * red), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.g * green), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.b * blue), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.a * alpha), 0, 255));
                }

                output = new Texture2D(
                    requiredWidth,
                    requiredHeight,
                    TextureFormat.RGBA32,
                    false);
                output.SetPixels32(outputPixels);
                output.Apply(false, false);
                transaction.ReplaceAssetBytes(
                    NormalizeAssetPath(targetPath),
                    output.EncodeToPNG());
                DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                    NormalizeAssetPath(targetPath),
                    requiredWidth,
                    requiredHeight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (output != null)
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }

            Texture2D baked = AssetDatabase.LoadAssetAtPath<Texture2D>(
                NormalizeAssetPath(targetPath));
            if (baked == null)
            {
                throw new InvalidOperationException(
                    "Could not import the recolored Swirls sheet at " + targetPath + ".");
            }

            return baked;
        }
    }
}
