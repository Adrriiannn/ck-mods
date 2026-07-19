using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public enum DimensionGenerationBudgetGuidanceKind
    {
        None = 0,
        Overview = 1,
        Blocked = 2,
        MissingPlayableArea = 3,
        MissingGenerationContent = 4,
        Complexity = 5,
        GenerationPassCount = 6,
        WeightedEntryCount = 7,
        SceneCount = 8,
        ResourceCount = 9,
        SpawnRuleCount = 10,
        Ready = 11
    }

    public readonly struct DimensionGenerationBudgetGuidanceItem
    {
        public readonly DimensionGenerationBudgetGuidanceKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly DimensionAuthoringSeverity Severity;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string Title;
        public readonly string Message;
        public readonly string PrimaryActionId;
        public readonly int Priority;
        public readonly int Score;
        public readonly int Count;
        public readonly bool BlocksExport;
        public readonly bool BlocksRuntimeGeneration;

        public DimensionGenerationBudgetGuidanceItem(
            DimensionGenerationBudgetGuidanceKind kind,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string dimensionId,
            string biomeId,
            string title,
            string message,
            string primaryActionId,
            int priority,
            int score,
            int count,
            bool blocksExport,
            bool blocksRuntimeGeneration)
        {
            Kind = kind;
            State = state;
            Severity = severity;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            Priority = priority < 0 ? 0 : priority;
            Score = score < 0 ? 0 : score;
            Count = count < 0 ? 0 : count;
            BlocksExport = blocksExport;
            BlocksRuntimeGeneration = blocksRuntimeGeneration;
        }
    }

    public sealed class DimensionGenerationBudgetGuidanceCatalog
    {
        public DimensionGenerationBudgetGuidanceCatalog(
            DimensionGenerationBudgetSummary summary,
            int itemCount,
            int errorCount,
            int warningCount,
            int infoCount,
            bool blocksExport,
            bool blocksRuntimeGeneration,
            IReadOnlyList<DimensionGenerationBudgetGuidanceItem> items)
        {
            Summary = summary;
            ItemCount = itemCount < 0 ? 0 : itemCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            InfoCount = infoCount < 0 ? 0 : infoCount;
            BlocksExport = blocksExport;
            BlocksRuntimeGeneration = blocksRuntimeGeneration;
            Items = items ?? new List<DimensionGenerationBudgetGuidanceItem>();
        }

        public DimensionGenerationBudgetSummary Summary { get; private set; }

        public int ItemCount { get; private set; }

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public int InfoCount { get; private set; }

        public bool BlocksExport { get; private set; }

        public bool BlocksRuntimeGeneration { get; private set; }

        public IReadOnlyList<DimensionGenerationBudgetGuidanceItem> Items { get; private set; }
    }

    public static class DimensionGenerationBudgetGuidanceUtility
    {
        private const int HeavyScoreThreshold = 1000;
        private const int ModerateScoreThreshold = 350;
        private const int ManyPassesThreshold = 12;
        private const int ManyWeightedEntriesThreshold = 128;
        private const int ManyScenesThreshold = 24;
        private const int ManyResourcesThreshold = 24;
        private const int ManySpawnRulesThreshold = 24;

        public static DimensionGenerationBudgetGuidanceCatalog Build(
            DimensionGenerationBudgetSummary summary)
        {
            List<DimensionGenerationBudgetGuidanceItem> items =
                new List<DimensionGenerationBudgetGuidanceItem>();

            AddSummaryItem(items, summary);

            IReadOnlyList<DimensionBiomeGenerationBudget> biomeBudgets =
                summary.BiomeBudgets;
            if (biomeBudgets != null)
            {
                for (int i = 0; i < biomeBudgets.Count; i++)
                {
                    AddBiomeItems(items, biomeBudgets[i]);
                }
            }

            items.Sort(CompareItems);

            int errors = 0;
            int warnings = 0;
            int infos = 0;
            bool blocksExport = false;
            bool blocksRuntimeGeneration = false;

            for (int i = 0; i < items.Count; i++)
            {
                DimensionGenerationBudgetGuidanceItem item = items[i];
                if (item.Severity == DimensionAuthoringSeverity.Error)
                {
                    errors++;
                }
                else if (item.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warnings++;
                }
                else
                {
                    infos++;
                }

                blocksExport = blocksExport || item.BlocksExport;
                blocksRuntimeGeneration =
                    blocksRuntimeGeneration || item.BlocksRuntimeGeneration;
            }

            return new DimensionGenerationBudgetGuidanceCatalog(
                summary,
                items.Count,
                errors,
                warnings,
                infos,
                blocksExport,
                blocksRuntimeGeneration,
                items);
        }

        private static void AddSummaryItem(
            List<DimensionGenerationBudgetGuidanceItem> items,
            DimensionGenerationBudgetSummary summary)
        {
            if (items == null)
            {
                return;
            }

            DimensionAuthoringReadinessState state;
            DimensionAuthoringSeverity severity;
            string title;
            string message;
            int priority;
            bool blocksExport;
            bool blocksRuntimeGeneration;

            if (summary.TotalBlockingIssueCount > 0 ||
                summary.TotalReadinessBlockedCount > 0)
            {
                state = DimensionAuthoringReadinessState.Blocked;
                severity = DimensionAuthoringSeverity.Error;
                title = "Generation budget blocked";
                message = "Fix blocking authoring issues before trusting generation budget numbers.";
                priority = 0;
                blocksExport = true;
                blocksRuntimeGeneration = true;
            }
            else if (summary.BiomeCount <= 0)
            {
                state = DimensionAuthoringReadinessState.Missing;
                severity = DimensionAuthoringSeverity.Warning;
                title = "No biome budgets yet";
                message = "Add at least one biome before reviewing generation cost.";
                priority = 20;
                blocksExport = false;
                blocksRuntimeGeneration = true;
            }
            else if (summary.TotalEstimatedComplexityScore >= HeavyScoreThreshold)
            {
                state = DimensionAuthoringReadinessState.Partial;
                severity = DimensionAuthoringSeverity.Warning;
                title = "Heavy generation budget";
                message = "This dimension should use staged providers and conservative runtime tickets.";
                priority = 40;
                blocksExport = false;
                blocksRuntimeGeneration = false;
            }
            else if (summary.TotalEstimatedComplexityScore >= ModerateScoreThreshold)
            {
                state = DimensionAuthoringReadinessState.Partial;
                severity = DimensionAuthoringSeverity.Info;
                title = "Moderate generation budget";
                message = "Generation cost looks reasonable, but still worth previewing before runtime use.";
                priority = 80;
                blocksExport = false;
                blocksRuntimeGeneration = false;
            }
            else
            {
                state = DimensionAuthoringReadinessState.Ready;
                severity = DimensionAuthoringSeverity.Info;
                title = "Generation budget looks light";
                message = "The authored biome data looks lightweight from the current budget estimate.";
                priority = 200;
                blocksExport = false;
                blocksRuntimeGeneration = false;
            }

            items.Add(new DimensionGenerationBudgetGuidanceItem(
                DimensionGenerationBudgetGuidanceKind.Overview,
                state,
                severity,
                summary.DimensionId,
                string.Empty,
                title,
                message,
                "inspect-generation-budget",
                priority,
                summary.TotalEstimatedComplexityScore,
                summary.BiomeCount,
                blocksExport,
                blocksRuntimeGeneration));
        }

        private static void AddBiomeItems(
            List<DimensionGenerationBudgetGuidanceItem> items,
            DimensionBiomeGenerationBudget budget)
        {
            if (items == null)
            {
                return;
            }

            string biomeName = string.IsNullOrEmpty(budget.DisplayName)
                ? budget.BiomeId
                : budget.DisplayName;
            if (string.IsNullOrEmpty(biomeName))
            {
                biomeName = "Biome";
            }

            if (budget.BlockingIssueCount > 0 ||
                budget.ReadinessBlockedCount > 0)
            {
                AddBiomeItem(
                    items,
                    budget,
                    DimensionGenerationBudgetGuidanceKind.Blocked,
                    DimensionAuthoringReadinessState.Blocked,
                    DimensionAuthoringSeverity.Error,
                    biomeName + " has blockers",
                    "Fix this biome's blocking issues before using its generation budget.",
                    "fix-issues",
                    10,
                    budget.BlockingIssueCount + budget.ReadinessBlockedCount,
                    true,
                    true);
                return;
            }

            if (budget.EstimatedTileArea <= 0)
            {
                AddBiomeItem(
                    items,
                    budget,
                    DimensionGenerationBudgetGuidanceKind.MissingPlayableArea,
                    DimensionAuthoringReadinessState.Missing,
                    DimensionAuthoringSeverity.Warning,
                    biomeName + " has no area",
                    "Add a biome region or generation bounds so this biome has a real playable area.",
                    "inspect-layout",
                    30,
                    0,
                    false,
                    true);
            }

            if (budget.GenerationPassCount <= 0 &&
                budget.GenerationTableCount <= 0 &&
                budget.SemanticObjectCount <= 0)
            {
                AddBiomeItem(
                    items,
                    budget,
                    DimensionGenerationBudgetGuidanceKind.MissingGenerationContent,
                    DimensionAuthoringReadinessState.Missing,
                    DimensionAuthoringSeverity.Warning,
                    biomeName + " has no generation content",
                    "Add semantic terrain, weighted tables, or generation passes before runtime generation.",
                    "add-generation-content",
                    35,
                    0,
                    false,
                    true);
            }

            AddComplexityItem(items, budget, biomeName);
            AddCountThresholdItem(
                items,
                budget,
                biomeName,
                DimensionGenerationBudgetGuidanceKind.GenerationPassCount,
                budget.GenerationPassCount,
                ManyPassesThreshold,
                "generation passes",
                "review-generation-passes",
                90);
            AddCountThresholdItem(
                items,
                budget,
                biomeName,
                DimensionGenerationBudgetGuidanceKind.WeightedEntryCount,
                budget.GenerationTableEntryCount,
                ManyWeightedEntriesThreshold,
                "weighted table entries",
                "review-generation-tables",
                95);
            AddCountThresholdItem(
                items,
                budget,
                biomeName,
                DimensionGenerationBudgetGuidanceKind.SceneCount,
                budget.SceneTemplateCount,
                ManyScenesThreshold,
                "scene templates",
                "review-scenes",
                100);
            AddCountThresholdItem(
                items,
                budget,
                biomeName,
                DimensionGenerationBudgetGuidanceKind.ResourceCount,
                budget.ResourceNodeCount,
                ManyResourcesThreshold,
                "resource nodes",
                "review-resources",
                105);
            AddCountThresholdItem(
                items,
                budget,
                biomeName,
                DimensionGenerationBudgetGuidanceKind.SpawnRuleCount,
                budget.SpawnRuleCount,
                ManySpawnRulesThreshold,
                "spawn rules",
                "review-spawns",
                110);
        }

        private static void AddComplexityItem(
            List<DimensionGenerationBudgetGuidanceItem> items,
            DimensionBiomeGenerationBudget budget,
            string biomeName)
        {
            if (budget.EstimatedComplexityScore >= HeavyScoreThreshold)
            {
                AddBiomeItem(
                    items,
                    budget,
                    DimensionGenerationBudgetGuidanceKind.Complexity,
                    DimensionAuthoringReadinessState.Partial,
                    DimensionAuthoringSeverity.Warning,
                    biomeName + " is heavy",
                    "This biome should be split into staged generation passes or smaller authored regions.",
                    "review-generation-budget",
                    50,
                    budget.EstimatedComplexityScore,
                    false,
                    false);
            }
            else if (budget.EstimatedComplexityScore >= ModerateScoreThreshold)
            {
                AddBiomeItem(
                    items,
                    budget,
                    DimensionGenerationBudgetGuidanceKind.Complexity,
                    DimensionAuthoringReadinessState.Partial,
                    DimensionAuthoringSeverity.Info,
                    biomeName + " is moderate",
                    "This biome should be previewed, but its generation budget looks manageable.",
                    "review-generation-budget",
                    150,
                    budget.EstimatedComplexityScore,
                    false,
                    false);
            }
        }

        private static void AddCountThresholdItem(
            List<DimensionGenerationBudgetGuidanceItem> items,
            DimensionBiomeGenerationBudget budget,
            string biomeName,
            DimensionGenerationBudgetGuidanceKind kind,
            int count,
            int threshold,
            string label,
            string actionId,
            int priority)
        {
            if (count < threshold)
            {
                return;
            }

            AddBiomeItem(
                items,
                budget,
                kind,
                DimensionAuthoringReadinessState.Partial,
                DimensionAuthoringSeverity.Warning,
                biomeName + " has many " + label,
                count + " " + label + " are authored for this biome. Review whether they can be grouped or staged.",
                actionId,
                priority,
                count,
                false,
                false);
        }

        private static void AddBiomeItem(
            List<DimensionGenerationBudgetGuidanceItem> items,
            DimensionBiomeGenerationBudget budget,
            DimensionGenerationBudgetGuidanceKind kind,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string title,
            string message,
            string actionId,
            int priority,
            int count,
            bool blocksExport,
            bool blocksRuntimeGeneration)
        {
            items.Add(new DimensionGenerationBudgetGuidanceItem(
                kind,
                state,
                severity,
                budget.DimensionId,
                budget.BiomeId,
                title,
                message,
                actionId,
                priority,
                budget.EstimatedComplexityScore,
                count,
                blocksExport,
                blocksRuntimeGeneration));
        }

        private static int CompareItems(
            DimensionGenerationBudgetGuidanceItem left,
            DimensionGenerationBudgetGuidanceItem right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int biome = string.CompareOrdinal(left.BiomeId, right.BiomeId);
            if (biome != 0)
            {
                return biome;
            }

            return string.CompareOrdinal(left.Title, right.Title);
        }
    }
}
