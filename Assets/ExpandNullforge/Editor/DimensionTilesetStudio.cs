using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{

    /// <summary>
    /// The modular Tileset Studio: a guided Add-block wizard (type → states → item/reskin) and a
    /// single page per block showing everything about it — preview, sheet, states, generated data
    /// and the linked item. One block = one page; no rail, no per-state design panels.
    /// </summary>
    internal sealed partial class DimensionTilesetStudio
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
    }
}
