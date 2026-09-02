using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool PortalDefinitionRouteEquals(
        DimensionPortalDefinition a,
        DimensionPortalDefinition b)
    {
      return string.Equals(a.PortalId, b.PortalId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.FromDimensionId, b.FromDimensionId, StringComparison.Ordinal) &&
             a.FromLocalPosition.x == b.FromLocalPosition.x &&
             a.FromLocalPosition.y == b.FromLocalPosition.y &&
             string.Equals(a.ToDimensionId, b.ToDimensionId, StringComparison.Ordinal) &&
             a.ToLocalPosition.x == b.ToLocalPosition.x &&
             a.ToLocalPosition.y == b.ToLocalPosition.y;
    }

    private bool PortalDefinitionEquals(
        DimensionPortalDefinition a,
        DimensionPortalDefinition b)
    {
      return PortalDefinitionRouteEquals(a, b) &&
             a.State == b.State;
    }

    private bool IsValidPortalState(DimensionPortalState state)
    {
      return state == DimensionPortalState.Locked ||
             state == DimensionPortalState.Available ||
             state == DimensionPortalState.LoadingDestination ||
             state == DimensionPortalState.Disabled ||
             state == DimensionPortalState.Error;
    }
  }
}
