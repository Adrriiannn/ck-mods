using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionEnvironmentProfile> GetEnvironmentProfiles(
        string dimensionId,
        string zoneId,
        bool includeDisabled)
    {
      List<DimensionEnvironmentProfile> result =
          new List<DimensionEnvironmentProfile>();
      foreach (DimensionEnvironmentProfile profile in environmentProfiles.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(profile.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(zoneId) &&
            !string.IsNullOrEmpty(profile.ZoneId) &&
            !string.Equals(profile.ZoneId, zoneId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && !profile.Enabled)
        {
          continue;
        }

        result.Add(profile);
      }

      result.Sort(CompareEnvironmentProfiles);
      return result;
    }

    public bool TryGetEnvironmentProfile(
        string profileId,
        out DimensionEnvironmentProfile profile)
    {
      if (string.IsNullOrEmpty(profileId))
      {
        profile = default(DimensionEnvironmentProfile);
        return false;
      }

      return environmentProfiles.TryGetValue(profileId, out profile);
    }

    public bool TryFindEnvironmentProfile(
        string dimensionId,
        string zoneId,
        out DimensionEnvironmentProfile profile)
    {
      bool found = false;
      bool foundExactZone = false;
      DimensionEnvironmentProfile best = default(DimensionEnvironmentProfile);
      foreach (DimensionEnvironmentProfile candidate in environmentProfiles.Values)
      {
        if (!candidate.Enabled ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        bool exactZone =
            !string.IsNullOrEmpty(zoneId) &&
            string.Equals(candidate.ZoneId, zoneId, StringComparison.Ordinal);
        bool globalZone = string.IsNullOrEmpty(candidate.ZoneId);
        if (!exactZone && !globalZone)
        {
          continue;
        }

        if (!found ||
            (exactZone && !foundExactZone) ||
            (exactZone == foundExactZone && CompareEnvironmentProfiles(candidate, best) < 0))
        {
          best = candidate;
          found = true;
          foundExactZone = exactZone;
        }
      }

      profile = best;
      return found;
    }

    public bool TryFindEnvironmentProfileAtLocal(
        string dimensionId,
        float2 localPosition,
        out DimensionEnvironmentProfile profile)
    {
      DimensionEnvironmentProfileResolutionResult result =
          ResolveEnvironmentProfileAtLocal(dimensionId, localPosition);
      profile = result.Profile;
      return result.Success && result.HasProfile;
    }

    public DimensionEnvironmentProfileResolutionResult ResolveEnvironmentProfileAtLocal(
        string dimensionId,
        float2 localPosition)
    {
      DimensionDefinition dimension;
      if (!definitions.TryGetValue(dimensionId, out dimension) ||
          !dimension.ContainsLocal(localPosition))
      {
        return new DimensionEnvironmentProfileResolutionResult(
            false,
            dimensionId,
            localPosition,
            false,
            default(DimensionZoneInfo),
            false,
            default(DimensionBiomeDefinition),
            false,
            default(DimensionEnvironmentProfile),
            DimensionEnvironmentProfileResolutionSource.None,
            "dimension-position-not-found",
            "No dimension contains that local position.");
      }

      DimensionZoneInfo zone = default(DimensionZoneInfo);
      bool hasZone = TryGetZoneAtLocal(dimensionId, localPosition, out zone);
      DimensionBiomeDefinition biome = default(DimensionBiomeDefinition);
      bool hasBiome = false;

      if (hasZone && TryResolveBiomeForZone(zone, out biome))
      {
        hasBiome = true;
      }

      if (!hasBiome && TryResolveFallbackBiomeForDimension(dimensionId, out biome))
      {
        hasBiome = true;
      }

      DimensionEnvironmentProfile profile;
      if (hasBiome &&
          !string.IsNullOrEmpty(biome.EnvironmentProfileId) &&
          TryGetEnabledEnvironmentProfile(biome.EnvironmentProfileId, out profile))
      {
        return new DimensionEnvironmentProfileResolutionResult(
            true,
            dimensionId,
            localPosition,
            hasZone,
            zone,
            true,
            biome,
            true,
            profile,
            DimensionEnvironmentProfileResolutionSource.Biome,
            string.Empty,
            string.Empty);
      }

      string zoneId = hasZone ? zone.ZoneId : string.Empty;
      if (TryFindEnvironmentProfile(dimensionId, zoneId, out profile))
      {
        DimensionEnvironmentProfileResolutionSource source =
            !string.IsNullOrEmpty(zoneId) &&
            string.Equals(profile.ZoneId, zoneId, StringComparison.Ordinal)
                ? DimensionEnvironmentProfileResolutionSource.Zone
                : DimensionEnvironmentProfileResolutionSource.DimensionFallback;

        return new DimensionEnvironmentProfileResolutionResult(
            true,
            dimensionId,
            localPosition,
            hasZone,
            zone,
            hasBiome,
            biome,
            true,
            profile,
            source,
            string.Empty,
            string.Empty);
      }

      return new DimensionEnvironmentProfileResolutionResult(
          false,
          dimensionId,
          localPosition,
          hasZone,
          zone,
          hasBiome,
          biome,
          false,
          default(DimensionEnvironmentProfile),
          DimensionEnvironmentProfileResolutionSource.None,
          "environment-profile-not-found",
          "No enabled environment profile applies to that local position.");
    }

    public bool TryRegisterEnvironmentProfile(
        DimensionEnvironmentProfile profile,
        out DimensionOperationResult result)
    {
      if (!ValidateEnvironmentProfile(profile, out result))
      {
        return false;
      }

      if (environmentProfiles.ContainsKey(profile.ProfileId))
      {
        result = DimensionOperationResult.Failed("environment-profile-already-registered", "An environment profile with that id is already registered.");
        return false;
      }

      environmentProfiles[profile.ProfileId] = profile;
      RaiseEnvironmentProfileChanged(
          profile,
          DimensionEnvironmentProfileChangeKind.Registered,
          false,
          profile.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateEnvironmentProfile(
        DimensionEnvironmentProfile profile,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionEnvironmentProfile previous;
      if (!environmentProfiles.TryGetValue(profile.ProfileId, out previous))
      {
        result = DimensionOperationResult.Failed("environment-profile-not-found", "No environment profile with that id is registered.");
        return false;
      }

      if (!ValidateEnvironmentProfile(profile, out result))
      {
        return false;
      }

      if (EnvironmentProfileEquals(previous, profile))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      environmentProfiles[profile.ProfileId] = profile;
      RaiseEnvironmentProfileChanged(
          profile,
          previous.Enabled == profile.Enabled
              ? DimensionEnvironmentProfileChangeKind.Updated
              : DimensionEnvironmentProfileChangeKind.EnabledChanged,
          previous.Enabled,
          profile.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetEnvironmentProfileEnabled(
        string profileId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(profileId))
      {
        result = DimensionOperationResult.Failed("environment-profile-id-empty", "An environment profile id is required.");
        return false;
      }

      DimensionEnvironmentProfile profile;
      if (!environmentProfiles.TryGetValue(profileId, out profile))
      {
        result = DimensionOperationResult.Failed("environment-profile-not-found", "No environment profile with that id is registered.");
        return false;
      }

      if (profile.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionEnvironmentProfile updated =
          new DimensionEnvironmentProfile(
              profile.ProfileId,
              profile.DisplayName,
              profile.DimensionId,
              profile.ZoneId,
              profile.MusicCueId,
              profile.AmbientCueId,
              profile.LightingProfileId,
              profile.FogProfileId,
              profile.HasMapColor,
              profile.MapColorRgba,
              profile.Priority,
              enabled);

      environmentProfiles[profileId] = updated;
      RaiseEnvironmentProfileChanged(
          updated,
          DimensionEnvironmentProfileChangeKind.EnabledChanged,
          profile.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveEnvironmentProfile(
        string profileId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(profileId))
      {
        result = DimensionOperationResult.Failed("environment-profile-id-empty", "An environment profile id is required.");
        return false;
      }

      DimensionEnvironmentProfile profile;
      if (!environmentProfiles.TryGetValue(profileId, out profile))
      {
        result = DimensionOperationResult.Failed("environment-profile-not-found", "No environment profile with that id is registered.");
        return false;
      }

      environmentProfiles.Remove(profileId);
      RaiseEnvironmentProfileChanged(
          profile,
          DimensionEnvironmentProfileChangeKind.Removed,
          profile.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
