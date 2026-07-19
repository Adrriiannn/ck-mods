namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationPlanPreflightResult
    {
        public readonly bool CanExecute;
        public readonly string Code;
        public readonly string Message;
        public readonly DimensionGenerationPlan Plan;
        public readonly int MatchingPassCount;
        public readonly int EnabledPassCount;
        public readonly int DisabledPassCount;
        public readonly int MissingProviderCount;
        public readonly int ProviderRejectedPassCount;
        public readonly int ExecutablePassCount;
        public readonly bool HasFallbackProvider;

        public DimensionGenerationPlanPreflightResult(
            bool canExecute,
            string code,
            string message,
            DimensionGenerationPlan plan,
            int matchingPassCount,
            int enabledPassCount,
            int disabledPassCount,
            int missingProviderCount,
            int providerRejectedPassCount,
            int executablePassCount,
            bool hasFallbackProvider)
        {
            CanExecute = canExecute;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Plan = plan;
            MatchingPassCount = matchingPassCount;
            EnabledPassCount = enabledPassCount;
            DisabledPassCount = disabledPassCount;
            MissingProviderCount = missingProviderCount;
            ProviderRejectedPassCount = providerRejectedPassCount;
            ExecutablePassCount = executablePassCount;
            HasFallbackProvider = hasFallbackProvider;
        }
    }
}
