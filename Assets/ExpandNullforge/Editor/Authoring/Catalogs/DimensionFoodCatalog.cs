using System.Collections.Generic;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Every ingredient and dish the game already ships, so a combiner can answer for pairs that
    /// have nothing of the mod's own in them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MEASURED, NOT REMEMBERED. These numbers were read straight off the game's own 79 ingredient
    /// prefabs — the object id, the dish its <c>turnsIntoFood</c> names, its kind and its rarity —
    /// so a preview built on them says what the pot says. Nothing here is a guess, and nothing here
    /// needs the game installed to be true.
    /// </para>
    /// <para>
    /// Parallel arrays rather than a list of a small class, matching the rule the rest of the
    /// framework follows: a serialized list of a custom class does not survive into the game. This
    /// table is editor-only and never serialized, but keeping one shape everywhere means nobody has
    /// to work out which of the two rules applies where.
    /// </para>
    /// <para>
    /// The dish families are not listed at all, because they do not need to be: an object id turned
    /// back into its enum name IS the dish's name, and the game's fifteen families sit at 9500 to
    /// 9514 with their better versions at +50 and +75. A table of them would be a second copy of
    /// something already true.
    /// </para>
    /// </remarks>
    internal static class DimensionFoodCatalog
    {
        /// <summary>The object id of each ingredient the game ships.</summary>
        private static readonly int[] IngredientIds =
        {
            1645, 1646, 5500, 5501, 5502, 5503, 5607, 5773, 7900, 7901,
            7902, 8003, 8006, 8009, 8012, 8015, 8024, 8027, 8030, 8033,
            8036, 8039, 8100, 8101, 8102, 8103, 8104, 8105, 8106, 8107,
            8108, 8109, 8110, 9618, 9622, 9700, 9701, 9702, 9703, 9704,
            9705, 9706, 9707, 9708, 9709, 9710, 9711, 9712, 9713, 9714,
            9715, 9716, 9717, 9718, 9719, 9720, 9721, 9722, 9723, 9724,
            9725, 9726, 9727, 9728, 9729, 9730, 9731, 9732, 9733, 9734,
            9735, 9736, 9737, 9738, 9739, 9740, 9741, 9742, 9743,
        };

        /// <summary>The dish each of those ingredients makes when it leads.</summary>
        private static readonly int[] MakesDish =
        {
            9504, 9504, 9500, 9502, 9502, 9501, 9501, 9500, 9505, 9504,
            9514, 9501, 9502, 9503, 9506, 9505, 9510, 9511, 9511, 9500,
            9513, 9512, 9551, 9552, 9553, 9556, 9555, 9560, 9561, 9561,
            9550, 9563, 9562, 9504, 9504, 9509, 9509, 9508, 9507, 9509,
            9508, 9508, 9507, 9509, 9509, 9508, 9507, 9509, 9508, 9507,
            9509, 9509, 9508, 9507, 9509, 9507, 9507, 9508, 9508, 9509,
            9509, 9508, 9508, 9507, 9508, 9508, 9509, 9509, 9557, 9507,
            9509, 9508, 9507, 9509, 9507, 9509, 9501, 9501, 9508,
        };

        /// <summary>Plant, fish or meat, as the cook book's filter reads it.</summary>
        private static readonly int[] Kinds =
        {
            3, 3, 1, 1, 1, 3, 3, 1, 3, 3,
            3, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 3, 3, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2,
        };

        /// <summary>How many ingredients the game ships.</summary>
        internal static int Count
        {
            get { return IngredientIds.Length; }
        }

        internal static int IngredientIdAt(int index)
        {
            return IngredientIds[index];
        }

        internal static int MakesDishAt(int index)
        {
            return MakesDish[index];
        }

        internal static int KindAt(int index)
        {
            return Kinds[index];
        }

        /// <summary>The dish a game ingredient makes when it leads, or 0 when it is not one.</summary>
        internal static int DishFor(int ingredientObjectId)
        {
            for (int i = 0; i < IngredientIds.Length; i++)
            {
                if (IngredientIds[i] == ingredientObjectId)
                {
                    return MakesDish[i];
                }
            }

            return 0;
        }

        /// <summary>Whether an object id is one of the game's own ingredients.</summary>
        internal static bool IsAGameIngredient(int objectId)
        {
            return DishFor(objectId) != 0;
        }

        /// <summary>
        /// What a player calls one of the game's objects.
        /// </summary>
        /// <remarks>
        /// The enum name with its words split apart. Not a translation — the framework has no way
        /// to read the game's own localization table from the editor — but it is the name a creator
        /// will recognise, and it is never shown to a player.
        /// </remarks>
        internal static string ReadableName(int objectId)
        {
            string raw = ((ObjectID)objectId).ToString();
            if (string.IsNullOrEmpty(raw))
            {
                return objectId.ToString();
            }

            System.Text.StringBuilder spaced = new System.Text.StringBuilder(raw.Length + 8);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                {
                    spaced.Append(' ');
                }

                spaced.Append(raw[i]);
            }

            return spaced.ToString();
        }

        /// <summary>Every game ingredient, ordered by the name a creator would search for.</summary>
        internal static List<int> AllIngredientIdsByName()
        {
            List<int> ids = new List<int>(IngredientIds);
            ids.Sort(delegate(int a, int b)
            {
                return string.Compare(
                    ReadableName(a), ReadableName(b), System.StringComparison.OrdinalIgnoreCase);
            });
            return ids;
        }
    }
}
