using System.Collections.Generic;
using ExpandNullforge.Api;
using PugTilemap;
using Unity.Mathematics;

namespace ExpandNullforge.Generation
{
    /// <summary>One resolved tile ready to write into the world: absolute position, type, tileset.</summary>
    public readonly struct DimensionResolvedTileWrite
    {
        public DimensionResolvedTileWrite(int2 absolutePosition, TileType tileType, int tileset)
        {
            AbsolutePosition = absolutePosition;
            TileType = tileType;
            Tileset = tileset;
        }

        public int2 AbsolutePosition { get; }

        public TileType TileType { get; }

        public int Tileset { get; }
    }

    /// <summary>What a compile produced: the writes to apply, and anything that could not resolve.</summary>
    public sealed class DimensionTileMapCompileResult
    {
        public readonly List<DimensionResolvedTileWrite> Writes =
            new List<DimensionResolvedTileWrite>();

        /// <summary>Placements skipped, each with a reason — never dropped silently.</summary>
        public readonly List<string> Skipped = new List<string>();

        /// <summary>
        /// Situations worth surfacing that did NOT cost a tile. Kept separate from
        /// <see cref="Skipped"/> so "we generated everything, but some of it will look wrong until a
        /// mod is installed" cannot be mistaken for "we failed to generate part of your dimension".
        /// </summary>
        public readonly List<string> Notes = new List<string>();

        public int WriteCount => Writes.Count;
    }

    /// <summary>
    /// Turns painted placements into resolved world-tile writes: maps each block's role to a
    /// Core Keeper <see cref="TileType"/>, resolves its tileset, and translates its
    /// dimension-local position into absolute world coordinates. Pure logic with no ECS or world
    /// access, so the error-prone parts — the coordinate translation, area clipping, and
    /// unresolved-tileset handling — are unit-testable offline; the generation system only has to
    /// apply the resulting list.
    /// </summary>
    public static class DimensionTileMapCompiler
    {
        /// <summary>
        /// Resolves placements against the area being generated. A placement outside the area is
        /// skipped (it belongs to a different generation pass); a custom block whose tileset does
        /// not resolve is skipped with a reason rather than defaulting to tileset 0.
        /// </summary>
        public static DimensionTileMapCompileResult Compile(
            IEnumerable<DimensionTilePlacement> placements,
            DimensionBounds areaLocalBounds,
            DimensionBounds areaAbsoluteBounds)
        {
            DimensionTileMapCompileResult result = new DimensionTileMapCompileResult();
            if (placements == null)
            {
                return result;
            }

            int2 localMin = areaLocalBounds.Min;
            int2 absoluteMin = areaAbsoluteBounds.Min;
            HashSet<string> reportedCustom = new HashSet<string>();

            foreach (DimensionTilePlacement placement in placements)
            {
                int2 local = placement.LocalPosition;
                if (!Contains(areaLocalBounds, local))
                {
                    // Belongs to a different pass/area; not this generation's job.
                    continue;
                }

                DimensionCompiledBlock block = placement.Block;
                if (!DimensionBlockTileMapping.TryResolveTileset(block, out int tileset))
                {
                    // The only unresolvable case left: a block flagged custom that names no tileset.
                    // Reported once per distinct id rather than once per painted tile.
                    if (reportedCustom.Add(block.CustomTilesetId))
                    {
                        result.Skipped.Add(
                            "A block is marked as using a custom tileset but names none, so its tiles " +
                            "were not generated. Pick a tileset for it, or switch it back to a vanilla one.");
                    }

                    continue;
                }

                // A tileset that is not installed still generates: its id comes from its name, so the
                // terrain is correct and only its appearance waits on the mod. Refusing would leave
                // holes — missing ground is a pit — and holes outlive the missing mod.
                if (DimensionBlockTileMapping.IsCustomTilesetMissing(block) &&
                    reportedCustom.Add(block.CustomTilesetId))
                {
                    result.Notes.Add(
                        "Custom tileset '" + block.CustomTilesetId +
                        "' is not installed in this session. Its tiles are generated with the correct " +
                        "identity and render as a placeholder until the mod that owns it is present.");
                }

                int2 absolute = absoluteMin + (local - localMin);

                // A painted Vein must bring its own wall, and the wall must be queued FIRST.
                // The ore tile type requires a wall at its cell; an ore Add with no wall is
                // rejected by the game's server, and the rejection drops the resolved ore as
                // a loose item — a hand-painted vein would silently mint ore on bare floor.
                // (The parallel gap — a wall painted with no ground beneath — is the map
                // author's to see; the game rejects it the same way but drops a wall block.)
                if (block.Role == DimensionTileRole.Vein)
                {
                    result.Writes.Add(new DimensionResolvedTileWrite(
                        absolute,
                        TileType.wall,
                        tileset));
                }

                result.Writes.Add(new DimensionResolvedTileWrite(
                    absolute,
                    DimensionBlockTileMapping.ToTileType(block.Role),
                    tileset));
            }

            return result;
        }

        private static bool Contains(DimensionBounds bounds, int2 position)
        {
            return position.x >= bounds.Min.x &&
                   position.y >= bounds.Min.y &&
                   position.x < bounds.MaxExclusive.x &&
                   position.y < bounds.MaxExclusive.y;
        }
    }
}
