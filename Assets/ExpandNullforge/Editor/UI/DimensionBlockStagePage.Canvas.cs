using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The block canvas, its toolbar, and the state chips under it.
    /// </summary>
    internal sealed partial class DimensionBlockStagePage
    {
        // ---------------------------------------------------------------- the canvas ---

        private void RebuildCanvas()
        {
            canvasHost.Clear();
            if (selected == null)
            {
                return;
            }

            // The picture needs no caption and no title: the selected block's name is already
            // lit in the list beside it, and the card frame says the rest.
            VisualElement group = new VisualElement();
            group.AddToClassList("dim-group");
            VisualElement body = new VisualElement();
            body.AddToClassList("dim-group-body");
            group.Add(body);

            canvasToolbar = BuildCanvasToolbar();
            body.Add(canvasToolbar);

            // No fixed height: the island reports its measured content height and the card
            // shrink-wraps it. A fixed number left a dead band of card background under the
            // canvas — the same disease the Portal Studio island had.
            canvasIsland = new IMGUIContainer(DrawCanvasIsland);
            canvasIsland.AddToClassList("dim-canvas-island");
            body.Add(canvasIsland);

            stateChips = new VisualElement();
            body.Add(stateChips);
            chipSignature = string.Empty;
            RebuildStateChips();

            canvasHost.Add(group);
        }

        private VisualElement BuildCanvasToolbar()
        {
            VisualElement bar = new VisualElement();
            bar.AddToClassList("dim-canvas-toolbar");
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.flexShrink = 0;

            // One row: what you are painting on the left, what you can do with the picture on
            // the right. Ground is the surface you walk on, Wall the solid part you mine.
            VisualElement paints = DimensionsApiControls.Tabs(
                new[] { "Ground", "Wall" },
                studio.PreviewPaintsWall ? 1 : 0,
                index =>
                {
                    studio.PreviewPaintsWall = index == 1;
                    RefreshCanvasToolbar();
                });
            bar.Add(paints);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("dim-canvas-actions");

            Button paint = studio.PreviewPainting
                ? DimensionsApiControls.PrimaryButton("Painting", TogglePainting)
                : DimensionsApiControls.GhostButton("Paint", TogglePainting);
            paint.tooltip = "While painting is on, the left mouse button adds a block and the right " +
                            "mouse button takes one away. Turn it off to pick a block and read it.";
            actions.Add(paint);

            Button shuffle = DimensionsApiControls.GhostButton("Shuffle", () =>
            {
                studio.ShufflePreview();
                MarkCanvasDirty();
            });
            shuffle.tooltip = "Lays the same tiles out in another arrangement. Nothing about the " +
                              "block itself changes, so this is only a second opinion on the art.";
            actions.Add(shuffle);

            Button reset = DimensionsApiControls.GhostButton("Reset View", () =>
            {
                studio.ResetPreviewView();
                MarkCanvasDirty();
            });
            reset.tooltip = "Puts the camera, the zoom and the arrangement back where they started.";
            actions.Add(reset);

            bar.Add(actions);
            return bar;
        }

        private void TogglePainting()
        {
            studio.PreviewPainting = !studio.PreviewPainting;
            RefreshCanvasToolbar();
        }

        /// <summary>
        /// Puts the controls back with the new answer showing. Deferred, because the button that
        /// asked for it is one of the ones being replaced.
        /// </summary>
        private void RefreshCanvasToolbar()
        {
            if (root == null)
            {
                return;
            }

            root.schedule.Execute(() =>
            {
                if (canvasToolbar == null || canvasToolbar.parent == null)
                {
                    return;
                }

                VisualElement parent = canvasToolbar.parent;
                int index = parent.IndexOf(canvasToolbar);
                VisualElement rebuilt = BuildCanvasToolbar();
                parent.Insert(index, rebuilt);
                parent.Remove(canvasToolbar);
                canvasToolbar = rebuilt;
                MarkCanvasDirty();
            });
        }

        private void MarkCanvasDirty()
        {
            if (canvasIsland != null)
            {
                canvasIsland.MarkDirtyRepaint();
            }

            repaint();
        }

        private void DrawCanvasIsland()
        {
            if (selected == null)
            {
                return;
            }

            float width = canvasIsland == null ? 0f : canvasIsland.contentRect.width;
            if (float.IsNaN(width) || width < 200f)
            {
                width = 560f;
            }

            Color previousBackground;
            Color previousContent;
            DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
            try
            {
                studio.DrawPreviewIsland(selected, width, CanvasHeight);
            }
            finally
            {
                DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
            }
        }

        /// <summary>Keeps the chips honest about whatever is selected inside the canvas.</summary>
        private void SyncWithCanvas()
        {
            if (root == null || wizardShowing || stateChips == null || selected == null)
            {
                return;
            }

            if (!string.Equals(chipSignature, ReadCanvasSignature(), System.StringComparison.Ordinal))
            {
                RebuildStateChips();
            }
        }

        private string ReadCanvasSignature()
        {
            DimensionTilesetBlockPreview canvas = studio.Preview;
            if (!canvas.HasSelection)
            {
                return "nothing";
            }

            StringBuilder builder = new StringBuilder(canvas.SelectionIsWall ? "wall" : "ground");
            for (int i = 0; i < DimensionTilesetBlockPreview.StateCatalog.Length; i++)
            {
                DimensionTilesetBlockPreview.StateEntry state =
                    DimensionTilesetBlockPreview.StateCatalog[i];
                if (state.IsWall == canvas.SelectionIsWall)
                {
                    builder.Append(canvas.SelectionHasState(state.Layer) ? '1' : '0');
                }
            }

            return builder.ToString();
        }

        private void RebuildStateChips()
        {
            if (stateChips == null)
            {
                return;
            }

            chipSignature = ReadCanvasSignature();
            stateChips.Clear();
            DimensionTilesetBlockPreview canvas = studio.Preview;
            if (!canvas.HasSelection)
            {
                return;
            }

            bool wall = canvas.SelectionIsWall;
            VisualElement row = DimensionsApiControls.ChipRow();
            for (int i = 0; i < DimensionTilesetBlockPreview.StateCatalog.Length; i++)
            {
                DimensionTilesetBlockPreview.StateEntry state =
                    DimensionTilesetBlockPreview.StateCatalog[i];
                if (state.IsWall != wall)
                {
                    continue;
                }

                bool on = canvas.SelectionHasState(state.Layer);
                Button chip = new Button(() =>
                {
                    // No rebuild here: the canvas is the truth, and the sync above notices the
                    // change and puts the chips back a moment later.
                    canvas.ToggleSelectionState(state.Layer, !on);
                    MarkCanvasDirty();
                })
                {
                    text = state.Label
                };
                chip.AddToClassList("dim-chip");
                chip.AddToClassList("dim-chip-toggle");
                if (on)
                {
                    chip.AddToClassList("dim-chip-toggle-on");
                }

                row.Add(chip);
            }

            stateChips.Add(DimensionsApiControls.Field(
                wall ? "Wall States" : "Ground States",
                "Only in the canvas, and only on the block you picked. Nothing here is saved onto " +
                "the block itself. It is a way to look at the art.",
                row));
        }
    }
}
