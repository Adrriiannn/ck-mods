using System;
using ExpandNullforge.Api;
using Unity.Mathematics;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private bool ValidateScene(
        DimensionSceneDefinition scene,
        string allowedExistingSceneId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(scene.SceneId))
      {
        result = DimensionOperationResult.Failed("scene-id-empty", "A scene id is required.");
        return false;
      }

      if (!IsValidSceneState(scene.State))
      {
        result = DimensionOperationResult.Failed("scene-state-invalid", "The scene state is not supported.");
        return false;
      }

      if (scene.LocalBounds.Size.x <= 0 || scene.LocalBounds.Size.y <= 0)
      {
        result = DimensionOperationResult.Failed("scene-bounds-invalid", "The scene local bounds must have a positive size.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(scene.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("scene-dimension-not-found", "The scene dimension is not registered.");
        return false;
      }

      if (!dimension.LocalBounds.Contains(scene.LocalBounds.Min) ||
          !dimension.LocalBounds.Contains(scene.LocalBounds.MaxExclusive - new int2(1, 1)))
      {
        result = DimensionOperationResult.Failed("scene-bounds-out-of-dimension", "The scene bounds are outside the scene dimension.");
        return false;
      }

      DimensionSceneDefinition overlap;
      if (SceneBlocksOverlap(scene.State) &&
          TryFindOverlappingScene(scene, allowedExistingSceneId, out overlap))
      {
        result =
            DimensionOperationResult.Failed(
                "scene-bounds-overlap",
                "The scene bounds overlap an existing scene reservation: " + overlap.SceneId + ".");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool TryFindOverlappingScene(
        DimensionSceneDefinition scene,
        string allowedExistingSceneId,
        out DimensionSceneDefinition overlap)
    {
      foreach (DimensionSceneDefinition candidate in scenes.Values)
      {
        if (!SceneBlocksOverlap(candidate.State) ||
            !string.Equals(candidate.DimensionId, scene.DimensionId, StringComparison.Ordinal) ||
            string.Equals(candidate.SceneId, allowedExistingSceneId, StringComparison.Ordinal))
        {
          continue;
        }

        if (BoundsOverlap(candidate.LocalBounds, scene.LocalBounds))
        {
          overlap = candidate;
          return true;
        }
      }

      overlap = default(DimensionSceneDefinition);
      return false;
    }

    private static bool SceneBlocksOverlap(DimensionSceneState state)
    {
      return state != DimensionSceneState.Disabled;
    }

    private static int CompareScenes(
        DimensionSceneDefinition left,
        DimensionSceneDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int state = left.State.CompareTo(right.State);
      if (state != 0)
      {
        return state;
      }

      return string.Compare(left.SceneId, right.SceneId, StringComparison.Ordinal);
    }

    private bool SceneEquals(
        DimensionSceneDefinition a,
        DimensionSceneDefinition b)
    {
      return string.Equals(a.SceneId, b.SceneId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             BoundsEqual(a.LocalBounds, b.LocalBounds) &&
             string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
             a.Priority == b.Priority &&
             a.State == b.State;
    }

    private bool ValidateSceneTemplate(
        DimensionSceneTemplateDefinition template,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(template.TemplateId))
      {
        result = DimensionOperationResult.Failed("scene-template-id-empty", "A scene template id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(template.Kind))
      {
        result = DimensionOperationResult.Failed("scene-template-kind-empty", "A scene template kind is required.");
        return false;
      }

      if (template.FootprintSize.x <= 0 || template.FootprintSize.y <= 0)
      {
        result = DimensionOperationResult.Failed("scene-template-footprint-invalid", "A scene template footprint must have a positive size.");
        return false;
      }

      if (template.Weight <= 0)
      {
        result = DimensionOperationResult.Failed("scene-template-weight-invalid", "A scene template weight must be greater than zero.");
        return false;
      }

      DimensionDefinition dimension;
      if (!TryGetDimension(template.DimensionId, out dimension))
      {
        result = DimensionOperationResult.Failed("scene-template-dimension-not-found", "The scene template dimension is not registered.");
        return false;
      }

      if (template.FootprintSize.x > dimension.LocalBounds.Size.x ||
          template.FootprintSize.y > dimension.LocalBounds.Size.y)
      {
        result = DimensionOperationResult.Failed("scene-template-footprint-too-large", "The scene template footprint is larger than the target dimension.");
        return false;
      }

      if (!string.IsNullOrEmpty(template.ZoneId))
      {
        DimensionZoneDefinition zone;
        if (zoneDefinitions.TryGetValue(template.ZoneId, out zone) &&
            !string.Equals(zone.DimensionId, template.DimensionId, StringComparison.Ordinal))
        {
          result = DimensionOperationResult.Failed("scene-template-zone-dimension-mismatch", "The scene template zone belongs to another dimension.");
          return false;
        }
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private static bool SceneTemplateMatchesQuery(
        DimensionSceneTemplateDefinition template,
        string dimensionId,
        string zoneId,
        string kind,
        bool includeDisabled)
    {
      if (!includeDisabled && !template.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(dimensionId) &&
          !string.Equals(template.DimensionId, dimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(zoneId) &&
          !string.IsNullOrEmpty(template.ZoneId) &&
          !string.Equals(template.ZoneId, zoneId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(kind) &&
          !string.Equals(template.Kind, kind, StringComparison.Ordinal))
      {
        return false;
      }

      return true;
    }

    private static int CompareSceneTemplates(
        DimensionSceneTemplateDefinition left,
        DimensionSceneTemplateDefinition right)
    {
      int priority = left.Priority.CompareTo(right.Priority);
      if (priority != 0)
      {
        return priority;
      }

      int weight = right.Weight.CompareTo(left.Weight);
      if (weight != 0)
      {
        return weight;
      }

      int dimension = string.Compare(left.DimensionId, right.DimensionId, StringComparison.Ordinal);
      if (dimension != 0)
      {
        return dimension;
      }

      int zone = string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal);
      if (zone != 0)
      {
        return zone;
      }

      int kind = string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
      if (kind != 0)
      {
        return kind;
      }

      return string.Compare(left.TemplateId, right.TemplateId, StringComparison.Ordinal);
    }

    private bool SceneTemplateEquals(
        DimensionSceneTemplateDefinition a,
        DimensionSceneTemplateDefinition b)
    {
      return string.Equals(a.TemplateId, b.TemplateId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) &&
             string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
             string.Equals(a.ProviderId, b.ProviderId, StringComparison.Ordinal) &&
             a.FootprintSize.x == b.FootprintSize.x &&
             a.FootprintSize.y == b.FootprintSize.y &&
             a.Weight == b.Weight &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled;
    }
  }
}
