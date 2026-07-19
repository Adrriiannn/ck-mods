namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPlanPreflightRequest
    {
        public readonly DimensionGenerationPlanRequest PlanRequest;
        public readonly bool RequireAnyPass;
        public readonly bool RequireRegisteredProviders;
        public readonly bool RequireProviderCanGenerate;
        public readonly bool AllowFallbackProvider;
        public readonly string Reason;

        public DimensionGenerationPlanPreflightRequest(
            DimensionGenerationPlanRequest planRequest,
            bool requireAnyPass,
            bool requireRegisteredProviders,
            bool requireProviderCanGenerate,
            bool allowFallbackProvider,
            string reason)
        {
            PlanRequest = planRequest;
            RequireAnyPass = requireAnyPass;
            RequireRegisteredProviders = requireRegisteredProviders;
            RequireProviderCanGenerate = requireProviderCanGenerate;
            AllowFallbackProvider = allowFallbackProvider;
            Reason = reason ?? string.Empty;
        }
    }
}
