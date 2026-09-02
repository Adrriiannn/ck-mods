using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>The tile surface a returning creature appears on, in player words.</summary>
    /// <remarks>
    /// The four surfaces vanilla's own respawn files key on, and nothing else: every shipped
    /// rule is chrysalis, groundSlime, plain ground or water. The mapping to the game's
    /// <c>TileType</c> values lives in the generator, the authoring pattern's one rule.
    /// </remarks>
    public enum DimensionRespawnSurface
    {
        /// <summary>Plain walkable ground of the chosen block.</summary>
        Ground = 0,

        /// <summary>The fleshy nest tile — how hives keep producing larvae.</summary>
        Nest = 1,

        /// <summary>The slimy coating — how slime pits keep producing slimes.</summary>
        SlimeCoat = 2,

        /// <summary>Water of the chosen block.</summary>
        Water = 3
    }

    /// <summary>
    /// The creature keeps coming back: a kind of tile breeds it, forever.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the mechanism that keeps vanilla dungeons alive after their first garrison
    /// dies: the world sweeps roughly every fifteen minutes wherever a player is within 200
    /// tiles, and every tile matching the rule rolls the chance. It is the honest answer to
    /// "my dungeon's monsters should return" — the one-time garrison never does.
    /// </para>
    /// <para>
    /// The rule keys on a SURFACE, not a place: every matching tile in the world breeds the
    /// creature, wherever that tile exists. Scoping is by block — a rule on your own tileset
    /// only ever fires where your tileset was placed, which in practice means your dimension
    /// and your dungeons.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionRespawnTemplate
    {
        [Tooltip("It keeps coming back: tiles of the chosen kind breed it over time, the way " +
                 "hive nests keep making larvae. Without this, placed creatures spawn once " +
                 "and stay dead.")]
        [SerializeField] private bool keepsComingBack;

        [Tooltip("Whose tiles breed it: one of your tilesets by name, or a vanilla one " +
                 "(Dirt, Stone, Nature, Clay, City, Desert, Crystal...). Empty means any " +
                 "tileset — usually far too broad.")]
        [SerializeField] private string onTileset = string.Empty;

        [Tooltip("The kind of tile: plain Ground, a hive Nest, a slimy SlimeCoat, or Water.")]
        [SerializeField] private DimensionRespawnSurface surface = DimensionRespawnSurface.Ground;

        [Tooltip("How likely each matching tile is per sweep, 0 to 1. The game's own rules " +
                 "run 0.005 for rare brutes up to 0.3 for common mobs.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.15f;

        [Tooltip("Most that can appear in one sweep of an area.")]
        [Min(1)]
        [SerializeField] private int mostPerSweep = 3;

        [Tooltip("Crowd limit: most of them per matching tile. The game's rules run 0.02 " +
                 "to 0.15 — well under one per tile.")]
        [Min(0f)]
        [SerializeField] private float mostPerTile = 0.15f;

        [Tooltip("Fewest matching tiles an area needs before any appear. 0 means no floor.")]
        [Min(0f)]
        [SerializeField] private float fewestTilesNeeded;

        [Tooltip("How strongly a crowd slows new arrivals, 0 to 1: the chance is multiplied " +
                 "by (1 minus this) once per one already alive.")]
        [Range(0f, 1f)]
        [SerializeField] private float crowdSlowdown = 0.3f;

        public bool KeepsComingBack
        {
            get { return keepsComingBack; }
        }

        public string OnTileset
        {
            get { return onTileset ?? string.Empty; }
        }

        public DimensionRespawnSurface Surface
        {
            get { return surface; }
        }

        public float Chance
        {
            get { return Mathf.Clamp01(chance); }
        }

        public int MostPerSweep
        {
            get { return mostPerSweep < 1 ? 1 : mostPerSweep; }
        }

        public float MostPerTile
        {
            get { return mostPerTile < 0f ? 0f : mostPerTile; }
        }

        public float FewestTilesNeeded
        {
            get { return fewestTilesNeeded < 0f ? 0f : fewestTilesNeeded; }
        }

        public float CrowdSlowdown
        {
            get { return Mathf.Clamp01(crowdSlowdown); }
        }
    }
}
