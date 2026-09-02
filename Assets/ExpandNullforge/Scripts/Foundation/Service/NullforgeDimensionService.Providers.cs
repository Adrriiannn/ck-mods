using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public bool TryGetZoneAtLocal(string dimensionId, float2 localPosition, out DimensionZoneInfo zone)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition) || !definition.ContainsLocal(localPosition))
      {
        zone = default(DimensionZoneInfo);
        return false;
      }

      DimensionZoneDefinition definitionZone;
      if (TryFindZoneDefinitionAtLocal(dimensionId, localPosition, out definitionZone))
      {
        zone =
            new DimensionZoneInfo(
                definitionZone.DimensionId,
                definitionZone.ZoneId,
                definitionZone.DisplayName,
                definitionZone.Kind,
                definitionZone.LocalBounds);
        return true;
      }

      foreach (IDimensionZoneProvider provider in zoneProviders.Values)
      {
        if (provider != null &&
            provider.CanResolveZone(definition) &&
            provider.TryGetZoneAtLocal(definition, localPosition, out zone))
        {
          return true;
        }
      }

      zone = default(DimensionZoneInfo);
      return false;
    }

    public bool TryRegisterZoneProvider(
        IDimensionZoneProvider provider,
        out DimensionOperationResult result)
    {
      if (provider == null)
      {
        result = DimensionOperationResult.Failed("zone-provider-null", "A zone provider is required.");
        return false;
      }

      if (string.IsNullOrEmpty(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("zone-provider-id-empty", "A zone provider id is required.");
        return false;
      }

      if (zoneProviders.ContainsKey(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("zone-provider-duplicate", "A zone provider with that id is already registered.");
        return false;
      }

      zoneProviders[provider.ProviderId] = provider;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Zone provider registered: " + provider.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveZoneProvider(string providerId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("zone-provider-id-empty", "A zone provider id is required.");
        return false;
      }

      if (!zoneProviders.Remove(providerId))
      {
        result = DimensionOperationResult.Failed("zone-provider-not-found", "No zone provider with that id is registered.");
        return false;
      }

      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Zone provider removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRegisterAccessProvider(
        IDimensionAccessProvider provider,
        out DimensionOperationResult result)
    {
      if (provider == null)
      {
        result = DimensionOperationResult.Failed("access-provider-null", "An access provider is required.");
        return false;
      }

      if (string.IsNullOrEmpty(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("access-provider-id-empty", "An access provider id is required.");
        return false;
      }

      if (accessProviders.ContainsKey(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("access-provider-duplicate", "An access provider with that id is already registered.");
        return false;
      }

      accessProviders[provider.ProviderId] = provider;
      accessProviderOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Access provider registered: " + provider.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveAccessProvider(string providerId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("access-provider-id-empty", "An access provider id is required.");
        return false;
      }

      if (!accessProviders.Remove(providerId))
      {
        result = DimensionOperationResult.Failed("access-provider-not-found", "No access provider with that id is registered.");
        return false;
      }

      accessProviderOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Access provider removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<string> GetAccessProviderIds()
    {
      List<string> result = new List<string>(accessProviders.Count);
      foreach (string providerId in accessProviders.Keys)
      {
        result.Add(providerId);
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public bool TryRegisterPermissionProvider(
        IDimensionPermissionProvider provider,
        out DimensionOperationResult result)
    {
      if (provider == null)
      {
        result = DimensionOperationResult.Failed("permission-provider-null", "A permission provider is required.");
        return false;
      }

      if (string.IsNullOrEmpty(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("permission-provider-id-empty", "A permission provider id is required.");
        return false;
      }

      if (permissionProviders.ContainsKey(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("permission-provider-duplicate", "A permission provider with that id is already registered.");
        return false;
      }

      permissionProviders[provider.ProviderId] = provider;
      permissionProviderOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Permission provider registered: " + provider.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemovePermissionProvider(
        string providerId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("permission-provider-id-empty", "A permission provider id is required.");
        return false;
      }

      if (!permissionProviders.Remove(providerId))
      {
        result = DimensionOperationResult.Failed("permission-provider-not-found", "No permission provider with that id is registered.");
        return false;
      }

      permissionProviderOrderDirty = true;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Permission provider removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<string> GetPermissionProviderIds()
    {
      List<string> result = new List<string>(permissionProviders.Count);
      foreach (string providerId in permissionProviders.Keys)
      {
        result.Add(providerId);
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public DimensionPermissionResult EvaluatePermission(
        DimensionPermissionContext context)
    {
      EnsurePermissionProviderOrder();
      for (int i = 0; i < orderedPermissionProviders.Count; i++)
      {
        IDimensionPermissionProvider provider = orderedPermissionProviders[i];
        if (provider == null)
        {
          continue;
        }

        DimensionPermissionResult providerResult;
        try
        {
          if (!provider.TryEvaluatePermission(context, out providerResult))
          {
            continue;
          }
        }
        catch (Exception exception)
        {
          return DimensionPermissionResult.Deny(
              provider.ProviderId,
              "permission-provider-error",
              "Permission provider '" + provider.ProviderId + "' failed: " + exception.Message);
        }

        return providerResult;
      }

      return EvaluateBuiltInPermissionDefault(context);
    }
  }
}
