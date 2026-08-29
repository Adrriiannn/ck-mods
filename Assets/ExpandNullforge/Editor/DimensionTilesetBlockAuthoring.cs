using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Turns an already-configured block <em>item</em> prefab (ObjectAuthoring + InventoryItem +
    /// PlaceableObjectAuthoring, emitted by <see cref="DimensionItemGenerator"/> for the Block
    /// archetype) into a working custom <em>tile</em> block by attaching the tile-behaviour
    /// components a ground or wall needs.
    ///
    /// The one load-bearing stamp is <c>TileAuthoring.tileset</c> = the tileset's derived hash id:
    /// the game's own ObjectAuthoring→ObjectInfo conversion copies it into
    /// <c>ObjectInfo.tileset</c> (verified in Pug.ECS.Authoring), which is what the reverse tile
    /// maps and the placement/dig/drop path all key on. Everything else here is standard vanilla
    /// block wiring.
    ///
    /// Deliberate v1 scope (see the tileset plan memory):
    /// - Only the identity tile (TileAuthoring) carries the custom id. Transient feedback tiles
    ///   (the pit/small-stones spawned on death, the crack overlay while mining) stay vanilla Dirt
    ///   so they always render, exactly as the vanilla dirt block does — the custom skin for those
    ///   is a later enhancement once a sheet supplies those regions.
    /// - No DropLootAuthoring: the Block archetype excludes Loot, and a mined block drops itself
    ///   through the (tileset,tileType)→object reverse map, not a loot table — so there is no risk
    ///   of a vanilla table dropping the wrong item.
    /// - SFX (TileEffectAuthoring), damage-reduction/area-level difficulty tuning, and any
    ///   hand-authored Ghost/PhysicsShape are left to the SDK conversion / future passes.
    /// </summary>
    internal static class DimensionTilesetBlockAuthoring
    {
        // Starting hardness. Dirt-soft on purpose; tune per tileset in a later pass if needed.
        private const int GroundMaxHealth = 1;
        private const int WallMaxHealth = 5;

        public static void Apply(
            GameObject root,
            DimensionTilesetAsset tileset,
            bool isWall,
            DimensionItemGenerationReport report)
        {
            if (root == null || tileset == null)
            {
                return;
            }

            int tilesetId = tileset.TilesetId;
            if (!DimensionTilesetRegistry.IsCustomTilesetId(tilesetId))
            {
                report.Warnings.Add(
                    "Tileset '" + tileset.TilesetName + "' resolved to id " + tilesetId +
                    ", which is not in the custom range; its block was generated without a tile identity.");
                return;
            }

            TileType tileType = isWall ? TileType.wall : TileType.ground;

            // The identity stamp. (Tileset)tilesetId holds the raw hash under the enum; the game's
            // conversion reads it back with (int)tileset, so ObjectInfo.tileset ends up == tilesetId.
            TileAuthoring tile = Ensure<TileAuthoring>(root);
            tile.tileset = (Tileset)tilesetId;
            tile.tileType = tileType;

            // Chunk data owns the placed tile; the entity is transient and must not be serialized.
            Ensure<DontSerializeAuthoring>(root);

            // WITHOUT THIS A CUSTOM BLOCK IS COMPLETELY SILENT AND THROWS NOTHING. It is not a
            // query but a converter-chain gate: the game wraps ALL of its tile audio and ALL of its
            // puffs — its own defaults included — inside a check for this component, so a block
            // without it has no hit sound, no break sound, no dust and no debris. Every one of the
            // game's own blocks and ores carries it. The two sound tables and the puff list are
            // left at their defaults, which is what makes the game fall back to its own; a block
            // that wants its own sounds is a separate answer nobody has been asked for yet.
            Ensure<TileEffectAuthoring>(root);

            HealthAuthoring health = Ensure<HealthAuthoring>(root);
            health.maxHealth = isWall ? WallMaxHealth : GroundMaxHealth;
            // No AreaLevelAuthoring on the object, so keep health off the level curve.
            health.dontCalculateHealthFromLevel = true;

            // Crack overlay shown while the tile is being dug/mined. Vanilla points every block's
            // cracks at its OWN tileset (TileCrackSystem places the crack tile with
            // CrackableTileCD.crackTileset verbatim), so custom blocks use their own baked crack
            // states — all three damage stages come from the custom sheet, not Dirt's.
            CrackableTileAuthoring crack = Ensure<CrackableTileAuthoring>(root);
            crack.crackTileType = isWall ? TileType.wallCrack : TileType.floorCrack;
            crack.crackTileset = (Tileset)tilesetId;

            // What the tile leaves behind when destroyed: a pit under dug ground (clears whatever was
            // there), loose small stones from a mined wall (half the time, non-clearing) — vanilla.
            // The stones carry OUR tileset id (vanilla walls do the same with theirs), so the pebbles
            // that appear render from the custom sheet's pebble art. Pits are data-only; vanilla
            // itself always writes them as Dirt, so the id is cosmetic-irrelevant there but kept
            // consistent.
            SpawnTileOnDeathAuthoring spawnOnDeath = Ensure<SpawnTileOnDeathAuthoring>(root);
            spawnOnDeath.tileType = isWall ? TileType.smallStones : TileType.pit;
            spawnOnDeath.tileset = isWall ? (Tileset)tilesetId : Tileset.Dirt;
            spawnOnDeath.spawnChance = isWall ? 0.5f : 1.0f;
            spawnOnDeath.clearOtherTiles = !isWall;

            // Exactly one of these, and the other actively removed: a prefab that somehow carried
            // both would be mineable AND diggable, which is not a state vanilla ever produces.
            Set<MineableAuthoring>(root, isWall);
            Set<DiggableAuthoring>(root, !isWall);

            // The marker Core Keeper's own square-looking blocks carry. It adds
            // IgnoreVertexOffsetsCD, which ShaderTexturesSystem writes into the global
            // IgnoreVertexOffsetTex — a world-window mask the tile shader samples to decide where
            // to skip its noise displacement. This, not any per-layer flag, is how vanilla exempts
            // something from the wobble.
            //
            // Set rather than Ensure because turning the toggle OFF has to take the marker away.
            // It used to only ever be added, so a block that was once rigid stayed rigid forever.
            Set<IgnoreVertexOffsetsAuthoring>(root, tileset.RigidSurface);

            ConfigurePlacement(root, tileset, isWall);
        }

        /// <summary>
        /// Tunes the PlaceableObjectAuthoring the Block archetype already added: show on the world
        /// map with the tileset's colour, and let the block fill a dug hole.
        /// </summary>
        /// <remarks>
        /// <c>canBePlacedOnPit</c> has to be set on BOTH objects, and the wall one is the load-bearing
        /// case. The player only ever holds the wall item; the ground object is hidden infrastructure.
        /// Placement validation reads the flag off the prefab in hand, so with it set only on the
        /// ground object the game rejected the placement outright (red hologram over the hole) and
        /// the ground-vs-wall resolution that would have consulted the ground object never ran.
        /// Vanilla's Dirt Block carries the flag on its wall object for the same reason.
        /// </remarks>
        private static void ConfigurePlacement(
            GameObject root,
            DimensionTilesetAsset tileset,
            bool isWall)
        {
            PlaceableObjectAuthoring placeable = root.GetComponent<PlaceableObjectAuthoring>();
            if (placeable == null)
            {
                return;
            }

            placeable.appearInMapUI = true;
            placeable.mapColor = isWall ? (Color)tileset.WallMapColor : (Color)tileset.GroundMapColor;
            placeable.canBePlacedOnPit = true;
        }

        private static T Ensure<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            if (component == null)
            {
                component = root.AddComponent<T>();
            }

            return component;
        }

        /// <summary>
        /// Makes the prefab's state match <paramref name="wanted"/> — adding the component when it
        /// should be there and REMOVING it when it should not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY THIS EXISTS RATHER THAN A BARE <see cref="Ensure{T}"/>. Generation updates prefabs in
        /// place, so anything an earlier run added survives until something takes it away. With
        /// add-only helpers the framework can grant a capability but never revoke one: switching
        /// "rigid surface" off and regenerating left <c>IgnoreVertexOffsetsAuthoring</c> exactly
        /// where it was, and the block kept rendering rigid while the dashboard said it should not.
        /// The toggle appeared broken; what was actually broken is that the generator was not
        /// authoritative over the state it owns.
        /// </para>
        /// <para>
        /// Any component the framework attaches CONDITIONALLY has to go through here. An add-only
        /// call is safe only for components that are unconditionally part of every block.
        /// </para>
        /// </remarks>
        private static void Set<T>(GameObject root, bool wanted) where T : Component
        {
            T component = root.GetComponent<T>();
            if (wanted)
            {
                if (component == null)
                {
                    root.AddComponent<T>();
                }

                return;
            }

            if (component != null)
            {
                // Routed through the one dependency-aware removal, which says so in the console
                // when a RequireComponent blocks it. It used to destroy the component outright:
                // harmless while nothing a block carries is required by anything else on it, and a
                // silent stale value the first time one is.
                DimensionObjectSpine.TryRemoveComponent<T>(root);
            }
        }
    }
}
