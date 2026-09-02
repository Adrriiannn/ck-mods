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
    /// The panel beside the canvas: what the block is, how it looks, what it does.
    /// </summary>
    internal sealed partial class DimensionBlockStagePage
    {
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

            // THE CARD, on the page it was written for and never reached. Everything a block
            // rename needs already existed and was reachable by tests only: the token-collision
            // refusal that catches "Eerie Stone" against "Eerie-Stone", the saved-world warning,
            // the pin, and the pair of answers. This is the call site that makes them a screen.
            VisualElement identity = DimensionIdentityCard.Build(
                template, selected, false, () => Refresh(template));
            if (identity != null)
            {
                detailHost.Add(identity);
            }
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

            // SHOWN, NOT EDITED HERE. A block's name is not only what players read: the number a
            // saved world writes into every tile of it is worked out from this, and the two block
            // items in players' chests are named after it. It was a bound text box that committed
            // on every keystroke and swept nothing. The card at the foot of this page refuses a
            // name another block already is, says what a played world loses, and offers the two
            // answers a block rename has — keep the identity, or start fresh.
            Label blockName = new Label(selected.BlockName);
            blockName.AddToClassList("dim-readonly-value");
            body.Add(DimensionsApiControls.Field(
                "Name",
                "What players see this called. The wall version adds the word Block, and the " +
                "ground version is named after it too. To change it, use \"Its name, and what " +
                "points at it\" at the foot of this page.",
                blockName));

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
    }
}
