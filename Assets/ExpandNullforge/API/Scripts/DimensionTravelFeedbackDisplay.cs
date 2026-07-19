namespace ExpandNullforge.Api
{
    public readonly struct DimensionTravelFeedbackDisplay
    {
        public readonly bool Visible;
        public readonly DimensionTravelFeedbackSeverity Severity;
        public readonly string Title;
        public readonly string Message;
        public readonly string Detail;
        public readonly string PrimaryActionLabel;
        public readonly bool CanCancel;
        public readonly bool CanDismiss;

        public DimensionTravelFeedbackDisplay(
            bool visible,
            DimensionTravelFeedbackSeverity severity,
            string title,
            string message,
            string detail,
            string primaryActionLabel,
            bool canCancel,
            bool canDismiss)
        {
            Visible = visible;
            Severity = severity;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            Detail = detail ?? string.Empty;
            PrimaryActionLabel = primaryActionLabel ?? string.Empty;
            CanCancel = canCancel;
            CanDismiss = canDismiss;
        }

        public static DimensionTravelFeedbackDisplay Hidden()
        {
            return new DimensionTravelFeedbackDisplay(
                false,
                DimensionTravelFeedbackSeverity.Hidden,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false);
        }
    }
}
