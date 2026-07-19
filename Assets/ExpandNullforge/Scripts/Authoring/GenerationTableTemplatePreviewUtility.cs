using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public static class GenerationTableTemplatePreviewUtility
    {
        public static DimensionGenerationTablePreview BuildPreview(
            GenerationTableTemplateAsset tableAsset,
            string dimensionId,
            string fallbackBiomeId,
            bool enabledEntriesOnly)
        {
            if (tableAsset == null)
            {
                return DimensionGenerationTablePreviewUtility.BuildPreview(
                    new DimensionGenerationTableDefinition(
                        string.Empty,
                        string.Empty,
                        dimensionId ?? string.Empty,
                        fallbackBiomeId ?? string.Empty,
                        DimensionGenerationTableKind.Any,
                        0,
                        false,
                        "Missing generation table template."),
                    new List<DimensionGenerationTableEntryDefinition>(),
                    enabledEntriesOnly);
            }

            string tableId = BuildScopedId(
                dimensionId,
                fallbackBiomeId,
                tableAsset.TableId);
            DimensionGenerationTableDefinition table =
                tableAsset.ToTableDefinition(tableId, dimensionId, fallbackBiomeId);
            List<DimensionGenerationTableEntryDefinition> entries =
                new List<DimensionGenerationTableEntryDefinition>();
            tableAsset.AddEntryDefinitions(table.TableId, entries);

            return DimensionGenerationTablePreviewUtility.BuildPreview(
                table,
                entries,
                enabledEntriesOnly);
        }

        public static List<DimensionGenerationTablePreview> BuildBiomePreviews(
            BiomeTemplateAsset biome,
            string dimensionId,
            bool enabledEntriesOnly)
        {
            List<DimensionGenerationTablePreview> previews =
                new List<DimensionGenerationTablePreview>();
            if (biome == null)
            {
                return previews;
            }

            AddTablePreviews(
                biome.GetGenerationTablesWithProfile(),
                dimensionId,
                biome.BiomeId,
                enabledEntriesOnly,
                previews);
            return previews;
        }

        public static List<DimensionGenerationTablePreview> BuildDimensionPreviews(
            DimensionTemplateAsset template,
            bool enabledEntriesOnly)
        {
            List<DimensionGenerationTablePreview> previews =
                new List<DimensionGenerationTablePreview>();
            if (template == null)
            {
                return previews;
            }

            string dimensionId = template.DimensionId;
            AddTablePreviews(
                template.GlobalGenerationTables,
                dimensionId,
                string.Empty,
                enabledEntriesOnly,
                previews);

            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome == null)
                {
                    continue;
                }

                AddTablePreviews(
                    biome.GetGenerationTablesWithProfile(),
                    dimensionId,
                    biome.BiomeId,
                    enabledEntriesOnly,
                    previews);
            }

            previews.Sort(CompareTablePreviews);
            return previews;
        }

        private static void AddTablePreviews(
            GenerationTableTemplateAsset[] tableAssets,
            string dimensionId,
            string fallbackBiomeId,
            bool enabledEntriesOnly,
            List<DimensionGenerationTablePreview> destination)
        {
            if (tableAssets == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < tableAssets.Length; i++)
            {
                GenerationTableTemplateAsset tableAsset = tableAssets[i];
                if (tableAsset == null)
                {
                    continue;
                }

                destination.Add(BuildPreview(
                    tableAsset,
                    dimensionId,
                    fallbackBiomeId,
                    enabledEntriesOnly));
            }
        }

        private static int CompareTablePreviews(
            DimensionGenerationTablePreview left,
            DimensionGenerationTablePreview right)
        {
            int dimension = string.CompareOrdinal(left.DimensionId, right.DimensionId);
            if (dimension != 0)
            {
                return dimension;
            }

            int biome = string.CompareOrdinal(left.BiomeId, right.BiomeId);
            if (biome != 0)
            {
                return biome;
            }

            int kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
            {
                return kind;
            }

            int priority = right.Table.Priority.CompareTo(left.Table.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.CompareOrdinal(left.TableId, right.TableId);
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

            string resolvedDimensionId = dimensionId ?? string.Empty;
            string resolvedZoneId = zoneId ?? string.Empty;
            if (!string.IsNullOrEmpty(resolvedZoneId))
            {
                if (!string.IsNullOrEmpty(resolvedDimensionId) &&
                    resolvedZoneId.StartsWith(resolvedDimensionId + "."))
                {
                    return resolvedZoneId + "." + resolvedId;
                }

                if (!string.IsNullOrEmpty(resolvedDimensionId))
                {
                    return resolvedDimensionId + "." + resolvedZoneId + "." + resolvedId;
                }

                return resolvedZoneId + "." + resolvedId;
            }

            if (string.IsNullOrEmpty(resolvedDimensionId))
            {
                return resolvedId;
            }

            return resolvedDimensionId + "." + resolvedId;
        }
    }
}
