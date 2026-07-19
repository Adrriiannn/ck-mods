using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelPreviewResult
    {
        public readonly bool Allowed;
        public readonly DimensionAccessResult AccessResult;
        public readonly string TargetDimensionId;
        public readonly float2 TargetLocalPosition;
        public readonly float2 TargetAbsolutePosition;
        public readonly bool HasPortal;
        public readonly DimensionPortalDefinition Portal;
        public readonly bool HasPresentation;
        public readonly DimensionPortalPresentationDefinition Presentation;
        public readonly IReadOnlyList<DimensionTravelRequirementEvaluationResult> RequirementResults;
        public readonly bool HasGenerationPreview;
        public readonly DimensionGenerationPreviewResult GenerationPreview;

        public DimensionTravelPreviewResult(
            bool allowed,
            DimensionAccessResult accessResult,
            string targetDimensionId,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition,
            bool hasPortal,
            DimensionPortalDefinition portal,
            bool hasPresentation,
            DimensionPortalPresentationDefinition presentation,
            IReadOnlyList<DimensionTravelRequirementEvaluationResult> requirementResults)
            : this(
                  allowed,
                  accessResult,
                  targetDimensionId,
                  targetLocalPosition,
                  targetAbsolutePosition,
                  hasPortal,
                  portal,
                  hasPresentation,
                  presentation,
                  requirementResults,
                  false,
                  default(DimensionGenerationPreviewResult))
        {
        }

        public DimensionTravelPreviewResult(
            bool allowed,
            DimensionAccessResult accessResult,
            string targetDimensionId,
            float2 targetLocalPosition,
            float2 targetAbsolutePosition,
            bool hasPortal,
            DimensionPortalDefinition portal,
            bool hasPresentation,
            DimensionPortalPresentationDefinition presentation,
            IReadOnlyList<DimensionTravelRequirementEvaluationResult> requirementResults,
            bool hasGenerationPreview,
            DimensionGenerationPreviewResult generationPreview)
        {
            Allowed = allowed;
            AccessResult = accessResult;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            TargetLocalPosition = targetLocalPosition;
            TargetAbsolutePosition = targetAbsolutePosition;
            HasPortal = hasPortal;
            Portal = portal;
            HasPresentation = hasPresentation;
            Presentation = presentation;
            RequirementResults =
                requirementResults ?? new List<DimensionTravelRequirementEvaluationResult>();
            HasGenerationPreview = hasGenerationPreview;
            GenerationPreview = generationPreview;
        }
    }
}
