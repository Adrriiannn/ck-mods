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
    /// Reading a palette off artwork, and baking a changed one back into it.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        public static bool SynchronizePaletteFromReference(
            SerializedObject serializedProfile,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            if (serializedProfile == null || profile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            if (descriptor.SourcePalette == null)
            {
                return false;
            }

            SerializedProperty reference = serializedProfile.FindProperty(descriptor.ReferenceProperty);
            DimensionPortalArtworkReferenceKind kind =
                ClassifyReference(reference, profile, layer, out SpriteAsset asset);
            if (kind == DimensionPortalArtworkReferenceKind.Empty ||
                kind == DimensionPortalArtworkReferenceKind.Framework)
            {
                SetPaletteToSource(serializedProfile, descriptor);
                return true;
            }

            if (kind != DimensionPortalArtworkReferenceKind.Managed || asset == null)
            {
                if (kind == DimensionPortalArtworkReferenceKind.External &&
                    asset != null &&
                    TryExtractRepresentativePalette(asset, descriptor, out Color[] extracted))
                {
                    for (int i = 0; i < descriptor.PaletteProperties.Length; i++)
                    {
                        SerializedProperty color = serializedProfile.FindProperty(
                            descriptor.PaletteProperties[i]);
                        if (color != null)
                        {
                            color.colorValue = extracted[i];
                        }
                    }

                    return true;
                }

                return false;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            if (!TryReadManagedMetadata(assetPath, profile, descriptor, out ManagedArtworkMetadata metadata) ||
                metadata.palette == null ||
                metadata.palette.Length != descriptor.PaletteProperties.Length)
            {
                return false;
            }

            for (int i = 0; i < descriptor.PaletteProperties.Length; i++)
            {
                SerializedProperty color = serializedProfile.FindProperty(
                    descriptor.PaletteProperties[i]);
                if (color != null)
                {
                    color.colorValue = metadata.palette[i];
                }
            }

            return true;
        }

        private static bool TryExtractRepresentativePalette(
            SpriteAsset targetAsset,
            LayerDescriptor descriptor,
            out Color[] palette)
        {
            palette = null;
            SpriteAsset sourceAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                descriptor.FrameworkAssetPath);
            if (sourceAsset == null || targetAsset == null)
            {
                return false;
            }

            SerializedObject serializedSource = new SerializedObject(sourceAsset);
            SerializedObject serializedTarget = new SerializedObject(targetAsset);
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            SerializedProperty targetAnimations = serializedTarget.FindProperty("m_animations");
            if (sourceAnimations == null || targetAnimations == null ||
                sourceAnimations.arraySize == 0 || targetAnimations.arraySize == 0)
            {
                return false;
            }

            SerializedProperty sourceData = sourceAnimations.GetArrayElementAtIndex(0)
                .FindPropertyRelative("m_spriteData");
            SerializedProperty targetData = targetAnimations.GetArrayElementAtIndex(0)
                .FindPropertyRelative("m_spriteData");
            Texture2D sourceTexture = sourceData == null
                ? null
                : sourceData.FindPropertyRelative("texture")?.objectReferenceValue as Texture2D;
            Texture2D targetTexture = targetData == null
                ? null
                : targetData.FindPropertyRelative("texture")?.objectReferenceValue as Texture2D;
            Texture2D decodedSource = null;
            Texture2D decodedTarget = null;
            bool decodedSourceReady = TryDecodeTexture(sourceTexture, out decodedSource);
            bool decodedTargetReady = TryDecodeTexture(targetTexture, out decodedTarget);
            if (!decodedSourceReady || !decodedTargetReady)
            {
                if (decodedSource != null)
                {
                    UnityEngine.Object.DestroyImmediate(decodedSource);
                }

                if (decodedTarget != null)
                {
                    UnityEngine.Object.DestroyImmediate(decodedTarget);
                }

                return false;
            }

            try
            {
                if (decodedSource.width != decodedTarget.width ||
                    decodedSource.height != decodedTarget.height)
                {
                    return false;
                }

                Color32[] sourcePixels = decodedSource.GetPixels32();
                Color32[] targetPixels = decodedTarget.GetPixels32();
                Dictionary<int, int>[] counts =
                    new Dictionary<int, int>[descriptor.SourcePalette.Length];
                for (int i = 0; i < counts.Length; i++)
                {
                    counts[i] = new Dictionary<int, int>();
                }

                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    if (sourcePixels[i].a == 0 || targetPixels[i].a == 0)
                    {
                        continue;
                    }

                    int role = FindClosestPaletteIndex(
                        sourcePixels[i],
                        descriptor.SourcePalette);
                    Color32 target = targetPixels[i];
                    int key = target.r |
                              target.g << 8 |
                              target.b << 16 |
                              target.a << 24;
                    counts[role].TryGetValue(key, out int count);
                    counts[role][key] = count + 1;
                }

                palette = new Color[descriptor.SourcePalette.Length];
                for (int role = 0; role < counts.Length; role++)
                {
                    int bestKey = 0;
                    int bestCount = 0;
                    foreach (KeyValuePair<int, int> pair in counts[role])
                    {
                        if (pair.Value > bestCount)
                        {
                            bestKey = pair.Key;
                            bestCount = pair.Value;
                        }
                    }

                    if (bestCount == 0)
                    {
                        palette = null;
                        return false;
                    }

                    palette[role] = new Color32(
                        (byte)(bestKey & 0xff),
                        (byte)((bestKey >> 8) & 0xff),
                        (byte)((bestKey >> 16) & 0xff),
                        (byte)((bestKey >> 24) & 0xff));
                }

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decodedSource);
                UnityEngine.Object.DestroyImmediate(decodedTarget);
            }
        }

        private static bool TryDecodeTexture(Texture2D texture, out Texture2D decoded)
        {
            decoded = null;
            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            string absolutePath = AssetPathToAbsolutePath(path);
            if (texture == null ||
                string.IsNullOrEmpty(absolutePath) ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(absolutePath))
            {
                return false;
            }

            decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (decoded.LoadImage(File.ReadAllBytes(absolutePath)))
            {
                return true;
            }

            UnityEngine.Object.DestroyImmediate(decoded);
            decoded = null;
            return false;
        }

        public static bool QueuePaletteBake(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            bool immediate = false)
        {
            LayerDescriptor descriptor = GetDescriptor(layer);
            if (descriptor.SourcePalette == null)
            {
                return false;
            }

            if (!TryResolveConsumerOwnership(
                    template,
                    profile,
                    out _,
                    out _,
                    out string ownershipError))
            {
                if (!string.IsNullOrEmpty(ownershipError))
                {
                    Debug.LogWarning(ownershipError, profile);
                }

                return false;
            }

            string profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
            string profileGuid = AssetDatabase.AssetPathToGUID(profilePath);
            if (string.IsNullOrEmpty(profileGuid))
            {
                return false;
            }

            string key = profileGuid + ":" + descriptor.Key;
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty reference = serializedProfile.FindProperty(
                descriptor.ReferenceProperty);
            TryReadReferenceAddress(reference, out long expectedLow, out long expectedHigh);
            DimensionPortalArtworkReferenceKind expectedKind = ClassifyReference(
                reference,
                profile,
                layer,
                out SpriteAsset expectedAsset);
            PendingBakes[key] = new PendingBake
            {
                Template = template,
                Profile = profile,
                Layer = layer,
                ExpectedAddressLow = expectedLow,
                ExpectedAddressHigh = expectedHigh,
                ExpectedAssetGuid = GetAssetGuid(expectedAsset),
                ExpectedReferenceKind = expectedKind,
                ExpectedPaletteHash = GetPaletteHash(
                    ReadPalette(serializedProfile, descriptor)),
                DueTime = immediate
                    ? EditorApplication.timeSinceStartup
                    : EditorApplication.timeSinceStartup + PaletteBakeDebounceSeconds
            };
            InstallUpdateHook();
            return true;
        }

        public static bool FlushPending(
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                return true;
            }

            string profileGuid = AssetDatabase.AssetPathToGUID(
                NormalizeAssetPath(AssetDatabase.GetAssetPath(profile)));
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, PendingBake> pair in PendingBakes)
            {
                if (pair.Key.StartsWith(profileGuid + ":", StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                PendingBake pending = PendingBakes[keys[i]];
                PendingBakes.Remove(keys[i]);
                if (!IsPendingBakeCurrent(pending))
                {
                    continue;
                }

                if (!CreateOrUpdateManagedArtwork(
                        pending.Template,
                        pending.Profile,
                        pending.Layer,
                        out message))
                {
                    RemoveUpdateHookIfIdle();
                    return false;
                }
            }

            RemoveUpdateHookIfIdle();
            // Swirls no longer bake per-color textures; their tint is applied at runtime.
            message = string.Empty;
            return true;
        }

        public static void CancelPending(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return;
            }

            string profileGuid = AssetDatabase.AssetPathToGUID(
                NormalizeAssetPath(AssetDatabase.GetAssetPath(profile)));
            if (string.IsNullOrEmpty(profileGuid))
            {
                return;
            }

            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, PendingBake> pair in PendingBakes)
            {
                if (pair.Key.StartsWith(profileGuid + ":", StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                PendingBakes.Remove(keys[i]);
            }

            RemoveUpdateHookIfIdle();
        }

        public static void CancelPending(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            if (profile == null)
            {
                return;
            }

            string profileGuid = AssetDatabase.AssetPathToGUID(
                NormalizeAssetPath(AssetDatabase.GetAssetPath(profile)));
            if (string.IsNullOrEmpty(profileGuid))
            {
                return;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            PendingBakes.Remove(profileGuid + ":" + descriptor.Key);
            RemoveUpdateHookIfIdle();
        }

        public static bool CreateEditableCopy(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            out string message)
        {
            return CreateOrUpdateManagedArtwork(template, profile, layer, out message, true);
        }

        private static void InstallUpdateHook()
        {
            if (updateHookInstalled)
            {
                return;
            }

            EditorApplication.update += ProcessPendingBakes;
            updateHookInstalled = true;
        }

        private static void RemoveUpdateHookIfIdle()
        {
            if (!updateHookInstalled || PendingBakes.Count > 0)
            {
                return;
            }

            EditorApplication.update -= ProcessPendingBakes;
            updateHookInstalled = false;
        }

        private static void ProcessPendingBakes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            List<string> ready = new List<string>();
            foreach (KeyValuePair<string, PendingBake> pair in PendingBakes)
            {
                if (pair.Value == null || pair.Value.DueTime <= now)
                {
                    ready.Add(pair.Key);
                }
            }

            for (int i = 0; i < ready.Count; i++)
            {
                if (!PendingBakes.TryGetValue(ready[i], out PendingBake pending))
                {
                    continue;
                }

                PendingBakes.Remove(ready[i]);
                if (pending == null || pending.Template == null || pending.Profile == null)
                {
                    continue;
                }

                if (!IsPendingBakeCurrent(pending))
                {
                    continue;
                }

                if (!CreateOrUpdateManagedArtwork(
                        pending.Template,
                        pending.Profile,
                        pending.Layer,
                        out string error))
                {
                    Debug.LogError("Dimensions API could not update portal artwork: " + error);
                }
            }

            RemoveUpdateHookIfIdle();
        }

        private static bool IsPendingBakeCurrent(PendingBake pending)
        {
            if (pending == null || pending.Profile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(pending.Layer);
            SerializedObject serializedProfile = new SerializedObject(pending.Profile);
            serializedProfile.Update();
            SerializedProperty reference = serializedProfile.FindProperty(
                descriptor.ReferenceProperty);
            if (!TryReadReferenceAddress(reference, out long low, out long high) ||
                low != pending.ExpectedAddressLow ||
                high != pending.ExpectedAddressHigh ||
                GetPaletteHash(ReadPalette(serializedProfile, descriptor)) !=
                pending.ExpectedPaletteHash)
            {
                return false;
            }

            // A different DataBlock can legally reuse an address. Resolve afresh at the
            // debounce boundary so a later artwork selection can never be overwritten
            // by work queued for the previous selection.
            InvalidateReferenceCache(pending.Profile, pending.Layer);
            DimensionPortalArtworkReferenceKind kind = ClassifyReference(
                reference,
                pending.Profile,
                pending.Layer,
                out SpriteAsset asset);
            return kind == pending.ExpectedReferenceKind &&
                   string.Equals(
                       GetAssetGuid(asset),
                       pending.ExpectedAssetGuid,
                       StringComparison.Ordinal);
        }

        private static void SetFrameworkReference(
            SerializedProperty reference,
            LayerDescriptor descriptor)
        {
            SetFrameworkReference(
                reference,
                descriptor.FrameworkAssetPath,
                descriptor.FrameworkAddressLow,
                descriptor.FrameworkAddressHigh);
        }

        private static void SetFrameworkReference(
            SerializedProperty reference,
            string frameworkAssetPath,
            long frameworkAddressLow,
            long frameworkAddressHigh)
        {
            SpriteAsset framework = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                frameworkAssetPath);
            if (framework != null)
            {
                ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, framework);
                return;
            }

            SerializedProperty address = reference == null
                ? null
                : reference.FindPropertyRelative("m_address");
            SerializedProperty low = address == null
                ? null
                : address.FindPropertyRelative("m_low");
            SerializedProperty high = address == null
                ? null
                : address.FindPropertyRelative("m_high");
            if (low != null && high != null)
            {
                low.longValue = frameworkAddressLow;
                high.longValue = frameworkAddressHigh;
            }
        }

        private static void SetPaletteToSource(
            SerializedObject serializedProfile,
            LayerDescriptor descriptor)
        {
            if (descriptor.SourcePalette == null)
            {
                return;
            }

            for (int i = 0; i < descriptor.PaletteProperties.Length; i++)
            {
                SerializedProperty color = serializedProfile.FindProperty(
                    descriptor.PaletteProperties[i]);
                if (color != null)
                {
                    color.colorValue = descriptor.SourcePalette[i];
                }
            }
        }

        private static bool PaletteMatchesSource(
            SerializedObject serializedProfile,
            LayerDescriptor descriptor)
        {
            if (descriptor.SourcePalette == null ||
                descriptor.PaletteProperties.Length != descriptor.SourcePalette.Length)
            {
                return true;
            }

            for (int i = 0; i < descriptor.PaletteProperties.Length; i++)
            {
                SerializedProperty color = serializedProfile.FindProperty(
                    descriptor.PaletteProperties[i]);
                Color32 target = color == null ? default(Color32) : (Color32)color.colorValue;
                Color32 source = descriptor.SourcePalette[i];
                if (target.r != source.r ||
                    target.g != source.g ||
                    target.b != source.b ||
                    target.a != source.a)
                {
                    return false;
                }
            }

            return true;
        }

        private static Color[] ReadPalette(
            SerializedObject serializedProfile,
            LayerDescriptor descriptor)
        {
            Color[] palette = new Color[descriptor.PaletteProperties.Length];
            for (int i = 0; i < palette.Length; i++)
            {
                SerializedProperty property = serializedProfile.FindProperty(
                    descriptor.PaletteProperties[i]);
                palette[i] = property == null
                    ? (Color)descriptor.SourcePalette[i]
                    : property.colorValue;
            }

            return palette;
        }

        private static int GetPaletteHash(Color[] palette)
        {
            unchecked
            {
                int hash = 17;
                if (palette == null)
                {
                    return hash;
                }

                for (int i = 0; i < palette.Length; i++)
                {
                    Color color = palette[i];
                    hash = hash * 31 + color.r.GetHashCode();
                    hash = hash * 31 + color.g.GetHashCode();
                    hash = hash * 31 + color.b.GetHashCode();
                    hash = hash * 31 + color.a.GetHashCode();
                }

                return hash;
            }
        }

        private static int FindClosestPaletteIndex(Color32 color, Color32[] palette)
        {
            int closest = 0;
            int closestDistance = int.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                int red = color.r - palette[i].r;
                int green = color.g - palette[i].g;
                int blue = color.b - palette[i].b;
                int distance = red * red + green * green + blue * blue;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = i;
                }
            }

            return closest;
        }
    }
}
