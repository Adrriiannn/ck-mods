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
    /// Filling a region with scenes until its budget runs out.
    /// </summary>
    public sealed partial class DimensionScenePlacementPassProvider
    {
        /// <summary>Runs fill stamps inside this tick's budget. True when the phase is finished.</summary>
        private bool TickFillPhase(
            DimensionGenerationContext context,
            DimensionBounds passBounds,
            PlacementJob job,
            ref int stamps)
        {
            if (job.FillPool.Count == 0)
            {
                return true;
            }

            while (stamps < MaxStampsPerTick &&
                   job.FillRemaining > 0 &&
                   job.FillFailureStreak < FillFailureLimit)
            {
                int fillIndex = FillBudget - job.FillRemaining;
                int2 spot;
                if (!TryFindFillSpot(job, passBounds, fillIndex, out spot))
                {
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                FillCandidate choice = PickFillCandidate(job, spot, context.Dimension.Id);
                if (choice == null)
                {
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                int2 absolute = context.Area.AbsoluteBounds.Min +
                                (spot - context.Area.LocalBounds.Min);
                uint instanceSeed = (uint)DimensionOverlayScatter.Hash(job.Seed, absolute, 3);

                DimensionScenePlacementResult result;
                string error;
                if (!DimensionScenePlacement.TryPlace(
                        context.ServerWorld,
                        choice.Entry.RegisteredSceneName,
                        absolute,
                        instanceSeed,
                        out result,
                        out error))
                {
                    if (result == DimensionScenePlacementResult.AreaNotLoaded)
                    {
                        // Do not burn budget on a chunk that is still loading — come back.
                        return false;
                    }

                    job.Placed.Add(new PlacedFootprint
                    {
                        LocalPosition = spot,
                        Radius = FillFootprintRadius(choice.Entry)
                    });
                    job.FillRemaining--;
                    job.FillFailureStreak++;
                    continue;
                }

                int radius = FillFootprintRadius(choice.Entry);
                job.Placed.Add(new PlacedFootprint { LocalPosition = spot, Radius = radius });
                choice.PlacedAt.Add(spot);
                RegisterSceneTriggers(context.Dimension.Id, choice.Entry.RegisteredSceneName, absolute);
                RecordFillPlacement(context.Dimension.Id, choice, spot);
                job.PlacedCount++;
                job.FillRemaining--;
                job.FillFailureStreak = 0;
                stamps++;
            }

            return job.FillRemaining <= 0 || job.FillFailureStreak >= FillFailureLimit;
        }

        /// <summary>A fill spot only needs open ground — per-scene policy is applied at the pick.</summary>
        private static bool TryFindFillSpot(
            PlacementJob job,
            DimensionBounds bounds,
            int fillIndex,
            out int2 spot)
        {
            spot = default;
            int2 size = bounds.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                return false;
            }

            int indexOffset = (int)(job.Seed % 65536) + 20000 + fillIndex * HaltonSamplesPerAttempt;
            float bestScore = -1f;
            int2 best = default;
            for (int sample = 0; sample < HaltonSamplesPerAttempt; sample++)
            {
                int2 candidate = HaltonPoint(indexOffset + sample, bounds);
                float score = math.min(
                    math.min(candidate.x - bounds.Min.x, bounds.MaxExclusive.x - 1 - candidate.x),
                    math.min(candidate.y - bounds.Min.y, bounds.MaxExclusive.y - 1 - candidate.y));
                for (int i = 0; i < job.Placed.Count; i++)
                {
                    PlacedFootprint placed = job.Placed[i];
                    float distance = math.length(new float2(
                        candidate.x - placed.LocalPosition.x,
                        candidate.y - placed.LocalPosition.y)) - placed.Radius;
                    score = math.min(score, distance);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore < 2f)
            {
                return false;
            }

            spot = best;
            return true;
        }

        /// <summary>
        /// Weighted roulette among the repeatable scenes whose policy admits this spot —
        /// deterministic, seeded from the spot itself.
        /// </summary>
        private FillCandidate PickFillCandidate(PlacementJob job, int2 spot, string dimensionId)
        {
            int totalWeight = 0;
            for (int i = 0; i < job.FillPool.Count; i++)
            {
                FillCandidate candidate = job.FillPool[i];
                candidate.EligibleThisRoll = !candidate.Exhausted &&
                                             IsFillEligible(candidate, spot, dimensionId);
                if (candidate.EligibleThisRoll)
                {
                    totalWeight += candidate.Entry.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            uint roll = (uint)(DimensionOverlayScatter.Hash(job.Seed, spot, 2) % (ulong)totalWeight);
            for (int i = 0; i < job.FillPool.Count; i++)
            {
                FillCandidate candidate = job.FillPool[i];
                if (!candidate.EligibleThisRoll)
                {
                    continue;
                }

                if (roll < (uint)candidate.Entry.Weight)
                {
                    return candidate;
                }

                roll -= (uint)candidate.Entry.Weight;
            }

            return null;
        }

        private bool IsFillEligible(FillCandidate candidate, int2 spot, string dimensionId)
        {
            DimensionScenePoolEntry entry = candidate.Entry;

            // Same-scene spacing: copies of one template keep two footprints of air between
            // them, so a heavy weight makes a scene common rather than carpeted.
            int minSelfDistance = 2 * math.max(entry.FootprintSize.x, entry.FootprintSize.y);
            for (int i = 0; i < candidate.PlacedAt.Count; i++)
            {
                if (math.length(new float2(
                        spot.x - candidate.PlacedAt[i].x,
                        spot.y - candidate.PlacedAt[i].y)) < minSelfDistance)
                {
                    return false;
                }
            }

            if (entry.Mode == DimensionScenePlacementMode.PreferredBounds &&
                entry.HasPreferredLocalBounds &&
                !entry.PreferredLocalBounds.Contains(spot))
            {
                return false;
            }

            return IsEligibleSpot(candidate.Probe, dimensionId, spot);
        }

        private void RecordFillPlacement(string dimensionId, FillCandidate candidate, int2 spot)
        {
            DimensionOperationResult opResult;
            string copyId = candidate.Entry.SceneId + "#" + candidate.PlacedAt.Count;
            int2 half = candidate.Entry.FootprintSize / 2;
            DimensionSceneDefinition record = new DimensionSceneDefinition(
                copyId,
                candidate.Entry.SceneId,
                dimensionId,
                new DimensionBounds(spot - half, spot + half + new int2(1, 1)),
                string.IsNullOrEmpty(candidate.Entry.BiomeId) ? "scene" : candidate.Entry.BiomeId,
                candidate.Entry.Priority,
                DimensionSceneState.Planned);
            service.TryRegisterScene(record, out opResult);
            service.TrySetSceneState(copyId, DimensionSceneState.Ready, "scene-placement-fill", out opResult);
        }

        private static int FillFootprintRadius(DimensionScenePoolEntry entry)
        {
            return math.max(
                1,
                (int)math.ceil(0.5f * math.length(
                    new float2(entry.FootprintSize.x, entry.FootprintSize.y))));
        }
    }
}
