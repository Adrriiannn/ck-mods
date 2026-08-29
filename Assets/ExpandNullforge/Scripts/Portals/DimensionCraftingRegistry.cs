using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Portals
{
  public readonly struct DimensionCraftingRecipeDefinition
  {
    public readonly string CraftedObjectName;
    public readonly ObjectID CraftingStationObjectID;
    public readonly int Amount;
    public readonly float CraftingTimeOverride;
    public readonly string DisplayName;

    /// <summary>
    /// The station this recipe is still waiting to find, by name, or empty once it is known.
    /// </summary>
    /// <remarks>
    /// A crafting station can belong to another mod, and another mod's objects have no number
    /// until the game hands them one during world conversion — long after this recipe is
    /// registered. So a recipe aimed at a station the framework cannot name as one of the game's
    /// own keeps the name instead of a number, and <see cref="DimensionCraftingRegistry.TryResolveStationNames"/>
    /// swaps it for the real one the moment that station exists.
    /// </remarks>
    public readonly string PendingCraftingStationObjectName;

    public DimensionCraftingRecipeDefinition(
        string craftedObjectName,
        ObjectID craftingStationObjectID,
        int amount,
        float craftingTimeOverride,
        string displayName)
        : this(
            craftedObjectName,
            craftingStationObjectID,
            amount,
            craftingTimeOverride,
            displayName,
            string.Empty)
    {
    }

    public DimensionCraftingRecipeDefinition(
        string craftedObjectName,
        string craftingStationObjectName,
        int amount,
        float craftingTimeOverride,
        string displayName)
        : this(
            craftedObjectName,
            ObjectID.None,
            amount,
            craftingTimeOverride,
            displayName,
            craftingStationObjectName)
    {
    }

    private DimensionCraftingRecipeDefinition(
        string craftedObjectName,
        ObjectID craftingStationObjectID,
        int amount,
        float craftingTimeOverride,
        string displayName,
        string pendingCraftingStationObjectName)
    {
      CraftedObjectName = craftedObjectName ?? string.Empty;
      CraftingStationObjectID = craftingStationObjectID;
      Amount = amount < 1 ? 1 : amount;
      CraftingTimeOverride = craftingTimeOverride < 0.0f ? 0.0f : craftingTimeOverride;
      DisplayName = string.IsNullOrEmpty(displayName) ? CraftedObjectName : displayName;
      PendingCraftingStationObjectName = pendingCraftingStationObjectName ?? string.Empty;
    }

    /// <summary>The same recipe, with the station it was waiting for finally resolved.</summary>
    public DimensionCraftingRecipeDefinition WithResolvedStation(ObjectID craftingStationObjectID)
    {
      return new DimensionCraftingRecipeDefinition(
          CraftedObjectName,
          craftingStationObjectID,
          Amount,
          CraftingTimeOverride,
          DisplayName,
          string.Empty);
    }

    /// <summary>True while this recipe knows its station only by name.</summary>
    public bool IsWaitingForCraftingStation
    {
      get
      {
        return CraftingStationObjectID == ObjectID.None &&
               !string.IsNullOrEmpty(PendingCraftingStationObjectName);
      }
    }

    public bool IsValid
    {
      get
      {
        return !string.IsNullOrEmpty(CraftedObjectName) &&
               (CraftingStationObjectID != ObjectID.None ||
                !string.IsNullOrEmpty(PendingCraftingStationObjectName));
      }
    }
  }

  public static class DimensionCraftingRegistry
  {
    private static readonly List<DimensionCraftingRecipeDefinition> Recipes =
        new List<DimensionCraftingRecipeDefinition>();

    public static int Count
    {
      get { return Recipes.Count; }
    }

    public static void Clear()
    {
      Recipes.Clear();
    }

    public static bool Register(DimensionCraftingRecipeDefinition definition)
    {
      if (!definition.IsValid)
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        DimensionCraftingRecipeDefinition existing = Recipes[i];
        // The station is part of the identity, and while a station is still only a name that
        // name is the identity — otherwise two recipes crafting the same object at two different
        // modded stations would collapse into one and only the last would ever appear.
        if (existing.CraftingStationObjectID == definition.CraftingStationObjectID &&
            string.Equals(
                existing.PendingCraftingStationObjectName,
                definition.PendingCraftingStationObjectName,
                StringComparison.Ordinal) &&
            string.Equals(existing.CraftedObjectName, definition.CraftedObjectName, StringComparison.Ordinal))
        {
          Recipes[i] = definition;
          return true;
        }
      }

      Recipes.Add(definition);
      return true;
    }

    /// <summary>
    /// Gives every recipe still waiting on a named crafting station the number of that station,
    /// if the game knows it yet. Returns true when at least one recipe found its station.
    /// </summary>
    /// <remarks>
    /// Called on every object type the game adds, because that is the only moment a mod's object
    /// gains a number. A name that never resolves leaves its recipe parked rather than dropping
    /// it onto some other bench: a recipe appearing at the wrong station is far harder to
    /// diagnose than one that has not appeared yet, and the object-id cache logs what it is
    /// still waiting for.
    /// </remarks>
    public static bool TryResolveStationNames()
    {
      bool resolvedAny = false;
      for (int i = 0; i < Recipes.Count; i++)
      {
        DimensionCraftingRecipeDefinition definition = Recipes[i];
        if (!definition.IsWaitingForCraftingStation)
        {
          continue;
        }

        ObjectID station;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.PendingCraftingStationObjectName,
            out station))
        {
          continue;
        }

        Recipes[i] = definition.WithResolvedStation(station);
        resolvedAny = true;
      }

      return resolvedAny;
    }

    public static bool TryGetRecipe(
        int index,
        out DimensionCraftingRecipeDefinition definition)
    {
      if (index < 0 || index >= Recipes.Count)
      {
        definition = default;
        return false;
      }

      definition = Recipes[index];
      return true;
    }

    public static bool HasRecipeForCraftingStation(ObjectID craftingStationObjectID)
    {
      if (craftingStationObjectID == ObjectID.None)
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        if (Recipes[i].CraftingStationObjectID == craftingStationObjectID)
        {
          return true;
        }
      }

      return false;
    }

    /// <summary>True while some recipe is still waiting for the station with this name.</summary>
    public static bool IsWaitingForCraftingStationName(string objectName)
    {
      if (string.IsNullOrEmpty(objectName))
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        if (Recipes[i].IsWaitingForCraftingStation &&
            string.Equals(
                Recipes[i].PendingCraftingStationObjectName,
                objectName,
                StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }

    public static bool IsRegisteredCraftedObjectName(string objectName)
    {
      if (string.IsNullOrEmpty(objectName))
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        if (string.Equals(Recipes[i].CraftedObjectName, objectName, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }
  }
}
