using System;
using ExpandNullforge.Api;
using System.Collections.Generic;

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

    // Content-pack readiness: dependencies and API versions.

    private DimensionContentPackReadinessResult EvaluateContentPackReadinessInternal(
        DimensionContentPackDefinition contentPack)
    {
      List<string> missingDependencyIds = new List<string>();
      if (contentPack.DependencyIds != null)
      {
        for (int i = 0; i < contentPack.DependencyIds.Count; i++)
        {
          string dependencyId = contentPack.DependencyIds[i];
          DimensionContentPackDefinition dependency;
          if (!contentPacks.TryGetValue(dependencyId, out dependency) ||
              !dependency.Enabled)
          {
            missingDependencyIds.Add(dependencyId);
          }
        }
      }

      missingDependencyIds.Sort(StringComparer.Ordinal);

      // The same question the manifest validation asks, asked the same way. It was
      // "MinimumApiVersion <= CurrentApiVersion" here and ">" there, which agreed by accident and
      // left the bottom of the range unchecked in both.
      bool apiCompatible =
          DimensionApiCompatibility.IsApiVersionSupported(contentPack.MinimumApiVersion);
      if (!contentPack.Enabled)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            false,
            apiCompatible,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            "content-pack-disabled",
            "The content pack is disabled.");
      }

      if (!apiCompatible)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            contentPack.Enabled,
            false,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            contentPack.MinimumApiVersion > DimensionApi.CurrentApiVersion
                ? "content-pack-api-too-new"
                : "content-pack-api-not-supported",
            contentPack.MinimumApiVersion > DimensionApi.CurrentApiVersion
                ? "The content pack requires a newer Dimension API version."
                : "The content pack declares a Dimension API version this build does not support.");
      }

      if (missingDependencyIds.Count > 0)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            contentPack.Enabled,
            true,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            "content-pack-missing-dependencies",
            "The content pack has missing or disabled dependencies.");
      }

      return new DimensionContentPackReadinessResult(
          contentPack.ContentPackId,
          true,
          true,
          contentPack.Enabled,
          true,
          contentPack.MinimumApiVersion,
          DimensionApi.CurrentApiVersion,
          missingDependencyIds,
          string.Empty,
          string.Empty);
    }
  }
}
