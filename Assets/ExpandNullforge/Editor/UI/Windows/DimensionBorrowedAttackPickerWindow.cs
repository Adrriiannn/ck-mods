using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The list of every move Core Keeper authored, for a creature to take one whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS DOOR ONE, and until now it had no handle. The moves were harvested off the game's
    /// own prefabs and written into a table months ago; the only way in was a drop-down of a
    /// hundred and fifty names buried in a panel the studio no longer draws. A creator who wanted
    /// Pyrdra's swing found nothing, and wrote their own — which is the exact outcome the framework
    /// exists to prevent.
    /// </para>
    /// <para>
    /// WHAT A PICKER OWES A CREATOR. Somewhere to type, because a list of hundreds is not a list
    /// anyone reads. The name the game uses, not the name the file was saved under. And, before
    /// the choice rather than after it, what will not come across — a route that lives inside the
    /// game, a clip your sprite sheet has no frame for, a swing built for a body five tiles wide.
    /// Those are on the right of the window, beside the Take it button, so nobody meets them for
    /// the first time in a world they have already loaded.
    /// </para>
    /// <para>
    /// TAKING ONE IS NOT A COMMITMENT. Every number lands in the ordinary fields on the creature,
    /// where it is then just a number. Nothing records which move it came from, so nothing is ever
    /// quietly put back; changing all of it, or none of it, are both finished answers.
    /// </para>
    /// </remarks>
    internal sealed class DimensionBorrowedAttackPickerWindow : EditorWindow
    {
        private const float RowHeight = 20f;
        private const string AnyKind = "Anything";

        private Object asset;
        private System.Action onApplied;

        private string filter = string.Empty;
        private string kind = AnyKind;
        private string[] kindChoices;

        private Vector2 listScroll;
        private float listViewportHeight = 260f;
        private readonly List<DimensionBorrowedAttacks.Preset> matched =
            new List<DimensionBorrowedAttacks.Preset>();

        private DimensionBorrowedAttacks.Preset chosen;
        private Vector2 detailScroll;

        // Worked out once when the selection changes: probing takes a scratch asset, and doing
        // that every repaint would build and destroy a ScriptableObject sixty times a second.
        private string probedPresetName;
        private readonly List<string> probedHomeless = new List<string>();

        private string lastResult = string.Empty;
        private MessageType lastResultKind = MessageType.None;

        private static GUIStyle leftAlignedButton;

        private static GUIStyle LeftAlignedButton
        {
            get
            {
                if (leftAlignedButton == null)
                {
                    leftAlignedButton = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                }

                return leftAlignedButton;
            }
        }

        /// <summary>
        /// Opens the list for one creature.
        /// </summary>
        /// <param name="target">The mob or boss the move will be poured into.</param>
        /// <param name="startingKind">
        /// Which sort to show first, or null for everything. The row a creator pressed knows what
        /// they were after, so arriving already filtered saves them saying it again.
        /// </param>
        /// <param name="applied">Run after a move lands, so the page behind can redraw.</param>
        internal static void Open(Object target, string startingKind, System.Action applied)
        {
            DimensionBorrowedAttackPickerWindow window =
                GetWindow<DimensionBorrowedAttackPickerWindow>(true, "Take a move from the game", true);
            window.minSize = new Vector2(720f, 460f);
            window.asset = target;
            window.onApplied = applied;
            window.kind = string.IsNullOrEmpty(startingKind) ? AnyKind : startingKind;
            window.chosen = null;
            window.probedPresetName = null;
            window.lastResult = string.Empty;
            window.lastResultKind = MessageType.None;
            window.Show();
        }

        /// <summary>Whether this asset is one a move can be poured into at all.</summary>
        /// <remarks>
        /// Having a combat block is what makes something a creature here. A workbench has none, and
        /// offering it a swing would be a control that cannot do what its label says.
        /// </remarks>
        internal static bool CanTakeAMove(Object target)
        {
            if (target == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(target);
            return serialized.FindProperty("combat") != null;
        }

        private void OnGUI()
        {
            if (asset == null)
            {
                EditorGUILayout.HelpBox(
                    "The creature this was opened for is gone. Open the list again from the " +
                    "creature you want to work on.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                "Take a move from something in the game",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "It arrives with the game's own numbers, measured off the creature it belongs to. " +
                "Change as much or as little of it afterwards as you like, or nothing at all. " +
                "Building one from nothing instead is the other way in: leave this alone and fill " +
                "the fight fields in on the creature.",
                EditorStyles.wordWrappedMiniLabel);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            DrawKindChoice();
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Search", GUILayout.Width(48f));
            filter = EditorGUILayout.TextField(filter ?? string.Empty);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawKindChoice()
        {
            if (kindChoices == null)
            {
                string[] kinds = DimensionBorrowedAttacks.Kinds;
                kindChoices = new string[kinds.Length + 1];
                kindChoices[0] = AnyKind;
                for (int i = 0; i < kinds.Length; i++)
                {
                    kindChoices[i + 1] = kinds[i];
                }
            }

            int current = 0;
            for (int i = 0; i < kindChoices.Length; i++)
            {
                if (string.Equals(kindChoices[i], kind, System.StringComparison.Ordinal))
                {
                    current = i;
                    break;
                }
            }

            int picked = EditorGUILayout.Popup(current, kindChoices, GUILayout.Width(140f));
            kind = kindChoices[Mathf.Clamp(picked, 0, kindChoices.Length - 1)];
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320f));
            Rebuild();

            EditorGUILayout.LabelField(
                matched.Count + " of " + DimensionBorrowedAttacks.All.Length,
                EditorStyles.miniLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            if (matched.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "Nothing in the game matches that.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                Rect content = GUILayoutUtility.GetRect(
                    1f, matched.Count * RowHeight, GUILayout.ExpandWidth(true));
                int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / RowHeight) - 2);
                int last = Mathf.Min(
                    matched.Count - 1,
                    Mathf.CeilToInt((listScroll.y + listViewportHeight) / RowHeight) + 2);
                for (int i = first; i <= last; i++)
                {
                    DimensionBorrowedAttacks.Preset preset = matched[i];
                    Rect row = new Rect(
                        content.x,
                        content.y + i * RowHeight,
                        content.width,
                        RowHeight - 2f);
                    bool selected = chosen != null &&
                        string.Equals(chosen.Name, preset.Name, System.StringComparison.Ordinal);
                    if (GUI.Toggle(
                            row,
                            selected,
                            DimensionBorrowedAttackCatalog.Label(preset),
                            LeftAlignedButton) != selected)
                    {
                        chosen = selected ? null : preset;
                        lastResult = string.Empty;
                        lastResultKind = MessageType.None;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                listViewportHeight = GUILayoutUtility.GetLastRect().height;
            }

            EditorGUILayout.EndVertical();
        }

        private void Rebuild()
        {
            string needle = (filter ?? string.Empty).Trim();
            matched.Clear();
            DimensionBorrowedAttacks.Preset[] all = DimensionBorrowedAttacks.All;
            for (int i = 0; i < all.Length; i++)
            {
                DimensionBorrowedAttacks.Preset preset = all[i];
                if (!string.Equals(kind, AnyKind, System.StringComparison.Ordinal) &&
                    !string.Equals(preset.Kind, kind, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (needle.Length > 0 &&
                    DimensionBorrowedAttackCatalog.Label(preset)
                        .IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    (preset.Kind ?? string.Empty)
                        .IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                matched.Add(preset);
            }
        }

        private void DrawDetail()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            if (chosen == null)
            {
                EditorGUILayout.LabelField(
                    "Pick one on the left to see what it does and what it brings with it.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(
                DimensionBorrowedAttackCatalog.Label(chosen),
                EditorStyles.boldLabel);

            string what = DimensionBorrowedAttackCatalog.WhatAKindIs(chosen.Kind);
            if (!string.IsNullOrEmpty(what))
            {
                EditorGUILayout.LabelField(what, EditorStyles.wordWrappedMiniLabel);
            }

            int carried = chosen.Values == null ? 0 : chosen.Values.Length;
            DimensionBorrowedAttacks.Value[] finishing =
                DimensionBorrowedAttackCatalog.FinishingValues(chosen);
            if (finishing != null)
            {
                carried += finishing.Length;
            }

            EditorGUILayout.LabelField(
                carried + " values, every one of them still yours to change afterwards.",
                EditorStyles.miniLabel);

            EnsureProbed();
            DrawWhatWillNotCome();

            GUILayout.Space(6f);
            if (GUILayout.Button("Take it", GUILayout.Height(24f)))
            {
                Take();
            }

            if (!string.IsNullOrEmpty(lastResult))
            {
                EditorGUILayout.HelpBox(lastResult, lastResultKind);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawWhatWillNotCome()
        {
            List<string> said = DimensionBorrowedAttackCatalog.Caveats(chosen);

            int silent = DimensionBorrowedAttackCatalog.SoundsWithNoNameLeft(chosen);
            if (silent > 0)
            {
                said.Add(
                    silent + (silent == 1 ? " of its sounds is" : " of its sounds are") +
                    " stored as a number the game ships no name for, so that part arrives silent.");
            }

            if (probedHomeless.Count > 0)
            {
                said.Add(
                    probedHomeless.Count + " of its values ask for something this creature no " +
                    "longer has a field for, so those parts stay at their defaults: " +
                    string.Join(", ", probedHomeless.ToArray()));
            }

            if (DimensionBorrowedAttackCatalog.CarriesADamageNumber(chosen) && ReadsDamageFromTheArea())
            {
                said.Add(
                    "This creature takes its numbers from the area it spawns in, so the game works " +
                    "its damage out again and the borrowed figure will not survive. Set its stats " +
                    "to the typed-in numbers if you want to keep it.");
            }

            if (said.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "Nothing is left behind. It arrives complete.",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Before you take it", EditorStyles.miniBoldLabel);
            for (int i = 0; i < said.Count; i++)
            {
                EditorGUILayout.HelpBox(said[i], MessageType.Warning);
            }
        }

        /// <summary>
        /// Whether the creature's numbers come from the area rather than from what was typed.
        /// </summary>
        /// <remarks>
        /// Read straight off the asset each time it is asked, because a creator can change the
        /// answer in the panel behind this window while it is open.
        /// </remarks>
        private bool ReadsDamageFromTheArea()
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty source = serialized.FindProperty("creatureStats.statSource");
            return source != null &&
                source.propertyType == SerializedPropertyType.Enum &&
                source.enumValueIndex == (int)DimensionCreatureStatSource.AreaLevelCurve;
        }

        /// <summary>
        /// Works out which of the move's values have nowhere to land, without touching the asset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A scratch creature is used rather than the real one because finding out whether a value
        /// belonging to a charge or a leap has a home means having an entry in the ability list to
        /// look inside, and growing the real creature's list to answer a question would be an edit
        /// nobody asked for.
        /// </para>
        /// <para>
        /// Every path in the table lands today. This is here so that it stays true: the table is
        /// regenerated from the game, the fields are renamed by hand, and the day the two disagree
        /// a creator should be told which part of the move is missing rather than left to notice.
        /// </para>
        /// </remarks>
        private void EnsureProbed()
        {
            if (chosen == null ||
                string.Equals(probedPresetName, chosen.Name, System.StringComparison.Ordinal))
            {
                return;
            }

            probedPresetName = chosen.Name;
            probedHomeless.Clear();

            DimensionMobAsset scratch = ScriptableObject.CreateInstance<DimensionMobAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(scratch);
                SerializedProperty combat = serialized.FindProperty("combat");
                if (combat == null)
                {
                    return;
                }

                SerializedProperty abilities = combat.FindPropertyRelative("abilities");
                SerializedProperty ability = null;
                if (abilities != null && abilities.isArray)
                {
                    abilities.arraySize = 1;
                    ability = abilities.GetArrayElementAtIndex(0);
                }

                Probe(chosen.Values, combat, ability);
                Probe(DimensionBorrowedAttackCatalog.FinishingValues(chosen), combat, ability);
            }
            finally
            {
                Object.DestroyImmediate(scratch);
            }
        }

        private void Probe(
            DimensionBorrowedAttacks.Value[] values,
            SerializedProperty combat,
            SerializedProperty ability)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                string path = values[i].Path ?? string.Empty;
                SerializedProperty field;
                if (path.StartsWith("@ability.", System.StringComparison.Ordinal))
                {
                    field = ability == null ? null : ability.FindPropertyRelative(path.Substring(9));
                }
                else
                {
                    field = combat.FindPropertyRelative(path);
                }

                if (field == null)
                {
                    probedHomeless.Add(path);
                }
            }
        }

        private void Take()
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty combat = serialized.FindProperty("combat");
            if (combat == null)
            {
                lastResult =
                    "This is not a creature — it has no fight settings for a move to land in.";
                lastResultKind = MessageType.Error;
                return;
            }

            List<string> lost = new List<string>();
            int written = DimensionBorrowedAttackUtility.Apply(combat, chosen, lost.Add);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);

            if (lost.Count > 0)
            {
                lastResult =
                    "Took " + written + " values. These did not land:\n" +
                    string.Join("\n", lost.ToArray());
                lastResultKind = MessageType.Warning;
            }
            else
            {
                lastResult =
                    "Took " + written + " values. They are ordinary fields on the creature now — " +
                    "change any of them, or leave it exactly as the game has it.";
                lastResultKind = MessageType.Info;
            }

            if (onApplied != null)
            {
                onApplied();
            }
        }
    }
}
