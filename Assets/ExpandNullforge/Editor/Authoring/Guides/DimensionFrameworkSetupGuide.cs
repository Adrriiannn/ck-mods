using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionFrameworkSetupGuideStep
    {
        public DimensionFrameworkSetupGuideStep(
            string stepId,
            string title,
            DimensionAuthoringReadinessState state,
            DimensionAuthoringSeverity severity,
            string assetKind,
            string recommendedLocation,
            string guidance,
            string verification,
            string actionId)
        {
            StepId = stepId ?? string.Empty;
            Title = title ?? string.Empty;
            State = state;
            Severity = severity;
            AssetKind = assetKind ?? string.Empty;
            RecommendedLocation = recommendedLocation ?? string.Empty;
            Guidance = guidance ?? string.Empty;
            Verification = verification ?? string.Empty;
            ActionId = actionId ?? string.Empty;
        }

        public string StepId { get; private set; }

        public string Title { get; private set; }

        public DimensionAuthoringReadinessState State { get; private set; }

        public DimensionAuthoringSeverity Severity { get; private set; }

        public string AssetKind { get; private set; }

        public string RecommendedLocation { get; private set; }

        public string Guidance { get; private set; }

        public string Verification { get; private set; }

        public string ActionId { get; private set; }
    }

    public sealed class DimensionFrameworkSetupGuide
    {
        public DimensionFrameworkSetupGuide(
            DimensionAuthoringReadinessState state,
            string code,
            string message,
            int readyCount,
            int warningCount,
            int blockedCount,
            IReadOnlyList<DimensionFrameworkSetupGuideStep> steps)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            Steps = steps ?? new List<DimensionFrameworkSetupGuideStep>();
        }

        public DimensionAuthoringReadinessState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int ReadyCount { get; private set; }

        public int WarningCount { get; private set; }

        public int BlockedCount { get; private set; }

        public IReadOnlyList<DimensionFrameworkSetupGuideStep> Steps { get; private set; }
    }

    public static class DimensionFrameworkSetupGuideUtility
    {
        public static DimensionFrameworkSetupGuide Build(DimensionTemplateAuthoringWorkspace workspace)
        {
            List<DimensionFrameworkSetupGuideStep> steps =
                new List<DimensionFrameworkSetupGuideStep>();
            DimensionTemplateStarterGraph graph = workspace == null ? null : workspace.Graph;
            DimensionTemplateStarterAssessment assessment =
                workspace == null ? null : workspace.Assessment;
            DimensionVisualAssetValidationReport visualReport =
                workspace == null ? null : workspace.VisualAssetValidation;

            AddStep(
                steps,
                "dimension-root",
                "Create the root Dimension Asset",
                graph != null && graph.Dimension != null,
                "Dimension Asset",
                "Assets/<YourDimension>/Authoring/<DimensionName>.asset",
                "Defines the dimension ID, display name, coordinate domain, capabilities, content-pack owner, and root links to layout/biomes.",
                "The dashboard should show a dimension ID/name and no blocking identity issues.",
                "create-dimension-template");

            AddStep(
                steps,
                "layout-template",
                "Attach a biome layout template",
                graph != null && graph.Layout != null,
                "DimensionLayoutTemplateAsset",
                "Assets/<YourDimension>/Authoring/Layout/<DimensionName>Layout.asset",
                "Controls playable local bounds and biome placement through a square, grid, radial, painted-mask, or hybrid layout.",
                "The preview canvas should show at least one playable/biome region around local 0,0.",
                "create-layout-template");

            AddStep(
                steps,
                "biome-template",
                "Author at least one biome template",
                graph != null && graph.Biome != null,
                "BiomeTemplateAsset",
                "Assets/<YourDimension>/Authoring/Biomes/<BiomeName>.asset",
                "Defines biome identity, fallback bounds, its floor and wall blocks, scenes, and generation links.",
                "The biome section should list the biome and no blocking missing-biome issues.",
                "create-biome-template");

            AddStep(
                steps,
                "generation-passes",
                "Declare deterministic generation passes",
                assessment != null && assessment.HasGenerationPass,
                "GenerationPassTemplateAsset",
                "Assets/<YourDimension>/Authoring/Generation/Passes/*.asset",
                "Each pass tells a provider what to generate, where to sample biome data, and which weighted table or semantic palette to use.",
                "Runtime generation readiness requires at least one pass with a real provider ID.",
                "create-generation-pass");

            AddStep(
                steps,
                "manifest-export",
                "Validate and export a content manifest",
                assessment != null && assessment.ReadyForManifestExport,
                "DimensionContentManifest",
                "Generated by the authoring dashboard/export action",
                "The manifest packages dimensions, biomes, layouts, tables, passes, palettes, scenes, resources, spawns, and ownership metadata.",
                "The export section should show no blockers before the manifest is applied or shipped.",
                "validate-export-manifest");

            AddStep(
                steps,
                "runtime-generation",
                "Confirm runtime generation readiness",
                assessment != null && assessment.ReadyForRuntimeGeneration,
                "Generation provider implementation",
                "Assets/<YourDimension>/Scripts/Generation",
                "Providers should consume framework work plans instead of reading templates directly, so generation remains deterministic and multiplayer-safe.",
                "Runtime readiness should be ready, then validated in-game with a temporary fixture portal/travel path.",
                "preflight-runtime-generation");

            return BuildGuide(steps);
        }

        private static void AddStep(
            List<DimensionFrameworkSetupGuideStep> steps,
            string stepId,
            string title,
            bool ready,
            string assetKind,
            string recommendedLocation,
            string guidance,
            string verification,
            string actionId)
        {
            AddStep(
                steps,
                stepId,
                title,
                ready,
                assetKind,
                recommendedLocation,
                guidance,
                verification,
                actionId,
                DimensionAuthoringReadinessState.Missing,
                DimensionAuthoringSeverity.Error);
        }

        private static void AddStep(
            List<DimensionFrameworkSetupGuideStep> steps,
            string stepId,
            string title,
            bool ready,
            string assetKind,
            string recommendedLocation,
            string guidance,
            string verification,
            string actionId,
            DimensionAuthoringReadinessState notReadyState,
            DimensionAuthoringSeverity notReadySeverity)
        {
            steps.Add(new DimensionFrameworkSetupGuideStep(
                stepId,
                title,
                ready ? DimensionAuthoringReadinessState.Ready : notReadyState,
                ready ? DimensionAuthoringSeverity.Info : notReadySeverity,
                assetKind,
                recommendedLocation,
                guidance,
                verification,
                actionId));
        }

        private static bool VisualReportHasNoBlockedRefs(DimensionVisualAssetValidationReport report)
        {
            return report == null || report.BlockedCount == 0;
        }

        private static DimensionFrameworkSetupGuide BuildGuide(
            IReadOnlyList<DimensionFrameworkSetupGuideStep> steps)
        {
            int ready = 0;
            int warning = 0;
            int blocked = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].State == DimensionAuthoringReadinessState.Ready)
                {
                    ready++;
                }
                else if (steps[i].Severity == DimensionAuthoringSeverity.Warning)
                {
                    warning++;
                }
                else
                {
                    blocked++;
                }
            }

            DimensionAuthoringReadinessState state = blocked > 0
                ? DimensionAuthoringReadinessState.Blocked
                : warning > 0
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready;
            string code = state == DimensionAuthoringReadinessState.Ready
                ? "setup-ready"
                : state == DimensionAuthoringReadinessState.Partial
                    ? "setup-advisory"
                    : "setup-blocked";
            string message = state == DimensionAuthoringReadinessState.Ready
                ? "The dimension authoring setup has all major framework requirements covered."
                : state == DimensionAuthoringReadinessState.Partial
                    ? "The dimension can move forward, but advisory setup tasks remain."
                    : "The dimension setup still has blocking requirements before it can be reliably exported or generated.";

            return new DimensionFrameworkSetupGuide(
                state,
                code,
                message,
                ready,
                warning,
                blocked,
                steps);
        }
    }
}
