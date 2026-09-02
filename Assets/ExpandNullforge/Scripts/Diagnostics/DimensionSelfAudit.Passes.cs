using System;
using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Api;
using ExpandNullforge.Foundation;
using Unity.Entities;
using UnityEngine;
using Ch = ExpandNullforge.Foundation.DimensionLogChannels;

namespace ExpandNullforge.Diagnostics
{
    /// <summary>
    /// The liveness and patch passes, and how their findings are worded.
    /// </summary>
    internal static partial class DimensionSelfAudit
    {
        // ---------------------------------------------------------------------- liveness pass ---

        private static void RunLivenessPass(ArmedWorld armed)
        {
            World world = armed.World;
            int silent = 0;
            int ticked = 0;
            int reported = 0;
            int switchedOff = 0;

            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].AppliesTo(armed.IsClient))
                {
                    continue;
                }

                ComponentSystemBase system = null;
                try
                {
                    system = rows[i].Find(world);
                }
                catch (Exception)
                {
                    system = null;
                }

                if (system == null)
                {
                    // Already reported by the registration pass.
                    continue;
                }

                if (system.LastSystemVersion != 0)
                {
                    ticked++;
                    continue;
                }

                silent++;

                if (!system.Enabled)
                {
                    // THE FRAMEWORK SWITCHES ONE OF THESE OFF ITSELF. The return-portal spawner is
                    // created disabled and woken when a player needs it, and a disabled system
                    // never advances its version — so without this it was a guaranteed failure
                    // line, on the first real run, offering two explanations that were both wrong.
                    switchedOff++;
                    DimensionLog.Trace(
                        Ch.Audit,
                        world,
                        rows[i].Name + " has not updated in " + world.Name + " because it is "
                            + "switched off. Something switches it on when it is needed; nothing "
                            + "is wrong here.");
                    continue;
                }

                if (!WorthReportingAsNeverRan(rows[i]))
                {
                    // A ZERO AND AN UNCOUNTABLE ARE BOTH "NOT A FAULT", and the second one is why
                    // this reads <= rather than ==. Eighteen of the rows have no way to count what
                    // is waiting on them; reported as Problems they were eighteen warnings whose
                    // own text admitted they might mean nothing, and they made the summary line
                    // below claim content was waiting when nothing had been counted.
                    DimensionLog.Trace(
                        Ch.Audit,
                        world,
                        rows[i].Name + " has never updated in " + world.Name + " in "
                            + FramesBeforeLivenessPass + " frames, and " + DescribeWork(rows[i])
                            + " A system with nothing waiting on it has nothing to wake it up.");
                    continue;
                }

                reported++;
                Problem(
                    armed,
                    Ch.Audit,
                    rows[i].Name + " has never updated in " + world.Name + " in "
                        + FramesBeforeLivenessPass + " frames, and " + DescribeWork(rows[i])
                        + " Either the group it is in never updates, or its RequireForUpdate has "
                        + "not been satisfied — the components it waits for have not appeared on "
                        + "anything. What stops working: " + rows[i].Capability + ".");
            }

            // ALWAYS A LINE, because "several of them have never run" is the normal state and the
            // reader has to be able to tell it apart from the state where that matters. A system
            // whose RequireForUpdate is never satisfied because nothing registered anything for it
            // is idle, not dead, and the two numbers say which is which.
            DimensionLog.Milestone(
                Ch.Audit,
                world,
                ticked + " framework systems have run in " + world.Name + " and " + silent
                    + " have not"
                    + (silent == 0
                        ? "."
                        : ". Of those " + silent + ", " + reported + " had rows registered and "
                            + (reported == 1 ? "is" : "are") + " named above as problems, "
                            + switchedOff + (switchedOff == 1 ? " is" : " are")
                            + " switched off on purpose, and the rest either had nothing "
                            + "registered for them or keep no total that can be counted. A system "
                            + "with nothing waiting on it has nothing to wake it up, which is not "
                            + "a fault."));

            RunPatchPass();
        }

        /// <summary>
        /// Reports the patches that never ran while something was waiting on them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Once per process, not once per world, and with no world on the line: Harmony patching is
        /// process-wide, a count cannot be attributed to a world, and several of these targets run
        /// in neither. Attaching one would be a claim the number does not support.
        /// </para>
        /// <para>
        /// BUT A TARGET THAT IS NOT IN THIS PROCESS IS NOT A FAILED PATCH. The roster's
        /// <see cref="DimensionPatchRoster.Row.Where"/> column names the side of the game each
        /// measured target lives on, and a row whose side has no world here is passed over. Without
        /// that, a dedicated server running a pack with biome atmosphere, a music roster or a named
        /// area reported up to five patches as broken — every one of them bound correctly, with
        /// nothing on a headless server for them to run against — and a player joined to somebody
        /// else's server got the mirror image for the dungeon and ambient-spawn patches. The
        /// suppression is worth exactly as much as the column is honest, which is why anything not
        /// read out of Core Keeper's own source stays <c>Both</c> and is still reported.
        /// </para>
        /// </remarks>
        private static void RunPatchPass()
        {
            if (patchPassDone)
            {
                return;
            }

            patchPassDone = true;

            int fired = 0;
            int idle = 0;
            int reported = 0;
            int elsewhere = 0;

            DimensionPatchRoster.Row[] rows = DimensionPatchRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                DimensionPatchRoster.Row row = rows[i];
                if (row.Fired() > 0)
                {
                    fired++;
                    continue;
                }

                idle++;
                if (row.OnlyOnPlayerAction)
                {
                    continue;
                }

                if (!ThisProcessHasAWorldFor(row.Where))
                {
                    elsewhere++;
                    continue;
                }

                // Wrapped for the same reason the system-side count is: a registry read that throws
                // here would take the whole audit down, and the answer to "it threw" is the same as
                // the answer to "nothing is waiting on it" — say nothing.
                int work;
                try
                {
                    work = row.CountWork == null ? 0 : row.CountWork();
                }
                catch (Exception)
                {
                    work = 0;
                }

                if (work <= 0)
                {
                    continue;
                }

                reported++;
                DimensionLog.Problem(
                    Ch.Patch,
                    null,
                    row.PatchClass + " has never run, and " + row.WorkName + " holds " + work
                        + " row" + (work == 1 ? "" : "s") + " that need it. It is declared against "
                        + row.Target + ". A patch that never runs is either one Harmony never bound "
                        + "— the mod loader prints \"failed to patch mod ExpandNullforge\" above "
                        + "this line when that happens, and PatchAll stops at the first target it "
                        + "cannot resolve, so everything declared after it is skipped too — or one "
                        + "the game simply never reached. What breaks either way: " + row.WhatBreaks
                        + ".");
            }

            DimensionLog.Milestone(
                Ch.Patch,
                null,
                fired + " of " + rows.Length + " Harmony patches have run; " + idle
                    + (idle == 1 ? " has" : " have") + " not, of which " + reported
                    + (reported == 1 ? " has" : " have")
                    + " content waiting on " + (reported == 1 ? "it" : "them")
                    + ". A patch that has not run and has nothing waiting "
                    + "on it is not a fault: most of them only run when a player does something."
                    + (elsewhere == 0
                        ? string.Empty
                        : " " + elsewhere + " of them patch " + (elsewhere == 1 ? "a part" : "parts")
                            + " of the game this process does not run — this session has "
                            + (sawServerWorld
                                ? "no client world, so nothing that is drawn on screen is here"
                                : "no server world, so nothing that generates or simulates a world "
                                    + "is here")
                            + " — and " + (elsewhere == 1 ? "it is" : "they are")
                            + " not counted above."));
        }

        /// <summary>
        /// Whether a world of the given side was ever attached in this process.
        /// </summary>
        /// <remarks>
        /// Two latches rather than a walk of the armed list, because a world that has already been
        /// forgotten still proves this process had one, and the patch pass runs five seconds after
        /// a world load rather than during it. <see cref="Reset"/> clears them, so a mod reload
        /// starts the question again.
        /// </remarks>
        private static bool ThisProcessHasAWorldFor(DimensionSystemRoster.Peer where)
        {
            if (where == DimensionSystemRoster.Peer.Client)
            {
                return sawClientWorld;
            }

            if (where == DimensionSystemRoster.Peer.Server)
            {
                return sawServerWorld;
            }

            return true;
        }

        // ---------------------------------------------------------------------------- helpers ---

        private static void Problem(ArmedWorld armed, string channel, string message)
        {
            armed.Problems++;
            DimensionLog.Problem(channel, armed.World, message);
        }

        /// <summary>
        /// Whether a system that has never updated is worth calling a failure.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ONLY WHEN SOMETHING IS WAITING ON IT. A system whose registry is empty has nothing to
        /// wake it up, and a system whose registry keeps no total is one nothing can say that about
        /// either way — both are traces, not problems. Reported as problems, the second group was
        /// eighteen of the thirty-one rows, each producing a warning whose own text admitted it
        /// might mean nothing.
        /// </para>
        /// <para>
        /// It is a method of its own, and internal, because it is the rule the roster has to be
        /// held to and a rule buried inside a loop cannot be. The patch side of this file has had
        /// the same rule enforced by a test since it was written; the system side did not, which is
        /// how the difference survived.
        /// </para>
        /// </remarks>
        internal static bool WorthReportingAsNeverRan(DimensionSystemRoster.Row row)
        {
            return row != null && WorkCount(row) > 0;
        }

        private static int WorkCount(DimensionSystemRoster.Row row)
        {
            if (row.CountWork == null)
            {
                return DimensionSystemRoster.WorkUnknown;
            }

            try
            {
                return row.CountWork();
            }
            catch (Exception)
            {
                return DimensionSystemRoster.WorkUnknown;
            }
        }

        /// <summary>
        /// What is waiting on a system, as a clause that starts lower case.
        /// </summary>
        /// <remarks>
        /// LOWER CASE BECAUSE IT IS SPLICED. Half the callers put it mid-sentence after "and", the
        /// other half start a sentence with it and pass it through <see cref="UpperFirst"/>. It
        /// used to start with a capital either way, so the mid-sentence sites read "…in 300 frames,
        /// and Nothing here can count…". The parenthetical went too: several rows describe their
        /// work in a whole clause ("nothing registers rows for this; it answers whatever clients
        /// send"), which cannot be the subject of "keeps no total".
        /// </remarks>
        private static string DescribeWork(DimensionSystemRoster.Row row)
        {
            int work = WorkCount(row);
            if (work == DimensionSystemRoster.WorkUnknown)
            {
                return "nothing here counts what is waiting on it — " + row.WorkName
                    + " — so this may or may not matter to this pack.";
            }

            if (work == 0)
            {
                return "nothing is registered in " + row.WorkName + ", so nothing is waiting on it.";
            }

            return row.WorkName + " holds " + work + " row" + (work == 1 ? "" : "s")
                + " that need it.";
        }

        private static string UpperFirst(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static int CountOf(System.Collections.IEnumerable rows)
        {
            return DimensionSystemRoster.CountOf(rows);
        }
    }
}
