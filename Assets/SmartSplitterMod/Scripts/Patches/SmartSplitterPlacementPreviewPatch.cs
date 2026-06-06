using HarmonyLib;
using Pug.ECS.Components;
using Pug.Properties;
using Unity.Entities;

[HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
public static class SmartSplitterPlacementPreviewPatch
{
  private static void Postfix(
      PlacementHandler __instance,
      in PlacementCD placementCD,
      ObjectDataCD infoAboutObjectToPlace,
      DynamicBuffer<PlacementSizeByEquipmentTypeBuffer> placementSizeByEquipmentTypeBuffer,
      Entity placementPrefab,
      ComponentLookup<DirectionCD> directionLookup,
      ComponentLookup<ResizableTileSizeCD> sizeVariationLookup,
      ComponentLookup<DirectionBasedOnVariationCD> directionBasedOnVariationLookup,
      ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup,
      PugDatabase.DatabaseBankCD databaseBankCD)
  {
    if (infoAboutObjectToPlace.objectID != ObjectID.ConveyorBeltSplitter ||
        __instance == null ||
        __instance.placeableIcon == null)
    {
      return;
    }

    int variation = infoAboutObjectToPlace.variation;

    bool canRotate = PlacementHandler.ObjectCanBeRotated(
        placementPrefab,
        directionBasedOnVariationLookup,
        objectPropertiesLookup,
        directionLookup);

    bool canToggle = PlacementHandler.ObjectCanBeToggledToNewNonRotationOption(
        placementPrefab,
        objectPropertiesLookup);

    if (canRotate)
    {
      variation = placementCD.rotationVariationToPlace;
      variation = (variation + __instance.GetIconRotationOffset(placementPrefab, directionLookup) + 3) % 4;
    }
    else if (canToggle)
    {
      variation = placementCD.nonRotationVariationToPlace;
    }

    __instance.placeableIcon.SetState(
        placementCD.canPlaceObject,
        variation,
        canRotate || canToggle,
        DisplayPlaceableType.TCircuit);
  }
}