using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
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

    [CreateAssetMenu(menuName = "Dimensions API/Item")]
    public sealed class DimensionItemAsset : ScriptableObject
    {
        [SerializeField] private string itemId = "mod:item";
        [SerializeField] private string displayName = "Item";
        [SerializeField] private DimensionItemKind kind = DimensionItemKind.BaseItem;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private int maxStack = 999;
        [SerializeField] private string rarityId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionItemKind Kind
        {
            get { return kind; }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public int MaxStack
        {
            get { return Mathf.Max(1, maxStack); }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "icon",
                DisplayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                string.Empty,
                0,
                Enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                10,
                Enabled,
                Notes);
        }
    }

    [CreateAssetMenu(menuName = "Dimensions API/Recipe")]
    public sealed class DimensionRecipeAsset : ScriptableObject
    {
        [SerializeField] private string recipeId = "mod:recipe";
        [SerializeField] private string displayName = "Recipe";
        [SerializeField] private string outputItemId = string.Empty;
        [SerializeField] private int outputAmount = 1;
        [SerializeField] private string craftingStationId = string.Empty;
        [SerializeField] private float craftTimeSeconds;
        [SerializeField] private bool enabled = true;
        [SerializeField] private DimensionRecipeIngredientTemplate[] ingredients =
            new DimensionRecipeIngredientTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string RecipeId
        {
            get { return recipeId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string OutputItemId
        {
            get { return outputItemId ?? string.Empty; }
        }

        public int OutputAmount
        {
            get { return Mathf.Max(1, outputAmount); }
        }

        public string CraftingStationId
        {
            get { return craftingStationId ?? string.Empty; }
        }

        public float CraftTimeSeconds
        {
            get { return Mathf.Max(0f, craftTimeSeconds); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionRecipeIngredientTemplate[] Ingredients
        {
            get { return ingredients ?? new DimensionRecipeIngredientTemplate[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

    }

    [CreateAssetMenu(menuName = "Dimensions API/Workbench")]
    public sealed class DimensionWorkbenchAsset : ScriptableObject
    {
        [SerializeField] private string workbenchId = "mod:workbench";
        [SerializeField] private string displayName = "Workbench";
        [SerializeField] private string objectId = string.Empty;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private DimensionRecipeAsset[] recipes =
            new DimensionRecipeAsset[0];
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string WorkbenchId
        {
            get { return workbenchId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public DimensionRecipeAsset[] Recipes
        {
            get { return recipes ?? new DimensionRecipeAsset[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                WorkbenchId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                0,
                Enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                WorkbenchId,
                "icon",
                DisplayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                string.Empty,
                10,
                Enabled,
                Notes);
        }
    }

    [CreateAssetMenu(menuName = "Dimensions API/Loot Table")]
    public sealed class DimensionLootTableAsset : ScriptableObject
    {
        [SerializeField] private string lootTableId = "mod:loot";
        [SerializeField] private string displayName = "Loot Table";
        [SerializeField] private bool allowEmptyRoll;
        [SerializeField] private bool enabled = true;
        [SerializeField] private DimensionLootEntryTemplate[] entries =
            new DimensionLootEntryTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string LootTableId
        {
            get { return lootTableId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool AllowEmptyRoll
        {
            get { return allowEmptyRoll; }
        }

        public DimensionLootEntryTemplate[] Entries
        {
            get { return entries ?? new DimensionLootEntryTemplate[0]; }
        }

        public int EnabledEntryCount
        {
            get
            {
                int count = 0;
                DimensionLootEntryTemplate[] values = Entries;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != null && values[i].Enabled)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }
}
