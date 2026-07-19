using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Portals
{
  public readonly struct DimensionPortalCraftingRecipeDefinition
  {
    public readonly string PortalObjectName;
    public readonly ObjectID CraftingStationObjectID;
    public readonly int Amount;
    public readonly float CraftingTimeOverride;
    public readonly string DisplayName;

    public DimensionPortalCraftingRecipeDefinition(
        string portalObjectName,
        ObjectID craftingStationObjectID,
        int amount,
        float craftingTimeOverride,
        string displayName)
    {
      PortalObjectName = portalObjectName ?? string.Empty;
      CraftingStationObjectID = craftingStationObjectID;
      Amount = amount < 1 ? 1 : amount;
      CraftingTimeOverride = craftingTimeOverride < 0.0f ? 0.0f : craftingTimeOverride;
      DisplayName = string.IsNullOrEmpty(displayName) ? PortalObjectName : displayName;
    }

    public bool IsValid
    {
      get
      {
        return !string.IsNullOrEmpty(PortalObjectName) &&
               CraftingStationObjectID != ObjectID.None;
      }
    }
  }

  public static class DimensionPortalCraftingRegistry
  {
    private static readonly List<DimensionPortalCraftingRecipeDefinition> Recipes =
        new List<DimensionPortalCraftingRecipeDefinition>();

    public static int Count
    {
      get { return Recipes.Count; }
    }

    public static void Clear()
    {
      Recipes.Clear();
    }

    public static bool Register(DimensionPortalCraftingRecipeDefinition definition)
    {
      if (!definition.IsValid)
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        DimensionPortalCraftingRecipeDefinition existing = Recipes[i];
        if (existing.CraftingStationObjectID == definition.CraftingStationObjectID &&
            string.Equals(existing.PortalObjectName, definition.PortalObjectName, StringComparison.Ordinal))
        {
          Recipes[i] = definition;
          return true;
        }
      }

      Recipes.Add(definition);
      return true;
    }

    public static bool TryGetRecipe(
        int index,
        out DimensionPortalCraftingRecipeDefinition definition)
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

    public static bool IsRegisteredPortalObjectName(string objectName)
    {
      if (string.IsNullOrEmpty(objectName))
      {
        return false;
      }

      for (int i = 0; i < Recipes.Count; i++)
      {
        if (string.Equals(Recipes[i].PortalObjectName, objectName, StringComparison.Ordinal))
        {
          return true;
        }
      }

      return false;
    }
  }
}
