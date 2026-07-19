using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerSearchEntryKind
    {
        Content = 0,
        Preview = 1,
        Issue = 2
    }

    public readonly struct DimensionTemplateCustomizerSearchEntry
    {
        public readonly DimensionTemplateCustomizerSearchEntryKind Kind;
        public readonly DimensionAuthoringSeverity Severity;
        public readonly string SectionId;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string Message;
        public readonly string SearchText;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;
        public readonly int Priority;

        public DimensionTemplateCustomizerSearchEntry(
            DimensionTemplateCustomizerSearchEntryKind kind,
            DimensionAuthoringSeverity severity,
            string sectionId,
            string biomeId,
            string zoneId,
            string recordKind,
            string recordId,
            string displayName,
            string message,
            string searchText,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            int priority)
        {
            Kind = kind;
            Severity = severity;
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Message = message ?? string.Empty;
            SearchText = searchText ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            Priority = priority < 0 ? 0 : priority;
        }
    }

    public sealed class DimensionTemplateCustomizerSearchIndex
    {
        public DimensionTemplateCustomizerSearchIndex(
            string dimensionId,
            string displayName,
            int entryCount,
            int contentCount,
            int previewCount,
            int issueCount,
            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> entries)
        {
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EntryCount = entryCount < 0 ? 0 : entryCount;
            ContentCount = contentCount < 0 ? 0 : contentCount;
            PreviewCount = previewCount < 0 ? 0 : previewCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            Entries = entries ?? new List<DimensionTemplateCustomizerSearchEntry>();
        }

        public string DimensionId { get; private set; }

        public string DisplayName { get; private set; }

        public int EntryCount { get; private set; }

        public int ContentCount { get; private set; }

        public int PreviewCount { get; private set; }

        public int IssueCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerSearchEntry> Entries { get; private set; }
    }

    public static class DimensionTemplateCustomizerSearchIndexUtility
    {
        public static DimensionTemplateCustomizerSearchIndex Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            if (workspace == null)
            {
                return new DimensionTemplateCustomizerSearchIndex(
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    0,
                    0,
                    new List<DimensionTemplateCustomizerSearchEntry>());
            }

            DimensionAuthoringPreviewSummary preview = workspace.Preview;
            List<DimensionTemplateCustomizerSearchEntry> entries =
                new List<DimensionTemplateCustomizerSearchEntry>();

            AddContentEntries(entries, preview.ContentEntries);
            AddPreviewEntries(entries, preview.Entries);
            AddIssueEntries(entries, preview.Issues);
            entries.Sort(CompareEntries);

            int contentCount;
            int previewCount;
            int issueCount;
            CountEntries(entries, out contentCount, out previewCount, out issueCount);

            return new DimensionTemplateCustomizerSearchIndex(
                preview.DimensionId,
                preview.DisplayName,
                entries.Count,
                contentCount,
                previewCount,
                issueCount,
                entries);
        }

        public static IReadOnlyList<DimensionTemplateCustomizerSearchEntry> Search(
            DimensionTemplateCustomizerSearchIndex index,
            string query,
            string sectionId,
            string biomeId,
            int maxResults)
        {
            List<DimensionTemplateCustomizerSearchEntry> results =
                new List<DimensionTemplateCustomizerSearchEntry>();
            if (index == null || index.Entries == null)
            {
                return results;
            }

            string normalizedQuery = Normalize(query);
            string normalizedSection = sectionId ?? string.Empty;
            string normalizedBiome = biomeId ?? string.Empty;
            int resultLimit = maxResults <= 0 ? 64 : maxResults;

            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> entries = index.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                DimensionTemplateCustomizerSearchEntry entry = entries[i];
                if (!string.IsNullOrEmpty(normalizedSection) &&
                    entry.SectionId != normalizedSection)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedBiome) &&
                    entry.BiomeId != normalizedBiome)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(normalizedQuery) &&
                    Normalize(entry.SearchText).IndexOf(normalizedQuery) < 0)
                {
                    continue;
                }

                results.Add(entry);
                if (results.Count >= resultLimit)
                {
                    break;
                }
            }

            return results;
        }

        private static void AddContentEntries(
            List<DimensionTemplateCustomizerSearchEntry> entries,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries)
        {
            if (entries == null || contentEntries == null)
            {
                return;
            }

            for (int i = 0; i < contentEntries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = contentEntries[i];
                string recordKind = entry.Kind.ToString();
                entries.Add(new DimensionTemplateCustomizerSearchEntry(
                    DimensionTemplateCustomizerSearchEntryKind.Content,
                    DimensionAuthoringSeverity.Info,
                    ResolveSectionForContent(entry.Kind),
                    entry.BiomeId,
                    entry.ZoneId,
                    recordKind,
                    entry.RecordId,
                    entry.DisplayName,
                    entry.Notes,
                    BuildSearchText(recordKind, entry.RecordId, entry.DisplayName, entry.BiomeId, entry.ZoneId, entry.Notes),
                    false,
                    default(DimensionBounds),
                    50));
            }
        }

        private static void AddPreviewEntries(
            List<DimensionTemplateCustomizerSearchEntry> entries,
            IReadOnlyList<DimensionAuthoringPreviewEntry> previewEntries)
        {
            if (entries == null || previewEntries == null)
            {
                return;
            }

            for (int i = 0; i < previewEntries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = previewEntries[i];
                string recordKind = entry.LayerKind.ToString();
                entries.Add(new DimensionTemplateCustomizerSearchEntry(
                    DimensionTemplateCustomizerSearchEntryKind.Preview,
                    DimensionAuthoringSeverity.Info,
                    ResolveSectionForLayer(entry.LayerKind),
                    entry.BiomeId,
                    entry.ZoneId,
                    recordKind,
                    entry.RecordId,
                    entry.DisplayName,
                    "Preview layer " + recordKind,
                    BuildSearchText(recordKind, entry.RecordId, entry.DisplayName, entry.BiomeId, entry.ZoneId, string.Empty),
                    entry.HasLocalBounds,
                    entry.LocalBounds,
                    entry.Priority));
            }
        }

        private static void AddIssueEntries(
            List<DimensionTemplateCustomizerSearchEntry> entries,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            if (entries == null || issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                entries.Add(new DimensionTemplateCustomizerSearchEntry(
                    DimensionTemplateCustomizerSearchEntryKind.Issue,
                    issue.Severity,
                    ResolveSectionForRecordKind(issue.RecordKind),
                    string.Empty,
                    string.Empty,
                    issue.RecordKind,
                    issue.RecordId,
                    issue.Code,
                    issue.Message,
                    BuildSearchText(issue.RecordKind, issue.RecordId, issue.Code, string.Empty, string.Empty, issue.Message),
                    issue.HasLocalBounds,
                    issue.LocalBounds,
                    ResolveIssuePriority(issue.Severity)));
            }
        }

        private static string ResolveSectionForContent(DimensionAuthoringContentSummaryKind kind)
        {
            if (kind == DimensionAuthoringContentSummaryKind.Dimension)
            {
                return "dimension";
            }

            if (kind == DimensionAuthoringContentSummaryKind.LayoutTemplate)
            {
                return "layout";
            }

            if (kind == DimensionAuthoringContentSummaryKind.SceneTemplate ||
                kind == DimensionAuthoringContentSummaryKind.SceneProp ||
                kind == DimensionAuthoringContentSummaryKind.SceneLootContainer ||
                kind == DimensionAuthoringContentSummaryKind.SceneTrigger)
            {
                return "scenes";
            }

            if (kind == DimensionAuthoringContentSummaryKind.ResourceNode ||
                kind == DimensionAuthoringContentSummaryKind.Item ||
                kind == DimensionAuthoringContentSummaryKind.Recipe ||
                kind == DimensionAuthoringContentSummaryKind.Workbench ||
                kind == DimensionAuthoringContentSummaryKind.LootTable)
            {
                return "resources";
            }

            if (kind == DimensionAuthoringContentSummaryKind.SpawnRule ||
                kind == DimensionAuthoringContentSummaryKind.Animal ||
                kind == DimensionAuthoringContentSummaryKind.Critter ||
                kind == DimensionAuthoringContentSummaryKind.Mob ||
                kind == DimensionAuthoringContentSummaryKind.Boss ||
                kind == DimensionAuthoringContentSummaryKind.SceneSpawnPoint)
            {
                return "spawns";
            }

            if (kind == DimensionAuthoringContentSummaryKind.GenerationPass ||
                kind == DimensionAuthoringContentSummaryKind.GenerationTable ||
                kind == DimensionAuthoringContentSummaryKind.GenerationTableEntry ||
                kind == DimensionAuthoringContentSummaryKind.BiomeGenerationProfile)
            {
                return "generation";
            }

            if (kind == DimensionAuthoringContentSummaryKind.SemanticFloorObject ||
                kind == DimensionAuthoringContentSummaryKind.SemanticWallObject ||
                kind == DimensionAuthoringContentSummaryKind.SemanticOreObject ||
                kind == DimensionAuthoringContentSummaryKind.SemanticWaterObject ||
                kind == DimensionAuthoringContentSummaryKind.BiomePaletteEntry)
            {
                return "terrain";
            }

            if (kind == DimensionAuthoringContentSummaryKind.Biome ||
                kind == DimensionAuthoringContentSummaryKind.BiomeContentPreset ||
                kind == DimensionAuthoringContentSummaryKind.EnvironmentProfile ||
                kind == DimensionAuthoringContentSummaryKind.BiomePalette)
            {
                return "biomes";
            }

            return "overview";
        }

        private static string ResolveSectionForLayer(DimensionAuthoringPreviewLayerKind kind)
        {
            if (kind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
            {
                return "layout";
            }

            if (kind == DimensionAuthoringPreviewLayerKind.BiomeRegion)
            {
                return "biomes";
            }

            if (kind == DimensionAuthoringPreviewLayerKind.GenerationPassBounds)
            {
                return "generation";
            }

            if (kind == DimensionAuthoringPreviewLayerKind.ScenePlacement)
            {
                return "scenes";
            }

            if (kind == DimensionAuthoringPreviewLayerKind.ResourceNode)
            {
                return "resources";
            }

            if (kind == DimensionAuthoringPreviewLayerKind.SpawnRule)
            {
                return "spawns";
            }

            return "overview";
        }

        private static string ResolveSectionForRecordKind(string recordKind)
        {
            string normalized = Normalize(recordKind);
            if (normalized.IndexOf("layout") >= 0 || normalized.IndexOf("bounds") >= 0)
            {
                return "layout";
            }

            if (normalized.IndexOf("scene") >= 0)
            {
                return "scenes";
            }

            if (normalized.IndexOf("resource") >= 0 ||
                normalized == "item" ||
                normalized.IndexOf("itemasset") >= 0 ||
                normalized.IndexOf("recipe") >= 0 ||
                normalized.IndexOf("workbench") >= 0 ||
                normalized.IndexOf("loot") >= 0)
            {
                return "resources";
            }

            if (normalized.IndexOf("spawn") >= 0 ||
                normalized.IndexOf("mob") >= 0 ||
                normalized.IndexOf("animal") >= 0 ||
                normalized.IndexOf("critter") >= 0 ||
                normalized.IndexOf("boss") >= 0)
            {
                return "spawns";
            }

            if (normalized.IndexOf("generation") >= 0 || normalized.IndexOf("table") >= 0 || normalized.IndexOf("pass") >= 0)
            {
                return "generation";
            }

            if (normalized.IndexOf("floor") >= 0 || normalized.IndexOf("wall") >= 0 || normalized.IndexOf("ore") >= 0 || normalized.IndexOf("water") >= 0 || normalized.IndexOf("palette") >= 0)
            {
                return "terrain";
            }

            if (normalized.IndexOf("biome") >= 0 || normalized.IndexOf("environment") >= 0)
            {
                return "biomes";
            }

            if (normalized.IndexOf("dimension") >= 0)
            {
                return "dimension";
            }

            return "diagnostics";
        }

        private static int ResolveIssuePriority(DimensionAuthoringSeverity severity)
        {
            if (severity == DimensionAuthoringSeverity.Error)
            {
                return 0;
            }

            return severity == DimensionAuthoringSeverity.Warning ? 10 : 40;
        }

        private static string BuildSearchText(
            string recordKind,
            string recordId,
            string displayName,
            string biomeId,
            string zoneId,
            string message)
        {
            return (recordKind ?? string.Empty) + " " +
                (recordId ?? string.Empty) + " " +
                (displayName ?? string.Empty) + " " +
                (biomeId ?? string.Empty) + " " +
                (zoneId ?? string.Empty) + " " +
                (message ?? string.Empty);
        }

        private static string Normalize(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();
        }

        private static void CountEntries(
            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> entries,
            out int contentCount,
            out int previewCount,
            out int issueCount)
        {
            contentCount = 0;
            previewCount = 0;
            issueCount = 0;
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Kind == DimensionTemplateCustomizerSearchEntryKind.Content)
                {
                    contentCount++;
                }
                else if (entries[i].Kind == DimensionTemplateCustomizerSearchEntryKind.Preview)
                {
                    previewCount++;
                }
                else if (entries[i].Kind == DimensionTemplateCustomizerSearchEntryKind.Issue)
                {
                    issueCount++;
                }
            }
        }

        private static int CompareEntries(
            DimensionTemplateCustomizerSearchEntry left,
            DimensionTemplateCustomizerSearchEntry right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int section = string.CompareOrdinal(left.SectionId, right.SectionId);
            if (section != 0)
            {
                return section;
            }

            int kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
            {
                return kind;
            }

            return string.CompareOrdinal(left.DisplayName, right.DisplayName);
        }
    }
}
