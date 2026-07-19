using System;
using ExpandNullforge.Api;
using Unity.Entities;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool TryEvaluateAccessProviders(
        DimensionAccessContext context,
        out DimensionAccessResult result)
    {
      EnsureAccessProviderOrder();
      for (int i = 0; i < orderedAccessProviders.Count; i++)
      {
        IDimensionAccessProvider provider = orderedAccessProviders[i];
        if (provider == null)
        {
          continue;
        }

        DimensionAccessResult providerResult;
        try
        {
          if (!provider.TryEvaluateTravelAccess(context, out providerResult))
          {
            continue;
          }
        }
        catch (Exception exception)
        {
          result =
              DimensionAccessResult.Deny(
                  "access-provider-error",
                  "Access provider '" + provider.ProviderId + "' failed while evaluating travel: " + exception.Message);
          return false;
        }

        if (!providerResult.Allowed)
        {
          if (string.IsNullOrEmpty(providerResult.Code))
          {
            result =
                DimensionAccessResult.Deny(
                    "access-provider-denied",
                    "Access provider '" + provider.ProviderId + "' denied dimension travel.");
          }
          else
          {
            result = providerResult;
          }

          return false;
        }
      }

      result = DimensionAccessResult.Allow();
      return true;
    }

    private void EnsureAccessProviderOrder()
    {
      if (!accessProviderOrderDirty)
      {
        return;
      }

      orderedAccessProviders.Clear();
      foreach (IDimensionAccessProvider provider in accessProviders.Values)
      {
        if (provider != null)
        {
          orderedAccessProviders.Add(provider);
        }
      }

      orderedAccessProviders.Sort(CompareAccessProviders);
      accessProviderOrderDirty = false;
    }

    private static int CompareAccessProviders(
        IDimensionAccessProvider left,
        IDimensionAccessProvider right)
    {
      if (left == null && right == null)
      {
        return 0;
      }

      if (left == null)
      {
        return 1;
      }

      if (right == null)
      {
        return -1;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      return string.Compare(left.ProviderId, right.ProviderId, StringComparison.Ordinal);
    }

    private void EnsurePermissionProviderOrder()
    {
      if (!permissionProviderOrderDirty)
      {
        return;
      }

      orderedPermissionProviders.Clear();
      foreach (IDimensionPermissionProvider provider in permissionProviders.Values)
      {
        if (provider != null)
        {
          orderedPermissionProviders.Add(provider);
        }
      }

      orderedPermissionProviders.Sort(ComparePermissionProviders);
      permissionProviderOrderDirty = false;
    }

    private static int ComparePermissionProviders(
        IDimensionPermissionProvider left,
        IDimensionPermissionProvider right)
    {
      if (left == null && right == null)
      {
        return 0;
      }

      if (left == null)
      {
        return 1;
      }

      if (right == null)
      {
        return -1;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      return string.Compare(left.ProviderId, right.ProviderId, StringComparison.Ordinal);
    }

    private static DimensionPermissionResult EvaluateBuiltInPermissionDefault(
        DimensionPermissionContext context)
    {
      switch (context.Kind)
      {
        case DimensionPermissionKind.ReadContext:
        case DimensionPermissionKind.Travel:
          return DimensionPermissionResult.Allow(
              "expandnullforge:default-permissions",
              "Dimension read/travel permission is allowed by the framework default.");

        case DimensionPermissionKind.RegisterContent:
        case DimensionPermissionKind.LoadArea:
        case DimensionPermissionKind.GenerateArea:
          if (context.IsServerSide && context.Player == Entity.Null)
          {
            return DimensionPermissionResult.Allow(
                "expandnullforge:default-permissions",
                "Server-side framework operation is allowed by the framework default.");
          }

          return DimensionPermissionResult.Deny(
              "expandnullforge:default-permissions",
              "permission-provider-required",
              "Player-initiated dimension generation/loading/content changes require a permission provider.");

        case DimensionPermissionKind.MutateRegistry:
        case DimensionPermissionKind.DebugTool:
        case DimensionPermissionKind.AdminTool:
          return DimensionPermissionResult.Deny(
              "expandnullforge:default-permissions",
              "permission-provider-required",
              "Admin/debug/registry mutation actions require a permission provider.");

        default:
          return DimensionPermissionResult.Deny(
              "expandnullforge:default-permissions",
              "permission-kind-unknown",
              "Unknown permission kind.");
      }
    }

    private DimensionTravelRequirementEvaluationResult EvaluateTravelRequirementProviders(
        DimensionTravelRequirementEvaluationContext context)
    {
      EnsureTravelRequirementEvaluatorOrder();
      for (int i = 0; i < orderedTravelRequirementEvaluators.Count; i++)
      {
        IDimensionTravelRequirementEvaluator evaluator = orderedTravelRequirementEvaluators[i];
        if (evaluator == null)
        {
          continue;
        }

        DimensionTravelRequirementEvaluationResult providerResult;
        try
        {
          if (!evaluator.TryEvaluateTravelRequirement(context, out providerResult))
          {
            continue;
          }
        }
        catch (Exception exception)
        {
          return DimensionTravelRequirementEvaluationResult.Unmet(
              context.Requirement,
              evaluator.ProviderId,
              "travel-requirement-evaluator-error",
              "Travel requirement evaluator '" + evaluator.ProviderId + "' failed: " + exception.Message);
        }

        return providerResult;
      }

      return DimensionTravelRequirementEvaluationResult.Unevaluated(
          context.Requirement,
          "travel-requirement-no-evaluator",
          "No travel requirement evaluator handled this requirement.");
    }

    private void EnsureTravelRequirementEvaluatorOrder()
    {
      if (!travelRequirementEvaluatorOrderDirty)
      {
        return;
      }

      orderedTravelRequirementEvaluators.Clear();
      foreach (IDimensionTravelRequirementEvaluator evaluator in travelRequirementEvaluators.Values)
      {
        if (evaluator != null)
        {
          orderedTravelRequirementEvaluators.Add(evaluator);
        }
      }

      orderedTravelRequirementEvaluators.Sort(CompareTravelRequirementEvaluators);
      travelRequirementEvaluatorOrderDirty = false;
    }

    private static int CompareTravelRequirementEvaluators(
        IDimensionTravelRequirementEvaluator left,
        IDimensionTravelRequirementEvaluator right)
    {
      if (left == null && right == null)
      {
        return 0;
      }

      if (left == null)
      {
        return 1;
      }

      if (right == null)
      {
        return -1;
      }

      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      return string.Compare(left.ProviderId, right.ProviderId, StringComparison.Ordinal);
    }
  }
}
