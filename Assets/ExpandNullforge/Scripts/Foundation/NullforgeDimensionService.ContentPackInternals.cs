using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static bool ContentPackEquals(
        DimensionContentPackDefinition a,
        DimensionContentPackDefinition b)
    {
      if (!string.Equals(a.ContentPackId, b.ContentPackId, StringComparison.Ordinal) ||
          !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) ||
          !string.Equals(a.Version, b.Version, StringComparison.Ordinal) ||
          !string.Equals(a.Author, b.Author, StringComparison.Ordinal) ||
          !string.Equals(a.Description, b.Description, StringComparison.Ordinal) ||
          a.MinimumApiVersion != b.MinimumApiVersion ||
          a.Enabled != b.Enabled)
      {
        return false;
      }

      int aCount = a.DependencyIds == null ? 0 : a.DependencyIds.Count;
      int bCount = b.DependencyIds == null ? 0 : b.DependencyIds.Count;
      if (aCount != bCount)
      {
        return false;
      }

      for (int i = 0; i < aCount; i++)
      {
        if (!string.Equals(a.DependencyIds[i], b.DependencyIds[i], StringComparison.Ordinal))
        {
          return false;
        }
      }

      return true;
    }
  }
}
