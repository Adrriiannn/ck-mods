using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public enum DimensionAuthoringSpatialConflictKind
    {
        OutsidePlayableBounds = 0,
        OutsideBiomeRegion = 1,
        ExactPlacementOverlap = 2
    }

    public readonly struct DimensionAuthoringSpatialConflict
    {
        public readonly DimensionAuthoringSeverity Severity;
        public readonly DimensionAuthoringSpatialConflictKind Kind;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string PrimaryRecordId;
        public readonly string SecondaryRecordId;
        public readonly DimensionAuthoringPreviewLayerKind PrimaryLayer;
        public readonly DimensionAuthoringPreviewLayerKind SecondaryLayer;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly string Code;
        public readonly string Message;

        public DimensionAuthoringSpatialConflict(
            DimensionAuthoringSeverity severity,
            DimensionAuthoringSpatialConflictKind kind,
            string dimensionId,
            string biomeId,
            string primaryRecordId,
            string secondaryRecordId,
            DimensionAuthoringPreviewLayerKind primaryLayer,
            DimensionAuthoringPreviewLayerKind secondaryLayer,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            string code,
            string message)
        {
            Severity = severity;
            Kind = kind;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            PrimaryRecordId = primaryRecordId ?? string.Empty;
            SecondaryRecordId = secondaryRecordId ?? string.Empty;
            PrimaryLayer = primaryLayer;
            SecondaryLayer = secondaryLayer;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public static class DimensionAuthoringSpatialConflictUtility
    {
        public static List<DimensionAuthoringSpatialConflict> BuildConflicts(
            DimensionAuthoringPreviewSummary summary)
        {
            return BuildConflicts(summary, string.Empty);
        }

        public static List<DimensionAuthoringSpatialConflict> BuildConflicts(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            List<DimensionAuthoringSpatialConflict> conflicts =
                new List<DimensionAuthoringSpatialConflict>();
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries = summary.Entries;
            if (entries == null)
            {
                return conflicts;
            }

            DimensionAuthoringPreviewEntry playable;
            bool hasPlayable = TryGetPlayableBounds(entries, out playable);

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = entries[i];
                if (!ShouldInspect(entry, biomeId))
                {
                    continue;
                }

                AddPlayableBoundsConflict(summary, entry, playable, hasPlayable, conflicts);
                AddBiomeRegionConflict(summary, entry, entries, conflicts);
            }

            AddExactPlacementOverlaps(summary, entries, biomeId, conflicts);
            return conflicts;
        }

        private static bool ShouldInspect(
            DimensionAuthoringPreviewEntry entry,
            string biomeId)
        {
            if (!entry.HasLocalBounds || !IsContentLayer(entry.LayerKind))
            {
                return false;
            }

            return string.IsNullOrEmpty(biomeId) || entry.BiomeId == biomeId;
        }

        private static bool IsContentLayer(
            DimensionAuthoringPreviewLayerKind layer)
        {
            return layer == DimensionAuthoringPreviewLayerKind.ScenePlacement ||
                layer == DimensionAuthoringPreviewLayerKind.ResourceNode ||
                layer == DimensionAuthoringPreviewLayerKind.SpawnRule ||
                layer == DimensionAuthoringPreviewLayerKind.GenerationPassBounds;
        }

        private static bool IsExactPlacementLayer(
            DimensionAuthoringPreviewLayerKind layer)
        {
            return layer == DimensionAuthoringPreviewLayerKind.ScenePlacement ||
                layer == DimensionAuthoringPreviewLayerKind.ResourceNode;
        }

        private static bool TryGetPlayableBounds(
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries,
            out DimensionAuthoringPreviewEntry playable)
        {
            playable = default(DimensionAuthoringPreviewEntry);
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = entries[i];
                if (entry.LayerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds &&
                    entry.HasLocalBounds)
                {
                    playable = entry;
                    return true;
                }
            }

            return false;
        }

        private static void AddPlayableBoundsConflict(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringPreviewEntry entry,
            DimensionAuthoringPreviewEntry playable,
            bool hasPlayable,
            List<DimensionAuthoringSpatialConflict> conflicts)
        {
            if (!hasPlayable || ContainsBounds(playable.LocalBounds, entry.LocalBounds))
            {
                return;
            }

            conflicts.Add(new DimensionAuthoringSpatialConflict(
                DimensionAuthoringSeverity.Error,
                DimensionAuthoringSpatialConflictKind.OutsidePlayableBounds,
                summary.DimensionId,
                entry.BiomeId,
                entry.RecordId,
                playable.RecordId,
                entry.LayerKind,
                playable.LayerKind,
                true,
                entry.LocalBounds,
                "outside-playable-bounds",
                DisplayName(entry) + " is outside the dimension's playable bounds."));
        }

        private static void AddBiomeRegionConflict(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringPreviewEntry entry,
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries,
            List<DimensionAuthoringSpatialConflict> conflicts)
        {
            if (string.IsNullOrEmpty(entry.BiomeId) ||
                entry.LayerKind == DimensionAuthoringPreviewLayerKind.GenerationPassBounds ||
                IsContainedInMatchingBiomeRegion(entry, entries))
            {
                return;
            }

            conflicts.Add(new DimensionAuthoringSpatialConflict(
                DimensionAuthoringSeverity.Warning,
                DimensionAuthoringSpatialConflictKind.OutsideBiomeRegion,
                summary.DimensionId,
                entry.BiomeId,
                entry.RecordId,
                string.Empty,
                entry.LayerKind,
                DimensionAuthoringPreviewLayerKind.BiomeRegion,
                true,
                entry.LocalBounds,
                "outside-biome-region",
                DisplayName(entry) + " is not fully inside a matching biome region."));
        }

        private static bool IsContainedInMatchingBiomeRegion(
            DimensionAuthoringPreviewEntry entry,
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry region = entries[i];
                if (region.LayerKind != DimensionAuthoringPreviewLayerKind.BiomeRegion ||
                    !region.HasLocalBounds ||
                    region.BiomeId != entry.BiomeId)
                {
                    continue;
                }

                if (ContainsBounds(region.LocalBounds, entry.LocalBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddExactPlacementOverlaps(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries,
            string biomeId,
            List<DimensionAuthoringSpatialConflict> conflicts)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry left = entries[i];
                if (!left.HasLocalBounds ||
                    !IsExactPlacementLayer(left.LayerKind) ||
                    (!string.IsNullOrEmpty(biomeId) && left.BiomeId != biomeId))
                {
                    continue;
                }

                for (int j = i + 1; j < entries.Count; j++)
                {
                    DimensionAuthoringPreviewEntry right = entries[j];
                    if (!right.HasLocalBounds ||
                        !IsExactPlacementLayer(right.LayerKind) ||
                        (!string.IsNullOrEmpty(biomeId) && right.BiomeId != biomeId) ||
                        !SameBiomeOrGlobal(left, right) ||
                        !BoundsOverlap(left.LocalBounds, right.LocalBounds))
                    {
                        continue;
                    }

                    conflicts.Add(new DimensionAuthoringSpatialConflict(
                        DimensionAuthoringSeverity.Warning,
                        DimensionAuthoringSpatialConflictKind.ExactPlacementOverlap,
                        summary.DimensionId,
                        string.IsNullOrEmpty(left.BiomeId) ? right.BiomeId : left.BiomeId,
                        left.RecordId,
                        right.RecordId,
                        left.LayerKind,
                        right.LayerKind,
                        true,
                        left.LocalBounds,
                        "exact-placement-overlap",
                        DisplayName(left) + " overlaps " + DisplayName(right) + "."));
                }
            }
        }

        private static bool SameBiomeOrGlobal(
            DimensionAuthoringPreviewEntry left,
            DimensionAuthoringPreviewEntry right)
        {
            return string.IsNullOrEmpty(left.BiomeId) ||
                string.IsNullOrEmpty(right.BiomeId) ||
                left.BiomeId == right.BiomeId;
        }

        private static bool ContainsBounds(
            DimensionBounds outer,
            DimensionBounds inner)
        {
            return inner.Min.x >= outer.Min.x &&
                inner.Min.y >= outer.Min.y &&
                inner.MaxExclusive.x <= outer.MaxExclusive.x &&
                inner.MaxExclusive.y <= outer.MaxExclusive.y;
        }

        private static bool BoundsOverlap(
            DimensionBounds left,
            DimensionBounds right)
        {
            return left.Min.x < right.MaxExclusive.x &&
                left.MaxExclusive.x > right.Min.x &&
                left.Min.y < right.MaxExclusive.y &&
                left.MaxExclusive.y > right.Min.y;
        }

        private static string DisplayName(
            DimensionAuthoringPreviewEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.DisplayName))
            {
                return entry.DisplayName;
            }

            if (!string.IsNullOrEmpty(entry.RecordId))
            {
                return entry.RecordId;
            }

            return entry.LayerKind.ToString();
        }
    }
}
