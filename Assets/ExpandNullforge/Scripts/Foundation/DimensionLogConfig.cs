using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Which diagnostic lines this session prints, and where that was decided.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THREE DOORS, LATER WINS. The config file is read first, then the command line, because the
    /// command line is what somebody types when the config file did not do what they wanted. Both
    /// are read once, in <c>EarlyInit</c>, into plain statics, so asking whether a channel is on
    /// costs one set lookup and asking whether milestones print costs a bool.
    /// </para>
    /// <para>
    /// THE COMMAND LINE IS THE ONLY DOOR THAT WORKS ON A DEDICATED SERVER before its first launch,
    /// which is the case this scheme exists for. Core Keeper's own <c>CommandLineArgs</c> is a
    /// global static and parses <c>-flag value</c> pairs, so <c>-nfchannels portal,travel</c> is the
    /// form it understands; <c>-nfchannels=portal,travel</c> is read too, by walking the arguments,
    /// because that is the form people type.
    /// </para>
    /// <para>
    /// NOTHING HERE MAY STOP THE MOD LOADING. A missing config file, a game that never called
    /// <c>CommandLineArgs.Init</c>, a mod API that is not up yet — each is caught and leaves the
    /// defaults in place, because a diagnostic switch that can fail the load is worse than no
    /// switch at all.
    /// </para>
    /// </remarks>
    internal static class DimensionLogConfig
    {
        /// <summary>The mod name the config file is filed under.</summary>
        public const string ConfigMod = "ExpandNullforge";

        /// <summary>The section within it.</summary>
        public const string ConfigSection = "diagnostics";

        private static readonly HashSet<string> Channels =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool loaded;

        /// <summary>Whether every channel is on.</summary>
        public static bool AllChannels { get; private set; }

        /// <summary>Whether the one-line positive facts print. On by default.</summary>
        public static bool Milestones { get; private set; } = true;

        /// <summary>Whether the world-load self-audit runs. Read by the audit, not by the log.</summary>
        public static bool Audit { get; private set; } = true;

        /// <summary>Whether the report file is written on every world load.</summary>
        public static bool Report { get; private set; }

        /// <summary>
        /// Whether the audit checks the mod's own objects against the game's own queries.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Audit"/> because it is the only part with a cost that scales
        /// with how much content is installed: one prefab query and a few thousand component
        /// lookups per world load. Somebody with a very large pack who has already seen it come
        /// back clean can turn just this off and keep the rest.
        /// </remarks>
        public static bool EntityAudit { get; private set; } = true;

        /// <summary>How many of the mod's objects the entity check looks at before it stops.</summary>
        /// <remarks>
        /// Four hundred is above every pack anybody has built with this so far, so the ordinary
        /// case checks everything. It exists so that a pack ten times that size cannot turn a world
        /// load into a stall, and when it does stop early the audit says so and names this setting.
        /// </remarks>
        public static int EntityAuditBudget { get; private set; } = 400;

        /// <summary>Whether any Trace channel at all is on.</summary>
        /// <remarks>
        /// This is what <see cref="DimensionFrameworkLog.VerboseRuntimeLogging"/> answers, so the
        /// five call sites that ask it directly keep meaning what they meant: "is anyone listening
        /// to detail". They are re-asked per channel as they move over.
        /// </remarks>
        public static bool TraceAny
        {
            get { return AllChannels || Channels.Count > 0; }
        }

        /// <summary>Whether detail on this channel prints.</summary>
        public static bool TraceOn(string channel)
        {
            return AllChannels || (channel != null && Channels.Contains(channel));
        }

        /// <summary>Reads both doors. Safe to call more than once; only the first does anything.</summary>
        public static void Load()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            ReadConfigFile();
            ReadCommandLine();
        }

        /// <summary>Forgets what was read, so a reloaded mod reads it again.</summary>
        public static void Reset()
        {
            loaded = false;
            AllChannels = false;
            Channels.Clear();
            Milestones = true;
            Audit = true;
            Report = false;
            EntityAudit = true;
            EntityAuditBudget = 400;
        }

        /// <summary>
        /// Turns channels on from a comma-separated list. <c>*</c> means all of them.
        /// </summary>
        /// <remarks>
        /// Public so a console command and a test can use the same parser as the two doors, rather
        /// than a third reading of the same string.
        /// </remarks>
        public static void SetChannels(string commaSeparated)
        {
            Channels.Clear();
            AllChannels = false;
            if (string.IsNullOrEmpty(commaSeparated))
            {
                return;
            }

            string[] parts = commaSeparated.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                if (name == "*")
                {
                    AllChannels = true;
                    continue;
                }

                Channels.Add(name);
            }
        }

        /// <summary>Every channel currently switched on, for the report header and for a test.</summary>
        public static string DescribeChannels()
        {
            if (AllChannels)
            {
                return "*";
            }

            if (Channels.Count == 0)
            {
                return "none";
            }

            List<string> names = new List<string>(Channels);
            names.Sort(StringComparer.Ordinal);
            return string.Join(",", names.ToArray());
        }

        private static void ReadConfigFile()
        {
            try
            {
                if (API.Config == null)
                {
                    return;
                }

                // Register writes the file with the default and the description the first time the
                // mod runs, so the switches document themselves once the mod is installed.
                SetChannels(API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "Which kinds of detail to print, comma separated, for example " +
                    "portal,travel,tileset. Use * for all of them, or leave it empty for none. " +
                    "Problems always print whatever this says.",
                    "channels",
                    string.Empty).Value);

                Milestones = API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "Print the one-line facts that say what loaded, what bound and how many of " +
                    "each. Turning this off makes a working session silent.",
                    "milestones",
                    true).Value;

                Audit = API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "Check the framework's own wiring when a world loads and say what is wrong.",
                    "audit",
                    true).Value;

                Report = API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "Write a full report file next to the dimension save files every time a world " +
                    "loads. Off by default because writing it can stall the game briefly.",
                    "report",
                    false).Value;

                EntityAudit = API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "Check that the objects this mod put into the game carry everything the " +
                    "game's own systems ask for before they will look at them.",
                    "entityAudit",
                    true).Value;

                EntityAuditBudget = API.Config.Register(
                    ConfigMod,
                    ConfigSection,
                    "How many of this mod's objects that check looks at before it stops.",
                    "entityAuditBudget",
                    400).Value;
            }
            catch (Exception)
            {
                // A missing or unreadable config file leaves the defaults standing. It must never
                // be the reason the mod does not load.
            }
        }

        private static void ReadCommandLine()
        {
            try
            {
                int count = CommandLineArgs.GetArgCount();
                for (int i = 0; i < count; i++)
                {
                    string arg = CommandLineArgs.GetArg(i);
                    if (string.IsNullOrEmpty(arg))
                    {
                        continue;
                    }

                    if (arg.StartsWith("-nfchannels=", StringComparison.OrdinalIgnoreCase))
                    {
                        SetChannels(arg.Substring("-nfchannels=".Length));
                        continue;
                    }

                    if (string.Equals(arg, "-nfchannels", StringComparison.OrdinalIgnoreCase) &&
                        i + 1 < count)
                    {
                        SetChannels(CommandLineArgs.GetArg(i + 1));
                        continue;
                    }

                    if (string.Equals(arg, "-nfquiet", StringComparison.OrdinalIgnoreCase))
                    {
                        Milestones = false;
                        continue;
                    }

                    if (string.Equals(arg, "-nfnoaudit", StringComparison.OrdinalIgnoreCase))
                    {
                        Audit = false;
                        continue;
                    }

                    if (string.Equals(arg, "-nfnoentityaudit", StringComparison.OrdinalIgnoreCase))
                    {
                        EntityAudit = false;
                        continue;
                    }

                    if (string.Equals(arg, "-nfreport", StringComparison.OrdinalIgnoreCase))
                    {
                        Report = true;
                    }
                }
            }
            catch (Exception)
            {
                // CommandLineArgs holds a static array the game fills in at start-up. In an editor
                // play session or a test it can be empty, and reading it must not matter.
            }
        }
    }
}
