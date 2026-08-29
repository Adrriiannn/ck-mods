using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The shared vocabulary every stage page is built from: group cards, labelled fields with a
    /// help badge, chip rows, tab strips and empty states.
    /// </summary>
    /// <remarks>
    /// Pages call these rather than assembling raw elements, which is the only practical way to
    /// keep one style across a framework this wide. A field built here always looks and behaves
    /// like a field built anywhere else, and every one of them can carry an explanation.
    /// </remarks>
    internal static class DimensionsApiControls
    {
        /// <summary>
        /// The round help badge: a filled dark circle with a white question mark, hovering to
        /// explain the field beside it. Built from a styled label rather than an image so it
        /// stays crisp at any zoom and follows the theme.
        /// </summary>
        internal static VisualElement HelpBadge(string tooltip)
        {
            Label badge = new Label("?");
            badge.AddToClassList("dim-help");
            badge.tooltip = string.IsNullOrEmpty(tooltip) ? string.Empty : tooltip;
            badge.pickingMode = PickingMode.Position;
            return badge;
        }

        /// <summary>A titled card. The head is index 0 and the body index 1, always.</summary>
        /// <remarks>
        /// The hint is deliberately not rendered. Cards used to carry a small grey caption
        /// beside their title, and the design ruling was that they read as clutter: a card's
        /// title says what the card is, the fields inside say the rest, and anything that needs
        /// more explanation belongs on a field's own help badge. The parameter survives so the
        /// many existing call sites stay valid; it now feeds the head's tooltip and nothing
        /// visible.
        /// </remarks>
        internal static VisualElement Group(string title, string hint)
        {
            VisualElement group = new VisualElement();
            group.AddToClassList("dim-group");

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-group-head");
            Label heading = new Label(title);
            heading.AddToClassList("dim-h3");
            if (!string.IsNullOrEmpty(hint))
            {
                heading.tooltip = hint;
            }

            head.Add(heading);

            group.Add(head);

            VisualElement body = new VisualElement();
            body.AddToClassList("dim-group-body");
            group.Add(body);
            return group;
        }

        /// <summary>The body of a card built by <see cref="Group"/>.</summary>
        internal static VisualElement BodyOf(VisualElement group)
        {
            return group[1];
        }

        /// <summary>
        /// Runs a registration only after a freshly built element has finished binding.
        /// </summary>
        /// <remarks>
        /// Binding a field fires an initial change event as it adopts the property's value —
        /// and so does assigning <c>.value</c> or <c>.index</c> while building. A side-effectful
        /// handler registered at build time receives that furniture-moving as if the creator had
        /// edited something. On two pages that turned into a self-sustaining loop of rebuild,
        /// rebind, phantom edit, synchronous asset import — the busy cursor flickering forever.
        /// Registering once, after the element has settled, removes the class of bug: every
        /// event a handler sees is a real edit.
        /// </remarks>
        internal static void AfterBinding(VisualElement element, System.Action register)
        {
            element.schedule.Execute(() => register()).StartingIn(60);
        }

        /// <summary>
        /// A labelled row: the label, its help badge, and the control beneath. Laying the control
        /// under its label rather than beside it gives long values room and keeps every page
        /// aligned regardless of how wide a label happens to be.
        /// </summary>
        internal static VisualElement Field(string label, string tooltip, VisualElement control)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-field");

            VisualElement head = new VisualElement();
            head.AddToClassList("dim-field-head");
            Label caption = new Label(label);
            caption.AddToClassList("dim-field-label");
            head.Add(caption);
            if (!string.IsNullOrEmpty(tooltip))
            {
                head.Add(HelpBadge(tooltip));
            }

            row.Add(head);
            if (control != null)
            {
                control.AddToClassList("dim-field-control");
                row.Add(control);
            }

            return row;
        }

        /// <summary>A control bound to a serialized property, or a clear note when it is gone.</summary>
        internal static VisualElement Bound(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip)
        {
            return Bound(serialized, propertyPath, label, tooltip, null);
        }

        /// <summary>
        /// The same row, with the option of a control the field builds for itself.
        /// </summary>
        /// <remarks>
        /// The chooser is asked first and the property's own type is the fallback, so a row that
        /// does not name one is byte for byte the row it was before. A chooser that returns null —
        /// because the value it lists could not be read — falls back the same way rather than
        /// leaving a hole where the field was.
        /// </remarks>
        internal static VisualElement Bound(
            SerializedObject serialized,
            string propertyPath,
            string label,
            string tooltip,
            System.Func<SerializedProperty, VisualElement> chooser)
        {
            SerializedProperty property = serialized == null
                ? null
                : serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return MissingField(label, propertyPath);
            }

            VisualElement control = chooser == null ? null : chooser(property);
            if (control == null)
            {
                control = ControlFor(property);
            }
            if (control == null)
            {
                PropertyField fallback = new PropertyField(property, string.Empty);
                fallback.Bind(serialized);
                control = fallback;
            }

            return Field(label, tooltip, control);
        }

        /// <summary>
        /// The right control for a property's type. Anything without a natural control falls back
        /// to a PropertyField, so a page never silently omits a value.
        /// </summary>
        private static VisualElement ControlFor(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                {
                    TextField field = new TextField();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Integer:
                {
                    IntegerField field = new IntegerField();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Float:
                {
                    FloatField field = new FloatField();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Boolean:
                {
                    Toggle field = new Toggle();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Color:
                {
                    ColorField field = new ColorField();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.ObjectReference:
                {
                    PropertyField field = new PropertyField(property, string.Empty);
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Enum:
                {
                    PropertyField field = new PropertyField(property, string.Empty);
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Vector2Int:
                {
                    Vector2IntField field = new Vector2IntField();
                    field.BindProperty(property);
                    return field;
                }

                case SerializedPropertyType.Vector2:
                {
                    Vector2Field field = new Vector2Field();
                    field.BindProperty(property);
                    return field;
                }

                default:
                    return null;
            }
        }

        private static VisualElement MissingField(string label, string propertyPath)
        {
            // A page naming a field the asset no longer has is a bug in the page, not something
            // to hide from whoever is looking at it.
            Label note = new Label(label + " is not on this asset any more (" + propertyPath + ").");
            note.AddToClassList("dim-missing");
            return note;
        }

        /// <summary>A row of chips, wrapping as needed.</summary>
        internal static VisualElement ChipRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("dim-chip-row");
            return row;
        }

        internal static Label Chip(string text, string variant = null)
        {
            Label chip = new Label(text);
            chip.AddToClassList("dim-chip");
            if (!string.IsNullOrEmpty(variant))
            {
                chip.AddToClassList("dim-chip-" + variant);
            }

            return chip;
        }

        /// <summary>
        /// One tab. A page that owns a strip of them calls <see cref="Tabs"/>; a page whose tabs
        /// sit among other controls, or whose current tab is decided by something other than the
        /// last click, builds them one at a time from here. Either way there is a single place
        /// that decides what a tab looks like.
        /// </summary>
        internal static Button Tab(
            string text,
            string tooltip,
            bool current,
            System.Action onSelect)
        {
            Button tab = new Button(onSelect) { text = text };
            tab.AddToClassList("dim-tab");
            if (!string.IsNullOrEmpty(tooltip))
            {
                tab.tooltip = tooltip;
            }

            if (current)
            {
                tab.AddToClassList("dim-tab-current");
            }

            return tab;
        }

        /// <summary>The tab strip a stage uses when it owns more than one kind of thing.</summary>
        internal static VisualElement Tabs(
            IReadOnlyList<string> labels,
            int selected,
            System.Action<int> onSelect)
        {
            VisualElement strip = new VisualElement();
            strip.AddToClassList("dim-tabs");
            for (int i = 0; i < labels.Count; i++)
            {
                int index = i;
                strip.Add(Tab(labels[i], null, i == selected, () => onSelect(index)));
            }

            return strip;
        }

        /// <summary>What a page shows when there is nothing in it yet.</summary>
        internal static VisualElement EmptyState(string headline, string help, Button action)
        {
            VisualElement empty = new VisualElement();
            empty.AddToClassList("dim-empty");

            Label title = new Label(headline);
            title.AddToClassList("dim-empty-title");
            empty.Add(title);

            if (!string.IsNullOrEmpty(help))
            {
                Label body = new Label(help);
                body.AddToClassList("dim-empty-body");
                empty.Add(body);
            }

            if (action != null)
            {
                action.AddToClassList("dim-button");
                action.AddToClassList("dim-button-primary");
                empty.Add(action);
            }

            return empty;
        }

        internal static Button PrimaryButton(string text, System.Action action)
        {
            Button button = new Button(action) { text = text };
            button.AddToClassList("dim-button");
            button.AddToClassList("dim-button-primary");
            return button;
        }

        internal static Button GhostButton(string text, System.Action action)
        {
            Button button = new Button(action) { text = text };
            button.AddToClassList("dim-button");
            button.AddToClassList("dim-button-ghost");
            return button;
        }
    }
}
