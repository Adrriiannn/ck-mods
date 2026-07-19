using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionBiomeGenerationBudget
    {
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string DisplayName;
        public readonly int EstimatedTileArea;
        public readonly int PreviewEntryCount;
        public readonly int ContentEntryCount;
        public readonly int GenerationPassCount;
        public readonly int GenerationTableCount;
        public readonly int GenerationTableEntryCount;
        public readonly int SemanticObjectCount;
        public readonly int SceneTemplateCount;
        public readonly int ResourceNodeCount;
        public readonly int SpawnRuleCount;
        public readonly int IssueCount;
        public readonly int BlockingIssueCount;
        public readonly int WarningCount;
        public readonly int ReadinessBlockedCount;
        public readonly int EstimatedComplexityScore;
        public readonly string ComplexityCode;
        public readonly string ComplexityMessage;

        public DimensionBiomeGenerationBudget(
            string dimensionId,
            string biomeId,
            string displayName,
            int estimatedTileArea,
            int previewEntryCount,
            int contentEntryCount,
            int generationPassCount,
            int generationTableCount,
            int generationTableEntryCount,
            int semanticObjectCount,
            int sceneTemplateCount,
            int resourceNodeCount,
            int spawnRuleCount,
            int issueCount,
            int blockingIssueCount,
            int warningCount,
            int readinessBlockedCount,
            int estimatedComplexityScore,
            string complexityCode,
            string complexityMessage)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            EstimatedTileArea = estimatedTileArea < 0 ? 0 : estimatedTileArea;
            PreviewEntryCount = previewEntryCount < 0 ? 0 : previewEntryCount;
            ContentEntryCount = contentEntryCount < 0 ? 0 : contentEntryCount;
            GenerationPassCount = generationPassCount < 0 ? 0 : generationPassCount;
            GenerationTableCount = generationTableCount < 0 ? 0 : generationTableCount;
            GenerationTableEntryCount = generationTableEntryCount < 0 ? 0 : generationTableEntryCount;
            SemanticObjectCount = semanticObjectCount < 0 ? 0 : semanticObjectCount;
            SceneTemplateCount = sceneTemplateCount < 0 ? 0 : sceneTemplateCount;
            ResourceNodeCount = resourceNodeCount < 0 ? 0 : resourceNodeCount;
            SpawnRuleCount = spawnRuleCount < 0 ? 0 : spawnRuleCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            BlockingIssueCount = blockingIssueCount < 0 ? 0 : blockingIssueCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            ReadinessBlockedCount = readinessBlockedCount < 0 ? 0 : readinessBlockedCount;
            EstimatedComplexityScore = estimatedComplexityScore < 0 ? 0 : estimatedComplexityScore;
            ComplexityCode = complexityCode ?? string.Empty;
            ComplexityMessage = complexityMessage ?? string.Empty;
        }
    }

    public readonly struct DimensionGenerationBudgetSummary
    {
        public readonly string DimensionId;
        public readonly int BiomeCount;
        public readonly int TotalEstimatedTileArea;
        public readonly int TotalGenerationPassCount;
        public readonly int TotalGenerationTableCount;
        public readonly int TotalGenerationTableEntryCount;
        public readonly int TotalSemanticObjectCount;
        public readonly int TotalSceneTemplateCount;
        public readonly int TotalResourceNodeCount;
        public readonly int TotalSpawnRuleCount;
        public readonly int TotalIssueCount;
        public readonly int TotalBlockingIssueCount;
        public readonly int TotalWarningCount;
        public readonly int TotalReadinessBlockedCount;
        public readonly int TotalEstimatedComplexityScore;
        public readonly string ComplexityCode;
        public readonly string ComplexityMessage;
        public readonly IReadOnlyList<DimensionBiomeGenerationBudget> BiomeBudgets;

        public DimensionGenerationBudgetSummary(
            string dimensionId,
            int biomeCount,
            int totalEstimatedTileArea,
            int totalGenerationPassCount,
            int totalGenerationTableCount,
            int totalGenerationTableEntryCount,
            int totalSemanticObjectCount,
            int totalSceneTemplateCount,
            int totalResourceNodeCount,
            int totalSpawnRuleCount,
            int totalIssueCount,
            int totalBlockingIssueCount,
            int totalWarningCount,
            int totalReadinessBlockedCount,
            int totalEstimatedComplexityScore,
            string complexityCode,
            string complexityMessage,
            IReadOnlyList<DimensionBiomeGenerationBudget> biomeBudgets)
        {
            DimensionId = dimensionId ?? string.Empty;
            BiomeCount = biomeCount < 0 ? 0 : biomeCount;
            TotalEstimatedTileArea = totalEstimatedTileArea < 0 ? 0 : totalEstimatedTileArea;
            TotalGenerationPassCount = totalGenerationPassCount < 0 ? 0 : totalGenerationPassCount;
            TotalGenerationTableCount = totalGenerationTableCount < 0 ? 0 : totalGenerationTableCount;
            TotalGenerationTableEntryCount = totalGenerationTableEntryCount < 0 ? 0 : totalGenerationTableEntryCount;
            TotalSemanticObjectCount = totalSemanticObjectCount < 0 ? 0 : totalSemanticObjectCount;
            TotalSceneTemplateCount = totalSceneTemplateCount < 0 ? 0 : totalSceneTemplateCount;
            TotalResourceNodeCount = totalResourceNodeCount < 0 ? 0 : totalResourceNodeCount;
            TotalSpawnRuleCount = totalSpawnRuleCount < 0 ? 0 : totalSpawnRuleCount;
            TotalIssueCount = totalIssueCount < 0 ? 0 : totalIssueCount;
            TotalBlockingIssueCount = totalBlockingIssueCount < 0 ? 0 : totalBlockingIssueCount;
            TotalWarningCount = totalWarningCount < 0 ? 0 : totalWarningCount;
            TotalReadinessBlockedCount = totalReadinessBlockedCount < 0 ? 0 : totalReadinessBlockedCount;
            TotalEstimatedComplexityScore = totalEstimatedComplexityScore < 0 ? 0 : totalEstimatedComplexityScore;
            ComplexityCode = complexityCode ?? string.Empty;
            ComplexityMessage = complexityMessage ?? string.Empty;
            BiomeBudgets = biomeBudgets ?? new List<DimensionBiomeGenerationBudget>();
        }
    }

    public static class DimensionGenerationBudgetUtility
    {
        public static DimensionGenerationBudgetSummary BuildSummary(
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews)
        {
            List<DimensionBiomeGenerationBudget> budgets =
                new List<DimensionBiomeGenerationBudget>();
            if (biomeOverviews != null)
            {
                for (int i = 0; i < biomeOverviews.Count; i++)
                {
                    budgets.Add(BuildBiomeBudget(biomeOverviews[i]));
                }
            }

            return BuildSummaryFromBudgets(budgets);
        }

        public static DimensionBiomeGenerationBudget BuildBiomeBudget(
            DimensionBiomeAuthoringOverview overview)
        {
            int passCount = 0;
            int tableCount = 0;
            int tableEntryCount = 0;
            int semanticObjectCount = 0;
            int sceneTemplateCount = 0;
            int resourceNodeCount = 0;
            int spawnRuleCount = 0;

            CountContentEntries(
                overview.ContentEntries,
                out passCount,
                out tableCount,
                out tableEntryCount,
                out semanticObjectCount,
                out sceneTemplateCount,
                out resourceNodeCount,
                out spawnRuleCount);

            int area = EstimateTileArea(overview.PreviewEntries);
            int complexityScore = EstimateComplexityScore(
                area,
                passCount,
                tableCount,
                tableEntryCount,
                semanticObjectCount,
                sceneTemplateCount,
                resourceNodeCount,
                spawnRuleCount);
            string complexityCode;
            string complexityMessage;
            ResolveComplexity(
                complexityScore,
                overview.BlockedCount,
                overview.ErrorCount,
                out complexityCode,
                out complexityMessage);

            return new DimensionBiomeGenerationBudget(
                overview.DimensionId,
                overview.BiomeId,
                overview.DisplayName,
                area,
                overview.PreviewEntryCount,
                overview.ContentEntryCount,
                passCount,
                tableCount,
                tableEntryCount,
                semanticObjectCount,
                sceneTemplateCount,
                resourceNodeCount,
                spawnRuleCount,
                overview.IssueCount,
                overview.ErrorCount,
                overview.WarningCount,
                overview.BlockedCount,
                complexityScore,
                complexityCode,
                complexityMessage);
        }

        private static DimensionGenerationBudgetSummary BuildSummaryFromBudgets(
            IReadOnlyList<DimensionBiomeGenerationBudget> budgets)
        {
            string dimensionId = string.Empty;
            int area = 0;
            int passes = 0;
            int tables = 0;
            int entries = 0;
            int semanticObjects = 0;
            int scenes = 0;
            int resources = 0;
            int spawns = 0;
            int issues = 0;
            int blockingIssues = 0;
            int warnings = 0;
            int readinessBlocked = 0;
            int complexity = 0;

            if (budgets != null)
            {
                for (int i = 0; i < budgets.Count; i++)
                {
                    DimensionBiomeGenerationBudget budget = budgets[i];
                    if (string.IsNullOrEmpty(dimensionId))
                    {
                        dimensionId = budget.DimensionId;
                    }

                    area += budget.EstimatedTileArea;
                    passes += budget.GenerationPassCount;
                    tables += budget.GenerationTableCount;
                    entries += budget.GenerationTableEntryCount;
                    semanticObjects += budget.SemanticObjectCount;
                    scenes += budget.SceneTemplateCount;
                    resources += budget.ResourceNodeCount;
                    spawns += budget.SpawnRuleCount;
                    issues += budget.IssueCount;
                    blockingIssues += budget.BlockingIssueCount;
                    warnings += budget.WarningCount;
                    readinessBlocked += budget.ReadinessBlockedCount;
                    complexity += budget.EstimatedComplexityScore;
                }
            }

            string complexityCode;
            string complexityMessage;
            ResolveComplexity(
                complexity,
                readinessBlocked,
                blockingIssues,
                out complexityCode,
                out complexityMessage);

            return new DimensionGenerationBudgetSummary(
                dimensionId,
                budgets == null ? 0 : budgets.Count,
                area,
                passes,
                tables,
                entries,
                semanticObjects,
                scenes,
                resources,
                spawns,
                issues,
                blockingIssues,
                warnings,
                readinessBlocked,
                complexity,
                complexityCode,
                complexityMessage,
                budgets);
        }

        private static void CountContentEntries(
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries,
            out int passCount,
            out int tableCount,
            out int tableEntryCount,
            out int semanticObjectCount,
            out int sceneTemplateCount,
            out int resourceNodeCount,
            out int spawnRuleCount)
        {
            passCount = 0;
            tableCount = 0;
            tableEntryCount = 0;
            semanticObjectCount = 0;
            sceneTemplateCount = 0;
            resourceNodeCount = 0;
            spawnRuleCount = 0;

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                int count = entry.Count <= 0 ? 1 : entry.Count;
                if (entry.Kind == DimensionAuthoringContentSummaryKind.GenerationPass)
                {
                    passCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.GenerationTable)
                {
                    tableCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.GenerationTableEntry)
                {
                    tableEntryCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.SceneTemplate)
                {
                    sceneTemplateCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.ResourceNode)
                {
                    resourceNodeCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.SpawnRule)
                {
                    spawnRuleCount += count;
                }
                else if (entry.Kind == DimensionAuthoringContentSummaryKind.SemanticFloorObject ||
                    entry.Kind == DimensionAuthoringContentSummaryKind.SemanticWallObject ||
                    entry.Kind == DimensionAuthoringContentSummaryKind.SemanticOreObject ||
                    entry.Kind == DimensionAuthoringContentSummaryKind.SemanticWaterObject)
                {
                    semanticObjectCount += count;
                }
            }
        }

        private static int EstimateTileArea(
            IReadOnlyList<DimensionAuthoringPreviewEntry> entries)
        {
            int area = 0;
            if (entries == null)
            {
                return area;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringPreviewEntry entry = entries[i];
                if (!entry.HasLocalBounds)
                {
                    continue;
                }

                if (entry.LayerKind != DimensionAuthoringPreviewLayerKind.BiomeRegion &&
                    entry.LayerKind != DimensionAuthoringPreviewLayerKind.GenerationPassBounds &&
                    entry.LayerKind != DimensionAuthoringPreviewLayerKind.PlayableBounds)
                {
                    continue;
                }

                DimensionBounds bounds = entry.LocalBounds;
                int width = bounds.MaxExclusive.x - bounds.Min.x;
                int height = bounds.MaxExclusive.y - bounds.Min.y;
                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                long entryArea = (long)width * height;
                if (entryArea > int.MaxValue)
                {
                    area = int.MaxValue;
                    break;
                }

                if (int.MaxValue - area < entryArea)
                {
                    area = int.MaxValue;
                    break;
                }

                area += (int)entryArea;
            }

            return area;
        }

        private static int EstimateComplexityScore(
            int area,
            int passCount,
            int tableCount,
            int tableEntryCount,
            int semanticObjectCount,
            int sceneTemplateCount,
            int resourceNodeCount,
            int spawnRuleCount)
        {
            long score =
                (area / 256L) +
                passCount * 20L +
                tableCount * 8L +
                tableEntryCount * 2L +
                semanticObjectCount +
                sceneTemplateCount * 12L +
                resourceNodeCount * 10L +
                spawnRuleCount * 10L;

            return score > int.MaxValue ? int.MaxValue : (int)score;
        }

        private static void ResolveComplexity(
            int score,
            int readinessBlocked,
            int blockingIssues,
            out string code,
            out string message)
        {
            if (blockingIssues > 0 || readinessBlocked > 0)
            {
                code = "blocked";
                message = "Authoring issues must be fixed before this generation budget is meaningful.";
                return;
            }

            if (score >= 1000)
            {
                code = "heavy";
                message = "Generation is likely to need careful provider budgeting and staged execution.";
                return;
            }

            if (score >= 350)
            {
                code = "moderate";
                message = "Generation complexity is moderate; provider work should be batched and previewed.";
                return;
            }

            code = "light";
            message = "Generation complexity looks light from the current authoring data.";
        }
    }
}
