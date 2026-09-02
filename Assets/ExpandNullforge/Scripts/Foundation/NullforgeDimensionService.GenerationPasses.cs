using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// The providers that can generate, and the passes they run.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    public bool TryRegisterGenerationProvider(
        IDimensionGenerationProvider provider,
        out DimensionOperationResult result)
    {
      if (provider == null)
      {
        result = DimensionOperationResult.Failed("generation-provider-null", "A generation provider is required.");
        return false;
      }

      if (string.IsNullOrEmpty(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("generation-provider-id-empty", "A generation provider id is required.");
        return false;
      }

      if (generationProviders.ContainsKey(provider.ProviderId))
      {
        result = DimensionOperationResult.Failed("generation-provider-duplicate", "A generation provider with that id is already registered.");
        return false;
      }

      generationProviders[provider.ProviderId] = provider;
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Generation provider registered: " + provider.ProviderId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationProvider(
        string providerId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(providerId))
      {
        result = DimensionOperationResult.Failed("generation-provider-id-empty", "A generation provider id is required.");
        return false;
      }

      IDimensionGenerationProvider provider;
      if (!generationProviders.TryGetValue(providerId, out provider))
      {
        result = DimensionOperationResult.Failed("generation-provider-not-found", "No generation provider with that id is registered.");
        return false;
      }

      CancelRuntimeGenerationRecordsForProvider(
          providerId,
          provider,
          "Generation provider was removed.");
      generationProviders.Remove(providerId);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, string.Empty, "Generation provider removed: " + providerId + ".");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<string> GetGenerationProviderIds()
    {
      List<string> result = new List<string>(generationProviders.Count);
      foreach (string providerId in generationProviders.Keys)
      {
        result.Add(providerId);
      }

      result.Sort(StringComparer.Ordinal);
      return result;
    }

    public IReadOnlyList<DimensionGenerationPassDefinition> GetGenerationPasses(
        string dimensionId,
        string zoneId,
        bool includeDisabled)
    {
      List<DimensionGenerationPassDefinition> result =
          new List<DimensionGenerationPassDefinition>();

      foreach (DimensionGenerationPassDefinition generationPass in generationPasses.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(generationPass.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(zoneId) &&
            !string.IsNullOrEmpty(generationPass.ZoneId) &&
            !string.Equals(generationPass.ZoneId, zoneId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && !generationPass.Enabled)
        {
          continue;
        }

        result.Add(generationPass);
      }

      result.Sort(CompareGenerationPasses);
      return result;
    }

    public bool TryGetGenerationPass(
        string passId,
        out DimensionGenerationPassDefinition generationPass)
    {
      if (string.IsNullOrEmpty(passId))
      {
        generationPass = default(DimensionGenerationPassDefinition);
        return false;
      }

      return generationPasses.TryGetValue(passId, out generationPass);
    }

    public bool TryRegisterGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationPass(generationPass, out result))
      {
        return false;
      }

      if (generationPasses.ContainsKey(generationPass.PassId))
      {
        result = DimensionOperationResult.Failed("generation-pass-already-registered", "A generation pass with that id is already registered.");
        return false;
      }

      generationPasses[generationPass.PassId] = generationPass;

      // A type that promises "the authored stamp is the whole place" is contradicted by a
      // procedural pass. Registered anyway — the author may know something — but said aloud.
      DimensionDefinition passDimension;
      if (TryGetDimension(generationPass.DimensionId, out passDimension) &&
          DimensionTypePolicy.For(passDimension.Type).WarnOnProceduralContent)
      {
        AddDiagnostic(
            DimensionDiagnosticSeverity.Warning,
            generationPass.DimensionId,
            "Generation pass '" + generationPass.PassId + "' targets a " +
            passDimension.Type + "-type dimension, which is meant to stay exactly as " +
            "authored. The pass will run; make sure that is what you want.");
      }

      RaiseGenerationPassChanged(
          generationPass,
          DimensionGenerationPassChangeKind.Registered,
          false,
          generationPass.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateGenerationPass(
        DimensionGenerationPassDefinition generationPass,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionGenerationPassDefinition previous;
      if (!generationPasses.TryGetValue(generationPass.PassId, out previous))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      if (!ValidateGenerationPass(generationPass, out result))
      {
        return false;
      }

      if (GenerationPassEquals(previous, generationPass))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      generationPasses[generationPass.PassId] = generationPass;
      RaiseGenerationPassChanged(
          generationPass,
          previous.Enabled == generationPass.Enabled
              ? DimensionGenerationPassChangeKind.Updated
              : DimensionGenerationPassChangeKind.EnabledChanged,
          previous.Enabled,
          generationPass.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetGenerationPassEnabled(
        string passId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(passId))
      {
        result = DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
        return false;
      }

      DimensionGenerationPassDefinition generationPass;
      if (!generationPasses.TryGetValue(passId, out generationPass))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      if (generationPass.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionGenerationPassDefinition updated =
          new DimensionGenerationPassDefinition(
              generationPass.PassId,
              generationPass.DisplayName,
              generationPass.DimensionId,
              generationPass.ZoneId,
              generationPass.HasLocalBounds,
              generationPass.LocalBounds,
              generationPass.Phase,
              generationPass.Priority,
              generationPass.ProviderId,
              enabled);

      generationPasses[passId] = updated;
      RaiseGenerationPassChanged(
          updated,
          DimensionGenerationPassChangeKind.EnabledChanged,
          generationPass.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationPass(
        string passId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(passId))
      {
        result = DimensionOperationResult.Failed("generation-pass-id-empty", "A generation pass id is required.");
        return false;
      }

      DimensionGenerationPassDefinition generationPass;
      if (!generationPasses.TryGetValue(passId, out generationPass))
      {
        result = DimensionOperationResult.Failed("generation-pass-not-found", "No generation pass with that id is registered.");
        return false;
      }

      generationPasses.Remove(passId);
      RaiseGenerationPassChanged(
          generationPass,
          DimensionGenerationPassChangeKind.Removed,
          generationPass.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
