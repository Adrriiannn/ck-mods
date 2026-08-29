using HarmonyLib;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>
  /// Gives an offering slot the look its author chose — the game's own dimmed ghost, a black
  /// mystery silhouette, or a custom sprite at a custom dimness.
  /// </summary>
  /// <remarks>
  /// <para>
  /// VANILLA ALREADY DRAWS THE GHOST. Any empty slot whose requirement accepts exactly one item
  /// shows that item's icon dimmed, and hovering shows the item's name and description — no code
  /// of ours involved. This patch only steps in when the slot belongs to a portal whose author
  /// asked for something other than the default: Mystery flips the vanilla silhouette flag (which
  /// also suppresses the tooltip — the game treats a silhouette as a riddle, and so do we), and
  /// CustomSprite swaps the artwork and dimness while keeping the tooltip.
  /// </para>
  /// <para>
  /// The slot is matched to its portal through two public doors — <c>GetInventoryHandler()</c> on
  /// the slot and <c>entityMonoBehaviour</c> on the handler — so no reflection is involved beyond
  /// Harmony itself.
  /// </para>
  /// </remarks>
  [HarmonyPatch(typeof(InventorySlotUI), nameof(InventorySlotUI.ShowHint))]
  internal static class DimensionPortalOfferingHintPatch
  {
    /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
    /// <remarks>
    /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
    /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
    /// framework cannot ask Harmony what it bound. One static increment is the whole of the
    /// evidence, and it costs one add on a path the game was already walking.
    /// </remarks>
    internal static int Fired;

    [HarmonyPrefix]
    private static void Prefix(
        InventorySlotUI __instance,
        ref bool showSilhouette,
        ref Sprite overrideSprite,
        ref float overrideAlpha)
    {
      Fired++;

      if (__instance == null)
      {
        return;
      }

      InventoryHandler handler = __instance.GetInventoryHandler();
      DimensionPortal portal =
          handler != null ? handler.entityMonoBehaviour as DimensionPortal : null;
      if (portal == null)
      {
        return;
      }

      DimensionPortal.OfferingSlotLook look;
      if (!portal.TryGetOfferingLook(__instance.inventorySlotIndex, out look))
      {
        return;
      }

      switch (look.look)
      {
        case DimensionPortalOfferingLook.Mystery:
          showSilhouette = true;
          break;

        case DimensionPortalOfferingLook.CustomSprite:
          if (look.customSprite != null)
          {
            overrideSprite = look.customSprite;
          }

          if (look.dimness > 0f)
          {
            overrideAlpha = Mathf.Clamp01(look.dimness);
          }

          break;

        default:
          // The game's own ghost, with the author's dimness when one was chosen.
          if (look.dimness > 0f)
          {
            overrideAlpha = Mathf.Clamp01(look.dimness);
          }

          break;
      }
    }
  }
}
