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
    /// Drawing the canvas: the frames, the guides, the grid and the selection.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        /// <summary>
        /// Draws the portal preview and nothing else: no header, no layer list, no settings
        /// panel. The rebuilt page owns those, and hosts this as the one island of drawn pixels.
        /// </summary>
        internal void DrawCanvasIsland(
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
                lastCanvasResult = result;
                return;
            }

            paletteBakeRequested = false;

            SerializedObject serializedTemplate = GetSerializedTemplate(template);
            serializedTemplate.UpdateIfRequiredOrScript();
            SerializedObject serializedProfile = GetSerializedProfile(profile);
            serializedProfile.UpdateIfRequiredOrScript();
            ProcessUndoRedoPaletteChanges(template, profile, serializedProfile);
            EnsurePreviewComposition(serializedProfile);
            ConfigurePreviewViewBounds();
            ResolveActiveZoom(
                Mathf.Max(220f, availableWidth),
                Mathf.Max(220f, availableHeight - 44f));

            hidePreviewToolbar = true;
            try
            {
                DrawPreviewCanvas(
                    template,
                    profile,
                    serializedProfile,
                    previewErrors.Count == 0);
            }
            finally
            {
                hidePreviewToolbar = false;
            }

            // Nothing trails the playbar unless there is genuinely something to say — an
            // unconditional spacer here painted a strip of not-quite-canvas under every preview.
            if (previewErrors.Count > 0 || previewWarnings.Count > 0)
            {
                GUILayout.Space(6f);
                DrawPreviewIssues();
            }

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

            lastCanvasResult = result;
        }

        private void DrawPreviewCanvas(
            DimensionTemplateAsset template,
            DimensionPortalVisualProfileAsset profile,
            SerializedObject serializedProfile,
            bool canApply)
        {
            // The canvas and its host are one rectangle. The old layout sized the host for
            // the largest zoom and floated a smaller canvas inside it, which left a dead
            // darker band under the picture at every other zoom.
            float previewContentWidth = Mathf.Ceil(lastPreviewAreaWidth);
            float canvasHostHeight = Mathf.Ceil(lastPreviewAreaHeight);
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
            Rect canvasRect = canvasHostRect;
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

                // The pool of light goes down before everything: it lights the floor, the blob
                // shadow then darkens that floor, and the portal's own art — emissive and unlit
                // in-game — draws over both untinted. It is drawn OUTSIDE the clip group and
                // clipped by hand, because the material blit ignores GUI clipping — the first
                // build of this washed portal light across the page below the preview.
                DrawGroundLightPool(canvasRect);

                GUI.BeginGroup(canvasRect);
                Rect localCanvas = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
                // The floor shadow lies under the portal in the world, so it goes down first.
                DrawPortalShadowPreview();
                for (int i = 0; i < visibleFrames.Count; i++)
                {
                    DrawVisibleFrame(localCanvas, visibleFrames[i]);
                }

                DrawFleckPreview(localCanvas);
                DrawReadyBurstPreview();

                DrawPaletteFocusOverlay(localCanvas);

                if (showGuides)
                {
                    DrawAlignmentGuides(localCanvas);
                }

                DrawGroundLightGizmo(localCanvas);
                DrawSelectedLayerOutline();
                if (showGrid && activeZoom >= 4f)
                {
                    DrawPixelGrid(localCanvas);
                }

                DrawSelectedPixel();
                DrawCanvasHint(localCanvas);
                GUI.EndGroup();
            }

            HandleCanvasInteraction(canvasRect, serializedProfile, canvasControlId);
            if (previewPhase == PreviewPhase.Charging)
            {
                DrawChargingPreviewFooter(previewContentWidth);
            }

            EditorGUILayout.EndVertical();
            // No outline around the preview window: the canvas draws its own border, and this
            // second, lighter frame was one of the strips in the darker band below it.
            HandlePreviewZoomScroll(previewWindowRect);
            HandlePreviewPan(canvasHostRect);

            if (!hidePreviewToolbar)
            {
                // The rebuilt page offers these as the product's own buttons, so drawing them
                // here as well would be a second copy of the same controls.
                GUILayout.Space(5f);
                EditorGUILayout.BeginVertical(GUILayout.Width(previewWindowWidth));
                DrawPresetToolbar(template, profile, canApply);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewToolbar(float viewportWidth)
        {
            if (hidePreviewToolbar)
            {
                // The rebuilt page draws these same controls above the canvas in the product's
                // own buttons, so the drawn-in-place strip would be a second copy of itself.
                return;
            }

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
                DrawReplayBurstControl();
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
            DrawReplayBurstControl();
            DrawReplayClosingControl();
            GUILayout.FlexibleSpace();
            DrawViewControls();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCanvasBackground(Rect canvasRect)
        {
            // The floor of a cavern, not an image editor: one near-black tone with no
            // checkerboard, so the portal's own light is the brightest thing in the frame.
            // The transparency checker said "this is a texture"; the game never shows one.
            GUI.BeginGroup(canvasRect);
            Rect clippedCanvas = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
            EditorGUI.DrawRect(clippedCanvas, new Color(0.027f, 0.035f, 0.045f, 1f));
            DrawOutline(clippedCanvas, new Color(0.10f, 0.17f, 0.16f, 1f), 1f);
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

        // The one-shot burst outlives its particles by a margin; past this the preview stops
        // simulating a system that can no longer emit anything.
        private const float ReadyBurstPreviewSeconds = 3f;

        // EnsurePortalShadowRenderer draws the vanilla fallback shadow sliced to this world
        // size rather than at the sprite's native rect.
        private static readonly Vector2 DefaultShadowSlicedSize = new Vector2(3.5f, 0.75f);

        private void DrawReadyBurstPreview()
        {
            if (instantPortalMode ||
                previewPhase != PreviewPhase.Activated ||
                activeProfile == null ||
                !activeProfile.PlayReadyFlash)
            {
                return;
            }

            float burstTime = activatedClock - readyBurstBaseClock;
            if (burstTime < 0f || burstTime > ReadyBurstPreviewSeconds)
            {
                return;
            }

            if (readyBurstPreviewRenderer.TryRender(
                    activeProfile,
                    burstTime,
                    out Texture burstTexture,
                    out string renderError))
            {
                // Composited around the same aperture anchor as the persistent swirl; the
                // authored offset is already baked into the render. The burst flashes over
                // the whole portal in the world, so the aperture mask is opened wide.
                // The render is centred on the aperture, which sits at fixed game pixels no
                // matter what size the frame artwork is; bodyScreenOrigin converts that into
                // this canvas, so a 64 x 64 frame override does not drag the burst off centre.
                Vector2 apertureCanvas =
                    DimensionPortalVisualContract.ProjectToGamePixels(
                        DimensionPortalVisualContract.CenterLocalPosition) - bodyScreenOrigin;
                Rect nativeBurstCanvas = new Rect(
                    apertureCanvas.x - CanonicalCanvasPixels * 0.5f,
                    apertureCanvas.y - CanonicalCanvasPixels * 0.5f,
                    CanonicalCanvasPixels,
                    CanonicalCanvasPixels);
                readyBurstPreviewRenderer.DrawAdditive(
                    CanvasRectToGuiRect(nativeBurstCanvas),
                    burstTexture,
                    new Vector2(CanonicalCanvasPixels * 0.5f, CanonicalCanvasPixels * 0.5f),
                    new Vector2(4096f, 4096f));
            }
            else if (!string.IsNullOrEmpty(renderError))
            {
                AddPreviewWarning(renderError);
            }
        }

        private void DrawPortalShadowPreview()
        {
            if (activeProfile == null || !activeProfile.PortalShadowEnabled)
            {
                return;
            }

            Sprite sprite = activeProfile.PortalShadowSprite;
            if (sprite == null)
            {
                sprite = DimensionRuntimeConsumerBootstrapUtility.LoadDefaultPortalShadowSprite();
            }

            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            // Runtime placement: PortalShadowGroup at (-0.5 + offX/16, 0, 0.8125 + offY/16)
            // plus the Shadow child at (0.5, 0.0625, -0.5) — a flat X-rotated sprite whose
            // ground footprint projects 1:1 into canvas pixels, drawn 70% black.
            Vector2 offsetPixels = activeProfile.PortalShadowOffsetPixels;
            Vector2 anchorGamePixels = new Vector2(offsetPixels.x, 6f + offsetPixels.y);
            Vector2 rawScale = activeProfile.PortalShadowScale;
            Vector2 scale = new Vector2(
                Mathf.Clamp(Mathf.Abs(rawScale.x), 0.05f, 8f),
                Mathf.Clamp(Mathf.Abs(rawScale.y), 0.05f, 8f));

            // The generated renderer draws the default shadow sliced to a fixed world size and
            // a custom one at its native size; matching both here is what keeps the preview
            // honest for an author aligning the blob against their own frame art.
            bool authoredSprite = activeProfile.PortalShadowSprite != null;
            Vector2 unscaledPixels;
            if (authoredSprite)
            {
                float pixelsPerUnit = sprite.pixelsPerUnit <= 0f ? 16f : sprite.pixelsPerUnit;
                float gamePixelsPerSpritePixel =
                    DimensionPortalVisualContract.PixelsPerUnit / pixelsPerUnit;
                unscaledPixels = new Vector2(
                    sprite.rect.width * gamePixelsPerSpritePixel,
                    sprite.rect.height * gamePixelsPerSpritePixel);
            }
            else
            {
                unscaledPixels = DefaultShadowSlicedSize *
                                 DimensionPortalVisualContract.PixelsPerUnit;
            }

            Vector2 sizePixels = new Vector2(
                unscaledPixels.x * scale.x,
                unscaledPixels.y * scale.y);
            Vector2 normalizedPivot = authoredSprite
                ? new Vector2(
                    sprite.rect.width <= 0f ? 0.5f : sprite.pivot.x / sprite.rect.width,
                    sprite.rect.height <= 0f ? 0.5f : sprite.pivot.y / sprite.rect.height)
                : new Vector2(0.5f, 0.5f);
            Vector2 anchorCanvas = anchorGamePixels - bodyScreenOrigin;
            Rect canvasRect = new Rect(
                anchorCanvas.x - normalizedPivot.x * sizePixels.x,
                anchorCanvas.y - normalizedPivot.y * sizePixels.y,
                sizePixels.x,
                sizePixels.y);
            Rect guiRect = CanvasRectToGuiRect(canvasRect);

            Texture2D texture = sprite.texture;
            Rect texCoords = new Rect(
                sprite.rect.x / texture.width,
                sprite.rect.y / texture.height,
                sprite.rect.width / texture.width,
                sprite.rect.height / texture.height);
            if (activeProfile.PortalShadowFlipX)
            {
                texCoords.x += texCoords.width;
                texCoords.width = -texCoords.width;
            }
            if (activeProfile.PortalShadowFlipY)
            {
                texCoords.y += texCoords.height;
                texCoords.height = -texCoords.height;
            }

            float rotation = activeProfile.PortalShadowRotationDegrees;
            Matrix4x4 previousMatrix = GUI.matrix;
            if (!Mathf.Approximately(rotation, 0f))
            {
                // A ground-plane spin projects as a plain 2D rotation of the blob.
                Vector2 pivotGui = CanvasPointToGuiPoint(anchorCanvas);
                GUIUtility.RotateAroundPivot(-rotation, pivotGui);
            }

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7019608f);
            GUI.DrawTextureWithTexCoords(guiRect, texture, texCoords, true);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
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
    }
}
