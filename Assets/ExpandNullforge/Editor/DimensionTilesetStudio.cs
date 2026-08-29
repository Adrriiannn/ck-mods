using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
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

        // ---- rename guard state ----
        // The name being typed, and which block it belongs to. Held here rather than written
        // straight onto the asset because a block's identity is derived from its name: committing
        // on every keystroke would re-mint the identity letter by letter, and by the time the guard
        // could ask anything the old identity would already be gone.
        private DimensionTilesetAsset renameTarget;
        private string renameDraft = string.Empty;

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
                EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
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

            EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
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
            GUILayout.Label("The type decides which parts of the game your art feeds. It also decides everything the Studio asks you afterwards.", sub);
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
                        "A hoe turns it into tilled soil and a watering can waters it, both drawn with this block's own art."),
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
                    GUILayout.Label(heading, DimensionsApiImguiTheme.SectionLabel);
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
                        new GUIContent("Create its item", "One item, exactly like a block the game ships with. Placed the same way, and mined back into itself."),
                        createOn) && !createOn)
                {
                    wizardItemMode = DimensionTilesetItemMode.CreateItem;
                }
            }

            if (!canCreateItem && type != null && !typeIsOverlay)
            {
                GUILayout.Label("An item for this kind of block is coming later. The block itself works today.", sub);
            }

            bool reskinOn = wizardItemMode == DimensionTilesetItemMode.ReskinVanilla;
            if (EditorGUILayout.ToggleLeft(
                    new GUIContent("Reskin one of the game's blocks", "Your art replaces that block's art everywhere, while this one is included. It only changes the look, so old saves stay valid."),
                    reskinOn) && !reskinOn)
            {
                wizardItemMode = DimensionTilesetItemMode.ReskinVanilla;
            }

            if (wizardItemMode == DimensionTilesetItemMode.ReskinVanilla)
            {
                GUILayout.Space(2f);
                DrawReskinPicker(wizardReskinIndex, idx => wizardReskinIndex = idx);
                GUILayout.Label("Every tile of that block wears your art, in every world, while this one is included. A biome's ground and wall share one block, so both are covered.", sub);
            }

            bool noneOn = wizardItemMode == DimensionTilesetItemMode.None;
            if (EditorGUILayout.ToggleLeft(
                    new GUIContent("No item", "Nobody can hold this block. It exists for the world to generate and for your scenes to place."),
                    noneOn) && !noneOn)
            {
                wizardItemMode = DimensionTilesetItemMode.None;
            }

            if (wizardItemMode == DimensionTilesetItemMode.CreateItem)
            {
                GUILayout.Space(10f);
                DrawSectionLabel("The item");
                GUILayout.Label("Icons are yours to draw (16×16 in the inventory, 10×10 in the hand). Drop them on the item afterwards. Everything else is set here.", sub);
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
            DrawNameField(so, asset);
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

                // The sheet has to match the block's own pixel for pixel, so offer to make it rather
                // than leaving someone to size and lay out a PNG by hand and find out at generation.
                SerializedProperty emissiveTex = so.FindProperty("emissiveTexture");
                if (emissiveTex != null && emissiveTex.objectReferenceValue == null)
                {
                    GUILayout.Space(4f);
                    if (GUILayout.Button(
                            new GUIContent(
                                "Create emissive sheet",
                                "Writes a glow map beside this block's sheet, starting from its brightest " +
                                "pixels. Paint out what should stay dark and paint in whatever it missed."),
                            GUILayout.Width(FieldW)))
                    {
                        Texture2D emissiveSheet;
                        string emissiveError;
                        if (DimensionTilesetEmissiveSheet.TryCreate(
                                so.targetObject as DimensionTilesetAsset, out emissiveSheet, out emissiveError))
                        {
                            emissiveTex.objectReferenceValue = emissiveSheet;
                        }
                        else
                        {
                            Debug.LogWarning("[ExpandNullforge] " + emissiveError);
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(28f);
            EditorGUILayout.BeginVertical();
            if (type.Role == DimensionBlockRole.Terrain && !IsReskin(asset))
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
        /// place. Master switches wire real behavior — not just visuals. A capability still waiting on
        /// its backend wave says so where it is authored rather than implying it works: the ore panel
        /// carries an explicit "not wired yet" notice, because its list currently reaches the runtime
        /// only to have its length logged.
        /// </summary>
        /// <summary>
        /// The block's name, with the guard that stands between a rename and every world it would
        /// break.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE DAMAGE A RENAME DOES IS INVISIBLE AND PERMANENT. A block's identity is its name, the
        /// numeric tileset id is a hash of that identity, and that number is what a saved world
        /// writes into every tile of the block. Rename the block and the number changes: every tile
        /// already placed in somebody's world stops matching any registered block and renders as
        /// the unknown-tileset placeholder, which is exactly what a player sees when a mod has been
        /// uninstalled. Nothing in the editor would have said a word.
        /// </para>
        /// <para>
        /// So the name is edited into a draft and the guard asks before it lands, with the two
        /// answers that are actually different: keep the identity (the usual answer — the block is
        /// the same block, it is only called something else now) or start fresh (a genuinely new
        /// block reusing an old asset, where orphaning the old tiles is the point). A rename whose
        /// identity would not change at all — punctuation, spacing, capitalisation — needs no
        /// question and says so.
        /// </para>
        /// </remarks>
        private void DrawNameField(SerializedObject so, DimensionTilesetAsset asset)
        {
            SerializedProperty nameProp = so.FindProperty("blockName");
            if (renameTarget != asset)
            {
                renameTarget = asset;
                renameDraft = nameProp.stringValue;
            }

            FieldLabel("Block name");
            renameDraft = EditorGUILayout.TextField(renameDraft, GUILayout.Width(FieldW));

            if (string.Equals(renameDraft, nameProp.stringValue, System.StringComparison.Ordinal))
            {
                if (asset.IdentityIsFrozen)
                {
                    GUILayout.Label(
                        "Known to saved worlds as \"" + asset.IdentityToken + "\".",
                        sub);
                }

                return;
            }

            bool identityChanges =
                !asset.IdentityIsFrozen &&
                !string.Equals(
                    DimensionTilesetAsset.IdentityTokenFor(renameDraft),
                    asset.IdentityToken,
                    System.StringComparison.Ordinal);

            GUILayout.Space(4f);
            if (!identityChanges)
            {
                EditorGUILayout.HelpBox(
                    "This block keeps the same identity, so tiles already placed in a world are " +
                    "unaffected.",
                    MessageType.None);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rename", GUILayout.Width(100f)))
                {
                    nameProp.stringValue = renameDraft;
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
                {
                    renameDraft = nameProp.stringValue;
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.HelpBox(
                "Renaming this block changes what it IS, not only what it is called. Its identity " +
                "is \"" + asset.IdentityToken + "\" and would become \"" +
                DimensionTilesetAsset.IdentityTokenFor(renameDraft) + "\".\n\n" +
                "Every tile of this block already placed in a saved world remembers the old " +
                "identity. After the change those tiles match nothing and show as the missing " +
                "block, exactly as they would if this mod had been uninstalled. The block items " +
                "in players' chests change id with it.\n\n" +
                "Choose which you mean:",
                MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    new GUIContent(
                        "Rename, keep the identity",
                        "The same block under a new name. Tiles already placed stay this block, " +
                        "and its id is pinned to \"" + asset.IdentityToken + "\" from now on."),
                    GUILayout.Width(200f)))
            {
                // Pinned BEFORE the name lands, because the token is derived from the name it is
                // replacing. Written through the same serialized object so one undo covers both.
                so.FindProperty("identityToken").stringValue = asset.IdentityToken;
                nameProp.stringValue = renameDraft;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button(
                    new GUIContent(
                        "Rename and start fresh",
                        "A different block that happens to reuse this asset. Anything already " +
                        "placed of the old one is orphaned."),
                    GUILayout.Width(190f)))
            {
                so.FindProperty("identityToken").stringValue = string.Empty;
                nameProp.stringValue = renameDraft;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
            {
                renameDraft = nameProp.stringValue;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Whether this block dresses one of the game's tilesets instead of being its own.</summary>
        private static bool IsReskin(DimensionTilesetAsset asset)
        {
            return asset != null && asset.ItemMode == DimensionTilesetItemMode.ReskinVanilla;
        }

        /// <summary>
        /// Says, once, why a reskin offers fewer switches than a block of its own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE REASON IS THE TILE'S NUMBER, and it is worth stating plainly because nothing else in
        /// the Studio hints at it. A reskin registers art against one of the game's tileset indexes
        /// (<c>DimensionTilesetRuntime.RegisterReskin</c>), and the tiles it dresses keep carrying
        /// that vanilla number in the world and in the save. Every capability below the art —
        /// fog, ground cover, ore veins, what walking on it does, its colour on the map — is looked
        /// up by the block's OWN id, which no tile in the world is stamped with. They would
        /// register, log cheerfully, and never once be consulted.
        /// </para>
        /// <para>
        /// So they are not shown rather than shown and quietly ignored. A creator who needs them
        /// needs a block of its own, which is one choice away and is what this says.
        /// </para>
        /// </remarks>
        private void DrawReskinLimits()
        {
            EditorGUILayout.HelpBox(
                "This block dresses one of the game's own blocks, so the tiles it paints are still " +
                "the game's tiles underneath — they carry the game's number in the world and in " +
                "the save.\n\n" +
                "That means everything except the art belongs to the block it dresses: its fog, " +
                "the grass and pebbles that grow on it, the ore in its walls, what walking on it " +
                "does, and its colour on the map. Those switches are hidden here because they " +
                "could not take effect.\n\n" +
                "To set them, make this a block of its own instead: the wizard's item step is " +
                "where the choice lives.",
                MessageType.Info);
            GUILayout.Space(8f);
        }

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

        // ---- preview ----

        // Interactive 3D block preview — the skin rendered on a Core-Keeper-style block you can
        // rotate and zoom, so a tileset can be judged in Unity without building the mod.
        private readonly DimensionTilesetBlockPreview blockPreview = new DimensionTilesetBlockPreview();

        public void Cleanup()
        {
            blockPreview.Cleanup();
        }

        // ---- what the rebuilt Blocks page drives ----
        //
        // The page owns the chrome and this owns the canvas, so everything below is a seam rather
        // than a second implementation: the same preview instance, the same wizard, the same
        // serialized edits the panel has always made.

        /// <summary>The live canvas, so a page can read what is selected in it and toggle its states.</summary>
        internal DimensionTilesetBlockPreview Preview
        {
            get { return blockPreview; }
        }

        /// <summary>
        /// Draws only the block canvas, without the controls it usually paints along its top edge,
        /// for a page that carries those controls in its own design.
        /// </summary>
        internal void DrawPreviewIsland(DimensionTilesetAsset asset, float width, float height)
        {
            if (asset == null)
            {
                return;
            }

            EnsureStyles();
            blockPreview.SetActiveOverlay(null, false);

            float w = Mathf.Max(160f, width);
            float h = Mathf.Max(160f, height);
            Rect rect = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));

            // Only for the length of this draw: the panel that draws its own controls is untouched.
            blockPreview.DrawsOwnToolbar = false;
            try
            {
                blockPreview.Draw(rect, asset.TilesetTexture, asset);
            }
            finally
            {
                blockPreview.DrawsOwnToolbar = true;
            }
        }

        /// <summary>Whether the canvas adds walls (else ground) when someone paints in it.</summary>
        internal bool PreviewPaintsWall
        {
            get { return blockPreview.PaintKind == DimensionTilesetBlockPreview.BlockKind.Wall; }
            set
            {
                blockPreview.PaintKind = value
                    ? DimensionTilesetBlockPreview.BlockKind.Wall
                    : DimensionTilesetBlockPreview.BlockKind.Ground;
            }
        }

        /// <summary>True while clicks paint blocks; false while they pick one to look at.</summary>
        internal bool PreviewPainting
        {
            get { return blockPreview.Painting; }
            set { blockPreview.Painting = value; }
        }

        /// <summary>Shifts the pattern to another arrangement of the same tiles.</summary>
        internal void ShufflePreview()
        {
            blockPreview.ShufflePattern();
        }

        /// <summary>Puts the camera, the zoom and the pattern back where they started.</summary>
        internal void ResetPreviewView()
        {
            blockPreview.ResetView();
        }

        /// <summary>
        /// Draws the Add-block wizard on its own, so a page can host the one creation path that
        /// exists rather than growing a second one. The returned request is the finished block.
        /// </summary>
        internal DrawResult DrawWizardIsland()
        {
            result = default;
            result.SelectIndex = -1;
            if (!wizardActive)
            {
                return result;
            }

            EnsureStyles();
            EditorGUILayout.BeginVertical(DimensionsApiImguiTheme.CardBox);
            DrawWizard();
            EditorGUILayout.EndVertical();
            return result;
        }

        /// <summary>The serialized entry for one state key, created off if it was never touched.</summary>
        internal SerializedProperty LayerProperty(SerializedObject so, string key, bool create)
        {
            return GetLayerProp(so, key, create);
        }

        /// <summary>
        /// The farming answer, written the way the panel writes it: tilled, watered and flooded move
        /// together, because a block that can be hoed can be watered and flooded as well.
        /// </summary>
        internal void SetFarmable(SerializedObject so, bool on)
        {
            foreach (string key in new[] { "tilled", "watered", "flooded" })
            {
                GetLayerProp(so, key, true).FindPropertyRelative("enabled").boolValue = on;
            }
        }

        /// <summary>Fixes a freshly assigned sheet's import so pixel art stays pixel art.</summary>
        internal static void ApplySheetImport(Texture2D texture)
        {
            ApplySheetImportSettings(texture);
        }

        /// <summary>
        /// Fills in the framework's circuit-floor material when that capability is switched on. The
        /// serialized reference is what carries the material and its shader into the mod bundle.
        /// </summary>
        internal static void EnsureCircuitFloorMaterial(SerializedObject so)
        {
            if (so == null)
            {
                return;
            }

            SerializedProperty material = so.FindProperty("circuitFloorMaterial");
            if (material == null || material.objectReferenceValue != null)
            {
                return;
            }

            material.objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(CircuitFloorMaterialPath);
        }

        /// <summary>Where the framework's circuit-floor material lives, for reporting it missing.</summary>
        internal static string CircuitFloorMaterialAssetPath
        {
            get { return CircuitFloorMaterialPath; }
        }

        /// <summary>The game's own ores a vein can drop.</summary>
        internal static IReadOnlyList<string> VanillaOres
        {
            get { return VanillaOreItems; }
        }

        /// <summary>The overlays a block grows on its own ground, and what each one is.</summary>
        internal static IReadOnlyList<(string Key, string Label, string Blurb)> GroundCover
        {
            get { return GroundCoverStates; }
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
                GUILayout.Label("With painting off, click a block to see the states it can wear.", sub);
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

            return "This art is " + sheet.width + " by " + sheet.height + " pixels and the game's " +
                   "layout is " + w + " by " + h + ". Every tile is read from a fixed spot on that " +
                   "layout, so art of another size bakes into nonsense. Resize the canvas without " +
                   "scaling what you drew, then drop it in again.";
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
            GUILayout.Label(text.ToUpperInvariant(), DimensionsApiImguiTheme.SectionLabel);
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

            // Same palette and typefaces as the rebuilt pages. The Studio's controls stay
            // exactly where they are; only how they read changes.
            stageTitle = DimensionsApiImguiTheme.Text(
                EditorStyles.boldLabel,
                DimensionsApiImguiTheme.Display,
                15,
                DimensionsApiImguiTheme.Lavender);
            // Prose, not data: the body face at a readable weight. Monospace here made every
            // explanation look like a machine field rather than a sentence.
            sub = DimensionsApiImguiTheme.Text(
                EditorStyles.wordWrappedMiniLabel,
                DimensionsApiImguiTheme.Body,
                11,
                DimensionsApiImguiTheme.Periwinkle);
            sub.wordWrap = true;
            panelTitle = DimensionsApiImguiTheme.Text(
                EditorStyles.boldLabel,
                DimensionsApiImguiTheme.BodyStrong,
                13,
                DimensionsApiImguiTheme.Lavender);

            headerTitle = DimensionsApiImguiTheme.Text(
                EditorStyles.boldLabel,
                DimensionsApiImguiTheme.Display,
                15,
                DimensionsApiImguiTheme.Lavender);
            headerTitle.alignment = TextAnchor.MiddleLeft;

            addBlockStyle = DimensionsApiImguiTheme.Text(
                EditorStyles.label,
                DimensionsApiImguiTheme.BodyStrong,
                13,
                DimensionsApiImguiTheme.CoreBlue);
            addBlockStyle.alignment = TextAnchor.MiddleRight;
            addBlockStyle.hover.textColor = DimensionsApiImguiTheme.CoreBluePale;

            orangeDropStyle = DimensionsApiImguiTheme.Text(
                EditorStyles.label,
                DimensionsApiImguiTheme.BodyStrong,
                12,
                DimensionsApiImguiTheme.CoreBlue);
            orangeDropStyle.alignment = TextAnchor.MiddleRight;
            orangeDropStyle.hover.textColor = DimensionsApiImguiTheme.CoreBluePale;

            fieldLabel = DimensionsApiImguiTheme.Text(
                DimensionsApiImguiTheme.Caption,
                DimensionsApiImguiTheme.Body,
                12,
                DimensionsApiImguiTheme.Periwinkle);

            stateCell = DimensionsApiImguiTheme.Text(
                EditorStyles.label,
                DimensionsApiImguiTheme.Mono,
                10,
                DimensionsApiImguiTheme.Periwinkle);
            stateCell.clipping = TextClipping.Clip;

            wizardOption = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }
    }
}
