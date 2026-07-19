using System;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    internal void RaisePlayerDimensionChanged(DimensionChangedEvent changedEvent)
    {
      Action<DimensionChangedEvent> handler = PlayerDimensionChanged;
      if (handler != null)
      {
        handler(changedEvent);
      }
    }

    private void RaiseContentPackChanged(
        DimensionContentPackDefinition contentPack,
        DimensionContentPackChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionContentPackChangedEvent> handler = ContentPackChanged;
      if (handler != null)
      {
        handler(
            new DimensionContentPackChangedEvent(
                contentPack,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseContentOwnershipChanged(
        DimensionContentOwnershipBinding binding,
        DimensionContentOwnershipChangeKind changeKind,
        string previousContentPackId,
        string reason)
    {
      Action<DimensionContentOwnershipChangedEvent> handler = ContentOwnershipChanged;
      if (handler != null)
      {
        handler(
            new DimensionContentOwnershipChangedEvent(
                binding,
                changeKind,
                previousContentPackId,
                reason ?? string.Empty));
      }
    }

    private void RaiseAssetReferenceChanged(
        DimensionAssetReferenceDefinition assetReference,
        DimensionAssetReferenceChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionAssetReferenceChangedEvent> handler = AssetReferenceChanged;
      if (handler != null)
      {
        handler(
            new DimensionAssetReferenceChangedEvent(
                assetReference,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseBiomeChanged(
        DimensionBiomeDefinition biome,
        DimensionBiomeChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionBiomeChangedEvent> handler = BiomeChanged;
      if (handler != null)
      {
        handler(
            new DimensionBiomeChangedEvent(
                biome,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseGenerationTableChanged(
        DimensionGenerationTableDefinition table,
        DimensionGenerationTableChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionGenerationTableChangedEvent> handler = GenerationTableChanged;
      if (handler != null)
      {
        handler(
            new DimensionGenerationTableChangedEvent(
                table,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseGenerationTableEntryChanged(
        DimensionGenerationTableEntryDefinition entry,
        DimensionGenerationTableChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionGenerationTableEntryChangedEvent> handler = GenerationTableEntryChanged;
      if (handler != null)
      {
        handler(
            new DimensionGenerationTableEntryChangedEvent(
                entry,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaisePlayerVisitChanged(
        DimensionPlayerVisitRecord previousVisit,
        DimensionPlayerVisitRecord currentVisit,
        DimensionPlayerVisitChangeKind changeKind,
        string reason)
    {
      Action<DimensionPlayerVisitChangedEvent> handler = PlayerVisitChanged;
      if (handler != null)
      {
        handler(
            new DimensionPlayerVisitChangedEvent(
                previousVisit,
                currentVisit,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaiseDimensionLifecycleChanged(
        string dimensionId,
        DimensionLifecycleState previousState,
        DimensionLifecycleState currentState,
        string reason)
    {
      DimensionLifecycleEvent lifecycleEvent =
          new DimensionLifecycleEvent(dimensionId, previousState, currentState, reason ?? string.Empty);

      Action<DimensionLifecycleEvent> handler = DimensionLifecycleChanged;
      if (handler != null)
      {
        handler(lifecycleEvent);
      }
    }

    private void RaisePortalChanged(
        DimensionPortalDefinition portal,
        DimensionPortalState previousState,
        DimensionPortalState currentState,
        DimensionPortalChangeKind changeKind,
        string reason)
    {
      Action<DimensionPortalChangedEvent> handler = PortalChanged;
      if (handler != null)
      {
        handler(
            new DimensionPortalChangedEvent(
                portal,
                previousState,
                currentState,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaisePortalPresentationChanged(
        DimensionPortalPresentationDefinition presentation,
        DimensionPortalPresentationChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionPortalPresentationChangedEvent> handler = PortalPresentationChanged;
      if (handler != null)
      {
        handler(
            new DimensionPortalPresentationChangedEvent(
                presentation,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseTravelRequirementChanged(
        DimensionTravelRequirementDefinition requirement,
        DimensionTravelRequirementChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionTravelRequirementChangedEvent> handler = TravelRequirementChanged;
      if (handler != null)
      {
        handler(
            new DimensionTravelRequirementChangedEvent(
                requirement,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseMapLayerChanged(
        DimensionMapLayerDefinition previousLayer,
        DimensionMapLayerDefinition currentLayer,
        DimensionMapLayerChangeKind changeKind,
        string reason)
    {
      Action<DimensionMapLayerChangedEvent> handler = MapLayerChanged;
      if (handler != null)
      {
        handler(
            new DimensionMapLayerChangedEvent(
                previousLayer,
                currentLayer,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaiseMarkerChanged(
        DimensionMapMarker previousMarker,
        DimensionMapMarker currentMarker,
        DimensionMarkerChangeKind changeKind,
        string reason)
    {
      Action<DimensionMarkerChangedEvent> handler = MarkerChanged;
      if (handler != null)
      {
        handler(
            new DimensionMarkerChangedEvent(
                previousMarker,
                currentMarker,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaiseAnchorChanged(
        DimensionAnchorDefinition anchor,
        DimensionAnchorChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionAnchorChangedEvent> handler = AnchorChanged;
      if (handler != null)
      {
        handler(
            new DimensionAnchorChangedEvent(
                anchor,
                changeKind,
                previousEnabled,
                currentEnabled,
            reason ?? string.Empty));
      }
    }

    private void RaiseZoneChanged(
        DimensionZoneDefinition zone,
        DimensionZoneChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionZoneChangedEvent> handler = ZoneChanged;
      if (handler != null)
      {
        handler(
            new DimensionZoneChangedEvent(
                zone,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseEnvironmentProfileChanged(
        DimensionEnvironmentProfile profile,
        DimensionEnvironmentProfileChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionEnvironmentProfileChangedEvent> handler = EnvironmentProfileChanged;
      if (handler != null)
      {
        handler(
            new DimensionEnvironmentProfileChangedEvent(
                profile,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseSceneChanged(
        DimensionSceneDefinition scene,
        DimensionSceneChangeKind changeKind,
        DimensionSceneState previousState,
        DimensionSceneState currentState,
        string reason)
    {
      Action<DimensionSceneChangedEvent> handler = SceneChanged;
      if (handler != null)
      {
        handler(
            new DimensionSceneChangedEvent(
                scene,
                changeKind,
                previousState,
                currentState,
            reason ?? string.Empty));
      }
    }

    private void RaiseSceneTemplateChanged(
        DimensionSceneTemplateDefinition template,
        DimensionSceneTemplateChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionSceneTemplateChangedEvent> handler = SceneTemplateChanged;
      if (handler != null)
      {
        handler(
            new DimensionSceneTemplateChangedEvent(
                template,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseEncounterChanged(
        DimensionEncounterDefinition encounter,
        DimensionEncounterChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionEncounterChangedEvent> handler = EncounterChanged;
      if (handler != null)
      {
        handler(
            new DimensionEncounterChangedEvent(
                encounter,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseSpawnRuleChanged(
        DimensionSpawnRule rule,
        DimensionSpawnRuleChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionSpawnRuleChangedEvent> handler = SpawnRuleChanged;
      if (handler != null)
      {
        handler(
            new DimensionSpawnRuleChangedEvent(
                rule,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseResourceNodeChanged(
        DimensionResourceNodeDefinition node,
        DimensionResourceNodeChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionResourceNodeChangedEvent> handler = ResourceNodeChanged;
      if (handler != null)
      {
        handler(
            new DimensionResourceNodeChangedEvent(
                node,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseWorldEventChanged(
        DimensionWorldEventDefinition worldEvent,
        DimensionWorldEventChangeKind changeKind,
        bool previousEnabled,
        bool currentEnabled,
        string reason)
    {
      Action<DimensionWorldEventChangedEvent> handler = WorldEventChanged;
      if (handler != null)
      {
        handler(
            new DimensionWorldEventChangedEvent(
                worldEvent,
                changeKind,
                previousEnabled,
                currentEnabled,
                reason ?? string.Empty));
      }
    }

    private void RaiseProgressFlagChanged(
        DimensionProgressFlag previousFlag,
        DimensionProgressFlag currentFlag,
        DimensionProgressFlagChangeKind changeKind,
        string reason)
    {
      Action<DimensionProgressFlagChangedEvent> handler = ProgressFlagChanged;
      if (handler != null)
      {
        handler(
            new DimensionProgressFlagChangedEvent(
                previousFlag,
                currentFlag,
                changeKind,
                reason ?? string.Empty));
      }
    }

    private void RaiseLoadTicketChanged(RuntimeLoadRecord record)
    {
      Action<DimensionLoadTicketSnapshot> handler = LoadTicketChanged;
      if (handler != null && record != null)
      {
        handler(CreateLoadTicketSnapshot(record));
      }
    }
  }
}
