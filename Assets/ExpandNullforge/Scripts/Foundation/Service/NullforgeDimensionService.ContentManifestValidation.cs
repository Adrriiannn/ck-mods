using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Checking one record of a manifest before it is applied.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private DimensionOperationResult ValidateManifestContentPack(
        DimensionContentPackDefinition contentPack,
        bool updateExisting,
        Dictionary<string, bool> manifestIds)
    {
      DimensionOperationResult result;
      if (!ValidateContentPack(contentPack, out result))
      {
        return result;
      }

      if (!TryAddManifestId(manifestIds, contentPack.ContentPackId))
      {
        return DimensionOperationResult.Failed("manifest-content-pack-duplicate", "The manifest contains the same content pack more than once.");
      }

      if (!updateExisting && contentPacks.ContainsKey(contentPack.ContentPackId))
      {
        return DimensionOperationResult.Failed("content-pack-already-registered", "A content pack with that id is already registered.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestDimension(
        DimensionDefinition dimension,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        List<DimensionDefinition> acceptedManifestDimensions)
    {
      if (string.IsNullOrEmpty(dimension.Id))
      {
        return DimensionOperationResult.Failed("dimension-id-empty", "A dimension id is required.");
      }

      if (!TryAddManifestId(manifestDimensionIds, dimension.Id))
      {
        return DimensionOperationResult.Failed("manifest-dimension-duplicate", "The manifest contains the same dimension more than once.");
      }

      int2 size = dimension.LocalBounds.Size;
      if (size.x <= 0 || size.y <= 0)
      {
        return DimensionOperationResult.Failed("dimension-bounds-invalid", "A dimension must have positive local bounds.");
      }

      DimensionDefinition existing;
      if (definitions.TryGetValue(dimension.Id, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("dimension-already-registered", "A dimension with that id is already registered.");
        }

        if (!DimensionDefinitionEquals(existing, dimension))
        {
          return DimensionOperationResult.Failed(
              "dimension-update-not-supported",
              "Manifest preflight cannot mutate an existing dimension definition.");
        }

        return DimensionOperationResult.Ok();
      }

      if (dimension.Id != DimensionIds.Overworld && OverlapsExistingNonOverworldDimension(dimension))
      {
        return DimensionOperationResult.Failed(
            "dimension-absolute-bounds-overlap",
            "The requested dimension absolute bounds overlap another registered non-overworld dimension.");
      }

      for (int i = 0; i < acceptedManifestDimensions.Count; i++)
      {
        DimensionDefinition existingManifestDimension = acceptedManifestDimensions[i];
        if (dimension.Id == DimensionIds.Overworld ||
            existingManifestDimension.Id == DimensionIds.Overworld)
        {
          continue;
        }

        if (BoundsOverlap(dimension.AbsoluteBounds, existingManifestDimension.AbsoluteBounds))
        {
          return DimensionOperationResult.Failed(
              "manifest-dimension-absolute-bounds-overlap",
              "The requested dimension absolute bounds overlap another dimension in this manifest.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestZoneDefinition(
        DimensionZoneDefinition zone,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(zone.ZoneId))
      {
        return DimensionOperationResult.Failed("zone-id-empty", "A zone id is required.");
      }

      if (!TryAddManifestId(manifestZoneIds, zone.ZoneId))
      {
        return DimensionOperationResult.Failed("manifest-zone-duplicate", "The manifest contains the same zone more than once.");
      }

      if (!updateExisting && zoneDefinitions.ContainsKey(zone.ZoneId))
      {
        return DimensionOperationResult.Failed("zone-already-registered", "A zone with that id is already registered.");
      }

      if (zone.LocalBounds.Size.x <= 0 || zone.LocalBounds.Size.y <= 0)
      {
        return DimensionOperationResult.Failed("zone-bounds-invalid", "The zone local bounds must have a positive size.");
      }

      if (string.IsNullOrEmpty(zone.DimensionId))
      {
        return DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(zone.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("zone-dimension-not-found", "The zone dimension is not registered or declared by this manifest.");
      }

      if (!dimension.LocalBounds.Contains(zone.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(zone.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        return DimensionOperationResult.Failed("zone-bounds-out-of-dimension", "The zone bounds are outside the zone dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestMapLayer(
        DimensionMapLayerDefinition layer,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestMapLayerIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(layer.LayerId))
      {
        return DimensionOperationResult.Failed("map-layer-id-empty", "A map layer id is required.");
      }

      if (!TryAddManifestId(manifestMapLayerIds, layer.LayerId))
      {
        return DimensionOperationResult.Failed("manifest-map-layer-duplicate", "The manifest contains the same map layer more than once.");
      }

      DimensionMapLayerDefinition existing;
      if (mapLayers.TryGetValue(layer.LayerId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("map-layer-already-registered", "A map layer with that id is already registered.");
        }

        if (IsProtectedMapLayerId(layer.LayerId) && !MapLayerAnchorEquals(existing, layer))
        {
          return DimensionOperationResult.Failed("map-layer-protected", "Built-in map layers cannot be moved to another dimension.");
        }
      }

      if (string.IsNullOrEmpty(layer.DimensionId))
      {
        return DimensionOperationResult.Failed("map-layer-dimension-not-found", "The map layer dimension is not registered or declared by this manifest.");
      }

      if (!TryGetManifestAwareDimension(layer.DimensionId, manifestDimensionIds, manifestDimensions, out _))
      {
        return DimensionOperationResult.Failed("map-layer-dimension-not-found", "The map layer dimension is not registered or declared by this manifest.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestMapMarker(
        DimensionMapMarker marker,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestMapMarkerIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(marker.MarkerId))
      {
        return DimensionOperationResult.Failed("marker-id-empty", "A marker id is required.");
      }

      if (!TryAddManifestId(manifestMapMarkerIds, marker.MarkerId))
      {
        return DimensionOperationResult.Failed("manifest-map-marker-duplicate", "The manifest contains the same map marker more than once.");
      }

      DimensionMapMarker existing;
      if (markers.TryGetValue(marker.MarkerId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("marker-already-registered", "A marker with that id is already registered.");
        }

      }

      if (string.IsNullOrEmpty(marker.DimensionId))
      {
        return DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(marker.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("marker-dimension-not-found", "The marker dimension is not registered or declared by this manifest.");
      }

      if (!dimension.ContainsLocal(marker.LocalPosition))
      {
        return DimensionOperationResult.Failed("marker-position-out-of-bounds", "The marker position is outside the marker dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestAnchor(
        DimensionAnchorDefinition anchor,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestAnchorIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(anchor.AnchorId))
      {
        return DimensionOperationResult.Failed("anchor-id-empty", "An anchor id is required.");
      }

      if (!TryAddManifestId(manifestAnchorIds, anchor.AnchorId))
      {
        return DimensionOperationResult.Failed("manifest-anchor-duplicate", "The manifest contains the same anchor more than once.");
      }

      if (!IsValidAnchorKind(anchor.Kind))
      {
        return DimensionOperationResult.Failed("anchor-kind-invalid", "The anchor kind is not supported.");
      }

      DimensionAnchorDefinition existing;
      if (anchors.TryGetValue(anchor.AnchorId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("anchor-already-registered", "An anchor with that id is already registered.");
        }

      }

      if (string.IsNullOrEmpty(anchor.DimensionId))
      {
        return DimensionOperationResult.Failed("anchor-dimension-not-found", "The anchor dimension is not registered or declared by this manifest.");
      }

      DimensionDefinition dimension;
      if (!TryGetManifestAwareDimension(anchor.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("anchor-dimension-not-found", "The anchor dimension is not registered or declared by this manifest.");
      }

      if (!dimension.ContainsLocal(anchor.LocalPosition))
      {
        return DimensionOperationResult.Failed("anchor-position-out-of-bounds", "The anchor position is outside the anchor dimension.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestPortal(
        DimensionPortalDefinition portal,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestPortalIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(portal.PortalId))
      {
        return DimensionOperationResult.Failed("portal-id-empty", "A portal id is required.");
      }

      if (!TryAddManifestId(manifestPortalIds, portal.PortalId))
      {
        return DimensionOperationResult.Failed("manifest-portal-duplicate", "The manifest contains the same portal more than once.");
      }

      if (!IsValidPortalState(portal.State))
      {
        return DimensionOperationResult.Failed("portal-state-invalid", "The portal state is not valid.");
      }

      DimensionPortalDefinition existing;
      if (portals.TryGetValue(portal.PortalId, out existing))
      {
        if (!updateExisting)
        {
          return DimensionOperationResult.Failed("portal-already-registered", "A portal with that id is already registered.");
        }

        if (!PortalDefinitionRouteEquals(existing, portal))
        {
          return DimensionOperationResult.Failed(
              "portal-update-not-supported",
              "Manifest preflight cannot move or reroute an existing portal definition.");
        }
      }

      if (string.IsNullOrEmpty(portal.FromDimensionId) ||
          string.IsNullOrEmpty(portal.ToDimensionId))
      {
        return DimensionOperationResult.Failed("portal-dimension-not-found", "Both portal dimensions must be registered or declared by this manifest.");
      }

      DimensionDefinition fromDimension;
      DimensionDefinition toDimension;
      if (!TryGetManifestAwareDimension(portal.FromDimensionId, manifestDimensionIds, manifestDimensions, out fromDimension) ||
          !TryGetManifestAwareDimension(portal.ToDimensionId, manifestDimensionIds, manifestDimensions, out toDimension))
      {
        return DimensionOperationResult.Failed("portal-dimension-not-found", "Both portal dimensions must be registered or declared by this manifest.");
      }

      if (!fromDimension.ContainsLocal(portal.FromLocalPosition) ||
          !toDimension.ContainsLocal(portal.ToLocalPosition))
      {
        return DimensionOperationResult.Failed("portal-position-out-of-bounds", "Portal positions must be inside their dimensions.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestPortalPresentation(
        DimensionPortalPresentationDefinition presentation,
        bool updateExisting,
        Dictionary<string, bool> manifestPortalIds,
        Dictionary<string, bool> manifestPresentationIds)
    {
      if (string.IsNullOrEmpty(presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("portal-presentation-id-empty", "A portal presentation id is required.");
      }

      if (!TryAddManifestId(manifestPresentationIds, presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("manifest-portal-presentation-duplicate", "The manifest contains the same portal presentation more than once.");
      }

      if (!updateExisting && portalPresentations.ContainsKey(presentation.PresentationId))
      {
        return DimensionOperationResult.Failed("portal-presentation-already-registered", "A portal presentation with that id is already registered.");
      }

      if (string.IsNullOrEmpty(presentation.PortalId))
      {
        return DimensionOperationResult.Failed("portal-presentation-portal-empty", "A portal id is required.");
      }

      if (!PortalKnownForManifest(presentation.PortalId, manifestPortalIds))
      {
        return DimensionOperationResult.Failed("portal-presentation-portal-not-found", "The portal presentation target portal is not registered or declared by this manifest.");
      }

      if (presentation.CooldownSeconds < 0f)
      {
        return DimensionOperationResult.Failed("portal-presentation-cooldown-invalid", "A portal presentation cooldown cannot be negative.");
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestProgressFlag(
        DimensionProgressFlag flag,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestFlagIds,
        List<DimensionDefinition> manifestDimensions)
    {
      if (string.IsNullOrEmpty(flag.FlagId))
      {
        return DimensionOperationResult.Failed("progress-flag-id-empty", "A progress flag id is required.");
      }

      if (!TryAddManifestId(manifestFlagIds, flag.FlagId))
      {
        return DimensionOperationResult.Failed("manifest-progress-flag-duplicate", "The manifest contains the same progress flag more than once.");
      }

      if (!updateExisting && progressFlags.ContainsKey(flag.FlagId))
      {
        return DimensionOperationResult.Failed("progress-flag-already-registered", "A progress flag with that id is already registered.");
      }

      if (!string.IsNullOrEmpty(flag.DimensionId))
      {
        DimensionDefinition dimension;
        if (!TryGetManifestAwareDimension(flag.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
        {
          return DimensionOperationResult.Failed("progress-flag-dimension-not-found", "The progress flag dimension is not registered or declared by this manifest.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestTravelRequirement(
        DimensionTravelRequirementDefinition requirement,
        bool updateExisting,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestPortalIds,
        Dictionary<string, bool> manifestRequirementIds,
        List<DimensionDefinition> manifestDimensions,
        List<DimensionPortalDefinition> manifestPortals,
        Dictionary<string, bool> manifestProgressFlagIds,
        List<DimensionProgressFlag> manifestProgressFlags)
    {
      if (string.IsNullOrEmpty(requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("travel-requirement-id-empty", "A travel requirement id is required.");
      }

      if (!TryAddManifestId(manifestRequirementIds, requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("manifest-travel-requirement-duplicate", "The manifest contains the same travel requirement more than once.");
      }

      if (!updateExisting && travelRequirements.ContainsKey(requirement.RequirementId))
      {
        return DimensionOperationResult.Failed("travel-requirement-already-registered", "A travel requirement with that id is already registered.");
      }

      if (string.IsNullOrEmpty(requirement.PortalId) &&
          string.IsNullOrEmpty(requirement.DimensionId))
      {
        return DimensionOperationResult.Failed("travel-requirement-target-empty", "A travel requirement needs a portal id, dimension id, or both.");
      }

      if (!IsValidTravelRequirementKind(requirement.Kind) ||
          requirement.Kind == DimensionTravelRequirementKind.Any)
      {
        return DimensionOperationResult.Failed("travel-requirement-kind-invalid", "The travel requirement kind is not supported.");
      }

      if (requirement.RequiredAmount < 0)
      {
        return DimensionOperationResult.Failed("travel-requirement-amount-invalid", "A travel requirement amount cannot be negative.");
      }

      if (RequirementKindNeedsSubject(requirement.Kind) &&
          string.IsNullOrEmpty(requirement.SubjectId))
      {
        return DimensionOperationResult.Failed("travel-requirement-subject-empty", "This travel requirement kind needs a subject id.");
      }

      DimensionPortalDefinition portal;
      bool hasPortal = false;
      if (!string.IsNullOrEmpty(requirement.PortalId))
      {
        if (!TryGetManifestAwarePortal(requirement.PortalId, manifestPortalIds, manifestPortals, out portal))
        {
          return DimensionOperationResult.Failed("travel-requirement-portal-not-found", "The travel requirement portal is not registered or declared by this manifest.");
        }

        hasPortal = true;
      }
      else
      {
        portal = default(DimensionPortalDefinition);
      }

      DimensionDefinition dimension;
      if (!string.IsNullOrEmpty(requirement.DimensionId) &&
          !TryGetManifestAwareDimension(requirement.DimensionId, manifestDimensionIds, manifestDimensions, out dimension))
      {
        return DimensionOperationResult.Failed("travel-requirement-dimension-not-found", "The travel requirement dimension is not registered or declared by this manifest.");
      }

      if (hasPortal &&
          !string.IsNullOrEmpty(requirement.DimensionId) &&
          !string.Equals(portal.ToDimensionId, requirement.DimensionId, StringComparison.Ordinal))
      {
        return DimensionOperationResult.Failed("travel-requirement-portal-dimension-mismatch", "The travel requirement dimension must match the portal destination dimension.");
      }

      if (requirement.Kind == DimensionTravelRequirementKind.ProgressFlag &&
          !string.IsNullOrEmpty(requirement.SubjectId))
      {
        DimensionProgressFlag flag;
        if (TryGetManifestAwareProgressFlag(requirement.SubjectId, manifestProgressFlagIds, manifestProgressFlags, out flag) &&
            !string.IsNullOrEmpty(flag.DimensionId) &&
            !string.IsNullOrEmpty(requirement.DimensionId) &&
            !string.Equals(flag.DimensionId, requirement.DimensionId, StringComparison.Ordinal))
        {
          return DimensionOperationResult.Failed("travel-requirement-progress-flag-dimension-mismatch", "The travel requirement progress flag belongs to another dimension.");
        }
      }

      return DimensionOperationResult.Ok();
    }

    private DimensionOperationResult ValidateManifestAssetReference(
        DimensionAssetReferenceDefinition assetReference,
        bool updateExisting,
        Dictionary<string, bool> manifestContentPackIds,
        Dictionary<string, bool> manifestDimensionIds,
        Dictionary<string, bool> manifestZoneIds,
        Dictionary<string, bool> manifestAssetReferenceIds)
    {
      if (string.IsNullOrEmpty(assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("asset-reference-id-empty", "An asset reference id is required.");
      }

      if (!TryAddManifestId(manifestAssetReferenceIds, assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("manifest-asset-reference-duplicate", "The manifest contains the same asset reference more than once.");
      }

      if (!updateExisting && assetReferences.ContainsKey(assetReference.AssetId))
      {
        return DimensionOperationResult.Failed("asset-reference-already-registered", "An asset reference with that id is already registered.");
      }

      if (string.IsNullOrEmpty(assetReference.ContentPackId))
      {
        return DimensionOperationResult.Failed("content-pack-id-empty", "A content pack id is required.");
      }

      if (!ContentPackKnownForManifest(assetReference.ContentPackId, manifestContentPackIds))
      {
        return DimensionOperationResult.Failed("content-pack-not-found", "No content pack with that id is registered or declared by this manifest.");
      }

      if (!IsValidAssetReferenceKind(assetReference.Kind))
      {
        return DimensionOperationResult.Failed("asset-reference-kind-invalid", "A valid asset reference kind is required.");
      }

      if (string.IsNullOrEmpty(assetReference.ResourceKey))
      {
        return DimensionOperationResult.Failed("asset-reference-resource-key-empty", "An asset reference resource key is required.");
      }

      if (!string.IsNullOrEmpty(assetReference.DimensionId) &&
          !DimensionKnownForManifest(assetReference.DimensionId, manifestDimensionIds))
      {
        return DimensionOperationResult.Failed("asset-reference-dimension-not-found", "No dimension with that id is registered.");
      }

      if (!string.IsNullOrEmpty(assetReference.ZoneId) &&
          !ZoneKnownForManifest(assetReference.ZoneId, manifestZoneIds))
      {
        return DimensionOperationResult.Failed("asset-reference-zone-not-found", "No zone with that id is registered.");
      }

      return DimensionOperationResult.Ok();
    }
  }
}
