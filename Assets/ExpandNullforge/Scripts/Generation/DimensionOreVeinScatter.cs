using System.Collections.Generic;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>One block's vein rule: which ore its walls grow, how often, how big.</summary>
    public readonly struct DimensionOreVeinRule
    {
        public DimensionOreVeinRule(
            int carrierTilesetId,
            string oreItemId,
            float abundancePer100Walls,
            int minVeinSize,
            int maxVeinSize)
        {
            CarrierTilesetId = carrierTilesetId;
            OreItemId = oreItemId ?? string.Empty;
            AbundancePer100Walls = math.clamp(abundancePer100Walls, 0f, 10f);
            MinVeinSize = math.max(1, minVeinSize);
            MaxVeinSize = math.clamp(maxVeinSize < MinVeinSize ? MinVeinSize : maxVeinSize, 1, 12);
        }

        /// <summary>
        /// The tileset the ore TILE carries — the block's own id, because the generated vein
        /// object stamps (own tileset, ore) and the drop resolves by first match on that pair.
        /// </summary>
        public readonly int CarrierTilesetId;

        /// <summary>The item mining the vein drops; also what the biome gate matches on.</summary>
        public readonly string OreItemId;

        public readonly float AbundancePer100Walls;

        public readonly int MinVeinSize;

        public readonly int MaxVeinSize;
    }

    /// <summary>Which walls grow which veins, keyed by the WALL's tileset id.</summary>
    public static class DimensionOreVeinRuleRegistry
    {
        private static readonly Dictionary<int, List<DimensionOreVeinRule>> ByTileset =
            new Dictionary<int, List<DimensionOreVeinRule>>();

        private static readonly List<DimensionOreVeinRule> None = new List<DimensionOreVeinRule>();

        /// <summary>Replaces rather than appends: re-registering is a reload, not twice the ore.</summary>
        public static void Register(int wallTilesetId, IReadOnlyList<DimensionOreVeinRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                ByTileset.Remove(wallTilesetId);
                return;
            }

            List<DimensionOreVeinRule> copy = new List<DimensionOreVeinRule>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].AbundancePer100Walls > 0f)
                {
                    copy.Add(rules[i]);
                }
            }

            if (copy.Count == 0)
            {
                ByTileset.Remove(wallTilesetId);
                return;
            }

            ByTileset[wallTilesetId] = copy;
        }

        public static IReadOnlyList<DimensionOreVeinRule> For(int wallTilesetId)
        {
            List<DimensionOreVeinRule> rules;
            return ByTileset.TryGetValue(wallTilesetId, out rules) ? rules : None;
        }

        public static bool Any
        {
            get { return ByTileset.Count > 0; }
        }

        public static void Clear()
        {
            ByTileset.Clear();
        }
    }

    /// <summary>
    /// Grows ore veins in generated walls — the natural spawning the ore panel promises.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's veins have no CPU code to borrow: they are threshold blobs in a compiled
    /// compute shader. This is our own cluster walk, shaped to match what a player sees —
    /// small contiguous clumps inside walls — with authored size bounds so the resemblance
    /// never has to be exact.
    /// </para>
    /// <para>
    /// EVERYTHING HERE IS A PURE FUNCTION of the dimension id and the wall positions. Host
    /// and client regenerate identical veins with no communication, exactly like the
    /// decoration scatter this mirrors. That is also why the caller hands in the wall map
    /// rather than the world: reading tiles back mid-generation sees pre-terrain emptiness.
    /// </para>
    /// <para>
    /// THE LAW OF ADD ORDER shapes the output contract: an ore tile whose wall has not been
    /// applied yet is rejected by the game's server — and the rejection MINTS a loose ore
    /// item on the floor. Appended writes ride after the full base list (which holds every
    /// wall) through the same in-order paint loop, so every vein cell's wall lands first.
    /// </para>
    /// </remarks>
    public static class DimensionOreVeinScatter
    {
        private const int OreSeedChannel = 70001;
        private const int OreSizeChannel = 70002;
        private const int OreStepChannelBase = 70100;

        private static readonly int2[] Offsets =
        {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1)
        };

        /// <summary>
        /// Appends vein writes for every rule-bearing wall in <paramref name="writes"/>.
        /// Hand-painted ore cells are pre-claimed and never overwritten.
        /// </summary>
        public static void AppendVeins(
            string dimensionId,
            List<DimensionResolvedTileWrite> writes,
            System.Func<int2, string, bool> biomeAllows)
        {
            if (!DimensionOreVeinRuleRegistry.Any || writes == null || writes.Count == 0)
            {
                return;
            }

            ulong seed = DimensionOverlayScatter.Hash(0UL, default, StableHash(dimensionId));

            // Snapshot: veins never grow on the ore they just added.
            int baseCount = writes.Count;
            Dictionary<int2, int> wallTilesetAt = new Dictionary<int2, int>();
            HashSet<int2> claimed = new HashSet<int2>();
            for (int i = 0; i < baseCount; i++)
            {
                if (writes[i].TileType == TileType.wall)
                {
                    wallTilesetAt[writes[i].AbsolutePosition] = writes[i].Tileset;
                }
                else if (writes[i].TileType == TileType.ore ||
                         writes[i].TileType == TileType.ancientCrystal)
                {
                    claimed.Add(writes[i].AbsolutePosition);
                }
            }

            for (int i = 0; i < baseCount; i++)
            {
                DimensionResolvedTileWrite write = writes[i];
                if (write.TileType != TileType.wall)
                {
                    continue;
                }

                IReadOnlyList<DimensionOreVeinRule> rules =
                    DimensionOreVeinRuleRegistry.For(write.Tileset);
                for (int r = 0; r < rules.Count; r++)
                {
                    DimensionOreVeinRule rule = rules[r];
                    if (rule.AbundancePer100Walls <= 0f || claimed.Contains(write.AbsolutePosition))
                    {
                        continue;
                    }

                    if (biomeAllows != null && !biomeAllows(write.AbsolutePosition, rule.OreItemId))
                    {
                        continue;
                    }

                    uint bits = (uint)(DimensionOverlayScatter.Hash(
                        seed, write.AbsolutePosition, OreSeedChannel) >> 40) & 0xFFFFFF;
                    if (bits / (float)0x1000000 >= rule.AbundancePer100Walls / 100f)
                    {
                        continue;
                    }

                    GrowVein(seed, write.AbsolutePosition, rule, write.Tileset,
                        wallTilesetAt, claimed, writes);
                }
            }
        }

        /// <summary>
        /// Grows one blob outward from its origin, only into unclaimed walls of the same
        /// tileset. A vein that hits its container's edge comes out smaller, never illegal.
        /// </summary>
        private static void GrowVein(
            ulong seed,
            int2 origin,
            DimensionOreVeinRule rule,
            int wallTileset,
            Dictionary<int2, int> wallTilesetAt,
            HashSet<int2> claimed,
            List<DimensionResolvedTileWrite> writes)
        {
            int span = rule.MaxVeinSize - rule.MinVeinSize + 1;
            int size = rule.MinVeinSize +
                (int)(DimensionOverlayScatter.Hash(seed, origin, OreSizeChannel) % (ulong)span);

            List<int2> vein = new List<int2>(size) { origin };
            claimed.Add(origin);

            for (int step = 1; step < size; step++)
            {
                ulong hash = DimensionOverlayScatter.Hash(seed, origin, OreStepChannelBase + step);
                int2 next = default;
                bool found = false;
                for (int v = 0; v < vein.Count && !found; v++)
                {
                    int2 cell = vein[(v + (int)(hash % (ulong)vein.Count)) % vein.Count];
                    for (int d = 0; d < 4 && !found; d++)
                    {
                        int2 neighbour = cell + Offsets[(d + (int)((hash >> 8) & 3)) % 4];
                        int neighbourTileset;
                        if (!claimed.Contains(neighbour) &&
                            wallTilesetAt.TryGetValue(neighbour, out neighbourTileset) &&
                            neighbourTileset == wallTileset)
                        {
                            next = neighbour;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    break;
                }

                vein.Add(next);
                claimed.Add(next);
            }

            for (int i = 0; i < vein.Count; i++)
            {
                writes.Add(new DimensionResolvedTileWrite(
                    vein[i], TileType.ore, rule.CarrierTilesetId));
            }
        }

        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
