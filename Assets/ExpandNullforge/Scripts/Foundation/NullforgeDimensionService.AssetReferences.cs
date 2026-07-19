using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionAssetReferenceDefinition> GetAssetReferences(
        DimensionAssetReferenceQuery query)
    {
      List<DimensionAssetReferenceDefinition> result = new List<DimensionAssetReferenceDefinition>();
      foreach (DimensionAssetReferenceDefinition assetReference in assetReferences.Values)
      {
        if (!AssetReferenceMatchesQuery(assetReference, query))
        {
          continue;
        }

        result.Add(assetReference);
      }

      result.Sort(CompareAssetReferences);
      return result;
    }

    public DimensionAssetReferenceResolutionResult ResolveAssetReferences(
        DimensionAssetReferenceResolutionRequest request)
    {
      if (string.IsNullOrEmpty(request.DimensionId))
      {
        return new DimensionAssetReferenceResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            request.ZoneId,
            request.Kind,
            request.VariantId,
            new List<DimensionAssetReferenceDefinition>(),
            0,
            "dimension-id-empty",
            "A dimension id is required.");
      }

      if (request.Kind != DimensionAssetReferenceKind.Any &&
          !IsValidAssetReferenceKind(request.Kind))
      {
        return new DimensionAssetReferenceResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            request.ZoneId,
            request.Kind,
            request.VariantId,
            new List<DimensionAssetReferenceDefinition>(),
            0,
            "asset-reference-kind-invalid",
            "A valid asset reference kind is required.");
      }

      DimensionDefinition dimension;
      if (!definitions.TryGetValue(request.DimensionId, out dimension))
      {
        return new DimensionAssetReferenceResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            request.ZoneId,
            request.Kind,
            request.VariantId,
            new List<DimensionAssetReferenceDefinition>(),
            0,
            "dimension-not-found",
            "No dimension with that id is registered.");
      }

      DimensionZoneInfo zone = default(DimensionZoneInfo);
      bool hasZone = false;
      string zoneId = request.ZoneId;
      if (request.ResolveZoneFromPosition)
      {
        if (!dimension.ContainsLocal(request.LocalPosition))
        {
          return new DimensionAssetReferenceResolutionResult(
              false,
              request.DimensionId,
              request.LocalPosition,
              false,
              default(DimensionZoneInfo),
              request.ZoneId,
              request.Kind,
              request.VariantId,
              new List<DimensionAssetReferenceDefinition>(),
              0,
              "dimension-position-not-found",
              "No dimension contains that local position.");
        }

        if (TryGetZoneAtLocal(request.DimensionId, request.LocalPosition, out zone))
        {
          hasZone = true;
          zoneId = zone.ZoneId;
        }
        else
        {
          zoneId = string.Empty;
        }
      }

      List<DimensionAssetReferenceDefinition> result =
          new List<DimensionAssetReferenceDefinition>();
      foreach (DimensionAssetReferenceDefinition assetReference in assetReferences.Values)
      {
        if (!AssetReferenceAppliesToResolution(assetReference, request, zoneId))
        {
          continue;
        }

        result.Add(assetReference);
      }

      result.Sort(CompareAssetReferences);
      return new DimensionAssetReferenceResolutionResult(
          true,
          request.DimensionId,
          request.LocalPosition,
          hasZone,
          zone,
          zoneId,
          request.Kind,
          request.VariantId,
          result,
          result.Count,
          string.Empty,
          string.Empty);
    }

    public bool TryGetAssetReference(
        string assetId,
        out DimensionAssetReferenceDefinition assetReference)
    {
      if (string.IsNullOrEmpty(assetId))
      {
        assetReference = default(DimensionAssetReferenceDefinition);
        return false;
      }

      return assetReferences.TryGetValue(assetId, out assetReference);
    }

    public bool TryRegisterAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        out DimensionOperationResult result)
    {
      if (!ValidateAssetReference(assetReference, out result))
      {
        return false;
      }

      if (assetReferences.ContainsKey(assetReference.AssetId))
      {
        result = DimensionOperationResult.Failed("asset-reference-already-registered", "An asset reference with that id is already registered.");
        return false;
      }

      assetReferences[assetReference.AssetId] = assetReference;
      RaiseAssetReferenceChanged(
          assetReference,
          DimensionAssetReferenceChangeKind.Registered,
          false,
          assetReference.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateAssetReference(assetReference, out result))
      {
        return false;
      }

      DimensionAssetReferenceDefinition previous;
      if (!assetReferences.TryGetValue(assetReference.AssetId, out previous))
      {
        result = DimensionOperationResult.Failed("asset-reference-not-found", "No asset reference with that id is registered.");
        return false;
      }

      if (AssetReferenceEquals(previous, assetReference))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      assetReferences[assetReference.AssetId] = assetReference;
      RaiseAssetReferenceChanged(
          assetReference,
          previous.Enabled == assetReference.Enabled
              ? DimensionAssetReferenceChangeKind.Updated
              : DimensionAssetReferenceChangeKind.EnabledChanged,
          previous.Enabled,
          assetReference.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetAssetReferenceEnabled(
        string assetId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(assetId))
      {
        result = DimensionOperationResult.Failed("asset-reference-id-empty", "An asset reference id is required.");
        return false;
      }

      DimensionAssetReferenceDefinition assetReference;
      if (!assetReferences.TryGetValue(assetId, out assetReference))
      {
        result = DimensionOperationResult.Failed("asset-reference-not-found", "No asset reference with that id is registered.");
        return false;
      }

      if (assetReference.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionAssetReferenceDefinition updated =
          new DimensionAssetReferenceDefinition(
              assetReference.AssetId,
              assetReference.ContentPackId,
              assetReference.DisplayName,
              assetReference.Kind,
              assetReference.ResourceKey,
              assetReference.DimensionId,
              assetReference.ZoneId,
              assetReference.VariantId,
              assetReference.Priority,
              enabled,
              assetReference.Notes);

      assetReferences[assetId] = updated;
      RaiseAssetReferenceChanged(
          updated,
          DimensionAssetReferenceChangeKind.EnabledChanged,
          assetReference.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveAssetReference(
        string assetId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(assetId))
      {
        result = DimensionOperationResult.Failed("asset-reference-id-empty", "An asset reference id is required.");
        return false;
      }

      DimensionAssetReferenceDefinition assetReference;
      if (!assetReferences.TryGetValue(assetId, out assetReference))
      {
        result = DimensionOperationResult.Failed("asset-reference-not-found", "No asset reference with that id is registered.");
        return false;
      }

      assetReferences.Remove(assetId);
      RaiseAssetReferenceChanged(
          assetReference,
          DimensionAssetReferenceChangeKind.Removed,
          assetReference.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
