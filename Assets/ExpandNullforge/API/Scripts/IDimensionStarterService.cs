using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionStarterService
    {
        IReadOnlyList<DimensionStarterDefinition> GetStarters(
            string dimensionId,
            bool includeDisabled);

        bool TryGetStarter(
            string starterId,
            out DimensionStarterDefinition starter);

        bool TryRegisterStarter(
            DimensionStarterDefinition starter,
            out DimensionOperationResult result);

        bool TryRemoveStarter(
            string starterId,
            out DimensionOperationResult result);

        DimensionStarterReadinessSnapshot GetStarterReadiness(
            string starterId);

        DimensionGenerationStatus EnsureStarterAreaQueued(
            string starterId,
            string requesterId,
            int priority,
            string reason);
    }
}
