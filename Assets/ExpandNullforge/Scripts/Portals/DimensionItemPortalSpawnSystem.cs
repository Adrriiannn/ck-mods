using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Interaction;
using PugMod;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace ExpandNullforge.Portals
{
    /// <summary>Marks a temporary item-portal entity and when it should close/despawn.</summary>
    public struct DimensionItemPortalLifetimeCD : IComponentData
    {
        /// <summary>When to begin closing: deactivate so the swirls and light switch off before removal.</summary>
        public double CloseAt;

        /// <summary>When to remove the portal entity entirely.</summary>
        public double DespawnAt;
    }

    /// <summary>
    /// Spawns the instantaneous item portal (V2). When the item-use hook enqueues a request
    /// (<see cref="DimensionItemPortalRegistry.EnqueueSpawn"/>), this server system places a
    /// temporary portal in a FREE CARDINAL tile next to the player (never diagonal), configures it to
    /// travel to the item's target dimension, and gives it a lifetime. The player interacts with it
    /// (E) exactly like any other portal — it does NOT teleport on use of the item. When the lifetime
    /// ends the portal despawns. Opening a portal starts the shared cross-mod cooldown so item portals
    /// cannot be spammed.
    ///
    /// The spawn reuses the same entity pattern as <see cref="DimensionReturnPortalSpawnSystem"/>. The
    /// forward "open" animation is driven by the portal visual when the portal becomes active; the
    /// reverse "close" animation before despawn is a visual follow-up.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DimensionPortalHydrationSystem))]
    public partial class DimensionItemPortalSpawnSystem : SystemBase
    {
        // How long before despawn the portal switches off so the client can play the closing
        // animation (the 5-frame reverse opening runs 0.5s at 10 fps) before the entity is
        // destroyed; the extra quarter second absorbs replication latency. The visual reacts to
        // DimensionPortalCD.Active flipping off, which starts the close.
        private const double CloseLeadSeconds = 0.75d;

        // A queued reopen-on-return waits for its overworld tile's area to load (the returning
        // player is teleporting there, which loads it); if that never happens it is dropped.
        private const double ReopenTimeoutSeconds = 30d;

        private static readonly int2[] Cardinals =
        {
            new int2(1, 0),
            new int2(-1, 0),
            new int2(0, 1),
            new int2(0, -1)
        };

        private EntityQuery databaseQuery;
        private EntityQuery networkTimeQuery;
        private EntityQuery tickRateQuery;

        protected override void OnCreate()
        {
            databaseQuery = GetEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
            // Classic queries, deliberately not SystemAPI: the mod loader's script pipeline
            // cannot rewrite SystemAPI helpers with out-parameters, which makes them throw at
            // runtime ("No suitable code replacement generated").
            networkTimeQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            tickRateQuery = GetEntityQuery(ComponentType.ReadOnly<ClientServerTickRate>());
            RequireForUpdate(databaseQuery);
        }

        protected override void OnUpdate()
        {
            double now = World.Time.ElapsedTime;

            UpdateItemPortalLifetimes(now);
            ProcessReopenSpawns(now);

            if (!DimensionItemPortalRegistry.HasPendingSpawns)
            {
                return;
            }

            IDimensionService service;
            bool hasService = DimensionApi.TryGetService(out service) && service != null && service.IsReady;

            while (DimensionItemPortalRegistry.TryDequeueSpawn(out DimensionItemPortalSpawnRequest request))
            {
                if (DimensionItemPortalRegistry.IsOnGlobalCooldown(now))
                {
                    // A portal is already active/cooling down — drop this request rather than queue it.
                    continue;
                }

                if (!hasService ||
                    !DimensionItemPortalRegistry.TryGet(request.ItemObjectName, out DimensionItemPortalConfig config))
                {
                    continue;
                }

                // The game's brief global use-lock: every vanilla item use starts a short
                // (≤0.5s) PlayerAttackCooldownCD on the player, and every slot's use path
                // honors it. Respecting it here means the portal item cannot fire in the same
                // breath as another item — matching vanilla feel.
                if (IsPlayerBriefUseLockActive(request.Player))
                {
                    continue;
                }

                // Context-aware direction: using the item INSIDE its own target dimension opens a
                // portal home (the ".back" definition, dimension -> overworld) instead of a pointless
                // portal to the dimension the player is already in.
                bool backward =
                    service.TryGetDimensionAtAbsolute(
                        new float2(request.PlayerTile.x, request.PlayerTile.y),
                        out DimensionDefinition currentDimension) &&
                    string.Equals(
                        currentDimension.Id,
                        config.ToDimensionId,
                        System.StringComparison.Ordinal);

                if (TrySpawnItemPortal(request.PlayerTile, config, now, backward))
                {
                    DimensionItemPortalRegistry.StartGlobalCooldown(now, config.DurationSeconds);
                    StartPlayerBriefUseLock(request.Player);
                }
            }
        }

        /// <summary>
        /// Re-opens instant portals on the exact tile a returning player originally left from.
        /// Queued by the activation system when a to-overworld travel is accepted; each request
        /// waits for its tile's area to load (the arriving player loads it) and is dropped after
        /// <see cref="ReopenTimeoutSeconds"/>. Reopens bypass the global cooldown check — they are
        /// the tail end of a travel that already paid it — but restart it, keeping the
        /// one-portal-at-a-time rule intact.
        /// </summary>
        private void ProcessReopenSpawns(double now)
        {
            if (!DimensionItemPortalRegistry.HasPendingReopenSpawns)
            {
                return;
            }

            int pending = DimensionItemPortalRegistry.PendingReopenSpawnCount;
            for (int i = 0; i < pending; i++)
            {
                if (!DimensionItemPortalRegistry.TryDequeueReopenSpawn(
                    out DimensionItemPortalReopenRequest request))
                {
                    break;
                }

                if (now - request.RequestedAt > ReopenTimeoutSeconds)
                {
                    DimensionFrameworkLog.Verbose(
                        "[ExpandNullforge] Dropped item-portal reopen at (" +
                        request.Tile.x + ", " + request.Tile.y +
                        ") — the area never loaded.");
                    continue;
                }

                if (!DimensionItemPortalRegistry.TryGet(
                    request.ItemObjectName, out DimensionItemPortalConfig config))
                {
                    continue;
                }

                TileAccessor tileAccessor =
                    new TileAccessor(World.GetExistingSystemManaged<PugQuerySystem>());
                if (!tileAccessor.IsInitialized(request.Tile))
                {
                    DimensionItemPortalRegistry.RequeueReopenSpawn(request);
                    continue;
                }

                if (SpawnItemPortalAt(request.Tile, config, now, false))
                {
                    DimensionItemPortalRegistry.StartGlobalCooldown(now, config.DurationSeconds);
                }
                else
                {
                    // Entity creation can fail transiently while the area streams in; retry
                    // until the timeout above gives up.
                    DimensionItemPortalRegistry.RequeueReopenSpawn(request);
                }
            }
        }

        private bool TrySpawnItemPortal(
            int2 playerTile,
            DimensionItemPortalConfig config,
            double now,
            bool backward)
        {
            int2 tile = ResolveFreeCardinalTile(playerTile);
            return SpawnItemPortalAt(tile, config, now, backward);
        }

        // Matches the vanilla per-use lock magnitude: placement uses 0.25–0.4s, and
        // StartCooldownForItem clamps the shared portion to 0.5s.
        private const float BriefUseLockSeconds = 0.4f;

        /// <summary>True while the player's native brief use-lock (started by any item use) runs.</summary>
        private bool IsPlayerBriefUseLockActive(Entity player)
        {
            if (player == Entity.Null ||
                !EntityManager.Exists(player) ||
                !EntityManager.HasComponent<PlayerAttackCooldownCD>(player) ||
                !TryGetTickContext(out NetworkTick currentTick, out _))
            {
                return false;
            }

            PlayerAttackCooldownCD attackCooldown =
                EntityManager.GetComponentData<PlayerAttackCooldownCD>(player);
            return EquipmentSlot.IsAttackOnCooldown(ref attackCooldown, in currentTick);
        }

        /// <summary>
        /// Restarts the native brief use-lock after a successful portal spawn, so other items go
        /// on the same short global cooldown a vanilla item use would trigger.
        /// </summary>
        private void StartPlayerBriefUseLock(Entity player)
        {
            if (player == Entity.Null ||
                !EntityManager.Exists(player) ||
                !EntityManager.HasComponent<PlayerAttackCooldownCD>(player) ||
                !TryGetTickContext(out NetworkTick currentTick, out uint tickRate))
            {
                return;
            }

            PlayerAttackCooldownCD attackCooldown =
                EntityManager.GetComponentData<PlayerAttackCooldownCD>(player);
            attackCooldown.cooldown.Start(currentTick, BriefUseLockSeconds, tickRate);
            EntityManager.SetComponentData(player, attackCooldown);
        }

        private bool TryGetTickContext(out NetworkTick currentTick, out uint tickRate)
        {
            currentTick = default(NetworkTick);
            tickRate = 20u;
            if (!networkTimeQuery.TryGetSingleton(out NetworkTime networkTime) ||
                !networkTime.ServerTick.IsValid)
            {
                return false;
            }

            currentTick = networkTime.ServerTick;
            if (tickRateQuery.TryGetSingleton(out ClientServerTickRate clientServerTickRate))
            {
                clientServerTickRate.ResolveDefaults();
                tickRate = (uint)clientServerTickRate.SimulationTickRate;
            }

            return true;
        }

        private bool SpawnItemPortalAt(
            int2 tile,
            DimensionItemPortalConfig config,
            double now,
            bool backward)
        {
            ObjectID portalObjectId = API.Authoring.GetObjectID(config.PortalObjectName);
            if (portalObjectId == ObjectID.None)
            {
                DimensionFrameworkLog.Warning(
                    "[ExpandNullforge] Item portal could not resolve portal object '" +
                    config.PortalObjectName + "'.");
                return false;
            }

            PugDatabase.DatabaseBankCD databaseBank = databaseQuery.GetSingleton<PugDatabase.DatabaseBankCD>();
            Entity portalEntity = EntityUtility.CreateEntity(
                World,
                new Vector3(tile.x, 0f, tile.y),
                portalObjectId,
                1,
                databaseBank.databaseBankBlob,
                0);

            if (portalEntity == Entity.Null || !EntityManager.Exists(portalEntity))
            {
                return false;
            }

            ConfigureItemPortal(portalEntity, config, tile, backward);
            double despawnAt = now + config.DurationSeconds;
            EntityManager.AddComponentData(portalEntity, new DimensionItemPortalLifetimeCD
            {
                CloseAt = math.max(now, despawnAt - CloseLeadSeconds),
                DespawnAt = despawnAt
            });
            return true;
        }

        private int2 ResolveFreeCardinalTile(int2 playerTile)
        {
            TileAccessor tileAccessor = new TileAccessor(World.GetExistingSystemManaged<PugQuerySystem>());
            for (int i = 0; i < Cardinals.Length; i++)
            {
                int2 candidate = playerTile + Cardinals[i];
                if (!IsBlocked(tileAccessor, candidate))
                {
                    return candidate;
                }
            }

            // Everything around is blocked — fall back to the first cardinal so the portal still spawns.
            return playerTile + Cardinals[0];
        }

        private static bool IsBlocked(TileAccessor tileAccessor, int2 position)
        {
            if (!tileAccessor.IsInitialized(position))
            {
                return false;
            }

            NativeArray<TileCD> tiles = tileAccessor.Get(position, Allocator.Temp);
            bool blocked = false;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (TileTypeUtility.IsBlockingTile(tiles[i].tileType))
                {
                    blocked = true;
                    break;
                }
            }

            tiles.Dispose();
            return blocked;
        }

        private void ConfigureItemPortal(
            Entity portalEntity,
            DimensionItemPortalConfig config,
            int2 tile,
            bool backward)
        {
            // Backward portals carry the ".back" definition (dimension -> overworld). The overworld
            // needs no generation, and the actual landing comes from the player's tracked overworld
            // exit point, so no generated-area gate applies.
            string portalId = backward
                ? config.PortalId + DimensionItemPortalRegistry.BackPortalIdSuffix
                : config.PortalId;
            string targetDimensionId = backward ? DimensionIds.Overworld : config.ToDimensionId;

            DimensionPortalCD portal = new DimensionPortalCD
            {
                PortalId = ToFixed64(portalId),
                TargetDimensionId = ToFixed64(targetDimensionId),
                TargetLocalX = 0f,
                TargetLocalY = 0f,
                ActivationCooldownSeconds = 0f,
                ActivationChargeSeconds = 0f,
                ActivationProgress = 1f,
                ActivationStartedAt = -1d,
                RequireGeneratedArea = backward ? (byte)0 : (byte)1,
                AllowFallbackPosition = 1,
                Active = 1,
                Charged = 1,
                Interactable = 1
            };

            if (EntityManager.HasComponent<DimensionPortalCD>(portalEntity))
            {
                EntityManager.SetComponentData(portalEntity, portal);
            }
            else
            {
                EntityManager.AddComponentData(portalEntity, portal);
            }

            if (!EntityManager.HasBuffer<DimensionPortalActivationBuffer>(portalEntity))
            {
                EntityManager.AddBuffer<DimensionPortalActivationBuffer>(portalEntity);
            }

            if (!EntityManager.HasComponent<DimensionPortalHydratedCD>(portalEntity))
            {
                EntityManager.AddComponent<DimensionPortalHydratedCD>(portalEntity);
            }

            if (!EntityManager.HasBuffer<TriggerUseInteractionBuffer>(portalEntity))
            {
                EntityManager.AddBuffer<TriggerUseInteractionBuffer>(portalEntity);
            }

            if (!EntityManager.HasBuffer<TriggerExitInteractionBuffer>(portalEntity))
            {
                EntityManager.AddBuffer<TriggerExitInteractionBuffer>(portalEntity);
            }

            // Unbreakable while it exists — it is transient and removed on its own timer.
            if (!EntityManager.HasComponent<IndestructibleCD>(portalEntity))
            {
                EntityManager.AddComponent<IndestructibleCD>(portalEntity);
            }

            if (EntityManager.HasComponent<LocalTransform>(portalEntity))
            {
                LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(portalEntity);
                transform.Position = new float3(tile.x, 0f, tile.y);
                transform.Rotation = quaternion.identity;
                EntityManager.SetComponentData(portalEntity, transform);
            }
        }

        private void UpdateItemPortalLifetimes(double now)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            // The lifetime component is runtime-only: an instant portal whose area unloads
            // (its user travelled into the dimension) is saved WITHOUT it and would come back
            // immortal on reload. Cull any registered instant portal entity that has no
            // lifetime instead of letting it linger forever.
            foreach (var (objectData, entity) in
                SystemAPI.Query<RefRO<ObjectDataCD>>()
                    .WithAll<DimensionPortalCD>()
                    .WithNone<DimensionItemPortalLifetimeCD>()
                    .WithEntityAccess())
            {
                if (DimensionItemPortalRegistry.IsInstantPortalObjectId(objectData.ValueRO.objectID))
                {
                    ecb.DestroyEntity(entity);
                }
            }

            foreach (var (lifetime, entity) in
                SystemAPI.Query<RefRO<DimensionItemPortalLifetimeCD>>().WithEntityAccess())
            {
                if (now >= lifetime.ValueRO.DespawnAt)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Closing window: switch the portal off so its swirls and light fade out (the visual
                // follows DimensionPortalCD.Active) and it can no longer be entered, then let it despawn.
                if (now >= lifetime.ValueRO.CloseAt &&
                    EntityManager.HasComponent<DimensionPortalCD>(entity))
                {
                    DimensionPortalCD portal = EntityManager.GetComponentData<DimensionPortalCD>(entity);
                    if (portal.Active != 0 || portal.Interactable != 0)
                    {
                        portal.Active = 0;
                        portal.Interactable = 0;
                        EntityManager.SetComponentData(entity, portal);
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private static FixedString64Bytes ToFixed64(string value)
        {
            FixedString64Bytes result = default(FixedString64Bytes);
            if (!string.IsNullOrEmpty(value))
            {
                result.CopyFromTruncated(value);
            }

            return result;
        }
    }
}
