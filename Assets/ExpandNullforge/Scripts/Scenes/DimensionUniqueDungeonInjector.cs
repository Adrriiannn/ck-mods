using System.Collections.Generic;
using ExpandNullforge.Foundation;
using HarmonyLib;
using Unity.Entities;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>One dungeon pinned into the vanilla Overworld, the way Glurch and Azeos are.</summary>
    public sealed class DimensionUniqueDungeonDefinition
    {
        public DimensionUniqueDungeonDefinition(
            string dungeonId,
            string uniqueName,
            bool exactPosition,
            int2 position,
            int distanceFromCore,
            string biomeName,
            bool spawnImmediately)
        {
            DungeonId = dungeonId ?? string.Empty;
            UniqueName = uniqueName ?? string.Empty;
            ExactPosition = exactPosition;
            Position = position;
            DistanceFromCore = distanceFromCore < 0 ? 0 : distanceFromCore;
            BiomeName = biomeName ?? string.Empty;
            SpawnImmediately = spawnImmediately;
        }

        /// <summary>Which assembled prototype this pin spawns.</summary>
        public readonly string DungeonId;

        /// <summary>
        /// The world's memory of this dungeon — the save's dedupe key, capped by the game at
        /// 29 UTF-8 bytes. Changing it makes an updated mod place a SECOND copy.
        /// </summary>
        public readonly string UniqueName;

        public readonly bool ExactPosition;

        public readonly int2 Position;

        public readonly int DistanceFromCore;

        /// <summary>A vanilla biome name, or empty for anywhere.</summary>
        public readonly string BiomeName;

        public readonly bool SpawnImmediately;
    }

    /// <summary>The Overworld pins the generated bootstrap registered.</summary>
    public static class DimensionUniqueDungeonRegistry
    {
        private static readonly List<DimensionUniqueDungeonDefinition> Definitions =
            new List<DimensionUniqueDungeonDefinition>();

        public static IReadOnlyList<DimensionUniqueDungeonDefinition> All
        {
            get { return Definitions; }
        }

        public static void Register(DimensionUniqueDungeonDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.DungeonId) ||
                string.IsNullOrEmpty(definition.UniqueName))
            {
                return;
            }

            for (int i = 0; i < Definitions.Count; i++)
            {
                if (string.Equals(Definitions[i].UniqueName, definition.UniqueName,
                        System.StringComparison.Ordinal))
                {
                    Definitions[i] = definition;
                    return;
                }
            }

            Definitions.Add(definition);
        }

        public static void Clear()
        {
            Definitions.Clear();
        }
    }

    /// <summary>
    /// Puts the registered pins into vanilla's own unique-dungeon planner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE SEAM IS ONE ENTITY PER PIN, injected before the planner's single per-session run.
    /// The planner queues every <c>PugWorldGen.PugWorldGenCD</c> entity by sort index and burns exactly
    /// ten thousand deterministic samples per entry — so a framework pin with a sort index
    /// above every vanilla entry samples only the TAIL of the sequence, and every vanilla
    /// dungeon lands exactly where it would have without us. From there vanilla does the
    /// whole job on its own rails: scoring, footprint blockers, the load-keeper, the spawn,
    /// the save record that survives reloads, and — on an old save whose terrain already
    /// generated — the forced work order that stamps the dungeon into the intact world.
    /// </para>
    /// <para>
    /// THE ONE OBLIGATION THE FRAMEWORK CARRIES: the instantiated prototype must eventually
    /// be destroyed, or the whole 256-tile cell's procedural generation stalls forever behind
    /// a pending-spawn count that never reaches zero. Our prototypes are full pipeline
    /// dungeons — the game's own apply system destroys the root when generation completes —
    /// so the obligation is met by construction, and the preflight refuses a pin whose
    /// prototype is missing rather than risk injecting an undestroyable one.
    /// </para>
    /// <para>
    /// Sort indices derive from the pin's name hash folded into the top half of the int
    /// range: deterministic across load orders, colliding with vanilla never and with each
    /// other only astronomically.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(SpawnUniqueDungeonInitSystem), "OnStartRunning")]
    public static class DimensionUniqueDungeonInjector
    {
        private static readonly HashSet<string> InjectedWorlds =
            new HashSet<string>(System.StringComparer.Ordinal);

        public static void ResetForNewSession()
        {
            InjectedWorlds.Clear();
        }

        private static void Prefix(SpawnUniqueDungeonInitSystem __instance)
        {
            if (DimensionUniqueDungeonRegistry.All.Count == 0)
            {
                return;
            }

            World world = __instance.World;
            if (world == null || !world.IsCreated || !InjectedWorlds.Add(world.Name + world.SequenceNumber))
            {
                return;
            }

            EntityManager entityManager = world.EntityManager;

            // Prototypes must exist before the pin references them; the assembler is
            // idempotent and its tables provably exist here (the planner itself requires the
            // converted authoring data this world builds them from).
            if (!DimensionDungeonAssembler.TryAssemble(world))
            {
                DimensionFrameworkLog.Warning(
                    "The dungeon tables were not ready when the unique " +
                    "planner started, so no framework dungeon could be pinned into this " +
                    "world's Overworld this session.");
                return;
            }

            // The content-bundle gate silently skips entries from bundles the world has not
            // activated. Copying the address from a live vanilla entry rides whatever this
            // world considers base content.
            EntityQuery vanillaEntries = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PugWorldGen.PugWorldGenCD>());
            if (vanillaEntries.IsEmptyIgnoreFilter)
            {
                DimensionFrameworkLog.Warning(
                    "No vanilla unique entries were present at planning " +
                    "time, so no content-bundle address could be borrowed. Framework pins " +
                    "were skipped this session.");
                return;
            }

            PugWorldGen.PugWorldGenCD template;
            using (Unity.Collections.NativeArray<PugWorldGen.PugWorldGenCD> entries =
                vanillaEntries.ToComponentDataArray<PugWorldGen.PugWorldGenCD>(Unity.Collections.Allocator.Temp))
            {
                template = entries[0];
            }

            int injected = 0;
            IReadOnlyList<DimensionUniqueDungeonDefinition> pins = DimensionUniqueDungeonRegistry.All;
            for (int i = 0; i < pins.Count; i++)
            {
                DimensionUniqueDungeonDefinition pin = pins[i];
                Entity prototype;
                if (!DimensionDungeonAssembler.TryGetPrototype(world, pin.DungeonId, out prototype) ||
                    !entityManager.Exists(prototype))
                {
                    DimensionFrameworkLog.Warning(
                        "Overworld pin '" + pin.UniqueName + "' names dungeon '" +
                        pin.DungeonId + "', which has no assembled prototype. The pin was " +
                        "skipped — injecting it would stall a whole spawn cell forever.");
                    continue;
                }

                string error;
                if (!DimensionCustomSceneNames.IsValid(pin.UniqueName, out error))
                {
                    DimensionFrameworkLog.Warning(
                        "Overworld pin name '" + pin.UniqueName + "' does " +
                        "not fit the game's save record: " + error);
                    continue;
                }

                Biome biome = Biome.None;
                if (!string.IsNullOrEmpty(pin.BiomeName) &&
                    !System.Enum.TryParse(pin.BiomeName, false, out biome))
                {
                    DimensionFrameworkLog.Warning(
                        "Overworld pin '" + pin.UniqueName + "' names biome '" +
                        pin.BiomeName + "', which is not a vanilla biome. It may appear anywhere.");
                    biome = Biome.None;
                }

                int placementRadius = 24;
                if (entityManager.HasComponent<PugWorldGen.DungeonAreaCD>(prototype))
                {
                    placementRadius = math.max(
                        8,
                        entityManager.GetComponentData<PugWorldGen.DungeonAreaCD>(prototype)
                            .placementRadius);
                }

                PugWorldGen.PugWorldGenCD entry = template;
                entry.replacedByBundle = default;
                entry.spawnImmediatelyOnLoad = pin.SpawnImmediately;
                entry.destroyMarkerAfterSpawn = false;
                entry.biome = new WorldGenerationTypeDependentValue<Biome>
                {
                    classic = biome,
                    fullRelease = biome
                };
                entry.placementType = pin.ExactPosition
                    ? UniqueScenePlacementType.ExactPosition
                    : UniqueScenePlacementType.DistanceFromCoreInBiome;
                entry.positionSampling = biome == Biome.None
                    ? UniqueScenePositionSampling.InsideRing4
                    : UniqueScenePositionSampling.InsideBiomeBounds;
                entry.targetDistanceFromCore = new WorldGenerationTypeDependentValue<int>
                {
                    classic = pin.DistanceFromCore,
                    fullRelease = pin.DistanceFromCore
                };
                entry.allowOverlappingSpawnCellBorders = false;
                entry.spawnPosition = new WorldGenerationTypeDependentValue<int2>
                {
                    classic = pin.Position,
                    fullRelease = pin.Position
                };
                entry.entity = prototype;
                entry.markerEntity = Entity.Null;
                entry.name = new Unity.Collections.FixedString32Bytes(pin.UniqueName);
                entry.sortIndex = TailSortIndex(pin.UniqueName);
                entry.radius = placementRadius;

                Entity pinEntity = entityManager.CreateEntity(typeof(PugWorldGen.PugWorldGenCD));
                entityManager.SetComponentData(pinEntity, entry);
                injected++;
            }

            if (injected > 0)
            {
                DimensionFrameworkLog.Info(
                    "Pinned " + injected + " framework dungeon(s) into the " +
                    "Overworld's unique placement plan.");
            }
        }

        /// <summary>
        /// A sort index in the top half of the int range, derived from the name so it is
        /// stable across load orders — the property vanilla's seeded layout depends on.
        /// </summary>
        private static int TailSortIndex(string uniqueName)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < uniqueName.Length; i++)
                {
                    hash = (hash ^ uniqueName[i]) * 16777619u;
                }

                return int.MaxValue / 2 + (int)(hash % (uint)(int.MaxValue / 2));
            }
        }
    }
}
