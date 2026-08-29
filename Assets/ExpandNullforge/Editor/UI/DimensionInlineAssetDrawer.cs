using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Opens a referenced framework asset where it is referenced, so a thing the product let a
    /// creator make is also a thing the product lets them edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE GAP THIS CLOSES. A couple of the framework's assets are satellites: they are made from a
    /// button, they are pointed at from a field on their owner, and they appear in no list of their
    /// own — so the only way to fill one in was to find it in the Project window and use Unity's raw
    /// Inspector. Room Fillings are the clearest case: the Dungeons page offers "+ New Room
    /// Filling", writes the asset, shows a slot for it, and then has nowhere to type what the
    /// filling actually places. Being creatable and not being editable is worse than not existing,
    /// because the creator is invited into it.
    /// </para>
    /// <para>
    /// A DRAWER RATHER THAN A NEW PAGE, deliberately. These are not top-level nouns — nobody sets
    /// out to make a Room Filling, they set out to fill a dungeon's rooms — so the right place to
    /// edit one is under the field that names it, next to the thing it belongs to. A page of its
    /// own would move it away from its only context.
    /// </para>
    /// <para>
    /// Registered against the ASSET TYPES rather than the fields, so the editor follows the
    /// reference wherever it is shown: the Dungeons page, the Biome Studio, or the collection
    /// page's "Everything else" card, which is exactly where a value with no worded row ends up.
    /// </para>
    /// </remarks>
    [CustomPropertyDrawer(typeof(DimensionRoomFillingAsset))]
    internal sealed class DimensionInlineAssetDrawer : PropertyDrawer
    {
        /// <summary>
        /// One serialized view per referenced asset, kept between frames.
        /// </summary>
        /// <remarks>
        /// Rebuilding it every repaint would drop the edit in progress: a SerializedObject made
        /// this frame has not seen the keystroke the last one is still holding, so a typed field
        /// would reset itself on the next mouse move.
        /// </remarks>
        private readonly Dictionary<int, SerializedObject> openAssets =
            new Dictionary<int, SerializedObject>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Object target = property.objectReferenceValue;
            if (target == null || !property.isExpanded)
            {
                return line;
            }

            float height = line + spacing;
            SerializedObject serialized = Resolve(target);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                height += EditorGUI.GetPropertyHeight(iterator, true) + spacing;
            }

            return height + spacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Object target = property.objectReferenceValue;

            Rect row = new Rect(position.x, position.y, position.width, line);
            if (target == null)
            {
                // Nothing to open, so no arrow: an arrow that expands to nothing reads as broken.
                EditorGUI.PropertyField(row, property, label);
                return;
            }

            Rect arrow = new Rect(row.x, row.y, 14f, row.height);
            Rect field = new Rect(row.x + 14f, row.y, row.width - 14f, row.height);
            property.isExpanded = EditorGUI.Foldout(arrow, property.isExpanded, GUIContent.none, true);
            EditorGUI.PropertyField(field, property, label);

            if (!property.isExpanded)
            {
                return;
            }

            SerializedObject serialized = Resolve(target);
            serialized.Update();

            float y = position.y + line + spacing;
            EditorGUI.indentLevel++;
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    continue;
                }

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, height), iterator, true);
                y += height + spacing;
            }

            EditorGUI.indentLevel--;
            serialized.ApplyModifiedProperties();
        }

        private SerializedObject Resolve(Object target)
        {
            int id = target.GetInstanceID();
            SerializedObject serialized;
            if (openAssets.TryGetValue(id, out serialized) &&
                serialized != null && serialized.targetObject != null)
            {
                return serialized;
            }

            serialized = new SerializedObject(target);
            openAssets[id] = serialized;
            return serialized;
        }
    }
}
