using System.Collections.Generic;
using ExpandNullforge.Api;

using System;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public DimensionContentValidationReport ValidateContent(
        DimensionContentValidationRequest request)
    {
      List<DimensionContentValidationIssue> issues =
          new List<DimensionContentValidationIssue>();
      int errorCount = 0;
      int warningCount = 0;
      ValidateContentPackReadinessRecords(request, issues, ref errorCount, ref warningCount);
      ValidateOrphanedOwnershipRecords(request, issues, ref errorCount, ref warningCount);
      ValidateZoneRecords(request, issues, ref errorCount, ref warningCount);
      ValidateMapLayerRecords(request, issues, ref errorCount, ref warningCount);
      ValidateMapMarkerRecords(request, issues, ref errorCount, ref warningCount);
      ValidateAnchorRecords(request, issues, ref errorCount, ref warningCount);
      ValidatePortalRecords(request, issues, ref errorCount, ref warningCount);
      ValidatePortalPresentationRecords(request, issues, ref errorCount, ref warningCount);
      ValidateTravelRequirementRecords(request, issues, ref errorCount, ref warningCount);
      ValidateProgressFlagRecords(request, issues, ref errorCount, ref warningCount);
      ValidateSceneTemplateRecords(request, issues, ref errorCount, ref warningCount);
      ValidateSceneRecords(request, issues, ref errorCount, ref warningCount);
      ValidateEncounterRecords(request, issues, ref errorCount, ref warningCount);
      ValidateGenerationPassRecords(request, issues, ref errorCount, ref warningCount);
      ValidateWorldEventRecords(request, issues, ref errorCount, ref warningCount);
      ValidateAssetReferenceRecords(request, issues, ref errorCount, ref warningCount);
      ValidateBiomeRecords(request, issues, ref errorCount, ref warningCount);

      return new DimensionContentValidationReport(
          true,
          errorCount > 0,
          errorCount,
          warningCount,
          issues);
    }

    /// <summary>
    /// Checks every content pack is ready, and says which is not this service holds.
    /// </summary>
    private void ValidateContentPackReadinessRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      IReadOnlyList<DimensionContentPackReadinessResult> readinessResults =
          EvaluateContentPackReadiness(request.IncludeDisabled);
      for (int i = 0; i < readinessResults.Count; i++)
      {
        DimensionContentPackReadinessResult readiness = readinessResults[i];
        if (!ContentPackMatchesValidationRequest(readiness.ContentPackId, request))
        {
          continue;
        }

        if (!readiness.Ready)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Custom,
              readiness.ContentPackId,
              readiness.ContentPackId,
              readiness.Code,
              readiness.Message);
        }
      }
    }

    /// <summary>
    /// Checks no ownership binding points at something that is gone.
    /// </summary>
    private void ValidateOrphanedOwnershipRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      if (request.IncludeWarnings)
      {
        IReadOnlyList<DimensionContentOwnershipBinding> orphanedBindings =
            GetOrphanedContentOwnershipBindings();
        for (int i = 0; i < orphanedBindings.Count; i++)
        {
          DimensionContentOwnershipBinding binding = orphanedBindings[i];
          if (!ContentPackMatchesValidationRequest(binding.ContentPackId, request))
          {
            continue;
          }

          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Warning,
              binding.RecordKind,
              binding.RecordId,
              binding.ContentPackId,
              "content-ownership-orphaned",
              "The content ownership binding points at a missing owner or record.");
        }

        AddUnownedContentValidationWarnings(
            issues,
            request,
            ref errorCount,
            ref warningCount);
      }
    }
  }
}
