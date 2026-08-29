using System.Collections.Generic;

namespace ExpandNullforge.Plants
{
    /// <summary>One better version of a crop: which variations carry it, and how often it comes up.</summary>
    /// <remarks>
    /// A plain struct with no Unity types so the two decisions that matter — which version a roll
    /// lands on, and which plant a version's seed must become — can be tested without a world.
    /// </remarks>
    public struct DimensionCropTier
    {
        /// <summary>The variation the seed sits on once this version has been rolled.</summary>
        public int SeedVariation;

        /// <summary>The variation the plant sits on when it sprouts from that seed.</summary>
        public int PlantVariation;

        /// <summary>Out of every hundred plantings, how many come up as this version.</summary>
        public float ChancePercent;

        /// <summary>
        /// Whether the game's own golden roll decides this version instead of the framework's.
        /// </summary>
        /// <remarks>
        /// Core Keeper rolls once when a seed is planted, at three percent plus the player's
        /// gardening bonuses, and places the seed on <c>rareSeedVariation</c> if it wins. A version
        /// that opts into that roll must be left alone here or it would be rolled for twice.
        /// </remarks>
        public bool UsesTheGamesGoldenRoll;
    }

    /// <summary>
    /// Which version of a crop a planting comes up as, and which plant that version grows into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THESE ARE THE TWO SEAMS CORE KEEPER DOES NOT LEAVE OPEN. Everything else about a better crop
    /// version is authored data the game already reads — the variation prefab carries its own
    /// produce, its own give-backs, its own look. But the roll is hardcoded at one chance for one
    /// variation inside the item-use slots, and the seed-to-plant mapping inside
    /// <c>PlantsGrowJob</c> promotes exactly one authored pair and collapses everything else to
    /// variation 0. That job is Burst-compiled, so it cannot be patched. Both live here instead.
    /// </para>
    /// <para>
    /// THE ROLL IS ONE DRAW ACROSS CONSECUTIVE BANDS, not a draw per version. Rolling each version
    /// separately would let two of them win at once and would make the printed chances lie about
    /// each other; laying them end to end on a single number keeps "five in a hundred" meaning five
    /// in a hundred no matter what else is in the list. Versions past a hundred percent are
    /// unreachable, which the generator says out loud rather than leaving to be discovered.
    /// </para>
    /// </remarks>
    public static class DimensionCropTierRoll
    {
        /// <summary>What <see cref="Choose"/> answers when the planting is an ordinary one.</summary>
        public const int NoTier = -1;

        /// <summary>
        /// Which version a planting comes up as, given one number between 0 and 1.
        /// </summary>
        /// <remarks>
        /// The draw is passed in rather than taken here so the same list always answers the same way
        /// for the same number — which is what makes this testable, and what lets the caller use the
        /// seed entity's own saved random state.
        /// </remarks>
        public static int Choose(IList<DimensionCropTier> tiers, float roll)
        {
            if (tiers == null)
            {
                return NoTier;
            }

            float cumulative = 0f;
            for (int i = 0; i < tiers.Count; i++)
            {
                DimensionCropTier tier = tiers[i];
                if (tier.UsesTheGamesGoldenRoll || tier.ChancePercent <= 0f || tier.SeedVariation <= 0)
                {
                    continue;
                }

                cumulative += tier.ChancePercent / 100f;
                if (roll < cumulative)
                {
                    return i;
                }
            }

            return NoTier;
        }

        /// <summary>
        /// Which plant variation a seed sitting on <paramref name="seedVariation"/> has to become.
        /// </summary>
        /// <remarks>
        /// Zero means "the game already handles this one": either the seed is an ordinary one, or it
        /// is on the version that opted into the game's golden roll, which <c>PlantsGrowJob</c>
        /// promotes by itself.
        /// </remarks>
        public static int PlantVariationForSeedVariation(
            IList<DimensionCropTier> tiers,
            int seedVariation)
        {
            if (tiers == null || seedVariation <= 0)
            {
                return 0;
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                DimensionCropTier tier = tiers[i];
                if (tier.SeedVariation != seedVariation)
                {
                    continue;
                }

                return tier.UsesTheGamesGoldenRoll ? 0 : tier.PlantVariation;
            }

            return 0;
        }

        /// <summary>
        /// How much of the hundred the framework's own versions claim between them.
        /// </summary>
        /// <remarks>
        /// The generator uses this to warn about a list whose later versions can never come up. It
        /// ignores a version that opted into the game's golden roll, because that roll happens
        /// before this one and takes no share of this number.
        /// </remarks>
        public static float TotalChancePercent(IList<DimensionCropTier> tiers)
        {
            if (tiers == null)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (!tiers[i].UsesTheGamesGoldenRoll && tiers[i].ChancePercent > 0f)
                {
                    total += tiers[i].ChancePercent;
                }
            }

            return total;
        }
    }
}
