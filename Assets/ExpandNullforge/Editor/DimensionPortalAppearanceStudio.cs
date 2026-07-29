using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionPortalAppearanceStudio : IDisposable
    {
        internal struct DrawResult
        {
            public bool Changed;
            public bool TemplateChanged;
            public bool CanApply;
            public bool RuntimeSyncRequested;
            public DimensionPortalVisualProfileAsset UseProfileRequested;
            public DimensionPortalVisualProfileAsset EditingProfile;
            public string Message;
            public MessageType MessageType;
        }

        internal enum StudioLayer
        {
            Frame,
            ChargeSweep,
            Milestones,
            Center,
            InnerFlecks,
            ReadyBurst,
            GroundLight
        }

        private enum PreviewPhase
        {
            Charging,
            Activated
        }

        private enum StudioLayoutMode
        {
            Compact,
            Standard,
            Expanded
        }

        private enum PendingPresetAction
        {
            None,
            Save,
            Duplicate,
            Create,
            Browse,
            Use
        }

        private struct AssetIdentity
        {
            public string Guid;
            public long LocalFileId;
            public int InstanceId;

            public bool HasPersistentIdentity
            {
                get { return !string.IsNullOrEmpty(Guid); }
            }
        }

        private struct ColorRole
        {
            public readonly string PropertyName;
            public readonly string Label;
            public readonly string ShortLabel;
            public readonly bool IsHdr;
            public readonly bool IsPaletteColor;

            public ColorRole(
                string propertyName,
                string label,
                string shortLabel,
                bool isHdr,
                bool isPaletteColor)
            {
                PropertyName = propertyName;
                Label = label;
                ShortLabel = shortLabel;
                IsHdr = isHdr;
                IsPaletteColor = isPaletteColor;
            }
        }

        private struct ColorPickerKey : IEquatable<ColorPickerKey>
        {
            public int TargetInstanceId;
            public string PropertyPath;

            public bool Equals(ColorPickerKey other)
            {
                return TargetInstanceId == other.TargetInstanceId &&
                       string.Equals(PropertyPath, other.PropertyPath, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ColorPickerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return TargetInstanceId * 397 ^
                           StringComparer.Ordinal.GetHashCode(PropertyPath ?? string.Empty);
                }
            }
        }

        private sealed class PreviewSheet
        {
            public Texture2D Texture;
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public int FrameCount;
            public bool OwnsTexture;

            public int FrameWidth
            {
                get { return FrameCount <= 0 ? 0 : Width / FrameCount; }
            }

            public bool TryGetPixel(int frameIndex, int x, int y, out Color32 color)
            {
                color = default(Color32);
                int frameWidth = FrameWidth;
                if (Pixels == null ||
                    frameWidth <= 0 ||
                    x < 0 ||
                    x >= frameWidth ||
                    y < 0 ||
                    y >= Height)
                {
                    return false;
                }

                int safeFrame = Mathf.Clamp(frameIndex, 0, FrameCount - 1);
                int pixelIndex = y * Width + safeFrame * frameWidth + x;
                if (pixelIndex < 0 || pixelIndex >= Pixels.Length)
                {
                    return false;
                }

                color = Pixels[pixelIndex];
                return true;
            }
        }

        private sealed class RecolorCacheEntry
        {
            public PreviewSheet Source;
            public Color32[] SourcePalette;
            public PreviewSheet Recolored;
            public byte[] SourcePaletteIndices;
            public int PaletteHash;
        }

        private sealed class FrameCompositeCacheEntry
        {
            public PreviewSheet Albedo;
            public PreviewSheet Emissive;
            public PreviewSheet Composite;
            public int CompositeHash;
        }

        private struct AnimationCacheKey : IEquatable<AnimationCacheKey>
        {
            public int AssetInstanceId;
            public int AnimationIndex;
            public int FallbackFrames;
            public float FallbackFps;

            public bool Equals(AnimationCacheKey other)
            {
                return AssetInstanceId == other.AssetInstanceId &&
                       AnimationIndex == other.AnimationIndex &&
                       FallbackFrames == other.FallbackFrames &&
                       FallbackFps.Equals(other.FallbackFps);
            }

            public override bool Equals(object obj)
            {
                return obj is AnimationCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = AssetInstanceId;
                    hash = hash * 397 ^ AnimationIndex;
                    hash = hash * 397 ^ FallbackFrames;
                    hash = hash * 397 ^ FallbackFps.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class AnimationCacheEntry
        {
            public FrameAnimation Animation;
            public int Signature;
            public AnimationSheet Sheet;
        }

        private struct TextureSlotCacheKey : IEquatable<TextureSlotCacheKey>
        {
            public int ProfileInstanceId;
            public DimensionPortalArtworkLayer Layer;
            public long AddressLow;
            public long AddressHigh;
            public int ResolvedAssetInstanceId;
            public int ResolvedAssetDirtyCount;

            public bool Equals(TextureSlotCacheKey other)
            {
                return ProfileInstanceId == other.ProfileInstanceId &&
                       Layer == other.Layer &&
                       AddressLow == other.AddressLow &&
                       AddressHigh == other.AddressHigh &&
                       ResolvedAssetInstanceId == other.ResolvedAssetInstanceId &&
                       ResolvedAssetDirtyCount == other.ResolvedAssetDirtyCount;
            }

            public override bool Equals(object obj)
            {
                return obj is TextureSlotCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ProfileInstanceId;
                    hash = hash * 397 ^ (int)Layer;
                    hash = hash * 397 ^ AddressLow.GetHashCode();
                    hash = hash * 397 ^ AddressHigh.GetHashCode();
                    hash = hash * 397 ^ ResolvedAssetInstanceId;
                    hash = hash * 397 ^ ResolvedAssetDirtyCount;
                    return hash;
                }
            }
        }

        private sealed class TextureSlotCacheEntry
        {
            public DimensionPortalArtworkEditorUtility.TextureSlot[] Slots;
            public string Message;
            public bool UsesDirectTextureOverride;
        }

        private sealed class PendingTextureUpdate
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public DimensionPortalArtworkLayer Layer;
            public Texture2D[] Textures;
            public Texture2D[] EmissiveTextures;
            public Texture2D[] NormalTextures;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public string ExpectedAssetGuid;
            public DimensionPortalArtworkReferenceKind ExpectedReferenceKind;
            public AssetIdentity ExpectedTemplateIdentity;
            public AssetIdentity ExpectedProfileIdentity;
        }

        private sealed class PendingSwirlTextureUpdate
        {
            public DimensionTemplateAsset Template;
            public DimensionPortalVisualProfileAsset Profile;
            public Texture2D ColorTexture;
            public Texture2D EmissiveTexture;
            public Texture2D NormalTexture;
            public long ExpectedAddressLow;
            public long ExpectedAddressHigh;
            public AssetIdentity ExpectedTemplateIdentity;
            public AssetIdentity ExpectedProfileIdentity;
        }

        private sealed class SwirlTextureSlotCacheEntry
        {
            public int ProfileInstanceId;
            public long AddressLow;
            public long AddressHigh;
            public DimensionPortalSwirlArtworkEditorUtility.AnimationZeroTextureSlot Slot;
            public string Message;
        }

        private struct VisibleFrame
        {
            public StudioLayer Layer;
            public PreviewSheet DisplaySheet;
            public PreviewSheet HitSheet;
            public Rect CanvasRect;
            public int FrameIndex;
            public Color Tint;
            public Color32[] SourcePalette;
            public bool PalettePickingEnabled;
            public Vector2 Pivot;
            public Vector2 Scale;
            public float RotationDegrees;
            public bool FlipX;
            public bool FlipY;
            public string SourceLabel;
            public bool DirectOverride;
        }

        private struct FleckPreviewState
        {
            public bool Visible;
            public Vector2 Origin;
            public Vector2 Scale;
            public float EmissionMultiplier;
            public float SizeMultiplier;
            public float RadiusMultiplier;
        }

        private struct AnimationSheet
        {
            public PreviewSheet Sheet;
            public int FrameCount;
            public float Fps;
            public bool Loop;
            public Vector2 Pivot;
            public FrameAnimation Animation;
            public int[] RuntimeFrameRemap;
            public string SourceLabel;
        }

        private const int CanonicalCanvasPixels =
            DimensionPortalVisualContract.CanonicalFramePixels;
        private const float SidebarWidth = 184f;
        private const float InspectorMinWidth = 300f;
        private const float InspectorPreferredWidth = 360f;
        private const float CompactBreakpoint = 760f;
        private const float ExpandedBreakpoint = 1120f;
        private const float LayoutHysteresis = 24f;
        private const double PreviewFrameInterval = 1.0 / 60.0;
        private const double PreviewClockResumeThreshold = 0.5;
        private const float DemonstrationChargeSeconds = 8f;
        private const float PortalPreviewEmissionExposure = 0.22f;
        private const float PickerHueSaturationEpsilon = 0.0001f;
        private const int PreviewZoomStepPercent = 10;
        private const int MinPreviewZoomPercent = 10;
        private const int MaxPreviewZoomPercent = 100;
        private const float PreviewFitInset = 4f;
        private const float PreviewToolbarMinimumWidth = 300f;
        private const string HexColorFieldControlName =
            "DimensionPortalAppearanceStudio.HexColor";
        private const int CanvasControlHint = 0x504F5254;

        private static readonly string[] PreviewPhaseLabels = { "Charging", "Activated" };
        private static readonly string[] ChargePaletteProperties =
        {
            "chargeWaveDarkColor",
            "chargeWaveDeepColor",
            "chargeWaveMidColor",
            "chargeWaveBrightColor",
            "chargeWaveCoreColor"
        };
        private static readonly string[] MilestonePaletteProperties =
        {
            "milestoneDarkColor",
            "milestoneDeepColor",
            "milestoneMidColor",
            "milestoneBrightColor",
            "milestoneCoreColor"
        };
        private static readonly string[] CenterPaletteProperties =
        {
            "centerDarkColor",
            "centerDeepColor",
            "centerMidColor",
            "centerBrightColor",
            "centerCoreColor",
            "centerHighlightColor"
        };

        private const string FrameTexturePath =
            "Assets/ExpandNullforge/VanillaPortalReference/Texture2D/portal.png";
        private const string ChargeTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/PortalEmissiveWave_wave.png";
        private const string MilestoneTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/PortalChargeProgress_progress.png";
        private const string CenterIdleTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/PortalCenterEffect_idle.png";
        private const string CenterOpeningTexturePath =
            "Assets/ExpandNullforge/PortalVisuals/Texture2D/PortalCenterEffect_open.png";
        private const int CenterOpeningFrameCountVanilla = 4;

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

        private static readonly ColorRole[] FrameColorRoles =
        {
            new ColorRole("frameTint", "Frame tint", "TINT", false, false),
            new ColorRole("frameEmissiveColor", "Frame emission", "GLOW", true, false)
        };

        private static readonly ColorRole[] ChargeColorRoles =
        {
            new ColorRole("chargeWaveDarkColor", "Dark trail", "DARK", false, true),
            new ColorRole("chargeWaveDeepColor", "Deep trail", "DEEP", false, true),
            new ColorRole("chargeWaveMidColor", "Sweep body", "BODY", false, true),
            new ColorRole("chargeWaveBrightColor", "Bright edge", "EDGE", false, true),
            new ColorRole("chargeWaveCoreColor", "Sweep core", "CORE", false, true),
            new ColorRole("chargeWaveTint", "Sweep tint", "TINT", false, false),
            new ColorRole("chargeWaveEmissiveColor", "Sweep emission", "GLOW", true, false)
        };

        private static readonly ColorRole[] MilestoneColorRoles =
        {
            new ColorRole("milestoneDarkColor", "Dark edge", "DARK", false, true),
            new ColorRole("milestoneDeepColor", "Deep edge", "DEEP", false, true),
            new ColorRole("milestoneMidColor", "Blob body", "BODY", false, true),
            new ColorRole("milestoneBrightColor", "Bright edge", "EDGE", false, true),
            new ColorRole("milestoneCoreColor", "Blob core", "CORE", false, true),
            new ColorRole("milestoneTint", "Milestone tint", "TINT", false, false),
            new ColorRole("milestoneEmissiveColor", "Milestone emission", "GLOW", true, false)
        };

        private static readonly ColorRole[] CenterColorRoles =
        {
            new ColorRole("centerDarkColor", "Inner dark", "DARK", false, true),
            new ColorRole("centerDeepColor", "Inner artwork", "DEEP", false, true),
            new ColorRole("centerMidColor", "Outer ring", "RING", false, true),
            new ColorRole("centerBrightColor", "Bright swirl", "EDGE", false, true),
            new ColorRole("centerCoreColor", "Portal core", "CORE", false, true),
            new ColorRole("centerHighlightColor", "White highlight", "HIGH", false, true),
            new ColorRole("centerTint", "Center tint", "TINT", false, false),
            new ColorRole("centerEmissiveColor", "Center emission", "GLOW", true, false)
        };

        private static readonly ColorRole[] FlecksColorRoles =
        {
            new ColorRole("centerParticleTint", "Swirl tint", "TINT", false, false),
            new ColorRole("centerSwirlEmissiveColor", "Swirl emission", "GLOW", true, false)
        };

        private static readonly ColorRole[] ReadyBurstColorRoles =
        {
            new ColorRole("readyFlashTint", "Ready burst color", "BURST", false, false)
        };

        private static readonly ColorRole[] LightColorRoles =
        {
            new ColorRole("groundLightColor", "Projected light", "LIGHT", false, false)
        };

        private readonly Dictionary<long, PreviewSheet> sourceSheets =
            new Dictionary<long, PreviewSheet>();
        private readonly Dictionary<string, PreviewSheet> sourceSheetsByPath =
            new Dictionary<string, PreviewSheet>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecolorCacheEntry> recoloredSheets =
            new Dictionary<string, RecolorCacheEntry>(StringComparer.Ordinal);
        private readonly Dictionary<long, FrameCompositeCacheEntry> frameCompositeSheets =
            new Dictionary<long, FrameCompositeCacheEntry>();
        private readonly Dictionary<AnimationCacheKey, AnimationCacheEntry> animationSheets =
            new Dictionary<AnimationCacheKey, AnimationCacheEntry>();
        private readonly List<VisibleFrame> visibleFrames = new List<VisibleFrame>(5);
        private readonly List<string> previewWarnings = new List<string>(8);
        private readonly List<string> previewErrors = new List<string>(4);
        private readonly int[] selectedColorRoleByLayer =
            new int[Enum.GetValues(typeof(StudioLayer)).Length];
        private readonly Color[] effectPaletteBuffer = new Color[5];
        private readonly Color[] centerPaletteBuffer = new Color[6];
        private readonly Dictionary<ColorPickerKey, float> retainedHueByColor =
            new Dictionary<ColorPickerKey, float>();
        private readonly DimensionPortalParticlePreviewRenderer particlePreviewRenderer =
            new DimensionPortalParticlePreviewRenderer();
        private FleckPreviewState fleckPreviewState;

        private StudioLayer selectedLayer = StudioLayer.ChargeSweep;
        private PreviewPhase previewPhase = PreviewPhase.Charging;
        private StudioLayoutMode layoutMode = StudioLayoutMode.Expanded;
        private bool isPlaying = true;
        private bool showGrid = true;
        private bool showGuides = true;
        private int pinnedLayerIndex = -1;
        private bool paletteFocusActive;
        private StudioLayer paletteFocusLayer;
        private int paletteFocusRoleIndex = -1;
        private float activeZoom = 6f;
        private float fittedZoom = 6f;
        private int previewZoomPercent;
        private bool previewZoomWasAdjusted;
        private int canvasPixelWidth = CanonicalCanvasPixels;
        private int canvasPixelHeight = CanonicalCanvasPixels;
        private Rect activeViewBounds = new Rect(0f, 0f, CanonicalCanvasPixels, CanonicalCanvasPixels);
        private Vector2 bodyScreenOrigin;
        private Vector2 bodyPivot = new Vector2(0.5f, 0.5f);
        private bool hasSelectedPixel;
        private float chargeProgress = 0.38f;
        private float animationClock;
        private float activatedClock;
        private float currentCenterOpeningDuration = 0.4f;
        private bool skipCenterOpeningOnNextBuild = true;
        private bool currentCenterIsOpening;
        // One-shot closing replay for the instant portal preview: plays the closing sheet from
        // a fresh clock, hides the swirls while it runs (matching the in-game close), then
        // returns to the mature loop.
        private bool previewCenterClosingActive;
        private double lastClockTime;
        private double nextRepaintTime;
        private Vector2Int selectedPixel = new Vector2Int(-1, -1);
        private StudioLayer selectedPixelLayer;
        private Vector2Int selectedSourcePixel = new Vector2Int(-1, -1);
        private int canvasControlId;
        private bool canvasDragPending;
        private bool canvasDragUndoRecorded;
        private StudioLayer canvasDragLayer;
        private Vector2 canvasDragStartPoint;
        private Vector2 canvasDragStartOffset;

        private Texture2D hueTexture;
        private Texture2D saturationValueTexture;
        private Texture2D paletteFocusTexture;
        private Texture2D pinnedLayerIconTexture;
        private Texture2D unpinnedLayerIconTexture;
        private Color32[] paletteFocusPixels;
        private Color32[] saturationValuePixels;
        private PreviewSheet paletteFocusDisplaySheet;
        private PreviewSheet paletteFocusHitSheet;
        private Color32[] paletteFocusSourcePalette;
        private StudioLayer paletteFocusTextureLayer;
        private int paletteFocusTextureRole = -1;
        private float saturationValueHue = -1f;
        private ColorPickerKey hexEditKey;
        private bool hasHexEditKey;
        private bool hexEditWasFocused;
        private string hexEditBuffer = string.Empty;
        private GUIStyle layerButtonStyle;
        private GUIStyle layerChipStyle;
        private GUIStyle swatchLabelStyle;
        private GUIStyle selectedSwatchLabelStyle;
        private readonly float[] measuredArtworkMainPanelHeights =
            { 190f, 190f, 190f, 190f, 190f };
        private float measuredSwirlMainPanelHeight = 190f;
        private readonly Dictionary<TextureSlotCacheKey, TextureSlotCacheEntry>
            textureSlotCache =
                new Dictionary<TextureSlotCacheKey, TextureSlotCacheEntry>();
        private SwirlTextureSlotCacheEntry swirlTextureSlotCache;
        private SerializedObject serializedTemplateCache;
        private DimensionTemplateAsset serializedTemplateTarget;
        private SerializedObject serializedProfileCache;
        private DimensionPortalVisualProfileAsset serializedProfileTarget;
        private int colorUndoGroup = -1;
        private bool collapseColorUndoAfterApply;
        private bool paletteBakeRequested;
        private StudioLayer paletteBakeLayer;
        private bool undoRedoPaletteCheckRequested;
        private bool hasPaletteSnapshot;
        private bool previewCompositionDirty = true;
        private bool hasPreviewComposition;
        private DimensionPortalVisualProfileAsset previewCompositionProfile;
        private int previewCompositionProfileDirtyCount = -1;
        private DimensionPortalVisualProfileAsset paletteSnapshotProfile;
        private int chargePaletteSnapshotHash;
        private int milestonePaletteSnapshotHash;
        private int centerPaletteSnapshotHash;
        private bool artworkVariantCreateQueued;
        private StudioLayer pendingArtworkVariantLayer;
        private int artworkVariantRequestVersion;
        private bool textureUpdateQueued;
        private PendingTextureUpdate pendingTextureUpdate;
        private bool swirlTextureUpdateQueued;
        private PendingSwirlTextureUpdate pendingSwirlTextureUpdate;
        private IReadOnlyList<DimensionPortalVisualProfileAsset> presetListCache;
        private string[] presetLabelCache = Array.Empty<string>();
        private bool presetActionQueued;
        private PendingPresetAction pendingPresetAction;
        private DimensionTemplateAsset pendingPresetTemplate;
        private DimensionPortalVisualProfileAsset pendingPresetTarget;
        private DimensionPortalVisualProfileAsset pendingPresetBindingProfile;
        private AssetIdentity pendingPresetTemplateIdentity;
        private AssetIdentity pendingPresetTargetIdentity;
        private AssetIdentity pendingPresetBindingProfileIdentity;
        private DimensionTemplateAsset activeTemplate;
        private DimensionPortalVisualProfileAsset activeProfile;
        private DimensionTemplateAsset editingProfileTemplate;
        private DimensionPortalVisualProfileAsset editingProfile;
        private readonly DimensionPortalProfileEditSession profileEditSession =
            new DimensionPortalProfileEditSession();
        private bool templateSettingsChanged;
        private DimensionPortalVisualProfileAsset runtimeOutOfDateProfile;
        private bool runtimeSyncRequested;
        private DimensionPortalVisualProfileAsset useProfileRequested;
        private bool repaintRequested;
        private bool disposed;
        private string pendingArtworkMessage;
        private MessageType pendingArtworkMessageType = MessageType.Info;

        private bool instantPortalMode;

        public DimensionPortalAppearanceStudio()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        /// <summary>
        /// Switches this studio instance to the instant item-portal (V2) flavor: the Center layer
        /// edits the three-animation instant contract (idle, opening, closing at five 20 x 25
        /// frames), every bound-profile check reads the template's item portal profile, and the
        /// layers an instant portal can never show (frame, charge sweep, milestones, ready burst)
        /// are removed from the navigation.
        /// </summary>
        public void ConfigureInstantPortalMode()
        {
            instantPortalMode = true;
            selectedLayer = StudioLayer.Center;
            // An instant portal spawns fully charged, so its preview has no charging phase.
            previewPhase = PreviewPhase.Activated;
            skipCenterOpeningOnNextBuild = true;
        }

        /// <summary>
        /// Layers hidden in the instant-portal studio: an instant portal is inherently frameless
        /// and spawns fully charged, so the frame, charge sweep, milestones and the
        /// charge-completion ready burst can never appear on it.
        /// </summary>
        private static bool IsHiddenInstantLayer(StudioLayer layer)
        {
            return layer == StudioLayer.Frame ||
                   layer == StudioLayer.ChargeSweep ||
                   layer == StudioLayer.Milestones ||
                   layer == StudioLayer.ReadyBurst;
        }

        private DimensionPortalArtworkLayer CenterArtworkLayer
        {
            get
            {
                return instantPortalMode
                    ? DimensionPortalArtworkLayer.CenterInstant
                    : DimensionPortalArtworkLayer.Center;
            }
        }

        private DimensionPortalVisualProfileAsset GetBoundProfile(DimensionTemplateAsset template)
        {
            return template == null
                ? null
                : instantPortalMode
                    ? template.ItemPortalVisualProfile
                    : template.PortalVisualProfile;
        }

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        public bool HasPendingChanges
        {
            get { return profileEditSession.HasChanges; }
        }

        public string EditingProfileDisplayName
        {
            get { return GetPresetDisplayName(editingProfile); }
        }

        public bool SavePendingChanges(
            out bool runtimeUpdateRequired,
            out string message)
        {
            runtimeUpdateRequired = false;
            message = string.Empty;
            DimensionTemplateAsset template = activeTemplate ?? editingProfileTemplate;
            DimensionPortalVisualProfileAsset profile = activeProfile ?? editingProfile;
            if (template == null || profile == null ||
                !DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    profile))
            {
                message = "The Portal Studio no longer has a valid profile to save.";
                return false;
            }

            if (!FlushPendingFrameTextureUpdate(out message) ||
                !DimensionPortalArtworkEditorUtility.FlushPending(profile, out message) ||
                !DimensionPortalPresetEditorUtility.SaveProfile(
                    template,
                    profile,
                    out message))
            {
                return false;
            }

            if (!profileEditSession.Commit(out string baselineMessage))
            {
                message = string.IsNullOrEmpty(baselineMessage)
                    ? "The portal was saved, but its editing baseline could not be refreshed."
                    : baselineMessage;
                return false;
            }

            DimensionPortalVisualProfileAsset boundProfile =
                GetBoundProfile(template);
            runtimeUpdateRequired =
                profile == boundProfile ||
                templateSettingsChanged ||
                runtimeOutOfDateProfile == boundProfile;
            templateSettingsChanged = false;
            if (runtimeUpdateRequired)
            {
                runtimeOutOfDateProfile = boundProfile;
            }

            return true;
        }

        public bool DiscardPendingChanges(out string message)
        {
            message = string.Empty;
            CancelPendingTextureUpdate();
            DimensionPortalArtworkEditorUtility.CancelPending(editingProfile);
            if (!profileEditSession.Discard(out message))
            {
                return false;
            }

            editingProfile = profileEditSession.Profile;
            activeProfile = editingProfile;
            templateSettingsChanged = false;
            serializedTemplateCache = null;
            serializedTemplateTarget = null;
            serializedProfileCache = null;
            serializedProfileTarget = null;
            hasPaletteSnapshot = false;
            InvalidateTextureSlotCache();
            ClearPreviewAssetCaches();
            ClearPaletteFocus();
            hasSelectedPixel = false;
            repaintRequested = true;
            return true;
        }

        public void NotifyRuntimeSyncResult(
            DimensionPortalVisualProfileAsset profile,
            bool succeeded)
        {
            if (succeeded)
            {
                runtimeOutOfDateProfile = null;
            }
            else if (profile != null)
            {
                runtimeOutOfDateProfile = profile;
            }

            repaintRequested = true;
        }

        public bool Tick(bool active)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!active)
            {
                activeTemplate = null;
                activeProfile = null;
            }

            bool repaintForUndoRedo = active && undoRedoPaletteCheckRequested;
            bool repaintForRequest = active && repaintRequested;
            if (repaintForRequest)
            {
                repaintRequested = false;
            }

            bool forcedRepaint = repaintForUndoRedo || repaintForRequest;
            if (!active ||
                !isPlaying ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                lastClockTime = now;
                nextRepaintTime = 0.0;
                return forcedRepaint;
            }

            if (lastClockTime <= 0.0)
            {
                lastClockTime = now;
            }

            double elapsed = Math.Max(0.0, now - lastClockTime);
            lastClockTime = now;
            if (elapsed > PreviewClockResumeThreshold)
            {
                // Preserve forward motion after a slow editor/import frame. Dropping
                // the whole interval can freeze playback indefinitely when repeated
                // hitches are slightly longer than the resume threshold.
                elapsed = PreviewClockResumeThreshold;
                nextRepaintTime = now;
            }

            AdvancePreviewClock((float)elapsed);
            if (nextRepaintTime <= 0.0)
            {
                nextRepaintTime = now;
            }

            if (now < nextRepaintTime)
            {
                return forcedRepaint;
            }

            double missedIntervals = Math.Floor(
                Math.Max(0.0, now - nextRepaintTime) / PreviewFrameInterval);
            nextRepaintTime += (missedIntervals + 1.0) * PreviewFrameInterval;
            return true;
        }

        public DrawResult Draw(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            float availableWidth,
            float availableHeight)
        {
            if (instantPortalMode)
            {
                if (IsHiddenInstantLayer(selectedLayer))
                {
                    selectedLayer = StudioLayer.Center;
                }

                // No charging phase exists for an instant portal; heal any state a hot
                // reload restored.
                if (previewPhase == PreviewPhase.Charging)
                {
                    previewPhase = PreviewPhase.Activated;
                    skipCenterOpeningOnNextBuild = true;
                    previewCompositionDirty = true;
                }
            }

            profile = ResolveEditingProfile(template, profile);
            DrawResult result = new DrawResult
            {
                CanApply = profile != null,
                RuntimeSyncRequested = runtimeSyncRequested,
                UseProfileRequested = useProfileRequested,
                EditingProfile = profile,
                Message = pendingArtworkMessage ?? string.Empty,
                MessageType = string.IsNullOrEmpty(pendingArtworkMessage)
                    ? MessageType.Info
                    : pendingArtworkMessageType
            };
            runtimeSyncRequested = false;
            useProfileRequested = null;
            pendingArtworkMessage = string.Empty;
            activeTemplate = template;
            activeProfile = profile;
            if (profile == null)
            {
                return result;
            }

            paletteBakeRequested = false;

            SerializedObject serializedTemplate = GetSerializedTemplate(template);
            serializedTemplate.UpdateIfRequiredOrScript();
            SerializedObject serializedProfile = GetSerializedProfile(profile);
            serializedProfile.UpdateIfRequiredOrScript();
            ProcessUndoRedoPaletteChanges(template, profile, serializedProfile);
            EnsurePreviewComposition(serializedProfile);
            UpdateLayoutMode(Mathf.Max(360f, availableWidth));
            ConfigurePreviewViewBounds();
            float previewColumnWidth = GetPreviewColumnWidth(
                Mathf.Max(360f, availableWidth),
                layoutMode);
            ResolveActiveZoom(
                previewColumnWidth,
                Mathf.Clamp(availableHeight - 250f, 300f, 560f));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawStudioHeader(
                profile,
                serializedTemplate,
                Mathf.Max(360f, availableWidth));
            GUILayout.Space(6f);

            if (layoutMode == StudioLayoutMode.Expanded)
            {
                EditorGUILayout.BeginHorizontal();
                DrawLayerNavigation();
                GUILayout.Space(6f);
                DrawPreviewCanvas(
                    template,
                    profile,
                    serializedProfile,
                    previewErrors.Count == 0);
                GUILayout.Space(8f);
                DrawContextInspector(
                    template,
                    serializedProfile,
                    ref result,
                    0f,
                    Mathf.Clamp(
                        availableWidth * 0.34f,
                        InspectorMinWidth,
                        460f));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawCompactLayerNavigation(layoutMode == StudioLayoutMode.Compact);
                GUILayout.Space(5f);
                if (layoutMode == StudioLayoutMode.Standard)
                {
                    EditorGUILayout.BeginHorizontal();
                    DrawPreviewCanvas(
                        template,
                        profile,
                        serializedProfile,
                        previewErrors.Count == 0);
                    GUILayout.Space(8f);
                    DrawContextInspector(
                        template,
                        serializedProfile,
                        ref result,
                        0f,
                        Mathf.Clamp(
                            availableWidth * 0.42f,
                            InspectorMinWidth,
                            420f));
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    DrawPreviewCanvas(
                        template,
                        profile,
                        serializedProfile,
                        previewErrors.Count == 0);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(6f);
                    DrawContextInspector(template, serializedProfile, ref result);
                }
            }

            GUILayout.Space(6f);
            DrawPreviewIssues();
            EditorGUILayout.EndVertical();

            result.CanApply = previewErrors.Count == 0;

            bool profileChanged = serializedProfile.ApplyModifiedProperties();
            bool templateChanged = serializedTemplate.ApplyModifiedProperties();
            result.Changed = result.Changed || profileChanged || templateChanged;
            result.TemplateChanged = templateChanged;
            if (profileChanged)
            {
                EditorUtility.SetDirty(profile);
                previewCompositionDirty = true;
                repaintRequested = true;
                if (paletteBakeRequested &&
                    TryGetArtworkLayer(paletteBakeLayer, out DimensionPortalArtworkLayer artworkLayer, instantPortalMode))
                {
                    DimensionPortalArtworkEditorUtility.QueuePaletteBake(
                        template,
                        profile,
                        artworkLayer);
                }

                if (selectedLayer == StudioLayer.InnerFlecks)
                {
                    // Bake the chosen color into the profile-owned Swirls SpriteAsset (like
                    // every other layer). Debounced so a color drag only replaces a pending
                    // record; the neutral white starter makes any hue reproduce correctly.
                    DimensionPortalSwirlArtworkEditorUtility.QueueSwirlBake(
                        template,
                        profile);
                }
            }

            if (templateChanged)
            {
                EditorUtility.SetDirty(template);
                templateSettingsChanged = true;
                repaintRequested = true;
            }

            if (result.Changed)
            {
                profileEditSession.MarkChanged();
            }

            CapturePaletteSnapshot(profile, serializedProfile);

            if (collapseColorUndoAfterApply && colorUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(colorUndoGroup);
                colorUndoGroup = -1;
                collapseColorUndoAfterApply = false;
            }

            return result;
        }

        private DimensionPortalVisualProfileAsset ResolveEditingProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset boundProfile)
        {
            bool selectionIsValid =
                template != null &&
                editingProfileTemplate == template &&
                editingProfile != null &&
                DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    editingProfile);
            if (selectionIsValid)
            {
                return editingProfile;
            }

            if (template == null || boundProfile == null)
            {
                editingProfileTemplate = template;
                editingProfile = null;
                return null;
            }

            SelectEditingProfile(template, boundProfile);
            return editingProfile;
        }

        private bool SelectEditingProfile(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile)
        {
            if (template == null || profile == null ||
                !DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                    template,
                    profile))
            {
                pendingArtworkMessage =
                    "The selected portal profile does not belong to this Dimension Asset.";
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            // The host window initializes the profile currently bound to the Dimension
            // Asset, but Portal Studio can browse any package-owned profile without
            // binding it first. Migrate that selected profile before capturing the edit
            // session baseline; otherwise older profiles expose newly-added fields (most
            // visibly the Swirls artwork reference) as their serialized zero/default
            // values until they happen to become the bound profile.
            bool profileMigrated =
                DimensionPortalArtworkEditorUtility.EnsureProfileInitialized(
                    template,
                    profile,
                    out string initializationMessage);
            if (!profileMigrated && !string.IsNullOrEmpty(initializationMessage))
            {
                pendingArtworkMessage = initializationMessage;
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            if (profileMigrated)
            {
                // Migration is framework bookkeeping, not an author edit. Persist it
                // before Begin() snapshots the clean profile state so switching away does
                // not offer to discard the framework-assigned artwork defaults.
                AssetDatabase.SaveAssetIfDirty(profile);
            }

            if (!profileEditSession.Begin(template, profile, out string message))
            {
                pendingArtworkMessage = message;
                pendingArtworkMessageType = MessageType.Error;
                return false;
            }

            editingProfileTemplate = template;
            editingProfile = profile;
            activeTemplate = template;
            activeProfile = profile;

            serializedProfileCache = null;
            serializedProfileTarget = null;
            paletteSnapshotProfile = null;
            hasPaletteSnapshot = false;
            InvalidateTextureSlotCache();
            ClearPreviewAssetCaches();
            ClearPaletteFocus();
            hasSelectedPixel = false;
            repaintRequested = true;
            return true;
        }

        public void Dispose()
        {
            disposed = true;
            DimensionPortalArtworkEditorUtility.CancelPending(editingProfile);
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.projectChanged -= OnProjectChanged;
            ClearPreviewAssetCaches();
            DestroyTexture(ref hueTexture);
            DestroyTexture(ref saturationValueTexture);
            DestroyTexture(ref paletteFocusTexture);
            DestroyTexture(ref pinnedLayerIconTexture);
            DestroyTexture(ref unpinnedLayerIconTexture);
            particlePreviewRenderer.Dispose();
            paletteFocusPixels = null;
            saturationValuePixels = null;
            retainedHueByColor.Clear();
            hasHexEditKey = false;
            hexEditWasFocused = false;
            hexEditBuffer = string.Empty;
            InvalidatePaletteFocusTexture();
            serializedTemplateCache = null;
            serializedTemplateTarget = null;
            serializedProfileCache = null;
            serializedProfileTarget = null;
            paletteSnapshotProfile = null;
            hasPaletteSnapshot = false;
            pendingTextureUpdate = null;
            EditorApplication.delayCall -= ProcessPendingTextureUpdate;
            textureUpdateQueued = false;
            pendingSwirlTextureUpdate = null;
            EditorApplication.delayCall -= ProcessPendingSwirlTextureUpdate;
            swirlTextureUpdateQueued = false;
            EditorApplication.delayCall -= ProcessPendingPresetAction;
            presetActionQueued = false;
            pendingPresetAction = PendingPresetAction.None;
            pendingPresetTemplate = null;
            pendingPresetTarget = null;
            pendingPresetBindingProfile = null;
            pendingPresetTemplateIdentity = default(AssetIdentity);
            pendingPresetTargetIdentity = default(AssetIdentity);
            pendingPresetBindingProfileIdentity = default(AssetIdentity);
            presetListCache = null;
            presetLabelCache = Array.Empty<string>();
            profileEditSession.Dispose();
            editingProfileTemplate = null;
            editingProfile = null;
            activeTemplate = null;
            activeProfile = null;
        }

        private void OnUndoRedoPerformed()
        {
            undoRedoPaletteCheckRequested = true;
            profileEditSession.MarkChanged();
            CancelPendingTextureUpdate();
            ClearPreviewAssetCaches();
            repaintRequested = true;
        }

        private void OnProjectChanged()
        {
            if (disposed)
            {
                return;
            }

            InvalidateTextureSlotCache();
            ClearPreviewAssetCaches();
            repaintRequested = true;
        }

        private void ClearPreviewAssetCaches()
        {
            foreach (KeyValuePair<long, PreviewSheet> pair in sourceSheets)
            {
                DestroyOwnedSheet(pair.Value);
            }

            foreach (KeyValuePair<string, RecolorCacheEntry> pair in recoloredSheets)
            {
                if (pair.Value != null)
                {
                    DestroyOwnedSheet(pair.Value.Recolored);
                }
            }

            foreach (KeyValuePair<long, FrameCompositeCacheEntry> pair in frameCompositeSheets)
            {
                if (pair.Value != null)
                {
                    DestroyOwnedSheet(pair.Value.Composite);
                }
            }

            sourceSheets.Clear();
            sourceSheetsByPath.Clear();
            recoloredSheets.Clear();
            frameCompositeSheets.Clear();
            animationSheets.Clear();
            textureSlotCache.Clear();
            visibleFrames.Clear();
            previewWarnings.Clear();
            previewErrors.Clear();
            previewCompositionDirty = true;
            hasPreviewComposition = false;
            previewCompositionProfile = null;
            previewCompositionProfileDirtyCount = -1;
            InvalidatePaletteFocusTexture();
            particlePreviewRenderer.Invalidate();
        }

        private void ProcessUndoRedoPaletteChanges(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile)
        {
            if (!hasPaletteSnapshot || paletteSnapshotProfile != profile)
            {
                CapturePaletteSnapshot(profile, serializedProfile);
                undoRedoPaletteCheckRequested = false;
                return;
            }

            if (!undoRedoPaletteCheckRequested)
            {
                return;
            }

            undoRedoPaletteCheckRequested = false;
            int chargeHash = GetPaletteHash(GetPalette(serializedProfile, ChargePaletteProperties));
            int milestoneHash = GetPaletteHash(GetPalette(serializedProfile, MilestonePaletteProperties));
            int centerHash = GetPaletteHash(GetPalette(serializedProfile, CenterPaletteProperties));

            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                DimensionPortalArtworkLayer.ChargeSweep,
                "chargeWaveSpriteAsset",
                chargeHash != chargePaletteSnapshotHash);
            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                DimensionPortalArtworkLayer.Milestones,
                "milestoneSpriteAsset",
                milestoneHash != milestonePaletteSnapshotHash);
            QueueManagedPaletteAfterUndoRedo(
                template,
                profile,
                serializedProfile,
                CenterArtworkLayer,
                "centerEffectSpriteAsset",
                centerHash != centerPaletteSnapshotHash);
        }

        private static void QueueManagedPaletteAfterUndoRedo(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile,
            DimensionPortalArtworkLayer layer,
            string referencePropertyName,
            bool paletteChanged)
        {
            if (!paletteChanged || template == null || profile == null || serializedProfile == null)
            {
                return;
            }

            SerializedProperty reference = serializedProfile.FindProperty(referencePropertyName);
            if (DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    profile,
                    layer,
                    out _) == DimensionPortalArtworkReferenceKind.Managed)
            {
                DimensionPortalArtworkEditorUtility.QueuePaletteBake(
                    template,
                    profile,
                    layer);
            }
        }

        private void CapturePaletteSnapshot(
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile)
        {
            if (profile == null || serializedProfile == null)
            {
                paletteSnapshotProfile = null;
                hasPaletteSnapshot = false;
                return;
            }

            paletteSnapshotProfile = profile;
            chargePaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, ChargePaletteProperties));
            milestonePaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, MilestonePaletteProperties));
            centerPaletteSnapshotHash = GetPaletteHash(
                GetPalette(serializedProfile, CenterPaletteProperties));
            hasPaletteSnapshot = true;
        }

        private SerializedObject GetSerializedTemplate(
            DimensionTemplateAsset template)
        {
            if (serializedTemplateCache == null || serializedTemplateTarget != template)
            {
                serializedTemplateTarget = template;
                serializedTemplateCache = new SerializedObject(template);
            }

            return serializedTemplateCache;
        }

        private SerializedObject GetSerializedProfile(
            DimensionPortalVisualProfileAsset profile)
        {
            if (serializedProfileCache == null || serializedProfileTarget != profile)
            {
                serializedProfileTarget = profile;
                serializedProfileCache = new SerializedObject(profile);
                hasSelectedPixel = false;
                hasHexEditKey = false;
                hexEditWasFocused = false;
                ClearPaletteFocus();
                previewCompositionDirty = true;
                hasPreviewComposition = false;
                previewCompositionProfile = null;
                previewCompositionProfileDirtyCount = -1;
            }

            return serializedProfileCache;
        }

        private void AdvancePreviewClock(float delta)
        {
            if (delta <= 0f)
            {
                return;
            }

            animationClock += delta;
            previewCompositionDirty = true;
            if (previewPhase == PreviewPhase.Charging)
            {
                chargeProgress = Mathf.Repeat(
                    chargeProgress + delta / DemonstrationChargeSeconds,
                    1f);
            }
            else
            {
                activatedClock += delta;
            }
        }

        private void UpdateLayoutMode(float availableWidth)
        {
            switch (layoutMode)
            {
                case StudioLayoutMode.Expanded:
                    if (availableWidth < ExpandedBreakpoint - LayoutHysteresis)
                    {
                        layoutMode = availableWidth < CompactBreakpoint
                            ? StudioLayoutMode.Compact
                            : StudioLayoutMode.Standard;
                    }

                    break;
                case StudioLayoutMode.Standard:
                    if (availableWidth >= ExpandedBreakpoint + LayoutHysteresis)
                    {
                        layoutMode = StudioLayoutMode.Expanded;
                    }
                    else if (availableWidth < CompactBreakpoint - LayoutHysteresis)
                    {
                        layoutMode = StudioLayoutMode.Compact;
                    }

                    break;
                default:
                    if (availableWidth >= ExpandedBreakpoint + LayoutHysteresis)
                    {
                        layoutMode = StudioLayoutMode.Expanded;
                    }
                    else if (availableWidth >= CompactBreakpoint + LayoutHysteresis)
                    {
                        layoutMode = StudioLayoutMode.Standard;
                    }

                    break;
            }
        }

        private static float GetPreviewColumnWidth(float availableWidth, StudioLayoutMode mode)
        {
            switch (mode)
            {
                case StudioLayoutMode.Expanded:
                    return Mathf.Max(
                        220f,
                        availableWidth - SidebarWidth - InspectorPreferredWidth - 24f);
                case StudioLayoutMode.Standard:
                    return Mathf.Max(
                        210f,
                        availableWidth - Mathf.Clamp(
                            availableWidth * 0.42f,
                            InspectorMinWidth,
                            420f) - 12f);
                default:
                    return Mathf.Max(210f, availableWidth - 12f);
            }
        }

        private void ResolveActiveZoom(float availablePreviewWidth, float availablePreviewHeight)
        {
            float width = Mathf.Max(1f, activeViewBounds.width);
            float height = Mathf.Max(1f, activeViewBounds.height);
            int fitted = Mathf.Min(
                Mathf.FloorToInt(
                    (availablePreviewWidth - PreviewFitInset * 2f) / width),
                Mathf.FloorToInt(
                    (availablePreviewHeight - PreviewFitInset * 2f) / height));
            fittedZoom = Mathf.Clamp(fitted, 1, 10);
            if (!previewZoomWasAdjusted)
            {
                previewZoomPercent = Mathf.Clamp(
                    Mathf.RoundToInt(fittedZoom) * PreviewZoomStepPercent,
                    MinPreviewZoomPercent,
                    MaxPreviewZoomPercent);
            }
            else
            {
                previewZoomPercent = Mathf.Clamp(
                    previewZoomPercent,
                    MinPreviewZoomPercent,
                    MaxPreviewZoomPercent);
            }

            activeZoom = previewZoomPercent / (float)PreviewZoomStepPercent;
        }

        private void ConfigurePreviewViewBounds()
        {
            activeViewBounds = new Rect(
                0f,
                0f,
                canvasPixelWidth,
                canvasPixelHeight);
        }

        private void DrawStudioHeader(
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedTemplate,
            float availableWidth)
        {
            // The Portal Studio identity strip and its mode tabs now live in the window's header
            // (DrawPortalHeaderStrip); here we only surface the placed portal's charge-up time. An
            // instant portal spawns fully charged, so it has nothing to show.
            if (instantPortalMode)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawChargeDuration(serializedTemplate);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawChargeDuration(SerializedObject serializedTemplate)
        {
            SerializedProperty chargeDuration = serializedTemplate == null
                ? null
                : serializedTemplate.FindProperty("portalActivationChargeSeconds");
            if (chargeDuration == null)
            {
                return;
            }

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 112f;
            EditorGUILayout.PropertyField(
                chargeDuration,
                new GUIContent(
                    "Charge duration (s)",
                    "How many seconds this portal takes to become ready."),
                false,
                GUILayout.Width(190f));
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private void DrawPresetToolbar(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            bool canApply)
        {
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets =
                DimensionPortalPresetEditorUtility.GetPresets(template);
            EnsurePresetLabels(presets);

            int selectedPreset = FindPresetIndex(presets, profile);
            bool busy = presetActionQueued ||
                        EditorApplication.isCompiling ||
                        EditorApplication.isUpdating;
            EditorGUI.BeginDisabledGroup(busy);
            int nextPreset = Mathf.Max(0, selectedPreset);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginDisabledGroup(presets == null || presets.Count == 0);
            nextPreset = EditorGUILayout.Popup(
                Mathf.Max(0, selectedPreset),
                presetLabelCache,
                EditorStyles.toolbarPopup,
                GUILayout.MinWidth(100f),
                GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            Color previousBackground = GUI.backgroundColor;
            bool saveNeedsAttention =
                profileEditSession.HasChanges ||
                (profile == GetBoundProfile(template) &&
                 runtimeOutOfDateProfile == profile);
            if (saveNeedsAttention)
            {
                // Blue attention tint keeps the whole Portal Studio in one palette (orange now
                // signals the Tileset Studio) while still standing out from the grey neighbours.
                GUI.backgroundColor = new Color(0.26f, 0.67f, 0.95f, 1f);
            }

            if (GUILayout.Button(
                    new GUIContent(
                        "Save & Update",
                        profile == GetBoundProfile(template)
                            ? "Save this profile and update the portal generated for the mod."
                            : "Save this profile without changing the profile used by the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(104f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Save,
                    template,
                    profile);
            }
            GUI.backgroundColor = previousBackground;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(
                    new GUIContent(
                        "New profile",
                        "Create and open a new vanilla-based portal profile without using it in the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(88f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Create,
                    template,
                    null);
            }
            if (GUILayout.Button(
                    new GUIContent(
                        "Duplicate profile",
                        "Create and open a complete copy of this profile without using it in the mod."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(112f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Duplicate,
                    template,
                    profile);
            }
            GUILayout.FlexibleSpace();
            bool profileInUse = profile == GetBoundProfile(template);
            EditorGUI.BeginDisabledGroup(!canApply || profileInUse);
            if (GUILayout.Button(
                    new GUIContent(
                        profileInUse ? "Profile in use" : "Use profile",
                        profileInUse
                            ? "This is the profile currently used by the mod."
                            : "Save this profile, use it for the mod, and regenerate the portal output."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(94f)))
            {
                QueuePresetAction(
                    PendingPresetAction.Use,
                    template,
                    profile);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (presets != null &&
                nextPreset >= 0 &&
                nextPreset < presets.Count &&
                nextPreset != selectedPreset)
            {
                QueuePresetAction(
                    PendingPresetAction.Browse,
                    template,
                    presets[nextPreset]);
            }
        }

        private static string GetPresetDisplayName(
            DimensionPortalVisualProfileAsset profile)
        {
            if (profile == null)
            {
                return "Portal";
            }

            if (DimensionPortalPackageEditorUtility.TryGetPackage(
                    profile,
                    out DimensionPortalPackageAsset package,
                    out _) &&
                package != null &&
                !string.IsNullOrWhiteSpace(package.DisplayName))
            {
                return package.DisplayName.Trim();
            }

            return string.IsNullOrWhiteSpace(profile.name) ? "Portal" : profile.name;
        }

        private void EnsurePresetLabels(
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets)
        {
            if (ReferenceEquals(presetListCache, presets) &&
                presetLabelCache != null &&
                presetLabelCache.Length == (presets == null ? 0 : presets.Count))
            {
                return;
            }

            presetListCache = presets;
            int count = presets == null ? 0 : presets.Count;
            presetLabelCache = new string[count];
            for (int i = 0; i < count; i++)
            {
                DimensionPortalVisualProfileAsset preset = presets[i];
                presetLabelCache[i] = preset == null
                    ? "Missing portal"
                    : GetPresetDisplayName(preset);
            }
        }

        private static int FindPresetIndex(
            IReadOnlyList<DimensionPortalVisualProfileAsset> presets,
            DimensionPortalVisualProfileAsset profile)
        {
            if (presets == null)
            {
                return -1;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] == profile)
                {
                    return i;
                }
            }

            return -1;
        }

        private void QueuePresetAction(
            PendingPresetAction action,
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset target)
        {
            if (presetActionQueued || template == null || action == PendingPresetAction.None)
            {
                return;
            }

            pendingPresetAction = action;
            pendingPresetTemplate = template;
            pendingPresetTarget = target;
            pendingPresetBindingProfile = activeProfile;
            pendingPresetTemplateIdentity = CaptureAssetIdentity(template);
            pendingPresetTargetIdentity = CaptureAssetIdentity(target);
            pendingPresetBindingProfileIdentity = CaptureAssetIdentity(activeProfile);
            presetActionQueued = true;
            EditorApplication.delayCall -= ProcessPendingPresetAction;
            EditorApplication.delayCall += ProcessPendingPresetAction;
        }

        private void ProcessPendingPresetAction()
        {
            EditorApplication.delayCall -= ProcessPendingPresetAction;
            PendingPresetAction action = pendingPresetAction;
            DimensionTemplateAsset template = pendingPresetTemplate;
            DimensionPortalVisualProfileAsset target = pendingPresetTarget;
            DimensionPortalVisualProfileAsset bindingProfile =
                pendingPresetBindingProfile;
            AssetIdentity templateIdentity = pendingPresetTemplateIdentity;
            AssetIdentity targetIdentity = pendingPresetTargetIdentity;
            AssetIdentity bindingProfileIdentity =
                pendingPresetBindingProfileIdentity;
            presetActionQueued = false;
            pendingPresetAction = PendingPresetAction.None;
            pendingPresetTemplate = null;
            pendingPresetTarget = null;
            pendingPresetBindingProfile = null;
            pendingPresetTemplateIdentity = default(AssetIdentity);
            pendingPresetTargetIdentity = default(AssetIdentity);
            pendingPresetBindingProfileIdentity = default(AssetIdentity);
            if (disposed || template == null || action == PendingPresetAction.None)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        template,
                        templateIdentity,
                        bindingProfile,
                        bindingProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending portal preset action was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (target != null && !MatchesAssetIdentity(target, targetIdentity))
                {
                    pendingArtworkMessage =
                        "The pending portal preset action was cancelled because its target preset changed or was replaced.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                bool leavesEditingProfile =
                    action == PendingPresetAction.Browse ||
                    action == PendingPresetAction.Create ||
                    action == PendingPresetAction.Duplicate;
                if (leavesEditingProfile &&
                    !ResolvePendingProfileChangesBeforeLeave(out string transitionMessage))
                {
                    pendingArtworkMessage = transitionMessage;
                    pendingArtworkMessageType = MessageType.Error;
                    repaintRequested = true;
                    return;
                }

                bool succeeded;
                string message;
                DimensionPortalVisualProfileAsset created = null;
                switch (action)
                {
                    case PendingPresetAction.Save:
                        succeeded = SavePendingChanges(
                            out bool saveRuntimeUpdate,
                            out message);
                        if (succeeded && saveRuntimeUpdate)
                        {
                            runtimeSyncRequested = true;
                        }
                        break;
                    case PendingPresetAction.Duplicate:
                        succeeded = DimensionPortalPresetEditorUtility.DuplicateProfile(
                            template,
                            target,
                            out created,
                            out message);
                        if (succeeded)
                        {
                            succeeded = SelectEditingProfile(template, created);
                        }
                        break;
                    case PendingPresetAction.Create:
                        succeeded = DimensionPortalPresetEditorUtility.CreateVanilla(
                            template,
                            out created,
                            out message);
                        if (succeeded)
                        {
                            succeeded = SelectEditingProfile(template, created);
                        }
                        break;
                    case PendingPresetAction.Browse:
                        succeeded = SelectEditingProfile(template, target);
                        message = succeeded
                            ? "Opened portal profile '" +
                              GetPresetDisplayName(target) + "'."
                            : pendingArtworkMessage;
                        break;
                    case PendingPresetAction.Use:
                        succeeded = SavePendingChanges(
                            out _,
                            out message);
                        if (succeeded)
                        {
                            runtimeOutOfDateProfile = target;
                            useProfileRequested = target;
                        }
                        break;
                    default:
                        succeeded = false;
                        message = "Unknown Portal Studio preset action.";
                        break;
                }

                pendingArtworkMessage = message;
                pendingArtworkMessageType = succeeded
                    ? MessageType.Info
                    : MessageType.Error;
                if (succeeded)
                {
                    DimensionPortalPresetEditorUtility.Invalidate(template);
                    presetListCache = null;
                    presetLabelCache = Array.Empty<string>();
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    hasPaletteSnapshot = false;
                    InvalidateTextureSlotCache();
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                    activeTemplate = template;
                    activeProfile = editingProfile;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage = "Portal preset operation failed: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private bool ResolvePendingProfileChangesBeforeLeave(out string message)
        {
            message = string.Empty;
            if (!profileEditSession.HasChanges)
            {
                return true;
            }

            bool applyAndLeave = EditorUtility.DisplayDialog(
                "Unsaved Portal Studio changes",
                "Leaving '" + GetPresetDisplayName(editingProfile) +
                "' now will revert its unsaved changes.",
                "Apply and leave",
                "Leave without applying");
            if (applyAndLeave)
            {
                if (!SavePendingChanges(
                        out bool runtimeUpdateRequired,
                        out message))
                {
                    return false;
                }

                if (runtimeUpdateRequired)
                {
                    runtimeSyncRequested = true;
                }

                return true;
            }

            return DiscardPendingChanges(out message);
        }

        private void DrawChargingPreviewFooter(float viewportWidth)
        {
            EditorGUILayout.BeginHorizontal(
                EditorStyles.toolbar,
                GUILayout.Width(viewportWidth));

            string playbackTooltip = isPlaying
                ? "Pause portal preview"
                : "Play portal preview";
            GUIContent editorIcon = EditorGUIUtility.IconContent(
                isPlaying ? "PauseButton" : "PlayButton");
            GUIContent playbackContent = editorIcon != null && editorIcon.image != null
                ? new GUIContent(editorIcon.image, playbackTooltip)
                : new GUIContent(isPlaying ? "Ⅱ" : "▶", playbackTooltip);
            if (GUILayout.Button(
                    playbackContent,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(24f)))
            {
                isPlaying = !isPlaying;
                lastClockTime = EditorApplication.timeSinceStartup;
                nextRepaintTime = 0.0;
            }

            GUILayout.Space(4f);

            float previousProgress = chargeProgress;
            chargeProgress = GUILayout.HorizontalSlider(chargeProgress, 0f, 1f);
            if (!Mathf.Approximately(previousProgress, chargeProgress))
            {
                PausePreviewPlayback();
                previewCompositionDirty = true;
                repaintRequested = true;
            }

            EditorGUILayout.LabelField(
                Mathf.RoundToInt(chargeProgress * 100f) + "%",
                GUILayout.Width(38f));
            EditorGUILayout.EndHorizontal();
        }

        private void PausePreviewPlayback()
        {
            isPlaying = false;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
        }

        private void DrawPortalStateControls(float width)
        {
            // The instant portal preview is always in the activated phase; there is no
            // charging state to switch to, so the phase toolbar is omitted entirely.
            if (instantPortalMode)
            {
                return;
            }

            PreviewPhase previousPhase = previewPhase;
            previewPhase = (PreviewPhase)GUILayout.Toolbar(
                (int)previewPhase,
                PreviewPhaseLabels,
                EditorStyles.toolbarButton,
                GUILayout.Width(width));
            if (previewPhase != previousPhase)
            {
                pinnedLayerIndex = -1;
                activatedClock = 0f;
                skipCenterOpeningOnNextBuild = previewPhase == PreviewPhase.Activated;
                isPlaying = true;
                lastClockTime = EditorApplication.timeSinceStartup;
                nextRepaintTime = 0.0;
                previewCompositionDirty = true;

                hasSelectedPixel = false;
                ClearPaletteFocus();
            }
        }

        private void DrawReplayOpeningControl()
        {
            EditorGUI.BeginDisabledGroup(previewPhase != PreviewPhase.Activated);
            if (GUILayout.Button(
                    "Replay opening",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(94f)))
            {
                previewCenterClosingActive = false;
                activatedClock = 0f;
                skipCenterOpeningOnNextBuild = false;
                isPlaying = true;
                lastClockTime = EditorApplication.timeSinceStartup;
                nextRepaintTime = 0.0;
                previewCompositionDirty = true;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawReplayClosingControl()
        {
            // Closing exists only on the instant portal's three-animation center contract.
            if (!instantPortalMode)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(previewPhase != PreviewPhase.Activated);
            if (GUILayout.Button(
                    "Replay closing",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(94f)))
            {
                previewCenterClosingActive = true;
                activatedClock = 0f;
                skipCenterOpeningOnNextBuild = false;
                isPlaying = true;
                lastClockTime = EditorApplication.timeSinceStartup;
                nextRepaintTime = 0.0;
                previewCompositionDirty = true;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawViewControls()
        {
            showGrid = GUILayout.Toggle(
                showGrid,
                new GUIContent(
                    "Grid",
                    "Show the source-pixel grid over the portal preview."),
                EditorStyles.toolbarButton,
                GUILayout.Width(42f));
            showGuides = GUILayout.Toggle(
                showGuides,
                new GUIContent(
                    "Guides",
                    "Show the portal alignment guides."),
                EditorStyles.toolbarButton,
                GUILayout.Width(54f));
            DrawZoomControls();
        }

        private void DrawZoomControls()
        {
            using (new EditorGUI.DisabledScope(
                       previewZoomPercent <= MinPreviewZoomPercent))
            {
                if (GUILayout.Button(
                        new GUIContent(
                            "-",
                            "Zoom out by " + PreviewZoomStepPercent + "%"),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(22f)))
                {
                    AdjustPreviewZoom(-1);
                }
            }

            EditorGUILayout.LabelField(
                new GUIContent(
                    previewZoomPercent + "%",
                    "Portal preview zoom. Use the buttons or scroll over the preview."),
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(42f));

            using (new EditorGUI.DisabledScope(
                       previewZoomPercent >= MaxPreviewZoomPercent))
            {
                if (GUILayout.Button(
                        new GUIContent(
                            "+",
                            "Zoom in by " + PreviewZoomStepPercent + "%"),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(22f)))
                {
                    AdjustPreviewZoom(1);
                }
            }
        }

        private void AdjustPreviewZoom(int direction)
        {
            int nextZoom = Mathf.Clamp(
                previewZoomPercent + direction * PreviewZoomStepPercent,
                MinPreviewZoomPercent,
                MaxPreviewZoomPercent);
            if (nextZoom == previewZoomPercent)
            {
                return;
            }

            previewZoomPercent = nextZoom;
            previewZoomWasAdjusted = true;
            repaintRequested = true;
            GUI.changed = true;
        }

        private void HandlePreviewZoomScroll(Rect previewWindowRect)
        {
            Event current = Event.current;
            if (current == null ||
                current.type != EventType.ScrollWheel ||
                !previewWindowRect.Contains(current.mousePosition) ||
                Mathf.Approximately(current.delta.y, 0f) ||
                canvasDragPending ||
                GUIUtility.hotControl == canvasControlId)
            {
                return;
            }

            AdjustPreviewZoom(current.delta.y > 0f ? -1 : 1);
            current.Use();
        }

        private void DrawLayerNavigation()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
            EditorGUILayout.LabelField("LAYERS", EditorStyles.miniBoldLabel);
            if (!instantPortalMode)
            {
                DrawLayerButton(StudioLayer.Frame, "FRAME", "Base artwork");
                DrawLayerButton(StudioLayer.ChargeSweep, "CHARGE", "Continuous sweep");
                DrawLayerButton(StudioLayer.Milestones, "MILESTONES", "Persistent blobs");
            }

            DrawLayerButton(StudioLayer.Center, "CENTER", "Activated ring");
            DrawLayerButton(StudioLayer.InnerFlecks, "SWIRLS", "Inner motion");
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("IN-GAME", EditorStyles.miniBoldLabel);
            if (!instantPortalMode)
            {
                DrawLayerButton(StudioLayer.ReadyBurst, "EFFECTS", "Ready activation burst");
            }

            DrawLayerButton(StudioLayer.GroundLight, "LIGHT", "Ground light / shadow");
            EditorGUILayout.EndVertical();
        }

        private void DrawLayerButton(StudioLayer layer, string title, string subtitle)
        {
            bool selected = selectedLayer == layer;
            Rect rowRect = GUILayoutUtility.GetRect(
                SidebarWidth,
                42f,
                GUILayout.Width(SidebarWidth),
                GUILayout.Height(42f));
            Rect pinRect = new Rect(rowRect.xMax - 28f, rowRect.y, 28f, rowRect.height);
            Rect selectRect = new Rect(
                rowRect.x,
                rowRect.y,
                Mathf.Max(1f, rowRect.width - pinRect.width),
                rowRect.height);
            bool selectHovered = selectRect.Contains(Event.current.mousePosition);
            Color previousBackground = GUI.backgroundColor;
            if (selected)
            {
                GUI.backgroundColor = new Color(0.26f, 0.67f, 0.95f, 1f);
            }

            // GUIStyle.Draw is a repaint-only API. Calling it during layout, mouse-move,
            // or input events corrupts IMGUI's control bookkeeping and causes the portal
            // dashboard to spam GUILayout/GUIClip errors.
            if (Event.current.type == EventType.Repaint)
            {
                GetLayerButtonStyle().Draw(
                    rowRect,
                    new GUIContent("  <b>" + title + "</b>\n  " + subtitle),
                    selectHovered,
                    false,
                    false,
                    false);
            }
            GUI.backgroundColor = previousBackground;

            if (GUI.Button(selectRect, GUIContent.none, GUIStyle.none))
            {
                SelectLayer(layer);
            }

            DrawLayerPinControl(
                layer,
                pinRect,
                rowRect.Contains(Event.current.mousePosition));
        }

        private void DrawCompactLayerNavigation(bool twoRows)
        {
            EditorGUILayout.LabelField("LAYERS", EditorStyles.miniBoldLabel);
            if (instantPortalMode)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawLayerChip(StudioLayer.Center, "Center", "Activated center ring");
                DrawLayerChip(StudioLayer.InnerFlecks, "Swirls", "Persistent inner motion");
                EditorGUILayout.EndHorizontal();
            }
            else if (twoRows)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawLayerChip(StudioLayer.Frame, "Frame", "Base portal artwork");
                DrawLayerChip(StudioLayer.ChargeSweep, "Charge", "Continuous charging sweep");
                DrawLayerChip(StudioLayer.Milestones, "Milestones", "Persistent activation blobs");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawLayerChip(StudioLayer.Center, "Center", "Activated center ring");
                DrawLayerChip(StudioLayer.InnerFlecks, "Swirls", "Persistent inner motion");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                DrawLayerChip(StudioLayer.Frame, "Frame", "Base portal artwork");
                DrawLayerChip(StudioLayer.ChargeSweep, "Charge", "Continuous charging sweep");
                DrawLayerChip(StudioLayer.Milestones, "Milestones", "Persistent activation blobs");
                DrawLayerChip(StudioLayer.Center, "Center", "Activated center ring");
                DrawLayerChip(StudioLayer.InnerFlecks, "Swirls", "Persistent inner motion");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("IN-GAME", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (!instantPortalMode)
            {
                DrawLayerChip(StudioLayer.ReadyBurst, "Effects", "Ready activation burst");
            }

            DrawLayerChip(StudioLayer.GroundLight, "Light", "Projected light and shadows");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLayerChip(StudioLayer layer, string label, string tooltip)
        {
            bool selected = selectedLayer == layer;
            Rect rowRect = GUILayoutUtility.GetRect(
                66f,
                20f,
                GUILayout.MinWidth(66f),
                GUILayout.Height(20f),
                GUILayout.ExpandWidth(true));
            Rect pinRect = new Rect(rowRect.xMax - 20f, rowRect.y, 20f, rowRect.height);
            Rect selectRect = new Rect(
                rowRect.x,
                rowRect.y,
                Mathf.Max(1f, rowRect.width - pinRect.width),
                rowRect.height);
            bool selectHovered = selectRect.Contains(Event.current.mousePosition);
            Color previousBackground = GUI.backgroundColor;
            if (selected)
            {
                GUI.backgroundColor = new Color(0.26f, 0.67f, 0.95f, 1f);
            }

            if (Event.current.type == EventType.Repaint)
            {
                GetLayerChipStyle().Draw(
                    rowRect,
                    new GUIContent(label, tooltip),
                    selectHovered,
                    false,
                    false,
                    false);
            }
            GUI.backgroundColor = previousBackground;

            if (GUI.Button(selectRect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                SelectLayer(layer);
            }

            DrawLayerPinControl(
                layer,
                pinRect,
                rowRect.Contains(Event.current.mousePosition));
        }

        private void SelectLayer(StudioLayer layer)
        {
            if (selectedLayer != layer)
            {
                ClearPaletteFocus();
            }

            selectedLayer = layer;
            hasSelectedPixel = false;
            if (pinnedLayerIndex >= 0)
            {
                return;
            }

            ApplyNaturalPreviewPhase(layer);
        }

        private void DrawLayerPinControl(
            StudioLayer layer,
            Rect hitRect,
            bool layerHovered)
        {
            bool isPinned = pinnedLayerIndex == (int)layer;
            string tooltip = isPinned
                ? "Unpin this layer and resume automatic preview phase switching."
                : "Pin this layer's preview phase while editing other layers.";

            EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.Link);
            if (GUI.Button(hitRect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                ToggleLayerPin(layer);
                isPinned = pinnedLayerIndex == (int)layer;
                GUI.changed = true;
            }

            if (!isPinned && !layerHovered)
            {
                return;
            }

            Texture2D icon = GetLayerPinIcon(isPinned);
            if (icon == null)
            {
                return;
            }

            const float iconSize = 16f;
            Rect iconRect = new Rect(
                Mathf.Round(hitRect.center.x - iconSize * 0.5f),
                Mathf.Round(hitRect.center.y - iconSize * 0.5f),
                iconSize,
                iconSize);
            Color previousColor = GUI.color;
            GUI.color = isPinned
                ? new Color(0.2f, 0.88f, 1f, 1f)
                : new Color(0.92f, 0.95f, 1f, 0.9f);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
        }

        private void ToggleLayerPin(StudioLayer layer)
        {
            if (pinnedLayerIndex == (int)layer)
            {
                pinnedLayerIndex = -1;
                ApplyNaturalPreviewPhase(selectedLayer);
                return;
            }

            pinnedLayerIndex = (int)layer;
            ApplyNaturalPreviewPhase(layer);
        }

        private void ApplyNaturalPreviewPhase(StudioLayer layer)
        {
            if (layer == StudioLayer.ChargeSweep)
            {
                SetPreviewPhaseFromLayer(PreviewPhase.Charging);
            }
            else if (layer == StudioLayer.Center ||
                     layer == StudioLayer.InnerFlecks ||
                     layer == StudioLayer.ReadyBurst ||
                     layer == StudioLayer.GroundLight)
            {
                SetPreviewPhaseFromLayer(PreviewPhase.Activated);
            }
        }

        private void SetPreviewPhaseFromLayer(PreviewPhase phase)
        {
            previewCenterClosingActive = false;
            if (previewPhase == phase)
            {
                if (phase == PreviewPhase.Activated)
                {
                    // Hot reload can preserve an activated phase together with an old
                    // opening-frame clock. Selecting an activated layer is an explicit
                    // request to inspect its mature appearance; Replay opening remains
                    // the dedicated one-shot preview.
                    skipCenterOpeningOnNextBuild = true;
                }

                isPlaying = true;
                lastClockTime = EditorApplication.timeSinceStartup;
                nextRepaintTime = 0.0;
                previewCompositionDirty = true;
                return;
            }

            previewPhase = phase;
            activatedClock = 0f;
            skipCenterOpeningOnNextBuild = phase == PreviewPhase.Activated;
            isPlaying = true;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
            previewCompositionDirty = true;

            hasSelectedPixel = false;
            ClearPaletteFocus();
        }

        private void SetPaletteFocus(StudioLayer layer, int roleIndex)
        {
            paletteFocusActive = true;
            paletteFocusLayer = layer;
            paletteFocusRoleIndex = roleIndex;
            hasSelectedPixel = false;
            InvalidatePaletteFocusTexture();
        }

        private void ClearPaletteFocus()
        {
            paletteFocusActive = false;
            paletteFocusRoleIndex = -1;
            InvalidatePaletteFocusTexture();
        }

        private void InvalidatePaletteFocusTexture()
        {
            paletteFocusDisplaySheet = null;
            paletteFocusHitSheet = null;
            paletteFocusSourcePalette = null;
            paletteFocusTextureRole = -1;
        }

        private void DrawPreviewCanvas(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile,
            bool canApply)
        {
            float zoomedCanvasWidth = Mathf.Max(
                1f,
                activeViewBounds.width * activeZoom);
            float zoomedCanvasHeight = Mathf.Max(
                1f,
                activeViewBounds.height * activeZoom);
            float previewContentWidth = Mathf.Ceil(
                Mathf.Max(
                    PreviewToolbarMinimumWidth,
                    activeViewBounds.width *
                    (MaxPreviewZoomPercent / (float)PreviewZoomStepPercent)));
            float canvasHostHeight = Mathf.Ceil(
                activeViewBounds.height *
                (MaxPreviewZoomPercent / (float)PreviewZoomStepPercent));
            float previewWindowWidth = previewContentWidth;

            EditorGUILayout.BeginVertical(GUILayout.Width(previewWindowWidth));
            Rect previewWindowRect = EditorGUILayout.BeginVertical(
                GUILayout.Width(previewWindowWidth));
            DrawPreviewToolbar(previewContentWidth);
            Rect canvasHostRect = GUILayoutUtility.GetRect(
                previewContentWidth,
                canvasHostHeight,
                GUILayout.Width(previewContentWidth),
                GUILayout.Height(canvasHostHeight));
            Rect canvasRect = new Rect(
                canvasHostRect.x + (canvasHostRect.width - zoomedCanvasWidth) * 0.5f,
                canvasHostRect.y + (canvasHostRect.height - zoomedCanvasHeight) * 0.5f,
                zoomedCanvasWidth,
                zoomedCanvasHeight);
            canvasControlId = GUIUtility.GetControlID(
                CanvasControlHint,
                FocusType.Keyboard,
                canvasRect);
            if (IsTransformableLayer(selectedLayer))
            {
                EditorGUIUtility.AddCursorRect(canvasRect, MouseCursor.MoveArrow);
            }
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    canvasHostRect,
                    new Color(0.035f, 0.04f, 0.05f, 1f));
                DrawCanvasBackground(canvasRect);

                GUI.BeginGroup(canvasRect);
                Rect localCanvas = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
                for (int i = 0; i < visibleFrames.Count; i++)
                {
                    DrawVisibleFrame(localCanvas, visibleFrames[i]);
                }

                DrawFleckPreview(localCanvas);

                DrawPaletteFocusOverlay(localCanvas);

                if (showGuides)
                {
                    DrawAlignmentGuides(localCanvas);
                }

                DrawSelectedLayerOutline();
                if (showGrid && activeZoom >= 4f)
                {
                    DrawPixelGrid(localCanvas);
                }

                DrawSelectedPixel();
                GUI.EndGroup();
            }

            HandleCanvasInteraction(canvasRect, serializedProfile, canvasControlId);
            if (previewPhase == PreviewPhase.Charging)
            {
                DrawChargingPreviewFooter(previewContentWidth);
            }

            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                DrawOutline(
                    previewWindowRect,
                    new Color(0.38f, 0.41f, 0.47f, 1f),
                    1f);
            }
            HandlePreviewZoomScroll(previewWindowRect);

            GUILayout.Space(5f);
            EditorGUILayout.BeginVertical(GUILayout.Width(previewWindowWidth));
            DrawPresetToolbar(template, profile, canApply);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewToolbar(float viewportWidth)
        {
            bool compact = viewportWidth < 470f;
            if (compact)
            {
                if (!instantPortalMode)
                {
                    EditorGUILayout.BeginHorizontal(
                        EditorStyles.toolbar,
                        GUILayout.Width(viewportWidth));
                    DrawPortalStateControls(Mathf.Max(1f, viewportWidth));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal(
                    EditorStyles.toolbar,
                    GUILayout.Width(viewportWidth));
                DrawReplayOpeningControl();
                DrawReplayClosingControl();
                GUILayout.FlexibleSpace();
                DrawViewControls();
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal(
                EditorStyles.toolbar,
                GUILayout.Width(viewportWidth));
            DrawPortalStateControls(170f);
            DrawReplayOpeningControl();
            DrawReplayClosingControl();
            GUILayout.FlexibleSpace();
            DrawViewControls();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvasBackground(Rect canvasRect)
        {
            GUI.BeginGroup(canvasRect);
            Rect clippedCanvas = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
            Color baseColor = new Color(0.055f, 0.06f, 0.075f, 1f);
            EditorGUI.DrawRect(clippedCanvas, baseColor);
            float checkerSize = activeZoom * 4f;
            int columns = Mathf.CeilToInt(clippedCanvas.width / checkerSize);
            int rows = Mathf.CeilToInt(clippedCanvas.height / checkerSize);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (((x + y) & 1) == 0)
                    {
                        continue;
                    }

                    float tileX = x * checkerSize;
                    float tileY = y * checkerSize;
                    float tileWidth = Mathf.Min(checkerSize, clippedCanvas.xMax - tileX);
                    float tileHeight = Mathf.Min(checkerSize, clippedCanvas.yMax - tileY);
                    if (tileWidth <= 0f || tileHeight <= 0f)
                    {
                        continue;
                    }

                    EditorGUI.DrawRect(
                        new Rect(
                            tileX,
                            tileY,
                            tileWidth,
                            tileHeight),
                        new Color(0.085f, 0.09f, 0.11f, 1f));
                }
            }

            DrawOutline(clippedCanvas, new Color(0.36f, 0.39f, 0.45f, 1f), 1f);
            GUI.EndGroup();
        }

        private void DrawAlignmentGuides(Rect localCanvas)
        {
            float thickness = Mathf.Max(0.5f, 1f / EditorGUIUtility.pixelsPerPoint);
            float axisCanvasX = -bodyScreenOrigin.x;
            float axisGuiX = (axisCanvasX - activeViewBounds.xMin) * activeZoom;
            EditorGUI.DrawRect(
                new Rect(axisGuiX, 0f, thickness, localCanvas.height),
                new Color(0.2f, 0.78f, 1f, 0.22f));

            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame frame = visibleFrames[i];
                if (frame.Layer != selectedLayer)
                {
                    continue;
                }

                Vector2 pivotCanvas = new Vector2(
                    frame.CanvasRect.x + frame.Pivot.x * frame.CanvasRect.width,
                    frame.CanvasRect.y + frame.Pivot.y * frame.CanvasRect.height);
                Vector2 pivotGui = CanvasPointToGuiPoint(pivotCanvas);
                Color pivotColor = new Color(1f, 0.74f, 0.22f, 0.95f);
                EditorGUI.DrawRect(
                    new Rect(pivotGui.x - 6f, pivotGui.y, 12f, thickness),
                    pivotColor);
                EditorGUI.DrawRect(
                    new Rect(pivotGui.x, pivotGui.y - 6f, thickness, 12f),
                    pivotColor);
                return;
            }
        }

        private Vector2 CanvasPointToGuiPoint(Vector2 canvasPoint)
        {
            return new Vector2(
                (canvasPoint.x - activeViewBounds.xMin) * activeZoom,
                (activeViewBounds.yMax - canvasPoint.y) * activeZoom);
        }

        private bool IsCenterOpening()
        {
            return previewPhase == PreviewPhase.Activated && currentCenterIsOpening;
        }

        private void DrawPreviewIssues()
        {
            if (previewErrors.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    string.Join("\n", previewErrors.ToArray()),
                    MessageType.Error);
            }

            if (previewWarnings.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    string.Join("\n", previewWarnings.ToArray()),
                    MessageType.Warning);
            }
        }

        private static string FormatRect(Rect value)
        {
            return "(" + value.x.ToString("0.###") + ", " +
                   value.y.ToString("0.###") + ", " +
                   value.width.ToString("0.###") + ", " +
                   value.height.ToString("0.###") + ")";
        }

        private void EnsurePreviewComposition(SerializedObject profile)
        {
            DimensionPortalVisualProfileAsset target = profile == null
                ? null
                : profile.targetObject as DimensionPortalVisualProfileAsset;
            int targetDirtyCount = target == null
                ? -1
                : EditorUtility.GetDirtyCount(target);
            if (!hasPreviewComposition ||
                previewCompositionProfile != target ||
                previewCompositionProfileDirtyCount != targetDirtyCount)
            {
                previewCompositionDirty = true;
            }

            if (!previewCompositionDirty)
            {
                return;
            }

            Event current = Event.current;
            if (current != null && current.type != EventType.Layout)
            {
                // One immutable composition is shared by the Layout/Repaint pair. Input
                // events merely mark it dirty; the next Layout samples serialized state
                // and animation clocks once, preventing expensive asset inspection from
                // running for every mouse/key/repaint event.
                return;
            }

            BuildVisibleFrames(profile);
            hasPreviewComposition = true;
            previewCompositionProfile = target;
            previewCompositionProfileDirtyCount = target == null
                ? -1
                : EditorUtility.GetDirtyCount(target);
            previewCompositionDirty = false;
        }

        private void BuildVisibleFrames(SerializedObject profile)
        {
            visibleFrames.Clear();
            previewWarnings.Clear();
            previewErrors.Clear();

            SerializedProperty frameOverride = profile.FindProperty("portalFrameSpriteAsset");
            DimensionPortalArtworkReferenceKind frameReferenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                frameOverride,
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.Frame,
                out SpriteAsset frameAsset);
            bool hasCustomFrameArtwork =
                frameReferenceKind == DimensionPortalArtworkReferenceKind.Managed ||
                frameReferenceKind == DimensionPortalArtworkReferenceKind.External;
            if (frameReferenceKind == DimensionPortalArtworkReferenceKind.Unresolved)
            {
                AddPreviewError("Frame override address could not be resolved in Scriptable Data.");
            }

            SpriteAsset effectiveFrameAsset = hasCustomFrameArtwork && frameAsset != null
                ? frameAsset
                : DimensionPortalArtworkEditorUtility.GetFrameworkAsset(
                    DimensionPortalArtworkLayer.Frame);
            SpriteData frameData = effectiveFrameAsset == null
                ? null
                : effectiveFrameAsset.staticSpriteData;
            Texture2D frameTexture = frameData == null ? null : frameData.texture;
            Texture2D frameEmissiveTexture = frameData == null
                ? null
                : frameData.emissiveTexture;
            if (TryGetPendingTextureSlot(
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.Frame,
                    0,
                    out Texture2D pendingFrameTexture,
                    out Texture2D pendingFrameEmissiveTexture))
            {
                frameTexture = pendingFrameTexture;
                frameEmissiveTexture = pendingFrameEmissiveTexture;
            }

            if (hasCustomFrameArtwork && frameTexture == null)
            {
                AddPreviewError("Frame override must contain a static portal-frame texture.");
            }

            PreviewSheet frameSheet = frameTexture != null
                ? GetSourceSheet(frameTexture, 1)
                : GetSourceSheet(FrameTexturePath, 1);
            PreviewSheet frameDisplaySheet = GetFrameCompositeSheet(
                frameTexture,
                frameEmissiveTexture,
                GetColor(profile, "frameEmissiveColor", Color.clear));
            if (frameDisplaySheet == null)
            {
                frameDisplaySheet = frameSheet;
            }
            bodyPivot = frameData != null
                ? frameData.pivot
                : new Vector2(0.5f, 0.5f);
            canvasPixelWidth = frameSheet == null
                ? CanonicalCanvasPixels
                : Mathf.Max(1, frameSheet.FrameWidth);
            canvasPixelHeight = frameSheet == null
                ? CanonicalCanvasPixels
                : Mathf.Max(1, frameSheet.Height);
            bodyScreenOrigin = DimensionPortalVisualContract.GetBodyScreenOrigin(
                new Vector2Int(canvasPixelWidth, canvasPixelHeight),
                bodyPivot);
            if (canvasPixelWidth != CanonicalCanvasPixels ||
                canvasPixelHeight != CanonicalCanvasPixels)
            {
                AddPreviewWarning(
                    "Frame override is " + canvasPixelWidth + " x " + canvasPixelHeight +
                    "; vanilla portal overlays are authored for 48 x 48. The preview keeps native size and pivot instead of stretching it.");
            }

            bool customSwirlPreview =
                previewPhase != PreviewPhase.Charging &&
                !previewCenterClosingActive &&
                GetBool(profile, "centerSwirlVisible", true) &&
                GetBool(profile, "centerSwirlOverrideVanilla", false);
            if (customSwirlPreview)
            {
                // Custom swirl artwork belongs behind the physical portal and its center
                // ring. Drawing it first lets those opaque pixels form the natural inner
                // aperture instead of allowing the swirl to paint across the frame.
                BuildFleckPreview(profile);
            }

            if (GetBool(profile, "frameVisible", true))
            {
                Vector2 frameOffset = GetVector2(
                    profile,
                    "frameOffsetPixels",
                    Vector2.zero);
                AddVisibleFrame(
                    StudioLayer.Frame,
                    frameDisplaySheet,
                    frameSheet,
                    new Rect(
                        frameOffset.x,
                        frameOffset.y,
                        canvasPixelWidth,
                        canvasPixelHeight),
                    0,
                    GetColor(profile, "frameTint", Color.white),
                    null,
                    false,
                    bodyPivot,
                    GetVector2(profile, "frameScale", Vector2.one),
                    GetFloat(profile, "frameRotationDegrees", 0f),
                    GetBool(profile, "frameFlipX", false),
                    GetBool(profile, "frameFlipY", false),
                    hasCustomFrameArtwork
                        ? frameAsset == null ? "Unresolved override" : frameAsset.name
                        : "Framework vanilla frame",
                    frameReferenceKind == DimensionPortalArtworkReferenceKind.External);
            }

            if (previewPhase == PreviewPhase.Charging)
            {
                AddChargingFrame(profile);
                AddMilestoneFrame(profile, false);
            }
            else
            {
                AddMilestoneFrame(profile, true);
                AddCenterFrame(profile);
                // The in-game close hides the swirls the moment it starts, so the closing
                // replay does too. AddCenterFrame clears the flag when the one-shot ends,
                // which lets the flecks return on the same repaint.
                if (!customSwirlPreview && !previewCenterClosingActive)
                {
                    BuildFleckPreview(profile);
                }
            }
        }

        private void BuildFleckPreview(SerializedObject profile)
        {
            fleckPreviewState = default(FleckPreviewState);
            if (profile == null)
            {
                return;
            }

            if (!GetBool(profile, "centerSwirlVisible", true))
            {
                return;
            }

            if (GetBool(profile, "centerSwirlOverrideVanilla", false))
            {
                BuildCustomSwirlPreview(profile);
                return;
            }

            fleckPreviewState = new FleckPreviewState
            {
                Visible = true,
                Origin = new Vector2(
                    CanonicalCanvasPixels * 0.5f,
                    2f + DimensionPortalVisualContract.CanonicalCenterHeight * 0.5f),
                Scale = Vector2.one,
                EmissionMultiplier = 1f,
                SizeMultiplier = 1f,
                RadiusMultiplier = 1f
            };
        }

        private void BuildCustomSwirlPreview(SerializedObject profile)
        {
            if (!TryResolveCustomSwirlAsset(
                    profile,
                    out SpriteAsset swirlAsset,
                    out string resolveError))
            {
                AddPreviewError(resolveError);
                return;
            }

            if (!TryValidateCustomSwirlAsset(swirlAsset, out string validationError))
            {
                AddPreviewError(validationError);
                return;
            }

            AnimationSheet animation = GetAnimationSheet(swirlAsset, 0, 1, 10f);
            if (animation.Sheet == null || animation.Sheet.Texture == null)
            {
                // GetAnimationSheet records the specific SpriteAsset error.
                return;
            }

            float playbackSpeed = Mathf.Max(
                0.01f,
                GetFloat(profile, "centerSwirlPlaybackSpeed", 1f));
            int frame = GetAnimationFrame(animation, activatedClock * playbackSpeed);
            Vector2 offset = GetVector2(
                profile,
                "centerParticleOffsetPixels",
                Vector2.zero);
            FrameAnimation sourceAnimation = swirlAsset.GetAnimationAt(0);
            Texture2D albedoTexture = sourceAnimation == null || sourceAnimation.spriteData == null
                ? null
                : sourceAnimation.spriteData.texture;
            Texture2D emissiveTexture = sourceAnimation == null || sourceAnimation.spriteData == null
                ? null
                : sourceAnimation.spriteData.emissiveTexture;
            Color previewEmission = GetColor(
                profile,
                "centerSwirlEmissiveColor",
                Color.white);
            // A profile-owned swirl bakes the color into its pixels, so preview it neutral.
            // The shared white framework starter is still tinted live so a color drag reads
            // immediately, before the debounced bake materializes the owned asset.
            Color previewTint =
                DimensionPortalSwirlArtworkEditorUtility.IsFrameworkAsset(swirlAsset)
                    ? GetColor(profile, "centerParticleTint", Color.white)
                    : Color.white;
            float emissionMultiplier = Mathf.Max(
                0f,
                GetFloat(profile, "centerParticleEmissionMultiplier", 1f));
            previewEmission.r *= emissionMultiplier;
            previewEmission.g *= emissionMultiplier;
            previewEmission.b *= emissionMultiplier;
            Color compositeEmission = new Color(
                previewEmission.r * previewTint.r,
                previewEmission.g * previewTint.g,
                previewEmission.b * previewTint.b,
                1f);
            PreviewSheet display = GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                previewTint,
                compositeEmission,
                animation.Sheet.FrameCount);
            if (display == null)
            {
                display = animation.Sheet;
            }

            // The runtime multiplies the untinted swirl pixels by CenterParticleTint and
            // adds the emissive at draw time. When GetFrameCompositeSheet has already
            // composited that tint into the preview sheet, draw it neutral; otherwise apply
            // the tint here so the Studio preview matches the in-game material tint exactly.
            Color drawTint = ReferenceEquals(display, animation.Sheet)
                ? previewTint
                : Color.white;

            // Centre the full-canvas swirl sheet on the aperture (the same anchor the vanilla
            // fleck preview and the runtime SpriteObject use), not the outer 48x48 frame
            // centre, so its flecks land inside the inner circle by default.
            AddVisibleFrame(
                StudioLayer.InnerFlecks,
                display,
                animation.Sheet,
                new Rect(
                    offset.x,
                    offset.y + 2f +
                        DimensionPortalVisualContract.CanonicalCenterHeight * 0.5f -
                        CanonicalCanvasPixels * 0.5f,
                    CanonicalCanvasPixels,
                    CanonicalCanvasPixels),
                frame,
                drawTint,
                null,
                false,
                animation.Pivot,
                GetVector2(
                    profile,
                    "centerParticleScale",
                    Vector2.one),
                GetFloat(profile, "centerParticleRotationDegrees", 0f),
                GetBool(profile, "centerSwirlFlipX", false),
                GetBool(profile, "centerSwirlFlipY", false),
                animation.SourceLabel,
                true);
        }

        private static bool TryResolveCustomSwirlAsset(
            SerializedObject profile,
            out SpriteAsset asset,
            out string error)
        {
            asset = null;
            error = string.Empty;
            DimensionPortalVisualProfileAsset visualProfile = profile == null
                ? null
                : profile.targetObject as DimensionPortalVisualProfileAsset;
            if (visualProfile == null)
            {
                error = "The active portal profile is unavailable.";
                return false;
            }

            SerializedProperty reference = profile.FindProperty("centerSwirlSpriteAsset");
            if (reference == null)
            {
                error =
                    "The active portal profile does not expose an Artwork override for Swirls.";
                return false;
            }

            DimensionPortalPackageEditorUtility.TryGetPackage(
                visualProfile,
                out _,
                out string packageFolder);
            if (!DimensionPortalSwirlArtworkEditorUtility.TryResolveReference(
                    visualProfile,
                    reference,
                    packageFolder,
                    out asset))
            {
                error =
                    "Override vanilla is enabled, but the Artwork override address could not " +
                    "be resolved in Scriptable Data.";
                return false;
            }

            if (asset == null)
            {
                error =
                    "Override vanilla is enabled, but Artwork override has no SpriteAsset. " +
                    "Select a looping 48 x 48 SpriteAsset before applying this profile.";
                return false;
            }

            return true;
        }

        private static bool TryValidateCustomSwirlAsset(
            SpriteAsset asset,
            out string error)
        {
            error = string.Empty;
            if (asset == null || asset.animationCount <= 0)
            {
                error = "Swirl Artwork override must contain animation 0.";
                return false;
            }

            FrameAnimation animation = asset.GetAnimationAt(0);
            if (animation == null || animation.spriteData == null)
            {
                error = "Swirl Artwork override animation 0 has no SpriteData.";
                return false;
            }

            Texture2D texture = animation.spriteData.GetSrcTexture();
            if (texture == null)
            {
                error = "Swirl Artwork override animation 0 has no source texture.";
                return false;
            }

            int frameCount = animation.srcFrameCount;
            if (frameCount <= 0)
            {
                error = "Swirl Artwork override animation 0 has no source frames.";
                return false;
            }

            if (texture.width % frameCount != 0)
            {
                error =
                    "Swirl Artwork override sheet width " + texture.width +
                    " is not divisible by its " + frameCount + " frames.";
                return false;
            }

            int frameWidth = texture.width / frameCount;
            if (frameWidth <= 0 ||
                frameWidth > CanonicalCanvasPixels ||
                texture.height > CanonicalCanvasPixels)
            {
                error =
                    "Swirl Artwork override animation 0 frames must fit within the 48 x 48 " +
                    "portal canvas. Current frames are " + frameWidth + " x " +
                    texture.height + ".";
                return false;
            }

            if (!animation.loop)
            {
                error = "Swirl Artwork override animation 0 must be configured to loop.";
                return false;
            }

            return true;
        }

        private void AddChargingFrame(SerializedObject profile)
        {
            if (!GetBool(profile, "chargeWaveVisible", true))
            {
                return;
            }

            SerializedProperty overrideProperty = profile.FindProperty("chargeWaveSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.ChargeSweep,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.ChargeSweep,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Charge sweep override address could not be resolved in Scriptable Data.");
            }

            AnimationSheet animation = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(ChargeTexturePath, 42),
                    FrameCount = 42,
                    Fps = 27.3f,
                    Loop = true,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla charging sweep"
                }
                : GetAnimationSheet(overrideAsset, 0, 42, 27.3f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                display = GetRecoloredSheet(
                    "charge",
                    animation.Sheet,
                    EffectSourcePalette,
                    GetPalette(profile, ChargePaletteProperties),
                    GetColor(
                        profile,
                        "chargeWaveEmissiveColor",
                        DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor));
            }

            ValidateLayerSheet(
                StudioLayer.ChargeSweep,
                animation,
                CanonicalCanvasPixels,
                CanonicalCanvasPixels,
                "Charge sweep");
            float speed = Mathf.Max(0.01f, GetFloat(profile, "chargeWaveSpeed", 1f));
            int frame = GetAnimationFrame(animation, animationClock * speed);
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.ChargeSweep,
                animation,
                GetVector2(profile, "chargeWaveOffsetPixels", Vector2.zero));
            AddVisibleFrame(
                StudioLayer.ChargeSweep,
                display,
                animation.Sheet,
                layerRect,
                frame,
                GetColor(profile, "chargeWaveTint", Color.white),
                EffectSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "chargeWaveScale", Vector2.one),
                GetFloat(profile, "chargeWaveRotationDegrees", 0f),
                GetBool(profile, "chargeWaveFlipX", false),
                GetBool(profile, "chargeWaveFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddMilestoneFrame(SerializedObject profile, bool ready)
        {
            if (!GetBool(profile, "milestonesVisible", true))
            {
                return;
            }

            // Stage -> frame follows the fixed vanilla sheet order (empty 0, bottom 1,
            // middle 3, upper 4, ready 7) — matching the runtime visual. Frame zero is
            // transparent in the vanilla sheet, so the pre-threshold state draws nothing
            // unless custom artwork fills it in.
            int frame;
            if (ready)
            {
                frame = 7;
            }
            else
            {
                float first = Mathf.Clamp01(GetFloat(profile, "firstMilestone", 0.25f));
                float second = Mathf.Clamp(
                    GetFloat(profile, "secondMilestone", 0.5f),
                    first,
                    1f);
                float third = Mathf.Clamp(
                    GetFloat(profile, "thirdMilestone", 0.75f),
                    second,
                    1f);
                if (chargeProgress >= third)
                {
                    frame = 4;
                }
                else if (chargeProgress >= second)
                {
                    frame = 3;
                }
                else if (chargeProgress >= first)
                {
                    frame = 1;
                }
                else
                {
                    frame = 0;
                }
            }

            SerializedProperty overrideProperty = profile.FindProperty("milestoneSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    DimensionPortalArtworkLayer.Milestones,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                DimensionPortalArtworkLayer.Milestones,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Milestone override address could not be resolved in Scriptable Data.");
            }

            AnimationSheet animation = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(MilestoneTexturePath, 8),
                    FrameCount = 8,
                    Fps = 1f,
                    Loop = true,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla milestone states"
                }
                : GetAnimationSheet(overrideAsset, 0, 8, 1f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                display = GetRecoloredSheet(
                    "milestones",
                    animation.Sheet,
                    EffectSourcePalette,
                    GetPalette(profile, MilestonePaletteProperties),
                    GetColor(
                        profile,
                        "milestoneEmissiveColor",
                        DimensionPortalVisualProfileAsset.VanillaLoadPointEmissiveColor));
            }

            ValidateLayerSheet(
                StudioLayer.Milestones,
                animation,
                CanonicalCanvasPixels,
                CanonicalCanvasPixels,
                "Milestones");
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.Milestones,
                animation,
                GetVector2(profile, "milestoneOffsetPixels", Vector2.zero));
            AddVisibleFrame(
                StudioLayer.Milestones,
                display,
                animation.Sheet,
                layerRect,
                Mathf.Clamp(frame, 0, Mathf.Max(0, animation.FrameCount - 1)),
                GetColor(profile, "milestoneTint", Color.white),
                EffectSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "milestoneScale", Vector2.one),
                GetFloat(profile, "milestoneRotationDegrees", 0f),
                GetBool(profile, "milestoneFlipX", false),
                GetBool(profile, "milestoneFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddCenterFrame(SerializedObject profile)
        {
            if (!GetBool(profile, "centerVisible", true))
            {
                return;
            }

            SerializedProperty overrideProperty = profile.FindProperty("centerEffectSpriteAsset");
            DimensionPortalArtworkReferenceKind referenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    overrideProperty,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    CenterArtworkLayer,
                    out SpriteAsset overrideAsset);
            bool hasOverrideAddress = referenceKind !=
                                      DimensionPortalArtworkReferenceKind.Empty;
            bool hasDirectOverride = IsDirectTextureReference(
                profile.targetObject as DimensionPortalVisualProfileAsset,
                CenterArtworkLayer,
                referenceKind,
                overrideAsset,
                overrideProperty);
            if (hasOverrideAddress && overrideAsset == null)
            {
                AddPreviewError("Center override address could not be resolved in Scriptable Data.");
            }

            const int openingIndex = 1;
            const int idleIndex = 0;
            string fallbackOpeningPath = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantOpenTexturePath
                : CenterOpeningTexturePath;
            string fallbackIdlePath = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantIdleTexturePath
                : CenterIdleTexturePath;
            int fallbackOpeningFrames = instantPortalMode
                ? DimensionPortalInstantArtworkEditorUtility.InstantFrameCount
                : CenterOpeningFrameCountVanilla;

            AnimationSheet opening = !hasDirectOverride
                ? new AnimationSheet
                {
                    Sheet = GetSourceSheet(fallbackOpeningPath, fallbackOpeningFrames),
                    FrameCount = fallbackOpeningFrames,
                    Fps = 10f,
                    Loop = false,
                    Pivot = new Vector2(0.5f, 0.5f),
                    SourceLabel = "Framework vanilla center opening"
                }
                : GetAnimationSheet(overrideAsset, openingIndex, fallbackOpeningFrames, 10f);
            float openingDuration = GetAnimationDuration(opening, 0.4f);
            if (skipCenterOpeningOnNextBuild)
            {
                // Selecting an activated layer should present the useful mature loop,
                // even when preview playback is paused. The explicit Replay opening
                // control remains the way to inspect the one-shot formation sequence.
                activatedClock = Mathf.Max(activatedClock, openingDuration);
                skipCenterOpeningOnNextBuild = false;
            }

            // The one-shot closing replay (instant portal only): the closing sheet runs on a
            // fresh clock, then the preview returns to the mature loop. The clock is clamped
            // past the opening on exit so the opening cannot restart afterwards.
            bool isClosing = false;
            AnimationSheet closing = default(AnimationSheet);
            if (previewCenterClosingActive && instantPortalMode)
            {
                closing = !hasDirectOverride
                    ? new AnimationSheet
                    {
                        Sheet = GetSourceSheet(
                            DimensionPortalInstantArtworkEditorUtility.InstantCloseTexturePath,
                            DimensionPortalInstantArtworkEditorUtility.InstantFrameCount),
                        FrameCount = DimensionPortalInstantArtworkEditorUtility.InstantFrameCount,
                        Fps = 10f,
                        Loop = false,
                        Pivot = new Vector2(0.5f, 0.5f),
                        SourceLabel = "Framework instant center closing"
                    }
                    : GetAnimationSheet(
                        overrideAsset,
                        DimensionPortalInstantArtworkEditorUtility.InstantClosingAnimationIndex,
                        DimensionPortalInstantArtworkEditorUtility.InstantFrameCount,
                        10f);
                float closingDuration = GetAnimationDuration(closing, 0.5f);
                if (closing.Sheet != null && activatedClock < closingDuration)
                {
                    isClosing = true;
                }
                else
                {
                    previewCenterClosingActive = false;
                    activatedClock = Mathf.Max(activatedClock, openingDuration);
                }
            }

            bool isOpening = !isClosing && opening.Sheet != null && activatedClock < openingDuration;
            AnimationSheet animation = isClosing
                ? closing
                : isOpening
                    ? opening
                    : !hasDirectOverride
                        ? new AnimationSheet
                        {
                            Sheet = GetSourceSheet(fallbackIdlePath, 5),
                            FrameCount = 5,
                            Fps = 10f,
                            Loop = true,
                            Pivot = new Vector2(0.5f, 0.5f),
                            SourceLabel = "Framework vanilla center idle"
                        }
                        : GetAnimationSheet(overrideAsset, idleIndex, 5, 10f);
            if (animation.Sheet == null)
            {
                return;
            }

            PreviewSheet display = animation.Sheet;
            if (!hasDirectOverride)
            {
                display = GetRecoloredSheet(
                    isClosing ? "center-closing" : isOpening ? "center-opening" : "center-idle",
                    animation.Sheet,
                    CenterSourcePalette,
                    GetPalette(profile, CenterPaletteProperties));
            }

            currentCenterOpeningDuration = openingDuration;
            currentCenterIsOpening = isOpening;
            ValidateLayerSheet(
                StudioLayer.Center,
                animation,
                DimensionPortalVisualContract.CanonicalCenterWidth,
                DimensionPortalVisualContract.CanonicalCenterHeight,
                isClosing ? "Center closing" : isOpening ? "Center opening" : "Center idle",
                true);
            float localClock = isOpening || isClosing
                ? activatedClock
                : Mathf.Max(0f, activatedClock - openingDuration);
            int frame = GetAnimationFrame(animation, localClock);
            Vector2 centerOffset = GetVector2(profile, "centerOffsetPixels", Vector2.zero);
            Rect layerRect = ResolveLayerRect(
                DimensionPortalVisualContract.Layer.Center,
                animation,
                centerOffset);
            AddVisibleFrame(
                StudioLayer.Center,
                display,
                animation.Sheet,
                layerRect,
                frame,
                GetColor(profile, "centerTint", Color.white),
                CenterSourcePalette,
                !hasDirectOverride,
                animation.Pivot,
                GetVector2(profile, "centerScale", Vector2.one),
                GetFloat(profile, "centerRotationDegrees", 0f),
                GetBool(profile, "centerFlipX", false),
                GetBool(profile, "centerFlipY", false),
                animation.SourceLabel,
                hasDirectOverride);
        }

        private void AddVisibleFrame(
            StudioLayer layer,
            PreviewSheet displaySheet,
            PreviewSheet hitSheet,
            Rect canvasRect,
            int frameIndex,
            Color tint,
            Color32[] sourcePalette,
            bool palettePickingEnabled,
            Vector2 pivot,
            Vector2 scale,
            float rotationDegrees,
            bool flipX,
            bool flipY,
            string sourceLabel,
            bool directOverride)
        {
            if (displaySheet == null || displaySheet.Texture == null)
            {
                return;
            }

            VisibleFrame visibleFrame = new VisibleFrame
            {
                Layer = layer,
                DisplaySheet = displaySheet,
                HitSheet = hitSheet,
                CanvasRect = canvasRect,
                FrameIndex = frameIndex,
                Tint = tint,
                SourcePalette = sourcePalette,
                PalettePickingEnabled = palettePickingEnabled,
                Pivot = pivot,
                Scale = ClampPreviewScale(scale),
                RotationDegrees = NormalizePreviewRotation(rotationDegrees),
                FlipX = flipX,
                FlipY = flipY,
                SourceLabel = sourceLabel ?? string.Empty,
                DirectOverride = directOverride
            };
            visibleFrames.Add(visibleFrame);
            ValidateTransformedFrameBounds(visibleFrame);
        }

        private Rect ResolveLayerRect(
            DimensionPortalVisualContract.Layer layer,
            AnimationSheet animation,
            Vector2 centerOffsetPixels)
        {
            Vector2Int bodySize = new Vector2Int(canvasPixelWidth, canvasPixelHeight);
            Vector2Int layerSize = new Vector2Int(
                animation.Sheet == null ? 1 : Mathf.Max(1, animation.Sheet.FrameWidth),
                animation.Sheet == null ? 1 : Mathf.Max(1, animation.Sheet.Height));
            Rect rect = DimensionPortalVisualContract.ResolveBodyLocalRect(
                bodySize,
                bodyPivot,
                layerSize,
                animation.Pivot,
                DimensionPortalVisualContract.GetLocalPosition(layer, centerOffsetPixels));
            rect.x = SnapNearPixel(rect.x);
            rect.y = SnapNearPixel(rect.y);
            if (!DimensionPortalVisualContract.IsPixelAligned(rect))
            {
                AddPreviewWarning(
                    GetLayerTitle(ToStudioLayer(layer)) + " lands between source pixels at " +
                    FormatRect(rect) + ". The preview preserves the runtime transform; use a pixel-aligned pivot or center offset for crisp point-filtered artwork.");
            }

            return rect;
        }

        private void ValidateTransformedFrameBounds(VisibleFrame frame)
        {
            Vector2 bottomLeft = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMin, frame.CanvasRect.yMin));
            Vector2 bottomRight = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMax, frame.CanvasRect.yMin));
            Vector2 topRight = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMax, frame.CanvasRect.yMax));
            Vector2 topLeft = TransformFrameCanvasPoint(
                frame,
                new Vector2(frame.CanvasRect.xMin, frame.CanvasRect.yMax));
            float minimumX = Mathf.Min(
                Mathf.Min(bottomLeft.x, bottomRight.x),
                Mathf.Min(topRight.x, topLeft.x));
            float maximumX = Mathf.Max(
                Mathf.Max(bottomLeft.x, bottomRight.x),
                Mathf.Max(topRight.x, topLeft.x));
            float minimumY = Mathf.Min(
                Mathf.Min(bottomLeft.y, bottomRight.y),
                Mathf.Min(topRight.y, topLeft.y));
            float maximumY = Mathf.Max(
                Mathf.Max(bottomLeft.y, bottomRight.y),
                Mathf.Max(topRight.y, topLeft.y));
            if (minimumX >= -0.001f &&
                minimumY >= -0.001f &&
                maximumX <= canvasPixelWidth + 0.001f &&
                maximumY <= canvasPixelHeight + 0.001f)
            {
                return;
            }

            AddPreviewWarning(
                GetLayerTitle(frame.Layer) +
                " extends outside the 48 x 48 portal artboard after its layout transform. " +
                "The Studio and the generated portal both preserve the transform and clip any pixels beyond the authored canvas.");
        }

        private static float SnapNearPixel(float value)
        {
            float rounded = Mathf.Round(value);
            return Mathf.Abs(value - rounded) < 0.01f ? rounded : value;
        }

        private static StudioLayer ToStudioLayer(DimensionPortalVisualContract.Layer layer)
        {
            switch (layer)
            {
                case DimensionPortalVisualContract.Layer.ChargeSweep:
                    return StudioLayer.ChargeSweep;
                case DimensionPortalVisualContract.Layer.Milestones:
                    return StudioLayer.Milestones;
                case DimensionPortalVisualContract.Layer.Center:
                    return StudioLayer.Center;
                default:
                    return StudioLayer.Frame;
            }
        }

        private void ValidateLayerSheet(
            StudioLayer layer,
            AnimationSheet animation,
            int expectedWidth,
            int expectedHeight,
            string label,
            bool allowFullCanvasFrames = false)
        {
            if (animation.Sheet == null || animation.Sheet.Texture == null)
            {
                AddPreviewError(label + " has no usable source texture.");
                return;
            }

            if (animation.Sheet.Width % Mathf.Max(1, animation.FrameCount) != 0)
            {
                AddPreviewError(
                    label + " sheet width " + animation.Sheet.Width +
                    " is not divisible by its " + animation.FrameCount + " source frames.");
            }

            // Non-vanilla frame sizes are fully supported (shown at native size/pivot, never
            // stretched) — deliberately no advisory about them; creators chose their size.

            if (animation.Sheet.Pixels == null)
            {
                AddPreviewWarning(
                    label + " is not backed by a directly readable PNG. It can be displayed, but alpha-aware pixel picking is approximate.");
            }
        }

        private static int GetAnimationFrame(AnimationSheet animation, float time)
        {
            if (animation.FrameCount <= 1 || animation.Fps <= 0f)
            {
                return 0;
            }

            int runtimeFrameCount = animation.RuntimeFrameRemap != null &&
                                    animation.RuntimeFrameRemap.Length > 0
                ? animation.RuntimeFrameRemap.Length
                : animation.FrameCount;
            int runtimeFrame = Mathf.FloorToInt(Mathf.Max(0f, time) * animation.Fps);
            runtimeFrame = animation.Loop
                ? ((runtimeFrame % runtimeFrameCount) + runtimeFrameCount) % runtimeFrameCount
                : Mathf.Clamp(runtimeFrame, 0, runtimeFrameCount - 1);
            if (animation.RuntimeFrameRemap != null &&
                animation.RuntimeFrameRemap.Length == runtimeFrameCount)
            {
                return Mathf.Clamp(
                    animation.RuntimeFrameRemap[runtimeFrame],
                    0,
                    animation.FrameCount - 1);
            }

            return Mathf.Clamp(runtimeFrame, 0, animation.FrameCount - 1);
        }

        private static float GetAnimationDuration(AnimationSheet animation, float fallback)
        {
            if (animation.Fps <= 0f)
            {
                return fallback;
            }

            int runtimeFrames = animation.RuntimeFrameRemap != null &&
                                animation.RuntimeFrameRemap.Length > 0
                ? animation.RuntimeFrameRemap.Length
                : animation.FrameCount;
            return Mathf.Max(0.01f, runtimeFrames / animation.Fps);
        }

        private void AddPreviewWarning(string message)
        {
            if (!string.IsNullOrEmpty(message) && !previewWarnings.Contains(message))
            {
                previewWarnings.Add(message);
            }
        }

        private void AddPreviewError(string message)
        {
            if (!string.IsNullOrEmpty(message) && !previewErrors.Contains(message))
            {
                previewErrors.Add(message);
            }
        }

        private void DrawVisibleFrame(Rect localCanvas, VisibleFrame frame)
        {
            if (!IsFramePreviewVisible(frame) ||
                frame.DisplaySheet == null ||
                frame.DisplaySheet.Texture == null)
            {
                return;
            }

            DrawFrameTexture(frame, frame.DisplaySheet.Texture);
        }

        private void DrawFleckPreview(Rect localCanvas)
        {
            FleckPreviewState state = fleckPreviewState;
            if (previewPhase != PreviewPhase.Activated ||
                !state.Visible ||
                activeProfile == null ||
                state.EmissionMultiplier <= 0f)
            {
                return;
            }

            if (particlePreviewRenderer.TryRender(
                    activeProfile,
                    activatedClock,
                    out Texture particleTexture,
                    out string renderError))
            {
                Rect nativeParticleCanvas = new Rect(
                    state.Origin.x - CanonicalCanvasPixels * 0.5f,
                    state.Origin.y - CanonicalCanvasPixels * 0.5f,
                    CanonicalCanvasPixels,
                    CanonicalCanvasPixels);
                particlePreviewRenderer.DrawAdditive(
                    CanvasRectToGuiRect(nativeParticleCanvas),
                    particleTexture);
            }
            else if (!string.IsNullOrEmpty(renderError))
            {
                AddPreviewWarning(renderError);
            }

            if (selectedLayer == StudioLayer.InnerFlecks)
            {
                Vector2 scale = new Vector2(
                    Mathf.Clamp(Mathf.Abs(state.Scale.x), 0.05f, 8f),
                    Mathf.Clamp(Mathf.Abs(state.Scale.y), 0.05f, 8f));
                float radius = 8.8f * state.RadiusMultiplier;
                float averageScale = Mathf.Sqrt(scale.x * scale.y);
                float particleSize = Mathf.Clamp(
                    1.6f * state.SizeMultiplier * averageScale,
                    0.45f,
                    12f);
                Vector2 extent = new Vector2(
                    radius * scale.x + particleSize,
                    radius * 1.1f * scale.y + particleSize);
                Rect bounds = new Rect(
                    state.Origin - extent,
                    extent * 2f);
                DrawOutline(
                    CanvasRectToGuiRect(bounds),
                    new Color(0.2f, 0.78f, 1f, 0.7f),
                    1f);
            }
        }

        private void DrawFrameTexture(VisibleFrame frame, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Rect destination = CanvasRectToGuiRect(frame.CanvasRect);
            int frameCount = Mathf.Max(1, frame.DisplaySheet.FrameCount);
            int frameIndex = Mathf.Clamp(frame.FrameIndex, 0, frameCount - 1);
            Rect uv = new Rect(
                frameIndex / (float)frameCount,
                0f,
                1f / frameCount,
                1f);
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            try
            {
                GUI.matrix = previousMatrix * GetFrameGuiMatrix(frame);
                GUI.color = frame.Tint;
                GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
            }
            finally
            {
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
            }
        }

        private Matrix4x4 GetFrameGuiMatrix(VisibleFrame frame)
        {
            Vector2 pivotCanvas = GetFramePivotCanvas(frame);
            Vector2 pivotGui = CanvasPointToGuiPoint(pivotCanvas);
            Vector2 signedScale = GetSignedPreviewScale(frame);
            return Matrix4x4.Translate(new Vector3(pivotGui.x, pivotGui.y, 0f)) *
                   Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -frame.RotationDegrees)) *
                   Matrix4x4.Scale(new Vector3(signedScale.x, signedScale.y, 1f)) *
                   Matrix4x4.Translate(new Vector3(-pivotGui.x, -pivotGui.y, 0f));
        }

        private static Vector2 GetFramePivotCanvas(VisibleFrame frame)
        {
            return frame.CanvasRect.position + Vector2.Scale(
                frame.Pivot,
                frame.CanvasRect.size);
        }

        private static Vector2 GetSignedPreviewScale(VisibleFrame frame)
        {
            Vector2 scale = ClampPreviewScale(frame.Scale);
            return new Vector2(
                frame.FlipX ? -scale.x : scale.x,
                frame.FlipY ? -scale.y : scale.y);
        }

        private static Vector2 TransformFrameCanvasPoint(
            VisibleFrame frame,
            Vector2 untransformedPoint)
        {
            Vector2 pivot = GetFramePivotCanvas(frame);
            Vector2 signedScale = GetSignedPreviewScale(frame);
            Vector2 delta = untransformedPoint - pivot;
            delta = Vector2.Scale(delta, signedScale);
            float radians = frame.RotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return pivot + new Vector2(
                cosine * delta.x - sine * delta.y,
                sine * delta.x + cosine * delta.y);
        }

        private static bool TryInverseTransformFrameCanvasPoint(
            VisibleFrame frame,
            Vector2 transformedPoint,
            out Vector2 untransformedPoint)
        {
            Vector2 pivot = GetFramePivotCanvas(frame);
            Vector2 signedScale = GetSignedPreviewScale(frame);
            if (Mathf.Abs(signedScale.x) < 0.0001f ||
                Mathf.Abs(signedScale.y) < 0.0001f)
            {
                untransformedPoint = Vector2.zero;
                return false;
            }

            Vector2 delta = transformedPoint - pivot;
            float radians = -frame.RotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            Vector2 unrotated = new Vector2(
                cosine * delta.x - sine * delta.y,
                sine * delta.x + cosine * delta.y);
            untransformedPoint = pivot + new Vector2(
                unrotated.x / signedScale.x,
                unrotated.y / signedScale.y);
            return true;
        }

        private static Vector2 ClampPreviewScale(Vector2 scale)
        {
            return new Vector2(
                Mathf.Clamp(Mathf.Abs(scale.x), 0.05f, 8f),
                Mathf.Clamp(Mathf.Abs(scale.y), 0.05f, 8f));
        }

        private static float NormalizePreviewRotation(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            {
                return 0f;
            }

            return Mathf.Repeat(degrees + 180f, 360f) - 180f;
        }

        private void DrawPaletteFocusOverlay(Rect localCanvas)
        {
            if (!TryGetPaletteFocusFrame(out VisibleFrame frame, out int paletteIndex) ||
                !EnsurePaletteFocusTexture(frame, paletteIndex))
            {
                return;
            }

            EditorGUI.DrawRect(
                localCanvas,
                new Color(0.025f, 0.028f, 0.035f, 0.82f));
            DrawFrameTexture(frame, paletteFocusTexture);
        }

        private bool TryGetPaletteFocusFrame(
            out VisibleFrame focusFrame,
            out int paletteIndex)
        {
            focusFrame = default(VisibleFrame);
            paletteIndex = -1;
            if (!paletteFocusActive ||
                paletteFocusLayer != selectedLayer ||
                paletteFocusRoleIndex < 0)
            {
                return false;
            }

            ColorRole[] roles = GetColorRoles(paletteFocusLayer);
            if (paletteFocusRoleIndex >= roles.Length ||
                !roles[paletteFocusRoleIndex].IsPaletteColor)
            {
                return false;
            }

            int rolePaletteIndex = GetPaletteOrdinal(roles, paletteFocusRoleIndex);
            if (rolePaletteIndex < 0)
            {
                return false;
            }

            if (!TryFindPaletteFocusFrame(
                    paletteFocusLayer,
                    rolePaletteIndex,
                    out focusFrame))
            {
                return false;
            }

            paletteIndex = rolePaletteIndex;
            return true;
        }

        private static int GetPaletteOrdinal(ColorRole[] roles, int roleIndex)
        {
            if (roles == null || roleIndex < 0 || roleIndex >= roles.Length)
            {
                return -1;
            }

            int paletteIndex = 0;
            for (int i = 0; i < roleIndex; i++)
            {
                if (roles[i].IsPaletteColor)
                {
                    paletteIndex++;
                }
            }

            return roles[roleIndex].IsPaletteColor ? paletteIndex : -1;
        }

        private bool CanFocusPaletteRole(
            StudioLayer layer,
            ColorRole[] roles,
            int roleIndex)
        {
            int paletteIndex = GetPaletteOrdinal(roles, roleIndex);
            return paletteIndex >= 0 &&
                   TryFindPaletteFocusFrame(
                       layer,
                       paletteIndex,
                       out VisibleFrame unused);
        }

        private bool TryFindPaletteFocusFrame(
            StudioLayer layer,
            int paletteIndex,
            out VisibleFrame focusFrame)
        {
            focusFrame = default(VisibleFrame);
            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame candidate = visibleFrames[i];
                if (!IsFramePreviewVisible(candidate) ||
                    candidate.Layer != layer ||
                    !candidate.PalettePickingEnabled ||
                    candidate.DirectOverride ||
                    candidate.DisplaySheet == null ||
                    candidate.DisplaySheet.Pixels == null ||
                    candidate.HitSheet == null ||
                    candidate.HitSheet.Pixels == null ||
                    candidate.SourcePalette == null ||
                    paletteIndex < 0 ||
                    paletteIndex >= candidate.SourcePalette.Length)
                {
                    continue;
                }

                focusFrame = candidate;
                return true;
            }

            return false;
        }

        private bool EnsurePaletteFocusTexture(VisibleFrame frame, int paletteIndex)
        {
            int width = frame.DisplaySheet.Width;
            int height = frame.DisplaySheet.Height;
            if (width <= 0 ||
                height <= 0 ||
                frame.HitSheet.Width != width ||
                frame.HitSheet.Height != height ||
                frame.DisplaySheet.Pixels.Length != width * height ||
                frame.HitSheet.Pixels.Length != width * height)
            {
                return false;
            }

            bool cacheMatches =
                paletteFocusTexture != null &&
                paletteFocusTexture.width == width &&
                paletteFocusTexture.height == height &&
                paletteFocusDisplaySheet == frame.DisplaySheet &&
                paletteFocusHitSheet == frame.HitSheet &&
                paletteFocusSourcePalette == frame.SourcePalette &&
                paletteFocusTextureLayer == frame.Layer &&
                paletteFocusTextureRole == paletteIndex;
            if (cacheMatches)
            {
                return true;
            }

            if (paletteFocusTexture == null ||
                paletteFocusTexture.width != width ||
                paletteFocusTexture.height != height)
            {
                DestroyTexture(ref paletteFocusTexture);
                paletteFocusTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "Portal Studio Palette Focus",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            int pixelCount = width * height;
            if (paletteFocusPixels == null ||
                paletteFocusPixels.Length != pixelCount)
            {
                paletteFocusPixels = new Color32[pixelCount];
            }

            Color32 transparent = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixelCount; i++)
            {
                Color32 sourcePixel = frame.HitSheet.Pixels[i];
                if (sourcePixel.a == 0 ||
                    FindClosestPaletteIndex(
                        sourcePixel,
                        frame.SourcePalette) != paletteIndex)
                {
                    paletteFocusPixels[i] = transparent;
                    continue;
                }

                paletteFocusPixels[i] = frame.DisplaySheet.Pixels[i];
            }

            paletteFocusTexture.SetPixels32(paletteFocusPixels);
            paletteFocusTexture.Apply(false, false);
            paletteFocusDisplaySheet = frame.DisplaySheet;
            paletteFocusHitSheet = frame.HitSheet;
            paletteFocusSourcePalette = frame.SourcePalette;
            paletteFocusTextureLayer = frame.Layer;
            paletteFocusTextureRole = paletteIndex;
            return true;
        }

        private static bool IsFramePreviewVisible(VisibleFrame frame)
        {
            return true;
        }

        private void DrawPixelGrid(Rect localCanvas)
        {
            Color gridColor = new Color(1f, 1f, 1f, activeZoom >= 7f ? 0.12f : 0.075f);
            Rect artboard = CanvasRectToGuiRect(
                new Rect(0f, 0f, canvasPixelWidth, canvasPixelHeight));
            float thickness = Mathf.Max(0.5f, 1f / EditorGUIUtility.pixelsPerPoint);
            for (int i = 1; i < canvasPixelWidth; i++)
            {
                float coordinate = artboard.x + i * activeZoom;
                EditorGUI.DrawRect(
                    new Rect(coordinate, artboard.y, thickness, artboard.height),
                    gridColor);
            }

            for (int i = 1; i < canvasPixelHeight; i++)
            {
                float coordinate = artboard.y + i * activeZoom;
                EditorGUI.DrawRect(
                    new Rect(artboard.x, coordinate, artboard.width, thickness),
                    gridColor);
            }
        }

        private void DrawSelectedLayerOutline()
        {
            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame frame = visibleFrames[i];
                if (!IsFramePreviewVisible(frame) || frame.Layer != selectedLayer)
                {
                    continue;
                }

                Matrix4x4 previousMatrix = GUI.matrix;
                GUI.matrix = previousMatrix * GetFrameGuiMatrix(frame);
                DrawOutline(
                    CanvasRectToGuiRect(frame.CanvasRect),
                    new Color(0.2f, 0.78f, 1f, 0.7f),
                    1f);
                GUI.matrix = previousMatrix;
                return;
            }
        }

        private void DrawSelectedPixel()
        {
            if (!hasSelectedPixel)
            {
                return;
            }

            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame frame = visibleFrames[i];
                if (frame.Layer != selectedPixelLayer ||
                    !IsFramePreviewVisible(frame))
                {
                    continue;
                }

                Rect sourcePixelRect = new Rect(
                    frame.CanvasRect.x + selectedSourcePixel.x,
                    frame.CanvasRect.y + selectedSourcePixel.y,
                    1f,
                    1f);
                Matrix4x4 previousMatrix = GUI.matrix;
                GUI.matrix = previousMatrix * GetFrameGuiMatrix(frame);
                Rect pixelRect = CanvasRectToGuiRect(sourcePixelRect);
                DrawOutline(pixelRect, Color.black, 2f);
                DrawOutline(pixelRect, Color.white, 1f);
                GUI.matrix = previousMatrix;
                return;
            }
        }

        private void HandleCanvasInteraction(
            Rect canvasRect,
            SerializedObject profile,
            int controlId)
        {
            Event current = Event.current;
            if (current == null || profile == null)
            {
                return;
            }

            if (current.type == EventType.KeyDown &&
                GUIUtility.keyboardControl == controlId &&
                TryGetArrowNudge(current, out Vector2 nudge))
            {
                string offsetPropertyName = GetOffsetPropertyName(selectedLayer);
                SerializedProperty offsetProperty = string.IsNullOrEmpty(offsetPropertyName)
                    ? null
                    : profile.FindProperty(offsetPropertyName);
                if (offsetProperty != null)
                {
                    Undo.RecordObject(
                        profile.targetObject,
                        "Nudge " + GetLayerTitle(selectedLayer));
                    offsetProperty.vector2Value += nudge;
                    MarkPreviewTransformChanged();
                    current.Use();
                }

                return;
            }

            if (current.type == EventType.MouseDrag &&
                GUIUtility.hotControl == controlId &&
                canvasDragPending)
            {
                string offsetPropertyName = GetOffsetPropertyName(canvasDragLayer);
                SerializedProperty offsetProperty = string.IsNullOrEmpty(offsetPropertyName)
                    ? null
                    : profile.FindProperty(offsetPropertyName);
                if (offsetProperty != null)
                {
                    Vector2 pointer = GuiPointToCanvasPoint(
                        canvasRect,
                        current.mousePosition);
                    Vector2 delta = pointer - canvasDragStartPoint;
                    Vector2 next = canvasDragStartOffset + new Vector2(
                        Mathf.Round(delta.x),
                        Mathf.Round(delta.y));
                    if (offsetProperty.vector2Value != next)
                    {
                        if (!canvasDragUndoRecorded)
                        {
                            Undo.RecordObject(
                                profile.targetObject,
                                "Move " + GetLayerTitle(canvasDragLayer));
                            canvasDragUndoRecorded = true;
                        }

                        offsetProperty.vector2Value = next;
                        MarkPreviewTransformChanged();
                    }
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp &&
                current.button == 0 &&
                GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                canvasDragPending = false;
                if (canvasDragUndoRecorded)
                {
                    Undo.FlushUndoRecordObjects();
                }

                canvasDragUndoRecorded = false;
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown ||
                current.button != 0 ||
                !canvasRect.Contains(current.mousePosition))
            {
                return;
            }

            GUIUtility.keyboardControl = controlId;
            ClearPaletteFocus();
            Vector2 canvasPoint = GuiPointToCanvasPoint(
                canvasRect,
                current.mousePosition);
            selectedPixel = new Vector2Int(
                Mathf.FloorToInt(canvasPoint.x),
                Mathf.FloorToInt(canvasPoint.y));
            hasSelectedPixel = false;

            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame frame = visibleFrames[i];
                if (!IsFramePreviewVisible(frame) ||
                    !TryCanvasPointToFramePixel(
                        frame,
                        canvasPoint,
                        out int localX,
                        out int localY,
                        out Color32 pixel,
                        out bool canReadPixel))
                {
                    continue;
                }

                SelectLayer(frame.Layer);
                selectedPixelLayer = frame.Layer;
                selectedSourcePixel = new Vector2Int(localX, localY);
                hasSelectedPixel = true;
                if (canReadPixel && frame.PalettePickingEnabled && frame.SourcePalette != null)
                {
                    selectedColorRoleByLayer[(int)frame.Layer] =
                        FindClosestPaletteIndex(pixel, frame.SourcePalette);
                }

                string offsetPropertyName = GetOffsetPropertyName(frame.Layer);
                SerializedProperty offsetProperty = string.IsNullOrEmpty(offsetPropertyName)
                    ? null
                    : profile.FindProperty(offsetPropertyName);
                if (offsetProperty != null)
                {
                    canvasDragLayer = frame.Layer;
                    canvasDragStartPoint = canvasPoint;
                    canvasDragStartOffset = offsetProperty.vector2Value;
                    canvasDragPending = true;
                    canvasDragUndoRecorded = false;
                    GUIUtility.hotControl = controlId;
                }

                current.Use();
                return;
            }

            // Transparent pixels are still useful drag handles for the layer that is
            // already selected. This keeps thin, crooked, or deliberately broken art
            // easy to position without stealing normal pixel/color selection from the
            // opaque artwork of other layers.
            for (int i = visibleFrames.Count - 1; i >= 0; i--)
            {
                VisibleFrame frame = visibleFrames[i];
                if (frame.Layer != selectedLayer ||
                    !IsFramePreviewVisible(frame) ||
                    !TryInverseTransformFrameCanvasPoint(
                        frame,
                        canvasPoint,
                        out Vector2 untransformed) ||
                    !frame.CanvasRect.Contains(untransformed))
                {
                    continue;
                }

                string offsetPropertyName = GetOffsetPropertyName(frame.Layer);
                SerializedProperty offsetProperty = string.IsNullOrEmpty(offsetPropertyName)
                    ? null
                    : profile.FindProperty(offsetPropertyName);
                if (offsetProperty != null)
                {
                    canvasDragLayer = frame.Layer;
                    canvasDragStartPoint = canvasPoint;
                    canvasDragStartOffset = offsetProperty.vector2Value;
                    canvasDragPending = true;
                    canvasDragUndoRecorded = false;
                    GUIUtility.hotControl = controlId;
                }

                current.Use();
                return;
            }

            current.Use();
        }

        private Vector2 GuiPointToCanvasPoint(Rect canvasRect, Vector2 guiPoint)
        {
            return new Vector2(
                activeViewBounds.x + (guiPoint.x - canvasRect.x) / activeZoom,
                activeViewBounds.yMax - (guiPoint.y - canvasRect.y) / activeZoom);
        }

        private static bool TryCanvasPointToFramePixel(
            VisibleFrame frame,
            Vector2 canvasPoint,
            out int localX,
            out int localY,
            out Color32 pixel,
            out bool canReadPixel)
        {
            localX = -1;
            localY = -1;
            pixel = default(Color32);
            canReadPixel = frame.HitSheet != null && frame.HitSheet.Pixels != null;
            if (!TryInverseTransformFrameCanvasPoint(
                    frame,
                    canvasPoint,
                    out Vector2 untransformed) ||
                !frame.CanvasRect.Contains(untransformed))
            {
                return false;
            }

            localX = Mathf.FloorToInt(untransformed.x - frame.CanvasRect.x);
            localY = Mathf.FloorToInt(untransformed.y - frame.CanvasRect.y);
            if (canReadPixel &&
                (!frame.HitSheet.TryGetPixel(
                     frame.FrameIndex,
                     localX,
                     localY,
                     out pixel) ||
                 pixel.a == 0))
            {
                return false;
            }

            return true;
        }

        private static bool TryGetArrowNudge(Event current, out Vector2 nudge)
        {
            nudge = Vector2.zero;
            if (current == null)
            {
                return false;
            }

            float amount = current.shift ? 4f : 1f;
            switch (current.keyCode)
            {
                case KeyCode.LeftArrow:
                    nudge = Vector2.left * amount;
                    return true;
                case KeyCode.RightArrow:
                    nudge = Vector2.right * amount;
                    return true;
                case KeyCode.DownArrow:
                    nudge = Vector2.down * amount;
                    return true;
                case KeyCode.UpArrow:
                    nudge = Vector2.up * amount;
                    return true;
                default:
                    return false;
            }
        }

        private bool IsTransformableLayer(StudioLayer layer)
        {
            return layer == StudioLayer.Frame ||
                   layer == StudioLayer.ChargeSweep ||
                   layer == StudioLayer.Milestones ||
                   layer == StudioLayer.Center ||
                   (layer == StudioLayer.InnerFlecks &&
                    activeProfile != null &&
                    activeProfile.CenterSwirlOverrideVanilla);
        }

        private static string GetOffsetPropertyName(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return "frameOffsetPixels";
                case StudioLayer.ChargeSweep:
                    return "chargeWaveOffsetPixels";
                case StudioLayer.Milestones:
                    return "milestoneOffsetPixels";
                case StudioLayer.Center:
                    return "centerOffsetPixels";
                case StudioLayer.InnerFlecks:
                    return "centerParticleOffsetPixels";
                default:
                    return string.Empty;
            }
        }

        private void MarkPreviewTransformChanged()
        {
            previewCompositionDirty = true;
            repaintRequested = true;
            GUI.changed = true;
        }

        private void DrawContextInspector(
            DimensionTemplateAsset template,
            SerializedObject profile,
            ref DrawResult result,
            float minimumHeight = 0f,
            float fixedWidth = 0f)
        {
            if (minimumHeight > 0f && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(minimumHeight));
            }
            else if (minimumHeight > 0f)
            {
                EditorGUILayout.BeginVertical(GUILayout.Height(minimumHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical();
            }

            DimensionPortalArtworkReferenceKind artworkReferenceKind =
                DimensionPortalArtworkReferenceKind.Empty;
            SpriteAsset resolvedArtworkAsset = null;
            Rect mainPanelRect;
            if (fixedWidth > 0f)
            {
                mainPanelRect = EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                mainPanelRect = EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.MinWidth(InspectorMinWidth),
                    GUILayout.ExpandWidth(true));
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(GetLayerTitle(selectedLayer), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Restore vanilla", GUILayout.Width(104f)))
            {
                CancelPendingTextureUpdate();
                CancelPendingArtworkVariantCreation(selectedLayer);
                profileEditSession.MarkChanged();

                DimensionPortalVisualProfileAsset restoreProfile =
                    profile.targetObject as DimensionPortalVisualProfileAsset;
                if (TryGetArtworkLayer(
                        selectedLayer,
                        out DimensionPortalArtworkLayer restoreArtworkLayer,
                        instantPortalMode))
                {
                    DimensionPortalArtworkEditorUtility.CancelPending(
                        restoreProfile,
                        restoreArtworkLayer);
                }
                else if (selectedLayer == StudioLayer.InnerFlecks)
                {
                    DimensionPortalSwirlArtworkEditorUtility.CancelSwirlBake(restoreProfile);
                }

                if (!RestoreLayerToVanilla(
                        profile,
                        selectedLayer,
                        out string restoreMessage,
                        instantPortalMode))
                {
                    result.Message = restoreMessage;
                    result.MessageType = MessageType.Error;
                }

                InvalidateTextureSlotCache();
                ClearPaletteFocus();
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);

            bool directOverride = false;
            long artworkReferenceLow = 0L;
            long artworkReferenceHigh = 0L;
            string overridePropertyName = GetOverridePropertyName(selectedLayer);
            if (!string.IsNullOrEmpty(overridePropertyName))
            {
                SerializedProperty overrideProperty = profile.FindProperty(overridePropertyName);
                TryReadReferenceAddress(
                    overrideProperty,
                    out artworkReferenceLow,
                    out artworkReferenceHigh);
                DrawArtworkOverride(
                    template,
                    profile,
                    selectedLayer,
                    overrideProperty,
                    ref result);
                if (TryGetArtworkLayer(
                        selectedLayer,
                        out DimensionPortalArtworkLayer artworkLayer,
                        instantPortalMode))
                {
                    artworkReferenceKind =
                        DimensionPortalArtworkEditorUtility.ClassifyReference(
                            overrideProperty,
                            profile.targetObject as DimensionPortalVisualProfileAsset,
                            artworkLayer,
                            out resolvedArtworkAsset);
                    directOverride = IsDirectTextureReference(
                        profile.targetObject as DimensionPortalVisualProfileAsset,
                        artworkLayer,
                        artworkReferenceKind,
                        resolvedArtworkAsset,
                        overrideProperty);

                    if (artworkReferenceKind == DimensionPortalArtworkReferenceKind.Unresolved)
                    {
                        EditorGUILayout.HelpBox(
                            "This SpriteAsset address is not available in the live Scriptable Data lookup. The Studio is showing the framework artwork with the current palette until the lookup refreshes; Apply remains blocked.",
                            MessageType.Error);
                    }
                }
                if (directOverride &&
                    (selectedLayer == StudioLayer.ChargeSweep ||
                     selectedLayer == StudioLayer.Milestones ||
                     selectedLayer == StudioLayer.Center))
                {
                    EditorGUILayout.HelpBox(
                        "This SpriteAsset supplies the final pixels, so its built-in palette colors are bypassed. Tint, emission, timing, and playback settings still apply.",
                        MessageType.None);
                }
            }

            ColorRole[] roles = GetColorRoles(selectedLayer);
            if (selectedLayer == StudioLayer.InnerFlecks)
            {
                DrawFlecksPaletteControls(profile);
                GUILayout.Space(4f);
                DrawArtworkOverride(
                    template,
                    profile,
                    StudioLayer.InnerFlecks,
                    profile.FindProperty("centerSwirlSpriteAsset"),
                    ref result);
                if (GetBool(profile, "centerSwirlOverrideVanilla", false) &&
                    (!TryResolveCustomSwirlAsset(
                         profile,
                         out SpriteAsset swirlArtwork,
                         out string swirlArtworkError) ||
                     !TryValidateCustomSwirlAsset(
                         swirlArtwork,
                         out swirlArtworkError)))
                {
                    EditorGUILayout.HelpBox(swirlArtworkError, MessageType.Error);
                }
                GUILayout.Space(4f);
            }
            else if (selectedLayer == StudioLayer.ReadyBurst)
            {
                DrawReadyBurstPaletteControls(profile);
                GUILayout.Space(4f);
                DrawProperty(profile, "readyFlashSprites", "Animation frames");
                GUILayout.Space(4f);
            }

            if (roles.Length > 0)
            {
                DrawColorRoleStrip(profile, roles, directOverride);
                int selectedRoleIndex = Mathf.Clamp(
                    selectedColorRoleByLayer[(int)selectedLayer],
                    0,
                    roles.Length - 1);
                ColorRole selectedRole = roles[selectedRoleIndex];
                bool paletteDisabled = directOverride && selectedRole.IsPaletteColor;
                bool followsCenterPalette =
                    IsSelectedParticleTintFollowingCenter(
                        profile,
                        selectedLayer,
                        selectedRoleIndex);
                EditorGUI.BeginDisabledGroup(paletteDisabled || followsCenterPalette);
                EditorGUI.BeginChangeCheck();
                DrawModernColorEditor(profile.FindProperty(selectedRole.PropertyName), selectedRole);
                bool colorChanged = EditorGUI.EndChangeCheck();
                EditorGUI.EndDisabledGroup();
                if (colorChanged && selectedRole.IsPaletteColor)
                {
                    paletteBakeRequested = true;
                    paletteBakeLayer = selectedLayer;
                }

                if (followsCenterPalette)
                {
                    EditorGUILayout.HelpBox(
                        "Following the activated center palette. Vanilla center colors preserve the exact vanilla particle gradient; disable the matching follow option to use this independent color.",
                        MessageType.None);
                }
            }

            GUILayout.Space(6f);
            if (selectedLayer != StudioLayer.InnerFlecks ||
                GetBool(profile, "centerSwirlOverrideVanilla", false))
            {
                DrawLayerTransformSettings(profile, selectedLayer);
            }
            DrawLayerSpecificSettings(profile);
            EditorGUILayout.EndVertical();
            if (TryGetArtworkLayer(
                    selectedLayer,
                    out DimensionPortalArtworkLayer selectedArtworkLayer,
                    instantPortalMode) &&
                Event.current.type == EventType.Repaint &&
                mainPanelRect.height > 1f)
            {
                measuredArtworkMainPanelHeights[(int)selectedArtworkLayer] =
                    mainPanelRect.height;
            }
            else if (selectedLayer == StudioLayer.InnerFlecks &&
                     Event.current.type == EventType.Repaint &&
                     mainPanelRect.height > 1f)
            {
                measuredSwirlMainPanelHeight = mainPanelRect.height;
            }

            if (TryGetArtworkLayer(
                    selectedLayer,
                    out selectedArtworkLayer,
                    instantPortalMode))
            {
                GUILayout.Space(4f);
                float minimumTexturePanelHeight = GetTexturePanelMinimumHeight(
                    selectedArtworkLayer);
                float texturePanelHeight = minimumHeight > 0f
                    ? Mathf.Max(
                        minimumTexturePanelHeight,
                        minimumHeight -
                        measuredArtworkMainPanelHeights[(int)selectedArtworkLayer] -
                        measuredSoundsPanelHeight -
                        8f -
                        EditorGUIUtility.standardVerticalSpacing * 3f)
                    : 0f;
                DrawTexturePanel(
                    template,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    selectedArtworkLayer,
                    artworkReferenceKind,
                    resolvedArtworkAsset,
                    artworkReferenceLow,
                    artworkReferenceHigh,
                    texturePanelHeight,
                    fixedWidth);
            }
            else if (selectedLayer == StudioLayer.InnerFlecks)
            {
                GUILayout.Space(4f);
                const float minimumSwirlTexturePanelHeight = 64f;
                float texturePanelHeight = minimumHeight > 0f
                    ? Mathf.Max(
                        minimumSwirlTexturePanelHeight,
                        minimumHeight -
                        measuredSwirlMainPanelHeight -
                        measuredSoundsPanelHeight -
                        8f -
                        EditorGUIUtility.standardVerticalSpacing * 3f)
                    : 0f;
                DrawSwirlTexturePanel(
                    template,
                    profile.targetObject as DimensionPortalVisualProfileAsset,
                    GetBool(profile, "centerSwirlOverrideVanilla", false),
                    texturePanelHeight,
                    fixedWidth);
            }

            GUILayout.Space(4f);
            DrawPortalSoundsPanel(template, fixedWidth);

            EditorGUILayout.EndVertical();
        }

        // Measured on repaint so the texture panel above can budget its height around the
        // sounds panel; seeded with a sensible estimate for the first frame.
        private float measuredSoundsPanelHeight = 92f;

        /// <summary>
        /// Portal sound configuration, living under the Textures panel. The placed portal only
        /// offers an activation sound; the instant portal picks ONE exclusive mode — Peak
        /// (activation/deactivation one-shots) or Loop (a bed while it stands open). Each field
        /// takes an SfxID name or a Sound Library key; the Pick button browses and previews.
        /// </summary>
        private void DrawPortalSoundsPanel(DimensionTemplateAsset template, float fixedWidth)
        {
            if (template == null)
            {
                return;
            }

            if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            EditorGUILayout.LabelField("Sounds", EditorStyles.boldLabel);

            string placedActivation = template.PlacedPortalActivationSound;
            int instantMode = template.InstantPortalSoundMode;
            string instantActivation = template.InstantPortalActivationSound;
            string instantDeactivation = template.InstantPortalDeactivationSound;
            string instantLoop = template.InstantPortalLoopSound;

            EditorGUI.BeginChangeCheck();
            if (!instantPortalMode)
            {
                placedActivation = DrawSoundKeyField(
                    template,
                    "Activation",
                    "Plays once when the portal finishes charging and lights up. SfxID name or " +
                    "a Sound Library key. Heard within 8 tiles. Empty = silent.",
                    DimensionSoundPickerWindow.FieldPlacedActivation,
                    placedActivation,
                    false);
            }
            else
            {
                instantMode = GUILayout.Toolbar(
                    instantMode,
                    new[]
                    {
                        new GUIContent(
                            "Peak",
                            "One-shots at the portal's edges: an activation sound when it opens " +
                            "and a deactivation sound as it closes."),
                        new GUIContent(
                            "Loop",
                            "A looping bed that plays the whole time the portal stands open.")
                    },
                    GUILayout.Width(160f));
                GUILayout.Space(2f);

                if (instantMode == 0)
                {
                    instantActivation = DrawSoundKeyField(
                        template,
                        "Activation",
                        "Plays once as the portal tears open. SfxID name or a Sound Library " +
                        "key. Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantActivation,
                        instantActivation,
                        false);
                    instantDeactivation = DrawSoundKeyField(
                        template,
                        "Deactivation",
                        "Plays once as the portal winks out. SfxID name or a Sound Library " +
                        "key. Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantDeactivation,
                        instantDeactivation,
                        false);
                }
                else
                {
                    instantLoop = DrawSoundKeyField(
                        template,
                        "Loop",
                        "Loops while the portal stands open and stops as it closes. Needs a " +
                        "Sound Library clip (SfxID names cannot loop). Heard within 8 tiles.",
                        DimensionSoundPickerWindow.FieldInstantLoop,
                        instantLoop,
                        true);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(template, "Portal Sounds");
                template.SetPortalSoundSettings(
                    placedActivation,
                    instantMode,
                    instantActivation,
                    instantDeactivation,
                    instantLoop);
                EditorUtility.SetDirty(template);
            }

            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                measuredSoundsPanelHeight = GUILayoutUtility.GetLastRect().height;
            }
        }

        private static string DrawSoundKeyField(
            DimensionTemplateAsset template,
            string label,
            string tooltip,
            string fieldId,
            string value,
            bool clipOnly)
        {
            EditorGUILayout.BeginHorizontal();
            string result = EditorGUILayout.TextField(new GUIContent(label, tooltip), value);
            if (GUILayout.Button(
                new GUIContent("Pick", "Browse the game's sounds, listen, and select."),
                GUILayout.Width(40f)))
            {
                DimensionSoundPickerWindow.Open(template, fieldId, clipOnly);
            }

            EditorGUILayout.EndHorizontal();
            return result;
        }

        private void DrawTexturePanel(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            long referenceLow,
            long referenceHigh,
            float targetHeight,
            float fixedWidth)
        {
            bool expandHeight = targetHeight > 0f;
            if (expandHeight && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(targetHeight));
            }
            else if (expandHeight)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Height(targetHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            TextureSlotCacheEntry cacheEntry = GetTextureSlotCacheEntry(
                profile,
                layer,
                referenceKind,
                resolvedAsset,
                referenceLow,
                referenceHigh);
            DimensionPortalArtworkEditorUtility.TextureSlot[] slots =
                cacheEntry.Slots ??
                Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>();
            int expectedSlotCount = GetExpectedTextureSlotCount(layer);
            bool slotsReady = slots.Length == expectedSlotCount;
            for (int i = 0; slotsReady && i < expectedSlotCount; i++)
            {
                slotsReady = slots[i] != null;
            }

            string unavailableMessage = string.IsNullOrEmpty(cacheEntry.Message)
                ? "The selected SpriteAsset does not expose compatible texture slots."
                : cacheEntry.Message;
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

            Texture2D[] displayedTextures = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                0,
                expectedSlotCount);
            Texture2D[] displayedEmissive = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                1,
                expectedSlotCount);
            Texture2D[] displayedNormals = GetDisplayedTextureArray(
                profile,
                layer,
                slots,
                2,
                expectedSlotCount);
            bool changed = false;
            EditorGUI.BeginDisabledGroup(!slotsReady);
            // One field per animation, labeled with the animation's name ("Loop", "Opening",
            // "Closing", or the layer's own name for single-sheet layers). It edits the COLOR
            // sheet — the artwork itself. Emissive (the self-glow sheet, cloned from the same
            // art in managed assets) and normal maps are intentionally not exposed; existing
            // values pass through QueueTextureUpdate untouched.
            for (int i = 0; i < expectedSlotCount; i++)
            {
                DimensionPortalArtworkEditorUtility.TextureSlot slot =
                    i < slots.Length ? slots[i] : null;
                if (slot == null)
                {
                    DrawTextureSlotPlaceholder(layer, i, unavailableMessage);
                    continue;
                }

                Rect textureRect = EditorGUILayout.GetControlRect(
                    false,
                    EditorGUIUtility.singleLineHeight);
                Texture2D texture = EditorGUI.ObjectField(
                    textureRect,
                    new GUIContent(
                        slot.DisplayName ?? "Texture",
                        GetTextureFieldTooltip(slot, "Color")),
                    displayedTextures[i],
                    typeof(Texture2D),
                    false) as Texture2D;
                if (texture != displayedTextures[i])
                {
                    displayedTextures[i] = texture;
                    changed = true;
                }
            }
            EditorGUI.EndDisabledGroup();

            if (changed && slotsReady)
            {
                QueueTextureUpdate(
                    template,
                    profile,
                    layer,
                    displayedTextures,
                    displayedEmissive,
                    displayedNormals);
            }

            if (expandHeight)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSwirlTexturePanel(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            bool customMode,
            float targetHeight,
            float fixedWidth)
        {
            bool expandHeight = targetHeight > 0f;
            if (expandHeight && fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Width(fixedWidth),
                    GUILayout.Height(targetHeight));
            }
            else if (expandHeight)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Height(targetHeight));
            }
            else if (fixedWidth > 0f)
            {
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox,
                    GUILayout.Width(fixedWidth));
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            }

            SwirlTextureSlotCacheEntry cacheEntry =
                GetSwirlTextureSlotCacheEntry(profile);
            DimensionPortalSwirlArtworkEditorUtility.AnimationZeroTextureSlot slot =
                cacheEntry == null ? null : cacheEntry.Slot;
            string unavailableMessage = cacheEntry == null ||
                                        string.IsNullOrEmpty(cacheEntry.Message)
                ? "The selected Swirls SpriteAsset does not expose animation 0 textures."
                : cacheEntry.Message;

            Texture2D colorTexture = slot == null ? null : slot.ColorTexture;
            Texture2D emissiveTexture = slot == null ? null : slot.EmissiveTexture;
            Texture2D normalTexture = slot == null ? null : slot.NormalTexture;
            if (pendingSwirlTextureUpdate != null &&
                pendingSwirlTextureUpdate.Profile == profile)
            {
                colorTexture = pendingSwirlTextureUpdate.ColorTexture;
                emissiveTexture = pendingSwirlTextureUpdate.EmissiveTexture;
                normalTexture = pendingSwirlTextureUpdate.NormalTexture;
            }

            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
            bool fieldsEnabled = customMode && slot != null;
            string modeTooltip = customMode
                ? unavailableMessage
                : "Enable Override vanilla to use and edit this custom animation.";
            EditorGUI.BeginDisabledGroup(!fieldsEnabled);
            EditorGUI.BeginChangeCheck();
            // Draw the texture slot with an explicit single-line rect so Unity renders the compact
            // one-line object field (small circle picker), matching the other artwork panels —
            // rather than the large checkerboard Texture2D thumbnail the layout overload reserves.
            Rect swirlColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Texture2D selectedColor = EditorGUI.ObjectField(
                swirlColorRect,
                new GUIContent(
                    "Loop",
                    fieldsEnabled
                        ? "Sheet for the custom Swirls loop animation."
                        : modeTooltip),
                colorTexture,
                typeof(Texture2D),
                false) as Texture2D;
            // Emissive (self-glow) and normal maps are intentionally not exposed; the existing
            // values pass through QueueSwirlTextureUpdate untouched.
            Texture2D selectedEmissive = emissiveTexture;
            Texture2D selectedNormal = normalTexture;
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.EndDisabledGroup();

            if (changed && fieldsEnabled)
            {
                if (selectedColor == null)
                {
                    pendingArtworkMessage =
                        "Custom Swirls require a Color texture for animation 0.";
                    pendingArtworkMessageType = MessageType.Error;
                    repaintRequested = true;
                }
                else
                {
                    QueueSwirlTextureUpdate(
                        template,
                        profile,
                        selectedColor,
                        selectedEmissive,
                        selectedNormal);
                }
            }

            if (expandHeight)
            {
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.EndVertical();
        }

        private SwirlTextureSlotCacheEntry GetSwirlTextureSlotCacheEntry(
            DimensionPortalVisualProfileAsset profile)
        {
            long low = 0L;
            long high = 0L;
            if (profile != null)
            {
                SerializedObject serialized = new SerializedObject(profile);
                serialized.Update();
                TryReadReferenceAddress(
                    serialized.FindProperty(
                        DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                    out low,
                    out high);
            }

            int profileInstanceId = profile == null ? 0 : profile.GetInstanceID();
            if (swirlTextureSlotCache != null &&
                swirlTextureSlotCache.ProfileInstanceId == profileInstanceId &&
                swirlTextureSlotCache.AddressLow == low &&
                swirlTextureSlotCache.AddressHigh == high)
            {
                return swirlTextureSlotCache;
            }

            SwirlTextureSlotCacheEntry entry = new SwirlTextureSlotCacheEntry
            {
                ProfileInstanceId = profileInstanceId,
                AddressLow = low,
                AddressHigh = high,
                Slot = null,
                Message = string.Empty
            };
            if (profile != null &&
                !DimensionPortalSwirlArtworkEditorUtility.TryGetAnimationZeroTextureSlot(
                    profile,
                    out entry.Slot,
                    out entry.Message))
            {
                entry.Slot = null;
            }

            swirlTextureSlotCache = entry;
            return entry;
        }

        private TextureSlotCacheEntry GetTextureSlotCacheEntry(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            long referenceLow,
            long referenceHigh)
        {
            TextureSlotCacheKey key = new TextureSlotCacheKey
            {
                ProfileInstanceId = profile == null ? 0 : profile.GetInstanceID(),
                Layer = layer,
                AddressLow = referenceLow,
                AddressHigh = referenceHigh,
                ResolvedAssetInstanceId = resolvedAsset == null
                    ? 0
                    : resolvedAsset.GetInstanceID(),
                ResolvedAssetDirtyCount = resolvedAsset == null
                    ? 0
                    : EditorUtility.GetDirtyCount(resolvedAsset)
            };
            if (textureSlotCache.TryGetValue(key, out TextureSlotCacheEntry cached))
            {
                return cached;
            }

            TextureSlotCacheEntry entry = new TextureSlotCacheEntry
            {
                Slots = Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>(),
                Message = string.Empty,
                UsesDirectTextureOverride = false
            };
            if (profile != null &&
                !DimensionPortalArtworkEditorUtility.TryGetTextureSlots(
                    profile,
                    layer,
                    out entry.Slots,
                    out entry.Message))
            {
                entry.Slots = Array.Empty<DimensionPortalArtworkEditorUtility.TextureSlot>();
            }

            if (profile != null &&
                referenceKind == DimensionPortalArtworkReferenceKind.Managed)
            {
                entry.UsesDirectTextureOverride =
                    DimensionPortalArtworkEditorUtility.UsesDirectTextureOverride(
                        profile,
                        layer);
            }

            textureSlotCache[key] = entry;
            return entry;
        }

        private Texture2D[] GetDisplayedTextureArray(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkEditorUtility.TextureSlot[] slots,
            int channel,
            int expectedSlotCount)
        {
            if (pendingTextureUpdate != null &&
                pendingTextureUpdate.Profile == profile &&
                pendingTextureUpdate.Layer == layer)
            {
                Texture2D[] pending = channel == 1
                    ? pendingTextureUpdate.EmissiveTextures
                    : channel == 2
                        ? pendingTextureUpdate.NormalTextures
                        : pendingTextureUpdate.Textures;
                if (pending != null && pending.Length == expectedSlotCount)
                {
                    return (Texture2D[])pending.Clone();
                }
            }

            Texture2D[] result = new Texture2D[expectedSlotCount];
            for (int i = 0; i < expectedSlotCount; i++)
            {
                DimensionPortalArtworkEditorUtility.TextureSlot slot =
                    i < slots.Length ? slots[i] : null;
                if (slot != null)
                {
                    result[i] = channel == 1
                        ? slot.EmissiveTexture
                        : channel == 2
                            ? slot.NormalTexture
                            : slot.Texture;
                }
            }

            return result;
        }

        private bool IsDirectTextureReference(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            DimensionPortalArtworkReferenceKind referenceKind,
            SpriteAsset resolvedAsset,
            SerializedProperty reference)
        {
            if (referenceKind == DimensionPortalArtworkReferenceKind.External)
            {
                return true;
            }

            if (referenceKind != DimensionPortalArtworkReferenceKind.Managed)
            {
                return false;
            }

            TryReadReferenceAddress(reference, out long low, out long high);
            return GetTextureSlotCacheEntry(
                       profile,
                       layer,
                       referenceKind,
                       resolvedAsset,
                       low,
                       high)
                   .UsesDirectTextureOverride;
        }

        private bool TryGetPendingTextureSlot(
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            int slotIndex,
            out Texture2D texture,
            out Texture2D emissive)
        {
            texture = null;
            emissive = null;
            if (pendingTextureUpdate == null ||
                pendingTextureUpdate.Profile != profile ||
                pendingTextureUpdate.Layer != layer ||
                pendingTextureUpdate.Textures == null ||
                pendingTextureUpdate.EmissiveTextures == null ||
                slotIndex < 0 ||
                slotIndex >= pendingTextureUpdate.Textures.Length ||
                slotIndex >= pendingTextureUpdate.EmissiveTextures.Length)
            {
                return false;
            }

            texture = pendingTextureUpdate.Textures[slotIndex];
            emissive = pendingTextureUpdate.EmissiveTextures[slotIndex];
            return true;
        }

        private static void DrawTextureSlotPlaceholder(
            DimensionPortalArtworkLayer layer,
            int slotIndex,
            string message)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent(
                    GetExpectedTextureSlotDisplayName(layer, slotIndex),
                    message),
                EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                new GUIContent("Waiting for SpriteAsset data", message),
                EditorStyles.miniLabel,
                GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
        }

        private static string GetTextureFieldTooltip(
            DimensionPortalArtworkEditorUtility.TextureSlot slot,
            string channel)
        {
            return channel + " texture for " +
                   (slot.DisplayName ?? "this animation") +
                   ". Color, emissive, and normal textures in one animation must use matching dimensions. " +
                   "Clear the field to restore the vanilla sheet for this slot.";
        }

        private static float GetTexturePanelMinimumHeight(
            DimensionPortalArtworkLayer layer)
        {
            // One labeled row per animation slot; emissive and normal maps are not exposed.
            return 34f + GetExpectedTextureSlotCount(layer) * 24f;
        }

        private static int GetExpectedTextureSlotCount(
            DimensionPortalArtworkLayer layer)
        {
            return layer == DimensionPortalArtworkLayer.CenterInstant
                ? 3
                : layer == DimensionPortalArtworkLayer.Center
                    ? 2
                    : 1;
        }

        private static string GetExpectedTextureSlotDisplayName(
            DimensionPortalArtworkLayer layer,
            int slotIndex)
        {
            if (layer == DimensionPortalArtworkLayer.CenterInstant)
            {
                return slotIndex == 0
                    ? "Loop"
                    : slotIndex == 1
                        ? "Opening"
                        : "Closing";
            }

            if (layer == DimensionPortalArtworkLayer.Center)
            {
                return slotIndex == 0 ? "Loop" : "Opening";
            }

            return GetArtworkLayerDisplayName(layer);
        }

        private static string GetArtworkLayerDisplayName(
            DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "Frame";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "Charge sweep";
                case DimensionPortalArtworkLayer.Milestones:
                    return "Milestones";
                case DimensionPortalArtworkLayer.Center:
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "Center";
                default:
                    return "portal artwork";
            }
        }

        private static string GetArtworkReferencePropertyName(
            DimensionPortalArtworkLayer layer)
        {
            switch (layer)
            {
                case DimensionPortalArtworkLayer.Frame:
                    return "portalFrameSpriteAsset";
                case DimensionPortalArtworkLayer.ChargeSweep:
                    return "chargeWaveSpriteAsset";
                case DimensionPortalArtworkLayer.Milestones:
                    return "milestoneSpriteAsset";
                case DimensionPortalArtworkLayer.Center:
                case DimensionPortalArtworkLayer.CenterInstant:
                    return "centerEffectSpriteAsset";
                default:
                    return string.Empty;
            }
        }

        private void InvalidateTextureSlotCache()
        {
            textureSlotCache.Clear();
            swirlTextureSlotCache = null;
        }

        private void QueueTextureUpdate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            DimensionPortalArtworkLayer layer,
            Texture2D[] textures,
            Texture2D[] emissiveTextures,
            Texture2D[] normalTextures)
        {
            if (template == null ||
                profile == null ||
                textures == null ||
                emissiveTextures == null ||
                normalTextures == null ||
                textures.Length != emissiveTextures.Length ||
                textures.Length != normalTextures.Length)
            {
                return;
            }

            PendingTextureUpdate update = new PendingTextureUpdate
            {
                Template = template,
                Profile = profile,
                Layer = layer,
                Textures = (Texture2D[])textures.Clone(),
                EmissiveTextures = (Texture2D[])emissiveTextures.Clone(),
                NormalTextures = (Texture2D[])normalTextures.Clone()
            };
            CapturePendingTextureReference(update);
            pendingTextureUpdate = update;
            profileEditSession.MarkChanged();
            previewCompositionDirty = true;
            repaintRequested = true;
            if (textureUpdateQueued)
            {
                return;
            }

            textureUpdateQueued = true;
            EditorApplication.delayCall += ProcessPendingTextureUpdate;
        }

        private void QueueSwirlTextureUpdate(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            Texture2D colorTexture,
            Texture2D emissiveTexture,
            Texture2D normalTexture)
        {
            if (template == null || profile == null || colorTexture == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(profile);
            serialized.Update();
            TryReadReferenceAddress(
                serialized.FindProperty(
                    DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                out long low,
                out long high);
            pendingSwirlTextureUpdate = new PendingSwirlTextureUpdate
            {
                Template = template,
                Profile = profile,
                ColorTexture = colorTexture,
                EmissiveTexture = emissiveTexture,
                NormalTexture = normalTexture,
                ExpectedAddressLow = low,
                ExpectedAddressHigh = high,
                ExpectedTemplateIdentity = CaptureAssetIdentity(template),
                ExpectedProfileIdentity = CaptureAssetIdentity(profile)
            };
            profileEditSession.MarkChanged();
            previewCompositionDirty = true;
            repaintRequested = true;
            if (swirlTextureUpdateQueued)
            {
                return;
            }

            swirlTextureUpdateQueued = true;
            EditorApplication.delayCall += ProcessPendingSwirlTextureUpdate;
        }

        public bool FlushPendingFrameTextureUpdate(out string message)
        {
            message = string.Empty;
            if (pendingTextureUpdate != null)
            {
                EditorApplication.delayCall -= ProcessPendingTextureUpdate;
                ProcessPendingTextureUpdate();
                message = pendingArtworkMessage ?? string.Empty;
                if (pendingArtworkMessageType == MessageType.Error)
                {
                    return false;
                }
            }

            if (pendingSwirlTextureUpdate != null)
            {
                EditorApplication.delayCall -= ProcessPendingSwirlTextureUpdate;
                ProcessPendingSwirlTextureUpdate();
                message = pendingArtworkMessage ?? string.Empty;
                if (pendingArtworkMessageType == MessageType.Error)
                {
                    return false;
                }
            }

            return true;
        }

        private void ProcessPendingTextureUpdate()
        {
            textureUpdateQueued = false;
            PendingTextureUpdate update = pendingTextureUpdate;
            pendingTextureUpdate = null;
            if (disposed || update == null)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        update.Template,
                        update.ExpectedTemplateIdentity,
                        update.Profile,
                        update.ExpectedProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending " + GetArtworkLayerDisplayName(update.Layer) +
                        " texture change was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!IsTextureReferenceCurrent(update))
                {
                    pendingArtworkMessage =
                        "The pending " + GetArtworkLayerDisplayName(update.Layer) +
                        " texture change was cancelled because the Artwork override changed before it was saved.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!DimensionPortalArtworkEditorUtility.CreateOrUpdateLayerTextures(
                        update.Template,
                        update.Profile,
                        update.Layer,
                        update.Textures,
                        update.EmissiveTextures,
                        update.NormalTextures,
                        out string message))
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Error;
                }
                else
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Info;
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    hasPaletteSnapshot = false;
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage =
                    "Could not update the " + GetArtworkLayerDisplayName(update.Layer) +
                    " textures: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private void ProcessPendingSwirlTextureUpdate()
        {
            swirlTextureUpdateQueued = false;
            PendingSwirlTextureUpdate update = pendingSwirlTextureUpdate;
            pendingSwirlTextureUpdate = null;
            if (disposed || update == null)
            {
                return;
            }

            try
            {
                if (!IsActiveEditingContextCurrent(
                        update.Template,
                        update.ExpectedTemplateIdentity,
                        update.Profile,
                        update.ExpectedProfileIdentity))
                {
                    pendingArtworkMessage =
                        "The pending Swirls texture change was cancelled because the active Dimension Asset or portal profile changed.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                SerializedObject serialized = new SerializedObject(update.Profile);
                serialized.Update();
                if (!TryReadReferenceAddress(
                        serialized.FindProperty(
                            DimensionPortalSwirlArtworkEditorUtility.ReferencePropertyName),
                        out long low,
                        out long high) ||
                    low != update.ExpectedAddressLow ||
                    high != update.ExpectedAddressHigh)
                {
                    pendingArtworkMessage =
                        "The pending Swirls texture change was cancelled because the Artwork override changed before it was saved.";
                    pendingArtworkMessageType = MessageType.Warning;
                    repaintRequested = true;
                    return;
                }

                if (!DimensionPortalSwirlArtworkEditorUtility
                        .CreateOrUpdateAnimationZeroTextures(
                            update.Template,
                            update.Profile,
                            update.ColorTexture,
                            update.EmissiveTexture,
                            update.NormalTexture,
                            out string message))
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Error;
                }
                else
                {
                    pendingArtworkMessage = message;
                    pendingArtworkMessageType = MessageType.Info;
                    serializedProfileCache = null;
                    serializedProfileTarget = null;
                    InvalidateTextureSlotCache();
                    ClearPreviewAssetCaches();
                    ClearPaletteFocus();
                    hasSelectedPixel = false;
                }
            }
            catch (Exception exception)
            {
                pendingArtworkMessage =
                    "Could not update the Swirls textures: " + exception.Message;
                pendingArtworkMessageType = MessageType.Error;
                Debug.LogException(exception);
            }

            repaintRequested = true;
        }

        private void CapturePendingTextureReference(PendingTextureUpdate update)
        {
            update.ExpectedTemplateIdentity = CaptureAssetIdentity(update.Template);
            update.ExpectedProfileIdentity = CaptureAssetIdentity(update.Profile);
            update.ExpectedAddressLow = 0L;
            update.ExpectedAddressHigh = 0L;
            update.ExpectedAssetGuid = string.Empty;
            update.ExpectedReferenceKind = DimensionPortalArtworkReferenceKind.Empty;
            if (update.Profile == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(update.Profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                GetArtworkReferencePropertyName(update.Layer));
            TryReadReferenceAddress(
                reference,
                out update.ExpectedAddressLow,
                out update.ExpectedAddressHigh);
            update.ExpectedReferenceKind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    update.Profile,
                    update.Layer,
                    out SpriteAsset asset);
            update.ExpectedAssetGuid = GetAssetGuid(asset);
        }

        private static bool IsTextureReferenceCurrent(PendingTextureUpdate update)
        {
            if (update == null || update.Profile == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(update.Profile);
            serialized.Update();
            SerializedProperty reference = serialized.FindProperty(
                GetArtworkReferencePropertyName(update.Layer));
            if (!TryReadReferenceAddress(reference, out long low, out long high) ||
                low != update.ExpectedAddressLow ||
                high != update.ExpectedAddressHigh)
            {
                return false;
            }

            DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
            DimensionPortalArtworkReferenceKind kind =
                DimensionPortalArtworkEditorUtility.ClassifyReference(
                    reference,
                    update.Profile,
                    update.Layer,
                    out SpriteAsset asset);
            return kind == update.ExpectedReferenceKind &&
                   string.Equals(
                       GetAssetGuid(asset),
                       update.ExpectedAssetGuid ?? string.Empty,
                       StringComparison.Ordinal);
        }

        private static bool TryReadReferenceAddress(
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

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
        }

        private static AssetIdentity CaptureAssetIdentity(UnityEngine.Object asset)
        {
            AssetIdentity identity = new AssetIdentity
            {
                Guid = string.Empty,
                LocalFileId = 0L,
                InstanceId = asset == null ? 0 : asset.GetInstanceID()
            };
            if (asset != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    asset,
                    out string guid,
                    out long localFileId))
            {
                identity.Guid = guid ?? string.Empty;
                identity.LocalFileId = localFileId;
            }

            return identity;
        }

        private static bool MatchesAssetIdentity(
            UnityEngine.Object asset,
            AssetIdentity expected)
        {
            if (asset == null)
            {
                return expected.InstanceId == 0 && !expected.HasPersistentIdentity;
            }

            AssetIdentity current = CaptureAssetIdentity(asset);
            if (expected.HasPersistentIdentity)
            {
                return current.HasPersistentIdentity &&
                       string.Equals(
                           current.Guid,
                           expected.Guid,
                           StringComparison.Ordinal) &&
                       current.LocalFileId == expected.LocalFileId;
            }

            return expected.InstanceId != 0 &&
                   current.InstanceId == expected.InstanceId;
        }

        private bool IsActiveEditingContextCurrent(
            DimensionTemplateAsset template,
            AssetIdentity templateIdentity,
            DimensionPortalVisualProfileAsset profile,
            AssetIdentity profileIdentity)
        {
            return template != null &&
                   profile != null &&
                   MatchesAssetIdentity(template, templateIdentity) &&
                   MatchesAssetIdentity(profile, profileIdentity) &&
                   MatchesAssetIdentity(activeTemplate, templateIdentity) &&
                   MatchesAssetIdentity(activeProfile, profileIdentity) &&
                   DimensionPortalPresetEditorUtility.IsPresetOwnedByTemplate(
                       template,
                       profile);
        }

        private void CancelPendingTextureUpdate()
        {
            EditorApplication.delayCall -= ProcessPendingTextureUpdate;
            textureUpdateQueued = false;
            pendingTextureUpdate = null;
            EditorApplication.delayCall -= ProcessPendingSwirlTextureUpdate;
            swirlTextureUpdateQueued = false;
            pendingSwirlTextureUpdate = null;
            previewCompositionDirty = true;
            repaintRequested = true;
        }

        private void DrawArtworkOverride(
            DimensionTemplateAsset template,
            SerializedObject serializedProfile,
            StudioLayer layer,
            SerializedProperty property,
            ref DrawResult result)
        {
            if (property == null)
            {
                return;
            }

            GUIContent label = new GUIContent("Artwork override");
            float height = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            Rect fieldRect = EditorGUI.PrefixLabel(rect, label);
            float createButtonSize = Mathf.Min(
                EditorGUIUtility.singleLineHeight,
                Mathf.Max(1f, fieldRect.height));
            Rect createRect = new Rect(
                fieldRect.x,
                fieldRect.y,
                createButtonSize,
                createButtonSize);
            Event current = Event.current;
            bool createRequested =
                current != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 &&
                createRect.Contains(current.mousePosition);
            bool pointerInteraction =
                current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp) &&
                current.button == 0 &&
                fieldRect.Contains(current.mousePosition);
            bool contextReady = true;
            if (pointerInteraction &&
                !DimensionScriptableDataContextUtility.TryScopeToTemplate(
                    template,
                    out string contextError))
            {
                contextReady = false;
                result.Message = contextError;
                result.MessageType = MessageType.Warning;
            }

            if (createRequested)
            {
                current.Use();
                if (contextReady &&
                    (layer == StudioLayer.InnerFlecks ||
                     TryGetArtworkLayer(layer, out _, instantPortalMode)))
                {
                    QueueCreateArtworkVariant(
                        template,
                        serializedProfile.targetObject as DimensionPortalVisualProfileAsset,
                        layer);
                }
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && contextReady;
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none, true);
            bool referenceChanged = EditorGUI.EndChangeCheck();
            GUI.Box(
                createRect,
                new GUIContent(
                    "+",
                    artworkVariantCreateQueued
                        ? "An editable artwork variant is already being created."
                        : layer == StudioLayer.InnerFlecks
                            ? "Create and select an editable copy of the current Swirls SpriteAsset in this portal profile."
                            : "Create and select a new vanilla-derived SpriteAsset in this dimension mod."),
                EditorStyles.miniButton);
            GUI.enabled = previousEnabled;
            if (referenceChanged && layer == StudioLayer.InnerFlecks)
            {
                CancelPendingTextureUpdate();
                InvalidateTextureSlotCache();
                ClearPaletteFocus();
                hasSelectedPixel = false;
                result.Changed = true;
            }
            else if (referenceChanged &&
                     TryGetArtworkLayer(layer, out DimensionPortalArtworkLayer artworkLayer, instantPortalMode))
            {
                CancelPendingTextureUpdate();

                DimensionPortalArtworkEditorUtility.InvalidateReferenceCache();
                InvalidateTextureSlotCache();
                DimensionPortalArtworkEditorUtility.SynchronizePaletteFromReference(
                    serializedProfile,
                    serializedProfile.targetObject as DimensionPortalVisualProfileAsset,
                    artworkLayer);
                ClearPaletteFocus();
                hasSelectedPixel = false;
                result.Changed = true;
            }
        }

        private void QueueCreateArtworkVariant(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            StudioLayer layer)
        {
            if (artworkVariantCreateQueued || template == null || profile == null)
            {
                return;
            }

            CancelPendingTextureUpdate();

            AssetIdentity templateIdentity = CaptureAssetIdentity(template);
            AssetIdentity profileIdentity = CaptureAssetIdentity(profile);
            int requestVersion = ++artworkVariantRequestVersion;
            artworkVariantCreateQueued = true;
            pendingArtworkVariantLayer = layer;
            EditorApplication.delayCall += () =>
            {
                if (disposed)
                {
                    return;
                }

                if (requestVersion != artworkVariantRequestVersion)
                {
                    return;
                }

                artworkVariantCreateQueued = false;
                try
                {
                    if (!IsActiveEditingContextCurrent(
                            template,
                            templateIdentity,
                            profile,
                            profileIdentity))
                    {
                        pendingArtworkMessage =
                            "The pending artwork copy was cancelled because the active Dimension Asset or portal profile changed.";
                        pendingArtworkMessageType = MessageType.Warning;
                    }
                    else if (!DimensionScriptableDataContextUtility.TryScopeToTemplate(
                            template,
                            out string contextError))
                    {
                        pendingArtworkMessage = contextError;
                        pendingArtworkMessageType = MessageType.Warning;
                    }
                    else
                    {
                        string message;
                        bool created;
                        if (layer == StudioLayer.InnerFlecks)
                        {
                            created =
                                DimensionPortalSwirlArtworkEditorUtility.CreateEditableCopy(
                                    template,
                                    profile,
                                    out message);
                        }
                        else if (TryGetArtworkLayer(
                                     layer,
                                     out DimensionPortalArtworkLayer artworkLayer,
                                     instantPortalMode))
                        {
                            created =
                                DimensionPortalArtworkEditorUtility.CreateEditableCopy(
                                    template,
                                    profile,
                                    artworkLayer,
                                    out message);
                        }
                        else
                        {
                            created = false;
                            message = "This portal layer does not support editable artwork copies.";
                        }

                        if (!created)
                        {
                            pendingArtworkMessage = message;
                            pendingArtworkMessageType = MessageType.Error;
                        }
                        else
                        {
                            pendingArtworkMessage = message;
                            pendingArtworkMessageType = MessageType.Info;
                            profileEditSession.MarkChanged();
                            serializedProfileCache = null;
                            serializedProfileTarget = null;
                            hasPaletteSnapshot = false;
                            InvalidateTextureSlotCache();
                            ClearPaletteFocus();
                            hasSelectedPixel = false;
                        }
                    }
                }
                catch (Exception exception)
                {
                    pendingArtworkMessage =
                        "Could not create editable portal artwork: " + exception.Message;
                    pendingArtworkMessageType = MessageType.Error;
                    Debug.LogException(exception);
                }

                repaintRequested = true;
            };
        }

        private void CancelPendingArtworkVariantCreation(StudioLayer layer)
        {
            if (!artworkVariantCreateQueued || pendingArtworkVariantLayer != layer)
            {
                return;
            }

            ++artworkVariantRequestVersion;
            artworkVariantCreateQueued = false;
        }

        private void DrawColorRoleStrip(
            SerializedObject profile,
            ColorRole[] roles,
            bool directOverride)
        {
            if (paletteFocusActive &&
                paletteFocusLayer == selectedLayer &&
                (directOverride ||
                 !CanFocusPaletteRole(
                     selectedLayer,
                     roles,
                     paletteFocusRoleIndex)))
            {
                ClearPaletteFocus();
            }

            Rect strip = GUILayoutUtility.GetRect(100f, 44f, GUILayout.ExpandWidth(true));
            float gap = 3f;
            float width = Mathf.Max(28f, (strip.width - gap * (roles.Length - 1)) / roles.Length);
            int selectedIndex = Mathf.Clamp(
                selectedColorRoleByLayer[(int)selectedLayer],
                0,
                roles.Length - 1);
            for (int i = 0; i < roles.Length; i++)
            {
                Rect swatch = new Rect(strip.x + i * (width + gap), strip.y, width, 44f);
                SerializedProperty property = profile.FindProperty(roles[i].PropertyName);
                Color color = property == null ? Color.magenta : property.colorValue;
                Color display = ToneMapForEditor(color, roles[i].IsHdr);
                bool canFocusPixels = !directOverride &&
                                      CanFocusPaletteRole(selectedLayer, roles, i);
                bool focused = canFocusPixels &&
                               paletteFocusActive &&
                               paletteFocusLayer == selectedLayer &&
                               paletteFocusRoleIndex == i;
                Color borderColor = focused
                    ? new Color(0.1f, 0.88f, 1f, 1f)
                    : i == selectedIndex
                        ? new Color(0.22f, 0.56f, 0.72f, 1f)
                        : new Color(0.16f, 0.17f, 0.2f, 1f);
                EditorGUI.DrawRect(swatch, borderColor);
                Rect inside = new Rect(swatch.x + 2f, swatch.y + 2f, swatch.width - 4f, 25f);
                EditorGUI.DrawRect(inside, display);
                if (directOverride && roles[i].IsPaletteColor)
                {
                    EditorGUI.DrawRect(
                        new Rect(inside.x, inside.center.y, inside.width, 1f),
                        new Color(1f, 1f, 1f, 0.65f));
                }

                Rect labelRect = new Rect(swatch.x, swatch.y + 27f, swatch.width, 15f);
                bool emphasizeLabel = focused || i == selectedIndex;
                EditorGUI.DrawRect(
                    labelRect,
                    emphasizeLabel
                        ? new Color(0.035f, 0.055f, 0.075f, 0.96f)
                        : new Color(0.045f, 0.05f, 0.065f, 0.82f));
                GUI.Label(
                    labelRect,
                    new GUIContent(roles[i].ShortLabel, roles[i].Label),
                    GetSwatchLabelStyle(emphasizeLabel));
                string tooltip = roles[i].Label;
                if (canFocusPixels)
                {
                    tooltip += focused
                        ? "\nClick to clear pixel focus."
                        : "\nClick to highlight these pixels in the portal preview.";
                }

                if (GUI.Button(
                        swatch,
                        new GUIContent(string.Empty, tooltip),
                        GUIStyle.none))
                {
                    selectedColorRoleByLayer[(int)selectedLayer] = i;
                    if (focused)
                    {
                        ClearPaletteFocus();
                    }
                    else if (canFocusPixels)
                    {
                        SetPaletteFocus(selectedLayer, i);
                    }
                    else
                    {
                        ClearPaletteFocus();
                    }

                    GUI.changed = true;
                }
            }
        }

        private void DrawModernColorEditor(SerializedProperty property, ColorRole role)
        {
            if (property == null)
            {
                return;
            }

            GUILayout.Space(5f);
            // No repeated title here — the swatch itself already names the role.
            Color authoredColor = property.colorValue;
            float intensity = role.IsHdr
                ? Mathf.Max(1f, authoredColor.r, authoredColor.g, authoredColor.b)
                : 1f;
            Color normalized = role.IsHdr && intensity > 0f
                ? new Color(
                    authoredColor.r / intensity,
                    authoredColor.g / intensity,
                    authoredColor.b / intensity,
                    authoredColor.a)
                : authoredColor;
            Color.RGBToHSV(
                normalized,
                out float colorHue,
                out float saturation,
                out float value);
            ColorPickerKey pickerKey = GetColorPickerKey(property);
            float hue = ResolvePickerHue(pickerKey, colorHue, saturation);

            EnsureColorPickerTextures(hue);
            Rect pickerRow = GUILayoutUtility.GetRect(100f, 112f, GUILayout.ExpandWidth(true));
            float hueWidth = 18f;
            float previewWidth = 42f;
            Rect svRect = new Rect(
                pickerRow.x,
                pickerRow.y,
                Mathf.Max(100f, pickerRow.width - hueWidth - previewWidth - 14f),
                pickerRow.height);
            Rect hueRect = new Rect(svRect.xMax + 6f, pickerRow.y, hueWidth, pickerRow.height);
            Rect previewRect = new Rect(hueRect.xMax + 8f, pickerRow.y, previewWidth, pickerRow.height);

            GUI.DrawTexture(svRect, saturationValueTexture, ScaleMode.StretchToFill, false);
            GUI.DrawTexture(hueRect, hueTexture, ScaleMode.StretchToFill, false);
            EditorGUI.DrawRect(previewRect, ToneMapForEditor(authoredColor, role.IsHdr));
            DrawOutline(svRect, new Color(0f, 0f, 0f, 0.8f), 1f);
            DrawOutline(hueRect, new Color(0f, 0f, 0f, 0.8f), 1f);
            DrawOutline(previewRect, new Color(0f, 0f, 0f, 0.8f), 1f);

            float markerX = svRect.x + saturation * svRect.width;
            float markerY = svRect.y + (1f - value) * svRect.height;
            DrawCrosshair(new Vector2(markerX, markerY));
            float hueY = Mathf.Lerp(hueRect.yMax - 1f, hueRect.yMin + 1f, hue);
            EditorGUI.DrawRect(new Rect(hueRect.x - 2f, hueY - 1f, hueRect.width + 4f, 2f), Color.white);
            EditorGUI.DrawRect(new Rect(hueRect.x - 1f, hueY, hueRect.width + 2f, 1f), Color.black);

            bool hsvChanged = false;
            if (HandlePickerRect(svRect, 811, out Vector2 svPosition))
            {
                saturation = Mathf.Clamp01(svPosition.x);
                value = Mathf.Clamp01(1f - svPosition.y);
                hsvChanged = true;
            }

            if (HandlePickerRect(hueRect, 812, out Vector2 huePosition, 3f))
            {
                hue = Mathf.Clamp01(1f - huePosition.y);
                retainedHueByColor[pickerKey] = hue;
                hsvChanged = true;
            }

            if (hsvChanged)
            {
                Color changed = Color.HSVToRGB(hue, saturation, value, role.IsHdr);
                changed.r *= intensity;
                changed.g *= intensity;
                changed.b *= intensity;
                changed.a = authoredColor.a;
                if (!ColorsApproximatelyEqual(changed, authoredColor))
                {
                    property.colorValue = changed;
                    GUI.changed = true;
                }

                authoredColor = changed;
                if (saturation > 0.0001f)
                {
                    retainedHueByColor[pickerKey] = hue;
                }

                normalized = NormalizePickerColor(authoredColor, intensity, role.IsHdr);
                repaintRequested = true;
            }

            string currentHex = FormatPickerHex(normalized, authoredColor.a);
            DrawHexColorField(
                property,
                pickerKey,
                intensity,
                role.IsHdr,
                currentHex,
                ref authoredColor);

            float alpha = EditorGUILayout.Slider("Alpha", authoredColor.a, 0f, 1f);
            if (!Mathf.Approximately(alpha, authoredColor.a))
            {
                authoredColor.a = alpha;
                property.colorValue = authoredColor;
            }

            if (role.IsHdr)
            {
                float maxIntensity = Mathf.Max(16f, Mathf.Ceil(intensity));
                float changedIntensity = EditorGUILayout.Slider(
                    "HDR intensity",
                    intensity,
                    0f,
                    maxIntensity);
                if (!Mathf.Approximately(changedIntensity, intensity))
                {
                    Color baseColor = intensity <= 0f
                        ? Color.black
                        : new Color(
                            authoredColor.r / intensity,
                            authoredColor.g / intensity,
                            authoredColor.b / intensity,
                            authoredColor.a);
                    baseColor.r *= changedIntensity;
                    baseColor.g *= changedIntensity;
                    baseColor.b *= changedIntensity;
                    property.colorValue = baseColor;
                }
            }
        }

        private void DrawLayerSpecificSettings(SerializedObject profile)
        {
            switch (selectedLayer)
            {
                case StudioLayer.ChargeSweep:
                    DrawProperty(profile, "chargeWaveSpeed", "Playback speed");
                    break;
                case StudioLayer.Milestones:
                    EditorGUILayout.LabelField("Activation thresholds", EditorStyles.miniBoldLabel);
                    DrawProperty(profile, "firstMilestone", "Bottom pair");
                    DrawProperty(profile, "secondMilestone", "Middle pair");
                    DrawProperty(profile, "thirdMilestone", "Upper pair");
                    break;
                case StudioLayer.Center:
                    DrawProperty(profile, "centerGlowIntensity", "Highlight brightness");
                    break;
                case StudioLayer.InnerFlecks:
                    if (GetBool(profile, "centerSwirlOverrideVanilla", false))
                    {
                        EditorGUILayout.LabelField(
                            "Animation and glow",
                            EditorStyles.miniBoldLabel);
                        DrawProperty(profile, "centerSwirlPlaybackSpeed", "Playback speed");
                        DrawProperty(
                            profile,
                            "centerParticleEmissionMultiplier",
                            "Emission multiplier");
                    }
                    break;
                case StudioLayer.ReadyBurst:
                    EditorGUILayout.LabelField("Burst placement", EditorStyles.miniBoldLabel);
                    DrawProperty(profile, "readyFlashOffsetPixels", "Offset (pixels)");
                    DrawProperty(profile, "readyFlashScale", "Scale");
                    DrawProperty(profile, "readyFlashRotationDegrees", "Rotation");
                    DrawProperty(profile, "readyFlashEmissionMultiplier", "Emission multiplier");
                    DrawProperty(profile, "readyFlashSizeMultiplier", "Size multiplier");
                    break;
                case StudioLayer.GroundLight:
                    EditorGUILayout.HelpBox(
                        "In-game effect. Core Keeper's world light, fog, flicker, and responsive shadows cannot be reproduced exactly in this sprite canvas.",
                        MessageType.None);
                    EditorGUILayout.LabelField("Light placement", EditorStyles.miniBoldLabel);
                    DrawProperty(profile, "groundLightEnabled", "Enabled");
                    DrawProperty(profile, "groundLightOffsetPixels", "Offset (pixels)");
                    DrawProperty(profile, "groundLightIntensity", "Authored intensity");
                    DrawProperty(profile, "groundLightRange", "Range");
                    DrawProperty(profile, "groundLightMinimumIntensity", "Runtime minimum");
                    DrawProperty(profile, "groundLightMaximumIntensity", "Runtime maximum");
                    DrawProperty(profile, "groundLightMovement", "Vanilla movement");
                    DrawProperty(profile, "groundLightCastsShadows", "Responsive shadows");
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("Projected shadow", EditorStyles.miniBoldLabel);
                    DrawProperty(profile, "portalShadowEnabled", "Enabled");
                    DrawProperty(profile, "portalShadowSprite", "Floor shadow sprite");
                    DrawProperty(profile, "portalShadowCasterSprite", "Responsive caster sprite");
                    DrawProperty(profile, "portalShadowScale", "Scale");
                    EditorGUILayout.BeginHorizontal();
                    // Flip X/Y are intentionally not exposed for the shadow either.
                    EditorGUILayout.EndHorizontal();
                    break;
            }
        }

        private void DrawLayerTransformSettings(
            SerializedObject profile,
            StudioLayer layer)
        {
            if (!TryGetLayerTransformPropertyNames(
                    layer,
                    out string visibleName,
                    out string offsetName,
                    out string scaleName,
                    out string rotationName,
                    out string flipXName,
                    out string flipYName))
            {
                return;
            }

            SerializedProperty visible = profile.FindProperty(visibleName);
            SerializedProperty offset = profile.FindProperty(offsetName);
            SerializedProperty scale = profile.FindProperty(scaleName);
            SerializedProperty rotation = profile.FindProperty(rotationName);
            SerializedProperty flipX = profile.FindProperty(flipXName);
            SerializedProperty flipY = profile.FindProperty(flipYName);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Layout", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset layout", EditorStyles.miniButton, GUILayout.Width(82f)))
            {
                Undo.RecordObject(
                    profile.targetObject,
                    "Reset " + GetLayerTitle(layer) + " layout");
                if (visible != null)
                {
                    visible.boolValue = true;
                }

                if (offset != null)
                {
                    offset.vector2Value = Vector2.zero;
                }

                if (scale != null)
                {
                    scale.vector2Value = Vector2.one;
                }

                if (rotation != null)
                {
                    rotation.floatValue = 0f;
                }

                if (flipX != null)
                {
                    flipX.boolValue = false;
                }

                if (flipY != null)
                {
                    flipY.boolValue = false;
                }

                MarkPreviewTransformChanged();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            if (visible != null && layer != StudioLayer.InnerFlecks)
            {
                EditorGUILayout.PropertyField(
                    visible,
                    new GUIContent(
                        "Visible",
                        "Include this visual layer in the generated portal."));
            }

            // Offset X/Y number fields and Rotation are intentionally not exposed: positioning is
            // done with the Nudge buttons / preview dragging below, and rotation is unused by this
            // framework's portal art. Existing serialized values still bake as-is.
            if (scale != null)
            {
                EditorGUILayout.PropertyField(
                    scale,
                    new GUIContent(
                        "Scale",
                        "Independent width and height scale around the SpriteAsset pivot."));
                Vector2 clampedScale = ClampPreviewScale(scale.vector2Value);
                if (scale.vector2Value != clampedScale)
                {
                    scale.vector2Value = clampedScale;
                }
            }

            // Flip X/Y are intentionally not exposed (unused by this framework's portal art);
            // existing serialized flip values still bake as-is.
            if (EditorGUI.EndChangeCheck())
            {
                MarkPreviewTransformChanged();
            }

            if (offset != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Nudge", GUILayout.Width(64f));
                DrawOffsetNudgeButton(offset, Vector2.left, "←");
                DrawOffsetNudgeButton(offset, Vector2.right, "→");
                DrawOffsetNudgeButton(offset, Vector2.down, "↓");
                DrawOffsetNudgeButton(offset, Vector2.up, "↑");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
        }

        private void DrawOffsetNudgeButton(
            SerializedProperty offset,
            Vector2 direction,
            string label)
        {
            if (!GUILayout.Button(label, GUILayout.Width(30f)))
            {
                return;
            }

            Undo.RecordObject(
                offset.serializedObject.targetObject,
                "Nudge " + GetLayerTitle(selectedLayer));
            offset.vector2Value += direction;
            MarkPreviewTransformChanged();
        }

        private static bool TryGetLayerTransformPropertyNames(
            StudioLayer layer,
            out string visible,
            out string offset,
            out string scale,
            out string rotation,
            out string flipX,
            out string flipY)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    visible = "frameVisible";
                    offset = "frameOffsetPixels";
                    scale = "frameScale";
                    rotation = "frameRotationDegrees";
                    flipX = "frameFlipX";
                    flipY = "frameFlipY";
                    return true;
                case StudioLayer.ChargeSweep:
                    visible = "chargeWaveVisible";
                    offset = "chargeWaveOffsetPixels";
                    scale = "chargeWaveScale";
                    rotation = "chargeWaveRotationDegrees";
                    flipX = "chargeWaveFlipX";
                    flipY = "chargeWaveFlipY";
                    return true;
                case StudioLayer.Milestones:
                    visible = "milestonesVisible";
                    offset = "milestoneOffsetPixels";
                    scale = "milestoneScale";
                    rotation = "milestoneRotationDegrees";
                    flipX = "milestoneFlipX";
                    flipY = "milestoneFlipY";
                    return true;
                case StudioLayer.Center:
                    visible = "centerVisible";
                    offset = "centerOffsetPixels";
                    scale = "centerScale";
                    rotation = "centerRotationDegrees";
                    flipX = "centerFlipX";
                    flipY = "centerFlipY";
                    return true;
                case StudioLayer.InnerFlecks:
                    visible = "centerSwirlVisible";
                    offset = "centerParticleOffsetPixels";
                    scale = "centerParticleScale";
                    rotation = "centerParticleRotationDegrees";
                    flipX = "centerSwirlFlipX";
                    flipY = "centerSwirlFlipY";
                    return true;
                default:
                    visible = string.Empty;
                    offset = string.Empty;
                    scale = string.Empty;
                    rotation = string.Empty;
                    flipX = string.Empty;
                    flipY = string.Empty;
                    return false;
            }
        }

        private static void DrawFlecksPaletteControls(SerializedObject profile)
        {
            EditorGUILayout.LabelField("Swirl mode", EditorStyles.miniBoldLabel);
            DrawProperty(profile, "centerSwirlVisible", "Visible");
            DrawProperty(profile, "centerSwirlOverrideVanilla", "Override vanilla");
        }

        private static void DrawReadyBurstPaletteControls(SerializedObject profile)
        {
            EditorGUILayout.LabelField("Ready burst visibility and color", EditorStyles.miniBoldLabel);
            DrawProperty(profile, "playReadyFlash", "Enabled");
            DrawProperty(
                profile,
                "readyFlashFollowsCenterPalette",
                "Follow center palette");
        }

        private static bool IsSelectedParticleTintFollowingCenter(
            SerializedObject profile,
            StudioLayer layer,
            int selectedRoleIndex)
        {
            if (profile == null || selectedRoleIndex != 0)
            {
                return false;
            }

            string propertyName;
            if (layer == StudioLayer.ReadyBurst)
            {
                propertyName = "readyFlashFollowsCenterPalette";
            }
            else
            {
                return false;
            }

            SerializedProperty property = profile.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static void DrawProperty(
            SerializedObject serializedObject,
            string propertyName,
            string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }


        private PreviewSheet GetSourceSheet(string assetPath, int frameCount)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            string cacheKey = assetPath + "|" + safeFrameCount;
            if (sourceSheetsByPath.TryGetValue(cacheKey, out PreviewSheet cached))
            {
                return cached;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            PreviewSheet sheet = GetSourceSheet(texture, safeFrameCount);
            if (sheet != null)
            {
                sourceSheetsByPath[cacheKey] = sheet;
            }

            return sheet;
        }

        private PreviewSheet GetSourceSheet(Texture2D texture, int frameCount)
        {
            if (texture == null)
            {
                return null;
            }

            int safeFrameCount = Mathf.Max(1, frameCount);
            long key = ((long)texture.GetInstanceID() << 32) ^ (uint)safeFrameCount;
            if (sourceSheets.TryGetValue(key, out PreviewSheet cached))
            {
                return cached;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            PreviewSheet sheet = new PreviewSheet
            {
                Texture = texture,
                Width = texture.width,
                Height = texture.height,
                FrameCount = safeFrameCount,
                OwnsTexture = false
            };
            string absolutePath = AssetPathToAbsolutePath(assetPath);
            if (!string.IsNullOrEmpty(absolutePath) &&
                assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(absolutePath))
            {
                Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = texture.name + " (Portal Studio Preview)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (readable.LoadImage(File.ReadAllBytes(absolutePath)))
                {
                    readable.filterMode = FilterMode.Point;
                    readable.wrapMode = TextureWrapMode.Clamp;
                    sheet.Texture = readable;
                    sheet.Width = readable.width;
                    sheet.Height = readable.height;
                    sheet.Pixels = readable.GetPixels32();
                    sheet.OwnsTexture = true;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }
            }

            sourceSheets.Add(key, sheet);
            return sheet;
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color emissiveColor)
        {
            return GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                emissiveColor,
                1);
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color emissiveColor,
            int frameCount)
        {
            return GetFrameCompositeSheet(
                albedoTexture,
                emissiveTexture,
                Color.white,
                emissiveColor,
                frameCount);
        }

        private PreviewSheet GetFrameCompositeSheet(
            Texture2D albedoTexture,
            Texture2D emissiveTexture,
            Color albedoTint,
            Color emissiveColor,
            int frameCount)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            PreviewSheet albedo = GetSourceSheet(albedoTexture, safeFrameCount);
            PreviewSheet emissive = GetSourceSheet(emissiveTexture, safeFrameCount);
            if (albedo == null ||
                emissive == null ||
                albedo.Pixels == null ||
                emissive.Pixels == null ||
                albedo.Width != emissive.Width ||
                albedo.Height != emissive.Height)
            {
                return albedo;
            }

            long key = ((long)albedoTexture.GetInstanceID() << 32) ^
                       (uint)emissiveTexture.GetInstanceID() ^
                       ((long)safeFrameCount << 17);
            if (!frameCompositeSheets.TryGetValue(
                    key,
                    out FrameCompositeCacheEntry cached) ||
                cached == null ||
                cached.Albedo != albedo ||
                cached.Emissive != emissive ||
                cached.Composite == null ||
                cached.Composite.Pixels == null ||
                cached.Composite.Pixels.Length != albedo.Pixels.Length)
            {
                if (cached != null)
                {
                    DestroyOwnedSheet(cached.Composite);
                }

                Texture2D compositeTexture = new Texture2D(
                    albedo.Width,
                    albedo.Height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = albedoTexture.name + " (Portal Studio Lit Preview)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                cached = new FrameCompositeCacheEntry
                {
                    Albedo = albedo,
                    Emissive = emissive,
                    Composite = new PreviewSheet
                    {
                        Texture = compositeTexture,
                        Pixels = new Color32[albedo.Pixels.Length],
                        Width = albedo.Width,
                        Height = albedo.Height,
                        FrameCount = safeFrameCount,
                        OwnsTexture = true
                    },
                    CompositeHash = int.MinValue
                };
                frameCompositeSheets[key] = cached;
            }

            int compositeHash;
            unchecked
            {
                compositeHash = emissiveColor.GetHashCode();
                compositeHash = compositeHash * 397 ^ albedoTint.GetHashCode();
            }
            if (cached.CompositeHash == compositeHash)
            {
                return cached.Composite;
            }

            Color32[] output = cached.Composite.Pixels;
            for (int i = 0; i < output.Length; i++)
            {
                Color sourceColor = albedo.Pixels[i];
                Color baseColor = new Color(
                    sourceColor.r * Mathf.Max(0f, albedoTint.r),
                    sourceColor.g * Mathf.Max(0f, albedoTint.g),
                    sourceColor.b * Mathf.Max(0f, albedoTint.b),
                    sourceColor.a * Mathf.Clamp01(albedoTint.a));
                Color mask = emissive.Pixels[i];
                Color previewEmission = new Color(
                    mask.r * emissiveColor.r,
                    mask.g * emissiveColor.g,
                    mask.b * emissiveColor.b,
                    1f);
                Color composited = ApplyPreviewEmission(baseColor, previewEmission);
                composited.a = baseColor.a;
                output[i] = composited;
            }

            cached.Composite.Texture.SetPixels32(output);
            cached.Composite.Texture.Apply(false, false);
            cached.CompositeHash = compositeHash;
            return cached.Composite;
        }

        private PreviewSheet GetRecoloredSheet(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette,
            Color[] targetPalette)
        {
            return GetRecoloredSheet(
                cacheKey,
                source,
                sourcePalette,
                targetPalette,
                Color.clear);
        }

        private PreviewSheet GetRecoloredSheet(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette,
            Color[] targetPalette,
            Color emissivePreviewColor)
        {
            if (source == null || source.Pixels == null || sourcePalette == null ||
                targetPalette == null || sourcePalette.Length != targetPalette.Length)
            {
                return source;
            }

            int paletteHash = GetPaletteHash(targetPalette);
            unchecked
            {
                paletteHash = paletteHash * 397 ^ emissivePreviewColor.GetHashCode();
            }
            if (recoloredSheets.TryGetValue(cacheKey, out RecolorCacheEntry cached) &&
                cached != null &&
                cached.Source == source &&
                cached.SourcePalette == sourcePalette &&
                cached.PaletteHash == paletteHash &&
                cached.Recolored != null)
            {
                return cached.Recolored;
            }

            bool canReuse = cached != null &&
                            cached.Source == source &&
                            cached.SourcePalette == sourcePalette &&
                            cached.SourcePaletteIndices != null &&
                            cached.SourcePaletteIndices.Length == source.Pixels.Length &&
                            cached.Recolored != null &&
                            cached.Recolored.Texture != null &&
                            cached.Recolored.Pixels != null &&
                            cached.Recolored.Pixels.Length == source.Pixels.Length;
            if (!canReuse && cached != null)
            {
                DestroyOwnedSheet(cached.Recolored);
            }

            if (!canReuse)
            {
                cached = CreateRecolorCacheEntry(cacheKey, source, sourcePalette);
                recoloredSheets[cacheKey] = cached;
            }

            Color32[] recoloredPixels = cached.Recolored.Pixels;
            for (int i = 0; i < source.Pixels.Length; i++)
            {
                Color32 sourcePixel = source.Pixels[i];
                int paletteIndex = cached.SourcePaletteIndices[i];
                if (paletteIndex == byte.MaxValue)
                {
                    recoloredPixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Color targetColor = ApplyPreviewEmission(
                    targetPalette[paletteIndex],
                    emissivePreviewColor);
                Color32 targetPixel = targetColor;
                targetPixel.a = (byte)Mathf.Clamp(
                    Mathf.RoundToInt(sourcePixel.a * Mathf.Clamp01(targetColor.a)),
                    0,
                    255);
                recoloredPixels[i] = targetPixel;
            }

            cached.Recolored.Texture.SetPixels32(recoloredPixels);
            cached.Recolored.Texture.Apply(false, false);
            cached.PaletteHash = paletteHash;
            return cached.Recolored;
        }

        private static Color ApplyPreviewEmission(Color baseColor, Color emissiveColor)
        {
            // The Studio cannot reproduce Core Keeper's HDR bloom, but it should show
            // the large vanilla distinction between the subtle charging emission and
            // the much stronger persistent load-point emission. This bounded screen
            // response leaves zero-emission pixels untouched and never clips to white.
            return new Color(
                1f - (1f - Mathf.Clamp01(baseColor.r)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.r) * PortalPreviewEmissionExposure),
                1f - (1f - Mathf.Clamp01(baseColor.g)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.g) * PortalPreviewEmissionExposure),
                1f - (1f - Mathf.Clamp01(baseColor.b)) *
                Mathf.Exp(-Mathf.Max(0f, emissiveColor.b) * PortalPreviewEmissionExposure),
                baseColor.a);
        }

        private static RecolorCacheEntry CreateRecolorCacheEntry(
            string cacheKey,
            PreviewSheet source,
            Color32[] sourcePalette)
        {
            int pixelCount = source.Pixels.Length;
            Color32[] recoloredPixels = new Color32[pixelCount];
            byte[] paletteIndices = new byte[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                Color32 sourcePixel = source.Pixels[i];
                paletteIndices[i] = sourcePixel.a == 0
                    ? byte.MaxValue
                    : (byte)FindClosestPaletteIndex(sourcePixel, sourcePalette);
            }

            Texture2D recoloredTexture = new Texture2D(
                source.Width,
                source.Height,
                TextureFormat.RGBA32,
                false)
            {
                name = cacheKey + " (Portal Studio Palette)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            PreviewSheet recolored = new PreviewSheet
            {
                Texture = recoloredTexture,
                Pixels = recoloredPixels,
                Width = source.Width,
                Height = source.Height,
                FrameCount = source.FrameCount,
                OwnsTexture = true
            };
            return new RecolorCacheEntry
            {
                Source = source,
                SourcePalette = sourcePalette,
                Recolored = recolored,
                SourcePaletteIndices = paletteIndices,
                PaletteHash = int.MinValue
            };
        }

        private AnimationSheet GetAnimationSheet(
            SpriteAsset asset,
            int animationIndex,
            int fallbackFrames,
            float fallbackFps)
        {
            if (asset == null || asset.animationCount <= 0)
            {
                return default(AnimationSheet);
            }

            if (animationIndex < 0 || animationIndex >= asset.animationCount)
            {
                AddPreviewError(
                    asset.name + " does not contain animation index " + animationIndex +
                    " (available: 0-" + (asset.animationCount - 1) + ").");
                return default(AnimationSheet);
            }

            FrameAnimation animation = asset.GetAnimationAt(animationIndex);
            if (animation == null || animation.spriteData == null)
            {
                AddPreviewError(asset.name + " animation " + animationIndex + " has no SpriteData.");
                return default(AnimationSheet);
            }

            int frameCount = Mathf.Max(1, animation.srcFrameCount > 0
                ? animation.srcFrameCount
                : fallbackFrames);
            Texture2D sourceTexture = animation.spriteData.GetSrcTexture();
            if (sourceTexture == null)
            {
                AddPreviewError(asset.name + " animation " + animationIndex + " has no source texture.");
                return default(AnimationSheet);
            }

            float fps = animation.fps > 0f ? animation.fps : fallbackFps;
            Vector2 pivot = GetEffectiveAnimationPivot(asset, animationIndex, animation);
            int signature = GetAnimationSignature(
                animation,
                sourceTexture,
                frameCount,
                fps,
                pivot);
            AnimationCacheKey cacheKey = new AnimationCacheKey
            {
                AssetInstanceId = asset.GetInstanceID(),
                AnimationIndex = animationIndex,
                FallbackFrames = fallbackFrames,
                FallbackFps = fallbackFps
            };
            if (animationSheets.TryGetValue(cacheKey, out AnimationCacheEntry cached) &&
                cached != null &&
                ReferenceEquals(cached.Animation, animation) &&
                cached.Signature == signature &&
                cached.Sheet.Sheet != null)
            {
                return cached.Sheet;
            }

            AnimationSheet sheet = new AnimationSheet
            {
                Sheet = GetSourceSheet(sourceTexture, frameCount),
                FrameCount = frameCount,
                Fps = fps,
                Loop = animation.loop,
                Pivot = pivot,
                Animation = animation,
                RuntimeFrameRemap = BuildRuntimeFrameRemap(animation, frameCount),
                SourceLabel = asset.name + " / animation " + animationIndex
            };
            animationSheets[cacheKey] = new AnimationCacheEntry
            {
                Animation = animation,
                Signature = signature,
                Sheet = sheet
            };
            return sheet;
        }

        private static int GetAnimationSignature(
            FrameAnimation animation,
            Texture2D sourceTexture,
            int frameCount,
            float fps,
            Vector2 pivot)
        {
            unchecked
            {
                int hash = sourceTexture == null ? 0 : sourceTexture.GetInstanceID();
                hash = hash * 397 ^ frameCount;
                hash = hash * 397 ^ fps.GetHashCode();
                hash = hash * 397 ^ (animation != null && animation.loop ? 1 : 0);
                hash = hash * 397 ^ pivot.GetHashCode();
                if (animation == null)
                {
                    return hash;
                }

                if (animation.runtimeFrameRemap != null &&
                    animation.runtimeFrameRemap.Length > 0)
                {
                    hash = hash * 397 ^ animation.runtimeFrameRemap.Length;
                    for (int i = 0; i < animation.runtimeFrameRemap.Length; i++)
                    {
                        hash = hash * 397 ^ animation.runtimeFrameRemap[i];
                    }

                    return hash;
                }

                int frameDataCount = animation.frameData == null
                    ? 0
                    : animation.frameData.Count;
                hash = hash * 397 ^ frameDataCount;
                for (int i = 0; i < frameDataCount; i++)
                {
                    hash = hash * 397 ^ animation.frameData[i].holdFrames;
                }

                return hash;
            }
        }

        private static int[] BuildRuntimeFrameRemap(FrameAnimation animation, int frameCount)
        {
            if (animation != null &&
                animation.runtimeFrameRemap != null &&
                animation.runtimeFrameRemap.Length > 0)
            {
                return animation.runtimeFrameRemap;
            }

            int sourceFrameCount = Mathf.Max(1, frameCount);
            bool hasHeldFrames = false;
            int runtimeFrameCount = sourceFrameCount;
            for (int sourceFrame = 0; sourceFrame < sourceFrameCount; sourceFrame++)
            {
                int holdFrames = animation != null &&
                                 animation.frameData != null &&
                                 sourceFrame < animation.frameData.Count
                    ? Mathf.Max(0, animation.frameData[sourceFrame].holdFrames)
                    : 0;
                hasHeldFrames |= holdFrames > 0;
                runtimeFrameCount += holdFrames;
            }

            if (!hasHeldFrames)
            {
                return null;
            }

            List<int> remap = new List<int>(runtimeFrameCount);
            for (int sourceFrame = 0; sourceFrame < sourceFrameCount; sourceFrame++)
            {
                int holdFrames = animation != null &&
                                 animation.frameData != null &&
                                 sourceFrame < animation.frameData.Count
                    ? Mathf.Max(0, animation.frameData[sourceFrame].holdFrames)
                    : 0;
                for (int repeat = 0; repeat <= holdFrames; repeat++)
                {
                    remap.Add(sourceFrame);
                }
            }

            return remap.ToArray();
        }

        private static Vector2 GetEffectiveAnimationPivot(
            SpriteAsset asset,
            int animationIndex,
            FrameAnimation animation)
        {
            if (animation == null || animation.spriteData == null)
            {
                return new Vector2(0.5f, 0.5f);
            }

            SpriteData data = animation.spriteData;
            if (!data.inheritPivot)
            {
                return data.pivot;
            }

            if (asset.staticSpriteData != null && asset.staticSpriteData.hasAnyTexture)
            {
                return asset.staticSpriteData.pivot;
            }

            for (int i = 0; i < Mathf.Min(animationIndex, asset.animationCount); i++)
            {
                FrameAnimation previous = asset.GetAnimationAt(i);
                if (previous != null && previous.hasAnyTexture && previous.spriteData != null)
                {
                    return previous.spriteData.pivot;
                }
            }

            return data.pivot;
        }

        private static ColorPickerKey GetColorPickerKey(SerializedProperty property)
        {
            UnityEngine.Object target = property == null || property.serializedObject == null
                ? null
                : property.serializedObject.targetObject;
            return new ColorPickerKey
            {
                TargetInstanceId = target == null ? 0 : target.GetInstanceID(),
                PropertyPath = property == null ? string.Empty : property.propertyPath
            };
        }

        private float ResolvePickerHue(
            ColorPickerKey key,
            float colorHue,
            float saturation)
        {
            if (saturation > PickerHueSaturationEpsilon)
            {
                retainedHueByColor[key] = colorHue;
                return colorHue;
            }

            if (retainedHueByColor.TryGetValue(key, out float retainedHue))
            {
                return retainedHue;
            }

            retainedHueByColor[key] = colorHue;
            return colorHue;
        }

        private static Color NormalizePickerColor(
            Color authoredColor,
            float intensity,
            bool isHdr)
        {
            if (!isHdr || intensity <= 0f)
            {
                return authoredColor;
            }

            return new Color(
                authoredColor.r / intensity,
                authoredColor.g / intensity,
                authoredColor.b / intensity,
                authoredColor.a);
        }

        private static string FormatPickerHex(Color normalized, float alpha)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(new Color(
                Mathf.Clamp01(normalized.r),
                Mathf.Clamp01(normalized.g),
                Mathf.Clamp01(normalized.b),
                Mathf.Clamp01(alpha)));
        }

        private void DrawHexColorField(
            SerializedProperty property,
            ColorPickerKey pickerKey,
            float intensity,
            bool isHdr,
            string currentHex,
            ref Color authoredColor)
        {
            bool keyChanged = !hasHexEditKey || !hexEditKey.Equals(pickerKey);
            if (keyChanged)
            {
                hexEditKey = pickerKey;
                hasHexEditKey = true;
                hexEditWasFocused = false;
                hexEditBuffer = currentHex;
            }

            bool focusedBefore = string.Equals(
                GUI.GetNameOfFocusedControl(),
                HexColorFieldControlName,
                StringComparison.Ordinal);
            if (!focusedBefore && !hexEditWasFocused && !keyChanged)
            {
                hexEditBuffer = currentHex;
            }

            Event current = Event.current;
            bool submitKey = focusedBefore &&
                             current != null &&
                             current.type == EventType.KeyDown &&
                             (current.keyCode == KeyCode.Return ||
                              current.keyCode == KeyCode.KeypadEnter);

            GUI.SetNextControlName(HexColorFieldControlName);
            bool guiChangedBefore = GUI.changed;
            EditorGUI.BeginChangeCheck();
            string editedHex = EditorGUILayout.TextField("Hex", hexEditBuffer);
            bool textChanged = EditorGUI.EndChangeCheck();
            GUI.changed = guiChangedBefore;
            if (textChanged)
            {
                hexEditBuffer = editedHex;
            }

            bool focusedAfter = string.Equals(
                GUI.GetNameOfFocusedControl(),
                HexColorFieldControlName,
                StringComparison.Ordinal);
            bool lostFocus = hexEditWasFocused && !focusedAfter;
            bool commitImmediately = textChanged && IsCompletePickerHex(hexEditBuffer);
            bool shouldCommit = commitImmediately || submitKey || lostFocus;
            if (shouldCommit && TryParsePickerHex(hexEditBuffer, out Color parsed))
            {
                Color.RGBToHSV(
                    parsed,
                    out float parsedHue,
                    out float parsedSaturation,
                    out _);
                if (parsedSaturation > PickerHueSaturationEpsilon)
                {
                    retainedHueByColor[pickerKey] = parsedHue;
                }

                parsed.r *= intensity;
                parsed.g *= intensity;
                parsed.b *= intensity;
                if (!ColorsApproximatelyEqual(parsed, authoredColor))
                {
                    property.colorValue = parsed;
                    authoredColor = parsed;
                    GUI.changed = true;
                    repaintRequested = true;
                }

                if (!focusedAfter || submitKey)
                {
                    Color normalized = NormalizePickerColor(authoredColor, intensity, isHdr);
                    hexEditBuffer = FormatPickerHex(normalized, authoredColor.a);
                }
            }
            else if (lostFocus)
            {
                hexEditBuffer = currentHex;
            }

            if (submitKey)
            {
                GUI.FocusControl(null);
                focusedAfter = false;
                current.Use();
            }

            hexEditWasFocused = focusedAfter;
        }

        private static bool IsCompletePickerHex(string value)
        {
            string trimmed = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            int digitCount = trimmed.StartsWith("#", StringComparison.Ordinal)
                ? trimmed.Length - 1
                : trimmed.Length;
            return digitCount == 6 || digitCount == 8;
        }

        private static bool TryParsePickerHex(string value, out Color parsed)
        {
            string trimmed = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = "#" + trimmed;
            }

            return ColorUtility.TryParseHtmlString(trimmed, out parsed);
        }

        private static bool ColorsApproximatelyEqual(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= 0.000001f &&
                   Mathf.Abs(left.g - right.g) <= 0.000001f &&
                   Mathf.Abs(left.b - right.b) <= 0.000001f &&
                   Mathf.Abs(left.a - right.a) <= 0.000001f;
        }

        private void EnsureColorPickerTextures(float hue)
        {
            if (hueTexture == null)
            {
                hueTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false)
                {
                    name = "Portal Studio Hue",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                Color32[] huePixels = new Color32[128];
                for (int y = 0; y < huePixels.Length; y++)
                {
                    huePixels[y] = Color.HSVToRGB(y / 127f, 1f, 1f);
                }

                hueTexture.SetPixels32(huePixels);
                hueTexture.Apply(false, true);
            }

            if (saturationValueTexture != null &&
                Mathf.Abs(Mathf.DeltaAngle(saturationValueHue * 360f, hue * 360f)) < 0.5f)
            {
                return;
            }

            const int width = 128;
            const int height = 96;
            if (saturationValueTexture == null)
            {
                saturationValueTexture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "Portal Studio Saturation Value",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            int pixelCount = width * height;
            if (saturationValuePixels == null ||
                saturationValuePixels.Length != pixelCount)
            {
                saturationValuePixels = new Color32[pixelCount];
            }

            for (int y = 0; y < height; y++)
            {
                float value = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float saturation = x / (float)(width - 1);
                    saturationValuePixels[y * width + x] =
                        Color.HSVToRGB(hue, saturation, value);
                }
            }

            saturationValueTexture.SetPixels32(saturationValuePixels);
            saturationValueTexture.Apply(false, false);
            saturationValueHue = hue;
        }

        private bool HandlePickerRect(
            Rect rect,
            int hint,
            out Vector2 normalized,
            float hitPadding = 0f)
        {
            normalized = Vector2.zero;
            Rect hitRect = rect;
            if (hitPadding > 0f)
            {
                hitRect.xMin -= hitPadding;
                hitRect.xMax += hitPadding;
                hitRect.yMin -= hitPadding;
                hitRect.yMax += hitPadding;
            }

            int controlId = GUIUtility.GetControlID(hint, FocusType.Passive, hitRect);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button != 0 ||
                        current.mousePosition.x < hitRect.xMin ||
                        current.mousePosition.x > hitRect.xMax ||
                        current.mousePosition.y < hitRect.yMin ||
                        current.mousePosition.y > hitRect.yMax)
                    {
                        return false;
                    }

                    Undo.IncrementCurrentGroup();
                    colorUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Edit portal color");
                    GUI.FocusControl(null);
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return false;
                    }

                    current.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return false;
                    }

                    GUIUtility.hotControl = 0;
                    collapseColorUndoAfterApply = true;
                    current.Use();
                    break;
                default:
                    return false;
            }

            normalized = new Vector2(
                Mathf.Clamp01((current.mousePosition.x - rect.x) / rect.width),
                Mathf.Clamp01((current.mousePosition.y - rect.y) / rect.height));
            return true;
        }

        private static void DrawCrosshair(Vector2 position)
        {
            EditorGUI.DrawRect(new Rect(position.x - 5f, position.y - 1f, 10f, 2f), Color.black);
            EditorGUI.DrawRect(new Rect(position.x - 1f, position.y - 5f, 2f, 10f), Color.black);
            EditorGUI.DrawRect(new Rect(position.x - 4f, position.y, 8f, 1f), Color.white);
            EditorGUI.DrawRect(new Rect(position.x, position.y - 4f, 1f, 8f), Color.white);
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private Rect CanvasRectToGuiRect(Rect canvasRect)
        {
            return new Rect(
                (canvasRect.x - activeViewBounds.x) * activeZoom,
                (activeViewBounds.yMax - canvasRect.y - canvasRect.height) * activeZoom,
                canvasRect.width * activeZoom,
                canvasRect.height * activeZoom);
        }

        private static int GetLoopedFrame(float time, float fps, int frameCount)
        {
            if (frameCount <= 1 || fps <= 0f)
            {
                return 0;
            }

            int frame = Mathf.FloorToInt(Mathf.Max(0f, time) * fps);
            return frame % frameCount;
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

        private static int GetPaletteHash(Color[] colors)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < colors.Length; i++)
                {
                    hash = hash * 31 + colors[i].GetHashCode();
                }

                return hash;
            }
        }

        private Color[] GetPalette(SerializedObject profile, string[] propertyNames)
        {
            Color[] colors = propertyNames.Length == centerPaletteBuffer.Length
                ? centerPaletteBuffer
                : effectPaletteBuffer;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                colors[i] = GetColor(profile, propertyNames[i], Color.white);
            }

            return colors;
        }

        private GUIStyle GetLayerButtonStyle()
        {
            if (layerButtonStyle == null)
            {
                layerButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 42f,
                    richText = true,
                    padding = new RectOffset(6, 30, 2, 2)
                };
            }

            return layerButtonStyle;
        }

        private GUIStyle GetLayerChipStyle()
        {
            if (layerChipStyle == null)
            {
                GUIStyle source = EditorStyles.toolbarButton;
                layerChipStyle = new GUIStyle(source)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(
                        source.padding.left,
                        20,
                        source.padding.top,
                        source.padding.bottom)
                };
            }

            return layerChipStyle;
        }

        private GUIStyle GetSwatchLabelStyle(bool emphasized)
        {
            GUIStyle style = emphasized ? selectedSwatchLabelStyle : swatchLabelStyle;
            if (style != null)
            {
                return style;
            }

            Color textColor = emphasized
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(0.82f, 0.86f, 0.9f, 1f);
            style = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 8,
                fontStyle = emphasized ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            SetStyleTextColor(style, textColor);

            if (emphasized)
            {
                selectedSwatchLabelStyle = style;
            }
            else
            {
                swatchLabelStyle = style;
            }

            return style;
        }

        private static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private Texture2D GetLayerPinIcon(bool filled)
        {
            if (filled)
            {
                if (pinnedLayerIconTexture == null)
                {
                    pinnedLayerIconTexture = CreateLayerPinIcon(true);
                }

                return pinnedLayerIconTexture;
            }

            if (unpinnedLayerIconTexture == null)
            {
                unpinnedLayerIconTexture = CreateLayerPinIcon(false);
            }

            return unpinnedLayerIconTexture;
        }

        private static Texture2D CreateLayerPinIcon(bool filled)
        {
            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = filled ? "PortalLayerPinFilled" : "PortalLayerPinOutline",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!IsLayerPinPixel(x, y))
                    {
                        continue;
                    }

                    bool border = !IsLayerPinPixel(x - 1, y) ||
                                  !IsLayerPinPixel(x + 1, y) ||
                                  !IsLayerPinPixel(x, y - 1) ||
                                  !IsLayerPinPixel(x, y + 1);
                    if (filled || border)
                    {
                        pixels[y * size + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool IsLayerPinPixel(int x, int y)
        {
            if (x < 0 || x >= 16 || y < 0 || y >= 16)
            {
                return false;
            }

            if (y >= 11 && y <= 13 && x >= 4 && x <= 11)
            {
                return true;
            }

            if (y >= 7 && y <= 11 && x >= 5 && x <= 10)
            {
                return true;
            }

            if (y >= 6 && y <= 7 && x >= 3 && x <= 12)
            {
                return true;
            }

            if (y >= 2 && y <= 5 && (x == 7 || x == 8))
            {
                return true;
            }

            return y == 1 && x == 7;
        }

        private static Color GetColor(
            SerializedObject profile,
            string propertyName,
            Color fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.colorValue;
        }

        private static float GetFloat(
            SerializedObject profile,
            string propertyName,
            float fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.floatValue;
        }

        private static Vector2 GetVector2(
            SerializedObject profile,
            string propertyName,
            Vector2 fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.vector2Value;
        }

        private static bool GetBool(
            SerializedObject profile,
            string propertyName,
            bool fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.boolValue;
        }

        private static int GetInt(
            SerializedObject profile,
            string propertyName,
            int fallback)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            return property == null ? fallback : property.intValue;
        }

        private static Color ToneMapForEditor(Color color, bool hdr)
        {
            if (!hdr)
            {
                return new Color(
                    Mathf.Clamp01(color.r),
                    Mathf.Clamp01(color.g),
                    Mathf.Clamp01(color.b),
                    1f);
            }

            return new Color(
                color.r / (1f + Mathf.Max(0f, color.r)),
                color.g / (1f + Mathf.Max(0f, color.g)),
                color.b / (1f + Mathf.Max(0f, color.b)),
                1f);
        }

        private static string GetOverridePropertyName(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return "portalFrameSpriteAsset";
                case StudioLayer.ChargeSweep:
                    return "chargeWaveSpriteAsset";
                case StudioLayer.Milestones:
                    return "milestoneSpriteAsset";
                case StudioLayer.Center:
                    return "centerEffectSpriteAsset";
                default:
                    return string.Empty;
            }
        }

        private static bool TryGetArtworkLayer(
            StudioLayer layer,
            out DimensionPortalArtworkLayer artworkLayer,
            bool instantPortal = false)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    artworkLayer = DimensionPortalArtworkLayer.Frame;
                    return true;
                case StudioLayer.ChargeSweep:
                    artworkLayer = DimensionPortalArtworkLayer.ChargeSweep;
                    return true;
                case StudioLayer.Milestones:
                    artworkLayer = DimensionPortalArtworkLayer.Milestones;
                    return true;
                case StudioLayer.Center:
                    artworkLayer = instantPortal
                        ? DimensionPortalArtworkLayer.CenterInstant
                        : DimensionPortalArtworkLayer.Center;
                    return true;
                default:
                    artworkLayer = default(DimensionPortalArtworkLayer);
                    return false;
            }
        }

        private static ColorRole[] GetColorRoles(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return FrameColorRoles;
                case StudioLayer.ChargeSweep:
                    return ChargeColorRoles;
                case StudioLayer.Milestones:
                    return MilestoneColorRoles;
                case StudioLayer.Center:
                    return CenterColorRoles;
                case StudioLayer.InnerFlecks:
                    return FlecksColorRoles;
                case StudioLayer.ReadyBurst:
                    return ReadyBurstColorRoles;
                case StudioLayer.GroundLight:
                    return LightColorRoles;
                default:
                    return new ColorRole[0];
            }
        }

        private static string GetLayerTitle(StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    return "Portal frame";
                case StudioLayer.ChargeSweep:
                    return "Continuous charging sweep";
                case StudioLayer.Milestones:
                    return "Persistent milestone blobs";
                case StudioLayer.Center:
                    return "Activated center ring";
                case StudioLayer.InnerFlecks:
                    return "Inner swirls";
                case StudioLayer.ReadyBurst:
                    return "Ready activation burst";
                case StudioLayer.GroundLight:
                    return "Projected portal light";
                default:
                    return "Portal layer";
            }
        }

        internal static bool RestoreLayerToVanilla(
            SerializedObject profile,
            StudioLayer layer,
            out string message,
            bool instantPortal = false)
        {
            message = string.Empty;
            if (profile == null ||
                !(profile.targetObject is DimensionPortalVisualProfileAsset))
            {
                message = "The active portal profile is required.";
                return false;
            }

            RestoreLayerDefaults(profile, layer);
            if (TryGetArtworkLayer(
                    layer,
                    out DimensionPortalArtworkLayer artworkLayer,
                    instantPortal))
            {
                if (!DimensionPortalArtworkEditorUtility.AssignFrameworkReference(
                        profile,
                        artworkLayer))
                {
                    message = "The framework " + GetLayerTitle(layer) +
                              " SpriteAsset could not be restored.";
                    return false;
                }
            }
            else if (layer == StudioLayer.InnerFlecks &&
                     !DimensionPortalSwirlArtworkEditorUtility.AssignFrameworkReference(
                         profile,
                         out message))
            {
                return false;
            }

            return true;
        }

        private static void RestoreLayerDefaults(
            SerializedObject profile,
            StudioLayer layer)
        {
            switch (layer)
            {
                case StudioLayer.Frame:
                    SetColor(profile, "frameTint", Color.white);
                    SetColor(profile, "frameEmissiveColor", DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor);
                    SetBool(profile, "frameVisible", true);
                    SetVector2(profile, "frameOffsetPixels", Vector2.zero);
                    SetVector2(profile, "frameScale", Vector2.one);
                    SetFloat(profile, "frameRotationDegrees", 0f);
                    SetBool(profile, "frameFlipX", false);
                    SetBool(profile, "frameFlipY", false);
                    break;
                case StudioLayer.ChargeSweep:
                    SetEffectPalette(profile, "chargeWave");
                    SetColor(profile, "chargeWaveTint", Color.white);
                    SetColor(profile, "chargeWaveEmissiveColor", DimensionPortalVisualProfileAsset.VanillaPortalShaderEmissiveColor);
                    SetFloat(profile, "chargeWaveSpeed", 1f);
                    SetBool(profile, "chargeWaveVisible", true);
                    SetVector2(profile, "chargeWaveOffsetPixels", Vector2.zero);
                    SetVector2(profile, "chargeWaveScale", Vector2.one);
                    SetFloat(profile, "chargeWaveRotationDegrees", 0f);
                    SetBool(profile, "chargeWaveFlipX", false);
                    SetBool(profile, "chargeWaveFlipY", false);
                    break;
                case StudioLayer.Milestones:
                    SetEffectPalette(profile, "milestone");
                    SetColor(profile, "milestoneTint", Color.white);
                    SetColor(profile, "milestoneEmissiveColor", DimensionPortalVisualProfileAsset.VanillaLoadPointEmissiveColor);
                    SetFloat(profile, "firstMilestone", 0.25f);
                    SetFloat(profile, "secondMilestone", 0.5f);
                    SetFloat(profile, "thirdMilestone", 0.75f);
                    SetBool(profile, "milestonesVisible", true);
                    SetVector2(profile, "milestoneOffsetPixels", Vector2.zero);
                    SetVector2(profile, "milestoneScale", Vector2.one);
                    SetFloat(profile, "milestoneRotationDegrees", 0f);
                    SetBool(profile, "milestoneFlipX", false);
                    SetBool(profile, "milestoneFlipY", false);
                    break;
                case StudioLayer.Center:
                    SetColor(profile, "centerDarkColor", DimensionPortalVisualProfileAsset.VanillaPaletteDark);
                    SetColor(profile, "centerDeepColor", DimensionPortalVisualProfileAsset.VanillaCenterPaletteDeep);
                    SetColor(profile, "centerMidColor", DimensionPortalVisualProfileAsset.VanillaPaletteDeep);
                    SetColor(profile, "centerBrightColor", DimensionPortalVisualProfileAsset.VanillaPaletteMid);
                    SetColor(profile, "centerCoreColor", DimensionPortalVisualProfileAsset.VanillaPaletteCore);
                    SetColor(profile, "centerHighlightColor", Color.white);
                    SetColor(profile, "centerTint", DimensionPortalVisualProfileAsset.VanillaCenterTint);
                    SetColor(profile, "centerEmissiveColor", DimensionPortalVisualProfileAsset.VanillaCenterEmissiveColor);
                    SetBool(profile, "centerVisible", true);
                    SetVector2(profile, "centerOffsetPixels", Vector2.zero);
                    SetVector2(profile, "centerScale", Vector2.one);
                    SetFloat(profile, "centerRotationDegrees", 0f);
                    SetBool(profile, "centerFlipX", false);
                    SetBool(profile, "centerFlipY", false);
                    SetFloat(profile, "centerGlowIntensity", DimensionPortalVisualProfileAsset.VanillaCenterGlowIntensity);
                    break;
                case StudioLayer.InnerFlecks:
                    SetBool(profile, "centerParticlesEnabled", true);
                    SetBool(profile, "centerSwirlOverrideVanilla", false);
                    SetBool(profile, "centerSwirlVisible", true);
                    SetBool(profile, "centerParticlesFollowCenterPalette", false);
                    SetColor(
                        profile,
                        "centerParticleTint",
                        DimensionPortalVisualProfileAsset.VanillaSwirlTint);
                    SetObjectReference(profile, "centerParticleSprite", null);
                    SetObjectReference(profile, "centerParticleTexture", null);
                    SetFloat(profile, "centerParticleEmissionMultiplier", 1f);
                    SetFloat(profile, "centerParticleSizeMultiplier", 1f);
                    SetFloat(profile, "centerParticleLifetimeMultiplier", 1f);
                    SetFloat(profile, "centerParticleOrbitSpeedMultiplier", 1f);
                    SetFloat(profile, "centerParticleRadialSpeedMultiplier", 1f);
                    SetFloat(profile, "centerParticleRadiusMultiplier", 1f);
                    SetBool(profile, "centerParticleTrailsEnabled", true);
                    SetFloat(profile, "centerParticleTrailLifetimeMultiplier", 1f);
                    SetVector2(profile, "centerParticleOffsetPixels", Vector2.zero);
                    SetVector2(profile, "centerParticleScale", Vector2.one);
                    SetFloat(profile, "centerParticleRotationDegrees", 0f);
                    SetBool(profile, "centerSwirlFlipX", false);
                    SetBool(profile, "centerSwirlFlipY", false);
                    SetFloat(profile, "centerSwirlPlaybackSpeed", 1f);
                    SetColor(profile, "centerSwirlEmissiveColor", Color.white);
                    break;
                case StudioLayer.ReadyBurst:
                    SetBool(profile, "playReadyFlash", true);
                    SetBool(profile, "readyFlashFollowsCenterPalette", true);
                    SetColor(profile, "readyFlashTint", Color.white);
                    SetArraySize(profile, "readyFlashSprites", 0);
                    SetObjectReference(profile, "readyFlashTexture", null);
                    SetFloat(profile, "readyFlashEmissionMultiplier", 1f);
                    SetFloat(profile, "readyFlashSizeMultiplier", 1f);
                    SetVector2(profile, "readyFlashOffsetPixels", Vector2.zero);
                    SetVector2(profile, "readyFlashScale", Vector2.one);
                    SetFloat(profile, "readyFlashRotationDegrees", 0f);
                    break;
                case StudioLayer.GroundLight:
                    SetBool(profile, "groundLightEnabled", true);
                    SetColor(profile, "groundLightColor", DimensionPortalVisualProfileAsset.VanillaGroundLightColor);
                    SetFloat(profile, "groundLightIntensity", 0.65f);
                    SetFloat(profile, "groundLightRange", 5f);
                    SetFloat(profile, "groundLightMinimumIntensity", 0.3f);
                    SetFloat(profile, "groundLightMaximumIntensity", 0.3f);
                    SetBool(profile, "groundLightMovement", true);
                    SetBool(profile, "groundLightCastsShadows", true);
                    SetVector2(profile, "groundLightOffsetPixels", Vector2.zero);
                    SetBool(profile, "portalShadowEnabled", true);
                    SetObjectReference(profile, "portalShadowSprite", null);
                    SetObjectReference(profile, "portalShadowCasterSprite", null);
                    SetVector2(profile, "portalShadowOffsetPixels", Vector2.zero);
                    SetVector2(profile, "portalShadowScale", Vector2.one);
                    SetFloat(profile, "portalShadowRotationDegrees", 0f);
                    SetBool(profile, "portalShadowFlipX", false);
                    SetBool(profile, "portalShadowFlipY", false);
                    break;
            }
        }

        private static void SetEffectPalette(SerializedObject profile, string prefix)
        {
            SetColor(profile, prefix + "DarkColor", DimensionPortalVisualProfileAsset.VanillaPaletteDark);
            SetColor(profile, prefix + "DeepColor", DimensionPortalVisualProfileAsset.VanillaPaletteDeep);
            SetColor(profile, prefix + "MidColor", DimensionPortalVisualProfileAsset.VanillaPaletteMid);
            SetColor(profile, prefix + "BrightColor", DimensionPortalVisualProfileAsset.VanillaPaletteBright);
            SetColor(profile, prefix + "CoreColor", DimensionPortalVisualProfileAsset.VanillaPaletteCore);
        }

        private static void SetColor(SerializedObject profile, string propertyName, Color value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetFloat(SerializedObject profile, string propertyName, float value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetVector2(SerializedObject profile, string propertyName, Vector2 value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        private static void SetInt(SerializedObject profile, string propertyName, int value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBool(SerializedObject profile, string propertyName, bool value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetObjectReference(
            SerializedObject profile,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetArraySize(
            SerializedObject profile,
            string propertyName,
            int size)
        {
            SerializedProperty property = profile.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                property.arraySize = Mathf.Max(0, size);
            }
        }

        private static string AssetPathToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void DestroyOwnedSheet(PreviewSheet sheet)
        {
            if (sheet != null && sheet.OwnsTexture && sheet.Texture != null)
            {
                UnityEngine.Object.DestroyImmediate(sheet.Texture);
                sheet.Texture = null;
            }
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
