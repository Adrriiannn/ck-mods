using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// A searchable list of legal values for a field, so nobody has to know a name by heart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FOUR FIELDS IN THIS FRAMEWORK ASK FOR A NAME OUT OF A LIST THE GAME HOLDS — a stat effect, a
    /// sound, a puff of dust, a skill — and until now every one of them was a blank text box. The
    /// legal values existed; they were in a decompile, or in a fourteen-hundred-line generated file.
    /// A wrong value in any of them fails the same way: it looks fine, it generates, and in the game
    /// it does nothing at all.
    /// </para>
    /// <para>
    /// ONE WINDOW RATHER THAN FOUR. Each field hands in its own rows and its own headline; the
    /// searching, the grouping and the scrolling are the same everywhere, so they are written once.
    /// The grouping is the framework's own doing — the game groups none of these — which is why
    /// there is always a "Show every one" box: nobody is ever locked out of a value by a heading
    /// somebody here chose.
    /// </para>
    /// <para>
    /// TYPING STILL WORKS. This window sits beside the text box, it does not replace it. A mod that
    /// ships a sound of its own has a name no list here can know, and that name must stay typeable.
    /// </para>
    /// </remarks>
    internal sealed class DimensionNamePickerWindow : EditorWindow
    {
        /// <summary>One offerable value.</summary>
        internal sealed class Entry
        {
            internal Entry(string value, string group, string note)
            {
                Value = value ?? string.Empty;
                Group = string.IsNullOrEmpty(group) ? "Everything else" : group;
                Note = note ?? string.Empty;
            }

            /// <summary>What gets written into the field.</summary>
            internal string Value { get; }

            /// <summary>The heading it sits under.</summary>
            internal string Group { get; }

            /// <summary>A short aside shown to the right of the row. May be empty.</summary>
            internal string Note { get; }
        }

        // Uniform row height lets the list virtualize: every row scrolls, only visible rows draw.
        private const float RowHeight = 22f;

        private string headline = string.Empty;
        private string help = string.Empty;
        private string emptyValueLabel = string.Empty;
        private List<Entry> allEntries = new List<Entry>();
        private string current = string.Empty;
        private Action<string> apply;

        private string filter = string.Empty;
        private bool showEveryOne;
        private Vector2 scroll;
        private float viewportHeight = 400f;

        // A row is either a heading or a value; both are one line tall, which is what keeps the
        // virtualization arithmetic honest once groups are in the list.
        private readonly List<string> rowHeadings = new List<string>();
        private readonly List<Entry> rowEntries = new List<Entry>();

        // Acting on a click in the middle of drawing means closing the window between one layout
        // call and the next, which Unity reports as a mismatched layout group rather than as
        // anything to do with the click. So the click is remembered and acted on once the drawing
        // has finished.
        private bool hasPendingPick;
        private string pendingPick = string.Empty;

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

        /// <summary>Opens the list. <paramref name="onPick"/> is called with the chosen value.</summary>
        internal static void Open(
            string headline,
            string help,
            string emptyValueLabel,
            IReadOnlyList<Entry> entries,
            string current,
            Action<string> onPick)
        {
            DimensionNamePickerWindow window =
                GetWindow<DimensionNamePickerWindow>(true, headline, true);
            window.minSize = new Vector2(460f, 420f);
            window.headline = headline ?? string.Empty;
            window.help = help ?? string.Empty;
            window.emptyValueLabel = emptyValueLabel ?? string.Empty;
            window.allEntries = new List<Entry>(entries ?? new Entry[0]);
            window.current = current ?? string.Empty;
            window.apply = onPick;
            window.filter = string.Empty;
            window.showEveryOne = false;
            window.scroll = Vector2.zero;
            window.Show();
        }

        private void OnGUI()
        {
            if (apply == null)
            {
                EditorGUILayout.HelpBox(
                    "The field this list was opened for is gone. Close this and open it again.",
                    MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(help))
            {
                EditorGUILayout.HelpBox(help, MessageType.None);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Find", GUILayout.Width(32f));
            filter = EditorGUILayout.TextField(filter);
            EditorGUILayout.EndHorizontal();

            showEveryOne = EditorGUILayout.ToggleLeft(
                "Show every one, in one flat list",
                showEveryOne);

            if (!string.IsNullOrEmpty(emptyValueLabel) && GUILayout.Button(emptyValueLabel))
            {
                Remember(string.Empty);
            }

            BuildRows();
            DrawRows();

            if (hasPendingPick)
            {
                hasPendingPick = false;
                Pick(pendingPick);
            }
        }

        private void Remember(string value)
        {
            hasPendingPick = true;
            pendingPick = value ?? string.Empty;
        }

        /// <summary>
        /// Flattens the matching entries into one list of drawable rows, headings included.
        /// </summary>
        private void BuildRows()
        {
            rowHeadings.Clear();
            rowEntries.Clear();

            string needle = (filter ?? string.Empty).Trim();
            List<Entry> matched = new List<Entry>();
            for (int i = 0; i < allEntries.Count; i++)
            {
                Entry entry = allEntries[i];
                if (needle.Length == 0 ||
                    entry.Value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Note.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched.Add(entry);
                }
            }

            bool flat = showEveryOne || needle.Length > 0;
            if (flat)
            {
                matched.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.Ordinal));
                for (int i = 0; i < matched.Count; i++)
                {
                    rowHeadings.Add(null);
                    rowEntries.Add(matched[i]);
                }

                return;
            }

            List<string> groupOrder = new List<string>();
            Dictionary<string, List<Entry>> byGroup =
                new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
            for (int i = 0; i < matched.Count; i++)
            {
                List<Entry> bucket;
                if (!byGroup.TryGetValue(matched[i].Group, out bucket))
                {
                    bucket = new List<Entry>();
                    byGroup.Add(matched[i].Group, bucket);
                    groupOrder.Add(matched[i].Group);
                }

                bucket.Add(matched[i]);
            }

            for (int g = 0; g < groupOrder.Count; g++)
            {
                rowHeadings.Add(groupOrder[g]);
                rowEntries.Add(null);

                List<Entry> bucket = byGroup[groupOrder[g]];
                bucket.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.Ordinal));
                for (int i = 0; i < bucket.Count; i++)
                {
                    rowHeadings.Add(null);
                    rowEntries.Add(bucket[i]);
                }
            }
        }

        private void DrawRows()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (rowEntries.Count == 0)
            {
                EditorGUILayout.LabelField("Nothing matches that.", EditorStyles.miniLabel);
            }
            else
            {
                Rect content = GUILayoutUtility.GetRect(
                    1f, rowEntries.Count * RowHeight, GUILayout.ExpandWidth(true));
                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight) - 2);
                int lastVisible = Mathf.Min(
                    rowEntries.Count - 1,
                    Mathf.CeilToInt((scroll.y + viewportHeight) / RowHeight) + 2);
                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    Rect row = new Rect(
                        content.x, content.y + i * RowHeight, content.width, RowHeight - 2f);
                    if (rowEntries[i] == null)
                    {
                        GUI.Label(row, rowHeadings[i], EditorStyles.boldLabel);
                        continue;
                    }

                    Entry entry = rowEntries[i];
                    string label = string.IsNullOrEmpty(entry.Note)
                        ? entry.Value
                        : entry.Value + "    " + entry.Note;
                    bool isCurrent = string.Equals(entry.Value, current, StringComparison.Ordinal);
                    if (GUI.Toggle(row, isCurrent, label, LeftAlignedButton) != isCurrent)
                    {
                        Remember(entry.Value);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                viewportHeight = GUILayoutUtility.GetLastRect().height;
            }
        }

        private void Pick(string value)
        {
            Action<string> sink = apply;
            apply = null;
            Close();
            if (sink != null)
            {
                sink(value);
            }
        }
    }
}
