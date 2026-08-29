using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugMod;
using Unity.Entities;

namespace ExpandNullforge.Explosives
{
    /// <summary>
    /// Points a mod's bombs at the mod's own blasts, on the prefab the game actually reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THE GENERATOR CANNOT BAKE THIS. <c>ExplosiveAuthoring.explosionID</c> is an
    /// <c>ObjectID</c> enum field, and a mod's object ids do not exist until the game hands them
    /// out during load (<c>ck-db\Pug.ECS.Authoring\ObjectAuthoring.cs:57</c> asks
    /// <c>API.Authoring.GetObjectID</c> at conversion time). A blast that is one of the GAME's own
    /// objects is a known number and IS baked at generate; one of the mod's own blasts can only be
    /// resolved here.
    /// </para>
    /// <para>
    /// IT IS THE PREFAB ENTITY THAT MATTERS. Placing a bomb instantiates the database's prefab
    /// entity, so writing <c>IsExplosiveCD.explosionID</c> there makes every bomb the player will
    /// ever place come out correct — no per-instance patching, and no race with the fuse. This runs
    /// long before any bomb can be placed: the world has to exist and a player has to reach for the
    /// item.
    /// </para>
    /// <para>
    /// Runs in every world with a database rather than server-only. <c>ExplosiveSystem</c> is
    /// declared for client simulation as well as server
    /// (<c>ck-db\Pug.Other\ExplosiveSystem.cs:27</c>), so a client whose prefab still said
    /// <c>None</c> would predict a bomb that quietly fizzles and then have the real blast arrive as
    /// a ghost — a visible hitch for something that is supposed to be instant.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(
        WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionExplosiveHydrationSystem : SystemBase
    {
        private EntityQuery databaseQuery;
        private readonly HashSet<string> settled = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly HashSet<string> complained = new HashSet<string>(System.StringComparer.Ordinal);

        protected override void OnCreate()
        {
            databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            RequireForUpdate(databaseQuery);
        }

        protected override void OnUpdate()
        {
            IReadOnlyList<DimensionExplosiveDefinition> bombs = DimensionExplosiveRegistry.All;
            if (bombs.Count == 0 || settled.Count >= bombs.Count)
            {
                return;
            }

            PugDatabase.DatabaseBankCD bank =
                databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();

            for (int i = 0; i < bombs.Count; i++)
            {
                DimensionExplosiveDefinition bomb = bombs[i];
                if (settled.Contains(bomb.BombObjectName))
                {
                    continue;
                }

                ObjectID bombId = API.Authoring.GetObjectID(bomb.BombObjectName);
                ObjectID blastId = API.Authoring.GetObjectID(bomb.BlastObjectName);
                if (bombId == ObjectID.None || blastId == ObjectID.None)
                {
                    // Neither has registered yet. Say nothing and try again next tick —
                    // registration order is not something a mod can depend on.
                    continue;
                }

                Entity prefab = PugDatabase.GetPrimaryPrefabEntity(bombId, bank.databaseBankBlob);
                if (prefab == Entity.Null || !EntityManager.Exists(prefab))
                {
                    continue;
                }

                if (!EntityManager.HasComponent<IsExplosiveCD>(prefab))
                {
                    Complain(
                        "'" + bomb.BombObjectName + "' is registered as a bomb but its object " +
                        "carries no explosive data, so it will never go off. Tick 'it explodes' on " +
                        "the item and generate again.");
                    settled.Add(bomb.BombObjectName);
                    continue;
                }

                IsExplosiveCD explosive = EntityManager.GetComponentData<IsExplosiveCD>(prefab);
                if (explosive.explosionID != blastId ||
                    explosive.explosionVariation != bomb.BlastVariation)
                {
                    explosive.explosionID = blastId;
                    explosive.explosionVariation = bomb.BlastVariation;
                    EntityManager.SetComponentData(prefab, explosive);
                }

                settled.Add(bomb.BombObjectName);
                DimensionFrameworkLog.Verbose(
                    "'" + bomb.BombObjectName + "' now explodes into '" +
                    bomb.BlastObjectName + "'.");
            }
        }

        /// <summary>Says a thing once. A per-tick repeat of the same line is noise, not information.</summary>
        private void Complain(string message)
        {
            if (complained.Add(message))
            {
                DimensionFrameworkLog.Warning(message);
            }
        }
    }
}
