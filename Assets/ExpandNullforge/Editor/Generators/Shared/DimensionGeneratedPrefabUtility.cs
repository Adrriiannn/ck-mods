using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Small shared helpers for the generators that write prefabs the framework owns.
    /// </summary>
    internal static class DimensionGeneratedPrefabUtility
    {
        /// <summary>Adds the component if it is missing, and returns it either way.</summary>
        public static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        /// <summary>
        /// Sets an enum field by member NAME rather than by referencing the enum value.
        /// </summary>
        /// <remarks>
        /// Core Keeper's enums are authored data we do not control. Writing a member name means a
        /// renumbering cannot silently shift the value we store, and a rename fails loudly here
        /// (returns false, caller warns) instead of compiling into the wrong constant.
        /// </remarks>
        public static bool TrySetEnumMember(Object target, string propertyName, string memberName)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum ||
                property.enumNames == null)
            {
                return false;
            }

            for (int i = 0; i < property.enumNames.Length; i++)
            {
                if (string.Equals(property.enumNames[i], memberName, StringComparison.Ordinal))
                {
                    property.enumValueIndex = i;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A file-name-safe form of an id THIS FRAMEWORK BUILT. Dots survive because generated
        /// names use them as separators (<c>mod:stone.wall.block</c>); the colon and anything else
        /// does not.
        /// </summary>
        /// <remarks>
        /// Not the one to use on a name a creator typed — a dot in an authored name is just a dot
        /// and has to go, or two names that differ only by punctuation land on one file. Use
        /// <see cref="SanitizeAuthoredName"/> for those.
        /// </remarks>
        public static string SanitizeFileName(string value)
        {
            return Clean(value, "unnamed", true);
        }

        /// <summary>
        /// A file-name-safe form of a name A CREATOR TYPED, falling back to
        /// <paramref name="whenBlank"/> when there is nothing to clean.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY GENERATOR ASKS HERE, and that is the point rather than tidiness. Copies of this
        /// fall into two families that disagree about the same name: one replaces
        /// anything that is not a letter, a digit, an underscore or a hyphen, and the other only
        /// replaces the characters Windows refuses — keeping spaces, dots, <c>+</c>, <c>&amp;</c>
        /// and <c>#</c>. With both in the tree an item called <c>Ember Bolt+</c> is written to
        /// <c>Ember_Bolt_</c> by one generator and to <c>Ember Bolt+</c> by another, and the pruner,
        /// which asks one of them, cannot recognise the other's output and leaves it on disk for ever.
        /// </para>
        /// <para>
        /// THE LETTER TEST IS <c>char.IsLetterOrDigit</c>, NOT AN ASCII RANGE. Two of the fifteen
        /// tested <c>a-z A-Z 0-9</c> by hand, which turns every accented or non-Latin letter into an
        /// underscore — so a creator writing in their own language got a row of underscores where
        /// the other thirteen generators gave them their name back. Unity asset paths take those
        /// letters, so the narrower rule bought nothing.
        /// </para>
        /// </remarks>
        public static string SanitizeAuthoredName(string value, string whenBlank)
        {
            return Clean(value, whenBlank, false);
        }

        private static string Clean(string value, string whenBlank, bool keepDots)
        {
            if (string.IsNullOrEmpty(value))
            {
                return whenBlank;
            }

            char[] buffer = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool safe = char.IsLetterOrDigit(c) || c == '_' || c == '-' ||
                            (keepDots && c == '.');
                buffer[i] = safe ? c : '_';
            }

            return new string(buffer);
        }
    }
}
