using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The control panel and the sections under it: cover, surface, slime, ore, items.
    /// </summary>
    internal sealed partial class DimensionTilesetStudio
    {
        private void DrawControlPanel(SerializedObject so, DimensionTilesetAsset asset, DimensionTilesetType type)
        {
            DrawSectionLabel("Block capabilities");
            GUILayout.Label("Everything this block can do. Switches here wire the real behavior, not just the look.", sub);
            GUILayout.Space(6f);

            bool reskin = IsReskin(asset);
            if (reskin)
            {
                DrawReskinLimits();
            }

            if (type.HasStates)
            {
                // -- FARMING: the first live master switch. On a reskin this is the ART of tilled
                // and watered soil and nothing more — whether the block CAN be farmed is decided
                // by the game's own block underneath, and the hidden tilled-ground object is not
                // generated (GenerateGroundBlock is false for a reskin).
                bool farm = asset.IsStateEnabled("tilled");
                bool now = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        reskin ? "Draw its farmed states" : "Can be farmed",
                        reskin
                            ? "Your art for tilled and watered soil, used wherever the block this " +
                              "dresses is farmed. Whether it can be farmed at all stays the game's own answer."
                            : "A hoe turns it into tilled soil and a watering can waters it, both drawn with this block's own art."),
                    farm, EditorStyles.boldLabel);
                if (now != farm)
                {
                    foreach (string key in new[] { "tilled", "watered", "flooded" })
                    {
                        GetLayerProp(so, key, true).FindPropertyRelative("enabled").boolValue = now;
                    }
                }

                if (now && !reskin)
                {
                    DrawCapability("Can be tilled (hoe)", true,
                        "Hoeing keeps this block's own soil instead of turning it into dirt. Generation " +
                        "writes the hidden object the game looks for before it decides.");
                    DrawCapability("Can be watered (watering can, sprinkler)", true,
                        "Watered soil inherits whatever tileset it was tilled from, so it follows the line above.");
                }

                GUILayout.Space(8f);

                // -- CIRCUIT FLOOR: the ground renders through the framework's under-glass
                // circuit material. The regular art shows as-is (dark glass + dormant traces);
                // the emissive circuit art lights up from rare deterministic ambient pulses and
                // from real powered electricity nearby.
                // Machined surfaces read wrong with Core Keeper's hand-drawn wobble: it stretches each
                // tile's art unevenly, which organic rock hides and straight panelling does not.
                SerializedProperty rigid = so.FindProperty("rigidSurface");
                if (rigid != null)
                {
                    rigid.boolValue = EditorGUILayout.ToggleLeft(
                        new GUIContent("Stands rigid, with no wobble",
                            "Tiles sit perfectly straight instead of taking the gentle wobble the game " +
                            "gives hand drawn terrain. Right for metal plating, glass panels and " +
                            "circuitry, where a crooked edge reads as a mistake. Walls are already " +
                            "straight in the game."),
                        rigid.boolValue, EditorStyles.boldLabel);
                    if (rigid.boolValue)
                    {
                        DrawCapability(
                            "Art renders exactly as drawn, corner to corner",
                            true,
                            "Motifs that sit on a tile edge or corner stay symmetric instead of being " +
                            "stretched by each tile's own vertex displacement.");

                        // Rigidity is a marker component the game looks for on the tile's own PREFAB.
                        // No block object means no prefab, so nothing carries the marker and the
                        // setting quietly does nothing at all. Read through the asset rather than by
                        // serialized name: GenerateBlock is derived from the type and item mode, and
                        // has no backing field to look up.
                        if (!asset.GenerateBlock)
                        {
                            EditorGUILayout.HelpBox(
                                "This block generates no block object, so there is no prefab for the " +
                                "rigid marker to live on and the setting will have no effect in game. " +
                                "Turn on its ground or wall block.",
                                MessageType.Warning);
                        }
                    }

                    GUILayout.Space(6f);
                }

                // Fog is a data block the game builds its lookup from, keyed by the block's own
                // tileset id. A reskinned tile carries the game's id, so the block would be written
                // and never matched.
                SerializedProperty fog = reskin ? null : so.FindProperty("hasGroundFog");
                if (fog != null)
                {
                    fog.boolValue = EditorGUILayout.ToggleLeft(
                        new GUIContent("Ground fog",
                            "Low fog lying on this block's ground, the way it does in the mold " +
                            "biome. Only the ground carries it. Walls never do."),
                        fog.boolValue, EditorStyles.boldLabel);

                    if (fog.boolValue)
                    {
                        SerializedProperty tint = so.FindProperty("groundFogTint");
                        if (tint != null)
                        {
                            tint.colorValue = EditorGUILayout.ColorField(
                                new GUIContent("Fog colour",
                                    "Alpha is the fog's DENSITY, not its transparency. Full alpha is " +
                                    "much heavier than the swatch suggests. The mold biome sits near a third."),
                                tint.colorValue);
                        }

                        DrawCapability(
                            "Written as a data block the game reads at startup",
                            true,
                            "Nothing is patched. Core Keeper builds its fog lookup from these blocks, " +
                            "so custom fog renders through the game's own pass.");
                    }

                    GUILayout.Space(6f);
                }

                // The circuit surface is served through the material override, and
                // GetOverrideMaterial passes vanilla indexes straight through — a reskinned tile
                // could never be handed it.
                SerializedProperty circuit = reskin ? null : so.FindProperty("circuitFloor");
                bool circuitOn = circuit != null && EditorGUILayout.ToggleLeft(
                    new GUIContent("Circuit floor, glowing under glass",
                        "Ground renders its regular art (dark glass + dormant traces); the emissive circuit art glows. Rare ambient pulses sweep it, and real powered electricity lights it up."),
                    circuit.boolValue, EditorStyles.boldLabel);
                if (circuit != null && circuitOn != circuit.boolValue)
                {
                    circuit.boolValue = circuitOn;
                }

                if (circuitOn)
                {
                    // The serialized material reference is what ships the material + shader in the
                    // bundle; fill it here (the only editing surface) if it is still empty.
                    SerializedProperty circuitMaterial = so.FindProperty("circuitFloorMaterial");
                    if (circuitMaterial != null && circuitMaterial.objectReferenceValue == null)
                    {
                        circuitMaterial.objectReferenceValue =
                            AssetDatabase.LoadAssetAtPath<Material>(CircuitFloorMaterialPath);
                    }

                    if (circuitMaterial == null || circuitMaterial.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            "The framework's circuit surface is missing from " + CircuitFloorMaterialPath +
                            ", so this will not glow.",
                            MessageType.Warning);
                    }
                    else
                    {
                        DrawCapability("Ambient dreams (rare traveling pulses)", true,
                            "Pulses travel across the glow art on their own, one cell at a time. Never permanently lit.");
                        DrawCapability("Stability glow (real electricity)", true,
                            "Powered entities nearby light the circuits through the game's live electricity texture.");
                    }
                }

                // Ground cover, both behaviours and ore veins are all registered against this
                // block's own tileset id: the scatter rules, DimensionTilesetBehaviourRegistry and
                // ApplyOreVeinRules all key on it. A reskin's tiles carry the game's id, so all
                // three would be registered and never consulted.
                if (!reskin)
                {
                    GUILayout.Space(8f);
                    DrawGroundCoverSection(so);

                    // Only meaningful once the block actually has slime to walk on.
                    if (asset.IsStateEnabled("slime"))
                    {
                        GUILayout.Space(8f);
                        DrawSlimeBehaviourSection(so);
                    }

                    // Unconditional, unlike the slime section: this is about the ground the block
                    // always has, so it applies whether or not the block grows slime.
                    GUILayout.Space(8f);
                    DrawSurfaceBehaviourSection(so);
                }

                GUILayout.Space(8f);

                // -- Always-on capabilities the game drives; the sheet decides their look.
                DrawSectionLabel("Always included");
                DrawCapability("Mining & digging cracks (3 stages)", true, "Damage cracks render from this block's own crack art.");
                DrawCapability("Pebbles when a wall is mined", true, "Mined walls scatter this block's own pebbles.");
                DrawCapability("Slime, grass, roots & debris from the world", true, "Creatures and scenes apply these too; they draw with your art wherever they land on this block.");
                DrawCapability(
                    "Ore veins & ancient crystal (art)",
                    true,
                    reskin
                        ? "The vein art is ready. Which ore is in there belongs to the block this dresses."
                        : "The vein art is ready; linking real ore drops is the next capability below.");
                DrawCapability("Vines & roof holes", true, "The world places these; your sheet carries their look.");

                if (!reskin)
                {
                    GUILayout.Space(8f);
                    DrawOreSection(so);
                }
            }
            else
            {
                GUILayout.Label("This block type has no optional capabilities. Its single surface is the whole story.", sub);
            }
        }

        /// <summary>
        /// The overlays this block grows on its own ground when a dimension generates it, and how
        /// thickly.
        /// </summary>
        /// <remarks>
        /// These four are the only scatterable states. The rest are not things that "grow": tilled and
        /// watered soil are made by the player's tools, cracks come from damage, ore is placed by vein
        /// generation. Offering a density for those would produce terrain that looks farmed or mined
        /// before anyone touched it.
        /// </remarks>
        private static readonly (string Key, string Label, string Blurb)[] GroundCoverStates =
        {
            ("grass", "Grass tufts", "Sparse blades scattered over the surface."),
            ("pebbles", "Pebbles", "Loose stones lying on the ground."),
            ("roots", "Big roots", "Thick roots breaking through, blocking movement."),
            ("slime", "Slime", "A wet coating over the surface."),
        };

        private void DrawGroundCoverSection(SerializedObject so)
        {
            DrawSectionLabel("Ground cover");
            GUILayout.Label(
                "What grows on this block when a dimension generates it. Same world, same places, every time.",
                sub);
            GUILayout.Space(4f);

            for (int i = 0; i < GroundCoverStates.Length; i++)
            {
                (string key, string label, string blurb) = GroundCoverStates[i];
                SerializedProperty layer = GetLayerProp(so, key, true);
                SerializedProperty enabled = layer.FindPropertyRelative("enabled");
                SerializedProperty density = layer.FindPropertyRelative("density");

                EditorGUILayout.BeginHorizontal();
                enabled.boolValue = EditorGUILayout.ToggleLeft(
                    new GUIContent(label, blurb), enabled.boolValue, GUILayout.Width(150f));

                // The slider is meaningless while the overlay is off, and leaving it live would
                // suggest a number that changes nothing.
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    density.floatValue = EditorGUILayout.Slider(
                        density.floatValue, 0f, 1f, GUILayout.Width(220f));
                    GUILayout.Label(
                        Mathf.RoundToInt(density.floatValue * 100f) + "% of the ground", sub,
                        GUILayout.Width(130f));
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// The twin of the slime section, for ground that is itself the hazard.
        /// </summary>
        /// <remarks>
        /// Drawn as its own section rather than folded into the slime one because they are genuinely
        /// independent: mold ground hurts with nothing on it, while most hazardous blocks are safe
        /// stone under a dangerous puddle. One dropdown could not say both.
        /// </remarks>
        private void DrawSurfaceBehaviourSection(SerializedObject so)
        {
            SerializedProperty behaviour = so.FindProperty("surfaceBehaviour");
            if (behaviour == null)
            {
                return;
            }

            DrawSectionLabel("What the ground itself does");

            GUILayout.Label(
                "For ground that is the hazard, like mold. No slime needed. Leave as None for an " +
                "ordinary floor.", sub);
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            behaviour.enumValueIndex = (int)(DimensionTilesetGroundBehaviour)EditorGUILayout.EnumPopup(
                (DimensionTilesetGroundBehaviour)behaviour.enumValueIndex, GUILayout.Width(FieldW));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if ((DimensionTilesetGroundBehaviour)behaviour.enumValueIndex !=
                DimensionTilesetGroundBehaviour.None)
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "Every step on this block's plain ground applies the effect. That is a strong " +
                    "thing to do to a floor players walk across. The game reserves it for places " +
                    "they are meant to hurry through.",
                    MessageType.Info);
            }

            GUILayout.Space(6f);
        }

        /// <summary>
        /// What this block's slime does underfoot, borrowed from Core Keeper's own set.
        /// </summary>
        /// <remarks>
        /// Worded as "behaves like" rather than naming the vanilla tileset it comes from, because the
        /// author is choosing an experience, not a donor. The honesty note about conditions matters:
        /// the look and sound land immediately, the damage does not, and someone testing an acid
        /// block would otherwise reasonably conclude it was broken.
        /// </remarks>
        private void DrawSlimeBehaviourSection(SerializedObject so)
        {
            DrawSectionLabel("What the slime does");

            SerializedProperty behaviour = so.FindProperty("slimeBehaviour");
            if (behaviour == null)
            {
                return;
            }

            GUILayout.Label(
                "Borrow one of the game's own slimes. Your art stays; its behaviour comes along.", sub);
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            behaviour.enumValueIndex = (int)(DimensionTilesetGroundBehaviour)EditorGUILayout.EnumPopup(
                (DimensionTilesetGroundBehaviour)behaviour.enumValueIndex, GUILayout.Width(FieldW));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4f);
            switch ((DimensionTilesetGroundBehaviour)behaviour.enumValueIndex)
            {
                case DimensionTilesetGroundBehaviour.None:
                    GUILayout.Label("Decoration only. Walking through it does nothing.", sub);
                    break;
                case DimensionTilesetGroundBehaviour.Slime:
                    GUILayout.Label("Ordinary slime. Muffles the dust kicked up when running.", sub);
                    break;
                case DimensionTilesetGroundBehaviour.Acid:
                    GUILayout.Label("Burns, and hisses while you stand in it.", sub);
                    break;
                case DimensionTilesetGroundBehaviour.PoisonSlime:
                    GUILayout.Label("Poisons whoever walks through.", sub);
                    break;
                case DimensionTilesetGroundBehaviour.SlipperySlime:
                    GUILayout.Label("Slippery. You carry further than you meant to.", sub);
                    break;
                case DimensionTilesetGroundBehaviour.Oil:
                    GUILayout.Label("Drenches whatever walks through. Relevant near fire.", sub);
                    break;
            }

            if ((DimensionTilesetGroundBehaviour)behaviour.enumValueIndex == DimensionTilesetGroundBehaviour.Slime)
            {
                EditorGUILayout.HelpBox(
                    "Plain slime is presentation only. Footsteps, splashes and the muffled run dust. " +
                    "That is all vanilla's own orange slime really does underfoot.",
                    MessageType.Info);
            }
        }

        // Vanilla ore items a vein can drop — exact ObjectID enum names from the decompile census
        // (CopperOre=1500 … ReluciteOre=1526). Custom items are typed by id.
        private static readonly string[] VanillaOreItems =
        {
            "CopperOre", "TinOre", "IronOre", "GoldOre", "ScarletOre",
            "OctarineOre", "GalaxiteOre", "SolariteOre", "PandoriumOre", "ReluciteOre",
        };

        private string pendingOreAdd;

        private bool pendingOreAddCustom;

        // "Can contain ores": each row links one vein to the item it drops. Rows edit the asset's
        // ores list directly; the generator emits the hidden vein object per row.
        private void DrawOreSection(SerializedObject so)
        {
            DrawSectionLabel("Can contain ores");
            GUILayout.Label("Veins this block's walls can hold. Vein art comes from your sheet's ore region.", sub);

            // Both paths generate now. The one-ore rule is stated because it is a vanilla limit the
            // modder cannot discover any other way except by mining a wall for an hour: the drop
            // resolves a vein by FIRST MATCH on the tileset, so a second entry is unreachable no
            // matter how it is authored.
            EditorGUILayout.HelpBox(
                "Generating wires this up: your own ore items are stamped so the item IS the vein, and " +
                "a vanilla ore gets its own vein object pointing at the real Copper/Tin/… item.\n\n" +
                "One ore per block. The game finds a vein by taking the first match on this tileset, so " +
                "anything after the first could never be reached. Vanilla has the same limit (Solarite " +
                "and Pandorium collide on Crystal). Extra entries are reported when you generate.\n\n" +
                "Veins also grow on their own in this block's generated walls. Set the slider to zero " +
                "to place them only by painting.",
                MessageType.Info);
            GUILayout.Space(2f);

            SerializedProperty ores = so.FindProperty("ores");

            // An ore picked from the add-menu lands here on a later event.
            if (pendingOreAdd != null)
            {
                int idx = ores.arraySize;
                ores.InsertArrayElementAtIndex(idx);
                SerializedProperty added = ores.GetArrayElementAtIndex(idx);
                added.FindPropertyRelative("oreItemId").stringValue = pendingOreAdd;
                added.FindPropertyRelative("isCustomItem").boolValue = pendingOreAddCustom;
                pendingOreAdd = null;
            }

            for (int i = 0; i < ores.arraySize; i++)
            {
                SerializedProperty element = ores.GetArrayElementAtIndex(i);
                SerializedProperty id = element.FindPropertyRelative("oreItemId");
                SerializedProperty custom = element.FindPropertyRelative("isCustomItem");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(custom.boolValue ? "custom" : "vanilla", sub, GUILayout.Width(48f));
                if (custom.boolValue)
                {
                    id.stringValue = EditorGUILayout.TextField(id.stringValue, GUILayout.Width(170f));
                }
                else
                {
                    GUILayout.Label(id.stringValue, EditorStyles.boldLabel, GUILayout.Width(170f));
                }

                if (GUILayout.Button("✕", GUILayout.Width(22f), GUILayout.Height(18f)))
                {
                    ores.DeleteArrayElementAtIndex(i);
                    GUI.FocusControl(null);
                    break;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // How the vein appears on its own, in player words: how often, and how big.
                SerializedProperty abundance = element.FindPropertyRelative("abundance");
                SerializedProperty sizeMin = element.FindPropertyRelative("veinSizeMin");
                SerializedProperty sizeMax = element.FindPropertyRelative("veinSizeMax");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Found in walls", sub, GUILayout.Width(90f));
                abundance.floatValue = GUILayout.HorizontalSlider(
                    abundance.floatValue, 0f, 10f, GUILayout.Width(140f));
                abundance.floatValue = UnityEngine.Mathf.Round(abundance.floatValue * 10f) / 10f;
                GUILayout.Label(
                    abundance.floatValue <= 0f
                        ? "paint only"
                        : "about " + abundance.floatValue.ToString("0.#") + " veins per 100 wall tiles",
                    sub);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Vein size", sub, GUILayout.Width(90f));
                float veinMin = sizeMin.intValue;
                float veinMax = sizeMax.intValue < sizeMin.intValue ? sizeMin.intValue : sizeMax.intValue;
                EditorGUILayout.MinMaxSlider(ref veinMin, ref veinMax, 1f, 12f, GUILayout.Width(140f));
                sizeMin.intValue = UnityEngine.Mathf.RoundToInt(veinMin);
                sizeMax.intValue = UnityEngine.Mathf.RoundToInt(veinMax);
                GUILayout.Label(sizeMin.intValue + " to " + sizeMax.intValue + " blocks", sub);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
            }

            Rect addRect = GUILayoutUtility.GetRect(120f, 20f, GUILayout.Width(120f));
            if (GUI.Button(addRect, "+ Add ore  ▾", EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                foreach (string ore in VanillaOreItems)
                {
                    string captured = ore;
                    menu.AddItem(new GUIContent("Vanilla/" + captured), false, () =>
                    {
                        pendingOreAdd = captured;
                        pendingOreAddCustom = false;
                    });
                }

                menu.AddItem(new GUIContent("Custom item id…"), false, () =>
                {
                    pendingOreAdd = string.Empty;
                    pendingOreAddCustom = true;
                });
                menu.DropDown(addRect);
            }
        }

        private void DrawItemSection(DimensionTemplateAsset template, DimensionTilesetAsset asset, SerializedObject so)
        {
            DrawSectionLabel("Linked item");
            switch (asset.ItemMode)
            {
                case DimensionTilesetItemMode.CreateItem:
                    GUILayout.Label("One item, exactly like a vanilla block: ground on open terrain, wall where ground exists, and it drops itself. Icons, description and rarity live on the item.", sub);
                    GUILayout.Space(4f);
                    EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
                    DrawItemRow(asset.BlockName + " Block", asset.GenerateBlock, template, asset);
                    EditorGUILayout.EndVertical();
                    break;

                case DimensionTilesetItemMode.ReskinVanilla:
                    GUILayout.Label("This block reskins one of the game's own. No item of its own. While it is included, every tile of that block wears your art, and because only the look changes, old saves stay valid.", sub);
                    GUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Reskins:", EditorStyles.boldLabel, GUILayout.Width(60f));
                    SerializedProperty reskinProp = so.FindProperty("reskinTilesetIndex");
                    DrawReskinPicker(reskinProp.intValue, idx => reskinProp.intValue = idx);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    break;

                default:
                    GUILayout.Label("No item. This tileset exists for worldgen/scene use only.", sub);
                    break;
            }
        }

        private void DrawItemRow(string name, bool generated, DimensionTemplateAsset template, DimensionTilesetAsset asset)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Block", EditorStyles.boldLabel);
            GUILayout.Label(generated ? name : "This block type does not generate an item yet.", sub);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!generated))
            {
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = Amber;
                if (GUILayout.Button("Edit ▸", GUILayout.Width(78f), GUILayout.Height(26f)))
                {
                    DimensionItemAsset item =
                        DimensionFrameworkAuthoringAssetUtility.EnsureAndGetTilesetBlockItem(template, asset, true);
                    if (item != null)
                    {
                        result.FocusItem = item;
                        result.Message = "Editing \"" + item.DisplayName + "\" in Resources. Icons, description, rarity.";
                        result.MessageType = MessageType.Info;
                    }
                }

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGenerateSection(DimensionTilesetAsset asset)
        {
            DrawSectionLabel("Tileset data");
            GUILayout.Label(
                "Bakes the adaptive sheets the game samples, so every enabled layer draws from your art. Run it after changing the sheet or states.",
                sub);
            GUILayout.Space(5f);

            bool atlasReady = DimensionTilesetAtlas.IsReady;
            bool hasTexture = asset.TilesetTexture != null;
            using (new EditorGUI.DisabledScope(!hasTexture || !atlasReady))
            {
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = Amber;
                if (GUILayout.Button(
                        asset.HasGeneratedGen ? "Regenerate tileset data" : "Generate tileset data",
                        GUILayout.Width(200f)))
                {
                    result.GenerateRequested = true;
                }

                GUI.backgroundColor = prevBg;
            }

            if (!hasTexture)
            {
                GUILayout.Label("Assign a tileset sheet above first.", sub);
            }
            else if (!atlasReady)
            {
                // The atlas has a status line of its own, written for whoever built the atlas. What
                // matters to someone making a block is the one thing that fixes it.
                GUILayout.Label(
                    "Play the mod once so the framework can learn how the game lays a block out. " +
                    "Baking needs that layout, and it only has to happen the one time.", sub);
            }
            else if (asset.HasGeneratedGen)
            {
                GUILayout.Label("Generated ✓. " + asset.GeneratedGen.Count + " layer sheet(s) stored.", sub);
            }
        }
    }
}
