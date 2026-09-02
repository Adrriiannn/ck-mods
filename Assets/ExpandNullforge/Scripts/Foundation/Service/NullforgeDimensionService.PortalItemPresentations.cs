using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Portals;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionPortalItemPresentationDefinition> GetPortalItemPresentations(
        bool includeDisabled)
    {
      List<DimensionPortalItemPresentationDefinition> result =
          new List<DimensionPortalItemPresentationDefinition>();
      for (int i = 0; i < DimensionPortalItemPresentationRegistry.Count; i++)
      {
        DimensionPortalItemPresentationDefinition presentation;
        if (!DimensionPortalItemPresentationRegistry.TryGet(i, out presentation))
        {
          continue;
        }

        if (!includeDisabled && !presentation.Enabled)
        {
          continue;
        }

        result.Add(presentation);
      }

      result.Sort(ComparePortalItemPresentations);
      return result;
    }

    public bool TryFindPortalItemPresentationForObject(
        string portalObjectName,
        out DimensionPortalItemPresentationDefinition presentation)
    {
      return DimensionPortalItemPresentationRegistry.TryFindForPortalObject(
          portalObjectName,
          out presentation);
    }

    public bool TryRegisterPortalItemPresentation(
        DimensionPortalItemPresentationDefinition presentation,
        out DimensionOperationResult result)
    {
      if (!ValidatePortalItemPresentation(presentation, out result))
      {
        return false;
      }

      if (!DimensionPortalItemPresentationRegistry.Register(presentation))
      {
        result = DimensionOperationResult.Failed(
            "portal-item-presentation-registration-failed",
            "The portal item presentation could not be registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool ValidatePortalItemPresentation(
        DimensionPortalItemPresentationDefinition presentation,
        out DimensionOperationResult result)
    {
      if (!presentation.IsValid)
      {
        result = DimensionOperationResult.Failed(
            "portal-item-presentation-object-empty",
            "A portal object name is required.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static int ComparePortalItemPresentations(
        DimensionPortalItemPresentationDefinition left,
        DimensionPortalItemPresentationDefinition right)
    {
      int priority = right.Priority.CompareTo(left.Priority);
      if (priority != 0)
      {
        return priority;
      }

      return string.Compare(
          left.PortalObjectName,
          right.PortalObjectName,
          System.StringComparison.Ordinal);
    }
  }
}
