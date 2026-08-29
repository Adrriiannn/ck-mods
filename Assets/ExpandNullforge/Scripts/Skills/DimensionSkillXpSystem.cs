using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace ExpandNullforge.Skills
{
    /// <summary>
    /// Gives a player skill experience for killing something a mod made worth killing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A SYSTEM OF OUR OWN AND NOT A PATCH. Every place Core Keeper awards experience is inside
    /// a Burst-compiled job, where a Harmony patch never arrives. What it does leave in the open is
    /// the receiving end: <c>AddSkillValueSystem</c> takes one entity carrying
    /// <c>AddSkillValueCD</c> and does the whole job — the level cap, the per-level bonus, the
    /// condition that goes with it. Writing one of those entities is a supported thing to do;
    /// <c>PlayerController.AddSkill</c> is a public method that does exactly and only that.
    /// </para>
    /// <para>
    /// WHAT THE QUERY HAS TO SAY, in full. The chunk query is
    /// <c>ObjectDataCD</c> (which kind of thing died), <c>EntityDestroyedCD</c> (that it died) and
    /// <c>Simulate</c>. Then, per entity, <c>KilledByPlayerCD</c> is read the way
    /// <c>DropLootSystem.DropSelfJob</c> reads it (<c>ck-db\Pug.Other\DropLootSystem.cs:4417</c>):
    /// <c>TryGetComponent</c> <b>and</b> <c>IsComponentEnabled</c>, both. The component is
    /// <c>IEnableableComponent</c> and <c>HealthConverter</c> adds it <em>disabled</em> to
    /// everything that has health, so asking only whether it is present matches every corpse in the
    /// world including the ones nobody killed.
    /// </para>
    /// <para>
    /// ONCE PER DEATH, AND THE COMPONENT DOES NOT SAY THAT ON ITS OWN. <c>EntityDestroyedCD</c> is
    /// enabled and then stays enabled while a destroy timer runs down, so it matches for many ticks
    /// and not one. Vanilla's loot job handles that with a second pair of components it owns; this
    /// keeps a small list of what it has already paid for instead, which needs no component added
    /// to anybody's entity and so cannot pull a creature into a query it does not belong in. The
    /// list is emptied of entities the world no longer has, every tick it runs.
    /// </para>
    /// <para>
    /// SERVER ONLY, because the buffers being written are the server's authority — the same reason
    /// <c>PlayerController.AddSkill</c> returns immediately when it is not the server.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ONE BOOLEAN. <c>RequireForUpdate</c> cannot express "our registry has
    /// something in it", so the check is the first line of the update, the same pattern
    /// <c>DimensionHazardConditionSystem</c> uses.
    /// </para>
    /// <para>
    /// TWO HONEST LIMITS. Experience given this way does not count towards the game's achievements
    /// — those are reached only from vanilla's own writers — and the amount is a flat number rather
    /// than one that scales with how deep the area is, because that scaling lives inside the
    /// Bursted jobs too.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(SetEntitiesDestroyedSystem))]
    public partial class DimensionSkillXpSystem : SystemBase
    {
        private EntityQuery deadQuery;

        /// <summary>Everything already paid for, so a death that lingers is not paid for twice.</summary>
        private readonly HashSet<Entity> alreadyAwarded = new HashSet<Entity>();

        private readonly List<Entity> goneSinceLastTime = new List<Entity>();

        protected override void OnCreate()
        {
            deadQuery = GetEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<EntityDestroyedCD>(),
                ComponentType.ReadOnly<Simulate>());
        }

        protected override void OnUpdate()
        {
            if (!DimensionSkillXpRegistry.HasAny)
            {
                return;
            }

            // A mod's object has no number until the game has handed one out, so the names are
            // turned into numbers the first tick a world knows them, and not before.
            if (!DimensionSkillXpRegistry.EnsureResolved())
            {
                return;
            }

            ForgetWhatTheWorldNoLongerHas();

            NativeArray<Entity> dead = deadQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < dead.Length; i++)
                {
                    Entity entity = dead[i];
                    if (alreadyAwarded.Contains(entity))
                    {
                        continue;
                    }

                    if (!SystemAPI.IsComponentEnabled<EntityDestroyedCD>(entity))
                    {
                        continue;
                    }

                    // Both halves, exactly as vanilla's own loot job reads it.
                    if (!EntityManager.HasComponent<KilledByPlayerCD>(entity) ||
                        !EntityManager.IsComponentEnabled<KilledByPlayerCD>(entity))
                    {
                        continue;
                    }

                    KilledByPlayerCD killedBy = EntityManager.GetComponentData<KilledByPlayerCD>(entity);
                    if (killedBy.playerEntity == Entity.Null)
                    {
                        continue;
                    }

                    ObjectDataCD objectData = EntityManager.GetComponentData<ObjectDataCD>(entity);
                    DimensionSkillXpRegistry.Award award;
                    if (!DimensionSkillXpRegistry.TryGet(objectData.objectID, out award))
                    {
                        continue;
                    }

                    alreadyAwarded.Add(entity);

                    // One entity carrying one component, which is the whole of what
                    // PlayerController.AddSkill creates and the whole of what AddSkillValueSystem
                    // asks for. Anything else on it would be read by nothing.
                    Entity command = EntityManager.CreateEntity();
                    EntityManager.AddComponentData(command, new AddSkillValueCD
                    {
                        entity = killedBy.playerEntity,
                        skillID = award.Skill,
                        amount = award.Amount
                    });
                }
            }
            finally
            {
                dead.Dispose();
            }
        }

        protected override void OnDestroy()
        {
            alreadyAwarded.Clear();
        }

        private void ForgetWhatTheWorldNoLongerHas()
        {
            if (alreadyAwarded.Count == 0)
            {
                return;
            }

            goneSinceLastTime.Clear();
            foreach (Entity entity in alreadyAwarded)
            {
                if (!EntityManager.Exists(entity))
                {
                    goneSinceLastTime.Add(entity);
                }
            }

            for (int i = 0; i < goneSinceLastTime.Count; i++)
            {
                alreadyAwarded.Remove(goneSinceLastTime[i]);
            }
        }
    }
}
