using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Puts a Browse button beside a box whose right answers are a list the game holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A DRAWER RATHER THAN A ROW ON A PAGE. Most of these fields are nested — a sound on the
    /// attack block inside a creature, a puff on the look block inside a plant — and the pages are
    /// built from field paths, which cannot name something inside an array of structs. A drawer
    /// reaches the field wherever it is drawn, including inside the "Everything else" fold, which
    /// is where a nested field otherwise lands with its raw C# name and nothing else.
    /// </para>
    /// <para>
    /// The box is still the box: the value can always be typed, which a mod shipping a sound of its
    /// own needs.
    /// </para>
    /// </remarks>
    internal abstract class DimensionValueListDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 70f;

        protected abstract string Headline { get; }

        protected abstract string Help { get; }

        /// <summary>The label on the "none of them" button, or empty for no such button.</summary>
        protected abstract string EmptyValueLabel { get; }

        protected abstract IReadOnlyList<DimensionNamePickerWindow.Entry> Entries();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                // The mark was put on something that is not a text field. Drawing it normally is
                // better than drawing nothing, and the mark is simply ignored.
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Rect box = new Rect(
                position.x, position.y, position.width - ButtonWidth - 4f, position.height);
            Rect button = new Rect(
                position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            EditorGUI.PropertyField(box, property, label);
            if (!GUI.Button(button, "Browse"))
            {
                return;
            }

            // Held by path off the owning object rather than as a property: the inspector rebuilds
            // these between the click and the pick, and a held property is dead by then.
            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            DimensionNamePickerWindow.Open(
                Headline,
                Help,
                EmptyValueLabel,
                Entries(),
                property.stringValue,
                picked => Write(target, path, picked));
        }

        private static void Write(UnityEngine.Object target, string path, string value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(DimensionSoundNameAttribute))]
    internal sealed class DimensionSoundNameDrawer : DimensionValueListDrawer
    {
        protected override string Headline
        {
            get { return "Pick a sound"; }
        }

        protected override string Help
        {
            get
            {
                return "These are the names Core Keeper's own sounds go by. A sound your mod " +
                    "ships has a name of its own and is typed in rather than picked. This list " +
                    "cannot play a sound: the name and the audio file are addressed two different " +
                    "ways, and only the file can be played.";
            }
        }

        protected override string EmptyValueLabel
        {
            get { return "No sound"; }
        }

        protected override IReadOnlyList<DimensionNamePickerWindow.Entry> Entries()
        {
            return DimensionFieldChoosers.SoundEntries();
        }
    }

    [CustomPropertyDrawer(typeof(DimensionPuffNameAttribute))]
    internal sealed class DimensionPuffNameDrawer : DimensionValueListDrawer
    {
        protected override string Headline
        {
            get { return "Pick a puff"; }
        }

        protected override string Help
        {
            get
            {
                return "A puff is the little burst of dust, leaves or sparks the game throws off. " +
                    "These are all of Core Keeper's own; a mod cannot add one, and a name that is " +
                    "not one of them falls back to leaves.";
            }
        }

        protected override string EmptyValueLabel
        {
            get { return "No puff"; }
        }

        protected override IReadOnlyList<DimensionNamePickerWindow.Entry> Entries()
        {
            return DimensionFieldChoosers.PuffEntries();
        }
    }

    [CustomPropertyDrawer(typeof(DimensionSkillNameAttribute))]
    internal sealed class DimensionSkillNameDrawer : DimensionValueListDrawer
    {
        protected override string Headline
        {
            get { return "Pick a skill"; }
        }

        protected override string Help
        {
            get
            {
                return "The twelve skills are the game's. A name that is not one of them is left " +
                    "out of the build with a warning rather than written into a table with no " +
                    "room for it.";
            }
        }

        protected override string EmptyValueLabel
        {
            get { return string.Empty; }
        }

        protected override IReadOnlyList<DimensionNamePickerWindow.Entry> Entries()
        {
            return DimensionFieldChoosers.SkillEntries();
        }
    }

    [CustomPropertyDrawer(typeof(DimensionConditionNameAttribute))]
    internal sealed class DimensionConditionNameDrawer : DimensionValueListDrawer
    {
        protected override string Headline
        {
            get { return "Pick an effect"; }
        }

        protected override string Help
        {
            get
            {
                return "Core Keeper's own effects, and the ones this project invents. A name that " +
                    "is neither is reported when you generate.";
            }
        }

        protected override string EmptyValueLabel
        {
            get { return "No effect"; }
        }

        protected override IReadOnlyList<DimensionNamePickerWindow.Entry> Entries()
        {
            return DimensionFieldChoosers.ConditionEntries(null);
        }
    }

    /// <summary>
    /// The list of stat effects, wherever one is edited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SINGLE MOST IMPORTANT FIELD IN THE CONDITIONS DOMAIN HAD NO CONTROL OF ITS OWN. What a
    /// condition actually does is one of 144 members of an enum Core Keeper never grouped, and
    /// Unity's stock popup lists all of them as raw C# identifiers, unsearchable, with six reserved
    /// placeholders and an end marker in the middle.
    /// </para>
    /// <para>
    /// Drawn for the type rather than for a marked field because a mod's condition asset is the
    /// only place the type appears, so there is nothing else this can reach and change.
    /// </para>
    /// </remarks>
    [CustomPropertyDrawer(typeof(ConditionEffect))]
    internal sealed class DimensionConditionEffectDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Rect value = EditorGUI.PrefixLabel(position, label);
            string current =
                property.enumValueIndex >= 0 &&
                property.enumValueIndex < property.enumNames.Length
                    ? property.enumNames[property.enumValueIndex]
                    : string.Empty;

            if (!GUI.Button(value, string.IsNullOrEmpty(current) ? "Pick an effect" : current,
                EditorStyles.popup))
            {
                return;
            }

            UnityEngine.Object target = property.serializedObject.targetObject;
            string path = property.propertyPath;
            DimensionNamePickerWindow.Open(
                "Pick an effect",
                "This is the part Core Keeper implements. You choose which of its effects your " +
                "condition applies; how much, for how long and on what terms are yours.",
                string.Empty,
                DimensionFieldChoosers.EffectEntries(),
                current,
                picked => WriteByName(target, path, picked));
        }

        /// <summary>
        /// Writes the enum by NAME, because <c>enumValueIndex</c> is a position in the list of
        /// names and not the value behind it. They agree for this enum, and writing by name means
        /// they never have to.
        /// </summary>
        private static void WriteByName(UnityEngine.Object target, string path, string name)
        {
            if (target == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
            {
                return;
            }

            string[] names = property.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    property.enumValueIndex = i;
                    serialized.ApplyModifiedProperties();
                    return;
                }
            }
        }
    }
}
