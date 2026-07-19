using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public sealed class DimensionFrameworkExtractionReadinessItem
    {
        public DimensionFrameworkExtractionReadinessItem(
            string itemId,
            string title,
            DimensionAuthoringReadinessState state,
            bool blocksExtraction,
            string evidence,
            string guidance)
        {
            ItemId = itemId ?? string.Empty;
            Title = title ?? string.Empty;
            State = state;
            BlocksExtraction = blocksExtraction;
            Evidence = evidence ?? string.Empty;
            Guidance = guidance ?? string.Empty;
        }

        public string ItemId { get; private set; }

        public string Title { get; private set; }

        public DimensionAuthoringReadinessState State { get; private set; }

        public bool BlocksExtraction { get; private set; }

        public string Evidence { get; private set; }

        public string Guidance { get; private set; }
    }

    public sealed class DimensionFrameworkExtractionReadinessReport
    {
        public DimensionFrameworkExtractionReadinessReport(
            DimensionAuthoringReadinessState state,
            string code,
            string message,
            int readyCount,
            int advisoryCount,
            int blockedCount,
            IReadOnlyList<DimensionFrameworkExtractionReadinessItem> items)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ReadyCount = readyCount < 0 ? 0 : readyCount;
            AdvisoryCount = advisoryCount < 0 ? 0 : advisoryCount;
            BlockedCount = blockedCount < 0 ? 0 : blockedCount;
            Items = items ?? new List<DimensionFrameworkExtractionReadinessItem>();
        }

        public DimensionAuthoringReadinessState State { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public int ReadyCount { get; private set; }

        public int AdvisoryCount { get; private set; }

        public int BlockedCount { get; private set; }

        public IReadOnlyList<DimensionFrameworkExtractionReadinessItem> Items { get; private set; }
    }

    public static class DimensionFrameworkExtractionReadinessUtility
    {
        public static DimensionFrameworkExtractionReadinessReport Build(
            DimensionTemplateAuthoringWorkspace workspace)
        {
            List<DimensionFrameworkExtractionReadinessItem> items =
                new List<DimensionFrameworkExtractionReadinessItem>();

            if (workspace == null)
            {
                items.Add(new DimensionFrameworkExtractionReadinessItem(
                    "workspace",
                    "Authoring workspace",
                    DimensionAuthoringReadinessState.Blocked,
                    true,
                    "No selected Dimension Asset is available.",
                    "Select or create a Dimension Template before evaluating framework extraction readiness."));
                return BuildReport(items);
            }

            AddTemplateIdentityItem(items, workspace);
            AddNeutralSavePathItem(items, workspace);
            AddManifestItem(items, workspace);
            AddRuntimeGenerationItem(items, workspace);
            AddVisualPreflightItem(items, workspace);
            AddSetupGuideItem(items, workspace);
            AddFixtureBoundaryItem(items);
            AddPackageRenameItem(items);

            return BuildReport(items);
        }

        private static void AddTemplateIdentityItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAsset template = workspace.Graph == null
                ? null
                : workspace.Graph.Dimension;
            if (template == null)
            {
                items.Add(new DimensionFrameworkExtractionReadinessItem(
                    "template-identity",
                    "Dimension template identity",
                    DimensionAuthoringReadinessState.Blocked,
                    true,
                    "No root Dimension Template is present in the workspace graph.",
                    "Create a root Dimension Template with a stable dimension ID, display name, and content-pack owner."));
                return;
            }

            bool fixtureNamed =
                ContainsFixtureName(template.DimensionId) ||
                ContainsFixtureName(template.DisplayName) ||
                ContainsFixtureName(template.ContentPackId) ||
                ContainsFixtureName(template.ContentPackDisplayName);

            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "template-identity",
                "Dimension template identity",
                fixtureNamed
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready,
                false,
                "DimensionId=" + Safe(template.DimensionId) +
                ", DisplayName=" + Safe(template.DisplayName) +
                ", ContentPackId=" + Safe(template.ContentPackId),
                fixtureNamed
                    ? "This selected Dimension Asset still looks like the Nullforge validation fixture. That is fine for testing, but it should live outside the dimension-less framework package before release."
                    : "The selected Dimension Asset identity does not look tied to the Nullforge validation fixture."));
        }

        private static void AddNeutralSavePathItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateAssetSavePlan savePlan = workspace.SavePlan;
            string root = savePlan == null ? string.Empty : savePlan.RootFolder;
            bool empty = string.IsNullOrEmpty(root);
            bool oldPackagePath = ContainsIgnoreCase(root, "Assets/ExpandNullforge");

            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "neutral-save-path",
                "Generated asset save path",
                empty || oldPackagePath
                    ? DimensionAuthoringReadinessState.Blocked
                    : DimensionAuthoringReadinessState.Ready,
                true,
                empty ? "No save-plan root folder is available." : root,
                oldPackagePath
                    ? "Generated assets still target the old ExpandNullforge package folder. Use a neutral DimensionFramework or consuming-mod folder."
                    : empty
                        ? "Rebuild the workspace so a save plan can be prepared."
                        : "Generated authoring assets are planned outside the old ExpandNullforge package path."));
        }

        private static void AddManifestItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionTemplateManifestExportPreview preview = workspace.ManifestExportPreview;
            bool ready = workspace.ReadyForManifestExport &&
                preview.ManifestBuilt &&
                preview.ReadyForManifestExport &&
                preview.BuildResult.Success;
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "manifest-export",
                "Manifest export readiness",
                ready
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Blocked,
                true,
                string.IsNullOrEmpty(preview.Code) ? "manifest-preview" : preview.Code,
                ready
                    ? "The selected Dimension Asset can produce a content manifest through the framework export path."
                    : "Resolve manifest/export blockers before treating this as a reusable dimension pack."));
        }

        private static void AddRuntimeGenerationItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            bool ready = workspace.ReadyForRuntimeGeneration;
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "runtime-generation",
                "Runtime generation readiness",
                ready
                    ? DimensionAuthoringReadinessState.Ready
                    : DimensionAuthoringReadinessState.Partial,
                false,
                ready ? "Runtime generation checks are ready." : "Runtime generation checks are not fully ready.",
                ready
                    ? "The template has enough authored content to be handed to generation providers."
                    : "This does not block framework extraction, but a consuming dimension pack will need generation passes/providers before it can create playable terrain."));
        }

        private static void AddVisualPreflightItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionVisualAssetValidationReport report = workspace.VisualAssetValidation;
            bool blocked = report.BlockedCount > 0;
            bool advisory = report.AdvisoryCount > 0 || report.UnknownSourceCount > 0;
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "visual-preflight",
                "Visual asset preflight",
                blocked
                    ? DimensionAuthoringReadinessState.Blocked
                    : advisory
                        ? DimensionAuthoringReadinessState.Partial
                        : DimensionAuthoringReadinessState.Ready,
                blocked,
                "Ready=" + report.ReadyCount +
                ", Advisory=" + report.AdvisoryCount +
                ", Blocked=" + report.BlockedCount +
                ", UnknownSource=" + report.UnknownSourceCount,
                blocked
                    ? "Fix blocked visual references before exporting or validating a dimension pack."
                    : advisory
                        ? "Review advisory visual references for UGC SpriteObject Lit swaps, runtime material swaps, lighting, shadows, or vanilla-reference intent."
                        : "Visual references are ready for the current level of framework validation."));
        }

        private static void AddSetupGuideItem(
            List<DimensionFrameworkExtractionReadinessItem> items,
            DimensionTemplateAuthoringWorkspace workspace)
        {
            DimensionFrameworkSetupGuide guide = workspace.SetupGuide;
            bool blocked = guide.BlockedCount > 0;
            bool advisory = guide.WarningCount > 0;
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "setup-guide",
                "Authoring setup guide",
                blocked
                    ? DimensionAuthoringReadinessState.Blocked
                    : advisory
                        ? DimensionAuthoringReadinessState.Partial
                        : DimensionAuthoringReadinessState.Ready,
                blocked,
                "Ready=" + guide.ReadyCount +
                ", Advisory=" + guide.WarningCount +
                ", Blocked=" + guide.BlockedCount,
                blocked
                    ? "Complete the blocked setup-guide steps before extracting or publishing the framework workflow."
                    : advisory
                        ? "The setup guide has advisory steps that should be reviewed before release."
                        : "The current setup-guide model is satisfied for the selected workspace."));
        }

        private static void AddFixtureBoundaryItem(
            List<DimensionFrameworkExtractionReadinessItem> items)
        {
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "fixture-boundary",
                "Nullforge validation fixture boundary",
                DimensionAuthoringReadinessState.Partial,
                false,
                "Public API constants are neutral, generic portals no longer default to Nullforge, and Nullforge fixture content is registered through an opt-in fixture bootstrap instead of the framework service constructor.",
                "Move the remaining Nullforge generation, zone, portal-content, travel-action, and coordinate-presentation systems into the consuming Nullforge mod before the final framework package rename."));
        }

        private static void AddPackageRenameItem(
            List<DimensionFrameworkExtractionReadinessItem> items)
        {
            items.Add(new DimensionFrameworkExtractionReadinessItem(
                "package-rename",
                "Final package/API namespace rename",
                DimensionAuthoringReadinessState.Partial,
                false,
                "The source package still compiles under the current project namespace while the framework is being proven.",
                "After the framework is runtime-proven, rename assemblies/folders/namespaces in one controlled pass and leave compatibility aliases only where they are explicitly needed."));
        }

        private static DimensionFrameworkExtractionReadinessReport BuildReport(
            IReadOnlyList<DimensionFrameworkExtractionReadinessItem> items)
        {
            int ready = 0;
            int advisory = 0;
            int blocked = 0;

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    DimensionFrameworkExtractionReadinessItem item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.BlocksExtraction &&
                        (item.State == DimensionAuthoringReadinessState.Blocked ||
                         item.State == DimensionAuthoringReadinessState.Missing))
                    {
                        blocked++;
                    }
                    else if (item.State == DimensionAuthoringReadinessState.Ready)
                    {
                        ready++;
                    }
                    else
                    {
                        advisory++;
                    }
                }
            }

            DimensionAuthoringReadinessState state = blocked > 0
                ? DimensionAuthoringReadinessState.Blocked
                : advisory > 0
                    ? DimensionAuthoringReadinessState.Partial
                    : DimensionAuthoringReadinessState.Ready;
            string code = state == DimensionAuthoringReadinessState.Ready
                ? "ready"
                : state == DimensionAuthoringReadinessState.Partial
                    ? "advisory"
                    : "blocked";
            string message = state == DimensionAuthoringReadinessState.Ready
                ? "The selected workspace looks ready for framework extraction checks."
                : state == DimensionAuthoringReadinessState.Partial
                    ? "The selected workspace is usable, but fixture/extraction advisories remain."
                    : "The selected workspace has blockers that should be fixed before framework extraction.";

            return new DimensionFrameworkExtractionReadinessReport(
                state,
                code,
                message,
                ready,
                advisory,
                blocked,
                items);
        }

        private static bool ContainsFixtureName(string value)
        {
            return ContainsIgnoreCase(value, "nullforge") ||
                ContainsIgnoreCase(value, "expandnullforge") ||
                ContainsIgnoreCase(value, "expand nullforge");
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(token) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
