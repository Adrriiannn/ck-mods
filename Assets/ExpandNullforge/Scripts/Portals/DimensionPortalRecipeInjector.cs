using PugMod;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// Puts every registered framework recipe onto the station that offers it, as the game converts
  /// that station's object.
  /// </summary>
  /// <remarks>
  /// Named for portals because portals were the first thing that needed it, but it carries every
  /// recipe the framework registers — a mod's items at a mod's own Workbench, and the by-hand list
  /// on the player, which Core Keeper models as a crafting station like any other
  /// (<c>ObjectID.Player</c>). The one moment this can happen is while an object type is being
  /// added: a station's recipe buffer only exists on the converted entity, and a mod object has no
  /// number before then.
  /// <para>
  /// Appending is safe even on a station whose recipes are filtered by world progress.
  /// <c>AvailableRecipesFromContentBundlesSystem</c> rewrites a recipe buffer from the station's
  /// stored unfiltered list, but only for as many entries as that list holds — everything added
  /// here sits past its end and is left alone.
  /// </para>
  /// </remarks>
  public static class DimensionPortalRecipeInjector
  {
    /// <summary>
    /// How many recipes a station can show without being split into categories.
    /// </summary>
    /// <remarks>
    /// Three side-by-side windows of six: the game's own UI prefab holds exactly three
    /// <c>SimpleCraftingUI</c>s and each walks six recipes. Past this the game logs
    /// "Not enough SimpleCraftingUIs" and shows nothing, which is the whole reason categories are
    /// written at all.
    /// </remarks>
    private const int UncategorizedRecipeCapacity = 18;

    private static readonly List<PendingCraftingTarget> PendingCraftingTargets =
        new List<PendingCraftingTarget>();

    private static readonly List<ResolvedRecipe> ResolvedRecipes = new List<ResolvedRecipe>();

    public static void OnObjectTypeAdded(
        Entity entity,
        GameObject authoringData,
        EntityManager entityManager)
    {
      RefreshRegisteredPortalObjectIds();

      // A recipe may name a crafting station belonging to another mod, and that station has no
      // number until the game gives it one — which is happening right now, for one object at a
      // time. Resolving here is what lets a portal be craftable at a modded bench at all.
      DimensionCraftingRegistry.TryResolveStationNames();

      string modObjectName = GetModObjectName(authoringData);
      ObjectID objectID = GetObjectID(authoringData);
      if (objectID == ObjectID.None && !string.IsNullOrEmpty(modObjectName))
      {
        // A mod's own object carries no EntityMonoBehaviourData — only ObjectAuthoring — so the
        // game's own way of reading an object id off the prefab comes back empty for every bench
        // this framework generates. Without this fall-back a recipe could name one of the mod's
        // Workbenches and the bench would never be recognised as a crafting station at all, which
        // is exactly why custom-station injection used to stop at a warning.
        DimensionPortalObjectIdCache.TryResolve(modObjectName, out objectID);
      }

      // The station being added may BE the one a recipe is waiting for, and on this pass its name
      // may still not resolve. Remembering it now means the retry pass can reach it later instead
      // of the recipe waiting forever for a station the game already built.
      bool awaitedStation = DimensionCraftingRegistry.IsWaitingForCraftingStationName(modObjectName);
      if ((DimensionCraftingRegistry.HasRecipeForCraftingStation(objectID) || awaitedStation) &&
          EntityStillExists(entity, entityManager))
      {
        RememberCraftingTarget(entity, entityManager, objectID, modObjectName);
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
        DimensionLog.Problem(DimensionLogChannels.Portal, null, "Registered portal crafting station has no crafting recipe buffer.");
        return false;
      }

      DynamicBuffer<CanCraftObjectsBuffer> recipes = entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);

      // Collect everything this station should show in one pass. The framework's recipes belong
      // together in the crafting window, so they have to be placed as a block rather than one at a
      // time — objects resolve at different moments during conversion, and adding them piecemeal is
      // what used to scatter them across separate pages.
      ResolvedRecipes.Clear();
      for (int definitionIndex = 0; definitionIndex < DimensionCraftingRegistry.Count; definitionIndex++)
      {
        DimensionCraftingRecipeDefinition definition;
        if (!DimensionCraftingRegistry.TryGetRecipe(definitionIndex, out definition) ||
            definition.CraftingStationObjectID != craftingStationObjectID)
        {
          continue;
        }

        ObjectID craftedObjectID;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.CraftedObjectName,
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
              "Added " +
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
          lastRecipeIndex - firstRecipeIndex + 1,
          recipes.Length))
      {
        addedAnyRecipe = true;
      }

      return addedAnyRecipe;
    }

    private static void RememberCraftingTarget(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID,
        string craftingStationObjectName)
    {
      for (int i = 0; i < PendingCraftingTargets.Count; i++)
      {
        PendingCraftingTarget existing = PendingCraftingTargets[i];
        if (existing.Entity == entity)
        {
          PendingCraftingTargets[i] = new PendingCraftingTarget(
              entity, entityManager, craftingStationObjectID, craftingStationObjectName);
          return;
        }
      }

      PendingCraftingTargets.Add(new PendingCraftingTarget(
          entity, entityManager, craftingStationObjectID, craftingStationObjectName));
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

        // A station remembered before its own number existed is still only a name. Resolving it
        // here is what lets a mod's Workbench be filled at all: the pass that remembered it had
        // nothing but the name, and a target left holding None matches no recipe forever.
        ObjectID stationObjectID = target.CraftingStationObjectID;
        if (stationObjectID == ObjectID.None &&
            !string.IsNullOrEmpty(target.CraftingStationObjectName) &&
            DimensionPortalObjectIdCache.TryResolve(
                target.CraftingStationObjectName,
                out stationObjectID))
        {
          target = new PendingCraftingTarget(
              target.Entity,
              target.EntityManager,
              stationObjectID,
              target.CraftingStationObjectName);
          PendingCraftingTargets[i] = target;
        }

        TryAddRecipes(
            target.Entity,
            target.EntityManager,
            target.CraftingStationObjectID);
        if (!HasUnresolvedPortalRecipeForStation(target.CraftingStationObjectID) &&
            !DimensionCraftingRegistry.IsWaitingForCraftingStationName(
                target.CraftingStationObjectName))
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
            "Dropped stale pending dimension portal recipe target after its conversion world was unloaded.");
        return false;
      }
      catch (InvalidOperationException)
      {
        DimensionFrameworkLog.Warning(
            "Dropped invalid pending dimension portal recipe target after a world transition.");
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
          "Cleared pending dimension portal recipe targets (" +
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

    /// <summary>The name a mod-defined object carries, or empty for one of the game's own.</summary>
    private static string GetModObjectName(GameObject authoringData)
    {
      if (authoringData == null)
      {
        return string.Empty;
      }

      ObjectAuthoring objectAuthoring = authoringData.GetComponent<ObjectAuthoring>();
      return objectAuthoring == null || objectAuthoring.objectName == null
          ? string.Empty
          : objectAuthoring.objectName;
    }

    private static bool IsRegisteredPortalObject(GameObject authoringData)
    {
      if (authoringData == null)
      {
        return false;
      }

      ObjectAuthoring objectAuthoring = authoringData.GetComponent<ObjectAuthoring>();
      return objectAuthoring != null &&
             DimensionCraftingRegistry.IsRegisteredCraftedObjectName(objectAuthoring.objectName);
    }

    private static bool AnyRegisteredPortalObjectResolved()
    {
      for (int i = 0; i < DimensionCraftingRegistry.Count; i++)
      {
        DimensionCraftingRecipeDefinition definition;
        if (!DimensionCraftingRegistry.TryGetRecipe(i, out definition))
        {
          continue;
        }

        ObjectID objectID;
        if (DimensionPortalObjectIdCache.TryResolve(
            definition.CraftedObjectName,
            out objectID))
        {
          return true;
        }
      }

      return false;
    }

    private static bool HasUnresolvedPortalRecipeForStation(ObjectID craftingStationObjectID)
    {
      for (int i = 0; i < DimensionCraftingRegistry.Count; i++)
      {
        DimensionCraftingRecipeDefinition definition;
        if (!DimensionCraftingRegistry.TryGetRecipe(i, out definition) ||
            definition.CraftingStationObjectID != craftingStationObjectID)
        {
          continue;
        }

        ObjectID objectID;
        if (!DimensionPortalObjectIdCache.TryResolve(
            definition.CraftedObjectName,
            out objectID))
        {
          return true;
        }
      }

      return false;
    }

    private static void RefreshRegisteredPortalObjectIds()
    {
      for (int i = 0; i < DimensionCraftingRegistry.Count; i++)
      {
        DimensionCraftingRecipeDefinition definition;
        if (!DimensionCraftingRegistry.TryGetRecipe(i, out definition))
        {
          continue;
        }

        DimensionPortalObjectIdCache.Refresh(definition.CraftedObjectName);
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
        DimensionCraftingRecipeDefinition definition)
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
    /// <para>
    /// It is only done when a station needs it. A station with no categories shows every recipe it
    /// has, so inventing the first category on one that still fits would HIDE whatever falls
    /// outside our block — which is what a small custom bench looks like, and what a bench that
    /// already lists the same recipe itself looks like.
    /// </para>
    /// </remarks>
    private static bool EnsureSharedCategory(
        Entity entity,
        EntityManager entityManager,
        ObjectID craftingStationObjectID,
        ObjectID categoryObjectID,
        int firstRecipeIndex,
        int recipeCount,
        int stationRecipeCount)
    {
      if (recipeCount < 1 || firstRecipeIndex < 0)
      {
        return false;
      }

      if (craftingStationObjectID == ObjectID.Player)
      {
        // Made-by-hand recipes live on the player, and the by-hand window never pages: categories
        // are read by CraftingBuilding, which is a placed object and not the player. Writing a
        // category here would add a buffer nothing consults, so the by-hand list simply appends —
        // and that is why the generator caps how many recipes may be made by hand.
        return false;
      }

      if (stationRecipeCount <= UncategorizedRecipeCapacity &&
          !entityManager.HasBuffer<IncludedCraftingBuildingsBuffer>(entity))
      {
        // Still fits in the three windows the UI draws, and the station has no categories of its
        // own, so every recipe is already visible. Adding one now would narrow the station down to
        // our slice.
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
      public readonly DimensionCraftingRecipeDefinition Definition;

      public ResolvedRecipe(ObjectID objectID, DimensionCraftingRecipeDefinition definition)
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

      /// <summary>
      /// The station's own name, kept so a target stays remembered while a recipe is still
      /// waiting for that name rather than for its number.
      /// </summary>
      public readonly string CraftingStationObjectName;

      public PendingCraftingTarget(
          Entity entity,
          EntityManager entityManager,
          ObjectID craftingStationObjectID,
          string craftingStationObjectName)
      {
        Entity = entity;
        EntityManager = entityManager;
        CraftingStationObjectID = craftingStationObjectID;
        CraftingStationObjectName = craftingStationObjectName ?? string.Empty;
      }
    }
  }
}
