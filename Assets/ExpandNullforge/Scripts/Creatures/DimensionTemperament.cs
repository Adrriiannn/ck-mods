using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Marks a creature that will not start a fight but will finish one: the Defensive temper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper decides "may I start chasing this?" against
    /// <c>ChaseStateCD.chaseAtDistanceSq</c>, EXCEPT for whoever hit the creature last, who is
    /// always measured against a forced 400 (twenty tiles squared) —
    /// <c>num9 = flag9 ? 400f : chaseStateCD.chaseAtDistanceSq;</c> then
    /// <c>if (flag10) num9 = 400f;</c> at
    /// <c>ck-db\Pug.Other\ChaseStateRequest.cs:224-228</c>, where <c>flag10</c> is the candidate
    /// that came out of <c>LastAttackerCD</c>. The chase, once entered, is held open against the
    /// same forced 400 (<c>ck-db\Pug.Other\ChaseStateSystem.cs:384-386</c>), so the retaliation
    /// does not evaporate on the next tick. A chase distance of zero therefore means exactly
    /// "never notices anyone, always answers the person who hit it", which is the temper.
    /// </para>
    /// <para>
    /// So why is this a RUNTIME component rather than a zero baked into the prefab? Because the
    /// authored <c>chaseAtDistance</c> feeds two different things through one converter, and only
    /// one of them should be zero. <c>ChaseStateConverter</c> passes it to
    /// <c>PathFindingConversion.CreatePathfindingEntity</c>, which sets
    /// <c>PathFindCD.searchRadius = ceil(chaseAtDistance)</c>
    /// (<c>ck-db\Pug.ECS.Conversion\PathFindingConversion.cs:12</c>). A search radius of zero
    /// makes the path search refuse to expand past the creature's own tile —
    /// <c>if (!math.any(math.abs(int2 - startPosition) &gt; searchRadius) ...)</c> at
    /// <c>ck-db\Pug.Other\PathFindSystem.cs:757</c> — so no path can ever exist. A defender that
    /// also asks for "needs a path to chase" then deadlocks outright: it cannot enter the chase
    /// without a path and cannot build a path without a chase, and every vanilla cattle prefab
    /// ships with exactly that flag set. Baking the zero bought a working aggro gate at the cost
    /// of a creature that could not walk.
    /// </para>
    /// <para>
    /// The prefab therefore keeps a real, sensible chase distance — the path search is sized from
    /// it correctly — and this component zeroes the aggro gate alone, once, on the server. Writing
    /// <c>chaseAtDistanceSq</c> at runtime is the game's own move, not an invention:
    /// <c>EnemySpawnerPlatformSystem.cs:278</c> does the same thing in the opposite direction to
    /// widen an arena spawn's aggro to twenty tiles.
    /// </para>
    /// </remarks>
    public sealed class DimensionHoldsFireAuthoring : MonoBehaviour
    {
    }

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
