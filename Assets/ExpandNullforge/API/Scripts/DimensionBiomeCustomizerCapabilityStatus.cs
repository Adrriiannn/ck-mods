using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeCustomizerCapabilityStatus
    {
        public readonly DimensionBiomeCustomizerCapability Capability;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly int ConfiguredCount;
        public readonly int ActiveCount;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly int InfoCount;
        public readonly DimensionAuthoringReadinessState State;
        public readonly string Code;
        public readonly string Message;
        public readonly IReadOnlyList<DimensionAuthoringContentSummaryEntry> ContentEntries;
        public readonly IReadOnlyList<DimensionAuthoringIssue> Issues;

        public DimensionBiomeCustomizerCapabilityStatus(
            DimensionBiomeCustomizerCapability capability,
            string dimensionId,
            string biomeId,
            int configuredCount,
            int activeCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int infoCount,
            DimensionAuthoringReadinessState state,
            string code,
            string message,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries,
            IReadOnlyList<DimensionAuthoringIssue> issues)
        {
            Capability = capability;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ConfiguredCount = configuredCount < 0 ? 0 : configuredCount;
            ActiveCount = activeCount < 0 ? 0 : activeCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            InfoCount = infoCount < 0 ? 0 : infoCount;
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ContentEntries = contentEntries ?? new List<DimensionAuthoringContentSummaryEntry>();
            Issues = issues ?? new List<DimensionAuthoringIssue>();
        }
    }

    public static class DimensionBiomeCustomizerCapabilityStatusUtility
    {
        public static List<DimensionBiomeCustomizerCapabilityStatus> BuildBuiltInStatuses(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            return BuildStatuses(
                summary,
                DimensionBiomeCustomizerCapabilityCatalog.GetBuiltInCapabilities(),
                biomeId);
        }

        public static List<DimensionBiomeCustomizerCapabilityStatus> BuildStatuses(
            DimensionAuthoringPreviewSummary summary,
            IReadOnlyList<DimensionBiomeCustomizerCapability> capabilities,
            string biomeId)
        {
            List<DimensionBiomeCustomizerCapabilityStatus> statuses =
                new List<DimensionBiomeCustomizerCapabilityStatus>();

            if (capabilities == null)
            {
                return statuses;
            }

            for (int i = 0; i < capabilities.Count; i++)
            {
                statuses.Add(BuildStatus(summary, capabilities[i], biomeId));
            }

            return statuses;
        }

        public static DimensionBiomeCustomizerCapabilityStatus BuildStatus(
            DimensionAuthoringPreviewSummary summary,
            DimensionBiomeCustomizerCapability capability,
            string biomeId)
        {
            List<DimensionAuthoringContentSummaryEntry> matchingContent =
                CollectContent(summary, capability, biomeId);
            List<DimensionAuthoringIssue> matchingIssues =
                CollectIssues(summary, capability, matchingContent);

            int configured = matchingContent.Count;
            int active = CountActive(matchingContent);
            int errors;
            int warnings;
            int infos;
            CountIssueSeverities(matchingIssues, out errors, out warnings, out infos);

            DimensionAuthoringReadinessState state;
            string code;
            string message;
            BuildState(
                capability,
                configured,
                active,
                errors,
                warnings,
                out state,
                out code,
                out message);

            return new DimensionBiomeCustomizerCapabilityStatus(
                capability,
                summary.DimensionId,
                biomeId,
                configured,
                active,
                matchingIssues.Count,
                errors,
                warnings,
                infos,
                state,
                code,
                message,
                matchingContent,
                matchingIssues);
        }

        private static List<DimensionAuthoringContentSummaryEntry> CollectContent(
            DimensionAuthoringPreviewSummary summary,
            DimensionBiomeCustomizerCapability capability,
            string biomeId)
        {
            List<DimensionAuthoringContentSummaryEntry> matches =
                new List<DimensionAuthoringContentSummaryEntry>();
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries = summary.ContentEntries;
            if (entries == null)
            {
                return matches;
            }

            DimensionAuthoringContentSummaryKind[] kinds = GetContentKinds(capability);
            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (!ContainsKind(kinds, entry.Kind))
                {
                    continue;
                }

                if (!MatchesBiomeScope(entry, capability, biomeId))
                {
                    continue;
                }

                matches.Add(entry);
            }

            return matches;
        }

        private static List<DimensionAuthoringIssue> CollectIssues(
            DimensionAuthoringPreviewSummary summary,
            DimensionBiomeCustomizerCapability capability,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> matchingContent)
        {
            List<DimensionAuthoringIssue> matches = new List<DimensionAuthoringIssue>();
            IReadOnlyList<DimensionAuthoringIssue> issues = summary.Issues;
            if (issues == null)
            {
                return matches;
            }

            bool allowKindFallback =
                !capability.BiomeScoped ||
                (matchingContent != null && matchingContent.Count > 0);

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (MatchesIssueRecord(issue, matchingContent) ||
                    (allowKindFallback && MatchesIssueKind(issue, capability)))
                {
                    matches.Add(issue);
                }
            }

            return matches;
        }

        private static DimensionAuthoringContentSummaryKind[] GetContentKinds(
            DimensionBiomeCustomizerCapability capability)
        {
            if (capability.CapabilityId == "semantic-terrain")
            {
                return new[]
                {
                    DimensionAuthoringContentSummaryKind.SemanticFloorObject,
                    DimensionAuthoringContentSummaryKind.SemanticWallObject,
                    DimensionAuthoringContentSummaryKind.SemanticOreObject,
                    DimensionAuthoringContentSummaryKind.SemanticWaterObject
                };
            }

            if (capability.CapabilityId == "biome-palette")
            {
                return new[]
                {
                    DimensionAuthoringContentSummaryKind.BiomePalette,
                    DimensionAuthoringContentSummaryKind.BiomePaletteEntry
                };
            }

            if (capability.CapabilityId == "generation-table")
            {
                return new[]
                {
                    DimensionAuthoringContentSummaryKind.GenerationTable,
                    DimensionAuthoringContentSummaryKind.GenerationTableEntry
                };
            }

            return new[] { capability.ContentKind };
        }

        private static bool MatchesBiomeScope(
            DimensionAuthoringContentSummaryEntry entry,
            DimensionBiomeCustomizerCapability capability,
            string biomeId)
        {
            if (!capability.BiomeScoped)
            {
                return string.IsNullOrEmpty(entry.BiomeId);
            }

            if (string.IsNullOrEmpty(biomeId))
            {
                return !string.IsNullOrEmpty(entry.BiomeId);
            }

            return entry.BiomeId == biomeId;
        }

        private static bool ContainsKind(
            IReadOnlyList<DimensionAuthoringContentSummaryKind> kinds,
            DimensionAuthoringContentSummaryKind kind)
        {
            if (kinds == null)
            {
                return false;
            }

            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesIssueRecord(
            DimensionAuthoringIssue issue,
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> matchingContent)
        {
            if (string.IsNullOrEmpty(issue.RecordId) || matchingContent == null)
            {
                return false;
            }

            for (int i = 0; i < matchingContent.Count; i++)
            {
                if (issue.RecordId == matchingContent[i].RecordId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesIssueKind(
            DimensionAuthoringIssue issue,
            DimensionBiomeCustomizerCapability capability)
        {
            if (string.IsNullOrEmpty(issue.RecordKind))
            {
                return false;
            }

            string kind = issue.RecordKind.ToLowerInvariant();
            string capabilityId = capability.CapabilityId;
            if (capabilityId == "environment-profile")
            {
                return kind.Contains("environment");
            }

            if (capabilityId == "biome-palette" || capabilityId == "semantic-terrain")
            {
                return kind.Contains("palette") || kind.Contains("semantic");
            }

            if (capabilityId == "generation-table")
            {
                return kind.Contains("generationtable") || kind.Contains("generation table");
            }

            if (capabilityId == "generation-pass")
            {
                return kind.Contains("generationpass") || kind.Contains("generation pass");
            }

            if (capabilityId == "scene-template")
            {
                return kind.Contains("scene");
            }

            if (capabilityId == "resource-node")
            {
                return kind.Contains("resource");
            }

            if (capabilityId == "spawn-rule")
            {
                return kind.Contains("spawn");
            }

            if (capabilityId == "layout-template")
            {
                return kind.Contains("layout");
            }

            if (capabilityId == "content-preset")
            {
                return kind.Contains("preset");
            }

            return false;
        }

        private static int CountActive(
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries)
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Count > 0)
                {
                    count += entries[i].Count;
                }
            }

            return count;
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

        private static void BuildState(
            DimensionBiomeCustomizerCapability capability,
            int configured,
            int active,
            int errors,
            int warnings,
            out DimensionAuthoringReadinessState state,
            out string code,
            out string message)
        {
            if (errors > 0)
            {
                state = DimensionAuthoringReadinessState.Blocked;
                code = "blocked";
                message = capability.DisplayName + " has authoring errors that should be fixed.";
                return;
            }

            if (configured <= 0)
            {
                state = DimensionAuthoringReadinessState.Missing;
                code = "empty";
                message = capability.DisplayName + " has not been configured yet.";
                return;
            }

            if (active <= 0)
            {
                state = DimensionAuthoringReadinessState.Partial;
                code = "configured-disabled";
                message = capability.DisplayName + " is configured, but all matching entries appear inactive.";
                return;
            }

            if (warnings > 0)
            {
                state = DimensionAuthoringReadinessState.Partial;
                code = "configured-with-warnings";
                message = capability.DisplayName + " is configured, but has warnings.";
                return;
            }

            state = DimensionAuthoringReadinessState.Ready;
            code = "configured";
            message = capability.DisplayName + " is configured.";
        }
    }
}
