using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    public enum DimensionTemplateCustomizerActionRisk
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public sealed class DimensionTemplateCustomizerActionPreview
    {
        public DimensionTemplateCustomizerActionPreview(
            DimensionTemplateCustomizerActionDescriptor action,
            string previewCode,
            string summary,
            string inputHint,
            string safetyMessage,
            string expectedResult,
            DimensionTemplateCustomizerActionRisk risk,
            bool canRunAutomatically,
            bool requiresEditorImplementation)
        {
            Action = action;
            PreviewCode = previewCode ?? string.Empty;
            Summary = summary ?? string.Empty;
            InputHint = inputHint ?? string.Empty;
            SafetyMessage = safetyMessage ?? string.Empty;
            ExpectedResult = expectedResult ?? string.Empty;
            Risk = risk;
            CanRunAutomatically = canRunAutomatically;
            RequiresEditorImplementation = requiresEditorImplementation;
        }

        public DimensionTemplateCustomizerActionDescriptor Action { get; private set; }

        public string PreviewCode { get; private set; }

        public string Summary { get; private set; }

        public string InputHint { get; private set; }

        public string SafetyMessage { get; private set; }

        public string ExpectedResult { get; private set; }

        public DimensionTemplateCustomizerActionRisk Risk { get; private set; }

        public bool CanRunAutomatically { get; private set; }

        public bool RequiresEditorImplementation { get; private set; }
    }

    public sealed class DimensionTemplateCustomizerActionPreviewCatalog
    {
        public DimensionTemplateCustomizerActionPreviewCatalog(
            string activeSectionId,
            int previewCount,
            int highRiskCount,
            int mediumRiskCount,
            int requiresImplementationCount,
            IReadOnlyList<DimensionTemplateCustomizerActionPreview> previews)
        {
            ActiveSectionId = activeSectionId ?? string.Empty;
            PreviewCount = previewCount < 0 ? 0 : previewCount;
            HighRiskCount = highRiskCount < 0 ? 0 : highRiskCount;
            MediumRiskCount = mediumRiskCount < 0 ? 0 : mediumRiskCount;
            RequiresImplementationCount = requiresImplementationCount < 0 ? 0 : requiresImplementationCount;
            Previews = previews ?? new List<DimensionTemplateCustomizerActionPreview>();
        }

        public string ActiveSectionId { get; private set; }

        public int PreviewCount { get; private set; }

        public int HighRiskCount { get; private set; }

        public int MediumRiskCount { get; private set; }

        public int RequiresImplementationCount { get; private set; }

        public IReadOnlyList<DimensionTemplateCustomizerActionPreview> Previews { get; private set; }
    }

    public static class DimensionTemplateCustomizerActionPreviewUtility
    {
        public static DimensionTemplateCustomizerActionPreviewCatalog Build(
            DimensionTemplateAuthoringWorkspace workspace,
            string sectionId)
        {
            DimensionTemplateCustomizerActionCatalog catalog =
                workspace == null
                    ? DimensionTemplateCustomizerActionCatalogUtility.Build(null, sectionId)
                    : workspace.GetActionCatalog(sectionId);

            List<DimensionTemplateCustomizerActionPreview> previews =
                new List<DimensionTemplateCustomizerActionPreview>();
            if (catalog.Actions != null)
            {
                for (int i = 0; i < catalog.Actions.Count; i++)
                {
                    previews.Add(CreatePreview(catalog.Actions[i]));
                }
            }

            int highRisk;
            int mediumRisk;
            int requiresImplementation;
            CountPreviews(previews, out highRisk, out mediumRisk, out requiresImplementation);

            return new DimensionTemplateCustomizerActionPreviewCatalog(
                catalog.ActiveSectionId,
                previews.Count,
                highRisk,
                mediumRisk,
                requiresImplementation,
                previews);
        }

        private static DimensionTemplateCustomizerActionPreview CreatePreview(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            DimensionTemplateCustomizerActionRisk risk = ResolveRisk(action);
            bool canRunAutomatically = CanRunAutomatically(action);
            bool requiresImplementation = RequiresEditorImplementation(action);

            return new DimensionTemplateCustomizerActionPreview(
                action,
                BuildPreviewCode(action),
                BuildSummary(action),
                BuildInputHint(action),
                BuildSafetyMessage(action, risk),
                BuildExpectedResult(action),
                risk,
                canRunAutomatically,
                requiresImplementation);
        }

        private static DimensionTemplateCustomizerActionRisk ResolveRisk(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestApply)
            {
                return DimensionTemplateCustomizerActionRisk.High;
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.AssetMutation ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.RuntimePreparation ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestExport)
            {
                return DimensionTemplateCustomizerActionRisk.Medium;
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestValidation)
            {
                return DimensionTemplateCustomizerActionRisk.Low;
            }

            return DimensionTemplateCustomizerActionRisk.None;
        }

        private static bool CanRunAutomatically(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            return action.Mutability == DimensionTemplateCustomizerActionMutability.Navigation ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestValidation;
        }

        private static bool RequiresEditorImplementation(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            return action.Mutability == DimensionTemplateCustomizerActionMutability.AssetMutation ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestExport ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestApply ||
                action.Mutability == DimensionTemplateCustomizerActionMutability.RuntimePreparation;
        }

        private static string BuildPreviewCode(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (action.Mutability == DimensionTemplateCustomizerActionMutability.Navigation)
            {
                return "navigation";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestValidation)
            {
                return "dry-run-validation";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestExport)
            {
                return "manifest-export";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestApply)
            {
                return "manifest-apply";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.RuntimePreparation)
            {
                return "runtime-preparation";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.AssetMutation)
            {
                return "template-asset-mutation";
            }

            return "none";
        }

        private static string BuildSummary(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (action.Intent == DimensionTemplateCustomizerActionIntent.SelectTemplate)
            {
                return "Open or create a Dimension Asset before editing biome and generation content.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.AddContent)
            {
                return "Add a missing authoring record for the selected biome or section.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.ConfigureContent)
            {
                return "Open the selected content area so the modder can configure values safely.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.ResolveIssue)
            {
                return "Guide the modder to the blocking issue and offer a safe correction path.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.Review)
            {
                return "Show warnings, blockers, or recommendations without mutating the authoring graph.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.Validate)
            {
                return "Run a dry-run validation that does not register runtime content or write assets.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.Export)
            {
                return "Prepare a manifest payload that can be inspected or saved by the editor workflow.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.Apply)
            {
                return "Apply the manifest to the runtime content registries after validation succeeds.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.PrepareRuntime)
            {
                return "Review the runtime-generation requirements before allowing the dimension to be generated in game.";
            }

            if (action.Intent == DimensionTemplateCustomizerActionIntent.Ready)
            {
                return "No immediate action is required for this section.";
            }

            return "Inspect the selected authoring record or section.";
        }

        private static string BuildInputHint(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (!string.IsNullOrEmpty(action.RecordId))
            {
                return "Target record: " + action.RecordKind + " / " + action.RecordId;
            }

            if (!string.IsNullOrEmpty(action.BiomeId))
            {
                return "Target biome: " + action.BiomeId;
            }

            if (!string.IsNullOrEmpty(action.SectionId))
            {
                return "Target section: " + action.SectionId;
            }

            return action.RequiresSelection
                ? "Select a target record before running this action."
                : "No record selection is required.";
        }

        private static string BuildSafetyMessage(
            DimensionTemplateCustomizerActionDescriptor action,
            DimensionTemplateCustomizerActionRisk risk)
        {
            if (risk == DimensionTemplateCustomizerActionRisk.High)
            {
                return "Requires explicit confirmation because it may change runtime registries or applied dimension content.";
            }

            if (risk == DimensionTemplateCustomizerActionRisk.Medium)
            {
                return action.MutatesAssets
                    ? "May mutate Dimension Assets; the editor UI must present a confirmation or undo-safe flow."
                    : "May prepare runtime-facing data; do not run it during normal inspector repaint/update loops.";
            }

            if (risk == DimensionTemplateCustomizerActionRisk.Low)
            {
                return "Safe to run as a dry-run validation when requested by the modder.";
            }

            return "Read-only or navigation-only action.";
        }

        private static string BuildExpectedResult(
            DimensionTemplateCustomizerActionDescriptor action)
        {
            if (action.Availability == DimensionTemplateCustomizerActionAvailability.Blocked)
            {
                return "The action should be disabled until blocking issues are resolved.";
            }

            if (action.Availability == DimensionTemplateCustomizerActionAvailability.Disabled)
            {
                return "The action is informational only.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.Navigation)
            {
                return "The customizer changes focus without changing assets.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestValidation)
            {
                return "The customizer reports validation results and blockers.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.AssetMutation)
            {
                return "The selected Dimension Asset is updated through public helper methods.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestExport)
            {
                return "A manifest payload becomes available for saving or inspection.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.ManifestApply)
            {
                return "Validated content is registered or updated in the framework registries.";
            }

            if (action.Mutability == DimensionTemplateCustomizerActionMutability.RuntimePreparation)
            {
                return "Runtime-generation readiness is refreshed for this dimension.";
            }

            return "No state change is expected.";
        }

        private static void CountPreviews(
            IReadOnlyList<DimensionTemplateCustomizerActionPreview> previews,
            out int highRisk,
            out int mediumRisk,
            out int requiresImplementation)
        {
            highRisk = 0;
            mediumRisk = 0;
            requiresImplementation = 0;
            if (previews == null)
            {
                return;
            }

            for (int i = 0; i < previews.Count; i++)
            {
                DimensionTemplateCustomizerActionPreview preview = previews[i];
                if (preview.Risk == DimensionTemplateCustomizerActionRisk.High)
                {
                    highRisk++;
                }
                else if (preview.Risk == DimensionTemplateCustomizerActionRisk.Medium)
                {
                    mediumRisk++;
                }

                if (preview.RequiresEditorImplementation)
                {
                    requiresImplementation++;
                }
            }
        }
    }
}
