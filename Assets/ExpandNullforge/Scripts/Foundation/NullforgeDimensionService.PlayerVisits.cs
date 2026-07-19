using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionPlayerVisitRecord> GetPlayerVisits(string playerId)
    {
      DimensionWorldRegistry.GetPlayerVisits(playerId, persistedPlayerVisits);
      return new List<DimensionPlayerVisitRecord>(persistedPlayerVisits);
    }

    public bool TryGetPlayerVisit(
        string playerId,
        string dimensionId,
        out DimensionPlayerVisitRecord visit)
    {
      return DimensionWorldRegistry.TryGetPlayerVisit(playerId, dimensionId, out visit);
    }

    public bool TryUpsertPlayerVisit(
        string playerId,
        string dimensionId,
        float2 localPosition,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(playerId))
      {
        result = DimensionOperationResult.Failed("player-id-empty", "A player id is required.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(dimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("player-visit-dimension-not-found", "The player visit dimension is not registered.");
        return false;
      }

      if (dimension.Id != DimensionIds.Overworld && !dimension.ContainsLocal(localPosition))
      {
        result = DimensionOperationResult.Failed("player-visit-local-position-out-of-dimension", "The player visit local position is outside the target dimension.");
        return false;
      }

      DimensionPlayerVisitRecord previous;
      bool hadPrevious =
          DimensionWorldRegistry.TryGetPlayerVisit(playerId, dimension.Id, out previous);

      float2 absolutePosition = dimension.ToAbsolute(localPosition);
      DimensionWorldRegistry.UpsertPlayerVisit(
          playerId,
          dimension.Id,
          localPosition,
          absolutePosition);

      DimensionPlayerVisitRecord current;
      if (!DimensionWorldRegistry.TryGetPlayerVisit(playerId, dimension.Id, out current))
      {
        current =
            new DimensionPlayerVisitRecord(
                playerId,
                dimension.Id,
                localPosition,
                absolutePosition,
                DateTime.UtcNow.Ticks);
      }

      RaisePlayerVisitChanged(
          hadPrevious ? previous : default(DimensionPlayerVisitRecord),
          current,
          DimensionPlayerVisitChangeKind.Upserted,
          reason ?? string.Empty);

      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemovePlayerVisit(
        string playerId,
        string dimensionId,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(playerId))
      {
        result = DimensionOperationResult.Failed("player-id-empty", "A player id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(dimensionId))
      {
        result = DimensionOperationResult.Failed("player-visit-dimension-empty", "A player visit dimension id is required.");
        return false;
      }

      DimensionPlayerVisitRecord previous;
      if (!DimensionWorldRegistry.TryGetPlayerVisit(playerId, dimensionId, out previous))
      {
        result = DimensionOperationResult.Failed("player-visit-not-found", "No player visit is stored for that player and dimension.");
        return false;
      }

      DimensionWorldRegistry.RemovePlayerVisit(playerId, dimensionId);
      RaisePlayerVisitChanged(
          previous,
          default(DimensionPlayerVisitRecord),
          DimensionPlayerVisitChangeKind.Removed,
          reason ?? string.Empty);

      result = DimensionOperationResult.Ok();
      return true;
    }

    public DimensionReturnTargetResult ResolveReturnTarget(DimensionReturnTargetRequest request)
    {
      DimensionDefinition dimension;
      if (!TryGetDimension(request.TargetDimensionId, out dimension))
      {
        return DimensionReturnTargetResult.Failed(
            "return-target-dimension-not-found",
            "The return target dimension is not registered.");
      }

      if (request.PreferPlayerVisit && !string.IsNullOrEmpty(request.PlayerId))
      {
        DimensionPlayerVisitRecord visit;
        DimensionReturnTargetResult visitResult;
        if (DimensionWorldRegistry.TryGetPlayerVisit(
                request.PlayerId,
                dimension.Id,
                out visit) &&
            TryCreateReturnTargetResult(
                dimension,
                visit.LocalPosition,
                true,
                string.Empty,
                request.RequireGeneratedArea,
                out visitResult))
        {
          return visitResult;
        }
      }

      if (request.AllowReturnAnchorFallback)
      {
        DimensionAnchorDefinition anchor;
        DimensionReturnTargetResult anchorResult;
        if (TryResolveBestAnchor(dimension.Id, DimensionAnchorKind.Return, out anchor) &&
            TryCreateReturnTargetResult(
                dimension,
                anchor.LocalPosition,
                false,
                anchor.AnchorId,
                request.RequireGeneratedArea,
                out anchorResult))
        {
          return anchorResult;
        }
      }

      if (request.AllowEntryAnchorFallback)
      {
        DimensionAnchorDefinition anchor;
        DimensionReturnTargetResult anchorResult;
        if (TryResolveBestAnchor(dimension.Id, DimensionAnchorKind.Entry, out anchor) &&
            TryCreateReturnTargetResult(
                dimension,
                anchor.LocalPosition,
                false,
                anchor.AnchorId,
                request.RequireGeneratedArea,
                out anchorResult))
        {
          return anchorResult;
        }
      }

      return DimensionReturnTargetResult.Failed(
          "return-target-not-found",
          "No usable visit, return anchor, or entry anchor could be resolved.");
    }

    private bool TryCreateReturnTargetResult(
        DimensionDefinition dimension,
        float2 localPosition,
        bool usedPlayerVisit,
        string anchorId,
        bool requireGeneratedArea,
        out DimensionReturnTargetResult result)
    {
      if (dimension.Id != DimensionIds.Overworld && !dimension.ContainsLocal(localPosition))
      {
        result = DimensionReturnTargetResult.Failed(
            "return-target-out-of-dimension",
            "The return target is outside the target dimension.");
        return false;
      }

      if (requireGeneratedArea && !IsAreaGenerated(dimension.Id, LocalTileAreaAround(localPosition)))
      {
        result = DimensionReturnTargetResult.Failed(
            "return-target-not-generated",
            "The return target area is not generated.");
        return false;
      }

      float2 absolutePosition = dimension.ToAbsolute(localPosition);
      result =
          new DimensionReturnTargetResult(
              true,
              "ok",
              "Return target resolved.",
              dimension.Id,
              localPosition,
              absolutePosition,
              usedPlayerVisit,
              anchorId);
      return true;
    }
  }
}
