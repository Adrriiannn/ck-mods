using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public static class DimensionBiomeAuthoringOverviewUtility
    {
        public static List<DimensionBiomeAuthoringOverview> BuildBiomeOverviews(
            DimensionAuthoringPreviewSummary summary)
        {
            DimensionAuthoringReadinessReport readiness =
                DimensionAuthoringReadinessUtility.BuildReport(summary);
            return BuildBiomeOverviews(summary, readiness);
        }

        public static List<DimensionBiomeAuthoringOverview> BuildBiomeOverviews(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringReadinessReport readiness)
        {
            List<DimensionBiomeAuthoringOverview> overviews =
                new List<DimensionBiomeAuthoringOverview>();

            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries = summary.ContentEntries;
            if (contentEntries == null)
            {
                return overviews;
            }

            for (int i = 0; i < contentEntries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry biomeEntry = contentEntries[i];
                if (biomeEntry.Kind != DimensionAuthoringContentSummaryKind.Biome)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(biomeEntry.BiomeId))
                {
                    continue;
                }

                if (ContainsBiome(overviews, biomeEntry.BiomeId))
                {
                    continue;
                }

                overviews.Add(BuildBiomeOverview(summary, readiness, biomeEntry));
            }

            return overviews;
        }

        private static DimensionBiomeAuthoringOverview BuildBiomeOverview(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringReadinessReport readiness,
            DimensionAuthoringContentSummaryEntry biomeEntry)
        {
            DimensionAuthoringPreviewQuery query =
                DimensionAuthoringPreviewQuery.ForBiome(biomeEntry.BiomeId);

            List<DimensionAuthoringContentSummaryEntry> biomeContent =
                DimensionAuthoringPreviewQueryUtility.FilterContentEntries(summary, query);
            List<DimensionAuthoringPreviewEntry> biomePreview =
                DimensionAuthoringPreviewQueryUtility.FilterPreviewEntries(summary, query);
            List<DimensionAuthoringIssue> biomeIssues =
                DimensionAuthoringPreviewQueryUtility.FilterIssues(summary, query);
            List<DimensionAuthoringReadinessEntry> biomeReadiness =
                FilterReadiness(readiness, biomeEntry.BiomeId);
            List<DimensionBiomeCustomizerCapabilityStatus> capabilityStatuses =
                DimensionBiomeCustomizerCapabilityStatusUtility.BuildBuiltInStatuses(
                    summary,
                    biomeEntry.BiomeId);
            List<DimensionBiomeCustomizerGuidanceItem> guidanceItems =
                DimensionBiomeCustomizerGuidanceUtility.BuildGuidance(capabilityStatuses);
            List<DimensionAuthoringSpatialConflict> spatialConflicts =
                DimensionAuthoringSpatialConflictUtility.BuildConflicts(
                    summary,
                    biomeEntry.BiomeId);

            int errors;
            int warnings;
            int infos;
            CountIssueSeverities(biomeIssues, out errors, out warnings, out infos);

            int ready;
            int partial;
            int missing;
            int blocked;
            CountReadinessStates(biomeReadiness, out ready, out partial, out missing, out blocked);

            return new DimensionBiomeAuthoringOverview(
                summary.DimensionId,
                biomeEntry.BiomeId,
                biomeEntry.DisplayName,
                biomeContent.Count,
                biomePreview.Count,
                biomeIssues.Count,
                errors,
                warnings,
                infos,
                ready,
                partial,
                missing,
                blocked,
                biomeContent,
                biomePreview,
                biomeIssues,
                biomeReadiness,
                capabilityStatuses,
                guidanceItems,
                spatialConflicts);
        }

        private static bool ContainsBiome(
            IReadOnlyList<DimensionBiomeAuthoringOverview> overviews,
            string biomeId)
        {
            if (overviews == null || string.IsNullOrEmpty(biomeId))
            {
                return false;
            }

            for (int i = 0; i < overviews.Count; i++)
            {
                if (overviews[i].BiomeId == biomeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<DimensionAuthoringReadinessEntry> FilterReadiness(
            DimensionAuthoringReadinessReport readiness,
            string biomeId)
        {
            List<DimensionAuthoringReadinessEntry> results =
                new List<DimensionAuthoringReadinessEntry>();
            IReadOnlyList<DimensionAuthoringReadinessEntry> entries = readiness.Entries;
            if (entries == null)
            {
                return results;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessEntry entry = entries[i];
                if (entry.BiomeId == biomeId)
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        private static void CountIssueSeverities(
            IReadOnlyList<DimensionAuthoringIssue> issues,
            out int errors,
            out int warnings,
            out int infos)
        {
            errors = 0;
            warnings = 0;
            infos = 0;

            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringSeverity severity = issues[i].Severity;
                if (severity == DimensionAuthoringSeverity.Error)
                {
                    errors++;
                }
                else if (severity == DimensionAuthoringSeverity.Warning)
                {
                    warnings++;
                }
                else
                {
                    infos++;
                }
            }
        }

        private static void CountReadinessStates(
            IReadOnlyList<DimensionAuthoringReadinessEntry> entries,
            out int ready,
            out int partial,
            out int missing,
            out int blocked)
        {
            ready = 0;
            partial = 0;
            missing = 0;
            blocked = 0;

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessState state = entries[i].State;
                if (state == DimensionAuthoringReadinessState.Ready)
                {
                    ready++;
                }
                else if (state == DimensionAuthoringReadinessState.Partial)
                {
                    partial++;
                }
                else if (state == DimensionAuthoringReadinessState.Missing)
                {
                    missing++;
                }
                else if (state == DimensionAuthoringReadinessState.Blocked)
                {
                    blocked++;
                }
            }
        }
    }
}
