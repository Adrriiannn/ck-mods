using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The sounds a portal makes, and the field each one is chosen in.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
        /// <summary>What the portal sounds like: one card per version's own moments.</summary>
        private VisualElement BuildSoundCard(
            DimensionPortalAppearanceStudio studio,
            SerializedObject serializedTemplate)
        {
            VisualElement group = DimensionsApiControls.Group("Sound", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!studio.InstantPortalMode)
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "placedPortalActivationSound",
                    "Activation",
                    "Plays once when the portal finishes charging and lights up. A game sound's " +
                    "name or a Sound Library key. Heard within 8 tiles. Empty is silent.",
                    DimensionSoundPickerWindow.FieldPlacedActivation,
                    false));
                return group;
            }

            SerializedProperty mode = serializedTemplate.FindProperty("instantPortalSoundMode");
            int currentMode = mode == null ? 0 : Mathf.Clamp(mode.intValue, 0, 1);
            body.Add(DimensionsApiControls.Field(
                "Mode",
                "Peak plays one sound as the portal opens and another as it closes. Loop plays " +
                "one sound the whole time it stands open.",
                DimensionsApiControls.Tabs(
                    new[] { "Peak", "Loop" },
                    currentMode,
                    index =>
                    {
                        if (mode != null && mode.intValue != index)
                        {
                            mode.intValue = index;
                            serializedTemplate.ApplyModifiedProperties();
                            NotifyTemplateSoundEdited();
                            DeferredRefresh();
                        }
                    })));

            if (currentMode == 0)
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalActivationSound",
                    "Activation",
                    "Plays once as the portal tears open. A game sound's name or a Sound " +
                    "Library key. Heard within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantActivation,
                    false));
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalDeactivationSound",
                    "Deactivation",
                    "Plays once as the portal winks out. A game sound's name or a Sound " +
                    "Library key. Heard within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantDeactivation,
                    false));
            }
            else
            {
                body.Add(BuildSoundKeyRow(
                    serializedTemplate,
                    "instantPortalLoopSound",
                    "Loop",
                    "Loops while the portal stands open and stops as it closes. Needs a Sound " +
                    "Library clip, because the game's own one-shot sounds cannot loop. Heard " +
                    "within 8 tiles.",
                    DimensionSoundPickerWindow.FieldInstantLoop,
                    true));
            }

            return group;
        }

        /// <summary>A sound key with a browse button beside it, bound to the dimension.</summary>
        private VisualElement BuildSoundKeyRow(
            SerializedObject serializedTemplate,
            string propertyPath,
            string label,
            string tooltip,
            string pickerFieldId,
            bool clipOnly)
        {
            SerializedProperty property = serializedTemplate.FindProperty(propertyPath);
            if (property == null)
            {
                return new VisualElement();
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            TextField key = new TextField();
            key.BindProperty(property);
            key.style.flexGrow = 1;
            row.Add(key);

            DimensionTemplateAsset pickerTemplate = template;
            Button browse = DimensionsApiControls.GhostButton(
                "Browse",
                () => DimensionSoundPickerWindow.Open(pickerTemplate, pickerFieldId, clipOnly));
            browse.tooltip = "Pick from every sound the game and the Sound Library carry, and " +
                             "hear each one before choosing.";
            browse.style.marginLeft = 6;
            row.Add(browse);

            VisualElement field = DimensionsApiControls.Field(label, tooltip, row);
            // A sound is a dimension edit, not a change to the look: it must not trip the
            // profile's unsaved-work counter, so the event stops here and the template is told.
            AfterBinding(field, () => field.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                NotifyTemplateSoundEdited();
                evt.StopPropagation();
            }));
            return field;
        }

        private void NotifyTemplateSoundEdited()
        {
            DimensionPortalAppearanceStudio studio = Studio;
            if (studio != null)
            {
                studio.NotifyTemplateEdited();
            }
        }
    }
}
