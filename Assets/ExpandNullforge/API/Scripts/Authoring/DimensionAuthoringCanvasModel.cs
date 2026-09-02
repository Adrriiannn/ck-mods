using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionAuthoringCanvasItem
    {
        public readonly DimensionAuthoringPreviewEntry Entry;
        public readonly DimensionAuthoringPreviewLayerKind LayerKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly DimensionBounds LocalBounds;
        public readonly int Priority;
        public readonly uint FillColorRgba;
        public readonly uint OutlineColorRgba;
        public readonly bool HasConflict;
        public readonly DimensionAuthoringSeverity HighestConflictSeverity;
        public readonly int ConflictCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;

        public DimensionAuthoringCanvasItem(
            DimensionAuthoringPreviewEntry entry,
            uint fillColorRgba,
            uint outlineColorRgba,
            bool hasConflict,
            DimensionAuthoringSeverity highestConflictSeverity,
            int conflictCount,
            int errorCount,
            int warningCount)
        {
            Entry = entry;
            LayerKind = entry.LayerKind;
            RecordId = entry.RecordId ?? string.Empty;
            DisplayName = entry.DisplayName ?? string.Empty;
            BiomeId = entry.BiomeId ?? string.Empty;
            ZoneId = entry.ZoneId ?? string.Empty;
            LocalBounds = entry.LocalBounds;
            Priority = entry.Priority;
            FillColorRgba = fillColorRgba;
            OutlineColorRgba = outlineColorRgba;
            HasConflict = hasConflict;
            HighestConflictSeverity = highestConflictSeverity;
            ConflictCount = conflictCount < 0 ? 0 : conflictCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
        }
    }

    public readonly struct DimensionAuthoringCanvasLayer
    {
        public readonly DimensionAuthoringPreviewLayerKind LayerKind;
        public readonly string DisplayName;
        public readonly int SortOrder;
        public readonly bool VisibleByDefault;
        public readonly bool FillByDefault;
        public readonly bool OutlineByDefault;
        public readonly uint DefaultFillColorRgba;
        public readonly uint DefaultOutlineColorRgba;
        public readonly IReadOnlyList<DimensionAuthoringCanvasItem> Items;

        public DimensionAuthoringCanvasLayer(
            DimensionAuthoringPreviewLayerKind layerKind,
            string displayName,
            int sortOrder,
            bool visibleByDefault,
            bool fillByDefault,
            bool outlineByDefault,
            uint defaultFillColorRgba,
            uint defaultOutlineColorRgba,
            IReadOnlyList<DimensionAuthoringCanvasItem> items)
        {
            LayerKind = layerKind;
            DisplayName = displayName ?? string.Empty;
            SortOrder = sortOrder;
            VisibleByDefault = visibleByDefault;
            FillByDefault = fillByDefault;
            OutlineByDefault = outlineByDefault;
            DefaultFillColorRgba = defaultFillColorRgba;
            DefaultOutlineColorRgba = defaultOutlineColorRgba;
            Items = items ?? new List<DimensionAuthoringCanvasItem>();
        }
    }

    public readonly struct DimensionAuthoringCanvasModel
    {
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly DimensionBounds PlayableLocalBounds;
        public readonly int CoordinateShellPaddingTiles;
        public readonly int ItemCount;
        public readonly int ConflictCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly IReadOnlyList<DimensionAuthoringCanvasLayer> Layers;

        public DimensionAuthoringCanvasModel(
            string dimensionId,
            string displayName,
            DimensionBounds playableLocalBounds,
            int coordinateShellPaddingTiles,
            int itemCount,
            int conflictCount,
            int errorCount,
            int warningCount,
            IReadOnlyList<DimensionAuthoringCanvasLayer> layers)
        {
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PlayableLocalBounds = playableLocalBounds;
            CoordinateShellPaddingTiles = coordinateShellPaddingTiles < 0 ? 0 : coordinateShellPaddingTiles;
            ItemCount = itemCount < 0 ? 0 : itemCount;
            ConflictCount = conflictCount < 0 ? 0 : conflictCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Layers = layers ?? new List<DimensionAuthoringCanvasLayer>();
        }
    }

    public static class DimensionAuthoringCanvasModelUtility
    {
        public static DimensionAuthoringCanvasModel BuildCanvas(
            DimensionAuthoringPreviewSummary summary)
        {
            return BuildCanvas(
                summary,
                DimensionAuthoringSpatialConflictUtility.BuildConflicts(summary));
        }

        public static DimensionAuthoringCanvasModel BuildCanvas(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts)
        {
            List<DimensionAuthoringCanvasLayer> layers =
                new List<DimensionAuthoringCanvasLayer>();
            int itemCount = 0;
            int conflictCount = conflicts == null ? 0 : conflicts.Count;
            int errorCount = 0;
            int warningCount = 0;

            if (conflicts != null)
            {
                for (int i = 0; i < conflicts.Count; i++)
                {
                    DimensionAuthoringSpatialConflict conflict = conflicts[i];
                    if (conflict.Severity == DimensionAuthoringSeverity.Error)
                    {
                        errorCount++;
                    }
                    else if (conflict.Severity == DimensionAuthoringSeverity.Warning)
                    {
                        warningCount++;
                    }
                }
            }

            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.PlayableBounds, layers, ref itemCount);
            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.BiomeRegion, layers, ref itemCount);
            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.GenerationPassBounds, layers, ref itemCount);
            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.ScenePlacement, layers, ref itemCount);
            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.ResourceNode, layers, ref itemCount);
            AddLayer(summary, conflicts, DimensionAuthoringPreviewLayerKind.SpawnRule, layers, ref itemCount);

            return new DimensionAuthoringCanvasModel(
                summary.DimensionId,
                summary.DisplayName,
                summary.PlayableLocalBounds,
                summary.CoordinateShellPaddingTiles,
                itemCount,
                conflictCount,
                errorCount,
                warningCount,
                layers);
        }

        private static void AddLayer(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts,
            DimensionAuthoringPreviewLayerKind layerKind,
            List<DimensionAuthoringCanvasLayer> layers,
            ref int itemCount)
        {
            List<DimensionAuthoringCanvasItem> items =
                BuildItems(summary, conflicts, layerKind);
            itemCount += items.Count;

            DimensionAuthoringCanvasLayer layer = new DimensionAuthoringCanvasLayer(
                layerKind,
                GetLayerDisplayName(layerKind),
                GetLayerSortOrder(layerKind),
                true,
                ShouldFillLayer(layerKind),
                true,
                GetLayerFillColor(layerKind),
                GetLayerOutlineColor(layerKind),
                items);
            layers.Add(layer);
        }

        private static List<DimensionAuthoringCanvasItem> BuildItems(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts,
            DimensionAuthoringPreviewLayerKind layerKind)
        {
            List<DimensionAuthoringCanvasItem> items =
                new List<DimensionAuthoringCanvasItem>();
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries = summary.Entries;
            if (entries == null)
            {
                return items;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = entries[i];
                if (entry.LayerKind != layerKind || !entry.HasLocalBounds)
                {
                    continue;
                }

                DimensionAuthoringSeverity severity;
                int total;
                int errors;
                int warnings;
                CountConflicts(entry, conflicts, out severity, out total, out errors, out warnings);
                uint outlineColor = total > 0
                    ? GetConflictOutlineColor(severity)
                    : GetLayerOutlineColor(layerKind);
                uint fillColor = entry.ColorRgba != 0u
                    ? entry.ColorRgba
                    : GetLayerFillColor(layerKind);

                items.Add(new DimensionAuthoringCanvasItem(
                    entry,
                    fillColor,
                    outlineColor,
                    total > 0,
                    severity,
                    total,
                    errors,
                    warnings));
            }

            items.Sort(CompareItems);
            return items;
        }

        private static void CountConflicts(
            DimensionAuthoringPreviewEntry entry,
            IReadOnlyList<DimensionAuthoringSpatialConflict> conflicts,
            out DimensionAuthoringSeverity highestSeverity,
            out int total,
            out int errors,
            out int warnings)
        {
            highestSeverity = DimensionAuthoringSeverity.Info;
            total = 0;
            errors = 0;
            warnings = 0;

            if (conflicts == null)
            {
                return;
            }

            for (int i = 0; i < conflicts.Count; i++)
            {
                DimensionAuthoringSpatialConflict conflict = conflicts[i];
                if (!MatchesConflict(entry, conflict))
                {
                    continue;
                }

                total++;
                if (conflict.Severity == DimensionAuthoringSeverity.Error)
                {
                    errors++;
                    highestSeverity = DimensionAuthoringSeverity.Error;
                }
                else if (conflict.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warnings++;
                    if (highestSeverity != DimensionAuthoringSeverity.Error)
                    {
                        highestSeverity = DimensionAuthoringSeverity.Warning;
                    }
                }
            }
        }

        private static bool MatchesConflict(
            DimensionAuthoringPreviewEntry entry,
            DimensionAuthoringSpatialConflict conflict)
        {
            if (entry.LayerKind == conflict.PrimaryLayer &&
                string.Equals(entry.RecordId, conflict.PrimaryRecordId))
            {
                return true;
            }

            return entry.LayerKind == conflict.SecondaryLayer &&
                string.Equals(entry.RecordId, conflict.SecondaryRecordId);
        }

        private static int CompareItems(
            DimensionAuthoringCanvasItem left,
            DimensionAuthoringCanvasItem right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int biome = string.CompareOrdinal(left.BiomeId, right.BiomeId);
            if (biome != 0)
            {
                return biome;
            }

            return string.CompareOrdinal(left.RecordId, right.RecordId);
        }

        private static int GetLayerSortOrder(DimensionAuthoringPreviewLayerKind layerKind)
        {
            switch (layerKind)
            {
                case DimensionAuthoringPreviewLayerKind.PlayableBounds:
                    return 0;
                case DimensionAuthoringPreviewLayerKind.BiomeRegion:
                    return 10;
                case DimensionAuthoringPreviewLayerKind.GenerationPassBounds:
                    return 20;
                case DimensionAuthoringPreviewLayerKind.ScenePlacement:
                    return 30;
                case DimensionAuthoringPreviewLayerKind.ResourceNode:
                    return 40;
                case DimensionAuthoringPreviewLayerKind.SpawnRule:
                    return 50;
                default:
                    return 100;
            }
        }

        private static string GetLayerDisplayName(DimensionAuthoringPreviewLayerKind layerKind)
        {
            switch (layerKind)
            {
                case DimensionAuthoringPreviewLayerKind.PlayableBounds:
                    return "Playable Bounds";
                case DimensionAuthoringPreviewLayerKind.BiomeRegion:
                    return "Biome Regions";
                case DimensionAuthoringPreviewLayerKind.GenerationPassBounds:
                    return "Generation Pass Bounds";
                case DimensionAuthoringPreviewLayerKind.ScenePlacement:
                    return "Scene Placements";
                case DimensionAuthoringPreviewLayerKind.ResourceNode:
                    return "Resource Nodes";
                case DimensionAuthoringPreviewLayerKind.SpawnRule:
                    return "Spawn Rules";
                default:
                    return layerKind.ToString();
            }
        }

        private static bool ShouldFillLayer(DimensionAuthoringPreviewLayerKind layerKind)
        {
            return layerKind == DimensionAuthoringPreviewLayerKind.BiomeRegion ||
                layerKind == DimensionAuthoringPreviewLayerKind.GenerationPassBounds;
        }

        private static uint GetLayerFillColor(DimensionAuthoringPreviewLayerKind layerKind)
        {
            switch (layerKind)
            {
                case DimensionAuthoringPreviewLayerKind.PlayableBounds:
                    return 0xE6E6E620u;
                case DimensionAuthoringPreviewLayerKind.BiomeRegion:
                    return 0x4AA3C055u;
                case DimensionAuthoringPreviewLayerKind.GenerationPassBounds:
                    return 0xF0C35A33u;
                case DimensionAuthoringPreviewLayerKind.ScenePlacement:
                    return 0xA66CFF55u;
                case DimensionAuthoringPreviewLayerKind.ResourceNode:
                    return 0x73D46B55u;
                case DimensionAuthoringPreviewLayerKind.SpawnRule:
                    return 0xFF7A6655u;
                default:
                    return 0xFFFFFF33u;
            }
        }

        private static uint GetLayerOutlineColor(DimensionAuthoringPreviewLayerKind layerKind)
        {
            switch (layerKind)
            {
                case DimensionAuthoringPreviewLayerKind.PlayableBounds:
                    return 0xE6E6E6CCu;
                case DimensionAuthoringPreviewLayerKind.BiomeRegion:
                    return 0x4AA3C0CCu;
                case DimensionAuthoringPreviewLayerKind.GenerationPassBounds:
                    return 0xF0C35ACCu;
                case DimensionAuthoringPreviewLayerKind.ScenePlacement:
                    return 0xA66CFFCCu;
                case DimensionAuthoringPreviewLayerKind.ResourceNode:
                    return 0x73D46BCCu;
                case DimensionAuthoringPreviewLayerKind.SpawnRule:
                    return 0xFF7A66CCu;
                default:
                    return 0xFFFFFFFFu;
            }
        }

        private static uint GetConflictOutlineColor(DimensionAuthoringSeverity severity)
        {
            if (severity == DimensionAuthoringSeverity.Error)
            {
                return 0xFF5C5CFFu;
            }

            if (severity == DimensionAuthoringSeverity.Warning)
            {
                return 0xFFD35AFFu;
            }

            return 0xB8C7D9FFu;
        }
    }
}
