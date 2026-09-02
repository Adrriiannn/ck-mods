using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static int CompareAssetReferences(
        DimensionAssetReferenceDefinition left,
        DimensionAssetReferenceDefinition right)
    {
      int byPriority = right.Priority.CompareTo(left.Priority);
      if (byPriority != 0)
      {
        return byPriority;
      }

      int byKind = left.Kind.CompareTo(right.Kind);
      if (byKind != 0)
      {
        return byKind;
      }

      return string.Compare(left.AssetId, right.AssetId, StringComparison.Ordinal);
    }

    private bool AssetReferenceMatchesQuery(
        DimensionAssetReferenceDefinition assetReference,
        DimensionAssetReferenceQuery query)
    {
      if (query.EnabledOnly && !assetReference.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.ContentPackId) &&
          !string.Equals(assetReference.ContentPackId, query.ContentPackId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(assetReference.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.ZoneId) &&
          !string.Equals(assetReference.ZoneId, query.ZoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.Kind != DimensionAssetReferenceKind.Any &&
          assetReference.Kind != query.Kind)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.VariantId) &&
          !string.Equals(assetReference.VariantId, query.VariantId, StringComparison.Ordinal))
      {
        return false;
      }

      return true;
    }

    private bool AssetReferenceAppliesToResolution(
        DimensionAssetReferenceDefinition assetReference,
        DimensionAssetReferenceResolutionRequest request,
        string resolvedZoneId)
    {
      if (!request.IncludeDisabled)
      {
        if (!assetReference.Enabled ||
            !ContentPackExistsAndEnabled(assetReference.ContentPackId, true))
        {
          return false;
        }
      }

      if (!string.IsNullOrEmpty(request.ContentPackId) &&
          !string.Equals(assetReference.ContentPackId, request.ContentPackId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(assetReference.DimensionId) &&
          !string.Equals(assetReference.DimensionId, request.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(assetReference.ZoneId) &&
          !string.Equals(assetReference.ZoneId, resolvedZoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (request.Kind != DimensionAssetReferenceKind.Any &&
          assetReference.Kind != request.Kind)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(assetReference.VariantId) &&
          !string.Equals(assetReference.VariantId, request.VariantId, StringComparison.Ordinal))
      {
        return false;
      }

      return true;
    }

    private bool ValidateAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(assetReference.AssetId))
      {
        result = DimensionOperationResult.Failed("asset-reference-id-empty", "An asset reference id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(assetReference.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
        return false;
      }

      if (!contentPacks.ContainsKey(assetReference.ContentPackId))
      {
        result = DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered.");
        return false;
      }

      if (!IsValidAssetReferenceKind(assetReference.Kind))
      {
        result = DimensionOperationResult.Failed("asset-reference-kind-invalid", "A valid asset reference kind is required.");
        return false;
      }

      if (string.IsNullOrEmpty(assetReference.ResourceKey))
      {
        result = DimensionOperationResult.Failed("asset-reference-resource-key-empty", "An asset reference resource key is required.");
        return false;
      }

      if (!string.IsNullOrEmpty(assetReference.DimensionId) &&
          !definitions.ContainsKey(assetReference.DimensionId))
      {
        result = DimensionOperationResult.Failed("asset-reference-dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(assetReference.ZoneId) &&
          !zoneDefinitions.ContainsKey(assetReference.ZoneId))
      {
        result = DimensionOperationResult.Failed("asset-reference-zone-not-found", "No zone with that id is registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool IsValidAssetReferenceKind(DimensionAssetReferenceKind kind)
    {
      return kind == DimensionAssetReferenceKind.Prefab ||
             kind == DimensionAssetReferenceKind.Sprite ||
             kind == DimensionAssetReferenceKind.Material ||
             kind == DimensionAssetReferenceKind.Audio ||
             kind == DimensionAssetReferenceKind.Music ||
             kind == DimensionAssetReferenceKind.VisualEffect ||
             kind == DimensionAssetReferenceKind.Tile ||
             kind == DimensionAssetReferenceKind.Object ||
             kind == DimensionAssetReferenceKind.Item ||
             kind == DimensionAssetReferenceKind.Icon ||
             kind == DimensionAssetReferenceKind.Cursor ||
             kind == DimensionAssetReferenceKind.UiSprite ||
             kind == DimensionAssetReferenceKind.Palette ||
             kind == DimensionAssetReferenceKind.Scene ||
             kind == DimensionAssetReferenceKind.Custom;
    }

    private static bool AssetReferenceEquals(
        DimensionAssetReferenceDefinition a,
        DimensionAssetReferenceDefinition b)
    {
      return string.Equals(a.AssetId, b.AssetId, StringComparison.Ordinal) &&
             string.Equals(a.ContentPackId, b.ContentPackId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             a.Kind == b.Kind &&
             string.Equals(a.ResourceKey, b.ResourceKey, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.VariantId, b.VariantId, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled &&
             string.Equals(a.Notes, b.Notes, StringComparison.Ordinal);
    }

    private void RemoveAssetReferencesForContentPack(string contentPackId)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionAssetReferenceDefinition> pair in assetReferences)
      {
        if (string.Equals(pair.Value.ContentPackId, contentPackId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionAssetReferenceDefinition assetReference = assetReferences[keysToRemove[i]];
        assetReferences.Remove(keysToRemove[i]);
        RaiseAssetReferenceChanged(
            assetReference,
            DimensionAssetReferenceChangeKind.ContentPackRemoved,
            assetReference.Enabled,
            false,
            "content pack removed");
      }
    }
  }
}