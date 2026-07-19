using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionResourceNodeDefinition> GetResourceNodes(
        DimensionResourceNodeQuery query)
    {
      List<DimensionResourceNodeDefinition> result =
          new List<DimensionResourceNodeDefinition>();
      foreach (DimensionResourceNodeDefinition node in resourceNodes.Values)
      {
        if (!ResourceNodeMatchesQuery(node, query))
        {
          continue;
        }

        result.Add(node);
      }

      result.Sort(CompareResourceNodes);
      return result;
    }

    public bool TryGetResourceNode(
        string nodeId,
        out DimensionResourceNodeDefinition node)
    {
      if (string.IsNullOrEmpty(nodeId))
      {
        node = default(DimensionResourceNodeDefinition);
        return false;
      }

      return resourceNodes.TryGetValue(nodeId, out node);
    }

    public bool TryRegisterResourceNode(
        DimensionResourceNodeDefinition node,
        out DimensionOperationResult result)
    {
      if (!ValidateResourceNode(node, out result))
      {
        return false;
      }

      if (resourceNodes.ContainsKey(node.NodeId))
      {
        result = DimensionOperationResult.Failed("resource-node-already-registered", "A resource node with that id is already registered.");
        return false;
      }

      resourceNodes[node.NodeId] = node;
      RaiseResourceNodeChanged(
          node,
          DimensionResourceNodeChangeKind.Registered,
          false,
          node.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateResourceNode(
        DimensionResourceNodeDefinition node,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionResourceNodeDefinition previous;
      if (!resourceNodes.TryGetValue(node.NodeId, out previous))
      {
        result = DimensionOperationResult.Failed("resource-node-not-found", "No resource node with that id is registered.");
        return false;
      }

      if (!ValidateResourceNode(node, out result))
      {
        return false;
      }

      if (ResourceNodeEquals(previous, node))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      resourceNodes[node.NodeId] = node;
      RaiseResourceNodeChanged(
          node,
          previous.Enabled == node.Enabled
              ? DimensionResourceNodeChangeKind.Updated
              : DimensionResourceNodeChangeKind.EnabledChanged,
          previous.Enabled,
          node.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetResourceNodeEnabled(
        string nodeId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(nodeId))
      {
        result = DimensionOperationResult.Failed("resource-node-id-empty", "A resource node id is required.");
        return false;
      }

      DimensionResourceNodeDefinition node;
      if (!resourceNodes.TryGetValue(nodeId, out node))
      {
        result = DimensionOperationResult.Failed("resource-node-not-found", "No resource node with that id is registered.");
        return false;
      }

      if (node.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionResourceNodeDefinition updated =
          new DimensionResourceNodeDefinition(
              node.NodeId,
              node.DisplayName,
              node.DimensionId,
              node.ZoneId,
              node.HasLocalBounds,
              node.LocalBounds,
              node.ResourceId,
              node.Kind,
              node.ProviderId,
              node.GenerationPassId,
              node.Weight,
              node.Priority,
              enabled);

      resourceNodes[nodeId] = updated;
      RaiseResourceNodeChanged(
          updated,
          DimensionResourceNodeChangeKind.EnabledChanged,
          node.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveResourceNode(
        string nodeId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(nodeId))
      {
        result = DimensionOperationResult.Failed("resource-node-id-empty", "A resource node id is required.");
        return false;
      }

      DimensionResourceNodeDefinition node;
      if (!resourceNodes.TryGetValue(nodeId, out node))
      {
        result = DimensionOperationResult.Failed("resource-node-not-found", "No resource node with that id is registered.");
        return false;
      }

      resourceNodes.Remove(nodeId);
      RaiseResourceNodeChanged(
          node,
          DimensionResourceNodeChangeKind.Removed,
          node.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
