using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFocusKind
    {
        None = 0,
        Section = 1,
        Record = 2,
        Bounds = 3,
        Action = 4
    }

    public enum DimensionTemplateCustomizerFocusSource
    {
        None = 0,
        DefaultSection = 1,
        ExplicitRequest = 2,
        DetailRow = 3,
        Action = 4,
        SearchResult = 5
    }

    public readonly struct DimensionTemplateCustomizerFocusRequest
    {
        public readonly string SectionId;
        public readonly string BiomeId;
        public readonly string ZoneId;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly string ActionId;
        public readonly bool PreferBounds;

        public DimensionTemplateCustomizerFocusRequest(
            string sectionId,
            string biomeId,
            string zoneId,
            string recordKind,
            string recordId,
            string actionId,
            bool preferBounds)
        {
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            PreferBounds = preferBounds;
        }

        public static DimensionTemplateCustomizerFocusRequest ForSection(string sectionId)
        {
            return new DimensionTemplateCustomizerFocusRequest(
                sectionId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false);
        }

        public static DimensionTemplateCustomizerFocusRequest ForRecord(
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            bool preferBounds)
        {
            return new DimensionTemplateCustomizerFocusRequest(
                sectionId,
                biomeId,
                string.Empty,
                recordKind,
                recordId,
                string.Empty,
                preferBounds);
        }

        public static DimensionTemplateCustomizerFocusRequest ForAction(
            string sectionId,
            string actionId)
        {
            return new DimensionTemplateCustomizerFocusRequest(
                sectionId,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                actionId,
                false);
        }
    }

    public sealed class DimensionTemplateCustomizerFocusState
    {
        public DimensionTemplateCustomizerFocusState(
            bool hasFocus,
            DimensionTemplateCustomizerFocusKind kind,
            DimensionTemplateCustomizerFocusSource source,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string sectionId,
            string biomeId,
            string zoneId,
            string recordKind,
            string recordId,
            string actionId,
            string displayName,
            string message,
            bool hasLocalBounds,
            DimensionBounds localBounds,
            bool canJumpToBounds,
            bool canRunAction,
            int priority)
        {
            HasFocus = hasFocus;
            Kind = kind;
            Source = source;
            State = state;
            Severity = severity;
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Message = message ?? string.Empty;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
            CanJumpToBounds = canJumpToBounds;
            CanRunAction = canRunAction;
            Priority = priority < 0 ? 0 : priority;
        }

        public bool HasFocus { get; private set; }

        public DimensionTemplateCustomizerFocusKind Kind { get; private set; }

        public DimensionTemplateCustomizerFocusSource Source { get; private set; }

        public DimensionAuthoringReadinessState State { get; private set; }

        public DimensionAuthoringSeverity Severity { get; private set; }

        public string SectionId { get; private set; }

        public string BiomeId { get; private set; }

        public string ZoneId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public string ActionId { get; private set; }

        public string DisplayName { get; private set; }

        public string Message { get; private set; }

        public bool HasLocalBounds { get; private set; }

        public DimensionBounds LocalBounds { get; private set; }

        public bool CanJumpToBounds { get; private set; }

        public bool CanRunAction { get; private set; }

        public int Priority { get; private set; }
    }

    public static class DimensionTemplateCustomizerFocusUtility
    {
        public static DimensionTemplateCustomizerFocusState Resolve(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFocusRequest request)
        {
            if (workspace == null)
            {
                return CreateMissingFocus();
            }

            string sectionId = ResolveSectionId(workspace, request.SectionId);
            if (!string.IsNullOrEmpty(request.ActionId))
            {
                DimensionTemplateCustomizerFocusState actionFocus =
                    TryResolveAction(workspace, sectionId, request.ActionId);
                if (actionFocus.HasFocus)
                {
                    return actionFocus;
                }
            }

            if (HasRecordRequest(request))
            {
                DimensionTemplateCustomizerFocusState detailFocus =
                    TryResolveDetailRow(workspace, sectionId, request);
                if (detailFocus.HasFocus)
                {
                    return detailFocus;
                }

                DimensionTemplateCustomizerFocusState searchFocus =
                    TryResolveSearchResult(workspace, sectionId, request);
                if (searchFocus.HasFocus)
                {
                    return searchFocus;
                }
            }

            return ResolveSection(workspace, sectionId);
        }

        public static DimensionTemplateCustomizerFocusState FromSearchEntry(
            DimensionTemplateCustomizerSearchEntry entry)
        {
            return new DimensionTemplateCustomizerFocusState(
                true,
                entry.HasLocalBounds
                    ? DimensionTemplateCustomizerFocusKind.Bounds
                    : DimensionTemplateCustomizerFocusKind.Record,
                DimensionTemplateCustomizerFocusSource.SearchResult,
                ResolveState(entry.Severity),
                entry.Severity,
                entry.SectionId,
                entry.BiomeId,
                entry.ZoneId,
                entry.RecordKind,
                entry.RecordId,
                string.Empty,
                entry.DisplayName,
                entry.Message,
                entry.HasLocalBounds,
                entry.LocalBounds,
                entry.HasLocalBounds,
                false,
                entry.Priority);
        }

        private static DimensionTemplateCustomizerFocusState ResolveSection(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            DimensionTemplateCustomizerSectionItem section;
            if (!TryGetSection(workspace.Navigation.Sections, sectionId, out section))
            {
                return CreateMissingFocus();
            }

            return new DimensionTemplateCustomizerFocusState(
                true,
                DimensionTemplateCustomizerFocusKind.Section,
                DimensionTemplateCustomizerFocusSource.DefaultSection,
                section.State,
                ResolveSeverity(section),
                section.SectionId,
                string.Empty,
                string.Empty,
                "Section",
                section.SectionId,
                section.PrimaryActionId,
                section.DisplayName,
                section.Message,
                false,
                default(DimensionBounds),
                false,
                !string.IsNullOrEmpty(section.PrimaryActionId),
                section.Priority);
        }

        private static DimensionTemplateCustomizerFocusState TryResolveDetailRow(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId,
            DimensionTemplateCustomizerFocusRequest request)
        {
            DimensionTemplateCustomizerSectionDetail detail =
                workspace.GetSectionDetail(sectionId);
            IReadOnlyList<DimensionTemplateCustomizerDetailRow> rows = detail.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                DimensionTemplateCustomizerDetailRow row = rows[i];
                if (!MatchesRequest(row.BiomeId, row.RecordKind, row.RecordId, request))
                {
                    continue;
                }

                return new DimensionTemplateCustomizerFocusState(
                    true,
                    request.PreferBounds && row.HasLocalBounds
                        ? DimensionTemplateCustomizerFocusKind.Bounds
                        : DimensionTemplateCustomizerFocusKind.Record,
                    DimensionTemplateCustomizerFocusSource.DetailRow,
                    row.State,
                    row.Severity,
                    row.SectionId,
                    row.BiomeId,
                    string.Empty,
                    row.RecordKind,
                    row.RecordId,
                    row.PrimaryActionId,
                    row.Title,
                    row.Message,
                    row.HasLocalBounds,
                    row.LocalBounds,
                    row.HasLocalBounds,
                    !string.IsNullOrEmpty(row.PrimaryActionId),
                    row.Priority);
            }

            return CreateMissingFocus();
        }

        private static DimensionTemplateCustomizerFocusState TryResolveAction(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId,
            string actionId)
        {
            DimensionTemplateCustomizerActionCatalog catalog =
                workspace.GetActionCatalog(sectionId);
            IReadOnlyList<DimensionTemplateCustomizerActionDescriptor> actions =
                catalog.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                DimensionTemplateCustomizerActionDescriptor action = actions[i];
                if (action.ActionId != actionId)
                {
                    continue;
                }

                return new DimensionTemplateCustomizerFocusState(
                    true,
                    DimensionTemplateCustomizerFocusKind.Action,
                    DimensionTemplateCustomizerFocusSource.Action,
                    action.State,
                    action.Severity,
                    action.SectionId,
                    action.BiomeId,
                    string.Empty,
                    action.RecordKind,
                    action.RecordId,
                    action.ActionId,
                    action.Label,
                    action.Tooltip,
                    false,
                    default(DimensionBounds),
                    false,
                    action.Availability == DimensionTemplateCustomizerActionAvailability.Available,
                    action.Priority);
            }

            return CreateMissingFocus();
        }

        private static DimensionTemplateCustomizerFocusState TryResolveSearchResult(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId,
            DimensionTemplateCustomizerFocusRequest request)
        {
            IReadOnlyList<DimensionTemplateCustomizerSearchEntry> results =
                workspace.Search(
                    string.IsNullOrEmpty(request.RecordId) ? request.RecordKind : request.RecordId,
                    sectionId,
                    request.BiomeId,
                    128);
            for (int i = 0; i < results.Count; i++)
            {
                DimensionTemplateCustomizerSearchEntry entry = results[i];
                if (!MatchesRequest(entry.BiomeId, entry.RecordKind, entry.RecordId, request))
                {
                    continue;
                }

                DimensionTemplateCustomizerFocusState focus = FromSearchEntry(entry);
                if (!request.PreferBounds || focus.HasLocalBounds)
                {
                    return focus;
                }
            }

            return CreateMissingFocus();
        }

        private static bool HasRecordRequest(DimensionTemplateCustomizerFocusRequest request)
        {
            return !string.IsNullOrEmpty(request.BiomeId) ||
                !string.IsNullOrEmpty(request.RecordKind) ||
                !string.IsNullOrEmpty(request.RecordId);
        }

        private static bool MatchesRequest(
            string biomeId,
            string recordKind,
            string recordId,
            DimensionTemplateCustomizerFocusRequest request)
        {
            if (!string.IsNullOrEmpty(request.BiomeId) &&
                biomeId != request.BiomeId)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.RecordKind) &&
                recordKind != request.RecordKind)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.RecordId) &&
                recordId != request.RecordId)
            {
                return false;
            }

            return true;
        }

        private static string ResolveSectionId(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            if (!string.IsNullOrEmpty(sectionId))
            {
                return sectionId;
            }

            return workspace.Navigation.ActiveSectionId;
        }

        private static bool TryGetSection(
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections,
            string sectionId,
            out DimensionTemplateCustomizerSectionItem section)
        {
            section = default(DimensionTemplateCustomizerSectionItem);
            if (sections == null)
            {
                return false;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].SectionId == sectionId)
                {
                    section = sections[i];
                    return true;
                }
            }

            return false;
        }

        private static DimensionAuthoringSeverity ResolveSeverity(
            DimensionTemplateCustomizerSectionItem section)
        {
            if (section.ErrorCount > 0 || section.State == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionAuthoringSeverity.Error;
            }

            if (section.WarningCount > 0 ||
                section.State == DimensionAuthoringReadinessState.Partial ||
                section.State == DimensionAuthoringReadinessState.Missing)
            {
                return DimensionAuthoringSeverity.Warning;
            }

            return DimensionAuthoringSeverity.Info;
        }

        private static DimensionAuthoringReadinessState ResolveState(
            DimensionAuthoringSeverity severity)
        {
            if (severity == DimensionAuthoringSeverity.Error)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            return severity == DimensionAuthoringSeverity.Warning
                ? DimensionAuthoringReadinessState.Partial
                : DimensionAuthoringReadinessState.Ready;
        }

        private static DimensionTemplateCustomizerFocusState CreateMissingFocus()
        {
            return new DimensionTemplateCustomizerFocusState(
                false,
                DimensionTemplateCustomizerFocusKind.None,
                DimensionTemplateCustomizerFocusSource.None,
                DimensionAuthoringReadinessState.Missing,
                DimensionAuthoringSeverity.Info,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                default(DimensionBounds),
                false,
                false,
                0);
        }
    }
}
