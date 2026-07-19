using Unity.Mathematics;

namespace ExpandNullforge.Api
{
    public readonly struct DimensionDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int2 AbsoluteOrigin;
        public readonly DimensionBounds LocalBounds;
        public readonly int GenerationVersion;
        public readonly DimensionSpaceKind SpaceKind;
        public readonly DimensionCapabilityFlags Capabilities;
        public readonly DimensionLifecycleState LifecycleState;

        public DimensionDefinition(
            string id,
            string displayName,
            int2 absoluteOrigin,
            DimensionBounds localBounds,
            int generationVersion)
            : this(
                id,
                displayName,
                absoluteOrigin,
                localBounds,
                generationVersion,
                id == DimensionIds.Overworld ? DimensionSpaceKind.Overworld : DimensionSpaceKind.PocketWorld,
                DimensionCapabilityFlags.LocalCoordinates
                    | DimensionCapabilityFlags.AbsoluteCoordinates
                    | DimensionCapabilityFlags.PlayerContext
                    | DimensionCapabilityFlags.Map
                    | DimensionCapabilityFlags.Minimap
                    | DimensionCapabilityFlags.Markers
                    | DimensionCapabilityFlags.CoordinateInterop,
                DimensionLifecycleState.Registered)
        {
        }

        public DimensionDefinition(
            string id,
            string displayName,
            int2 absoluteOrigin,
            DimensionBounds localBounds,
            int generationVersion,
            DimensionSpaceKind spaceKind,
            DimensionCapabilityFlags capabilities,
            DimensionLifecycleState lifecycleState)
        {
            Id = id;
            DisplayName = displayName;
            AbsoluteOrigin = absoluteOrigin;
            LocalBounds = localBounds;
            GenerationVersion = generationVersion;
            SpaceKind = spaceKind;
            Capabilities = capabilities;
            LifecycleState = lifecycleState;
        }

        public DimensionBounds AbsoluteBounds
        {
            get { return new DimensionBounds(AbsoluteOrigin + LocalBounds.Min, AbsoluteOrigin + LocalBounds.MaxExclusive); }
        }

        public bool HasCapability(DimensionCapabilityFlags capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool ContainsLocal(float2 localPosition)
        {
            return LocalBounds.Contains(localPosition);
        }

        public bool ContainsAbsolute(float2 absolutePosition)
        {
            return LocalBounds.Contains(absolutePosition - (float2)AbsoluteOrigin);
        }

        public float2 ToLocal(float2 absolutePosition)
        {
            return absolutePosition - (float2)AbsoluteOrigin;
        }

        public float2 ToAbsolute(float2 localPosition)
        {
            return (float2)AbsoluteOrigin + localPosition;
        }

        public DimensionDefinition WithLifecycleState(DimensionLifecycleState lifecycleState)
        {
            return new DimensionDefinition(
                Id,
                DisplayName,
                AbsoluteOrigin,
                LocalBounds,
                GenerationVersion,
                SpaceKind,
                Capabilities,
                lifecycleState);
        }
    }
}
