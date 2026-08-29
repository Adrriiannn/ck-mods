using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The card on a creature's page that offers it one of the game's own moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE DOOR THAT WAS MISSING. The framework harvested every move Core Keeper authored, wrote
    /// them into a table, wrote a way to pour one into a creature, and wrote a picker — and then
    /// the studio was rebuilt around the stage pages, which do not draw the old panel the picker
    /// lived in. Everything existed and nothing was reachable. This card is the reachable part: it
    /// appears on any page whose selected thing has fight settings, which is what makes it a
    /// creature.
    /// </para>
    /// <para>
    /// ONE BUTTON PER SORT OF MOVE, not one long list, because a creator arrives wanting a swing or
    /// wanting a wander and should not have to sift the other four hundred to find it. Each button
    /// says how many the game has of that sort, and opens the list already narrowed to it. The
    /// first button opens the lot, for the creator who knows the creature they want rather than the
    /// sort of move.
    /// </para>
    /// <para>
    /// AND IT SAYS THE OTHER DOOR IS THERE. Taking one of these is optional in both directions:
    /// nothing here has to be pressed, and the fight fields under this card are the whole of the
    /// second door. A creator who reads only this card should still know that.
    /// </para>
    /// </remarks>
    internal static class DimensionBorrowedAttackCard
    {
        /// <summary>
        /// Builds the card, or returns null for a thing that cannot fight.
        /// </summary>
        /// <param name="asset">The selected mob or boss.</param>
        /// <param name="onApplied">Run after a move lands, so the page redraws with the numbers in.</param>
        internal static VisualElement Build(Object asset, System.Action onApplied)
        {
            if (asset == null || !DimensionBorrowedAttackPickerWindow.CanTakeAMove(asset))
            {
                return null;
            }

            VisualElement card = DimensionsApiControls.Group(
                "Take a move from the game",
                "Every attack, chase, wander and set of fight sounds Core Keeper authored, ready to " +
                "be taken whole.");
            VisualElement body = DimensionsApiControls.BodyOf(card);

            Label blurb = new Label(
                "Pick one and it arrives with the game's own numbers, measured off the creature it " +
                "belongs to. Every one of them lands in the fields below, where you can change as " +
                "much or as little as you like — changing nothing is a finished answer. Or take " +
                "none of them and fill those fields in yourself.");
            blurb.AddToClassList("dim-note");
            body.Add(blurb);

            // The chip row is the page's own "a line of things that wraps"; the rule that styles
            // chips inside it only matches chips, so buttons sit in it untouched.
            VisualElement row = DimensionsApiControls.ChipRow();

            row.Add(DimensionsApiControls.PrimaryButton(
                "Browse all " + DimensionBorrowedAttacks.All.Length,
                () => DimensionBorrowedAttackPickerWindow.Open(asset, null, onApplied)));

            string[] kinds = DimensionBorrowedAttacks.Kinds;
            for (int i = 0; i < kinds.Length; i++)
            {
                string kind = kinds[i];
                int count = DimensionBorrowedAttacks.OfKind(kind).Length;
                if (count == 0)
                {
                    continue;
                }

                Button button = DimensionsApiControls.GhostButton(
                    NameForCreators(kind) + " (" + count + ")",
                    () => DimensionBorrowedAttackPickerWindow.Open(asset, kind, onApplied));
                button.tooltip = DimensionBorrowedAttackCatalog.WhatAKindIs(kind);
                row.Add(button);
            }

            body.Add(row);
            return card;
        }

        /// <summary>What a sort of move is called on its button.</summary>
        /// <remarks>
        /// The sorts come out of the generated table, so a new one appears here on its own. One
        /// with no phrase written for it is offered under its own name, which reads plainly enough
        /// to ship with.
        /// </remarks>
        private static string NameForCreators(string kind)
        {
            switch (kind)
            {
                case "Melee":
                    return "Close-up swings";
                case "Ranged":
                    return "Ranged shots";
                case "Chase":
                    return "Ways of chasing";
                case "Wander":
                    return "Ways of wandering";
                case "Sounds":
                    return "Fight sounds";
                case "Charge":
                    return "Charges";
                case "Jump":
                    return "Leaps";
                case "Explode":
                    return "Explosions";
                case "Ray":
                    return "Sweeping rays";
                default:
                    return kind;
            }
        }
    }
}
