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
    internal sealed partial class DimensionBlockStagePage
    {
        private const float CanvasHeight = 460f;

        private readonly DimensionTilesetStudio studio;
        private readonly System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction;
        private readonly System.Action<DimensionItemAsset> openItem;
        private readonly System.Action repaint;

        /// <summary>
        /// Where this page says something to the creator. Making the glow sheet can fail with a
        /// sentence explaining why, and a sentence that goes to the Console alone is one a creator
        /// who has not opened it never sees.
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
