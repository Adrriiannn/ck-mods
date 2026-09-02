namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// The old logging front door. Every call it takes goes to <see cref="DimensionLog"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS A STAGING POST, NOT A DESIGN. A hundred and thirty call sites still ask for it by
    /// name and none of them names a subject, so everything through here prints on the
    /// <see cref="DimensionLogChannels.Legacy"/> channel. Each one moves to a real channel and a
    /// world as its subsystem is worked on; when the last one has, this file goes with it.
    /// </para>
    /// <para>
    /// WHAT IT FIXES TODAY. <c>VerboseRuntimeLogging</c> was a public field that nothing anywhere
    /// assigned, so its thirty-three <c>Verbose</c> calls and the five explicit checks of it could
    /// not produce output under any circumstances — and two of those dark lines were the only ones
    /// that would ever say the framework had bound to a world. A working framework and a framework
    /// that never loaded printed the same nothing. It is now the answer to "is anyone listening to
    /// detail", which a person can switch on from the command line or the config file.
    /// </para>
    /// <para>
    /// <c>Info</c> is a Milestone, not a Trace: the sixteen calls are one-line positive facts, which
    /// is exactly what a Milestone is, and burying them behind the same switch as the running
    /// commentary would leave a working session silent again.
    /// </para>
    /// </remarks>
    internal static class DimensionFrameworkLog
    {
        /// <summary>Whether any detail channel is switched on.</summary>
        /// <remarks>
        /// Read-only on purpose. It was a settable field and that is what made it dead: nothing set
        /// it, and nothing could — the class is internal, so not even a consumer mod could reach
        /// it. The switch is now <c>-nfchannels</c> on the command line or <c>channels</c> in
        /// <c>diagnostics-channels.json</c>.
        /// </remarks>
        public static bool VerboseRuntimeLogging
        {
            get { return DimensionLogConfig.TraceAny; }
        }

        public static void Verbose(string message)
        {
            DimensionLog.Trace(DimensionLogChannels.Legacy, null, message);
        }

        public static void Info(string message)
        {
            DimensionLog.Milestone(DimensionLogChannels.Legacy, null, message);
        }

        public static void Warning(string message)
        {
            DimensionLog.Problem(DimensionLogChannels.Legacy, null, message);
        }

        public static void Error(string message)
        {
            DimensionLog.Fatal(DimensionLogChannels.Legacy, null, message);
        }
    }
}
