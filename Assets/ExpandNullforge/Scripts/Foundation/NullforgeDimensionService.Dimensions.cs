using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool TryCreateContextFromPersistedPlayerState(
        DimensionWorldRegistry.DimensionPlayerStateRecord record,
        out DimensionContext context)
    {
      if (record == null || string.IsNullOrEmpty(record.dimensionId))
      {
        context = DimensionContext.Unknown(default(float2));
        return false;
      }

      DimensionDefinition definition;
      if (!TryGetDimension(record.dimensionId, out definition))
      {
        context = DimensionContext.Unknown(new float2(record.absoluteX, record.absoluteY));
        return false;
      }

      float2 localPosition = new float2(record.localX, record.localY);
      float2 absolutePosition = new float2(record.absoluteX, record.absoluteY);
      if (definition.Id == DimensionIds.Overworld)
      {
        context = DimensionContext.Overworld(absolutePosition);
        return true;
      }

      if (!definition.ContainsLocal(localPosition))
      {
        context = DimensionContext.Unknown(absolutePosition);
        return false;
      }

      context = new DimensionContext(
          true,
          definition.Id,
          absolutePosition,
          localPosition);
      return true;
    }

    private bool SetDimensionLifecycleInternal(
        DimensionDefinition definition,
        DimensionLifecycleState previous,
        DimensionLifecycleState lifecycleState,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionDefinition updated = definition.WithLifecycleState(lifecycleState);
      if (previous == lifecycleState)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      definitions[definition.Id] = updated;
      RebuildDefinitionSnapshot();
      PersistDimensionIfWorldRegistryLoaded(updated, IsProtectedDimensionId(definition.Id));
      RaiseDimensionLifecycleChanged(definition.Id, previous, lifecycleState, reason);
      AddDiagnostic(DimensionDiagnosticSeverity.Info, definition.Id, "Lifecycle changed to " + lifecycleState + ".");

      result = DimensionOperationResult.Ok();
      return true;
    }

    private void RebuildDefinitionSnapshot()
    {
      definitionSnapshot.Clear();
      foreach (DimensionDefinition definition in definitions.Values)
      {
        definitionSnapshot.Add(definition);
      }
    }

    private bool IsProtectedDimensionId(string dimensionId)
    {
      return string.Equals(dimensionId, DimensionIds.Overworld, StringComparison.Ordinal);
    }

    public IReadOnlyList<DimensionDefinition> GetDimensions()
    {
      return new List<DimensionDefinition>(definitionSnapshot);
    }

    public bool TryGetDimension(string dimensionId, out DimensionDefinition definition)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        definition = default(DimensionDefinition);
        return false;
      }

      return definitions.TryGetValue(dimensionId, out definition);
    }

    public bool TryRegisterDimension(DimensionDefinition definition, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(definition.Id))
      {
        result = DimensionOperationResult.Failed("dimension-id-empty", "A dimension id is required.");
        return false;
      }

      if (definitions.ContainsKey(definition.Id))
      {
        result = DimensionOperationResult.Failed("dimension-already-registered", "A dimension with that id is already registered.");
        return false;
      }

      if (definition.Id != DimensionIds.Overworld && Overlaps(definition.AbsoluteBounds, ProtectedOverworldCoordinateBounds))
      {
        result = DimensionOperationResult.Failed(
            "dimension-overworld-coordinate-conflict",
            "The requested dimension playable bounds overlap the protected vanilla coordinate area.");
        return false;
      }

      if (definition.Id != DimensionIds.Overworld && OverlapsExistingNonOverworldDimension(definition))
      {
        result = DimensionOperationResult.Failed(
            "dimension-absolute-bounds-overlap",
            "The requested dimension absolute bounds overlap another non-overworld dimension.");
        return false;
      }

      AddDefinitionInternal(definition);
      RebuildDefinitionSnapshot();
      PersistDimensionIfWorldRegistryLoaded(definition, false);
      RaiseDimensionLifecycleChanged(definition.Id, DimensionLifecycleState.Unknown, definition.LifecycleState, "registered");
      AddDiagnostic(DimensionDiagnosticSeverity.Info, definition.Id, "Dimension registered.");

      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUnregisterDimension(string dimensionId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(dimensionId))
      {
        result = DimensionOperationResult.Failed("dimension-id-empty", "A dimension id is required.");
        return false;
      }

      if (IsProtectedDimensionId(dimensionId))
      {
        result = DimensionOperationResult.Failed("dimension-protected", "Built-in dimensions cannot be unregistered.");
        return false;
      }

      DimensionDefinition removed;
      if (!definitions.TryGetValue(dimensionId, out removed))
      {
        result = DimensionOperationResult.Failed("dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      definitions.Remove(dimensionId);
      RebuildDefinitionSnapshot();
      RemovePersistedDimensionIfWorldRegistryLoaded(dimensionId);
      DimensionWorldRegistry.RemovePlayerVisitsForDimension(dimensionId);
      RemovePortalsForDimension(dimensionId);
      RemoveMapLayersForDimension(dimensionId);
      RemoveMarkersForDimension(dimensionId);
      RemoveAnchorsForDimension(dimensionId);
      RemoveBiomesForDimension(dimensionId);
      RemoveGenerationTablesForDimension(dimensionId);
      RemoveGenerationReservationsForDimension(dimensionId);
      RemoveZoneDefinitionsForDimension(dimensionId);
      RemoveEnvironmentProfilesForDimension(dimensionId);
      RemoveGenerationPassesForDimension(dimensionId);
      RemoveScenesForDimension(dimensionId);
      RemoveSceneTemplatesForDimension(dimensionId);
      RemoveEncountersForDimension(dimensionId);
      RemoveResourceNodesForDimension(dimensionId);
      RemoveWorldEventsForDimension(dimensionId);
      RemoveTravelRequirementsForDimension(dimensionId);
      RemoveProgressFlagsForDimension(dimensionId);
      RaiseDimensionLifecycleChanged(dimensionId, removed.LifecycleState, DimensionLifecycleState.Disabled, "unregistered");
      AddDiagnostic(DimensionDiagnosticSeverity.Info, dimensionId, "Dimension unregistered.");

      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetDimensionLifecycle(
        string dimensionId,
        DimensionLifecycleState lifecycleState,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        result = DimensionOperationResult.Failed("dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      DimensionLifecycleState previous = definition.LifecycleState;
      return SetDimensionLifecycleInternal(
          definition,
          previous,
          lifecycleState,
          reason,
          out result);
    }

    public bool TryGetDimensionAtAbsolute(float2 absolutePosition, out DimensionDefinition definition)
    {
      for (int i = 0; i < definitionSnapshot.Count; i++)
      {
        DimensionDefinition candidate = definitionSnapshot[i];
        if (candidate.Id == DimensionIds.Overworld)
        {
          continue;
        }

        if (candidate.ContainsAbsolute(absolutePosition))
        {
          definition = candidate;
          return true;
        }
      }

      definition = OverworldDefinition;
      return true;
    }

    public DimensionContext GetContextForAbsolute(float2 absolutePosition)
    {
      DimensionDefinition definition;
      if (!TryGetDimensionAtAbsolute(absolutePosition, out definition))
      {
        return DimensionContext.Unknown(absolutePosition);
      }

      if (definition.Id == DimensionIds.Overworld)
      {
        return DimensionContext.Overworld(absolutePosition);
      }

      return new DimensionContext(
          true,
          definition.Id,
          absolutePosition,
          definition.ToLocal(absolutePosition));
    }

    public bool TryGetPlayerContext(Entity player, out DimensionContext context)
    {
      return TryGetEntityContext(player, DimensionEntityWorldScope.BestAvailable, out context);
    }

    public bool TryGetEntityContext(Entity entity, out DimensionContext context)
    {
      return TryGetEntityContext(entity, DimensionEntityWorldScope.BestAvailable, out context);
    }

    public bool TryGetEntityContext(
        Entity entity,
        DimensionEntityWorldScope worldScope,
        out DimensionContext context)
    {
      float2 absolutePosition;
      if (!TryGetEntityAbsolutePosition(entity, worldScope, out absolutePosition))
      {
        context = DimensionContext.Unknown(default(float2));
        return false;
      }

      context = GetContextForAbsolute(absolutePosition);
      return context.IsKnown;
    }

    public DimensionResolveResult ResolveEntityLocalTarget(Entity entity, float2 localPosition)
    {
      return ResolveEntityLocalTarget(entity, DimensionEntityWorldScope.BestAvailable, localPosition);
    }

    public DimensionResolveResult ResolveEntityLocalTarget(
        Entity entity,
        DimensionEntityWorldScope worldScope,
        float2 localPosition)
    {
      DimensionContext context;
      if (!TryGetEntityContext(entity, worldScope, out context))
      {
        return DimensionResolveResult.Failed("The entity's dimension context is not available.");
      }

      return ResolveLocal(context.DimensionId, localPosition);
    }

    public DimensionRespawnTarget ResolveRespawnTarget(DimensionRespawnRequest request)
    {
      List<string> candidateDimensionIds = new List<string>(3);

      AddRespawnCandidateDimension(candidateDimensionIds, request.PreferredDimensionId);

      if (request.PreferPlayerDimension)
      {
        DimensionContext playerContext;
        if (TryGetEntityContext(request.Player, DimensionEntityWorldScope.BestAvailable, out playerContext))
        {
          AddRespawnCandidateDimension(candidateDimensionIds, playerContext.DimensionId);
        }
      }

      if (request.AllowOverworldFallback)
      {
        AddRespawnCandidateDimension(candidateDimensionIds, DimensionIds.Overworld);
      }

      if (candidateDimensionIds.Count == 0)
      {
        return DimensionRespawnTarget.Failed(
            "respawn-no-candidate-dimension",
            "No candidate dimension is available for respawn target resolution.");
      }

      for (int i = 0; i < candidateDimensionIds.Count; i++)
      {
        DimensionRespawnTarget target;
        if (TryResolveRespawnTargetInDimension(candidateDimensionIds[i], request, out target))
        {
          return target;
        }
      }

      return DimensionRespawnTarget.Failed(
          "respawn-anchor-not-found",
          "No enabled respawn, checkpoint, entry, or fallback anchor could be resolved.");
    }

    public bool TryGetPersistedPlayerContext(Entity player, out DimensionContext context)
    {
      string playerId;
      if (!TryGetPlayerPersistentId(player, out playerId))
      {
        context = DimensionContext.Unknown(default(float2));
        return false;
      }

      return TryGetPersistedPlayerContext(playerId, out context);
    }

    public bool TryGetPersistedPlayerContext(string playerId, out DimensionContext context)
    {
      DimensionWorldRegistry.DimensionPlayerStateRecord record;
      if (!DimensionWorldRegistry.TryGetPlayerState(playerId, out record))
      {
        context = DimensionContext.Unknown(default(float2));
        return false;
      }

      return TryCreateContextFromPersistedPlayerState(record, out context);
    }

    public bool TryToLocal(string dimensionId, float2 absolutePosition, out float2 localPosition)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        localPosition = default(float2);
        return false;
      }

      localPosition = definition.ToLocal(absolutePosition);
      return definition.Id == DimensionIds.Overworld || definition.ContainsLocal(localPosition);
    }

    public bool TryToAbsolute(string dimensionId, float2 localPosition, out float2 absolutePosition)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        absolutePosition = default(float2);
        return false;
      }

      if (definition.Id != DimensionIds.Overworld && !definition.ContainsLocal(localPosition))
      {
        absolutePosition = default(float2);
        return false;
      }

      absolutePosition = definition.ToAbsolute(localPosition);
      return true;
    }

    public bool TryGetArea(string dimensionId, DimensionBounds localBounds, out DimensionArea area)
    {
      DimensionDefinition definition;
      if (!TryGetDimension(dimensionId, out definition))
      {
        area = default(DimensionArea);
        return false;
      }

      if (definition.Id != DimensionIds.Overworld
          && (!definition.ContainsLocal(localBounds.Min) || !definition.ContainsLocal(localBounds.MaxExclusive - new int2(1, 1))))
      {
        area = default(DimensionArea);
        return false;
      }

      DimensionBounds absoluteBounds =
          new DimensionBounds(definition.AbsoluteOrigin + localBounds.Min, definition.AbsoluteOrigin + localBounds.MaxExclusive);

      area = new DimensionArea(dimensionId, localBounds, absoluteBounds);
      return true;
    }

    public DimensionResolveResult ResolveAbsolute(float2 absolutePosition)
    {
      DimensionContext context = GetContextForAbsolute(absolutePosition);
      if (!context.IsKnown)
      {
        return DimensionResolveResult.Failed("No dimension contains the absolute position.");
      }

      return DimensionResolveResult.Resolved(
          context.DimensionId,
          context.AbsolutePosition,
          context.LocalPosition);
    }

    public DimensionResolveResult ResolveLocal(string dimensionId, float2 localPosition)
    {
      float2 absolutePosition;
      if (!TryToAbsolute(dimensionId, localPosition, out absolutePosition))
      {
        return DimensionResolveResult.Failed("The local position could not be resolved.");
      }

      return DimensionResolveResult.Resolved(dimensionId, absolutePosition, localPosition);
    }

    public DimensionCoordinateCompatibilityResult ResolveCoordinateForDimensionAwareOperation(
        DimensionCoordinateCompatibilityRequest request)
    {
      DimensionContext playerContext = default(DimensionContext);
      bool hasPlayerContext =
          request.Player != Entity.Null &&
          TryGetPlayerContext(request.Player, out playerContext) &&
          playerContext.IsKnown;

      string dimensionId = request.DimensionId;
      if (request.PreferPlayerCurrentDimension && hasPlayerContext)
      {
        dimensionId = playerContext.DimensionId;
      }

      if (request.PositionIsLocal)
      {
        if (string.IsNullOrEmpty(dimensionId))
        {
          return DimensionCoordinateCompatibilityResult.Failed(
              "dimension-id-required",
              "A dimension id is required when resolving a local coordinate.",
              playerContext);
        }

        float2 absolutePosition;
        if (!TryToAbsolute(dimensionId, request.Position, out absolutePosition))
        {
          return DimensionCoordinateCompatibilityResult.Failed(
              "local-coordinate-unresolved",
              "The local coordinate could not be converted into an absolute coordinate.",
              playerContext);
        }

        return new DimensionCoordinateCompatibilityResult(
            true,
            dimensionId,
            request.Position,
            absolutePosition,
            playerContext,
            "coordinate-resolved",
            "Local dimension coordinate resolved.");
      }

      if (!string.IsNullOrEmpty(dimensionId))
      {
        float2 localPosition;
        if (!TryToLocal(dimensionId, request.Position, out localPosition))
        {
          return DimensionCoordinateCompatibilityResult.Failed(
              "absolute-coordinate-outside-dimension",
              "The absolute coordinate does not map into the requested dimension.",
              playerContext);
        }

        return new DimensionCoordinateCompatibilityResult(
            true,
            dimensionId,
            localPosition,
            request.Position,
            playerContext,
            "coordinate-resolved",
            "Absolute coordinate resolved in the requested dimension.");
      }

      DimensionContext absoluteContext = GetCoordinateContextForAbsolute(request.Position);
      if (!absoluteContext.IsKnown)
      {
        return DimensionCoordinateCompatibilityResult.Failed(
            "absolute-coordinate-unresolved",
            "No registered dimension contains the absolute coordinate.",
            playerContext);
      }

      return new DimensionCoordinateCompatibilityResult(
          true,
          absoluteContext.DimensionId,
          absoluteContext.LocalPosition,
          absoluteContext.AbsolutePosition,
          playerContext,
          "coordinate-resolved",
          "Absolute coordinate resolved by registered coordinate domains.");
    }

    public DimensionResolveResult ResolvePlayerLocalTarget(Entity player, float2 localPosition)
    {
      DimensionContext context;
      if (!TryGetPlayerContext(player, out context))
      {
        return DimensionResolveResult.Failed("The player's dimension context is not available.");
      }

      return ResolveLocal(context.DimensionId, localPosition);
    }

    public DimensionLandingValidationResult ValidateLandingTarget(
        DimensionLandingValidationRequest request)
    {
      return ValidateLandingTargetInternal(request, false, string.Empty);
    }
  }
}
