namespace ExpandNullforge.Api
{
    public readonly struct DimensionProgressFlag
    {
        public readonly string FlagId;
        public readonly string DimensionId;
        public readonly string Category;
        public readonly bool Value;
        public readonly long UpdatedUtcTicks;

        public DimensionProgressFlag(
            string flagId,
            string dimensionId,
            string category,
            bool value,
            long updatedUtcTicks)
        {
            FlagId = flagId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            Category = category ?? string.Empty;
            Value = value;
            UpdatedUtcTicks = updatedUtcTicks;
        }
    }
}
