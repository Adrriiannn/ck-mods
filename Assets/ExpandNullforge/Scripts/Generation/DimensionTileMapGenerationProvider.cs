using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using Unity.Entities;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Generates a dimension from its painted tile map. Plugs into the same orchestration as the
    /// safe-platform provider: it is ticked on the main thread, but the actual tile writing is
    /// handed to <see cref="DimensionTileMapGenerationSystem"/>, which runs a Burst job on Core
    /// Keeper's own <c>TileAccessor.Set</c> path. The provider only computes the writes (cheap,
    /// pure) and polls the system for completion.
    ///
    /// The map comes from <see cref="DimensionTileMapRegistry"/>, populated when the dimension's
    /// content loads. A dimension with no registered map is not this provider's job.
    /// </summary>
    public sealed class DimensionTileMapGenerationProvider : IDimensionGenerationProvider
    {
        private readonly HashSet<string> submitted = new HashSet<string>();

        public string ProviderId => DimensionGenerationProviderIds.TileMap;

        public bool CanGenerate(DimensionDefinition dimension, DimensionBounds localBounds)
        {
            if (string.IsNullOrEmpty(dimension.Id) ||
                dimension.Id == DimensionIds.Overworld ||
                !dimension.HasCapability(DimensionCapabilityFlags.Generation))
            {
                return false;
            }

            // Only when a painted map exists for this dimension.
            return DimensionTileMapRegistry.Has(dimension.Id);
        }

        public DimensionGenerationProviderResult TickGeneration(DimensionGenerationContext context)
        {
            if (context.ServerWorld == null || !context.ServerWorld.IsCreated)
            {
                return DimensionGenerationProviderResult.Failed(
                    "The server world is not available for tile-map generation.");
            }

            if (!DimensionTileMapRegistry.TryGet(context.Dimension.Id, out DimensionTileMapModel map) ||
                map == null)
            {
                return DimensionGenerationProviderResult.Failed(
                    "No tile map is registered for dimension '" + context.Dimension.Id + "'.");
            }

            DimensionTileMapGenerationSystem system =
                context.ServerWorld.GetOrCreateSystemManaged<DimensionTileMapGenerationSystem>();
            if (system == null)
            {
                return DimensionGenerationProviderResult.Failed(
                    "The tile-map generation system could not be created.");
            }

            string key = BuildKey(context.Dimension.Id, context.Area.LocalBounds);

            if (!submitted.Contains(key))
            {
                DimensionTileMapCompileResult compiled = DimensionTileMapCompiler.Compile(
                    map.EnumeratePlacements(),
                    context.Area.LocalBounds,
                    context.Area.AbsoluteBounds);

                for (int i = 0; i < compiled.Skipped.Count; i++)
                {
                    DimensionFrameworkLog.Warning("[ExpandNullforge] " + compiled.Skipped[i]);
                }

                if (compiled.WriteCount == 0)
                {
                    // Nothing to write for this area (all outside it, or all unresolved). Done.
                    return DimensionGenerationProviderResult.Ready(
                        "No tiles to generate for this area.");
                }

                DimensionFrameworkLog.Warning(
                    "[ExpandNullforge][tilemap] provider generating '" + context.Dimension.Id +
                    "': " + compiled.WriteCount + " writes, " + compiled.Skipped.Count + " skipped.");
                system.Submit(key, compiled.Writes);
                submitted.Add(key);
                return DimensionGenerationProviderResult.Progress(
                    DimensionGenerationState.GeneratingTerrain,
                    0.1f,
                    "Writing " + compiled.WriteCount + " tiles.");
            }

            if (!system.TryGetStatus(key, out bool done, out int deferred))
            {
                // The batch is gone but we still think it is pending — treat as complete rather
                // than looping forever.
                submitted.Remove(key);
                return DimensionGenerationProviderResult.Ready("Tile-map generation complete.");
            }

            if (done)
            {
                system.Release(key);
                submitted.Remove(key);
                return DimensionGenerationProviderResult.Ready("Tile-map terrain generated.");
            }

            return DimensionGenerationProviderResult.Progress(
                DimensionGenerationState.GeneratingTerrain,
                0.6f,
                deferred > 0
                    ? "Waiting for " + deferred + " tiles' chunks to load."
                    : "Writing tiles.");
        }

        /// <summary>Drops any tracked keys, e.g. when the framework tears down generation state.</summary>
        public void ClearJobs()
        {
            submitted.Clear();
        }

        private static string BuildKey(string dimensionId, DimensionBounds bounds)
        {
            return (dimensionId ?? string.Empty) +
                   "|" + bounds.Min.x + "," + bounds.Min.y +
                   "|" + bounds.MaxExclusive.x + "," + bounds.MaxExclusive.y;
        }
    }
}
