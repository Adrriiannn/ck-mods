namespace ExpandNullforge.Api
{
    public readonly struct DimensionResourceNodeChangedEvent
    {
        public readonly DimensionResourceNodeDefinition Node;
        public readonly DimensionResourceNodeChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionResourceNodeChangedEvent(
            DimensionResourceNodeDefinition node,
            DimensionResourceNodeChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Node = node;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
