using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool TryResolveRespawnTargetInDimension(
        string dimensionId,
        DimensionRespawnRequest request,
        out DimensionRespawnTarget target)
    {
      if (TryResolveRespawnAnchorTarget(dimensionId, DimensionAnchorKind.Respawn, out target))
      {
        return true;
      }

      if (request.AllowCheckpointFallback &&
          TryResolveRespawnAnchorTarget(dimensionId, DimensionAnchorKind.Checkpoint, out target))
      {
        return true;
      }

      if (request.AllowEntryFallback &&
          TryResolveRespawnAnchorTarget(dimensionId, DimensionAnchorKind.Entry, out target))
      {
        return true;
      }

      if (request.AllowFallbackAnchors &&
          TryResolveRespawnAnchorTarget(dimensionId, DimensionAnchorKind.Fallback, out target))
      {
        return true;
      }

      target = DimensionRespawnTarget.Failed(
          "respawn-anchor-not-found",
          "No enabled respawn anchor could be resolved for the candidate dimension.");
      return false;
    }

    private bool TryResolveRespawnAnchorTarget(
        string dimensionId,
        DimensionAnchorKind kind,
        out DimensionRespawnTarget target)
    {
      DimensionAnchorDefinition anchor;
      if (!TryResolveBestAnchor(dimensionId, kind, out anchor))
      {
        target = DimensionRespawnTarget.Failed(
            "respawn-anchor-not-found",
            "No enabled respawn anchor could be resolved for the candidate dimension.");
        return false;
      }

      float2 absolutePosition;
      if (!TryToAbsolute(anchor.DimensionId, anchor.LocalPosition, out absolutePosition))
      {
        target = DimensionRespawnTarget.Failed(
            "respawn-anchor-invalid",
            "The resolved respawn anchor is outside its dimension bounds.");
        return false;
      }

      target =
          DimensionRespawnTarget.Resolved(
              anchor.DimensionId,
              anchor.LocalPosition,
              absolutePosition,
              anchor.AnchorId,
              anchor.Kind,
              IsRespawnAnchorGenerated(anchor.DimensionId, anchor.LocalPosition));
      return true;
    }

    private bool IsRespawnAnchorGenerated(string dimensionId, float2 localPosition)
    {
      if (dimensionId == DimensionIds.Overworld)
      {
        return true;
      }

      int2 localTile =
          new int2(
              (int)math.floor(localPosition.x),
              (int)math.floor(localPosition.y));
      return IsAreaGenerated(
          dimensionId,
          new DimensionBounds(localTile, localTile + new int2(1, 1)));
    }

    private static void AddRespawnCandidateDimension(List<string> candidateDimensionIds, string dimensionId)
    {
      if (candidateDimensionIds == null || string.IsNullOrEmpty(dimensionId))
      {
        return;
      }

      for (int i = 0; i < candidateDimensionIds.Count; i++)
      {
        if (string.Equals(candidateDimensionIds[i], dimensionId, StringComparison.Ordinal))
        {
          return;
        }
      }

      candidateDimensionIds.Add(dimensionId);
    }

    private bool TryGetEntityAbsolutePosition(
        Entity entity,
        DimensionEntityWorldScope worldScope,
        out float2 absolutePosition)
    {
      if (entity == Entity.Null)
      {
        absolutePosition = default(float2);
        return false;
      }

      if (worldScope == DimensionEntityWorldScope.ServerOnly)
      {
        return TryGetEntityAbsolutePositionInWorld(serverWorld, entity, out absolutePosition);
      }

      if (worldScope == DimensionEntityWorldScope.ClientOnly)
      {
        return TryGetEntityAbsolutePositionInWorld(clientWorld, entity, out absolutePosition);
      }

      if (TryGetEntityAbsolutePositionInWorld(serverWorld, entity, out absolutePosition))
      {
        return true;
      }

      return TryGetEntityAbsolutePositionInWorld(clientWorld, entity, out absolutePosition);
    }

    private static bool TryGetEntityAbsolutePositionInWorld(
        World world,
        Entity entity,
        out float2 absolutePosition)
    {
      if (world == null || !world.IsCreated || entity == Entity.Null)
      {
        absolutePosition = default(float2);
        return false;
      }

      EntityManager entityManager = world.EntityManager;
      if (!entityManager.Exists(entity) || !entityManager.HasComponent<LocalTransform>(entity))
      {
        absolutePosition = default(float2);
        return false;
      }

      LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
      absolutePosition = new float2(transform.Position.x, transform.Position.z);
      return true;
    }
  }
}