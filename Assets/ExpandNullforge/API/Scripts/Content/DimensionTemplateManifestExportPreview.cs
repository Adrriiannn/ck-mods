using System.Collections.Generic;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTemplateManifestExportPreview
    {
        public readonly bool ManifestBuilt;
        public readonly bool ReadyForManifestExport;
        public readonly bool ReadyForRuntimeGeneration;
        public readonly bool ReadyForValidation;
        public readonly bool ReadyForApply;
        public readonly string Code;
        public readonly string Message;
        public readonly string DimensionId;
        public readonly string DisplayName;
        public readonly DimensionBounds ReservedLocalBounds;
        public readonly DimensionBounds PlayableLocalBounds;
        public readonly int CoordinateShellPaddingTiles;
        public readonly int ContentPackCount;
        public readonly int DimensionCount;
        public readonly int ZoneCount;
        public readonly int BiomeCount;
        public readonly int SceneTemplateCount;
        public readonly int SceneCount;
        public readonly int GenerationPassCount;
        public readonly int OwnershipBindingCount;
        public readonly int AssetReferenceCount;
        public readonly int AuthoredContentCount;
        public readonly int AuthoredSceneContentCount;
        public readonly int AuthoredResourceContentCount;
        public readonly int AuthoredSpawnableContentCount;
        public readonly int AuthoringOperationCount;
        public readonly int BlockingManifestExportCount;
        public readonly int BlockingRuntimeGenerationCount;
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly DimensionContentManifest Manifest;
        public readonly DimensionCompiledGenerationPlan CompiledPlan;
        public readonly DimensionAuthoringOperationPlan OperationPlan;
        public readonly DimensionOperationResult BuildResult;
        public readonly DimensionContentManifestRequest ValidationRequest;
        public readonly DimensionContentManifestRequest ApplyRequest;
        public readonly IReadOnlyList<string> Notes;

        public DimensionTemplateManifestExportPreview(
            bool manifestBuilt,
            bool readyForManifestExport,
            bool readyForRuntimeGeneration,
            bool readyForValidation,
            bool readyForApply,
            string code,
            string message,
            string dimensionId,
            string displayName,
            DimensionBounds reservedLocalBounds,
            DimensionBounds playableLocalBounds,
            int coordinateShellPaddingTiles,
            int contentPackCount,
            int dimensionCount,
            int zoneCount,
            int biomeCount,
            int sceneTemplateCount,
            int sceneCount,
            int generationPassCount,
            int ownershipBindingCount,
            int assetReferenceCount,
            int authoredContentCount,
            int authoredSceneContentCount,
            int authoredResourceContentCount,
            int authoredSpawnableContentCount,
            int authoringOperationCount,
            int blockingManifestExportCount,
            int blockingRuntimeGenerationCount,
            int errorCount,
            int warningCount,
            DimensionContentManifest manifest,
            DimensionCompiledGenerationPlan compiledPlan,
            DimensionAuthoringOperationPlan operationPlan,
            DimensionOperationResult buildResult,
            DimensionContentManifestRequest validationRequest,
            DimensionContentManifestRequest applyRequest,
            IReadOnlyList<string> notes)
        {
            ManifestBuilt = manifestBuilt;
            ReadyForManifestExport = readyForManifestExport;
            ReadyForRuntimeGeneration = readyForRuntimeGeneration;
            ReadyForValidation = readyForValidation;
            ReadyForApply = readyForApply;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ReservedLocalBounds = reservedLocalBounds;
            PlayableLocalBounds = playableLocalBounds;
            CoordinateShellPaddingTiles = coordinateShellPaddingTiles < 0 ? 0 : coordinateShellPaddingTiles;
            ContentPackCount = contentPackCount < 0 ? 0 : contentPackCount;
            DimensionCount = dimensionCount < 0 ? 0 : dimensionCount;
            ZoneCount = zoneCount < 0 ? 0 : zoneCount;
            BiomeCount = biomeCount < 0 ? 0 : biomeCount;
            SceneTemplateCount = sceneTemplateCount < 0 ? 0 : sceneTemplateCount;
            SceneCount = sceneCount < 0 ? 0 : sceneCount;
            GenerationPassCount = generationPassCount < 0 ? 0 : generationPassCount;
            OwnershipBindingCount = ownershipBindingCount < 0 ? 0 : ownershipBindingCount;
            AssetReferenceCount = assetReferenceCount < 0 ? 0 : assetReferenceCount;
            AuthoredContentCount = authoredContentCount < 0 ? 0 : authoredContentCount;
            AuthoredSceneContentCount = authoredSceneContentCount < 0 ? 0 : authoredSceneContentCount;
            AuthoredResourceContentCount = authoredResourceContentCount < 0 ? 0 : authoredResourceContentCount;
            AuthoredSpawnableContentCount = authoredSpawnableContentCount < 0 ? 0 : authoredSpawnableContentCount;
            AuthoringOperationCount = authoringOperationCount < 0 ? 0 : authoringOperationCount;
            BlockingManifestExportCount = blockingManifestExportCount < 0 ? 0 : blockingManifestExportCount;
            BlockingRuntimeGenerationCount = blockingRuntimeGenerationCount < 0 ? 0 : blockingRuntimeGenerationCount;
            ErrorCount = errorCount < 0 ? 0 : errorCount;
            WarningCount = warningCount < 0 ? 0 : warningCount;
            Manifest = manifest;
            CompiledPlan = compiledPlan;
            OperationPlan = operationPlan;
            BuildResult = buildResult;
            ValidationRequest = validationRequest;
            ApplyRequest = applyRequest;
            Notes = notes ?? new List<string>();
        }
    }
}
