using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Persistence;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    private static int CompareGenerationTables(
        DimensionGenerationTableDefinition left,
        DimensionGenerationTableDefinition right)
    {
      int byPriority = right.Priority.CompareTo(left.Priority);
      if (byPriority != 0)
      {
        return byPriority;
      }

      int byKind = left.Kind.CompareTo(right.Kind);
      if (byKind != 0)
      {
        return byKind;
      }

      return string.Compare(left.TableId, right.TableId, StringComparison.Ordinal);
    }

    private static int CompareGenerationTableEntries(
        DimensionGenerationTableEntryDefinition left,
        DimensionGenerationTableEntryDefinition right)
    {
      int byPriority = right.Priority.CompareTo(left.Priority);
      if (byPriority != 0)
      {
        return byPriority;
      }

      int byWeight = right.Weight.CompareTo(left.Weight);
      if (byWeight != 0)
      {
        return byWeight;
      }

      return string.Compare(left.EntryId, right.EntryId, StringComparison.Ordinal);
    }

    private static int CompareResolvedGenerationTables(
        DimensionResolvedGenerationTable left,
        DimensionResolvedGenerationTable right)
    {
      return CompareGenerationTables(left.Table, right.Table);
    }

    private bool GenerationTableMatchesQuery(
        DimensionGenerationTableDefinition table,
        DimensionGenerationTableQuery query)
    {
      if (query.EnabledOnly && !table.Enabled)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.DimensionId) &&
          !string.Equals(table.DimensionId, query.DimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(query.BiomeId) &&
          !string.Equals(table.BiomeId, query.BiomeId, StringComparison.Ordinal))
      {
        return false;
      }

      if (query.Kind != DimensionGenerationTableKind.Any &&
          table.Kind != query.Kind)
      {
        return false;
      }

      return true;
    }

    private static bool GenerationTableAppliesToResolution(
        DimensionGenerationTableDefinition table,
        string dimensionId,
        string biomeId,
        DimensionGenerationTableKind kind,
        bool includeDisabled)
    {
      if (!includeDisabled && !table.Enabled)
      {
        return false;
      }

      if (kind != DimensionGenerationTableKind.Any &&
          table.Kind != kind)
      {
        return false;
      }

      if (!string.IsNullOrEmpty(table.DimensionId) &&
          !string.Equals(table.DimensionId, dimensionId, StringComparison.Ordinal))
      {
        return false;
      }

      if (!string.IsNullOrEmpty(table.BiomeId) &&
          !string.Equals(table.BiomeId, biomeId, StringComparison.Ordinal))
      {
        return false;
      }

      return true;
    }

    private static int SumGenerationTableEntryWeights(
        IReadOnlyList<DimensionGenerationTableEntryDefinition> entries)
    {
      long totalWeight = 0;
      for (int i = 0; i < entries.Count; i++)
      {
        totalWeight += entries[i].Weight;
        if (totalWeight >= int.MaxValue)
        {
          return int.MaxValue;
        }
      }

      return (int)totalWeight;
    }

    private static int SelectGenerationEntryIndex(
        IReadOnlyList<DimensionGenerationTableEntryDefinition> entries,
        int roll)
    {
      int remaining = roll;
      for (int i = 0; i < entries.Count; i++)
      {
        remaining -= entries[i].Weight;
        if (remaining < 0)
        {
          return i;
        }
      }

      return entries.Count - 1;
    }

    private static int ResolveGenerationEntrySelectedCount(
        DimensionGenerationTableEntryDefinition entry,
        uint seed,
        string tableId,
        string salt,
        int pickIndex)
    {
      if (entry.MaxCount <= entry.MinCount)
      {
        return entry.MinCount;
      }

      int span = entry.MaxCount - entry.MinCount + 1;
      int offset = StableGenerationRange(
          seed,
          tableId,
          salt + ":count:" + entry.EntryId,
          pickIndex,
          span);
      return entry.MinCount + offset;
    }

    private static int StableGenerationRange(
        uint seed,
        string tableId,
        string salt,
        int pickIndex,
        int maxExclusive)
    {
      if (maxExclusive <= 1)
      {
        return 0;
      }

      uint hash = StableHashOffset;
      hash = MixGenerationHash(hash, seed);
      hash = MixGenerationHash(hash, (uint)pickIndex);
      hash = MixGenerationHash(hash, tableId);
      hash = MixGenerationHash(hash, salt);
      return (int)(hash % (uint)maxExclusive);
    }

    private static uint ComputeGenerationSeed(DimensionGenerationSeedRequest request)
    {
      string worldKey = DimensionWorldRegistry.WorldKey;
      if (string.IsNullOrEmpty(worldKey))
      {
        DimensionWorldRegistry.EnsureLoadedForCurrentWorld();
        worldKey = DimensionWorldRegistry.WorldKey;
      }

      uint hash = StableHashOffset;
      hash = MixGenerationHash(hash, request.ExternalSeed);
      hash = MixGenerationHash(hash, worldKey);
      hash = MixGenerationHash(hash, request.DimensionId);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.Min.x);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.Min.y);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.MaxExclusive.x);
      hash = MixGenerationHash(hash, (uint)request.LocalBounds.MaxExclusive.y);
      hash = MixGenerationHash(hash, request.ProviderId);
      hash = MixGenerationHash(hash, request.PassId);
      hash = MixGenerationHash(hash, request.Purpose);
      hash = MixGenerationHash(hash, request.Salt);
      return hash == 0u ? StableHashPrime : hash;
    }

    private static uint MixGenerationHash(uint hash, uint value)
    {
      unchecked
      {
        hash ^= value & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 8) & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 16) & 0xffu;
        hash *= StableHashPrime;
        hash ^= (value >> 24) & 0xffu;
        hash *= StableHashPrime;
        return hash;
      }
    }

    private static uint MixGenerationHash(uint hash, string value)
    {
      if (string.IsNullOrEmpty(value))
      {
        return MixGenerationHash(hash, 0u);
      }

      unchecked
      {
        for (int i = 0; i < value.Length; i++)
        {
          hash ^= value[i];
          hash *= StableHashPrime;
        }

        return hash;
      }
    }

    private bool ValidateGenerationTable(
        DimensionGenerationTableDefinition table,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(table.TableId))
      {
        result = DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
        return false;
      }

      if (!IsValidGenerationTableKind(table.Kind))
      {
        result = DimensionOperationResult.Failed("generation-table-kind-invalid", "A valid generation table kind is required.");
        return false;
      }

      if (!string.IsNullOrEmpty(table.DimensionId) &&
          !definitions.ContainsKey(table.DimensionId))
      {
        result = DimensionOperationResult.Failed("generation-table-dimension-not-found", "No dimension with that id is registered.");
        return false;
      }

      if (!string.IsNullOrEmpty(table.BiomeId) &&
          !biomes.ContainsKey(table.BiomeId))
      {
        result = DimensionOperationResult.Failed("generation-table-biome-not-found", "No biome with that id is registered.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool ValidateGenerationTableEntry(
        DimensionGenerationTableEntryDefinition entry,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(entry.EntryId))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-id-empty", "A generation table entry id is required.");
        return false;
      }

      if (string.IsNullOrEmpty(entry.TableId))
      {
        result = DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
        return false;
      }

      if (!generationTables.ContainsKey(entry.TableId))
      {
        result = DimensionOperationResult.Failed("generation-table-not-found", "No generation table with that id is registered.");
        return false;
      }

      if (string.IsNullOrEmpty(entry.SubjectId))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-subject-id-empty", "A generation table entry subject id is required.");
        return false;
      }

      if (entry.Weight <= 0)
      {
        result = DimensionOperationResult.Failed("generation-table-entry-weight-invalid", "A generation table entry weight must be greater than zero.");
        return false;
      }

      if (entry.MinCount < 0 || entry.MaxCount < entry.MinCount)
      {
        result = DimensionOperationResult.Failed("generation-table-entry-count-invalid", "A generation table entry count range is invalid.");
        return false;
      }

      result = DimensionOperationResult.Ok();
      return true;
    }

    private bool IsValidGenerationTableKind(DimensionGenerationTableKind kind)
    {
      return kind == DimensionGenerationTableKind.Terrain ||
             kind == DimensionGenerationTableKind.Object ||
             kind == DimensionGenerationTableKind.Resource ||
             kind == DimensionGenerationTableKind.Spawn ||
             kind == DimensionGenerationTableKind.WorldEvent ||
             kind == DimensionGenerationTableKind.Scene ||
             kind == DimensionGenerationTableKind.Loot ||
             kind == DimensionGenerationTableKind.Fishing ||
             kind == DimensionGenerationTableKind.Farming ||
             kind == DimensionGenerationTableKind.Hazard ||
             kind == DimensionGenerationTableKind.Custom;
    }

    private static bool GenerationTableEquals(
        DimensionGenerationTableDefinition a,
        DimensionGenerationTableDefinition b)
    {
      return string.Equals(a.TableId, b.TableId, StringComparison.Ordinal) &&
             string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
             string.Equals(a.DimensionId, b.DimensionId, StringComparison.Ordinal) &&
             string.Equals(a.BiomeId, b.BiomeId, StringComparison.Ordinal) &&
             a.Kind == b.Kind &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled &&
             string.Equals(a.Notes, b.Notes, StringComparison.Ordinal);
    }

    private static bool GenerationTableEntryEquals(
        DimensionGenerationTableEntryDefinition a,
        DimensionGenerationTableEntryDefinition b)
    {
      return string.Equals(a.EntryId, b.EntryId, StringComparison.Ordinal) &&
             string.Equals(a.TableId, b.TableId, StringComparison.Ordinal) &&
             string.Equals(a.SubjectId, b.SubjectId, StringComparison.Ordinal) &&
             string.Equals(a.SubjectKind, b.SubjectKind, StringComparison.Ordinal) &&
             a.Weight == b.Weight &&
             a.MinCount == b.MinCount &&
             a.MaxCount == b.MaxCount &&
             a.Priority == b.Priority &&
             a.Enabled == b.Enabled &&
             string.Equals(a.Notes, b.Notes, StringComparison.Ordinal);
    }

    private void RemoveGenerationTablesForDimension(string dimensionId)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionGenerationTableDefinition> pair in generationTables)
      {
        if (string.Equals(pair.Value.DimensionId, dimensionId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionGenerationTableDefinition table = generationTables[keysToRemove[i]];
        generationTables.Remove(keysToRemove[i]);
        RemoveGenerationTableEntriesForTable(table.TableId, DimensionGenerationTableChangeKind.ParentTableRemoved, "parent table removed");
        RaiseGenerationTableChanged(
            table,
            DimensionGenerationTableChangeKind.DimensionRemoved,
            table.Enabled,
            false,
            "dimension removed");
      }
    }

    private void RemoveGenerationTableEntriesForTable(
        string tableId,
        DimensionGenerationTableChangeKind changeKind,
        string reason)
    {
      List<string> keysToRemove = new List<string>();
      foreach (KeyValuePair<string, DimensionGenerationTableEntryDefinition> pair in generationTableEntries)
      {
        if (string.Equals(pair.Value.TableId, tableId, StringComparison.Ordinal))
        {
          keysToRemove.Add(pair.Key);
        }
      }

      for (int i = 0; i < keysToRemove.Count; i++)
      {
        DimensionGenerationTableEntryDefinition entry = generationTableEntries[keysToRemove[i]];
        generationTableEntries.Remove(keysToRemove[i]);
        RaiseGenerationTableEntryChanged(
            entry,
            changeKind,
            entry.Enabled,
            false,
            reason ?? string.Empty);
      }
    }

    private DimensionContentPackReadinessResult EvaluateContentPackReadinessInternal(
        DimensionContentPackDefinition contentPack)
    {
      List<string> missingDependencyIds = new List<string>();
      if (contentPack.DependencyIds != null)
      {
        for (int i = 0; i < contentPack.DependencyIds.Count; i++)
        {
          string dependencyId = contentPack.DependencyIds[i];
          DimensionContentPackDefinition dependency;
          if (!contentPacks.TryGetValue(dependencyId, out dependency) ||
              !dependency.Enabled)
          {
            missingDependencyIds.Add(dependencyId);
          }
        }
      }

      missingDependencyIds.Sort(StringComparer.Ordinal);

      bool apiCompatible = contentPack.MinimumApiVersion <= DimensionApi.CurrentApiVersion;
      if (!contentPack.Enabled)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            false,
            apiCompatible,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            "content-pack-disabled",
            "The content pack is disabled.");
      }

      if (!apiCompatible)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            contentPack.Enabled,
            false,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            "content-pack-api-too-new",
            "The content pack requires a newer Dimension API version.");
      }

      if (missingDependencyIds.Count > 0)
      {
        return new DimensionContentPackReadinessResult(
            contentPack.ContentPackId,
            true,
            false,
            contentPack.Enabled,
            true,
            contentPack.MinimumApiVersion,
            DimensionApi.CurrentApiVersion,
            missingDependencyIds,
            "content-pack-missing-dependencies",
            "The content pack has missing or disabled dependencies.");
      }

      return new DimensionContentPackReadinessResult(
          contentPack.ContentPackId,
          true,
          true,
          contentPack.Enabled,
          true,
          contentPack.MinimumApiVersion,
          DimensionApi.CurrentApiVersion,
          missingDependencyIds,
          string.Empty,
          string.Empty);
    }
  }
}
