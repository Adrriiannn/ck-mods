using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionStarterDefinition> GetStarters(
        string dimensionId,
        bool includeDisabled)
    {
      List<DimensionStarterDefinition> result = new List<DimensionStarterDefinition>();
      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        if (!includeDisabled && !starter.Enabled)
        {
          continue;
        }

        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(starter.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        result.Add(starter);
      }

      result.Sort(CompareStarters);
      return result;
    }

    public bool TryGetStarter(
        string starterId,
        out DimensionStarterDefinition starter)
    {
      if (string.IsNullOrEmpty(starterId))
      {
        starter = default(DimensionStarterDefinition);
        return false;
      }

      return starters.TryGetValue(starterId, out starter);
    }

    public bool TryRegisterStarter(
        DimensionStarterDefinition starter,
        out DimensionOperationResult result)
    {
      if (!ValidateStarterDefinition(starter, false, out result))
      {
        return false;
      }

      if (starters.ContainsKey(starter.StarterId))
      {
        result = DimensionOperationResult.Failed(
            "starter-already-registered",
            "A dimension starter with that id is already registered.");
        return false;
      }

      AddStarterInternal(starter);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveStarter(
        string starterId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(starterId))
      {
        result = DimensionOperationResult.Failed("starter-id-empty", "A starter id is required.");
        return false;
      }

      if (IsProtectedStarterId(starterId))
      {
        result = DimensionOperationResult.Failed(
            "starter-protected",
            "Built-in dimension starters cannot be removed.");
        return false;
      }

      if (!starters.Remove(starterId))
      {
        result = DimensionOperationResult.Failed(
            "starter-not-found",
            "No dimension starter with that id is registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    public DimensionStarterReadinessSnapshot GetStarterReadiness(string starterId)
    {
      DimensionStarterDefinition starter;
      if (!TryGetStarter(starterId, out starter))
      {
        return new DimensionStarterReadinessSnapshot(
            false,
            "starter-not-found",
            "No dimension starter with that id is registered.",
            starterId,
            string.Empty,
            default(DimensionContentReadinessResult),
            default(DimensionTravelLoopPreflightResult));
      }

      if (!starter.Enabled)
      {
        return new DimensionStarterReadinessSnapshot(
            false,
            "starter-disabled",
            "The requested dimension starter is disabled.",
            starter.StarterId,
            starter.DimensionId,
            default(DimensionContentReadinessResult),
            default(DimensionTravelLoopPreflightResult));
      }

      return GetStarterReadiness(
          new DimensionStarterReadinessRequest(
              starter.StarterId,
              starter.DimensionId,
              starter.ContentReadinessRequest,
              starter.TravelLoopPreflightRequest));
    }

    public DimensionGenerationStatus EnsureStarterAreaQueued(
        string starterId,
        string requesterId,
        int priority,
        string reason)
    {
      DimensionStarterDefinition starter;
      if (!TryGetStarter(starterId, out starter))
      {
        return new DimensionGenerationStatus(
            string.Empty,
            default(DimensionBounds),
            DimensionGenerationState.Failed,
            0f,
            "No dimension starter with that id is registered.");
      }

      if (!starter.Enabled)
      {
        return new DimensionGenerationStatus(
            starter.DimensionId,
            starter.GenerationRequest.LocalBounds,
            DimensionGenerationState.Failed,
            0f,
            "The requested dimension starter is disabled.");
      }

      DimensionStarterGenerationRequest request = starter.GenerationRequest;
      return EnsureStarterAreaQueued(
          new DimensionStarterGenerationRequest(
              starter.StarterId,
              string.IsNullOrEmpty(requesterId) ? request.RequesterId : requesterId,
              starter.DimensionId,
              request.LocalBounds,
              priority <= 0 ? request.Priority : priority,
              request.CreateIfMissing,
              string.IsNullOrEmpty(reason) ? request.Reason : reason));
    }

    private void AddStarterInternal(DimensionStarterDefinition starter)
    {
      starters[starter.StarterId] = starter;
    }

    private bool ValidateStarterDefinition(
        DimensionStarterDefinition starter,
        bool allowExisting,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(starter.StarterId))
      {
        result = DimensionOperationResult.Failed("starter-id-empty", "A starter id is required.");
        return false;
      }

      if (!allowExisting && starters.ContainsKey(starter.StarterId))
      {
        result = DimensionOperationResult.Failed(
            "starter-already-registered",
            "A dimension starter with that id is already registered.");
        return false;
      }

      if (!definitions.ContainsKey(starter.DimensionId))
      {
        result = DimensionOperationResult.Failed(
            "starter-dimension-not-found",
            "The starter dimension is not registered.");
        return false;
      }

      if (!string.Equals(
              starter.GenerationRequest.DimensionId,
              starter.DimensionId,
              StringComparison.Ordinal))
      {
        result = DimensionOperationResult.Failed(
            "starter-generation-dimension-mismatch",
            "The starter generation request must target the starter dimension.");
        return false;
      }

      string boundsError;
      if (!IsValidGenerationBounds(starter.GenerationRequest.LocalBounds, out boundsError))
      {
        result = DimensionOperationResult.Failed("starter-generation-bounds-invalid", boundsError);
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareStarters(
        DimensionStarterDefinition left,
        DimensionStarterDefinition right)
    {
      int byDimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (byDimension != 0)
      {
        return byDimension;
      }

      return string.Compare(left.StarterId, right.StarterId, StringComparison.Ordinal);
    }

    private bool StarterDefinitionEquals(
        DimensionStarterDefinition left,
        DimensionStarterDefinition right)
    {
      return string.Equals(left.StarterId, right.StarterId, StringComparison.Ordinal) &&
             string.Equals(left.DimensionId, right.DimensionId, StringComparison.Ordinal) &&
             string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
             string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
             string.Equals(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal) &&
             left.Enabled == right.Enabled &&
             ContentReadinessRequestEquals(left.ContentReadinessRequest, right.ContentReadinessRequest) &&
             TravelLoopPreflightRequestEquals(left.TravelLoopPreflightRequest, right.TravelLoopPreflightRequest) &&
             StarterGenerationRequestEquals(left.GenerationRequest, right.GenerationRequest);
    }

    private bool StarterGenerationRequestEquals(
        DimensionStarterGenerationRequest left,
        DimensionStarterGenerationRequest right)
    {
      return string.Equals(left.StarterId, right.StarterId, StringComparison.Ordinal) &&
             string.Equals(left.RequesterId, right.RequesterId, StringComparison.Ordinal) &&
             string.Equals(left.DimensionId, right.DimensionId, StringComparison.Ordinal) &&
             BoundsEqual(left.LocalBounds, right.LocalBounds) &&
             left.Priority == right.Priority &&
             left.CreateIfMissing == right.CreateIfMissing &&
             string.Equals(left.Reason, right.Reason, StringComparison.Ordinal);
    }

    private bool TravelLoopPreflightRequestEquals(
        DimensionTravelLoopPreflightRequest left,
        DimensionTravelLoopPreflightRequest right)
    {
      return string.Equals(left.SourceDimensionId, right.SourceDimensionId, StringComparison.Ordinal) &&
             string.Equals(left.TargetDimensionId, right.TargetDimensionId, StringComparison.Ordinal) &&
             string.Equals(left.EntryPortalId, right.EntryPortalId, StringComparison.Ordinal) &&
             string.Equals(left.ReturnPortalId, right.ReturnPortalId, StringComparison.Ordinal) &&
             string.Equals(left.SourceAnchorId, right.SourceAnchorId, StringComparison.Ordinal) &&
             string.Equals(left.TargetAnchorId, right.TargetAnchorId, StringComparison.Ordinal) &&
             string.Equals(left.SourceMarkerId, right.SourceMarkerId, StringComparison.Ordinal) &&
             string.Equals(left.TargetMarkerId, right.TargetMarkerId, StringComparison.Ordinal) &&
             BoundsEqual(left.TargetLandingBounds, right.TargetLandingBounds) &&
             left.RequireReturnPortal == right.RequireReturnPortal &&
             left.RequireSourceAnchor == right.RequireSourceAnchor &&
             left.RequireTargetAnchor == right.RequireTargetAnchor &&
             left.RequireMarkers == right.RequireMarkers &&
             left.RequireTargetAreaReady == right.RequireTargetAreaReady &&
             left.RequireMapLayers == right.RequireMapLayers;
    }

    private static bool ContentReadinessRequestEquals(
        DimensionContentReadinessRequest left,
        DimensionContentReadinessRequest right)
    {
      if (!string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) ||
          !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal))
      {
        return false;
      }

      IReadOnlyList<DimensionContentReadinessRequirement> leftRequirements =
          left.Requirements ?? new List<DimensionContentReadinessRequirement>();
      IReadOnlyList<DimensionContentReadinessRequirement> rightRequirements =
          right.Requirements ?? new List<DimensionContentReadinessRequirement>();
      if (leftRequirements.Count != rightRequirements.Count)
      {
        return false;
      }

      for (int i = 0; i < leftRequirements.Count; i++)
      {
        if (!ContentReadinessRequirementEquals(leftRequirements[i], rightRequirements[i]))
        {
          return false;
        }
      }

      return true;
    }

    private static bool ContentReadinessRequirementEquals(
        DimensionContentReadinessRequirement left,
        DimensionContentReadinessRequirement right)
    {
      return left.RecordKind == right.RecordKind &&
             string.Equals(left.RecordId, right.RecordId, StringComparison.Ordinal) &&
             string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
             left.RequireOwnership == right.RequireOwnership &&
             string.Equals(left.RequiredOwnerContentPackId, right.RequiredOwnerContentPackId, StringComparison.Ordinal) &&
             left.RequireEnabledOwnerContentPack == right.RequireEnabledOwnerContentPack;
    }

    private static bool IsProtectedStarterId(string starterId)
    {
      return false;
    }
  }
}
