using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionArea
    {
        public readonly string DimensionId;
        public readonly DimensionBounds LocalBounds;
        public readonly DimensionBounds AbsoluteBounds;

        public DimensionArea(string dimensionId, DimensionBounds localBounds, DimensionBounds absoluteBounds)
        {
            DimensionId = dimensionId ?? string.Empty;
            LocalBounds = localBounds;
            AbsoluteBounds = absoluteBounds;
        }

        public bool ContainsLocal(int2 tile)
        {
            return LocalBounds.Contains(tile);
        }

        public bool ContainsAbsolute(int2 tile)
        {
            return AbsoluteBounds.Contains(tile);
        }
    }
}
