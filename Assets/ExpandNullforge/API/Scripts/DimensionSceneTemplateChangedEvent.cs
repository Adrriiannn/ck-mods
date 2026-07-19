namespace ExpandNullforge.Api
{
    public readonly struct DimensionSceneTemplateChangedEvent
    {
        public readonly DimensionSceneTemplateDefinition Template;
        public readonly DimensionSceneTemplateChangeKind ChangeKind;
        public readonly bool PreviousEnabled;
        public readonly bool CurrentEnabled;
        public readonly string Reason;

        public DimensionSceneTemplateChangedEvent(
            DimensionSceneTemplateDefinition template,
            DimensionSceneTemplateChangeKind changeKind,
            bool previousEnabled,
            bool currentEnabled,
            string reason)
        {
            Template = template;
            ChangeKind = changeKind;
            PreviousEnabled = previousEnabled;
            CurrentEnabled = currentEnabled;
            Reason = reason ?? string.Empty;
        }
    }
}
