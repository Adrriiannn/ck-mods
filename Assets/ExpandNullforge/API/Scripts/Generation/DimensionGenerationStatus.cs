namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationStatus
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly DimensionGenerationState State;
        public readonly float Progress01;
        public readonly string Message;

        public DimensionGenerationStatus(
            string dimensionId,
            DimensionBounds localBounds,
            DimensionGenerationState state,
            float progress01,
            string message)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            State = state;
            Progress01 = progress01;
            Message = message ?? string.Empty;
        }
    }
}
