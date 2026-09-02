using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionWorldEventDefinition> GetWorldEvents(
        DimensionWorldEventQuery query)
    {
      List<DimensionWorldEventDefinition> result =
          new List<DimensionWorldEventDefinition>();
      foreach (DimensionWorldEventDefinition worldEvent in worldEvents.Values)
      {
        if (!WorldEventMatchesQuery(worldEvent, query))
        {
          continue;
        }

        result.Add(worldEvent);
      }

      result.Sort(CompareWorldEvents);
      return result;
    }

    public bool TryGetWorldEvent(
        string eventId,
        out DimensionWorldEventDefinition worldEvent)
    {
      if (string.IsNullOrEmpty(eventId))
      {
        worldEvent = default(DimensionWorldEventDefinition);
        return false;
      }

      return worldEvents.TryGetValue(eventId, out worldEvent);
    }

    public bool TryRegisterWorldEvent(
        DimensionWorldEventDefinition worldEvent,
        out DimensionOperationResult result)
    {
      if (!ValidateWorldEvent(worldEvent, out result))
      {
        return false;
      }

      if (worldEvents.ContainsKey(worldEvent.EventId))
      {
        result = DimensionOperationResult.Failed("world-event-already-registered", "A world event with that id is already registered.");
        return false;
      }

      worldEvents[worldEvent.EventId] = worldEvent;
      RaiseWorldEventChanged(
          worldEvent,
          DimensionWorldEventChangeKind.Registered,
          false,
          worldEvent.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateWorldEvent(
        DimensionWorldEventDefinition worldEvent,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionWorldEventDefinition previous;
      if (!worldEvents.TryGetValue(worldEvent.EventId, out previous))
      {
        result = DimensionOperationResult.Failed("world-event-not-found", "No world event with that id is registered.");
        return false;
      }

      if (!ValidateWorldEvent(worldEvent, out result))
      {
        return false;
      }

      if (WorldEventEquals(previous, worldEvent))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      worldEvents[worldEvent.EventId] = worldEvent;
      RaiseWorldEventChanged(
          worldEvent,
          previous.Enabled == worldEvent.Enabled
              ? DimensionWorldEventChangeKind.Updated
              : DimensionWorldEventChangeKind.EnabledChanged,
          previous.Enabled,
          worldEvent.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetWorldEventEnabled(
        string eventId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(eventId))
      {
        result = DimensionOperationResult.Failed("world-event-id-empty", "A world event id is required.");
        return false;
      }

      DimensionWorldEventDefinition worldEvent;
      if (!worldEvents.TryGetValue(eventId, out worldEvent))
      {
        result = DimensionOperationResult.Failed("world-event-not-found", "No world event with that id is registered.");
        return false;
      }

      if (worldEvent.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionWorldEventDefinition updated =
          new DimensionWorldEventDefinition(
              worldEvent.EventId,
              worldEvent.DisplayName,
              worldEvent.DimensionId,
              worldEvent.ZoneId,
              worldEvent.HasLocalBounds,
              worldEvent.LocalBounds,
              worldEvent.Kind,
              worldEvent.ProviderId,
              worldEvent.ProgressFlagId,
              worldEvent.Weight,
              worldEvent.Priority,
              worldEvent.CooldownSeconds,
              enabled);

      worldEvents[eventId] = updated;
      RaiseWorldEventChanged(
          updated,
          DimensionWorldEventChangeKind.EnabledChanged,
          worldEvent.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveWorldEvent(
        string eventId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(eventId))
      {
        result = DimensionOperationResult.Failed("world-event-id-empty", "A world event id is required.");
        return false;
      }

      DimensionWorldEventDefinition worldEvent;
      if (!worldEvents.TryGetValue(eventId, out worldEvent))
      {
        result = DimensionOperationResult.Failed("world-event-not-found", "No world event with that id is registered.");
        return false;
      }

      worldEvents.Remove(eventId);
      RaiseWorldEventChanged(
          worldEvent,
          DimensionWorldEventChangeKind.Removed,
          worldEvent.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
