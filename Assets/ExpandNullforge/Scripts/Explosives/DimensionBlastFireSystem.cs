using Unity.Collections;
using Unity.Entities;

namespace ExpandNullforge.Explosives
{
    /// <summary>
    /// Tells a blast to leave fire, in the one window where saying so still counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE WINDOW IS THE WHOLE DESIGN. <c>ExplosiveSystem.CreateExplosion</c> writes the blast's
    /// <c>ExplosionCD</c> wholesale on the way out, and one of the things it writes is
    /// <c>spawnNapalmObjectID = &lt;dice roll&gt; ? Napalm : None</c>
    /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:181</c>). Anything baked onto the prefab is gone.
    /// <c>ExplosionDamageSystem</c> is what reads the field and spawns the fire
    /// (<c>ck-db\Pug.Other\ExplosionDamageSystem.cs:299-302</c>). Between those two is where this
    /// writes.
    /// </para>
    /// <para>
    /// <c>BeforePredictedSimulationSystemGroup</c> is that between: it runs first in the simulation
    /// group and before the predicted group that holds both of the game's systems
    /// (<c>ck-db\Pug.Other\BeforePredictedSimulationSystemGroup.cs:8-10</c>), so a blast created on
    /// one tick is stamped at the top of the next and the game's own damage pass reads the stamp.
    /// The margin is far wider than one tick anyway: the blast sits on a 0.1-second delay timer
    /// before it does anything at all (<c>ExplosiveSystem.cs:621</c>), which is six ticks at the
    /// game's rate.
    /// </para>
    /// <para>
    /// A blast that already says it leaves fire is left alone. That is the case where the player's
    /// own gear rolled the napalm chance, and overwriting it would be writing the same answer twice
    /// — except for the variation, which the author's choice should win, because it is the one
    /// thing they said out loud.
    /// </para>
    /// <para>
    /// Declared for client simulation as well as server because the game's own damage pass is: a
    /// player-owned blast is predicted on the client, and a client that had not been told about the
    /// fire would predict a blast that leaves none.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(BeforePredictedSimulationSystemGroup))]
    public partial class DimensionBlastFireSystem : SystemBase
    {
        private EntityQuery blastQuery;

        protected override void OnCreate()
        {
            blastQuery = GetEntityQuery(
                ComponentType.ReadOnly<DimensionBlastFireCD>(),
                ComponentType.ReadWrite<ExplosionCD>());
            RequireForUpdate(blastQuery);
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> blasts = blastQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < blasts.Length; i++)
            {
                Entity blast = blasts[i];
                ExplosionCD explosion = EntityManager.GetComponentData<ExplosionCD>(blast);

                // Once the blast has dealt its damage the fire question has already been asked and
                // answered; writing now would be writing to a corpse.
                if (explosion.hasDealtDamage)
                {
                    continue;
                }

                DimensionBlastFireCD fire =
                    EntityManager.GetComponentData<DimensionBlastFireCD>(blast);
                if (explosion.spawnNapalmObjectID == ObjectID.Napalm &&
                    explosion.spawnNapalmVariation == fire.NapalmVariation)
                {
                    continue;
                }

                explosion.spawnNapalmObjectID = ObjectID.Napalm;
                explosion.spawnNapalmVariation = fire.NapalmVariation;
                EntityManager.SetComponentData(blast, explosion);
            }
        }
    }
}
