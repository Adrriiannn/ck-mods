using System.Collections.Generic;
using ExpandNullforge.Foundation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Carries custom-tileset tiles across Core Keeper's submap deserialization, which would
    /// otherwise delete them.
    /// </summary>
    /// <remarks>
    /// <c>DeserializeComponentsSystem</c> rebuilds a submap's live <c>SubMapLayerBuffer</c> from its
    /// serialized form and drops every layer whose tileset is outside vanilla's range:
    /// <code>if (tileset &lt; 0 || tileset &gt;= 75) { LogWarning("Discarding submap with invalid tileset …"); }</code>
    /// Every custom id is above that bound, so custom tiles vanish on world load and on a multiplayer
    /// client as submaps stream in. That check cannot be patched — it lives inside a
    /// <c>[BurstCompile]</c> job, so there is no managed method left to hook.
    ///
    /// Instead the game's own path is left completely alone and bracketed: a capture system reads the
    /// custom layers out of the serialized buffer before deserialization, and a restore system appends
    /// them to the rebuilt buffer afterwards. Records are held by submap position because the
    /// serialized entity is destroyed in the process and the rebuilt one is a different entity.
    /// </remarks>
    public static class DimensionCustomTileRescue
    {
        /// <summary>
        /// How many restore passes a record may go unclaimed before it is dropped. The rebuilt submap
        /// appears only once the deserialize system's command buffer plays back, which is not
        /// guaranteed to be the same frame, so records have to outlive a pass or two — but a submap
        /// that never reappears (the deserialize system rejected it for its own reasons) must not
        /// leak.
        /// </summary>
        private const int MaxPassesPending = 8;

        private sealed class PendingSubMap
        {
            public List<SubMapLayer> Layers;
            public int Age;
        }

        private static readonly Dictionary<int2, PendingSubMap> Pending = new Dictionary<int2, PendingSubMap>();

        internal static bool HasPending
        {
            get { return Pending.Count > 0; }
        }

        /// <summary>Remembers one submap's custom layers, replacing any earlier record for it.</summary>
        internal static void Record(int2 position, List<SubMapLayer> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return;
            }

            Pending[position] = new PendingSubMap { Layers = layers, Age = 0 };
        }

        /// <summary>Claims a submap's record, removing it. False when nothing was held for it.</summary>
        internal static bool TryClaim(int2 position, out List<SubMapLayer> layers)
        {
            PendingSubMap pending;
            if (!Pending.TryGetValue(position, out pending))
            {
                layers = null;
                return false;
            }

            Pending.Remove(position);
            layers = pending.Layers;
            return true;
        }

        /// <summary>Ages every unclaimed record and discards the ones whose submap never came back.</summary>
        internal static void AgePending()
        {
            if (Pending.Count == 0)
            {
                return;
            }

            List<int2> expired = null;
            foreach (KeyValuePair<int2, PendingSubMap> entry in Pending)
            {
                entry.Value.Age++;
                if (entry.Value.Age < MaxPassesPending)
                {
                    continue;
                }

                if (expired == null)
                {
                    expired = new List<int2>();
                }

                expired.Add(entry.Key);
            }

            if (expired == null)
            {
                return;
            }

            for (int i = 0; i < expired.Count; i++)
            {
                Pending.Remove(expired[i]);
            }
        }

        public static void Clear()
        {
            Pending.Clear();
        }

        /// <summary>
        /// Places the capture system in the same group as the deserializer, by hand.
        /// </summary>
        /// <remarks>
        /// Its <c>[UpdateBefore(DeserializeComponentsSystem)]</c> attribute is silently dropped every
        /// run — the game logs "Ignoring invalid [UpdateBeforeAttribute] … can only order systems that
        /// are members of the same ComponentSystemGroup instance", because a mod's systems are not
        /// created into <c>SerializationSystemGroup</c>. Ordering then falls to whatever the default
        /// happens to be, which has worked so far purely by luck. The warning names this remedy itself:
        /// add the system to the group's update list directly, then re-sort so the attribute applies.
        /// </remarks>
        public static void EnsureSystemOrdering(World world)
        {
            if (world == null || !world.IsCreated)
            {
                return;
            }

            SerializationSystemGroup group = world.GetExistingSystemManaged<SerializationSystemGroup>();
            DimensionCustomTileCaptureSystem capture =
                world.GetExistingSystemManaged<DimensionCustomTileCaptureSystem>();
            if (group == null || capture == null)
            {
                Debug.LogWarning(
                    "[NF_TILESET] Could not place the tile-capture system before the deserializer in " +
                    world.Name + " (group " + (group == null ? "missing" : "ok") +
                    ", system " + (capture == null ? "missing" : "ok") +
                    "). Custom tiles may not survive a reload in this world.");
                return;
            }

            group.AddSystemToUpdateList(capture);
            group.SortSystems();
            Debug.Log(
                "[NF_TILESET] Tile-capture system ordered before the deserializer in " + world.Name + ".");
        }
    }

    /// <summary>
    /// Reads custom-tileset layers out of serialized submaps just before the game deserializes them
    /// and throws those layers away. See <see cref="DimensionCustomTileRescue"/>.
    /// </summary>
    [UpdateInGroup(typeof(SerializationSystemGroup))]
    [UpdateBefore(typeof(DeserializeComponentsSystem))]
    public partial class DimensionCustomTileCaptureSystem : SystemBase
    {
        private EntityQuery serializedSubMaps;

        protected override void OnCreate()
        {
            serializedSubMaps = GetEntityQuery(
                ComponentType.ReadOnly<SubMapSerializedCD>(),
                ComponentType.ReadOnly<SubMapLayerSerializedBuffer>());
            RequireForUpdate(serializedSubMaps);
        }

        protected override void OnUpdate()
        {
            NativeArray<Entity> entities = serializedSubMaps.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SubMapSerializedCD submap = EntityManager.GetComponentData<SubMapSerializedCD>(entity);
                DynamicBuffer<SubMapLayerSerializedBuffer> layers =
                    EntityManager.GetBuffer<SubMapLayerSerializedBuffer>(entity, true);

                List<SubMapLayer> rescued = null;
                for (int l = 0; l < layers.Length; l++)
                {
                    SubMapLayer layer = layers[l];
                    if (!DimensionTilesetRegistry.IsCustomTilesetId(layer.layer.tileset))
                    {
                        continue;
                    }

                    if (rescued == null)
                    {
                        rescued = new List<SubMapLayer>();
                    }

                    rescued.Add(layer);
                }

                if (rescued != null)
                {
                    Debug.Log(
                        "[NF_TILESET] Captured " + rescued.Count +
                        " custom tile layer(s) before deserialization, submap " + submap.Position + ".");
                    DimensionCustomTileRescue.Record(submap.Position, rescued);
                }
            }

            entities.Dispose();
        }
    }

    /// <summary>
    /// Puts the captured custom-tileset layers back onto the rebuilt submap. Runs after the command
    /// buffer that creates it has played back. See <see cref="DimensionCustomTileRescue"/>.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SerializationSystemGroup))]
    public partial class DimensionCustomTileRestoreSystem : SystemBase
    {
        private EntityQuery subMaps;

        protected override void OnCreate()
        {
            subMaps = GetEntityQuery(
                ComponentType.ReadOnly<SubMapCD>(),
                ComponentType.ReadWrite<SubMapLayerBuffer>());
        }

        protected override void OnUpdate()
        {
            if (!DimensionCustomTileRescue.HasPending)
            {
                return;
            }

            NativeArray<Entity> entities = subMaps.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SubMapCD submap = EntityManager.GetComponentData<SubMapCD>(entity);

                List<SubMapLayer> rescued;
                if (!DimensionCustomTileRescue.TryClaim(submap.index, out rescued))
                {
                    continue;
                }

                DynamicBuffer<SubMapLayerBuffer> buffer = EntityManager.GetBuffer<SubMapLayerBuffer>(entity);
                int restored = 0;
                for (int l = 0; l < rescued.Count; l++)
                {
                    SubMapLayer layer = rescued[l];

                    // The game drops our layers rather than mangling them, so a match here would mean
                    // something else already restored this submap — never append a second copy.
                    if (ContainsLayer(buffer, layer))
                    {
                        continue;
                    }

                    buffer.Add(layer);
                    restored++;
                }

                if (restored > 0)
                {
                    // Deliberately not verbose-gated: this is the only proof the rescue is working,
                    // and it fires once per submap rather than per frame.
                    Debug.Log(
                        "[NF_TILESET] Restored " + restored +
                        " custom tile layer(s) the deserializer discarded, submap " + submap.index + ".");
                }
            }

            entities.Dispose();
            DimensionCustomTileRescue.AgePending();
        }

        private static bool ContainsLayer(DynamicBuffer<SubMapLayerBuffer> buffer, SubMapLayer layer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                SubMapLayer existing = buffer[i];
                if (existing.layer.tileset == layer.layer.tileset &&
                    existing.layer.tileType == layer.layer.tileType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
