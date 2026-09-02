namespace ExpandNullforge.Api
{
    /// <summary>
    /// Outcome of asking whether a dimension may change its local bounds in place.
    ///
    /// Resizing is allowed, but it is watched: growing a dimension can push its absolute
    /// footprint into a neighbour or into the protected overworld band, and shrinking one can cut
    /// away ground a player has already built on. Both cases are reported here rather than being
    /// applied silently.
    /// </summary>
    public readonly struct DimensionSlotResizeResult
    {
        public readonly bool Accepted;
        public readonly string Code;
        public readonly string Message;

        /// <summary>Dimension the proposed footprint would overlap, when a conflict blocked it.</summary>
        public readonly string ConflictingDimensionId;

        /// <summary>
        /// True when the resize itself is reasonable but cannot happen at the current origin, so
        /// the caller must allocate a new slot and move the dimension instead of refusing outright.
        /// </summary>
        public readonly bool RequiresRelocation;

        /// <summary>
        /// True when the new bounds no longer contain the old bounds, so tiles that exist today
        /// fall outside the dimension afterwards. Accepted, but the caller must warn.
        /// </summary>
        public readonly bool DiscardsContent;

        public DimensionSlotResizeResult(
            bool accepted,
            string code,
            string message,
            string conflictingDimensionId,
            bool requiresRelocation,
            bool discardsContent)
        {
            Accepted = accepted;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ConflictingDimensionId = conflictingDimensionId ?? string.Empty;
            RequiresRelocation = requiresRelocation;
            DiscardsContent = discardsContent;
        }

        public static DimensionSlotResizeResult Success(
            string code,
            string message,
            bool discardsContent)
        {
            return new DimensionSlotResizeResult(
                true,
                code,
                message,
                string.Empty,
                false,
                discardsContent);
        }

        public static DimensionSlotResizeResult Failed(
            string code,
            string message,
            string conflictingDimensionId,
            bool requiresRelocation)
        {
            return new DimensionSlotResizeResult(
                false,
                code,
                message,
                conflictingDimensionId,
                requiresRelocation,
                false);
        }
    }
}
