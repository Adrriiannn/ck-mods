using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Draws one talent row with the three lists it actually needs behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TALENT ROW WAS THREE BLANK TEXT BOXES. The skill it belongs to is one of twelve names the
    /// game has; the effect it grants is one of the game's own or one this mod invented; the name
    /// is either one of the game's own 96 (which is the ordinary "change only the numbers" case) or
    /// a new one. None of those three were written down anywhere a creator could read, and getting
    /// any of them wrong is silent — a skill the game does not have drops the whole tree, and an
    /// effect it does not have writes the talent as granting nothing.
    /// </para>
    /// <para>
    /// WHY A DRAWER AND NOT A ROW IN THE CATALOG. The talents are an array of structs, and the
    /// catalog names one field by path. A path cannot name "the skill on the fourth talent", so a
    /// row there could never reach these. A drawer reaches them wherever the array is drawn,
    /// including inside the "Everything else" fold — which is where a nested field like this one
    /// otherwise ends up.
    /// </para>
    /// <para>
    /// TYPING STILL WORKS. Each box is still the box; the button beside it is an offer.
    /// </para>
    /// </remarks>
    [CustomPropertyDrawer(typeof(DimensionTalent))]
    internal sealed class DimensionTalentDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 70f;

        private static readonly string[] Paths =
        {
            "skill", "talentName", "gives", "perPoint", "shownAs", "icon"
        };

        private static readonly string[] Labels =
        {
            "Skill", "Talent name", "Grants", "Per point", "Shown as", "Picture"
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (!property.isExpanded)
            {
                return line;
            }

            int rows = 1;
            for (int i = 0; i < Paths.Length; i++)
            {
                if (property.FindPropertyRelative(Paths[i]) != null)
                {
                    rows++;
                }
            }

            return rows * line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float step = line + EditorGUIUtility.standardVerticalSpacing;
            Rect row = new Rect(position.x, position.y, position.width, line);

            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, Summary(property), true);
            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < Paths.Length; i++)
            {
                SerializedProperty field = property.FindPropertyRelative(Paths[i]);
                if (field == null)
                {
                    continue;
                }

                row.y += step;
                DrawOne(row, field, Labels[i], Paths[i], property);
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawOne(
            Rect row,
            SerializedProperty field,
            string label,
            string path,
            SerializedProperty talent)
        {
            bool hasList = path == "skill" || path == "talentName" || path == "gives";
            if (!hasList)
            {
                EditorGUI.PropertyField(row, field, new GUIContent(label));
                return;
            }

            Rect box = new Rect(row.x, row.y, row.width - ButtonWidth - 4f, row.height);
            Rect button = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height);
            EditorGUI.PropertyField(box, field, new GUIContent(label));

            if (!GUI.Button(button, "Browse"))
            {
                return;
            }

            // Captured by path off the owning object, not by holding the property: the inspector
            // rebuilds these between the click and the pick, and a held property is dead by then.
            Object target = field.serializedObject.targetObject;
            string propertyPath = field.propertyPath;

            switch (path)
            {
                case "skill":
                    DimensionNamePickerWindow.Open(
                        "Pick a skill",
                        "Which of the twelve trees this talent sits on. A name that is not one of " +
                        "them leaves every talent for it out of the build.",
                        string.Empty,
                        DimensionFieldChoosers.SkillEntries(),
                        field.stringValue,
                        picked => Write(target, propertyPath, picked));
                    break;

                case "talentName":
                    DimensionNamePickerWindow.Open(
                        "Pick a talent name",
                        "Reusing one of Core Keeper's own talent names keeps its wording and its " +
                        "picture in every language, and changes only the numbers. Type anything " +
                        "else and it becomes a talent of yours, with a line written for it.",
                        string.Empty,
                        DimensionFieldChoosers.TalentEntries(),
                        field.stringValue,
                        picked => Write(target, propertyPath, picked));
                    break;

                default:
                    DimensionNamePickerWindow.Open(
                        "Pick an effect",
                        "Core Keeper's own effects, and the ones this project invents. A name that " +
                        "is neither writes the talent as granting nothing, and is reported when " +
                        "you generate.",
                        "Grants nothing",
                        DimensionFieldChoosers.ConditionEntries(null),
                        field.stringValue,
                        picked => Write(target, propertyPath, picked));
                    break;
            }
        }

        private static void Write(Object target, string propertyPath, string value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>The one line the collapsed row shows, so a list of eight reads at a glance.</summary>
        private static string Summary(SerializedProperty property)
        {
            SerializedProperty skill = property.FindPropertyRelative("skill");
            SerializedProperty name = property.FindPropertyRelative("talentName");
            string skillText = skill == null || string.IsNullOrEmpty(skill.stringValue)
                ? "No skill"
                : skill.stringValue;
            string nameText = name == null || string.IsNullOrEmpty(name.stringValue)
                ? "Unnamed"
                : name.stringValue;
            return skillText + " — " + nameText;
        }
    }
}
