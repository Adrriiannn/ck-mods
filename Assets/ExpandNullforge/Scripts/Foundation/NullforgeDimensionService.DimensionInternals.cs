using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool BoundsContain(DimensionBounds outer, DimensionBounds inner)
    {
      return inner.Min.x >= outer.Min.x &&
             inner.Min.y >= outer.Min.y &&
             inner.MaxExclusive.x <= outer.MaxExclusive.x &&
             inner.MaxExclusive.y <= outer.MaxExclusive.y;
    }

    private bool BoundsOverlap(DimensionBounds a, DimensionBounds b)
    {
      return a.Min.x < b.MaxExclusive.x &&
             a.MaxExclusive.x > b.Min.x &&
             a.Min.y < b.MaxExclusive.y &&
             a.MaxExclusive.y > b.Min.y;
    }

    private bool BoundsEqual(DimensionBounds a, DimensionBounds b)
    {
      return a.Min.x == b.Min.x &&
             a.Min.y == b.Min.y &&
             a.MaxExclusive.x == b.MaxExclusive.x &&
             a.MaxExclusive.y == b.MaxExclusive.y;
    }

    private bool DimensionDefinitionEquals(
        DimensionDefinition a,
        DimensionDefinition b)
    {
      return string.Equals(a.Id, b.Id, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             math.all(a.AbsoluteOrigin == b.AbsoluteOrigin) &&
             BoundsEqual(a.LocalBounds, b.LocalBounds) &&
             a.GenerationVersion == b.GenerationVersion &&
             a.SpaceKind == b.SpaceKind &&
             a.Capabilities == b.Capabilities &&
             a.LifecycleState == b.LifecycleState;
    }
  }
}
