using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public static class DimensionSlotAllocator
  {
    private const int ProtectedOverworldCoordinateRadiusTiles = 5000;

    private static readonly DimensionBounds ProtectedOverworldCoordinateBounds =
        new DimensionBounds(
            new int2(-ProtectedOverworldCoordinateRadiusTiles, -ProtectedOverworldCoordinateRadiusTiles),
            new int2(ProtectedOverworldCoordinateRadiusTiles, ProtectedOverworldCoordinateRadiusTiles));

    public static DimensionSlotAllocationResult Allocate(
        DimensionSlotAllocationRequest request,
        IEnumerable<DimensionDefinition> existingDimensions)
    {
      return Allocate(request, existingDimensions, null);
    }

    public static DimensionSlotAllocationResult Allocate(
        DimensionSlotAllocationRequest request,
        IEnumerable<DimensionDefinition> existingDimensions,
        IEnumerable<DimensionSlotRecord> existingSlots)
    {
      if (string.IsNullOrEmpty(request.DimensionId))
      {
        return DimensionSlotAllocationResult.Failed(
            "dimension-id-empty",
            "A dimension id is required before a coordinate slot can be allocated.");
      }

      if (string.Equals(request.DimensionId, DimensionIds.Overworld, System.StringComparison.Ordinal))
      {
        return DimensionSlotAllocationResult.Failed(
            "overworld-slot-not-allocatable",
            "The overworld uses the game's native coordinate space and cannot reserve a custom dimension slot.");
      }

      if (!IsValidBounds(request.LocalBounds))
      {
        return DimensionSlotAllocationResult.Failed(
            "local-bounds-invalid",
            "Dimension local bounds must have a positive width and height.");
      }

      int safetyMargin = math.max(0, request.SafetyMarginTiles);

      if (request.UseFixedAbsoluteOrigin)
      {
        if (ConflictsWithExisting(
            request.DimensionId,
            request.FixedAbsoluteOrigin,
            request.LocalBounds,
            safetyMargin,
            existingDimensions,
            existingSlots))
        {
          return DimensionSlotAllocationResult.Failed(
              "fixed-slot-conflict",
              "The requested fixed dimension slot overlaps an existing dimension.");
        }

        return DimensionSlotAllocationResult.Success(
            request.FixedAbsoluteOrigin,
            0,
            true,
            "Fixed dimension slot accepted.");
      }

      int baseOffset = math.max(1, request.BaseOffsetTiles);
      int step = math.max(baseOffset, request.StepTiles);
      int maximumRings = math.max(1, request.MaximumSearchRings);
      int candidateIndex = 0;

      for (int ring = 1; ring <= maximumRings; ring++)
      {
        int coordinate = baseOffset + ((ring - 1) * step);
        if (TryCandidate(
            request,
            new int2(coordinate, coordinate),
            safetyMargin,
            existingDimensions,
            existingSlots,
            candidateIndex++,
            out DimensionSlotAllocationResult result))
        {
          return result;
        }

        if (TryCandidate(
            request,
            new int2(coordinate, -coordinate),
            safetyMargin,
            existingDimensions,
            existingSlots,
            candidateIndex++,
            out result))
        {
          return result;
        }

        if (TryCandidate(
            request,
            new int2(-coordinate, coordinate),
            safetyMargin,
            existingDimensions,
            existingSlots,
            candidateIndex++,
            out result))
        {
          return result;
        }

        if (TryCandidate(
            request,
            new int2(-coordinate, -coordinate),
            safetyMargin,
            existingDimensions,
            existingSlots,
            candidateIndex++,
            out result))
        {
          return result;
        }
      }

      return DimensionSlotAllocationResult.Failed(
          "auto-slot-exhausted",
          "No non-overlapping dimension slot was found in the configured search range.");
    }

    private static bool TryCandidate(
        DimensionSlotAllocationRequest request,
        int2 absoluteOrigin,
        int safetyMargin,
        IEnumerable<DimensionDefinition> existingDimensions,
        IEnumerable<DimensionSlotRecord> existingSlots,
        int candidateIndex,
        out DimensionSlotAllocationResult result)
    {
      if (ConflictsWithExisting(
          request.DimensionId,
          absoluteOrigin,
          request.LocalBounds,
          safetyMargin,
          existingDimensions,
          existingSlots))
      {
        result = default;
        return false;
      }

      result =
          DimensionSlotAllocationResult.Success(
              absoluteOrigin,
              candidateIndex,
              false,
              "Automatic dimension slot allocated.");
      return true;
    }

    private static bool ConflictsWithExisting(
        string dimensionId,
        int2 absoluteOrigin,
        DimensionBounds localBounds,
        int safetyMargin,
        IEnumerable<DimensionDefinition> existingDimensions,
        IEnumerable<DimensionSlotRecord> existingSlots)
    {
      DimensionBounds proposed =
          ExpandBounds(
              new DimensionBounds(
                  absoluteOrigin + localBounds.Min,
                  absoluteOrigin + localBounds.MaxExclusive),
              safetyMargin);

      if (Overlaps(proposed, ProtectedOverworldCoordinateBounds))
      {
        return true;
      }

      if (ConflictsWithExistingDimensions(dimensionId, proposed, safetyMargin, existingDimensions))
      {
        return true;
      }

      return ConflictsWithExistingSlots(dimensionId, proposed, safetyMargin, existingSlots);
    }

    private static bool ConflictsWithExistingDimensions(
        string dimensionId,
        DimensionBounds proposed,
        int safetyMargin,
        IEnumerable<DimensionDefinition> existingDimensions)
    {
      if (existingDimensions == null)
      {
        return false;
      }

      foreach (DimensionDefinition existing in existingDimensions)
      {
        if (string.Equals(existing.Id, dimensionId, System.StringComparison.Ordinal))
        {
          continue;
        }

        if (existing.SpaceKind == DimensionSpaceKind.Overworld)
        {
          continue;
        }

        DimensionBounds existingBounds =
            ExpandBounds(existing.AbsoluteBounds, safetyMargin);
        if (Overlaps(proposed, existingBounds))
        {
          return true;
        }
      }

      return false;
    }

    private static bool ConflictsWithExistingSlots(
        string dimensionId,
        DimensionBounds proposed,
        int safetyMargin,
        IEnumerable<DimensionSlotRecord> existingSlots)
    {
      if (existingSlots == null)
      {
        return false;
      }

      foreach (DimensionSlotRecord existing in existingSlots)
      {
        if (string.Equals(existing.DimensionId, dimensionId, System.StringComparison.Ordinal))
        {
          continue;
        }

        if (string.IsNullOrEmpty(existing.DimensionId))
        {
          continue;
        }

        DimensionBounds existingBounds =
            ExpandBounds(existing.AbsoluteBounds, safetyMargin);
        if (Overlaps(proposed, existingBounds))
        {
          return true;
        }
      }

      return false;
    }

    private static bool IsValidBounds(DimensionBounds bounds)
    {
      int2 size = bounds.Size;
      return size.x > 0 && size.y > 0;
    }

    private static DimensionBounds ExpandBounds(
        DimensionBounds bounds,
        int margin)
    {
      if (margin <= 0)
      {
        return bounds;
      }

      int2 offset = new int2(margin, margin);
      return new DimensionBounds(bounds.Min - offset, bounds.MaxExclusive + offset);
    }

    private static bool Overlaps(
        DimensionBounds left,
        DimensionBounds right)
    {
      return left.Min.x < right.MaxExclusive.x &&
             left.MaxExclusive.x > right.Min.x &&
             left.Min.y < right.MaxExclusive.y &&
             left.MaxExclusive.y > right.Min.y;
    }
  }
}
