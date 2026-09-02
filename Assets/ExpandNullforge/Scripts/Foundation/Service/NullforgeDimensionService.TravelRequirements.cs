using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionTravelRequirementDefinition> GetTravelRequirements(
        DimensionTravelRequirementQuery query)
    {
      List<DimensionTravelRequirementDefinition> result =
          new List<DimensionTravelRequirementDefinition>();
      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        if (!TravelRequirementMatchesQuery(requirement, query))
        {
          continue;
        }

        result.Add(requirement);
      }

      result.Sort(CompareTravelRequirements);
      return result;
    }

    public bool TryGetTravelRequirement(
        string requirementId,
        out DimensionTravelRequirementDefinition requirement)
    {
      if (string.IsNullOrEmpty(requirementId))
      {
        requirement = default(DimensionTravelRequirementDefinition);
        return false;
      }

      return travelRequirements.TryGetValue(requirementId, out requirement);
    }

    public bool TryRegisterTravelRequirement(
        DimensionTravelRequirementDefinition requirement,
        out DimensionOperationResult result)
    {
      if (!ValidateTravelRequirement(requirement, out result))
      {
        return false;
      }

      if (travelRequirements.ContainsKey(requirement.RequirementId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-already-registered", "A travel requirement with that id is already registered.");
        return false;
      }

      travelRequirements[requirement.RequirementId] = requirement;
      RaiseTravelRequirementChanged(
          requirement,
          DimensionTravelRequirementChangeKind.Registered,
          false,
          requirement.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateTravelRequirement(
        DimensionTravelRequirementDefinition requirement,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionTravelRequirementDefinition previous;
      if (!travelRequirements.TryGetValue(requirement.RequirementId, out previous))
      {
        result = DimensionOperationResult.Failed("travel-requirement-not-found", "No travel requirement with that id is registered.");
        return false;
      }

      if (!ValidateTravelRequirement(requirement, out result))
      {
        return false;
      }

      if (TravelRequirementEquals(previous, requirement))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      travelRequirements[requirement.RequirementId] = requirement;
      RaiseTravelRequirementChanged(
          requirement,
          previous.Enabled == requirement.Enabled
              ? DimensionTravelRequirementChangeKind.Updated
              : DimensionTravelRequirementChangeKind.EnabledChanged,
          previous.Enabled,
          requirement.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetTravelRequirementEnabled(
        string requirementId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(requirementId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-id-empty", "A travel requirement id is required.");
        return false;
      }

      DimensionTravelRequirementDefinition requirement;
      if (!travelRequirements.TryGetValue(requirementId, out requirement))
      {
        result = DimensionOperationResult.Failed("travel-requirement-not-found", "No travel requirement with that id is registered.");
        return false;
      }

      if (requirement.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionTravelRequirementDefinition updated =
          new DimensionTravelRequirementDefinition(
              requirement.RequirementId,
              requirement.DisplayName,
              requirement.PortalId,
              requirement.DimensionId,
              requirement.Kind,
              requirement.SubjectId,
              requirement.RequiredAmount,
              requirement.ConsumeOnTravel,
              requirement.FailureMessage,
              requirement.Priority,
              enabled);

      travelRequirements[requirementId] = updated;
      RaiseTravelRequirementChanged(
          updated,
          DimensionTravelRequirementChangeKind.EnabledChanged,
          requirement.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveTravelRequirement(
        string requirementId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(requirementId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-id-empty", "A travel requirement id is required.");
        return false;
      }

      DimensionTravelRequirementDefinition requirement;
      if (!travelRequirements.TryGetValue(requirementId, out requirement))
      {
        result = DimensionOperationResult.Failed("travel-requirement-not-found", "No travel requirement with that id is registered.");
        return false;
      }

      travelRequirements.Remove(requirementId);
      RaiseTravelRequirementChanged(
          requirement,
          DimensionTravelRequirementChangeKind.Removed,
          requirement.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRegisterTravelRequirementEvaluator(
        IDimensionTravelRequirementEvaluator evaluator,
        out DimensionOperationResult result)
    {
      if (evaluator == null)
      {
        result = DimensionOperationResult.Failed("travel-requirement-evaluator-null", "A travel requirement evaluator is required.");
        return false;
      }

      if (string.IsNullOrEmpty(evaluator.ProviderId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-evaluator-id-empty", "A travel requirement evaluator id is required.");
        return false;
      }

      if (travelRequirementEvaluators.ContainsKey(evaluator.ProviderId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-evaluator-duplicate", "A travel requirement evaluator with that id is already registered.");
        return false;
      }

      travelRequirementEvaluators[evaluator.ProviderId] = evaluator;
      travelRequirementEvaluatorOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Travel requirement evaluator registered: " + evaluator.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveTravelRequirementEvaluator(
        string providerId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-evaluator-id-empty", "A travel requirement evaluator id is required.");
        return false;
      }

      if (!travelRequirementEvaluators.Remove(providerId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-evaluator-not-found", "No travel requirement evaluator with that id is registered.");
        return false;
      }

      travelRequirementEvaluatorOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Travel requirement evaluator removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<string> GetTravelRequirementEvaluatorIds()
    {
      List<string> result = new List<string>(travelRequirementEvaluators.Count);
      foreach (string providerId in travelRequirementEvaluators.Keys)
      {
        result.Add(providerId);
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public DimensionTravelRequirementEvaluationResult EvaluateTravelRequirement(
        DimensionTravelRequirementEvaluationContext context)
    {
      DimensionOperationResult validation;
      if (!ValidateTravelRequirement(context.Requirement, out validation))
      {
        return DimensionTravelRequirementEvaluationResult.Unevaluated(
            context.Requirement,
            validation.Code,
            validation.Message);
      }

      return EvaluateTravelRequirementProviders(context);
    }

    public IReadOnlyList<DimensionTravelRequirementEvaluationResult> EvaluateTravelRequirements(
        DimensionAccessContext travelContext,
        bool enabledOnly)
    {
      string targetDimensionId = travelContext.TargetDimension.Id;
      DimensionTravelRequirementQuery query =
          new DimensionTravelRequirementQuery(
              travelContext.PortalId,
              targetDimensionId,
              DimensionTravelRequirementKind.Any,
              enabledOnly,
              true,
              true);

      IReadOnlyList<DimensionTravelRequirementDefinition> requirements =
          GetTravelRequirements(query);
      List<DimensionTravelRequirementEvaluationResult> result =
          new List<DimensionTravelRequirementEvaluationResult>(requirements.Count);

      for (int i = 0; i < requirements.Count; i++)
      {
        result.Add(
            EvaluateTravelRequirement(
                new DimensionTravelRequirementEvaluationContext(
                    travelContext,
                    requirements[i],
                    travelContext.Reason)));
      }

      return result;
    }
  }
}
