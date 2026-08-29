using System.Collections.Generic;
using ExpandNullforge.Authoring;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// One scene a dimension may place, with the placement policy its author wrote.
    /// </summary>
    /// <remarks>
    /// This record exists because the policy did not survive to runtime before it: the
    /// Studio authored placement mode, radial band, weight, unique and required — and the
    /// compiler carried none of them past the manifest. Every field here is something the
    /// placement pass genuinely reads; nothing is stored to look configurable.
    /// </remarks>
    public sealed class DimensionScenePoolEntry
    {
        public DimensionScenePoolEntry(
            string registeredSceneName,
            string sceneId,
            string biomeId,
            IReadOnlyList<string> allowedBiomeIds,
            DimensionScenePlacementMode mode,
            int2 exactLocalPosition,
            Api.DimensionBounds preferredLocalBounds,
            bool hasPreferredLocalBounds,
            int minRadiusTiles,
            int maxRadiusTiles,
            string radialBiomeId,
            int2 footprintSize,
            int weight,
            bool unique,
            bool required,
            int priority)
        {
            RegisteredSceneName = registeredSceneName ?? string.Empty;
            SceneId = sceneId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            AllowedBiomeIds = allowedBiomeIds ?? System.Array.Empty<string>();
            Mode = mode;
            ExactLocalPosition = exactLocalPosition;
            PreferredLocalBounds = preferredLocalBounds;
            HasPreferredLocalBounds = hasPreferredLocalBounds;
            MinRadiusTiles = math.max(0, minRadiusTiles);
            MaxRadiusTiles = math.max(MinRadiusTiles, maxRadiusTiles);
            FootprintSize = math.max(footprintSize, new int2(1, 1));
            RadialBiomeId = radialBiomeId ?? string.Empty;
            Weight = math.max(1, weight);
            Unique = unique;
            Required = required;
            Priority = priority;
        }

        /// <summary>The name the scene table resolves — the key TryPlace needs.</summary>
        public readonly string RegisteredSceneName;

        /// <summary>The runtime scene record id, for state tracking and idempotence.</summary>
        public readonly string SceneId;

        /// <summary>The biome that owns this scene's pool entry; empty for a global one.</summary>
        public readonly string BiomeId;

        public readonly IReadOnlyList<string> AllowedBiomeIds;

        public readonly DimensionScenePlacementMode Mode;

        public readonly int2 ExactLocalPosition;

        public readonly Api.DimensionBounds PreferredLocalBounds;

        public readonly bool HasPreferredLocalBounds;

        public readonly int MinRadiusTiles;

        public readonly int MaxRadiusTiles;

        public readonly string RadialBiomeId;

        public readonly int2 FootprintSize;

        public readonly int Weight;

        public readonly bool Unique;

        public readonly bool Required;

        public readonly int Priority;
    }

    /// <summary>
    /// Every dimension's placeable scenes and their placement policy, fed at content load.
    /// </summary>
    public static class DimensionScenePoolRegistry
    {
        private static readonly Dictionary<string, List<DimensionScenePoolEntry>> Pools =
            new Dictionary<string, List<DimensionScenePoolEntry>>(System.StringComparer.Ordinal);

        private static readonly IReadOnlyList<DimensionScenePoolEntry> Empty =
            System.Array.Empty<DimensionScenePoolEntry>();

        public static void Register(string dimensionId, DimensionScenePoolEntry entry)
        {
            if (string.IsNullOrEmpty(dimensionId) || entry == null ||
                string.IsNullOrEmpty(entry.RegisteredSceneName))
            {
                return;
            }

            if (!Pools.TryGetValue(dimensionId, out List<DimensionScenePoolEntry> pool))
            {
                pool = new List<DimensionScenePoolEntry>();
                Pools[dimensionId] = pool;
            }

            // Re-registration replaces: content reload feeds the same ids again.
            for (int i = 0; i < pool.Count; i++)
            {
                if (string.Equals(pool[i].SceneId, entry.SceneId, System.StringComparison.Ordinal))
                {
                    pool[i] = entry;
                    return;
                }
            }

            pool.Add(entry);
        }

        public static bool Has(string dimensionId)
        {
            return !string.IsNullOrEmpty(dimensionId) &&
                   Pools.TryGetValue(dimensionId, out List<DimensionScenePoolEntry> pool) &&
                   pool.Count > 0;
        }

        public static IReadOnlyList<DimensionScenePoolEntry> For(string dimensionId)
        {
            return !string.IsNullOrEmpty(dimensionId) &&
                   Pools.TryGetValue(dimensionId, out List<DimensionScenePoolEntry> pool)
                ? pool
                : Empty;
        }

        public static void Clear()
        {
            Pools.Clear();
        }
    }
}
