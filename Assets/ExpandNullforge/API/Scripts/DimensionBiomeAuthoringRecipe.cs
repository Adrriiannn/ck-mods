using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public enum DimensionBiomeAuthoringRecipeSectionKind
    {
        Environment = 0,
        Palette = 1,
        Terrain = 2,
        GenerationFlow = 3,
        Scenes = 4,
        Resources = 5,
        Spawns = 6
    }

    public readonly struct DimensionBiomeAuthoringRecipeEntry
    {
        public readonly DimensionAuthoringContentSummaryKind ContentKind;
        public readonly string RecordId;
        public readonly string DisplayName;
        public readonly string ZoneId;
        public readonly int Count;
        public readonly string Notes;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;

        public DimensionBiomeAuthoringRecipeEntry(
            DimensionAuthoringContentSummaryKind contentKind,
            string recordId,
            string displayName,
            string zoneId,
            int count,
            string notes,
            int issueCount,
            int errorCount,
            int warningCount)
        {
            ContentKind = contentKind;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            Count = count < 0 ? 0 : count;
            Notes = notes ?? string.Empty;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
        }
    }

    public readonly struct DimensionBiomeAuthoringRecipeSection
    {
        public readonly DimensionBiomeAuthoringRecipeSectionKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly string SectionId;
        public readonly string DisplayName;
        public readonly string Message;
        public readonly int EntryCount;
        public readonly int ConfiguredCount;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly bool RuntimeRelevant;
        public readonly IReadOnlyList<DimensionBiomeAuthoringRecipeEntry> Entries;

        public DimensionBiomeAuthoringRecipeSection(
            DimensionBiomeAuthoringRecipeSectionKind kind,
            DimensionAuthoringReadinessState state,
            string sectionId,
            string displayName,
            string message,
            int configuredCount,
            int issueCount,
            int errorCount,
            int warningCount,
            bool runtimeRelevant,
            IReadOnlyList<DimensionBiomeAuthoringRecipeEntry> entries)
        {
            Kind = kind;
            State = state;
            SectionId = sectionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Message = message ?? string.Empty;
            Entries = entries ?? new List<DimensionBiomeAuthoringRecipeEntry>();
            EntryCount = Entries.Count;
            ConfiguredCount = configuredCount < 0 ? 0 : configuredCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            RuntimeRelevant = runtimeRelevant;
        }
    }

    public readonly struct DimensionBiomeAuthoringRecipe
    {
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string DisplayName;
        public readonly bool ReadyForRuntimeGeneration;
        public readonly int SectionCount;
        public readonly int ConfiguredSectionCount;
        public readonly int MissingSectionCount;
        public readonly int BlockedSectionCount;
        public readonly int EntryCount;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly IReadOnlyList<DimensionBiomeAuthoringRecipeSection> Sections;

        public DimensionBiomeAuthoringRecipe(
            string dimensionId,
            string biomeId,
            string displayName,
            bool readyForRuntimeGeneration,
            int configuredSectionCount,
            int missingSectionCount,
            int blockedSectionCount,
            int entryCount,
            int issueCount,
            int errorCount,
            int warningCount,
            IReadOnlyList<DimensionBiomeAuthoringRecipeSection> sections)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            ConfiguredSectionCount = configuredSectionCount < 0 ? 0 : configuredSectionCount;
            MissingSectionCount = missingSectionCount < 0 ? 0 : missingSectionCount;
            BlockedSectionCount = blockedSectionCount < 0 ? 0 : blockedSectionCount;
            EntryCount = entryCount < 0 ? 0 : entryCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Sections = sections ?? new List<DimensionBiomeAuthoringRecipeSection>();
            SectionCount = Sections.Count;
        }
    }

    public static class DimensionBiomeAuthoringRecipeUtility
    {
        public static List<DimensionBiomeAuthoringRecipe> BuildRecipes(
            IReadOnlyList<DimensionBiomeAuthoringOverview> overviews)
        {
            List<DimensionBiomeAuthoringRecipe> recipes =
                new List<DimensionBiomeAuthoringRecipe>();
            if (overviews == null)
            {
                return recipes;
            }

            for (int i = 0; i < overviews.Count; i++)
            {
                recipes.Add(BuildRecipe(overviews[i]));
            }

            return recipes;
        }

        public static DimensionBiomeAuthoringRecipe BuildRecipe(
            DimensionBiomeAuthoringOverview overview)
        {
            List<DimensionBiomeAuthoringRecipeSection> sections =
                new List<DimensionBiomeAuthoringRecipeSection>();

            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Environment);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Palette);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Terrain);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Scenes);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Resources);
            AddSection(sections, overview, DimensionBiomeAuthoringRecipeSectionKind.Spawns);

            int configuredSections = 0;
            int missingSections = 0;
            int blockedSections = 0;
            int entries = 0;
            int issues = 0;
            int errors = 0;
            int warnings = 0;

            for (int i = 0; i < sections.Count; i++)
            {
                DimensionBiomeAuthoringRecipeSection section = sections[i];
                if (section.ConfiguredCount > 0)
                {
                    configuredSections++;
                }

                if (section.State == DimensionAuthoringReadinessState.Missing)
                {
                    missingSections++;
                }
                else if (section.State == DimensionAuthoringReadinessState.Blocked)
                {
                    blockedSections++;
                }

                entries += section.EntryCount;
                issues += section.IssueCount;
                errors += section.ErrorCount;
                warnings += section.WarningCount;
            }

            bool readyForRuntime = missingSections == 0 && blockedSections == 0 && errors == 0;
            string displayName = string.IsNullOrEmpty(overview.DisplayName)
                ? overview.BiomeId
                : overview.DisplayName;

            return new DimensionBiomeAuthoringRecipe(
                overview.DimensionId,
                overview.BiomeId,
                displayName,
                readyForRuntime,
                configuredSections,
                missingSections,
                blockedSections,
                entries,
                issues,
                errors,
                warnings,
                sections);
        }

        private static void AddSection(
            List<DimensionBiomeAuthoringRecipeSection> sections,
            DimensionBiomeAuthoringOverview overview,
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            List<DimensionBiomeAuthoringRecipeEntry> entries =
                new List<DimensionBiomeAuthoringRecipeEntry>();
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries = overview.ContentEntries;
            if (contentEntries != null)
            {
                for (int i = 0; i < contentEntries.Count; i++)
                {
                    DimensionAuthoringContentSummaryEntry content = contentEntries[i];
                    if (!BelongsToSection(content.Kind, sectionKind))
                    {
                        continue;
                    }

                    int issueCount;
                    int errorCount;
                    int warningCount;
                    CountEntryIssues(
                        overview.Issues,
                        content,
                        out issueCount,
                        out errorCount,
                        out warningCount);

                    entries.Add(new DimensionBiomeAuthoringRecipeEntry(
                        content.Kind,
                        content.RecordId,
                        content.DisplayName,
                        content.ZoneId,
                        content.Count < 1 ? 1 : content.Count,
                        content.Notes,
                        issueCount,
                        errorCount,
                        warningCount));
                }
            }

            int sectionIssues;
            int sectionErrors;
            int sectionWarnings;
            CountSectionIssues(overview.Issues, sectionKind, out sectionIssues, out sectionErrors, out sectionWarnings);

            DimensionAuthoringReadinessState state = ResolveSectionState(
                overview,
                sectionKind,
                entries.Count,
                sectionErrors,
                sectionWarnings);

            sections.Add(new DimensionBiomeAuthoringRecipeSection(
                sectionKind,
                state,
                BuildSectionId(sectionKind),
                BuildSectionDisplayName(sectionKind),
                BuildSectionMessage(sectionKind, state, entries.Count, sectionErrors, sectionWarnings),
                entries.Count,
                sectionIssues,
                sectionErrors,
                sectionWarnings,
                IsRuntimeRelevant(sectionKind),
                entries));
        }

        private static bool BelongsToSection(
            DimensionAuthoringContentSummaryKind contentKind,
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Environment)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.EnvironmentProfile;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Palette)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.BiomePalette ||
                    contentKind == DimensionAuthoringContentSummaryKind.BiomePaletteEntry;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Terrain)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.SemanticFloorObject ||
                    contentKind == DimensionAuthoringContentSummaryKind.SemanticWallObject ||
                    contentKind == DimensionAuthoringContentSummaryKind.SemanticOreObject ||
                    contentKind == DimensionAuthoringContentSummaryKind.SemanticWaterObject;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.BiomeGenerationProfile ||
                    contentKind == DimensionAuthoringContentSummaryKind.GenerationPass ||
                    contentKind == DimensionAuthoringContentSummaryKind.GenerationTable ||
                    contentKind == DimensionAuthoringContentSummaryKind.GenerationTableEntry;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Scenes)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.SceneTemplate;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Resources)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.ResourceNode;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Spawns)
            {
                return contentKind == DimensionAuthoringContentSummaryKind.SpawnRule;
            }

            return false;
        }

        private static DimensionAuthoringReadinessState ResolveSectionState(
            DimensionBiomeAuthoringOverview overview,
            DimensionBiomeAuthoringRecipeSectionKind sectionKind,
            int entryCount,
            int errorCount,
            int warningCount)
        {
            if (errorCount > 0)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            DimensionAuthoringReadinessCategory category = ResolveReadinessCategory(sectionKind);
            DimensionAuthoringReadinessState readiness;
            if (TryFindReadiness(overview.ReadinessEntries, category, out readiness))
            {
                return readiness;
            }

            if (entryCount > 0)
            {
                return warningCount > 0
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready;
            }

            return IsRuntimeRelevant(sectionKind)
                ? DimensionAuthoringReadinessState.Missing
                : DimensionAuthoringReadinessState.Partial;
        }

        private static bool TryFindReadiness(
            IReadOnlyList<DimensionAuthoringReadinessEntry> readinessEntries,
            DimensionAuthoringReadinessCategory category,
            out DimensionAuthoringReadinessState state)
        {
            state = DimensionAuthoringReadinessState.Partial;
            if (readinessEntries == null)
            {
                return false;
            }

            for (int i = 0; i < readinessEntries.Count; i++)
            {
                DimensionAuthoringReadinessEntry entry = readinessEntries[i];
                if (entry.Category != category)
                {
                    continue;
                }

                state = entry.State;
                return true;
            }

            return false;
        }

        private static DimensionAuthoringReadinessCategory ResolveReadinessCategory(
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Environment)
            {
                return DimensionAuthoringReadinessCategory.Environment;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Scenes)
            {
                return DimensionAuthoringReadinessCategory.Scenes;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Resources)
            {
                return DimensionAuthoringReadinessCategory.Resources;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Spawns)
            {
                return DimensionAuthoringReadinessCategory.Spawns;
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow)
            {
                return DimensionAuthoringReadinessCategory.GenerationPasses;
            }

            return DimensionAuthoringReadinessCategory.Terrain;
        }

        private static void CountEntryIssues(
            IReadOnlyList<DimensionAuthoringIssue> issues,
            DimensionAuthoringContentSummaryEntry content,
            out int issueCount,
            out int errorCount,
            out int warningCount)
        {
            issueCount = 0;
            errorCount = 0;
            warningCount = 0;

            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (!IssueMatchesContent(issue, content))
                {
                    continue;
                }

                issueCount++;
                if (issue.Severity == DimensionAuthoringSeverity.Error)
                {
                    errorCount++;
                }
                else if (issue.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warningCount++;
                }
            }
        }

        private static void CountSectionIssues(
            IReadOnlyList<DimensionAuthoringIssue> issues,
            DimensionBiomeAuthoringRecipeSectionKind sectionKind,
            out int issueCount,
            out int errorCount,
            out int warningCount)
        {
            issueCount = 0;
            errorCount = 0;
            warningCount = 0;

            if (issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                DimensionAuthoringContentSummaryKind contentKind;
                if (!TryResolveIssueContentKind(issue, out contentKind) ||
                    !BelongsToSection(contentKind, sectionKind))
                {
                    continue;
                }

                issueCount++;
                if (issue.Severity == DimensionAuthoringSeverity.Error)
                {
                    errorCount++;
                }
                else if (issue.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warningCount++;
                }
            }
        }

        private static bool IssueMatchesContent(
            DimensionAuthoringIssue issue,
            DimensionAuthoringContentSummaryEntry content)
        {
            if (!string.IsNullOrEmpty(issue.RecordId) &&
                issue.RecordId == content.RecordId)
            {
                return true;
            }

            DimensionAuthoringContentSummaryKind issueKind;
            return TryResolveIssueContentKind(issue, out issueKind) &&
                issueKind == content.Kind;
        }

        private static bool TryResolveIssueContentKind(
            DimensionAuthoringIssue issue,
            out DimensionAuthoringContentSummaryKind contentKind)
        {
            contentKind = DimensionAuthoringContentSummaryKind.Dimension;
            string recordKind = issue.RecordKind;
            if (string.IsNullOrEmpty(recordKind))
            {
                return false;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.EnvironmentProfile.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.EnvironmentProfile;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.BiomePalette.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.BiomePalette;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.BiomePaletteEntry.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.BiomePaletteEntry;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.GenerationPass.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.GenerationPass;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.GenerationTable.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.GenerationTable;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.GenerationTableEntry.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.GenerationTableEntry;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SceneTemplate.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SceneTemplate;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.ResourceNode.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.ResourceNode;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SpawnRule.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SpawnRule;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SemanticFloorObject.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SemanticFloorObject;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SemanticWallObject.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SemanticWallObject;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SemanticOreObject.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SemanticOreObject;
                return true;
            }

            if (recordKind == DimensionAuthoringContentSummaryKind.SemanticWaterObject.ToString())
            {
                contentKind = DimensionAuthoringContentSummaryKind.SemanticWaterObject;
                return true;
            }

            return false;
        }

        private static string BuildSectionId(
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow)
            {
                return "generation-flow";
            }

            return sectionKind.ToString();
        }

        private static string BuildSectionDisplayName(
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Environment)
            {
                return "Environment";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Palette)
            {
                return "Palette";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Terrain)
            {
                return "Terrain";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow)
            {
                return "Generation Flow";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Scenes)
            {
                return "Scenes";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Resources)
            {
                return "Resources";
            }

            if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Spawns)
            {
                return "Spawns";
            }

            return "Section";
        }

        private static string BuildSectionMessage(
            DimensionBiomeAuthoringRecipeSectionKind sectionKind,
            DimensionAuthoringReadinessState state,
            int entryCount,
            int errorCount,
            int warningCount)
        {
            string name = BuildSectionDisplayName(sectionKind);
            if (errorCount > 0)
            {
                return name + " has blocking authoring errors.";
            }

            if (warningCount > 0)
            {
                return name + " has warnings to review.";
            }

            if (state == DimensionAuthoringReadinessState.Missing)
            {
                if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Scenes)
                {
                    return "Scenes are optional. Add them when this biome needs authored rooms, landmarks, dungeons, boss arenas, or exact structure placements.";
                }

                if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Resources)
                {
                    return "Resources are optional. Add them when this biome needs ore boulders, landmarks, or special gatherable nodes.";
                }

                if (sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Spawns)
                {
                    return "Spawns are optional. Add them when this biome needs mobs, critters, NPCs, bosses, or encounters.";
                }

                return name + " is not configured yet.";
            }

            if (entryCount <= 0)
            {
                return name + " has no explicit records.";
            }

            return name + " has " + entryCount + " configured records.";
        }

        private static bool IsRuntimeRelevant(
            DimensionBiomeAuthoringRecipeSectionKind sectionKind)
        {
            return sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Palette ||
                sectionKind == DimensionBiomeAuthoringRecipeSectionKind.Terrain ||
                sectionKind == DimensionBiomeAuthoringRecipeSectionKind.GenerationFlow;
        }
    }
}
