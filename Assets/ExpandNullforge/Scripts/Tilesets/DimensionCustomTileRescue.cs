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

        /// <summary>
        /// Pending records, bucketed by world.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE WORLD KEY IS NOT OPTIONAL. The capture and restore systems are installed into BOTH the
        /// server and the client world, because both deserialize submaps and both hit the same gate.
        /// A host runs the two side by side and they see the SAME submap positions. Keyed by position
        /// alone, that is three separate bugs sharing one dictionary:
        /// </para>
        /// <list type="bullet">
        /// <item>the second world's capture overwrites the first world's record for that position;</item>
        /// <item><see cref="TryClaim"/> REMOVES what it returns, so whichever restore runs first
        /// consumes the record and the other world silently keeps none of its custom tiles — and
        /// which one wins is whatever order the two worlds happen to tick in;</item>
        /// <item>ageing ran once per world per frame, so records expired in half the intended passes.</item>
        /// </list>
        /// <para>
        /// Bucketing by <c>World.SequenceNumber</c> (unique per world instance and never reused) makes
        /// each world's rescue completely independent, which is what it always meant to be.
        /// </para>
        /// </remarks>
        private static readonly Dictionary<ulong, Dictionary<int2, PendingSubMap>> PendingByWorld =
            new Dictionary<ulong, Dictionary<int2, PendingSubMap>>();

        /// <summary>
        /// The worlds that have actually taken layers out of a submap.
        /// </summary>
        /// <remarks>
        /// THE TALLY IS PROCESS-WIDE AND THE REPORT IS PER WORLD, which without this made a host
        /// say the wrong thing about the wrong world. <c>DimensionLog.Count</c> keys on the channel
        /// and nothing else, so the captured/restored pair is one bucket for the whole process,
        /// while <see cref="Clear"/> is called once per world as it goes away — and the capture and
        /// restore systems are server-side, so a client world contributes nothing and used to
        /// arrive first, drain the server's tally and print it under ClientWorld0. A mid-flight
        /// difference between the two counts then reads as "custom terrain will be gone", about a
        /// world that never held any. A world that never captured says nothing and drains nothing.
        /// </remarks>
        private static readonly HashSet<ulong> WorldsThatCaptured = new HashSet<ulong>();

        private static ulong KeyOf(World world)
        {
            // A null world would mean a system ticked without one, which cannot happen; bucket 0 keeps
            // that case self-consistent rather than throwing during world teardown.
            return world != null ? world.SequenceNumber : 0UL;
        }

        internal static bool HasPendingFor(World world)
        {
            Dictionary<int2, PendingSubMap> bucket;
            return PendingByWorld.TryGetValue(KeyOf(world), out bucket) && bucket.Count > 0;
        }

        /// <summary>Remembers one submap's custom layers, replacing any earlier record for it.</summary>
        internal static void Record(World world, int2 position, List<SubMapLayer> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return;
            }

            ulong key = KeyOf(world);
            WorldsThatCaptured.Add(key);
            Dictionary<int2, PendingSubMap> bucket;
            if (!PendingByWorld.TryGetValue(key, out bucket))
            {
                bucket = new Dictionary<int2, PendingSubMap>();
                PendingByWorld[key] = bucket;
            }

            bucket[position] = new PendingSubMap { Layers = layers, Age = 0 };
        }

        /// <summary>Claims a submap's record, removing it. False when nothing was held for it.</summary>
        internal static bool TryClaim(World world, int2 position, out List<SubMapLayer> layers)
        {
            layers = null;

            Dictionary<int2, PendingSubMap> bucket;
            if (!PendingByWorld.TryGetValue(KeyOf(world), out bucket))
            {
                return false;
            }

            PendingSubMap pending;
            if (!bucket.TryGetValue(position, out pending))
            {
                return false;
            }

            bucket.Remove(position);
            layers = pending.Layers;
            return true;
        }

        /// <summary>
        /// Ages one world's unclaimed records and discards the ones whose submap never came back.
        /// Empties its bucket too, so a world torn down without a <see cref="Clear"/> cannot leak one.
        /// </summary>
        internal static void AgePending(World world)
        {
            ulong key = KeyOf(world);
            Dictionary<int2, PendingSubMap> bucket;
            if (!PendingByWorld.TryGetValue(key, out bucket) || bucket.Count == 0)
            {
                return;
            }

            List<int2> expired = null;
            foreach (KeyValuePair<int2, PendingSubMap> entry in bucket)
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
                bucket.Remove(expired[i]);
            }

            if (bucket.Count == 0)
            {
                PendingByWorld.Remove(key);
            }
        }

        /// <summary>
        /// Drops one world's pending records. Takes the world rather than clearing everything: on a
        /// host the other world is still live, and wiping its records would cost it the very tiles
        /// this class exists to save.
        /// </summary>
        public static void Clear(World world)
        {
            ReportTheBracket(world);
            PendingByWorld.Remove(KeyOf(world));
            WorldsThatCaptured.Remove(KeyOf(world));
        }

        /// <summary>
        /// Says how many custom tile layers this world took out of its submaps and how many it put
        /// back, once, as the world goes away.
        /// </summary>
        /// <remarks>
        /// THE PAIRING IS THE POINT AND NOTHING USED TO CHECK IT. Capture and restore each printed
        /// a line per submap — unbounded, on a streaming path — and a session could show a hundred
        /// captures and ninety restores with nobody in a position to notice. Counting instead costs
        /// one dictionary increment per submap and turns the two tallies into one line that can
        /// disagree with itself out loud.
        /// </remarks>
        private static void ReportTheBracket(World world)
        {
            if (!WorldsThatCaptured.Contains(KeyOf(world)))
            {
                // This world took nothing out of a submap, so the tally is somebody else's and
                // draining it here would both mis-attribute the line and leave the world that did
                // the work with nothing to report.
                return;
            }

            int captured = DimensionLog.CountOf(DimensionLogChannels.Tileset, "captured");
            int restored = DimensionLog.CountOf(DimensionLogChannels.Tileset, "restored");
            if (captured == 0 && restored == 0)
            {
                return;
            }

            string tally = DimensionLog.DrainCounts(DimensionLogChannels.Tileset);
            if (captured != restored)
            {
                DimensionLog.Problem(
                    DimensionLogChannels.Tileset,
                    world,
                    "custom tile layers were taken out of submaps and not all of them were put " +
                    "back (" + tally + "). Every layer in the difference is a patch of custom " +
                    "terrain that will be gone the next time this world loads.");
                return;
            }

            DimensionLog.Milestone(
                DimensionLogChannels.Tileset,
                world,
                "carried custom tile layers across the deserializer (" + tally + ").");
        }

        /// <summary>
        /// Places the capture system in the same group as the deserializer, by hand, and confirms the
        /// other half of the bracket exists.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A MOD'S SYSTEMS ARE CREATED INTO THIS GROUP, and this comment used to say the opposite.
        /// The game builds its worlds after the mod assembly is in memory and puts every system it
        /// finds into the group its <c>[UpdateInGroup]</c> names — which is why the engine's
        /// "Ignoring invalid [UpdateBeforeAttribute]" line can appear at all: that message is only
        /// emitted while sorting a group's own members. The capture system is already in the group
        /// before this method runs.
        /// </para>
        /// <para>
        /// SO WHAT THIS CALL IS ACTUALLY FOR IS THE SORT. <c>AddSystemToUpdateList</c> returns
        /// early without marking the list dirty when the system is already a member, and
        /// <c>SortSystems</c> then puts the group into run order. The add is kept because it costs
        /// nothing and covers the case where the sweep did not place the system; the sort is the
        /// half with an effect.
        /// </para>
        /// <para>
        /// The restore half needs no such repair, and that is worth stating because it looks like it
        /// should: <c>SerializationSystemGroup</c> is itself <c>[UpdateInGroup(SimulationSystemGroup)]</c>,
        /// so it and the restore system are members of the SAME group instance and the
        /// <c>[UpdateAfter]</c> between them is valid. Only its existence is checked here — a bracket
        /// missing one half captures tiles and never puts them back, which otherwise looks exactly
        /// like the bug this class fixes.
        /// </para>
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
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Could not place the tile-capture system before the deserializer in " +
                    world.Name + " (group " + (group == null ? "missing" : "ok") +
                    ", system " + (capture == null ? "missing" : "ok") +
                    "). Custom tiles may not survive a reload in this world.");
                return;
            }

            group.AddSystemToUpdateList(capture);
            group.SortSystems();

            if (world.GetExistingSystemManaged<DimensionCustomTileRestoreSystem>() == null)
            {
                DimensionLog.Fatal(DimensionLogChannels.Tileset, null, 
                    "The tile-restore system is missing in " + world.Name +
                    " while capture is running. Captured layers would be held and never put back, so " +
                    "custom tiles will not survive a reload in this world.");
                return;
            }

            // Nothing is said here on purpose. This point states an intention — the bracket has
            // been arranged — and a tester cannot act on an intention. What matters is whether the
            // two halves agree, which is what Clear reports when the world goes away.
        }
    }

    /// <summary>
    /// Reads custom-tileset layers out of serialized submaps just before the game deserializes them
    /// and throws those layers away. See <see cref="DimensionCustomTileRescue"/>.
    /// </summary>
    /// <remarks>
    /// SERVER ONLY, AND IT WAS NOT. With no <c>[WorldSystemFilter]</c> a system falls back to
    /// local, server and client, so this was created and scheduled in the client world too — where
    /// there is nothing for it to do, because every system that touches
    /// <c>SubMapSerializedCD</c> is server-side, <c>DeserializeComponentsSystem</c> included. That
    /// is the whole reason the engine's "Ignoring invalid [UpdateBefore]" line appeared three times
    /// a session: on the client the ordering target is not absent from the group, it is absent from
    /// the world. Saying which world this belongs in fixes the warning, the pointless client-side
    /// work, and an audit that was certifying a pair of client systems that could never run.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
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
                    // COUNTED, NOT PRINTED. This fires once per submap that carries custom layers,
                    // for every submap the world streams in, so printing it buries everything else
                    // in the session. The tally is reported once when the world goes away, beside
                    // the restore tally, and a mismatch between the two is the failure to spot.
                    ExpandNullforge.Foundation.DimensionLog.Count(
                        ExpandNullforge.Foundation.DimensionLogChannels.Tileset, "captured");
                    DimensionCustomTileRescue.Record(World, submap.Position, rescued);
                }
            }

            entities.Dispose();
        }
    }

    /// <summary>
    /// Puts the captured custom-tileset layers back onto the rebuilt submap. Runs after the command
    /// buffer that creates it has played back. See <see cref="DimensionCustomTileRescue"/>.
    /// </summary>
    /// <remarks>
    /// Server only, for the same reason as its other half: the only thing that ever gives it work
    /// is the capture system, which is server-side, so on a client this was a query and an early
    /// return every frame for the life of the session.
    /// </remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
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
            if (!DimensionCustomTileRescue.HasPendingFor(World))
            {
                return;
            }

            NativeArray<Entity> entities = subMaps.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                SubMapCD submap = EntityManager.GetComponentData<SubMapCD>(entity);

                List<SubMapLayer> rescued;
                if (!DimensionCustomTileRescue.TryClaim(World, submap.index, out rescued))
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
                    // The other half of the pair. Counted for the same reason, and the two counts
                    // are the whole point of the bracket: captured without restored means custom
                    // tiles were taken out of a submap and never put back.
                    ExpandNullforge.Foundation.DimensionLog.Count(
                        ExpandNullforge.Foundation.DimensionLogChannels.Tileset, "restored");
                }
            }

            entities.Dispose();
            DimensionCustomTileRescue.AgePending(World);
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
