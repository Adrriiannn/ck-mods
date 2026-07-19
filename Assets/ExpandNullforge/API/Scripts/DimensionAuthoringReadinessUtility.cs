using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public static class DimensionAuthoringReadinessUtility
    {
        public static DimensionAuthoringReadinessReport BuildReport(
            DimensionAuthoringPreviewSummary summary)
        {
            List<DimensionAuthoringReadinessEntry> entries =
                new List<DimensionAuthoringReadinessEntry>();

            string dimensionId = summary.DimensionId;
            int biomeCount = CountContent(summary, DimensionAuthoringContentSummaryKind.Biome, string.Empty);
            int layoutCount = CountContent(summary, DimensionAuthoringContentSummaryKind.LayoutTemplate, string.Empty);
            int errors = summary.ErrorCount;
            int warnings = summary.WarningCount;

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Dimension,
                string.Empty,
                !string.IsNullOrEmpty(dimensionId)
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Blocked,
                "dimension-id",
                !string.IsNullOrEmpty(dimensionId)
                    ? "Dimension identity is present."
                    : "Dimension identity is missing.",
                string.IsNullOrEmpty(dimensionId) ? 0 : 1,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Layout,
                string.Empty,
                layoutCount > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Missing,
                "layout-template",
                layoutCount > 0
                    ? "A biome layout template is present."
                    : "No biome layout template is present.",
                layoutCount,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Biome,
                string.Empty,
                biomeCount > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Missing,
                "biome-count",
                biomeCount > 0
                    ? "At least one biome is declared."
                    : "No biome is declared.",
                biomeCount,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Validation,
                string.Empty,
                errors > 0
                    ? DimensionAuthoringReadinessState.Blocked
                    : warnings > 0
                        ? DimensionAuthoringReadinessState.Partial
                        : DimensionAuthoringReadinessState.Ready,
                "authoring-issues",
                errors > 0
                    ? "Authoring errors must be fixed before this dimension should be generated."
                    : warnings > 0
                        ? "Authoring warnings are present; generation can continue, but the customizer should show them."
                        : "No blocking authoring issues were reported.",
                errors > 0 ? errors : warnings,
                0,
                dimensionId);

            IReadOnlyList<DimensionAuthoringContentSummaryEntry> contentEntries = summary.ContentEntries;
            if (contentEntries != null)
            {
                for (int i = 0; i < contentEntries.Count; i++)
                {
                    DimensionAuthoringContentSummaryEntry content = contentEntries[i];
                    if (content.Kind != DimensionAuthoringContentSummaryKind.Biome)
                    {
                        continue;
                    }

                    AddBiomeReadiness(summary, entries, dimensionId, content.BiomeId);
                }
            }

            int readyCount;
            int partialCount;
            int missingCount;
            int blockedCount;
            CountStates(entries, out readyCount, out partialCount, out missingCount, out blockedCount);

            bool ready = missingCount == 0 && blockedCount == 0;
            return new DimensionAuthoringReadinessReport(
                ready,
                ready ? "ready" : "not-ready",
                ready
                    ? "The authoring preview has enough declared content for generation tooling."
                    : "The authoring preview is missing required generation authoring pieces.",
                readyCount,
                partialCount,
                missingCount,
                blockedCount,
                entries);
        }

        private static void AddBiomeReadiness(
            DimensionAuthoringPreviewSummary summary,
            List<DimensionAuthoringReadinessEntry> entries,
            string dimensionId,
            string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return;
            }

            int paletteEntries = CountContent(summary, DimensionAuthoringContentSummaryKind.BiomePaletteEntry, biomeId);
            int semanticTerrain =
                CountContent(summary, DimensionAuthoringContentSummaryKind.SemanticFloorObject, biomeId) +
                CountContent(summary, DimensionAuthoringContentSummaryKind.SemanticWallObject, biomeId) +
                CountContent(summary, DimensionAuthoringContentSummaryKind.SemanticOreObject, biomeId) +
                CountContent(summary, DimensionAuthoringContentSummaryKind.SemanticWaterObject, biomeId);
            int generationPasses = CountContent(summary, DimensionAuthoringContentSummaryKind.GenerationPass, biomeId);
            int generationTables = CountContent(summary, DimensionAuthoringContentSummaryKind.GenerationTable, biomeId);
            int environmentProfiles = CountContent(summary, DimensionAuthoringContentSummaryKind.EnvironmentProfile, biomeId);
            int scenes = CountSceneContent(summary, biomeId);
            int resources = CountResourceContent(summary, biomeId);
            int spawns = CountSpawnContent(summary, biomeId);

            int terrainSignals = paletteEntries + semanticTerrain + generationTables;
            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Terrain,
                biomeId,
                terrainSignals > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : generationPasses > 0
                        ? DimensionAuthoringReadinessState.Partial
                        : DimensionAuthoringReadinessState.Missing,
                "biome-terrain",
                terrainSignals > 0
                    ? "Biome has terrain/object content declarations."
                    : generationPasses > 0
                        ? "Biome has generation passes but no explicit terrain/object content declarations."
                        : "Biome has no terrain/object content declarations.",
                terrainSignals,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.GenerationPasses,
                biomeId,
                generationPasses > 0 || generationTables > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                "biome-generation-flow",
                generationPasses > 0 || generationTables > 0
                    ? "Biome has generation passes or generation tables."
                    : "Biome has no generation passes or tables yet; defaults may still generate, but custom behavior is undefined.",
                generationPasses + generationTables,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Environment,
                biomeId,
                environmentProfiles > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                "biome-environment",
                environmentProfiles > 0
                    ? "Biome has an environment profile."
                    : "Biome has no environment profile; dimension or vanilla fallback visuals may be used.",
                environmentProfiles,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Scenes,
                biomeId,
                scenes > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                "biome-scenes",
                scenes > 0
                    ? "Biome has scene or structure content."
                    : "Biome has no scene or structure content yet.",
                scenes,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Resources,
                biomeId,
                resources > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                "biome-resources",
                resources > 0
                    ? "Biome has resource, item, recipe, workbench, or loot declarations."
                    : "Biome has no resource, item, recipe, workbench, or loot declarations yet.",
                resources,
                1,
                dimensionId);

            AddEntry(
                entries,
                DimensionAuthoringReadinessCategory.Spawns,
                biomeId,
                spawns > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                "biome-spawns",
                spawns > 0
                    ? "Biome has spawn rules or authored spawnable content."
                    : "Biome has no spawn rules or authored spawnable content yet.",
                spawns,
                1,
                dimensionId);
        }

        private static int CountContent(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringContentSummaryKind kind,
            string biomeId)
        {
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries = summary.ContentEntries;
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (entry.Kind != kind)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(biomeId) && entry.BiomeId != biomeId)
                {
                    continue;
                }

                count += entry.Count < 1 ? 1 : entry.Count;
            }

            return count;
        }

        private static int CountResourceContent(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            return
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.ResourceNode, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Item, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Recipe, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Workbench, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.LootTable, biomeId);
        }

        private static int CountSceneContent(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            return
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneTemplate, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneProp, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneLootContainer, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneSpawnPoint, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneTrigger, biomeId);
        }

        private static int CountSpawnContent(
            DimensionAuthoringPreviewSummary summary,
            string biomeId)
        {
            return
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SpawnRule, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Animal, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Critter, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Mob, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.Boss, biomeId) +
                CountContentForBiomeOrGlobal(summary, DimensionAuthoringContentSummaryKind.SceneSpawnPoint, biomeId);
        }

        private static int CountContentForBiomeOrGlobal(
            DimensionAuthoringPreviewSummary summary,
            DimensionAuthoringContentSummaryKind kind,
            string biomeId)
        {
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries = summary.ContentEntries;
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (entry.Kind != kind)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(biomeId) &&
                    !string.IsNullOrEmpty(entry.BiomeId) &&
                    entry.BiomeId != biomeId)
                {
                    continue;
                }

                count += entry.Count < 1 ? 1 : entry.Count;
            }

            return count;
        }

        private static void AddEntry(
            List<DimensionAuthoringReadinessEntry> entries,
            DimensionAuthoringReadinessCategory category,
            string biomeId,
            DimensionAuthoringReadinessState state,
            string code,
            string message,
            int presentCount,
            int expectedCount,
            string dimensionId)
        {
            entries.Add(
                new DimensionAuthoringReadinessEntry(
                    category,
                    state,
                    dimensionId,
                    biomeId,
                    code,
                    message,
                    presentCount,
                    expectedCount));
        }

        private static void CountStates(
            IReadOnlyList<DimensionAuthoringReadinessEntry> entries,
            out int readyCount,
            out int partialCount,
            out int missingCount,
            out int blockedCount)
        {
            readyCount = 0;
            partialCount = 0;
            missingCount = 0;
            blockedCount = 0;

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessState state = entries[i].State;
                if (state == DimensionAuthoringReadinessState.Ready)
                {
                    readyCount++;
                }
                else if (state == DimensionAuthoringReadinessState.Partial)
                {
                    partialCount++;
                }
                else if (state == DimensionAuthoringReadinessState.Missing)
                {
                    missingCount++;
                }
                else if (state == DimensionAuthoringReadinessState.Blocked)
                {
                    blockedCount++;
                }
            }
        }
    }
}
