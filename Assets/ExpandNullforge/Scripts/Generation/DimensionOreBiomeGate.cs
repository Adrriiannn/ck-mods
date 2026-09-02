using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>
    /// Which ores each biome allows, as the vein scatter reads it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first real consumer of the biome pages' ore lists: those ids have compiled into
    /// generation tables since the biome page shipped, and nothing ever read them — the
    /// identity-table bug class in its purest form. This gate closes it.
    /// </para>
    /// <para>
    /// Allow-all when nothing covers a cell, on purpose: a biome that never named ores is a
    /// biome whose author has not decided, not one that forbids everything. Only a biome that
    /// NAMES ores narrows its ground to them.
    /// </para>
    /// </remarks>
    public static class DimensionOreBiomeGate
    {
        private sealed class Row
        {
            public DimensionBounds LocalBounds;
            public HashSet<string> AllowedOreItemIds;
        }

        private static readonly Dictionary<string, List<Row>> ByDimension =
            new Dictionary<string, List<Row>>(System.StringComparer.Ordinal);

        public static void Register(
            string dimensionId,
            DimensionBounds localBounds,
            IReadOnlyList<string> allowedOreItemIds)
        {
            if (string.IsNullOrEmpty(dimensionId) ||
                allowedOreItemIds == null ||
                allowedOreItemIds.Count == 0)
            {
                return;
            }

            List<Row> rows;
            if (!ByDimension.TryGetValue(dimensionId, out rows))
            {
                rows = new List<Row>();
                ByDimension[dimensionId] = rows;
            }

            HashSet<string> allowed = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < allowedOreItemIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(allowedOreItemIds[i]))
                {
                    allowed.Add(allowedOreItemIds[i]);
                }
            }

            if (allowed.Count == 0)
            {
                return;
            }

            // Binding is idempotent, because it is not a one-shot. It happens at
            // zone registration so the bootstrap's own zones reach it too, and zone UPDATE fires
            // repeatedly on one zone — without this, one zone updated a hundred times leaves
            // a hundred identical rows for Allows to walk per ore.
            for (int i = 0; i < rows.Count; i++)
            {
                if (SameBounds(rows[i].LocalBounds, localBounds) &&
                    rows[i].AllowedOreItemIds.SetEquals(allowed))
                {
                    return;
                }
            }

            rows.Add(new Row { LocalBounds = localBounds, AllowedOreItemIds = allowed });
        }

        private static bool SameBounds(DimensionBounds a, DimensionBounds b)
        {
            return a.Min.x == b.Min.x &&
                   a.Min.y == b.Min.y &&
                   a.MaxExclusive.x == b.MaxExclusive.x &&
                   a.MaxExclusive.y == b.MaxExclusive.y;
        }

        private sealed class PendingBiomeOres
        {
            public string DimensionId;
            public string BiomeId;
            public List<string> OreItemIds;
        }

        private static readonly List<PendingBiomeOres> Pending = new List<PendingBiomeOres>();

        /// <summary>
        /// A biome's ore list, registered before anyone knows where the biome IS.
        /// </summary>
        /// <remarks>
        /// The generated bootstrap knows which ores a biome names but not the biome's bounds —
        /// those exist only once the manifest's zones apply. So the list waits here, and
        /// <see cref="BindZone"/> marries it to each zone that carries the biome's id. This
        /// closes the audit's finding that the gate's only Register call sat on a path with no
        /// callers, leaving the biome pages' ore chips decorative.
        /// </remarks>
        public static void RegisterBiomeOres(
            string dimensionId,
            string biomeId,
            IReadOnlyList<string> allowedOreItemIds)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(biomeId) ||
                allowedOreItemIds == null || allowedOreItemIds.Count == 0)
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

            Pending.Add(new PendingBiomeOres
            {
                DimensionId = dimensionId,
                BiomeId = biomeId,
                OreItemIds = new List<string>(allowedOreItemIds)
            });
        }

        /// <summary>
        /// Gives a freshly registered zone its biome's waiting ore list, if one exists.
        /// Call for every zone the manifest applies; zones without a waiting list no-op.
        /// </summary>
        public static void BindZone(string dimensionId, string zoneKind, DimensionBounds localBounds)
        {
            if (string.IsNullOrEmpty(dimensionId) || string.IsNullOrEmpty(zoneKind))
            {
                return;
            }

            for (int i = 0; i < Pending.Count; i++)
            {
                if (string.Equals(Pending[i].DimensionId, dimensionId, System.StringComparison.Ordinal) &&
                    string.Equals(Pending[i].BiomeId, zoneKind, System.StringComparison.Ordinal))
                {
                    Register(dimensionId, localBounds, Pending[i].OreItemIds);
                    return;
                }
            }
        }

        public static void Clear(string dimensionId)
        {
            if (!string.IsNullOrEmpty(dimensionId))
            {
                ByDimension.Remove(dimensionId);
                Pending.RemoveAll(p =>
                    string.Equals(p.DimensionId, dimensionId, System.StringComparison.Ordinal));
            }
        }

        public static void ClearAll()
        {
            ByDimension.Clear();
            Pending.Clear();
        }

        /// <summary>
        /// Whether this ore may appear at this LOCAL position. True when nothing covers it.
        /// </summary>
        public static bool Allows(string dimensionId, int2 localPosition, string oreItemId)
        {
            List<Row> rows;
            if (string.IsNullOrEmpty(dimensionId) || !ByDimension.TryGetValue(dimensionId, out rows))
            {
                return true;
            }

            bool covered = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].LocalBounds.Contains(localPosition))
                {
                    continue;
                }

                covered = true;
                if (rows[i].AllowedOreItemIds.Contains(oreItemId))
                {
                    return true;
                }
            }

            return !covered;
        }
    }
}
