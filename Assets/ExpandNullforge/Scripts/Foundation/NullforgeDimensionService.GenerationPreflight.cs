using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// What a generation request would do, and the plan it would run.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    public DimensionGenerationPreflightResult PreflightGeneration(
        DimensionGenerationPreflightRequest request)
    {
      DimensionDefinition definition;
      bool dimensionExists = TryGetDimension(request.DimensionId, out definition);
      bool hasGenerationCapability =
          dimensionExists &&
          definition.HasCapability(DimensionCapabilityFlags.Generation);

      string boundsError;
      bool boundsValid = IsValidGenerationBounds(request.LocalBounds, out boundsError);

      bool areaInsideDimension = false;
      if (dimensionExists && boundsValid)
      {
        DimensionArea area;
        areaInsideDimension = TryGetArea(request.DimensionId, request.LocalBounds, out area);
      }

      DimensionGenerationStatus status =
          new DimensionGenerationStatus(
              request.DimensionId,
              request.LocalBounds,
              DimensionGenerationState.Unknown,
              0f,
              "Generation preflight has not resolved a status.");
      if (dimensionExists && boundsValid)
      {
        TryGetGenerationStatus(request.DimensionId, request.LocalBounds, out status);
      }

      bool alreadyReady = status.State == DimensionGenerationState.Ready;
      bool activeGeneration = IsTransientGenerationState(status.State);

      DimensionGenerationReservation blockingReservation =
          default(DimensionGenerationReservation);
      bool reservationFree = true;
      if (request.RequireReservationFree && dimensionExists && boundsValid)
      {
        bool allowSameOwner =
            request.AllowReservationOverlapWithSameOwner &&
            !string.IsNullOrEmpty(request.OwnerId);
        reservationFree =
            !TryFindGenerationReservationOverlap(
                request.DimensionId,
                request.LocalBounds,
                request.OwnerId,
                allowSameOwner,
                out blockingReservation);
      }

      if (!dimensionExists)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "dimension-not-found",
            "No dimension with that id is registered.",
            false,
            false,
            boundsValid,
            false,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (request.RequireGenerationCapability && !hasGenerationCapability)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "dimension-generation-disabled",
            "The target dimension does not allow custom generation.",
            dimensionExists,
            hasGenerationCapability,
            boundsValid,
            areaInsideDimension,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (!boundsValid)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-bounds-invalid",
            boundsError,
            dimensionExists,
            hasGenerationCapability,
            false,
            false,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (request.RequireAreaInsideDimension && !areaInsideDimension)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-out-of-bounds",
            "The requested generation area is outside the target dimension bounds.",
            dimensionExists,
            hasGenerationCapability,
            boundsValid,
            areaInsideDimension,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (alreadyReady && !request.AllowReadyArea)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-ready",
            "The requested generation area is already ready.",
            dimensionExists,
            hasGenerationCapability,
            boundsValid,
            areaInsideDimension,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (activeGeneration && !request.AllowActiveGeneration)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-area-active",
            "The requested generation area already has an active generation job.",
            dimensionExists,
            hasGenerationCapability,
            boundsValid,
            areaInsideDimension,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      if (!reservationFree)
      {
        return BuildGenerationPreflightResult(
            request,
            false,
            "generation-reservation-overlap",
            "The requested generation area overlaps " + blockingReservation.ReservationId + ".",
            dimensionExists,
            hasGenerationCapability,
            boundsValid,
            areaInsideDimension,
            alreadyReady,
            activeGeneration,
            reservationFree,
            status,
            blockingReservation);
      }

      return BuildGenerationPreflightResult(
          request,
          true,
          string.Empty,
          "Generation preflight passed.",
          dimensionExists,
          hasGenerationCapability,
          boundsValid,
          areaInsideDimension,
          alreadyReady,
          activeGeneration,
          reservationFree,
          status,
          blockingReservation);
    }

    public DimensionGenerationPreviewResult PreviewGenerationRequest(
        DimensionGenerationPreviewRequest request)
    {
      DimensionGenerationRequest generationRequest = request.GenerationRequest;
      DimensionGenerationPreflightResult areaPreflight =
          PreflightGeneration(
              new DimensionGenerationPreflightRequest(
                  generationRequest.RequesterId,
                  generationRequest.DimensionId,
                  generationRequest.LocalBounds,
                  RuntimeGenerationOwnerId(generationRequest.RequesterId),
                  true,
                  true,
                  request.AllowReadyArea,
                  request.AllowActiveGeneration,
                  false,
                  false,
                  string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));

      DimensionGenerationPlanPreflightResult planPreflight =
          SkippedGenerationPlanPreflight(
              generationRequest.DimensionId,
              generationRequest.LocalBounds);

      if (!areaPreflight.CanProceed)
      {
        return BuildGenerationPreviewResult(
            false,
            areaPreflight.Code,
            areaPreflight.Message,
            areaPreflight,
            planPreflight,
            false,
            false,
            false,
            areaPreflight.Status);
      }

      bool wouldReuseExistingStatus =
          areaPreflight.AlreadyReady ||
          areaPreflight.ActiveGeneration;

      if (!wouldReuseExistingStatus && request.RequireReservationFree)
      {
        areaPreflight =
            PreflightGeneration(
                new DimensionGenerationPreflightRequest(
                    generationRequest.RequesterId,
                    generationRequest.DimensionId,
                    generationRequest.LocalBounds,
                    RuntimeGenerationOwnerId(generationRequest.RequesterId),
                    true,
                    true,
                    request.AllowReadyArea,
                    request.AllowActiveGeneration,
                    true,
                    false,
                    string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));
        if (!areaPreflight.CanProceed)
        {
          return BuildGenerationPreviewResult(
              false,
              areaPreflight.Code,
              areaPreflight.Message,
              areaPreflight,
              planPreflight,
              false,
              false,
              false,
              areaPreflight.Status);
        }
      }

      bool wouldReturnNotGenerated =
          !generationRequest.CreateIfMissing &&
          !wouldReuseExistingStatus &&
          areaPreflight.Status.State == DimensionGenerationState.NotGenerated;
      if (wouldReturnNotGenerated)
      {
        return BuildGenerationPreviewResult(
            true,
            string.Empty,
            "Generation request would return NotGenerated without queueing work.",
            areaPreflight,
            planPreflight,
            false,
            false,
            true,
            areaPreflight.Status);
      }

      if (wouldReuseExistingStatus)
      {
        return BuildGenerationPreviewResult(
            true,
            string.Empty,
            "Generation request would reuse the existing generated-area status.",
            areaPreflight,
            planPreflight,
            false,
            true,
            false,
            areaPreflight.Status);
      }

      if (generationRequest.CreateIfMissing && request.RequireExecutablePlan)
      {
        planPreflight =
            PreflightGenerationPlan(
                new DimensionGenerationPlanPreflightRequest(
                    new DimensionGenerationPlanRequest(
                        generationRequest.DimensionId,
                        generationRequest.LocalBounds,
                        string.Empty,
                        false,
                        false,
                        true),
                    true,
                    true,
                    true,
                    request.AllowFallbackProvider,
                    string.IsNullOrEmpty(request.Reason) ? generationRequest.Reason : request.Reason));
        if (!planPreflight.CanExecute)
        {
          return BuildGenerationPreviewResult(
              false,
              planPreflight.Code,
              planPreflight.Message,
              areaPreflight,
              planPreflight,
              false,
              false,
              false,
              areaPreflight.Status);
        }
      }

      return BuildGenerationPreviewResult(
          true,
          string.Empty,
          generationRequest.CreateIfMissing
              ? "Generation request would queue work."
              : "Generation request is valid and would not queue work.",
          areaPreflight,
          planPreflight,
          generationRequest.CreateIfMissing,
          false,
          false,
          areaPreflight.Status);
    }

    public DimensionGenerationPlan BuildGenerationPlan(DimensionGenerationPlanRequest request)
    {
      DimensionDefinition dimension;
      if (!TryGetDimension(request.DimensionId, out dimension))
      {
        return new DimensionGenerationPlan(
            false,
            "The target dimension is not registered.",
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      string boundsError;
      if (!IsValidGenerationBounds(request.LocalBounds, out boundsError))
      {
        return new DimensionGenerationPlan(
            false,
            boundsError,
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      DimensionArea area;
      if (!TryGetArea(request.DimensionId, request.LocalBounds, out area))
      {
        return new DimensionGenerationPlan(
            false,
            "The requested generation area is outside the target dimension bounds.",
            request.DimensionId,
            request.LocalBounds,
            request.ZoneId,
            new List<DimensionGenerationPassDefinition>());
      }

      string effectiveZoneId = request.ZoneId ?? string.Empty;
      if (request.ResolveZoneFromArea && string.IsNullOrEmpty(effectiveZoneId))
      {
        float2 center =
            new float2(
                (request.LocalBounds.Min.x + request.LocalBounds.MaxExclusive.x) * 0.5f,
                (request.LocalBounds.Min.y + request.LocalBounds.MaxExclusive.y) * 0.5f);
        DimensionZoneDefinition zone;
        if (TryFindZoneDefinitionAtLocal(request.DimensionId, center, out zone))
        {
          effectiveZoneId = zone.ZoneId;
        }
      }

      List<DimensionGenerationPassDefinition> passes =
          new List<DimensionGenerationPassDefinition>();
      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (GenerationPassAppliesToPlan(generationPass, request, effectiveZoneId))
        {
          passes.Add(generationPass);
        }
      }

      passes.Sort(CompareGenerationPasses);
      return new DimensionGenerationPlan(
          true,
          passes.Count == 0
              ? "No generation passes matched the requested area."
              : "Generation plan built.",
          request.DimensionId,
          request.LocalBounds,
          effectiveZoneId,
          passes);
    }

    public DimensionGenerationPlanPreflightResult PreflightGenerationPlan(
        DimensionGenerationPlanPreflightRequest request)
    {
      DimensionGenerationPlanRequest inclusivePlanRequest =
          new DimensionGenerationPlanRequest(
              request.PlanRequest.DimensionId,
              request.PlanRequest.LocalBounds,
              request.PlanRequest.ZoneId,
              true,
              request.PlanRequest.ResolveZoneFromArea,
              false);
      DimensionGenerationPlan plan = BuildGenerationPlan(inclusivePlanRequest);
      if (!plan.Success)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-invalid",
            plan.Message,
            plan,
            0,
            0,
            0,
            0,
            0,
            0,
            false);
      }

      DimensionDefinition definition;
      bool hasDimension = TryGetDimension(plan.DimensionId, out definition);
      int matchingPassCount = plan.Passes == null ? 0 : plan.Passes.Count;
      int enabledPassCount = 0;
      int disabledPassCount = 0;
      int missingProviderCount = 0;
      int providerRejectedPassCount = 0;
      int executablePassCount = 0;

      for (int i = 0; i < matchingPassCount; i++)
      {
        DimensionGenerationPassDefinition generationPass = plan.Passes[i];
        if (!generationPass.Enabled)
        {
          disabledPassCount++;
          continue;
        }

        enabledPassCount++;
        IDimensionGenerationProvider provider;
        if (string.IsNullOrEmpty(generationPass.ProviderId) ||
            !generationProviders.TryGetValue(generationPass.ProviderId, out provider) ||
            provider == null)
        {
          missingProviderCount++;
          continue;
        }

        if (request.RequireProviderCanGenerate &&
            (!hasDimension || !provider.CanGenerate(definition, plan.LocalBounds)))
        {
          providerRejectedPassCount++;
          continue;
        }

        executablePassCount++;
      }

      bool hasFallbackProvider =
          request.AllowFallbackProvider &&
          HasFallbackGenerationProvider(plan.DimensionId, plan.LocalBounds);

      if (request.RequireRegisteredProviders && missingProviderCount > 0)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-missing-provider",
            "One or more matching generation passes reference a missing provider.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      if (request.RequireAnyPass && matchingPassCount <= 0 && !hasFallbackProvider)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-empty",
            "No generation passes or fallback providers matched the requested area.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      if (request.RequireProviderCanGenerate &&
          executablePassCount <= 0 &&
          !hasFallbackProvider)
      {
        return BuildGenerationPlanPreflightResult(
            false,
            "generation-plan-no-executable-provider",
            "No matching generation provider can generate the requested area.",
            plan,
            matchingPassCount,
            enabledPassCount,
            disabledPassCount,
            missingProviderCount,
            providerRejectedPassCount,
            executablePassCount,
            hasFallbackProvider);
      }

      return BuildGenerationPlanPreflightResult(
          true,
          string.Empty,
          "Generation plan preflight passed.",
          plan,
          matchingPassCount,
          enabledPassCount,
          disabledPassCount,
          missingProviderCount,
          providerRejectedPassCount,
          executablePassCount,
          hasFallbackProvider);
    }
  }
}
