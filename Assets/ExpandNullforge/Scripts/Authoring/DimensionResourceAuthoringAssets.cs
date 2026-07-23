using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // The ScriptableObject asset classes that used to live here (DimensionItemAsset,
    // DimensionRecipeAsset, DimensionWorkbenchAsset, DimensionLootTableAsset) moved to their
    // own same-named files: Unity only binds .asset files to the MonoScript matching the
    // filename, and the mismatch silently severed every dashboard-created asset's script on
    // the next domain reload. Only plain serializable templates and enums may stay here.

    public enum DimensionItemKind
    {
        BaseItem = 0,
        CraftableItem = 1,
        Ore = 2,
        Bar = 3,
        Block = 4,
        Liquid = 5,
        Fish = 6,
        PortalItem = 7,
        Custom = 100
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
        [SerializeField] private float dropChance = 1f;
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
