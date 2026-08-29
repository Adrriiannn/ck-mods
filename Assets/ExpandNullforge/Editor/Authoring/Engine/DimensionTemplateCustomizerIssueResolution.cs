using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerIssueResolutionKind
    {
        Inspect = 0,
        JumpToBounds = 1,
        EditField = 2,
        ConfigureContent = 3,
        AddContent = 4,
        Validate = 5
    }

    public sealed class DimensionTemplateCustomizerIssueResolutionHint
    {
        public DimensionTemplateCustomizerIssueResolutionHint(
            DimensionAuthoringSeverity severity,
            string issueCode,
            string issueMessage,
            string sectionId,
            string recordKind,
            string recordId,
            DimensionTemplateCustomizerIssueResolutionKind kind,
            DimensionTemplateCustomizerFocusRequest focusRequest,
            string suggestedActionId,
            string suggestedFieldId,
            string label,
            string hint,
            bool canJumpToBounds,
            bool requiresEditorImplementation,
            int priority)
        {
            Severity = severity;
            IssueCode = issueCode ?? string.Empty;
            IssueMessage = issueMessage ?? string.Empty;
            SectionId = sectionId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Kind = kind;
            FocusRequest = focusRequest;
            SuggestedActionId = suggestedActionId ?? string.Empty;
            SuggestedFieldId = suggestedFieldId ?? string.Empty;
            Label = label ?? string.Empty;
            Hint = hint ?? string.Empty;
            CanJumpToBounds = canJumpToBounds;
            RequiresEditorImplementation = requiresEditorImplementation;
            Priority = priority < 0 ? 0 : priority;
        }

        public DimensionAuthoringSeverity Severity { get; private set; }

        public string IssueCode { get; private set; }

        public string IssueMessage { get; private set; }

        public string SectionId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public DimensionTemplateCustomizerIssueResolutionKind Kind { get; private set; }

        public DimensionTemplateCustomizerFocusRequest FocusRequest { get; private set; }

        public string SuggestedActionId { get; private set; }

        public string SuggestedFieldId { get; private set; }

        public string Label { get; private set; }

        public string Hint { get; private set; }

        public bool CanJumpToBounds { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }

        public int Priority { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerIssueResolutionCatalog
    {
        public DimensionTemplateCustomizerIssueResolutionCatalog(
            string sectionId,
            int hintCount,
            int errorCount,
            int warningCount,
            int jumpToBoundsCount,
            int editFieldCount,
            IReadOnlyList<DimensionTemplateCustomizerIssueResolutionHint> hints)
        {
            SectionId = sectionId ?? string.Empty;
            HintCount = hintCount < 0 ? 0 : hintCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            JumpToBoundsCount = jumpToBoundsCount < 0 ? 0 : jumpToBoundsCount;
            EditFieldCount = editFieldCount < 0 ? 0 : editFieldCount;
            Hints = hints ?? new List<DimensionTemplateCustomizerIssueResolutionHint>();
        }

        public string SectionId { get; private set; }

        public int HintCount { get; private set; }

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public int JumpToBoundsCount { get; private set; }

        public int EditFieldCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerIssueResolutionHint> Hints { get; private set; }
    }

    public static class DimensionTemplateCustomizerIssueResolutionUtility
    {
        public static DimensionTemplateCustomizerIssueResolutionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            return Build(workspace, string.Empty);
        }

        public static DimensionTemplateCustomizerIssueResolutionCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            List<DimensionTemplateCustomizerIssueResolutionHint> hints =
                new List<DimensionTemplateCustomizerIssueResolutionHint>();
            if (workspace == null)
            {
                return new DimensionTemplateCustomizerIssueResolutionCatalog(
                    sectionId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    hints);
            }

            DimensionAuthoringPreviewSummary preview = workspace.Preview;
            if (preview.Issues == null)
            {
                return new DimensionTemplateCustomizerIssueResolutionCatalog(
                    sectionId,
                    0,
                    0,
                    0,
                    0,
                    0,
                    hints);
            }

            string normalizedSection = Normalize(sectionId);
            IReadOnlyList<DimensionAuthoringIssue> issues = preview.Issues;
            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                DimensionTemplateCustomizerIssueResolutionHint hint = BuildHint(issue);
                if (!string.IsNullOrEmpty(normalizedSection) &&
                    Normalize(hint.SectionId) != normalizedSection)
                {
                    continue;
                }

                hints.Add(hint);
            }

            hints.Sort(CompareHints);

            int errorCount;
            int warningCount;
            int jumpToBoundsCount;
            int editFieldCount;
            CountHints(hints, out errorCount, out warningCount, out jumpToBoundsCount, out editFieldCount);

            return new DimensionTemplateCustomizerIssueResolutionCatalog(
                sectionId,
                hints.Count,
                errorCount,
                warningCount,
                jumpToBoundsCount,
                editFieldCount,
                hints);
        }

        private static DimensionTemplateCustomizerIssueResolutionHint BuildHint(
            DimensionAuthoringIssue issue)
        {
            string sectionId = ResolveSectionForRecordKind(issue.RecordKind);
            string actionId = ResolveSuggestedActionId(sectionId, issue);
            string fieldId = ResolveSuggestedFieldId(issue);
            DimensionTemplateCustomizerIssueResolutionKind kind = ResolveKind(issue, fieldId);
            DimensionTemplateCustomizerFocusRequest focus = DimensionTemplateCustomizerFocusRequest.ForRecord(
                sectionId,
                ResolveBiomeId(issue),
                issue.RecordKind,
                issue.RecordId,
                issue.HasLocalBounds);

            return new DimensionTemplateCustomizerIssueResolutionHint(
                issue.Severity,
                issue.Code,
                issue.Message,
                sectionId,
                issue.RecordKind,
                issue.RecordId,
                kind,
                focus,
                actionId,
                fieldId,
                ResolveLabel(kind, issue),
                ResolveHint(kind, issue, fieldId),
                issue.HasLocalBounds,
                RequiresEditorImplementation(kind),
                ResolvePriority(issue, kind));
        }

        private static DimensionTemplateCustomizerIssueResolutionKind ResolveKind(
            DimensionAuthoringIssue issue,
            string fieldId)
        {
            if (!string.IsNullOrEmpty(fieldId))
            {
                return DimensionTemplateCustomizerIssueResolutionKind.EditField;
            }

            string normalizedCode = Normalize(issue.Code);
            if (normalizedCode.IndexOf("missing") >= 0 ||
                normalizedCode.IndexOf("empty") >= 0)
            {
                return DimensionTemplateCustomizerIssueResolutionKind.AddContent;
            }

            if (issue.HasLocalBounds)
            {
                return DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds;
            }

            if (normalizedCode.IndexOf("validate") >= 0 ||
                normalizedCode.IndexOf("invalid") >= 0)
            {
                return DimensionTemplateCustomizerIssueResolutionKind.Validate;
            }

            return DimensionTemplateCustomizerIssueResolutionKind.ConfigureContent;
        }

        private static string ResolveSuggestedFieldId(DimensionAuthoringIssue issue)
        {
            string code = Normalize(issue.Code);
            string kind = Normalize(issue.RecordKind);

            if (code.IndexOf("dimension-id") >= 0 || kind == "dimensiontemplate")
            {
                if (code.IndexOf("reserved") >= 0 || code.IndexOf("bounds") >= 0)
                {
                    return "reserved-local-bounds";
                }

                if (code.IndexOf("id") >= 0)
                {
                    return "dimension-id";
                }
            }

            if (code.IndexOf("display-name") >= 0)
            {
                return "display-name";
            }

            if (code.IndexOf("biome-id") >= 0)
            {
                return "biome-id";
            }

            if (code.IndexOf("environment") >= 0)
            {
                return "environment-profile-id";
            }

            if (code.IndexOf("palette") >= 0)
            {
                return "palette-id";
            }

            if (code.IndexOf("fallback") >= 0 && code.IndexOf("bounds") >= 0)
            {
                return "fallback-local-bounds";
            }

            if (code.IndexOf("enabled") >= 0)
            {
                return "enabled";
            }

            return string.Empty;
        }

        private static string ResolveSuggestedActionId(
            string sectionId,
            DimensionAuthoringIssue issue)
        {
            string code = Normalize(issue.Code);
            if (code.IndexOf("id") >= 0 ||
                code.IndexOf("bounds") >= 0 ||
                code.IndexOf("invalid") >= 0)
            {
                return "configure-" + sectionId;
            }

            if (code.IndexOf("missing") >= 0 ||
                code.IndexOf("empty") >= 0)
            {
                return "add-" + sectionId;
            }

            return "review-" + sectionId;
        }

        private static string ResolveLabel(
            DimensionTemplateCustomizerIssueResolutionKind kind,
            DimensionAuthoringIssue issue)
        {
            if (kind == DimensionTemplateCustomizerIssueResolutionKind.EditField)
            {
                return "Edit field";
            }

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds)
            {
                return "Inspect bounds";
            }

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.AddContent)
            {
                return "Add missing content";
            }

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.Validate)
            {
                return "Review validation";
            }

            if (issue.Severity == DimensionAuthoringSeverity.Error)
            {
                return "Fix blocking issue";
            }

            return "Review issue";
        }

        private static string ResolveHint(
            DimensionTemplateCustomizerIssueResolutionKind kind,
            DimensionAuthoringIssue issue,
            string fieldId)
        {
            if (kind == DimensionTemplateCustomizerIssueResolutionKind.EditField)
            {
                return "Focus this record and edit `" + fieldId + "`.";
            }

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds)
            {
                return "Jump to the affected local bounds and inspect the overlapping or invalid area.";
            }

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.AddContent)
            {
                return "Open the matching customizer section and add the missing authored content.";
            }

            return string.IsNullOrEmpty(issue.Message)
                ? "Open the matching customizer section and review this issue."
                : issue.Message;
        }

        private static bool RequiresEditorImplementation(
            DimensionTemplateCustomizerIssueResolutionKind kind)
        {
            return kind == DimensionTemplateCustomizerIssueResolutionKind.EditField ||
                kind == DimensionTemplateCustomizerIssueResolutionKind.AddContent ||
                kind == DimensionTemplateCustomizerIssueResolutionKind.ConfigureContent;
        }

        private static int ResolvePriority(
            DimensionAuthoringIssue issue,
            DimensionTemplateCustomizerIssueResolutionKind kind)
        {
            int priority = issue.Severity == DimensionAuthoringSeverity.Error
                ? 0
                : issue.Severity == DimensionAuthoringSeverity.Warning ? 100 : 200;

            if (kind == DimensionTemplateCustomizerIssueResolutionKind.EditField)
            {
                priority += 0;
            }
            else if (kind == DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds)
            {
                priority += 10;
            }
            else if (kind == DimensionTemplateCustomizerIssueResolutionKind.AddContent)
            {
                priority += 20;
            }
            else
            {
                priority += 40;
            }

            return priority;
        }

        private static string ResolveBiomeId(DimensionAuthoringIssue issue)
        {
            string kind = Normalize(issue.RecordKind);
            if (kind.IndexOf("biome") >= 0)
            {
                return issue.RecordId;
            }

            return string.Empty;
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

            if (normalized.IndexOf("resource") >= 0)
            {
                return "resources";
            }

            if (normalized.IndexOf("spawn") >= 0 || normalized.IndexOf("mob") >= 0)
            {
                return "spawns";
            }

            if (normalized.IndexOf("generation") >= 0 ||
                normalized.IndexOf("table") >= 0 ||
                normalized.IndexOf("pass") >= 0)
            {
                return "generation";
            }

            if (normalized.IndexOf("floor") >= 0 ||
                normalized.IndexOf("wall") >= 0 ||
                normalized.IndexOf("ore") >= 0 ||
                normalized.IndexOf("water") >= 0 ||
                normalized.IndexOf("palette") >= 0)
            {
                return "terrain";
            }

            if (normalized.IndexOf("biome") >= 0 ||
                normalized.IndexOf("environment") >= 0)
            {
                return "biomes";
            }

            if (normalized.IndexOf("dimension") >= 0)
            {
                return "dimension";
            }

            return "diagnostics";
        }

        private static void CountHints(
            IReadOnlyList<DimensionTemplateCustomizerIssueResolutionHint> hints,
            out int errorCount,
            out int warningCount,
            out int jumpToBoundsCount,
            out int editFieldCount)
        {
            errorCount = 0;
            warningCount = 0;
            jumpToBoundsCount = 0;
            editFieldCount = 0;

            for (int i = 0; i < hints.Count; i++)
            {
                DimensionTemplateCustomizerIssueResolutionHint hint = hints[i];
                if (hint.Severity == DimensionAuthoringSeverity.Error)
                {
                    errorCount++;
                }
                else if (hint.Severity == DimensionAuthoringSeverity.Warning)
                {
                    warningCount++;
                }

                if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.JumpToBounds)
                {
                    jumpToBoundsCount++;
                }
                else if (hint.Kind == DimensionTemplateCustomizerIssueResolutionKind.EditField)
                {
                    editFieldCount++;
                }
            }
        }

        private static int CompareHints(
            DimensionTemplateCustomizerIssueResolutionHint left,
            DimensionTemplateCustomizerIssueResolutionHint right)
        {
            int priorityCompare = left.Priority.CompareTo(right.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            int sectionCompare = string.CompareOrdinal(left.SectionId, right.SectionId);
            if (sectionCompare != 0)
            {
                return sectionCompare;
            }

            int recordCompare = string.CompareOrdinal(left.RecordKind, right.RecordKind);
            if (recordCompare != 0)
            {
                return recordCompare;
            }

            return string.CompareOrdinal(left.IssueCode, right.IssueCode);
        }

        private static string Normalize(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();
        }
    }
}
