using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Map: how the world is arranged, and the studio that draws it.
    /// </summary>
    /// <remarks>
    /// The canvas stays where it belongs, inside an island of the original drawing code, because
    /// it is a live picture of the compiled layout and nothing is gained by redrawing it. What
    /// changed is everything around it: the rings and regions are now cards worded as questions,
    /// and the plumbing the framework fills in is no longer presented as a form to fill.
    /// </remarks>
    internal sealed class DimensionMapStagePage
    {
        private readonly System.Action drawStudio;
        private readonly System.Action createLayout;
        private readonly DimensionMapPaintPanel paintPanel;

        private VisualElement root;
        private VisualElement body;
        private IMGUIContainer studioHost;
        private DimensionTemplateAsset template;

        // 0 = Arrange (where each biome sits), 1 = Paint (the shape of the ground itself).
        private int activeTab;

        internal DimensionMapStagePage(System.Action drawStudio, System.Action createLayout)
        {
            this.drawStudio = drawStudio;
            this.createLayout = createLayout;
            paintPanel = new DimensionMapPaintPanel(() =>
            {
                if (root != null)
                {
                    root.MarkDirtyRepaint();
                }
            });
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            ScrollView scroller = new ScrollView(ScrollViewMode.Vertical);
            scroller.AddToClassList("dim-fill");

            body = new VisualElement();
            body.AddToClassList("dim-single-detail");
            body.style.maxWidth = 980;
            scroller.Add(body);
            root.Add(scroller);
            return root;
        }

        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            if (body == null)
            {
                return;
            }

            body.Clear();
            if (template == null)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its map appears here.",
                    null));
                return;
            }

            if (template.LayoutTemplate == null)
            {
                body.Add(DimensionsApiControls.EmptyState(
                    "No layout yet",
                    "A map decides where each biome lives. Core Keeper arranges everything in rings and arcs around the centre, and yours can too.",
                    DimensionsApiControls.PrimaryButton("Create a layout", createLayout)));
                return;
            }

            // Two tabs, one page. Arrange says where each biome sits; Paint draws the ground
            // itself. They are the same question at two scales, and the Shell already routes this
            // whole section here, so the painter needs no stage of its own.
            body.Add(DimensionsApiControls.Tabs(
                new[] { "Arrange", "Paint" },
                activeTab,
                index =>
                {
                    activeTab = index;
                    // Deferred: swapping the body from inside a child's click callback would
                    // destroy the button while it is still handling the event.
                    root.schedule.Execute(() => Refresh(template));
                }));

            if (activeTab == 1)
            {
                body.Add(paintPanel.Build(template, template.LayoutTemplate));
                return;
            }

            body.Add(BuildStudioCard());
            body.Add(BuildLayoutCards());
        }

        /// <summary>The live picture of the compiled layout, hosted as it was written.</summary>
        private VisualElement BuildStudioCard()
        {
            VisualElement group = DimensionsApiControls.Group(
                "Preview",
                "the shape everything sits in");
            VisualElement groupBody = DimensionsApiControls.BodyOf(group);

            studioHost = new IMGUIContainer(() =>
            {
                Color previousBackground;
                Color previousContent;
                DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
                try
                {
                    drawStudio();
                }
                finally
                {
                    DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
                }
            });
            studioHost.AddToClassList("dim-canvas-island");
            groupBody.Add(studioHost);
            return group;
        }

        private VisualElement BuildLayoutCards()
        {
            VisualElement host = new VisualElement();
            DimensionLayoutTemplateAsset layout = template.LayoutTemplate;
            SerializedObject serialized = new SerializedObject(layout);

            VisualElement what = DimensionsApiControls.Group("Basics", "name and mode");
            VisualElement whatBody = DimensionsApiControls.BodyOf(what);
            whatBody.Add(DimensionsApiControls.Bound(
                serialized,
                "displayName",
                "Name",
                "What this map is called in your project."));
            whatBody.Add(DimensionsApiControls.Bound(
                serialized,
                "layoutKind",
                "Arrangement",
                "Rings around a centre, a grid of cells, hand placed regions, or a painted mask. The game's own world is rings."));
            host.Add(what);

            VisualElement rings = DimensionsApiControls.Group("Rings", "distance from the centre");
            VisualElement ringsBody = DimensionsApiControls.BodyOf(rings);

            // "Ring Width" is deliberately not drawn here. It is read into a variable, passed into
            // BuildRadialRingBands and never mentioned again in the body of that method: the
            // scanline algorithm writes an exact circle, so there is
            // no coarseness to tune. The serialized field stays put, because it is part of
            // every layout fingerprint already pinned into a saved world.
            Label ringsNote = new Label(
                "Each ring is written into the world exactly as a circle — there is nothing to " +
                "tune here.");
            ringsNote.AddToClassList("dim-note");
            ringsBody.Add(ringsNote);

            ringsBody.Add(DimensionsApiControls.Bound(
                serialized,
                "radialRings",
                "Rings",
                "Each ring, from the centre outward, and which biome fills it."));
            host.Add(rings);

            VisualElement regions = DimensionsApiControls.Group("Regions", "placed by hand");
            VisualElement regionsBody = DimensionsApiControls.BodyOf(regions);
            regionsBody.Add(DimensionsApiControls.Bound(
                serialized,
                "regions",
                "Regions",
                "Areas you position yourself, rather than letting the rings decide."));
            regionsBody.Add(DimensionsApiControls.Bound(
                serialized,
                "gridCellSize",
                "Cell Size",
                "Used when the arrangement above is a grid."));
            regionsBody.Add(DimensionsApiControls.Bound(
                serialized,
                "gridCells",
                "Cells",
                "Which biome fills each cell of that grid."));
            host.Add(regions);

            VisualElement mask = DimensionsApiControls.Group("Painted Shape", null);
            VisualElement maskBody = DimensionsApiControls.BodyOf(mask);
            maskBody.Add(DimensionsApiControls.Bound(
                serialized,
                "biomeMask",
                "Mask Image",
                "An image whose colours say which biome goes where. One way to draw a world that rings cannot describe."));
            maskBody.Add(DimensionsApiControls.Bound(
                serialized,
                "maskBiomeMappings",
                "Colour Mapping",
                "Which colour in that picture stands for which of your biomes."));
            maskBody.Add(DimensionsApiControls.Bound(
                serialized,
                "tilesPerMaskPixel",
                "Tiles per Pixel",
                "How much ground one pixel of the picture covers."));
            host.Add(mask);

            VisualElement notes = DimensionsApiControls.Group("Notes", "just for you");
            DimensionsApiControls.BodyOf(notes).Add(DimensionsApiControls.Bound(
                serialized,
                "notes",
                "Notes",
                "A note to yourself. Never shown to a player, never shipped."));
            host.Add(notes);

            return host;
        }
    }
}
