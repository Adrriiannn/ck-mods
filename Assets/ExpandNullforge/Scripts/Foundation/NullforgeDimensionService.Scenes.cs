using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionSceneDefinition> GetScenes(string dimensionId, bool includeDisabled)
    {
      List<DimensionSceneDefinition> result = new List<DimensionSceneDefinition>();
      foreach (DimensionSceneDefinition scene in scenes.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(scene.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!includeDisabled && scene.State == DimensionSceneState.Disabled)
        {
          continue;
        }

        result.Add(scene);
      }

      result.Sort(CompareScenes);
      return result;
    }

    public bool TryGetScene(string sceneId, out DimensionSceneDefinition scene)
    {
      if (string.IsNullOrEmpty(sceneId))
      {
        scene = default(DimensionSceneDefinition);
        return false;
      }

      return scenes.TryGetValue(sceneId, out scene);
    }

    public bool TryFindSceneAtLocal(
        string dimensionId,
        float2 localPosition,
        out DimensionSceneDefinition scene)
    {
      scene = default(DimensionSceneDefinition);
      if (string.IsNullOrEmpty(dimensionId))
      {
        return false;
      }

      bool found = false;
      foreach (DimensionSceneDefinition candidate in scenes.Values)
      {
        if (candidate.State == DimensionSceneState.Disabled ||
            !string.Equals(candidate.DimensionId, dimensionId, StringComparison.Ordinal) ||
            !candidate.LocalBounds.Contains(localPosition))
        {
          continue;
        }

        if (!found || CompareScenes(candidate, scene) < 0)
        {
          scene = candidate;
          found = true;
        }
      }

      return found;
    }

    public bool TryRegisterScene(DimensionSceneDefinition scene, out DimensionOperationResult result)
    {
      if (!ValidateScene(scene, string.Empty, out result))
      {
        return false;
      }

      if (scenes.ContainsKey(scene.SceneId))
      {
        result = DimensionOperationResult.Failed("scene-already-registered", "A scene with that id is already registered.");
        return false;
      }

      scenes[scene.SceneId] = scene;
      PersistSceneIfWorldRegistryLoaded(scene);
      RaiseSceneChanged(
          scene,
          DimensionSceneChangeKind.Registered,
          DimensionSceneState.Disabled,
          scene.State,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateScene(
        DimensionSceneDefinition scene,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionSceneDefinition previous;
      if (!scenes.TryGetValue(scene.SceneId, out previous))
      {
        result = DimensionOperationResult.Failed("scene-not-found", "No scene with that id is registered.");
        return false;
      }

      if (!ValidateScene(scene, scene.SceneId, out result))
      {
        return false;
      }

      if (SceneEquals(previous, scene))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      scenes[scene.SceneId] = scene;
      PersistSceneIfWorldRegistryLoaded(scene);
      RaiseSceneChanged(
          scene,
          previous.State == scene.State
              ? DimensionSceneChangeKind.Updated
              : DimensionSceneChangeKind.StateChanged,
          previous.State,
          scene.State,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetSceneState(
        string sceneId,
        DimensionSceneState state,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(sceneId))
      {
        result = DimensionOperationResult.Failed("scene-id-empty", "A scene id is required.");
        return false;
      }

      if (!IsValidSceneState(state))
      {
        result = DimensionOperationResult.Failed("scene-state-invalid", "The requested scene state is invalid.");
        return false;
      }

      DimensionSceneDefinition scene;
      if (!scenes.TryGetValue(sceneId, out scene))
      {
        result = DimensionOperationResult.Failed("scene-not-found", "No scene with that id is registered.");
        return false;
      }

      if (scene.State == state)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionSceneDefinition updated =
          new DimensionSceneDefinition(
              scene.SceneId,
              scene.DisplayName,
              scene.DimensionId,
              scene.LocalBounds,
              scene.Kind,
              scene.Priority,
              state);

      if (!ValidateScene(updated, updated.SceneId, out result))
      {
        return false;
      }

      scenes[sceneId] = updated;
      PersistSceneIfWorldRegistryLoaded(updated);
      RaiseSceneChanged(
          updated,
          DimensionSceneChangeKind.StateChanged,
          scene.State,
          updated.State,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveScene(string sceneId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(sceneId))
      {
        result = DimensionOperationResult.Failed("scene-id-empty", "A scene id is required.");
        return false;
      }

      DimensionSceneDefinition scene;
      if (!scenes.TryGetValue(sceneId, out scene))
      {
        result = DimensionOperationResult.Failed("scene-not-found", "No scene with that id is registered.");
        return false;
      }

      scenes.Remove(sceneId);
      RemovePersistedSceneIfWorldRegistryLoaded(sceneId);
      RaiseSceneChanged(
          scene,
          DimensionSceneChangeKind.Removed,
          scene.State,
          DimensionSceneState.Disabled,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<DimensionSceneTemplateDefinition> GetSceneTemplates(
        string dimensionId,
        string zoneId,
        string kind,
        bool includeDisabled)
    {
      List<DimensionSceneTemplateDefinition> result =
          new List<DimensionSceneTemplateDefinition>();
      foreach (DimensionSceneTemplateDefinition template in sceneTemplates.Values)
      {
        if (!SceneTemplateMatchesQuery(template, dimensionId, zoneId, kind, includeDisabled))
        {
          continue;
        }

        result.Add(template);
      }

      result.Sort(CompareSceneTemplates);
      return result;
    }

    public bool TryGetSceneTemplate(
        string templateId,
        out DimensionSceneTemplateDefinition template)
    {
      if (string.IsNullOrEmpty(templateId))
      {
        template = default(DimensionSceneTemplateDefinition);
        return false;
      }

      return sceneTemplates.TryGetValue(templateId, out template);
    }

    public bool TryRegisterSceneTemplate(
        DimensionSceneTemplateDefinition template,
        out DimensionOperationResult result)
    {
      if (!ValidateSceneTemplate(template, out result))
      {
        return false;
      }

      if (sceneTemplates.ContainsKey(template.TemplateId))
      {
        result = DimensionOperationResult.Failed("scene-template-already-registered", "A scene template with that id is already registered.");
        return false;
      }

      sceneTemplates[template.TemplateId] = template;
      RaiseSceneTemplateChanged(
          template,
          DimensionSceneTemplateChangeKind.Registered,
          false,
          template.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateSceneTemplate(
        DimensionSceneTemplateDefinition template,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionSceneTemplateDefinition previous;
      if (string.IsNullOrEmpty(template.TemplateId))
      {
        result = DimensionOperationResult.Failed("scene-template-id-empty", "A scene template id is required.");
        return false;
      }

      if (!sceneTemplates.TryGetValue(template.TemplateId, out previous))
      {
        result = DimensionOperationResult.Failed("scene-template-not-found", "No scene template with that id is registered.");
        return false;
      }

      if (!ValidateSceneTemplate(template, out result))
      {
        return false;
      }

      if (SceneTemplateEquals(previous, template))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      sceneTemplates[template.TemplateId] = template;
      RaiseSceneTemplateChanged(
          template,
          previous.Enabled == template.Enabled
              ? DimensionSceneTemplateChangeKind.Updated
              : DimensionSceneTemplateChangeKind.EnabledChanged,
          previous.Enabled,
          template.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetSceneTemplateEnabled(
        string templateId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(templateId))
      {
        result = DimensionOperationResult.Failed("scene-template-id-empty", "A scene template id is required.");
        return false;
      }

      DimensionSceneTemplateDefinition template;
      if (!sceneTemplates.TryGetValue(templateId, out template))
      {
        result = DimensionOperationResult.Failed("scene-template-not-found", "No scene template with that id is registered.");
        return false;
      }

      if (template.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionSceneTemplateDefinition updated =
          new DimensionSceneTemplateDefinition(
              template.TemplateId,
              template.DisplayName,
              template.DimensionId,
              template.ZoneId,
              template.Kind,
              template.ProviderId,
              template.FootprintSize,
              template.Weight,
              template.Priority,
              enabled);
      sceneTemplates[templateId] = updated;
      RaiseSceneTemplateChanged(
          updated,
          DimensionSceneTemplateChangeKind.EnabledChanged,
          template.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveSceneTemplate(
        string templateId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(templateId))
      {
        result = DimensionOperationResult.Failed("scene-template-id-empty", "A scene template id is required.");
        return false;
      }

      DimensionSceneTemplateDefinition template;
      if (!sceneTemplates.TryGetValue(templateId, out template))
      {
        result = DimensionOperationResult.Failed("scene-template-not-found", "No scene template with that id is registered.");
        return false;
      }

      sceneTemplates.Remove(templateId);
      RaiseSceneTemplateChanged(
          template,
          DimensionSceneTemplateChangeKind.Removed,
          template.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
