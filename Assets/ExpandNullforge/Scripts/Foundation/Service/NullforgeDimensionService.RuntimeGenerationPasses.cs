using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Loading;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
  /// <summary>
  /// Starting a provider, ticking it, and the passes made for a template that names none.
  /// </summary>
  public sealed partial class NullforgeDimensionService
  {
    private void StartRuntimeGenerationProvider(RuntimeGenerationRecord record, double now)
    {
      IDimensionGenerationProvider provider;
      if (TryPreparePlannedGeneration(record, now, out provider))
      {
        return;
      }

      if (!TryGetGenerationProvider(record.Request.DimensionId, record.Request.LocalBounds, out provider))
      {
        FailRuntimeGeneration(record, "No registered generation provider can generate this area.");
        return;
      }

      record.ProviderId = provider.ProviderId;
      record.ProviderStartedAt = now;
      record.State = DimensionGenerationState.GeneratingTerrain;
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          DimensionGenerationState.GeneratingTerrain,
          0.25f,
          "Generation provider started: " + provider.ProviderId + ".");
    }

    private void TickRuntimeGenerationProvider(
        RuntimeGenerationRecord record,
        DimensionGenerationStatus current,
        double now)
    {
      if (record.UsePlannedPasses)
      {
        TickRuntimeGenerationPassProvider(record, current, now);
        return;
      }

      IDimensionGenerationProvider provider;
      if (string.IsNullOrEmpty(record.ProviderId) ||
          !generationProviders.TryGetValue(record.ProviderId, out provider))
      {
        if (!TryGetGenerationProvider(record.Request.DimensionId, record.Request.LocalBounds, out provider))
        {
          FailRuntimeGeneration(record, "Generation provider is no longer available.");
          return;
        }

        record.ProviderId = provider.ProviderId;
      }

      DimensionDefinition definition;
      DimensionArea area;
      if (!TryGetDimension(record.Request.DimensionId, out definition) ||
          !TryGetArea(record.Request.DimensionId, record.Request.LocalBounds, out area))
      {
        FailRuntimeGeneration(record, "Generation target dimension or area is no longer valid.");
        return;
      }

      DimensionGenerationProviderResult providerResult =
          provider.TickGeneration(
              new DimensionGenerationContext(
                  serverWorld,
                  definition,
                  area,
                  current,
                  Math.Max(0.0d, now - record.ProviderStartedAt)));

      DimensionGenerationState state =
          NormalizeGenerationState(providerResult.State);
      if (state == DimensionGenerationState.Unknown ||
          state == DimensionGenerationState.NotGenerated ||
          state == DimensionGenerationState.Queued ||
          state == DimensionGenerationState.LoadingArea)
      {
        state = DimensionGenerationState.GeneratingTerrain;
      }

      record.State = state;
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          state,
          providerResult.Progress01,
          providerResult.Message);

      if (state == DimensionGenerationState.Ready ||
          state == DimensionGenerationState.Failed)
      {
        if (state == DimensionGenerationState.Failed)
        {
          NotifyGenerationProviderCancelled(
              record,
              "Generation provider reported failure.",
              provider);
        }

        CompleteRuntimeGenerationRecord(record, "generation provider finished");
      }
    }

    private bool TryPreparePlannedGeneration(
        RuntimeGenerationRecord record,
        double now,
        out IDimensionGenerationProvider provider)
    {
      provider = null;
      if (record.PlannedPasses == null)
      {
        record.PlannedPasses = new List<DimensionGenerationPassDefinition>();
      }
      else
      {
        record.PlannedPasses.Clear();
      }

      record.PlannedPassIndex = 0;
      record.UsePlannedPasses = false;

      DimensionGenerationPlan plan =
          BuildGenerationPlan(
              new DimensionGenerationPlanRequest(
                  record.Request.DimensionId,
                  record.Request.LocalBounds,
                  string.Empty,
                  false,
                  false,
                  true));
      if (!plan.Success || plan.Passes.Count == 0)
      {
        return false;
      }

      DimensionDefinition definition;
      if (!TryGetDimension(record.Request.DimensionId, out definition))
      {
        return false;
      }

      for (int i = 0; i < plan.Passes.Count; i++)
      {
        DimensionGenerationPassDefinition generationPass = plan.Passes[i];
        IDimensionGenerationProvider candidate;
        if (!generationProviders.TryGetValue(generationPass.ProviderId, out candidate) ||
            candidate == null ||
            !(candidate is IDimensionGenerationPassProvider) ||
            !candidate.CanGenerate(definition, record.Request.LocalBounds))
        {
          continue;
        }

        record.PlannedPasses.Add(generationPass);
      }

      SynthesizeDefaultPasses(record, definition);

      if (record.PlannedPasses.Count == 0)
      {
        return false;
      }

      DimensionGenerationPassDefinition firstPass = record.PlannedPasses[0];
      if (!generationProviders.TryGetValue(firstPass.ProviderId, out provider) || provider == null)
      {
        return false;
      }

      record.ProviderId = provider.ProviderId;
      record.ProviderStartedAt = now;
      record.PlannedPassIndex = 0;
      record.UsePlannedPasses = true;
      record.State = GenerationStateForPassPhase(firstPass.Phase);
      record.UpdatedAt = now;
      SetGenerationStatus(
          record.Request.DimensionId,
          record.Request.LocalBounds,
          record.State,
          ComputePlannedGenerationProgress(record, 0f),
          "Generation pass started: " + GenerationPassName(firstPass) + ".");
      return true;
    }

    /// <summary>
    /// Completes an authored plan with the passes every dimension needs but nobody has to
    /// author: terrain first, scene placement after.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Passes only exist when a creator authors them, and most never will — before this,
    /// a dimension with no authored passes ran exactly one provider in single-provider mode
    /// and its scenes never placed, while a dimension with ONE authored scenes pass ran
    /// scenes over bare void. The synthetic entries live only in the record's planned list:
    /// validation, persistence and the Studio's pass UI never see them.
    /// </para>
    /// <para>
    /// The terrain choice mirrors the providers' own mutual exclusion: the tile-map
    /// provider claims dimensions with a painted map, the safe platform takes the rest.
    /// </para>
    /// </remarks>
    private void SynthesizeDefaultPasses(
        RuntimeGenerationRecord record,
        DimensionDefinition definition)
    {
      bool hasTerrain = false;
      bool hasScenes = false;
      for (int i = 0; i < record.PlannedPasses.Count; i++)
      {
        hasTerrain |= record.PlannedPasses[i].Phase == DimensionGenerationPassPhase.Terrain;
        hasScenes |= record.PlannedPasses[i].Phase == DimensionGenerationPassPhase.Scenes;
      }

      if (!hasTerrain)
      {
        string terrainProviderId = SelectDefaultTerrainProvider(definition, record);
        if (!string.IsNullOrEmpty(terrainProviderId))
        {
          record.PlannedPasses.Insert(0, new DimensionGenerationPassDefinition(
              definition.Id + ":auto-terrain",
              "Terrain",
              definition.Id,
              string.Empty,
              false,
              default,
              DimensionGenerationPassPhase.Terrain,
              int.MinValue,
              terrainProviderId,
              true));
        }
      }

      // Dungeons carve before scenes decorate: Structures phase sorts between them.
      bool hasStructures = false;
      for (int i = 0; i < record.PlannedPasses.Count; i++)
      {
        hasStructures |= record.PlannedPasses[i].Phase == DimensionGenerationPassPhase.Structures;
      }

      if (!hasStructures &&
          generationProviders.TryGetValue(
              DimensionGenerationProviderIds.DungeonPlacement,
              out IDimensionGenerationProvider dungeonProvider) &&
          dungeonProvider is IDimensionGenerationPassProvider &&
          dungeonProvider.CanGenerate(definition, record.Request.LocalBounds))
      {
        record.PlannedPasses.Add(new DimensionGenerationPassDefinition(
            definition.Id + ":auto-dungeons",
            "Dungeons",
            definition.Id,
            string.Empty,
            false,
            default,
            DimensionGenerationPassPhase.Structures,
            int.MaxValue,
            DimensionGenerationProviderIds.DungeonPlacement,
            true));
      }

      if (!hasScenes &&
          generationProviders.TryGetValue(
              DimensionGenerationProviderIds.ScenePlacement,
              out IDimensionGenerationProvider sceneProvider) &&
          sceneProvider is IDimensionGenerationPassProvider &&
          sceneProvider.CanGenerate(definition, record.Request.LocalBounds))
      {
        record.PlannedPasses.Add(new DimensionGenerationPassDefinition(
            definition.Id + ":auto-scenes",
            "Places",
            definition.Id,
            string.Empty,
            false,
            default,
            DimensionGenerationPassPhase.Scenes,
            int.MaxValue,
            DimensionGenerationProviderIds.ScenePlacement,
            true));
      }

      // Ore for the non-painted world: the painted path grows veins inside its own write
      // list, so the provider's CanGenerate refuses painted dimensions and this pass only
      // appears where it is the sole way veins can exist.
      bool hasOre = false;
      for (int i = 0; i < record.PlannedPasses.Count; i++)
      {
        hasOre |= record.PlannedPasses[i].Phase == DimensionGenerationPassPhase.Ore;
      }

      if (!hasOre &&
          generationProviders.TryGetValue(
              DimensionGenerationProviderIds.OreScatter,
              out IDimensionGenerationProvider oreProvider) &&
          oreProvider is IDimensionGenerationPassProvider &&
          oreProvider.CanGenerate(definition, record.Request.LocalBounds))
      {
        record.PlannedPasses.Add(new DimensionGenerationPassDefinition(
            definition.Id + ":auto-ore",
            "Ore",
            definition.Id,
            string.Empty,
            false,
            default,
            DimensionGenerationPassPhase.Ore,
            int.MaxValue,
            DimensionGenerationProviderIds.OreScatter,
            true));
      }

      // Synthetic entries may land around authored ones of other phases; the ladder's
      // promise is phase order, so restate it.
      record.PlannedPasses.Sort(
          (left, right) =>
          {
            int phase = ((int)left.Phase).CompareTo((int)right.Phase);
            return phase != 0 ? phase : left.Priority.CompareTo(right.Priority);
          });
    }

    /// <summary>The terrain provider this dimension would use in single-provider mode.</summary>
    private string SelectDefaultTerrainProvider(
        DimensionDefinition definition,
        RuntimeGenerationRecord record)
    {
      if (generationProviders.TryGetValue(
              DimensionGenerationProviderIds.TileMap,
              out IDimensionGenerationProvider tileMap) &&
          tileMap is IDimensionGenerationPassProvider &&
          tileMap.CanGenerate(definition, record.Request.LocalBounds))
      {
        return DimensionGenerationProviderIds.TileMap;
      }

      if (generationProviders.TryGetValue(
              DimensionGenerationProviderIds.SafePlatform,
              out IDimensionGenerationProvider safePlatform) &&
          safePlatform is IDimensionGenerationPassProvider &&
          safePlatform.CanGenerate(definition, record.Request.LocalBounds))
      {
        return DimensionGenerationProviderIds.SafePlatform;
      }

      return string.Empty;
    }

    private void TickRuntimeGenerationPassProvider(
        RuntimeGenerationRecord record,
        DimensionGenerationStatus current,
        double now)
    {
      if (record.PlannedPasses == null ||
          record.PlannedPassIndex < 0 ||
          record.PlannedPassIndex >= record.PlannedPasses.Count)
      {
        FailRuntimeGeneration(record, "The generation plan is no longer valid.");
        return;
      }

      DimensionDefinition definition;
      DimensionArea area;
      if (!TryGetDimension(record.Request.DimensionId, out definition) ||
          !TryGetArea(record.Request.DimensionId, record.Request.LocalBounds, out area))
      {
        FailRuntimeGeneration(record, "Generation target dimension or area is no longer valid.");
        return;
      }

      int completedPassesThisTick = 0;
      while (completedPassesThisTick < MaxPlannedGenerationPassesPerTick)
      {
        if (record.PlannedPassIndex < 0 ||
            record.PlannedPassIndex >= record.PlannedPasses.Count)
        {
          FailRuntimeGeneration(record, "The generation plan is no longer valid.");
          return;
        }

        DimensionGenerationPassDefinition generationPass =
            record.PlannedPasses[record.PlannedPassIndex];

        IDimensionGenerationProvider provider;
        if (string.IsNullOrEmpty(record.ProviderId) ||
            !string.Equals(record.ProviderId, generationPass.ProviderId, StringComparison.Ordinal) ||
            !generationProviders.TryGetValue(generationPass.ProviderId, out provider) ||
            provider == null)
        {
          FailRuntimeGeneration(record, "Generation pass provider is no longer available.");
          return;
        }

        IDimensionGenerationPassProvider passProvider =
            provider as IDimensionGenerationPassProvider;
        if (passProvider == null)
        {
          FailRuntimeGeneration(record, "Generation pass provider does not support pass execution.");
          return;
        }

        DimensionGenerationProviderResult providerResult =
            passProvider.TickGenerationPass(
                new DimensionGenerationPassContext(
                    new DimensionGenerationContext(
                        serverWorld,
                        definition,
                        area,
                        current,
                        Math.Max(0.0d, now - record.ProviderStartedAt)),
                    generationPass,
                    record.PlannedPassIndex,
                    record.PlannedPasses.Count));

        DimensionGenerationState state =
            NormalizeGenerationState(providerResult.State);

        if (state == DimensionGenerationState.Failed)
        {
          record.State = DimensionGenerationState.Failed;
          record.UpdatedAt = now;
          SetGenerationStatus(
              record.Request.DimensionId,
              record.Request.LocalBounds,
              DimensionGenerationState.Failed,
              ComputePlannedGenerationProgress(record, providerResult.Progress01),
              providerResult.Message);
          NotifyGenerationProviderCancelled(
              record,
              "Generation pass provider reported failure.",
              provider);
          CompleteRuntimeGenerationRecord(record, "generation pass failed");
          return;
        }

        if (state == DimensionGenerationState.Ready)
        {
          completedPassesThisTick++;
          if (record.PlannedPassIndex + 1 >= record.PlannedPasses.Count)
          {
            record.State = DimensionGenerationState.Ready;
            record.UpdatedAt = now;
            SetGenerationStatus(
                record.Request.DimensionId,
                record.Request.LocalBounds,
                DimensionGenerationState.Ready,
                1.0f,
                string.IsNullOrEmpty(providerResult.Message)
                    ? "Generation plan completed."
                    : providerResult.Message);
            CompleteRuntimeGenerationRecord(record, "generation plan completed");
            return;
          }

          record.PlannedPassIndex++;
          DimensionGenerationPassDefinition nextPass =
              record.PlannedPasses[record.PlannedPassIndex];
          IDimensionGenerationProvider nextProvider;
          if (!generationProviders.TryGetValue(nextPass.ProviderId, out nextProvider) || nextProvider == null)
          {
            FailRuntimeGeneration(record, "Next generation pass provider is no longer available.");
            return;
          }

          record.ProviderId = nextProvider.ProviderId;
          record.ProviderStartedAt = now;
          record.State = GenerationStateForPassPhase(nextPass.Phase);
          record.UpdatedAt = now;
          current =
              new DimensionGenerationStatus(
                  record.Request.DimensionId,
                  record.Request.LocalBounds,
                  record.State,
                  ComputePlannedGenerationProgress(record, 0f),
                  "Generation pass started: " + GenerationPassName(nextPass) + ".");
          SetGenerationStatus(
              current.DimensionId,
              current.LocalBounds,
              current.State,
              current.Progress01,
              current.Message);
          continue;
        }

        DimensionGenerationState passState = GenerationStateForPassPhase(generationPass.Phase);
        if (state == DimensionGenerationState.Unknown ||
            state == DimensionGenerationState.NotGenerated ||
            state == DimensionGenerationState.Queued ||
            state == DimensionGenerationState.LoadingArea)
        {
          state = passState;
        }

        record.State = state;
        record.UpdatedAt = now;
        SetGenerationStatus(
            record.Request.DimensionId,
            record.Request.LocalBounds,
            state,
            ComputePlannedGenerationProgress(record, providerResult.Progress01),
            string.IsNullOrEmpty(providerResult.Message)
                ? "Running generation pass: " + GenerationPassName(generationPass) + "."
                : providerResult.Message);
        return;
      }
    }

    private bool TryGetGenerationProvider(
        string dimensionId,
        DimensionBounds localBounds,
        out IDimensionGenerationProvider provider)
    {
      provider = null;
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        return false;
      }

      DimensionGenerationPlan plan =
          BuildGenerationPlan(
              new DimensionGenerationPlanRequest(
                  dimensionId,
                  localBounds,
                  string.Empty,
                  false,
                  false,
                  true));
      if (plan.Success)
      {
        for (int i = 0; i < plan.Passes.Count; i++)
        {
          DimensionGenerationPassDefinition generationPass = plan.Passes[i];
          IDimensionGenerationProvider plannedProvider;
          if (generationProviders.TryGetValue(generationPass.ProviderId, out plannedProvider) &&
              plannedProvider != null &&
              plannedProvider.CanGenerate(definition, localBounds))
          {
            provider = plannedProvider;
            return true;
          }
        }
      }

      IReadOnlyList<string> providerIds = GetGenerationProviderIds();
      for (int i = 0; i < providerIds.Count; i++)
      {
        IDimensionGenerationProvider candidate;
        generationProviders.TryGetValue(providerIds[i], out candidate);
        if (candidate != null && candidate.CanGenerate(definition, localBounds))
        {
          provider = candidate;
          return true;
        }
      }

      return false;
    }
  }
}
