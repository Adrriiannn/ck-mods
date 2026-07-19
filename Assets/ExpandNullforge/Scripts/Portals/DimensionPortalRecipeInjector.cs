using PugMod;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Portals
{
  public static class DimensionPortalRecipeInjector
  {
    private static readonly List<PendingCraftingTarget> PendingCraftingTargets =
        new List<PendingCraftingTarget>();

    public static void OnObjectTypeAdded(
        Entity entity,
        GameObject authoringData,
        EntityManager entityManager)
    {
      RefreshRegisteredPortalObjectIds();

      ObjectID objectID = GetObjectID(authoringData);
      if (DimensionPortalCraftingRegistry.HasRecipeForCraftingStation(objectID) &&
          EntityStillExists(entity, entityManager))
      {
        RememberCraftingTarget(entity, entityManager, objectID);
        TryAddRecipes(entity, entityManager, objectID);
      }

      if (IsRegisteredPortalObject(authoringData))
      {
        TryInjectPendingCraftingTargets();
      }
      else if (AnyRegisteredPortalObjectResolved())
      {
        TryInjectPendingCraftingTargets();
      }
    }

    public static void Reset()
    {
      ClearPendingCraftingTargets("reset");
    }

    public static void ClearWorldState(string reason)
    {
      ClearPendingCraftingTargets(reason);
    }

    private static bool TryAddRecipes(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID)
    {
      if (craftingStationObjectID == ObjectID.None)
      {
        return false;
      }

      if (!entityManager.HasBuffer<CanCraftObjectsBuffer>(entity))
      {
        Debug.LogWarning("[ExpandNullforge] Registered portal crafting station has no crafting recipe buffer.");
        return false;
      }

      bool addedAnyRecipe = false;
      DynamicBuffer<CanCraftObjectsBuffer> recipes = entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);
      for (int definitionIndex = 0; definitionIndex < DimensionPortalCraftingRegistry.Count; definitionIndex++)
      {
        DimensionPortalCraftingRecipeDefinition definition;
        if (!DimensionPortalCraftingRegistry.TryGetRecipe(definitionIndex, out definition) ||
            definition.CraftingStationObjectID != craftingStationObjectID)
        {
          continue;
        }

        ObjectID portalObjectID;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.PortalObjectName,
            out portalObjectID))
        {
          continue;
        }

        int recipeIndex = FindRecipe(recipes, portalObjectID);
        bool addedRecipe = false;
        if (recipeIndex < 0)
        {
          recipeIndex = recipes.Length;
          recipes.Add(CreatePortalRecipe(portalObjectID, definition));
          addedRecipe = true;
        }

        bool addedCategory = EnsurePortalCategory(
            entity,
            entityManager,
            craftingStationObjectID,
            portalObjectID,
            recipeIndex);

        if (addedRecipe || addedCategory)
        {
          addedAnyRecipe = true;
          DimensionFrameworkLog.Verbose(
              "[ExpandNullforge] Added " +
              definition.DisplayName +
              " recipe to registered crafting station " +
              (int)craftingStationObjectID +
              ".");
        }
      }

      return addedAnyRecipe;
    }

    private static void RememberCraftingTarget(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID)
    {
      for (int i = 0; i < PendingCraftingTargets.Count; i++)
      {
        PendingCraftingTarget existing = PendingCraftingTargets[i];
        if (existing.Entity == entity)
        {
          PendingCraftingTargets[i] =
              new PendingCraftingTarget(entity, entityManager, craftingStationObjectID);
          return;
        }
      }

      PendingCraftingTargets.Add(
          new PendingCraftingTarget(entity, entityManager, craftingStationObjectID));
    }

    private static void TryInjectPendingCraftingTargets()
    {
      if (!AnyRegisteredPortalObjectResolved())
      {
        return;
      }

      for (int i = PendingCraftingTargets.Count - 1; i >= 0; i--)
      {
        PendingCraftingTarget target = PendingCraftingTargets[i];
        if (!PendingTargetStillExists(target))
        {
          PendingCraftingTargets.RemoveAt(i);
          continue;
        }

        TryAddRecipes(
            target.Entity,
            target.EntityManager,
            target.CraftingStationObjectID);
        if (!HasUnresolvedPortalRecipeForStation(target.CraftingStationObjectID))
        {
          PendingCraftingTargets.RemoveAt(i);
        }
      }
    }

    private static bool EntityStillExists(
        Entity entity,
        EntityManager entityManager)
    {
      try
      {
        return entity != Entity.Null &&
               entityManager.Exists(entity);
      }
      catch (NullReferenceException)
      {
        return false;
      }
      catch (InvalidOperationException)
      {
        return false;
      }
    }

    private static bool PendingTargetStillExists(PendingCraftingTarget target)
    {
      try
      {
        return target.Entity != Entity.Null &&
               target.EntityManager.Exists(target.Entity);
      }
      catch (NullReferenceException)
      {
        DimensionFrameworkLog.Warning(
            "[ExpandNullforge] Dropped stale pending dimension portal recipe target after its conversion world was unloaded.");
        return false;
      }
      catch (InvalidOperationException)
      {
        DimensionFrameworkLog.Warning(
            "[ExpandNullforge] Dropped invalid pending dimension portal recipe target after a world transition.");
        return false;
      }
    }

    private static void ClearPendingCraftingTargets(string reason)
    {
      if (PendingCraftingTargets.Count == 0)
      {
        return;
      }

      PendingCraftingTargets.Clear();
      DimensionFrameworkLog.Verbose(
          "[ExpandNullforge] Cleared pending dimension portal recipe targets (" +
          reason +
          ").");
    }

    private static ObjectID GetObjectID(GameObject authoringData)
    {
      if (authoringData == null)
      {
        return ObjectID.None;
      }

      EntityMonoBehaviourData entityData = authoringData.GetComponent<EntityMonoBehaviourData>();
      if (entityData == null || entityData.objectInfo == null)
      {
        return ObjectID.None;
      }

      return entityData.objectInfo.objectID;
    }

    private static bool IsRegisteredPortalObject(GameObject authoringData)
    {
      if (authoringData == null)
      {
        return false;
      }

      ObjectAuthoring objectAuthoring = authoringData.GetComponent<ObjectAuthoring>();
      return objectAuthoring != null &&
             DimensionPortalCraftingRegistry.IsRegisteredPortalObjectName(objectAuthoring.objectName);
    }

    private static bool AnyRegisteredPortalObjectResolved()
    {
      for (int i = 0; i < DimensionPortalCraftingRegistry.Count; i++)
      {
        DimensionPortalCraftingRecipeDefinition definition;
        if (!DimensionPortalCraftingRegistry.TryGetRecipe(i, out definition))
        {
          continue;
        }

        ObjectID objectID;
        if (DimensionPortalObjectIdCache.TryResolve(
            definition.PortalObjectName,
            out objectID))
        {
          return true;
        }
      }

      return false;
    }

    private static bool HasUnresolvedPortalRecipeForStation(ObjectID craftingStationObjectID)
    {
      for (int i = 0; i < DimensionPortalCraftingRegistry.Count; i++)
      {
        DimensionPortalCraftingRecipeDefinition definition;
        if (!DimensionPortalCraftingRegistry.TryGetRecipe(i, out definition) ||
            definition.CraftingStationObjectID != craftingStationObjectID)
        {
          continue;
        }

        ObjectID objectID;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.PortalObjectName,
            out objectID))
        {
          return true;
        }
      }

      return false;
    }

    private static void RefreshRegisteredPortalObjectIds()
    {
      for (int i = 0; i < DimensionPortalCraftingRegistry.Count; i++)
      {
        DimensionPortalCraftingRecipeDefinition definition;
        if (!DimensionPortalCraftingRegistry.TryGetRecipe(i, out definition))
        {
          continue;
        }

        DimensionPortalObjectIdCache.Refresh(definition.PortalObjectName);
      }
    }

    private static int FindRecipe(
        DynamicBuffer<CanCraftObjectsBuffer> recipes,
        ObjectID objectID)
    {
      for (int i = 0; i < recipes.Length; i++)
      {
        if (recipes[i].objectID == objectID)
        {
          return i;
        }
      }

      return -1;
    }

    private static CanCraftObjectsBuffer CreatePortalRecipe(
        ObjectID portalObjectID,
        DimensionPortalCraftingRecipeDefinition definition)
    {
      return new CanCraftObjectsBuffer
      {
        objectID = portalObjectID,
        amount = definition.Amount,
        craftingTimeOverride = definition.CraftingTimeOverride
      };
    }

    private static bool EnsurePortalCategory(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID,
        ObjectID portalObjectID,
        int portalRecipeIndex)
    {
      if (!entityManager.HasBuffer<IncludedCraftingBuildingsBuffer>(entity))
      {
        DynamicBuffer<IncludedCraftingBuildingsBuffer> fallbackCategories =
            entityManager.AddBuffer<IncludedCraftingBuildingsBuffer>(entity);
        if (portalRecipeIndex > 0)
        {
          fallbackCategories.Add(new IncludedCraftingBuildingsBuffer
          {
            objectID = craftingStationObjectID,
            amountOfCraftingOptions = portalRecipeIndex
          });
        }
      }

      DynamicBuffer<IncludedCraftingBuildingsBuffer> categories =
          entityManager.GetBuffer<IncludedCraftingBuildingsBuffer>(entity);
      int existingPortalCategoryIndex = FindCategory(categories, portalObjectID);
      if (existingPortalCategoryIndex >= 0)
      {
        IncludedCraftingBuildingsBuffer portalCategory =
            categories[existingPortalCategoryIndex];
        if (portalCategory.amountOfCraftingOptions < 1)
        {
          portalCategory.amountOfCraftingOptions = 1;
          categories[existingPortalCategoryIndex] = portalCategory;
          return true;
        }

        return false;
      }

      int coveredRecipeCount = GetCoveredRecipeCount(categories);
      if (portalRecipeIndex < coveredRecipeCount)
      {
        // Another mod or an authored prefab already made this recipe visible in an
        // existing category range. Do not duplicate it into a second UI window.
        return false;
      }

      if (portalRecipeIndex > coveredRecipeCount)
      {
        int gap = portalRecipeIndex - coveredRecipeCount;
        if (categories.Length > 0)
        {
          IncludedCraftingBuildingsBuffer lastCategory =
              categories[categories.Length - 1];
          lastCategory.amountOfCraftingOptions += gap;
          categories[categories.Length - 1] = lastCategory;
        }
        else
        {
          categories.Add(new IncludedCraftingBuildingsBuffer
          {
            objectID = craftingStationObjectID,
            amountOfCraftingOptions = gap
          });
        }
      }

      categories.Add(new IncludedCraftingBuildingsBuffer
      {
        objectID = portalObjectID,
        amountOfCraftingOptions = 1
      });
      return true;
    }

    private static int FindCategory(
        DynamicBuffer<IncludedCraftingBuildingsBuffer> categories,
        ObjectID objectID)
    {
      for (int i = 0; i < categories.Length; i++)
      {
        if (categories[i].objectID == objectID)
        {
          return i;
        }
      }

      return -1;
    }

    private static int GetCoveredRecipeCount(
        DynamicBuffer<IncludedCraftingBuildingsBuffer> categories)
    {
      int count = 0;
      for (int i = 0; i < categories.Length; i++)
      {
        count += Mathf.Max(0, categories[i].amountOfCraftingOptions);
      }

      return count;
    }

    private readonly struct PendingCraftingTarget
    {
      public readonly Entity Entity;
      public readonly EntityManager EntityManager;
      public readonly ObjectID CraftingStationObjectID;

      public PendingCraftingTarget(
          Entity entity,
          EntityManager entityManager,
          ObjectID craftingStationObjectID)
      {
        Entity = entity;
        EntityManager = entityManager;
        CraftingStationObjectID = craftingStationObjectID;
      }
    }
  }
}
