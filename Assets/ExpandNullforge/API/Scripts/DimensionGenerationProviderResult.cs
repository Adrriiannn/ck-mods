namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationProviderResult
    {
        public readonly DimensionGenerationState State;
        public readonly float Progress01;
        public readonly string Message;

        public DimensionGenerationProviderResult(
            DimensionGenerationState state,
            float progress01,
            string message)
        {
            State = state;
            Progress01 = progress01;
            Message = message ?? string.Empty;
        }

        public static DimensionGenerationProviderResult Progress(
            DimensionGenerationState state,
            float progress01,
            string message)
        {
            return new DimensionGenerationProviderResult(state, progress01, message);
        }

        public static DimensionGenerationProviderResult Ready(string message)
        {
            return new DimensionGenerationProviderResult(
                DimensionGenerationState.Ready,
                1.0f,
                message);
        }

        public static DimensionGenerationProviderResult Failed(string message)
        {
            return new DimensionGenerationProviderResult(
                DimensionGenerationState.Failed,
                0.0f,
                message);
        }
    }
}
