using HarmonyLib;

[HarmonyPatch(typeof(DroppedItem), nameof(DroppedItem.ManagedLateUpdate))]
public static class ConveyorTunnelDroppedItemVisualPatch
{
  public static void Postfix(DroppedItem __instance)
  {
    ConveyorTunnelItemVisualEffects.ApplyToDroppedItem(__instance);
  }
}
