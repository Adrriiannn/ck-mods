namespace ExpandNullforge.Api
{
    public readonly struct DimensionMapLayerChangedEvent
    {
        public readonly DimensionMapLayerDefinition PreviousLayer;
        public readonly DimensionMapLayerDefinition CurrentLayer;
        public readonly DimensionMapLayerChangeKind ChangeKind;
        public readonly string Reason;

        public DimensionMapLayerChangedEvent(
            DimensionMapLayerDefinition previousLayer,
            DimensionMapLayerDefinition currentLayer,
            DimensionMapLayerChangeKind changeKind,
            string reason)
        {
            PreviousLayer = previousLayer;
            CurrentLayer = currentLayer;
            ChangeKind = changeKind;
            Reason = reason ?? string.Empty;
        }
    }
}
