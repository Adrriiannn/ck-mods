using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public static class DimensionSlotAllocator
  {
    private const int ProtectedOverworldCoordinateRadiusTiles = 5000;

    // Default radial allocation: dimensions start 5000 tiles from the global origin and each
    // completed ring steps out by another 5000. Used when the request does not override them.
    private const int DefaultRingRadiusTiles = 5000;
    private const int DefaultRingStepTiles = 5000;

    // Deterministic 8-direction order around global (0,0): N, NE, E, SE, S, SW, W, NW. Each
    // unit direction is scaled by the current ring radius to produce the candidate origin, so
    // the first ever slot is (0, 5000) (due north) and collisions fall through to the next.
    // Core Keeper world axes: +X = east, +Y = north (origin at the lower-left corner).
    private static readonly int2[] RingDirections =
    {
        new int2(0, 1),   // north
        new int2(1, 1),   // north-east
        new int2(1, 0),   // east
        new int2(1, -1),  // south-east
        new int2(0, -1),  // south
        new int2(-1, -1), // south-west
        new int2(-1, 0),  // west
        new int2(-1, 1),  // north-west
    };

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

      int baseOffset = request.BaseOffsetTiles > 0
          ? request.BaseOffsetTiles
          : DefaultRingRadiusTiles;
      int step = request.StepTiles > 0
          ? request.StepTiles
          : DefaultRingStepTiles;
      int maximumRings = math.max(1, request.MaximumSearchRings);
      int candidateIndex = 0;

      for (int ring = 1; ring <= maximumRings; ring++)
      {
        int radius = baseOffset + ((ring - 1) * step);
        for (int direction = 0; direction < RingDirections.Length; direction++)
        {
          int2 origin = RingDirections[direction] * radius;
          if (TryCandidate(
              request,
              origin,
              safetyMargin,
              existingDimensions,
              existingSlots,
              candidateIndex++,
              out DimensionSlotAllocationResult result))
          {
            return result;
          }
        }
      }

      return DimensionSlotAllocationResult.Failed(
          "auto-slot-exhausted",
          "No non-overlapping dimension slot was found in the configured search range.");
    }

    /// <summary>
    /// Decides whether a dimension may change its local bounds while staying at its current
    /// origin. Growth is checked against the same neighbours and the same protected overworld
    /// band that <see cref="Allocate"/> uses, so a resize can never quietly produce an overlap
    /// that allocation would have rejected. A shrink is allowed but reports that it drops tiles
    /// which exist today, since the caller has to warn before destroying built content.
    /// </summary>
    public static DimensionSlotResizeResult ValidateResize(
        string dimensionId,
        int2 absoluteOrigin,
        DimensionBounds currentLocalBounds,
        DimensionBounds proposedLocalBounds,
        int safetyMarginTiles,
        IEnumerable<DimensionDefinition> existingDimensions,
        IEnumerable<DimensionSlotRecord> existingSlots)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        return DimensionSlotResizeResult.Failed(
            "dimension-id-empty",
            "A dimension id is required before its slot can be resized.",
            string.Empty,
            false);
      }

      if (string.Equals(dimensionId, DimensionIds.Overworld, System.StringComparison.Ordinal))
      {
        return DimensionSlotResizeResult.Failed(
            "overworld-slot-not-resizable",
            "The overworld uses the game's native coordinate space and cannot be resized.",
            string.Empty,
            false);
      }

      if (!IsValidBounds(proposedLocalBounds))
      {
        return DimensionSlotResizeResult.Failed(
            "local-bounds-invalid",
            "Dimension local bounds must have a positive width and height.",
            string.Empty,
            false);
      }

      int safetyMargin = math.max(0, safetyMarginTiles);
      bool discardsContent =
          IsValidBounds(currentLocalBounds) && !Contains(proposedLocalBounds, currentLocalBounds);

      if (ConflictsWithExisting(
          dimensionId,
          absoluteOrigin,
          proposedLocalBounds,
          safetyMargin,
          existingDimensions,
          existingSlots,
          out string conflictingDimensionId))
      {
        bool overworld = string.IsNullOrEmpty(conflictingDimensionId);
        return DimensionSlotResizeResult.Failed(
            overworld ? "resize-overworld-protected" : "resize-conflict",
            overworld
                ? "The resized dimension would reach into the protected overworld coordinate band."
                : "The resized dimension would overlap '" + conflictingDimensionId +
                  "'. Move it to a new slot or reduce the size.",
            conflictingDimensionId,
            true);
      }

      if (AreEqual(currentLocalBounds, proposedLocalBounds))
      {
        return DimensionSlotResizeResult.Success(
            "resize-noop",
            "The dimension already uses these bounds.",
            false);
      }

      return DimensionSlotResizeResult.Success(
          discardsContent ? "resize-shrink-discards" : "ok",
          discardsContent
              ? "Accepted, but the new bounds no longer cover every tile of the old bounds; " +
                "anything outside them will be lost."
              : "Resized dimension slot accepted.",
          discardsContent);
    }

    /// <summary>
    /// Splits stored slot records into the ones still backed by a live dimension and the ones
    /// whose dimension is gone. Removing a dimension frees its slot immediately, but the record
    /// is only dropped here — at the next boot — so a world that is mid-save is never left
    /// pointing at a slot that has already been handed to something else. Freed slots become
    /// available to <see cref="Allocate"/> again.
    /// </summary>
    /// <returns>The number of freed (orphaned) records.</returns>
    public static int PruneOrphanedSlots(
        IEnumerable<DimensionSlotRecord> slots,
        IEnumerable<string> liveDimensionIds,
        List<DimensionSlotRecord> retained,
        List<DimensionSlotRecord> freed)
    {
      retained?.Clear();
      freed?.Clear();

      if (slots == null)
      {
        return 0;
      }

      HashSet<string> live =
          new HashSet<string>(System.StringComparer.Ordinal);
      if (liveDimensionIds != null)
      {
        foreach (string id in liveDimensionIds)
        {
          if (!string.IsNullOrEmpty(id))
          {
            live.Add(id);
          }
        }
      }

      int freedCount = 0;
      HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
      foreach (DimensionSlotRecord slot in slots)
      {
        // A record with no id, or a duplicate of one already kept, can never be resolved back to
        // a dimension; treat it as orphaned so the coordinate space does not leak.
        bool orphaned =
            string.IsNullOrEmpty(slot.DimensionId) ||
            !live.Contains(slot.DimensionId) ||
            !seen.Add(slot.DimensionId);

        if (orphaned)
        {
          freedCount++;
          freed?.Add(slot);
        }
        else
        {
          retained?.Add(slot);
        }
      }

      return freedCount;
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
      return ConflictsWithExisting(
          dimensionId,
          absoluteOrigin,
          localBounds,
          safetyMargin,
          existingDimensions,
          existingSlots,
          out _);
    }

    /// <summary>
    /// As above, but names the neighbour that blocked the placement so a resize can tell the
    /// creator which dimension is in the way. An empty id means the protected overworld band.
    /// </summary>
    private static bool ConflictsWithExisting(
        string dimensionId,
        int2 absoluteOrigin,
        DimensionBounds localBounds,
        int safetyMargin,
        IEnumerable<DimensionDefinition> existingDimensions,
        IEnumerable<DimensionSlotRecord> existingSlots,
        out string conflictingDimensionId)
    {
      conflictingDimensionId = string.Empty;

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

      if (ConflictsWithExistingDimensions(
          dimensionId, proposed, safetyMargin, existingDimensions, out conflictingDimensionId))
      {
        return true;
      }

      return ConflictsWithExistingSlots(
          dimensionId, proposed, safetyMargin, existingSlots, out conflictingDimensionId);
    }

    private static bool ConflictsWithExistingDimensions(
        string dimensionId,
        DimensionBounds proposed,
        int safetyMargin,
        IEnumerable<DimensionDefinition> existingDimensions,
        out string conflictingDimensionId)
    {
      conflictingDimensionId = string.Empty;
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

        // By ID, never by type: the old type check let ANY dimension claiming the
        // Overworld kind exempt itself from overlap conflicts — a silent stacking bug
        // an author could ship without knowing.
        if (string.Equals(existing.Id, DimensionIds.Overworld, System.StringComparison.Ordinal))
        {
          continue;
        }

        DimensionBounds existingBounds =
            ExpandBounds(existing.AbsoluteBounds, safetyMargin);
        if (Overlaps(proposed, existingBounds))
        {
          conflictingDimensionId = existing.Id ?? string.Empty;
          return true;
        }
      }

      return false;
    }

    private static bool ConflictsWithExistingSlots(
        string dimensionId,
        DimensionBounds proposed,
        int safetyMargin,
        IEnumerable<DimensionSlotRecord> existingSlots,
        out string conflictingDimensionId)
    {
      conflictingDimensionId = string.Empty;
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
          conflictingDimensionId = existing.DimensionId;
          return true;
        }
      }

      return false;
    }

    /// <summary>True when <paramref name="outer"/> fully covers <paramref name="inner"/>.</summary>
    private static bool Contains(
        DimensionBounds outer,
        DimensionBounds inner)
    {
      return outer.Min.x <= inner.Min.x &&
             outer.Min.y <= inner.Min.y &&
             outer.MaxExclusive.x >= inner.MaxExclusive.x &&
             outer.MaxExclusive.y >= inner.MaxExclusive.y;
    }

    private static bool AreEqual(
        DimensionBounds left,
        DimensionBounds right)
    {
      return left.Min.x == right.Min.x &&
             left.Min.y == right.Min.y &&
             left.MaxExclusive.x == right.MaxExclusive.x &&
             left.MaxExclusive.y == right.MaxExclusive.y;
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
