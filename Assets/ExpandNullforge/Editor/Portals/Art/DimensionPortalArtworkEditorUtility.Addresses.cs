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
    /// Finding a sprite asset by the address it carries, and the paths around it.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        /// <summary>
        /// Finds the SpriteAsset that carries a Scriptable Data address.
        ///
        /// The address is the reference's identity, but Scriptable Data can only resolve assets
        /// its registry knows about. Artwork that lives outside the portal package — a creator's
        /// own folder, or an asset authored during this session — resolves to nothing, and the
        /// layer then reads as unresolved even though the asset is sitting in the project. This
        /// searches by address so a reference stays meaningful wherever its artwork lives.
        ///
        /// Only reached when the registry lookup already failed, and callers cache their result,
        /// so the project-wide scan stays off the common path.
        /// </summary>
        internal static SpriteAsset FindSpriteAssetByAddress(
            long addressLow,
            long addressHigh,
            string packageArtworkFolder)
        {
            if (addressLow == 0L && addressHigh == 0L)
            {
                return null;
            }

            if (addressLow == FrameworkSwirlAddressLow &&
                addressHigh == FrameworkSwirlAddressHigh)
            {
                SpriteAsset framework =
                    AssetDatabase.LoadAssetAtPath<SpriteAsset>(FrameworkSwirlAssetPath);
                if (framework != null)
                {
                    return framework;
                }
            }

            // The owning package is the likeliest home and the cheapest place to look.
            SpriteAsset local = ScanFolderForSpriteAssetAddress(
                packageArtworkFolder, addressLow, addressHigh);
            return local != null
                ? local
                : ScanFolderForSpriteAssetAddress(string.Empty, addressLow, addressHigh);
        }

        private static SpriteAsset ScanFolderForSpriteAssetAddress(
            string folder,
            long addressLow,
            long addressHigh)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return LookUpProjectAddress(addressLow, addressHigh);
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:SpriteAsset", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                SpriteAsset candidate = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (candidate == null)
                {
                    continue;
                }

                ReadSpriteAssetAddress(candidate, out long low, out long high);
                if (low == addressLow && high == addressHigh)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Address to asset for the whole project, built once and reused.
        ///
        /// <see cref="ClassifyReference"/> runs from editor GUI code, so scanning and loading
        /// every SpriteAsset on each unresolved layer would stall the window. The index is
        /// rebuilt when the number of SpriteAssets changes, and explicitly whenever an address is
        /// rewritten (see <see cref="InvalidateSpriteAssetAddressIndex"/>).
        /// </summary>
        private static Dictionary<string, SpriteAsset> spriteAssetAddressIndex;

        private static int spriteAssetAddressIndexAssetCount = -1;

        internal static void InvalidateSpriteAssetAddressIndex()
        {
            spriteAssetAddressIndex = null;
            spriteAssetAddressIndexAssetCount = -1;
        }

        private static SpriteAsset LookUpProjectAddress(long addressLow, long addressHigh)
        {
            string[] guids = AssetDatabase.FindAssets("t:SpriteAsset");
            if (spriteAssetAddressIndex == null ||
                spriteAssetAddressIndexAssetCount != guids.Length)
            {
                Dictionary<string, SpriteAsset> index =
                    new Dictionary<string, SpriteAsset>(StringComparer.Ordinal);
                for (int i = 0; i < guids.Length; i++)
                {
                    SpriteAsset candidate = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                        AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (candidate == null)
                    {
                        continue;
                    }

                    ReadSpriteAssetAddress(candidate, out long low, out long high);
                    if (low == 0L && high == 0L)
                    {
                        continue;
                    }

                    // First writer wins: a duplicated address is a separate problem, and
                    // silently preferring the last one scanned would make it non-deterministic.
                    string key = BuildAddressKey(low, high);
                    if (!index.ContainsKey(key))
                    {
                        index.Add(key, candidate);
                    }
                }

                spriteAssetAddressIndex = index;
                spriteAssetAddressIndexAssetCount = guids.Length;
            }

            return spriteAssetAddressIndex.TryGetValue(
                       BuildAddressKey(addressLow, addressHigh),
                       out SpriteAsset resolved) && resolved != null
                ? resolved
                : null;
        }

        private static string BuildAddressKey(long addressLow, long addressHigh)
        {
            return addressLow.ToString(CultureInfo.InvariantCulture) + ":" +
                   addressHigh.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
        }

        private static bool HasAddress(SerializedProperty reference)
        {
            return TryReadReferenceAddress(reference, out long low, out long high) &&
                   (low != 0L || high != 0L);
        }

        private static bool AddressMatches(
            SerializedProperty reference,
            long expectedLow,
            long expectedHigh)
        {
            return TryReadReferenceAddress(reference, out long low, out long high) &&
                   low == expectedLow &&
                   high == expectedHigh;
        }

        private static bool TryReadReferenceAddress(
            SerializedProperty reference,
            out long lowValue,
            out long highValue)
        {
            lowValue = 0L;
            highValue = 0L;
            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                return false;
            }

            lowValue = low.longValue;
            highValue = high.longValue;
            return true;
        }

        private static void ReadSpriteAssetAddress(
            SpriteAsset asset,
            out long lowValue,
            out long highValue)
        {
            lowValue = 0L;
            highValue = 0L;
            if (asset == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low != null && high != null)
            {
                lowValue = low.longValue;
                highValue = high.longValue;
            }
        }

        private static void SetSpriteAssetAddress(
            SpriteAsset asset,
            long lowValue,
            long highValue)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty address = serialized.FindProperty("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low == null || high == null)
            {
                throw new InvalidOperationException(
                    "The SpriteAsset address could not be assigned.");
            }

            low.longValue = lowValue;
            high.longValue = highValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The address is the index's key, so re-addressing an asset invalidates it without
            // changing how many SpriteAssets exist.
            InvalidateSpriteAssetAddressIndex();
        }

        private static void EnsureSpriteAssetManifestContains(
            string modRoot,
            string spriteAssetPath)
        {
            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(spriteAssetPath);
            if (spriteAsset == null)
            {
                throw new InvalidOperationException(
                    "Could not load editable portal artwork at " + spriteAssetPath + ".");
            }

            string manifestPath = modRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                manifest.name = "SpriteAssetManifest";
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            bool changed = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                if (manifest.spriteAssets[i] == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
            }

            if (!manifest.spriteAssets.Contains(spriteAsset))
            {
                manifest.spriteAssets.Add(spriteAsset);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
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

        private static void DeleteFileIfPresent(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        internal static string AssetPathToAbsolutePath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return Path.Combine(
                NormalizeAssetPath(Application.dataPath),
                normalized.Substring("Assets/".Length)
                    .Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/').TrimEnd('/');
        }

        private static bool AssetPathsEqual(string left, string right)
        {
            return string.Equals(
                NormalizeAssetPath(left),
                NormalizeAssetPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool AssetPathIsWithin(string assetPath, string folder)
        {
            string normalizedAsset = NormalizeAssetPath(assetPath);
            string normalizedFolder = NormalizeAssetPath(folder);
            return !string.IsNullOrEmpty(normalizedAsset) &&
                   !string.IsNullOrEmpty(normalizedFolder) &&
                   (AssetPathsEqual(normalizedAsset, normalizedFolder) ||
                    normalizedAsset.StartsWith(
                        normalizedFolder + "/",
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
