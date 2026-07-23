using ExpandNullforge.Foundation;
using HarmonyLib;
using PlayerEquipment;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Portals
{
    /// <summary>
    /// Item-use trigger for the instantaneous item portal (V2). The framework's portal item is a plain
    /// <c>NonUsableSlot</c> item (it has no native use behaviour of its own), so right-clicking it falls
    /// through the per-slot switch in <see cref="EquipmentUpdateSystem"/> to the base
    /// <see cref="EquipmentSlot.UpdateEquipment(bool, bool, in ClientInput, in EquipmentUpdateAspect, in EquipmentUpdateSharedData, in LookupEquipmentUpdateData)"/>.
    /// We post-fix that base method: when the held item is a registered portal item and the player is
    /// holding secondary-interact (right-click), we queue a spawn for the server system to place a
    /// temporary portal next to the player. We only act on the server execution so a single portal is
    /// spawned authoritatively in single-player, host, and dedicated-server play alike.
    ///
    /// The equipment update runs inside a Burst <c>IJobEntity</c>, which would bypass this managed patch,
    /// so <see cref="ExpandNullforgeModEntry"/> disables Burst for <see cref="EquipmentUpdateSystem"/>
    /// and <see cref="DimensionEquipmentUpdateForceJobCompletePatch"/> forces the job to finish while
    /// Burst is still disabled — the same workaround the ModSDK TeleportAfterEating example uses.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentSlot), "UpdateEquipment")]
    internal static class DimensionItemPortalUseHook
    {
        [HarmonyPostfix]
        private static void AfterUpdateEquipment(
            bool secondInteractHeld,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            // Right-click only, and only on the authoritative server tick.
            if (!secondInteractHeld || !equipmentUpdateSharedData.isServer)
            {
                return;
            }

            ObjectID itemObjectId = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData.objectID;
            if (itemObjectId == ObjectID.None)
            {
                return;
            }

            if (!DimensionItemPortalRegistry.TryGetConfigByObjectId(itemObjectId, out DimensionItemPortalConfig config))
            {
                return;
            }

            Entity player = equipmentUpdateAspect.entity;
            ComponentLookup<LocalTransform> transformLookup = equipmentUpdateLookupData.localTransformLookup;
            if (!transformLookup.HasComponent(player))
            {
                return;
            }

            float3 position = transformLookup[player].Position;
            int2 playerTile = new int2(
                (int)math.round(position.x),
                (int)math.round(position.z));

            // The registry debounces (one pending spawn) and the server system gates on the shared
            // cooldown, so it is safe to enqueue every tick the button is held.
            DimensionItemPortalRegistry.EnqueueSpawn(playerTile, config.ItemObjectName, player);
        }
    }

    /// <summary>
    /// Reliability companion to disabling Burst for <see cref="EquipmentUpdateSystem"/>: the update job
    /// can start after <c>OnUpdate</c> returns (when Burst would already be re-enabled), so we force the
    /// job to complete at the end of <c>OnUpdate</c> while Burst is still off. High priority ensures we
    /// run before Burst is turned back on. Mirrors the ModSDK TeleportAfterEating example.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentUpdateSystem), "OnUpdate")]
    internal static class DimensionEquipmentUpdateForceJobCompletePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(ref SystemState state)
        {
            state.Dependency.Complete();
        }
    }
}
