using HarmonyLib;
using Pug.ECS.Components;
using Pug.Properties;
using Unity.Entities;
using Unity.Mathematics;

[HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
public static class ConveyorTunnelPlacementPreviewPatch
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
    if (__instance == null ||
        __instance.placeableIcon == null ||
        !ConveyorTunnelIds.TryRefresh() ||
        infoAboutObjectToPlace.objectID != ConveyorTunnelIds.ConveyorTunnelObjectID)
    {
      ConveyorTunnelPlacementGuideController.Hide();
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
    }
    else if (canToggle)
    {
      variation = placementCD.nonRotationVariationToPlace;
    }

    int2 direction =
        ConveyorTunnelDirectionUtility.GetDirectionFromVariation(variation);
    int2 targetTile = new int2(
        placementCD.bestPositionToPlaceAt.x,
        placementCD.bestPositionToPlaceAt.z);

    ConveyorTunnelPlacementGuideController.SetPlacementPreview(
        targetTile,
        direction,
        __instance.placeableIcon);
  }
}

[HarmonyPatch(typeof(PlacementIcon), "LateUpdate")]
public static class ConveyorTunnelPlacementIconFadePatch
{
  private static void Postfix(PlacementIcon __instance)
  {
    ConveyorTunnelPlacementGuideController.SyncAppearanceFromPlacementIcon(
        __instance);
  }
}
