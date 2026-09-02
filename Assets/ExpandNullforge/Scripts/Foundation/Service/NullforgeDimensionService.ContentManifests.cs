using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateContentPack(
        DimensionContentPackDefinition contentPack,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(contentPack.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      // ASKED, NOT RE-DERIVED. This read "MinimumApiVersion > CurrentApiVersion" and so did the
      // readiness check thirty files away, and neither of them checked the bottom of the range at
      // all — a pack declaring version 0, or -3, was accepted as compatible. The rule that says
      // otherwise was already written down, in DimensionApiCompatibility, with a test on it and no
      // caller. Routing both through it is what makes that test about something.
      if (!DimensionApiCompatibility.IsApiVersionSupported(contentPack.MinimumApiVersion))
      {
        result = contentPack.MinimumApiVersion > DimensionApi.CurrentApiVersion
            ? DimensionOperationResult.Failed("content-pack-api-too-new", "The content pack requires a newer Dimension API version.")
            : DimensionOperationResult.Failed("content-pack-api-not-supported", "The content pack declares a Dimension API version this build does not support.");
        return false;
      }

      if (contentPack.DependencyIds != null)
      {
        for (int i = 0; i < contentPack.DependencyIds.Count; i++)
        {
          if (string.Equals(contentPack.DependencyIds[i], contentPack.ContentPackId, StringComparison.Ordinal))
          {
            result = DimensionOperationResult.Failed("content-pack-self-dependency", "A content pack cannot depend on itself.");
            return false;
          }
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int CompareContentPacks(
        DimensionContentPackDefinition left,
        DimensionContentPackDefinition right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackReadiness(
        DimensionContentPackReadinessResult left,
        DimensionContentPackReadinessResult right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackSummaries(
        DimensionContentPackSummary left,
        DimensionContentPackSummary right)
    {
      return string.Compare(left.ContentPackId, right.ContentPackId, StringComparison.Ordinal);
    }

    private static int CompareContentPackRecordCounts(
        DimensionContentPackRecordCount left,
        DimensionContentPackRecordCount right)
    {
      return left.RecordKind.CompareTo(right.RecordKind);
    }

    private DimensionContentPackSummary BuildContentPackSummary(
        DimensionContentPackDefinition contentPack,
        DimensionContentPackSummaryRequest request)
    {
      Dictionary<DimensionContentRecordKind, int> counts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<DimensionContentRecordKind, int> orphanCounts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<string, bool> recordKeys =
          new Dictionary<string, bool>(StringComparer.Ordinal);

      int ownedRecordCount = 0;
      int orphanedRecordCount = 0;
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (!string.Equals(binding.ContentPackId, contentPack.ContentPackId, StringComparison.Ordinal))
        {
          continue;
        }

        bool isOrphaned =
            !ContentRecordExists(binding.RecordKind, binding.RecordId) &&
            !IsMetadataOnlyContentRecordKind(binding.RecordKind);
        if (isOrphaned && !request.IncludeOrphanedOwnership)
        {
          continue;
        }

        if (TryCountContentPackSummaryRecord(
                binding.RecordKind,
                binding.RecordId,
                counts,
                recordKeys))
        {
          ownedRecordCount++;
        }

        if (isOrphaned)
        {
          orphanedRecordCount++;
          IncrementContentPackSummaryCount(orphanCounts, binding.RecordKind);
        }
      }

      foreach (DimensionAssetReferenceDefinition assetReference in assetReferences.Values)
      {
        if (!string.Equals(assetReference.ContentPackId, contentPack.ContentPackId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!request.IncludeDisabled && !assetReference.Enabled)
        {
          continue;
        }

        if (TryCountContentPackSummaryRecord(
                DimensionContentRecordKind.AssetReference,
                assetReference.AssetId,
                counts,
                recordKeys))
        {
          ownedRecordCount++;
        }
      }

      int validationErrorCount = 0;
      int validationWarningCount = 0;
      if (request.IncludeValidation)
      {
        DimensionContentValidationReport validation =
            ValidateContent(
                new DimensionContentValidationRequest(
                    contentPack.ContentPackId,
                    request.IncludeDisabled,
                    request.IncludeWarnings));
        validationErrorCount = validation.ErrorCount;
        validationWarningCount = validation.WarningCount;
      }

      DimensionContentPackReadinessResult readiness =
          EvaluateContentPackReadinessInternal(contentPack);

      return new DimensionContentPackSummary(
          contentPack.ContentPackId,
          true,
          contentPack,
          readiness,
          ownedRecordCount,
          orphanedRecordCount,
          validationErrorCount,
          validationWarningCount,
          BuildContentPackRecordCounts(counts, orphanCounts));
    }

    private DimensionContentPackSummary BuildMissingContentPackSummary(
        string contentPackId,
        DimensionContentPackSummaryRequest request)
    {
      Dictionary<DimensionContentRecordKind, int> counts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<DimensionContentRecordKind, int> orphanCounts =
          new Dictionary<DimensionContentRecordKind, int>();
      Dictionary<string, bool> recordKeys =
          new Dictionary<string, bool>(StringComparer.Ordinal);

      int ownedRecordCount = 0;
      int orphanedRecordCount = 0;
      if (request.IncludeOrphanedOwnership)
      {
        foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
        {
          if (!string.Equals(binding.ContentPackId, contentPackId, StringComparison.Ordinal))
          {
            continue;
          }

          if (TryCountContentPackSummaryRecord(
                  binding.RecordKind,
                  binding.RecordId,
                  counts,
                  recordKeys))
          {
            ownedRecordCount++;
          }

          orphanedRecordCount++;
          IncrementContentPackSummaryCount(orphanCounts, binding.RecordKind);
        }
      }

      DimensionContentPackReadinessResult readiness =
          EvaluateContentPackReadiness(contentPackId);
      int validationErrorCount = request.IncludeValidation && !readiness.Ready ? 1 : 0;
      return new DimensionContentPackSummary(
          contentPackId,
          false,
          default(DimensionContentPackDefinition),
          readiness,
          ownedRecordCount,
          orphanedRecordCount,
          validationErrorCount,
          0,
          BuildContentPackRecordCounts(counts, orphanCounts));
    }

    private void AddMissingContentPackSummaries(
        List<DimensionContentPackSummary> summaries,
        DimensionContentPackSummaryRequest request)
    {
      if (!request.IncludeOrphanedOwnership)
      {
        return;
      }

      Dictionary<string, bool> summaryIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      for (int i = 0; i < summaries.Count; i++)
      {
        if (!string.IsNullOrEmpty(summaries[i].ContentPackId))
        {
          summaryIds[summaries[i].ContentPackId] = true;
        }
      }

      Dictionary<string, bool> missingIds =
          new Dictionary<string, bool>(StringComparer.Ordinal);
      foreach (DimensionContentOwnershipBinding binding in contentOwnershipBindings.Values)
      {
        if (string.IsNullOrEmpty(binding.ContentPackId) ||
            summaryIds.ContainsKey(binding.ContentPackId) ||
            contentPacks.ContainsKey(binding.ContentPackId))
        {
          continue;
        }

        missingIds[binding.ContentPackId] = true;
      }

      foreach (string contentPackId in missingIds.Keys)
      {
        summaries.Add(BuildMissingContentPackSummary(contentPackId, request));
      }
    }

    private static bool TryCountContentPackSummaryRecord(
        DimensionContentRecordKind recordKind,
        string recordId,
        Dictionary<DimensionContentRecordKind, int> counts,
        Dictionary<string, bool> recordKeys)
    {
      if (string.IsNullOrEmpty(recordId))
      {
        return false;
      }

      string key = BuildContentOwnershipKey(recordKind, recordId);
      if (recordKeys.ContainsKey(key))
      {
        return false;
      }

      recordKeys[key] = true;
      IncrementContentPackSummaryCount(counts, recordKind);
      return true;
    }

    private static void IncrementContentPackSummaryCount(
        Dictionary<DimensionContentRecordKind, int> counts,
        DimensionContentRecordKind recordKind)
    {
      int count;
      counts.TryGetValue(recordKind, out count);
      counts[recordKind] = count + 1;
    }

    private static List<DimensionContentPackRecordCount> BuildContentPackRecordCounts(
        Dictionary<DimensionContentRecordKind, int> counts,
        Dictionary<DimensionContentRecordKind, int> orphanCounts)
    {
      List<DimensionContentPackRecordCount> result =
          new List<DimensionContentPackRecordCount>();
      foreach (KeyValuePair<DimensionContentRecordKind, int> pair in counts)
      {
        int orphanedCount;
        orphanCounts.TryGetValue(pair.Key, out orphanedCount);
        result.Add(
            new DimensionContentPackRecordCount(
                pair.Key,
                pair.Value,
                orphanedCount));
      }

      result.Sort(CompareContentPackRecordCounts);
      return result;
    }
  }
}
