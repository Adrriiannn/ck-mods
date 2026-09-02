using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Every runtime message this framework prints goes through here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE OUTPUT PRIMITIVE, THREE DESTINATIONS. <c>UnityEngine.Debug.Log*</c> is the only channel
    /// that reaches the player log, a dedicated server's stdout and the in-game console at once, so
    /// there is nothing to plumb and nothing that works in one place and not another.
    /// </para>
    /// <para>
    /// THREE SEVERITIES, NOT A DIAL. A Problem is something that will not work and always prints. A
    /// Milestone is a positive fact — this loaded, with these counts — and prints unless the session
    /// was asked to be quiet. Trace is the running commentary and prints only for the channels
    /// somebody switched on. Volume is not the question a tester has; the subject is.
    /// </para>
    /// <para>
    /// EVERY LINE NAMES ITS WORLD. Nine framework systems run in both the server and the client
    /// world, so on a host their lines appear twice; without the tag a duplicate is
    /// indistinguishable from a disagreement. The frame number orders the two worlds' interleaved
    /// output, which matters because Core Keeper's player log carries no timestamps.
    /// </para>
    /// <para>
    /// DEDUP IS PER WORLD, NOT PER PROCESS. Ten ad-hoc "only say this once" stores exist around the
    /// framework and every one of them is process-static, so on a host the server silences the
    /// client's identical warning, and quitting to the menu to retry a fix makes the two most
    /// useful lines never appear again. The store here is keyed by peer as well as by message and
    /// is cleared when a world goes away.
    /// </para>
    /// </remarks>
    internal static class DimensionLog
    {
        /// <summary>What separates the parts of a dedup key, so two keys cannot run together.</summary>
        private static readonly string Sep = ((char)1).ToString();

        private static readonly Dictionary<string, double> Said =
            new Dictionary<string, double>(StringComparer.Ordinal);

        private static readonly Dictionary<string, int> Counts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Something will not work. Always printed.</summary>
        public static void Problem(string channel, World world, string message)
        {
            Debug.LogWarning(Line(channel, world, message));
        }

        /// <summary>Something is dead rather than degraded. Always printed, as an error.</summary>
        public static void Fatal(string channel, World world, string message)
        {
            Debug.LogError(Line(channel, world, message));
        }

        /// <summary>A positive fact worth one line. Printed unless the session asked for quiet.</summary>
        public static void Milestone(string channel, World world, string message)
        {
            if (!DimensionLogConfig.Milestones)
            {
                return;
            }

            Debug.Log(Line(channel, world, message));
        }

        /// <summary>Detail. Printed only when this channel is switched on.</summary>
        /// <remarks>
        /// A caller whose message costs anything to build must ask <see cref="TraceOn"/> first;
        /// this cannot stop an argument that has already been evaluated.
        /// </remarks>
        public static void Trace(string channel, World world, string message)
        {
            if (!DimensionLogConfig.TraceOn(channel))
            {
                return;
            }

            Debug.Log(Line(channel, world, message));
        }

        /// <summary>Whether detail on this channel would print.</summary>
        public static bool TraceOn(string channel)
        {
            return DimensionLogConfig.TraceOn(channel);
        }

        /// <summary>A Problem said once per world for this key.</summary>
        public static void ProblemOnce(string channel, World world, string key, string message)
        {
            if (FirstTime(channel, world, key))
            {
                Problem(channel, world, message);
            }
        }

        /// <summary>A Fatal said once per world for this key.</summary>
        public static void FatalOnce(string channel, World world, string key, string message)
        {
            if (FirstTime(channel, world, key))
            {
                Fatal(channel, world, message);
            }
        }

        /// <summary>A Milestone said once per world for this key.</summary>
        public static void MilestoneOnce(string channel, World world, string key, string message)
        {
            if (FirstTime(channel, world, key))
            {
                Milestone(channel, world, message);
            }
        }

        /// <summary>
        /// A Problem said at most once every <paramref name="seconds"/> for this key.
        /// </summary>
        /// <remarks>
        /// For anything that repeats while a condition lasts — a trigger step that runs every tick,
        /// a boss phase that re-checks. Saying it once would hide that it is still happening; saying
        /// it every time buries everything else.
        /// </remarks>
        public static void ProblemEvery(
            string channel,
            World world,
            string key,
            float seconds,
            string message)
        {
            string full = Key(channel, world, key);
            double now = Now();
            double last;
            if (Said.TryGetValue(full, out last) && now - last < seconds)
            {
                return;
            }

            Said[full] = now;
            Problem(channel, world, message);
        }

        /// <summary>
        /// Counts an event instead of printing it, for anything that happens per submap or per
        /// entity. <see cref="DrainCounts"/> turns the tally into one line.
        /// </summary>
        public static void Count(string channel, string key)
        {
            string full = channel + Sep + key;
            int current;
            Counts.TryGetValue(full, out current);
            Counts[full] = current + 1;
        }

        /// <summary>What was counted on this channel, as "captured=118 restored=118", and clears it.</summary>
        public static string DrainCounts(string channel)
        {
            string prefix = channel + Sep;
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, int> pair in Counts)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keys.Add(pair.Key);
                }
            }

            keys.Sort(StringComparer.Ordinal);

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (text.Length > 0)
                {
                    text.Append(' ');
                }

                text.Append(keys[i].Substring(prefix.Length));
                text.Append('=');
                text.Append(Counts[keys[i]]);
                Counts.Remove(keys[i]);
            }

            return text.ToString();
        }

        /// <summary>How many times an event has been counted, without clearing it.</summary>
        public static int CountOf(string channel, string key)
        {
            int current;
            Counts.TryGetValue(channel + Sep + key, out current);
            return current;
        }

        /// <summary>
        /// Forgets everything said for a world, so reloading it says the same things again.
        /// </summary>
        public static void ResetForWorld(World world)
        {
            string tag = PeerTag(world) + Sep;
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, double> pair in Said)
            {
                if (pair.Key.StartsWith(tag, StringComparison.Ordinal))
                {
                    gone.Add(pair.Key);
                }
            }

            for (int i = 0; i < gone.Count; i++)
            {
                Said.Remove(gone[i]);
            }
        }

        /// <summary>Forgets everything, for a mod shutdown.</summary>
        public static void ResetAll()
        {
            Said.Clear();
            Counts.Clear();
        }

        /// <summary>
        /// Which side of the game a line came from.
        /// </summary>
        /// <remarks>
        /// <c>MOD</c> means process scope — before any world exists. <c>HOST</c> means the call site
        /// named no world while both exist, which is itself worth seeing, because it is a line that
        /// cannot say which side it is talking about.
        /// </remarks>
        public static string PeerTag(World world)
        {
            try
            {
                if (world != null)
                {
                    World client = API.Client == null ? null : API.Client.World;
                    return ReferenceEquals(world, client) ? "CLI" : "SRV";
                }

                bool server = API.Server != null && API.Server.World != null &&
                              API.Server.World.IsCreated;
                bool onClient = API.Client != null && API.Client.World != null &&
                                API.Client.World.IsCreated;
                if (server && onClient)
                {
                    return "HOST";
                }

                return server ? "SRV" : onClient ? "CLI" : "MOD";
            }
            catch (Exception)
            {
                // Asked from a context where the mod API is not up. The tag is not worth a throw.
                return "MOD";
            }
        }

        /// <summary>
        /// Prints an entry from the service's own diagnostic ring.
        /// </summary>
        /// <remarks>
        /// Sixty-three call sites write into that ring and nothing has ever read it. Errors and
        /// warnings become Problems; the forty-one Info entries become Trace on the service channel,
        /// because turning them all into console lines is how a scheme like this gets switched off.
        /// The ring keeps filling either way — the console gets the problems, the report file gets
        /// everything.
        /// </remarks>
        public static void OnServiceDiagnostic(DimensionDiagnosticEntry entry)
        {
            string where = string.IsNullOrEmpty(entry.DimensionId) ? "-" : entry.DimensionId;
            string message = entry.Message + " (dim=" + where + ", t=" +
                             entry.TimeSeconds.ToString("F1") + ")";

            if (entry.Severity == DimensionDiagnosticSeverity.Error ||
                entry.Severity == DimensionDiagnosticSeverity.Warning)
            {
                Problem(DimensionLogChannels.Service, null, message);
                return;
            }

            Trace(DimensionLogChannels.Service, null, message);
        }

        /// <summary>The shape of every line: channel, peer, frame, message.</summary>
        public static string Line(string channel, World world, string message)
        {
            return "[NF/" + (channel ?? DimensionLogChannels.Legacy) + "][" + PeerTag(world) +
                   " f" + FrameCount() + "] " + message;
        }

        private static bool FirstTime(string channel, World world, string key)
        {
            string full = Key(channel, world, key);
            if (Said.ContainsKey(full))
            {
                return false;
            }

            Said[full] = Now();
            return true;
        }

        private static string Key(string channel, World world, string key)
        {
            return PeerTag(world) + Sep + (channel ?? string.Empty) + Sep +
                   (key ?? string.Empty);
        }

        private static double Now()
        {
            try
            {
                return Time.realtimeSinceStartupAsDouble;
            }
            catch (Exception)
            {
                // Off the main thread there is no clock. Rate limiting degrades to "say it".
                return 0d;
            }
        }

        private static int FrameCount()
        {
            try
            {
                return Time.frameCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
