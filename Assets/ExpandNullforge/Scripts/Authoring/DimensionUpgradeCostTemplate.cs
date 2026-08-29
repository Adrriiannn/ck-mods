using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What upgrading a piece of gear costs at each level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE LEVEL AT A TIME, AND NAMING A LEVEL REPLACES IT. Every row here says "at this level, this
    /// many of this thing". All the rows sharing a level become that level's whole price, so a level
    /// nobody names keeps exactly the price the game has. There is no way to add one ingredient to a
    /// level and leave the rest alone, because the game stores a level's price as a single list and
    /// a half-edit would be a guess about the other half.
    /// </para>
    /// <para>
    /// THE NAMES ARE ANSWERED WHILE THE GAME LOADS, not while you are typing. That is deliberate:
    /// an item this mod adds does not have a number until the mod is loaded, so a price can be paid
    /// in this mod's own bars and coins as easily as in Core Keeper's. A name nothing answers is
    /// reported in the log and that row is dropped, leaving the level at the game's own price rather
    /// than at a free one.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionUpgradeCostTemplate
    {
        [Header("Does this change what upgrading costs?")]
        [Tooltip("Turn on to set your own prices. Off leaves every level at the game's own price.")]
        [SerializeField] private bool changesWhatUpgradingCosts;

        [Tooltip("Each row is one ingredient at one level. Rows sharing a level become that level's whole price.")]
        [SerializeField] private DimensionUpgradeCost[] costs = new DimensionUpgradeCost[0];

        public bool ChangesWhatUpgradingCosts { get { return changesWhatUpgradingCosts; } }

        public DimensionUpgradeCost[] Costs
        {
            get { return costs ?? new DimensionUpgradeCost[0]; }
        }

        /// <summary>Switched on with no rows, so every level would keep the game's own price.</summary>
        public bool NothingWasPriced
        {
            get { return changesWhatUpgradingCosts && Costs.Length == 0; }
        }
    }

    /// <summary>One ingredient in one upgrade level's price.</summary>
    [Serializable]
    public struct DimensionUpgradeCost
    {
        [Tooltip("Which upgrade level this price is for. 0 is the first upgrade.")]
        [Min(0)]
        [SerializeField] private int level;

        [Tooltip("What it costs, named as the game names it, or as one of this mod's own items is named.")]
        [SerializeField] private string itemId;

        [Tooltip("How many of it.")]
        [Min(0)]
        [SerializeField] private int amount;

        public int Level { get { return level < 0 ? 0 : level; } }

        public string ItemId { get { return itemId ?? string.Empty; } }

        public int Amount { get { return amount < 0 ? 0 : amount; } }
    }
}
