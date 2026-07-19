using System;
using ExpandNullforge.Api;
using Unity.Entities;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateProgressFlag(
        DimensionProgressFlag flag,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(flag.FlagId))
      {
        result = DimensionOperationResult.Failed("progress-flag-id-empty", "A progress flag id is required.");
        return false;
      }

      if (!string.IsNullOrEmpty(flag.DimensionId))
      {
        DimensionDefinition dimension;
        if (!TryGetDimension(flag.DimensionId, out dimension))
        {
          result = DimensionOperationResult.Failed("progress-flag-dimension-not-found", "The progress flag dimension is not registered.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareProgressFlags(
        DimensionProgressFlag left,
        DimensionProgressFlag right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int category = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
      if (category != 0)
      {
        return category;
      }

      return string.Compare(left.FlagId, right.FlagId, StringComparison.Ordinal);
    }

    private bool ProgressFlagEquals(
        DimensionProgressFlag a,
        DimensionProgressFlag b,
        bool includeUpdatedTimestamp)
    {
      return string.Equals(a.FlagId, b.FlagId, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.Category, b.Category, StringComparison.Ordinal) &&
             a.Value == b.Value &&
             (!includeUpdatedTimestamp || a.UpdatedUtcTicks == b.UpdatedUtcTicks);
    }


    private World GetBestAvailableWorld()
    {
      if (serverWorld != null && serverWorld.IsCreated)
      {
        return serverWorld;
      }

      if (clientWorld != null && clientWorld.IsCreated)
      {
        return clientWorld;
      }

      return null;
    }
  }
}
