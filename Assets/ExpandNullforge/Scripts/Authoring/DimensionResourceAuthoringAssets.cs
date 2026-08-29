using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // The ScriptableObject asset classes that used to live here (DimensionItemAsset,
    // DimensionRecipeAsset, DimensionWorkbenchAsset, DimensionLootTableAsset) moved to their
    // own same-named files: Unity only binds .asset files to the MonoScript matching the
    // filename, and the mismatch silently severed every dashboard-created asset's script on
    // the next domain reload. Only plain serializable templates and enums may stay here.

    /// <summary>
    /// Which of the framework's three item shapes an item is, as the generator branches on it.
    /// </summary>
    /// <remarks>
    /// Not a creator's choice — the button that makes the item sets it. Only these three values
    /// have ever changed what is generated: an ordinary item, a tileset's block, and a portal item
    /// (which is forced to Rare so it reads as the special tool it is). The list used to also offer
    /// CraftableItem, Ore, Bar, Liquid, Fish and Custom; nothing anywhere read them, and each named
    /// something a working surface already decides — a tileset says which item is its ore, cooking
    /// says which item can be caught. The numbers are left where they were so an item saved with one
    /// of the removed values reads as an ordinary item rather than as a block.
    /// </remarks>
    public enum DimensionItemKind
    {
        BaseItem = 0,
        Block = 4,
        PortalItem = 7
    }

    [Serializable]
    public sealed class DimensionRecipeIngredientTemplate
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private int amount = 1;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public int Amount
        {
            get { return Mathf.Max(1, amount); }
        }
    }

    [Serializable]
    public sealed class DimensionLootEntryTemplate
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private int minAmount = 1;
        [SerializeField] private int maxAmount = 1;
        [Tooltip("How often this drops, from 0 to 1. 1 means it always drops; 0.05 means one kill " +
                 "in twenty. Each row takes its own chance, so several rows can all drop at once.")]
        [Range(0f, 1f)]
        [SerializeField] private float dropChance = 1f;

        // NO LONGER READ, AND NO LONGER DRAWN. The chance above is what decides the odds now, and
        // the table is built so that it is true — which leaves a share nothing to divide. It stays
        // serialized so that a table authored before this keeps loading rather than erroring, and
        // it is hidden rather than deleted so nobody types a number into a control that reaches
        // nothing. A row that still carries one is said out loud at generate time.
        [HideInInspector]
        [SerializeField] private int weight = 1;

        [SerializeField] private bool enabled = true;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public int MinAmount
        {
            get { return Mathf.Max(1, minAmount); }
        }

        public int MaxAmount
        {
            get { return Mathf.Max(MinAmount, maxAmount); }
        }

        public float DropChance
        {
            get { return Mathf.Clamp01(dropChance); }
        }

        public int Weight
        {
            get { return Mathf.Max(1, weight); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }
    }
}
