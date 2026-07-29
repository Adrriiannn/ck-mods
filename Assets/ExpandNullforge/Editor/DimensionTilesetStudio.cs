using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Everything the Add-block wizard collects; handed to the window as one request when the
    /// modder clicks Create. The utility turns it into a configured tileset asset (with no sheet —
    /// the modder authors that next) plus, in create-item mode, the seeded inventory item.
    /// </summary>
    internal sealed class DimensionTilesetWizardRequest
    {
        public string TypeKey;
        public List<string> StateKeys = new List<string>();
        public DimensionTilesetItemMode ItemMode;
        public int ReskinIndex = -1;
        public string BlockName;
        public string Description = string.Empty;
        public string RarityId = string.Empty;
    }

    /// <summary>
    /// The modular Tileset Studio: a guided Add-block wizard (type → states → item/reskin) and a
    /// single page per block showing everything about it — preview, sheet, states, generated data
    /// and the linked item. One block = one page; no rail, no per-state design panels.
    /// </summary>
    internal sealed class DimensionTilesetStudio
    {
        internal struct DrawResult
        {
            public bool Changed;
            public string Message;
            public MessageType MessageType;
            public bool DeleteRequested;
            public bool GenerateRequested;
            public int SelectIndex;
            public DimensionTilesetWizardRequest WizardRequest;
            public DimensionItemAsset FocusItem;
        }

        private static readonly Color Amber = new Color(0.90f, 0.58f, 0.26f);

        // The framework-shipped under-glass circuit-floor material; auto-assigned onto the tileset
        // asset when its "Circuit floor" capability is switched on.
        private const string CircuitFloorMaterialPath =
            "Assets/ExpandNullforge/Materials/NullforgeCircuitFloor.mat";

        private const float PreviewH = 460f;
        private const float PreviewW = 580f;
        private const float FieldW = 220f;

        private GUIStyle stageTitle;
        private GUIStyle sub;
        private GUIStyle panelTitle;
        private GUIStyle headerTitle;
        private GUIStyle addBlockStyle;
        private GUIStyle orangeDropStyle;
        private GUIStyle fieldLabel;
        private GUIStyle stateCell;
        private GUIStyle wizardOption;
        private int pendingSelectIndex = -1;
        private DrawResult result;

        // ---- wizard state ----
        private bool wizardActive;
        private int wizardStep;
        private string wizardTypeKey;
        private readonly HashSet<string> wizardStates = new HashSet<string>();
        private DimensionTilesetItemMode wizardItemMode = DimensionTilesetItemMode.CreateItem;
        private int wizardReskinIndex = -1;
        private string wizardName = string.Empty;
        private string wizardDescription = string.Empty;
        private string wizardRarity = string.Empty;

        /// <summary>True while the Add-block wizard is open (the window may draw it with zero blocks).</summary>
        public bool WizardActive
        {
            get { return wizardActive; }
        }

        /// <summary>Opens the Add-block wizard fresh.</summary>
        public void BeginWizard()
        {
            wizardActive = true;
            wizardStep = 0;
            wizardTypeKey = null;
            wizardStates.Clear();
            wizardItemMode = DimensionTilesetItemMode.CreateItem;
            wizardReskinIndex = -1;
            wizardName = string.Empty;
            wizardDescription = string.Empty;
            wizardRarity = string.Empty;
        }

        public DrawResult Draw(
            DimensionTemplateAsset template,
            DimensionTilesetAsset[] tilesets,
            int selectedIndex,
            float width)
        {
            result = default;
            result.SelectIndex = -1;

            // A block picked from the switcher's GenericMenu lands here on a later event.
            if (pendingSelectIndex >= 0)
            {
                result.SelectIndex = pendingSelectIndex;
                pendingSelectIndex = -1;
            }

            EnsureStyles();

            if (wizardActive)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawWizard();
                EditorGUILayout.EndVertical();
                return result;
            }

            DimensionTilesetAsset asset =
                (tilesets != null && selectedIndex >= 0 && selectedIndex < tilesets.Length)
                    ? tilesets[selectedIndex]
                    : null;
            if (asset == null)
            {
                return result;
            }

            SerializedObject so = new SerializedObject(asset);
            so.UpdateIfRequiredOrScript();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawHeaderBar(tilesets, selectedIndex, asset);
            GUILayout.Space(2f);
            DrawBlockPage(so, template, asset);
            EditorGUILayout.EndVertical();

            if (so.ApplyModifiedProperties())
            {
                result.Changed = true;
            }

            return result;
        }

        // ---- header ----

        private void DrawHeaderBar(DimensionTilesetAsset[] tilesets, int selectedIndex, DimensionTilesetAsset asset)
        {
            Rect bar = EditorGUILayout.GetControlRect(false, 40f);
            EditorGUI.DrawRect(bar, new Color(Amber.r, Amber.g, Amber.b, 0.15f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), Amber);

            GUI.Label(new Rect(bar.x + 12f, bar.y, 260f, bar.height), "Tileset Studio", headerTitle);

            // "+ Add block" opens the wizard — the only way a block is born, so every block gets a
            // deliberate type, states and item decision instead of a pile of defaults.
            Rect add = new Rect(bar.xMax - 108f, bar.y, 100f, bar.height);
            if (GUI.Button(add, "+ Add block", addBlockStyle))
            {
                BeginWizard();
            }

            string ddLabel = asset.BlockName + "  ▾";
            float ddW = Mathf.Min(220f, orangeDropStyle.CalcSize(new GUIContent(ddLabel)).x + 4f);
            Rect ddRect = new Rect(add.x - ddW - 14f, bar.y, ddW, bar.height);
            if (GUI.Button(ddRect, ddLabel, orangeDropStyle))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < tilesets.Length; i++)
                {
                    if (tilesets[i] == null)
                    {
                        continue;
                    }

                    int idx = i;
                    menu.AddItem(new GUIContent(tilesets[i].BlockName), idx == selectedIndex, () => pendingSelectIndex = idx);
                }

                menu.DropDown(ddRect);
            }

            GUILayout.Space(4f);
        }

        // ---- the wizard ----

        private void DrawWizard()
        {
            Rect bar = EditorGUILayout.GetControlRect(false, 40f);
            EditorGUI.DrawRect(bar, new Color(Amber.r, Amber.g, Amber.b, 0.15f));
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, 3f, bar.height), Amber);
            GUI.Label(new Rect(bar.x + 12f, bar.y, 400f, bar.height), "New Block", headerTitle);

            DimensionTilesetType type = DimensionTilesetTypeCatalog.Resolve(wizardTypeKey);
            bool hasType = !string.IsNullOrEmpty(wizardTypeKey);
            bool typeIsOverlay = hasType && type.Role == DimensionBlockRole.Overlay;

            GUILayout.Space(8f);
            switch (wizardStep)
            {
                case 0: DrawWizardTypeStep(); break;
                case 2: DrawWizardItemStep(type, typeIsOverlay); break;
            }

            GUILayout.Space(12f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
            {
                wizardActive = false;
            }

            GUILayout.FlexibleSpace();
            if (wizardStep > 0 && GUILayout.Button("◂ Back", GUILayout.Width(90f)))
            {
                wizardStep = 0;
            }

            if (wizardStep < 2)
            {
                using (new EditorGUI.DisabledScope(!hasType))
                {
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = Amber;
                    if (GUILayout.Button("Next ▸", GUILayout.Width(90f)))
                    {
                        wizardStep = 2;
                    }

                    GUI.backgroundColor = prev;
                }
            }
            else
            {
                bool nameOk = !string.IsNullOrEmpty(wizardName?.Trim());
                bool reskinOk = wizardItemMode != DimensionTilesetItemMode.ReskinVanilla || wizardReskinIndex >= 0;
                using (new EditorGUI.DisabledScope(!nameOk || !reskinOk))
                {
                    Color prev = GUI.backgroundColor;
                    GUI.backgroundColor = Amber;
                    if (GUILayout.Button("Create block", GUILayout.Width(120f)))
                    {
                        DimensionTilesetWizardRequest request = new DimensionTilesetWizardRequest
                        {
                            TypeKey = wizardTypeKey,
                            ItemMode = wizardItemMode,
                            ReskinIndex = wizardItemMode == DimensionTilesetItemMode.ReskinVanilla ? wizardReskinIndex : -1,
                            BlockName = wizardName.Trim(),
                            Description = wizardDescription ?? string.Empty,
                            RarityId = wizardRarity ?? string.Empty
                        };
                        request.StateKeys.AddRange(wizardStates);
                        result.WizardRequest = request;
                        wizardActive = false;
                    }

                    GUI.backgroundColor = prev;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawWizardTypeStep()
        {
            GUILayout.Label("1 · What kind of block is this?", stageTitle);
            GUILayout.Label("The type decides which parts of the game's tile system your sheet feeds — and everything the Studio asks of you afterwards.", sub);
            GUILayout.Space(6f);

            DrawTypeGroup("THE STANDARD BLOCK", DimensionBlockRole.Terrain);
            DrawTypeGroup("BUILT SURFACES & STRUCTURES", DimensionBlockRole.Built);
            DrawTypeGroup("LIQUID", DimensionBlockRole.Liquid);
            DrawTypeGroup("SPECIAL", DimensionBlockRole.Special);
            DrawTypeGroup("WORLD OVERLAYS", DimensionBlockRole.Overlay);

            // The one gameplay choice, right where the type is picked: farmable (terrain only).
            if (!string.IsNullOrEmpty(wizardTypeKey) &&
                DimensionTilesetTypeCatalog.TryGet(wizardTypeKey, out DimensionTilesetType picked) &&
                picked.HasStates)
            {
                GUILayout.Space(8f);
                bool farm = wizardStates.Contains("tilled");
                bool now = EditorGUILayout.ToggleLeft(
                    new GUIContent("Can be farmed",
                        "Hoe → tilled soil, watering can → watered soil, using this block's own art."),
                    farm);
                if (now != farm)
                {
                    foreach (string key in new[] { "tilled", "watered", "flooded" })
                    {
                        if (now)
                        {
                            wizardStates.Add(key);
                        }
                        else
                        {
                            wizardStates.Remove(key);
                        }
                    }
                }
            }
        }

        private void DrawTypeGroup(string heading, DimensionBlockRole role)
        {
            // Every group lays its options out two per row, so wide groups (Built has nine) stack
            // into tidy rows instead of one endless strip.
            const int cols = 2;
            bool any = false;
            bool rowOpen = false;
            int col = 0;
            foreach (DimensionTilesetType type in DimensionTilesetTypeCatalog.All)
            {
                if (type.Role != role)
                {
                    continue;
                }

                if (!any)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(heading, EditorStyles.miniBoldLabel);
                    any = true;
                }

                if (!rowOpen)
                {
                    EditorGUILayout.BeginHorizontal();
                    rowOpen = true;
                }

                bool selected = type.Key == wizardTypeKey;
                Color prev = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = Amber;
                }

                if (GUILayout.Button(type.DisplayName, wizardOption, GUILayout.Width(150f), GUILayout.Height(26f)))
                {
                    wizardTypeKey = type.Key;
                    // Overlays can't be standalone items — steer their item step to reskin.
                    if (type.Role == DimensionBlockRole.Overlay || !type.SupportsItem)
                    {
                        if (wizardItemMode == DimensionTilesetItemMode.CreateItem)
                        {
                            wizardItemMode = type.Role == DimensionBlockRole.Overlay
                                ? DimensionTilesetItemMode.ReskinVanilla
                                : DimensionTilesetItemMode.None;
                        }
                    }
                    else
                    {
                        wizardItemMode = DimensionTilesetItemMode.CreateItem;
                    }

                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = prev;

                col++;
                if (col % cols == 0)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2f);
                    rowOpen = false;
                }
            }

            if (rowOpen)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawWizardItemStep(DimensionTilesetType type, bool typeIsOverlay)
        {
            GUILayout.Label("2 · Name it, and choose how it reaches the game", stageTitle);
            GUILayout.Space(6f);

            FieldLabel("Block name");
            wizardName = EditorGUILayout.TextField(wizardName, GUILayout.Width(FieldW));
            GUILayout.Space(8f);

            bool canCreateItem = type != null && type.SupportsItem && !typeIsOverlay;

            using (new EditorGUI.DisabledScope(!canCreateItem))
            {
                bool createOn = wizardItemMode == DimensionTilesetItemMode.CreateItem;
                if (EditorGUILayout.ToggleLeft(
                        new GUIContent("Create its item", "A fully customizable \"{name} Block\" inventory item — placed like any vanilla block, mined back into itself."),
                        createOn) && !createOn)
                {
                    wizardItemMode = DimensionTilesetItemMode.CreateItem;
                }
            }

            if (!canCreateItem && type != null && !typeIsOverlay)
            {
                GUILayout.Label("Item generation for this type is coming later — the tileset itself works today.", sub);
            }

            bool reskinOn = wizardItemMode == DimensionTilesetItemMode.ReskinVanilla;
            if (EditorGUILayout.ToggleLeft(
                    new GUIContent("Reskin a vanilla block", "Your textures replace a vanilla tileset's everywhere, while this block is enabled. Render-only and save-safe."),
                    reskinOn) && !reskinOn)
            {
                wizardItemMode = DimensionTilesetItemMode.ReskinVanilla;
            }

            if (wizardItemMode == DimensionTilesetItemMode.ReskinVanilla)
            {
                GUILayout.Space(2f);
                DrawReskinPicker(wizardReskinIndex, idx => wizardReskinIndex = idx);
                GUILayout.Label("Reskins every tile of that vanilla tileset, in every world, while enabled. A biome's ground and wall share one tileset, so both are covered.", sub);
            }

            bool noneOn = wizardItemMode == DimensionTilesetItemMode.None;
            if (EditorGUILayout.ToggleLeft(
                    new GUIContent("No item", "The tileset exists for worldgen/scene use only."),
                    noneOn) && !noneOn)
            {
                wizardItemMode = DimensionTilesetItemMode.None;
            }

            if (wizardItemMode == DimensionTilesetItemMode.CreateItem)
            {
                GUILayout.Space(10f);
                DrawSectionLabel("The item");
                GUILayout.Label("Icons are yours to draw (16×16 inventory + 10×10 in-hand) — drop them on the item afterwards. Everything else is set here.", sub);
                GUILayout.Space(2f);
                FieldLabel("Description");
                wizardDescription = EditorGUILayout.TextField(wizardDescription, GUILayout.Width(FieldW * 1.6f));
                FieldLabel("Rarity (empty = Common; e.g. Uncommon, Rare, Epic, Legendary)");
                wizardRarity = EditorGUILayout.TextField(wizardRarity, GUILayout.Width(FieldW));
            }
        }

        private void DrawReskinPicker(int currentIndex, System.Action<int> assign)
        {
            string label = currentIndex >= 0
                ? DimensionVanillaTilesetCatalog.NameOf(currentIndex)
                : "Choose a vanilla block…";
            Rect r = EditorGUILayout.GetControlRect(false, 20f, GUILayout.Width(FieldW));
            if (GUI.Button(r, label + "  ▾", EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                foreach (DimensionVanillaTilesetEntry entry in DimensionVanillaTilesetCatalog.All)
                {
                    DimensionVanillaTilesetEntry e = entry;
                    menu.AddItem(
                        new GUIContent(e.DisplayName + (e.HasGround ? "  (wall + ground)" : "  (wall)")),
                        e.TilesetIndex == currentIndex,
                        () => assign(e.TilesetIndex));
                }

                menu.DropDown(r);
            }
        }

        // ---- the one page ----

        private void DrawBlockPage(SerializedObject so, DimensionTemplateAsset template, DimensionTilesetAsset asset)
        {
            DimensionTilesetType type = asset.BlockType;

            GUILayout.Label(asset.BlockName, stageTitle);
            string kind = type.DisplayName +
                          (asset.ItemMode == DimensionTilesetItemMode.ReskinVanilla
                              ? " · reskins " + DimensionVanillaTilesetCatalog.NameOf(asset.ReskinTilesetIndex)
                              : string.Empty);
            GUILayout.Label(kind, sub);
            GUILayout.Space(6f);

            // Preview anchored left; the block's CONTROL PANEL sits to its right — the one place that
            // describes and edits everything the block can do, as master switches.
            blockPreview.SetActiveOverlay(null, false);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(PreviewW));
            Rect previewRect = GUILayoutUtility.GetRect(
                PreviewW, PreviewH, GUILayout.Width(PreviewW), GUILayout.Height(PreviewH));
            blockPreview.Draw(previewRect, asset.TilesetTexture, asset);
            DrawBlockInspector();
            EditorGUILayout.EndVertical();
            GUILayout.Space(18f);
            EditorGUILayout.BeginVertical();
            DrawControlPanel(so, asset, type);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(12f);

            // -- fields, two columns --
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(FieldW + 8f));
            SerializedProperty nameProp = so.FindProperty("blockName");
            FieldLabel("Block name");
            nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue, GUILayout.Width(FieldW));
            GUILayout.Space(8f);
            FieldLabel("Tileset sheet");
            DrawInlineTextureNoLabel(so, "tilesetTexture");
            if (asset.TilesetTexture == null)
            {
                GUILayout.Label("Author your sheet in the vanilla dirt layout and drop it here.", sub);
            }
            else
            {
                string sizeProblem = DescribeSheetSizeProblem(asset.TilesetTexture);
                if (sizeProblem != null)
                {
                    EditorGUILayout.HelpBox(sizeProblem, MessageType.Error);
                }
            }

            GUILayout.Space(8f);
            SerializedProperty emis = so.FindProperty("isEmissive");
            emis.boolValue = EditorGUILayout.ToggleLeft("Glows in the dark", emis.boolValue, GUILayout.Width(FieldW));
            if (emis.boolValue)
            {
                GUILayout.Space(4f);
                FieldLabel("Emissive sheet");
                DrawInlineTextureNoLabel(so, "emissiveTexture");
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(28f);
            EditorGUILayout.BeginVertical();
            if (type.Role == DimensionBlockRole.Terrain)
            {
                DrawSectionLabel("World map colors");
                GUILayout.Space(2f);
                EditorGUILayout.BeginHorizontal();
                DrawColorAbove(so, "groundMapColor", "Ground");
                GUILayout.Space(20f);
                DrawColorAbove(so, "wallMapColor", "Wall");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(12f);
            }

            SerializedProperty en = so.FindProperty("enabled");
            en.boolValue = EditorGUILayout.ToggleLeft("Include in the mod", en.boolValue);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // -- linked item / reskin --
            GUILayout.Space(14f);
            DrawItemSection(template, asset, so);

            // -- generated data --
            GUILayout.Space(14f);
            DrawGenerateSection(asset);

            GUILayout.Space(14f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete this block", GUILayout.Width(140f)) &&
                EditorUtility.DisplayDialog(
                    "Delete block",
                    "Delete the \"" + asset.BlockName + "\" tileset? Its generated block items stay in the item list.",
                    "Delete",
                    "Cancel"))
            {
                result.DeleteRequested = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        // ---- the control panel: what this block can do, as master switches ----

        // One capability line: "· label" with an amber dot when active.
        private void DrawCapability(string label, bool active, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            Rect dot = GUILayoutUtility.GetRect(10f, 16f, GUILayout.Width(10f));
            EditorGUI.DrawRect(new Rect(dot.x + 2f, dot.y + 6f, 5f, 5f),
                active ? Amber : new Color(1f, 1f, 1f, 0.15f));
            GUILayout.Label(new GUIContent(label, tooltip), sub);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The block's master control panel: every capability it has (or can gain), editable in one
        /// place. Master switches wire real behavior — not just visuals; capabilities that still need
        /// their backend wave (ores, growables) are shown honestly as "coming" rather than lying.
        /// </summary>
        private void DrawControlPanel(SerializedObject so, DimensionTilesetAsset asset, DimensionTilesetType type)
        {
            DrawSectionLabel("Block capabilities");
            GUILayout.Label("Everything this block can do. Switches here wire the real behavior, not just the look.", sub);
            GUILayout.Space(6f);

            if (type.HasStates)
            {
                // -- FARMING: the first live master switch.
                bool farm = asset.IsStateEnabled("tilled");
                bool now = EditorGUILayout.ToggleLeft(
                    new GUIContent("Can be farmed",
                        "Hoe → tilled soil, watering can → watered soil, using this block's own art."),
                    farm, EditorStyles.boldLabel);
                if (now != farm)
                {
                    foreach (string key in new[] { "tilled", "watered", "flooded" })
                    {
                        GetLayerProp(so, key, true).FindPropertyRelative("enabled").boolValue = now;
                    }
                }

                if (now)
                {
                    DrawCapability("Can be tilled (hoe)", true, "The tilled-soil art bakes from your sheet.");
                    DrawCapability("Can be watered (watering can, sprinkler)", true, "Watered soil inherits this block's tileset.");
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
                        new GUIContent("Rigid surface (no wobble)",
                            "Tiles sit perfectly straight instead of taking the game's hand-drawn " +
                            "vertex wobble. Right for metal plating, glass panels and circuitry, where " +
                            "a crooked edge reads as a bug. Walls are already rigid in vanilla."),
                        rigid.boolValue, EditorStyles.boldLabel);
                    if (rigid.boolValue)
                    {
                        DrawCapability(
                            "Art renders exactly as drawn, corner to corner",
                            true,
                            "Motifs that sit on a tile edge or corner stay symmetric instead of being " +
                            "stretched by each tile's own vertex displacement.");
                    }

                    GUILayout.Space(6f);
                }

                SerializedProperty circuit = so.FindProperty("circuitFloor");
                bool circuitOn = EditorGUILayout.ToggleLeft(
                    new GUIContent("Circuit floor (under-glass glow)",
                        "Ground renders its regular art (dark glass + dormant traces); the emissive circuit art glows — rare ambient pulses sweep it, and real powered electricity lights it up."),
                    circuit.boolValue, EditorStyles.boldLabel);
                if (circuitOn != circuit.boolValue)
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
                            "Circuit-floor material not found at " + CircuitFloorMaterialPath + ".",
                            MessageType.Warning);
                    }
                    else
                    {
                        DrawCapability("Ambient dreams (rare traveling pulses)", true,
                            "Deterministic per-cell pulses sweep the circuit emissive art — never permanently lit.");
                        DrawCapability("Stability glow (real electricity)", true,
                            "Powered entities nearby light the circuits through the game's live electricity texture.");
                    }
                }

                GUILayout.Space(8f);

                // -- Always-on capabilities the game drives; the sheet decides their look.
                DrawSectionLabel("Always included");
                DrawCapability("Mining & digging cracks (3 stages)", true, "Damage cracks render from this block's own crack art.");
                DrawCapability("Pebbles when a wall is mined", true, "Mined walls scatter this block's own pebbles.");
                DrawCapability("Slime, grass, roots, debris & rubble", true, "Applied by creatures, scenes and worldgen; drawn with your art when placed with this tileset.");
                DrawCapability("Ore veins & ancient crystal (art)", true, "The vein art is ready; linking real ore drops is the next capability below.");
                DrawCapability("Vines & roof holes", true, "World-driven overlays; your sheet carries their look.");

                GUILayout.Space(8f);
                DrawOreSection(so);
            }
            else
            {
                GUILayout.Label("This block type has no optional capabilities — its single surface is the whole story.", sub);
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
            GUILayout.Label("Veins this block's walls can hold — each drops its linked item when mined. Vein art comes from your sheet's ore region.", sub);
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
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawItemRow(asset.BlockName + " Block", asset.GenerateBlock, template, asset);
                    EditorGUILayout.EndVertical();
                    break;

                case DimensionTilesetItemMode.ReskinVanilla:
                    GUILayout.Label("This block reskins a vanilla tileset — no item of its own. While enabled, every tile of that tileset wears your textures (render-only, save-safe).", sub);
                    GUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Reskins:", EditorStyles.boldLabel, GUILayout.Width(60f));
                    SerializedProperty reskinProp = so.FindProperty("reskinTilesetIndex");
                    DrawReskinPicker(reskinProp.intValue, idx => reskinProp.intValue = idx);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    break;

                default:
                    GUILayout.Label("No item — this tileset exists for worldgen/scene use only.", sub);
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
                        result.Message = "Editing \"" + item.DisplayName + "\" in Resources — icons, description, rarity.";
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
                "Bake this block's adaptive sheets — the \"mint\" files the game samples so every enabled layer renders natively. Run it after changing the sheet or states.",
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
                GUILayout.Label("Run the mod in-game once so the layout is captured. " + DimensionTilesetAtlas.Status, sub);
            }
            else if (asset.HasGeneratedGen)
            {
                GUILayout.Label("Generated ✓ — " + asset.GeneratedGen.Count + " layer sheet(s) stored.", sub);
            }
        }

        // ---- preview ----

        // Interactive 3D block preview — the skin rendered on a Core-Keeper-style block you can
        // rotate and zoom, so a tileset can be judged in Unity without building the mod.
        private readonly DimensionTilesetBlockPreview blockPreview = new DimensionTilesetBlockPreview();

        public void Cleanup()
        {
            blockPreview.Cleanup();
        }

        // When a block is selected in the preview (View mode, left-click), a panel of its applicable
        // states appears; toggling a chip flips that state on just that block, rendered live.
        private void DrawBlockInspector()
        {
            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical(GUILayout.Width(PreviewW));
            if (!blockPreview.HasSelection)
            {
                GUILayout.Label("Tip: in View mode, left-click a block to toggle its states.", sub);
            }
            else
            {
                bool isWall = blockPreview.SelectionIsWall;
                DrawSectionLabel("States on the selected " + (isWall ? "wall" : "ground") + " block");
                GUILayout.Space(3f);

                List<DimensionTilesetBlockPreview.StateEntry> applicable =
                    new List<DimensionTilesetBlockPreview.StateEntry>();
                foreach (DimensionTilesetBlockPreview.StateEntry st in DimensionTilesetBlockPreview.StateCatalog)
                {
                    if (st.IsWall == isWall)
                    {
                        applicable.Add(st);
                    }
                }

                for (int i = 0; i < applicable.Count; i++)
                {
                    if (i % 5 == 0)
                    {
                        EditorGUILayout.BeginHorizontal();
                    }

                    DimensionTilesetBlockPreview.StateEntry st = applicable[i];
                    bool on = blockPreview.SelectionHasState(st.Layer);
                    Color prev = GUI.backgroundColor;
                    if (on)
                    {
                        GUI.backgroundColor = Amber;
                    }

                    if (GUILayout.Button(st.Label, EditorStyles.miniButton, GUILayout.Width(96f), GUILayout.Height(20f)))
                    {
                        blockPreview.ToggleSelectionState(st.Layer, !on);
                    }

                    GUI.backgroundColor = prev;
                    if (i % 5 == 4 || i == applicable.Count - 1)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ---- helpers ----

        private SerializedProperty GetLayerProp(SerializedObject so, string key, bool create)
        {
            SerializedProperty arr = so.FindProperty("layers");
            for (int i = 0; i < arr.arraySize; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("key").stringValue == key)
                {
                    return el;
                }
            }

            if (!create)
            {
                return null;
            }

            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            SerializedProperty added = arr.GetArrayElementAtIndex(idx);
            added.FindPropertyRelative("key").stringValue = key;
            added.FindPropertyRelative("enabled").boolValue = false;
            added.FindPropertyRelative("texture").objectReferenceValue = null;
            return added;
        }

        private void FieldLabel(string text)
        {
            GUILayout.Label(text, fieldLabel);
        }

        /// <summary>A thin single-line texture field with no inline label (the label sits above it).</summary>
        private void DrawInlineTextureNoLabel(SerializedObject so, string prop)
        {
            SerializedProperty p = so.FindProperty(prop);
            Rect r = GUILayoutUtility.GetRect(FieldW, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(FieldW), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            UnityEngine.Object before = p.objectReferenceValue;
            UnityEngine.Object after = EditorGUI.ObjectField(r, before, typeof(Texture2D), false);
            p.objectReferenceValue = after;
            if (after != before)
            {
                ApplySheetImportSettings(after as Texture2D);
            }
        }

        /// <summary>
        /// A sheet dropped in from outside carries Unity's default import (bilinear, compressed,
        /// mipmapped) — which blurs and bleeds pixel art the moment the game samples it. Assigning
        /// one here fixes its import in place, so a hand-painted sheet behaves like a framework-built
        /// one without the modder knowing the settings exist.
        /// </summary>
        private static void ApplySheetImportSettings(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                Generation.DimensionTilesetPixelIo.ConfigureImport(path);
            }
        }

        /// <summary>
        /// The captured coordinates are fractions of the vanilla donor sheet, so a sheet of any other
        /// size silently samples the wrong pixels. Returns a human message when that is the case.
        /// </summary>
        private static string DescribeSheetSizeProblem(Texture2D sheet)
        {
            if (sheet == null || !DimensionTilesetAtlas.TryGetSourceSheetSize(out int w, out int h))
            {
                return null;
            }

            if (sheet.width == w && sheet.height == h)
            {
                return null;
            }

            return "This sheet is " + sheet.width + "×" + sheet.height + ", but the vanilla layout is " +
                   w + "×" + h + ". Every tile is read at a fixed spot on that layout, so a differently " +
                   "sized sheet bakes garbage. Resize the canvas (don't scale the art) and re-assign it.";
        }

        /// <summary>A narrow colour swatch with its label above it (keeps the field off the window edge).</summary>
        private void DrawColorAbove(SerializedObject so, string prop, string label)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(120f));
            FieldLabel(label);
            SerializedProperty p = so.FindProperty(prop);
            Rect r = GUILayoutUtility.GetRect(110f, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(110f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            p.colorValue = EditorGUI.ColorField(r, GUIContent.none, p.colorValue);
            EditorGUILayout.EndVertical();
        }

        private void DrawSectionLabel(string text)
        {
            GUILayout.Label(text.ToUpperInvariant(), EditorStyles.miniBoldLabel);
        }

        private static void DrawBorder(Rect r, Color c, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private void EnsureStyles()
        {
            if (stageTitle != null)
            {
                return;
            }

            stageTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            sub = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            sub.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
            panelTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

            headerTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft
            };

            addBlockStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            addBlockStyle.normal.textColor = Amber;
            addBlockStyle.hover.textColor = new Color(1f, 0.72f, 0.4f);

            orangeDropStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            orangeDropStyle.normal.textColor = Amber;
            orangeDropStyle.hover.textColor = new Color(1f, 0.72f, 0.4f);

            fieldLabel = new GUIStyle(EditorStyles.miniLabel);
            fieldLabel.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

            stateCell = new GUIStyle(EditorStyles.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            stateCell.clipping = TextClipping.Clip;

            wizardOption = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }
    }
}
