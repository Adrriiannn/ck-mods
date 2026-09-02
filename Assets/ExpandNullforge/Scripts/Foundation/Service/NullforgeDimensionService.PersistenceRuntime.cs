using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private void PersistDimensionIfWorldRegistryLoaded(DimensionDefinition definition, bool builtIn)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertDimension(definition, builtIn);
    }

    private void RemovePersistedDimensionIfWorldRegistryLoaded(string dimensionId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveDimension(dimensionId);
    }

    private readonly List<DimensionDefinition> persistedDefinitions =
        new List<DimensionDefinition>();

    private readonly List<DimensionPlayerVisitRecord> persistedPlayerVisits =
        new List<DimensionPlayerVisitRecord>();

    private readonly List<DimensionPortalDefinition> persistedPortals =
        new List<DimensionPortalDefinition>();

    private readonly List<DimensionMapMarker> persistedMarkers =
        new List<DimensionMapMarker>();

    private readonly List<DimensionAnchorDefinition> persistedAnchors =
        new List<DimensionAnchorDefinition>();

    private readonly List<DimensionSceneDefinition> persistedScenes =
        new List<DimensionSceneDefinition>();

    private readonly List<DimensionProgressFlag> persistedProgressFlags =
        new List<DimensionProgressFlag>();

    private readonly List<DimensionGenerationStatus> persistedGenerationStatuses =
        new List<DimensionGenerationStatus>();

    private readonly List<DimensionContentOwnershipBinding> persistedContentOwnershipBindings =
        new List<DimensionContentOwnershipBinding>();

    public void LoadPersistedDefinitionsForCurrentWorld()
    {
      persistedDefinitions.Clear();
      DimensionWorldRegistry.GetDimensions(persistedDefinitions);
      for (int i = 0; i < persistedDefinitions.Count; i++)
      {
        DimensionDefinition persisted = persistedDefinitions[i];
        if (IsProtectedDimensionId(persisted.Id) || string.IsNullOrEmpty(persisted.Id))
        {
          continue;
        }

        if (definitions.ContainsKey(persisted.Id))
        {
          definitions[persisted.Id] = persisted;
        }
        else if (!OverlapsExistingNonOverworldDimension(persisted))
        {
          definitions[persisted.Id] = persisted;
        }
        else
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              persisted.Id,
              "Skipped persisted dimension because its absolute bounds overlap another registered dimension.");
        }
      }

      RebuildDefinitionSnapshot();
      LoadPersistedPortalsForCurrentWorld();
      LoadPersistedMarkersForCurrentWorld();
      LoadPersistedAnchorsForCurrentWorld();
      LoadPersistedScenesForCurrentWorld();
      LoadPersistedProgressFlagsForCurrentWorld();
      LoadPersistedGenerationStatusesForCurrentWorld();
      LoadPersistedContentOwnershipForCurrentWorld();
      BindBuiltInContentOwnership();
      PersistBuiltInDefinitionsForCurrentWorld();
    }

    public void PersistBuiltInDefinitionsForCurrentWorld()
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertDimension(OverworldDefinition, true);
    }

    private void LoadPersistedPortalsForCurrentWorld()
    {
      persistedPortals.Clear();
      DimensionWorldRegistry.GetPortals(persistedPortals);
      for (int i = 0; i < persistedPortals.Count; i++)
      {
        DimensionPortalDefinition portal = persistedPortals[i];
        DimensionDefinition from;
        DimensionDefinition to;
        if (string.IsNullOrEmpty(portal.PortalId) ||
            !TryGetDimension(portal.FromDimensionId, out from) ||
            !TryGetDimension(portal.ToDimensionId, out to) ||
            !from.ContainsLocal(portal.FromLocalPosition) ||
            !to.ContainsLocal(portal.ToLocalPosition))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              portal.FromDimensionId,
              "Skipped invalid persisted portal: " + portal.PortalId + ".");
          continue;
        }

        portals[portal.PortalId] = portal;
      }
    }

    private void LoadPersistedMarkersForCurrentWorld()
    {
      persistedMarkers.Clear();
      DimensionWorldRegistry.GetMarkers(persistedMarkers);
      for (int i = 0; i < persistedMarkers.Count; i++)
      {
        DimensionMapMarker marker = persistedMarkers[i];
        DimensionDefinition dimension;
        if (string.IsNullOrEmpty(marker.MarkerId) ||
            !TryGetDimension(marker.DimensionId, out dimension) ||
            !dimension.ContainsLocal(marker.LocalPosition))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              marker.DimensionId,
              "Skipped invalid persisted marker: " + marker.MarkerId + ".");
          continue;
        }

        markers[marker.MarkerId] = marker;
      }
    }

    private void LoadPersistedAnchorsForCurrentWorld()
    {
      persistedAnchors.Clear();
      DimensionWorldRegistry.GetAnchors(persistedAnchors);
      for (int i = 0; i < persistedAnchors.Count; i++)
      {
        DimensionAnchorDefinition anchor = persistedAnchors[i];
        DimensionDefinition dimension;
        if (string.IsNullOrEmpty(anchor.AnchorId) ||
            !TryGetDimension(anchor.DimensionId, out dimension) ||
            !dimension.ContainsLocal(anchor.LocalPosition) ||
            !IsValidAnchorKind(anchor.Kind))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              anchor.DimensionId,
              "Skipped invalid persisted anchor: " + anchor.AnchorId + ".");
          continue;
        }

        anchors[anchor.AnchorId] = anchor;
      }
    }

    private void LoadPersistedScenesForCurrentWorld()
    {
      scenes.Clear();
      persistedScenes.Clear();
      DimensionWorldRegistry.GetScenes(persistedScenes);
      for (int i = 0; i < persistedScenes.Count; i++)
      {
        DimensionSceneDefinition scene = persistedScenes[i];
        DimensionOperationResult validation;
        if (!ValidateScene(scene, scene.SceneId, out validation))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              scene.DimensionId,
              "Skipped invalid persisted scene: " + scene.SceneId + ".");
          continue;
        }

        scenes[scene.SceneId] = scene;
      }
    }

    private void LoadPersistedProgressFlagsForCurrentWorld()
    {
      progressFlags.Clear();
      persistedProgressFlags.Clear();
      DimensionWorldRegistry.GetProgressFlags(persistedProgressFlags);
      for (int i = 0; i < persistedProgressFlags.Count; i++)
      {
        DimensionProgressFlag flag = persistedProgressFlags[i];
        DimensionOperationResult validation;
        if (!ValidateProgressFlag(flag, out validation))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              flag.DimensionId,
              "Skipped invalid persisted progress flag: " + flag.FlagId + ".");
          continue;
        }

        progressFlags[flag.FlagId] = flag;
      }
    }

    private void LoadPersistedGenerationStatusesForCurrentWorld()
    {
      generationStatuses.Clear();
      persistedGenerationStatuses.Clear();
      DimensionWorldRegistry.GetGeneratedAreas(persistedGenerationStatuses);
      for (int i = 0; i < persistedGenerationStatuses.Count; i++)
      {
        DimensionGenerationStatus status = persistedGenerationStatuses[i];
        DimensionDefinition dimension;
        string boundsError;
        if (string.IsNullOrEmpty(status.DimensionId) ||
            !TryGetDimension(status.DimensionId, out dimension) ||
            !IsValidGenerationBounds(status.LocalBounds, out boundsError) ||
            !dimension.ContainsLocal(status.LocalBounds.Min) ||
            !dimension.ContainsLocal(status.LocalBounds.MaxExclusive - new int2(1, 1)))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              status.DimensionId,
              "Skipped invalid persisted generation status.");
          continue;
        }

        DimensionGenerationStatus loadedStatus = NormalizePersistedGenerationStatus(status);
        generationStatuses[GenerationStatusKey(loadedStatus.DimensionId, loadedStatus.LocalBounds)] =
            loadedStatus;
        if (loadedStatus.State != status.State ||
            !string.Equals(loadedStatus.Message, status.Message, StringComparison.Ordinal))
        {
          DimensionWorldRegistry.UpsertGeneratedArea(loadedStatus);
        }
      }
    }

    private void LoadPersistedContentOwnershipForCurrentWorld()
    {
      persistedContentOwnershipBindings.Clear();
      DimensionWorldRegistry.GetContentOwnershipBindings(persistedContentOwnershipBindings);
      for (int i = 0; i < persistedContentOwnershipBindings.Count; i++)
      {
        DimensionContentOwnershipBinding binding = persistedContentOwnershipBindings[i];
        if (string.IsNullOrEmpty(binding.ContentPackId) ||
            string.IsNullOrEmpty(binding.RecordId) ||
            !IsValidContentRecordKind(binding.RecordKind))
        {
          AddDiagnostic(
              DimensionDiagnosticSeverity.Warning,
              binding.ContentPackId,
              "Skipped invalid persisted content ownership binding.");
          continue;
        }

        contentOwnershipBindings[BuildContentOwnershipKey(binding.RecordKind, binding.RecordId)] =
            binding;
      }
    }

    private void PersistPortalIfWorldRegistryLoaded(DimensionPortalDefinition portal)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertPortal(portal);
    }

    private void RemovePersistedPortalIfWorldRegistryLoaded(string portalId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemovePortal(portalId);
    }

    private void PersistMarkerIfWorldRegistryLoaded(DimensionMapMarker marker)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertMarker(marker);
    }

    private void PersistAnchorIfWorldRegistryLoaded(DimensionAnchorDefinition anchor)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertAnchor(anchor);
    }

    private void PersistSceneIfWorldRegistryLoaded(DimensionSceneDefinition scene)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertScene(scene);
    }

    private void PersistProgressFlagIfWorldRegistryLoaded(DimensionProgressFlag flag)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertProgressFlag(flag);
    }

    private void PersistContentOwnershipIfWorldRegistryLoaded(DimensionContentOwnershipBinding binding)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertContentOwnership(binding);
    }

    private void PersistGenerationStatusIfWorldRegistryLoaded(DimensionGenerationStatus status)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.UpsertGeneratedArea(status);
    }

    private void RemovePersistedGeneratedAreaIfWorldRegistryLoaded(
        string dimensionId,
        DimensionBounds localBounds)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveGeneratedArea(dimensionId, localBounds);
    }

    private void RemovePersistedContentOwnershipIfWorldRegistryLoaded(
        DimensionContentRecordKind recordKind,
        string recordId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveContentOwnership(recordKind, recordId);
    }

    private void RemovePersistedMarkerIfWorldRegistryLoaded(string markerId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveMarker(markerId);
    }

    private void RemovePersistedAnchorIfWorldRegistryLoaded(string anchorId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveAnchor(anchorId);
    }

    private void RemovePersistedSceneIfWorldRegistryLoaded(string sceneId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveScene(sceneId);
    }

    private void RemovePersistedProgressFlagIfWorldRegistryLoaded(string flagId)
    {
      if (!DimensionWorldRegistry.IsLoaded)
      {
        return;
      }

      DimensionWorldRegistry.RemoveProgressFlag(flagId);
    }
  }
}
