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
    internal enum DimensionPortalArtworkLayer
    {
        Frame,
        ChargeSweep,
        Milestones,
        Center,

        // The instant item portal's center: same profile reference property as Center, but a
        // three-animation framework contract (idle, opening, closing — five frames each). Kept
        // out of the Descriptors array so profile initialization and legacy palette migration
        // keep operating on the placed-portal contract only.
        CenterInstant
    }

    internal enum DimensionPortalArtworkReferenceKind
    {
        Empty,
        Framework,
        Managed,
        External,
        Unresolved
    }

    /// <summary>
    /// Owns the editor-only transition between the framework's read-only portal art and
    /// consumer-owned, palette-editable SpriteAssets. Managed assets are deliberately kept
    /// outside Generated/Portal so generator cleanup can never delete author work.
    /// </summary>
    internal static class DimensionPortalArtworkEditorUtility
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

        private sealed class LayerDescriptor
        {
            public DimensionPortalArtworkLayer Layer;
            public string Key;
            public string DisplayName;
            public string ReferenceProperty;
            public string FrameworkAssetPath;
            public long FrameworkAddressLow;
            public long FrameworkAddressHigh;
            public Color32[] SourcePalette;
            public string[] PaletteProperties;
        }

        /// <summary>
        /// One native texture slot exposed by a portal SpriteAsset. Frame has one static
        /// slot; ChargeSweep and Milestones have one animation slot; Center has its mature
        /// loop and opening slots. Frame count comes from the immutable framework contract;
        /// Center dimensions may use either native 16 x 25 frames or full-canvas 48 x 48
        /// frames, while Texture/EmissiveTexture reflect the selected asset.
        /// </summary>
        internal sealed class TextureSlot
        {
            public string DisplayName;
            public int AnimationIndex;
            public int FrameCount;
            public int RequiredWidth;
            public int RequiredHeight;
            public int NativeWidth;
            public int NativeHeight;
            public int AlternateWidth;
            public int AlternateHeight;
            public Texture2D Texture;
            public Texture2D EmissiveTexture;
            public Texture2D NormalTexture;

            public bool IsStatic => AnimationIndex < 0;
            public bool HasAlternateSize => AlternateWidth > 0 && AlternateHeight > 0;
        }

        private sealed class TextureReplacement
        {
            public Texture2D[] Textures;
            public Texture2D[] EmissiveTextures;
            public Texture2D[] NormalTextures;
        }

        [Serializable]
        private sealed class ManagedArtworkMetadata
        {
            public int schemaVersion;
            public string owner;
            public string profileGuid;
            public string layer;
            public string sourceAssetGuid;
            public long sourceAddressLow;
            public long sourceAddressHigh;
            public string managedAssetGuid;
            public long managedAddressLow;
            public long managedAddressHigh;
            public Color[] palette;
            public bool directTextureOverride;
        }

        private sealed class PendingBake
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public DimensionPortalArtworkLayer Layer;
            public double DueTime;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public string ExpectedAssetGuid;
            public DimensionPortalArtworkReferenceKind ExpectedReferenceKind;
            public int ExpectedPaletteHash;
        }

        private sealed class ReferenceCacheEntry
        {
            public SpriteAsset Asset;
            public DimensionPortalArtworkReferenceKind Kind;
        }

        private readonly struct ReferenceCacheKey : IEquatable<ReferenceCacheKey>
        {
            public readonly int ProfileInstanceId;
            public readonly DimensionPortalArtworkLayer Layer;
            public readonly long AddressLow;
            public readonly long AddressHigh;
            public readonly string ContextDirectory;

            public ReferenceCacheKey(
                int profileInstanceId,
                DimensionPortalArtworkLayer layer,
                long addressLow,
                long addressHigh,
                string contextDirectory)
            {
                ProfileInstanceId = profileInstanceId;
                Layer = layer;
                AddressLow = addressLow;
                AddressHigh = addressHigh;
                ContextDirectory = contextDirectory ?? string.Empty;
            }

            public bool Equals(ReferenceCacheKey other)
            {
                return ProfileInstanceId == other.ProfileInstanceId &&
                       Layer == other.Layer &&
                       AddressLow == other.AddressLow &&
                       AddressHigh == other.AddressHigh &&
                       string.Equals(
                           ContextDirectory,
                           other.ContextDirectory,
                           StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is ReferenceCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ProfileInstanceId;
                    hash = hash * 397 ^ (int)Layer;
                    hash = hash * 397 ^ AddressLow.GetHashCode();
                    hash = hash * 397 ^ AddressHigh.GetHashCode();
                    hash = hash * 397 ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        ContextDirectory);
                    return hash;
                }
            }
        }

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

        public static bool IsDirectOverride(
            SerializedProperty reference,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            DimensionPortalArtworkReferenceKind kind =
                ClassifyReference(reference, profile, layer, out _);
            return kind == DimensionPortalArtworkReferenceKind.External;
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

        /// <summary>
        /// Reads the selected layer's texture selectors together with the immutable native
        /// framework dimensions. This performs bounded object inspection only; callers should
        /// cache the result for their IMGUI frame and refresh it after a selector changes.
        /// </summary>
        public static bool TryGetTextureSlots(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            out TextureSlot[] slots,
            out string message)
        {
            slots = Array.Empty<TextureSlot>();
            message = string.Empty;
            SpriteAsset framework = GetFrameworkAsset(layer);
            if (framework == null)
            {
                message = "Could not load the framework " +
                          GetDescriptor(layer).DisplayName + " SpriteAsset.";
                return false;
            }

            SpriteAsset selected = framework;
            if (profile != null)
            {
                LayerDescriptor descriptor = GetDescriptor(layer);
                SerializedObject serializedProfile = new SerializedObject(profile);
                serializedProfile.Update();
                SerializedProperty reference = serializedProfile.FindProperty(
                    descriptor.ReferenceProperty);
                DimensionPortalArtworkReferenceKind kind = ClassifyReference(
                    reference,
                    profile,
                    layer,
                    out SpriteAsset resolved);
                if (resolved != null && kind != DimensionPortalArtworkReferenceKind.Unresolved)
                {
                    selected = resolved;
                }
            }

            return TryBuildTextureSlots(framework, selected, layer, out slots, out message);
        }

        /// <summary>
        /// Replaces every native texture slot for a portal layer without ever mutating the
        /// framework or an externally-selected SpriteAsset. The first edit creates a stable,
        /// consumer-owned SpriteAsset and later edits update that same managed copy.
        ///
        /// Arrays must contain exactly one entry for Frame, ChargeSweep and Milestones, and
        /// exactly two entries for Center (mature loop, then opening). A null color entry
        /// restores that slot's framework color sheet. Emissive sheets are optional as an
        /// array; when supplied its length must match and a null entry restores the framework
        /// emissive contract. Normal sheets follow the same optional-array/null-restores-
        /// framework rules. Every non-null input must be a saved PNG inside the consumer mod
        /// (or the framework), at an allowed sheet size. Center accepts its native 16 x 25
        /// frames or full-canvas 48 x 48 frames; all channels in a slot must match. Selected
        /// PNG bytes are copied beside the managed SpriteAsset and imported point-filtered/
        /// uncompressed; normal copies use linear normal-map import semantics.
        /// </summary>
        public static bool CreateOrUpdateLayerTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            out string message)
        {
            Texture2D[] currentNormals = null;
            if (TryGetTextureSlots(
                    profile,
                    layer,
                    out TextureSlot[] currentSlots,
                    out _))
            {
                currentNormals = new Texture2D[currentSlots.Length];
                for (int i = 0; i < currentSlots.Length; i++)
                {
                    currentNormals[i] = currentSlots[i].NormalTexture;
                }
            }

            return CreateOrUpdateLayerTextures(
                template,
                profile,
                layer,
                textures,
                emissiveTextures,
                currentNormals,
                out message);
        }

        public static bool CreateOrUpdateLayerTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            Texture2D[] normalTextures,
            out string message)
        {
            message = string.Empty;
            SpriteAsset framework = GetFrameworkAsset(layer);
            if (framework == null ||
                !TryBuildTextureSlots(
                    framework,
                    framework,
                    layer,
                    out TextureSlot[] contract,
                    out message))
            {
                return false;
            }

            if (textures == null || textures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " color texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") + ".";
                return false;
            }

            if (emissiveTextures != null && emissiveTextures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " emissive texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") +
                          " when an emissive array is supplied.";
                return false;
            }

            if (normalTextures != null && normalTextures.Length != contract.Length)
            {
                message = GetDescriptor(layer).DisplayName + " requires exactly " +
                          contract.Length + " normal texture slot" +
                          (contract.Length == 1 ? string.Empty : "s") +
                          " when a normal array is supplied.";
                return false;
            }

            Texture2D[] normalizedEmissive = emissiveTextures ??
                                             new Texture2D[contract.Length];
            Texture2D[] normalizedNormals = normalTextures ??
                                            new Texture2D[contract.Length];
            return CreateOrUpdateManagedArtwork(
                template,
                profile,
                layer,
                out message,
                false,
                new TextureReplacement
                {
                    Textures = (Texture2D[])textures.Clone(),
                    EmissiveTextures = (Texture2D[])normalizedEmissive.Clone(),
                    NormalTextures = (Texture2D[])normalizedNormals.Clone()
                });
        }

        /// <summary>
        /// Returns true only for a profile-owned SpriteAsset created by the direct texture
        /// API. Semantic palette bakes intentionally leave these exact authored sheets alone.
        /// </summary>
        public static bool UsesDirectTextureOverride(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer)
        {
            if (profile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            SerializedObject serializedProfile = new SerializedObject(profile);
            serializedProfile.Update();
            SerializedProperty reference = serializedProfile.FindProperty(
                descriptor.ReferenceProperty);
            DimensionPortalArtworkReferenceKind kind = ClassifyReference(
                reference,
                profile,
                layer,
                out SpriteAsset asset);
            if (kind != DimensionPortalArtworkReferenceKind.Managed || asset == null)
            {
                return false;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            return TryReadManagedMetadata(
                       assetPath,
                       profile,
                       descriptor,
                       out ManagedArtworkMetadata metadata) &&
                   metadata.directTextureOverride;
        }

        public static bool AssignFrameworkReference(
            SerializedObject serializedProfile,
            DimensionPortalArtworkLayer layer)
        {
            if (serializedProfile == null)
            {
                return false;
            }

            LayerDescriptor descriptor = GetDescriptor(layer);
            SerializedProperty reference = serializedProfile.FindProperty(descriptor.ReferenceProperty);
            if (reference == null)
            {
                return false;
            }

            SetFrameworkReference(reference, descriptor);
            SetPaletteToSource(serializedProfile, descriptor);
            InvalidateReferenceCache();
            return true;
        }

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

        public static bool CreateOrUpdateFrameTextures(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            Texture2D frameTexture,
            Texture2D emissiveTexture,
            out string message)
        {
            return CreateOrUpdateLayerTextures(
                template,
                profile,
                DimensionPortalArtworkLayer.Frame,
                new[] { frameTexture },
                new[] { emissiveTexture },
                out message);
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
                    EnsureFolder(folder);
                    string baseName = SanitizeFileName(profile.name) + "_" + descriptor.DisplayName;
                    managedPath = AssetDatabase.GenerateUniqueAssetPath(
                        folder + "/" + baseName + ".asset");
                    managed = UnityEngine.Object.Instantiate(source);
                    managed.name = Path.GetFileNameWithoutExtension(managedPath);
                    SetSpriteAssetAddress(
                        managed,
                        ComputeStableAddressPart(managedPath, 0x706F7274616C6172UL),
                        ComputeStableAddressPart(managedPath, 0x7469737472796170UL));
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
                    addressLow = ComputeStableAddressPart(managedPath, 0x706F7274616C6172UL);
                    addressHigh = ComputeStableAddressPart(managedPath, 0x7469737472796170UL);
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

        private static bool TryBuildTextureSlots(
            SpriteAsset framework,
            SpriteAsset selected,
            DimensionPortalArtworkLayer layer,
            out TextureSlot[] slots,
            out string message)
        {
            slots = Array.Empty<TextureSlot>();
            message = string.Empty;
            if (framework == null || selected == null)
            {
                message = "The portal artwork SpriteAsset could not be resolved.";
                return false;
            }

            SerializedObject serializedFramework = new SerializedObject(framework);
            serializedFramework.Update();
            SerializedObject serializedSelected = new SerializedObject(selected);
            serializedSelected.Update();
            if (layer == DimensionPortalArtworkLayer.Frame)
            {
                SerializedProperty frameworkData = serializedFramework.FindProperty(
                    "m_staticSpriteData");
                SerializedProperty selectedData = serializedSelected.FindProperty(
                    "m_staticSpriteData");
                if (!TryReadSpriteDataTextures(
                        frameworkData,
                        out Texture2D frameworkTexture,
                        out _,
                        out _) ||
                    frameworkTexture == null ||
                    !TryReadSpriteDataTextures(
                        selectedData,
                        out Texture2D selectedTexture,
                        out Texture2D selectedEmissive,
                        out Texture2D selectedNormal))
                {
                    message = "The Frame SpriteAsset does not expose its static texture contract.";
                    return false;
                }

                slots = new[]
                {
                    new TextureSlot
                    {
                        DisplayName = "Frame",
                        AnimationIndex = -1,
                        FrameCount = 1,
                        RequiredWidth = frameworkTexture.width,
                        RequiredHeight = frameworkTexture.height,
                        NativeWidth = frameworkTexture.width,
                        NativeHeight = frameworkTexture.height,
                        Texture = selectedTexture,
                        EmissiveTexture = selectedEmissive,
                        NormalTexture = selectedNormal
                    }
                };
                return true;
            }

            SerializedProperty frameworkAnimations = serializedFramework.FindProperty(
                "m_animations");
            SerializedProperty selectedAnimations = serializedSelected.FindProperty(
                "m_animations");
            if (frameworkAnimations == null ||
                selectedAnimations == null ||
                frameworkAnimations.arraySize == 0 ||
                selectedAnimations.arraySize != frameworkAnimations.arraySize)
            {
                message = "The selected " + GetDescriptor(layer).DisplayName +
                          " SpriteAsset does not match the framework animation-slot contract.";
                return false;
            }

            slots = new TextureSlot[frameworkAnimations.arraySize];
            for (int i = 0; i < frameworkAnimations.arraySize; i++)
            {
                SerializedProperty frameworkAnimation =
                    frameworkAnimations.GetArrayElementAtIndex(i);
                SerializedProperty selectedAnimation =
                    selectedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty frameCountProperty =
                    frameworkAnimation.FindPropertyRelative("srcFrameCount");
                SerializedProperty selectedFrameCountProperty =
                    selectedAnimation.FindPropertyRelative("srcFrameCount");
                SerializedProperty frameworkData =
                    frameworkAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty selectedData =
                    selectedAnimation.FindPropertyRelative("m_spriteData");
                if (!TryReadSpriteDataTextures(
                        frameworkData,
                        out Texture2D frameworkTexture,
                        out _,
                        out _) ||
                    frameworkTexture == null ||
                    !TryReadSpriteDataTextures(
                        selectedData,
                        out Texture2D selectedTexture,
                        out Texture2D selectedEmissive,
                        out Texture2D selectedNormal))
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName +
                              " does not expose a complete texture contract.";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                int selectedFrameCount = selectedFrameCountProperty == null
                    ? 0
                    : selectedFrameCountProperty.intValue;
                if (frameCount <= 0 || frameworkTexture.width % frameCount != 0)
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName +
                              " is not an evenly-divided native sheet.";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                if (selectedFrameCount != frameCount)
                {
                    message = "Animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " must preserve its " +
                              frameCount + " source frames. Selected: " +
                              selectedFrameCount + ".";
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                if (!TryValidateSelectedTextureContract(
                        layer,
                        frameCount,
                        frameworkTexture,
                        selectedTexture,
                        selectedEmissive,
                        selectedNormal,
                        GetTextureSlotDisplayName(layer, i),
                        out message))
                {
                    slots = Array.Empty<TextureSlot>();
                    return false;
                }

                bool supportsFullCanvas =
                    layer == DimensionPortalArtworkLayer.Center ||
                    layer == DimensionPortalArtworkLayer.CenterInstant;

                slots[i] = new TextureSlot
                {
                    DisplayName = GetTextureSlotDisplayName(layer, i),
                    AnimationIndex = i,
                    FrameCount = frameCount,
                    RequiredWidth = selectedTexture.width,
                    RequiredHeight = selectedTexture.height,
                    NativeWidth = frameworkTexture.width,
                    NativeHeight = frameworkTexture.height,
                    AlternateWidth = supportsFullCanvas
                        ? frameCount * DimensionPortalVisualContract.CanonicalFramePixels
                        : 0,
                    AlternateHeight = supportsFullCanvas
                        ? DimensionPortalVisualContract.CanonicalFramePixels
                        : 0,
                    Texture = selectedTexture,
                    EmissiveTexture = selectedEmissive,
                    NormalTexture = selectedNormal
                };
            }

            return true;
        }

        private static bool TryValidateSelectedTextureContract(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            Texture2D frameworkTexture,
            Texture2D selectedTexture,
            Texture2D selectedEmissive,
            Texture2D selectedNormal,
            string label,
            out string message)
        {
            message = string.Empty;
            if (frameworkTexture == null || selectedTexture == null)
            {
                message = label + " has no usable color sheet.";
                return false;
            }

            if (!IsAllowedAnimatedSheetSize(
                    layer,
                    frameCount,
                    frameworkTexture.width,
                    frameworkTexture.height,
                    selectedTexture.width,
                    selectedTexture.height))
            {
                message = label + " color sheet must be " +
                          GetAllowedAnimatedSheetSizeText(
                              layer,
                              frameCount,
                              frameworkTexture.width,
                              frameworkTexture.height) +
                          ". Selected: " + selectedTexture.width + " x " +
                          selectedTexture.height + ".";
                return false;
            }

            if (!TextureMatchesSize(selectedEmissive, selectedTexture.width, selectedTexture.height))
            {
                message = label + " emissive sheet must match its color sheet at " +
                          selectedTexture.width + " x " + selectedTexture.height + ". Selected: " +
                          selectedEmissive.width + " x " + selectedEmissive.height + ".";
                return false;
            }

            if (!TextureMatchesSize(selectedNormal, selectedTexture.width, selectedTexture.height))
            {
                message = label + " normal sheet must match its color sheet at " +
                          selectedTexture.width + " x " + selectedTexture.height + ". Selected: " +
                          selectedNormal.width + " x " + selectedNormal.height + ".";
                return false;
            }

            return true;
        }

        private static bool IsAllowedAnimatedSheetSize(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            int nativeWidth,
            int nativeHeight,
            int candidateWidth,
            int candidateHeight)
        {
            if (candidateWidth == nativeWidth && candidateHeight == nativeHeight)
            {
                return true;
            }

            // Beyond the exact vanilla-native size, allow any evenly-framed custom sheet whose
            // frames fit within the 48x48 portal canvas. Creators can make an overlay larger,
            // smaller, or differently proportioned than vanilla as long as it stays inside the
            // artboard; the Studio and generated portal clip anything beyond it.
            int cap = DimensionPortalVisualContract.CanonicalFramePixels;
            return frameCount > 0 &&
                   candidateWidth > 0 &&
                   candidateHeight > 0 &&
                   candidateWidth % frameCount == 0 &&
                   candidateWidth / frameCount <= cap &&
                   candidateHeight <= cap;
        }

        private static bool TextureMatchesSize(Texture2D texture, int width, int height)
        {
            return texture == null || (texture.width == width && texture.height == height);
        }

        private static string GetAllowedAnimatedSheetSizeText(
            DimensionPortalArtworkLayer layer,
            int frameCount,
            int nativeWidth,
            int nativeHeight)
        {
            int cap = DimensionPortalVisualContract.CanonicalFramePixels;
            return nativeWidth + " x " + nativeHeight +
                   " pixels (native), or any evenly-framed sheet whose frames fit within " +
                   cap + " x " + cap + " pixels";
        }

        private static bool TryReadSpriteDataTextures(
            SerializedProperty spriteData,
            out Texture2D texture,
            out Texture2D emissive,
            out Texture2D normal)
        {
            texture = null;
            emissive = null;
            normal = null;
            if (spriteData == null)
            {
                return false;
            }

            SerializedProperty textureProperty = spriteData.FindPropertyRelative("texture");
            SerializedProperty emissiveProperty =
                spriteData.FindPropertyRelative("emissiveTexture");
            SerializedProperty normalProperty =
                spriteData.FindPropertyRelative("normalTexture");
            if (textureProperty == null ||
                emissiveProperty == null ||
                normalProperty == null)
            {
                return false;
            }

            texture = textureProperty.objectReferenceValue as Texture2D;
            emissive = emissiveProperty.objectReferenceValue as Texture2D;
            normal = normalProperty.objectReferenceValue as Texture2D;
            return true;
        }

        private static string GetTextureSlotDisplayName(
            DimensionPortalArtworkLayer layer,
            int animationIndex)
        {
            if (layer == DimensionPortalArtworkLayer.CenterInstant)
            {
                return animationIndex == 0
                    ? "Loop"
                    : animationIndex == 1
                        ? "Opening"
                        : "Closing";
            }

            if (layer == DimensionPortalArtworkLayer.Center)
            {
                return animationIndex == 0 ? "Loop" : "Opening";
            }

            if (layer == DimensionPortalArtworkLayer.ChargeSweep)
            {
                return "Charging sweep";
            }

            if (layer == DimensionPortalArtworkLayer.Milestones)
            {
                return "Milestone sequence";
            }

            return "Frame";
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

        private static bool PrepareStaticFrameArtwork(
            SpriteAsset source,
            SpriteAsset managed,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceData = serializedSource.FindProperty("m_staticSpriteData");
            if (!TryReadSpriteDataTextures(
                    sourceData,
                    out Texture2D sourceTexture,
                    out Texture2D sourceEmissive,
                    out Texture2D sourceNormal) ||
                sourceTexture == null)
            {
                message = "The framework Frame SpriteAsset has no static portal texture.";
                return false;
            }

            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedData = serializedManaged.FindProperty("m_staticSpriteData");
            SerializedProperty managedTextureProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("texture");
            SerializedProperty managedEmissiveProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("emissiveTexture");
            SerializedProperty managedNormalProperty = managedData == null
                ? null
                : managedData.FindPropertyRelative("normalTexture");
            if (managedTextureProperty == null ||
                managedEmissiveProperty == null ||
                managedNormalProperty == null)
            {
                message = "The editable Frame SpriteAsset has no static texture fields.";
                return false;
            }

            managedTextureProperty.objectReferenceValue = sourceTexture;
            managedEmissiveProperty.objectReferenceValue = sourceEmissive;
            managedNormalProperty.objectReferenceValue = sourceNormal;
            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PrepareTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            DimensionPortalArtworkLayer layer,
            TextureReplacement replacement,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            if (replacement == null || replacement.Textures == null)
            {
                message = "No portal texture replacement was supplied.";
                return false;
            }

            if (!TryBuildTextureSlots(
                    source,
                    source,
                    layer,
                    out TextureSlot[] contract,
                    out message) ||
                replacement.Textures.Length != contract.Length ||
                replacement.EmissiveTextures == null ||
                replacement.EmissiveTextures.Length != contract.Length ||
                replacement.NormalTextures == null ||
                replacement.NormalTextures.Length != contract.Length)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The supplied portal sheets do not match the native " +
                              GetDescriptor(layer).DisplayName + " slot count.";
                }

                return false;
            }

            // Each committed edit receives immutable PNG assets. Unity Undo can then
            // restore the SpriteAsset's previous texture references without relying on
            // bytes that were overwritten in place; Redo remains valid for the same reason.
            string revision = Guid.NewGuid().ToString("N").Substring(0, 12);
            return layer == DimensionPortalArtworkLayer.Frame
                ? PrepareStaticTextureReplacement(
                    source,
                    managed,
                    managedPath,
                    modRoot,
                    replacement,
                    revision,
                    transaction,
                    out message)
                : PrepareAnimatedTextureReplacement(
                    source,
                    managed,
                    managedPath,
                    modRoot,
                    layer,
                    replacement,
                    revision,
                    transaction,
                    out message);
        }

        private static bool HasAuthoredTextureSelection(TextureReplacement replacement)
        {
            if (replacement == null)
            {
                return false;
            }

            for (int i = 0; replacement.Textures != null && i < replacement.Textures.Length; i++)
            {
                if (replacement.Textures[i] != null)
                {
                    return true;
                }
            }

            for (int i = 0;
                 replacement.EmissiveTextures != null && i < replacement.EmissiveTextures.Length;
                 i++)
            {
                if (replacement.EmissiveTextures[i] != null)
                {
                    return true;
                }
            }

            for (int i = 0;
                 replacement.NormalTextures != null && i < replacement.NormalTextures.Length;
                 i++)
            {
                if (replacement.NormalTextures[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PrepareStaticTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            TextureReplacement replacement,
            string revision,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceData = serializedSource.FindProperty("m_staticSpriteData");
            if (!TryReadSpriteDataTextures(
                    sourceData,
                    out Texture2D sourceTexture,
                    out Texture2D sourceEmissive,
                    out Texture2D sourceNormal) ||
                sourceTexture == null)
            {
                message = "The framework Frame SpriteAsset has no static texture contract.";
                return false;
            }

            Texture2D requestedTexture = replacement.Textures[0] ?? sourceTexture;
            Texture2D requestedEmissive = replacement.EmissiveTextures[0];
            if (requestedEmissive == null)
            {
                requestedEmissive = sourceEmissive == sourceTexture &&
                                    replacement.Textures[0] != null
                    ? requestedTexture
                    : sourceEmissive;
            }
            Texture2D requestedNormal = replacement.NormalTextures[0] ?? sourceNormal;

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            if (!CopyManagedPortalTexture(
                    requestedTexture,
                    folder + "/" + stem + "_Static_Custom_" + revision + ".png",
                    "Portal frame",
                    modRoot,
                    sourceTexture.width,
                    sourceTexture.height,
                    transaction,
                    out Texture2D targetTexture,
                    out message))
            {
                return false;
            }

            Texture2D targetEmissive = null;
            if (requestedEmissive == requestedTexture)
            {
                targetEmissive = targetTexture;
            }
            else if (requestedEmissive != null &&
                     !CopyManagedPortalTexture(
                         requestedEmissive,
                         folder + "/" + stem + "_Static_Custom_" + revision + "_Emissive.png",
                         "Portal frame emissive",
                         modRoot,
                         sourceTexture.width,
                         sourceTexture.height,
                         transaction,
                         out targetEmissive,
                         out message))
            {
                return false;
            }

            Texture2D targetNormal = null;
            if (requestedNormal != null &&
                !CopyManagedPortalTexture(
                    requestedNormal,
                    folder + "/" + stem + "_Static_Custom_" + revision + "_Normal.png",
                    "Portal frame normal",
                    modRoot,
                    sourceTexture.width,
                    sourceTexture.height,
                    transaction,
                    true,
                    out targetNormal,
                    out message))
            {
                return false;
            }

            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedData = serializedManaged.FindProperty("m_staticSpriteData");
            SerializedProperty managedTexture = managedData?.FindPropertyRelative("texture");
            SerializedProperty managedEmissive =
                managedData?.FindPropertyRelative("emissiveTexture");
            SerializedProperty managedNormal =
                managedData?.FindPropertyRelative("normalTexture");
            if (managedTexture == null ||
                managedEmissive == null ||
                managedNormal == null)
            {
                message = "The editable Frame SpriteAsset has no static texture fields.";
                return false;
            }

            managedTexture.objectReferenceValue = targetTexture;
            managedEmissive.objectReferenceValue = targetEmissive;
            managedNormal.objectReferenceValue = targetNormal;
            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool PrepareAnimatedTextureReplacement(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            string modRoot,
            DimensionPortalArtworkLayer layer,
            TextureReplacement replacement,
            string revision,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedAnimations = serializedManaged.FindProperty("m_animations");
            if (sourceAnimations == null ||
                managedAnimations == null ||
                sourceAnimations.arraySize == 0 ||
                sourceAnimations.arraySize != managedAnimations.arraySize ||
                sourceAnimations.arraySize != replacement.Textures.Length)
            {
                message = "The editable " + GetDescriptor(layer).DisplayName +
                          " SpriteAsset does not preserve its native animation contract.";
                return false;
            }

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation =
                    sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty sourceData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                if (!TryReadSpriteDataTextures(
                        sourceData,
                        out Texture2D sourceTexture,
                        out Texture2D sourceEmissive,
                        out Texture2D sourceNormal) ||
                    sourceTexture == null)
                {
                    message = "Framework animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " is incomplete.";
                    return false;
                }

                Texture2D requestedTexture = replacement.Textures[i] ?? sourceTexture;
                Texture2D requestedEmissive = replacement.EmissiveTextures[i];
                if (sourceEmissive == sourceTexture &&
                    replacement.Textures[i] != null &&
                    (requestedEmissive == null || requestedEmissive == sourceEmissive))
                {
                    // The framework Center sheets use one PNG for both color and emission.
                    // When an author replaces only Color, keep that relationship intact so
                    // switching to the 48 x 48 full-canvas mode is one deliberate selection
                    // rather than an immediate native/full-size mismatch.
                    requestedEmissive = requestedTexture;
                }
                else if (requestedEmissive == null)
                {
                    requestedEmissive = sourceEmissive == sourceTexture &&
                                        replacement.Textures[i] != null
                        ? requestedTexture
                        : sourceEmissive;
                }
                Texture2D requestedNormal = replacement.NormalTextures[i] ?? sourceNormal;

                SerializedProperty sourceFrameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                int frameCount = sourceFrameCountProperty == null
                    ? 0
                    : sourceFrameCountProperty.intValue;
                SerializedProperty managedAnimation =
                    managedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty managedFrameCountProperty =
                    managedAnimation.FindPropertyRelative("srcFrameCount");
                int managedFrameCount = managedFrameCountProperty == null
                    ? 0
                    : managedFrameCountProperty.intValue;
                string slotLabel = GetTextureSlotDisplayName(layer, i);
                if (frameCount <= 0 || managedFrameCount != frameCount)
                {
                    message = slotLabel + " must retain the framework's " + frameCount +
                              " source frames. The editable SpriteAsset currently has " +
                              managedFrameCount + ".";
                    return false;
                }

                if (
                    !TryValidateSelectedTextureContract(
                        layer,
                        frameCount,
                        sourceTexture,
                        requestedTexture,
                        requestedEmissive,
                        requestedNormal,
                        slotLabel,
                        out message))
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message = slotLabel + " has no valid source-frame contract.";
                    }

                    return false;
                }

                int targetWidth = requestedTexture.width;
                int targetHeight = requestedTexture.height;

                string slotStem = stem + "_Anim" + i + "_Custom_" + revision;
                if (!CopyManagedPortalTexture(
                        requestedTexture,
                        folder + "/" + slotStem + ".png",
                        slotLabel,
                        modRoot,
                        targetWidth,
                        targetHeight,
                        transaction,
                        out Texture2D targetTexture,
                        out message))
                {
                    return false;
                }

                Texture2D targetEmissive = null;
                if (requestedEmissive == requestedTexture)
                {
                    targetEmissive = targetTexture;
                }
                else if (requestedEmissive != null &&
                          !CopyManagedPortalTexture(
                              requestedEmissive,
                             folder + "/" + slotStem + "_Emissive.png",
                             slotLabel + " emissive",
                             modRoot,
                             targetWidth,
                             targetHeight,
                             transaction,
                             out targetEmissive,
                             out message))
                {
                    return false;
                }

                Texture2D targetNormal = null;
                if (requestedNormal != null &&
                    !CopyManagedPortalTexture(
                        requestedNormal,
                        folder + "/" + slotStem + "_Normal.png",
                        slotLabel + " normal",
                        modRoot,
                        targetWidth,
                        targetHeight,
                        transaction,
                        true,
                        out targetNormal,
                        out message))
                {
                    return false;
                }

                SerializedProperty managedData =
                    managedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty managedTexture =
                    managedData?.FindPropertyRelative("texture");
                SerializedProperty managedEmissive =
                    managedData?.FindPropertyRelative("emissiveTexture");
                SerializedProperty managedNormal =
                    managedData?.FindPropertyRelative("normalTexture");
                if (managedTexture == null ||
                    managedEmissive == null ||
                    managedNormal == null)
                {
                    message = "Editable animation slot " + (i + 1) + " in " +
                              GetDescriptor(layer).DisplayName + " is incomplete.";
                    return false;
                }

                managedTexture.objectReferenceValue = targetTexture;
                managedEmissive.objectReferenceValue = targetEmissive;
                managedNormal.objectReferenceValue = targetNormal;
            }

            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool CopyManagedPortalTexture(
            Texture2D texture,
            string targetPath,
            string label,
            string modRoot,
            int requiredWidth,
            int requiredHeight,
            ArtworkFileTransaction transaction,
            out Texture2D managedTexture,
            out string message)
        {
            return CopyManagedPortalTexture(
                texture,
                targetPath,
                label,
                modRoot,
                requiredWidth,
                requiredHeight,
                transaction,
                false,
                out managedTexture,
                out message);
        }

        private static bool CopyManagedPortalTexture(
            Texture2D texture,
            string targetPath,
            string label,
            string modRoot,
            int requiredWidth,
            int requiredHeight,
            ArtworkFileTransaction transaction,
            bool normalMap,
            out Texture2D managedTexture,
            out string message)
        {
            managedTexture = null;
            message = string.Empty;
            if (texture == null)
            {
                message = label + " texture could not be resolved.";
                return false;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(path) ||
                (!AssetPathIsWithin(path, modRoot) &&
                 !AssetPathIsWithin(path, "Assets/ExpandNullforge")))
            {
                message = label + " must be saved inside this dimension mod or the Dimensions API framework.";
                return false;
            }

            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                message = label +
                          " must be a saved PNG so generated portal sprites preserve exact pixels and alpha.";
                return false;
            }

            if (texture.width != requiredWidth || texture.height != requiredHeight)
            {
                message = label + " must be " + requiredWidth + " x " + requiredHeight +
                          " pixels to match its native portal sheet. Selected: " +
                          texture.width + " x " + texture.height + ".";
                return false;
            }

            string absolutePath = AssetPathToAbsolutePath(path);
            if (transaction == null ||
                string.IsNullOrEmpty(absolutePath) ||
                !File.Exists(absolutePath))
            {
                message = label + " PNG bytes could not be read from " + path + ".";
                return false;
            }

            transaction.ReplaceAssetBytes(targetPath, File.ReadAllBytes(absolutePath));
            ConfigureSpriteTextureImporter(
                targetPath,
                requiredWidth,
                requiredHeight,
                normalMap);
            managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (managedTexture == null)
            {
                message = "Could not import the managed " + label + " PNG at " +
                          targetPath + ".";
                return false;
            }

            return true;
        }

        private static bool BakeAnimationTextures(
            SpriteAsset source,
            SpriteAsset managed,
            string managedPath,
            LayerDescriptor descriptor,
            Color[] palette,
            ArtworkFileTransaction transaction,
            out string message)
        {
            message = string.Empty;
            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceAnimations = serializedSource.FindProperty("m_animations");
            SerializedObject serializedManaged = new SerializedObject(managed);
            serializedManaged.Update();
            SerializedProperty managedAnimations = serializedManaged.FindProperty("m_animations");
            if (sourceAnimations == null ||
                managedAnimations == null ||
                sourceAnimations.arraySize == 0 ||
                managedAnimations.arraySize != sourceAnimations.arraySize)
            {
                message = "The " + descriptor.DisplayName +
                          " SpriteAsset animation contract could not be preserved.";
                return false;
            }

            string folder = NormalizeAssetPath(Path.GetDirectoryName(managedPath));
            string stem = Path.GetFileNameWithoutExtension(managedPath);
            for (int i = 0; i < sourceAnimations.arraySize; i++)
            {
                SerializedProperty sourceAnimation = sourceAnimations.GetArrayElementAtIndex(i);
                SerializedProperty sourceSpriteData =
                    sourceAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty sourceTextureProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("texture");
                SerializedProperty sourceEmissiveProperty = sourceSpriteData == null
                    ? null
                    : sourceSpriteData.FindPropertyRelative("emissiveTexture");
                SerializedProperty frameCountProperty =
                    sourceAnimation.FindPropertyRelative("srcFrameCount");
                Texture2D sourceTexture = sourceTextureProperty == null
                    ? null
                    : sourceTextureProperty.objectReferenceValue as Texture2D;
                Texture2D sourceEmissive = sourceEmissiveProperty == null
                    ? null
                    : sourceEmissiveProperty.objectReferenceValue as Texture2D;
                int frameCount = frameCountProperty == null ? 0 : frameCountProperty.intValue;
                if (sourceTexture == null || frameCount <= 0)
                {
                    message = "Animation " + i + " in the framework " +
                              descriptor.DisplayName + " artwork is incomplete.";
                    return false;
                }

                string texturePath = folder + "/" + stem + "_Anim" + i + ".png";
                Texture2D bakedTexture = BakeTexture(
                    sourceTexture,
                    texturePath,
                    frameCount,
                    descriptor.SourcePalette,
                    palette,
                    transaction);
                Texture2D bakedEmissive = null;
                if (sourceEmissive == sourceTexture)
                {
                    bakedEmissive = bakedTexture;
                }
                else if (sourceEmissive != null)
                {
                    bakedEmissive = BakeTexture(
                        sourceEmissive,
                        folder + "/" + stem + "_Anim" + i + "_Emissive.png",
                        frameCount,
                        descriptor.SourcePalette,
                        palette,
                        transaction);
                }

                SerializedProperty managedAnimation = managedAnimations.GetArrayElementAtIndex(i);
                SerializedProperty managedSpriteData =
                    managedAnimation.FindPropertyRelative("m_spriteData");
                SerializedProperty managedTextureProperty = managedSpriteData == null
                    ? null
                    : managedSpriteData.FindPropertyRelative("texture");
                SerializedProperty managedEmissiveProperty = managedSpriteData == null
                    ? null
                    : managedSpriteData.FindPropertyRelative("emissiveTexture");
                if (managedTextureProperty == null || managedEmissiveProperty == null)
                {
                    message = "Animation " + i + " in the editable " +
                              descriptor.DisplayName + " artwork is incomplete.";
                    return false;
                }

                managedTextureProperty.objectReferenceValue = bakedTexture;
                managedEmissiveProperty.objectReferenceValue = bakedEmissive;
            }

            serializedManaged.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static Texture2D BakeTexture(
            Texture2D source,
            string targetPath,
            int frameCount,
            Color32[] sourcePalette,
            Color[] targetPalette,
            ArtworkFileTransaction transaction)
        {
            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(source));
            string sourceAbsolute = AssetPathToAbsolutePath(sourcePath);
            if (string.IsNullOrEmpty(sourceAbsolute) ||
                !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourceAbsolute))
            {
                throw new InvalidOperationException(
                    "Portal palette source textures must be saved PNG files. Current source: " +
                    sourcePath + ".");
            }

            Texture2D decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D output = null;
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(sourceAbsolute)) ||
                    frameCount <= 0 ||
                    decoded.width % frameCount != 0)
                {
                    throw new InvalidOperationException(
                        "Could not decode an evenly divided portal animation sheet at " +
                        sourcePath + ".");
                }

                Color32[] sourcePixels = decoded.GetPixels32();
                Color32[] targetPixels = new Color32[sourcePixels.Length];
                for (int i = 0; i < sourcePixels.Length; i++)
                {
                    Color32 pixel = sourcePixels[i];
                    if (pixel.a == 0)
                    {
                        targetPixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (sourcePalette == null ||
                        targetPalette == null ||
                        sourcePalette.Length == 0 ||
                        targetPalette.Length != sourcePalette.Length)
                    {
                        // Frame artwork has no semantic recolor contract. An explicit
                        // editable copy preserves its source pixels exactly.
                        targetPixels[i] = pixel;
                    }
                    else
                    {
                        int paletteIndex = FindClosestPaletteIndex(pixel, sourcePalette);
                        Color target = targetPalette[paletteIndex];
                        Color32 recolored = target;
                        recolored.a = (byte)Mathf.Clamp(
                            Mathf.RoundToInt(pixel.a * Mathf.Clamp01(target.a)),
                            0,
                            255);
                        targetPixels[i] = recolored;
                    }
                }

                output = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, false);
                output.SetPixels32(targetPixels);
                output.Apply(false, false);
                if (transaction == null)
                {
                    throw new InvalidOperationException(
                        "A portal artwork file transaction was not available.");
                }

                transaction.ReplaceAssetBytes(targetPath, output.EncodeToPNG());
                ConfigureSpriteTextureImporter(targetPath, decoded.width, decoded.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(decoded);
                if (output != null)
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }

            Texture2D baked = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (baked == null)
            {
                throw new InvalidOperationException(
                    "Could not import editable portal artwork texture at " + targetPath + ".");
            }

            return baked;
        }

        internal static void ConfigureSpriteTextureImporter(
            string textureAssetPath,
            int sourceWidth,
            int sourceHeight,
            bool normalMap = false)
        {
            TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(
                    textureAssetPath,
                    ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            }

            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Could not configure editable portal texture " + textureAssetPath + ".");
            }

            importer.textureType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Sprite;
            if (!normalMap)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
            }

            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = !normalMap;
            importer.sRGBTexture = !normalMap;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            int largest = Mathf.Max(sourceWidth, sourceHeight);
            if (largest > 0)
            {
                importer.maxTextureSize = Mathf.Max(
                    importer.maxTextureSize,
                    Mathf.NextPowerOfTwo(largest));
            }

            importer.SaveAndReimport();
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

        private static void EnsureFolder(string folder)
        {
            string normalized = NormalizeAssetPath(folder);
            string[] parts = normalized.Split('/');
            string current = parts.Length > 0 ? parts[0] : string.Empty;
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
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

        private static string SanitizeFileName(string value)
        {
            string source = string.IsNullOrEmpty(value) ? "PortalVisualProfile" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                source = source.Replace(invalid[i], '_');
            }

            return source.Replace('/', '_').Replace('\\', '_').Trim();
        }

        private static long ComputeStableAddressPart(string value, ulong salt)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis ^ salt;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= prime;
                }

                return (long)(hash == 0UL ? salt | 1UL : hash);
            }
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

    /// <summary>
    /// Owns the optional full-canvas Swirls SpriteAsset without folding it into the four
    /// framework-derived palette layers. Vanilla mode still renders the extracted
    /// GatherEnergy ParticleSystem; this helper only materializes the author-selected
    /// animated SpriteObject alternative into a self-contained portal package.
    /// </summary>
    internal static class DimensionPortalSwirlArtworkEditorUtility
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
        /// Resolves the profile's current custom Swirls reference and reads animation zero.
        /// An empty reference uses the framework starter; a non-zero unresolved address is
        /// surfaced as an error rather than silently replacing author intent.
        /// </summary>
        public static bool TryGetAnimationZeroTextureSlot(
            DimensionPortalVisualProfileAsset profile,
            out AnimationZeroTextureSlot slot,
            out string message)
        {
            slot = null;
            message = string.Empty;
            string packageFolder = string.Empty;
            if (profile != null)
            {
                DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out _,
                    out packageFolder);
            }

            SpriteAsset selected = null;
            if (profile != null)
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.Update();
                SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
                if (reference == null)
                {
                    message = "The portal profile no longer contains the custom Swirls reference.";
                    return false;
                }

                if (HasAddress(reference) &&
                    !TryResolveReference(profile, reference, packageFolder, out selected))
                {
                    message =
                        "The selected Swirls SpriteAsset address could not be resolved in Scriptable Data.";
                    return false;
                }
            }

            if (selected == null)
            {
                selected = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath);
            }

            if (selected == null)
            {
                message = "The framework Swirls starter SpriteAsset could not be loaded.";
                return false;
            }

            return TryBuildAnimationZeroTextureSlot(
                selected,
                packageFolder,
                out slot,
                out message);
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
                string fileStem = SanitizeFileName(managed.name) + "_Editable_Anim0";
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


        private static bool TryResolveManagedAssetForTextureEdit(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            string packageFolder,
            string expectedFolder,
            out SpriteAsset managed,
            out string message)
        {
            managed = null;
            message = string.Empty;
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (reference == null)
            {
                message = "The portal profile no longer contains the custom Swirls reference.";
                return false;
            }

            if (!TryResolveReference(
                    profile,
                    reference,
                    packageFolder,
                    out SpriteAsset selected))
            {
                message =
                    "The selected Swirls SpriteAsset could not be resolved in Scriptable Data.";
                return false;
            }

            string selectedPath = selected == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            if (selected == null ||
                IsFrameworkAsset(selected) ||
                !AssetPathIsWithin(selectedPath, expectedFolder))
            {
                if (!CreateEditableCopy(template, profile, out message))
                {
                    return false;
                }

                serialized = new SerializedObject(profile);
                serialized.Update();
                reference = serialized.FindProperty(ReferencePropertyName);
                if (reference == null ||
                    !TryResolveReference(profile, reference, packageFolder, out selected) ||
                    selected == null)
                {
                    message =
                        "The newly-created Swirls SpriteAsset could not be resolved from its portal package.";
                    return false;
                }

                selectedPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            }

            if (IsFrameworkAsset(selected) ||
                string.IsNullOrEmpty(selectedPath) ||
                !AssetPathIsWithin(selectedPath, expectedFolder))
            {
                message =
                    "Swirls textures may only update the selected SpriteAsset inside this portal's Artwork/Swirls folder.";
                return false;
            }

            managed = selected;
            return true;
        }

        private static bool TryBuildAnimationZeroTextureSlot(
            SpriteAsset asset,
            string packageFolder,
            out AnimationZeroTextureSlot slot,
            out string message)
        {
            slot = null;
            message = string.Empty;
            if (asset == null)
            {
                message = "The Swirls SpriteAsset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            if (!TryGetAnimationZeroSpriteData(
                    serialized,
                    out SerializedProperty animation,
                    out SerializedProperty spriteData,
                    out message))
            {
                return false;
            }

            SerializedProperty frameCountProperty =
                animation.FindPropertyRelative("srcFrameCount");
            int frameCount = frameCountProperty == null
                ? 0
                : frameCountProperty.intValue;
            if (frameCount <= 0)
            {
                message = "Swirls animation zero has no source frames.";
                return false;
            }

            SerializedProperty loopProperty = animation.FindPropertyRelative("loop");
            if (loopProperty == null || !loopProperty.boolValue)
            {
                message = "Swirls animation zero must loop.";
                return false;
            }

            Texture2D color = GetTexture(spriteData, "texture");
            Texture2D emissive = GetTexture(spriteData, "emissiveTexture");
            Texture2D normal = GetTexture(spriteData, "normalTexture");
            if (color == null || color.width % frameCount != 0)
            {
                message =
                    "Swirls animation zero must have an evenly-divided color sheet.";
                return false;
            }

            int frameWidth = color.width / frameCount;
            if (frameWidth <= 0 ||
                frameWidth > NativeFrameSize ||
                color.height > NativeFrameSize)
            {
                message = "Swirls animation zero frames must fit within the " +
                          NativeFrameSize + " x " + NativeFrameSize +
                          " portal canvas. Found " + frameWidth + " x " + color.height + ".";
                return false;
            }

            if (!ChannelsMatch(color, emissive) || !ChannelsMatch(color, normal))
            {
                message =
                    "Swirls animation zero color, emissive, and normal sheets must have matching dimensions.";
                return false;
            }

            string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
            string expectedFolder = string.IsNullOrEmpty(packageFolder)
                ? string.Empty
                : NormalizeAssetPath(packageFolder + "/" + PackageRelativeFolder);
            slot = new AnimationZeroTextureSlot
            {
                SpriteAsset = asset,
                ColorTexture = color,
                EmissiveTexture = emissive,
                NormalTexture = normal,
                FrameCount = frameCount,
                SheetWidth = color.width,
                SheetHeight = color.height,
                FrameWidth = frameWidth,
                FrameHeight = color.height,
                IsFramework = IsFrameworkAsset(asset),
                IsManaged = !string.IsNullOrEmpty(expectedFolder) &&
                            AssetPathIsWithin(assetPath, expectedFolder)
            };
            return true;
        }

        private static bool TryGetAnimationZeroSpriteData(
            SerializedObject serialized,
            out SerializedProperty animation,
            out SerializedProperty spriteData,
            out string message)
        {
            animation = null;
            spriteData = null;
            message = string.Empty;
            SerializedProperty animations = serialized == null
                ? null
                : serialized.FindProperty("m_animations");
            if (animations == null || !animations.isArray || animations.arraySize == 0)
            {
                message = "The Swirls SpriteAsset must contain animation zero.";
                return false;
            }

            animation = animations.GetArrayElementAtIndex(0);
            spriteData = animation == null
                ? null
                : animation.FindPropertyRelative("m_spriteData");
            if (animation == null || spriteData == null)
            {
                message = "Swirls animation zero has no SpriteData.";
                return false;
            }

            return true;
        }

        private static bool TryValidateSelectedTexture(
            Texture2D texture,
            string label,
            int requiredWidth,
            int requiredHeight,
            bool optional,
            out string message)
        {
            message = string.Empty;
            if (texture == null)
            {
                if (optional)
                {
                    return true;
                }

                message = label + " is required.";
                return false;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                message = label +
                          " must be a saved PNG inside the Unity Assets folder.";
                return false;
            }

            string absolutePath =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(path);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                message = label + " bytes could not be read from " + path + ".";
                return false;
            }

            if (texture.width != requiredWidth || texture.height != requiredHeight)
            {
                message = label + " must be " + requiredWidth + " x " +
                          requiredHeight + " pixels. Selected: " + texture.width + " x " +
                          texture.height + ".";
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.filterMode != FilterMode.Point)
            {
                message = label +
                          " must use Point filtering so its portal pixels remain exact.";
                return false;
            }

            return true;
        }

        private static bool TryMaterializeSelectedTexture(
            Texture2D selected,
            Texture2D current,
            string destinationFolder,
            string fileStem,
            string suffix,
            int requiredWidth,
            int requiredHeight,
            bool normalMap,
            DimensionPortalArtworkEditorUtility.ArtworkFileTransaction transaction,
            out Texture2D managedTexture,
            out string message)
        {
            managedTexture = null;
            message = string.Empty;
            if (selected == null)
            {
                return true;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(selected));
            if (AssetPathIsWithin(sourcePath, destinationFolder))
            {
                // The package owns this texture, so normalize its importer in place.
                // This is particularly important for Normal, where merely assigning a
                // point-filtered PNG does not establish Unity's normal-map semantics.
                DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                    sourcePath,
                    requiredWidth,
                    requiredHeight,
                    normalMap);
                managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (managedTexture == null)
                {
                    message = "The package-owned Swirls texture could not be reloaded from " +
                              sourcePath + ".";
                    return false;
                }

                return true;
            }

            string targetBaseName = fileStem + suffix;
            string currentPath = current == null
                ? string.Empty
                : NormalizeAssetPath(AssetDatabase.GetAssetPath(current));
            string targetPath = IsEditableAnimationZeroTexture(
                    currentPath,
                    destinationFolder,
                    targetBaseName)
                ? currentPath
                : NormalizeAssetPath(
                    AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + targetBaseName + ".png"));
            string absoluteSource =
                DimensionPortalArtworkEditorUtility.AssetPathToAbsolutePath(sourcePath);
            if (transaction == null ||
                string.IsNullOrEmpty(absoluteSource) ||
                !File.Exists(absoluteSource))
            {
                message = "The selected Swirls texture bytes could not be read from " +
                          sourcePath + ".";
                return false;
            }

            transaction.ReplaceAssetBytes(targetPath, File.ReadAllBytes(absoluteSource));
            DimensionPortalArtworkEditorUtility.ConfigureSpriteTextureImporter(
                targetPath,
                requiredWidth,
                requiredHeight,
                normalMap);
            managedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
            if (managedTexture == null ||
                managedTexture.width != requiredWidth ||
                managedTexture.height != requiredHeight)
            {
                message = "The managed Swirls texture could not be imported at " +
                          targetPath + ".";
                return false;
            }

            TextureImporter importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
            if (importer == null ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled)
            {
                message = "The managed Swirls texture did not retain its point-sampled import contract.";
                return false;
            }

            return true;
        }

        private static bool IsEditableAnimationZeroTexture(
            string assetPath,
            string destinationFolder,
            string expectedBaseName)
        {
            if (!AssetPathIsWithin(assetPath, destinationFolder) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return string.Equals(
                       fileName,
                       expectedBaseName,
                       StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith(
                       expectedBaseName + " ",
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool CreateEditableCopy(
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
                    "Save this portal as a managed profile before creating editable Swirls artwork.";
                return false;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string modRoot = NormalizeAssetPath(
                DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(
                    templatePath));
            if (string.IsNullOrEmpty(modRoot) ||
                !AssetPathIsWithin(packageFolder, modRoot))
            {
                message =
                    "The portal profile package does not belong to the selected Dimension Asset's mod.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (!TryResolveReference(
                    profile,
                    reference,
                    packageFolder,
                    out SpriteAsset sourceAsset))
            {
                message =
                    "The selected Swirls SpriteAsset could not be resolved in Scriptable Data.";
                return false;
            }

            if (sourceAsset == null)
            {
                sourceAsset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                    DimensionPortalArtworkEditorUtility.FrameworkSwirlAssetPath);
            }

            if (!TryValidateAnimationContract(
                    sourceAsset,
                    true,
                    out _,
                    out message))
            {
                message = "The selected Swirls artwork cannot be copied. " + message;
                return false;
            }

            string destinationFolder = NormalizeAssetPath(
                packageFolder + "/" + PackageRelativeFolder);
            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    destinationFolder,
                    out message))
            {
                return false;
            }

            if (!TryCloneGraph(
                    sourceAsset,
                    profile,
                    destinationFolder,
                    modRoot,
                    out message))
            {
                return false;
            }

            message =
                "Created and selected an editable Swirls SpriteAsset inside this portal profile.";
            return true;
        }

        public static bool TryLocalizeIntoPackage(
            DimensionPortalVisualProfileAsset source,
            DimensionPortalVisualProfileAsset target,
            string packageFolder,
            string modRoot,
            out string message)
        {
            message = string.Empty;
            if (source == null || target == null)
            {
                message = "The source and destination portal profiles are required.";
                return false;
            }

            SerializedObject serializedSource = new SerializedObject(source);
            serializedSource.Update();
            SerializedProperty sourceReference = serializedSource.FindProperty(
                ReferencePropertyName);
            SerializedProperty sourceOverride = serializedSource.FindProperty(
                OverridePropertyName);
            if (sourceReference == null)
            {
                // Backward compatibility while an older profile schema is imported.
                return true;
            }

            bool overrideVanilla = sourceOverride != null && sourceOverride.boolValue;
            string sourcePackageFolder = string.Empty;
            DimensionPortalPackageEditorUtility.TryGetPackage(
                source,
                out _,
                out sourcePackageFolder);
            if (!TryResolveReference(
                    source,
                    sourceReference,
                    sourcePackageFolder,
                    out SpriteAsset sourceAsset))
            {
                if (!HasAddress(sourceReference) && !overrideVanilla)
                {
                    return true;
                }

                message = overrideVanilla
                    ? "Custom Swirls are enabled, but their SpriteAsset could not be resolved in Scriptable Data."
                    : "The dormant custom Swirls SpriteAsset could not be resolved, so it could not be preserved in this portal package.";
                return false;
            }

            if (sourceAsset == null)
            {
                if (!overrideVanilla)
                {
                    return true;
                }

                message = "Custom Swirls are enabled, but no Swirls SpriteAsset is selected.";
                return false;
            }

            // The framework starter is intentionally shared while vanilla mode is active.
            // Enabling the custom mode closes over it like any other custom selection, so
            // subsequent edits never mutate framework-owned artwork.
            if (IsFrameworkAsset(sourceAsset) && !overrideVanilla)
            {
                return AssignReference(target, sourceAsset, out message);
            }

            string expectedFolder = NormalizeAssetPath(
                packageFolder + "/" + PackageRelativeFolder);
            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
            if (ReferenceEquals(source, target) &&
                AssetPathIsWithin(sourcePath, expectedFolder))
            {
                // The package already owns this profile's pristine Swirls artwork. The tint
                // is applied at runtime from the profile colors, so an in-place Save & Update
                // needs no texture rewrite or import here.
                return true;
            }

            if (!TryValidateAnimationContract(
                    sourceAsset,
                    false,
                    out _,
                    out message))
            {
                message = "The selected Swirls SpriteAsset is not packageable. " + message;
                return false;
            }

            if (!DimensionPortalPackageEditorUtility.EnsureFolder(
                    expectedFolder,
                    out message))
            {
                return false;
            }

            return TryCloneGraph(
                sourceAsset,
                target,
                expectedFolder,
                modRoot,
                out message);
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

        public static bool TryValidateAnimationContract(
            SpriteAsset asset,
            bool requirePackageFrameSize,
            out List<TextureDependency> dependencies,
            out string message)
        {
            dependencies = new List<TextureDependency>();
            message = string.Empty;
            if (asset == null)
            {
                message = "The Swirls SpriteAsset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SerializedProperty animations = serialized.FindProperty("m_animations");
            if (animations == null || !animations.isArray || animations.arraySize == 0)
            {
                message = "The Swirls SpriteAsset must contain animation 0.";
                return false;
            }

            HashSet<Texture2D> seen = new HashSet<Texture2D>();
            for (int i = 0; i < animations.arraySize; i++)
            {
                SerializedProperty animation = animations.GetArrayElementAtIndex(i);
                SerializedProperty spriteData = animation.FindPropertyRelative("m_spriteData");
                SerializedProperty frameCountProperty =
                    animation.FindPropertyRelative("srcFrameCount");
                int frameCount = frameCountProperty == null
                    ? 0
                    : frameCountProperty.intValue;
                if (frameCount <= 0)
                {
                    message = "Swirls animation " + i + " has no source frames.";
                    return false;
                }

                Texture2D color = GetTexture(spriteData, "texture");
                Texture2D emissive = GetTexture(spriteData, "emissiveTexture");
                Texture2D normal = GetTexture(spriteData, "normalTexture");
                if (color == null)
                {
                    message = "Swirls animation " + i + " has no color sheet.";
                    return false;
                }

                if (color.width % frameCount != 0)
                {
                    message = "Swirls animation " + i +
                              " is not evenly divided into its " + frameCount + " frames.";
                    return false;
                }

                int frameWidth = color.width / frameCount;
                if (requirePackageFrameSize &&
                    (frameWidth <= 0 ||
                     frameWidth > NativeFrameSize ||
                     color.height > NativeFrameSize))
                {
                    message = "Swirls animation " + i + " frames must fit within the " +
                              NativeFrameSize + " x " + NativeFrameSize +
                              " portal canvas. Found " + frameWidth + " x " +
                              color.height + ".";
                    return false;
                }

                if (!ChannelsMatch(color, emissive) || !ChannelsMatch(color, normal))
                {
                    message = "Swirls animation " + i +
                              " color, emissive, and normal sheets must have matching dimensions.";
                    return false;
                }

                AddDependency(dependencies, seen, "Animation[" + i + "].Color", color);
                AddDependency(dependencies, seen, "Animation[" + i + "].Emissive", emissive);
                AddDependency(dependencies, seen, "Animation[" + i + "].Normal", normal);
            }

            // Preserve any authored static fallback data too, even though custom Swirls
            // render animation 0. This keeps duplication graph-complete.
            SerializedProperty staticData = serialized.FindProperty("m_staticSpriteData");
            AddDependency(
                dependencies,
                seen,
                "Static.Color",
                GetTexture(staticData, "texture"));
            AddDependency(
                dependencies,
                seen,
                "Static.Emissive",
                GetTexture(staticData, "emissiveTexture"));
            AddDependency(
                dependencies,
                seen,
                "Static.Normal",
                GetTexture(staticData, "normalTexture"));
            return true;
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

        private static bool TryCloneGraph(
            SpriteAsset sourceAsset,
            DimensionPortalVisualProfileAsset target,
            string destinationFolder,
            string modRoot,
            out string message)
        {
            message = string.Empty;
            List<string> createdPaths = new List<string>();
            DimensionPortalVisualProfileAsset targetSnapshot =
                DimensionPortalArtworkEditorUtility.InstantiateSnapshot(target);
            string manifestPath = NormalizeAssetPath(modRoot) + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            SpriteAssetManifest manifestSnapshot = manifest == null
                ? null
                : DimensionPortalArtworkEditorUtility.InstantiateSnapshot(manifest);
            try
            {
                string assetStem = SanitizeFileName(target.name) + "_Swirls";
                string destinationPath = AssetDatabase.GenerateUniqueAssetPath(
                    destinationFolder + "/" + assetStem + ".asset");
                SpriteAsset clone = UnityEngine.Object.Instantiate(sourceAsset);
                clone.name = Path.GetFileNameWithoutExtension(destinationPath);
                SetFreshAddress(clone, destinationPath);
                AssetDatabase.CreateAsset(clone, destinationPath);
                createdPaths.Add(destinationPath);

                SerializedObject serializedSource = new SerializedObject(sourceAsset);
                serializedSource.Update();
                SerializedObject serializedClone = new SerializedObject(clone);
                serializedClone.Update();
                Dictionary<string, Texture2D> localizedTextures =
                    new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

                if (!CopySpriteDataTextures(
                        serializedSource.FindProperty("m_staticSpriteData"),
                        serializedClone.FindProperty("m_staticSpriteData"),
                        destinationFolder,
                        assetStem + "_Static",
                        localizedTextures,
                        createdPaths,
                        null,
                        null,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                SerializedProperty sourceAnimations =
                    serializedSource.FindProperty("m_animations");
                SerializedProperty cloneAnimations =
                    serializedClone.FindProperty("m_animations");
                if (sourceAnimations == null || cloneAnimations == null ||
                    sourceAnimations.arraySize != cloneAnimations.arraySize)
                {
                    throw new InvalidOperationException(
                        "The cloned Swirls SpriteAsset did not preserve its animation slots.");
                }

                for (int i = 0; i < sourceAnimations.arraySize; i++)
                {
                    if (!CopySpriteDataTextures(
                            sourceAnimations.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("m_spriteData"),
                            cloneAnimations.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("m_spriteData"),
                            destinationFolder,
                            assetStem + "_Anim" + i,
                            localizedTextures,
                            createdPaths,
                            null,
                            null,
                            out message))
                    {
                        throw new InvalidOperationException(message);
                    }
                }

                serializedClone.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(clone);
                AssetDatabase.SaveAssetIfDirty(clone);
                AssetDatabase.ImportAsset(
                    destinationPath,
                    ImportAssetOptions.ForceSynchronousImport);
                clone = AssetDatabase.LoadAssetAtPath<SpriteAsset>(destinationPath);
                if (clone == null)
                {
                    throw new InvalidOperationException(
                        "The localized Swirls SpriteAsset could not be reloaded.");
                }

                string packageSuffix = "/" + PackageRelativeFolder;
                string clonePackageFolder = destinationFolder.EndsWith(
                        packageSuffix,
                        StringComparison.OrdinalIgnoreCase)
                    ? destinationFolder.Substring(
                        0,
                        destinationFolder.Length - packageSuffix.Length)
                    : NormalizeAssetPath(Path.GetDirectoryName(destinationFolder));
                if (!TryBuildAnimationZeroTextureSlot(
                        clone,
                        clonePackageFolder,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                // Carry the pristine untinted source sheet across the clone so the duplicated
                // profile can be recolored without re-selecting artwork. Otherwise the clone
                // would hold only the already-baked (tinted) output and the next color bake
                // would have no clean source to start from.
                string sourceAssetPath =
                    NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceAsset));
                string sourceSourceSheet =
                    NormalizeAssetPath(Path.GetDirectoryName(sourceAssetPath)) + "/" +
                    Path.GetFileNameWithoutExtension(sourceAssetPath) + SwirlSourceSuffix;
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourceSourceSheet)))
                {
                    string cloneSourceSheet = NormalizeAssetPath(destinationFolder) + "/" +
                        Path.GetFileNameWithoutExtension(destinationPath) + SwirlSourceSuffix;
                    if (AssetDatabase.CopyAsset(sourceSourceSheet, cloneSourceSheet))
                    {
                        createdPaths.Add(cloneSourceSheet);
                    }
                }

                EnsureManifestContains(modRoot, destinationPath);
                if (!AssignReference(target, clone, out message))
                {
                    throw new InvalidOperationException(message);
                }

                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.SaveAssets();
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not localize the custom Swirls artwork. " + exception.Message;
                try
                {
                    EditorUtility.CopySerialized(targetSnapshot, target);
                    EditorUtility.SetDirty(target);
                    AssetDatabase.SaveAssetIfDirty(target);
                }
                catch (Exception restoreException)
                {
                    message += " Could not restore the profile: " + restoreException.Message;
                }

                try
                {
                    RestoreManifest(manifestPath, manifestSnapshot);
                }
                catch (Exception restoreException)
                {
                    message += " Could not restore the SpriteAsset manifest: " +
                               restoreException.Message;
                }

                for (int i = createdPaths.Count - 1; i >= 0; i--)
                {
                    string path = createdPaths[i];
                    if (!string.IsNullOrEmpty(path) &&
                        AssetDatabase.LoadMainAssetAtPath(path) != null)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ScriptableDataEditorUtility.InvalidateDataBlockCache<SpriteAsset>();
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetSnapshot);
                if (manifestSnapshot != null)
                {
                    UnityEngine.Object.DestroyImmediate(manifestSnapshot);
                }
            }
        }

        private static bool CopySpriteDataTextures(
            SerializedProperty sourceData,
            SerializedProperty targetData,
            string destinationFolder,
            string fileStem,
            Dictionary<string, Texture2D> localizedTextures,
            List<string> createdPaths,
            Texture2D sourceColorOverride,
            Texture2D sourceEmissiveOverride,
            out string message)
        {
            message = string.Empty;
            if (sourceData == null || targetData == null)
            {
                return true;
            }

            string[] properties = { "texture", "emissiveTexture", "normalTexture" };
            string[] suffixes = { string.Empty, "_Emissive", "_Normal" };
            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty sourceProperty =
                    sourceData.FindPropertyRelative(properties[i]);
                SerializedProperty targetProperty =
                    targetData.FindPropertyRelative(properties[i]);
                if (targetProperty == null)
                {
                    message = "The Swirls SpriteAsset does not expose its " +
                              properties[i] + " texture slot.";
                    return false;
                }

                Texture2D sourceTexture;
                if (i == 0 && sourceColorOverride != null)
                {
                    sourceTexture = sourceColorOverride;
                }
                else if (i == 1 && sourceEmissiveOverride != null)
                {
                    sourceTexture = sourceEmissiveOverride;
                }
                else
                {
                    sourceTexture = sourceProperty == null
                        ? null
                        : sourceProperty.objectReferenceValue as Texture2D;
                }
                if (sourceTexture == null)
                {
                    targetProperty.objectReferenceValue = null;
                    continue;
                }

                string sourcePath = NormalizeAssetPath(
                    AssetDatabase.GetAssetPath(sourceTexture));
                if (string.IsNullOrEmpty(sourcePath) ||
                    !sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    message = "Swirls texture '" + sourceTexture.name +
                              "' must be a saved project asset before it can be packaged.";
                    return false;
                }

                if (!localizedTextures.TryGetValue(sourcePath, out Texture2D copiedTexture) ||
                    copiedTexture == null)
                {
                    string extension = Path.GetExtension(sourcePath);
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = ".png";
                    }

                    string copiedPath = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder + "/" + fileStem + suffixes[i] + extension);
                    if (!AssetDatabase.CopyAsset(sourcePath, copiedPath))
                    {
                        message = "Unity could not copy Swirls texture '" + sourcePath + "'.";
                        return false;
                    }

                    AssetDatabase.ImportAsset(
                        copiedPath,
                        ImportAssetOptions.ForceSynchronousImport);
                    copiedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(copiedPath);
                    if (copiedTexture == null)
                    {
                        message = "The copied Swirls texture could not be reloaded from '" +
                                  copiedPath + "'.";
                        return false;
                    }

                    localizedTextures[sourcePath] = copiedTexture;
                    createdPaths.Add(copiedPath);
                }

                targetProperty.objectReferenceValue = copiedTexture;
            }

            return true;
        }

        private static bool AssignReference(
            DimensionPortalVisualProfileAsset profile,
            SpriteAsset asset,
            out string message)
        {
            message = string.Empty;
            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(ReferencePropertyName);
            if (reference == null)
            {
                message = "The portal profile no longer contains the custom Swirls reference.";
                return false;
            }

            ScriptableDataEditorUtility.SetDataBlock<SpriteAsset>(reference, asset);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static Texture2D GetTexture(
            SerializedProperty spriteData,
            string propertyName)
        {
            SerializedProperty property = spriteData == null
                ? null
                : spriteData.FindPropertyRelative(propertyName);
            return property == null ? null : property.objectReferenceValue as Texture2D;
        }

        private static bool ChannelsMatch(Texture2D color, Texture2D optional)
        {
            return optional == null ||
                   (color != null && optional.width == color.width &&
                    optional.height == color.height);
        }

        private static void AddDependency(
            List<TextureDependency> dependencies,
            HashSet<Texture2D> seen,
            string role,
            Texture2D texture)
        {
            if (texture == null || !seen.Add(texture))
            {
                return;
            }

            dependencies.Add(new TextureDependency { Role = role, Texture = texture });
        }

        private static void SetFreshAddress(SpriteAsset asset, string assetPath)
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
                    "The Swirls SpriteAsset address could not be assigned.");
            }

            low.longValue = ComputeStableAddressPart(
                assetPath,
                0x737769726C617274UL);
            high.longValue = ComputeStableAddressPart(
                assetPath,
                0x706F7274616C7377UL);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The address is the lookup index's key, so re-addressing invalidates it.
            DimensionPortalArtworkEditorUtility.InvalidateSpriteAssetAddressIndex();
        }

        private static void EnsureManifestContains(string modRoot, string spriteAssetPath)
        {
            SpriteAssetBase spriteAsset =
                AssetDatabase.LoadAssetAtPath<SpriteAssetBase>(spriteAssetPath);
            if (spriteAsset == null)
            {
                throw new InvalidOperationException(
                    "The localized Swirls SpriteAsset could not be loaded for manifest registration.");
            }

            string manifestPath = NormalizeAssetPath(modRoot) + "/SpriteAssetManifest.asset";
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

            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                if (manifest.spriteAssets[i] == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                }
            }

            if (!manifest.spriteAssets.Contains(spriteAsset))
            {
                manifest.spriteAssets.Add(spriteAsset);
            }

            manifest.name = Path.GetFileNameWithoutExtension(manifestPath);
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }

        private static void RestoreManifest(
            string manifestPath,
            SpriteAssetManifest snapshot)
        {
            SpriteAssetManifest current =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (snapshot == null)
            {
                if (current != null)
                {
                    AssetDatabase.DeleteAsset(manifestPath);
                }
                return;
            }

            // The asset's own filename is the only name Unity's importer will accept, so it wins
            // over whatever name the in-memory snapshot happens to carry.
            string manifestName = Path.GetFileNameWithoutExtension(manifestPath);

            if (current == null)
            {
                SpriteAssetManifest restored =
                    DimensionPortalArtworkEditorUtility.InstantiateSnapshot(snapshot);
                restored.name = manifestName;
                AssetDatabase.CreateAsset(restored, manifestPath);
                return;
            }

            EditorUtility.CopySerialized(snapshot, current);
            current.name = manifestName;
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssetIfDirty(current);
        }

        private static long ComputeStableAddressPart(string value, ulong salt)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis ^ salt;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= prime;
                }

                return (long)(hash == 0UL ? salt | 1UL : hash);
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

        private static string SanitizeFileName(string value)
        {
            string source = string.IsNullOrEmpty(value) ? "Portal" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                source = source.Replace(invalid[i], '_');
            }

            return source.Replace('/', '_').Replace('\\', '_').Trim();
        }
    }
}
