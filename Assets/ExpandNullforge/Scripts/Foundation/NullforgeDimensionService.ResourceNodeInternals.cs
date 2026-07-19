using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateResourceNode(
        DimensionResourceNodeDefinition node,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(node.NodeId))
      {
        result = DimensionOperationResult.Failed("resource-node-id-empty", "A resource node id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(node.ResourceId))
      {
        result = DimensionOperationResult.Failed("resource-node-resource-empty", "A resource id is required.");
        return false;
      }

      if (!IsValidResourceNodeKind(node.Kind) || node.Kind == DimensionResourceNodeKind.Any)
      {
        result = DimensionOperationResult.Failed("resource-node-kind-invalid", "The resource node kind is not supported.");
        return false;
      }

      if (node.Weight <= 0)
      {
        result = DimensionOperationResult.Failed("resource-node-weight-invalid", "The resource node weight must be greater than zero.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(node.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("resource-node-dimension-not-found", "The resource node dimension is not registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(node.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(node.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, node.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("resource-node-zone-dimension-mismatch", "The resource node zone belongs to another dimension.");
          return false;
        }
      }

      if (!string.IsNullOrEmpty(node.GenerationPassId))
      {
        DimensionGenerationPassDefinition generationPass;
        if (generationPasses.TryGetValue(node.GenerationPassId, out generationPass) &&
            !string.Equals(generationPass.DimensionId, node.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("resource-node-pass-dimension-mismatch", "The resource node generation pass belongs to another dimension.");
          return false;
        }
      }

      if (node.HasLocalBounds)
      {
        if (node.LocalBounds.Size.x <= 0 || node.LocalBounds.Size.y <= 0)
        {
          result = DimensionOperationResult.Failed("resource-node-bounds-invalid", "The resource node local bounds must have a positive size.");
          return false;
        }

        if (!dimension.LocalBounds.Contains(node.LocalBounds.Min) ||
            !dimension.LocalBounds.Contains(node.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          result = DimensionOperationResult.Failed("resource-node-bounds-out-of-dimension", "The resource node bounds are outside the resource node dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool IsValidResourceNodeKind(DimensionResourceNodeKind kind)
    {
      return kind == DimensionResourceNodeKind.Any ||
             kind == DimensionResourceNodeKind.Ore ||
             kind == DimensionResourceNodeKind.Wall ||
             kind == DimensionResourceNodeKind.Ground ||
             kind == DimensionResourceNodeKind.Liquid ||
             kind == DimensionResourceNodeKind.Object ||
             kind == DimensionResourceNodeKind.Flora ||
             kind == DimensionResourceNodeKind.Container ||
             kind == DimensionResourceNodeKind.Loot ||
             kind == DimensionResourceNodeKind.Custom;
    }

    private static bool ResourceNodeMatchesQuery(
        DimensionResourceNodeDefinition node,
        DimensionResourceNodeQuery query)
    {
      if (query.EnabledOnly && !node.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(node.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.ZoneId) &&
          !string.IsNullOrEmpty(node.ZoneId) &&
          !string.Equals(node.ZoneId, query.ZoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.Kind != DimensionResourceNodeKind.Any && node.Kind != query.Kind)
      {
        return false;
      }

      if (query.HasLocalPosition &&
          node.HasLocalBounds &&
          !node.LocalBounds.Contains(query.LocalPosition))
      {
        return false;
      }

      return true;
    }

    private static int CompareResourceNodes(
        DimensionResourceNodeDefinition left,
        DimensionResourceNodeDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int kind = left.Kind.CompareTo(right.Kind);
      if (kind != 0)
      {
        return kind;
      }

      int weight = right.Weight.CompareTo(left.Weight);
      if (weight != 0)
      {
        return weight;
      }

      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int zone = string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
      if (zone != 0)
      {
        return zone;
      }

      return string.Compare(left.NodeId, right.NodeId, StringComparison.Ordinal);
    }

    private bool ResourceNodeEquals(
        DimensionResourceNodeDefinition a,
        DimensionResourceNodeDefinition b)
    {
      return string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             a.HasLocalBounds == b.HasLocalBounds &&
             (!a.HasLocalBounds || BoundsEqual(a.LocalBounds, b.LocalBounds)) &&
             string.Equals(a.ResourceId, b.ResourceId, StringComparison.Ordinal) &&
             a.Kind == b.Kind &&
             string.Equals(a.ProviderId, b.ProviderId, StringComparison.Ordinal) &&
             string.Equals(a.GenerationPassId, b.GenerationPassId, StringComparison.Ordinal) &&
             a.Weight == b.Weight &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
