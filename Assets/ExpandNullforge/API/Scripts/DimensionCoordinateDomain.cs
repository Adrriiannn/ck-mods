using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Coordinate presentation domain for a dimension.
    ///
    /// The playable bounds describe where dimension gameplay is valid. The
    /// coordinate bounds may be slightly larger, so map cursors and
    /// dimension-aware commands keep showing local coordinates near the edge
    /// instead of snapping back to raw Core Keeper absolute coordinates.
    /// </summary>
    public readonly struct DimensionCoordinateDomain
    {
        public readonly bool IsValid;
        public readonly string DimensionId;
        public readonly int2 AbsoluteOrigin;
        public readonly DimensionBounds PlayableLocalBounds;
        public readonly DimensionBounds PlayableAbsoluteBounds;
        public readonly DimensionBounds CoordinateLocalBounds;
        public readonly DimensionBounds CoordinateAbsoluteBounds;
        public readonly int PaddingTiles;
        public readonly DimensionSpaceKind SpaceKind;

        public DimensionCoordinateDomain(
            string dimensionId,
            int2 absoluteOrigin,
            DimensionBounds playableLocalBounds,
            DimensionBounds coordinateLocalBounds,
            int paddingTiles,
            DimensionSpaceKind spaceKind)
        {
            IsValid = !string.IsNullOrEmpty(dimensionId);
            DimensionId = dimensionId;
            AbsoluteOrigin = absoluteOrigin;
            PlayableLocalBounds = playableLocalBounds;
            PlayableAbsoluteBounds =
                new DimensionBounds(
                    absoluteOrigin + playableLocalBounds.Min,
                    absoluteOrigin + playableLocalBounds.MaxExclusive);
            CoordinateLocalBounds = coordinateLocalBounds;
            CoordinateAbsoluteBounds =
                new DimensionBounds(
                    absoluteOrigin + coordinateLocalBounds.Min,
                    absoluteOrigin + coordinateLocalBounds.MaxExclusive);
            PaddingTiles = math.max(0, paddingTiles);
            SpaceKind = spaceKind;
        }

        public bool IsOverworld
        {
            get { return DimensionId == DimensionIds.Overworld; }
        }

        public bool ContainsPlayableAbsolute(float2 absolutePosition)
        {
            return PlayableAbsoluteBounds.Contains(absolutePosition);
        }

        public bool ContainsCoordinateAbsolute(float2 absolutePosition)
        {
            return CoordinateAbsoluteBounds.Contains(absolutePosition);
        }

        public bool ContainsCoordinateLocal(float2 localPosition)
        {
            return CoordinateLocalBounds.Contains(localPosition);
        }

        public float2 ToLocal(float2 absolutePosition)
        {
            return absolutePosition - (float2)AbsoluteOrigin;
        }

        public float2 ToAbsolute(float2 localPosition)
        {
            return (float2)AbsoluteOrigin + localPosition;
        }
    }
}
