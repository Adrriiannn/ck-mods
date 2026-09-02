using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionContentPackReadinessResult
    {
        public readonly string ContentPackId;
        public readonly bool ContentPackExists;
        public readonly bool Ready;
        public readonly bool Enabled;
        public readonly bool ApiCompatible;
        public readonly int MinimumApiVersion;
        public readonly int CurrentApiVersion;
        public readonly IReadOnlyList<string> MissingDependencyIds;
        public readonly string Code;
        public readonly string Message;

        public DimensionContentPackReadinessResult(
            string contentPackId,
            bool contentPackExists,
            bool ready,
            bool enabled,
            bool apiCompatible,
            int minimumApiVersion,
            int currentApiVersion,
            IReadOnlyList<string> missingDependencyIds,
            string code,
            string message)
        {
            ContentPackId = contentPackId ?? string.Empty;
            ContentPackExists = contentPackExists;
            Ready = ready;
            Enabled = enabled;
            ApiCompatible = apiCompatible;
            MinimumApiVersion = minimumApiVersion;
            CurrentApiVersion = currentApiVersion;
            MissingDependencyIds = missingDependencyIds ?? new List<string>();
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
