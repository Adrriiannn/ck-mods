using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimension Framework/Generation Table Template")]
    public sealed class GenerationTableTemplateAsset : ScriptableObject
    {
        [SerializeField] private string tableId = "terrain";
        [SerializeField] private string displayName = "Terrain";
        [SerializeField] private DimensionGenerationTableKind kind = DimensionGenerationTableKind.Terrain;
        [SerializeField] private string biomeIdOverride = string.Empty;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private GenerationTableEntryTemplate[] entries = new GenerationTableEntryTemplate[0];
        [SerializeField] private string notes = string.Empty;

        public string TableId
        {
            get { return tableId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionGenerationTableKind Kind
        {
            get { return kind; }
        }

        public string BiomeIdOverride
        {
            get { return biomeIdOverride ?? string.Empty; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public GenerationTableEntryTemplate[] Entries
        {
            get { return entries ?? new GenerationTableEntryTemplate[0]; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void ConfigureIdentity(
            string newTableId,
            string newDisplayName,
            DimensionGenerationTableKind newKind,
            string newBiomeIdOverride,
            int newPriority,
            bool newEnabled,
            string newNotes)
        {
            tableId = newTableId ?? string.Empty;
            displayName = newDisplayName ?? string.Empty;
            kind = newKind;
            biomeIdOverride = newBiomeIdOverride ?? string.Empty;
            priority = newPriority;
            enabled = newEnabled;
            notes = newNotes ?? string.Empty;
        }

        public void SetEntries(IReadOnlyList<GenerationTableEntryTemplate> newEntries)
        {
            if (newEntries == null || newEntries.Count == 0)
            {
                entries = new GenerationTableEntryTemplate[0];
                return;
            }

            List<GenerationTableEntryTemplate> destination = new List<GenerationTableEntryTemplate>();
            for (int i = 0; i < newEntries.Count; i++)
            {
                GenerationTableEntryTemplate entry = newEntries[i];
                if (entry != null)
                {
                    destination.Add(entry);
                }
            }

            entries = destination.ToArray();
        }

        public void ClearEntries()
        {
            entries = new GenerationTableEntryTemplate[0];
        }

        public DimensionGenerationTableDefinition ToTableDefinition(
            string tableIdOverride,
            string dimensionId,
            string fallbackBiomeId)
        {
            string resolvedTableId = string.IsNullOrEmpty(tableIdOverride) ? TableId : tableIdOverride;
            string resolvedDisplayName = string.IsNullOrEmpty(displayName) ? TableId : displayName;
            string resolvedBiomeId = string.IsNullOrEmpty(biomeIdOverride) ? fallbackBiomeId : biomeIdOverride;

            return new DimensionGenerationTableDefinition(
                resolvedTableId,
                resolvedDisplayName,
                dimensionId,
                resolvedBiomeId,
                kind,
                priority,
                enabled,
                notes);
        }

        public void AddEntryDefinitions(
            string tableId,
            List<DimensionGenerationTableEntryDefinition> generationTableEntries)
        {
            if (generationTableEntries == null)
            {
                return;
            }

            GenerationTableEntryTemplate[] tableEntries = Entries;
            for (int i = 0; i < tableEntries.Length; i++)
            {
                GenerationTableEntryTemplate entry = tableEntries[i];
                if (entry == null)
                {
                    continue;
                }

                string entryId = BuildScopedEntryId(tableId, entry.EntryId, i);
                generationTableEntries.Add(entry.ToDefinition(entryId, tableId));
            }
        }

        private static string BuildScopedEntryId(string tableId, string entryId, int index)
        {
            string resolvedEntryId = entryId ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedEntryId))
            {
                resolvedEntryId = "entry-" + index.ToString();
            }

            if (resolvedEntryId.IndexOf('.') >= 0 || resolvedEntryId.IndexOf(':') >= 0)
            {
                return resolvedEntryId;
            }

            return (tableId ?? string.Empty) + "." + resolvedEntryId;
        }
    }

    [System.Serializable]
    public sealed class GenerationTableEntryTemplate
    {
        [SerializeField] private string entryId = "entry";
        [SerializeField] private string subjectId = string.Empty;
        [SerializeField] private string subjectKind = "object";
        [SerializeField] private int weight = 1;
        [SerializeField] private int minCount = 1;
        [SerializeField] private int maxCount = 1;
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public GenerationTableEntryTemplate()
        {
        }

        public GenerationTableEntryTemplate(
            string entryId,
            string subjectId,
            string subjectKind,
            int weight,
            int minCount,
            int maxCount,
            int priority,
            bool enabled,
            string notes)
        {
            Configure(
                entryId,
                subjectId,
                subjectKind,
                weight,
                minCount,
                maxCount,
                priority,
                enabled,
                notes);
        }

        public string EntryId
        {
            get { return entryId ?? string.Empty; }
        }

        public string SubjectId
        {
            get { return subjectId ?? string.Empty; }
        }

        public string SubjectKind
        {
            get { return subjectKind ?? string.Empty; }
        }

        public int Weight
        {
            get { return Mathf.Max(1, weight); }
        }

        public int MinCount
        {
            get { return Mathf.Max(0, minCount); }
        }

        public int MaxCount
        {
            get { return Mathf.Max(MinCount, maxCount); }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void Configure(
            string newEntryId,
            string newSubjectId,
            string newSubjectKind,
            int newWeight,
            int newMinCount,
            int newMaxCount,
            int newPriority,
            bool newEnabled,
            string newNotes)
        {
            entryId = newEntryId ?? string.Empty;
            subjectId = newSubjectId ?? string.Empty;
            subjectKind = string.IsNullOrEmpty(newSubjectKind) ? "object" : newSubjectKind;
            weight = Mathf.Max(1, newWeight);
            minCount = Mathf.Max(0, newMinCount);
            maxCount = Mathf.Max(minCount, newMaxCount);
            priority = newPriority;
            enabled = newEnabled;
            notes = newNotes ?? string.Empty;
        }

        public DimensionGenerationTableEntryDefinition ToDefinition(
            string entryIdOverride,
            string tableId)
        {
            string resolvedEntryId = string.IsNullOrEmpty(entryIdOverride) ? EntryId : entryIdOverride;
            int resolvedWeight = Mathf.Max(1, weight);
            int resolvedMinCount = Mathf.Max(0, minCount);
            int resolvedMaxCount = Mathf.Max(resolvedMinCount, maxCount);

            return new DimensionGenerationTableEntryDefinition(
                resolvedEntryId,
                tableId,
                subjectId,
                subjectKind,
                resolvedWeight,
                resolvedMinCount,
                resolvedMaxCount,
                priority,
                enabled,
                notes);
        }
    }
}
