using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Inclusive minimum / exclusive maximum integer tile bounds.
    /// </summary>
    public readonly struct DimensionBounds
    {
        public readonly int2 Min;
        public readonly int2 MaxExclusive;

        public DimensionBounds(int2 min, int2 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int2 tile)
        {
            return tile.x >= Min.x
                && tile.y >= Min.y
                && tile.x < MaxExclusive.x
                && tile.y < MaxExclusive.y;
        }

        public bool Contains(float2 position)
        {
            return position.x >= Min.x
                && position.y >= Min.y
                && position.x < MaxExclusive.x
                && position.y < MaxExclusive.y;
        }

        public int2 Size
        {
            get { return MaxExclusive - Min; }
        }
    }
}
