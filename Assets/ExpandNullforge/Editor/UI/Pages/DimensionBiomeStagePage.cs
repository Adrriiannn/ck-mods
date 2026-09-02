using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Biomes stage, built the way the design says a biome should read: the list of biomes on
    /// the left, and on the right one card per question a creator actually asks. What is it, what
    /// is it made of, how does it feel, and where is it used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the first stage body rebuilt in UI Toolkit rather than hosted as a legacy IMGUI
    /// panel. It edits the same <see cref="BiomeTemplateAsset"/> through
    /// <see cref="SerializedObject"/> bindings, so undo, prefab overrides and dirty tracking all
    /// behave exactly as they did before.
    /// </para>
    /// <para>
    /// The grouping is not decoration. A biome's fields were previously spread across three
    /// sections that each drew part of the same asset, which is what made the page feel like a
    /// Unity inspector rather than a tool.
    /// </para>
    /// </remarks>
    internal sealed class DimensionBiomeStagePage
    {
        private readonly System.Action<BiomeTemplateAsset> createBiome;
        private readonly System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction;
        private readonly System.Action repaint;

        private VisualElement root;
        private VisualElement listHost;
        private VisualElement detailHost;
        private DimensionTemplateAsset template;
        private BiomeTemplateAsset selected;

        internal DimensionBiomeStagePage(
            System.Action<BiomeTemplateAsset> createBiome,
            System.Action<DimensionFrameworkAuthoringAssetActionResult> runAction,
            System.Action repaint)
        {
            this.createBiome = createBiome;
            this.runAction = runAction;
            this.repaint = repaint;
        }

        internal VisualElement Build()
        {
            root = new VisualElement();
            root.AddToClassList("dim-stage-page");

            listHost = new VisualElement();
            listHost.AddToClassList("dim-biome-list");
            root.Add(listHost);

            ScrollView detailScroll = new ScrollView(ScrollViewMode.Vertical);
            detailScroll.AddToClassList("dim-biome-detail-scroll");
            detailHost = new VisualElement();
            detailHost.AddToClassList("dim-biome-detail");
            detailScroll.Add(detailHost);
            root.Add(detailScroll);
            return root;
        }

        /// <summary>Points the page at a dimension and redraws both columns.</summary>
        internal void Refresh(DimensionTemplateAsset dimensionTemplate)
        {
            template = dimensionTemplate;
            BiomeTemplateAsset[] biomes = template == null ? null : template.Biomes;
            if (biomes != null && (selected == null || System.Array.IndexOf(biomes, selected) < 0))
            {
                selected = biomes.Length > 0 ? biomes[0] : null;
            }

            RebuildList(biomes);
            RebuildDetail();
        }

        private void RebuildList(BiomeTemplateAsset[] biomes)
        {
            if (listHost == null)
            {
                return;
            }

            listHost.Clear();
            if (biomes != null)
            {
                for (int i = 0; i < biomes.Length; i++)
                {
                    listHost.Add(BuildBiomeCard(biomes[i]));
                }
            }

            Button add = new Button(() => createBiome(null)) { text = "+ New biome" };
            add.AddToClassList("dim-add-card");
            listHost.Add(add);
        }

        private VisualElement BuildBiomeCard(BiomeTemplateAsset biome)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");
            if (biome == selected)
            {
                card.AddToClassList("dim-item-card-selected");
            }

            VisualElement titleRow = new VisualElement();
            titleRow.AddToClassList("dim-row");

            VisualElement swatch = new VisualElement();
            swatch.AddToClassList("dim-card-swatch");
            swatch.style.backgroundColor = new StyleColor(ReadMapColor(biome));
            titleRow.Add(swatch);

            Label name = new Label(biome == null ? "Missing biome" : biome.DisplayName);
            name.AddToClassList("dim-item-card-name");
            titleRow.Add(name);
            card.Add(titleRow);

            Label sub = new Label(DescribeBiome(biome));
            sub.AddToClassList("dim-item-card-sub");
            card.Add(sub);

            if (biome != null)
            {
                card.RegisterCallback<MouseDownEvent>(evt =>
                {
                    selected = biome;
                    evt.StopPropagation();
                    DeferredRefresh();
                });
            }

            return card;
        }

        private static string DescribeBiome(BiomeTemplateAsset biome)
        {
            if (biome == null)
            {
                return string.Empty;
            }

            int blocks = CountIds(biome.WallObjectIds) +
                         CountIds(biome.FloorObjectIds);
            int scenes = biome.ScenePool == null ? 0 : biome.ScenePool.Length;
            string blockPart = blocks == 0 ? "no blocks yet" : blocks + " blocks";
            return blockPart + " · " + scenes + (scenes == 1 ? " place" : " places");
        }

        /// <summary>
        /// The biome's map colour. It has no public accessor, so the card reads it the same way
        /// the field editor writes it, through the serialized object.
        /// </summary>
        private static Color ReadMapColor(BiomeTemplateAsset biome)
        {
            if (biome == null)
            {
                return Color.gray;
            }

            SerializedObject serialized = new SerializedObject(biome);
            SerializedProperty property = serialized.FindProperty("mapColor");
            return property == null ? Color.gray : property.colorValue;
        }

        private static int CountIds(string[] ids)
        {
            if (ids == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private void RebuildDetail()
        {
            if (detailHost == null)
            {
                return;
            }

            detailHost.Clear();
            if (selected == null)
            {
                Label empty = new Label(
                    template == null
                        ? "Open a dimension to work on its biomes."
                        : "This dimension has no biomes yet. Make the first one.");
                empty.AddToClassList("dim-list-empty");
                detailHost.Add(empty);
                return;
            }

            SerializedObject serialized = new SerializedObject(selected);

            detailHost.Add(BuildIdentityGroup(serialized));
            detailHost.Add(BuildMaterialGroup(serialized));
            detailHost.Add(BuildFeelGroup(serialized));
            detailHost.Add(BuildGrowsGroup(serialized));
            detailHost.Add(BuildUsageGroup());

            // THE SAME CARD EVERY OTHER PAGE GETS, and this page needed it most. A biome is one
            // of the two names a saved world writes into itself, and it was the one kind of thing
            // whose id could be retyped with no sweep, no rewrite and no refusal at all — the card
            // has had one call site since it was written, and this was not it.
            // The name a player reads is already the first control in "Basics", so the card points
            // back at it rather than drawing a second editor over the same property.
            VisualElement identity = DimensionIdentityCard.Build(
                template, selected, true, DeferredRefresh);
            if (identity != null)
            {
                detailHost.Add(identity);
            }
        }

        private VisualElement BuildIdentityGroup(SerializedObject serialized)
        {
            VisualElement group = BuildGroup("Basics", null);
            VisualElement body = GroupBody(group);

            TextField name = new TextField("Name");
            name.BindProperty(serialized.FindProperty("displayName"));
            Decorate(name, "What the player sees on the map, and on the title card when they first walk in.");
            body.Add(name);

            body.Add(BuildBiomeIdRow(serialized));

            Toggle used = new Toggle("Used");
            used.BindProperty(serialized.FindProperty("enabled"));
            Decorate(used, "Turn off to keep this biome in your project without building it into the world.");
            DimensionsApiControls.AfterBinding(used, () =>
                used.RegisterValueChangedCallback(evt =>
                    RebuildList(template == null ? null : template.Biomes)));
            body.Add(used);

            // Was labelled "Map Colour" and said it painted the player's map. It does not: the map
            // colour a player sees comes from each block's own Ground Colour and Wall Colour in the
            // Tileset Studio. This colour reaches the title card, the gamepad light and the swatch
            // on the card to the left, and nothing else.
            ColorField mapColour = new ColorField("Signature colour");
            mapColour.BindProperty(serialized.FindProperty("mapColor"));
            Decorate(mapColour, "The colour of this biome's title card and the gamepad light, unless you give the title its own colour below. What the player sees on the map comes from the blocks — each block carries its own map colour in the Tileset Studio.");
            DimensionsApiControls.AfterBinding(mapColour, () =>
                mapColour.RegisterValueChangedCallback(evt =>
                    RebuildList(template == null ? null : template.Biomes)));
            body.Add(mapColour);

            Toggle announce = new Toggle("Announced on arrival");
            announce.BindProperty(serialized.FindProperty("showTitleOnDiscovery"));
            Decorate(announce, "Walking in for the first time shows the biome's name as a title card, the way the game announces its own biomes.");
            body.Add(announce);

            // The title card's own colour and icon: the emitter has consumed these fields the
            // whole time — the audit found the pipeline complete with no door on this side.
            Toggle ownColour = new Toggle("Title has its own colour");
            ownColour.BindProperty(serialized.FindProperty("overrideTitleColor"));
            Decorate(ownColour, "Off, the title card borrows the map colour above.");
            body.Add(ownColour);

            UnityEditor.UIElements.ColorField titleColour =
                new UnityEditor.UIElements.ColorField("Title colour");
            titleColour.BindProperty(serialized.FindProperty("titleColor"));
            Decorate(titleColour, "The colour the announced name is written in.");
            body.Add(titleColour);

            TextField titleIcon = new TextField("Title icon");
            titleIcon.BindProperty(serialized.FindProperty("titleIconObjectId"));
            Decorate(titleIcon, "An item whose icon sits beside the announced name — one of " +
                "yours by id, or a vanilla object by name. Empty means no icon.");
            body.Add(titleIcon);

            return group;
        }

        /// <summary>
        /// The biome's id, shown locked.
        /// </summary>
        /// <remarks>
        /// The id was auto numbered and unreachable — "Biome1", "Biome2" — and it is what the map's
        /// rings, this biome's ground and walls, its ores, its creatures and its title all point at.
        /// It is also the Kind a saved world stores against every zone of this biome, so renaming it
        /// orphans those zones in worlds people have already played. Hence a lock and a sentence
        /// saying what breaks, rather than a plain text box.
        /// </remarks>
        private VisualElement BuildBiomeIdRow(SerializedObject serialized)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-row");

            TextField id = new TextField("Id");
            id.BindProperty(serialized.FindProperty("biomeId"));

            // TYPING IS NOT RENAMING. A bound field commits on every keystroke, so replacing
            // "Caverns" with "Hollow" wrote "H", "Ho", "Hol", "Holl", "Hollo" on the way — five
            // real serialized values, any of which a generate started in between would have baked.
            // Delayed, the field commits once, when the creator says they are done.
            id.isDelayed = true;

            // AND IT IS NO LONGER UNLOCKABLE HERE. The lock and its dialogue were the whole of a
            // biome rename: unlocked, this was a plain bound text box that rewrote nothing, swept
            // nothing and refused nothing — for the one id in this framework a played world stores
            // against every piece of ground. The card at the foot of this page does all three, and
            // two controls over one property with neither aware of the other is the fault that put
            // the card there in the first place. So this stays a display, and says where to go.
            id.SetEnabled(false);
            id.style.flexGrow = 1f;
            Decorate(id, "A short name for this biome inside your mod. It is what the map, your " +
                         "dungeons and your creatures use to point at it, so keep it stable once " +
                         "people have played.");
            row.Add(id);

            Label where = new Label(
                "To change it, use \"Its name, and what points at it\" at the foot of this page. " +
                "It says what else would be rewritten, and what a world somebody has already " +
                "played would lose.");
            where.AddToClassList("dim-note");
            row.Add(where);

            return row;
        }

        private VisualElement BuildMaterialGroup(SerializedObject serialized)
        {
            VisualElement group = BuildGroup("Terrain", null);
            VisualElement body = GroupBody(group);

            body.Add(BuildIdRow(
                serialized,
                "wallObjectIds",
                "Walls",
                "The walls this biome is dug out of. The world digs them out of the first block " +
                "in this list; the rest are here to say what this biome is made of.",
                DimensionBlockPickerKind.Wall));
            body.Add(BuildIdRow(
                serialized,
                "floorObjectIds",
                "Ground",
                "The ground the player walks on here. The world builds the floor out of the first " +
                "block in this list; the rest are here to say what this biome is made of.",
                DimensionBlockPickerKind.Ground));
            body.Add(BuildIdRow(
                serialized,
                "oreObjectIds",
                "Ores",
                "What can be mined out of this biome's walls. Naming ores here narrows the " +
                "biome to them: only these veins grow inside it. An empty list allows every " +
                "block's own ores.",
                DimensionBlockPickerKind.None));

            return group;
        }

        private VisualElement BuildFeelGroup(SerializedObject serialized)
        {
            VisualElement group = BuildGroup("Audio", null);
            VisualElement body = GroupBody(group);

            TextField music = new TextField("Music");
            music.BindProperty(serialized.FindProperty("musicRosterName"));
            Decorate(music, "Which of the game's music sets plays here.");
            body.Add(music);

            TextField ambience = new TextField("Ambience");
            ambience.BindProperty(serialized.FindProperty("ambienceSoundKey"));
            Decorate(ambience, "The background sound bed: wind, dripping, hums.");
            body.Add(ambience);

            Slider volume = new Slider("Ambience Volume", 0f, 2f);
            volume.BindProperty(serialized.FindProperty("ambienceVolume"));
            Decorate(volume, "How loud that bed sits under everything else.");
            body.Add(volume);

            return group;
        }

        /// <summary>What the world grows here while it generates.</summary>
        private VisualElement BuildGrowsGroup(SerializedObject serialized)
        {
            VisualElement group = BuildGroup("Generation", null);
            VisualElement body = GroupBody(group);
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "generationPasses",
                "Passes",
                "Every step the world runs while it builds this biome, in the order it runs them: " +
                "carve, scatter, flood. This list is the whole recipe."));
            body.Add(DimensionsApiControls.Bound(
                serialized,
                "scenePool",
                "Places",
                "Which handcrafted rooms and ruins the world may drop into this biome."));
            return group;
        }

        private VisualElement BuildUsageGroup()
        {
            VisualElement group = BuildGroup("Used By", null);
            VisualElement body = GroupBody(group);

            VisualElement chips = new VisualElement();
            chips.AddToClassList("dim-chip-row");

            int scenes = selected.ScenePool == null ? 0 : selected.ScenePool.Length;
            int ores = CountIds(selected.OreObjectIds);

            chips.Add(Chip(scenes + (scenes == 1 ? " dungeon placed here" : " dungeons placed here"), scenes > 0));
            chips.Add(Chip(ores + (ores == 1 ? " ore" : " ores"), ores > 0));

            if (CountIds(selected.WallObjectIds) == 0)
            {
                chips.Add(Warn("no walls yet"));
            }

            body.Add(chips);
            return group;
        }

        private static VisualElement Chip(string text, bool live)
        {
            Label chip = new Label(text);
            chip.AddToClassList("dim-chip");
            if (live)
            {
                chip.AddToClassList("dim-chip-link");
            }

            return chip;
        }

        private static VisualElement Warn(string text)
        {
            Label chip = new Label(text);
            chip.AddToClassList("dim-chip");
            chip.AddToClassList("dim-chip-warn");
            return chip;
        }

        /// <summary>
        /// A list of object ids shown as chips with an inline add box, so a creator reads the
        /// contents at a glance instead of expanding a numbered array.
        /// </summary>
        /// <summary>Which half of a block a picker row is filling in, if it offers one at all.</summary>
        private enum DimensionBlockPickerKind
        {
            None,
            Ground,
            Wall
        }

        private VisualElement BuildIdRow(
            SerializedObject serialized,
            string propertyName,
            string label,
            string help,
            DimensionBlockPickerKind picker)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-field");

            Label caption = new Label(label);
            caption.AddToClassList("dim-field-label");
            caption.tooltip = help;
            row.Add(caption);

            VisualElement chips = new VisualElement();
            chips.AddToClassList("dim-chip-row");
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray)
            {
                for (int i = 0; i < property.arraySize; i++)
                {
                    string value = property.GetArrayElementAtIndex(i).stringValue;
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    chips.Add(BuildRemovableChip(serialized, propertyName, i, value));
                }
            }

            if (chips.childCount == 0)
            {
                Label none = new Label("none yet");
                none.AddToClassList("dim-chip");
                chips.Add(none);
            }

            if (picker != DimensionBlockPickerKind.None)
            {
                chips.Add(BuildBlockPickerButton(serialized, propertyName, picker));
            }

            TextField adder = new TextField();
            adder.AddToClassList("dim-inline-add");
            adder.textEdition.placeholder = "add an id and press enter";
            adder.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }

                string value = adder.value == null ? string.Empty : adder.value.Trim();
                if (value.Length == 0)
                {
                    return;
                }

                AppendId(serialized, propertyName, value);
                adder.value = string.Empty;
                evt.StopPropagation();
                DeferredRefresh();
            });
            chips.Add(adder);

            row.Add(chips);
            return row;
        }

        /// <summary>
        /// "Pick a block" — the list of blocks this biome can actually be made of.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The free-text box beside it stays, for anyone pasting an id. It is not enough on its
        /// own: the id the export matches on is "&lt;mod&gt;:&lt;block name&gt;.ground.block", nothing on
        /// the page ever said so, and an id that misses simply builds dirt.
        /// </para>
        /// <para>
        /// Picked blocks go on the end of the list, like typed ones. The first entry is what the
        /// world builds terrain from, so a pick onto an empty row becomes the biome's material and
        /// a pick onto a full one joins the description — which is what the row's tooltip says.
        /// </para>
        /// </remarks>
        private VisualElement BuildBlockPickerButton(
            SerializedObject serialized,
            string propertyName,
            DimensionBlockPickerKind kind)
        {
            bool isGround = kind == DimensionBlockPickerKind.Ground;
            Button pick = new Button
            {
                text = "Pick a block"
            };
            pick.AddToClassList("dim-chip");
            pick.tooltip = "Your own blocks, and the game's.";
            pick.clicked += () =>
            {
                GenericMenu menu = new GenericMenu();

                DimensionTilesetAsset[] tilesets =
                    template == null ? new DimensionTilesetAsset[0] : template.Tilesets;
                bool anyOwn = false;
                for (int i = 0; i < tilesets.Length; i++)
                {
                    DimensionTilesetAsset tileset = tilesets[i];
                    if (tileset == null || !tileset.Enabled)
                    {
                        continue;
                    }

                    string id = isGround ? tileset.GroundBlockItemId : tileset.WallBlockItemId;
                    string entry = "Your blocks/" + tileset.BlockName;
                    menu.AddItem(new GUIContent(entry), false, () =>
                    {
                        AppendId(serialized, propertyName, id);
                        DeferredRefresh();
                    });
                    anyOwn = true;
                }

                if (!anyOwn)
                {
                    menu.AddDisabledItem(new GUIContent("Your blocks/none yet"));
                }

                System.Collections.Generic.IReadOnlyList<DimensionVanillaTilesetEntry> vanilla =
                    DimensionVanillaTilesetCatalog.All;
                for (int i = 0; i < vanilla.Count; i++)
                {
                    DimensionVanillaTilesetEntry entry = vanilla[i];
                    // A wall-only block named as a biome's Ground builds a floor that is not
                    // there, so it is not offered on the Ground row at all.
                    if (isGround && !entry.HasGround)
                    {
                        continue;
                    }

                    string id = DimensionBiomeTerrainMaterial.VanillaId(entry.DisplayName);
                    menu.AddItem(new GUIContent("The game's blocks/" + entry.DisplayName), false, () =>
                    {
                        AppendId(serialized, propertyName, id);
                        DeferredRefresh();
                    });
                }

                menu.ShowAsContext();
            };

            return pick;
        }

        private VisualElement BuildRemovableChip(
            SerializedObject serialized,
            string propertyName,
            int index,
            string value)
        {
            Button chip = new Button(() =>
            {
                RemoveId(serialized, propertyName, index);
                DeferredRefresh();
            })
            {
                text = value + "  ×"
            };
            chip.AddToClassList("dim-chip");
            chip.AddToClassList("dim-chip-removable");
            chip.tooltip = "Remove " + value;
            return chip;
        }

        private void AppendId(SerializedObject serialized, string propertyName, string value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return;
            }

            serialized.Update();
            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).stringValue = value;
            serialized.ApplyModifiedProperties();
            repaint();
        }

        private void RemoveId(SerializedObject serialized, string propertyName, int index)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || index < 0 || index >= property.arraySize)
            {
                return;
            }

            serialized.Update();
            property.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            repaint();
        }

        /// <summary>
        /// Redraws both columns on the next frame. A chip or a card that rebuilds the page from
        /// inside its own callback would be destroyed while still handling the event.
        /// </summary>
        private void DeferredRefresh()
        {
            if (root == null)
            {
                return;
            }

            root.schedule.Execute(() => Refresh(template));
        }

        private static VisualElement BuildGroup(string title, string hint)
        {
            // Same ruling as DimensionsApiControls.Group: card captions read as clutter, so the
            // hint survives only as the heading's tooltip.
            VisualElement group = new VisualElement();
            group.AddToClassList("dim-group");

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-group-head");
            Label heading = new Label(title);
            heading.AddToClassList("dim-h3");
            if (!string.IsNullOrEmpty(hint))
            {
                heading.tooltip = hint;
            }

            head.Add(heading);
            group.Add(head);

            VisualElement body = new VisualElement();
            body.AddToClassList("dim-group-body");
            group.Add(body);
            return group;
        }

        private static VisualElement GroupBody(VisualElement group)
        {
            return group[1];
        }

        private static void Decorate(VisualElement field, string tooltip)
        {
            field.AddToClassList("dim-field");
            field.tooltip = tooltip;
        }
    }
}
