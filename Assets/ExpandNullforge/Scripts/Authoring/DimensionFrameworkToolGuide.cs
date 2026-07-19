using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionFrameworkToolModule
    {
        public DimensionFrameworkToolModule(
            string moduleId,
            string title,
            string category,
            DimensionAuthoringReadinessState state,
            bool coreWorkflow,
            string summary,
            string modderNeed,
            string nextAction,
            string actionId)
        {
            ModuleId = moduleId ?? string.Empty;
            Title = title ?? string.Empty;
            Category = category ?? string.Empty;
            State = state;
            CoreWorkflow = coreWorkflow;
            Summary = summary ?? string.Empty;
            ModderNeed = modderNeed ?? string.Empty;
            NextAction = nextAction ?? string.Empty;
            ActionId = actionId ?? string.Empty;
        }

        public string ModuleId { get; private set; }

        public string Title { get; private set; }

        public string Category { get; private set; }

        public DimensionAuthoringReadinessState State { get; private set; }

        public bool CoreWorkflow { get; private set; }

        public string Summary { get; private set; }

        public string ModderNeed { get; private set; }

        public string NextAction { get; private set; }

        public string ActionId { get; private set; }
    }

    public sealed class DimensionFrameworkToolGuide
    {
        public DimensionFrameworkToolGuide(
            DimensionAuthoringReadinessState state,
            string code,
            string message,
            int readyCount,
            int partialCount,
            int missingCount,
            int blockedCount,
            IReadOnlyList<DimensionFrameworkToolModule> modules)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            PartialCount = partialCount < 0 ? 0 : partialCount;
            MissingCount = missingCount < 0 ? 0 : missingCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            Modules = modules ?? new List<DimensionFrameworkToolModule>();
        }

        public DimensionAuthoringReadinessState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int ReadyCount { get; private set; }

        public int PartialCount { get; private set; }

        public int MissingCount { get; private set; }

        public int BlockedCount { get; private set; }

        public IReadOnlyList<DimensionFrameworkToolModule> Modules { get; private set; }
    }

    public static class DimensionFrameworkToolGuideUtility
    {
        public static DimensionFrameworkToolGuide Build(DimensionTemplateAuthoringWorkspace workspace)
        {
            List<DimensionFrameworkToolModule> modules =
                new List<DimensionFrameworkToolModule>();

            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            DimensionTemplateStarterAssessment assessment =
                workspace == null ? null : workspace.Assessment;
            DimensionTemplateManifestExportPreview manifestPreview =
                workspace == null
                    ? default(DimensionTemplateManifestExportPreview)
                    : workspace.ManifestExportPreview;
            DimensionVisualAssetValidationReport visualReport =
                workspace == null ? null : workspace.VisualAssetValidation;
            DimensionFrameworkExtractionReadinessReport extractionReport =
                workspace == null ? null : workspace.ExtractionReadiness;

            AddModule(
                modules,
                "start-template",
                "Create a dimension",
                "Foundation",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : DimensionAuthoringReadinessState.Ready,
                true,
                "Every dimension starts with a Dimension Asset. It becomes the source of truth for the world, its content pack, and the editor workflow.",
                "You need to create the Dimension Asset, name the dimension, choose a safe generated-asset folder, and seed the first biome.",
                workspace == null
                    ? "Open Create Dimension Asset. After it creates the asset, select it in the Dimension Asset field above."
                    : "Keep this Dimension Asset selected while you build the rest of the dimension.",
                "open-new-dimension-wizard");

            AddModule(
                modules,
                "dimension-domain",
                "Dimension Identity",
                "Foundation",
                graph != null && graph.Dimension != null
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Missing,
                true,
                "Give the dimension a stable identity and coordinate domain before any content is generated.",
                "You need a dimension ID, display name, owner pack, local origin, playable bounds, coordinate shell, and absolute reservation.",
                graph != null && graph.Dimension != null
                    ? "Review local/absolute coordinate previews and bounds warnings."
                    : "Create or select a Dimension Asset before editing the domain.",
                "edit-identity-coordinates");

            AddModule(
                modules,
                "access-and-travel",
                "Dimension Access",
                "Travel",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : DimensionAuthoringReadinessState.Partial,
                true,
                "Define how players enter the dimension, how they return, and what happens when travel cannot complete.",
                "You need to choose placed portals, inventory-use items, generated entrances, return anchors, recipes, unlocks, cooldowns, and failure feedback.",
                "Create reusable access rules instead of hardcoding a single test portal.",
                "open-travel-contract");

            AddModule(
                modules,
                "layout-and-bounds",
                "Dimension Layout",
                "World Shape",
                ResolveLayoutState(workspace),
                true,
                "Shape the playable space and decide how biomes occupy it.",
                "You need a layout preset or custom mask, then you should preview bounds, biome ownership, coordinate shells, and placement conflicts.",
                ResolveLayoutNext(workspace),
                "open-layout-designer");

            AddModule(
                modules,
                "biome-environment",
                "Environment Rules",
                "Biome Setup",
                ResolveBiomeState(workspace, assessment),
                true,
                "Define what the dimension feels like at ground level.",
                "You need terrain, walls, liquids, destructibility, density, fog, lighting, ambience, music, palettes, and generation rules for each biome.",
                ResolveBiomeNext(workspace, assessment),
                "open-biome-environment");

            AddModule(
                modules,
                "scenes-and-structures",
                "Scenes & Structures",
                "World Content",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : ResolveScenesState(manifestPreview),
                true,
                "Add authored places that make the biome feel intentional instead of just generated terrain.",
                "You need scene pools, exact placements, structures, ancient waypoints, locked entrances, chest pools, boss arenas, dungeon layers, and collision warnings.",
                "Create scene authoring cards with automatic and exact-coordinate placement modes.",
                "open-content-placement");

            AddModule(
                modules,
                "mobs-bosses-npcs",
                "Biome Habitat",
                "Ecology",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : ResolveSpawnsState(manifestPreview),
                true,
                "Decide what lives in the dimension and how encounters appear.",
                "You need enemy tables, passive animals, boss triggers, summoning items, NPC conditions, encounter zones, drops, sounds, arenas, and multiplayer-safe state.",
                "Split the habitat setup into mobs, animals, bosses, and NPC cards.",
                "open-spawn-encounter-planner");

            AddModule(
                modules,
                "resources-items-loot",
                "Biome Progression",
                "Rewards",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : ResolveResourcesState(manifestPreview),
                true,
                "Define what the biome rewards and how those rewards enter the world.",
                "You need ores, bars, blocks, fish, valuables, materials, mob drops, boss drops, scene exclusives, chest pools, and vanilla-item references.",
                "Link every item source back to a mob, scene, generated node, fishing table, or recipe chain.",
                "open-resource-loot-planner");

            AddModule(
                modules,
                "crafting-workbenches",
                "Progression Content",
                "Progression",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : DimensionAuthoringReadinessState.Partial,
                true,
                "Turn biome rewards into craftable progression.",
                "You need recipes, quantities, vanilla or custom workbenches, category icons, tabs, unlock conditions, craftability flags, and generated-only exceptions.",
                "Create a recipe/workbench authoring page before export is considered complete.",
                "open-crafting-progression");

            AddModule(
                modules,
                "visual-asset-readiness",
                "Visual Implementation",
                "Assets",
                ResolveVisualState(visualReport),
                true,
                "Make sure the dimension looks like it belongs in Core Keeper.",
                "You need sprites, SpriteObject material compatibility, lighting, shadows, inventory icons, map icons, vanilla references, and custom UGC rendering checks.",
                ResolveVisualNext(visualReport),
                "open-visual-preflight");

            AddModule(
                modules,
                "generation-validation",
                "Compatibility Verification",
                "Validation",
                ResolveGenerationState(workspace, manifestPreview),
                true,
                "Check whether the authored dimension can generate safely and coexist with other content.",
                "You need dry-run validation for generation passes, weighted tables, provider budgets, exact placements, overlaps, invalid assets, missing providers, and heavy rules.",
                ResolveGenerationNext(workspace, manifestPreview),
                "open-generation-planner");

            AddModule(
                modules,
                "manifest-export",
                "Exporting the Manifest",
                "Publish",
                ResolveManifestState(manifestPreview),
                true,
                "Finalize the authored dimension into the manifest consumed by runtime systems.",
                "You need dependency metadata, ownership checks, schema versioning, migration notes, runtime-readiness gates, and explicit export confirmation.",
                ResolveManifestNext(manifestPreview),
                "open-manifest-export");

            AddModule(
                modules,
                "framework-extraction",
                "Framework extraction readiness",
                "Framework",
                extractionReport == null
                    ? DimensionAuthoringReadinessState.Missing
                    : extractionReport.State,
                true,
                "Keep the reusable API separate from the Nullforge validation fixture.",
                "Track names, dependencies, test-only portals, generated assets, hardcoded IDs, and package-rename blockers.",
                extractionReport == null
                    ? "Run the extraction-readiness analyzer once a template is selected."
                    : extractionReport.Message,
                "open-extraction-readiness");

            AddModule(
                modules,
                "diagnostics-and-test-fixtures",
                "Diagnostics and proof fixtures",
                "Proof",
                workspace == null
                    ? DimensionAuthoringReadinessState.Missing
                    : DimensionAuthoringReadinessState.Partial,
                false,
                "Keep compile/load probes, portal proof loops, coordinate previews, and runtime debug aids out of the normal authoring flow.",
                "Use diagnostics to prove the framework without forcing every dimension mod to inherit the Nullforge test content.",
                "Keep fixture tools clearly labelled until the framework is extracted.",
                "open-diagnostics");

            return BuildGuide(modules);
        }

        private static DimensionAuthoringReadinessState ResolveLayoutState(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            if (workspace == null || workspace.Graph == null || workspace.Graph.Layout == null)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            return workspace.PreviewCanvas.ItemCount > 0
                ? DimensionAuthoringReadinessState.Ready
                : DimensionAuthoringReadinessState.Partial;
        }

        private static string ResolveLayoutNext(DimensionTemplateAuthoringWorkspace workspace)
        {
            if (workspace == null || workspace.Graph == null || workspace.Graph.Layout == null)
            {
                return "Create a layout before adding biome placement rules.";
            }

            return "Use the layout preview to verify playable bounds, shell, and biome coverage.";
        }

        private static DimensionAuthoringReadinessState ResolveBiomeState(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateStarterAssessment assessment)
        {
            if (workspace == null || Count(workspace.BiomeOverviews) == 0)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            return assessment != null &&
                assessment.HasGenerationPass &&
                assessment.HasGenerationTable
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial;
        }

        private static string ResolveBiomeNext(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateStarterAssessment assessment)
        {
            int biomeCount = workspace == null ? 0 : Count(workspace.BiomeOverviews);
            if (biomeCount == 0)
            {
                return "Create at least one biome, palette, and environment profile.";
            }

            if (assessment == null ||
                !assessment.HasGenerationPass ||
                !assessment.HasGenerationTable)
            {
                return "Finish generation passes and tables for each authored biome.";
            }

            return "Expand biome content from terrain into scenes, resources, and spawns.";
        }

        private static DimensionAuthoringReadinessState ResolveScenesState(
            DimensionTemplateManifestExportPreview preview)
        {
            return preview.SceneCount > 0 ||
                preview.SceneTemplateCount > 0 ||
                preview.AuthoredSceneContentCount > 0
                ? DimensionAuthoringReadinessState.Ready
                : DimensionAuthoringReadinessState.Partial;
        }

        private static DimensionAuthoringReadinessState ResolveSpawnsState(
            DimensionTemplateManifestExportPreview preview)
        {
            return preview.SpawnRuleCount > 0 ||
                preview.AuthoredSpawnableContentCount > 0
                ? DimensionAuthoringReadinessState.Ready
                : DimensionAuthoringReadinessState.Partial;
        }

        private static DimensionAuthoringReadinessState ResolveResourcesState(
            DimensionTemplateManifestExportPreview preview)
        {
            return preview.ResourceNodeCount > 0 ||
                preview.AuthoredResourceContentCount > 0
                ? DimensionAuthoringReadinessState.Ready
                : DimensionAuthoringReadinessState.Partial;
        }

        private static DimensionAuthoringReadinessState ResolveVisualState(
            DimensionVisualAssetValidationReport report)
        {
            if (report == null)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            if (report.BlockedCount > 0)
            {
                return DimensionAuthoringReadinessState.Blocked;
            }

            return report.AdvisoryCount > 0 || report.UnknownSourceCount > 0
                ? DimensionAuthoringReadinessState.Partial
                : DimensionAuthoringReadinessState.Ready;
        }

        private static string ResolveVisualNext(DimensionVisualAssetValidationReport report)
        {
            if (report == null)
            {
                return "Select a Dimension Asset to analyze vanilla and custom visual references.";
            }

            if (report.BlockedCount > 0)
            {
                return "Fix blocked visual references before runtime testing.";
            }

            if (report.AdvisoryCount > 0 || report.UnknownSourceCount > 0)
            {
                return "Review advisory and unknown-source rows before export.";
            }

            return "Keep using vanilla references where possible and custom UGC assets where needed.";
        }

        private static DimensionAuthoringReadinessState ResolveGenerationState(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview preview)
        {
            if (workspace == null)
            {
                return DimensionAuthoringReadinessState.Missing;
            }

            if (preview.ReadyForRuntimeGeneration)
            {
                return DimensionAuthoringReadinessState.Ready;
            }

            return preview.ReadyForManifestExport
                ? DimensionAuthoringReadinessState.Partial
                : DimensionAuthoringReadinessState.Blocked;
        }

        private static string ResolveGenerationNext(
            DimensionTemplateAuthoringWorkspace workspace,
            DimensionTemplateManifestExportPreview preview)
        {
            if (workspace == null)
            {
                return "Select a Dimension Asset before reviewing generation.";
            }

            if (!preview.ReadyForRuntimeGeneration)
            {
                return "Resolve runtime-generation blockers and budget warnings.";
            }

            return "Use dry-run validation before applying/exporting the manifest.";
        }

        private static DimensionAuthoringReadinessState ResolveManifestState(
            DimensionTemplateManifestExportPreview preview)
        {
            if (preview.ReadyForManifestExport)
            {
                return DimensionAuthoringReadinessState.Ready;
            }

            return preview.ManifestBuilt
                ? DimensionAuthoringReadinessState.Blocked
                : DimensionAuthoringReadinessState.Missing;
        }

        private static string ResolveManifestNext(DimensionTemplateManifestExportPreview preview)
        {
            if (preview.ReadyForManifestExport)
            {
                return "Export only after you have reviewed the generated changes.";
            }

            if (preview.ManifestBuilt)
            {
                return "Open diagnostics and clear manifest blockers.";
            }

            return "Build a manifest preview from a complete template first.";
        }

        private static DimensionFrameworkToolGuide BuildGuide(
            IReadOnlyList<DimensionFrameworkToolModule> modules)
        {
            int ready = 0;
            int partial = 0;
            int missing = 0;
            int blocked = 0;

            if (modules != null)
            {
                for (int i = 0; i < modules.Count; i++)
                {
                    DimensionFrameworkToolModule module = modules[i];
                    if (module == null)
                    {
                        continue;
                    }

                    if (module.State == DimensionAuthoringReadinessState.Ready)
                    {
                        ready++;
                    }
                    else if (module.State == DimensionAuthoringReadinessState.Partial)
                    {
                        partial++;
                    }
                    else if (module.State == DimensionAuthoringReadinessState.Missing)
                    {
                        missing++;
                    }
                    else if (module.State == DimensionAuthoringReadinessState.Blocked)
                    {
                        blocked++;
                    }
                }
            }

            DimensionAuthoringReadinessState state = blocked > 0
                ? DimensionAuthoringReadinessState.Blocked
                : missing > 0 || partial > 0
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready;

            string code = state == DimensionAuthoringReadinessState.Ready
                ? "tool-guide-ready"
                : state == DimensionAuthoringReadinessState.Blocked
                    ? "tool-guide-blocked"
                    : "tool-guide-active";
            string message = state == DimensionAuthoringReadinessState.Ready
                ? "The selected dimension covers the full authoring journey."
                : state == DimensionAuthoringReadinessState.Blocked
                    ? "At least one authoring stage is blocked and needs attention before export."
                    : "Follow the creation path from the first template to a playable dimension.";

            return new DimensionFrameworkToolGuide(
                state,
                code,
                message,
                ready,
                partial,
                missing,
                blocked,
                modules);
        }

        private static void AddModule(
            List<DimensionFrameworkToolModule> modules,
            string moduleId,
            string title,
            string category,
            DimensionAuthoringReadinessState state,
            bool coreWorkflow,
            string summary,
            string modderNeed,
            string nextAction,
            string actionId)
        {
            modules.Add(new DimensionFrameworkToolModule(
                moduleId,
                title,
                category,
                state,
                coreWorkflow,
                summary,
                modderNeed,
                nextAction,
                actionId));
        }

        private static int Count<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }
    }
}
