using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateZoneDefinition(
        DimensionZoneDefinition zone,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(zone.ZoneId))
      {
        result = DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
        return false;
      }

      if (zone.LocalBounds.Size.x <= 0 || zone.LocalBounds.Size.y <= 0)
      {
        result = DimensionOperationResult.Failed("zone-bounds-invalid", "The zone local bounds must have a positive size.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(zone.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered.");
        return false;
      }

      if (!dimension.LocalBounds.Contains(zone.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(zone.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        result = DimensionOperationResult.Failed("zone-bounds-out-of-dimension", "The zone bounds are outside the zone dimension.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool ValidateEnvironmentProfile(
        DimensionEnvironmentProfile profile,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(profile.ProfileId))
      {
        result = DimensionOperationResult.Failed("environment-profile-id-empty", "An environment profile id is required.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(profile.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("environment-profile-dimension-not-found", "The environment profile dimension is not registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(profile.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(profile.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, profile.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("environment-profile-zone-dimension-mismatch", "The environment profile zone belongs to another dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareEnvironmentProfiles(
        DimensionEnvironmentProfile left,
        DimensionEnvironmentProfile right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int zone = string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
      if (zone != 0)
      {
        return zone;
      }

      return string.Compare(left.ProfileId, right.ProfileId, StringComparison.Ordinal);
    }

    private bool EnvironmentProfileEquals(
        DimensionEnvironmentProfile a,
        DimensionEnvironmentProfile b)
    {
      return string.Equals(a.ProfileId, b.ProfileId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.MusicCueId, b.MusicCueId, StringComparison.Ordinal) &&
             string.Equals(a.AmbientCueId, b.AmbientCueId, StringComparison.Ordinal) &&
             string.Equals(a.LightingProfileId, b.LightingProfileId, StringComparison.Ordinal) &&
             string.Equals(a.FogProfileId, b.FogProfileId, StringComparison.Ordinal) &&
             a.HasMapColor == b.HasMapColor &&
             a.MapColorRgba == b.MapColorRgba &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }

    private static int CompareZoneDefinitions(
        DimensionZoneDefinition left,
        DimensionZoneDefinition right)
    {
      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int kind = string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
      if (kind != 0)
      {
        return kind;
      }

      return string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
    }

    private bool ZoneDefinitionEquals(
        DimensionZoneDefinition a,
        DimensionZoneDefinition b)
    {
      return string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             BoundsEqual(a.LocalBounds, b.LocalBounds) &&
             string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
