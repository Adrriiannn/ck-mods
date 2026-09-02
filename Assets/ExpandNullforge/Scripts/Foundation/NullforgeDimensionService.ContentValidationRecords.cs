using System.Collections.Generic;
using ExpandNullforge.Api;

using System;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Checking the records a dimension is made of: zones, layers, markers, anchors, portals.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    /// <summary>
    /// Checks every zone this service holds.
    /// </summary>
    private void ValidateZoneRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionZoneDefinition zone in zoneDefinitions.Values)
      {
        if (!ShouldValidateContentRecord(
                zone.Enabled,
                DimensionContentRecordKind.ZoneDefinition,
                zone.ZoneId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.ZoneDefinition, zone.ZoneId);
        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.ZoneDefinition,
                zone.ZoneId,
                ownerId,
                zone.DimensionId,
                "zone",
                "zone",
                out dimension))
        {
          ValidateContentBoundsInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.ZoneDefinition,
              zone.ZoneId,
              ownerId,
              zone.LocalBounds,
              dimension,
              "zone",
              "zone");
        }
      }
    }

    /// <summary>
    /// Checks every map layer this service holds.
    /// </summary>
    private void ValidateMapLayerRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionMapLayerDefinition layer in mapLayers.Values)
      {
        bool enabled = layer.Visible || layer.Selectable;
        if (!ShouldValidateContentRecord(
                enabled,
                DimensionContentRecordKind.MapLayer,
                layer.LayerId,
                string.Empty,
                request))
        {
          continue;
        }

        DimensionDefinition ignoredDimension;
        TryValidateContentDimensionReference(
            issues,
            request,
            ref errorCount,
            ref warningCount,
            DimensionContentRecordKind.MapLayer,
            layer.LayerId,
            GetContentValidationOwnerId(DimensionContentRecordKind.MapLayer, layer.LayerId),
            layer.DimensionId,
            "map-layer",
            "map layer",
            out ignoredDimension);
      }
    }

    /// <summary>
    /// Checks every map marker this service holds.
    /// </summary>
    private void ValidateMapMarkerRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionMapMarker marker in markers.Values)
      {
        if (!ShouldValidateContentRecord(
                marker.Visible,
                DimensionContentRecordKind.MapMarker,
                marker.MarkerId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.MapMarker, marker.MarkerId);
        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.MapMarker,
                marker.MarkerId,
                ownerId,
                marker.DimensionId,
                "map-marker",
                "map marker",
                out dimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.MapMarker,
              marker.MarkerId,
              ownerId,
              marker.LocalPosition,
              dimension,
              "map-marker",
              "map marker");
        }
      }
    }

    /// <summary>
    /// Checks every anchor this service holds.
    /// </summary>
    private void ValidateAnchorRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionAnchorDefinition anchor in anchors.Values)
      {
        if (!ShouldValidateContentRecord(
                anchor.Enabled,
                DimensionContentRecordKind.Anchor,
                anchor.AnchorId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Anchor, anchor.AnchorId);
        if (!IsValidAnchorKind(anchor.Kind))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Anchor,
              anchor.AnchorId,
              ownerId,
              "anchor-kind-invalid",
              "The anchor kind is not supported.");
        }

        DimensionDefinition dimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Anchor,
                anchor.AnchorId,
                ownerId,
                anchor.DimensionId,
                "anchor",
                "anchor",
                out dimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Anchor,
              anchor.AnchorId,
              ownerId,
              anchor.LocalPosition,
              dimension,
              "anchor",
              "anchor");
        }
      }
    }

    /// <summary>
    /// Checks every portal this service holds.
    /// </summary>
    private void ValidatePortalRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionPortalDefinition portal in portals.Values)
      {
        bool enabled = portal.State != DimensionPortalState.Disabled;
        if (!ShouldValidateContentRecord(
                enabled,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.Portal, portal.PortalId);
        if (!IsValidPortalState(portal.State))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              "portal-state-invalid",
              "The portal state is not supported.");
        }

        DimensionDefinition fromDimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                ownerId,
                portal.FromDimensionId,
                "portal-source",
                "portal source",
                out fromDimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              portal.FromLocalPosition,
              fromDimension,
              "portal-source",
              "portal source");
        }

        DimensionDefinition toDimension;
        if (TryValidateContentDimensionReference(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionContentRecordKind.Portal,
                portal.PortalId,
                ownerId,
                portal.ToDimensionId,
                "portal-target",
                "portal target",
                out toDimension))
        {
          ValidateContentPositionInsideDimension(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.Portal,
              portal.PortalId,
              ownerId,
              portal.ToLocalPosition,
              toDimension,
              "portal-target",
              "portal target");
        }
      }
    }

    /// <summary>
    /// Checks every portal presentation this service holds.
    /// </summary>
    private void ValidatePortalPresentationRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionPortalPresentationDefinition presentation in portalPresentations.Values)
      {
        if (!ShouldValidateContentRecord(
                presentation.Enabled,
                DimensionContentRecordKind.PortalPresentation,
                presentation.PresentationId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.PortalPresentation, presentation.PresentationId);
        DimensionPortalDefinition portal;
        if (!portals.TryGetValue(presentation.PortalId, out portal))
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.PortalPresentation,
              presentation.PresentationId,
              ownerId,
              "portal-presentation-portal-missing",
              "The portal presentation points at a missing portal.");
        }
        else if (portal.State == DimensionPortalState.Disabled)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Warning,
              DimensionContentRecordKind.PortalPresentation,
              presentation.PresentationId,
              ownerId,
              "portal-presentation-portal-disabled",
              "The portal presentation points at a disabled portal.");
        }
      }
    }

    /// <summary>
    /// Checks every travel requirement this service holds.
    /// </summary>
    private void ValidateTravelRequirementRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionTravelRequirementDefinition requirement in travelRequirements.Values)
      {
        if (!ShouldValidateContentRecord(
                requirement.Enabled,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                string.Empty,
                request))
        {
          continue;
        }

        string ownerId = GetContentValidationOwnerId(DimensionContentRecordKind.TravelRequirement, requirement.RequirementId);
        if (!IsValidTravelRequirementKind(requirement.Kind) ||
            requirement.Kind == DimensionTravelRequirementKind.Any)
        {
          AddContentValidationIssue(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionDiagnosticSeverity.Error,
              DimensionContentRecordKind.TravelRequirement,
              requirement.RequirementId,
              ownerId,
              "travel-requirement-kind-invalid",
              "The travel requirement kind is not supported.");
        }

        DimensionPortalDefinition portal;
        if (!string.IsNullOrEmpty(requirement.PortalId))
        {
          if (!portals.TryGetValue(requirement.PortalId, out portal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-portal-missing",
                "The travel requirement points at a missing portal.");
          }
          else if (!string.IsNullOrEmpty(requirement.DimensionId) &&
                   !string.Equals(portal.ToDimensionId, requirement.DimensionId, StringComparison.Ordinal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-portal-dimension-mismatch",
                "The travel requirement dimension does not match its portal destination.");
          }
        }

        if (!string.IsNullOrEmpty(requirement.DimensionId))
        {
          DimensionDefinition ignoredDimension;
          TryValidateContentDimensionReference(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.TravelRequirement,
              requirement.RequirementId,
              ownerId,
              requirement.DimensionId,
              "travel-requirement",
              "travel requirement",
              out ignoredDimension);
        }

        if (requirement.Kind == DimensionTravelRequirementKind.ProgressFlag &&
            !string.IsNullOrEmpty(requirement.SubjectId))
        {
          DimensionProgressFlag flag;
          if (!progressFlags.TryGetValue(requirement.SubjectId, out flag))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-progress-flag-missing",
                "The progress-flag travel requirement points at a missing progress flag.");
          }
          else if (!string.IsNullOrEmpty(requirement.DimensionId) &&
                   !string.IsNullOrEmpty(flag.DimensionId) &&
                   !string.Equals(flag.DimensionId, requirement.DimensionId, StringComparison.Ordinal))
          {
            AddContentValidationIssue(
                issues,
                request,
                ref errorCount,
                ref warningCount,
                DimensionDiagnosticSeverity.Error,
                DimensionContentRecordKind.TravelRequirement,
                requirement.RequirementId,
                ownerId,
                "travel-requirement-progress-flag-dimension-mismatch",
                "The progress-flag travel requirement points at a flag from another dimension.");
          }
        }
      }
    }

    /// <summary>
    /// Checks every progress flag this service holds.
    /// </summary>
    private void ValidateProgressFlagRecords(
        DimensionContentValidationRequest request,
        List<DimensionContentValidationIssue> issues,
        ref int errorCount,
        ref int warningCount)
    {
      foreach (DimensionProgressFlag flag in progressFlags.Values)
      {
        if (!ShouldValidateContentRecord(
                true,
                DimensionContentRecordKind.ProgressFlag,
                flag.FlagId,
                string.Empty,
                request))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(flag.DimensionId))
        {
          DimensionDefinition ignoredDimension;
          TryValidateContentDimensionReference(
              issues,
              request,
              ref errorCount,
              ref warningCount,
              DimensionContentRecordKind.ProgressFlag,
              flag.FlagId,
              GetContentValidationOwnerId(DimensionContentRecordKind.ProgressFlag, flag.FlagId),
              flag.DimensionId,
              "progress-flag",
              "progress flag",
              out ignoredDimension);
        }
      }
    }
  }
}
