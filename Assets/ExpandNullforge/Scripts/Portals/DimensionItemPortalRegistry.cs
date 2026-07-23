using System.Collections.Generic;
using PugMod;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Portals
{
    /// <summary>
    /// One instantaneous-item-portal definition: the custom item that spawns a temporary portal, the
    /// portal object it spawns, the link it opens, and how long it stays open. Populated by the
    /// generated bootstrap from the V2 access rule.
    /// </summary>
    public readonly struct DimensionItemPortalConfig
    {
        public DimensionItemPortalConfig(
            string itemObjectName,
            string portalObjectName,
            string portalId,
            string toDimensionId,
            float durationSeconds)
        {
            ItemObjectName = itemObjectName ?? string.Empty;
            PortalObjectName = portalObjectName ?? string.Empty;
            PortalId = portalId ?? string.Empty;
            ToDimensionId = toDimensionId ?? string.Empty;
            DurationSeconds = Mathf.Max(1f, durationSeconds);
        }

        public string ItemObjectName { get; }
        public string PortalObjectName { get; }
        public string PortalId { get; }
        public string ToDimensionId { get; }
        public float DurationSeconds { get; }
    }

    /// <summary>A queued request to spawn an item portal, produced by the item-use hook.</summary>
    public struct DimensionItemPortalSpawnRequest
    {
        public int2 PlayerTile;
        public string ItemObjectName;

        /// <summary>
        /// The using player, so the spawn can honor and restart the game's brief global
        /// use-lock (PlayerAttackCooldownCD) like any vanilla item use.
        /// </summary>
        public Entity Player;
    }

    /// <summary>
    /// A queued request to re-open an instant portal on the exact tile it previously occupied,
    /// produced when a player who entered a dimension through that portal travels back home.
    /// </summary>
    public struct DimensionItemPortalReopenRequest
    {
        public int2 Tile;
        public string ItemObjectName;
        public double RequestedAt;
    }

    /// <summary>
    /// Registry of instantaneous item portals (V2), keyed by the item's object name, plus the shared
    /// use cooldown and the spawn-request queue.
    ///
    /// The cooldown is deliberately a single framework-level static, so it is shared by EVERY item
    /// portal across EVERY dimension mod that uses this framework — opening one item portal puts them
    /// all on cooldown, which is exactly the "no spamming different portals" rule. (Consumer mods
    /// reference the one ExpandNullforge assembly, so they share this static.)
    ///
    /// Note: the spawn queue is a process-shared static — it carries the item-use event from the hook
    /// to the server spawn system. That is complete in single-player / host play (client and server
    /// share the process); a dedicated-server client would additionally need a client→server RPC,
    /// which is a follow-up.
    /// </summary>
    public static class DimensionItemPortalRegistry
    {
        /// <summary>
        /// Suffix of the generated "back home" twin of an item's portal id. Using the item INSIDE
        /// its own target dimension spawns the portal under this id instead, whose registered
        /// definition points dimension -> overworld, so travel lands at the player's tracked
        /// overworld exit point.
        /// </summary>
        public const string BackPortalIdSuffix = ".back";

        private static readonly Dictionary<string, DimensionItemPortalConfig> Configs =
            new Dictionary<string, DimensionItemPortalConfig>();

        // Lazily-resolved ObjectID -> item name map, so the item-use hook (which only has the used
        // item's ObjectID) can recognize a registered portal item without a per-frame name lookup.
        private static readonly Dictionary<ObjectID, string> ItemObjectIdToName =
            new Dictionary<ObjectID, string>();
        private static int itemObjectIdResolvedForConfigCount = -1;

        private static readonly Queue<DimensionItemPortalSpawnRequest> PendingSpawns =
            new Queue<DimensionItemPortalSpawnRequest>();

        // The global "any item portal was just opened" cooldown, in server time seconds.
        private static double globalCooldownUntil;

        public static void Register(
            string itemObjectName,
            string portalObjectName,
            string portalId,
            string toDimensionId,
            float durationSeconds)
        {
            if (string.IsNullOrEmpty(itemObjectName))
            {
                return;
            }

            Configs[itemObjectName] = new DimensionItemPortalConfig(
                itemObjectName, portalObjectName, portalId, toDimensionId, durationSeconds);
        }

        public static bool TryGet(string itemObjectName, out DimensionItemPortalConfig config)
        {
            config = default(DimensionItemPortalConfig);
            return !string.IsNullOrEmpty(itemObjectName) && Configs.TryGetValue(itemObjectName, out config);
        }

        public static bool IsRegistered(string itemObjectName)
        {
            return !string.IsNullOrEmpty(itemObjectName) && Configs.ContainsKey(itemObjectName);
        }

        public static IReadOnlyCollection<string> RegisteredItemNames => Configs.Keys;

        // Lazily-resolved set of the instant portal OBJECT ids (the temporary portal entities the
        // items spawn), mirroring the item-id map above.
        private static readonly HashSet<ObjectID> InstantPortalObjectIds = new HashSet<ObjectID>();
        private static int instantObjectIdResolvedForConfigCount = -1;

        /// <summary>
        /// True when <paramref name="objectId"/> is one of the registered instant portal objects.
        /// The client-side portal view uses this at bind time: pooled portal visuals are shared
        /// across portal prefabs, so the frameless instant look must be applied per entity from
        /// its own ObjectDataCD (available immediately at view creation) rather than trusted to
        /// the prefab the pool happened to hand over.
        /// </summary>
        public static bool IsInstantPortalObjectId(ObjectID objectId)
        {
            if (objectId == ObjectID.None || Configs.Count == 0)
            {
                return false;
            }

            if (instantObjectIdResolvedForConfigCount != Configs.Count)
            {
                InstantPortalObjectIds.Clear();
                foreach (DimensionItemPortalConfig config in Configs.Values)
                {
                    ObjectID id = API.Authoring.GetObjectID(config.PortalObjectName);
                    if (id != ObjectID.None)
                    {
                        InstantPortalObjectIds.Add(id);
                    }
                }

                instantObjectIdResolvedForConfigCount = Configs.Count;
            }

            return InstantPortalObjectIds.Contains(objectId);
        }

        /// <summary>
        /// Resolves the used item's ObjectID to its registered config, for the item-use hook. Builds
        /// (and rebuilds when new configs are registered) a cached ObjectID -> name map via the object
        /// database, so the hook stays a cheap dictionary lookup on the hot equipment-update path.
        /// </summary>
        public static bool TryGetConfigByObjectId(ObjectID itemObjectId, out DimensionItemPortalConfig config)
        {
            config = default(DimensionItemPortalConfig);
            if (itemObjectId == ObjectID.None || Configs.Count == 0)
            {
                return false;
            }

            EnsureItemObjectIdMap();
            return ItemObjectIdToName.TryGetValue(itemObjectId, out string itemObjectName) &&
                   Configs.TryGetValue(itemObjectName, out config);
        }

        /// <summary>
        /// Resolves a travel portal id back to its item-portal config: matches either the forward id
        /// (<see cref="DimensionItemPortalConfig.PortalId"/>) or its
        /// <see cref="BackPortalIdSuffix"/> twin, reporting which direction matched. Used by the
        /// portal activation hook to recognize instant-portal travel.
        /// </summary>
        public static bool TryGetConfigByPortalId(
            string portalId,
            out DimensionItemPortalConfig config,
            out bool backward)
        {
            config = default(DimensionItemPortalConfig);
            backward = false;
            if (string.IsNullOrEmpty(portalId) || Configs.Count == 0)
            {
                return false;
            }

            foreach (DimensionItemPortalConfig candidate in Configs.Values)
            {
                if (string.IsNullOrEmpty(candidate.PortalId))
                {
                    continue;
                }

                if (string.Equals(portalId, candidate.PortalId, System.StringComparison.Ordinal))
                {
                    config = candidate;
                    return true;
                }

                if (string.Equals(
                    portalId,
                    candidate.PortalId + BackPortalIdSuffix,
                    System.StringComparison.Ordinal))
                {
                    config = candidate;
                    backward = true;
                    return true;
                }
            }

            return false;
        }

        private static void EnsureItemObjectIdMap()
        {
            if (itemObjectIdResolvedForConfigCount == Configs.Count)
            {
                return;
            }

            ItemObjectIdToName.Clear();
            foreach (KeyValuePair<string, DimensionItemPortalConfig> entry in Configs)
            {
                ObjectID id = API.Authoring.GetObjectID(entry.Key);
                if (id != ObjectID.None)
                {
                    ItemObjectIdToName[id] = entry.Key;
                }
            }

            itemObjectIdResolvedForConfigCount = Configs.Count;
        }

        /// <summary>Queues a spawn from the item-use hook. Ignored while on the shared cooldown.</summary>
        public static void EnqueueSpawn(int2 playerTile, string itemObjectName, Entity player)
        {
            if (string.IsNullOrEmpty(itemObjectName) || !IsRegistered(itemObjectName))
            {
                return;
            }

            // A held right-click re-fires the use hook every tick; one pending spawn is enough (and the
            // shared cooldown gates the rest), so never let the queue grow.
            if (PendingSpawns.Count > 0)
            {
                return;
            }

            PendingSpawns.Enqueue(new DimensionItemPortalSpawnRequest
            {
                PlayerTile = playerTile,
                ItemObjectName = itemObjectName,
                Player = player
            });
        }

        public static bool TryDequeueSpawn(out DimensionItemPortalSpawnRequest request)
        {
            if (PendingSpawns.Count > 0)
            {
                request = PendingSpawns.Dequeue();
                return true;
            }

            request = default(DimensionItemPortalSpawnRequest);
            return false;
        }

        public static bool HasPendingSpawns => PendingSpawns.Count > 0;

        // ---- Reopen-on-return ("keep the immersion alive") ----------------------------------
        // When a player travels INTO a dimension through an instant portal, the portal's exact
        // tile is remembered per player. When that player later travels back to the overworld
        // (return portal, backward item portal — any portal whose target is the overworld), the
        // record is consumed and a fresh instant portal re-opens on that same tile for the full
        // duration, so the player arrives back through "the same" temporary portal they left by.

        private struct PendingReopenRecord
        {
            public int2 Tile;
            public string ItemObjectName;
            public double RecordedAt;
        }

        // Keyed by the player ENTITY: the server world survives dimension travel, so the entity is
        // a stable per-player key for the minutes-scale life of a record. Records are in-memory
        // only and expire, so a stale entity key is at worst a skipped courtesy respawn.
        private static readonly Dictionary<Entity, PendingReopenRecord> PendingReopens =
            new Dictionary<Entity, PendingReopenRecord>();

        private static readonly Queue<DimensionItemPortalReopenRequest> PendingReopenSpawns =
            new Queue<DimensionItemPortalReopenRequest>();

        // A record older than this is ignored on return — it survives deaths and detours, but a
        // half-hour-old portal tile is no longer "where I just left from".
        private const double PendingReopenExpirySeconds = 1800d;

        /// <summary>Remembers, per player, the instant portal tile they entered a dimension through.</summary>
        public static void RecordPendingReopen(
            Entity player,
            int2 tile,
            string itemObjectName,
            double now)
        {
            if (player == Entity.Null || string.IsNullOrEmpty(itemObjectName))
            {
                return;
            }

            PendingReopens[player] = new PendingReopenRecord
            {
                Tile = tile,
                ItemObjectName = itemObjectName,
                RecordedAt = now
            };
        }

        /// <summary>
        /// Consumes the player's entry record if present and fresh. Expired records are discarded.
        /// </summary>
        public static bool TryConsumePendingReopen(
            Entity player,
            double now,
            out int2 tile,
            out string itemObjectName)
        {
            tile = default(int2);
            itemObjectName = null;
            if (player == Entity.Null ||
                !PendingReopens.TryGetValue(player, out PendingReopenRecord record))
            {
                return false;
            }

            PendingReopens.Remove(player);
            if (now - record.RecordedAt > PendingReopenExpirySeconds)
            {
                return false;
            }

            tile = record.Tile;
            itemObjectName = record.ItemObjectName;
            return true;
        }

        /// <summary>Queues an exact-tile respawn, consumed by the spawn system once the area is loaded.</summary>
        public static void EnqueueReopenSpawn(int2 tile, string itemObjectName, double now)
        {
            if (string.IsNullOrEmpty(itemObjectName) || !IsRegistered(itemObjectName))
            {
                return;
            }

            PendingReopenSpawns.Enqueue(new DimensionItemPortalReopenRequest
            {
                Tile = tile,
                ItemObjectName = itemObjectName,
                RequestedAt = now
            });
        }

        public static bool TryDequeueReopenSpawn(out DimensionItemPortalReopenRequest request)
        {
            if (PendingReopenSpawns.Count > 0)
            {
                request = PendingReopenSpawns.Dequeue();
                return true;
            }

            request = default(DimensionItemPortalReopenRequest);
            return false;
        }

        /// <summary>Puts a not-yet-spawnable reopen back at the end of the queue (area still loading).</summary>
        public static void RequeueReopenSpawn(DimensionItemPortalReopenRequest request)
        {
            PendingReopenSpawns.Enqueue(request);
        }

        public static bool HasPendingReopenSpawns => PendingReopenSpawns.Count > 0;

        public static int PendingReopenSpawnCount => PendingReopenSpawns.Count;

        /// <summary>True while any item portal is on the shared cooldown.</summary>
        public static bool IsOnGlobalCooldown(double now)
        {
            return now < globalCooldownUntil;
        }

        // Wall-clock mirror of the shared cooldown, for the client-side item-icon overlay: the
        // server gate runs on server world time, which the UI cannot compare against, so the same
        // window is also recorded in Time.realtimeSinceStartupAsDouble terms when it starts.
        // (Host/single-player share these statics; a dedicated-server client would need an RPC —
        // the same known limitation as the spawn queue.)
        private static double uiCooldownStartedAtRealtime = double.NegativeInfinity;
        private static double uiCooldownDurationSeconds;

        /// <summary>Starts the shared cooldown for <paramref name="seconds"/> from <paramref name="now"/>.</summary>
        public static void StartGlobalCooldown(double now, float seconds)
        {
            double until = now + Mathf.Max(0f, seconds);
            if (until > globalCooldownUntil)
            {
                globalCooldownUntil = until;
                uiCooldownStartedAtRealtime = Time.realtimeSinceStartupAsDouble;
                uiCooldownDurationSeconds = Mathf.Max(0f, seconds);
            }
        }

        /// <summary>
        /// Remaining fraction (1 → 0) of the shared cooldown in UI time, for the item-icon
        /// cooldown overlay. False once elapsed or when no cooldown was started.
        /// </summary>
        public static bool TryGetGlobalCooldownNormalizedRemaining(out float normalizedRemaining)
        {
            normalizedRemaining = 0f;
            if (uiCooldownDurationSeconds <= 0d)
            {
                return false;
            }

            double elapsed = Time.realtimeSinceStartupAsDouble - uiCooldownStartedAtRealtime;
            if (elapsed < 0d || elapsed >= uiCooldownDurationSeconds)
            {
                return false;
            }

            normalizedRemaining = 1f - (float)(elapsed / uiCooldownDurationSeconds);
            return true;
        }

        public static void Clear()
        {
            Configs.Clear();
            ItemObjectIdToName.Clear();
            itemObjectIdResolvedForConfigCount = -1;
            InstantPortalObjectIds.Clear();
            instantObjectIdResolvedForConfigCount = -1;
            PendingSpawns.Clear();
            PendingReopens.Clear();
            PendingReopenSpawns.Clear();
            globalCooldownUntil = 0d;
            uiCooldownStartedAtRealtime = double.NegativeInfinity;
            uiCooldownDurationSeconds = 0d;
        }
    }
}
