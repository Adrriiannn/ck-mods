using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a portal asks for before it opens, and what greets a player on arrival.
    /// </summary>
    internal sealed partial class DimensionPortalStagePage
    {
        // -------------------------------------------------------------- the offering --

        /// <summary>
        /// What the portal asks to be given before it opens.
        /// </summary>
        /// <remarks>
        /// Only the placed portal ever shows an offering window: the generator puts the window and
        /// its inventory on the entry portal's entity and on nothing else, so an offering authored
        /// on the item portal would be asked for and never askable. The card says so rather than
        /// hiding, because a rule can be switched to an offering mode from either tab.
        /// </remarks>
        private VisualElement BuildOfferingCard(bool instant)
        {
            VisualElement group = DimensionsApiControls.Group("The offering", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            if (!accessRule.UsesRequiredItems)
            {
                body.Add(Note(
                    "This portal asks for nothing. Choose an offering under \"When it opens\" to " +
                    "give it a window players put items into.",
                    false));
                return group;
            }

            if (instant)
            {
                body.Add(Note(
                    "A portal torn open by an item has no window to put anything into, so the " +
                    "items below are never asked for. Put the offering on the placed portal, or " +
                    "make the item itself the price.",
                    true));
                return group;
            }

            SerializedProperty items = serializedAccessRule.FindProperty("requiredItems");
            if (items == null || !items.isArray)
            {
                body.Add(Note("This rule has no offering list any more.", true));
                return group;
            }

            if (items.arraySize == 0)
            {
                body.Add(Note(
                    "Nothing is asked for yet, so the portal stays shut and no player can open it.",
                    true));
            }

            for (int i = 0; i < items.arraySize; i++)
            {
                body.Add(BuildOfferingRow(items.GetArrayElementAtIndex(i), i));
            }

            if (items.arraySize > 1)
            {
                body.Add(Note(
                    "Two rows asking for the same item become one slot holding the total.",
                    false));
            }

            Button add = DimensionsApiControls.GhostButton("Ask for something", AddOfferingRow);
            add.tooltip = "Adds another slot to the portal's window.";
            body.Add(add);
            return group;
        }

        private VisualElement BuildOfferingRow(SerializedProperty element, int index)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("dim-item-card");

            SerializedProperty itemId = element.FindPropertyRelative("itemId");
            SerializedProperty displayName = element.FindPropertyRelative("displayName");
            SerializedProperty amount = element.FindPropertyRelative("amount");
            SerializedProperty consume = element.FindPropertyRelative("consumeOnTravel");
            SerializedProperty look = element.FindPropertyRelative("slotLook");
            SerializedProperty sprite = element.FindPropertyRelative("slotSprite");
            SerializedProperty dimness = element.FindPropertyRelative("slotDimness");
            if (itemId == null || amount == null)
            {
                return card;
            }

            card.Add(BoundField(
                itemId,
                "Item",
                "What goes in this slot, by the game's name for it — one of the game's own items, " +
                "or one of yours."));
            if (displayName != null)
            {
                card.Add(BoundField(
                    displayName,
                    "Called",
                    "The name a player is shown when the portal refuses them. Leave it empty and " +
                    "the item's own name is used."));
            }

            card.Add(BoundField(amount, "How many", "How many of the item the slot must hold."));
            if (consume != null)
            {
                card.Add(BoundField(
                    consume,
                    "Used up",
                    "On, the items are taken when a player travels and have to be put in again. " +
                    "Off, they stay in the portal and keep it open."));
            }

            if (look != null)
            {
                card.Add(BuildOfferingLookField(look));
                if (look.intValue == (int)Portals.DimensionPortalOfferingLook.CustomSprite &&
                    sprite != null)
                {
                    card.Add(BoundField(
                        sprite,
                        "Your picture",
                        "The hint drawn in the empty slot."));
                }

                if (dimness != null &&
                    look.intValue != (int)Portals.DimensionPortalOfferingLook.Mystery)
                {
                    card.Add(BoundField(
                        dimness,
                        "How faint",
                        "How faint the hint is, from 0 to 1. Leave it at 0 to keep the game's own " +
                        "dimming."));
                }
            }

            int removeAt = index;
            Button remove = DimensionsApiControls.GhostButton(
                "Stop asking for this",
                () => RemoveOfferingRow(removeAt));
            card.Add(remove);
            return card;
        }

        private VisualElement BuildOfferingLookField(SerializedProperty look)
        {
            List<string> choices = new List<string>
            {
                "The item, dimmed",
                "A black silhouette",
                "A picture of your own",
            };
            int current = Mathf.Clamp(look.intValue, 0, choices.Count - 1);
            PopupField<string> picker = new PopupField<string>(choices, current);
            string path = look.propertyPath;
            picker.RegisterValueChangedCallback(evt =>
            {
                int index = choices.IndexOf(evt.newValue);
                if (index < 0 || serializedAccessRule == null)
                {
                    return;
                }

                serializedAccessRule.Update();
                SerializedProperty own = serializedAccessRule.FindProperty(path);
                if (own == null || own.intValue == index)
                {
                    return;
                }

                own.intValue = index;
                serializedAccessRule.ApplyModifiedProperties();
                NotifyRuleEdited();
                DeferredRefresh();
            });
            return DimensionsApiControls.Field(
                "The hint",
                "What the empty slot shows. The dimmed item comes with its tooltip, the " +
                "silhouette shows nothing at all, and a picture of your own can be anything.",
                picker);
        }

        private void AddOfferingRow()
        {
            if (serializedAccessRule == null)
            {
                return;
            }

            serializedAccessRule.Update();
            SerializedProperty items = serializedAccessRule.FindProperty("requiredItems");
            if (items == null || !items.isArray)
            {
                return;
            }

            int index = items.arraySize;
            items.InsertArrayElementAtIndex(index);
            SerializedProperty added = items.GetArrayElementAtIndex(index);
            // Inserting copies the row before it, or zero-fills the first one. A zero amount is
            // read as one by the rule but shows as an empty box, and a copied item id would look
            // like a second slot while merging into the first, so the row is seeded by hand.
            added.FindPropertyRelative("itemId").stringValue = string.Empty;
            added.FindPropertyRelative("displayName").stringValue = string.Empty;
            added.FindPropertyRelative("amount").intValue = 1;
            added.FindPropertyRelative("consumeOnTravel").boolValue = true;
            added.FindPropertyRelative("slotLook").intValue =
                (int)Portals.DimensionPortalOfferingLook.GhostOfTheItem;
            added.FindPropertyRelative("slotSprite").objectReferenceValue = null;
            added.FindPropertyRelative("slotDimness").floatValue = 0f;
            serializedAccessRule.ApplyModifiedProperties();
            NotifyRuleEdited();
            DeferredRefresh();
        }

        private void RemoveOfferingRow(int index)
        {
            RemoveArrayElement("requiredItems", index);
        }

        // ------------------------------------------------------------ where you arrive --

        private VisualElement BuildArrivalCard()
        {
            VisualElement group = DimensionsApiControls.Group("Where you arrive", null);
            VisualElement body = DimensionsApiControls.BodyOf(group);

            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "toLocalPosition",
                "Where a player lands",
                "The spot inside the dimension a player steps out at, counted in tiles from the " +
                "middle of the dimension."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "requireGeneratedAreaOnUse",
                "Wait for the ground",
                "On, the portal holds a player until the ground where they would land has been " +
                "built. Off, they can arrive before it exists."));
            body.Add(DimensionsApiControls.Bound(
                serializedAccessRule,
                "allowFallbackPositionOnUse",
                "Land nearby if that spot is blocked",
                "On, a player blocked from landing on that exact tile is put down on the nearest " +
                "free one. Off, the travel is refused instead."));
            return group;
        }
    }
}
