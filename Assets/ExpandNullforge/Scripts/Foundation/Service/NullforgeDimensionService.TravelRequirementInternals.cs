using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateTravelRequirement(
        DimensionTravelRequirementDefinition requirement,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(requirement.RequirementId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-id-empty", "A travel requirement id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(requirement.PortalId) &&
          string.IsNullOrEmpty(requirement.DimensionId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-target-empty", "A travel requirement needs a portal id, dimension id, or both.");
        return false;
      }

      if (!IsValidTravelRequirementKind(requirement.Kind) ||
          requirement.Kind == DimensionTravelRequirementKind.Any)
      {
        result = DimensionOperationResult.Failed("travel-requirement-kind-invalid", "The travel requirement kind is not supported.");
        return false;
      }

      if (requirement.RequiredAmount < 0)
      {
        result = DimensionOperationResult.Failed("travel-requirement-amount-invalid", "A travel requirement amount cannot be negative.");
        return false;
      }

      if (RequirementKindNeedsSubject(requirement.Kind) &&
          string.IsNullOrEmpty(requirement.SubjectId))
      {
        result = DimensionOperationResult.Failed("travel-requirement-subject-empty", "This travel requirement kind needs a subject id.");
        return false;
      }

      DimensionPortalDefinition portal;
      if (!string.IsNullOrEmpty(requirement.PortalId) &&
          portals.TryGetValue(requirement.PortalId, out portal) &&
          !string.IsNullOrEmpty(requirement.DimensionId) &&
          !string.Equals(portal.ToDimensionId, requirement.DimensionId, StringComparison.Ordinal))
      {
        result = DimensionOperationResult.Failed("travel-requirement-portal-dimension-mismatch", "The travel requirement dimension must match the portal destination dimension.");
        return false;
      }

      DimensionDefinition dimension;
      if (!string.IsNullOrEmpty(requirement.DimensionId) &&
          !TryGetDimension(requirement.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("travel-requirement-dimension-not-found", "The travel requirement dimension is not registered.");
        return false;
      }

      if (requirement.Kind == DimensionTravelRequirementKind.ProgressFlag &&
          !string.IsNullOrEmpty(requirement.SubjectId))
      {
        DimensionProgressFlag flag;
        if (progressFlags.TryGetValue(requirement.SubjectId, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.IsNullOrEmpty(requirement.DimensionId) &&
            !string.Equals(flag.DimensionId, requirement.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("travel-requirement-progress-flag-dimension-mismatch", "The travel requirement progress flag belongs to another dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidTravelRequirementKind(DimensionTravelRequirementKind kind)
    {
      return kind == DimensionTravelRequirementKind.Any ||
             kind == DimensionTravelRequirementKind.ProgressFlag ||
             kind == DimensionTravelRequirementKind.Item ||
             kind == DimensionTravelRequirementKind.BossDefeat ||
             kind == DimensionTravelRequirementKind.Discovery ||
             kind == DimensionTravelRequirementKind.SceneClear ||
             kind == DimensionTravelRequirementKind.Custom;
    }

    private static bool RequirementKindNeedsSubject(DimensionTravelRequirementKind kind)
    {
      return kind == DimensionTravelRequirementKind.ProgressFlag ||
             kind == DimensionTravelRequirementKind.Item ||
             kind == DimensionTravelRequirementKind.BossDefeat ||
             kind == DimensionTravelRequirementKind.Discovery ||
             kind == DimensionTravelRequirementKind.SceneClear;
    }

    private static bool TravelRequirementMatchesQuery(
        DimensionTravelRequirementDefinition requirement,
        DimensionTravelRequirementQuery query)
    {
      if (query.EnabledOnly && !requirement.Enabled)
      {
        return false;
      }

      if (query.Kind != DimensionTravelRequirementKind.Any &&
          requirement.Kind != query.Kind)
      {
        return false;
      }

      bool hasPortal = !string.IsNullOrEmpty(query.PortalId);
      bool hasDimension = !string.IsNullOrEmpty(query.DimensionId);

      if (hasPortal)
      {
        bool portalMatch = string.Equals(requirement.PortalId, query.PortalId, StringComparison.Ordinal);
        bool dimensionWideMatch =
            query.IncludeDimensionWideRequirements &&
            string.IsNullOrEmpty(requirement.PortalId) &&
            hasDimension &&
            string.Equals(requirement.DimensionId, query.DimensionId, StringComparison.Ordinal);
        if (!portalMatch && !dimensionWideMatch)
        {
          return false;
        }
      }

      if (hasDimension)
      {
        bool dimensionMatch = string.Equals(requirement.DimensionId, query.DimensionId, StringComparison.Ordinal);
        bool portalWideMatch =
            query.IncludePortalWideRequirements &&
            string.IsNullOrEmpty(requirement.DimensionId) &&
            hasPortal &&
            string.Equals(requirement.PortalId, query.PortalId, StringComparison.Ordinal);
        if (!dimensionMatch && !portalWideMatch)
        {
          return false;
        }
      }

      return true;
    }

    private static int CompareTravelRequirements(
        DimensionTravelRequirementDefinition left,
        DimensionTravelRequirementDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int portal = string.Compare(left.PortalId, right.PortalId, StringComparison.Ordinal);
      if (portal != 0)
      {
        return portal;
      }

      return string.Compare(left.RequirementId, right.RequirementId, StringComparison.Ordinal);
    }

    private static bool TravelRequirementEquals(
        DimensionTravelRequirementDefinition a,
        DimensionTravelRequirementDefinition b)
    {
      return string.Equals(a.RequirementId, b.RequirementId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.PortalId, b.PortalId, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             a.Kind == b.Kind &&
             string.Equals(a.SubjectId, b.SubjectId, StringComparison.Ordinal) &&
             a.RequiredAmount == b.RequiredAmount &&
             a.ConsumeOnTravel == b.ConsumeOnTravel &&
             string.Equals(a.FailureMessage, b.FailureMessage, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
