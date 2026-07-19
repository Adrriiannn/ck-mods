using System;
using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Foundation
{
  public sealed partial class NullforgeDimensionService
  {
    public IReadOnlyList<DimensionGenerationTableDefinition> GetGenerationTables(
        DimensionGenerationTableQuery query)
    {
      List<DimensionGenerationTableDefinition> result = new List<DimensionGenerationTableDefinition>();
      foreach (DimensionGenerationTableDefinition table in generationTables.Values)
      {
        if (!GenerationTableMatchesQuery(table, query))
        {
          continue;
        }

        result.Add(table);
      }

      result.Sort(CompareGenerationTables);
      return result;
    }

    public DimensionGenerationTableResolutionResult ResolveGenerationTables(
        DimensionGenerationTableResolutionRequest request)
    {
      if (string.IsNullOrEmpty(request.DimensionId))
      {
        return new DimensionGenerationTableResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            false,
            default(DimensionBiomeDefinition),
            request.Kind,
            new List<DimensionResolvedGenerationTable>(),
            0,
            0,
            0,
            "dimension-id-empty",
            "A dimension id is required.");
      }

      if (request.Kind != DimensionGenerationTableKind.Any &&
          !IsValidGenerationTableKind(request.Kind))
      {
        return new DimensionGenerationTableResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            false,
            default(DimensionBiomeDefinition),
            request.Kind,
            new List<DimensionResolvedGenerationTable>(),
            0,
            0,
            0,
            "generation-table-kind-invalid",
            "A valid generation table kind is required.");
      }

      DimensionDefinition dimension;
      if (!definitions.TryGetValue(request.DimensionId, out dimension))
      {
        return new DimensionGenerationTableResolutionResult(
            false,
            request.DimensionId,
            request.LocalPosition,
            false,
            default(DimensionZoneInfo),
            false,
            default(DimensionBiomeDefinition),
            request.Kind,
            new List<DimensionResolvedGenerationTable>(),
            0,
            0,
            0,
            "dimension-not-found",
            "No dimension with that id is registered.");
      }

      DimensionZoneInfo resolvedZone = default(DimensionZoneInfo);
      bool hasZone = false;
      DimensionBiomeDefinition resolvedBiome = default(DimensionBiomeDefinition);
      bool hasBiome = false;
      string resolvedBiomeId = request.BiomeId;

      if (request.ResolveBiomeFromPosition)
      {
        if (!dimension.ContainsLocal(request.LocalPosition))
        {
          return new DimensionGenerationTableResolutionResult(
              false,
              request.DimensionId,
              request.LocalPosition,
              false,
              default(DimensionZoneInfo),
              false,
              default(DimensionBiomeDefinition),
              request.Kind,
              new List<DimensionResolvedGenerationTable>(),
              0,
              0,
              0,
              "dimension-position-not-found",
              "No dimension contains that local position.");
        }

        if (TryGetZoneAtLocal(request.DimensionId, request.LocalPosition, out resolvedZone))
        {
          hasZone = true;
          if (TryResolveBiomeForZone(resolvedZone, out resolvedBiome))
          {
            hasBiome = true;
            resolvedBiomeId = resolvedBiome.BiomeId;
          }
        }

        if (!hasBiome && TryResolveFallbackBiomeForDimension(request.DimensionId, out resolvedBiome))
        {
          hasBiome = true;
          resolvedBiomeId = resolvedBiome.BiomeId;
        }
      }
      else if (!string.IsNullOrEmpty(request.BiomeId))
      {
        if (!biomes.TryGetValue(request.BiomeId, out resolvedBiome))
        {
          return new DimensionGenerationTableResolutionResult(
              false,
              request.DimensionId,
              request.LocalPosition,
              false,
              default(DimensionZoneInfo),
              false,
              default(DimensionBiomeDefinition),
              request.Kind,
              new List<DimensionResolvedGenerationTable>(),
              0,
              0,
              0,
              "biome-not-found",
              "No biome with that id is registered.");
        }

        if (!request.IncludeDisabled && !resolvedBiome.Enabled)
        {
          return new DimensionGenerationTableResolutionResult(
              false,
              request.DimensionId,
              request.LocalPosition,
              false,
              default(DimensionZoneInfo),
              false,
              default(DimensionBiomeDefinition),
              request.Kind,
              new List<DimensionResolvedGenerationTable>(),
              0,
              0,
              0,
              "biome-disabled",
              "The requested biome is disabled.");
        }

        hasBiome = true;
        resolvedBiomeId = resolvedBiome.BiomeId;
      }

      List<DimensionResolvedGenerationTable> resolvedTables =
          new List<DimensionResolvedGenerationTable>();
      int totalEntryCount = 0;
      int totalWeight = 0;
      foreach (DimensionGenerationTableDefinition table in generationTables.Values)
      {
        if (!GenerationTableAppliesToResolution(
                table,
                request.DimensionId,
                resolvedBiomeId,
                request.Kind,
                request.IncludeDisabled))
        {
          continue;
        }

        IReadOnlyList<DimensionGenerationTableEntryDefinition> entries =
            GetGenerationTableEntries(table.TableId, !request.IncludeDisabled);
        int tableWeight = SumGenerationTableEntryWeights(entries);
        totalEntryCount += entries.Count;
        totalWeight += tableWeight;
        resolvedTables.Add(new DimensionResolvedGenerationTable(table, entries, tableWeight));
      }

      resolvedTables.Sort(CompareResolvedGenerationTables);
      return new DimensionGenerationTableResolutionResult(
          true,
          request.DimensionId,
          request.LocalPosition,
          hasZone,
          resolvedZone,
          hasBiome,
          resolvedBiome,
          request.Kind,
          resolvedTables,
          resolvedTables.Count,
          totalEntryCount,
          totalWeight,
          string.Empty,
          string.Empty);
    }

    public DimensionGenerationTableSelectionResult SelectGenerationTableEntries(
        DimensionGenerationTableSelectionRequest request)
    {
      DimensionGenerationTableResolutionResult resolution =
          ResolveGenerationTables(request.ResolutionRequest);
      if (!resolution.Success)
      {
        return new DimensionGenerationTableSelectionResult(
            false,
            resolution,
            new List<DimensionGenerationTableSelection>(),
            0,
            resolution.Code,
            resolution.Message);
      }

      int picksPerTable = request.PicksPerTable <= 0 ? 1 : request.PicksPerTable;
      List<DimensionGenerationTableSelection> selections =
          new List<DimensionGenerationTableSelection>();

      for (int tableIndex = 0; tableIndex < resolution.Tables.Count; tableIndex++)
      {
        DimensionResolvedGenerationTable resolvedTable = resolution.Tables[tableIndex];
        if (resolvedTable.TotalWeight <= 0 || resolvedTable.Entries.Count == 0)
        {
          continue;
        }

        List<DimensionGenerationTableEntryDefinition> candidates =
            new List<DimensionGenerationTableEntryDefinition>(resolvedTable.Entries.Count);
        for (int entryIndex = 0; entryIndex < resolvedTable.Entries.Count; entryIndex++)
        {
          candidates.Add(resolvedTable.Entries[entryIndex]);
        }

        int currentTotalWeight = resolvedTable.TotalWeight;
        for (int pickIndex = 0;
             pickIndex < picksPerTable && candidates.Count > 0 && currentTotalWeight > 0;
             pickIndex++)
        {
          int roll = StableGenerationRange(
              request.Seed,
              resolvedTable.Table.TableId,
              request.Salt,
              pickIndex,
              currentTotalWeight);
          int selectedIndex = SelectGenerationEntryIndex(candidates, roll);
          DimensionGenerationTableEntryDefinition selectedEntry = candidates[selectedIndex];
          int selectedCount = ResolveGenerationEntrySelectedCount(
              selectedEntry,
              request.Seed,
              resolvedTable.Table.TableId,
              request.Salt,
              pickIndex);

          selections.Add(
              new DimensionGenerationTableSelection(
                  resolvedTable.Table,
                  selectedEntry,
                  selectedCount,
                  roll,
                  currentTotalWeight,
                  pickIndex));

          if (!request.AllowDuplicateEntries)
          {
            currentTotalWeight -= selectedEntry.Weight;
            candidates.RemoveAt(selectedIndex);
          }
        }
      }

      return new DimensionGenerationTableSelectionResult(
          true,
          resolution,
          selections,
          selections.Count,
          string.Empty,
          string.Empty);
    }

    public uint ResolveGenerationSeed(DimensionGenerationSeedRequest request)
    {
      return ComputeGenerationSeed(request);
    }

    public int ResolveGenerationRange(
        DimensionGenerationSeedRequest request,
        int maxExclusive)
    {
      if (maxExclusive <= 1)
      {
        return 0;
      }

      return (int)(ResolveGenerationSeed(request) % (uint)maxExclusive);
    }

    public float ResolveGenerationUnitFloat(DimensionGenerationSeedRequest request)
    {
      uint seed = ResolveGenerationSeed(request);
      return (seed & 0x00ffffffu) / 16777216.0f;
    }

    public bool TryGetGenerationTable(
        string tableId,
        out DimensionGenerationTableDefinition table)
    {
      if (string.IsNullOrEmpty(tableId))
      {
        table = default(DimensionGenerationTableDefinition);
        return false;
      }

      return generationTables.TryGetValue(tableId, out table);
    }

    public bool TryRegisterGenerationTable(
        DimensionGenerationTableDefinition table,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationTable(table, out result))
      {
        return false;
      }

      if (generationTables.ContainsKey(table.TableId))
      {
        result = DimensionOperationResult.Failed("generation-table-already-registered", "A generation table with that id is already registered.");
        return false;
      }

      generationTables[table.TableId] = table;
      RaiseGenerationTableChanged(
          table,
          DimensionGenerationTableChangeKind.Registered,
          false,
          table.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateGenerationTable(
        DimensionGenerationTableDefinition table,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationTable(table, out result))
      {
        return false;
      }

      DimensionGenerationTableDefinition previous;
      if (!generationTables.TryGetValue(table.TableId, out previous))
      {
        result = DimensionOperationResult.Failed("generation-table-not-found", "No generation table with that id is registered.");
        return false;
      }

      if (GenerationTableEquals(previous, table))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      generationTables[table.TableId] = table;
      RaiseGenerationTableChanged(
          table,
          previous.Enabled == table.Enabled
              ? DimensionGenerationTableChangeKind.Updated
              : DimensionGenerationTableChangeKind.EnabledChanged,
          previous.Enabled,
          table.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetGenerationTableEnabled(
        string tableId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(tableId))
      {
        result = DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
        return false;
      }

      DimensionGenerationTableDefinition table;
      if (!generationTables.TryGetValue(tableId, out table))
      {
        result = DimensionOperationResult.Failed("generation-table-not-found", "No generation table with that id is registered.");
        return false;
      }

      if (table.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionGenerationTableDefinition updated =
          new DimensionGenerationTableDefinition(
              table.TableId,
              table.DisplayName,
              table.DimensionId,
              table.BiomeId,
              table.Kind,
              table.Priority,
              enabled,
              table.Notes);

      generationTables[tableId] = updated;
      RaiseGenerationTableChanged(
          updated,
          DimensionGenerationTableChangeKind.EnabledChanged,
          table.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationTable(
        string tableId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(tableId))
      {
        result = DimensionOperationResult.Failed("generation-table-id-empty", "A generation table id is required.");
        return false;
      }

      DimensionGenerationTableDefinition table;
      if (!generationTables.TryGetValue(tableId, out table))
      {
        result = DimensionOperationResult.Failed("generation-table-not-found", "No generation table with that id is registered.");
        return false;
      }

      generationTables.Remove(tableId);
      RemoveGenerationTableEntriesForTable(tableId, DimensionGenerationTableChangeKind.ParentTableRemoved, "parent table removed");
      RaiseGenerationTableChanged(
          table,
          DimensionGenerationTableChangeKind.Removed,
          table.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public IReadOnlyList<DimensionGenerationTableEntryDefinition> GetGenerationTableEntries(
        string tableId,
        bool enabledOnly)
    {
      List<DimensionGenerationTableEntryDefinition> result =
          new List<DimensionGenerationTableEntryDefinition>();
      foreach (DimensionGenerationTableEntryDefinition entry in generationTableEntries.Values)
      {
        if (!string.Equals(entry.TableId, tableId, StringComparison.Ordinal))
        {
          continue;
        }

        if (enabledOnly && !entry.Enabled)
        {
          continue;
        }

        result.Add(entry);
      }

      result.Sort(CompareGenerationTableEntries);
      return result;
    }

    public bool TryGetGenerationTableEntry(
        string entryId,
        out DimensionGenerationTableEntryDefinition entry)
    {
      if (string.IsNullOrEmpty(entryId))
      {
        entry = default(DimensionGenerationTableEntryDefinition);
        return false;
      }

      return generationTableEntries.TryGetValue(entryId, out entry);
    }

    public bool TryRegisterGenerationTableEntry(
        DimensionGenerationTableEntryDefinition entry,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationTableEntry(entry, out result))
      {
        return false;
      }

      if (generationTableEntries.ContainsKey(entry.EntryId))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-already-registered", "A generation table entry with that id is already registered.");
        return false;
      }

      generationTableEntries[entry.EntryId] = entry;
      RaiseGenerationTableEntryChanged(
          entry,
          DimensionGenerationTableChangeKind.Registered,
          false,
          entry.Enabled,
          "registered");
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryUpdateGenerationTableEntry(
        DimensionGenerationTableEntryDefinition entry,
        string reason,
        out DimensionOperationResult result)
    {
      if (!ValidateGenerationTableEntry(entry, out result))
      {
        return false;
      }

      DimensionGenerationTableEntryDefinition previous;
      if (!generationTableEntries.TryGetValue(entry.EntryId, out previous))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-not-found", "No generation table entry with that id is registered.");
        return false;
      }

      if (GenerationTableEntryEquals(previous, entry))
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      generationTableEntries[entry.EntryId] = entry;
      RaiseGenerationTableEntryChanged(
          entry,
          previous.Enabled == entry.Enabled
              ? DimensionGenerationTableChangeKind.Updated
              : DimensionGenerationTableChangeKind.EnabledChanged,
          previous.Enabled,
          entry.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TrySetGenerationTableEntryEnabled(
        string entryId,
        bool enabled,
        string reason,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(entryId))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-id-empty", "A generation table entry id is required.");
        return false;
      }

      DimensionGenerationTableEntryDefinition entry;
      if (!generationTableEntries.TryGetValue(entryId, out entry))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-not-found", "No generation table entry with that id is registered.");
        return false;
      }

      if (entry.Enabled == enabled)
      {
        result = DimensionOperationResult.Ok();
        return true;
      }

      DimensionGenerationTableEntryDefinition updated =
          new DimensionGenerationTableEntryDefinition(
              entry.EntryId,
              entry.TableId,
              entry.SubjectId,
              entry.SubjectKind,
              entry.Weight,
              entry.MinCount,
              entry.MaxCount,
              entry.Priority,
              enabled,
              entry.Notes);

      generationTableEntries[entryId] = updated;
      RaiseGenerationTableEntryChanged(
          updated,
          DimensionGenerationTableChangeKind.EnabledChanged,
          entry.Enabled,
          updated.Enabled,
          reason ?? string.Empty);
      result = DimensionOperationResult.Ok();
      return true;
    }

    public bool TryRemoveGenerationTableEntry(
        string entryId,
        out DimensionOperationResult result)
    {
      if (string.IsNullOrEmpty(entryId))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-id-empty", "A generation table entry id is required.");
        return false;
      }

      DimensionGenerationTableEntryDefinition entry;
      if (!generationTableEntries.TryGetValue(entryId, out entry))
      {
        result = DimensionOperationResult.Failed("generation-table-entry-not-found", "No generation table entry with that id is registered.");
        return false;
      }

      generationTableEntries.Remove(entryId);
      RaiseGenerationTableEntryChanged(
          entry,
          DimensionGenerationTableChangeKind.Removed,
          entry.Enabled,
          false,
          "removed");
      result = DimensionOperationResult.Ok();
      return true;
    }
  }
}
