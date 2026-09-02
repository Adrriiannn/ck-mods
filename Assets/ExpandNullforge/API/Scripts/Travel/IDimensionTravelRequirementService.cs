using System;
using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public interface IDimensionTravelRequirementService
    {
        event Action<DimensionTravelRequirementChangedEvent> TravelRequirementChanged;

        IReadOnlyList<DimensionTravelRequirementDefinition> GetTravelRequirements(
            DimensionTravelRequirementQuery query);

        bool TryGetTravelRequirement(
            string requirementId,
            out DimensionTravelRequirementDefinition requirement);

        bool TryRegisterTravelRequirement(
            DimensionTravelRequirementDefinition requirement,
            out DimensionOperationResult result);

        bool TryUpdateTravelRequirement(
            DimensionTravelRequirementDefinition requirement,
            string reason,
            out DimensionOperationResult result);

        bool TrySetTravelRequirementEnabled(
            string requirementId,
            bool enabled,
            string reason,
            out DimensionOperationResult result);

        bool TryRemoveTravelRequirement(
            string requirementId,
            out DimensionOperationResult result);

        bool TryRegisterTravelRequirementEvaluator(
            IDimensionTravelRequirementEvaluator evaluator,
            out DimensionOperationResult result);

        bool TryRemoveTravelRequirementEvaluator(
            string providerId,
            out DimensionOperationResult result);

        IReadOnlyList<string> GetTravelRequirementEvaluatorIds();

        DimensionTravelRequirementEvaluationResult EvaluateTravelRequirement(
            DimensionTravelRequirementEvaluationContext context);

        IReadOnlyList<DimensionTravelRequirementEvaluationResult> EvaluateTravelRequirements(
            DimensionAccessContext travelContext,
            bool enabledOnly);
    }
}
