using System.Collections.Generic;
using PugTilemap;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Which overlays each custom tileset scatters over its own ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Filled when a tileset registers at load and read per generated cell, so the generator can ask
    /// "what grows on this block?" without touching authoring assets — which do not exist at runtime.
    /// </para>
    /// <para>
    /// Keyed by tileset id rather than name because that is what a written tile carries. The generator
    /// has an id in hand and nothing else.
    /// </para>
    /// </remarks>
    public static class DimensionOverlayRuleRegistry
    {
        private static readonly Dictionary<int, List<DimensionOverlayRule>> byTileset =
            new Dictionary<int, List<DimensionOverlayRule>>();

        private static readonly List<DimensionOverlayRule> none = new List<DimensionOverlayRule>();

        /// <summary>
        /// Records that <paramref name="tilesetId"/> scatters <paramref name="rules"/>.
        /// </summary>
        /// <remarks>
        /// Replaces rather than appends: registering a tileset twice is a reload, not a request for
        /// twice as much grass.
        /// </remarks>
        public static void Register(int tilesetId, IReadOnlyList<DimensionOverlayRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                byTileset.Remove(tilesetId);
                return;
            }

            List<DimensionOverlayRule> copy = new List<DimensionOverlayRule>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Density > 0f)
                {
                    copy.Add(rules[i]);
                }
            }

            if (copy.Count == 0)
            {
                byTileset.Remove(tilesetId);
                return;
            }

            byTileset[tilesetId] = copy;
        }

        /// <summary>
        /// What <paramref name="tilesetId"/> scatters, or an empty list.
        /// </summary>
        public static IReadOnlyList<DimensionOverlayRule> For(int tilesetId)
        {
            List<DimensionOverlayRule> rules;
            return byTileset.TryGetValue(tilesetId, out rules) ? rules : none;
        }

        /// <summary>Whether anything at all scatters, so the generator can skip the pass entirely.</summary>
        public static bool Any
        {
            get { return byTileset.Count > 0; }
        }

        public static void Clear()
        {
            byTileset.Clear();
        }

        /// <summary>
        /// The overlay layers a block can scatter, paired with the tile they write.
        /// </summary>
        /// <remarks>
        /// Deliberately a short, explicit list rather than "every optional state". Most states are not
        /// scatterable at all — tilled and watered soil are made by the player's tools, cracks appear
        /// from damage, ore is placed by vein generation — and scattering those would produce terrain
        /// that looks farmed or mined before anyone touched it.
        /// </remarks>
        public static bool TryGetScatterLayer(string stateKey, out LayerName layer, out TileType tileType)
        {
            switch (stateKey)
            {
                case "grass":
                    layer = LayerName.smallGrass;
                    tileType = TileType.smallGrass;
                    return true;
                case "pebbles":
                    layer = LayerName.smallStones;
                    tileType = TileType.smallStones;
                    return true;
                case "roots":
                    layer = LayerName.bigRoot;
                    tileType = TileType.bigRoot;
                    return true;
                case "slime":
                    layer = LayerName.groundSlime;
                    tileType = TileType.groundSlime;
                    return true;
                default:
                    layer = default;
                    tileType = TileType.none;
                    return false;
            }
        }
    }
}
