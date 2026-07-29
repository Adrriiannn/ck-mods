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

    private static readonly List<ResolvedRecipe> ResolvedRecipes = new List<ResolvedRecipe>();

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

      DynamicBuffer<CanCraftObjectsBuffer> recipes = entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);

      // Collect everything this station should show in one pass. The framework's recipes belong
      // together in the crafting window, so they have to be placed as a block rather than one at a
      // time — objects resolve at different moments during conversion, and adding them piecemeal is
      // what used to scatter them across separate pages.
      ResolvedRecipes.Clear();
      for (int definitionIndex = 0; definitionIndex < DimensionPortalCraftingRegistry.Count; definitionIndex++)
      {
        DimensionPortalCraftingRecipeDefinition definition;
        if (!DimensionPortalCraftingRegistry.TryGetRecipe(definitionIndex, out definition) ||
            definition.CraftingStationObjectID != craftingStationObjectID)
        {
          continue;
        }

        ObjectID craftedObjectID;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.PortalObjectName,
            out craftedObjectID))
        {
          continue;
        }

        ResolvedRecipes.Add(new ResolvedRecipe(craftedObjectID, definition));
      }

      if (ResolvedRecipes.Count == 0)
      {
        return false;
      }

      bool addedAnyRecipe = false;
      int firstRecipeIndex = -1;
      int lastRecipeIndex = -1;
      for (int i = 0; i < ResolvedRecipes.Count; i++)
      {
        ResolvedRecipe resolved = ResolvedRecipes[i];
        int recipeIndex = FindRecipe(recipes, resolved.ObjectID);
        if (recipeIndex < 0)
        {
          recipeIndex = recipes.Length;
          recipes.Add(CreatePortalRecipe(resolved.ObjectID, resolved.Definition));
          addedAnyRecipe = true;
          DimensionFrameworkLog.Verbose(
              "[ExpandNullforge] Added " +
              resolved.Definition.DisplayName +
              " recipe to registered crafting station " +
              (int)craftingStationObjectID +
              ".");
        }

        if (firstRecipeIndex < 0 || recipeIndex < firstRecipeIndex)
        {
          firstRecipeIndex = recipeIndex;
        }

        if (recipeIndex > lastRecipeIndex)
        {
          lastRecipeIndex = recipeIndex;
        }
      }

      if (EnsureSharedCategory(
          entity,
          entityManager,
          craftingStationObjectID,
          ResolvedRecipes[0].ObjectID,
          firstRecipeIndex,
          lastRecipeIndex - firstRecipeIndex + 1))
      {
        addedAnyRecipe = true;
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

    /// <summary>
    /// Puts every framework recipe on this station into ONE crafting category, so the player finds
    /// them together on a single page instead of one per page.
    /// </summary>
    /// <remarks>
    /// A category is the only way to add recipes to a station that is already full. The UI renders
    /// at most three side-by-side windows of six, and it renders only the current category's slice —
    /// so a station whose own recipes already fill those three windows has no room to simply append
    /// (the game logs "Not enough SimpleCraftingUIs" and shows nothing). Giving the station's own
    /// recipes one category and ours another keeps both within the limit, and ours all land on the
    /// same page because they share a category. Ours is always added last, so if its icon object
    /// ever fails to resolve, the game drops our page without shifting anyone else's.
    /// </remarks>
    private static bool EnsureSharedCategory(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID,
        ObjectID categoryObjectID,
        int firstRecipeIndex,
        int recipeCount)
    {
      if (recipeCount < 1 || firstRecipeIndex < 0)
      {
        return false;
      }

      if (!entityManager.HasBuffer<IncludedCraftingBuildingsBuffer>(entity))
      {
        entityManager.AddBuffer<IncludedCraftingBuildingsBuffer>(entity);
      }

      DynamicBuffer<IncludedCraftingBuildingsBuffer> categories =
          entityManager.GetBuffer<IncludedCraftingBuildingsBuffer>(entity);
      int requiredCoverage = firstRecipeIndex + recipeCount;
      int existingIndex = FindCategory(categories, categoryObjectID);
      if (existingIndex >= 0)
      {
        // Our category already exists; resize it so it spans exactly our block, which may have
        // grown since the last pass as more objects finished resolving.
        int precedingCoverage = 0;
        for (int i = 0; i < existingIndex; i++)
        {
          precedingCoverage += Mathf.Max(0, categories[i].amountOfCraftingOptions);
        }

        int desired = Mathf.Max(1, requiredCoverage - precedingCoverage);
        IncludedCraftingBuildingsBuffer ours = categories[existingIndex];
        if (ours.amountOfCraftingOptions == desired)
        {
          return false;
        }

        ours.amountOfCraftingOptions = desired;
        categories[existingIndex] = ours;
        return true;
      }

      // Categories partition the recipe buffer by consecutive run lengths, so any gap ahead of our
      // block has to be absorbed or every later category would point at the wrong recipes.
      int coveredRecipeCount = GetCoveredRecipeCount(categories);
      if (coveredRecipeCount > firstRecipeIndex)
      {
        // Our recipes already fall inside an authored range. Only extend if our tail runs past it.
        if (coveredRecipeCount >= requiredCoverage || categories.Length == 0)
        {
          return false;
        }

        IncludedCraftingBuildingsBuffer tail = categories[categories.Length - 1];
        tail.amountOfCraftingOptions += requiredCoverage - coveredRecipeCount;
        categories[categories.Length - 1] = tail;
        return true;
      }

      if (coveredRecipeCount < firstRecipeIndex)
      {
        int gap = firstRecipeIndex - coveredRecipeCount;
        if (categories.Length > 0)
        {
          IncludedCraftingBuildingsBuffer lastCategory = categories[categories.Length - 1];
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
        objectID = categoryObjectID,
        amountOfCraftingOptions = recipeCount
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

    /// <summary>A registered recipe whose crafted object has resolved to a real id this pass.</summary>
    private readonly struct ResolvedRecipe
    {
      public readonly ObjectID ObjectID;
      public readonly DimensionPortalCraftingRecipeDefinition Definition;

      public ResolvedRecipe(ObjectID objectID, DimensionPortalCraftingRecipeDefinition definition)
      {
        ObjectID = objectID;
        Definition = definition;
      }
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
