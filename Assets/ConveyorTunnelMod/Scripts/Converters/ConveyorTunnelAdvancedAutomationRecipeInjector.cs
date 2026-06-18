using PugMod;
using Unity.Entities;
using UnityEngine;

public static class ConveyorTunnelAdvancedAutomationRecipeInjector
{
  private static readonly ObjectID AutomationTableObjectID = (ObjectID)4022;
  private static readonly ObjectID AdvancedAutomationTableObjectID = (ObjectID)4055;

  public static void OnObjectTypeAdded(
      Entity entity,
      GameObject authoringData,
      EntityManager entityManager)
  {
    if (!IsAdvancedAutomationTable(authoringData) ||
        !entityManager.Exists(entity))
    {
      return;
    }

    ObjectID tunnelObjectID = API.Authoring.GetObjectID(ConveyorTunnelIds.ObjectName);
    if (tunnelObjectID == ObjectID.None)
    {
      Debug.LogWarning(
          "[ConveyorTunnelMod] Could not add the tunnel recipe to the Advanced Automation Table because its ObjectID was unavailable.");
      return;
    }

    if (!entityManager.HasBuffer<CanCraftObjectsBuffer>(entity))
    {
      Debug.LogWarning(
          "[ConveyorTunnelMod] Advanced Automation Table has no crafting recipe buffer.");
      return;
    }

    DynamicBuffer<CanCraftObjectsBuffer> recipes =
        entityManager.GetBuffer<CanCraftObjectsBuffer>(entity);

    if (!entityManager.HasBuffer<IncludedCraftingBuildingsBuffer>(entity))
    {
      RebuildCategoryBuffer(authoringData, entity, entityManager);
    }

    DynamicBuffer<IncludedCraftingBuildingsBuffer> categories =
        entityManager.GetBuffer<IncludedCraftingBuildingsBuffer>(entity);

    int automationCategoryIndex = FindAutomationCategory(categories);
    if (automationCategoryIndex < 0)
    {
      Debug.LogWarning(
          "[ConveyorTunnelMod] Advanced Automation Table has no included Automation Table category.");
      return;
    }

    int automationCategoryStart = GetCategoryStart(categories, automationCategoryIndex);
    IncludedCraftingBuildingsBuffer automationCategory =
        categories[automationCategoryIndex];
    int automationCategoryEnd =
        automationCategoryStart + automationCategory.amountOfCraftingOptions;
    int tunnelRecipeIndex = FindRecipe(recipes, tunnelObjectID);

    if (tunnelRecipeIndex < 0)
    {
      int insertionIndex = Mathf.Clamp(automationCategoryEnd, 0, recipes.Length);
      recipes.Insert(insertionIndex, CreateTunnelRecipe(tunnelObjectID));
      automationCategory.amountOfCraftingOptions++;
      categories[automationCategoryIndex] = automationCategory;
    }
    else if (tunnelRecipeIndex == automationCategoryEnd)
    {
      // The included table already supplied the recipe, but vanilla category
      // metadata still describes its original authored slot count.
      automationCategory.amountOfCraftingOptions++;
      categories[automationCategoryIndex] = automationCategory;
    }
    else if (tunnelRecipeIndex < automationCategoryStart ||
             tunnelRecipeIndex >= automationCategoryEnd)
    {
      Debug.LogWarning(
          "[ConveyorTunnelMod] The tunnel recipe already exists outside the Advanced Automation Table's Automation category.");
      return;
    }

    Debug.Log(
        "[ConveyorTunnelMod] Added the tunnel recipe to the Advanced Automation Table's Automation category.");
  }

  private static bool IsAdvancedAutomationTable(GameObject authoringData)
  {
    if (authoringData == null)
    {
      return false;
    }

    EntityMonoBehaviourData entityData =
        authoringData.GetComponent<EntityMonoBehaviourData>();
    return entityData != null &&
           entityData.objectInfo != null &&
           entityData.objectInfo.objectID == AdvancedAutomationTableObjectID;
  }

  private static void RebuildCategoryBuffer(
      GameObject authoringData,
      Entity entity,
      EntityManager entityManager)
  {
    CraftingAuthoring craftingAuthoring =
        authoringData.GetComponent<CraftingAuthoring>();
    int advancedRecipeCount =
        craftingAuthoring != null && craftingAuthoring.canCraftObjects != null
            ? craftingAuthoring.canCraftObjects.Count
            : 0;
    int automationRecipeCount = GetIncludedAutomationRecipeCount(craftingAuthoring);

    DynamicBuffer<IncludedCraftingBuildingsBuffer> categories =
        entityManager.AddBuffer<IncludedCraftingBuildingsBuffer>(entity);
    categories.Add(new IncludedCraftingBuildingsBuffer
    {
      objectID = AdvancedAutomationTableObjectID,
      amountOfCraftingOptions = advancedRecipeCount
    });
    categories.Add(new IncludedCraftingBuildingsBuffer
    {
      objectID = AutomationTableObjectID,
      amountOfCraftingOptions = automationRecipeCount
    });
  }

  private static int GetIncludedAutomationRecipeCount(
      CraftingAuthoring craftingAuthoring)
  {
    if (craftingAuthoring == null ||
        craftingAuthoring.includeCraftedObjectsFromBuildings == null)
    {
      return 0;
    }

    for (int i = 0;
         i < craftingAuthoring.includeCraftedObjectsFromBuildings.Count;
         i++)
    {
      CraftingAuthoring includedAuthoring =
          craftingAuthoring.includeCraftedObjectsFromBuildings[i];
      if (includedAuthoring == null)
      {
        continue;
      }

      EntityMonoBehaviourData entityData =
          includedAuthoring.GetComponent<EntityMonoBehaviourData>();
      if (entityData != null &&
          entityData.objectInfo != null &&
          entityData.objectInfo.objectID == AutomationTableObjectID)
      {
        return includedAuthoring.canCraftObjects != null
            ? includedAuthoring.canCraftObjects.Count
            : 0;
      }
    }

    return 0;
  }

  private static int FindAutomationCategory(
      DynamicBuffer<IncludedCraftingBuildingsBuffer> categories)
  {
    for (int i = 0; i < categories.Length; i++)
    {
      if (categories[i].objectID == AutomationTableObjectID)
      {
        return i;
      }
    }

    return -1;
  }

  private static int GetCategoryStart(
      DynamicBuffer<IncludedCraftingBuildingsBuffer> categories,
      int categoryIndex)
  {
    int start = 0;
    for (int i = 0; i < categoryIndex; i++)
    {
      start += categories[i].amountOfCraftingOptions;
    }

    return start;
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

  private static CanCraftObjectsBuffer CreateTunnelRecipe(ObjectID tunnelObjectID)
  {
    return new CanCraftObjectsBuffer
    {
      objectID = tunnelObjectID,
      amount = 2,
      craftingTimeOverride = 2f
    };
  }
}
