using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{

    /// <summary>The baked half of the Defensive temper, plus its once-only latch.</summary>
    /// <remarks>
    /// <c>Applied</c> exists so the write happens once per living creature rather than every
    /// frame, which keeps the game's own systems free to change their mind afterwards — an arena
    /// platform widening a spawn's aggro, for instance, is not fought over forever. Instances are
    /// created from the prefab entity, which carries <c>Applied = 0</c> and is itself excluded
    /// from the query: Unity's entity queries skip prefab entities unless asked not to, so the
    /// latch cannot be tripped on the template and inherited already-spent by every spawn.
    /// </remarks>
    public struct DimensionHoldsFireCD : IComponentData
    {
        public byte Applied;
    }

    [Preserve]
    public sealed class DimensionHoldsFireConverter :
        SingleAuthoringComponentConverter<DimensionHoldsFireAuthoring>
    {
        protected override void Convert(DimensionHoldsFireAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionHoldsFireCD { Applied = 0 });
        }
    }

    /// <summary>
    /// Closes a Defensive creature's aggro gate on its first simulated tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Server-only, because chasing is decided server-side: <c>ChaseStateRequest</c> runs in the
    /// server's state-request pass and the client never reads <c>chaseAtDistanceSq</c> for
    /// anything.
    /// </para>
    /// <para>
    /// The timing window is the reason this is safe. <c>ChaseStateCD.shouldEnterStateCheckTimer</c>
    /// starts a fresh one-to-two second wait every time no target is found
    /// (<c>ChaseStateRequest.cs</c>, the <c>if (!flag2)</c> tail), so the earliest a newly spawned
    /// creature can pick a target is a whole tick after it exists — and this system runs in
    /// <c>SimulationSystemGroup</c> on that same first tick. The worst case if it somehow lost the
    /// race is one chase started at the authored distance, which the very next frame's write ends.
    /// </para>
    /// <para>
    /// A creature that carries the marker but no <c>ChaseStateCD</c> is not an error and not a
    /// warning: it simply never chases anyone, which for a Defensive creature means it hits back
    /// only at whatever is already in reach. The generator is where that is said out loud, because
    /// that is where the author can still do something about it.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionHoldsFireSystem : SystemBase
    {
        private EntityQuery holdsFireQuery;

        protected override void OnCreate()
        {
            holdsFireQuery = GetEntityQuery(
                ComponentType.ReadWrite<DimensionHoldsFireCD>(),
                ComponentType.ReadWrite<ChaseStateCD>());
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = holdsFireQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                DimensionHoldsFireCD state =
                    EntityManager.GetComponentData<DimensionHoldsFireCD>(entity);
                if (state.Applied != 0)
                {
                    continue;
                }

                ChaseStateCD chase = EntityManager.GetComponentData<ChaseStateCD>(entity);

                // The one number that decides whether being NEAR someone is enough to start a
                // chase. The last attacker is measured against a constant instead, so this closes
                // the gate on strangers without closing it on whoever drew blood.
                chase.chaseAtDistanceSq = 0f;
                EntityManager.SetComponentData(entity, chase);

                state.Applied = 1;
                EntityManager.SetComponentData(entity, state);
            }
        }
    }
}
