using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public enum DimensionPortalAccessKind
    {
        PlacedPortal = 0,
        GeneratedReturnPortal = 1,
        GeneratedEntrance = 2,
        InventoryItem = 3
    }

    public enum DimensionPortalActivationMode
    {
        VanillaCooldown = 0,
        RequiredItems = 1,
        RequiredItemsThenCooldown = 2,
        AlwaysAvailable = 3
    }

    [Serializable]
    public struct DimensionPortalRequiredItemTemplate
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private int amount;
        [SerializeField] private bool consumeOnTravel;

        public DimensionPortalRequiredItemTemplate(
            string itemId,
            string displayName,
            int amount,
            bool consumeOnTravel)
        {
            this.itemId = itemId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.amount = Mathf.Max(1, amount);
            this.consumeOnTravel = consumeOnTravel;
        }

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

        public bool ConsumeOnTravel
        {
            get { return consumeOnTravel; }
        }
    }

    /// <summary>
    /// One enemy the portal (object or item) can be made to drop from. Targets are resolved by
    /// Core Keeper object id/name (a curated vanilla list plus modder-supplied custom ids so a boss
    /// from another mod can be targeted). Weight is the loot-table roll weight; a chance percent of
    /// 100 with a dedicated single-entry table makes it a guaranteed drop.
    /// </summary>
    [Serializable]
    public struct DimensionPortalDropTarget
    {
        [SerializeField] private string targetObjectId;
        [SerializeField] private string displayName;
        [SerializeField] private bool isBoss;
        [Tooltip("Loot roll weight (higher = more likely relative to the table's other entries).")]
        [SerializeField] private int weight;
        [Tooltip("Independent chance (0-100) that the drop is rolled at all.")]
        [SerializeField] private float chancePercent;
        [SerializeField] private int minAmount;
        [SerializeField] private int maxAmount;

        public DimensionPortalDropTarget(
            string targetObjectId,
            string displayName,
            bool isBoss,
            int weight,
            float chancePercent,
            int minAmount,
            int maxAmount)
        {
            this.targetObjectId = targetObjectId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.isBoss = isBoss;
            this.weight = Mathf.Max(1, weight);
            this.chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
            this.minAmount = Mathf.Max(1, minAmount);
            this.maxAmount = Mathf.Max(this.minAmount, maxAmount);
        }

        public string TargetObjectId => targetObjectId ?? string.Empty;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? TargetObjectId : displayName;
        public bool IsBoss => isBoss;
        public int Weight => Mathf.Max(1, weight);
        public float ChancePercent => Mathf.Clamp(chancePercent, 0f, 100f);
        public int MinAmount => Mathf.Max(1, minAmount);
        public int MaxAmount => Mathf.Max(MinAmount, maxAmount);
    }

    [CreateAssetMenu(menuName = "Dimensions API/Portal Access Rule")]
    public sealed class DimensionPortalAccessRuleAsset : ScriptableObject
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string portalId;
        [SerializeField] private string presentationId;
        [SerializeField] private string displayName;
        [SerializeField] private string fromDimensionId = DimensionIds.Overworld;
        [SerializeField] private Vector2 fromLocalPosition;
        [SerializeField] private string toDimensionId;
        [SerializeField] private Vector2 toLocalPosition;
        [SerializeField] private DimensionPortalAccessKind accessKind;
        [SerializeField] private DimensionPortalActivationMode activationMode;
        [SerializeField] private float activationCooldownSeconds = 2f;
        [SerializeField] private bool requireGeneratedAreaOnUse = true;
        [SerializeField] private bool allowFallbackPositionOnUse = true;
        [SerializeField] private string promptText;
        [SerializeField] private string lockedPromptText;
        [SerializeField] private string iconId;
        [SerializeField] private string visualEffectId;
        [SerializeField] private string audioCueId;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool interactable = true;
        [SerializeField] private DimensionPortalRequiredItemTemplate[] requiredItems =
            new DimensionPortalRequiredItemTemplate[0];

        // --- Per-version settings (V1 placed portal, V2 instantaneous item portal) ---
        [Header("Availability")]
        [Tooltip("Can be crafted at a workbench (V1 object or V2 item).")]
        [SerializeField] private bool craftable = true;
        [Tooltip("Object id/name of the crafting station (e.g. WoodenWorkBench). Empty = Wooden Workbench.")]
        [SerializeField] private string craftingStationObjectId = string.Empty;
        [Tooltip("V1 only: the placed portal can be found/spawned in the vanilla world.")]
        [SerializeField] private bool generatedInWorld;
        [Tooltip("Can be dropped by mobs/bosses (writes into their loot table).")]
        [SerializeField] private bool droppable;
        [SerializeField] private DimensionPortalDropTarget[] dropTargets = new DimensionPortalDropTarget[0];

        [Header("Instantaneous item portal (V2)")]
        [Tooltip("V2 only: the custom item that, when used, spawns this portal. Auto-created; must be a mod item, never a vanilla item.")]
        [SerializeField] private string portalItemObjectId = string.Empty;
        [Tooltip("V2 only: seconds the spawned portal stays open before it closes (also the item's own use cooldown).")]
        [SerializeField] private float itemPortalDurationSeconds = 10f;

        public string RuleId
        {
            get { return ruleId ?? string.Empty; }
        }

        public bool Craftable => craftable;

        public string CraftingStationObjectId => craftingStationObjectId ?? string.Empty;

        public bool GeneratedInWorld => generatedInWorld;

        public bool Droppable => droppable;

        public DimensionPortalDropTarget[] DropTargets => dropTargets ?? new DimensionPortalDropTarget[0];

        public string PortalItemObjectId => portalItemObjectId ?? string.Empty;

        public float ItemPortalDurationSeconds => Mathf.Max(1f, itemPortalDurationSeconds);

        /// <summary>True when this rule is the instantaneous-item version.</summary>
        public bool IsItemPortal => accessKind == DimensionPortalAccessKind.InventoryItem;

        /// <summary>True when this rule is a placed/generated object the player interacts with.</summary>
        public bool IsPlacedObject =>
            accessKind == DimensionPortalAccessKind.PlacedPortal ||
            accessKind == DimensionPortalAccessKind.GeneratedReturnPortal ||
            accessKind == DimensionPortalAccessKind.GeneratedEntrance;

        /// <summary>
        /// Sets the per-version availability + item settings. Kept separate from
        /// <see cref="Configure"/> (which handles the link/presentation) so the authoring UI can
        /// update version settings without restating the whole link.
        /// </summary>
        public void ConfigureVersionSettings(
            bool newCraftable,
            string newCraftingStationObjectId,
            bool newGeneratedInWorld,
            bool newDroppable,
            IReadOnlyList<DimensionPortalDropTarget> newDropTargets,
            string newPortalItemObjectId,
            float newItemPortalDurationSeconds)
        {
            craftable = newCraftable;
            craftingStationObjectId = newCraftingStationObjectId ?? string.Empty;
            generatedInWorld = newGeneratedInWorld;
            droppable = newDroppable;
            dropTargets = CopyDropTargets(newDropTargets);
            portalItemObjectId = newPortalItemObjectId ?? string.Empty;
            itemPortalDurationSeconds = Mathf.Max(1f, newItemPortalDurationSeconds);
        }

        /// <summary>Enables/disables this version. At least one of V1/V2 must stay enabled (caller-enforced).</summary>
        public void SetEnabled(bool value)
        {
            enabled = value;
        }

        public string PortalId
        {
            get { return portalId ?? string.Empty; }
        }

        public string PresentationId
        {
            get { return presentationId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string FromDimensionId
        {
            get { return fromDimensionId ?? string.Empty; }
        }

        public string ToDimensionId
        {
            get { return toDimensionId ?? string.Empty; }
        }

        public Vector2 FromLocalPosition
        {
            get { return fromLocalPosition; }
        }

        public Vector2 ToLocalPosition
        {
            get { return toLocalPosition; }
        }

        public DimensionPortalAccessKind AccessKind
        {
            get { return accessKind; }
        }

        public DimensionPortalActivationMode ActivationMode
        {
            get { return activationMode; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public bool Interactable
        {
            get { return interactable; }
        }

        public bool UsesRequiredItems
        {
            get
            {
                return activationMode == DimensionPortalActivationMode.RequiredItems ||
                       activationMode == DimensionPortalActivationMode.RequiredItemsThenCooldown;
            }
        }

        public float EffectiveCooldownSeconds
        {
            get
            {
                if (activationMode == DimensionPortalActivationMode.AlwaysAvailable ||
                    activationMode == DimensionPortalActivationMode.RequiredItems)
                {
                    return 0f;
                }

                return Mathf.Max(0f, activationCooldownSeconds);
            }
        }

        public DimensionPortalRequiredItemTemplate[] RequiredItems
        {
            get { return requiredItems ?? new DimensionPortalRequiredItemTemplate[0]; }
        }

        public void Configure(
            string newRuleId,
            string newPortalId,
            string newPresentationId,
            string newDisplayName,
            string newFromDimensionId,
            Vector2 newFromLocalPosition,
            string newToDimensionId,
            Vector2 newToLocalPosition,
            DimensionPortalAccessKind newAccessKind,
            DimensionPortalActivationMode newActivationMode,
            float newActivationCooldownSeconds,
            bool newRequireGeneratedAreaOnUse,
            bool newAllowFallbackPositionOnUse,
            string newPromptText,
            string newLockedPromptText,
            string newIconId,
            string newVisualEffectId,
            string newAudioCueId,
            int newPriority,
            bool newEnabled,
            IReadOnlyList<DimensionPortalRequiredItemTemplate> newRequiredItems,
            bool newInteractable = true)
        {
            ruleId = newRuleId ?? string.Empty;
            portalId = newPortalId ?? string.Empty;
            presentationId = newPresentationId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            fromDimensionId = newFromDimensionId ?? string.Empty;
            fromLocalPosition = newFromLocalPosition;
            toDimensionId = newToDimensionId ?? string.Empty;
            toLocalPosition = newToLocalPosition;
            accessKind = newAccessKind;
            activationMode = newActivationMode;
            activationCooldownSeconds = Mathf.Max(0f, newActivationCooldownSeconds);
            requireGeneratedAreaOnUse = newRequireGeneratedAreaOnUse;
            allowFallbackPositionOnUse = newAllowFallbackPositionOnUse;
            promptText = newPromptText ?? string.Empty;
            lockedPromptText = newLockedPromptText ?? string.Empty;
            iconId = newIconId ?? string.Empty;
            visualEffectId = newVisualEffectId ?? string.Empty;
            audioCueId = newAudioCueId ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
            interactable = newInteractable;
            requiredItems = CopyItems(newRequiredItems);
        }

        public DimensionPortalDefinition ToPortalDefinition()
        {
            return new DimensionPortalDefinition(
                PortalId,
                DisplayName,
                FromDimensionId,
                new float2(fromLocalPosition.x, fromLocalPosition.y),
                ToDimensionId,
                new float2(toLocalPosition.x, toLocalPosition.y),
                ResolveState());
        }

        public DimensionPortalPresentationDefinition ToPortalPresentationDefinition()
        {
            return new DimensionPortalPresentationDefinition(
                PresentationId,
                PortalId,
                DisplayName,
                promptText,
                lockedPromptText,
                iconId,
                visualEffectId,
                audioCueId,
                EffectiveCooldownSeconds,
                priority,
                enabled,
                requireGeneratedAreaOnUse,
                allowFallbackPositionOnUse,
                interactable);
        }

        public void AppendTravelRequirements(List<DimensionTravelRequirementDefinition> requirements)
        {
            if (requirements == null || !enabled || !UsesRequiredItems)
            {
                return;
            }

            DimensionPortalRequiredItemTemplate[] items = RequiredItems;
            for (int i = 0; i < items.Length; i++)
            {
                DimensionPortalRequiredItemTemplate item = items[i];
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                string itemName = string.IsNullOrEmpty(item.DisplayName)
                    ? item.ItemId
                    : item.DisplayName;
                requirements.Add(new DimensionTravelRequirementDefinition(
                    RuleId + ".requires." + i.ToString(),
                    itemName,
                    PortalId,
                    ToDimensionId,
                    DimensionTravelRequirementKind.Item,
                    item.ItemId,
                    item.Amount,
                    item.ConsumeOnTravel,
                    "Missing " + itemName + ".",
                    priority + i,
                    true));
            }
        }

        private DimensionPortalState ResolveState()
        {
            return enabled ? DimensionPortalState.Available : DimensionPortalState.Disabled;
        }

        private static DimensionPortalRequiredItemTemplate[] CopyItems(
            IReadOnlyList<DimensionPortalRequiredItemTemplate> source)
        {
            if (source == null || source.Count == 0)
            {
                return new DimensionPortalRequiredItemTemplate[0];
            }

            DimensionPortalRequiredItemTemplate[] copy =
                new DimensionPortalRequiredItemTemplate[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static DimensionPortalDropTarget[] CopyDropTargets(
            IReadOnlyList<DimensionPortalDropTarget> source)
        {
            if (source == null || source.Count == 0)
            {
                return new DimensionPortalDropTarget[0];
            }

            DimensionPortalDropTarget[] copy = new DimensionPortalDropTarget[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
