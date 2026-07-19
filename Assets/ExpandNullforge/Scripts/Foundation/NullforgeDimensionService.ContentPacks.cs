using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool IsProtectedContentPackId(string contentPackId)
    {
      return string.Equals(contentPackId, BuiltInFrameworkContentPack.ContentPackId, StringComparison.Ordinal);
    }

    public IReadOnlyList<DimensionContentPackDefinition> GetContentPacks(bool includeDisabled)
    {
      List<DimensionContentPackDefinition> result =
          new List<DimensionContentPackDefinition>();
      foreach (DimensionContentPackDefinition contentPack in contentPacks.Values)
      {
        if (!includeDisabled && !contentPack.Enabled)
        {
          continue;
        }

        result.Add(contentPack);
      }

      result.Sort(CompareContentPacks);
      return result;
    }

    public bool TryGetContentPack(
        string contentPackId,
        out DimensionContentPackDefinition contentPack)
    {
      if (string.IsNullOrEmpty(contentPackId))
      {
        contentPack = default(DimensionContentPackDefinition);
        return false;
      }

      return contentPacks.TryGetValue(contentPackId, out contentPack);
    }

    public IReadOnlyList<string> GetMissingContentPackDependencies(string contentPackId)
    {
      List<string> result = new List<string>();
      DimensionContentPackDefinition contentPack;
      if (!contentPacks.TryGetValue(contentPackId, out contentPack) ||
          contentPack.DependencyIds == null)
      {
        return result;
      }

      for (int i = 0; i < contentPack.DependencyIds.Count; i++)
      {
        string dependencyId = contentPack.DependencyIds[i];
        DimensionContentPackDefinition dependency;
        if (!contentPacks.TryGetValue(dependencyId, out dependency) ||
            !dependency.Enabled)
        {
          result.Add(dependencyId);
        }
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public DimensionContentPackReadinessResult EvaluateContentPackReadiness(string contentPackId)
    {
      if (string.IsNullOrEmpty(contentPackId))
      {
        return new DimensionContentPackReadinessResult(
            string.Empty,
            false,
            false,
            false,
            false,
            0,
            DimensionApi.CurrentApiVersion,
            new List<string>(),
            "content-pack-id-empty",
            "A content pack id is required.");
      }

      DimensionContentPackDefinition contentPack;
      if (!contentPacks.TryGetValue(contentPackId, out contentPack))
      {
        return new DimensionContentPackReadinessResult(
            contentPackId,
            false,
            false,
            false,
            false,
            0,
            DimensionApi.CurrentApiVersion,
            new List<string>(),
            "content-pack-not-found",
            "No content pack with that id is registered.");
      }

      return EvaluateContentPackReadinessInternal(contentPack);
    }

    public IReadOnlyList<DimensionContentPackReadinessResult> EvaluateContentPackReadiness(bool includeDisabled)
    {
      List<DimensionContentPackReadinessResult> result = new List<DimensionContentPackReadinessResult>();
      foreach (DimensionContentPackDefinition contentPack in contentPacks.Values)
      {
        if (!includeDisabled && !contentPack.Enabled)
        {
          continue;
        }

        result.Add(EvaluateContentPackReadinessInternal(contentPack));
      }

      result.Sort(CompareContentPackReadiness);
      return result;
    }

    public IReadOnlyList<DimensionContentPackSummary> GetContentPackSummaries(
        DimensionContentPackSummaryRequest request)
    {
      List<DimensionContentPackSummary> result = new List<DimensionContentPackSummary>();
      if (!string.IsNullOrEmpty(request.ContentPackId))
      {
        DimensionContentPackDefinition contentPack;
        if (!contentPacks.TryGetValue(request.ContentPackId, out contentPack))
        {
          result.Add(BuildMissingContentPackSummary(request.ContentPackId, request));
          return result;
        }

        if (request.IncludeDisabled || contentPack.Enabled)
        {
          result.Add(BuildContentPackSummary(contentPack, request));
        }

        return result;
      }

      foreach (DimensionContentPackDefinition contentPack in contentPacks.Values)
      {
        if (!request.IncludeDisabled && !contentPack.Enabled)
        {
          continue;
        }

        result.Add(BuildContentPackSummary(contentPack, request));
      }

      AddMissingContentPackSummaries(result, request);
      result.Sort(CompareContentPackSummaries);
      return result;
    }

    public DimensionContentManifestResult ValidateContentManifest(
        DimensionContentManifestRequest request)
    {
      return BuildContentManifestPreflightResult(request);
    }

    public DimensionContentManifestResult TryApplyContentManifest(
        DimensionContentManifestRequest request)
    {
      DimensionContentManifestResult preflight = BuildContentManifestPreflightResult(request);
      if (!preflight.Success)
      {
        return preflight;
      }

      return ApplyContentManifest(request);
    }

    public IReadOnlyList<DimensionContentOwnershipBinding> GetContentOwnershipBindings(
        DimensionContentOwnershipQuery query)
    {
      List<DimensionContentOwnershipBinding> result = new List<DimensionContentOwnershipBinding>();
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (!ContentOwnershipMatchesQuery(binding, query))
        {
          continue;
        }

        result.Add(binding);
      }

      result.Sort(CompareContentOwnershipBindings);
      return result;
    }

    public IReadOnlyList<DimensionContentOwnershipBinding> GetOrphanedContentOwnershipBindings()
    {
      List<DimensionContentOwnershipBinding> result = new List<DimensionContentOwnershipBinding>();
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (!ContentPackExistsAndEnabled(binding.ContentPackId, false) ||
            (!ContentRecordExists(binding.RecordKind, binding.RecordId) &&
             !IsMetadataOnlyContentRecordKind(binding.RecordKind)))
        {
          result.Add(binding);
        }
      }

      result.Sort(CompareContentOwnershipBindings);
      return result;
    }

    public bool TryGetContentOwner(
        DimensionContentRecordKind recordKind,
        string recordId,
        out DimensionContentOwnershipBinding binding)
    {
      if (!IsValidContentRecordKind(recordKind) ||
          string.IsNullOrEmpty(recordId))
      {
        binding = default(DimensionContentOwnershipBinding);
        return false;
      }

      return contentOwnershipBindings.TryGetValue(BuildContentOwnershipKey(recordKind, recordId), out binding);
    }

    public DimensionContentReadinessResult PreflightStarterContent(string starterId)
    {
      DimensionStarterDefinition starter;
      if (!TryGetStarter(starterId, out starter))
      {
        DimensionContentReadinessRequest missingRequest =
            new DimensionContentReadinessRequest(
                string.IsNullOrEmpty(starterId) ? "dimension-starter-content" : starterId,
                "Dimension starter content",
                new List<DimensionContentReadinessRequirement>());
        return new DimensionContentReadinessResult(
            false,
            "starter-not-found",
            "No dimension starter with that id is registered.",
            missingRequest,
            new List<DimensionContentReadinessIssue>
            {
              new DimensionContentReadinessIssue(
                  DimensionContentRecordKind.Starter,
                  starterId,
                  "starter-not-found",
                  "No dimension starter with that id is registered.")
            });
      }

      if (!starter.Enabled)
      {
        return new DimensionContentReadinessResult(
            false,
            "starter-disabled",
            "The requested dimension starter is disabled.",
            starter.ContentReadinessRequest,
            new List<DimensionContentReadinessIssue>
            {
              new DimensionContentReadinessIssue(
                  DimensionContentRecordKind.Starter,
                  starter.StarterId,
                  "starter-disabled",
                  "The requested dimension starter is disabled.")
            });
      }

      return PreflightContentReadiness(starter.ContentReadinessRequest);
    }

    public DimensionStarterReadinessSnapshot GetStarterReadiness(
        DimensionStarterReadinessRequest request)
    {
      DimensionContentReadinessResult contentReadiness =
          PreflightContentReadiness(request.ContentReadinessRequest);
      DimensionTravelLoopPreflightResult travelLoopPreflight =
          PreflightTravelLoop(request.TravelLoopPreflightRequest);
      string starterId = string.IsNullOrEmpty(request.StarterId)
          ? request.ContentReadinessRequest.RequestId
          : request.StarterId;
      string dimensionId = string.IsNullOrEmpty(request.DimensionId)
          ? request.TravelLoopPreflightRequest.TargetDimensionId
          : request.DimensionId;

      return BuildStarterReadinessSnapshot(
          starterId,
          dimensionId,
          contentReadiness,
          travelLoopPreflight,
          string.IsNullOrEmpty(starterId)
              ? "Dimension starter loop is ready."
              : "Dimension starter loop '" + starterId + "' is ready.");
    }

    public DimensionGenerationStatus EnsureStarterAreaQueued(
        DimensionStarterGenerationRequest request)
    {
      DimensionGenerationStatus status;
      if (TryGetGenerationStatus(request.DimensionId, request.LocalBounds, out status) &&
          (status.State == DimensionGenerationState.Ready ||
           IsTransientGenerationState(status.State)))
      {
        return status;
      }

      string requesterId = string.IsNullOrEmpty(request.RequesterId)
          ? "dimension-starter:" + request.StarterId
          : request.RequesterId;
      string reason = string.IsNullOrEmpty(request.Reason)
          ? "Dimension starter area."
          : request.Reason;

      DimensionGenerationStatus queued =
          RequestGeneration(
              new DimensionGenerationRequest(
                  requesterId,
                  request.DimensionId,
                  request.LocalBounds,
                  request.Priority <= 0 ? 100 : request.Priority,
                  request.CreateIfMissing,
                  reason));

      if (queued.State == DimensionGenerationState.Failed)
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Error,
            request.DimensionId,
            "Starter area generation failed for '" + request.StarterId + "': " + queued.Message);
        return queued;
      }

      AddDiagnostic(
          DimensionDiagnosticSeverity.Info,
          request.DimensionId,
          "Starter area '" + request.StarterId + "' is " + queued.State + ".");
      return queued;
    }

    private DimensionStarterReadinessSnapshot BuildStarterReadinessSnapshot(
        string starterId,
        string dimensionId,
        DimensionContentReadinessResult contentReadiness,
        DimensionTravelLoopPreflightResult travelLoopPreflight,
        string readyMessage)
    {
      bool ready = contentReadiness.Ready && travelLoopPreflight.Ready;
      string code = ready
          ? "ready"
          : (!contentReadiness.Ready ? contentReadiness.Code : travelLoopPreflight.Code);
      string message = ready
          ? readyMessage
          : (!contentReadiness.Ready ? contentReadiness.Message : travelLoopPreflight.Message);

      return new DimensionStarterReadinessSnapshot(
          ready,
          code,
          message,
          starterId,
          dimensionId,
          contentReadiness,
          travelLoopPreflight);
    }

    public DimensionContentReadinessResult PreflightContentReadiness(
        DimensionContentReadinessRequest request)
    {
      List<DimensionContentReadinessIssue> issues =
          new List<DimensionContentReadinessIssue>();
      IReadOnlyList<DimensionContentReadinessRequirement> requirements =
          request.Requirements ?? new List<DimensionContentReadinessRequirement>();

      if (requirements.Count == 0)
      {
        issues.Add(
            new DimensionContentReadinessIssue(
                DimensionContentRecordKind.Any,
                request.RequestId,
                "content-readiness-empty",
                "The content readiness request does not contain any required records."));
      }

      for (int i = 0; i < requirements.Count; i++)
      {
        CheckContentReadinessRequirement(requirements[i], issues);
      }

      if (issues.Count > 0)
      {
        return new DimensionContentReadinessResult(
            false,
            "content-readiness-failed",
            "One or more required dimension content records are missing or unavailable.",
            request,
            issues);
      }

      return new DimensionContentReadinessResult(
          true,
          string.Empty,
          string.Empty,
          request,
          issues);
    }

    public DimensionTravelPolicySnapshot GetTravelPolicySnapshot()
    {
      return new DimensionTravelPolicySnapshot(
          TravelPreloadSideTiles,
          TravelLoadTimeoutSeconds,
          TravelArrivalTimeoutSeconds,
          TravelArrivalDistanceSquared,
          TravelTeleportRetryIntervalSeconds,
          DefaultLoadTimeoutSeconds,
          GenerationLoadTimeoutSeconds,
          RuntimeLoadReconcileIntervalSeconds,
          RuntimeGenerationTickIntervalSeconds,
          PlayerContextTrackIntervalSeconds,
          TravelPreloadSideTiles);
    }

    public bool TryBindContentOwnership(
        DimensionContentOwnershipBinding binding,
        bool requireExistingRecord,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateContentOwnershipBinding(binding, requireExistingRecord, out result))
      {
        return false;
      }

      string key = BuildContentOwnershipKey(binding.RecordKind, binding.RecordId);
      DimensionContentOwnershipBinding previous;
      if (contentOwnershipBindings.TryGetValue(key, out previous))
      {
        if (ContentOwnershipEquals(previous, binding))
        {
          PersistContentOwnershipIfWorldRegistryLoaded(binding);
          result = DimensionOperationResult.Ok();
          return true;
        }

        contentOwnershipBindings[key] = binding;
        PersistContentOwnershipIfWorldRegistryLoaded(binding);
        RaiseContentOwnershipChanged(
            binding,
            DimensionContentOwnershipChangeKind.Updated,
            previous.ContentPackId,
            reason ?? string.Empty);
        result = DimensionOperationResult.Ok();
        return true;
      }

      contentOwnershipBindings[key] = binding;
      PersistContentOwnershipIfWorldRegistryLoaded(binding);
      RaiseContentOwnershipChanged(
          binding,
          DimensionContentOwnershipChangeKind.Bound,
          string.Empty,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveContentOwnership(
        DimensionContentRecordKind recordKind,
        string recordId,
        out DimensionContentOwnershipBinding removedBinding,
        out DimensionOperationResult result)
    {
      if (!IsValidContentRecordKind(recordKind))
      {
        removedBinding = default(DimensionContentOwnershipBinding);
        result = DimensionOperationResult.Failed("content-record-kind-invalid", "A valid content record kind is required.");
        return false;
      }

      if (string.IsNullOrEmpty(recordId))
      {
        removedBinding = default(DimensionContentOwnershipBinding);
        result = DimensionOperationResult.Failed("content-record-id-empty", "A content record id is required.");
        return false;
      }

      string key = BuildContentOwnershipKey(recordKind, recordId);
      if (!contentOwnershipBindings.TryGetValue(key, out removedBinding))
      {
        result = DimensionOperationResult.Failed("content-ownership-not-found", "No content ownership binding exists for that record.");
        return false;
      }

      contentOwnershipBindings.Remove(key);
      RemovePersistedContentOwnershipIfWorldRegistryLoaded(recordKind, recordId);
      RaiseContentOwnershipChanged(
          removedBinding,
          DimensionContentOwnershipChangeKind.Removed,
          removedBinding.ContentPackId,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRegisterContentPack(
        DimensionContentPackDefinition contentPack,
        out DimensionOperationResult result)
    {
      if (!ValidateContentPack(contentPack, out result))
      {
        return false;
      }

      if (contentPacks.ContainsKey(contentPack.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-already-registered", "A content pack with that id is already registered.");
        return false;
      }

      AddContentPackInternal(contentPack);
      RaiseContentPackChanged(
          contentPack,
          DimensionContentPackChangeKind.Registered,
          false,
          contentPack.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateContentPack(
        DimensionContentPackDefinition contentPack,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionContentPackDefinition previous;
      if (!contentPacks.TryGetValue(contentPack.ContentPackId, out previous))
      {
        result = DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered.");
        return false;
      }

      if (!ValidateContentPack(contentPack, out result))
      {
        return false;
      }

      if (ContentPackEquals(previous, contentPack))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      contentPacks[contentPack.ContentPackId] = contentPack;
      RaiseContentPackChanged(
          contentPack,
          previous.Enabled == contentPack.Enabled
              ? DimensionContentPackChangeKind.Updated
              : DimensionContentPackChangeKind.EnabledChanged,
          previous.Enabled,
          contentPack.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetContentPackEnabled(
        string contentPackId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(contentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      if (IsProtectedContentPackId(contentPackId) && !enabled)
      {
        result = DimensionOperationResult.Failed("content-pack-protected", "Built-in content packs cannot be disabled.");
        return false;
      }

      DimensionContentPackDefinition contentPack;
      if (!contentPacks.TryGetValue(contentPackId, out contentPack))
      {
        result = DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered.");
        return false;
      }

      if (contentPack.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionContentPackDefinition updated =
          new DimensionContentPackDefinition(
              contentPack.ContentPackId,
              contentPack.DisplayName,
              contentPack.Version,
              contentPack.Author,
              contentPack.Description,
              contentPack.MinimumApiVersion,
              contentPack.DependencyIds,
              enabled);

      contentPacks[contentPackId] = updated;
      RaiseContentPackChanged(
          updated,
          DimensionContentPackChangeKind.EnabledChanged,
          contentPack.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveContentPack(
        string contentPackId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(contentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      if (IsProtectedContentPackId(contentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-protected", "Built-in content packs cannot be removed.");
        return false;
      }

      DimensionContentPackDefinition contentPack;
      if (!contentPacks.TryGetValue(contentPackId, out contentPack))
      {
        result = DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered.");
        return false;
      }

      contentPacks.Remove(contentPackId);
      RemoveContentOwnershipForContentPack(contentPackId);
      RemoveAssetReferencesForContentPack(contentPackId);
      RaiseContentPackChanged(
          contentPack,
          DimensionContentPackChangeKind.Removed,
          contentPack.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

  }
}
