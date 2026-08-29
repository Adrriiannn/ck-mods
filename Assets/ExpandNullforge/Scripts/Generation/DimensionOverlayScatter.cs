using System.Collections.Generic;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>One overlay a block scatters over its own ground, and how thickly.</summary>
    public readonly struct DimensionOverlayRule
    {
        public DimensionOverlayRule(LayerName layer, TileType tileType, float density)
        {
            Layer = layer;
            TileType = tileType;
            Density = math.clamp(density, 0f, 1f);
        }

        public readonly LayerName Layer;

        public readonly TileType TileType;

        /// <summary>Share of eligible cells that get this overlay, 0 to 1.</summary>
        public readonly float Density;
    }

    /// <summary>
    /// Decides where a block's decorative overlays — grass tufts, pebbles, roots, slime — actually go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Ticking "grass tufts" on a block made its grass art render, and nothing ever
    /// placed any. Core Keeper scatters vanilla overlays from generation code that names tilesets by
    /// hardcoded id, so a custom tileset is never a candidate no matter how complete its art is. The
    /// block looked finished in the Studio and came out bare in the world.
    /// </para>
    /// <para>
    /// DETERMINISTIC, AND DELIBERATELY NOT RANDOM. The decision for a cell is a pure function of the
    /// world seed, the cell's absolute position, and which overlay is being asked about. Nothing is
    /// carried between cells, so the same world always grows the same grass in the same places, in any
    /// order, on any machine — which matters because a host and a joining client generate terrain
    /// independently and would otherwise disagree about a mod's decoration. It also means the scatter
    /// can be computed for one cell in isolation, without a pass over the area.
    /// </para>
    /// <para>
    /// Each overlay is hashed with its own layer folded in, so enabling grass does not shift where the
    /// pebbles land. Sharing one hash across overlays would make every overlay pick the same cells,
    /// stacking them all on one patch of ground and leaving the rest bare.
    /// </para>
    /// </remarks>
    public static class DimensionOverlayScatter
    {
        /// <summary>
        /// Whether <paramref name="rule"/>'s overlay belongs on the cell at <paramref name="position"/>.
        /// </summary>
        public static bool ShouldPlace(ulong worldSeed, int2 position, DimensionOverlayRule rule)
        {
            if (rule.Density <= 0f)
            {
                return false;
            }

            if (rule.Density >= 1f)
            {
                return true;
            }

            // 0..1 from the hash's top bits. Comparing the whole hash to a scaled threshold would bias
            // toward the low end on the wrap-around; taking a fixed slice of high-quality bits does not.
            uint bits = (uint)(Hash(worldSeed, position, (int)rule.Layer) >> 40) & 0xFFFFFF;
            float sample = bits / (float)0x1000000;
            return sample < rule.Density;
        }

        /// <summary>
        /// Every overlay that belongs on one cell, in rule order.
        /// </summary>
        /// <remarks>
        /// Overlays are independent: a cell can legitimately grow grass AND pebbles, exactly as vanilla
        /// ground does. Callers that want them exclusive should express that in the rules they pass.
        /// </remarks>
        public static void Collect(
            ulong worldSeed,
            int2 position,
            IReadOnlyList<DimensionOverlayRule> rules,
            List<DimensionOverlayRule> into)
        {
            if (rules == null || into == null)
            {
                return;
            }

            for (int i = 0; i < rules.Count; i++)
            {
                if (ShouldPlace(worldSeed, position, rules[i]))
                {
                    into.Add(rules[i]);
                }
            }
        }

        /// <summary>
        /// A stable hash of a dimension id, for seeding a scatter from the dimension itself.
        /// </summary>
        /// <remarks>
        /// Shared by every provider that decorates, and that sharing is the point: the painted-map
        /// path and the generated-platform path can both cover the same dimension, and two copies
        /// of this that ever drifted would grow different grass in the same place. <c>string.GetHashCode</c>
        /// is explicitly not stable across runtimes or runs, so it cannot be used here — two players
        /// in one world would disagree.
        /// </remarks>
        internal static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash ^ value[i]) * 16777619;
                }

                return hash;
            }
        }

        /// <summary>
        /// 64-bit FNV-1a over the seed, position and layer.
        /// </summary>
        /// <remarks>
        /// The same construction the framework uses for tileset identity, for the same reason: it is
        /// cheap, has no state, and is stable across runtimes and platforms — none of which is true of
        /// <c>System.Random</c> or of anything seeded from frame state.
        /// </remarks>
        public static ulong Hash(ulong worldSeed, int2 position, int channel)
        {
            const ulong FnvOffsetBasis = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong hash = FnvOffsetBasis;
            hash = Mix(hash, worldSeed);
            hash = Mix(hash, (ulong)(uint)position.x);
            hash = Mix(hash, (ulong)(uint)position.y);
            hash = Mix(hash, (ulong)(uint)channel);
            return hash;

            ulong Mix(ulong current, ulong value)
            {
                for (int i = 0; i < 8; i++)
                {
                    current ^= (value >> (i * 8)) & 0xFF;
                    current *= FnvPrime;
                }

                return current;
            }
        }
    }
}
