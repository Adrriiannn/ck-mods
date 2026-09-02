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
    /// Making the copy of an artwork a creator owns, and the note beside it saying so.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        private static bool CreateOrUpdateManagedArtwork(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            out string message,
            bool forceNewCopy = false,
            TextureReplacement textureReplacement = null)
        {
            message = string.Empty;
            LayerDescriptor descriptor = GetDescriptor(layer);
            if (template == null || profile == null)
            {
                message = "Select a Dimension Asset and portal visual profile before creating editable portal artwork.";
                return false;
            }

            if (descriptor.SourcePalette == null &&
                !forceNewCopy &&
                textureReplacement == null)
            {
                message = "This portal layer has no semantic palette to rebake automatically.";
                return false;
            }

            if (!TryResolveConsumerOwnership(
                    template,
                    profile,
                    out string modRoot,
                    out string profileGuid,
                    out message))
            {
                return false;
            }

            // Palette bakes run from EditorApplication.update after the IMGUI event that
            // queued them. The Scriptable Data window is global, so another tool may have
            // changed its active context in the meantime. Always restore the selected
            // Dimension Asset owner's context before resolving or assigning DataBlockRefs.
            if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out string contextError))
            {
                message = contextError;
                return false;
            }

            SpriteAsset source = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                descriptor.FrameworkAssetPath);
            if (source == null)
            {
                message = "Could not load the framework " + descriptor.DisplayName +
                          " SpriteAsset at " + descriptor.FrameworkAssetPath + ".";
                return false;
            }

            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty reference = serializedProfile.FindProperty(descriptor.ReferenceProperty);
            DimensionPortalArtworkReferenceKind kind =
                ClassifyReference(reference, profile, layer, out SpriteAsset selectedAsset);

            // A newly-created DataBlock can remain temporarily unresolved while the SDK's
            // address cache reloads. Mutation is already outside IMGUI, so perform one
            // bounded lookup in this profile/layer folder to keep subsequent edits on the
            // same managed asset instead of manufacturing a duplicate.
            if (!forceNewCopy &&
                kind == DimensionPortalArtworkReferenceKind.Unresolved &&
                TryReadReferenceAddress(reference, out long unresolvedLow, out long unresolvedHigh) &&
                TryFindManagedArtworkByAddress(
                    modRoot,
                    profileGuid,
                    profile,
                    descriptor,
                    unresolvedLow,
                    unresolvedHigh,
                    out SpriteAsset unresolvedManaged))
            {
                selectedAsset = unresolvedManaged;
                kind = DimensionPortalArtworkReferenceKind.Managed;
            }

            SpriteAsset managed = !forceNewCopy &&
                                  kind == DimensionPortalArtworkReferenceKind.Managed
                ? selectedAsset
                : null;
            string managedPath = managed == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(managed));
            if (textureReplacement == null &&
                !forceNewCopy &&
                managed != null &&
                TryReadManagedMetadata(
                    managedPath,
                    profile,
                    descriptor,
                    out ManagedArtworkMetadata managedMetadata) &&
                managedMetadata.directTextureOverride)
            {
                // A user-authored sheet no longer has the framework's semantic palette
                // topology. Re-quantizing it against vanilla colors would destroy patterns
                // and hand-painted pixels, so palette gestures remain profile metadata while
                // the exact custom sheets stay authoritative.
                message = descriptor.DisplayName +
                          " uses exact custom texture sheets; its pixels were preserved.";
                return true;
            }

            bool managedWasCreated = false;
            bool referenceWasAssigned = false;
            int explicitSelectionUndoGroup = -1;
            int textureUndoGroup = -1;
            bool profileMayNeedRestore = false;
            bool manifestMayNeedRestore = false;
            SpriteAsset managedSnapshot = null;
            SpriteAsset stagedManaged = null;
            DimensionPortalVisualProfileAsset profileSnapshot =
                DimensionPortalArtworkEditorUtility.InstantiateSnapshot(profile);
            string manifestPath = modRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifestBefore =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            SpriteAssetManifest manifestSnapshot = manifestBefore == null
                ? null
                : DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifestBefore);
            ArtworkFileTransaction transaction = null;
            try
            {
                if (managed == null)
                {
                    string folder = GetManagedArtworkFolder(
                        modRoot,
                        profile,
                        profileGuid,
                        descriptor);
                    DimensionAssetFolders.Ensure(folder);
                    string baseName = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(profile.name, "PortalVisualProfile") + "_" + descriptor.DisplayName;
                    managedPath = AssetDatabase.GenerateUniqueAssetPath(
                        folder + "/" + baseName + ".asset");
                    managed = UnityEngine.Object.Instantiate(source);
                    managed.name = Path.GetFileNameWithoutExtension(managedPath);
                    SetSpriteAssetAddress(
                        managed,
                        DimensionSpriteAssetAddress.Part(managedPath, 0x706F7274616C6172UL),
                        DimensionSpriteAssetAddress.Part(managedPath, 0x7469737472796170UL));
                    AssetDatabase.CreateAsset(managed, managedPath);
                    managedWasCreated = true;
                }
                else
                {
                    managedSnapshot = DimensionPortalArtworkEditorUtility.InstantiateSnapshot(managed);
                }

                transaction = new ArtworkFileTransaction(managedPath, managedWasCreated);
                ReadSpriteAssetAddress(managed, out long addressLow, out long addressHigh);
                if (addressLow == 0L && addressHigh == 0L)
                {
                    addressLow = DimensionSpriteAssetAddress.Part(managedPath, 0x706F7274616C6172UL);
                    addressHigh = DimensionSpriteAssetAddress.Part(managedPath, 0x7469737472796170UL);
                }

                // Build the replacement SpriteAsset away from the live authored object.
                // A texture edit must preserve every authored field unrelated to the
                // selectors (animation timing, pivots, variants, placement data, and future
                // SpriteAsset metadata). Existing managed assets already own that state. On
                // the first edit of an external compatible SpriteAsset, derive from the
                // selected asset rather than silently reverting its metadata to vanilla.
                // Palette bakes and vanilla/empty selections still start from the immutable
                // framework contract.
                SpriteAsset stagingSource = source;
                if (textureReplacement != null && !managedWasCreated)
                {
                    stagingSource = managed;
                }
                else if (textureReplacement != null &&
                         managedWasCreated &&
                         kind == DimensionPortalArtworkReferenceKind.External &&
                         selectedAsset != null)
                {
                    if (!TryBuildTextureSlots(
                            source,
                            selectedAsset,
                            layer,
                            out _,
                            out string externalContractError))
                    {
                        throw new InvalidOperationException(
                            "The selected external " + descriptor.DisplayName +
                            " SpriteAsset cannot be converted into editable artwork. " +
                            externalContractError);
                    }

                    stagingSource = selectedAsset;
                }

                stagedManaged = DimensionPortalArtworkEditorUtility.InstantiateSnapshot(stagingSource);
                stagedManaged.name = Path.GetFileNameWithoutExtension(managedPath);
                SetSpriteAssetAddress(stagedManaged, addressLow, addressHigh);
                Color[] palette = ReadPalette(serializedProfile, descriptor);
                string bakeError;
                bool prepared;
                if (textureReplacement != null)
                {
                    prepared = PrepareTextureReplacement(
                        source,
                        stagedManaged,
                        managedPath,
                        modRoot,
                        layer,
                        textureReplacement,
                        transaction,
                        out bakeError);
                }
                else if (layer == DimensionPortalArtworkLayer.Frame)
                {
                    prepared = PrepareStaticFrameArtwork(
                        source,
                        stagedManaged,
                        out bakeError);
                }
                else
                {
                    prepared = BakeAnimationTextures(
                        source,
                        stagedManaged,
                        managedPath,
                        descriptor,
                        palette,
                        transaction,
                        out bakeError);
                }

                if (!prepared)
                {
                    throw new InvalidOperationException(bakeError);
                }

                if (textureReplacement != null && !managedWasCreated)
                {
                    Undo.IncrementCurrentGroup();
                    textureUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Edit portal " + descriptor.DisplayName + " textures");
                    Undo.RecordObject(managed, "Edit portal " + descriptor.DisplayName + " textures");
                }

                EditorUtility.CopySerialized(stagedManaged, managed);
                managed.name = Path.GetFileNameWithoutExtension(managedPath);
                SetSpriteAssetAddress(managed, addressLow, addressHigh);
                EditorUtility.SetDirty(managed);
                AssetDatabase.SaveAssetIfDirty(managed);
                WriteManagedMetadata(
                    managedPath,
                    profileGuid,
                    descriptor,
                    addressLow,
                    addressHigh,
                    palette,
                    HasAuthoredTextureSelection(textureReplacement));
                managed = AssetDatabase.LoadAssetAtPath<SpriteAsset>(managedPath);
                if (managed == null)
                {
                    throw new InvalidOperationException(
                        "Could not reload editable portal artwork at " + managedPath + ".");
                }

                manifestMayNeedRestore = true;
                EnsureSpriteAssetManifestContains(modRoot, managedPath);

                // The SDK keeps a separate address -> ScriptableDataBlock lookup. Creating
                // an asset through AssetDatabase does not update that lookup automatically;
                // save the manifest first, then request the same targeted refresh used by
                // the SDK's own data-block creation flow. The reload completes on the next
                // editor callback, so the Studio deliberately renders its framework fallback
                // for a transient unresolved reference.
                AssetDatabase.SaveAssets();

                // Rebaking an already-selected managed asset does not change the profile.
                // Palette materialization remains derived persistence for the originating
                // color gesture. A direct texture selection is an author edit, so its first
                // profile assignment shares the same isolated Undo group as subsequent
                // in-place mutations. The explicit '+' action keeps its selector-only
                // Undo group.
                referenceWasAssigned = managedWasCreated || selectedAsset != managed;
                if (referenceWasAssigned)
                {
                    if (forceNewCopy)
                    {
                        Undo.IncrementCurrentGroup();
                        explicitSelectionUndoGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("Select editable portal artwork");
                        Undo.RecordObject(profile, "Select editable portal artwork");
                    }
                    else if (textureReplacement != null)
                    {
                        if (textureUndoGroup < 0)
                        {
                            Undo.IncrementCurrentGroup();
                            textureUndoGroup = Undo.GetCurrentGroup();
                            Undo.SetCurrentGroupName(
                                "Edit portal " + descriptor.DisplayName + " textures");
                        }

                        Undo.RecordObject(
                            profile,
                            "Edit portal " + descriptor.DisplayName + " textures");
                    }

                    serializedProfile.Update();
                    reference = serializedProfile.FindProperty(descriptor.ReferenceProperty);
                    profileMayNeedRestore = true;
                    ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, managed);
                    if (forceNewCopy || textureReplacement != null)
                    {
                        serializedProfile.ApplyModifiedProperties();
                    }
                    else
                    {
                        serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                    }

                    EditorUtility.SetDirty(profile);
                }

                AssetDatabase.SaveAssets();

                if (explicitSelectionUndoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(explicitSelectionUndoGroup);
                }
                if (textureUndoGroup >= 0)
                {
                    Undo.SetCurrentGroupName(
                        "Edit portal " + descriptor.DisplayName + " textures");
                    Undo.CollapseUndoOperations(textureUndoGroup);
                }

                // Only this selector changed. Preserve the freshly-resolved entries for
                // the other package layers so a same-callback closure validation does not
                // have to wait for Scriptable Data's asynchronous global cache rebuild.
                InvalidateReferenceCache(profile, layer);
                CacheResolvedReference(
                    profile,
                    layer,
                    addressLow,
                    addressHigh,
                    managed,
                    DimensionPortalArtworkReferenceKind.Managed);
                if (managedWasCreated)
                {
                    // Invalidate only after the SpriteAsset, ownership metadata, manifest,
                    // and profile selection are all durable. The SDK must never observe a
                    // half-committed DataBlock while rebuilding its address lookup.
                    ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                }

                // Commit only after every fallible persistence, Undo, lookup-cache, and SDK
                // cache operation has completed. Until this point the live SpriteAsset and
                // every generated PNG remain byte-for-byte rollbackable.
                transaction.Commit();
                message = "Updated editable " + descriptor.DisplayName +
                          " artwork at " + managedPath + ".";
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = transaction == null
                    ? string.Empty
                    : transaction.Rollback(managed, managedSnapshot);
                if (transaction == null && managedWasCreated && !string.IsNullOrEmpty(managedPath))
                {
                    AssetDatabase.DeleteAsset(managedPath);
                }

                if (profileMayNeedRestore && profileSnapshot != null && profile != null)
                {
                    EditorUtility.CopySerialized(profileSnapshot, profile);
                    profile.name = ResolveAssetFileName(profile, profileSnapshot.name);
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }

                if (manifestMayNeedRestore)
                {
                    SpriteAssetManifest currentManifest =
                        AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
                    if (manifestSnapshot == null)
                    {
                        if (currentManifest != null)
                        {
                            AssetDatabase.DeleteAsset(manifestPath);
                        }
                    }
                    else if (currentManifest != null)
                    {
                        EditorUtility.CopySerialized(manifestSnapshot, currentManifest);
                        currentManifest.name =
                            ResolveAssetFileName(currentManifest, manifestSnapshot.name);
                        EditorUtility.SetDirty(currentManifest);
                        AssetDatabase.SaveAssetIfDirty(currentManifest);
                    }
                }

                int failedUndoGroup = textureUndoGroup >= 0
                    ? textureUndoGroup
                    : explicitSelectionUndoGroup;
                if (failedUndoGroup >= 0)
                {
                    try
                    {
                        // The transaction has already restored the serialized objects and
                        // files. Remove the isolated failed edit from Unity's Undo history as
                        // well so a later Undo cannot revive a half-completed mutation.
                        Undo.RevertAllDownToGroup(failedUndoGroup);
                    }
                    catch (Exception undoException)
                    {
                        string undoError = "clean failed portal-texture Undo group: " +
                                           undoException.Message;
                        rollbackError = string.IsNullOrEmpty(rollbackError)
                            ? undoError
                            : rollbackError + "; " + undoError;
                    }
                }

                if (managedWasCreated)
                {
                    // Remove a deleted/rolled-back address from the SDK lookup as well.
                    // Persistent managed alternatives are intentionally not object-destruction
                    // Undo targets: RegisterCreatedObjectUndo can leave an empty .asset shell.
                    try
                    {
                        ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                    }
                    catch (Exception cacheException)
                    {
                        string cacheError = "invalidate rolled-back SpriteAsset cache: " +
                                            cacheException.Message;
                        rollbackError = string.IsNullOrEmpty(rollbackError)
                            ? cacheError
                            : rollbackError + "; " + cacheError;
                    }
                }

                InvalidateReferenceCache();

                message = "Could not update editable " + descriptor.DisplayName +
                          " artwork. " + exception.Message +
                          (managedWasCreated
                              ? " The incomplete copy was removed."
                              : " The previously authored asset was restored.");
                if (!string.IsNullOrEmpty(rollbackError))
                {
                    message += " Rollback warning: " + rollbackError + ".";
                }

                return false;
            }
            finally
            {
                if (managedSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(managedSnapshot);
                }

                if (stagedManaged != null)
                {
                    UnityEngine.Object.DestroyImmediate(stagedManaged);
                }

                if (profileSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(profileSnapshot);
                }

                if (manifestSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifestSnapshot);
                }

                if (managed != null &&
                    string.IsNullOrEmpty(AssetDatabase.GetAssetPath(managed)))
                {
                    UnityEngine.Object.DestroyImmediate(managed);
                }
            }
        }

        private static bool TryFindManagedArtworkByAddress(
            string modRoot,
            string profileGuid,
            DimensionPortalVisualProfileAsset profile,
            LayerDescriptor descriptor,
            long addressLow,
            long addressHigh,
            out SpriteAsset asset)
        {
            asset = null;
            if ((addressLow == 0L && addressHigh == 0L) ||
                string.IsNullOrEmpty(modRoot) ||
                string.IsNullOrEmpty(profileGuid))
            {
                return false;
            }

            string folder = GetManagedArtworkFolder(
                modRoot,
                profile,
                profileGuid,
                descriptor);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return false;
            }

            string[] guids = AssetDatabase.FindAssets("t:SpriteAsset", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
                SpriteAsset candidate = AssetDatabase.LoadAssetAtPath<SpriteAsset>(path);
                ReadSpriteAssetAddress(candidate, out long candidateLow, out long candidateHigh);
                if (candidate == null ||
                    candidateLow != addressLow ||
                    candidateHigh != addressHigh ||
                    !TryReadManagedMetadata(path, profile, descriptor, out _))
                {
                    continue;
                }

                asset = candidate;
                return true;
            }

            return false;
        }

        private static void WriteManagedMetadata(
            string assetPath,
            string profileGuid,
            LayerDescriptor descriptor,
            long addressLow,
            long addressHigh,
            Color[] palette,
            bool directTextureOverride)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Could not attach ownership metadata to editable portal artwork at " +
                    assetPath + ".");
            }

            ManagedArtworkMetadata metadata = new ManagedArtworkMetadata
            {
                schemaVersion = MetadataSchemaVersion,
                owner = "Dimensions API",
                profileGuid = profileGuid,
                layer = descriptor.Key,
                sourceAssetGuid = AssetDatabase.AssetPathToGUID(descriptor.FrameworkAssetPath),
                sourceAddressLow = descriptor.FrameworkAddressLow,
                sourceAddressHigh = descriptor.FrameworkAddressHigh,
                managedAssetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                managedAddressLow = addressLow,
                managedAddressHigh = addressHigh,
                palette = palette,
                directTextureOverride = directTextureOverride
            };
            string value = MetadataPrefix + JsonUtility.ToJson(metadata);
            if (importer.userData == value)
            {
                return;
            }

            importer.userData = value;
            importer.SaveAndReimport();
        }

        private static bool TryReadManagedMetadata(
            string assetPath,
            DimensionPortalVisualProfileAsset profile,
            LayerDescriptor descriptor,
            out ManagedArtworkMetadata metadata)
        {
            metadata = null;
            string normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            AssetImporter importer = AssetImporter.GetAtPath(normalized);
            string userData = importer == null ? string.Empty : importer.userData;
            if (string.IsNullOrEmpty(userData) ||
                !userData.StartsWith(MetadataPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                metadata = JsonUtility.FromJson<ManagedArtworkMetadata>(
                    userData.Substring(MetadataPrefix.Length));
            }
            catch (Exception)
            {
                metadata = null;
            }

            string profileGuid = profile == null
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(
                    NormalizeAssetPath(AssetDatabase.GetAssetPath(profile)));
            string profilePath = profile == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
            string profileRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(profilePath));
            string assetRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(normalized));
            string expectedFolder = GetManagedArtworkFolder(
                profileRoot,
                profile,
                profileGuid,
                descriptor);
            string managedGuid = AssetDatabase.AssetPathToGUID(normalized);
            SpriteAsset managedAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(normalized);
            ReadSpriteAssetAddress(managedAsset, out long managedLow, out long managedHigh);
            return metadata != null &&
                   metadata.schemaVersion == MetadataSchemaVersion &&
                   metadata.owner == "Dimensions API" &&
                   !string.IsNullOrEmpty(profileGuid) &&
                   !string.IsNullOrEmpty(profileRoot) &&
                   AssetPathsEqual(profileRoot, assetRoot) &&
                   AssetPathIsWithin(normalized, expectedFolder) &&
                   metadata.profileGuid == profileGuid &&
                   metadata.layer == descriptor.Key &&
                   metadata.sourceAssetGuid == AssetDatabase.AssetPathToGUID(
                       descriptor.FrameworkAssetPath) &&
                   metadata.sourceAddressLow == descriptor.FrameworkAddressLow &&
                   metadata.sourceAddressHigh == descriptor.FrameworkAddressHigh &&
                   metadata.managedAssetGuid == managedGuid &&
                   metadata.managedAddressLow == managedLow &&
                   metadata.managedAddressHigh == managedHigh;
        }

        private static bool TryResolveConsumerOwnership(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string modRoot,
            out string profileGuid,
            out string message)
        {
            modRoot = string.Empty;
            profileGuid = string.Empty;
            message = string.Empty;
            if (template == null || profile == null)
            {
                message = "Select a saved Dimension Asset and portal visual profile before editing portal artwork.";
                return false;
            }

            string templatePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(template));
            string profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
            string templateRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(templatePath));
            string profileRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(profilePath));
            profileGuid = AssetDatabase.AssetPathToGUID(profilePath);
            if (string.IsNullOrEmpty(templatePath) ||
                string.IsNullOrEmpty(profilePath) ||
                string.IsNullOrEmpty(templateRoot) ||
                string.IsNullOrEmpty(profileRoot) ||
                string.IsNullOrEmpty(profileGuid))
            {
                message = "Save the Dimension Asset and portal visual profile inside the same consumer mod before editing portal artwork. No profile or artwork was changed.";
                return false;
            }

            if (AssetPathsEqual(templateRoot, "Assets/ExpandNullforge") ||
                AssetPathsEqual(profileRoot, "Assets/ExpandNullforge"))
            {
                message = "Framework portal profiles are read-only. Create or select a portal visual profile inside the dimension's consumer mod. No profile or artwork was changed.";
                return false;
            }

            if (!AssetPathsEqual(templateRoot, profileRoot))
            {
                message = "The selected Dimension Asset belongs to '" + templateRoot +
                          "', but its portal visual profile belongs to '" + profileRoot +
                          "'. Select or copy a profile into the same consumer mod. No profile or artwork was changed.";
                return false;
            }

            modRoot = templateRoot;
            return true;
        }

        private static string GetManagedArtworkFolder(
            string modRoot,
            DimensionPortalVisualProfileAsset profile,
            string profileGuid,
            LayerDescriptor descriptor)
        {
            if (profile != null && descriptor != null)
            {
                string packagedFolder =
                    DimensionPortalPackageEditorUtility.GetArtworkFolder(
                        profile,
                        descriptor.Layer);
                if (!string.IsNullOrEmpty(packagedFolder))
                {
                    return packagedFolder;
                }
            }

            return NormalizeAssetPath(modRoot) + ManagedFolderSegment +
                   profileGuid + "/" + descriptor.Key;
        }
    }
}
