namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelLoopPreflightRequest
    {
        public readonly string SourceDimensionId;
        public readonly string TargetDimensionId;
        public readonly string EntryPortalId;
        public readonly string ReturnPortalId;
        public readonly string SourceAnchorId;
        public readonly string TargetAnchorId;
        public readonly string SourceMarkerId;
        public readonly string TargetMarkerId;
        public readonly DimensionBounds TargetLandingBounds;
        public readonly bool RequireReturnPortal;
        public readonly bool RequireSourceAnchor;
        public readonly bool RequireTargetAnchor;
        public readonly bool RequireMarkers;
        public readonly bool RequireTargetAreaReady;
        public readonly bool RequireMapLayers;

        public DimensionTravelLoopPreflightRequest(
            string sourceDimensionId,
            string targetDimensionId,
            string entryPortalId,
            string returnPortalId,
            string sourceAnchorId,
            string targetAnchorId,
            string sourceMarkerId,
            string targetMarkerId,
            DimensionBounds targetLandingBounds,
            bool requireReturnPortal,
            bool requireSourceAnchor,
            bool requireTargetAnchor,
            bool requireMarkers,
            bool requireTargetAreaReady,
            bool requireMapLayers)
        {
            SourceDimensionId = sourceDimensionId ?? string.Empty;
            TargetDimensionId = targetDimensionId ?? string.Empty;
            EntryPortalId = entryPortalId ?? string.Empty;
            ReturnPortalId = returnPortalId ?? string.Empty;
            SourceAnchorId = sourceAnchorId ?? string.Empty;
            TargetAnchorId = targetAnchorId ?? string.Empty;
            SourceMarkerId = sourceMarkerId ?? string.Empty;
            TargetMarkerId = targetMarkerId ?? string.Empty;
            TargetLandingBounds = targetLandingBounds;
            RequireReturnPortal = requireReturnPortal;
            RequireSourceAnchor = requireSourceAnchor;
            RequireTargetAnchor = requireTargetAnchor;
            RequireMarkers = requireMarkers;
            RequireTargetAreaReady = requireTargetAreaReady;
            RequireMapLayers = requireMapLayers;
        }
    }
}
