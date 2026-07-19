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
    public DimensionRuntimeSnapshot GetRuntimeSnapshot()
    {
      return new DimensionRuntimeSnapshot(
          definitions.Count,
          contentPacks.Count,
          contentOwnershipBindings.Count,
          assetReferences.Count,
          biomes.Count,
          generationTables.Count,
          generationTableEntries.Count,
          DimensionWorldRegistry.PlayerVisitCount,
          portals.Count,
          portalPresentations.Count,
          travelRequirements.Count,
          travelRequirementEvaluators.Count,
          mapLayers.Count,
          markers.Count,
          anchors.Count,
          scenes.Count,
          sceneTemplates.Count,
          encounters.Count,
          resourceNodes.Count,
          spawnRules.Count,
          worldEvents.Count,
          progressFlags.Count,
          generationStatuses.Count,
          generationReservations.Count,
          generationProviders.Count,
          generationPasses.Count,
          zoneDefinitions.Count,
          environmentProfiles.Count,
          zoneProviders.Count,
          accessProviders.Count,
          runtimeGenerationRecords.Count,
          loadTickets.Count,
          runtimeLoadRecords.Count,
          runtimeSimulationRegionAnchors.Count,
          observedSubMaps.Count,
          IsServerWorldAvailable(),
          clientWorld != null && clientWorld.IsCreated,
          DimensionWorldRegistry.IsLoaded,
          DimensionWorldRegistry.Revision,
          DimensionWorldRegistry.WorldKey);
    }

    public DimensionMultiplayerSnapshot GetMultiplayerSnapshot()
    {
      return new DimensionMultiplayerSnapshot(
          IsServerWorldAvailable(),
          clientWorld != null && clientWorld.IsCreated,
          serverQueriesCreated,
          trackedPlayerContexts.Count,
          observedPlayerIds.Count,
          pendingTravelByPlayerId.Count,
          loadTickets.Count,
          runtimeLoadRecords.Count,
          DimensionWorldRegistry.IsLoaded,
          DimensionWorldRegistry.WorldKey);
    }

    public DimensionDebugSnapshot GetDebugSnapshot(
        Entity player,
        float2 fallbackAbsolutePosition)
    {
      DimensionContext context;
      if (player == Entity.Null || !TryGetPlayerContext(player, out context) || !context.IsKnown)
      {
        context = GetContextForAbsolute(fallbackAbsolutePosition);
      }

      if (!context.IsKnown)
      {
        DimensionPersistenceHealthSnapshot persistence = GetPersistenceHealthSnapshot();
        return new DimensionDebugSnapshot(
            false,
            string.Empty,
            fallbackAbsolutePosition,
            fallbackAbsolutePosition,
            string.Empty,
            string.Empty,
            false,
            pendingTravelByPlayerId.Count,
            runtimeLoadRecords.Count,
            persistence.Registry.Revision,
            persistence.ReadyForWorldUnload,
            "dimension-context-unknown",
            "No registered dimension contains the requested debug position.");
      }

      DimensionZoneInfo zone;
      bool hasZone = TryGetZoneAtLocal(context.DimensionId, context.LocalPosition, out zone);
      int2 localTile =
          new int2(
              (int)math.floor(context.LocalPosition.x),
              (int)math.floor(context.LocalPosition.y));
      DimensionBounds tileBounds =
          new DimensionBounds(localTile, localTile + new int2(1, 1));
      bool isGenerated = IsAreaGenerated(context.DimensionId, tileBounds);
      DimensionPersistenceHealthSnapshot health = GetPersistenceHealthSnapshot();

      return new DimensionDebugSnapshot(
          true,
          context.DimensionId,
          context.AbsolutePosition,
          context.LocalPosition,
          hasZone ? zone.ZoneId : string.Empty,
          hasZone ? zone.DisplayName : string.Empty,
          isGenerated,
          pendingTravelByPlayerId.Count,
          runtimeLoadRecords.Count,
          health.Registry.Revision,
          health.ReadyForWorldUnload,
          "dimension-debug-ready",
          "Dimension debug snapshot resolved.");
    }

    public DimensionEntityContextCostSnapshot GetEntityContextCostSnapshot()
    {
      int nonOverworldDimensionCount = 0;
      for (int i = 0; i < definitionSnapshot.Count; i++)
      {
        if (definitionSnapshot[i].Id != DimensionIds.Overworld)
        {
          nonOverworldDimensionCount++;
        }
      }

      return new DimensionEntityContextCostSnapshot(
          definitionSnapshot.Count,
          nonOverworldDimensionCount,
          nonOverworldDimensionCount,
          1,
          1,
          2,
          IsServerWorldAvailable(),
          clientWorld != null && clientWorld.IsCreated,
          false,
          "Unity.Transforms.LocalTransform",
          "dimension-entity-context-cost-ready",
          "Entity context lookup reads LocalTransform from the requested world and checks registered dimension bounds; it does not enumerate world entities.");
    }

    public DimensionRegistrySnapshot GetRegistrySnapshot()
    {
      return DimensionWorldRegistry.GetSnapshot();
    }

    public DimensionPersistenceHealthSnapshot GetPersistenceHealthSnapshot()
    {
      return DimensionWorldRegistry.GetPersistenceHealthSnapshot();
    }

    public DimensionOperationResult ForceFlushPersistence(string reason)
    {
      DimensionOperationResult result = DimensionWorldRegistry.ForceFlushNow(reason ?? string.Empty);
      AddDiagnostic(
          result.Success ? DimensionDiagnosticSeverity.Info : DimensionDiagnosticSeverity.Error,
          string.Empty,
          result.Code + ": " + result.Message);
      return result;
    }

    public DimensionOperationResult PreparePersistenceForWorldUnload(string reason)
    {
      DimensionOperationResult result =
          DimensionWorldRegistry.PrepareForWorldUnload(reason ?? "world-unload");
      AddDiagnostic(
          result.Success ? DimensionDiagnosticSeverity.Info : DimensionDiagnosticSeverity.Error,
          string.Empty,
          result.Code + ": " + result.Message);
      return result;
    }

    public IReadOnlyList<DimensionTravelSnapshot> GetTravelSnapshots()
    {
      return GetTravelSnapshots(default(DimensionTravelSnapshotQuery));
    }

    public IReadOnlyList<DimensionTravelSnapshot> GetTravelSnapshots(
        DimensionTravelSnapshotQuery query)
    {
      List<DimensionTravelSnapshot> result =
          new List<DimensionTravelSnapshot>(pendingTravelByPlayerId.Count);

      foreach (PendingTravelRecord record in pendingTravelByPlayerId.Values)
      {
        DimensionTravelSnapshot snapshot = ToTravelSnapshot(record);
        if (TravelSnapshotMatchesQuery(snapshot, query))
        {
          result.Add(snapshot);
        }
      }

      result.Sort(CompareTravelSnapshots);
      return result;
    }

    public bool TryGetTravelSnapshot(
        string travelId,
        out DimensionTravelSnapshot snapshot)
    {
      snapshot = default(DimensionTravelSnapshot);
      if (string.IsNullOrEmpty(travelId))
      {
        return false;
      }

      foreach (PendingTravelRecord record in pendingTravelByPlayerId.Values)
      {
        if (string.Equals(record.TravelId, travelId, StringComparison.Ordinal))
        {
          snapshot = ToTravelSnapshot(record);
          return true;
        }
      }

      return false;
    }
  }
}
