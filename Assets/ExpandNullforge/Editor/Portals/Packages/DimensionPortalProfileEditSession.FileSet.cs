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
    /// Capturing a set of files before an edit and putting them back exactly as they were.
    /// </summary>
    internal sealed partial class DimensionPortalProfileEditSession
    {
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
