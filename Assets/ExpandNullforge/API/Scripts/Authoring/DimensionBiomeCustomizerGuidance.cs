using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public enum DimensionBiomeCustomizerGuidanceKind
    {
        None = 0,
        AddContent = 1,
        ConfigureContent = 2,
        FixIssues = 3,
        ReviewWarnings = 4,
        Ready = 5
    }

    public readonly struct DimensionBiomeCustomizerGuidanceItem
    {
        public readonly DimensionBiomeCustomizerCapabilityStatus Status;
        public readonly DimensionBiomeCustomizerGuidanceKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly string CapabilityId;
        public readonly string Title;
        public readonly string Message;
        public readonly string PrimaryActionId;
        public readonly int Priority;
        public readonly bool BlocksExport;
        public readonly bool BlocksRuntimeGeneration;

        public DimensionBiomeCustomizerGuidanceItem(
            DimensionBiomeCustomizerCapabilityStatus status,
            DimensionBiomeCustomizerGuidanceKind kind,
            DimensionAuthoringReadinessState state,
            string dimensionId,
            string biomeId,
            string capabilityId,
            string title,
            string message,
            string primaryActionId,
            int priority,
            bool blocksExport,
            bool blocksRuntimeGeneration)
        {
            Status = status;
            Kind = kind;
            State = state;
            DimensionId = dimensionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            CapabilityId = capabilityId ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            Priority = priority < 0 ? 0 : priority;
            BlocksExport = blocksExport;
            BlocksRuntimeGeneration = blocksRuntimeGeneration;
        }
    }

    public static class DimensionBiomeCustomizerGuidanceUtility
    {
        public static List<DimensionBiomeCustomizerGuidanceItem> BuildBuiltInGuidance(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            List<DimensionBiomeCustomizerCapabilityStatus> statuses =
                DimensionBiomeCustomizerCapabilityStatusUtility.BuildBuiltInStatuses(
                    summary,
                    biomeId);

            return BuildGuidance(statuses);
        }

        public static List<DimensionBiomeCustomizerGuidanceItem> BuildGuidance(
            IReadOnlyList<DimensionBiomeCustomizerCapabilityStatus> statuses)
        {
            List<DimensionBiomeCustomizerGuidanceItem> guidance =
                new List<DimensionBiomeCustomizerGuidanceItem>();

            if (statuses == null)
            {
                return guidance;
            }

            for (int i = 0; i < statuses.Count; i++)
            {
                guidance.Add(BuildGuidanceItem(statuses[i]));
            }

            guidance.Sort(CompareGuidance);
            return guidance;
        }

        public static DimensionBiomeCustomizerGuidanceItem BuildGuidanceItem(
            DimensionBiomeCustomizerCapabilityStatus status)
        {
            string displayName = status.Capability.DisplayName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = status.Capability.CapabilityId;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = "Capability";
            }

            DimensionBiomeCustomizerGuidanceKind kind;
            string title;
            string message;
            string actionId;
            int priority;
            bool blocksExport;
            bool blocksRuntimeGeneration;

            switch (status.State)
            {
                case DimensionAuthoringReadinessState.Blocked:
                    kind = DimensionBiomeCustomizerGuidanceKind.FixIssues;
                    title = "Fix " + displayName;
                    message = BuildBlockedMessage(status, displayName);
                    actionId = "fix-issues";
                    priority = 0;
                    blocksExport = true;
                    blocksRuntimeGeneration = true;
                    break;

                case DimensionAuthoringReadinessState.Missing:
                    kind = DimensionBiomeCustomizerGuidanceKind.AddContent;
                    title = "Add " + displayName;
                    message = BuildMissingMessage(status, displayName);
                    actionId = "add-content";
                    priority = status.Capability.RuntimeProviderRequired ? 10 : 30;
                    blocksExport = false;
                    blocksRuntimeGeneration = status.Capability.RuntimeProviderRequired;
                    break;

                case DimensionAuthoringReadinessState.Partial:
                    if (status.WarningCount > 0 || status.IssueCount > 0)
                    {
                        kind = DimensionBiomeCustomizerGuidanceKind.ReviewWarnings;
                        title = "Review " + displayName;
                        message = BuildPartialIssueMessage(status, displayName);
                        actionId = "review-issues";
                        priority = 20;
                    }
                    else
                    {
                        kind = DimensionBiomeCustomizerGuidanceKind.ConfigureContent;
                        title = "Configure " + displayName;
                        message = BuildPartialMessage(status, displayName);
                        actionId = "configure-content";
                        priority = 40;
                    }

                    blocksExport = false;
                    blocksRuntimeGeneration = false;
                    break;

                default:
                    kind = DimensionBiomeCustomizerGuidanceKind.Ready;
                    title = displayName + " ready";
                    message = displayName + " is configured for this biome.";
                    actionId = "inspect-content";
                    priority = 100;
                    blocksExport = false;
                    blocksRuntimeGeneration = false;
                    break;
            }

            return new DimensionBiomeCustomizerGuidanceItem(
                status,
                kind,
                status.State,
                status.DimensionId,
                status.BiomeId,
                status.Capability.CapabilityId,
                title,
                message,
                actionId,
                priority,
                blocksExport,
                blocksRuntimeGeneration);
        }

        private static string BuildBlockedMessage(
            DimensionBiomeCustomizerCapabilityStatus status,
            string displayName)
        {
            if (status.ErrorCount == 1)
            {
                return displayName + " has 1 blocking issue that must be fixed.";
            }

            return displayName + " has " + status.ErrorCount + " blocking issues that must be fixed.";
        }

        private static string BuildMissingMessage(
            DimensionBiomeCustomizerCapabilityStatus status,
            string displayName)
        {
            string capabilityId = status.Capability.CapabilityId;
            if (capabilityId == "scene-template")
            {
                return "Optional for now. Add scenes when this biome needs hand-built rooms, landmarks, dungeons, boss arenas, or exact structure placements.";
            }

            if (capabilityId == "resource-node")
            {
                return "Optional for now. Add resource nodes when this biome should contain ore boulders, landmarks, or special gatherable resources.";
            }

            if (capabilityId == "spawn-rule")
            {
                return "Optional for now. Add spawn rules when this biome should spawn mobs, critters, NPCs, bosses, or encounters.";
            }

            if (capabilityId == "content-preset")
            {
                return "Optional helper. Add content presets only when you want reusable bundles of biome defaults.";
            }

            if (status.Capability.RuntimeProviderRequired)
            {
                return displayName + " needs a runtime-ready provider or generated authoring data before this biome can generate in game.";
            }

            return displayName + " is not configured yet. Add it when this biome should control this category.";
        }

        private static string BuildPartialIssueMessage(
            DimensionBiomeCustomizerCapabilityStatus status,
            string displayName)
        {
            if (status.WarningCount == 1)
            {
                return displayName + " is configured, but has 1 warning to review.";
            }

            if (status.WarningCount > 1)
            {
                return displayName + " is configured, but has " + status.WarningCount + " warnings to review.";
            }

            return displayName + " is configured, but has advisory issues to review.";
        }

        private static string BuildPartialMessage(
            DimensionBiomeCustomizerCapabilityStatus status,
            string displayName)
        {
            if (status.ActiveCount <= 0 && status.ConfiguredCount > 0)
            {
                return displayName + " has configured records, but none are active yet.";
            }

            return displayName + " is partly configured. Add or refine content before considering this biome complete.";
        }

        private static int CompareGuidance(
            DimensionBiomeCustomizerGuidanceItem left,
            DimensionBiomeCustomizerGuidanceItem right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.CompareOrdinal(left.Title, right.Title);
        }
    }
}
