using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Shared catalog of every audio clip shipped in the game's Addressables bundles, for the
    /// Sound Library window and the sound picker.
    ///
    /// The scan opens each bundle just long enough to read its asset NAMES (no assets are
    /// loaded) and keeps only entries with audio file extensions — so container bundles with no
    /// audio (ui, scripts, built-ins...) simply contribute nothing, and multi-clip banks like
    /// the sfx bundle flatten into one entry per clip. The result is cached per game path until
    /// invalidated. Only one bundle is ever held open at a time, for the clip currently being
    /// previewed.
    /// </summary>
    internal static class DimensionGameSoundCatalog
    {
        private const string GamePathPrefsKey = "ExpandNullforge.SoundLibrary.GamePath";
        private const string BundleSubfolder = "CoreKeeper_Data/StreamingAssets/aa/StandaloneWindows64";

        private static readonly string[] AudioExtensions =
        {
            ".ogg", ".wav", ".mp3", ".aif", ".aiff"
        };

        private static readonly string[] AutoDetectRoots =
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\Core Keeper",
            @"C:\Steam\steamapps\common\Core Keeper",
            @"D:\Steam\steamapps\common\Core Keeper",
            @"D:\SteamLibrary\steamapps\common\Core Keeper",
            @"E:\Steam\steamapps\common\Core Keeper",
            @"E:\SteamLibrary\steamapps\common\Core Keeper",
            @"F:\Steam\steamapps\common\Core Keeper",
            @"F:\SteamLibrary\steamapps\common\Core Keeper",
        };

        internal readonly struct SoundEntry
        {
            public SoundEntry(string bundlePath, string assetPath, string displayName)
            {
                BundlePath = bundlePath ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public string BundlePath { get; }

            /// <summary>The clip's asset path — also its runtime key.</summary>
            public string AssetPath { get; }

            public string DisplayName { get; }
        }

        private static string cachedGamePath;
        private static List<SoundEntry> cachedEntries;
        private static string cachedScanError;

        private static string loadedBundlePath;
        private static AssetBundle loadedBundle;

        internal static string ResolveGamePath()
        {
            string stored = EditorPrefs.GetString(GamePathPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(stored) && Directory.Exists(stored))
            {
                return stored;
            }

            foreach (string candidate in AutoDetectRoots)
            {
                if (Directory.Exists(Path.Combine(candidate, "CoreKeeper_Data")))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        internal static void SetGamePath(string path)
        {
            EditorPrefs.SetString(GamePathPrefsKey, path ?? string.Empty);
            InvalidateCache();
        }

        internal static void InvalidateCache()
        {
            cachedGamePath = null;
            cachedEntries = null;
            cachedScanError = null;
        }

        /// <summary>All audio clips in the game bundles, alphabetical. Cached until invalidated.</summary>
        internal static IReadOnlyList<SoundEntry> GetEntries(string gamePath, out string error)
        {
            if (cachedEntries != null &&
                string.Equals(cachedGamePath, gamePath, StringComparison.OrdinalIgnoreCase))
            {
                error = cachedScanError;
                return cachedEntries;
            }

            List<SoundEntry> entries = new List<SoundEntry>();
            cachedGamePath = gamePath;
            cachedEntries = entries;
            cachedScanError = null;

            if (string.IsNullOrEmpty(gamePath))
            {
                cachedScanError = "Core Keeper's install folder is not set.";
                error = cachedScanError;
                return entries;
            }

            string bundleFolder = Path.Combine(gamePath, BundleSubfolder);
            if (!Directory.Exists(bundleFolder))
            {
                cachedScanError =
                    "No bundle folder at '" + bundleFolder + "' — is this the game's install folder?";
                error = cachedScanError;
                return entries;
            }

            // The enumeration re-opens every bundle; anything this catalog still holds open
            // would make its own re-open fail, so release first.
            ReleaseLoadedBundle();

            string[] bundlePaths = Directory.GetFiles(bundleFolder, "*.bundle");
            foreach (string bundlePath in bundlePaths)
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    continue;
                }

                try
                {
                    foreach (string assetName in bundle.GetAllAssetNames())
                    {
                        if (!HasAudioExtension(assetName))
                        {
                            continue;
                        }

                        entries.Add(new SoundEntry(
                            bundlePath,
                            assetName,
                            Path.GetFileNameWithoutExtension(assetName)));
                    }
                }
                finally
                {
                    bundle.Unload(false);
                }
            }

            entries.Sort((a, b) => string.Compare(
                a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            error = cachedScanError;
            return entries;
        }

        private static bool HasAudioExtension(string assetName)
        {
            foreach (string extension in AudioExtensions)
            {
                if (assetName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Loads an entry's clip, swapping the single held-open bundle as needed.</summary>
        internal static AudioClip LoadClip(SoundEntry entry, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(entry.BundlePath))
            {
                return null;
            }

            if (loadedBundle == null ||
                !string.Equals(loadedBundlePath, entry.BundlePath, StringComparison.OrdinalIgnoreCase))
            {
                ReleaseLoadedBundle();
                loadedBundle = AssetBundle.LoadFromFile(entry.BundlePath);
                if (loadedBundle == null)
                {
                    error = "Could not open the bundle holding this clip (already open elsewhere?).";
                    return null;
                }

                loadedBundlePath = entry.BundlePath;
            }

            AudioClip clip = loadedBundle.LoadAsset<AudioClip>(entry.AssetPath);
            if (clip == null)
            {
                error = "The clip was not found in its bundle — rescan the library.";
            }

            return clip;
        }

        internal static void ReleaseLoadedBundle()
        {
            if (loadedBundle != null)
            {
                loadedBundle.Unload(true);
                loadedBundle = null;
            }

            loadedBundlePath = null;
        }

        // ---- Editor audio preview (internal AudioUtil, via reflection) -----------------------

        internal static void PlayPreview(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            InvokeAudioUtil(
                new[] { "PlayPreviewClip", "PlayClip" },
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                new object[] { clip, 0, false });
        }

        internal static void StopPreview()
        {
            InvokeAudioUtil(
                new[] { "StopAllPreviewClips", "StopAllClips" },
                Type.EmptyTypes,
                Array.Empty<object>());
        }

        private static void InvokeAudioUtil(string[] methodNames, Type[] signature, object[] args)
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null)
            {
                return;
            }

            foreach (string methodName in methodNames)
            {
                MethodInfo method = audioUtil.GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    signature,
                    null);
                if (method != null)
                {
                    method.Invoke(null, args);
                    return;
                }
            }
        }
    }
}
