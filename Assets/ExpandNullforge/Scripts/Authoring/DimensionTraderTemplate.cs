using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Someone who sells you things, and the rules about what can be sold and when it exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Merchants, seasonal objects and the odds and ends of money were the one whole category with
    /// no framework support at all — a custom NPC could stand there but never trade. All of it is
    /// here now: what a trader stocks, what has to have happened before each item appears, what a
    /// player cannot sell back, coins, souls, and objects that only exist during an event.
    /// </para>
    /// <para>
    /// THE REQUIREMENT PER ITEM IS THE INTERESTING PART. Vanilla gates a merchant's stock behind
    /// world progress — the Core being activated, a boss statue lit, the Core boss beaten — so the
    /// shop grows as the run does. That is per ITEM, not per merchant, so one trader can open with
    /// three things and finish the game with twelve.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionTraderTemplate
    {
        [Header("Trading")]
        [Tooltip("It buys and sells. Everything below is ignored unless this is on.")]
        [SerializeField] private bool isATrader;

        [Tooltip("What it stocks, and what has to have happened before each appears.")]
        [SerializeField] private DimensionTradeGood[] stock = new DimensionTradeGood[0];

        [Header("This object itself")]
        [Tooltip("A player can never sell this back to anyone.")]
        [SerializeField] private bool cannotBeSold;

        [Tooltip("It IS money — how much a single one is worth. 0 for not money.")]
        [Min(0)]
        [SerializeField] private int coinValue;

        [Tooltip("It grants a soul when collected, by the game's own soul names. Blank for none.")]
        [SerializeField] private string givesSoul = string.Empty;

        [Header("Seasons and events")]
        [Tooltip("It belongs to a season or event, by the game's own name. Blank for all year.")]
        [SerializeField] private string belongsToSeason = string.Empty;

        [Tooltip("It vanishes from the world once that season ends, rather than lingering.")]
        [SerializeField] private bool vanishesOutOfSeason = true;

        public bool IsATrader { get { return isATrader; } }

        public DimensionTradeGood[] Stock { get { return stock ?? new DimensionTradeGood[0]; } }

        public bool CannotBeSold { get { return cannotBeSold; } }

        public int CoinValue { get { return coinValue < 0 ? 0 : coinValue; } }

        public string GivesSoul { get { return givesSoul ?? string.Empty; } }

        public string BelongsToSeason { get { return belongsToSeason ?? string.Empty; } }

        public bool VanishesOutOfSeason { get { return vanishesOutOfSeason; } }

        /// <summary>Whether it trades with an empty shop.</summary>
        public bool TradesWithNothingToSell
        {
            get { return isATrader && Stock.Length == 0; }
        }

        /// <summary>Whether stock was listed on something that does not trade.</summary>
        public bool StockWillBeIgnored
        {
            get { return !isATrader && Stock.Length > 0; }
        }

        /// <summary>Whether it is told to vanish out of a season it does not belong to.</summary>
        public bool SeasonalRuleWithoutASeason
        {
            get { return string.IsNullOrEmpty(BelongsToSeason) && !vanishesOutOfSeason; }
        }
    }

    /// <summary>One thing a trader stocks.</summary>
    [Serializable]
    public struct DimensionTradeGood
    {
        [Tooltip("What it sells. One of the game's items, or one of yours.")]
        [SerializeField] private string objectId;

        [Tooltip("How many of it are on the shelf.")]
        [Min(0)]
        [SerializeField] private int amount;

        [Tooltip("What has to have happened in the world before this appears in the shop.")]
        [SerializeField] private DimensionTradeUnlock appearsAfter;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Amount { get { return amount < 0 ? 0 : amount; } }

        public DimensionTradeUnlock AppearsAfter { get { return appearsAfter; } }
    }

    /// <summary>
    /// What has to have happened before a trader will stock something. Core Keeper's
    /// <c>MerchantItemRequirement</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionTradeUnlock
    {
        /// <summary>Available from the start.</summary>
        FromTheStart = 0,

        /// <summary>Once the Larva boss statue has been lit.</summary>
        TheLarvaStatueIsLit = 1,

        /// <summary>Once the Hive boss statue has been lit.</summary>
        TheHiveStatueIsLit = 2,

        /// <summary>Once the Core has been activated.</summary>
        TheCoreIsAwake = 3,

        /// <summary>Once the Core boss has been beaten.</summary>
        TheCoreBossIsBeaten = 4
    }
}
