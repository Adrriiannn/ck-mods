using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private const int CoordinateDomainBoundsAlignmentTiles = 16;
    private const int MinimumCoordinateDomainPaddingTiles = 64;
    private const int MaximumCoordinateDomainPaddingTiles = 5000;
    private const int ProtectedOverworldCoordinateRadiusTiles = 5000;

    private static readonly DimensionBounds ProtectedOverworldCoordinateBounds =
        new DimensionBounds(
            new int2(-ProtectedOverworldCoordinateRadiusTiles, -ProtectedOverworldCoordinateRadiusTiles),
            new int2(ProtectedOverworldCoordinateRadiusTiles, ProtectedOverworldCoordinateRadiusTiles));

    public bool TryGetCoordinateDomain(
        string dimensionId,
        out DimensionCoordinateDomain domain)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        domain = default(DimensionCoordinateDomain);
        return false;
      }

      return TryBuildCoordinateDomain(definition, out domain);
    }

    public bool TryGetCoordinateDomainAtAbsolute(
        float2 absolutePosition,
        out DimensionCoordinateDomain domain)
    {
      DimensionCoordinateDomain playableDomain;
      if (TryGetPlayableCoordinateDomainAtAbsolute(absolutePosition, out playableDomain))
      {
        domain = playableDomain;
        return true;
      }

      bool found = false;
      float bestDistance = float.MaxValue;
      DimensionCoordinateDomain bestDomain = default(DimensionCoordinateDomain);

      for (int i = 0; i < definitionSnapshot.Count; i++)
      {
        DimensionDefinition candidate = definitionSnapshot[i];
        if (candidate.Id == DimensionIds.Overworld)
        {
          continue;
        }

        DimensionCoordinateDomain candidateDomain;
        if (!TryBuildCoordinateDomain(candidate, out candidateDomain) ||
            !candidateDomain.ContainsCoordinateAbsolute(absolutePosition))
        {
          continue;
        }

        float distance = DistanceSquaredToBounds(candidateDomain.PlayableAbsoluteBounds, absolutePosition);
        if (!found ||
            distance < bestDistance ||
            (math.abs(distance - bestDistance) <= 0.001f &&
             string.Compare(candidateDomain.DimensionId, bestDomain.DimensionId, StringComparison.Ordinal) < 0))
        {
          found = true;
          bestDistance = distance;
          bestDomain = candidateDomain;
        }
      }

      if (found)
      {
        domain = bestDomain;
        return true;
      }

      return TryBuildCoordinateDomain(OverworldDefinition, out domain);
    }

    public DimensionContext GetCoordinateContextForAbsolute(float2 absolutePosition)
    {
      DimensionCoordinateDomain domain;
      if (!TryGetCoordinateDomainAtAbsolute(absolutePosition, out domain) || !domain.IsValid)
      {
        return DimensionContext.Unknown(absolutePosition);
      }

      if (domain.IsOverworld)
      {
        return DimensionContext.Overworld(absolutePosition);
      }

      return new DimensionContext(
          true,
          domain.DimensionId,
          absolutePosition,
          domain.ToLocal(absolutePosition));
    }

    private bool TryGetPlayableCoordinateDomainAtAbsolute(
        float2 absolutePosition,
        out DimensionCoordinateDomain domain)
    {
      for (int i = 0; i < definitionSnapshot.Count; i++)
      {
        DimensionDefinition candidate = definitionSnapshot[i];
        if (candidate.Id == DimensionIds.Overworld)
        {
          continue;
        }

        DimensionCoordinateDomain candidateDomain;
        if (!TryBuildCoordinateDomain(candidate, out candidateDomain) ||
            !candidateDomain.ContainsPlayableAbsolute(absolutePosition))
        {
          continue;
        }

        domain = candidateDomain;
        return true;
      }

      domain = default(DimensionCoordinateDomain);
      return false;
    }

    private bool TryBuildCoordinateDomain(
        DimensionDefinition definition,
        out DimensionCoordinateDomain domain)
    {
      if (string.IsNullOrEmpty(definition.Id))
      {
        domain = default(DimensionCoordinateDomain);
        return false;
      }

      DimensionBounds playableLocalBounds = ResolvePlayableCoordinateLocalBounds(definition);
      int padding = 0;
      if (definition.Id != DimensionIds.Overworld)
      {
        padding = ResolveCoordinateDomainPadding(definition, playableLocalBounds);
      }

      DimensionBounds coordinateLocalBounds = ExpandBounds(playableLocalBounds, padding);
      domain =
          new DimensionCoordinateDomain(
              definition.Id,
              definition.AbsoluteOrigin,
              playableLocalBounds,
              coordinateLocalBounds,
              padding,
              definition.SpaceKind);
      return true;
    }

    private DimensionBounds ResolvePlayableCoordinateLocalBounds(DimensionDefinition definition)
    {
      if (definition.Id == DimensionIds.Overworld)
      {
        return definition.LocalBounds;
      }

      bool hasBounds = false;
      int2 min = default(int2);
      int2 max = default(int2);

      foreach (DimensionStarterDefinition starter in starters.Values)
      {
        if (!starter.Enabled ||
            !string.Equals(starter.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(starter.GenerationRequest.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (!generationPass.Enabled ||
            !generationPass.HasLocalBounds ||
            !string.Equals(generationPass.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(generationPass.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionSceneDefinition scene in scenes.Values)
      {
        if (!SceneContributesToPlayableCoordinateBounds(scene) ||
            !string.Equals(scene.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(scene.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionResourceNodeDefinition node in resourceNodes.Values)
      {
        if (!node.Enabled ||
            !node.HasLocalBounds ||
            !string.Equals(node.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(node.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionSpawnRule rule in spawnRules.Values)
      {
        if (!rule.Enabled ||
            !rule.HasLocalBounds ||
            !string.Equals(rule.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(rule.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionWorldEventDefinition worldEvent in worldEvents.Values)
      {
        if (!worldEvent.Enabled ||
            !worldEvent.HasLocalBounds ||
            !string.Equals(worldEvent.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(worldEvent.LocalBounds, ref hasBounds, ref min, ref max);
      }

      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        if (!ZoneContributesToPlayableCoordinateBounds(zone) ||
            !string.Equals(zone.DimensionId, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        AccumulateCoordinateBounds(zone.LocalBounds, ref hasBounds, ref min, ref max);
      }

      if (!hasBounds)
      {
        return CreateSquareCoordinateBounds(definition.LocalBounds);
      }

      return CreateSquareCoordinateBounds(new DimensionBounds(min, max));
    }

    private int ResolveCoordinateDomainPadding(
        DimensionDefinition definition,
        DimensionBounds playableLocalBounds)
    {
      int playableSide =
          math.max(playableLocalBounds.Size.x, playableLocalBounds.Size.y);
      int rawPadding =
          math.min(
              MaximumCoordinateDomainPaddingTiles,
              math.max(MinimumCoordinateDomainPaddingTiles, playableSide));
      int padding = RoundUpToMultiple(rawPadding, CoordinateDomainBoundsAlignmentTiles);
      DimensionBounds playableBounds = ToAbsoluteBounds(definition.AbsoluteOrigin, playableLocalBounds);

      padding =
          ClampPaddingAgainstFixedBounds(
              playableBounds,
              ProtectedOverworldCoordinateBounds,
              padding);

      for (int i = 0; i < definitionSnapshot.Count; i++)
      {
        DimensionDefinition other = definitionSnapshot[i];
        if (other.Id == DimensionIds.Overworld ||
            string.Equals(other.Id, definition.Id, StringComparison.Ordinal))
        {
          continue;
        }

        DimensionBounds otherPlayableLocalBounds = ResolvePlayableCoordinateLocalBounds(other);
        DimensionBounds otherPlayableBounds =
            ToAbsoluteBounds(other.AbsoluteOrigin, otherPlayableLocalBounds);

        padding =
            ClampSharedPaddingAgainstBounds(
                playableBounds,
                otherPlayableBounds,
                padding);
      }

      return math.max(0, padding);
    }

    private static bool SceneContributesToPlayableCoordinateBounds(
        DimensionSceneDefinition scene)
    {
      return scene.State != DimensionSceneState.Disabled &&
             scene.State != DimensionSceneState.Error;
    }

    private static bool ZoneContributesToPlayableCoordinateBounds(
        DimensionZoneDefinition zone)
    {
      if (!zone.Enabled)
      {
        return false;
      }

      if (!string.Equals(zone.Kind, "biome", StringComparison.Ordinal))
      {
        return false;
      }

      return false;
    }

    private static void AccumulateCoordinateBounds(
        DimensionBounds bounds,
        ref bool hasBounds,
        ref int2 min,
        ref int2 max)
    {
      if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
      {
        return;
      }

      if (!hasBounds)
      {
        hasBounds = true;
        min = bounds.Min;
        max = bounds.MaxExclusive;
        return;
      }

      min = math.min(min, bounds.Min);
      max = math.max(max, bounds.MaxExclusive);
    }

    private static DimensionBounds CreateSquareCoordinateBounds(
        DimensionBounds sourceBounds)
    {
      int2 sourceSize = sourceBounds.Size;
      int side =
          RoundUpToMultiple(
              math.max(
                  CoordinateDomainBoundsAlignmentTiles,
                  math.max(sourceSize.x, sourceSize.y)),
              CoordinateDomainBoundsAlignmentTiles);

      int extraX = side - sourceSize.x;
      int extraY = side - sourceSize.y;
      int2 min =
          new int2(
              sourceBounds.Min.x - (extraX / 2),
              sourceBounds.Min.y - (extraY / 2));
      int2 max = min + new int2(side, side);
      return new DimensionBounds(
          min,
          max);
    }

    private static DimensionBounds ToAbsoluteBounds(
        int2 absoluteOrigin,
        DimensionBounds localBounds)
    {
      return new DimensionBounds(
          absoluteOrigin + localBounds.Min,
          absoluteOrigin + localBounds.MaxExclusive);
    }

    private static int RoundUpToMultiple(
        int value,
        int multiple)
    {
      if (multiple <= 1)
      {
        return value;
      }

      int remainder = value % multiple;
      if (remainder == 0)
      {
        return value;
      }

      return value + multiple - remainder;
    }

    private static int ClampPaddingAgainstFixedBounds(
        DimensionBounds playableBounds,
        DimensionBounds fixedBounds,
        int maximumPadding)
    {
      if (maximumPadding <= 0)
      {
        return 0;
      }

      if (Overlaps(playableBounds, fixedBounds))
      {
        return 0;
      }

      int low = 0;
      int high = maximumPadding;
      while (low < high)
      {
        int middle = (low + high + 1) / 2;
        if (Overlaps(ExpandBounds(playableBounds, middle), fixedBounds))
        {
          high = middle - 1;
        }
        else
        {
          low = middle;
        }
      }

      return low;
    }

    private static int ClampSharedPaddingAgainstBounds(
        DimensionBounds playableBounds,
        DimensionBounds otherPlayableBounds,
        int maximumPadding)
    {
      if (maximumPadding <= 0)
      {
        return 0;
      }

      if (Overlaps(playableBounds, otherPlayableBounds))
      {
        return 0;
      }

      int low = 0;
      int high = maximumPadding;
      while (low < high)
      {
        int middle = (low + high + 1) / 2;
        if (Overlaps(
            ExpandBounds(playableBounds, middle),
            ExpandBounds(otherPlayableBounds, middle)))
        {
          high = middle - 1;
        }
        else
        {
          low = middle;
        }
      }

      return low;
    }

    private static DimensionBounds ExpandBounds(
        DimensionBounds bounds,
        int margin)
    {
      if (margin <= 0)
      {
        return bounds;
      }

      int2 offset = new int2(margin, margin);
      return new DimensionBounds(bounds.Min - offset, bounds.MaxExclusive + offset);
    }

    private static bool Overlaps(
        DimensionBounds left,
        DimensionBounds right)
    {
      return left.Min.x < right.MaxExclusive.x &&
             left.MaxExclusive.x > right.Min.x &&
             left.Min.y < right.MaxExclusive.y &&
             left.MaxExclusive.y > right.Min.y;
    }

    private static float DistanceSquaredToBounds(
        DimensionBounds bounds,
        float2 position)
    {
      float dx = 0.0f;
      if (position.x < bounds.Min.x)
      {
        dx = bounds.Min.x - position.x;
      }
      else if (position.x >= bounds.MaxExclusive.x)
      {
        dx = position.x - bounds.MaxExclusive.x;
      }

      float dy = 0.0f;
      if (position.y < bounds.Min.y)
      {
        dy = bounds.Min.y - position.y;
      }
      else if (position.y >= bounds.MaxExclusive.y)
      {
        dy = position.y - bounds.MaxExclusive.y;
      }

      return (dx * dx) + (dy * dy);
    }
  }
}
