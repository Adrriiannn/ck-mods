using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public static class DimensionSemanticObjectTableBuilder
    {
        public static void AddBiomeSemanticObjectTables(
            BiomeTemplateAsset biome,
            string dimensionId,
            List<DimensionGenerationTableDefinition> generationTables,
            List<DimensionGenerationTableEntryDefinition> generationTableEntries,
            Dictionary<string, bool> tableIds,
            Dictionary<string, bool> entryIds)
        {
            if (biome == null || !biome.Enabled)
            {
                return;
            }

            AddSemanticObjectTable(
                biome.GetFloorObjectIdsWithPresets(),
                dimensionId,
                biome.BiomeId,
                "floor-objects",
                "Floor Objects",
                DimensionGenerationTableKind.Terrain,
                DimensionGenerationSubjectKind.FloorObject,
                biome.Priority,
                generationTables,
                generationTableEntries,
                tableIds,
                entryIds);

            AddSemanticObjectTable(
                biome.GetWallObjectIdsWithPresets(),
                dimensionId,
                biome.BiomeId,
                "wall-objects",
                "Wall Objects",
                DimensionGenerationTableKind.Terrain,
                DimensionGenerationSubjectKind.WallObject,
                biome.Priority,
                generationTables,
                generationTableEntries,
                tableIds,
                entryIds);

            AddSemanticObjectTable(
                biome.GetOreObjectIdsWithPresets(),
                dimensionId,
                biome.BiomeId,
                "ore-objects",
                "Ore Objects",
                DimensionGenerationTableKind.Resource,
                DimensionGenerationSubjectKind.OreObject,
                biome.Priority,
                generationTables,
                generationTableEntries,
                tableIds,
                entryIds);

            AddSemanticObjectTable(
                biome.GetWaterObjectIdsWithPresets(),
                dimensionId,
                biome.BiomeId,
                "water-objects",
                "Water Objects",
                DimensionGenerationTableKind.Terrain,
                DimensionGenerationSubjectKind.WaterObject,
                biome.Priority,
                generationTables,
                generationTableEntries,
                tableIds,
                entryIds);
        }

        public static void AddExistingGenerationTableIds(
            List<DimensionGenerationTableDefinition> generationTables,
            Dictionary<string, bool> tableIds)
        {
            if (generationTables == null || tableIds == null)
            {
                return;
            }

            for (int i = 0; i < generationTables.Count; i++)
            {
                string tableId = generationTables[i].TableId;
                if (string.IsNullOrEmpty(tableId) || tableIds.ContainsKey(tableId))
                {
                    continue;
                }

                tableIds.Add(tableId, true);
            }
        }

        public static void AddExistingGenerationTableEntryIds(
            List<DimensionGenerationTableEntryDefinition> generationTableEntries,
            Dictionary<string, bool> entryIds)
        {
            if (generationTableEntries == null || entryIds == null)
            {
                return;
            }

            for (int i = 0; i < generationTableEntries.Count; i++)
            {
                string entryId = generationTableEntries[i].EntryId;
                if (string.IsNullOrEmpty(entryId) || entryIds.ContainsKey(entryId))
                {
                    continue;
                }

                entryIds.Add(entryId, true);
            }
        }

        private static void AddSemanticObjectTable(
            string[] objectIds,
            string dimensionId,
            string biomeId,
            string tableSuffix,
            string displayName,
            DimensionGenerationTableKind tableKind,
            string subjectKind,
            int priority,
            List<DimensionGenerationTableDefinition> generationTables,
            List<DimensionGenerationTableEntryDefinition> generationTableEntries,
            Dictionary<string, bool> tableIds,
            Dictionary<string, bool> entryIds)
        {
            if (objectIds == null ||
                objectIds.Length == 0 ||
                generationTables == null ||
                generationTableEntries == null)
            {
                return;
            }

            string tableId = BuildScopedId(dimensionId, biomeId, tableSuffix);
            if (string.IsNullOrEmpty(tableId))
            {
                return;
            }

            if (tableIds != null && tableIds.ContainsKey(tableId))
            {
                return;
            }

            Dictionary<string, bool> subjectIds = new Dictionary<string, bool>();
            List<DimensionGenerationTableEntryDefinition> entries =
                new List<DimensionGenerationTableEntryDefinition>();
            for (int i = 0; i < objectIds.Length; i++)
            {
                string objectId = objectIds[i] ?? string.Empty;
                if (string.IsNullOrEmpty(objectId) || subjectIds.ContainsKey(objectId))
                {
                    continue;
                }

                subjectIds.Add(objectId, true);

                string entryId = tableId + ".entry-" + entries.Count.ToString();
                if (entryIds != null && entryIds.ContainsKey(entryId))
                {
                    continue;
                }

                entries.Add(new DimensionGenerationTableEntryDefinition(
                    entryId,
                    tableId,
                    objectId,
                    subjectKind,
                    1,
                    1,
                    1,
                    priority,
                    true,
                    "Generated from biome semantic object ids."));
            }

            if (entries.Count == 0)
            {
                return;
            }

            generationTables.Add(new DimensionGenerationTableDefinition(
                tableId,
                displayName,
                dimensionId,
                biomeId,
                tableKind,
                priority,
                true,
                "Generated from biome semantic object ids."));

            if (tableIds != null)
            {
                tableIds.Add(tableId, true);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionGenerationTableEntryDefinition entry = entries[i];
                generationTableEntries.Add(entry);
                if (entryIds != null && !entryIds.ContainsKey(entry.EntryId))
                {
                    entryIds.Add(entry.EntryId, true);
                }
            }
        }

        private static string BuildScopedId(string dimensionId, string zoneId, string id)
        {
            string resolvedId = id ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedId))
            {
                return string.Empty;
            }

            if (resolvedId.IndexOf('.') >= 0 || resolvedId.IndexOf(':') >= 0)
            {
                return resolvedId;
            }

            if (!string.IsNullOrEmpty(zoneId))
            {
                if (zoneId.StartsWith(dimensionId + "."))
                {
                    return zoneId + "." + resolvedId;
                }

                return dimensionId + "." + zoneId + "." + resolvedId;
            }

            return dimensionId + "." + resolvedId;
        }
    }
}
