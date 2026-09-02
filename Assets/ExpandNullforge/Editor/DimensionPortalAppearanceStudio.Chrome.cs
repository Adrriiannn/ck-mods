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
    /// The controls around the canvas: header, play state, replay, zoom and pan.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        /// <summary>True when this studio is editing the instant portal an item opens.</summary>
        internal bool InstantPortalMode
        {
            get { return instantPortalMode; }
        }

        /// <summary>The portal whose look is being edited, once the studio has resolved it.</summary>
        internal DimensionPortalVisualProfileAsset ActiveProfile
        {
            get { return activeProfile ?? editingProfile; }
        }

        /// <summary>The dimension the edited portal belongs to.</summary>
        internal DimensionTemplateAsset ActiveTemplate
        {
            get { return activeTemplate ?? editingProfileTemplate; }
        }

        /// <summary>True when the preview is showing the portal after it finished charging.</summary>
        internal bool IsPreviewActivated
        {
            get { return previewPhase == PreviewPhase.Activated; }
        }

        internal bool CanReplayOpening
        {
            get { return previewPhase == PreviewPhase.Activated; }
        }

        internal bool CanReplayClosing
        {
            get { return instantPortalMode && previewPhase == PreviewPhase.Activated; }
        }

        internal bool CanReplayBurst
        {
            get
            {
                return !instantPortalMode &&
                       previewPhase == PreviewPhase.Activated &&
                       activeProfile != null &&
                       activeProfile.PlayReadyFlash;
            }
        }

        /// <summary>Whether the pixel grid is drawn over the preview.</summary>
        internal bool ShowGrid
        {
            get { return showGrid; }
            set
            {
                if (showGrid == value)
                {
                    return;
                }

                showGrid = value;
                repaintRequested = true;
            }
        }

        /// <summary>Whether the centre and pivot guides are drawn over the preview.</summary>
        internal bool ShowGuides
        {
            get { return showGuides; }
            set
            {
                if (showGuides == value)
                {
                    return;
                }

                showGuides = value;
                repaintRequested = true;
            }
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
            lastPreviewAreaWidth = Mathf.Max(PreviewToolbarMinimumWidth, availablePreviewWidth);
            lastPreviewAreaHeight = Mathf.Max(220f, availablePreviewHeight);

            // The comfortable default leaves more than a portal's width of floor on every
            // side, so the light it throws is part of the picture instead of cropped away.
            float content = Mathf.Max(
                1f,
                Mathf.Max(canvasPixelWidth, canvasPixelHeight));
            int fittedPercent = Mathf.Clamp(
                Mathf.RoundToInt(
                    Mathf.Min(lastPreviewAreaWidth, lastPreviewAreaHeight) /
                    (content * 2.2f) * PreviewZoomStepPercent),
                MinPreviewZoomPercent,
                MaxPreviewZoomPercent);
            fittedZoom = fittedPercent / (float)PreviewZoomStepPercent;
            if (!previewZoomWasAdjusted)
            {
                previewZoomPercent = fittedPercent;
            }
            else
            {
                previewZoomPercent = Mathf.Clamp(
                    previewZoomPercent,
                    MinPreviewZoomPercent,
                    MaxPreviewZoomPercent);
            }

            activeZoom = previewZoomPercent / (float)PreviewZoomStepPercent;

            // The viewport IS the canvas. The visible window, in canvas coordinates, is
            // whatever fits the viewport at this zoom, centred on the artwork plus wherever
            // the creator has panned — so zooming out genuinely walks away from the portal
            // and zooming in genuinely leans into one pixel of it.
            float viewWidth = lastPreviewAreaWidth / Mathf.Max(0.001f, activeZoom);
            float viewHeight = lastPreviewAreaHeight / Mathf.Max(0.001f, activeZoom);
            Vector2 viewCenter = new Vector2(
                canvasPixelWidth * 0.5f + previewPanCanvas.x,
                canvasPixelHeight * 0.5f + previewPanCanvas.y);
            activeViewBounds = new Rect(
                viewCenter.x - viewWidth * 0.5f,
                viewCenter.y - viewHeight * 0.5f,
                viewWidth,
                viewHeight);
        }

        private void ConfigurePreviewViewBounds()
        {
            // The visible window now follows the viewport, the zoom, and the creator's own
            // pan, so it is computed at the end of ResolveActiveZoom, once the viewport's
            // size is known. This hook stays because both draw paths call it in order.
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

            PreviewPhase chosenPhase = (PreviewPhase)GUILayout.Toolbar(
                (int)previewPhase,
                PreviewPhaseLabels,
                EditorStyles.toolbarButton,
                GUILayout.Width(width));
            SetPreviewActivated(chosenPhase == PreviewPhase.Activated);
        }

        /// <summary>
        /// Switches the preview between charging and activated. One body, shared by the toolbar
        /// here and by the rebuilt page that sits above the canvas; asking for the phase the
        /// preview is already in does nothing.
        /// </summary>
        internal void SetPreviewActivated(bool activated)
        {
            PreviewPhase requested = activated
                ? PreviewPhase.Activated
                : PreviewPhase.Charging;
            if (previewPhase == requested)
            {
                return;
            }

            previewPhase = requested;
            pinnedLayerIndex = -1;
            activatedClock = 0f;
            readyBurstBaseClock = 0f;
            skipCenterOpeningOnNextBuild = previewPhase == PreviewPhase.Activated;
            isPlaying = true;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
            previewCompositionDirty = true;

            hasSelectedPixel = false;
            ClearPaletteFocus();
        }

        private void DrawReplayOpeningControl()
        {
            EditorGUI.BeginDisabledGroup(previewPhase != PreviewPhase.Activated);
            if (GUILayout.Button(
                    "Replay opening",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(94f)))
            {
                ReplayOpening();
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Plays the portal's opening again from its first frame. One body, shared by the
        /// toolbar button here and by the rebuilt page that sits above the canvas.
        /// </summary>
        internal void ReplayOpening()
        {
            if (!CanReplayOpening)
            {
                return;
            }

            previewCenterClosingActive = false;
            activatedClock = 0f;
            // The in-game burst fires the moment charging completes, which is the same
            // moment the opening replay restarts.
            readyBurstBaseClock = 0f;
            skipCenterOpeningOnNextBuild = false;
            isPlaying = true;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
            previewCompositionDirty = true;
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
                ReplayClosing();
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Plays the instant portal closing again. One body, shared by the toolbar button here
        /// and by the rebuilt page that sits above the canvas.
        /// </summary>
        internal void ReplayClosing()
        {
            if (!CanReplayClosing)
            {
                return;
            }

            previewCenterClosingActive = true;
            activatedClock = 0f;
            readyBurstBaseClock = float.MaxValue;
            skipCenterOpeningOnNextBuild = false;
            isPlaying = true;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
            previewCompositionDirty = true;
        }

        private void DrawReplayBurstControl()
        {
            // The burst layer only exists on the placed portal; an instant portal spawns
            // fully charged and never plays the charge-completion flash.
            if (instantPortalMode)
            {
                return;
            }

            EditorGUI.BeginDisabledGroup(!CanReplayBurst);
            if (GUILayout.Button(
                    "Replay burst",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(84f)))
            {
                ReplayBurst();
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// Fires the ready burst again inside the running preview clock. One body, shared by the
        /// toolbar button here and by the rebuilt page that sits above the canvas.
        /// </summary>
        internal void ReplayBurst()
        {
            if (!CanReplayBurst)
            {
                return;
            }

            readyBurstBaseClock = activatedClock;
            isPlaying = true;
            lastClockTime = EditorApplication.timeSinceStartup;
            nextRepaintTime = 0.0;
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
            if (!TryAdjustPreviewZoom(direction))
            {
                return;
            }

            GUI.changed = true;
        }

        /// <summary>
        /// Steps the preview zoom one notch and reports whether anything moved. Safe to call from
        /// outside a drawing pass, which the rebuilt page above the canvas needs.
        /// </summary>
        internal bool TryAdjustPreviewZoom(int direction)
        {
            // Walk the ladder rather than adding a flat step: at 5% a 10-point step would
            // triple the view, and at 300% it would be imperceptible.
            int index = 0;
            for (int i = 0; i < PreviewZoomLadder.Length; i++)
            {
                if (PreviewZoomLadder[i] <= previewZoomPercent)
                {
                    index = i;
                }
            }

            int nextIndex = Mathf.Clamp(
                index + (direction > 0 ? 1 : -1),
                0,
                PreviewZoomLadder.Length - 1);
            int nextZoom = PreviewZoomLadder[nextIndex];
            if (nextZoom == previewZoomPercent)
            {
                return false;
            }

            previewZoomPercent = nextZoom;
            previewZoomWasAdjusted = true;
            repaintRequested = true;
            return true;
        }

        /// <summary>How far the preview is zoomed in, as a percentage.</summary>
        internal int PreviewZoomPercent
        {
            get { return previewZoomPercent; }
        }

        internal bool CanZoomPreviewIn
        {
            get { return previewZoomPercent < MaxPreviewZoomPercent; }
        }

        internal bool CanZoomPreviewOut
        {
            get { return previewZoomPercent > MinPreviewZoomPercent; }
        }

        private GUIStyle canvasHintStyle;

        /// <summary>The one-line how-to at the foot of the picture, matching the Tileset Studio.</summary>
        private void DrawCanvasHint(Rect localCanvas)
        {
            if (canvasHintStyle == null)
            {
                canvasHintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                };
                canvasHintStyle.normal.textColor = new Color(0.84f, 0.86f, 0.98f, 0.42f);
            }

            GUI.Label(
                new Rect(0f, localCanvas.height - 18f, localCanvas.width, 16f),
                "click & hold to reposition · middle click to move around · scroll to zoom",
                canvasHintStyle);
        }

        /// <summary>
        /// Middle-drag moves the view across the floor; a double middle click walks back to
        /// the portal. Pan is kept in canvas pixels so the view stays anchored while zooming.
        /// </summary>
        private void HandlePreviewPan(Rect canvasRect)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.MouseDown &&
                current.button == 2 &&
                canvasRect.Contains(current.mousePosition))
            {
                if (current.clickCount >= 2)
                {
                    previewPanCanvas = Vector2.zero;
                }

                previewPanActive = true;
                repaintRequested = true;
                current.Use();
            }
            else if (current.type == EventType.MouseDrag &&
                     previewPanActive &&
                     current.button == 2)
            {
                float zoom = Mathf.Max(0.001f, activeZoom);
                previewPanCanvas.x -= current.delta.x / zoom;
                previewPanCanvas.y += current.delta.y / zoom;
                float limit = Mathf.Max(canvasPixelWidth, canvasPixelHeight) * 3f;
                previewPanCanvas.x = Mathf.Clamp(previewPanCanvas.x, -limit, limit);
                previewPanCanvas.y = Mathf.Clamp(previewPanCanvas.y, -limit, limit);
                repaintRequested = true;
                current.Use();
            }
            else if (current.type == EventType.MouseUp && current.button == 2)
            {
                previewPanActive = false;
            }
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
    }
}
