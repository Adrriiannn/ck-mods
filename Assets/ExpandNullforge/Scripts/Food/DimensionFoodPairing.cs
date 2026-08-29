namespace ExpandNullforge.Food
{
    /// <summary>
    /// Which of two ingredients leads a dish, and how the pair is stored on the dish that comes out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THERE IS NO RECIPE TABLE IN CORE KEEPER'S COOKING. Every ordered pair of ingredients is a
    /// recipe: the pot picks one of the two as the primary, the dish that comes out is whatever that
    /// primary's <c>CookingIngredientCD.turnsIntoFood</c> names, and BOTH ingredients are packed into
    /// the one <c>variation</c> integer on the result. That is why a mod can add an ingredient
    /// without patching anything, and why this arithmetic has to be reproduced exactly rather than
    /// approximated — the combiner preview would otherwise promise a dish the pot does not make.
    /// </para>
    /// <para>
    /// This is a rewrite of <c>CookedFoodCD</c>'s statics rather than a call into them, because the
    /// same answer is needed in the editor (where a preview is drawn), in the running game (where a
    /// custom dish target is hydrated onto a prefab) and in tests (which compare this against the
    /// game's own implementation, so a divergence fails the build rather than shipping a lying
    /// preview).
    /// </para>
    /// <para>
    /// THE ONE HARD WALL is <see cref="MaximumPackableObjectId"/>. The pack is
    /// <c>(primary &lt;&lt; 16) | secondary</c> and the unpack masks with 65535, so an ingredient
    /// whose object id is larger than that is silently truncated: the dish comes out carrying the
    /// wrong pair, with the wrong colours and the wrong buffs, and nothing anywhere says so. Mod
    /// object ids start at 32768, so this only bites a load order carrying more than about 32,700
    /// modded objects — rare, silent, and worth failing loudly over.
    /// </para>
    /// </remarks>
    public static class DimensionFoodPairing
    {
        /// <summary>The seed Core Keeper mixes into both sides of the coin flip.</summary>
        private const int Seed = 87931;

        /// <summary>How far the primary ingredient is shifted up when the pair is packed.</summary>
        private const int EncodingShift = 16;

        /// <summary>The largest object id that survives being packed into a dish's variation.</summary>
        /// <remarks>
        /// Anything above this loses its high bits to the 16-bit mask on the way back out, so the
        /// dish would name a different ingredient than the one that went in.
        /// </remarks>
        public const int MaximumPackableObjectId = 65535;

        /// <summary>The lowest object id in the game's golden-plant band.</summary>
        public const int GoldenPlantFirstObjectId = 8100;

        /// <summary>The highest object id in the game's golden-plant band.</summary>
        public const int GoldenPlantLastObjectId = 8149;

        /// <summary>The one fish outside the golden band that also always leads.</summary>
        public const int StarlightNautilusObjectId = 9733;

        /// <summary>
        /// Whether an ingredient always takes the lead, whatever it is paired with.
        /// </summary>
        /// <remarks>
        /// A CUSTOM INGREDIENT CAN NEVER ANSWER TRUE HERE, and no amount of authoring changes that.
        /// The game asks this question from inside Burst-compiled inventory jobs, and the answer is
        /// two hardcoded id ranges — a band of vanilla ids no mod object can land in, plus one named
        /// fish. A mod's own "golden" ingredient still works: it can point straight at a rare dish
        /// and it still upgrades one tier at collection. It simply wins the lead half the time
        /// instead of always, which the combiner window says out loud rather than hiding.
        /// </remarks>
        public static bool AlwaysLeads(int objectId)
        {
            return (objectId >= GoldenPlantFirstObjectId && objectId <= GoldenPlantLastObjectId)
                || objectId == StarlightNautilusObjectId;
        }

        /// <summary>Whether an object id survives being packed into a dish's variation.</summary>
        public static bool FitsInAVariation(int objectId)
        {
            return objectId >= 0 && objectId <= MaximumPackableObjectId;
        }

        /// <summary>The ingredient whose dish the pot will produce.</summary>
        public static int Primary(int first, int second)
        {
            bool firstLeads = AlwaysLeads(first);
            bool secondLeads = AlwaysLeads(second);
            if (firstLeads && !secondLeads)
            {
                return first;
            }

            if (!firstLeads && secondLeads)
            {
                return second;
            }

            return FirstWinsTheFlip(first, second) ? first : second;
        }

        /// <summary>The ingredient that only lends its colours and its buffs.</summary>
        public static int Secondary(int first, int second)
        {
            bool firstLeads = AlwaysLeads(first);
            bool secondLeads = AlwaysLeads(second);
            if (firstLeads && !secondLeads)
            {
                return second;
            }

            if (!firstLeads && secondLeads)
            {
                return first;
            }

            return FirstWinsTheFlip(first, second) ? second : first;
        }

        /// <summary>The number the finished dish stores so it remembers what went into it.</summary>
        public static int Variation(int first, int second)
        {
            return (Primary(first, second) << EncodingShift) | Secondary(first, second);
        }

        /// <summary>The leading ingredient read back out of a finished dish.</summary>
        public static int PrimaryFromVariation(int variation)
        {
            return Primary(HighHalf(variation), LowHalf(variation));
        }

        /// <summary>The second ingredient read back out of a finished dish.</summary>
        public static int SecondaryFromVariation(int variation)
        {
            return Secondary(HighHalf(variation), LowHalf(variation));
        }

        /// <summary>
        /// The two halves of a packed variation, before the lead is worked out again.
        /// </summary>
        /// <remarks>
        /// The unsigned shift is not a detail. A dish whose primary id has bit 15 set packs into a
        /// negative integer, and an arithmetic shift would drag the sign bit down through the whole
        /// high half and hand back a nonsense id.
        /// </remarks>
        public static int HighHalf(int variation)
        {
            return (int)((uint)variation >> EncodingShift);
        }

        /// <summary>The low half of a packed variation, exactly as the game masks it.</summary>
        public static int LowHalf(int variation)
        {
            return variation & MaximumPackableObjectId;
        }

        /// <summary>
        /// The deterministic coin flip between two ingredients that neither always lead.
        /// </summary>
        /// <remarks>
        /// Two independent streams are seeded from the same pair in opposite orders and their first
        /// draws compared, so the same two ingredients always resolve the same way in every world,
        /// on every machine, forever. Reproduced from the game rather than invented: a preview that
        /// flipped its own coin would be right half the time and would be believed all of the time.
        /// </remarks>
        private static bool FirstWinsTheFlip(int first, int second)
        {
            Unity.Mathematics.Random forFirst =
                Unity.Mathematics.Random.CreateFromIndex((uint)((first * 2) + second + Seed));
            Unity.Mathematics.Random forSecond =
                Unity.Mathematics.Random.CreateFromIndex((uint)((second * 2) + first + Seed));
            return forFirst.NextFloat() > forSecond.NextFloat();
        }
    }
}
