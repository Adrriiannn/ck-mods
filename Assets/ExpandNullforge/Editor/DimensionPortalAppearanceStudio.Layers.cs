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
    /// Choosing a layer, pinning one, and what the preview shows while it is chosen.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
        // ------------------------------------------------------------------ page surface --
        // Everything below is what the rebuilt Portal page needs in order to own the chrome:
        // which layer is being edited, which phase the preview is in, and one entry point that
        // draws the canvas alone. None of it changes what the studio does; it only gives the
        // page a handle on the same state the drawn-in-place controls already read and write.

        /// <summary>The layer whose settings are being edited.</summary>
        internal StudioLayer SelectedLayer
        {
            get { return selectedLayer; }
            set
            {
                if (!IsLayerAvailable(value))
                {
                    return;
                }

                SelectLayer(value);
                repaintRequested = true;
            }
        }

        /// <summary>A layer an instant portal can never show is not offered at all.</summary>
        internal bool IsLayerAvailable(StudioLayer layer)
        {
            return !instantPortalMode || !IsHiddenInstantLayer(layer);
        }

        /// <summary>
        /// True when this layer has a full set of layout values, and can therefore be put back
        /// the way the artwork was drawn.
        /// </summary>
        internal bool LayerHasLayout(StudioLayer layer)
        {
            return TryGetLayerTransformPropertyNames(
                layer,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private void DrawLayerNavigation()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));
            EditorGUILayout.LabelField("LAYERS", DimensionsApiImguiTheme.SectionLabel);
            if (!instantPortalMode)
            {
                DrawLayerButton(StudioLayer.Frame, "FRAME", "Base artwork");
                DrawLayerButton(StudioLayer.ChargeSweep, "CHARGE", "Continuous sweep");
                DrawLayerButton(StudioLayer.Milestones, "MILESTONES", "Persistent blobs");
            }

            DrawLayerButton(StudioLayer.Center, "CENTER", "Activated ring");
            DrawLayerButton(StudioLayer.InnerFlecks, "SWIRLS", "Inner motion");
            if (!instantPortalMode)
            {
                DrawLayerButton(StudioLayer.ReadyBurst, "EFFECTS", "Ready activation burst");
            }

            GUILayout.Space(8f);
            EditorGUILayout.LabelField("ON THE GROUND", DimensionsApiImguiTheme.SectionLabel);
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
            EditorGUILayout.LabelField("LAYERS", DimensionsApiImguiTheme.SectionLabel);
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

            EditorGUILayout.LabelField("IN-GAME", DimensionsApiImguiTheme.SectionLabel);
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
            // The burst base rides the activated clock, so it has to restart with it; a stale
            // base would suppress the burst now and fire it unprompted much later.
            readyBurstBaseClock = 0f;
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
    }
}
