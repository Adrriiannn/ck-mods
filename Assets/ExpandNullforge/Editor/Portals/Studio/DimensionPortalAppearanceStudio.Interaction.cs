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
    /// What the mouse and the arrow keys do on the canvas.
    /// </summary>
    internal sealed partial class DimensionPortalAppearanceStudio
    {
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

            // The burst and the ground light are positioned on the canvas without owning a
            // sprite frame to grab, so their drag starts anywhere while they are selected.
            if (IsFramelessTransformableLayer(selectedLayer))
            {
                string framelessOffsetName = GetOffsetPropertyName(selectedLayer);
                SerializedProperty framelessOffset = string.IsNullOrEmpty(framelessOffsetName)
                    ? null
                    : profile.FindProperty(framelessOffsetName);
                if (framelessOffset != null)
                {
                    canvasDragLayer = selectedLayer;
                    canvasDragStartPoint = canvasPoint;
                    canvasDragStartOffset = framelessOffset.vector2Value;
                    canvasDragPending = true;
                    canvasDragUndoRecorded = false;
                    GUIUtility.hotControl = controlId;
                }
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
                   layer == StudioLayer.ReadyBurst ||
                   layer == StudioLayer.GroundLight ||
                   (layer == StudioLayer.InnerFlecks &&
                    activeProfile != null &&
                    activeProfile.CenterSwirlOverrideVanilla);
        }

        /// <summary>
        /// Layers positioned on the canvas without owning a sprite frame to grab: their drag
        /// starts anywhere on the canvas while they are selected.
        /// </summary>
        private static bool IsFramelessTransformableLayer(StudioLayer layer)
        {
            return layer == StudioLayer.ReadyBurst || layer == StudioLayer.GroundLight;
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
                case StudioLayer.ReadyBurst:
                    return "readyFlashOffsetPixels";
                case StudioLayer.GroundLight:
                    return "groundLightOffsetPixels";
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
    }
}
