using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using ExpandNullforge.Portals;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Arenas
{
    /// <summary>
    /// Puts an Arena back the way its author built it, once the last player leaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE RESET RUNS ON EXIT, NEVER ON ENTRY. A wipe racing an arriving player's chunk
    /// residency would rewrite tiles under their feet; an empty arena has nobody to disturb.
    /// Dirtiness persists as a progress flag, so a server that quits mid-fight resets the
    /// arena on its next boot instead of forgetting it was ever entered.
    /// </para>
    /// <para>
    /// The order inside a reset is the correctness. Tiles are wiped only while the area is
    /// held RESIDENT by an explicit load ticket — clear commands against non-resident submaps
    /// are dropped per chunk, silently, and the arena would keep the loser's cobblestone.
    /// Entities are wiped by their object data inside the bounds, EXCEPT portals: the return
    /// portal system remembers every portal id it has ever observed and never respawns one it
    /// believes exists, so destroying the portal entity would orphan the arena's only exit
    /// for the rest of the session.
    /// </para>
    /// <para>
    /// After the wipe, the generation records are forgotten through the reset-only bypass —
    /// the public forget refuses starter areas precisely so nothing else can do this — and
    /// the starter machinery regenerates the floor exactly as it did on first travel.
    /// </para>
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DimensionArenaResetSystem : SystemBase
    {
        private const double PollIntervalSeconds = 2.0d;
        private const double SettleSeconds = 5.0d;
        private const int MaxTileClearsPerTick = 384;
        private const int ReadyDelayFrames = 2;

        private enum Phase
        {
            Idle = 0,
            Settling = 1,
            Loading = 2,
            WipingEntities = 3,
            WipingTiles = 4,
            SettlingTiles = 5,
            Regenerating = 6
        }

        private sealed class ArenaState
        {
            public Phase Phase;
            public bool Dirty;
            public bool WasOccupied;
            public double SettleUntil;
            public string LoadTicketId;
            public int2 NextTile;
            public int ReadyAfterFrame;

            /// <summary>
            /// The absolute rectangles that were actually GENERATED, captured before the
            /// forget. The reset wipes these, never the dimension's reserved bounds — a
            /// default reservation is millions of tiles of nothing, and clearing it all
            /// would hold the reset for hours.
            /// </summary>
            public List<DimensionBounds> WipeBounds = new List<DimensionBounds>();
            public int WipeBoundsIndex;
        }

        private readonly Dictionary<string, ArenaState> states =
            new Dictionary<string, ArenaState>(System.StringComparer.Ordinal);

        private double nextPollAt;
        private bool bootSweepDone;

        protected override void OnUpdate()
        {
            double now = World.Time.ElapsedTime;

            IDimensionService service;
            if (!DimensionApi.TryGetService(out service) || service == null || !service.IsReady)
            {
                return;
            }

            NullforgeDimensionService runtime = service as NullforgeDimensionService;
            if (runtime == null)
            {
                return;
            }

            // Busy phases run every tick; discovery and the occupied/empty edge poll at 2s.
            bool pollDue = now >= nextPollAt;
            if (pollDue)
            {
                nextPollAt = now + PollIntervalSeconds;
            }

            IReadOnlyList<DimensionDefinition> dimensions = service.GetDimensions();
            for (int i = 0; i < dimensions.Count; i++)
            {
                DimensionDefinition dimension = dimensions[i];
                if (!DimensionTypePolicy.For(dimension.Type).ResetsWhenEmpty)
                {
                    continue;
                }

                ArenaState state;
                if (!states.TryGetValue(dimension.Id, out state))
                {
                    state = new ArenaState();
                    states[dimension.Id] = state;

                    // Boot sweep: a dirty flag left by a quit-while-occupied session means
                    // the arena is mid-fight stale and nobody is inside to disturb.
                    DimensionProgressFlag flag;
                    if (service.TryGetProgressFlag(DirtyFlagId(dimension.Id), out flag) && flag.Value)
                    {
                        state.Dirty = true;
                    }
                }

                TickArena(runtime, dimension, state, now, pollDue);
            }

            bootSweepDone = true;
        }

        private void TickArena(
            NullforgeDimensionService service,
            DimensionDefinition dimension,
            ArenaState state,
            double now,
            bool pollDue)
        {
            if (state.Phase == Phase.Idle)
            {
                if (!pollDue && bootSweepDone)
                {
                    return;
                }

                bool occupied = service.HasTrackedPlayerInDimension(dimension.Id);
                if (occupied && !state.Dirty)
                {
                    state.Dirty = true;
                    DimensionOperationResult flagResult;
                    service.TrySetProgressFlag(
                        DirtyFlagId(dimension.Id),
                        dimension.Id,
                        "arena",
                        true,
                        "A player entered the arena.",
                        out flagResult);
                }

                if (state.WasOccupied && !occupied && state.Dirty)
                {
                    state.Phase = Phase.Settling;
                    state.SettleUntil = now + SettleSeconds;
                }
                else if (!bootSweepDone && state.Dirty && !occupied)
                {
                    state.Phase = Phase.Settling;
                    state.SettleUntil = now + SettleSeconds;
                }

                state.WasOccupied = occupied;
                return;
            }

            // A player returning mid-reset aborts everything except the wipe phases, which
            // must finish or the arena is left half-cleared; the regenerate covers them.
            if (state.Phase == Phase.Settling &&
                service.HasTrackedPlayerInDimension(dimension.Id))
            {
                state.Phase = Phase.Idle;
                state.WasOccupied = true;
                return;
            }

            switch (state.Phase)
            {
                case Phase.Settling:
                    if (now >= state.SettleUntil)
                    {
                        // What was generated is what gets wiped. Captured now, because the
                        // regenerate step forgets these records.
                        state.WipeBounds.Clear();
                        state.WipeBoundsIndex = 0;
                        DimensionBounds loadBounds = dimension.LocalBounds;
                        IReadOnlyList<DimensionGenerationStatus> statuses =
                            service.GetGenerationStatuses(dimension.Id);
                        bool first = true;
                        for (int s = 0; s < statuses.Count; s++)
                        {
                            DimensionBounds local = statuses[s].LocalBounds;
                            state.WipeBounds.Add(new DimensionBounds(
                                dimension.AbsoluteOrigin + local.Min,
                                dimension.AbsoluteOrigin + local.MaxExclusive));
                            loadBounds = first
                                ? local
                                : new DimensionBounds(
                                    math.min(loadBounds.Min, local.Min),
                                    math.max(loadBounds.MaxExclusive, local.MaxExclusive));
                            first = false;
                        }

                        if (state.WipeBounds.Count == 0)
                        {
                            // Never generated: nothing to wipe, nothing to regenerate.
                            DimensionOperationResult clearResult;
                            service.TrySetProgressFlag(
                                DirtyFlagId(dimension.Id), dimension.Id, "arena", false,
                                "Arena reset found nothing generated.", out clearResult);
                            state.Dirty = false;
                            state.Phase = Phase.Idle;
                            return;
                        }

                        DimensionLoadTicket ticket = service.RequestLoad(new DimensionLoadRequest(
                            "arena-reset:" + dimension.Id,
                            dimension.Id,
                            loadBounds,
                            true,
                            false,
                            0f,
                            "Arena reset needs the generated areas resident for the wipe."));
                        if (!ticket.IsValid)
                        {
                            DimensionFrameworkLog.Warning(
                                "Arena '" + dimension.Id + "' could not load " +
                                "for its reset: " + ticket.Message + ". Retrying.");
                            state.SettleUntil = now + SettleSeconds;
                            return;
                        }

                        state.LoadTicketId = ticket.TicketId;
                        state.Phase = Phase.Loading;
                    }

                    return;

                case Phase.Loading:
                {
                    DimensionLoadTicket ticket;
                    if (!service.TryGetLoadStatus(state.LoadTicketId, out ticket))
                    {
                        state.Phase = Phase.Settling;
                        state.SettleUntil = now + SettleSeconds;
                        return;
                    }

                    if (ticket.State == DimensionLoadState.Failed)
                    {
                        DimensionFrameworkLog.Warning(
                            "Arena '" + dimension.Id + "' reset load failed; " +
                            "retrying from the top.");
                        service.ReleaseLoadTicket(state.LoadTicketId);
                        state.Phase = Phase.Settling;
                        state.SettleUntil = now + SettleSeconds;
                        return;
                    }

                    if (ticket.State == DimensionLoadState.Resident ||
                        ticket.State == DimensionLoadState.Simulating)
                    {
                        state.Phase = Phase.WipingEntities;
                    }

                    return;
                }

                case Phase.WipingEntities:
                    WipeEntities(dimension, state.WipeBounds);
                    state.WipeBoundsIndex = 0;
                    state.NextTile = state.WipeBounds[0].Min;
                    state.Phase = Phase.WipingTiles;
                    return;

                case Phase.WipingTiles:
                    if (WipeTilesSlice(state))
                    {
                        state.ReadyAfterFrame = UnityEngine.Time.frameCount + ReadyDelayFrames;
                        state.Phase = Phase.SettlingTiles;
                    }

                    return;

                case Phase.SettlingTiles:
                    if (UnityEngine.Time.frameCount >= state.ReadyAfterFrame)
                    {
                        state.Phase = Phase.Regenerating;
                    }

                    return;

                case Phase.Regenerating:
                {
                    service.ForgetGeneratedAreasForReset(dimension.Id);

                    DimensionOperationResult flagResult;
                    service.TrySetProgressFlag(
                        DirtyFlagId(dimension.Id),
                        dimension.Id,
                        "arena",
                        false,
                        "Arena reset finished.",
                        out flagResult);
                    service.TryRemoveProgressFlag(VictoryFlagId(dimension.Id), out flagResult);
                    DisarmVictoryPortals(dimension.Id);

                    service.ReleaseLoadTicket(state.LoadTicketId);
                    state.LoadTicketId = null;
                    state.Dirty = false;
                    state.WasOccupied = false;
                    state.Phase = Phase.Idle;

                    DimensionFrameworkLog.Info(
                        "Arena '" + dimension.Id + "' reset: entities and " +
                        "tiles wiped, floor queued to regenerate.");
                    return;
                }
            }
        }

        /// <summary>
        /// Destroys everything a fight leaves behind: mobs, dropped loot, placed objects.
        /// </summary>
        /// <remarks>
        /// Selection is by object data plus a transform inside the bounds — the identity every
        /// player-visible thing carries. Players and portals are excluded by component; our
        /// bookkeeping entities never had object data to begin with.
        /// </remarks>
        private void WipeEntities(DimensionDefinition dimension, List<DimensionBounds> wipeBounds)
        {
            EntityQuery query = GetEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());

            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            using NativeArray<LocalTransform> transforms =
                query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                float2 position = new float2(transforms[i].Position.x, transforms[i].Position.z);
                bool inside = false;
                for (int b = 0; b < wipeBounds.Count && !inside; b++)
                {
                    inside = wipeBounds[b].Contains(position);
                }

                if (!inside)
                {
                    continue;
                }

                if (EntityManager.HasComponent<PlayerGhost>(entities[i]) ||
                    EntityManager.HasComponent<DimensionPortalCD>(entities[i]))
                {
                    continue;
                }

                EntityManager.DestroyEntity(entities[i]);
            }
        }

        /// <summary>One tick's worth of tile clears. True when every generated area is queued.</summary>
        private bool WipeTilesSlice(ArenaState state)
        {
            EntityQuery bufferQuery = GetEntityQuery(ComponentType.ReadWrite<TileUpdateBuffer>());
            if (bufferQuery.IsEmptyIgnoreFilter)
            {
                return false;
            }

            DynamicBuffer<TileUpdateBuffer> tileUpdates =
                EntityManager.GetBuffer<TileUpdateBuffer>(bufferQuery.GetSingletonEntity());

            int cleared = 0;
            while (state.WipeBoundsIndex < state.WipeBounds.Count && cleared < MaxTileClearsPerTick)
            {
                DimensionBounds bounds = state.WipeBounds[state.WipeBoundsIndex];
                int2 cursor = state.NextTile;
                while (cursor.y < bounds.MaxExclusive.y && cleared < MaxTileClearsPerTick)
                {
                    tileUpdates.Add(new TileUpdateBuffer
                    {
                        command = TileUpdateBuffer.Command.Clear,
                        position = cursor,
                        tile = default
                    });

                    cleared++;
                    cursor.x++;
                    if (cursor.x >= bounds.MaxExclusive.x)
                    {
                        cursor.x = bounds.Min.x;
                        cursor.y++;
                    }
                }

                state.NextTile = cursor;
                if (cursor.y >= bounds.MaxExclusive.y)
                {
                    state.WipeBoundsIndex++;
                    if (state.WipeBoundsIndex < state.WipeBounds.Count)
                    {
                        state.NextTile = state.WipeBounds[state.WipeBoundsIndex].Min;
                    }
                }
            }

            return state.WipeBoundsIndex >= state.WipeBounds.Count;
        }

        private static void DisarmVictoryPortals(string dimensionId)
        {
            for (int i = 0; i < DimensionReturnPortalSpawnRegistry.Count; i++)
            {
                DimensionReturnPortalSpawnDefinition definition;
                if (DimensionReturnPortalSpawnRegistry.TryGet(i, out definition) &&
                    definition.ArmedByVictory &&
                    string.Equals(definition.SourceDimensionId, dimensionId, System.StringComparison.Ordinal))
                {
                    DimensionReturnPortalSpawnRegistry.Disarm(definition.PortalId);
                }
            }
        }

        internal static string DirtyFlagId(string dimensionId)
        {
            return dimensionId + ".arena.dirty";
        }

        internal static string VictoryFlagId(string dimensionId)
        {
            return dimensionId + ".arena.victory";
        }
    }
}
