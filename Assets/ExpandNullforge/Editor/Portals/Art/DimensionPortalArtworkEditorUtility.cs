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
    /// Owns the editor-only transition between the framework's read-only portal art and
    /// consumer-owned, palette-editable SpriteAssets. Managed assets are deliberately kept
    /// outside Generated/Portal so generator cleanup can never delete author work.
    /// </summary>
    internal static partial class DimensionPortalArtworkEditorUtility
    {
        private const int ProfileDefaultsVersion = 6;
        internal const string FrameworkSwirlAssetPath =
            "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCustomSwirl.asset";
        internal const long FrameworkSwirlAddressLow = -6475803387308123471L;
        internal const long FrameworkSwirlAddressHigh = 7342631739284561011L;
        private const int MetadataSchemaVersion = 1;
        private const string MetadataPrefix = "ExpandNullforge.PortalArtwork:";
        private const string ManagedFolderSegment = "/Data/SpriteAsset/PortalArtwork/";
        private const double PaletteBakeDebounceSeconds = 0.28;

        /// <summary>
        /// Takes an in-memory copy of an asset for rollback.
        ///
        /// <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> appends "(Clone)" to
        /// the copy's name, and a snapshot is restored by copying it back over the live asset —
        /// name included. Left alone, that suffix lands on the real asset file and Unity's
        /// importer rejects it ("Main Object Name 'X(Clone)' does not match filename 'X'"),
        /// after which the asset can no longer be resolved in Scriptable Data. Keeping the
        /// original name here fixes every restore path at once.
        /// </summary>
        internal static T InstantiateSnapshot<T>(T source)
            where T : UnityEngine.Object
        {
            if (source == null)
            {
                return null;
            }

            T snapshot = UnityEngine.Object.Instantiate(source);
            snapshot.name = source.name;
            return snapshot;
        }

        /// <summary>
        /// The name Unity's importer requires for an on-disk asset: its own filename. Used when
        /// restoring a snapshot, so a stale or suffixed in-memory name can never be written to
        /// the real asset. Falls back to the supplied name for objects with no asset path.
        /// </summary>
        internal static string ResolveAssetFileName(
            UnityEngine.Object asset,
            string fallbackName)
        {
            string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? fallbackName
                : Path.GetFileNameWithoutExtension(path);
        }

        private static readonly Color32[] EffectSourcePalette =
        {
            new Color32(20, 43, 92, 255),
            new Color32(20, 62, 171, 255),
            new Color32(22, 93, 217, 255),
            new Color32(24, 133, 216, 255),
            new Color32(25, 189, 198, 255)
        };

        private static readonly Color32[] CenterSourcePalette =
        {
            new Color32(20, 43, 92, 255),
            new Color32(29, 48, 137, 255),
            new Color32(20, 62, 171, 255),
            new Color32(22, 93, 217, 255),
            new Color32(25, 189, 198, 255),
            new Color32(255, 255, 255, 255)
        };

        private static readonly LayerDescriptor[] Descriptors =
        {
            new LayerDescriptor
            {
                Layer = DimensionPortalArtworkLayer.Frame,
                Key = "frame",
                DisplayName = "Frame",
                ReferenceProperty = "portalFrameSpriteAsset",
                FrameworkAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalBody.asset",
                FrameworkAddressLow = -3868348571319720596L,
                FrameworkAddressHigh = 8501189908206782088L,
                SourcePalette = null,
                PaletteProperties = Array.Empty<string>()
            },
            new LayerDescriptor
            {
                Layer = DimensionPortalArtworkLayer.ChargeSweep,
                Key = "charge",
                DisplayName = "ChargeSweep",
                ReferenceProperty = "chargeWaveSpriteAsset",
                FrameworkAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalEmissiveWave.asset",
                FrameworkAddressLow = 7016284293423569344L,
                FrameworkAddressHigh = 7926038299096422560L,
                SourcePalette = EffectSourcePalette,
                PaletteProperties = new[]
                {
                    "chargeWaveDarkColor",
                    "chargeWaveDeepColor",
                    "chargeWaveMidColor",
                    "chargeWaveBrightColor",
                    "chargeWaveCoreColor"
                }
            },
            new LayerDescriptor
            {
                Layer = DimensionPortalArtworkLayer.Milestones,
                Key = "milestones",
                DisplayName = "Milestones",
                ReferenceProperty = "milestoneSpriteAsset",
                FrameworkAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalChargeProgress.asset",
                FrameworkAddressLow = 4724424159173606565L,
                FrameworkAddressHigh = 2046921296165717419L,
                SourcePalette = EffectSourcePalette,
                PaletteProperties = new[]
                {
                    "milestoneDarkColor",
                    "milestoneDeepColor",
                    "milestoneMidColor",
                    "milestoneBrightColor",
                    "milestoneCoreColor"
                }
            },
            new LayerDescriptor
            {
                Layer = DimensionPortalArtworkLayer.Center,
                Key = "center",
                DisplayName = "Center",
                ReferenceProperty = "centerEffectSpriteAsset",
                FrameworkAssetPath = "Assets/ExpandNullforge/PortalVisuals/SpriteAsset/PortalCenterEffect.asset",
                FrameworkAddressLow = 5570163353262010590L,
                FrameworkAddressHigh = -2439472303744231237L,
                SourcePalette = CenterSourcePalette,
                PaletteProperties = new[]
                {
                    "centerDarkColor",
                    "centerDeepColor",
                    "centerMidColor",
                    "centerBrightColor",
                    "centerCoreColor",
                    "centerHighlightColor"
                }
            }
        };

        // The instant item portal's center descriptor. Deliberately NOT part of Descriptors:
        // EnsureProfileInitialized and the legacy palette migration iterate that array, and both
        // must keep treating "centerEffectSpriteAsset" as the placed-portal Center contract.
        private static readonly LayerDescriptor InstantCenterDescriptor = new LayerDescriptor
        {
            Layer = DimensionPortalArtworkLayer.CenterInstant,
            Key = "centerInstant",
            DisplayName = "CenterInstant",
            ReferenceProperty = "centerEffectSpriteAsset",
            FrameworkAssetPath =
                DimensionPortalInstantArtworkEditorUtility.InstantCenterAssetPath,
            FrameworkAddressLow =
                DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressLow,
            FrameworkAddressHigh =
                DimensionPortalInstantArtworkEditorUtility.InstantCenterAddressHigh,
            SourcePalette = CenterSourcePalette,
            PaletteProperties = new[]
            {
                "centerDarkColor",
                "centerDeepColor",
                "centerMidColor",
                "centerBrightColor",
                "centerCoreColor",
                "centerHighlightColor"
            }
        };

        private static readonly Dictionary<string, PendingBake> PendingBakes =
            new Dictionary<string, PendingBake>(StringComparer.Ordinal);
        private static readonly Dictionary<ReferenceCacheKey, ReferenceCacheEntry> ReferenceCache =
            new Dictionary<ReferenceCacheKey, ReferenceCacheEntry>();
        private static readonly SpriteAsset[] FrameworkAssetCache =
            new SpriteAsset[5];
        private static bool updateHookInstalled;

        static DimensionPortalArtworkEditorUtility()
        {
            EditorApplication.projectChanged += InvalidateCachesOnProjectChange;
            Undo.undoRedoPerformed += InvalidateReferenceCache;
        }

        public static bool EnsureProfileInitialized(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            out string message)
        {
            message = string.Empty;
            if (profile == null)
            {
                return false;
            }

            // This method is called from IMGUI. Keep the already-initialized path free
            // of ownership and mod-root discovery, which performs project-wide asset
            // searches and is only required while migrating an older profile.
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty version = serialized.FindProperty("portalArtworkDefaultsVersion");
            if (version == null || version.intValue >= ProfileDefaultsVersion)
            {
                return false;
            }

            int previousVersion = version.intValue;

            if (!TryResolveConsumerOwnership(
                    template,
                    profile,
                    out _,
                    out _,
                    out message))
            {
                return false;
            }

            List<DimensionPortalArtworkLayer> legacyPalettes =
                new List<DimensionPortalArtworkLayer>();
            Undo.RecordObject(profile, "Initialize portal artwork defaults");
            for (int i = 0; i < Descriptors.Length; i++)
            {
                LayerDescriptor descriptor = Descriptors[i];
                SerializedProperty reference = serialized.FindProperty(descriptor.ReferenceProperty);
                if (HasAddress(reference))
                {
                    continue;
                }

                if (descriptor.SourcePalette != null &&
                    !PaletteMatchesSource(serialized, descriptor))
                {
                    // Preserve legacy authored palettes. They are materialized after the
                    // schema/default transaction has been safely committed.
                    legacyPalettes.Add(descriptor.Layer);
                    continue;
                }

                SetFrameworkReference(reference, descriptor);
            }

            if (previousVersion < 2)
            {
                SerializedProperty flecksFollowCenter =
                    serialized.FindProperty("centerParticlesFollowCenterPalette");
                SerializedProperty burstFollowsCenter =
                    serialized.FindProperty("readyFlashFollowsCenterPalette");
                if (flecksFollowCenter != null)
                {
                    flecksFollowCenter.boolValue = true;
                }

                if (burstFollowsCenter != null)
                {
                    burstFollowsCenter.boolValue = true;
                }
            }

            if (previousVersion < 3)
            {
                string[] visibleProperties =
                {
                    "frameVisible",
                    "chargeWaveVisible",
                    "milestonesVisible",
                    "centerVisible",
                    "portalShadowEnabled"
                };
                for (int i = 0; i < visibleProperties.Length; i++)
                {
                    SerializedProperty property = serialized.FindProperty(visibleProperties[i]);
                    if (property != null)
                    {
                        property.boolValue = true;
                    }
                }

                string[] scaleProperties =
                {
                    "frameScale",
                    "chargeWaveScale",
                    "milestoneScale",
                    "centerScale",
                    "centerParticleScale",
                    "portalShadowScale"
                };
                for (int i = 0; i < scaleProperties.Length; i++)
                {
                    SerializedProperty property = serialized.FindProperty(scaleProperties[i]);
                    if (property != null)
                    {
                        property.vector2Value = Vector2.one;
                    }
                }
            }

            if (previousVersion < 4)
            {
                // The field was introduced after the first transform-layout schema.
                // Serialized bools default to false, which would silently turn off the
                // previously-visible ground light on every existing visual profile.
                SerializedProperty groundLightEnabled =
                    serialized.FindProperty("groundLightEnabled");
                if (groundLightEnabled != null)
                {
                    groundLightEnabled.boolValue = true;
                }
            }

            if (previousVersion < 5)
            {
                SerializedProperty followCenter =
                    serialized.FindProperty("centerParticlesFollowCenterPalette");
                if (followCenter != null && followCenter.boolValue)
                {
                    SerializedProperty explicitTint =
                        serialized.FindProperty("centerParticleTint");
                    if (explicitTint != null)
                    {
                        LayerDescriptor centerDescriptor = GetDescriptor(
                            DimensionPortalArtworkLayer.Center);
                        if (PaletteMatchesSource(serialized, centerDescriptor))
                        {
                            explicitTint.colorValue = Color.white;
                        }
                        else
                        {
                            SerializedProperty centerCore =
                                serialized.FindProperty("centerCoreColor");
                            Color core = centerCore == null
                                ? Color.white
                                : centerCore.colorValue;
                            float maximum = Mathf.Max(
                                core.r,
                                Mathf.Max(core.g, core.b));
                            explicitTint.colorValue = maximum <= 0.0001f
                                ? new Color(0.0f, 0.0f, 0.0f, core.a)
                                : new Color(
                                    Mathf.Clamp01(core.r / maximum),
                                    Mathf.Clamp01(core.g / maximum),
                                    Mathf.Clamp01(core.b / maximum),
                                    core.a);
                        }
                    }

                    followCenter.boolValue = false;
                }
            }

            if (previousVersion < 6)
            {
                // The exact vanilla swirl remains a ParticleSystem at runtime, but Portal
                // Studio exposes a framework-owned animated SpriteAsset as the authoring
                // starting point for the optional custom SpriteObject mode. Version 5
                // profiles can also have a zero address when they were saved before the
                // starter reference was materialized, so repair every pre-v6 empty
                // reference instead of limiting this to the v4 -> v5 transition.
                SerializedProperty swirlReference =
                    serialized.FindProperty("centerSwirlSpriteAsset");
                if (!HasAddress(swirlReference))
                {
                    SetFrameworkReference(
                        swirlReference,
                        FrameworkSwirlAssetPath,
                        FrameworkSwirlAddressLow,
                        FrameworkSwirlAddressHigh);
                }

                SerializedProperty swirlVisible =
                    serialized.FindProperty("centerSwirlVisible");
                if (swirlVisible != null)
                {
                    swirlVisible.boolValue = true;
                }

                SerializedProperty swirlEmission =
                    serialized.FindProperty("centerSwirlEmissiveColor");
                if (swirlEmission != null && swirlEmission.colorValue.maxColorComponent <= 0f)
                {
                    swirlEmission.colorValue = Color.white;
                }
            }

            version.intValue = ProfileDefaultsVersion;
            bool changed = serialized.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(profile);
            }

            for (int i = 0; i < legacyPalettes.Count; i++)
            {
                QueuePaletteBake(template, profile, legacyPalettes[i], true);
            }

            if (legacyPalettes.Count > 0)
            {
                message = "Preserved the existing custom portal palette and queued consumer-owned artwork for it.";
            }

            return changed || legacyPalettes.Count > 0;
        }

        public static DimensionPortalArtworkReferenceKind ClassifyReference(
            SerializedProperty reference,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            out SpriteAsset asset)
        {
            asset = null;
            LayerDescriptor descriptor = GetDescriptor(layer);
            if (reference == null ||
                !TryReadReferenceAddress(reference, out long addressLow, out long addressHigh) ||
                (addressLow == 0L && addressHigh == 0L))
            {
                return DimensionPortalArtworkReferenceKind.Empty;
            }

            ReferenceCacheKey cacheKey = new ReferenceCacheKey(
                profile == null ? 0 : profile.GetInstanceID(),
                layer,
                addressLow,
                addressHigh,
                ScriptableDataEditorUtility.currentContext.directory);
            if (ReferenceCache.TryGetValue(cacheKey, out ReferenceCacheEntry cached) &&
                cached != null &&
                cached.Asset != null)
            {
                asset = cached.Asset;
                return cached.Kind;
            }

            ScriptableDataEditorUtility.GetDataBlock(reference, out asset);
            if (asset == null)
            {
                // Registry miss: the artwork may still exist outside anything Scriptable Data
                // indexes, so look it up by address before declaring the layer unresolved.
                asset = FindSpriteAssetByAddress(addressLow, addressHigh, string.Empty);
            }

            DimensionPortalArtworkReferenceKind kind;
            if (asset != null)
            {
                string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                if (AssetPathsEqual(assetPath, descriptor.FrameworkAssetPath))
                {
                    kind = DimensionPortalArtworkReferenceKind.Framework;
                }
                else
                {
                    kind = TryReadManagedMetadata(assetPath, profile, descriptor, out _)
                        ? DimensionPortalArtworkReferenceKind.Managed
                        : DimensionPortalArtworkReferenceKind.External;
                }

                ReferenceCache[cacheKey] = new ReferenceCacheEntry
                {
                    Asset = asset,
                    Kind = kind
                };
                return kind;
            }

            // The SDK rebuilds its address -> DataBlock lookup on a later editor callback.
            // Package creation, however, must validate its closure in the same transaction
            // that created the SpriteAssets. Resolve a freshly-created managed asset from
            // this profile's single layer folder when the global lookup has not caught up
            // yet. The address and ownership metadata both have to match, so this cannot
            // adopt an arbitrary consumer SpriteAsset.
            if (profile != null)
            {
                string profilePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(profile));
                string profileGuid = string.IsNullOrEmpty(profilePath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(profilePath);
                string modRoot = NormalizeAssetPath(
                    DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(profilePath));
                if (TryFindManagedArtworkByAddress(
                        modRoot,
                        profileGuid,
                        profile,
                        descriptor,
                        addressLow,
                        addressHigh,
                        out SpriteAsset managed))
                {
                    asset = managed;
                    kind = DimensionPortalArtworkReferenceKind.Managed;
                    ReferenceCache[cacheKey] = new ReferenceCacheEntry
                    {
                        Asset = asset,
                        Kind = kind
                    };
                    return kind;
                }
            }

            // DataBlockRef addresses are not globally unique while authoring. Only use
            // the framework address as a fallback when the selected reference could not
            // be resolved to an actual asset whose path/metadata can establish ownership.
            if (AddressMatches(reference, descriptor.FrameworkAddressLow, descriptor.FrameworkAddressHigh))
            {
                asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(descriptor.FrameworkAssetPath);
                if (asset != null)
                {
                    kind = DimensionPortalArtworkReferenceKind.Framework;
                    ReferenceCache[cacheKey] = new ReferenceCacheEntry
                    {
                        Asset = asset,
                        Kind = kind
                    };
                    return kind;
                }
            }

            kind = DimensionPortalArtworkReferenceKind.Unresolved;
            return kind;
        }

        public static void InvalidateReferenceCache()
        {
            ReferenceCache.Clear();
        }

        public static void InvalidateReferenceCache(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            int profileInstanceId = profile == null ? 0 : profile.GetInstanceID();
            List<ReferenceCacheKey> keys = new List<ReferenceCacheKey>();
            foreach (ReferenceCacheKey key in ReferenceCache.Keys)
            {
                if (key.ProfileInstanceId == profileInstanceId && key.Layer == layer)
                {
                    keys.Add(key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                ReferenceCache.Remove(keys[i]);
            }
        }

        private static void CacheResolvedReference(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            long addressLow,
            long addressHigh,
            SpriteAsset asset,
            DimensionPortalArtworkReferenceKind kind)
        {
            if (profile == null || asset == null)
            {
                return;
            }

            ReferenceCache[new ReferenceCacheKey(
                profile.GetInstanceID(),
                layer,
                addressLow,
                addressHigh,
                ScriptableDataEditorUtility.currentContext.directory)] =
                new ReferenceCacheEntry
                {
                    Asset = asset,
                    Kind = kind
                };
        }

        private static void InvalidateCachesOnProjectChange()
        {
            InvalidateReferenceCache();
            for (int i = 0; i < FrameworkAssetCache.Length; i++)
            {
                FrameworkAssetCache[i] = null;
            }
        }

        public static bool IsFrameworkReference(
            DataBlockRef<SpriteAsset> reference,
            DimensionPortalArtworkLayer layer)
        {
            if (!reference.hasAddress)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            if (reference.TryGet(out SpriteAsset resolved) && resolved != null)
            {
                string resolvedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(resolved));
                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    return AssetPathsEqual(resolvedPath, descriptor.FrameworkAssetPath);
                }
            }

            // Preserve the address fallback for runtime-bootstrap authoring passes where
            // the SDK has not populated its ScriptableData lookup yet.
            return reference.address.lowBits == descriptor.FrameworkAddressLow &&
                   reference.address.highBits == descriptor.FrameworkAddressHigh;
        }

        public static SpriteAsset GetFrameworkAsset(DimensionPortalArtworkLayer layer)
        {
            int index = (int)layer;
            SpriteAsset cached = FrameworkAssetCache[index];
            if (cached == null)
            {
                // The instant center asset is built in editor code from the shipped Instant
                // sheets, so make sure it exists before the first load resolves it.
                if (layer == DimensionPortalArtworkLayer.CenterInstant)
                {
                    DimensionPortalInstantArtworkEditorUtility.EnsureFrameworkCenterAsset(out _);
                }

                cached = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    GetDescriptor(layer).FrameworkAssetPath);
                FrameworkAssetCache[index] = cached;
            }

            return cached;
        }

        private static LayerDescriptor GetDescriptor(DimensionPortalArtworkLayer layer)
        {
            if (layer == DimensionPortalArtworkLayer.CenterInstant)
            {
                return InstantCenterDescriptor;
            }

            for (int i = 0; i < Descriptors.Length; i++)
            {
                if (Descriptors[i].Layer == layer)
                {
                    return Descriptors[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
        }
    }
}
