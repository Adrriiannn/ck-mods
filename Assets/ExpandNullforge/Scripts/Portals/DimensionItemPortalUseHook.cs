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
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPostfix]
        private static void AfterUpdateEquipment(
            bool secondInteractHeld,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            Fired++;

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
    /// Forces <see cref="EquipmentUpdateSystem"/>'s update job to finish before <c>OnUpdate</c>
    /// returns. Without this the item-use hook's work is scheduled and never joined here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE JOB IS THE THING THAT MATTERS. The slot handlers we hook —
    /// <c>PlaceObjectSlot.UpdateEquipment</c> and friends — are not called from <c>OnUpdate</c>. They
    /// run inside <c>EquipmentUpdateSystem.UpdateJob</c>, which <c>OnUpdate</c> schedules into
    /// <c>state.Dependency</c> and does not complete. So every side effect our hook produces (the
    /// portal spawn enqueue, tile writes through the lifted <c>AddTile</c> guard) is still in flight
    /// when the system returns.
    /// </para>
    /// <para>
    /// DO NOT DELETE THIS ON THE ASSUMPTION THAT BurstDisabler COVERS IT. It does not, for the plain
    /// call we make. <c>EquipmentUpdateSystem</c> is a <c>struct : ISystem</c>, i.e. unmanaged, and
    /// for unmanaged systems <c>DisableBurstForSystemInternal</c> disables Burst through
    /// <c>SystemBaseRegistry.SetBurstEnabledForSystem</c> and then calls <c>PatchSystem</c> — which
    /// adds <b>no patches whatsoever</b> unless <c>addCompleteDependencyPatch</c> is set. The only
    /// thing that flag adds is a postfix on <c>OnUpdate</c> that completes the dependency: exactly
    /// what this class does. So <c>DisableBurstForSystemAndJobs</c> and this patch are two spellings
    /// of one mechanism, and using both would just complete an already-complete handle.
    /// </para>
    /// <para>
    /// THE PRIORITY IS LOAD-BEARING AFTER ALL, and the reason matters. An earlier revision of this
    /// comment claimed Burst is "switched off once at the registry level rather than toggled around
    /// each update". That is false, and it is worth being exact about because the whole hook rests on
    /// it: <c>DisableBurstForSystemPatch</c> is a prefix and postfix on
    /// <c>Unity.Entities.WorldUnmanagedImpl.UpdateSystem</c>, and the prefix sets the PROCESS-GLOBAL
    /// <c>BurstCompiler.Options.EnableBurstCompilation</c> to false while the postfix puts it back.
    /// Burst really is toggled around every single update, and that toggle is the only reason the
    /// managed bodies of <c>EquipmentSlot.UpdateEquipment</c> and <c>EntityUtility.AddTile</c> run at
    /// all — they are called from inside the Bursted <c>UpdateJob</c>, where a Harmony patch would
    /// otherwise never be reached. So this postfix has to join the job before Burst is switched back
    /// on, and High is what puts it there.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(EquipmentUpdateSystem), "OnUpdate")]
    internal static class DimensionEquipmentUpdateForceJobCompletePatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void Postfix(ref SystemState state)
        {
            Fired++;

            state.Dependency.Complete();
        }
    }
}
