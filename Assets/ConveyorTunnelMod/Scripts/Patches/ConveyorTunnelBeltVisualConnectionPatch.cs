using HarmonyLib;
using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

[HarmonyPatch(typeof(ConveyorBelt), "GetAdjacentBeltDirection")]
public static class ConveyorTunnelBeltVisualConnectionPatch
{
  private static void Postfix(
      ConveyorBelt __instance,
      Vector3 direction,
      ref int2 __result)
  {
    if (!__result.Equals(int2.zero) ||
        __instance == null)
    {
      return;
    }

    int2 adjacentTile = (__instance.WorldPosition + direction).RoundToInt2();
    if (ConveyorTunnelVisual.TryGetTunnelDirectionAtTile(adjacentTile, out int2 tunnelDirection))
    {
      __result = tunnelDirection;
    }
  }
}
