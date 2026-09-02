using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidatePortalPresentation(
        DimensionPortalPresentationDefinition presentation,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(presentation.PresentationId))
      {
        result = DimensionOperationResult.Failed("portal-presentation-id-empty", "A portal presentation id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(presentation.PortalId))
      {
        result = DimensionOperationResult.Failed("portal-presentation-portal-empty", "A portal id is required.");
        return false;
      }

      if (presentation.CooldownSeconds < 0f)
      {
        result = DimensionOperationResult.Failed("portal-presentation-cooldown-invalid", "A portal presentation cooldown cannot be negative.");
        return false;
      }

      DimensionPortalDefinition portal;
      if (portals.TryGetValue(presentation.PortalId, out portal))
      {
        DimensionDefinition fromDimension;
        DimensionDefinition toDimension;
        if (!TryGetDimension(portal.FromDimensionId, out fromDimension) ||
            !TryGetDimension(portal.ToDimensionId, out toDimension))
        {
          result = DimensionOperationResult.Failed("portal-presentation-route-invalid", "The portal presentation route is not valid.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int ComparePortalPresentations(
        DimensionPortalPresentationDefinition left,
        DimensionPortalPresentationDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int portal = string.Compare(left.PortalId, right.PortalId, StringComparison.Ordinal);
      if (portal != 0)
      {
        return portal;
      }

      return string.Compare(left.PresentationId, right.PresentationId, StringComparison.Ordinal);
    }

    private bool PortalPresentationEquals(
        DimensionPortalPresentationDefinition a,
        DimensionPortalPresentationDefinition b)
    {
      return string.Equals(a.PresentationId, b.PresentationId, StringComparison.Ordinal) &&
             string.Equals(a.PortalId, b.PortalId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.PromptText, b.PromptText, StringComparison.Ordinal) &&
             string.Equals(a.LockedPromptText, b.LockedPromptText, StringComparison.Ordinal) &&
             string.Equals(a.IconId, b.IconId, StringComparison.Ordinal) &&
             string.Equals(a.VisualEffectId, b.VisualEffectId, StringComparison.Ordinal) &&
             string.Equals(a.AudioCueId, b.AudioCueId, StringComparison.Ordinal) &&
             a.CooldownSeconds == b.CooldownSeconds &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled &&
             a.RequireGeneratedAreaOnUse == b.RequireGeneratedAreaOnUse &&
             a.AllowFallbackPositionOnUse == b.AllowFallbackPositionOnUse &&
             a.Interactable == b.Interactable;
    }
  }
}
