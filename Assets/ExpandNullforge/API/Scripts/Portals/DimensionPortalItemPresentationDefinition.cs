namespace ExpandNullforge.Api
{
    public readonly struct DimensionPortalItemPresentationDefinition
    {
        public readonly string PortalObjectName;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string IconId;
        public readonly string RarityId;
        public readonly string InventoryBorderId;
        public readonly string TooltipId;
        public readonly string PaletteId;
        public readonly int Priority;
        public readonly bool Enabled;

        public DimensionPortalItemPresentationDefinition(
            string portalObjectName,
            string displayName,
            string description,
            string iconId,
            string rarityId,
            string inventoryBorderId,
            string tooltipId,
            string paletteId,
            int priority,
            bool enabled)
        {
            PortalObjectName = portalObjectName ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IconId = iconId ?? string.Empty;
            RarityId = rarityId ?? string.Empty;
            InventoryBorderId = inventoryBorderId ?? string.Empty;
            TooltipId = tooltipId ?? string.Empty;
            PaletteId = paletteId ?? string.Empty;
            Priority = priority;
            Enabled = enabled;
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(PortalObjectName); }
        }
    }
}
