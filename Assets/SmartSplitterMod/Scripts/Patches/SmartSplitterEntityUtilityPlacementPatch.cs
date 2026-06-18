using System;
using HarmonyLib;
using Unity.Entities;

[HarmonyPatch(typeof(EntityUtility))]
[HarmonyPatch(nameof(EntityUtility.CreateEntity))]
[HarmonyPatch(new Type[]
{
  typeof(EntityCommandBuffer),
  typeof(ObjectID),
  typeof(int),
  typeof(BlobAssetReference<PugDatabase.PugDatabaseBank>),
  typeof(int)
})]
public static class SmartSplitterEntityUtilityPlacementPatch
{
  private static void Prefix(ObjectID objectID, ref int variation)
  {
    if (!SmartSplitterPlacementCompatibility.ShouldRemapEntityCreationVariation ||
        objectID != ObjectID.ConveyorBeltSplitter ||
        variation < 0 ||
        variation > 3)
    {
      return;
    }

    variation =
        SmartSplitterOrientationUtility.GetPlacementVariationForSmartForwardVariation(
            SmartSplitterOrientationUtility.NormalizeVariation(variation));
  }
}
