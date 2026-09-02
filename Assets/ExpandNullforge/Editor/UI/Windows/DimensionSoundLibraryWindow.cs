using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Browser for the game's shipped audio: one flat, alphabetical list of every audio clip in
    /// Core Keeper's bundles (multi-clip banks like the sfx bundle are flattened into individual
    /// entries; bundles with no audio never appear). Selecting a clip loads it for in-editor
    /// preview and offers its runtime key — the value sound fields and Addressables loads use.
    /// The scan and preview plumbing live in <see cref="DimensionGameSoundCatalog"/>.
    /// </summary>
    internal sealed class DimensionSoundLibraryWindow : EditorWindow
    {
        // Uniform row height lets the list virtualize: every entry scrolls, only visible rows draw.
        private const float RowHeight = 22f;

        private string gamePath = string.Empty;
        private string filter = string.Empty;
        private Vector2 listScroll;
        private float listViewportHeight = 400f;
        private readonly List<DimensionGameSoundCatalog.SoundEntry> matchedEntries =
            new List<DimensionGameSoundCatalog.SoundEntry>();

        private bool hasSelection;
        private DimensionGameSoundCatalog.SoundEntry selectedEntry;
        private AudioClip selectedClip;
        private string selectedError;

        private static GUIStyle leftAlignedButton;

        // Clip names are long and often share prefixes; a centered button style clips the start
        // away. Left alignment keeps the readable part visible. Built lazily inside OnGUI
        // (GUI.skin is only valid there).
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

        [MenuItem("Dimensions API/Sound Library")]
        private static void Open()
        {
            DimensionSoundLibraryWindow window = GetWindow<DimensionSoundLibraryWindow>();
            window.titleContent = new GUIContent("Sound Library");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            gamePath = DimensionGameSoundCatalog.ResolveGamePath();
        }

        private void OnDisable()
        {
            DimensionGameSoundCatalog.StopPreview();
            DimensionGameSoundCatalog.ReleaseLoadedBundle();
        }

        private void OnGUI()
        {
            DrawGamePathRow();

            IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries =
                DimensionGameSoundCatalog.GetEntries(gamePath, out string scanError);
            if (!string.IsNullOrEmpty(scanError))
            {
                EditorGUILayout.HelpBox(scanError, MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter", GUILayout.Width(40f));
            filter = EditorGUILayout.TextField(filter);
            if (GUILayout.Button("Stop sound", GUILayout.Width(90f)))
            {
                DimensionGameSoundCatalog.StopPreview();
            }

            EditorGUILayout.EndHorizontal();

            DrawEntryList(entries);
            DrawSelectionPanel();
        }

        private void DrawGamePathRow()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Game folder", GUILayout.Width(80f));
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(gamePath) ? "<not set>" : gamePath,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                string picked = EditorUtility.OpenFolderPanel(
                    "Core Keeper install folder", gamePath, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    gamePath = picked;
                    DimensionGameSoundCatalog.SetGamePath(picked);
                    ClearSelection();
                }
            }

            if (GUILayout.Button("Rescan", GUILayout.Width(70f)))
            {
                DimensionGameSoundCatalog.InvalidateCache();
                ClearSelection();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEntryList(IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries)
        {
            string needle = (filter ?? string.Empty).Trim();
            matchedEntries.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                if (needle.Length == 0 ||
                    entries[i].DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedEntries.Add(entries[i]);
                }
            }

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            if (matchedEntries.Count == 0)
            {
                EditorGUILayout.LabelField("No sounds match the filter.", EditorStyles.miniLabel);
            }
            else
            {
                // Virtualized: reserve space for every row, draw only the ones in view.
                Rect content = GUILayoutUtility.GetRect(
                    1f, matchedEntries.Count * RowHeight, GUILayout.ExpandWidth(true));
                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / RowHeight) - 2);
                int lastVisible = Mathf.Min(
                    matchedEntries.Count - 1,
                    Mathf.CeilToInt((listScroll.y + listViewportHeight) / RowHeight) + 2);
                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    DimensionGameSoundCatalog.SoundEntry entry = matchedEntries[i];
                    Rect row = new Rect(
                        content.x,
                        content.y + i * RowHeight,
                        content.width,
                        RowHeight - 2f);
                    bool isSelected = hasSelection &&
                        string.Equals(selectedEntry.AssetPath, entry.AssetPath, StringComparison.Ordinal) &&
                        string.Equals(selectedEntry.BundlePath, entry.BundlePath, StringComparison.OrdinalIgnoreCase);
                    if (GUI.Toggle(row, isSelected, entry.DisplayName, LeftAlignedButton) != isSelected)
                    {
                        DimensionGameSoundCatalog.StopPreview();
                        if (isSelected)
                        {
                            ClearSelection();
                        }
                        else
                        {
                            SelectEntry(entry);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                listViewportHeight = GUILayoutUtility.GetLastRect().height;
            }
        }

        private void DrawSelectionPanel()
        {
            if (!hasSelection)
            {
                return;
            }

            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField(selectedEntry.DisplayName, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(selectedError))
            {
                EditorGUILayout.HelpBox(selectedError, MessageType.Warning);
            }
            else if (selectedClip != null)
            {
                EditorGUILayout.LabelField(
                    selectedClip.length.ToString("0.0") + "s   " +
                    selectedClip.frequency + " Hz   " +
                    selectedClip.channels + " ch",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedClip == null))
            {
                if (GUILayout.Button("Play", GUILayout.Width(60f)))
                {
                    DimensionGameSoundCatalog.StopPreview();
                    DimensionGameSoundCatalog.PlayPreview(selectedClip);
                }
            }

            if (GUILayout.Button("Stop", GUILayout.Width(60f)))
            {
                DimensionGameSoundCatalog.StopPreview();
            }

            if (GUILayout.Button("Copy runtime key", GUILayout.Width(130f)))
            {
                EditorGUIUtility.systemCopyBuffer = selectedEntry.AssetPath;
                ShowNotification(new GUIContent("Key copied"));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(selectedEntry.AssetPath, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void SelectEntry(DimensionGameSoundCatalog.SoundEntry entry)
        {
            selectedEntry = entry;
            hasSelection = true;
            selectedClip = DimensionGameSoundCatalog.LoadClip(entry, out selectedError);
        }

        private void ClearSelection()
        {
            hasSelection = false;
            selectedClip = null;
            selectedError = null;
        }
    }
}
