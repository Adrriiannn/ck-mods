using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionProgressFlag> GetProgressFlags(
        string dimensionId,
        string category)
    {
      List<DimensionProgressFlag> result = new List<DimensionProgressFlag>();
      foreach (DimensionProgressFlag flag in progressFlags.Values)
      {
        if (!string.IsNullOrEmpty(dimensionId) &&
            !string.Equals(flag.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          continue;
        }

        if (!string.IsNullOrEmpty(category) &&
            !string.Equals(flag.Category, category, StringComparison.Ordinal))
        {
          continue;
        }

        result.Add(flag);
      }

      result.Sort(CompareProgressFlags);
      return result;
    }

    public bool TryGetProgressFlag(string flagId, out DimensionProgressFlag flag)
    {
      if (string.IsNullOrEmpty(flagId))
      {
        flag = default(DimensionProgressFlag);
        return false;
      }

      return progressFlags.TryGetValue(flagId, out flag);
    }

    public bool IsProgressFlagSet(string flagId)
    {
      DimensionProgressFlag flag;
      return TryGetProgressFlag(flagId, out flag) && flag.Value;
    }

    public bool TrySetProgressFlag(
        string flagId,
        string dimensionId,
        string category,
        bool value,
        string reason,
        out DimensionOperationResult result)
    {
      DimensionProgressFlag requested =
          new DimensionProgressFlag(
              flagId,
              dimensionId,
              category,
              value,
              DateTime.UtcNow.Ticks);

      if (!ValidateProgressFlag(requested, out result))
      {
        return false;
      }

      DimensionProgressFlag previous;
      if (!progressFlags.TryGetValue(requested.FlagId, out previous))
      {
        progressFlags[requested.FlagId] = requested;
        PersistProgressFlagIfWorldRegistryLoaded(requested);
        RaiseProgressFlagChanged(
            previous,
            requested,
            DimensionProgressFlagChangeKind.Created,
            reason ?? string.Empty);
        result = DimensionOperationResult.Ok();
        return true;
      }

      if (ProgressFlagEquals(previous, requested, false))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionProgressFlag updated =
          new DimensionProgressFlag(
              requested.FlagId,
              requested.DimensionId,
              requested.Category,
              requested.Value,
              requested.UpdatedUtcTicks);
      DimensionProgressFlagChangeKind changeKind =
          previous.Value == updated.Value
              ? DimensionProgressFlagChangeKind.MetadataChanged
              : DimensionProgressFlagChangeKind.ValueChanged;

      progressFlags[updated.FlagId] = updated;
      PersistProgressFlagIfWorldRegistryLoaded(updated);
      RaiseProgressFlagChanged(previous, updated, changeKind, reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveProgressFlag(string flagId, out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(flagId))
      {
        result = DimensionOperationResult.Failed("progress-flag-id-empty", "A progress flag id is required.");
        return false;
      }

      DimensionProgressFlag flag;
      if (!progressFlags.TryGetValue(flagId, out flag))
      {
        result = DimensionOperationResult.Failed("progress-flag-not-found", "No progress flag with that id is registered.");
        return false;
      }

      progressFlags.Remove(flagId);
      RemovePersistedProgressFlagIfWorldRegistryLoaded(flagId);
      RaiseProgressFlagChanged(
          flag,
          default(DimensionProgressFlag),
          DimensionProgressFlagChangeKind.Removed,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
