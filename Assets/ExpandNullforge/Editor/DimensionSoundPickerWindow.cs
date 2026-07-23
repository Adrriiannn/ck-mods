using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Sound selector for the Portal Studio's sound fields: one flat "Vanilla Sounds" list of
    /// every audio clip in the game's bundles (with in-editor preview) — the designed sfx bank
    /// included, since each of its clips is individually addressable at runtime. Clicking Select
    /// writes the clip's runtime key straight into the field that opened the picker. Hand-typed
    /// SfxID names remain valid keys for the one-shot fields; the picker just no longer needs a
    /// separate catalog for them.
    /// </summary>
    internal sealed class DimensionSoundPickerWindow : EditorWindow
    {
        public const string FieldPlacedActivation = "placedActivation";
        public const string FieldInstantActivation = "instantActivation";
        public const string FieldInstantDeactivation = "instantDeactivation";
        public const string FieldInstantLoop = "instantLoop";

        // Uniform row height lets the list virtualize: every entry scrolls, only visible rows draw.
        private const float RowHeight = 22f;

        private DimensionTemplateAsset targetTemplate;
        private string targetFieldId = string.Empty;

        private string clipFilter = string.Empty;
        private Vector2 clipScroll;
        private float clipViewportHeight = 400f;
        private readonly List<DimensionGameSoundCatalog.SoundEntry> matchedEntries =
            new List<DimensionGameSoundCatalog.SoundEntry>();

        private bool hasSelection;
        private DimensionGameSoundCatalog.SoundEntry selectedEntry;
        private AudioClip selectedClip;
        private string selectedError;
        private bool scrollToSelectionPending;

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

        /// <summary>
        /// Opens the picker for a sound field. <paramref name="clipOnly"/> is kept for the
        /// field call sites but no longer changes the UI — the list is clips-only for every
        /// field now.
        /// </summary>
        public static void Open(DimensionTemplateAsset template, string fieldId, bool clipOnly)
        {
            DimensionSoundPickerWindow window = GetWindow<DimensionSoundPickerWindow>(true, "Pick Sound", true);
            window.minSize = new Vector2(520f, 400f);
            window.targetTemplate = template;
            window.targetFieldId = fieldId ?? string.Empty;
            window.hasSelection = false;
            window.selectedClip = null;
            window.selectedError = null;
            window.scrollToSelectionPending = false;
            window.PreselectCurrentFieldValue();
            window.Show();
        }

        /// <summary>
        /// Highlights the sound the field currently uses: if its key matches a catalog clip, the
        /// entry opens pre-selected and the list scrolls to it. SfxID names and empty fields
        /// simply open with nothing selected.
        /// </summary>
        private void PreselectCurrentFieldValue()
        {
            if (targetTemplate == null)
            {
                return;
            }

            string currentKey;
            switch (targetFieldId)
            {
                case FieldPlacedActivation:
                    currentKey = targetTemplate.PlacedPortalActivationSound;
                    break;
                case FieldInstantActivation:
                    currentKey = targetTemplate.InstantPortalActivationSound;
                    break;
                case FieldInstantDeactivation:
                    currentKey = targetTemplate.InstantPortalDeactivationSound;
                    break;
                case FieldInstantLoop:
                    currentKey = targetTemplate.InstantPortalLoopSound;
                    break;
                default:
                    return;
            }

            if (string.IsNullOrEmpty(currentKey))
            {
                return;
            }

            string gamePath = DimensionGameSoundCatalog.ResolveGamePath();
            IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries =
                DimensionGameSoundCatalog.GetEntries(gamePath, out string scanError);
            if (!string.IsNullOrEmpty(scanError))
            {
                return;
            }

            string normalizedKey = currentKey.Trim().Replace('\\', '/');
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(
                    entries[i].AssetPath,
                    normalizedKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedEntry = entries[i];
                    hasSelection = true;
                    selectedClip = DimensionGameSoundCatalog.LoadClip(entries[i], out selectedError);
                    scrollToSelectionPending = true;
                    return;
                }
            }
        }

        private void OnDisable()
        {
            DimensionGameSoundCatalog.StopPreview();
            DimensionGameSoundCatalog.ReleaseLoadedBundle();
        }

        private void OnGUI()
        {
            if (targetTemplate == null)
            {
                EditorGUILayout.HelpBox("The target template is gone — reopen the picker.", MessageType.Info);
                return;
            }

            string gamePath = DimensionGameSoundCatalog.ResolveGamePath();
            IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries =
                DimensionGameSoundCatalog.GetEntries(gamePath, out string scanError);
            if (!string.IsNullOrEmpty(scanError))
            {
                EditorGUILayout.HelpBox(
                    scanError + " Set the game folder in Dimensions API ▸ Sound Library.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Vanilla Sounds", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter", GUILayout.Width(40f));
            clipFilter = EditorGUILayout.TextField(clipFilter);
            if (GUILayout.Button("Stop sound", GUILayout.Width(90f)))
            {
                DimensionGameSoundCatalog.StopPreview();
            }

            EditorGUILayout.EndHorizontal();

            DrawSoundList(entries);
            DrawSelectionPanel();
        }

        private void DrawSoundList(IReadOnlyList<DimensionGameSoundCatalog.SoundEntry> entries)
        {
            string needle = (clipFilter ?? string.Empty).Trim();
            matchedEntries.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                if (needle.Length == 0 ||
                    entries[i].DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedEntries.Add(entries[i]);
                }
            }

            if (scrollToSelectionPending)
            {
                scrollToSelectionPending = false;
                if (hasSelection)
                {
                    for (int i = 0; i < matchedEntries.Count; i++)
                    {
                        if (string.Equals(
                            matchedEntries[i].AssetPath,
                            selectedEntry.AssetPath,
                            StringComparison.Ordinal))
                        {
                            clipScroll.y = Mathf.Max(
                                0f, i * RowHeight - clipViewportHeight * 0.5f);
                            break;
                        }
                    }
                }
            }

            clipScroll = EditorGUILayout.BeginScrollView(clipScroll);
            if (matchedEntries.Count == 0)
            {
                EditorGUILayout.LabelField("No sounds match the filter.", EditorStyles.miniLabel);
            }
            else
            {
                // Virtualized: reserve space for every row, draw only the ones in view.
                Rect content = GUILayoutUtility.GetRect(
                    1f, matchedEntries.Count * RowHeight, GUILayout.ExpandWidth(true));
                int firstVisible = Mathf.Max(0, Mathf.FloorToInt(clipScroll.y / RowHeight) - 2);
                int lastVisible = Mathf.Min(
                    matchedEntries.Count - 1,
                    Mathf.CeilToInt((clipScroll.y + clipViewportHeight) / RowHeight) + 2);
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
                            hasSelection = false;
                            selectedClip = null;
                            selectedError = null;
                        }
                        else
                        {
                            selectedEntry = entry;
                            hasSelection = true;
                            selectedClip = DimensionGameSoundCatalog.LoadClip(entry, out selectedError);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint)
            {
                clipViewportHeight = GUILayoutUtility.GetLastRect().height;
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
                    selectedClip.length.ToString("0.0") + "s",
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

            using (new EditorGUI.DisabledScope(selectedClip == null))
            {
                if (GUILayout.Button("Select", GUILayout.Width(70f)))
                {
                    ApplySelection(selectedEntry.AssetPath);
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void ApplySelection(string soundKey)
        {
            if (targetTemplate == null)
            {
                Close();
                return;
            }

            Undo.RecordObject(targetTemplate, "Portal Sounds");
            string placedActivation = targetTemplate.PlacedPortalActivationSound;
            int instantMode = targetTemplate.InstantPortalSoundMode;
            string instantActivation = targetTemplate.InstantPortalActivationSound;
            string instantDeactivation = targetTemplate.InstantPortalDeactivationSound;
            string instantLoop = targetTemplate.InstantPortalLoopSound;
            switch (targetFieldId)
            {
                case FieldPlacedActivation:
                    placedActivation = soundKey;
                    break;
                case FieldInstantActivation:
                    instantActivation = soundKey;
                    break;
                case FieldInstantDeactivation:
                    instantDeactivation = soundKey;
                    break;
                case FieldInstantLoop:
                    instantLoop = soundKey;
                    break;
                default:
                    Close();
                    return;
            }

            targetTemplate.SetPortalSoundSettings(
                placedActivation,
                instantMode,
                instantActivation,
                instantDeactivation,
                instantLoop);
            EditorUtility.SetDirty(targetTemplate);
            DimensionGameSoundCatalog.StopPreview();
            Close();
        }
    }
}
