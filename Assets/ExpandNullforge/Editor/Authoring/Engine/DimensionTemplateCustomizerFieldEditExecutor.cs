using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerFieldEditExecutionState
    {
        MissingWorkspace = 0,
        Empty = 1,
        Blocked = 2,
        Applied = 3,
        PartiallyApplied = 4
    }

    public sealed class DimensionTemplateCustomizerFieldEditExecutionEntry
    {
        public DimensionTemplateCustomizerFieldEditExecutionEntry(
            int index,
            string fieldId,
            string targetKind,
            string targetId,
            string oldValue,
            string newValue,
            bool applied,
            bool supported,
            string code,
            string message)
        {
            Index = index < 0 ? 0 : index;
            FieldId = fieldId ?? string.Empty;
            TargetKind = targetKind ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            OldValue = oldValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
            Applied = applied;
            Supported = supported;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int Index { get; private set; }

        public string FieldId { get; private set; }

        public string TargetKind { get; private set; }

        public string TargetId { get; private set; }

        public string OldValue { get; private set; }

        public string NewValue { get; private set; }

        public bool Applied { get; private set; }

        public bool Supported { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerFieldEditExecutionResult
    {
        public DimensionTemplateCustomizerFieldEditExecutionResult(
            DimensionTemplateCustomizerFieldEditExecutionState state,
            string code,
            string message,
            int requestedCount,
            int appliedCount,
            int blockedCount,
            int unsupportedCount,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchPreview preview,
            DimensionTemplateCustomizerFieldEditChangeSet changeSet,
            DimensionTemplateCustomizerFieldEditRollbackPlan rollbackPlan,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditExecutionEntry> entries)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RequestedCount = requestedCount < 0 ? 0 : requestedCount;
            AppliedCount = appliedCount < 0 ? 0 : appliedCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            UnsupportedCount = unsupportedCount < 0 ? 0 : unsupportedCount;
            Workspace = workspace;
            Preview = preview;
            ChangeSet = changeSet;
            RollbackPlan = rollbackPlan;
            Entries = entries ?? new List<DimensionTemplateCustomizerFieldEditExecutionEntry>();
        }

        public DimensionTemplateCustomizerFieldEditExecutionState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int RequestedCount { get; private set; }

        public int AppliedCount { get; private set; }

        public int BlockedCount { get; private set; }

        public int UnsupportedCount { get; private set; }

        public DimensionTemplateAuthoringWorkspace Workspace { get; private set; }

        public DimensionTemplateCustomizerFieldEditBatchPreview Preview { get; private set; }

        public DimensionTemplateCustomizerFieldEditChangeSet ChangeSet { get; private set; }

        public DimensionTemplateCustomizerFieldEditRollbackPlan RollbackPlan { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerFieldEditExecutionEntry> Entries { get; private set; }
    }

    public static class DimensionTemplateCustomizerFieldEditExecutor
    {
        public static DimensionTemplateCustomizerFieldEditExecutionResult Apply(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditRequest request)
        {
            List<DimensionTemplateCustomizerFieldEditRequest> edits =
                new List<DimensionTemplateCustomizerFieldEditRequest>();
            if (request != null)
            {
                edits.Add(request);
            }

            return ApplyBatch(
                workspace,
                new DimensionTemplateCustomizerFieldEditBatchRequest(edits),
                ResolveRootFolder(workspace));
        }

        public static DimensionTemplateCustomizerFieldEditExecutionResult ApplyBatch(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request)
        {
            return ApplyBatch(workspace, request, ResolveRootFolder(workspace));
        }

        public static DimensionTemplateCustomizerFieldEditExecutionResult ApplyBatch(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchRequest request,
            string suggestedRootFolder)
        {
            if (workspace == null || workspace.Graph == null)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditExecutionState.MissingWorkspace,
                    "workspace-missing",
                    "Select or create a Dimension Asset before applying field edits.",
                    0,
                    0,
                    0,
                    0,
                    workspace,
                    null,
                    null,
                    null,
                    new List<DimensionTemplateCustomizerFieldEditExecutionEntry>());
            }

            DimensionTemplateCustomizerFieldEditBatchPreview preview =
                DimensionTemplateCustomizerFieldEditBatchUtility.Preview(workspace, request);
            DimensionTemplateCustomizerFieldEditChangeSet changeSet =
                DimensionTemplateCustomizerFieldEditChangeSetUtility.Build(preview);
            DimensionTemplateCustomizerFieldEditBatchApplyPlan applyPlan =
                DimensionTemplateCustomizerFieldEditBatchApplyPlanUtility.Build(preview);
            DimensionTemplateCustomizerFieldEditRollbackPlan rollbackPlan =
                DimensionTemplateCustomizerFieldEditRollbackPlanUtility.Build(applyPlan);

            if (preview == null || preview.TotalCount == 0)
            {
                return CreateResult(
                    DimensionTemplateCustomizerFieldEditExecutionState.Empty,
                    "field-edit-execution-empty",
                    "No field edits were supplied.",
                    0,
                    0,
                    0,
                    0,
                    workspace,
                    preview,
                    changeSet,
                    rollbackPlan,
                    new List<DimensionTemplateCustomizerFieldEditExecutionEntry>());
            }

            DimensionTemplateStarterGraph graph = workspace.Graph;
            DimensionBounds playableLocalBounds = graph.PlayableLocalBounds;
            DimensionBounds reservedLocalBounds = graph.ReservedLocalBounds;
            List<DimensionTemplateCustomizerFieldEditExecutionEntry> entries =
                new List<DimensionTemplateCustomizerFieldEditExecutionEntry>();
            List<DimensionTemplateCustomizerFieldEditBatchEntry> sortedEntries =
                SortEntriesForStableApplication(preview.Entries);

            int appliedCount = 0;
            int blockedCount = 0;
            int unsupportedCount = 0;
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                DimensionTemplateCustomizerFieldEditBatchEntry entry = sortedEntries[i];
                ApplyEntryResult entryResult = ApplyEntry(
                    graph,
                    entry,
                    ref playableLocalBounds,
                    ref reservedLocalBounds);

                if (entryResult.Applied)
                {
                    appliedCount++;
                }
                else
                {
                    blockedCount++;
                }

                if (!entryResult.Supported)
                {
                    unsupportedCount++;
                }

                entries.Add(entryResult.ToExecutionEntry());
            }

            DimensionTemplateAuthoringWorkspace rebuiltWorkspace = workspace;
            if (appliedCount > 0)
            {
                DimensionTemplateStarterGraph rebuiltGraph = new DimensionTemplateStarterGraph(
                    graph.Dimension,
                    graph.Layout,
                    graph.Biome,
                    graph.GenerationPasses,
                    playableLocalBounds,
                    reservedLocalBounds);
                rebuiltWorkspace = DimensionTemplateAuthoringWorkspaceUtility.BuildWorkspace(
                    rebuiltGraph,
                    string.IsNullOrEmpty(suggestedRootFolder)
                        ? ResolveRootFolder(workspace)
                        : suggestedRootFolder);
            }

            DimensionTemplateCustomizerFieldEditExecutionState state =
                ResolveState(preview.TotalCount, appliedCount, blockedCount);
            return CreateResult(
                state,
                ResolveCode(state, unsupportedCount),
                ResolveMessage(state, appliedCount, blockedCount, unsupportedCount),
                preview.TotalCount,
                appliedCount,
                blockedCount,
                unsupportedCount,
                rebuiltWorkspace,
                preview,
                changeSet,
                rollbackPlan,
                entries);
        }

        private static ApplyEntryResult ApplyEntry(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateCustomizerFieldEditBatchEntry entry,
            ref DimensionBounds playableLocalBounds,
            ref DimensionBounds reservedLocalBounds)
        {
            if (entry == null || !entry.CanApply || entry.ApplyPlan == null)
            {
                return ApplyEntryResult.CreateBlocked(entry, "field-edit-blocked", entry == null
                    ? "The staged edit is missing."
                    : entry.Message);
            }

            DimensionTemplateCustomizerFieldEditPreview preview = entry.Preview;
            DimensionTemplateCustomizerFieldSet fieldSet = preview == null ? null : preview.FieldSet;
            string fieldId = entry.ApplyPlan.FieldId;
            if (fieldSet == null || string.IsNullOrEmpty(fieldId))
            {
                return ApplyEntryResult.CreateBlocked(entry, "field-target-missing", "The staged edit has no resolved field target.");
            }

            if (IsDimensionField(fieldSet))
            {
                return ApplyDimensionField(
                    graph,
                    entry,
                    preview,
                    ref playableLocalBounds,
                    ref reservedLocalBounds);
            }

            if (IsBiomeField(fieldSet))
            {
                return ApplyBiomeField(
                    graph,
                    entry,
                    preview,
                    playableLocalBounds);
            }

            return ApplyEntryResult.CreateUnsupported(
                entry,
                "field-target-unsupported",
                "This target is exposed for inspection, but source-side execution does not support writing it yet.");
        }

        private static ApplyEntryResult ApplyDimensionField(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateCustomizerFieldEditBatchEntry entry,
            DimensionTemplateCustomizerFieldEditPreview preview,
            ref DimensionBounds playableLocalBounds,
            ref DimensionBounds reservedLocalBounds)
        {
            DimensionTemplateAsset dimension = graph == null ? null : graph.Dimension;
            if (dimension == null)
            {
                return ApplyEntryResult.CreateBlocked(entry, "dimension-missing", "The Dimension Asset is missing.");
            }

            string fieldId = entry.ApplyPlan.FieldId;
            if (fieldId == "reserved-local-bounds")
            {
                if (!preview.HasParsedBounds)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "bounds-missing", "The reserved bounds preview did not include parsed bounds.");
                }

                reservedLocalBounds = preview.ParsedBounds;
                dimension.ApplyReservedLocalBounds(reservedLocalBounds);
                return ApplyEntryResult.CreateApplied(entry, "reserved-bounds-applied", "Reserved local bounds updated in memory.");
            }

            if (fieldId == "playable-local-bounds")
            {
                if (!preview.HasParsedBounds)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "bounds-missing", "The playable bounds preview did not include parsed bounds.");
                }

                playableLocalBounds = preview.ParsedBounds;
                return ApplyEntryResult.CreateApplied(entry, "playable-bounds-applied", "Playable local bounds updated in the rebuilt authoring workspace.");
            }

            string dimensionId = dimension.DimensionId;
            string displayName = dimension.DisplayName;
            string description = dimension.Description;
            string contentPackId = dimension.ContentPackId;
            string contentPackName = dimension.ContentPackDisplayName;
            string contentPackVersion = dimension.ContentPackVersion;
            string contentPackAuthor = dimension.ContentPackAuthor;
            int minimumApiVersion = dimension.MinimumApiVersion;

            if (fieldId == "dimension-id")
            {
                dimensionId = preview.NormalizedValue;
                dimension.name = preview.NormalizedValue;
            }
            else if (fieldId == "display-name")
            {
                displayName = preview.NormalizedValue;
            }
            else if (fieldId == "description")
            {
                description = preview.NormalizedValue;
            }
            else if (fieldId == "content-pack-id")
            {
                contentPackId = preview.NormalizedValue;
            }
            else if (fieldId == "content-pack-name")
            {
                contentPackName = preview.NormalizedValue;
            }
            else if (fieldId == "content-pack-version")
            {
                contentPackVersion = preview.NormalizedValue;
            }
            else if (fieldId == "content-pack-author")
            {
                contentPackAuthor = preview.NormalizedValue;
            }
            else if (fieldId == "minimum-api-version")
            {
                if (!preview.HasParsedInteger)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "integer-missing", "The minimum API version preview did not include a parsed integer.");
                }

                minimumApiVersion = preview.ParsedInteger;
            }
            else
            {
                return ApplyEntryResult.CreateUnsupported(
                    entry,
                    "dimension-field-unsupported",
                    "This dimension field is not writable by the source-side executor yet.");
            }

            dimension.ApplyCustomizerMetadata(
                dimensionId,
                displayName,
                description,
                contentPackId,
                contentPackName,
                contentPackVersion,
                contentPackAuthor,
                minimumApiVersion);
            return ApplyEntryResult.CreateApplied(entry, "dimension-field-applied", "Dimension field updated in memory.");
        }

        private static ApplyEntryResult ApplyBiomeField(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateCustomizerFieldEditBatchEntry entry,
            DimensionTemplateCustomizerFieldEditPreview preview,
            DimensionBounds playableLocalBounds)
        {
            DimensionTemplateCustomizerFieldSet fieldSet = preview.FieldSet;
            BiomeTemplateAsset biome = ResolveBiome(graph, fieldSet);
            if (biome == null)
            {
                return ApplyEntryResult.CreateBlocked(entry, "biome-missing", "The target biome template asset is missing.");
            }

            string fieldId = entry.ApplyPlan.FieldId;
            if (fieldId == "fallback-bounds-enabled")
            {
                if (!preview.HasParsedBoolean)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "boolean-missing", "The fallback bounds toggle preview did not include a parsed boolean.");
                }

                if (preview.ParsedBoolean)
                {
                    DimensionBounds fallbackBounds = biome.HasFallbackLocalBounds
                        ? biome.FallbackLocalBounds
                        : playableLocalBounds;
                    biome.ApplyFallbackLocalBounds(
                        new Vector2Int(fallbackBounds.Min.x, fallbackBounds.Min.y),
                        new Vector2Int(fallbackBounds.MaxExclusive.x, fallbackBounds.MaxExclusive.y));
                }
                else
                {
                    biome.ClearFallbackLocalBounds();
                }

                return ApplyEntryResult.CreateApplied(entry, "biome-fallback-toggle-applied", "Biome fallback bounds toggle updated in memory.");
            }

            if (fieldId == "fallback-local-bounds")
            {
                if (!preview.HasParsedBounds)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "bounds-missing", "The fallback bounds preview did not include parsed bounds.");
                }

                DimensionBounds bounds = preview.ParsedBounds;
                biome.ApplyFallbackLocalBounds(
                    new Vector2Int(bounds.Min.x, bounds.Min.y),
                    new Vector2Int(bounds.MaxExclusive.x, bounds.MaxExclusive.y));
                return ApplyEntryResult.CreateApplied(entry, "biome-fallback-bounds-applied", "Biome fallback local bounds updated in memory.");
            }

            string biomeId = biome.BiomeId;
            string displayName = biome.DisplayName;
            int priority = biome.Priority;
            bool enabled = biome.Enabled;
            string environmentProfileId = biome.EnvironmentProfileId;
            string paletteId = biome.PaletteAssetId;
            string notes = biome.Notes;

            if (fieldId == "biome-id")
            {
                biomeId = preview.NormalizedValue;
                biome.name = preview.NormalizedValue;
            }
            else if (fieldId == "display-name")
            {
                displayName = preview.NormalizedValue;
            }
            else if (fieldId == "enabled")
            {
                if (!preview.HasParsedBoolean)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "boolean-missing", "The enabled preview did not include a parsed boolean.");
                }

                enabled = preview.ParsedBoolean;
            }
            else if (fieldId == "priority")
            {
                if (!preview.HasParsedInteger)
                {
                    return ApplyEntryResult.CreateBlocked(entry, "integer-missing", "The priority preview did not include a parsed integer.");
                }

                priority = preview.ParsedInteger;
            }
            else if (fieldId == "environment-profile-id")
            {
                environmentProfileId = preview.NormalizedValue;
            }
            else if (fieldId == "palette-id")
            {
                paletteId = preview.NormalizedValue;
            }
            else if (fieldId == "notes")
            {
                notes = preview.NormalizedValue;
            }
            else
            {
                return ApplyEntryResult.CreateUnsupported(
                    entry,
                    "biome-field-unsupported",
                    "This biome field is not writable by the source-side executor yet.");
            }

            biome.ApplyCustomizerMetadata(
                biomeId,
                displayName,
                priority,
                enabled,
                environmentProfileId,
                paletteId,
                notes);
            return ApplyEntryResult.CreateApplied(entry, "biome-field-applied", "Biome field updated in memory.");
        }

        private static List<DimensionTemplateCustomizerFieldEditBatchEntry> SortEntriesForStableApplication(
            IReadOnlyList<DimensionTemplateCustomizerFieldEditBatchEntry> entries)
        {
            List<DimensionTemplateCustomizerFieldEditBatchEntry> sorted =
                new List<DimensionTemplateCustomizerFieldEditBatchEntry>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null)
                    {
                        sorted.Add(entries[i]);
                    }
                }
            }

            sorted.Sort(CompareApplyOrder);
            return sorted;
        }

        private static int CompareApplyOrder(
            DimensionTemplateCustomizerFieldEditBatchEntry left,
            DimensionTemplateCustomizerFieldEditBatchEntry right)
        {
            int leftPriority = GetApplyPriority(left);
            int rightPriority = GetApplyPriority(right);
            if (leftPriority != rightPriority)
            {
                return leftPriority.CompareTo(rightPriority);
            }

            int leftIndex = left == null ? 0 : left.Index;
            int rightIndex = right == null ? 0 : right.Index;
            return leftIndex.CompareTo(rightIndex);
        }

        private static int GetApplyPriority(DimensionTemplateCustomizerFieldEditBatchEntry entry)
        {
            string fieldId = entry == null || entry.ApplyPlan == null
                ? string.Empty
                : entry.ApplyPlan.FieldId;
            if (fieldId == "biome-id" || fieldId == "dimension-id")
            {
                return 100;
            }

            return 0;
        }

        private static bool IsDimensionField(DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return fieldSet != null &&
                (fieldSet.RecordKind == "Dimension" ||
                 fieldSet.SectionId == "overview" ||
                 fieldSet.SectionId == "export");
        }

        private static bool IsBiomeField(DimensionTemplateCustomizerFieldSet fieldSet)
        {
            return fieldSet != null &&
                (fieldSet.RecordKind == "Biome" ||
                 fieldSet.SectionId == "biomes" ||
                 fieldSet.SectionId == "terrain" ||
                 !string.IsNullOrEmpty(fieldSet.BiomeId));
        }

        private static BiomeTemplateAsset ResolveBiome(
            DimensionTemplateStarterGraph graph,
            DimensionTemplateCustomizerFieldSet fieldSet)
        {
            if (graph == null || fieldSet == null)
            {
                return null;
            }

            string biomeId = !string.IsNullOrEmpty(fieldSet.BiomeId)
                ? fieldSet.BiomeId
                : fieldSet.RecordKind == "Biome"
                    ? fieldSet.RecordId
                    : string.Empty;

            if (!string.IsNullOrEmpty(biomeId) && graph.Dimension != null)
            {
                BiomeTemplateAsset[] biomes = graph.Dimension.Biomes;
                for (int i = 0; i < biomes.Length; i++)
                {
                    BiomeTemplateAsset candidate = biomes[i];
                    if (candidate != null && candidate.BiomeId == biomeId)
                    {
                        return candidate;
                    }
                }
            }

            return graph.Biome;
        }

        private static DimensionTemplateCustomizerFieldEditExecutionState ResolveState(
            int requestedCount,
            int appliedCount,
            int blockedCount)
        {
            if (requestedCount <= 0)
            {
                return DimensionTemplateCustomizerFieldEditExecutionState.Empty;
            }

            if (appliedCount <= 0)
            {
                return DimensionTemplateCustomizerFieldEditExecutionState.Blocked;
            }

            return blockedCount > 0
                ? DimensionTemplateCustomizerFieldEditExecutionState.PartiallyApplied
                : DimensionTemplateCustomizerFieldEditExecutionState.Applied;
        }

        private static string ResolveCode(
            DimensionTemplateCustomizerFieldEditExecutionState state,
            int unsupportedCount)
        {
            if (unsupportedCount > 0 && state == DimensionTemplateCustomizerFieldEditExecutionState.PartiallyApplied)
            {
                return "field-edit-partially-applied-unsupported";
            }

            if (state == DimensionTemplateCustomizerFieldEditExecutionState.Applied)
            {
                return "field-edit-applied";
            }

            if (state == DimensionTemplateCustomizerFieldEditExecutionState.PartiallyApplied)
            {
                return "field-edit-partially-applied";
            }

            if (state == DimensionTemplateCustomizerFieldEditExecutionState.Empty)
            {
                return "field-edit-empty";
            }

            return "field-edit-blocked";
        }

        private static string ResolveMessage(
            DimensionTemplateCustomizerFieldEditExecutionState state,
            int appliedCount,
            int blockedCount,
            int unsupportedCount)
        {
            if (state == DimensionTemplateCustomizerFieldEditExecutionState.Applied)
            {
                return "All supported field edits were applied in memory and the authoring workspace was rebuilt.";
            }

            if (state == DimensionTemplateCustomizerFieldEditExecutionState.PartiallyApplied)
            {
                return appliedCount
                    + " field edits were applied in memory; "
                    + blockedCount
                    + " were blocked"
                    + (unsupportedCount > 0 ? " and " + unsupportedCount + " are unsupported by the source-side executor." : ".");
            }

            if (state == DimensionTemplateCustomizerFieldEditExecutionState.Empty)
            {
                return "No field edits were supplied.";
            }

            return "No field edits were applied.";
        }

        private static string ResolveRootFolder(DimensionTemplateAuthoringWorkspace workspace)
        {
            return workspace == null || workspace.SavePlan == null
                ? string.Empty
                : workspace.SavePlan.RootFolder;
        }

        private static DimensionTemplateCustomizerFieldEditExecutionResult CreateResult(
            DimensionTemplateCustomizerFieldEditExecutionState state,
            string code,
            string message,
            int requestedCount,
            int appliedCount,
            int blockedCount,
            int unsupportedCount,
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateCustomizerFieldEditBatchPreview preview,
            DimensionTemplateCustomizerFieldEditChangeSet changeSet,
            DimensionTemplateCustomizerFieldEditRollbackPlan rollbackPlan,
            IReadOnlyList<DimensionTemplateCustomizerFieldEditExecutionEntry> entries)
        {
            return new DimensionTemplateCustomizerFieldEditExecutionResult(
                state,
                code,
                message,
                requestedCount,
                appliedCount,
                blockedCount,
                unsupportedCount,
                workspace,
                preview,
                changeSet,
                rollbackPlan,
                entries);
        }

        private sealed class ApplyEntryResult
        {
            private ApplyEntryResult(
                DimensionTemplateCustomizerFieldEditBatchEntry source,
                bool applied,
                bool supported,
                string code,
                string message)
            {
                Source = source;
                Applied = applied;
                Supported = supported;
                Code = code ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public DimensionTemplateCustomizerFieldEditBatchEntry Source { get; private set; }

            public bool Applied { get; private set; }

            public bool Supported { get; private set; }

            public string Code { get; private set; }

            public string Message { get; private set; }

            public static ApplyEntryResult CreateApplied(
                DimensionTemplateCustomizerFieldEditBatchEntry source,
                string code,
                string message)
            {
                return new ApplyEntryResult(source, true, true, code, message);
            }

            public static ApplyEntryResult CreateBlocked(
                DimensionTemplateCustomizerFieldEditBatchEntry source,
                string code,
                string message)
            {
                return new ApplyEntryResult(source, false, true, code, message);
            }

            public static ApplyEntryResult CreateUnsupported(
                DimensionTemplateCustomizerFieldEditBatchEntry source,
                string code,
                string message)
            {
                return new ApplyEntryResult(source, false, false, code, message);
            }

            public DimensionTemplateCustomizerFieldEditExecutionEntry ToExecutionEntry()
            {
                DimensionTemplateCustomizerFieldApplyPlan applyPlan =
                    Source == null ? null : Source.ApplyPlan;
                DimensionTemplateCustomizerFieldSet fieldSet =
                    Source == null || Source.Preview == null ? null : Source.Preview.FieldSet;
                return new DimensionTemplateCustomizerFieldEditExecutionEntry(
                    Source == null ? 0 : Source.Index,
                    applyPlan == null ? string.Empty : applyPlan.FieldId,
                    fieldSet == null ? string.Empty : fieldSet.RecordKind,
                    fieldSet == null ? string.Empty : fieldSet.RecordId,
                    applyPlan == null ? string.Empty : applyPlan.OldValue,
                    applyPlan == null ? string.Empty : applyPlan.NewValue,
                    Applied,
                    Supported,
                    Code,
                    Message);
            }
        }
    }
}
