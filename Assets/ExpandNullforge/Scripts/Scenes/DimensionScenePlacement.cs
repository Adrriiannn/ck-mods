using System.Collections.Generic;
using ExpandNullforge.Foundation;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// Places a registered scene at a chosen spot, refusing when doing so would destroy something a
    /// player built.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE HAZARD THIS EXISTS TO CONTAIN. Core Keeper's scene applier calls <c>Clear</c> on EVERY tile
    /// a scene covers before it writes anything. Drop a scene on someone's base and the base is gone —
    /// walls, floors, everything on those cells — with no warning and no undo. Vanilla never hits this
    /// because its own placement vets the area upstream and only ever runs during world generation, on
    /// terrain nobody has touched. An explicit "put it here" call has neither protection, so the vetting
    /// has to live here.
    /// </para>
    /// <para>
    /// WHAT COUNTS AS BUILT. Not "has the terrain changed" — that would need a memory of what
    /// generation produced, and digging a tunnel would then block placement forever. Instead the
    /// footprint is checked for tile types that ONLY exist because a player made them: flooring, rugs,
    /// bridges, rails, fences, thin and great walls, circuit plates. Nothing in world generation
    /// produces those in the open, so finding one is strong evidence of a build, and missing a base
    /// made purely of dug ground is a far better failure than erasing a real one.
    /// </para>
    /// <para>
    /// A cell whose chunk has not streamed in is treated as UNKNOWN and refuses placement. Reading an
    /// unloaded chunk cannot distinguish empty from unexamined, and guessing wrong here is destructive
    /// — the caller can retry once the area is loaded.
    /// </para>
    /// <para>
    /// No timing constraint applies to this path: the applier re-reads the scene table every update, so
    /// a scene registered at any point can be placed at any point. That is the opposite of natural
    /// world generation, which snapshots the table once and never looks again.
    /// </para>
    /// </remarks>
    /// <summary>What became of a placement request, told apart so a caller can retry honestly.</summary>
    public enum DimensionScenePlacementResult
    {
        /// <summary>The scene is queued; the game stamps it within a tick or two.</summary>
        Placed = 0,

        /// <summary>Refused for a lasting reason: unknown scene, or a player build in the way.</summary>
        Refused = 1,

        /// <summary>The footprint is not loaded yet. The same call may succeed shortly.</summary>
        AreaNotLoaded = 2,
    }

    public static class DimensionScenePlacement
    {
        /// <summary>
        /// Tile types that exist only because somebody built them.
        /// </summary>
        /// <remarks>
        /// Kept as an explicit list rather than derived from a "built" flag, because the engine has no
        /// such flag — these are the Built tile-type family from the tileset taxonomy, and naming them
        /// makes the rule reviewable.
        /// </remarks>
        private static readonly TileType[] PlayerBuiltTiles =
        {
            TileType.floor,
            TileType.litFloor,
            TileType.looseFlooring,
            TileType.rug,
            TileType.bridge,
            TileType.rail,
            TileType.fence,
            TileType.thinWall,
            TileType.greatWall,
            TileType.circuitPlate,
            TileType.ancientCircuitPlate
        };

        /// <summary>
        /// Queues <paramref name="sceneName"/> to be stamped centred on <paramref name="position"/>.
        /// </summary>
        /// <param name="world">The server world; scenes are server-authoritative.</param>
        /// <param name="sceneName">A name passed to <see cref="DimensionCustomSceneRegistry.Register"/>.</param>
        /// <param name="position">Where the scene's own centre lands, in absolute tile coordinates.</param>
        /// <param name="seed">Varies the scene's randomised content; the tiles themselves are fixed.</param>
        /// <param name="error">Why nothing was placed.</param>
        public static bool TryPlace(World world, string sceneName, int2 position, uint seed, out string error)
        {
            DimensionScenePlacementResult ignored;
            return TryPlace(world, sceneName, position, seed, out ignored, out error);
        }

        /// <summary>
        /// The result-typed form: a not-loaded footprint is a retry, not a failure, and the
        /// generation pass that drives placement needs to tell the two apart without parsing
        /// an error string.
        /// </summary>
        public static bool TryPlace(
            World world,
            string sceneName,
            int2 position,
            uint seed,
            out DimensionScenePlacementResult result,
            out string error)
        {
            result = DimensionScenePlacementResult.Refused;
            if (world == null || !world.IsCreated)
            {
                error = "The server world is not available.";
                return false;
            }

            DimensionCustomSceneDefinition definition;
            if (!DimensionCustomSceneRegistry.TryGet(sceneName, out definition))
            {
                error =
                    "No scene named '" + sceneName + "' is registered. Scenes resolve by name, and the " +
                    "name is the mod-qualified scene id.";
                return false;
            }

            string blocker;
            bool areaNotLoaded;
            if (!IsAreaFreeOfBuilds(world, definition, position, out blocker, out areaNotLoaded))
            {
                result = areaNotLoaded
                    ? DimensionScenePlacementResult.AreaNotLoaded
                    : DimensionScenePlacementResult.Refused;
                error =
                    "'" + sceneName + "' was not placed at " + position + ": " + blocker +
                    " Placing it would have erased that, because the game clears every tile a scene " +
                    "covers before writing.";
                return false;
            }

            EntityManager entityManager = world.EntityManager;
            Entity request = entityManager.CreateEntity();
            entityManager.AddComponentData(request, new SpawnCustomSceneCD
            {
                name = sceneName,
                seed = seed
            });
            entityManager.AddComponentData(request, LocalTransform.FromPosition(position.x, 0f, position.y));

            DimensionFrameworkLog.Info(
                "Queued scene '" + sceneName + "' at " + position + ".");
            result = DimensionScenePlacementResult.Placed;
            error = null;
            return true;
        }

        /// <summary>
        /// Whether the scene's footprint is clear of anything a player built.
        /// </summary>
        private static bool IsAreaFreeOfBuilds(
            World world,
            DimensionCustomSceneDefinition definition,
            int2 origin,
            out string blocker,
            out bool areaNotLoaded)
        {
            blocker = null;
            areaNotLoaded = false;

            PugQuerySystem querySystem = world.GetExistingSystemManaged<PugQuerySystem>();
            if (querySystem == null)
            {
                blocker = "the world's tile data is not readable yet.";
                return false;
            }

            TileAccessor tiles = new TileAccessor(querySystem);
            IReadOnlyList<DimensionSceneTile> sceneTiles = definition.Tiles;

            for (int i = 0; i < sceneTiles.Count; i++)
            {
                int2 cell = origin + sceneTiles[i].LocalPosition - definition.CenterPosition;

                if (!tiles.IsInitialized(cell))
                {
                    areaNotLoaded = true;
                    blocker =
                        "the area around " + cell + " has not loaded, so it cannot be checked for " +
                        "player builds.";
                    return false;
                }

                NativeArray<TileCD> layers = tiles.Get(cell, Allocator.Temp);
                TileType built = TileType.none;
                for (int t = 0; t < layers.Length; t++)
                {
                    if (IsPlayerBuilt(layers[t].tileType))
                    {
                        built = layers[t].tileType;
                        break;
                    }
                }

                layers.Dispose();

                if (built != TileType.none)
                {
                    blocker = "there is a " + built + " at " + cell + ", which somebody built.";
                    return false;
                }
            }

            return true;
        }

        private static bool IsPlayerBuilt(TileType tileType)
        {
            for (int i = 0; i < PlayerBuiltTiles.Length; i++)
            {
                if (PlayerBuiltTiles[i] == tileType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
