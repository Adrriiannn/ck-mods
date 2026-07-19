using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldEditChangeSetState
    {
        MissingPreview = 0,
        Empty = 1,
        Blocked = 2,
        NeedsWarningConfirmation = 3,
        Ready = 4
    }

    public sealed class DimensionTemplateCustomizerFieldEditChangeSetTarget
    {
        public DimensionTemplateCustomizerFieldEditChangeSetTarget(
            string targetKey,
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            string displayName,
            int fieldCount,
            bool hasBlockedEdits,
            bool requiresWarningConfirmation,
            IReadOnlyList<string> fieldIds)
        {
            TargetKey = targetKey ?? string.Empty;
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FieldCount = fieldCount < 0 ? 0 : fieldCount;
            HasBlockedEdits = hasBlockedEdits;
            RequiresWarningConfirmation = requiresWarningConfirmation;
            FieldIds = fieldIds ?? new List<string>();
        }

        public string TargetKey { get; private set; }

        public string SectionId { get; private set; }

        public string BiomeId { get; private set; }

        public string RecordKind { get; private set; }

        public string RecordId { get; private set; }

        public string DisplayName { get; private set; }

        public int FieldCount { get; private set; }

        public bool HasBlockedEdits { get; private set; }

        public bool RequiresWarningConfirmation { get; private set; }

        public IReadOnlyList<string> FieldIds { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditChangeSet
    {
        public DimensionTemplateCustomizerFieldEditChangeSet(
            DimensionTemplateCustomizerFieldEditChangeSetState state,
            string code,
            string message,
            bool canApply,
            int targetCount,
            int fieldCount,
            int warningFieldCount,
            int blockedFieldCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditChangeSetTarget> targets,
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanApply = canApply;
            TargetCount = targetCount < 0 ? 0 : targetCount;
            FieldCount = fieldCount < 0 ? 0 : fieldCount;
            WarningFieldCount = warningFieldCount < 0 ? 0 : warningFieldCount;
            BlockedFieldCount = blockedFieldCount < 0 ? 0 : blockedFieldCount;
            Targets = targets ?? new List<DimensionTemplateCustomizerFieldEditChangeSetTarget>();
            Preview = preview;
        }

        public DimensionTemplateCustomizerFieldEditChangeSetState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public bool CanApply { get; private set; }

        public int TargetCount { get; private set; }

        public int FieldCount { get; private set; }

        public int WarningFieldCount { get; private set; }

        public int BlockedFieldCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditChangeSetTarget> Targets { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchPreview Preview { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldEditChangeSetUtility
    {
        public static DimensionTemplateCustomizerFieldEditChangeSet Build(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return Build(DimensionTemplateCustomizerFieldEditBatchUtility.Preview(
                workspace,
                request));
        }

        public static DimensionTemplateCustomizerFieldEditChangeSet Build(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (preview == null)
            {
                return CreateChangeSet(
                    DimensionTemplateCustomizerFieldEditChangeSetState.MissingPreview,
                    "field-edit-change-set-preview-missing",
                    "No staged field edit batch preview was supplied.",
                    false,
                    0,
                    0,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditChangeSetTarget>(),
                    null);
            }

            if (preview.Entries == null || preview.Entries.Count == 0)
            {
                return CreateChangeSet(
                    DimensionTemplateCustomizerFieldEditChangeSetState.Empty,
                    preview.Code,
                    preview.Message,
                    false,
                    0,
                    0,
                    0,
                    new List<DimensionTemplateCustomizerFieldEditChangeSetTarget>(),
                    preview);
            }

            Dictionary<string, TargetBuilder> builders =
                new Dictionary<string, TargetBuilder>();
            List<string> targetOrder = new List<string>();
            int warningFieldCount = 0;
            int blockedFieldCount = 0;

            for (int i = 0; i < preview.Entries.Count; i++)
            {
                DimensionTemplateCustomizerFieldEditBatchEntry entry = preview.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                string targetKey = MakeTargetKey(entry);
                TargetBuilder builder;
                if (!builders.TryGetValue(targetKey, out builder))
                {
                    builder = CreateTargetBuilder(targetKey, entry);
                    builders.Add(targetKey, builder);
                    targetOrder.Add(targetKey);
                }

                string fieldId = ResolveFieldId(entry);
                if (!string.IsNullOrEmpty(fieldId))
                {
                    builder.AddField(fieldId);
                }

                if (entry.HasWarning)
                {
                    warningFieldCount++;
                    builder.RequiresWarningConfirmation = true;
                }

                if (!entry.CanApply)
                {
                    blockedFieldCount++;
                    builder.HasBlockedEdits = true;
                }
            }

            List<DimensionTemplateCustomizerFieldEditChangeSetTarget> targets =
                new List<DimensionTemplateCustomizerFieldEditChangeSetTarget>();
            for (int i = 0; i < targetOrder.Count; i++)
            {
                TargetBuilder builder = builders[targetOrder[i]];
                targets.Add(builder.ToTarget());
            }

            return CreateChangeSet(
                ResolveState(preview),
                preview.Code,
                preview.Message,
                preview.CanApply,
                preview.TotalCount,
                warningFieldCount,
                blockedFieldCount,
                targets,
                preview);
        }

        private static DimensionTemplateCustomizerFieldEditChangeSetState ResolveState(
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            if (preview == null)
            {
                return DimensionTemplateCustomizerFieldEditChangeSetState.MissingPreview;
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.Empty)
            {
                return DimensionTemplateCustomizerFieldEditChangeSetState.Empty;
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.Blocked)
            {
                return DimensionTemplateCustomizerFieldEditChangeSetState.Blocked;
            }

            if (preview.State == DimensionTemplateCustomizerFieldEditBatchState.NeedsWarningConfirmation)
            {
                return DimensionTemplateCustomizerFieldEditChangeSetState.NeedsWarningConfirmation;
            }

            return DimensionTemplateCustomizerFieldEditChangeSetState.Ready;
        }

        private static string MakeTargetKey(
            DimensionTemplateCustomizerFieldEditBatchEntry entry)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                entry == null || entry.Preview == null ? null : entry.Preview.FieldSet;
            if (fieldSet != null)
            {
                return (fieldSet.SectionId ?? string.Empty)
                    + "|"
                    + (fieldSet.BiomeId ?? string.Empty)
                    + "|"
                    + (fieldSet.RecordKind ?? string.Empty)
                    + "|"
                    + (fieldSet.RecordId ?? string.Empty);
            }

            return entry == null ? string.Empty : entry.TargetKey;
        }

        private static TargetBuilder CreateTargetBuilder(
            string targetKey,
            DimensionTemplateCustomizerFieldEditBatchEntry entry)
        {
            DimensionTemplateCustomizerFieldSet fieldSet =
                entry == null || entry.Preview == null ? null : entry.Preview.FieldSet;
            if (fieldSet == null)
            {
                return new TargetBuilder(
                    targetKey,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    targetKey);
            }

            return new TargetBuilder(
                targetKey,
                fieldSet.SectionId,
                fieldSet.BiomeId,
                fieldSet.RecordKind,
                fieldSet.RecordId,
                fieldSet.DisplayName);
        }

        private static string ResolveFieldId(
            DimensionTemplateCustomizerFieldEditBatchEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (entry.Preview != null && entry.Preview.Field != null)
            {
                return entry.Preview.Field.FieldId;
            }

            return entry.Request == null ? string.Empty : entry.Request.FieldId;
        }

        private static DimensionTemplateCustomizerFieldEditChangeSet CreateChangeSet(
            DimensionTemplateCustomizerFieldEditChangeSetState state,
            string code,
            string message,
            bool canApply,
            int fieldCount,
            int warningFieldCount,
            int blockedFieldCount,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditChangeSetTarget> targets,
            DimensionTemplateCustomizerFieldEditBatchPreview preview)
        {
            return new DimensionTemplateCustomizerFieldEditChangeSet(
                state,
                code,
                message,
                canApply,
                targets == null ? 0 : targets.Count,
                fieldCount,
                warningFieldCount,
                blockedFieldCount,
                targets,
                preview);
        }

        private sealed class TargetBuilder
        {
            private readonly List<string> fieldIds = new List<string>();

            public TargetBuilder(
                string targetKey,
                string sectionId,
                string biomeId,
                string recordKind,
                string recordId,
                string displayName)
            {
                TargetKey = targetKey ?? string.Empty;
                SectionId = sectionId ?? string.Empty;
                BiomeId = biomeId ?? string.Empty;
                RecordKind = recordKind ?? string.Empty;
                RecordId = recordId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public string TargetKey { get; private set; }

            public string SectionId { get; private set; }

            public string BiomeId { get; private set; }

            public string RecordKind { get; private set; }

            public string RecordId { get; private set; }

            public string DisplayName { get; private set; }

            public bool HasBlockedEdits { get; set; }

            public bool RequiresWarningConfirmation { get; set; }

            public void AddField(string fieldId)
            {
                if (string.IsNullOrEmpty(fieldId) || fieldIds.Contains(fieldId))
                {
                    return;
                }

                fieldIds.Add(fieldId);
            }

            public DimensionTemplateCustomizerFieldEditChangeSetTarget ToTarget()
            {
                return new DimensionTemplateCustomizerFieldEditChangeSetTarget(
                    TargetKey,
                    SectionId,
                    BiomeId,
                    RecordKind,
                    RecordId,
                    DisplayName,
                    fieldIds.Count,
                    HasBlockedEdits,
                    RequiresWarningConfirmation,
                    fieldIds);
            }
        }
    }
}
