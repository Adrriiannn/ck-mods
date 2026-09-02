using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionGenerationPassService
    {
        event Action<DimensionGenerationPassChangedEvent> GenerationPassChanged;

        IReadOnlyList<DimensionGenerationPassDefinition> GetGenerationPasses(
            string dimensionId,
            string zoneId,
            bool includeDisabled);

        bool TryGetGenerationPass(
            string passId,
            out DimensionGenerationPassDefinition generationPass);

        bool TryRegisterGenerationPass(
            DimensionGenerationPassDefinition generationPass,
            out DimensionOperationResult result);

        bool TryUpdateGenerationPass(
            DimensionGenerationPassDefinition generationPass,
            string reason,
            out DimensionOperationResult result);

        bool TrySetGenerationPassEnabled(
            string passId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveGenerationPass(
            string passId,
            out DimensionOperationResult result);
    }
}
