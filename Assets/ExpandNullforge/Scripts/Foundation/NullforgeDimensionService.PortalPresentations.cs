using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionPortalPresentationDefinition> GetPortalPresentations(
        string portalId,
        bool includeDisabled)
    {
      List<DimensionPortalPresentationDefinition> result =
          new List<DimensionPortalPresentationDefinition>();
      foreach (DimensionPortalPresentationDefinition presentation in portalPresentations.Values)
      {
        if (!string.IsNullOrEmpty(portalId) &&
            !string.Equals(presentation.PortalId, portalId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && !presentation.Enabled)
        {
          continue;
        }

        result.Add(presentation);
      }

      result.Sort(ComparePortalPresentations);
      return result;
    }

    public bool TryGetPortalPresentation(
        string presentationId,
        out DimensionPortalPresentationDefinition presentation)
    {
      if (string.IsNullOrEmpty(presentationId))
      {
        presentation = default(DimensionPortalPresentationDefinition);
        return false;
      }

      return portalPresentations.TryGetValue(presentationId, out presentation);
    }

    public bool TryFindPortalPresentationForPortal(
        string portalId,
        out DimensionPortalPresentationDefinition presentation)
    {
      presentation = default(DimensionPortalPresentationDefinition);
      if (string.IsNullOrEmpty(portalId))
      {
        return false;
      }

      IReadOnlyList<DimensionPortalPresentationDefinition> presentations =
          GetPortalPresentations(portalId, false);
      if (presentations.Count == 0)
      {
        return false;
      }

      presentation = presentations[0];
      return true;
    }

    public bool TryRegisterPortalPresentation(
        DimensionPortalPresentationDefinition presentation,
        out DimensionOperationResult result)
    {
      if (!ValidatePortalPresentation(presentation, out result))
      {
        return false;
      }

      if (portalPresentations.ContainsKey(presentation.PresentationId))
      {
        result = DimensionOperationResult.Failed("portal-presentation-already-registered", "A portal presentation with that id is already registered.");
        return false;
      }

      portalPresentations[presentation.PresentationId] = presentation;
      RaisePortalPresentationChanged(
          presentation,
          DimensionPortalPresentationChangeKind.Registered,
          false,
          presentation.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdatePortalPresentation(
        DimensionPortalPresentationDefinition presentation,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionPortalPresentationDefinition previous;
      if (!portalPresentations.TryGetValue(presentation.PresentationId, out previous))
      {
        result = DimensionOperationResult.Failed("portal-presentation-not-found", "No portal presentation with that id is registered.");
        return false;
      }

      if (!ValidatePortalPresentation(presentation, out result))
      {
        return false;
      }

      if (PortalPresentationEquals(previous, presentation))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      portalPresentations[presentation.PresentationId] = presentation;
      RaisePortalPresentationChanged(
          presentation,
          previous.Enabled == presentation.Enabled
              ? DimensionPortalPresentationChangeKind.Updated
              : DimensionPortalPresentationChangeKind.EnabledChanged,
          previous.Enabled,
          presentation.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetPortalPresentationEnabled(
        string presentationId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(presentationId))
      {
        result = DimensionOperationResult.Failed("portal-presentation-id-empty", "A portal presentation id is required.");
        return false;
      }

      DimensionPortalPresentationDefinition presentation;
      if (!portalPresentations.TryGetValue(presentationId, out presentation))
      {
        result = DimensionOperationResult.Failed("portal-presentation-not-found", "No portal presentation with that id is registered.");
        return false;
      }

      if (presentation.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionPortalPresentationDefinition updated =
          new DimensionPortalPresentationDefinition(
              presentation.PresentationId,
              presentation.PortalId,
              presentation.DisplayName,
              presentation.PromptText,
              presentation.LockedPromptText,
              presentation.IconId,
              presentation.VisualEffectId,
              presentation.AudioCueId,
              presentation.CooldownSeconds,
              presentation.Priority,
              enabled,
              presentation.RequireGeneratedAreaOnUse,
              presentation.AllowFallbackPositionOnUse,
              presentation.Interactable);

      portalPresentations[presentationId] = updated;
      RaisePortalPresentationChanged(
          updated,
          DimensionPortalPresentationChangeKind.EnabledChanged,
          presentation.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemovePortalPresentation(
        string presentationId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(presentationId))
      {
        result = DimensionOperationResult.Failed("portal-presentation-id-empty", "A portal presentation id is required.");
        return false;
      }

      DimensionPortalPresentationDefinition presentation;
      if (!portalPresentations.TryGetValue(presentationId, out presentation))
      {
        result = DimensionOperationResult.Failed("portal-presentation-not-found", "No portal presentation with that id is registered.");
        return false;
      }

      portalPresentations.Remove(presentationId);
      RaisePortalPresentationChanged(
          presentation,
          DimensionPortalPresentationChangeKind.Removed,
          presentation.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
