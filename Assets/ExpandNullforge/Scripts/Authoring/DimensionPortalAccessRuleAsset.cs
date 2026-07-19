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

        public string RuleId
        {
            get { return ruleId ?? string.Empty; }
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
    }
}
