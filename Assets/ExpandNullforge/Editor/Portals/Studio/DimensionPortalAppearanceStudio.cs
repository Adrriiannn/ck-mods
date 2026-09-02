using System;
using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed partial class DimensionPortalAppearanceStudio : IDisposable
    {

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
        private const int MinPreviewZoomPercent = 3;
        private const int MaxPreviewZoomPercent = 320;

        /// <summary>
        /// The zoom stops, in percent, where 10 draws one game pixel per screen pixel. The
        /// range is deliberately wide: 3 is the across-the-cavern view a player actually has
        /// of a portal, 320 is one game pixel blown up to thirty two, for judging a single
        /// colour change. Steps are geometric-ish so each notch feels like the same move.
        /// </summary>
        private static readonly int[] PreviewZoomLadder =
        {
            3, 5, 10, 20, 30, 40, 60, 90, 120, 160, 240, 320,
        };
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
        private readonly DimensionPortalParticlePreviewRenderer readyBurstPreviewRenderer =
            new DimensionPortalParticlePreviewRenderer(true);
        // The burst fires at the activation moment (activated clock zero); Replay burst moves
        // this base forward so the one-shot restarts inside the running preview clock, and a
        // closing replay parks it unreachably high so no burst plays over the close.
        private float readyBurstBaseClock;
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
        private float lastPreviewAreaWidth = 460f;
        private float lastPreviewAreaHeight = 460f;
        private Vector2 previewPanCanvas;
        private bool previewPanActive;
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
        // Set only while the rebuilt page hosts the canvas, which draws those same controls in
        // the product's own buttons above it.
        private bool hidePreviewToolbar;
        private DrawResult lastCanvasResult;

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

        /// <summary>
        /// Everything the last <see cref="DrawCanvasIsland"/> pass wants the window to act on:
        /// the preset it was asked to use, a runtime refresh, a message. Read it once per pass,
        /// the way the window reads the result of <see cref="Draw"/>.
        /// </summary>
        internal DrawResult ConsumeCanvasResult()
        {
            DrawResult result = lastCanvasResult;
            lastCanvasResult = default(DrawResult);
            return result;
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

            EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
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
            DestroyTexture(ref groundGlowTexture);
            if (groundGlowMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(groundGlowMaterial);
                groundGlowMaterial = null;
            }
            particlePreviewRenderer.Dispose();
            readyBurstPreviewRenderer.Dispose();
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
            readyBurstPreviewRenderer.Invalidate();
        }
    }
}
