using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerDetailRowKind
    {
        Content = 0,
        Operation = 1,
        Guidance = 2,
        Issue = 3,
        Note = 4
    }

    public readonly struct DimensionTemplateCustomizerDetailRow
    {
        public readonly DimensionTemplateCustomizerDetailRowKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly DimensionAuthoringSeverity Severity;
        public readonly string SectionId;
        public readonly string BiomeId;
        public readonly string RecordKind;
        public readonly string RecordId;
        public readonly string Title;
        public readonly string Message;
        public readonly string PrimaryActionId;
        public readonly int Count;
        public readonly int Priority;
        public readonly bool HasLocalBounds;
        public readonly DimensionBounds LocalBounds;

        public DimensionTemplateCustomizerDetailRow(
            DimensionTemplateCustomizerDetailRowKind kind,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string sectionId,
            string biomeId,
            string recordKind,
            string recordId,
            string title,
            string message,
            string primaryActionId,
            int count,
            int priority,
            bool hasLocalBounds,
            DimensionBounds localBounds)
        {
            Kind = kind;
            State = state;
            Severity = severity;
            SectionId = sectionId ?? string.Empty;
            BiomeId = biomeId ?? string.Empty;
            RecordKind = recordKind ?? string.Empty;
            RecordId = recordId ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            Count = count < 0 ? 0 : count;
            Priority = priority < 0 ? 0 : priority;
            HasLocalBounds = hasLocalBounds;
            LocalBounds = localBounds;
        }
    }

    public sealed class DimensionTemplateCustomizerSectionDetail
    {
        public DimensionTemplateCustomizerSectionDetail(
            DimensionTemplateCustomizerSectionItem section,
            int rowCount,
            int operationRowCount,
            int guidanceRowCount,
            int issueRowCount,
            int contentRowCount,
            IReadOnlyList<DimensionTemplateCustomizerDetailRow> rows)
        {
            Section = section;
            RowCount = rowCount < 0 ? 0 : rowCount;
            OperationRowCount = operationRowCount < 0 ? 0 : operationRowCount;
            GuidanceRowCount = guidanceRowCount < 0 ? 0 : guidanceRowCount;
            IssueRowCount = issueRowCount < 0 ? 0 : issueRowCount;
            ContentRowCount = contentRowCount < 0 ? 0 : contentRowCount;
            Rows = rows ?? new List<DimensionTemplateCustomizerDetailRow>();
        }

        public DimensionTemplateCustomizerSectionItem Section { get; private set; }

        public int RowCount { get; private set; }

        public int OperationRowCount { get; private set; }

        public int GuidanceRowCount { get; private set; }

        public int IssueRowCount { get; private set; }

        public int ContentRowCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerDetailRow> Rows { get; private set; }
    }

    public static class DimensionTemplateCustomizerSectionDetailUtility
    {
        public static DimensionTemplateCustomizerSectionDetail Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            if (workspace == null)
            {
                return CreateMissingDetail();
            }

            DimensionTemplateCustomizerSectionItem section;
            if (!TryGetSection(workspace.Navigation.Sections, sectionId, out section))
            {
                if (!TryGetSection(workspace.Navigation.Sections, workspace.Navigation.ActiveSectionId, out section))
                {
                    return CreateMissingDetail();
                }
            }

            List<DimensionTemplateCustomizerDetailRow> rows =
                new List<DimensionTemplateCustomizerDetailRow>();
            AddContentRows(rows, workspace.Preview, section.SectionId, section.Kind);
            AddOperationRows(rows, workspace.OperationPlan.Operations, section.SectionId);
            AddGuidanceRows(rows, workspace.BiomeOverviews, section.SectionId);
            AddBudgetGuidanceRows(rows, workspace.GenerationBudgetGuidance, section.SectionId);
            AddIssueRows(rows, workspace.Preview.Issues, section.SectionId);
            AddNoteRows(rows, workspace.SessionReport, section.SectionId);
            rows.Sort(CompareRows);

            int operationRows;
            int guidanceRows;
            int issueRows;
            int contentRows;
            CountRows(rows, out operationRows, out guidanceRows, out issueRows, out contentRows);

            return new DimensionTemplateCustomizerSectionDetail(
                section,
                rows.Count,
                operationRows,
                guidanceRows,
                issueRows,
                contentRows,
                rows);
        }

        public static IReadOnlyList<DimensionTemplateCustomizerSectionDetail> BuildAll(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            List<DimensionTemplateCustomizerSectionDetail> details =
                new List<DimensionTemplateCustomizerSectionDetail>();
            if (workspace == null || workspace.Navigation.Sections == null)
            {
                details.Add(CreateMissingDetail());
                return details;
            }

            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections =
                workspace.Navigation.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                details.Add(Build(workspace, sections[i].SectionId));
            }

            return details;
        }

        private static DimensionTemplateCustomizerSectionDetail CreateMissingDetail()
        {
            DimensionTemplateCustomizerSectionItem section =
                new DimensionTemplateCustomizerSectionItem(
                    DimensionTemplateCustomizerSectionKind.Overview,
                    DimensionAuthoringReadinessState.Blocked,
                    "overview",
                    "Overview",
                    "No customizer section is available.",
                    "select-dimension-template",
                    0,
                    1,
                    1,
                    0,
                    1,
                    0,
                    true);

            List<DimensionTemplateCustomizerDetailRow> rows =
                new List<DimensionTemplateCustomizerDetailRow>
                {
                    new DimensionTemplateCustomizerDetailRow(
                        DimensionTemplateCustomizerDetailRowKind.Note,
                        DimensionAuthoringReadinessState.Blocked,
                        DimensionAuthoringSeverity.Error,
                        "overview",
                        string.Empty,
                        "workspace",
                        string.Empty,
                        "Workspace missing",
                        "Select or create a Dimension Asset before opening the customizer.",
                        "select-dimension-template",
                        1,
                        0,
                        false,
                        default(DimensionBounds))
                };

            return new DimensionTemplateCustomizerSectionDetail(
                section,
                rows.Count,
                0,
                0,
                1,
                0,
                rows);
        }

        private static bool TryGetSection(
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections,
            string sectionId,
            out DimensionTemplateCustomizerSectionItem section)
        {
            if (sections != null && !string.IsNullOrEmpty(sectionId))
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i].SectionId == sectionId)
                    {
                        section = sections[i];
                        return true;
                    }
                }
            }

            section = default(DimensionTemplateCustomizerSectionItem);
            return false;
        }

        private static void AddContentRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            DimensionAuthoringPreviewSummary preview,
            string sectionId,
            DimensionTemplateCustomizerSectionKind sectionKind)
        {
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries =
                preview.ContentEntries;
            if (rows == null || entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (!ContentBelongsToSection(entry.Kind, sectionKind))
                {
                    continue;
                }

                rows.Add(new DimensionTemplateCustomizerDetailRow(
                    DimensionTemplateCustomizerDetailRowKind.Content,
                    DimensionAuthoringReadinessState.Ready,
                    DimensionAuthoringSeverity.Info,
                    sectionId,
                    entry.BiomeId,
                    entry.Kind.ToString(),
                    entry.RecordId,
                    ResolveTitle(entry.DisplayName, entry.RecordId, entry.Kind.ToString()),
                    entry.Notes,
                    "inspect-content",
                    entry.Count < 1 ? 1 : entry.Count,
                    200 + i,
                    false,
                    default(DimensionBounds)));
            }
        }

        private static void AddOperationRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            IReadOnlyList<DimensionAuthoringOperationItem> operations,
            string sectionId)
        {
            if (rows == null || operations == null)
            {
                return;
            }

            for (int i = 0; i < operations.Count; i++)
            {
                DimensionAuthoringOperationItem operation = operations[i];
                if (ResolveSectionForOperation(operation) != sectionId)
                {
                    continue;
                }

                rows.Add(new DimensionTemplateCustomizerDetailRow(
                    DimensionTemplateCustomizerDetailRowKind.Operation,
                    operation.State,
                    operation.Severity,
                    sectionId,
                    operation.BiomeId,
                    operation.RecordKind,
                    operation.RecordId,
                    operation.Title,
                    operation.Message,
                    operation.PrimaryActionId,
                    1,
                    operation.Priority,
                    operation.HasLocalBounds,
                    operation.LocalBounds));
            }
        }

        private static void AddGuidanceRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            IReadOnlyList<DimensionBiomeAuthoringOverview> biomeOverviews,
            string sectionId)
        {
            if (rows == null || biomeOverviews == null)
            {
                return;
            }

            for (int i = 0; i < biomeOverviews.Count; i++)
            {
                IReadOnlyList<DimensionBiomeCustomizerGuidanceItem> guidanceItems =
                    biomeOverviews[i].GuidanceItems;
                if (guidanceItems == null)
                {
                    continue;
                }

                for (int j = 0; j < guidanceItems.Count; j++)
                {
                    DimensionBiomeCustomizerGuidanceItem guidance = guidanceItems[j];
                    if (ResolveSectionForCapability(guidance.CapabilityId) != sectionId)
                    {
                        continue;
                    }

                    rows.Add(new DimensionTemplateCustomizerDetailRow(
                        DimensionTemplateCustomizerDetailRowKind.Guidance,
                        guidance.State,
                        ResolveSeverity(guidance.State),
                        sectionId,
                        guidance.BiomeId,
                        "Capability",
                        guidance.CapabilityId,
                        guidance.Title,
                        guidance.Message,
                        guidance.PrimaryActionId,
                        1,
                        guidance.Priority,
                        false,
                        default(DimensionBounds)));
                }
            }
        }

        private static void AddIssueRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            IReadOnlyList<DimensionAuthoringIssue> issues,
            string sectionId)
        {
            if (rows == null || issues == null)
            {
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                DimensionAuthoringIssue issue = issues[i];
                if (ResolveSectionForRecordKind(issue.RecordKind) != sectionId)
                {
                    continue;
                }

                rows.Add(new DimensionTemplateCustomizerDetailRow(
                    DimensionTemplateCustomizerDetailRowKind.Issue,
                    issue.Severity == DimensionAuthoringSeverity.Error
                        ? DimensionAuthoringReadinessState.Blocked
                        : DimensionAuthoringReadinessState.Partial,
                    issue.Severity,
                    sectionId,
                    string.Empty,
                    issue.RecordKind,
                    issue.RecordId,
                    issue.Code,
                    issue.Message,
                    "review-issue",
                    1,
                    issue.Severity == DimensionAuthoringSeverity.Error ? 0 : 50,
                    issue.HasLocalBounds,
                    issue.LocalBounds));
            }
        }

        private static void AddBudgetGuidanceRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            DimensionGenerationBudgetGuidanceCatalog catalog,
            string sectionId)
        {
            if (rows == null || catalog == null || catalog.Items == null)
            {
                return;
            }

            IReadOnlyList<DimensionGenerationBudgetGuidanceItem> items = catalog.Items;
            for (int i = 0; i < items.Count; i++)
            {
                DimensionGenerationBudgetGuidanceItem item = items[i];
                if (ResolveSectionForBudgetGuidance(item) != sectionId)
                {
                    continue;
                }

                rows.Add(new DimensionTemplateCustomizerDetailRow(
                    DimensionTemplateCustomizerDetailRowKind.Guidance,
                    item.State,
                    item.Severity,
                    sectionId,
                    item.BiomeId,
                    "GenerationBudget",
                    item.Kind.ToString(),
                    item.Title,
                    item.Message,
                    item.PrimaryActionId,
                    item.Count < 1 ? 1 : item.Count,
                    item.Priority,
                    false,
                    default(DimensionBounds)));
            }
        }

        private static void AddNoteRows(
            List<DimensionTemplateCustomizerDetailRow> rows,
            DimensionTemplateCustomizerSessionReport session,
            string sectionId)
        {
            if (rows == null || session == null)
            {
                return;
            }

            IReadOnlyList<string> notes = sectionId == "export"
                ? session.ManifestExportPreview.Notes
                : sectionId == "diagnostics"
                    ? session.UiNotes
                    : null;
            if (notes == null)
            {
                return;
            }

            for (int i = 0; i < notes.Count; i++)
            {
                string note = notes[i];
                if (string.IsNullOrEmpty(note))
                {
                    continue;
                }

                rows.Add(new DimensionTemplateCustomizerDetailRow(
                    DimensionTemplateCustomizerDetailRowKind.Note,
                    DimensionAuthoringReadinessState.Partial,
                    DimensionAuthoringSeverity.Info,
                    sectionId,
                    string.Empty,
                    "Note",
                    string.Empty,
                    "Note",
                    note,
                    "inspect-note",
                    1,
                    500 + i,
                    false,
                    default(DimensionBounds)));
            }
        }

        private static bool ContentBelongsToSection(
            DimensionAuthoringContentSummaryKind kind,
            DimensionTemplateCustomizerSectionKind sectionKind)
        {
            if (sectionKind == DimensionTemplateCustomizerSectionKind.Dimension)
            {
                return kind == DimensionAuthoringContentSummaryKind.Dimension;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Layout)
            {
                return kind == DimensionAuthoringContentSummaryKind.LayoutTemplate;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Biomes)
            {
                return kind == DimensionAuthoringContentSummaryKind.Biome ||
                    kind == DimensionAuthoringContentSummaryKind.BiomeContentPreset ||
                    kind == DimensionAuthoringContentSummaryKind.EnvironmentProfile ||
                    kind == DimensionAuthoringContentSummaryKind.BiomePalette;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Terrain)
            {
                return kind == DimensionAuthoringContentSummaryKind.SemanticFloorObject ||
                    kind == DimensionAuthoringContentSummaryKind.SemanticWallObject ||
                    kind == DimensionAuthoringContentSummaryKind.SemanticOreObject ||
                    kind == DimensionAuthoringContentSummaryKind.SemanticWaterObject ||
                    kind == DimensionAuthoringContentSummaryKind.BiomePaletteEntry;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Generation)
            {
                return kind == DimensionAuthoringContentSummaryKind.GenerationPass ||
                    kind == DimensionAuthoringContentSummaryKind.GenerationTable ||
                    kind == DimensionAuthoringContentSummaryKind.GenerationTableEntry;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Scenes)
            {
                return kind == DimensionAuthoringContentSummaryKind.SceneTemplate ||
                    kind == DimensionAuthoringContentSummaryKind.SceneProp ||
                    kind == DimensionAuthoringContentSummaryKind.SceneLootContainer ||
                    kind == DimensionAuthoringContentSummaryKind.SceneTrigger;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Resources)
            {
                return kind == DimensionAuthoringContentSummaryKind.ResourceNode ||
                    kind == DimensionAuthoringContentSummaryKind.Item ||
                    kind == DimensionAuthoringContentSummaryKind.Recipe ||
                    kind == DimensionAuthoringContentSummaryKind.Workbench ||
                    kind == DimensionAuthoringContentSummaryKind.LootTable;
            }

            if (sectionKind == DimensionTemplateCustomizerSectionKind.Spawns)
            {
                return kind == DimensionAuthoringContentSummaryKind.SpawnRule ||
                    kind == DimensionAuthoringContentSummaryKind.Animal ||
                    kind == DimensionAuthoringContentSummaryKind.Critter ||
                    kind == DimensionAuthoringContentSummaryKind.Mob ||
                    kind == DimensionAuthoringContentSummaryKind.Boss ||
                    kind == DimensionAuthoringContentSummaryKind.SceneSpawnPoint;
            }

            return false;
        }

        private static string ResolveSectionForOperation(
            DimensionAuthoringOperationItem operation)
        {
            if (operation.Kind == DimensionAuthoringOperationKind.ExportManifest)
            {
                return "export";
            }

            if (operation.Kind == DimensionAuthoringOperationKind.PrepareRuntimeGeneration)
            {
                return "generation";
            }

            if (operation.Kind == DimensionAuthoringOperationKind.ResolveSpatialConflict ||
                operation.Kind == DimensionAuthoringOperationKind.FixBlockingIssue ||
                operation.Kind == DimensionAuthoringOperationKind.ReviewWarning)
            {
                return "diagnostics";
            }

            string fromLayer = ResolveSectionForLayer(operation.LayerKind);
            if (!string.IsNullOrEmpty(fromLayer))
            {
                return fromLayer;
            }

            return ResolveSectionForRecordKind(operation.RecordKind);
        }

        private static string ResolveSectionForLayer(
            DimensionAuthoringPreviewLayerKind layerKind)
        {
            if (layerKind == DimensionAuthoringPreviewLayerKind.BiomeRegion)
            {
                return "biomes";
            }

            if (layerKind == DimensionAuthoringPreviewLayerKind.GenerationPassBounds)
            {
                return "generation";
            }

            if (layerKind == DimensionAuthoringPreviewLayerKind.ScenePlacement)
            {
                return "scenes";
            }

            if (layerKind == DimensionAuthoringPreviewLayerKind.ResourceNode)
            {
                return "resources";
            }

            if (layerKind == DimensionAuthoringPreviewLayerKind.SpawnRule)
            {
                return "spawns";
            }

            if (layerKind == DimensionAuthoringPreviewLayerKind.PlayableBounds)
            {
                return "layout";
            }

            return string.Empty;
        }

        private static string ResolveSectionForCapability(string capabilityId)
        {
            if (capabilityId == "semantic-terrain" ||
                capabilityId == "biome-palette")
            {
                return "terrain";
            }

            if (capabilityId == "generation-table" ||
                capabilityId == "generation-pass")
            {
                return "generation";
            }

            if (capabilityId == "scene-placement")
            {
                return "scenes";
            }

            if (capabilityId == "resource-node")
            {
                return "resources";
            }

            if (capabilityId == "spawn-rule")
            {
                return "spawns";
            }

            if (capabilityId == "environment-profile")
            {
                return "biomes";
            }

            return "diagnostics";
        }

        private static string ResolveSectionForRecordKind(string recordKind)
        {
            if (string.IsNullOrEmpty(recordKind))
            {
                return "diagnostics";
            }

            if (recordKind == "Dimension")
            {
                return "dimension";
            }

            if (recordKind == "Layout" ||
                recordKind == "LayoutTemplate" ||
                recordKind == "PlayableBounds")
            {
                return "layout";
            }

            if (recordKind == "Biome" ||
                recordKind == "BiomeContentPreset" ||
                recordKind == "EnvironmentProfile" ||
                recordKind == "BiomePalette")
            {
                return "biomes";
            }

            if (recordKind == "BiomePaletteEntry" ||
                recordKind == "SemanticFloorObject" ||
                recordKind == "SemanticWallObject" ||
                recordKind == "SemanticOreObject" ||
                recordKind == "SemanticWaterObject")
            {
                return "terrain";
            }

            if (recordKind == "GenerationPass" ||
                recordKind == "GenerationTable" ||
                recordKind == "GenerationTableEntry")
            {
                return "generation";
            }

            if (recordKind == "Scene" ||
                recordKind == "SceneTemplate" ||
                recordKind == "SceneProp" ||
                recordKind == "SceneLootContainer" ||
                recordKind == "SceneTrigger")
            {
                return "scenes";
            }

            if (recordKind == "ResourceNode" ||
                recordKind == "Item" ||
                recordKind == "Recipe" ||
                recordKind == "Workbench" ||
                recordKind == "LootTable")
            {
                return "resources";
            }

            if (recordKind == "SpawnRule" ||
                recordKind == "Animal" ||
                recordKind == "Critter" ||
                recordKind == "Mob" ||
                recordKind == "Boss" ||
                recordKind == "SceneSpawnPoint")
            {
                return "spawns";
            }

            return "diagnostics";
        }

        private static string ResolveSectionForBudgetGuidance(
            DimensionGenerationBudgetGuidanceItem item)
        {
            if (item.Kind == DimensionGenerationBudgetGuidanceKind.MissingPlayableArea)
            {
                return "layout";
            }

            if (item.Kind == DimensionGenerationBudgetGuidanceKind.SceneCount)
            {
                return "scenes";
            }

            if (item.Kind == DimensionGenerationBudgetGuidanceKind.ResourceCount)
            {
                return "resources";
            }

            if (item.Kind == DimensionGenerationBudgetGuidanceKind.SpawnRuleCount)
            {
                return "spawns";
            }

            if (item.Kind == DimensionGenerationBudgetGuidanceKind.Blocked)
            {
                return "diagnostics";
            }

            return "generation";
        }

        private static DimensionAuthoringSeverity ResolveSeverity(
            DimensionAuthoringReadinessState state)
        {
            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionAuthoringSeverity.Error;
            }

            if (state == DimensionAuthoringReadinessState.Missing ||
                state == DimensionAuthoringReadinessState.Partial)
            {
                return DimensionAuthoringSeverity.Warning;
            }

            return DimensionAuthoringSeverity.Info;
        }

        private static string ResolveTitle(
            string displayName,
            string recordId,
            string fallback)
        {
            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            if (!string.IsNullOrEmpty(recordId))
            {
                return recordId;
            }

            return fallback ?? string.Empty;
        }

        private static void CountRows(
            IReadOnlyList<DimensionTemplateCustomizerDetailRow> rows,
            out int operationRows,
            out int guidanceRows,
            out int issueRows,
            out int contentRows)
        {
            operationRows = 0;
            guidanceRows = 0;
            issueRows = 0;
            contentRows = 0;
            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                DimensionTemplateCustomizerDetailRow row = rows[i];
                if (row.Kind == DimensionTemplateCustomizerDetailRowKind.Operation)
                {
                    operationRows++;
                }
                else if (row.Kind == DimensionTemplateCustomizerDetailRowKind.Guidance)
                {
                    guidanceRows++;
                }
                else if (row.Kind == DimensionTemplateCustomizerDetailRowKind.Issue)
                {
                    issueRows++;
                }
                else if (row.Kind == DimensionTemplateCustomizerDetailRowKind.Content)
                {
                    contentRows++;
                }
            }
        }

        private static int CompareRows(
            DimensionTemplateCustomizerDetailRow left,
            DimensionTemplateCustomizerDetailRow right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
            {
                return kind;
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
