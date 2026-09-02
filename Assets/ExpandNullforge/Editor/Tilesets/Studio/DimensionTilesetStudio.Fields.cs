using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using ExpandNullforge.EditorTools.Generation;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// One field on the block page, the inspector rows, and the styles they are drawn in.
    /// </summary>
    internal sealed partial class DimensionTilesetStudio
    {
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
