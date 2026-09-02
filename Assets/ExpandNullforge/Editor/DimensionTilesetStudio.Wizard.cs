using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The three step wizard that adds a block, and what it asks at each step.
    /// </summary>
    internal sealed partial class DimensionTilesetStudio
    {
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
    }
}
