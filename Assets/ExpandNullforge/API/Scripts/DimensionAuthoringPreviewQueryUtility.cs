using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public static class DimensionAuthoringPreviewQueryUtility
    {
        public static List<DimensionAuthoringPreviewEntry> FilterPreviewEntries(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringPreviewQuery query)
        {
            List<DimensionAuthoringPreviewEntry> results = new List<DimensionAuthoringPreviewEntry>();
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries = summary.Entries;
            if (entries == null)
            {
                return results;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = entries[i];
                if (!MatchesPreviewEntry(entry, query))
                {
                    continue;
                }

                results.Add(entry);
            }

            return results;
        }

        public static List<DimensionAuthoringContentSummaryEntry> FilterContentEntries(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringPreviewQuery query)
        {
            List<DimensionAuthoringContentSummaryEntry> results =
                new List<DimensionAuthoringContentSummaryEntry>();
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries = summary.ContentEntries;
            if (entries == null)
            {
                return results;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (!MatchesContentEntry(entry, query))
                {
                    continue;
                }

                results.Add(entry);
            }

            return results;
        }

        public static List<DimensionAuthoringIssue> FilterIssues(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringPreviewQuery query)
        {
            List<DimensionAuthoringIssue> results = new List<DimensionAuthoringIssue>();
            IReadOnlyList<DimensionAuthoringIssue> issues = summary.Issues;
            if (issues == null)
            {
                return results;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (!MatchesIssue(issue, query))
                {
                    continue;
                }

                results.Add(issue);
            }

            return results;
        }

        public static bool MatchesPreviewEntry(
            DimensionAuthoringPreviewEntry entry,
            DimensionAuthoringPreviewQuery query)
        {
            if (query.HasPreviewLayerKind && entry.LayerKind != query.PreviewLayerKind)
            {
                return false;
            }

            if (!MatchesRecord(entry.RecordId, entry.BiomeId, entry.ZoneId, query))
            {
                return false;
            }

            return true;
        }

        public static bool MatchesContentEntry(
            DimensionAuthoringContentSummaryEntry entry,
            DimensionAuthoringPreviewQuery query)
        {
            if (query.HasContentKind && entry.Kind != query.ContentKind)
            {
                return false;
            }

            if (!MatchesRecord(entry.RecordId, entry.BiomeId, entry.ZoneId, query))
            {
                return false;
            }

            return true;
        }

        public static bool MatchesIssue(
            DimensionAuthoringIssue issue,
            DimensionAuthoringPreviewQuery query)
        {
            if (!string.IsNullOrEmpty(query.RecordId) && issue.RecordId != query.RecordId)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesRecord(
            string recordId,
            string biomeId,
            string zoneId,
            DimensionAuthoringPreviewQuery query)
        {
            if (!string.IsNullOrEmpty(query.RecordId) && recordId != query.RecordId)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(query.BiomeId))
            {
                bool isGlobal = string.IsNullOrEmpty(biomeId);
                if (isGlobal)
                {
                    return query.IncludeGlobalRecords;
                }

                if (biomeId != query.BiomeId)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(query.ZoneId) && zoneId != query.ZoneId)
            {
                return false;
            }

            return true;
        }
    }
}
