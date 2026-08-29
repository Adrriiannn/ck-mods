using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Scenes
{
    /// <summary>
    /// The scene-anchoring arithmetic, in one place, deterministic on purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY A SCENE NEEDS AN EXPLICIT CENTRE. The game stamps a scene as
    /// <c>worldPos = anchor + (tilePos − centerPosition)</c> — the blob's centre IS the pivot,
    /// verbatim (<c>DungeonGenerateRoomsSystem.SpawnCustomScene</c>). Corridors run room-centre
    /// to room-centre and their band is always an odd tile count centred on that line. A scene
    /// registered without a centre pivots on its bottom-left corner: the room's "centre" is the
    /// corner, corridors meet the corner, and every room reads as shoved up-and-right — the
    /// classic off-centred dungeon. Passing the computed middle fixes all of it at the source.
    /// </para>
    /// <para>
    /// WHY floor AND NOT round. Vanilla's own auto-centre is
    /// <c>(int2)math.round((min+max)/2f)</c>, and <c>math.round</c> ties-to-even — so an
    /// even-extent scene's bias direction depends on where it happened to sit in authoring
    /// coordinates, which no author controls. Flooring picks the same side every time: an
    /// even-extent scene always carries its one extra tile on the +X/+Y side. Deterministic
    /// beats vanilla-identical here, and for odd extents (the recommended shape) the two agree
    /// exactly.
    /// </para>
    /// </remarks>
    public static class DimensionSceneGeometry
    {
        /// <summary>
        /// The tile the scene treats as its own middle: <c>floor((min+max)/2)</c> per axis over
        /// every painted tile. An empty scene centres on its origin.
        /// </summary>
        public static int2 CentreOf(IReadOnlyList<DimensionSceneTile> tiles)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return int2.zero;
            }

            int2 minimum = tiles[0].LocalPosition;
            int2 maximum = tiles[0].LocalPosition;
            for (int i = 1; i < tiles.Count; i++)
            {
                minimum = math.min(minimum, tiles[i].LocalPosition);
                maximum = math.max(maximum, tiles[i].LocalPosition);
            }

            // Integer floor for possibly-negative sums: >> 1 floors, / 2 truncates toward zero,
            // and a scene authored around a negative corner must not centre differently from the
            // same scene authored around a positive one.
            return new int2((minimum.x + maximum.x) >> 1, (minimum.y + maximum.y) >> 1);
        }

        /// <summary>
        /// The smallest room radius that contains every tile of the scene when it pivots on
        /// <paramref name="centre"/>: <c>ceil(max Euclidean distance)</c>.
        /// </summary>
        /// <remarks>
        /// Euclidean, not Chebyshev, because everything the radius protects is circular: the
        /// spawn-block circle, room-to-room spacing and corridor routing clearance are all
        /// distance checks against <c>room.size</c>. A radius from this function means the scene
        /// cannot overhang any of them. (The game itself never checks fit — an overhanging scene
        /// stamps fine and is then legally overwritten by neighbours and corridors.)
        /// </remarks>
        public static int FitRadius(IReadOnlyList<DimensionSceneTile> tiles, int2 centre)
        {
            if (tiles == null || tiles.Count == 0)
            {
                return 0;
            }

            float maxDistanceSq = 0f;
            for (int i = 0; i < tiles.Count; i++)
            {
                int2 offset = tiles[i].LocalPosition - centre;
                float distanceSq = (float)offset.x * offset.x + (float)offset.y * offset.y;
                if (distanceSq > maxDistanceSq)
                {
                    maxDistanceSq = distanceSq;
                }
            }

            return (int)math.ceil(math.sqrt(maxDistanceSq));
        }

        /// <summary>
        /// The corridor band the game actually carves for an authored width: always an odd tile
        /// count, centred on the room-to-room line — <c>2·floor(width/2 + 0.1) + 1</c>.
        /// </summary>
        /// <remarks>
        /// Measured from the containment test (<c>|across| ≤ width/2 + 0.1</c> over integer
        /// offsets): authored 2 carves 3 tiles, authored 4 carves 5. There is no even-width
        /// corridor and no half-tile offset — asking for one silently gets the next odd band.
        /// Normalizing the stored width to the effective count keeps the number the author sees
        /// equal to the tiles the player walks.
        /// </remarks>
        public static int EffectiveCorridorTiles(float authoredWidth)
        {
            float width = math.max(0.5f, authoredWidth);
            return 2 * (int)math.floor(width / 2f + 0.1f) + 1;
        }
    }
}
