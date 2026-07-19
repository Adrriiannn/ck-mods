namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeChangedEvent
    {
        public readonly DimensionBiomeDefinition Biome;
        public readonly DimensionBiomeChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionBiomeChangedEvent(
            DimensionBiomeDefinition biome,
            DimensionBiomeChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Biome = biome;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
