using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using ExpandNullforge.Scenes;
using ExpandNullforge.Zones;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Finding somewhere a scene fits, and scoring how well it fits there.
    /// </summary>
    public sealed partial class DimensionScenePlacementPassProvider
    {
        /// <summary>
        /// Vanilla's spot search: sub-passes of falling ambition, best-of-100 Halton samples per
        /// attempt, scored by the smallest margin to anything already placed or to the edge.
        /// </summary>
        private bool TryFindSpot(
            PlacementJob job,
            DimensionBounds searchBounds,
            PendingPlacement pending,
            string dimensionId,
            out int2 spot)
        {
            spot = default;
            int2 size = searchBounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            int footprint = FootprintRadius(pending);
            bool small = math.min(size.x, size.y) < 96;

            for (int pass = small ? 1 : 0; pass < MinClearRadiusPerPass.Length; pass++)
            {
                int wantClear = math.max(MinClearRadiusPerPass[pass], footprint);
                for (int attempt = 0; attempt < AttemptsPerPass[pass]; attempt++)
                {
                    int indexOffset =
                        (int)(job.Seed % 65536) + attempt * HaltonSamplesPerAttempt + pass * 10000;
                    float bestScore = -1f;
                    int2 best = default;
                    for (int sample = 0; sample < HaltonSamplesPerAttempt; sample++)
                    {
                        int2 candidate = HaltonPoint(indexOffset + sample, searchBounds);
                        if (!IsEligibleSpot(pending, dimensionId, candidate))
                        {
                            continue;
                        }

                        float score = ScoreSpot(job, searchBounds, candidate, pending);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = candidate;
                        }
                    }

                    if (bestScore >= wantClear)
                    {
                        spot = best;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The policy gates that are about the SPOT, not about clearance.</summary>
        private bool IsEligibleSpot(PendingPlacement pending, string dimensionId, int2 candidate)
        {
            DimensionScenePoolEntry policy = pending.Policy;
            if (policy == null)
            {
                return true;
            }

            if (policy.Mode == DimensionScenePlacementMode.RadialBand)
            {
                // Local (0,0) is the dimension's centre by the fallback-bounds convention, so
                // the band is a plain distance check.
                float distance = math.length(new float2(candidate.x, candidate.y));
                if (distance < policy.MinRadiusTiles || distance >= policy.MaxRadiusTiles)
                {
                    return false;
                }
            }

            bool wantsBiome =
                !string.IsNullOrEmpty(policy.BiomeId) ||
                !string.IsNullOrEmpty(policy.RadialBiomeId) ||
                policy.AllowedBiomeIds.Count > 0;
            if (!wantsBiome)
            {
                return true;
            }

            DimensionZoneDefinition zone;
            if (!service.TryFindZoneDefinitionAtLocal(
                    dimensionId,
                    new float2(candidate.x, candidate.y),
                    out zone))
            {
                // A biome-restricted scene outside every biome region has no claim to the spot.
                return false;
            }

            string biomeAtSpot = zone.Kind ?? string.Empty;
            if (!string.IsNullOrEmpty(policy.RadialBiomeId) &&
                string.Equals(policy.RadialBiomeId, biomeAtSpot, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(policy.BiomeId) &&
                string.Equals(policy.BiomeId, biomeAtSpot, System.StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < policy.AllowedBiomeIds.Count; i++)
            {
                if (string.Equals(policy.AllowedBiomeIds[i], biomeAtSpot, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Smallest margin to the bounds edge or to any prior placement — bigger is safer.</summary>
        private static float ScoreSpot(
            PlacementJob job,
            DimensionBounds bounds,
            int2 candidate,
            PendingPlacement pending)
        {
            float score = math.min(
                math.min(candidate.x - bounds.Min.x, bounds.MaxExclusive.x - 1 - candidate.x),
                math.min(candidate.y - bounds.Min.y, bounds.MaxExclusive.y - 1 - candidate.y));

            int ownFootprint = FootprintRadius(pending);
            for (int i = 0; i < job.Placed.Count; i++)
            {
                PlacedFootprint placed = job.Placed[i];
                float distance =
                    math.length(new float2(
                        candidate.x - placed.LocalPosition.x,
                        candidate.y - placed.LocalPosition.y)) -
                    placed.Radius - ownFootprint;
                score = math.min(score, distance);
            }

            return score;
        }

        private static int FootprintRadius(PendingPlacement pending)
        {
            int2 size = pending.Policy != null
                ? pending.Policy.FootprintSize
                : math.max(pending.AuthoredBounds.Size, new int2(8, 8));
            return math.max(1, (int)math.ceil(0.5f * math.length(new float2(size.x, size.y))));
        }

        /// <summary>Low-discrepancy point n of the (2,3) Halton sequence, mapped over the bounds.</summary>
        private static int2 HaltonPoint(int index, DimensionBounds bounds)
        {
            int2 size = bounds.Size;
            return new int2(
                bounds.Min.x + (int)(RadicalInverse(index, 2) * size.x),
                bounds.Min.y + (int)(RadicalInverse(index, 3) * size.y));
        }

        private static float RadicalInverse(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;
            int n = index < 0 ? -index : index;
            while (n > 0)
            {
                result += (n % radix) * fraction;
                n /= radix;
                fraction /= radix;
            }

            return math.clamp(result, 0f, 0.9999f);
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                // FNV-1a, the framework's standard stable string hash (see the tileset ids).
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }

                return (int)hash;
            }
        }

        private static DimensionBounds Intersect(DimensionBounds left, DimensionBounds right)
        {
            return new DimensionBounds(
                math.max(left.Min, right.Min),
                math.min(left.MaxExclusive, right.MaxExclusive));
        }

        private static bool Intersects(DimensionBounds left, DimensionBounds right)
        {
            return left.Min.x < right.MaxExclusive.x && right.Min.x < left.MaxExclusive.x &&
                   left.Min.y < right.MaxExclusive.y && right.Min.y < left.MaxExclusive.y;
        }

        private static string CreateKey(
            string dimensionId,
            DimensionBounds area,
            DimensionBounds pass)
        {
            return dimensionId + "|" +
                   area.Min.x + "," + area.Min.y + "|" +
                   pass.Min.x + "," + pass.Min.y + "," +
                   pass.MaxExclusive.x + "," + pass.MaxExclusive.y;
        }
    }
}
