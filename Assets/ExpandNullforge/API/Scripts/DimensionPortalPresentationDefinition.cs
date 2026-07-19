namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalPresentationDefinition
    {
        public readonly string PresentationId;
        public readonly string PortalId;
        public readonly string DisplayName;
        public readonly string PromptText;
        public readonly string LockedPromptText;
        public readonly string IconId;
        public readonly string VisualEffectId;
        public readonly string AudioCueId;
        public readonly float CooldownSeconds;
        public readonly int Priority;
        public readonly bool Enabled;
        public readonly bool RequireGeneratedAreaOnUse;
        public readonly bool AllowFallbackPositionOnUse;
        public readonly bool Interactable;

        public DimensionPortalPresentationDefinition(
            string presentationId,
            string portalId,
            string displayName,
            string promptText,
            string lockedPromptText,
            string iconId,
            string visualEffectId,
            string audioCueId,
            float cooldownSeconds,
            int priority,
            bool enabled)
            : this(
                  presentationId,
                  portalId,
                  displayName,
                  promptText,
                  lockedPromptText,
                  iconId,
                  visualEffectId,
                  audioCueId,
                  cooldownSeconds,
                  priority,
                  enabled,
                  true,
                  true,
                  true)
        {
        }

        public DimensionPortalPresentationDefinition(
            string presentationId,
            string portalId,
            string displayName,
            string promptText,
            string lockedPromptText,
            string iconId,
            string visualEffectId,
            string audioCueId,
            float cooldownSeconds,
            int priority,
            bool enabled,
            bool requireGeneratedAreaOnUse,
            bool allowFallbackPositionOnUse)
            : this(
                  presentationId,
                  portalId,
                  displayName,
                  promptText,
                  lockedPromptText,
                  iconId,
                  visualEffectId,
                  audioCueId,
                  cooldownSeconds,
                  priority,
                  enabled,
                  requireGeneratedAreaOnUse,
                  allowFallbackPositionOnUse,
                  true)
        {
        }

        public DimensionPortalPresentationDefinition(
            string presentationId,
            string portalId,
            string displayName,
            string promptText,
            string lockedPromptText,
            string iconId,
            string visualEffectId,
            string audioCueId,
            float cooldownSeconds,
            int priority,
            bool enabled,
            bool requireGeneratedAreaOnUse,
            bool allowFallbackPositionOnUse,
            bool interactable)
        {
            PresentationId = presentationId ?? string.Empty;
            PortalId = portalId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PromptText = promptText ?? string.Empty;
            LockedPromptText = lockedPromptText ?? string.Empty;
            IconId = iconId ?? string.Empty;
            VisualEffectId = visualEffectId ?? string.Empty;
            AudioCueId = audioCueId ?? string.Empty;
            CooldownSeconds = cooldownSeconds < 0f ? 0f : cooldownSeconds;
            Priority = priority;
            Enabled = enabled;
            RequireGeneratedAreaOnUse = requireGeneratedAreaOnUse;
            AllowFallbackPositionOnUse = allowFallbackPositionOnUse;
            Interactable = interactable;
        }
    }
}
