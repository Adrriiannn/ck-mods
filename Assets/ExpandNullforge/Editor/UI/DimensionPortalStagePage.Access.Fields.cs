using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The bound fields the access cards are built from, and the notices around them.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
        // ------------------------------------------------------------------ plumbing --

        /// <summary>
        /// A bound control for a property of the rule, labelled and explained.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The shared helper takes a path from the root of an object, which array rows do not have
        /// to hand. They do not need it: a property already carries its own full path, so this
        /// hands that over and the row is the shared row.
        /// </para>
        /// <para>
        /// IT USED TO HAVE ITS OWN <c>switch (property.propertyType)</c>, and that made these rows
        /// the one place in the studio that never asked <c>DimensionsApiControls</c> anything. The
        /// worst-served field in the tree sits here — the portal's offering, a bare box naming an
        /// item, checked by nothing before this change — so a mark added to that field later would
        /// have been drawn everywhere except the page a creator actually fills it in on. The switch
        /// is gone rather than mirrored: the shared control answers string, int, float and bool
        /// with the same four controls this did, and answers colours, object references and
        /// vectors as well, which this did not.
        /// </para>
        /// </remarks>
        private static VisualElement BoundField(
            SerializedProperty property,
            string label,
            string tooltip)
        {
            if (property == null)
            {
                return DimensionsApiControls.Field(label, tooltip, null);
            }

            return DimensionsApiControls.Bound(
                property.serializedObject,
                property.propertyPath,
                label,
                tooltip);
        }

        /// <summary>
        /// A toggle that decides whether the controls under it exist at all, so turning it redraws
        /// the column rather than leaving a settings gap the author has to click away to notice.
        /// </summary>
        private VisualElement RedrawingToggle(string propertyPath, string label, string tooltip)
        {
            VisualElement field = DimensionsApiControls.Bound(
                serializedAccessRule, propertyPath, label, tooltip);
            Toggle toggle = field.Q<Toggle>();
            if (toggle != null)
            {
                AfterBinding(toggle, () =>
                    toggle.RegisterValueChangedCallback(evt => DeferredRefresh()));
            }

            return field;
        }

        private void SetRuleString(string propertyPath, string value)
        {
            if (serializedAccessRule == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty property = serializedAccessRule.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void RemoveArrayElement(string arrayPath, int index)
        {
            if (serializedAccessRule == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty array = serializedAccessRule.FindProperty(arrayPath);
            if (array == null || !array.isArray || index < 0 || index >= array.arraySize)
            {
                return;
            }

            array.DeleteArrayElementAtIndex(index);
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void NotifyRuleEdited()
        {
            NotifyRuleEdited(false);
        }

        private void NotifyRuleEdited(bool returnPortal)
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null)
            {
                studio.NotifyRuleEdited(returnPortal ? returnRule : accessRule);
            }
        }

        private void StopRuleEditsAtTheirCard(VisualElement card)
        {
            StopRuleEditsAtTheirCard(card, false);
        }

        /// <summary>
        /// Keeps a card's edits from being counted as changes to the portal's artwork.
        /// </summary>
        /// <remarks>
        /// Registered only once the card has finished binding. Binding a field fires a change event
        /// as it takes the property's value, and a handler that is already listening treats that as
        /// a real edit — which on this page used to mean an artwork rebake, a synchronous import, a
        /// refresh, and around again forever.
        /// </remarks>
        private void StopRuleEditsAtTheirCard(VisualElement card, bool returnPortal)
        {
            AfterBinding(card, () =>
            {
                card.RegisterCallback<ChangeEvent<bool>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<int>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<float>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<string>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<Vector2>>(evt => RuleEdited(evt, returnPortal));
                card.RegisterCallback<ChangeEvent<Object>>(evt => RuleEdited(evt, returnPortal));
            });
        }

        private void RuleEdited(EventBase evt, bool returnPortal)
        {
            NotifyRuleEdited(returnPortal);
            evt.StopPropagation();
        }

        private VisualElement Note(string text, bool warning)
        {
            Label note = new Label(text);
            note.AddToClassList("dim-note");
            if (warning)
            {
                note.AddToClassList("dim-note-warn");
            }

            return note;
        }

        /// <summary>A run-together name broken into words, so a page never shows code casing.</summary>
        private static string Spaced(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            System.Text.StringBuilder spaced = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                {
                    spaced.Append(' ');
                }

                spaced.Append(value[i]);
            }

            return spaced.ToString();
        }
    }
}
