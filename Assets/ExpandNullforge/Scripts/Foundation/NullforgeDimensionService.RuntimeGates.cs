using System;
using System.Collections.Generic;
using UnityEngine;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private readonly List<DimensionSlotRecord> dimensionSlotScratch =
        new List<DimensionSlotRecord>(16);

    public bool HasAttachedServerWorld
    {
      get { return serverWorld != null && serverWorld.IsCreated; }
    }

    public bool HasAttachedClientWorld
    {
      get { return clientWorld != null && clientWorld.IsCreated; }
    }

    public bool HasActiveRuntimeLoadingWork
    {
      get { return runtimeLoadRecords.Count > 0; }
    }

    public bool HasActiveRuntimeGenerationWork
    {
      get { return runtimeGenerationRecords.Count > 0; }
    }

    public bool HasActiveRuntimeTravelWork
    {
      get { return pendingTravelByPlayerId.Count > 0; }
    }

    public bool HasActiveRuntimeWork
    {
      get
      {
        return HasActiveRuntimeLoadingWork ||
               HasActiveRuntimeGenerationWork ||
               HasActiveRuntimeTravelWork;
      }
    }

    public bool ShouldFlushPersistence
    {
      get { return HasAttachedServerWorld && DimensionWorldRegistry.HasPendingFlush; }
    }

    public bool ShouldRunRuntimePlayerContextTracking
    {
      get
      {
        if (!HasAttachedServerWorld)
        {
          return false;
        }

        if (HasActiveRuntimeWork || HasTrackedNonOverworldPlayer())
        {
          return true;
        }

        if (!DimensionWorldRegistry.HasNonOverworldFootprint)
        {
          return false;
        }

        return Time.realtimeSinceStartupAsDouble >= nextPlayerContextTrackAt;
      }
    }

    public double RuntimeNow
    {
      get { return Time.realtimeSinceStartupAsDouble; }
    }

    public DimensionSlotAllocationResult AllocateDimensionSlot(
        DimensionSlotAllocationRequest request)
    {
      dimensionSlotScratch.Clear();
      DimensionWorldRegistry.GetDimensionSlots(dimensionSlotScratch);
      return DimensionSlotAllocator.Allocate(
          request,
          definitions.Values,
          dimensionSlotScratch);
    }

    public DimensionSlotAllocationResult ReserveDimensionSlot(
        DimensionSlotAllocationRequest request,
        out DimensionSlotRecord slot)
    {
      slot = default(DimensionSlotRecord);
      if (DimensionWorldRegistry.TryGetDimensionSlot(request.DimensionId, out slot))
      {
        if (BoundsEqual(slot.LocalBounds, request.LocalBounds) &&
            (!request.UseFixedAbsoluteOrigin ||
             math.all(slot.AbsoluteOrigin == request.FixedAbsoluteOrigin)))
        {
          return DimensionSlotAllocationResult.Success(
              slot.AbsoluteOrigin,
              slot.CandidateIndex,
              slot.UsedFixedOrigin,
              "Existing dimension slot reservation reused.");
        }

        DimensionSlotAllocationRequest resizeRequest =
            new DimensionSlotAllocationRequest(
                request.DimensionId,
                request.LocalBounds,
                true,
                slot.AbsoluteOrigin,
                request.BaseOffsetTiles,
                request.StepTiles,
                request.SafetyMarginTiles,
                request.MaximumSearchRings);
        DimensionSlotAllocationResult resizedAllocation = AllocateDimensionSlot(resizeRequest);
        if (resizedAllocation.Accepted)
        {
          slot = CreateSlotRecord(request, resizedAllocation);
          DimensionWorldRegistry.UpsertDimensionSlot(slot);
          return resizedAllocation;
        }
      }

      DimensionSlotAllocationResult allocation = AllocateDimensionSlot(request);
      if (!allocation.Accepted)
      {
        return allocation;
      }

      slot = CreateSlotRecord(request, allocation);
      DimensionWorldRegistry.UpsertDimensionSlot(slot);
      return allocation;
    }

    public bool TryGetReservedDimensionSlot(string dimensionId, out DimensionSlotRecord slot)
    {
      return DimensionWorldRegistry.TryGetDimensionSlot(dimensionId, out slot);
    }

    private static DimensionSlotRecord CreateSlotRecord(
        DimensionSlotAllocationRequest request,
        DimensionSlotAllocationResult allocation)
    {
      return new DimensionSlotRecord(
          request.DimensionId,
          request.LocalBounds,
          allocation.AbsoluteOrigin,
          allocation.CandidateIndex,
          allocation.UsedFixedOrigin,
          DateTime.UtcNow.Ticks,
          allocation.Code,
          allocation.Message);
    }

  }
}
