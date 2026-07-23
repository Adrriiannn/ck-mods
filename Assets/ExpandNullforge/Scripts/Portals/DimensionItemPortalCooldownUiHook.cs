using HarmonyLib;

namespace ExpandNullforge.Portals
{
    /// <summary>
    /// Drives the vanilla item-icon cooldown overlay (the dark wipe that uncovers downward) for
    /// registered portal items. The native overlay reads a per-player synced cooldown-group
    /// buffer, but every group is also resolved by unrelated vanilla items through the slot-type
    /// fallback (NonUsableSlot covers armor and materials, EatableSlot covers food, ...), so
    /// starting a native group timer would paint the wipe over half the inventory and block those
    /// items' use checks. Instead the framework's own shared item-portal cooldown is surfaced
    /// directly: when a slot holds a registered portal item and the shared cooldown is running,
    /// the overlay shows its remaining fraction — exactly the window in which using the item
    /// would be ignored.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentSlot), "GetNormalizedCooldownRemainingForItem")]
    internal static class DimensionItemPortalCooldownUiHook
    {
        [HarmonyPostfix]
        private static void AfterGetNormalizedCooldownRemainingForItem(
            in ObjectDataCD objectData,
            ref float __result)
        {
            // A real native cooldown (from a vanilla group) wins; we only fill the gap.
            if (__result > 0f)
            {
                return;
            }

            if (!DimensionItemPortalRegistry.TryGetConfigByObjectId(
                objectData.objectID, out DimensionItemPortalConfig _))
            {
                return;
            }

            if (DimensionItemPortalRegistry.TryGetGlobalCooldownNormalizedRemaining(
                out float normalizedRemaining))
            {
                __result = normalizedRemaining;
            }
        }
    }
}
