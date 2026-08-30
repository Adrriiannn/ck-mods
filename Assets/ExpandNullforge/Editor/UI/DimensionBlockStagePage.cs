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
    /// Blocks: the ones this dimension has down the left, the one you picked drawn in the middle,
    /// and everything it can do on the right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The canvas is the only thing here still drawn the old way, and deliberately so: it hit tests
    /// pixels, paints a live scene every frame and is the whole reason the Block Studio exists.
    /// Everything around it is now built from the same controls as every other page, so the block a
    /// creator is looking at reads like the rest of the product instead of like a Unity inspector.
    /// </para>
    /// <para>
    /// Nothing new decides anything. Blocks are still born in the studio's own wizard, states are
    /// still written through the studio's own serialized edits, and the canvas is the studio's own
    /// preview instance. This page asks the questions; the studio still answers them.
    /// </para>
    /// </remarks>
    internal sealed class DimensionBlockStagePage
    {
        private const float CanvasHeight = 460f;

        private readonly DimensionTilesetStudio studio;
        private readonly System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction;
        private readonly System.Action<DimensionItemAsset> openItem;
        private readonly System.Action repaint;

        /// <summary>
        /// Where this page says something to the creator. Making the glow sheet can fail with a
        /// sentence explaining why, and that sentence used to go to the Console alone.
        /// </summary>
        private readonly System.Action<string, MessageType> report;

        private VisualElement root;
        private VisualElement wizardHost;
        private VisualElement columns;
        private VisualElement listHost;
        private VisualElement canvasHost;
        private VisualElement detailHost;
        private VisualElement canvasToolbar;
        private VisualElement stateChips;
        private IMGUIContainer canvasIsland;
        private IMGUIContainer wizardIsland;

        private DimensionTemplateAsset template;
        private DimensionTilesetAsset selected;
        private SerializedObject serialized;
        private string chipSignature = string.Empty;
        private bool wizardShowing;

        internal DimensionBlockStagePage(
            DimensionTilesetStudio blockStudio,
            System.Action<DimensionFrameworkAuthoringAssetActionResult> runAssetAction,
            System.Action<DimensionItemAsset> openBlockItem,
            System.Action repaintWindow,
            System.Action<string, MessageType> reportToCreator)
        {
            studio = blockStudio;
            runAction = runAssetAction;
            openItem = openBlockItem;
            repaint = repaintWindow;
            report = reportToCreator;
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page-root");

            wizardHost = new VisualElement();
            wizardHost.AddToClassList("dim-single-detail");
            wizardHost.style.display = DisplayStyle.None;
            root.Add(wizardHost);

            columns = new VisualElement();
            columns.AddToClassList("dim-stage-page");
            root.Add(columns);

            ScrollView listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.AddToClassList("dim-list-column");
            listHost = new VisualElement();
            listScroll.Add(listHost);
            columns.Add(listScroll);

            ScrollView canvasScroll = new ScrollView(ScrollViewMode.Vertical);
            canvasScroll.AddToClassList("dim-block-canvas");
            canvasHost = new VisualElement();
            canvasScroll.Add(canvasHost);
            columns.Add(canvasScroll);

            ScrollView detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.AddToClassList("dim-detail-column");
            detailHost = new VisualElement();
            detailHost.AddToClassList("dim-detail");
            detailScroll.Add(detailHost);
            columns.Add(detailScroll);

            // The canvas is a live thing: what is selected in it changes under the page rather than
            // through it, so the chips that describe that selection ask it how it is doing.
            root.schedule.Execute(SyncWithCanvas).Every(200);
            return root;
        }

        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            if (root == null)
            {
                return;
            }

            wizardShowing = studio.WizardActive;
            wizardHost.style.display = wizardShowing ? DisplayStyle.Flex : DisplayStyle.None;
            columns.style.display = wizardShowing ? DisplayStyle.None : DisplayStyle.Flex;

            if (wizardShowing)
            {
                RebuildWizard();
                return;
            }

            ResolveSelection();
            RebuildList();
            RebuildCanvas();
            RebuildDetail();
        }

        // ------------------------------------------------------------ the new block ---

        /// <summary>
        /// The block wizard, hosted as it was written. A block is made in one place only, so the
        /// type, the states and the item decision are asked the same way whoever asks for it.
        /// </summary>
        private void RebuildWizard()
        {
            wizardHost.Clear();
            VisualElement group = DimensionsApiControls.Group(
                "New Block",
                "a few questions, then it exists");
            wizardIsland = new IMGUIContainer(DrawWizardIsland);
            wizardIsland.AddToClassList("dim-canvas-island");
            DimensionsApiControls.BodyOf(group).Add(wizardIsland);
            wizardHost.Add(group);
        }

        private void DrawWizardIsland()
        {
            Color previousBackground;
            Color previousContent;
            DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
            DimensionTilesetStudio.DrawResult drawn;
            try
            {
                drawn = studio.DrawWizardIsland();
            }
            finally
            {
                DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
            }

            if (drawn.WizardRequest != null)
            {
                DimensionTilesetWizardRequest request = drawn.WizardRequest;
                root.schedule.Execute(() => CreateBlock(request));
                return;
            }

            // Backing out of the wizard has to bring the page back, and the wizard is the only one
            // that knows it happened.
            if (wizardShowing != studio.WizardActive)
            {
                root.schedule.Execute(() => Refresh(template));
            }
        }

        /// <summary>Opens the one path a block is ever made by.</summary>
        private void StartWizard()
        {
            studio.BeginWizard();
            DeferredRefresh();
            repaint();
        }

        private void CreateBlock(DimensionTilesetWizardRequest request)
        {
            if (template == null || request == null)
            {
                return;
            }

            runAction(DimensionFrameworkAuthoringAssetUtility.CreateWizardBlock(template, request));
            DimensionTilesetAsset[] blocks = template.Tilesets ?? new DimensionTilesetAsset[0];
            for (int i = blocks.Length - 1; i >= 0; i--)
            {
                if (blocks[i] != null)
                {
                    selected = blocks[i];
                    break;
                }
            }

            Refresh(template);
        }

        // ------------------------------------------------------------------ the list ---

        private void ResolveSelection()
        {
            DimensionTilesetAsset[] blocks = template == null
                ? new DimensionTilesetAsset[0]
                : template.Tilesets ?? new DimensionTilesetAsset[0];

            bool stillThere = false;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != null && blocks[i] == selected)
                {
                    stillThere = true;
                    break;
                }
            }

            if (stillThere)
            {
                return;
            }

            selected = null;
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != null)
                {
                    selected = blocks[i];
                    return;
                }
            }
        }

        private void RebuildList()
        {
            listHost.Clear();
            if (template == null)
            {
                return;
            }

            DimensionTilesetAsset[] blocks = template.Tilesets ?? new DimensionTilesetAsset[0];
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != null)
                {
                    listHost.Add(BuildBlockCard(blocks[i]));
                }
            }

            Button add = new Button(StartWizard)
            {
                text = "+ New tileset"
            };
            add.AddToClassList("dim-add-card");
            add.tooltip = "Answers a few questions and makes the block. What kind it is, whether it " +
                          "can be farmed, and how it reaches a player's hands.";
            listHost.Add(add);
        }

        private VisualElement BuildBlockCard(DimensionTilesetAsset block)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");
            if (block == selected)
            {
                card.AddToClassList("dim-item-card-selected");
            }

            Label name = new Label(block.BlockName);
            name.AddToClassList("dim-item-card-name");
            card.Add(name);

            Label kind = new Label(DescribeKind(block));
            kind.AddToClassList("dim-item-card-sub");
            card.Add(kind);

            if (!block.Enabled)
            {
                VisualElement chips = DimensionsApiControls.ChipRow();
                chips.Add(DimensionsApiControls.Chip("left out", "warn"));
                card.Add(chips);
            }

            card.RegisterCallback<MouseDownEvent>(evt =>
            {
                selected = block;
                evt.StopPropagation();
                DeferredRefresh();
                repaint();
            });
            return card;
        }

        private static string DescribeKind(DimensionTilesetAsset block)
        {
            string kind = block.BlockType.DisplayName;
            if (block.ItemMode == DimensionTilesetItemMode.ReskinVanilla)
            {
                return kind + ", reskinning " +
                       DimensionVanillaTilesetCatalog.NameOf(block.ReskinTilesetIndex);
            }

            if (block.ItemMode == DimensionTilesetItemMode.CreateItem)
            {
                return kind + ", with its own item";
            }

            return kind + ", with no item";
        }

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

        // --------------------------------------------------------------- the settings ---

        private void RebuildDetail()
        {
            detailHost.Clear();
            if (template == null)
            {
                detailHost.Add(DimensionsApiControls.EmptyState(
                    "No dimension open",
                    "Open a dimension from Home and its blocks appear here.",
                    null));
                return;
            }

            if (selected == null)
            {
                detailHost.Add(DimensionsApiControls.EmptyState(
                    "No blocks yet",
                    "A block is a whole material: the ground you walk on and the wall you mine " +
                    "through, drawn from one sheet of art. Everything else in a dimension sits on top of them.",
                    DimensionsApiControls.PrimaryButton("Create your first block", StartWizard)));
                return;
            }

            serialized = new SerializedObject(selected);
            serialized.UpdateIfRequiredOrScript();

            DimensionTilesetType type = selected.BlockType;
            if (type.HasStates)
            {
                EnsureStateRows();
            }

            detailHost.Add(BuildWhatItIs(type));
            detailHost.Add(BuildHowItLooks(type));
            detailHost.Add(BuildOnTheMap(type));
            detailHost.Add(BuildWhatItDoes(type));
            detailHost.Add(BuildWhatCanBeMined(type));
            detailHost.Add(BuildReadyForTheGame());
        }

        /// <summary>
        /// The state rows the controls below bind to have to exist before anything can be bound to
        /// them, and a block only grows a row the first time it is asked about one.
        /// </summary>
        private void EnsureStateRows()
        {
            IReadOnlyList<(string Key, string Label, string Blurb)> cover =
                DimensionTilesetStudio.GroundCover;
            for (int i = 0; i < cover.Count; i++)
            {
                studio.LayerProperty(serialized, cover[i].Key, true);
            }

            studio.LayerProperty(serialized, "tilled", true);
            studio.LayerProperty(serialized, "watered", true);
            studio.LayerProperty(serialized, "flooded", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private VisualElement BuildWhatItIs(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group("Basics", "name and kind");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "blockName",
                "Name",
                "What players see this called. The wall version adds the word Block, and the " +
                "ground version is named after it too."));

            Label kind = new Label(type.DisplayName);
            kind.AddToClassList("dim-readonly-value");
            body.Add(DimensionsApiControls.Field(
                "Kind",
                "Chosen when the block was made, because it decides which parts of the game the " +
                "art feeds. To change it, make another block.",
                kind));

            body.Add(BuildItemModeRow());

            body.Add(Revealing(
                "enabled",
                "Enabled",
                "Turn this off to leave the block out of the next build without deleting anything.",
                true));
            return group;
        }

        private VisualElement BuildItemModeRow()
        {
            switch (selected.ItemMode)
            {
                case DimensionTilesetItemMode.CreateItem:
                {
                    Label value = new Label("Generated automatically");
                    value.AddToClassList("dim-readonly-value");
                    return DimensionsApiControls.Field(
                        "Item",
                        "One item, exactly like a vanilla block. Ground on open terrain, wall where " +
                        "there is already ground, and mining it gives the item back.",
                        value);
                }

                case DimensionTilesetItemMode.ReskinVanilla:
                    return BuildReskinPicker();

                default:
                {
                    Label value = new Label("None");
                    value.AddToClassList("dim-readonly-value");
                    return DimensionsApiControls.Field(
                        "Item",
                        "Nobody can hold this block. It exists for the world to generate and for " +
                        "your scenes to place.",
                        value);
                }
            }
        }

        /// <summary>Which of the game's own blocks this one wears the clothes of.</summary>
        private VisualElement BuildReskinPicker()
        {
            List<string> names = new List<string>();
            List<int> indices = new List<int>();
            int current = -1;
            foreach (DimensionVanillaTilesetEntry entry in DimensionVanillaTilesetCatalog.All)
            {
                if (entry.TilesetIndex == selected.ReskinTilesetIndex)
                {
                    current = names.Count;
                }

                names.Add(entry.HasGround
                    ? entry.DisplayName + " (wall and ground)"
                    : entry.DisplayName + " (wall)");
                indices.Add(entry.TilesetIndex);
            }

            DropdownField picker = new DropdownField();
            picker.choices = names;
            if (current >= 0)
            {
                picker.SetValueWithoutNotify(names[current]);
            }

            picker.RegisterValueChangedCallback(evt =>
            {
                int at = picker.index;
                if (at < 0)
                {
                    return;
                }

                serialized.Update();
                SerializedProperty target = serialized.FindProperty("reskinTilesetIndex");
                if (target != null)
                {
                    target.intValue = indices[at];
                    serialized.ApplyModifiedProperties();
                }

                // The card in the list says what this block reskins, so it has to hear about it.
                DeferredRefresh();
                repaint();
            });

            VisualElement host = new VisualElement();
            host.Add(DimensionsApiControls.Field(
                "Replaces",
                "Every tile of that block wears your art, in every world, while this one is " +
                "included. It only changes the look, so old saves stay valid. A biome's ground and " +
                "wall share one block, so both are covered.",
                picker));

            if (current < 0)
            {
                host.Add(Note(
                    "Nothing is chosen yet, so this block replaces nothing and never appears.",
                    true));
            }

            return host;
        }

        private VisualElement BuildHowItLooks(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group("Appearance", "art and surface");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(BuildSheetField(
                "tilesetTexture",
                "Artwork",
                "One sheet holding every piece of this block: wall tops, ground, edges and the bits " +
                "that join them. Draw it on the game's own dirt layout and drop it here."));

            string sizeProblem = DescribeArtSize(selected.TilesetTexture);
            if (selected.TilesetTexture == null)
            {
                body.Add(Note("Until there is art here the block is invisible, and the canvas has " +
                              "nothing to draw.", false));
            }
            else if (sizeProblem != null)
            {
                body.Add(Note(sizeProblem, true));
            }

            body.Add(Revealing(
                "isEmissive",
                "Glow",
                "For lava, crystal and anything else that makes its own light. Most blocks leave " +
                "this off.",
                false));

            if (selected.IsEmissive)
            {
                body.Add(BuildSheetField(
                    "emissiveTexture",
                    "Glow Artwork",
                    "A second sheet, laid out the same way, saying which pixels give off light."));

                SerializedProperty glow = serialized.FindProperty("emissiveTexture");
                if (glow != null && glow.objectReferenceValue == null)
                {
                    Button make = DimensionsApiControls.GhostButton("Generate Glow Artwork", MakeGlowSheet);
                    make.tooltip = "Writes a glow sheet next to the art, starting from its brightest " +
                                   "pixels. Paint out what should stay dark and paint in whatever it missed.";
                    body.Add(make);
                }
            }

            body.Add(Revealing(
                "rigidSurface",
                "Rigid",
                "Tiles sit square instead of taking the gentle wobble the game gives hand drawn " +
                "terrain. Right for metal plating, glass and circuitry, where a crooked edge reads " +
                "as a mistake. Walls are already straight in the game.",
                false));

            if (selected.RigidSurface && !selected.GenerateBlock)
            {
                body.Add(Note(
                    "This block makes nothing a player can place, so there is no object for the " +
                    "straight setting to live on and it will do nothing in game.",
                    true));
            }

            if (type.HasStates)
            {
                body.Add(BuildCircuitFloor());
            }

            return group;
        }

        private VisualElement BuildCircuitFloor()
        {
            VisualElement host = new VisualElement();
            Toggle toggle = new Toggle();
            SerializedProperty circuit = serialized.FindProperty("circuitFloor");
            if (circuit == null)
            {
                return host;
            }

            toggle.BindProperty(circuit);
            DimensionsApiControls.AfterBinding(toggle, () =>
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        serialized.Update();
                        DimensionTilesetStudio.EnsureCircuitFloorMaterial(serialized);
                        serialized.ApplyModifiedProperties();
                    }

                    DeferredDetail();
                }));

            host.Add(DimensionsApiControls.Field(
                "Circuit Floor",
                "The ground keeps its own art, dark glass with sleeping traces, and the glow art " +
                "lights up. Rare pulses travel across it on their own, and real powered wiring " +
                "nearby lights it properly.",
                toggle));

            if (selected.CircuitFloor && selected.CircuitFloorMaterial == null)
            {
                host.Add(Note(
                    "The framework's circuit surface is missing from " +
                    DimensionTilesetStudio.CircuitFloorMaterialAssetPath + ", so this will not glow.",
                    true));
            }

            return host;
        }

        private VisualElement BuildOnTheMap(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group("Map Colours", "colours and weather");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (type.Role == DimensionBlockRole.Terrain)
            {
                body.Add(DimensionsApiControls.Bound(
                    serialized,
                    "groundMapColor",
                    "Ground Colour",
                    "The colour this block's floor is drawn in on the world map a player opens."));
                body.Add(DimensionsApiControls.Bound(
                    serialized,
                    "wallMapColor",
                    "Wall Colour",
                    "The colour this block's walls are drawn in on the world map a player opens."));
            }

            if (type.HasStates)
            {
                body.Add(Revealing(
                    "hasGroundFog",
                    "Ground Fog",
                    "Low fog lying on the floor, the way it does in the mold biome. Only the ground " +
                    "carries it. Walls never do.",
                    false));

                if (selected.HasGroundFog)
                {
                    body.Add(DimensionsApiControls.Bound(
                        serialized,
                        "groundFogTint",
                        "Fog Colour",
                        "The alpha channel is how thick the fog is, not how see through it is. Full " +
                        "alpha is far heavier than the swatch suggests. The mold biome sits near a third."));
                }
            }

            if (body.childCount == 0)
            {
                body.Add(Note(
                    "This kind of block is not part of the terrain, so the world map never draws it.",
                    false));
            }

            return group;
        }

        private VisualElement BuildWhatItDoes(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group("Behaviour", "underfoot, and when a world grows");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!type.HasStates)
            {
                body.Add(Note(
                    "This kind of block has one surface and nothing optional about it. What you draw " +
                    "is the whole story.",
                    false));
                return group;
            }

            Toggle farm = new Toggle();
            farm.SetValueWithoutNotify(selected.IsStateEnabled("tilled"));
            farm.RegisterValueChangedCallback(evt =>
            {
                serialized.Update();
                studio.SetFarmable(serialized, evt.newValue);
                serialized.ApplyModifiedProperties();
                repaint();
            });
            body.Add(DimensionsApiControls.Field(
                "Farmable",
                "A hoe turns it into tilled soil and a watering can waters it, both drawn with this " +
                "block's own art rather than plain dirt.",
                farm));

            Label coverCaption = new Label("Ground Cover");
            coverCaption.AddToClassList("dim-h3");
            body.Add(coverCaption);

            if (selected.ItemMode == DimensionTilesetItemMode.ReskinVanilla)
            {
                // A reskin's tiles carry the VANILLA tileset id in the world and in the save, and
                // cover is looked up by the tileset a tile carries — so a rule registered under this
                // block's own id could never be matched. The registration is skipped on purpose
                // (DimensionTilesetAssetRuntime returns at the reskin branch before it), and four
                // switches that do nothing are worse than none.
                body.Add(Note(
                    "A reskin changes how the game's own block looks. What grows on it stays the " +
                    "game's decision, so there is nothing to set here.",
                    false));
            }
            else
            {
                body.Add(Note(
                    "What a world scatters over this block when it grows one. The same world puts them " +
                    "in the same places every time.",
                    false));

                IReadOnlyList<(string Key, string Label, string Blurb)> cover =
                    DimensionTilesetStudio.GroundCover;
                for (int i = 0; i < cover.Count; i++)
                {
                    body.Add(BuildCoverRow(cover[i].Key, cover[i].Label, cover[i].Blurb));
                }
            }

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "surfaceBehaviour",
                "Ground Hazard",
                "For ground that is itself the danger, the way mold is. Nothing on top of it is " +
                "needed. Leave this as None for an ordinary floor, because every single step on it " +
                "counts."));

            body.Add(DimensionsApiControls.Bound(
                serialized,
                "slimeBehaviour",
                "Slime Hazard",
                "Borrow one of the game's own slimes. Your art stays and its behaviour comes with " +
                "it. Plain slime is decoration, and the rest burn, poison, slip or drench."));

            return group;
        }

        /// <summary>One thing that grows on the block, and how much of it there is.</summary>
        private VisualElement BuildCoverRow(string key, string label, string blurb)
        {
            SerializedProperty layer = studio.LayerProperty(serialized, key, true);
            if (layer == null)
            {
                return new VisualElement();
            }

            SerializedProperty enabled = layer.FindPropertyRelative("enabled");
            SerializedProperty density = layer.FindPropertyRelative("density");
            if (enabled == null || density == null)
            {
                return new VisualElement();
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("dim-density");

            Toggle toggle = new Toggle();
            toggle.BindProperty(enabled);
            toggle.AddToClassList("dim-density-toggle");
            row.Add(toggle);

            Slider slider = new Slider(0f, 1f);
            slider.BindProperty(density);
            slider.SetEnabled(enabled.boolValue);
            row.Add(slider);

            Label amount = new Label(Percent(density.floatValue));
            amount.AddToClassList("dim-density-value");
            row.Add(amount);

            slider.RegisterValueChangedCallback(evt => amount.text = Percent(evt.newValue));
            toggle.RegisterValueChangedCallback(evt =>
            {
                slider.SetEnabled(evt.newValue);
                repaint();
            });

            return DimensionsApiControls.Field(label, blurb, row);
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "% of the ground";
        }

        // ------------------------------------------------------------------- the ore ---

        private VisualElement BuildWhatCanBeMined(DimensionTilesetType type)
        {
            VisualElement group = DimensionsApiControls.Group(
                "Ores",
                "veins in its walls");
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!type.HasStates)
            {
                body.Add(Note("Only terrain blocks have walls for a vein to sit in.", false));
                return group;
            }

            body.Add(Note(
                "Veins this block's walls can hold. The vein art comes from the ore corner of your " +
                "sheet, and building the mod wires the rest.",
                false));
            body.Add(Note(
                "One ore per block. The game finds a vein by taking the first match on this block, " +
                "so anything after the first could never be reached. The game has that same " +
                "limit in its own blocks.",
                false));

            SerializedProperty ores = serialized.FindProperty("ores");
            VisualElement chips = DimensionsApiControls.ChipRow();
            if (ores != null && ores.isArray && ores.arraySize > 0)
            {
                for (int i = 0; i < ores.arraySize; i++)
                {
                    SerializedProperty element = ores.GetArrayElementAtIndex(i);
                    SerializedProperty id = element.FindPropertyRelative("oreItemId");
                    SerializedProperty custom = element.FindPropertyRelative("isCustomItem");
                    if (id == null || custom == null)
                    {
                        continue;
                    }

                    chips.Add(BuildOreChip(i, id.stringValue, custom.boolValue));
                }
            }

            if (chips.childCount == 0)
            {
                Label none = DimensionsApiControls.Chip("nothing to mine yet");
                chips.Add(none);
            }

            body.Add(DimensionsApiControls.Field(
                "Ore Veins",
                "Click one to take it away. Whatever is listed first is the one the game will find.",
                chips));

            // The vein numbers, which used to live only on a page nothing routes to. Without
            // them here, every ore this page added was doomed to paint-only regardless of the
            // defaults, since the author had no way to see or change how much of it grows.
            if (ores != null && ores.isArray && ores.arraySize > 0)
            {
                SerializedProperty first = ores.GetArrayElementAtIndex(0);
                SerializedProperty abundance = first.FindPropertyRelative("abundance");
                SerializedProperty veinMin = first.FindPropertyRelative("veinSizeMin");
                SerializedProperty veinMax = first.FindPropertyRelative("veinSizeMax");
                if (abundance != null && veinMin != null && veinMax != null)
                {
                    Slider abundanceSlider = new Slider(0f, 5f)
                    {
                        value = abundance.floatValue,
                        showInputField = true
                    };
                    abundanceSlider.RegisterValueChangedCallback(evt =>
                    {
                        serialized.Update();
                        SerializedProperty ownOres = serialized.FindProperty("ores");
                        if (ownOres != null && ownOres.arraySize > 0)
                        {
                            ownOres.GetArrayElementAtIndex(0)
                                .FindPropertyRelative("abundance").floatValue = evt.newValue;
                            serialized.ApplyModifiedProperties();
                        }
                    });
                    body.Add(DimensionsApiControls.Field(
                        "How Much Grows",
                        "About how many veins per 100 wall tiles. 0 means paint-only: veins " +
                        "appear only where you placed one yourself.",
                        abundanceSlider));

                    MinMaxSlider veinSize = new MinMaxSlider(
                        veinMin.intValue, veinMax.intValue, 1, 16);
                    veinSize.RegisterValueChangedCallback(evt =>
                    {
                        serialized.Update();
                        SerializedProperty ownOres = serialized.FindProperty("ores");
                        if (ownOres != null && ownOres.arraySize > 0)
                        {
                            SerializedProperty el = ownOres.GetArrayElementAtIndex(0);
                            el.FindPropertyRelative("veinSizeMin").intValue =
                                Mathf.RoundToInt(evt.newValue.x);
                            el.FindPropertyRelative("veinSizeMax").intValue =
                                Mathf.Max(
                                    Mathf.RoundToInt(evt.newValue.x),
                                    Mathf.RoundToInt(evt.newValue.y));
                            serialized.ApplyModifiedProperties();
                        }
                    });
                    body.Add(DimensionsApiControls.Field(
                        "Vein Size",
                        "Smallest and largest vein, in blocks. The game's own run 3 to 6.",
                        veinSize));
                }
            }

            body.Add(BuildOreAdder());
            return group;
        }

        private VisualElement BuildOreChip(int index, string storedValue, bool custom)
        {
            string label = custom ? NameOfOwnItem(storedValue) : Spaced(storedValue);
            Button chip = new Button(() => RemoveOre(index))
            {
                text = label + "  ×"
            };
            chip.AddToClassList("dim-chip");
            chip.AddToClassList("dim-chip-removable");
            if (custom)
            {
                chip.AddToClassList("dim-chip-link");
            }

            chip.tooltip = custom
                ? "One of your own things, dropped by this block's walls. Click to take it away."
                : "One of the game's own ores, dropped by this block's walls. Click to take it away.";
            return chip;
        }

        private void RemoveOre(int index)
        {
            serialized.Update();
            SerializedProperty ores = serialized.FindProperty("ores");
            if (ores == null || !ores.isArray || index < 0 || index >= ores.arraySize)
            {
                return;
            }

            ores.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            DeferredDetail();
            repaint();
        }

        /// <summary>
        /// Picking what a vein drops, by name. The game's own ores first, then anything this
        /// dimension makes, so nobody has to know what an item is called behind the scenes.
        /// </summary>
        private VisualElement BuildOreAdder()
        {
            List<string> labels = new List<string>();
            List<string> values = new List<string>();
            List<bool> customFlags = new List<bool>();

            IReadOnlyList<string> vanilla = DimensionTilesetStudio.VanillaOres;
            for (int i = 0; i < vanilla.Count; i++)
            {
                labels.Add(Spaced(vanilla[i]));
                values.Add(vanilla[i]);
                customFlags.Add(false);
            }

            DimensionItemAsset[] mine = template == null
                ? new DimensionItemAsset[0]
                : template.GlobalItems;
            for (int i = 0; i < mine.Length; i++)
            {
                DimensionItemAsset item = mine[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                string shown = string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;
                labels.Add("Yours: " + shown);
                values.Add(item.ItemId);
                customFlags.Add(true);
            }

            if (labels.Count == 0)
            {
                return Note("There is nothing to put in a vein yet. Make an item first.", false);
            }

            DropdownField adder = new DropdownField();
            adder.choices = labels;
            adder.RegisterValueChangedCallback(evt =>
            {
                int at = adder.index;
                if (at >= 0)
                {
                    AddOre(values[at], customFlags[at]);
                }
            });

            return DimensionsApiControls.Field(
                "Add Ore",
                "Pick what a vein in this block drops. One of the game's ores makes a vein pointing " +
                "at the real thing, and one of yours becomes the vein itself.",
                adder);
        }

        private void AddOre(string value, bool custom)
        {
            serialized.Update();
            SerializedProperty ores = serialized.FindProperty("ores");
            if (ores == null || !ores.isArray)
            {
                return;
            }

            int index = ores.arraySize;
            ores.InsertArrayElementAtIndex(index);
            SerializedProperty added = ores.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("oreItemId").stringValue = value;
            added.FindPropertyRelative("isCustomItem").boolValue = custom;
            // InsertArrayElementAtIndex knows nothing of field initializers: it clones the
            // previous element or zero-fills. A zero abundance means paint-only — the runtime
            // drops the rule and every ore added here silently never grew a vein. Seed the
            // class's own defaults explicitly.
            added.FindPropertyRelative("abundance").floatValue = 1f;
            added.FindPropertyRelative("veinSizeMin").intValue = 3;
            added.FindPropertyRelative("veinSizeMax").intValue = 6;
            serialized.ApplyModifiedProperties();
            DeferredDetail();
            repaint();
        }

        private string NameOfOwnItem(string itemId)
        {
            DimensionItemAsset[] mine = template == null
                ? new DimensionItemAsset[0]
                : template.GlobalItems;
            for (int i = 0; i < mine.Length; i++)
            {
                if (mine[i] != null && string.Equals(mine[i].ItemId, itemId, System.StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(mine[i].DisplayName) ? mine[i].name : mine[i].DisplayName;
                }
            }

            // Never the stored id. If the item behind it is gone, say so in words rather than
            // showing a name only the framework uses.
            return "Something of yours that is no longer here";
        }

        // ------------------------------------------------------- baking and clearing ---

        private VisualElement BuildReadyForTheGame()
        {
            VisualElement group = DimensionsApiControls.Group("Manage", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (selected.ItemMode == DimensionTilesetItemMode.CreateItem && selected.GenerateBlock)
            {
                body.Add(Note(
                    "One item, exactly like a vanilla block. Ground on open terrain, wall where " +
                    "there is already ground, and it drops itself. Icon, description and rarity " +
                    "live on the item.",
                    false));
                Button edit = DimensionsApiControls.GhostButton(
                    "Open " + selected.BlockName + " Block",
                    OpenBlockItem);
                edit.tooltip = "Jumps to the item this block makes, where its icon, description and " +
                               "rarity are set.";
                body.Add(edit);
            }
            else if (selected.ItemMode == DimensionTilesetItemMode.CreateItem)
            {
                body.Add(Note(
                    "An item for this kind of block is coming later. The block itself already works.",
                    false));
            }

            bool artReady = selected.TilesetTexture != null;
            bool layoutReady = DimensionTilesetAtlas.IsReady;
            Button bake = DimensionsApiControls.PrimaryButton(
                selected.HasGeneratedGen ? "Rebake Artwork" : "Bake Artwork",
                BakeArt);
            bake.tooltip = "Works out every edge, corner and join from your sheet and stores the " +
                           "result, so the game draws this block properly instead of borrowing dirt.";
            bake.SetEnabled(artReady && layoutReady);
            body.Add(bake);

            if (!artReady)
            {
                body.Add(Note("Give the block some art first.", false));
            }
            else if (!layoutReady)
            {
                // The atlas has its own status line, and it is written for whoever built the atlas.
                // What matters here is the one thing that fixes it.
                body.Add(Note(
                    "Play the mod once so the framework can learn how the game lays a block out. " +
                    "Baking needs that layout, and it only has to happen the one time.",
                    false));
            }
            else if (selected.HasGeneratedGen)
            {
                VisualElement chips = DimensionsApiControls.ChipRow();
                chips.Add(DimensionsApiControls.Chip(
                    "baked, " + selected.GeneratedGen.Count + " sheets",
                    "ready"));
                body.Add(chips);
            }

            Button remove = DimensionsApiControls.GhostButton("Delete Block", DeleteBlock);
            remove.tooltip = "Removes the block and everything baked from it. Any item it made stays " +
                             "in your list of things.";
            body.Add(remove);
            return group;
        }

        private void OpenBlockItem()
        {
            DimensionItemAsset item =
                DimensionFrameworkAuthoringAssetUtility.EnsureAndGetTilesetBlockItem(
                    template, selected, true);
            if (item != null)
            {
                openItem(item);
            }
        }

        private void BakeArt()
        {
            runAction(DimensionFrameworkAuthoringAssetUtility.GenerateTilesetData(selected));
            DeferredDetail();
            MarkCanvasDirty();
        }

        private void DeleteBlock()
        {
            if (selected == null || !EditorUtility.DisplayDialog(
                    "Delete block",
                    "Delete \"" + selected.BlockName + "\"? Any block item it made stays in your " +
                    "list of things.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            runAction(DimensionFrameworkAuthoringAssetUtility.DeleteTileset(template, selected));
            selected = null;
            DeferredRefresh();
        }

        // ------------------------------------------------------------------ plumbing ---

        /// <summary>
        /// A sheet field that fixes its own import. Art dropped in from outside arrives smoothed and
        /// squashed, which is exactly wrong for pixels, and nobody should have to know that.
        /// </summary>
        private VisualElement BuildSheetField(string propertyPath, string label, string tooltip)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return new VisualElement();
            }

            ObjectField field = new ObjectField();
            field.objectType = typeof(Texture2D);
            field.allowSceneObjects = false;
            field.BindProperty(property);
            // AfterBinding is load-bearing here, not hygiene: this handler force-reimports the
            // sheet AND rebuilds the column that hosts this very field. Fired by the bind's own
            // initial event, that pair was a self-sustaining import loop — the busy cursor
            // flickering forever while BreachGate1.png reimported every editor tick.
            DimensionsApiControls.AfterBinding(field, () =>
                field.RegisterValueChangedCallback(evt =>
                {
                    DimensionTilesetStudio.ApplySheetImport(evt.newValue as Texture2D);
                    DeferredDetail();
                    MarkCanvasDirty();
                }));

            return DimensionsApiControls.Field(label, tooltip, field);
        }

        /// <summary>
        /// Tells the creator what a button on this page did, wherever the shell puts messages.
        /// </summary>
        /// <remarks>
        /// ONE ROUTE FOR BOTH OUTCOMES, which it was not. Only the failure branch spoke, so a
        /// creator who clicked "Build the glow sheet" and got a sheet was told nothing and could
        /// not tell it apart from a click that missed — the same complaint the portal page's
        /// PrepareRule already answers by reporting both. And the Console fallback was reachable
        /// only in a branch production never takes, because the shell always supplies a reporter;
        /// routed through here it is the one fallback for every message this page has.
        /// </remarks>
        private void Say(string message, MessageType type)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (report != null)
            {
                report(message, type);
                return;
            }

            if (type == MessageType.Error)
            {
                Debug.LogError("[ExpandNullforge] " + message);
            }
            else if (type == MessageType.Warning)
            {
                Debug.LogWarning("[ExpandNullforge] " + message);
            }
            else
            {
                Debug.Log("[ExpandNullforge] " + message);
            }
        }

        private void MakeGlowSheet()
        {
            Texture2D created;
            string error;
            if (!Generation.DimensionTilesetEmissiveSheet.TryCreate(selected, out created, out error))
            {
                Say(error, MessageType.Warning);
                return;
            }

            serialized.Update();
            SerializedProperty glow = serialized.FindProperty("emissiveTexture");
            if (glow == null)
            {
                // The sheet was written and there is nowhere on this asset to hang it, which is a
                // worse outcome than the failure above and used to be the quietest one.
                Say(
                    "The glow sheet was drawn and this block has no glow slot to put it in, so "
                        + "nothing on the block changed. The file is at " +
                        AssetDatabase.GetAssetPath(created) + ".",
                    MessageType.Warning);
                return;
            }

            glow.objectReferenceValue = created;
            serialized.ApplyModifiedProperties();

            Say(
                "Built the glow sheet for " + created.name + " and put it on the block.",
                MessageType.Info);

            DeferredDetail();
            MarkCanvasDirty();
        }

        /// <summary>
        /// A yes or no answer that decides which questions come after it, so the page has to be
        /// put back together once it changes. Everything else can simply bind and be left alone.
        /// </summary>
        private VisualElement Revealing(
            string propertyPath,
            string label,
            string tooltip,
            bool wholePage)
        {
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return DimensionsApiControls.Bound(serialized, propertyPath, label, tooltip);
            }

            Toggle toggle = new Toggle();
            toggle.BindProperty(property);
            DimensionsApiControls.AfterBinding(toggle, () =>
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (wholePage)
                    {
                        DeferredRefresh();
                    }
                    else
                    {
                        DeferredDetail();
                    }

                    repaint();
                }));
            return DimensionsApiControls.Field(label, tooltip, toggle);
        }

        /// <summary>
        /// Rebuilds after the click that asked for it has finished. A page that tears down the very
        /// button being clicked leaves the event with nowhere to land.
        /// </summary>
        private void DeferredRefresh()
        {
            if (root != null)
            {
                root.schedule.Execute(() => Refresh(template));
            }
        }

        private void DeferredDetail()
        {
            if (root != null)
            {
                root.schedule.Execute(RebuildDetail);
            }
        }

        private static VisualElement Note(string text, bool warning)
        {
            Label note = new Label(text);
            note.AddToClassList("dim-note");
            if (warning)
            {
                note.AddToClassList("dim-note-warn");
            }

            return note;
        }

        /// <summary>
        /// Art of the wrong size, said plainly. Every tile is read from a fixed spot on the game's
        /// own layout, so a sheet of another size is not stretched, it is simply read wrong.
        /// </summary>
        private static string DescribeArtSize(Texture2D sheet)
        {
            int width;
            int height;
            if (sheet == null || !DimensionTilesetAtlas.TryGetSourceSheetSize(out width, out height))
            {
                return null;
            }

            if (sheet.width == width && sheet.height == height)
            {
                return null;
            }

            return "This art is " + sheet.width + " by " + sheet.height + " pixels and the game's " +
                   "layout is " + width + " by " + height + ". Every tile is read from a fixed spot " +
                   "on that layout, so art of another size bakes into nonsense. Resize the canvas " +
                   "without scaling what you drew, then drop it in again.";
        }

        /// <summary>"CopperOre" reads as "Copper Ore" once it is in front of a person.</summary>
        private static string Spaced(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(raw[i]);
            }

            return builder.ToString();
        }
    }
}
