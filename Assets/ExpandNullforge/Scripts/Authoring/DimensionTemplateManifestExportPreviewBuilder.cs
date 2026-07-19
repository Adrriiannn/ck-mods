using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    public static class DimensionTemplateManifestExportPreviewBuilder
    {
        public static DimensionTemplateManifestExportPreview Build(
            DimensionTemplateAsset template)
        {
            return Build(
                template,
                false,
                false,
                "Dimension template manifest export preview.");
        }

        public static DimensionTemplateManifestExportPreview Build(
            DimensionTemplateAsset template,
            bool updateExisting,
            bool requireExistingOwnershipRecords,
            string reason)
        {
            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(template);
            DimensionAuthoringReadinessReport readiness =
                DimensionAuthoringReadinessUtility.BuildReport(preview);
            DimensionAuthoringOperationPlan operationPlan =
                DimensionAuthoringOperationPlanUtility.BuildPlan(preview, readiness);

            return Build(
                template,
                operationPlan,
                updateExisting,
                requireExistingOwnershipRecords,
                reason);
        }

        public static DimensionTemplateManifestExportPreview Build(
            DimensionTemplateAsset template,
            DimensionAuthoringOperationPlan operationPlan,
            bool updateExisting,
            bool requireExistingOwnershipRecords,
            string reason)
        {
            DimensionAuthoringPreviewSummary preview =
                DimensionAuthoringPreviewBuilder.Build(template);
            DimensionContentManifest manifest;
            DimensionCompiledGenerationPlan compiledPlan;
            DimensionOperationResult buildResult;
            bool manifestBuilt = DimensionTemplateManifestBuilder.TryBuildManifest(
                template,
                out manifest,
                out compiledPlan,
                out buildResult);

            bool readyForManifestExport =
                manifestBuilt &&
                buildResult.Success &&
                operationPlan.ReadyForManifestExport;
            bool readyForRuntimeGeneration =
                readyForManifestExport &&
                operationPlan.ReadyForRuntimeGeneration &&
                compiledPlan.Success;
            bool readyForValidation = readyForManifestExport;
            bool readyForApply = readyForValidation && compiledPlan.Success;

            DimensionContentManifestRequest validationRequest =
                new DimensionContentManifestRequest(
                    manifest,
                    updateExisting,
                    requireExistingOwnershipRecords,
                    ResolveReason(reason, "Validate Dimension Asset manifest."));
            DimensionContentManifestRequest applyRequest =
                new DimensionContentManifestRequest(
                    manifest,
                    updateExisting,
                    requireExistingOwnershipRecords,
                    ResolveReason(reason, "Apply Dimension Asset manifest."));

            string code = ResolveCode(
                manifestBuilt,
                buildResult,
                readyForManifestExport,
                readyForRuntimeGeneration,
                operationPlan);
            string message = ResolveMessage(
                manifestBuilt,
                buildResult,
                readyForManifestExport,
                readyForRuntimeGeneration,
                operationPlan);

            return new DimensionTemplateManifestExportPreview(
                manifestBuilt,
                readyForManifestExport,
                readyForRuntimeGeneration,
                readyForValidation,
                readyForApply,
                code,
                message,
                compiledPlan.DimensionId,
                compiledPlan.DisplayName,
                compiledPlan.ReservedLocalBounds,
                compiledPlan.PlayableLocalBounds,
                compiledPlan.CoordinateShellPaddingTiles,
                Count(manifest.ContentPacks),
                Count(manifest.Dimensions),
                Count(manifest.Zones),
                Count(manifest.Biomes),
                Count(manifest.SceneTemplates),
                Count(manifest.Scenes),
                Count(manifest.SpawnRules),
                Count(manifest.GenerationPasses),
                Count(manifest.ResourceNodes),
                Count(manifest.EnvironmentProfiles),
                Count(manifest.GenerationTables),
                Count(manifest.GenerationTableEntries),
                Count(manifest.OwnershipBindings),
                Count(manifest.AssetReferences),
                Count(preview.ContentEntries),
                CountSceneContent(preview),
                CountResourceContent(preview),
                CountSpawnableContent(preview),
                operationPlan.OperationCount,
                operationPlan.BlockingManifestExportCount,
                operationPlan.BlockingRuntimeGenerationCount,
                operationPlan.ErrorCount,
                operationPlan.WarningCount,
                manifest,
                compiledPlan,
                operationPlan,
                buildResult,
                validationRequest,
                applyRequest,
                BuildNotes(compiledPlan, operationPlan, readyForManifestExport, readyForRuntimeGeneration));
        }

        private static IReadOnlyList<string> BuildNotes(
            DimensionCompiledGenerationPlan compiledPlan,
            DimensionAuthoringOperationPlan operationPlan,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration)
        {
            List<string> notes = new List<string>();

            if (!string.IsNullOrEmpty(compiledPlan.Message))
            {
                notes.Add(compiledPlan.Message);
            }

            if (!string.IsNullOrEmpty(operationPlan.Message))
            {
                notes.Add(operationPlan.Message);
            }

            if (readyForManifestExport && !readyForRuntimeGeneration)
            {
                notes.Add("The manifest can be exported now, but runtime generation still has unresolved preparation work.");
            }

            if (readyForRuntimeGeneration)
            {
                notes.Add("The manifest is ready for validation, application, and runtime generation.");
            }

            return notes;
        }

        private static string ResolveReason(string reason, string fallback)
        {
            return string.IsNullOrEmpty(reason) ? fallback : reason;
        }

        private static string ResolveCode(
            bool manifestBuilt,
            DimensionOperationResult buildResult,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            DimensionAuthoringOperationPlan operationPlan)
        {
            if (!manifestBuilt || !buildResult.Success)
            {
                return string.IsNullOrEmpty(buildResult.Code) ? "manifest-build-failed" : buildResult.Code;
            }

            if (!readyForManifestExport)
            {
                return string.IsNullOrEmpty(operationPlan.Code) ? "manifest-export-blocked" : operationPlan.Code;
            }

            if (!readyForRuntimeGeneration)
            {
                return "manifest-export-ready-runtime-pending";
            }

            return "manifest-export-ready";
        }

        private static string ResolveMessage(
            bool manifestBuilt,
            DimensionOperationResult buildResult,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            DimensionAuthoringOperationPlan operationPlan)
        {
            if (!manifestBuilt || !buildResult.Success)
            {
                return string.IsNullOrEmpty(buildResult.Message)
                    ? "Dimension template manifest could not be built."
                    : buildResult.Message;
            }

            if (!readyForManifestExport)
            {
                return string.IsNullOrEmpty(operationPlan.Message)
                    ? "Dimension template manifest export is blocked by authoring issues."
                    : operationPlan.Message;
            }

            if (!readyForRuntimeGeneration)
            {
                return "Dimension template manifest can be exported, but runtime generation preparation is still pending.";
            }

            return "Dimension template manifest is ready for validation, application, and runtime generation.";
        }

        private static int Count<T>(IReadOnlyList<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private static int CountSceneContent(DimensionAuthoringPreviewSummary preview)
        {
            return
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneTemplate) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneProp) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneLootContainer) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneSpawnPoint) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneTrigger);
        }

        private static int CountResourceContent(DimensionAuthoringPreviewSummary preview)
        {
            return
                CountContent(preview, DimensionAuthoringContentSummaryKind.ResourceNode) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Item) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Recipe) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Workbench) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.LootTable);
        }

        private static int CountSpawnableContent(DimensionAuthoringPreviewSummary preview)
        {
            return
                CountContent(preview, DimensionAuthoringContentSummaryKind.SpawnRule) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Animal) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Critter) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Mob) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.Boss) +
                CountContent(preview, DimensionAuthoringContentSummaryKind.SceneSpawnPoint);
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
                    count++;
                }
            }

            return count;
        }
    }
}
