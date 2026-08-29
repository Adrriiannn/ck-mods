namespace ExpandNullforge.Api
{
    /// <summary>How loud a travel message is meant to be.</summary>
    /// <remarks>
    /// HELD, NOT LIVE. Nothing reads this yet: the write side publishes a travel snapshot on every
    /// travel event and no consumer has ever subscribed. It is a published ExpandNullforge.Api type,
    /// so a consumer mod could be using it today, and the whole travel-feedback set is finished or
    /// retired in one decision rather than one file at a time. The design is in
    /// Docs/travel-feedback-spec.md.
    /// </remarks>
    public enum DimensionTravelFeedbackSeverity
    {
        Hidden = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }
}
