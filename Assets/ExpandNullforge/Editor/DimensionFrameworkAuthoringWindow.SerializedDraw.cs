using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Drawing one authored asset from its serialized fields.
    /// </summary>
    public sealed partial class DimensionFrameworkAuthoringWindow
    {
        private struct SerializedFieldSpec
        {
            public string PropertyName;
            public string Label;
            public bool IncludeChildren;
            public bool ScopeToDimensionDataBlock;

            /// <summary>
            /// The field commits when the creator leaves it, not on every keystroke.
            /// </summary>
            /// <remarks>
            /// Only the name fields ask for this, and they have to. A bound id field writes a real
            /// serialized value per character: typing "EmberBolt" over "Blade" takes the asset
            /// through "B", "Bl", "Bla", "Blad" and the rest, and a generate started in between
            /// bakes whichever of them was current. Everything else in these panels is a number, a
            /// toggle or a block where an intermediate value costs nothing, so nothing else sets
            /// it.
            /// </remarks>
            public bool CommitsOnLeaving;

            public SerializedFieldSpec(
                string propertyName,
                string label,
                bool includeChildren,
                bool scopeToDimensionDataBlock)
                : this(propertyName, label, includeChildren, scopeToDimensionDataBlock, false)
            {
            }

            public SerializedFieldSpec(
                string propertyName,
                string label,
                bool includeChildren,
                bool scopeToDimensionDataBlock,
                bool commitsOnLeaving)
            {
                PropertyName = propertyName;
                Label = label;
                IncludeChildren = includeChildren;
                ScopeToDimensionDataBlock = scopeToDimensionDataBlock;
                CommitsOnLeaving = commitsOnLeaving;
            }
        }

        private static SerializedFieldSpec Field(string propertyName)
        {
            return new SerializedFieldSpec(propertyName, null, true, false);
        }

        private static SerializedFieldSpec Field(string propertyName, string label)
        {
            return new SerializedFieldSpec(propertyName, label, true, false);
        }

        private static SerializedFieldSpec Field(string propertyName, string label, bool includeChildren)
        {
            return new SerializedFieldSpec(propertyName, label, includeChildren, false);
        }

        /// <summary>
        /// A name field: the same row, committed once when the creator leaves it.
        /// </summary>
        /// <remarks>
        /// Used for the id of every kind of thing this window edits. See
        /// <see cref="SerializedFieldSpec.CommitsOnLeaving"/> for what a per-keystroke id field
        /// costs; the safe rename itself lives on the studio's own page, and this is the guard that
        /// stops the plain panels writing half-typed names underneath it.
        /// </remarks>
        private static SerializedFieldSpec NameField(string propertyName, string label)
        {
            return new SerializedFieldSpec(propertyName, label, true, false, true);
        }

        private static SerializedFieldSpec DimensionDataBlockField(string propertyName, string label)
        {
            return new SerializedFieldSpec(propertyName, label, true, true);
        }

        /// <summary>
        /// Offers every attack the game itself authored, for a creature to take whole.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS IS DOOR ONE. An author building a creature should never have to invent a swing from
        /// nothing just because they wanted the Hydra's. Picking one here writes its measured
        /// numbers into the ordinary fields below, where they stay editable — the creature does not
        /// remember it borrowed anything, so nothing is ever silently reverted.
        /// </para>
        /// <para>
        /// The list only appears on assets that have a combat block, which is what makes it a
        /// creature. Drawing it on a workbench would be noise.
        /// </para>
        /// </remarks>
        private void DrawBorrowedAttackPickers(
            UnityEngine.Object target,
            SerializedObject serializedObject)
        {
            SerializedProperty combat = serializedObject.FindProperty("combat");
            if (combat == null || target == null)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                "Take an attack from something in the game",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "It arrives complete, with the game's own numbers. Change as much or as little of " +
                "it as you like afterwards — or nothing at all.",
                EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button(
                    "Browse all " + DimensionBorrowedAttacks.All.Length + " with a search box"))
            {
                DimensionBorrowedAttackPickerWindow.Open(target, null, null);
            }

            for (int i = 0; i < DimensionBorrowedAttacks.Kinds.Length; i++)
            {
                string kind = DimensionBorrowedAttacks.Kinds[i];
                DrawOneBorrowedAttackPicker(target, serializedObject, combat, kind, LabelFor(kind));
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>What a row of the picker is called, in the words an author thinks in.</summary>
        /// <remarks>
        /// The kinds themselves come from the generated table, so a new sort of borrowable thing
        /// appears in the picker on its own. Anything without a phrase here falls back to its own
        /// name, which is readable enough to ship with while a better one is chosen.
        /// </remarks>
        private static string LabelFor(string kind)
        {
            switch (kind)
            {
                case "Melee":
                    return "A close-up swing";
                case "Ranged":
                    return "A ranged shot";
                case "Chase":
                    return "The way it chases";
                case "Wander":
                    return "The way it wanders";
                case "Sounds":
                    return "The sounds it makes fighting";
                case "Charge":
                    return "The way it charges";
                case "Jump":
                    return "Its leaping attack";
                case "Explode":
                    return "The way it explodes";
                case "Ray":
                    return "Its sweeping ray";
                default:
                    return kind;
            }
        }

        /// <summary>One row of the picker: a list of attacks of one kind, and a button.</summary>
        private void DrawOneBorrowedAttackPicker(
            UnityEngine.Object target,
            SerializedObject serializedObject,
            SerializedProperty combat,
            string kind,
            string label)
        {
            DimensionBorrowedAttacks.Preset[] presets = DimensionBorrowedAttacks.OfKind(kind);
            if (presets.Length == 0)
            {
                return;
            }

            string key = target.GetInstanceID() + "/" + kind;
            int chosen;
            if (!borrowedAttackChoices.TryGetValue(key, out chosen))
            {
                chosen = 0;
            }

            // The label a creator reads, not the file name the prefab was saved under. The preset's
            // own Name stays its identity everywhere else; only this list is renamed.
            string[] names = new string[presets.Length];
            for (int i = 0; i < presets.Length; i++)
            {
                names[i] = DimensionBorrowedAttackCatalog.Label(presets[i]);
            }

            EditorGUILayout.BeginHorizontal();
            chosen = EditorGUILayout.Popup(label, Mathf.Clamp(chosen, 0, presets.Length - 1), names);
            borrowedAttackChoices[key] = chosen;

            if (GUILayout.Button("Take it", GUILayout.Width(72f)))
            {
                List<string> lost = new List<string>();
                int written = DimensionBorrowedAttackUtility.Apply(combat, presets[chosen], lost.Add);
                serializedObject.ApplyModifiedProperties();

                if (lost.Count > 0)
                {
                    Debug.LogWarning(
                        "Some of '" + names[chosen] + "' could not be written:\n" +
                        string.Join("\n", lost.ToArray()),
                        target);
                }
                else
                {
                    Debug.Log(
                        "Took '" + names[chosen] + "' — " + written +
                        " values, all of them still editable below.",
                        target);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// A field's label with its help riding along, plus the little circled question mark that
        /// tells an author there IS help before they think to hover.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY AUTHORING FIELD ALREADY CARRIES ITS EXPLANATION — the templates were written with a
        /// plain-words tooltip on every single field, and Unity threads that text onto the
        /// serialized property. What was missing was any visible sign of it: a tooltip nobody knows
        /// exists is documentation nobody reads. So the label itself gains a "?" suffix whenever
        /// help exists, and hovering anywhere on the label shows it.
        /// </para>
        /// <para>
        /// Fields with no tooltip get no mark, deliberately: a "?" that reveals nothing teaches an
        /// author to stop hovering.
        /// </para>
        /// </remarks>
        private static GUIContent LabelWithHelp(string text, SerializedProperty property)
        {
            string help = property == null ? string.Empty : property.tooltip;
            if (string.IsNullOrEmpty(help))
            {
                return new GUIContent(text);
            }

            return new GUIContent(text + "  ⍰", help);
        }

        private bool DrawSerializedAsset(UnityEngine.Object target, string title, params SerializedFieldSpec[] fields)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (target != null && GUILayout.Button("Select", GUILayout.Width(72f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }

            EditorGUILayout.EndHorizontal();

            if (target == null)
            {
                EditorGUILayout.LabelField("Not assigned yet.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return false;
            }

            SerializedObject serializedObject = GetSerializedAssetBinding(target);
            serializedObject.UpdateIfRequiredOrScript();

            // ---- the first door: borrow one of the game's own attacks ----
            // Only creatures have a combat block, so only creatures are offered this.
            DrawBorrowedAttackPickers(target, serializedObject);

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedFieldSpec field = fields[i];
                SerializedProperty property = GetSerializedAssetProperty(
                    serializedObject,
                    field.PropertyName);
                if (property == null)
                {
                    EditorGUILayout.LabelField(
                        field.Label ?? ObjectNames.NicifyVariableName(field.PropertyName),
                        "Missing serialized field: " + field.PropertyName,
                        EditorStyles.wordWrappedMiniLabel);
                    continue;
                }

                GUIContent label = LabelWithHelp(
                    string.IsNullOrEmpty(field.Label)
                        ? ObjectNames.NicifyVariableName(field.PropertyName)
                        : field.Label,
                    property);
                if (field.ScopeToDimensionDataBlock)
                {
                    float propertyHeight = EditorGUI.GetPropertyHeight(
                        property,
                        label,
                        field.IncludeChildren);
                    Rect propertyRect = EditorGUILayout.GetControlRect(true, propertyHeight);
                    Event currentEvent = Event.current;
                    bool pointerInteraction =
                        currentEvent != null &&
                        (currentEvent.type == EventType.MouseDown ||
                         currentEvent.type == EventType.MouseUp) &&
                        currentEvent.button == 0 &&
                        propertyRect.Contains(currentEvent.mousePosition);
                    bool contextReady = true;
                    if (pointerInteraction &&
                        !TryScopeScriptableDataToSelectedDimension(out string contextError))
                    {
                        contextReady = false;
                        lastEditorActionMessage = contextError;
                        lastEditorActionType = MessageType.Warning;
                    }

                    bool previousGuiEnabled = GUI.enabled;
                    GUI.enabled = previousGuiEnabled && contextReady;
                    EditorGUI.PropertyField(
                        propertyRect,
                        property,
                        label,
                        field.IncludeChildren);
                    GUI.enabled = previousGuiEnabled;
                }
                else if (field.CommitsOnLeaving &&
                         property.propertyType == SerializedPropertyType.String)
                {
                    // The one row shape that must not write per keystroke. Falls through to the
                    // ordinary field for anything that is not a string, so a spec that names a
                    // field which later stops being one degrades to today's behaviour rather than
                    // drawing nothing.
                    EditorGUI.BeginChangeCheck();
                    string typed = EditorGUILayout.DelayedTextField(label, property.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.stringValue = typed;
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(property, label, field.IncludeChildren);
                }
            }

            // EVERYTHING THE CURATED LIST DID NOT MENTION.
            //
            // The panels are a first draft: their field lists were written early, from guesses about
            // what an asset would need, and the authoring layer has grown a long way past them. An
            // audit of this found whole features — creature combat, dungeons, quests — built,
            // generated and tested with no way to reach them from the dashboard at all. Maintaining
            // every list by hand against every asset would just reintroduce that gap the next time
            // the authoring layer moves.
            //
            // So a curated list means ORDERING, not permission. Whatever it leaves out is still
            // drawn, below, under its own foldout. Fields get promoted into the lists as the UI is
            // designed properly, and nothing is unreachable in the meantime.
            SerializedProperty remaining = serializedObject.GetIterator();
            bool enterChildren = true;
            List<SerializedProperty> extras = new List<SerializedProperty>();
            while (remaining.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (remaining.propertyPath == "m_Script")
                {
                    continue;
                }

                bool alreadyDrawn = false;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].PropertyName == remaining.propertyPath)
                    {
                        alreadyDrawn = true;
                        break;
                    }
                }

                if (!alreadyDrawn)
                {
                    extras.Add(remaining.Copy());
                }
            }

            if (extras.Count > 0)
            {
                string foldoutKey = target.GetInstanceID() + "/" + title;
                bool expanded;
                if (!uncuratedFieldFoldouts.TryGetValue(foldoutKey, out expanded))
                {
                    expanded = false;
                }

                expanded = EditorGUILayout.Foldout(
                    expanded,
                    "Everything else (" + extras.Count + ")",
                    true);
                uncuratedFieldFoldouts[foldoutKey] = expanded;

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < extras.Count; i++)
                    {
                        EditorGUILayout.PropertyField(
                            extras[i],
                            LabelWithHelp(extras[i].displayName, extras[i]),
                            true);
                    }

                    EditorGUI.indentLevel--;
                }
            }

            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                RebuildWorkspace();
                Repaint();
            }

            EditorGUILayout.EndVertical();
            return changed;
        }

        private SerializedObject GetSerializedAssetBinding(Object target)
        {
            if (serializedAssetBinding == null || serializedAssetBindingTarget != target)
            {
                ReleaseSerializedAssetBinding();
                serializedAssetBindingTarget = target;
                serializedAssetBinding = new SerializedObject(target);
            }

            return serializedAssetBinding;
        }

        private SerializedProperty GetSerializedAssetProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            if (serializedObject == serializedAssetBinding &&
                serializedAssetBindingProperties.TryGetValue(
                    propertyName,
                    out SerializedProperty cachedProperty) &&
                cachedProperty != null)
            {
                return cachedProperty;
            }

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (serializedObject == serializedAssetBinding && property != null)
            {
                serializedAssetBindingProperties[propertyName] = property;
            }

            return property;
        }

        private void ReleaseSerializedAssetBinding()
        {
            if (serializedAssetBinding != null)
            {
                serializedAssetBinding.Dispose();
            }

            serializedAssetBinding = null;
            serializedAssetBindingTarget = null;
            serializedAssetBindingProperties.Clear();
        }

        private bool TryScopeScriptableDataToSelectedDimension(out string error)
        {
            return DimensionScriptableDataContextUtility.TryScopeToTemplate(
                selectedTemplate,
                out error);
        }
    }
}
