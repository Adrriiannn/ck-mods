using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionGenerationTableEntryPreview
    {
        public readonly DimensionGenerationTableEntryDefinition Entry;
        public readonly string EntryId;
        public readonly string SubjectId;
        public readonly string SubjectKind;
        public readonly int Weight;
        public readonly int EffectiveWeight;
        public readonly float ProbabilityPercent;
        public readonly bool Enabled;
        public readonly bool Selectable;
        public readonly string Code;
        public readonly string Message;

        public DimensionGenerationTableEntryPreview(
            DimensionGenerationTableEntryDefinition entry,
            int effectiveWeight,
            float probabilityPercent,
            bool selectable,
            string code,
            string message)
        {
            Entry = entry;
            EntryId = entry.EntryId ?? string.Empty;
            SubjectId = entry.SubjectId ?? string.Empty;
            SubjectKind = entry.SubjectKind ?? string.Empty;
            Weight = entry.Weight;
            EffectiveWeight = effectiveWeight < 0 ? 0 : effectiveWeight;
            ProbabilityPercent = probabilityPercent < 0f ? 0f : probabilityPercent;
            Enabled = entry.Enabled;
            Selectable = selectable;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct DimensionGenerationTablePreview
    {
        public readonly DimensionGenerationTableDefinition Table;
        public readonly string TableId;
        public readonly string DisplayName;
        public readonly string DimensionId;
        public readonly string BiomeId;
        public readonly DimensionGenerationTableKind Kind;
        public readonly bool Enabled;
        public readonly int EntryCount;
        public readonly int EnabledEntryCount;
        public readonly int SelectableEntryCount;
        public readonly int DisabledEntryCount;
        public readonly int InvalidWeightCount;
        public readonly int TotalEffectiveWeight;
        public readonly string Code;
        public readonly string Message;
        public readonly IReadOnlyList<DimensionGenerationTableEntryPreview> Entries;

        public DimensionGenerationTablePreview(
            DimensionGenerationTableDefinition table,
            int enabledEntryCount,
            int selectableEntryCount,
            int disabledEntryCount,
            int invalidWeightCount,
            int totalEffectiveWeight,
            string code,
            string message,
            IReadOnlyList<DimensionGenerationTableEntryPreview> entries)
        {
            Table = table;
            TableId = table.TableId ?? string.Empty;
            DisplayName = table.DisplayName ?? string.Empty;
            DimensionId = table.DimensionId ?? string.Empty;
            BiomeId = table.BiomeId ?? string.Empty;
            Kind = table.Kind;
            Enabled = table.Enabled;
            EnabledEntryCount = enabledEntryCount < 0 ? 0 : enabledEntryCount;
            SelectableEntryCount = selectableEntryCount < 0 ? 0 : selectableEntryCount;
            DisabledEntryCount = disabledEntryCount < 0 ? 0 : disabledEntryCount;
            InvalidWeightCount = invalidWeightCount < 0 ? 0 : invalidWeightCount;
            TotalEffectiveWeight = totalEffectiveWeight < 0 ? 0 : totalEffectiveWeight;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Entries = entries ?? new List<DimensionGenerationTableEntryPreview>();
            EntryCount = Entries.Count;
        }
    }

    public static class DimensionGenerationTablePreviewUtility
    {
        public static List<DimensionGenerationTablePreview> BuildPreviews(
            IReadOnlyList<DimensionResolvedGenerationTable> resolvedTables)
        {
            List<DimensionGenerationTablePreview> previews =
                new List<DimensionGenerationTablePreview>();
            if (resolvedTables == null)
            {
                return previews;
            }

            for (int i = 0; i < resolvedTables.Count; i++)
            {
                DimensionResolvedGenerationTable resolved = resolvedTables[i];
                previews.Add(BuildPreview(resolved.Table, resolved.Entries, true));
            }

            return previews;
        }

        public static List<DimensionGenerationTablePreview> BuildPreviews(
            IDimensionGenerationTableService service,
            DimensionGenerationTableQuery query,
            bool enabledEntriesOnly)
        {
            List<DimensionGenerationTablePreview> previews =
                new List<DimensionGenerationTablePreview>();
            if (service == null)
            {
                return previews;
            }

            IReadOnlyList<DimensionGenerationTableDefinition> tables =
                service.GetGenerationTables(query);
            if (tables == null)
            {
                return previews;
            }

            for (int i = 0; i < tables.Count; i++)
            {
                DimensionGenerationTableDefinition table = tables[i];
                IReadOnlyList<DimensionGenerationTableEntryDefinition> entries =
                    service.GetGenerationTableEntries(table.TableId, enabledEntriesOnly);
                previews.Add(BuildPreview(table, entries, enabledEntriesOnly));
            }

            return previews;
        }

        public static DimensionGenerationTablePreview BuildPreview(
            DimensionGenerationTableDefinition table,
            IReadOnlyList<DimensionGenerationTableEntryDefinition> entries,
            bool enabledEntriesOnly)
        {
            int enabledCount = 0;
            int selectableCount = 0;
            int disabledCount = 0;
            int invalidWeightCount = 0;
            int totalWeight = CalculateTotalWeight(
                entries,
                enabledEntriesOnly,
                out enabledCount,
                out selectableCount,
                out disabledCount,
                out invalidWeightCount);

            List<DimensionGenerationTableEntryPreview> entryPreviews =
                BuildEntryPreviews(entries, enabledEntriesOnly, totalWeight);

            string code;
            string message;
            ResolveState(
                table,
                entryPreviews.Count,
                selectableCount,
                invalidWeightCount,
                out code,
                out message);

            return new DimensionGenerationTablePreview(
                table,
                enabledCount,
                selectableCount,
                disabledCount,
                invalidWeightCount,
                totalWeight,
                code,
                message,
                entryPreviews);
        }

        private static int CalculateTotalWeight(
            IReadOnlyList<DimensionGenerationTableEntryDefinition> entries,
            bool enabledEntriesOnly,
            out int enabledCount,
            out int selectableCount,
            out int disabledCount,
            out int invalidWeightCount)
        {
            enabledCount = 0;
            selectableCount = 0;
            disabledCount = 0;
            invalidWeightCount = 0;
            int totalWeight = 0;

            if (entries == null)
            {
                return 0;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionGenerationTableEntryDefinition entry = entries[i];
                if (!entry.Enabled)
                {
                    disabledCount++;
                    if (enabledEntriesOnly)
                    {
                        continue;
                    }
                }
                else
                {
                    enabledCount++;
                }

                if (!entry.Enabled || entry.Weight <= 0)
                {
                    if (entry.Enabled && entry.Weight <= 0)
                    {
                        invalidWeightCount++;
                    }

                    continue;
                }

                selectableCount++;
                totalWeight += entry.Weight;
            }

            return totalWeight;
        }

        private static List<DimensionGenerationTableEntryPreview> BuildEntryPreviews(
            IReadOnlyList<DimensionGenerationTableEntryDefinition> entries,
            bool enabledEntriesOnly,
            int totalWeight)
        {
            List<DimensionGenerationTableEntryPreview> previews =
                new List<DimensionGenerationTableEntryPreview>();
            if (entries == null)
            {
                return previews;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionGenerationTableEntryDefinition entry = entries[i];
                if (enabledEntriesOnly && !entry.Enabled)
                {
                    continue;
                }

                int effectiveWeight = entry.Enabled && entry.Weight > 0 ? entry.Weight : 0;
                bool selectable = effectiveWeight > 0 && totalWeight > 0;
                float probability = selectable
                    ? (effectiveWeight * 100f) / totalWeight
                    : 0f;

                string code;
                string message;
                ResolveEntryState(entry, selectable, out code, out message);

                previews.Add(new DimensionGenerationTableEntryPreview(
                    entry,
                    effectiveWeight,
                    probability,
                    selectable,
                    code,
                    message));
            }

            previews.Sort(CompareEntries);
            return previews;
        }

        private static void ResolveState(
            DimensionGenerationTableDefinition table,
            int entryCount,
            int selectableCount,
            int invalidWeightCount,
            out string code,
            out string message)
        {
            if (!table.Enabled)
            {
                code = "table-disabled";
                message = "This generation table is disabled.";
            }
            else if (entryCount <= 0)
            {
                code = "table-empty";
                message = "This generation table has no entries.";
            }
            else if (selectableCount <= 0)
            {
                code = "table-no-selectable-entries";
                message = "This generation table has no enabled entries with positive weight.";
            }
            else if (invalidWeightCount > 0)
            {
                code = "table-has-invalid-weights";
                message = "This generation table has enabled entries with zero or negative weight.";
            }
            else
            {
                code = "ready";
                message = "This generation table can be sampled.";
            }
        }

        private static void ResolveEntryState(
            DimensionGenerationTableEntryDefinition entry,
            bool selectable,
            out string code,
            out string message)
        {
            if (!entry.Enabled)
            {
                code = "entry-disabled";
                message = "This entry is disabled.";
            }
            else if (entry.Weight <= 0)
            {
                code = "entry-invalid-weight";
                message = "This entry is enabled but has no positive weight.";
            }
            else if (!selectable)
            {
                code = "entry-not-selectable";
                message = "This entry cannot be selected until the table has positive total weight.";
            }
            else
            {
                code = "ready";
                message = "This entry can be selected.";
            }
        }

        private static int CompareEntries(
            DimensionGenerationTableEntryPreview left,
            DimensionGenerationTableEntryPreview right)
        {
            int selectable = right.Selectable.CompareTo(left.Selectable);
            if (selectable != 0)
            {
                return selectable;
            }

            int probability = right.ProbabilityPercent.CompareTo(left.ProbabilityPercent);
            if (probability != 0)
            {
                return probability;
            }

            int priority = right.Entry.Priority.CompareTo(left.Entry.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.CompareOrdinal(left.SubjectId, right.SubjectId);
        }
    }
}
