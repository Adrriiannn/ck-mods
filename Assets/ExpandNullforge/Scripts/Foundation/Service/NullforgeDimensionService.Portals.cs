using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionPortalDefinition> GetPortals(string dimensionId)
    {
      List<DimensionPortalDefinition> result = new List<DimensionPortalDefinition>();
      foreach (DimensionPortalDefinition portal in portals.Values)
      {
        if (string.IsNullOrEmpty(dimensionId)
            || string.Equals(portal.FromDimensionId, dimensionId, StringComparison.Ordinal)
            || string.Equals(portal.ToDimensionId, dimensionId, StringComparison.Ordinal))
        {
          result.Add(portal);
        }
      }

      return result;
    }

    public bool TryGetPortal(string portalId, out DimensionPortalDefinition portal)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        portal = default(DimensionPortalDefinition);
        return false;
      }

      return portals.TryGetValue(portalId, out portal);
    }

    public bool TryRegisterPortal(DimensionPortalDefinition portal, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(portal.PortalId))
      {
        result = DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
        return false;
      }

      if (portals.ContainsKey(portal.PortalId))
      {
        result = DimensionOperationResult.Failed("portal-already-registered", "A portal with that id is already registered.");
        return false;
      }

      DimensionDefinition from;
      DimensionDefinition to;
      if (!TryGetDimension(portal.FromDimensionId, out from) || !TryGetDimension(portal.ToDimensionId, out to))
      {
        result = DimensionOperationResult.Failed("portal-dimension-not-found", "Both portal dimensions must be registered.");
        return false;
      }

      if (!from.ContainsLocal(portal.FromLocalPosition) || !to.ContainsLocal(portal.ToLocalPosition))
      {
        result = DimensionOperationResult.Failed("portal-position-out-of-bounds", "Portal positions must be inside their dimensions.");
        return false;
      }

      portals[portal.PortalId] = portal;
      PersistPortalIfWorldRegistryLoaded(portal);
      RaisePortalChanged(
          portal,
          DimensionPortalState.Unknown,
          portal.State,
          DimensionPortalChangeKind.Registered,
          "registered");
      AddDiagnostic(DimensionDiagnosticSeverity.Info, portal.FromDimensionId, "Portal registered: " + portal.PortalId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetPortalState(
        string portalId,
        DimensionPortalState state,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        result = DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
        return false;
      }

      if (!IsValidPortalState(state))
      {
        result = DimensionOperationResult.Failed("portal-state-invalid", "The portal state is not valid.");
        return false;
      }

      DimensionPortalDefinition portal;
      if (!portals.TryGetValue(portalId, out portal))
      {
        result = DimensionOperationResult.Failed("portal-not-found", "No portal with that id is registered.");
        return false;
      }

      if (portal.State == state)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionPortalDefinition updated =
          new DimensionPortalDefinition(
              portal.PortalId,
              portal.DisplayName,
              portal.FromDimensionId,
              portal.FromLocalPosition,
              portal.ToDimensionId,
              portal.ToLocalPosition,
              state);
      portals[portalId] = updated;
      PersistPortalIfWorldRegistryLoaded(updated);
      RaisePortalChanged(
          updated,
          portal.State,
          state,
          DimensionPortalChangeKind.StateChanged,
          reason ?? string.Empty);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, portal.FromDimensionId, "Portal state changed to " + state + ": " + portalId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemovePortal(string portalId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(portalId))
      {
        result = DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
        return false;
      }

      DimensionPortalDefinition portal;
      if (!portals.TryGetValue(portalId, out portal))
      {
        result = DimensionOperationResult.Failed("portal-not-found", "No portal with that id is registered.");
        return false;
      }

      portals.Remove(portalId);
      RemovePersistedPortalIfWorldRegistryLoaded(portalId);
      RemovePortalPresentationsForPortal(portalId);
      RemoveTravelRequirementsForPortal(portalId);
      RaisePortalChanged(
          portal,
          portal.State,
          DimensionPortalState.Disabled,
          DimensionPortalChangeKind.Removed,
          "removed");
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Portal removed: " + portalId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
