using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerSectionKind
    {
        Overview = 0,
        Dimension = 1,
        Layout = 2,
        Biomes = 3,
        Terrain = 4,
        Generation = 5,
        Scenes = 6,
        Resources = 7,
        Spawns = 8,
        Export = 9,
        Diagnostics = 10,
        Portals = 11,
        Tilesets = 12,
        Nature = 13,
        WorldRules = 14
    }

    public readonly struct DimensionTemplateCustomizerSectionItem
    {
        public readonly DimensionTemplateCustomizerSectionKind Kind;
        public readonly DimensionAuthoringReadinessState State;
        public readonly string SectionId;
        public readonly string DisplayName;
        public readonly string Message;
        public readonly string PrimaryActionId;
        public readonly int ContentCount;
        public readonly int IssueCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly int BlockingCount;
        public readonly int Priority;
        public readonly bool Recommended;

        public DimensionTemplateCustomizerSectionItem(
            DimensionTemplateCustomizerSectionKind kind,
            DimensionAuthoringReadinessState state,
            string sectionId,
            string displayName,
            string message,
            string primaryActionId,
            int contentCount,
            int issueCount,
            int errorCount,
            int warningCount,
            int blockingCount,
            int priority,
            bool recommended)
        {
            Kind = kind;
            State = state;
            SectionId = sectionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Message = message ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            ContentCount = contentCount < 0 ? 0 : contentCount;
            IssueCount = issueCount < 0 ? 0 : issueCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            BlockingCount = blockingCount < 0 ? 0 : blockingCount;
            Priority = priority < 0 ? 0 : priority;
            Recommended = recommended;
        }
    }

    public sealed class DimensionTemplateCustomizerNavigationModel
    {
        public DimensionTemplateCustomizerNavigationModel(
            string dimensionId,
            string displayName,
            string activeSectionId,
            string primaryActionId,
            int sectionCount,
            int recommendedSectionCount,
            int blockedSectionCount,
            int warningSectionCount,
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections)
        {
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ActiveSectionId = activeSectionId ?? string.Empty;
            PrimaryActionId = primaryActionId ?? string.Empty;
            SectionCount = sectionCount < 0 ? 0 : sectionCount;
            RecommendedSectionCount = recommendedSectionCount < 0 ? 0 : recommendedSectionCount;
            BlockedSectionCount = blockedSectionCount < 0 ? 0 : blockedSectionCount;
            WarningSectionCount = warningSectionCount < 0 ? 0 : warningSectionCount;
            Sections = sections ?? new List<DimensionTemplateCustomizerSectionItem>();
        }

        public string DimensionId { get; private set; }

        public string DisplayName { get; private set; }

        public string ActiveSectionId { get; private set; }

        public string PrimaryActionId { get; private set; }

        public int SectionCount { get; private set; }

        public int RecommendedSectionCount { get; private set; }

        public int BlockedSectionCount { get; private set; }

        public int WarningSectionCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerSectionItem> Sections { get; private set; }
    }

    public static class DimensionTemplateCustomizerNavigationUtility
    {
        public static DimensionTemplateCustomizerNavigationModel Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateCustomizerSessionReport session =
                workspace == null ? null : workspace.SessionReport;
            if (session == null || !session.HasWorkspace)
            {
                return CreateMissingNavigation();
            }

            List<DimensionTemplateCustomizerSectionItem> sections =
                new List<DimensionTemplateCustomizerSectionItem>();
            DimensionAuthoringPreviewSummary preview = workspace.Preview;
            DimensionAuthoringReadinessReport readiness = workspace.Readiness;
            int authoredScenes = CountAuthoredScenes(workspace);
            int authoredResources = CountAuthoredResources(workspace);
            int authoredSpawns = CountAuthoredSpawns(workspace);

            AddOverviewSection(sections, session);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Dimension,
                DimensionAuthoringReadinessCategory.Dimension,
                "dimension",
                "Dimension",
                CountContent(preview, DimensionAuthoringContentSummaryKind.Dimension),
                workspace);
            AddPortalSection(sections, workspace);
            AddTilesetsSection(sections, workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Layout,
                DimensionAuthoringReadinessCategory.Layout,
                "layout",
                "Layout",
                CountContent(preview, DimensionAuthoringContentSummaryKind.LayoutTemplate),
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Biomes,
                DimensionAuthoringReadinessCategory.Biome,
                "biomes",
                "Biomes",
                session.BiomeCount,
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Terrain,
                DimensionAuthoringReadinessCategory.Terrain,
                "terrain",
                "Terrain",
                CountTerrainContent(preview),
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Generation,
                DimensionAuthoringReadinessCategory.GenerationPasses,
                "generation",
                "Generation",
                CountGenerationContent(preview),
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Scenes,
                DimensionAuthoringReadinessCategory.Scenes,
                "scenes",
                "Scenes",
                authoredScenes,
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Resources,
                DimensionAuthoringReadinessCategory.Resources,
                "resources",
                "Resources",
                authoredResources,
                workspace);
            AddReadinessSection(
                sections,
                DimensionTemplateCustomizerSectionKind.Spawns,
                DimensionAuthoringReadinessCategory.Spawns,
                "spawns",
                "Spawns",
                authoredSpawns,
                workspace);
            AddNatureSection(sections, workspace);
            AddWorldRulesSection(sections, workspace);
            AddExportSection(sections, session, authoredScenes, authoredResources, authoredSpawns);
            AddDiagnosticsSection(sections, session, readiness);

            sections.Sort(CompareSections);

            string activeSectionId = ResolveActiveSection(sections, session);
            int recommendedSections;
            int blockedSections;
            int warningSections;
            CountSections(
                sections,
                out recommendedSections,
                out blockedSections,
                out warningSections);

            return new DimensionTemplateCustomizerNavigationModel(
                session.DimensionId,
                session.DisplayName,
                activeSectionId,
                session.PrimaryActionId,
                sections.Count,
                recommendedSections,
                blockedSections,
                warningSections,
                sections);
        }

        private static DimensionTemplateCustomizerNavigationModel CreateMissingNavigation()
        {
            List<DimensionTemplateCustomizerSectionItem> sections =
                new List<DimensionTemplateCustomizerSectionItem>
                {
                    new DimensionTemplateCustomizerSectionItem(
                        DimensionTemplateCustomizerSectionKind.Overview,
                        DimensionAuthoringReadinessState.Blocked,
                        "overview",
                        "Overview",
                        "Select or create a Dimension Asset before opening the customizer.",
                        "select-dimension-template",
                        0,
                        1,
                        1,
                        0,
                        1,
                        0,
                        true)
                };

            return new DimensionTemplateCustomizerNavigationModel(
                string.Empty,
                string.Empty,
                "overview",
                "select-dimension-template",
                sections.Count,
                1,
                1,
                0,
                sections);
        }

        private static void AddOverviewSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateCustomizerSessionReport session)
        {
            int hardBlockers = session.BlockingManifestExportCount;
            DimensionAuthoringReadinessState overviewState =
                hardBlockers > 0
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready;

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Overview,
                overviewState,
                "overview",
                "Overview",
                "Dashboard view of the current dimension asset.",
                session.PrimaryActionId,
                session.BiomeCount,
                session.OperationCount,
                session.ErrorCount,
                session.WarningCount,
                session.BlockingManifestExportCount,
                0,
                false));
        }

        private static void AddPortalSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset dimension =
                workspace == null || workspace.Graph == null
                    ? null
                    : workspace.Graph.Dimension;
            bool customProfile = dimension != null && dimension.PortalVisualProfile != null;
            string message = customProfile
                ? "A custom portal visual profile is assigned."
                : "Using the vanilla portal visual defaults. Create a profile to customize individual layers.";

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Portals,
                DimensionAuthoringReadinessState.Ready,
                "portals",
                "Portal Studio",
                message,
                customProfile ? "inspect-portals" : "configure-portals",
                customProfile ? 1 : 0,
                0,
                0,
                0,
                0,
                19,
                false));
        }

        private static void AddTilesetsSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset dimension =
                workspace == null || workspace.Graph == null
                    ? null
                    : workspace.Graph.Dimension;
            DimensionTilesetAsset[] tilesets =
                dimension == null ? new DimensionTilesetAsset[0] : dimension.Tilesets;
            int tilesetCount = 0;
            for (int i = 0; i < tilesets.Length; i++)
            {
                if (tilesets[i] != null)
                {
                    tilesetCount++;
                }
            }

            string message = tilesetCount > 0
                ? tilesetCount + (tilesetCount == 1 ? " custom tileset." : " custom tilesets.")
                : "Create a tileset from a dirt-layout sheet to get placeable custom blocks.";

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Tilesets,
                DimensionAuthoringReadinessState.Ready,
                "tilesets",
                "Tileset Studio",
                message,
                "configure-tilesets",
                tilesetCount,
                0,
                0,
                0,
                0,
                20,
                false));
        }

        /// <summary>
        /// Gardening's own row in the model. Without it the rail's Gardening Studio entry
        /// resolved Missing forever and could never show its check, whatever was authored.
        /// </summary>
        private static void AddNatureSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset dimension =
                workspace == null || workspace.Graph == null
                    ? null
                    : workspace.Graph.Dimension;
            int plantCount = CountNonNull(dimension == null ? null : dimension.GlobalPlants);

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Nature,
                plantCount > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Missing,
                "nature",
                "Gardening Studio",
                plantCount > 0
                    ? plantCount + (plantCount == 1 ? " plant." : " plants.")
                    : "Nothing grows here yet.",
                "configure-nature",
                plantCount,
                0,
                0,
                0,
                0,
                21,
                false));
        }

        /// <summary>World Generation's own row, for the same reachability reason.</summary>
        private static void AddWorldRulesSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset dimension =
                workspace == null || workspace.Graph == null
                    ? null
                    : workspace.Graph.Dimension;
            int setupCount = CountNonNull(dimension == null ? null : dimension.GlobalGameSetups);

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.WorldRules,
                setupCount > 0
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Missing,
                "worldrules",
                "World Generation",
                setupCount > 0
                    ? setupCount + (setupCount == 1 ? " rule set." : " rule sets.")
                    : "The world generates with the framework's defaults.",
                "configure-worldrules",
                setupCount,
                0,
                0,
                0,
                0,
                22,
                false));
        }

        private static int CountNonNull<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddReadinessSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateCustomizerSectionKind kind,
            DimensionAuthoringReadinessCategory category,
            string sectionId,
            string displayName,
            int contentCount,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionAuthoringReadinessReport readiness = workspace.Readiness;
            DimensionAuthoringReadinessState state;
            string message;
            int issueCount;
            int errorCount;
            int warningCount;
            int blockingCount;
            ResolveReadinessForCategory(
                readiness,
                category,
                out state,
                out message,
                out issueCount,
                out errorCount,
                out warningCount,
                out blockingCount);

            if (RequiresUserAuthoredContent(kind) &&
                contentCount <= 0)
            {
                state = DimensionAuthoringReadinessState.Missing;
                message = EmptySectionMessage(kind);
                issueCount = 0;
                errorCount = 0;
                warningCount = 0;
                blockingCount = 0;
            }

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                kind,
                state,
                sectionId,
                displayName,
                message,
                ResolvePrimaryActionId(kind, state),
                contentCount,
                issueCount,
                errorCount,
                warningCount,
                blockingCount,
                ResolvePriority(kind, state, blockingCount, warningCount),
                ShouldRecommendSection(kind, state, contentCount, blockingCount)));
        }

        private static void AddExportSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateCustomizerSessionReport session,
            int authoredScenes,
            int authoredResources,
            int authoredSpawns)
        {
            int authoredDimensionContent =
                ClampCount(authoredScenes) +
                ClampCount(authoredResources) +
                ClampCount(authoredSpawns);
            DimensionAuthoringReadinessState state =
                session.BlockingManifestExportCount > 0
                    ? DimensionAuthoringReadinessState.Blocked
                    : session.ReadyForApply
                        ? DimensionAuthoringReadinessState.Ready
                        : session.ReadyForValidation
                            ? DimensionAuthoringReadinessState.Partial
                            : DimensionAuthoringReadinessState.Missing;

            int blockerCount = session.BlockingManifestExportCount;
            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Export,
                state,
                "export",
                "Export",
                session.ManifestExportPreview.Message,
                session.ReadyForApply ? "apply-manifest" : "validate-manifest",
                authoredDimensionContent,
                session.ManifestExportPreview.AuthoringOperationCount,
                session.ManifestExportPreview.ErrorCount,
                session.ManifestExportPreview.WarningCount,
                blockerCount,
                ResolvePriority(DimensionTemplateCustomizerSectionKind.Export, state, blockerCount, session.ManifestExportPreview.WarningCount),
                state == DimensionAuthoringReadinessState.Blocked));
        }

        private static bool RequiresUserAuthoredContent(
            DimensionTemplateCustomizerSectionKind kind)
        {
            return kind == DimensionTemplateCustomizerSectionKind.Scenes ||
                kind == DimensionTemplateCustomizerSectionKind.Resources ||
                kind == DimensionTemplateCustomizerSectionKind.Spawns;
        }

        private static bool ShouldRecommendSection(
            DimensionTemplateCustomizerSectionKind kind,
            DimensionAuthoringReadinessState state,
            int contentCount,
            int blockingCount)
        {
            if (blockingCount > 0 || state == DimensionAuthoringReadinessState.Blocked)
            {
                return true;
            }

            if (RequiresUserAuthoredContent(kind) && contentCount <= 0)
            {
                return false;
            }

            return state == DimensionAuthoringReadinessState.Missing;
        }

        private static string EmptySectionMessage(
            DimensionTemplateCustomizerSectionKind kind)
        {
            if (kind == DimensionTemplateCustomizerSectionKind.Scenes)
            {
                return "No scenes or structures have been added yet.";
            }

            if (kind == DimensionTemplateCustomizerSectionKind.Resources)
            {
                return "No biome resources or obtainable content have been added yet.";
            }

            if (kind == DimensionTemplateCustomizerSectionKind.Spawns)
            {
                return "No animals, critters, mobs, bosses, or spawn rules have been added yet.";
            }

            return "This section has not been configured yet.";
        }

        private static void AddDiagnosticsSection(
            List<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateCustomizerSessionReport session,
            DimensionAuthoringReadinessReport readiness)
        {
            int issueCount = session.OperationCount + session.AdvisoryMessageCount + session.ExportNoteCount;
            int blockers = session.BlockingManifestExportCount;
            int warnings = session.WarningCount;
            DimensionAuthoringReadinessState state =
                blockers > 0
                    ? DimensionAuthoringReadinessState.Blocked
                    : warnings > 0 || issueCount > 0
                        ? DimensionAuthoringReadinessState.Partial
                        : DimensionAuthoringReadinessState.Ready;

            string message = readiness.Message;
            if (string.IsNullOrEmpty(message))
            {
                message = blockers > 0
                    ? "Diagnostics found blocking authoring issues."
                    : warnings > 0
                        ? "Diagnostics found warnings to review."
                        : "No blocking diagnostics are currently reported.";
            }

            sections.Add(new DimensionTemplateCustomizerSectionItem(
                DimensionTemplateCustomizerSectionKind.Diagnostics,
                state,
                "diagnostics",
                "Diagnostics",
                message,
                "review-diagnostics",
                issueCount,
                issueCount,
                session.ErrorCount,
                warnings,
                blockers,
                ResolvePriority(DimensionTemplateCustomizerSectionKind.Diagnostics, state, blockers, warnings),
                blockers > 0));
        }

        private static void ResolveReadinessForCategory(
            DimensionAuthoringReadinessReport readiness,
            DimensionAuthoringReadinessCategory category,
            out DimensionAuthoringReadinessState state,
            out string message,
            out int issueCount,
            out int errorCount,
            out int warningCount,
            out int blockingCount)
        {
            state = DimensionAuthoringReadinessState.Ready;
            message = string.Empty;
            issueCount = 0;
            errorCount = 0;
            warningCount = 0;
            blockingCount = 0;

            IReadOnlyList<DimensionAuthoringReadinessEntry> entries = readiness.Entries;
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringReadinessEntry entry = entries[i];
                if (entry.Category != category)
                {
                    continue;
                }

                state = PickWorstState(state, entry.State);
                if (string.IsNullOrEmpty(message) && !string.IsNullOrEmpty(entry.Message))
                {
                    message = entry.Message;
                }

                if (entry.State == DimensionAuthoringReadinessState.Blocked)
                {
                    issueCount++;
                    errorCount++;
                    blockingCount++;
                }
                else if (entry.State == DimensionAuthoringReadinessState.Missing)
                {
                    issueCount++;
                    warningCount++;
                }
                else if (entry.State == DimensionAuthoringReadinessState.Partial)
                {
                    issueCount++;
                    warningCount++;
                }
            }

            if (string.IsNullOrEmpty(message))
            {
                message = state == DimensionAuthoringReadinessState.Ready
                    ? "Ready."
                    : "Review this customizer section.";
            }
        }

        private static DimensionAuthoringReadinessState PickWorstState(
            DimensionAuthoringReadinessState current,
            DimensionAuthoringReadinessState candidate)
        {
            if (candidate == DimensionAuthoringReadinessState.Blocked ||
                current == DimensionAuthoringReadinessState.Blocked)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            if (candidate == DimensionAuthoringReadinessState.Missing ||
                current == DimensionAuthoringReadinessState.Missing)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            if (candidate == DimensionAuthoringReadinessState.Partial ||
                current == DimensionAuthoringReadinessState.Partial)
            {
                return DimensionAuthoringReadinessState.Partial;
            }

            return DimensionAuthoringReadinessState.Ready;
        }

        private static DimensionTemplateAsset GetWorkspaceTemplate(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            return graph == null ? null : graph.Dimension;
        }

        private static int CountAuthoredScenes(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset template = GetWorkspaceTemplate(workspace);
            if (template == null)
            {
                return 0;
            }

            int count = CountArray(template.GlobalScenes) +
                CountSceneContent(template.GlobalScenes);
            BiomeTemplateAsset[] biomes = template.Biomes;
            for (int i = 0; i < biomes.Length; i++)
            {
                BiomeTemplateAsset biome = biomes[i];
                if (biome != null)
                {
                    count += CountArray(biome.ScenePool) +
                        CountSceneContent(biome.ScenePool);
                }
            }

            return count;
        }

        private static int CountAuthoredResources(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset template = GetWorkspaceTemplate(workspace);
            if (template == null)
            {
                return 0;
            }

            return CountArray(template.GlobalItems) +
                CountArray(template.GlobalRecipes) +
                CountArray(template.GlobalWorkbenches) +
                CountArray(template.GlobalLootTables);
        }

        private static int CountAuthoredSpawns(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset template = GetWorkspaceTemplate(workspace);
            if (template == null)
            {
                return 0;
            }

            return CountArray(template.GlobalAnimals) +
                CountArray(template.GlobalCritters) +
                CountArray(template.GlobalMobs) +
                CountArray(template.GlobalBosses);
        }

        private static int CountSceneContent(SceneTemplateAsset[] scenes)
        {
            if (scenes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                SceneTemplateAsset scene = scenes[i];
                if (scene != null)
                {
                    count += CountArray(scene.Triggers);
                }
            }

            return count;
        }

        private static int CountArray<T>(T[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                object value = values[i];
                if (value != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ClampCount(int value)
        {
            return value < 0 ? 0 : value;
        }

        private static int CountContent(
            DimensionAuthoringPreviewSummary preview,
            DimensionAuthoringContentSummaryKind kind)
        {
            IReadOnlyList<DimensionAuthoringContentSummaryEntry> entries = preview.ContentEntries;
            if (entries == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                DimensionAuthoringContentSummaryEntry entry = entries[i];
                if (entry.Kind == kind)
                {
                    count += entry.Count < 1 ? 1 : entry.Count;
                }
            }

            return count;
        }

        private static int CountTerrainContent(
            DimensionAuthoringPreviewSummary preview)
        {
            return
                CountContent(preview, DimensionAuthoringContentSummaryKind.SemanticFloorObject) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SemanticWallObject) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SemanticOreObject) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SemanticWaterObject) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.BiomePaletteEntry);
        }

        private static int CountGenerationContent(
            DimensionAuthoringPreviewSummary preview)
        {
            return
                CountContent(preview, DimensionAuthoringContentSummaryKind.GenerationPass) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.GenerationTable) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.GenerationTableEntry);
        }

        private static string ResolvePrimaryActionId(
            DimensionTemplateCustomizerSectionKind kind,
            DimensionAuthoringReadinessState state)
        {
            if (state == DimensionAuthoringReadinessState.Ready)
            {
                return "inspect-" + kind.ToString().ToLowerInvariant();
            }

            if (state == DimensionAuthoringReadinessState.Blocked)
            {
                return "fix-" + kind.ToString().ToLowerInvariant();
            }

            return "configure-" + kind.ToString().ToLowerInvariant();
        }

        private static int ResolvePriority(
            DimensionTemplateCustomizerSectionKind kind,
            DimensionAuthoringReadinessState state,
            int blockingCount,
            int warningCount)
        {
            int priority = (int)kind * 10;
            if (blockingCount > 0 || state == DimensionAuthoringReadinessState.Blocked)
            {
                return priority;
            }

            if (state == DimensionAuthoringReadinessState.Missing)
            {
                return priority + 2;
            }

            if (warningCount > 0 || state == DimensionAuthoringReadinessState.Partial)
            {
                return priority + 4;
            }

            return priority + 8;
        }

        private static string ResolveActiveSection(
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections,
            DimensionTemplateCustomizerSessionReport session)
        {
            if (HasSection(sections, "overview"))
            {
                return "overview";
            }

            if (sections != null && sections.Count > 0)
            {
                return sections[0].SectionId;
            }

            return "overview";
        }

        private static bool HasSection(
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections,
            string sectionId)
        {
            if (sections == null || string.IsNullOrEmpty(sectionId))
            {
                return false;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].SectionId == sectionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CountSections(
            IReadOnlyList<DimensionTemplateCustomizerSectionItem> sections,
            out int recommended,
            out int blocked,
            out int warnings)
        {
            recommended = 0;
            blocked = 0;
            warnings = 0;
            if (sections == null)
            {
                return;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                DimensionTemplateCustomizerSectionItem item = sections[i];
                if (item.Recommended)
                {
                    recommended++;
                }

                if (item.BlockingCount > 0 || item.State == DimensionAuthoringReadinessState.Blocked)
                {
                    blocked++;
                }

                if (item.WarningCount > 0 || item.State == DimensionAuthoringReadinessState.Partial)
                {
                    warnings++;
                }
            }
        }

        private static int CompareSections(
            DimensionTemplateCustomizerSectionItem left,
            DimensionTemplateCustomizerSectionItem right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return left.Kind.CompareTo(right.Kind);
        }
    }
}
