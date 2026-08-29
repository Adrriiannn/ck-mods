using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// What a dimension's generated ground and walls are made of, per biome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Biome page's "Ground" and "Walls" rows have compiled into the export since the page
    /// shipped, and the terrain provider laid dirt regardless — it carried one hardcoded tileset
    /// and nothing else in the file mentioned a tileset at all. This is the missing link, and it
    /// is deliberately the same shape as <see cref="DimensionOreBiomeGate"/>: the bootstrap knows
    /// which blocks a biome names but not where the biome lies, so the choice waits here and
    /// <see cref="BindZone"/> marries it to each zone carrying the biome's id.
    /// </para>
    /// <para>
    /// FALLING BACK TO DIRT IS THE POINT. Where nothing covers a cell, <see cref="TryResolve"/>
    /// answers false and the caller uses <see cref="DefaultTileset"/> — which is exactly the number
    /// the provider used before this existed, so a dimension whose author never picked a block
    /// generates byte-for-byte what it generated yesterday.
    /// </para>
    /// </remarks>
    public static class DimensionTerrainMaterialRegistry
    {
        /// <summary>Dirt — what every generated dimension was made of before this existed.</summary>
        public const int DefaultTileset = 0;

        private sealed class Row
        {
            public string BiomeId;
            public DimensionBounds LocalBounds;
            public int GroundTileset;
            public int WallTileset;
        }

        private sealed class PendingBiomeMaterial
        {
            public string DimensionId;
            public string BiomeId;
            public int GroundTileset;
            public int WallTileset;
        }

        /// <summary>Every row for one dimension, and whether any two of them overlap.</summary>
        private sealed class DimensionRows
        {
            public readonly List<Row> Rows = new List<Row>();

            /// <summary>
            /// True when two rows cover a common cell.
            /// </summary>
            /// <remarks>
            /// It is what decides whether the memo below may be trusted. With overlapping rows the
            /// answer for a cell depends on which row is LAST, and a memo that only checks "does my
            /// remembered row contain this cell" would happily hand back the loser. Overlap is an
            /// authoring mistake the compiler warns about, so the fast path stays on for every
            /// dimension whose biomes do not fight over ground.
            /// </remarks>
            public bool RowsOverlap;
        }

        private static readonly Dictionary<string, DimensionRows> ByDimension =
            new Dictionary<string, DimensionRows>(System.StringComparer.Ordinal);

        private static readonly List<PendingBiomeMaterial> Pending =
            new List<PendingBiomeMaterial>();

        // A raster walk asks about thousands of cells inside one rectangle, so the row that
        // answered last almost always answers the next one too. Without this the walk is
        // O(rows) per tile at 384 tiles a tick.
        private static string memoDimensionId;
        private static Row memoRow;

        /// <summary>
        /// A biome's ground and wall material, registered before anyone knows where the biome IS.
        /// </summary>
        /// <remarks>
        /// Emitted by the generated bootstrap. Registering the same biome twice replaces the
        /// waiting entry rather than stacking a second one, because that is a reload.
        /// </remarks>
        public static void RegisterBiomeMaterial(
            string dimensionId,
            string biomeId,
            int groundTileset,
            int wallTileset)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(biomeId))
            {
                return;
            }

            for (int i = 0; i < Pending.Count; i++)
            {
                if (string.Equals(Pending[i].DimensionId, dimensionId, System.StringComparison.Ordinal) &&
                    string.Equals(Pending[i].BiomeId, biomeId, System.StringComparison.Ordinal))
                {
                    Pending.RemoveAt(i);
                    break;
                }
            }

            Pending.Add(new PendingBiomeMaterial
            {
                DimensionId = dimensionId,
                BiomeId = biomeId,
                GroundTileset = groundTileset,
                WallTileset = wallTileset
            });
        }

        /// <summary>
        /// Gives a zone its biome's waiting material, if one exists. Call for every zone the
        /// service registers or updates; a zone whose kind names no waiting biome no-ops.
        /// </summary>
        /// <remarks>
        /// Binding the same zone again replaces its row instead of appending, because zone update
        /// fires repeatedly on one zone and a growing pile of identical rows would make the
        /// overlap rule below meaningless.
        /// </remarks>
        public static void BindZone(string dimensionId, string zoneKind, DimensionBounds localBounds)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(zoneKind))
            {
                return;
            }

            for (int i = 0; i < Pending.Count; i++)
            {
                PendingBiomeMaterial pending = Pending[i];
                if (!string.Equals(pending.DimensionId, dimensionId, System.StringComparison.Ordinal) ||
                    !string.Equals(pending.BiomeId, zoneKind, System.StringComparison.Ordinal))
                {
                    continue;
                }

                Register(
                    dimensionId,
                    zoneKind,
                    localBounds,
                    pending.GroundTileset,
                    pending.WallTileset);
                return;
            }
        }

        /// <summary>Records a material over a rectangle directly, without waiting for a zone.</summary>
        public static void Register(
            string dimensionId,
            string biomeId,
            DimensionBounds localBounds,
            int groundTileset,
            int wallTileset)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(biomeId))
            {
                return;
            }

            DimensionRows dimension;
            if (!ByDimension.TryGetValue(dimensionId, out dimension))
            {
                dimension = new DimensionRows();
                ByDimension[dimensionId] = dimension;
            }

            List<Row> rows = dimension.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].BiomeId, biomeId, System.StringComparison.Ordinal) &&
                    SameBounds(rows[i].LocalBounds, localBounds))
                {
                    // A rebind of a zone that has not moved. Replacing in place matters: appending
                    // would move this biome to the end of the list and hand it every cell it shares
                    // with another biome, so which biome owned an overlap would depend on how many
                    // times a zone happened to be updated.
                    rows[i].GroundTileset = groundTileset;
                    rows[i].WallTileset = wallTileset;
                    ForgetMemo();
                    return;
                }
            }

            Row added = new Row
            {
                BiomeId = biomeId,
                LocalBounds = localBounds,
                GroundTileset = groundTileset,
                WallTileset = wallTileset
            };

            for (int i = 0; i < rows.Count && !dimension.RowsOverlap; i++)
            {
                if (BoundsOverlap(rows[i].LocalBounds, localBounds))
                {
                    dimension.RowsOverlap = true;
                }
            }

            rows.Add(added);
            ForgetMemo();
        }

        /// <summary>
        /// What this LOCAL cell's ground and walls are made of. False means nothing covers it, and
        /// the caller should lay <see cref="DefaultTileset"/>.
        /// </summary>
        /// <remarks>
        /// TWO BIOMES OVER ONE CELL IS AN AUTHORING MISTAKE, NOT A RUNTIME ONE. A cell has exactly
        /// one ground, so the answer has to be one number: the row bound LAST wins, always, and the
        /// compiler raises <c>biome-terrain-regions-overlap</c> naming both biomes and which one
        /// takes the ground. Picking deterministically and saying so beats picking arbitrarily.
        /// </remarks>
        public static bool TryResolve(
            string dimensionId,
            int2 localPosition,
            out int groundTileset,
            out int wallTileset)
        {
            groundTileset = DefaultTileset;
            wallTileset = DefaultTileset;
            if (string.IsNullOrEmpty(dimensionId))
            {
                return false;
            }

            DimensionRows dimension;
            if (!ByDimension.TryGetValue(dimensionId, out dimension))
            {
                return false;
            }

            if (!dimension.RowsOverlap &&
                memoRow != null &&
                string.Equals(memoDimensionId, dimensionId, System.StringComparison.Ordinal) &&
                memoRow.LocalBounds.Contains(localPosition))
            {
                groundTileset = memoRow.GroundTileset;
                wallTileset = memoRow.WallTileset;
                return true;
            }

            List<Row> rows = dimension.Rows;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (!rows[i].LocalBounds.Contains(localPosition))
                {
                    continue;
                }

                memoDimensionId = dimensionId;
                memoRow = rows[i];
                groundTileset = rows[i].GroundTileset;
                wallTileset = rows[i].WallTileset;
                return true;
            }

            return false;
        }

        public static void Clear(string dimensionId)
        {
            if (string.IsNullOrEmpty(dimensionId))
            {
                return;
            }

            ByDimension.Remove(dimensionId);
            Pending.RemoveAll(p =>
                string.Equals(p.DimensionId, dimensionId, System.StringComparison.Ordinal));
            ForgetMemo();
        }

        public static void ClearAll()
        {
            ByDimension.Clear();
            Pending.Clear();
            ForgetMemo();
        }

        private static void ForgetMemo()
        {
            memoDimensionId = null;
            memoRow = null;
        }

        private static bool BoundsOverlap(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x < b.MaxExclusive.x &&
                   b.Min.x < a.MaxExclusive.x &&
                   a.Min.y < b.MaxExclusive.y &&
                   b.Min.y < a.MaxExclusive.y;
        }

        private static bool SameBounds(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x == b.Min.x &&
                   a.Min.y == b.Min.y &&
                   a.MaxExclusive.x == b.MaxExclusive.x &&
                   a.MaxExclusive.y == b.MaxExclusive.y;
        }
    }
}
