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
    /// Writing a set of artwork files so a failure halfway puts them all back.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        private sealed class AssetFileRollbackState
        {
            public string AssetPath;
            public string AbsolutePath;
            public bool FileExisted;
            public byte[] FileContent;
            public bool MetaExisted;
            public byte[] MetaContent;
        }

        /// <summary>
        /// Keeps every live asset file byte-for-byte recoverable while a managed
        /// SpriteAsset is being rebaked. PNG bytes are first written beneath Library,
        /// then atomically replace their destination before Unity imports them.
        /// </summary>
        internal sealed class ArtworkFileTransaction
        {
            private readonly string managedAssetPath;
            private readonly bool managedAssetWasCreated;
            private readonly Dictionary<string, AssetFileRollbackState> states =
                new Dictionary<string, AssetFileRollbackState>(StringComparer.OrdinalIgnoreCase);
            private readonly List<AssetFileRollbackState> orderedStates =
                new List<AssetFileRollbackState>();
            private readonly List<string> temporaryFiles = new List<string>();
            private bool completed;

            public ArtworkFileTransaction(string assetPath, bool assetWasCreated)
            {
                managedAssetPath = NormalizeAssetPath(assetPath);
                managedAssetWasCreated = assetWasCreated;
                if (!managedAssetWasCreated)
                {
                    Capture(managedAssetPath);
                }
            }

            public void ReplaceAssetBytes(string assetPath, byte[] content)
            {
                if (content == null || content.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Could not encode editable portal artwork at " + assetPath + ".");
                }

                AssetFileRollbackState state = Capture(assetPath);
                string directory = Path.GetDirectoryName(state.AbsolutePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (state.FileExisted && ByteArraysEqual(File.ReadAllBytes(state.AbsolutePath), content))
                {
                    AssetDatabase.ImportAsset(
                        state.AssetPath,
                        ImportAssetOptions.ForceSynchronousImport);
                    return;
                }

                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string stagingFolder = Path.Combine(
                    projectRoot ?? string.Empty,
                    "Library",
                    "ExpandNullforge",
                    "PortalArtworkStaging");
                Directory.CreateDirectory(stagingFolder);
                string token = Guid.NewGuid().ToString("N");
                string stagedPath = Path.Combine(stagingFolder, token + ".png.stage");
                temporaryFiles.Add(stagedPath);
                File.WriteAllBytes(stagedPath, content);
                if (!ByteArraysEqual(File.ReadAllBytes(stagedPath), content))
                {
                    throw new IOException(
                        "The staged portal texture could not be verified before import.");
                }

                if (File.Exists(state.AbsolutePath))
                {
                    string backupPath = Path.Combine(stagingFolder, token + ".png.backup");
                    temporaryFiles.Add(backupPath);
                    File.Replace(stagedPath, state.AbsolutePath, backupPath, true);
                }
                else
                {
                    File.Move(stagedPath, state.AbsolutePath);
                }

                AssetDatabase.ImportAsset(
                    state.AssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            public void Commit()
            {
                completed = true;
                CleanupTemporaryFiles();
            }

            public string Rollback(SpriteAsset managedAsset, SpriteAsset managedSnapshot)
            {
                if (completed)
                {
                    return string.Empty;
                }

                List<string> errors = new List<string>();
                try
                {
                    if (!managedAssetWasCreated &&
                        managedAsset != null &&
                        managedSnapshot != null)
                    {
                        EditorUtility.CopySerialized(managedSnapshot, managedAsset);
                        managedAsset.name =
                            ResolveAssetFileName(managedAsset, managedSnapshot.name);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add("restore in-memory SpriteAsset: " + exception.Message);
                }

                for (int i = orderedStates.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Restore(orderedStates[i]);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(
                            "restore " + orderedStates[i].AssetPath + ": " + exception.Message);
                    }
                }

                if (managedAssetWasCreated && !string.IsNullOrEmpty(managedAssetPath))
                {
                    try
                    {
                        if (!AssetDatabase.DeleteAsset(managedAssetPath))
                        {
                            string absolute = AssetPathToAbsolutePath(managedAssetPath);
                            DeleteFileIfPresent(absolute);
                            DeleteFileIfPresent(absolute + ".meta");
                        }
                    }
                    catch (Exception exception)
                    {
                        errors.Add("remove incomplete SpriteAsset: " + exception.Message);
                    }
                }

                try
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    for (int i = 0; i < orderedStates.Count; i++)
                    {
                        if (orderedStates[i].FileExisted)
                        {
                            AssetDatabase.ImportAsset(
                                orderedStates[i].AssetPath,
                                ImportAssetOptions.ForceSynchronousImport);
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add("refresh restored assets: " + exception.Message);
                }

                completed = true;
                CleanupTemporaryFiles();
                return errors.Count == 0 ? string.Empty : string.Join("; ", errors);
            }

            private AssetFileRollbackState Capture(string assetPath)
            {
                string normalized = NormalizeAssetPath(assetPath);
                if (states.TryGetValue(normalized, out AssetFileRollbackState existing))
                {
                    return existing;
                }

                string absolute = AssetPathToAbsolutePath(normalized);
                if (string.IsNullOrEmpty(absolute))
                {
                    throw new InvalidOperationException(
                        "Portal artwork must be saved inside the Unity Assets folder: " +
                        normalized + ".");
                }

                string metaPath = absolute + ".meta";
                AssetFileRollbackState state = new AssetFileRollbackState
                {
                    AssetPath = normalized,
                    AbsolutePath = absolute,
                    FileExisted = File.Exists(absolute),
                    FileContent = File.Exists(absolute) ? File.ReadAllBytes(absolute) : null,
                    MetaExisted = File.Exists(metaPath),
                    MetaContent = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null
                };
                states.Add(normalized, state);
                orderedStates.Add(state);
                return state;
            }

            private static void Restore(AssetFileRollbackState state)
            {
                RestoreFile(state.AbsolutePath, state.FileExisted, state.FileContent);
                RestoreFile(state.AbsolutePath + ".meta", state.MetaExisted, state.MetaContent);
            }

            private static void RestoreFile(string path, bool existed, byte[] content)
            {
                if (!existed)
                {
                    DeleteFileIfPresent(path);
                    return;
                }

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(path, content ?? Array.Empty<byte>());
            }

            private void CleanupTemporaryFiles()
            {
                for (int i = 0; i < temporaryFiles.Count; i++)
                {
                    try
                    {
                        DeleteFileIfPresent(temporaryFiles[i]);
                    }
                    catch (Exception)
                    {
                        // Library staging residue is harmless and can be cleaned by Unity.
                    }
                }

                temporaryFiles.Clear();
            }
        }
    }
}
