namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// The door the C# this framework writes into somebody else's mod prints through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY IT IS PUBLIC AND <see cref="DimensionLog"/> IS NOT. The generated consumer bootstrap is
    /// compiled into the consumer's own assembly, so nothing internal to this one is in reach. The
    /// emitter must not write <c>Debug.LogWarning</c> as text straight into that file:
    /// a mod built that way ships ungated raw logging with a
    /// prefix of its own, and a player with three such mods installed gets three different prefixes
    /// and no way to quieten any of them.
    /// </para>
    /// <para>
    /// The dimension id goes in the message rather than in a prefix, because the prefix is the
    /// channel and the peer, and a reader following one mod's lines wants to filter on the same
    /// thing whether the line came from the framework or from the mod built on it.
    /// </para>
    /// </remarks>
    public static class DimensionConsumerLog
    {
        /// <summary>Something in this mod's content will not work. Always printed.</summary>
        public static void Problem(string dimensionId, string message)
        {
            DimensionLog.Problem(DimensionLogChannels.Registry, null, Say(dimensionId, message));
        }

        /// <summary>
        /// The same, said once per world for a given key, so a bootstrap that retries every frame
        /// does not repeat itself.
        /// </summary>
        public static void ProblemOnce(string dimensionId, string key, string message)
        {
            DimensionLog.ProblemOnce(
                DimensionLogChannels.Registry,
                null,
                dimensionId + "/" + key,
                Say(dimensionId, message));
        }

        /// <summary>A one-line positive fact about this mod's content.</summary>
        public static void Milestone(string dimensionId, string message)
        {
            DimensionLog.Milestone(DimensionLogChannels.Registry, null, Say(dimensionId, message));
        }

        private static string Say(string dimensionId, string message)
        {
            return string.IsNullOrEmpty(dimensionId)
                ? message ?? string.Empty
                : dimensionId + ": " + (message ?? string.Empty);
        }
    }
}
