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
    /// Checks the framework's own wiring when a world loads, and says what is wrong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY. The one recorded in-game session with this framework loaded printed eight lines all
    /// session: six about tilesets, two about the mod loading. Dimensions, portals, travel, scenes,
    /// dungeons, biomes, creatures, plants, food, loot, conditions and persistence said nothing at
    /// all — and a framework working perfectly and a framework that never bound to a world produce
    /// exactly that. This makes the difference visible without anybody adding a log and running
    /// again.
    /// </para>
    /// <para>
    /// IT RUNS TWICE, AND THE SECOND TIME IS THE POINT. The registration pass answers "is it
    /// wired": which systems exist, which are in a group's update list, whether the tile-rescue
    /// bracket came out in the right order, whether the objects this mod put in the game carry what
    /// the game's systems ask for. The liveness pass, five seconds later, answers the question
    /// frame zero cannot: did any of it actually run. <c>ComponentSystemBase.LastSystemVersion</c>
    /// is public and advances whenever a system updates, so nothing had to be instrumented for
    /// that half.
    /// </para>
    /// <para>
    /// IT WAITS FOR CONTENT TO STOP ARRIVING. The generated bootstrap registers content across
    /// frames behind a service gate with a sixty-frame retry, so an audit at world attach reads
    /// empty registries and reports failures that are not there. A world is armed when it attaches
    /// and audited on the first frame after item declarations have been quiet for
    /// <see cref="FramesToWaitAfterLastDeclaration"/> frames. It keeps that wait itself rather than
    /// riding the item report's, because the item report latches once per SESSION and this has to
    /// run once per WORLD — quit to the menu, fix something, come back, and the second world needs
    /// auditing too. Both waits are the same length and this one is driven first, so the audit's
    /// lines land above the item report's.
    /// </para>
    /// <para>
    /// SILENCE IS EARNED, NOT ASSUMED. When everything is right the whole audit is a handful of
    /// Milestone lines with numbers in them, and with milestones switched off it says nothing. When
    /// something is wrong it names the thing, the state that proves it, and what to do. A line that
    /// says "failed" without those three is a bug in this file.
    /// </para>
    /// <para>
    /// COST. One walk of <c>world.Systems</c>, one prefab query, about thirty registry reads and a
    /// few thousand <c>HasComponent</c> calls, once per world; then thirty-one integer reads and
    /// thirty-three more, once, five seconds later. Nothing is left running afterwards and nothing
    /// runs at all when the audit is switched off.
    /// </para>
    /// <para>
    /// WHAT NOBODY HAS WATCHED THIS DO. The one session that established that Core Keeper schedules
    /// a mod's systems by itself was a client joining a dedicated server: it had a
    /// <c>ClientWorld0</c> and no <c>ServerWorld</c> at all. A server world is built by the same
    /// <c>ClientServerBootstrap</c> path from the same sweep, so there is no reason to expect it to
    /// differ, but that is a reading rather than an observation. It is recorded here rather than
    /// printed because it is a fact about how far this framework has been tested and not a fact
    /// about the world in front of a player, and a constant cannot notice the day somebody does
    /// watch one.
    /// </para>
    /// </remarks>
    internal static partial class DimensionSelfAudit
    {
        /// <summary>
        /// How long after the registration pass the liveness pass runs, in frames.
        /// </summary>
        /// <remarks>
        /// About five seconds at sixty frames a second. Long enough that a system whose
        /// <c>RequireForUpdate</c> is satisfied has certainly updated, short enough that the answer
        /// arrives while the tester is still watching the load.
        /// </remarks>
        public const int FramesBeforeLivenessPass = 300;

        /// <summary>
        /// How many frames with no new item declaration count as "content has finished arriving".
        /// </summary>
        /// <remarks>
        /// The same wait the item report uses, for the same reason: the generated bootstrap emits
        /// its Declare call behind a service gate that retries across frames, so an audit that runs
        /// on the first frame a world exists reads empty registries and reports failures that are
        /// not there. The only cost of waiting is how late a genuinely missing thing is named.
        /// </remarks>
        public const int FramesToWaitAfterLastDeclaration = 10;

        private sealed class ArmedWorld
        {
            public World World;
            public bool IsClient;
            public bool RegistrationDone;
            public bool LivenessDone;
            public int LivenessDueFrame;
            public int Problems;
        }

        private static readonly List<ArmedWorld> Armed = new List<ArmedWorld>();

        private static readonly Dictionary<string, int> LastWorldCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static int lastWorldDeclarationVersion = -1;
        private static int worldsAudited;
        private static bool patchPassDone;
        private static bool processChecksDone;
        private static bool entityAuditDone;
        private static bool checkedNothing;

        /// <summary>
        /// Set when the object check ran and its subject list held items and nothing else.
        /// </summary>
        /// <remarks>
        /// The world's own "found nothing wrong" reads it, because that sentence is the one most
        /// people will see and it was the last place the narrowness of the list was not mentioned.
        /// It is not <see cref="checkedNothing"/>: that one means no pack declared anything, and
        /// this one means a pack declared items only — which looks identical in a count and is a
        /// different thing to be told.
        /// </remarks>
        private static bool objectCheckSawItemsOnly;

        /// <summary>
        /// Whether the object half of this pass was skipped in the world being summarised.
        /// </summary>
        /// <remarks>
        /// A third thing, and not either of the two above. <see cref="checkedNothing"/> means no
        /// pack declared anything; <see cref="objectCheckSawItemsOnly"/> means it ran and had only
        /// items to look at. This one means it did not run here at all — switched off, or already
        /// answered in an earlier world, or a client world where the question is not asked. Without
        /// it the verdict for such a world reads exactly like the verdict for a world where the
        /// check ran and found nothing, which is the difference this whole pass exists to keep.
        /// </remarks>
        private static bool objectCheckDidNotRunHere;

        private static int lastSeenDeclarationVersion = -1;
        private static int quietFrames;
        private static bool sawClientWorld;
        private static bool sawServerWorld;

        /// <summary>
        /// How many object findings are printed before the rest are counted instead.
        /// </summary>
        /// <remarks>
        /// The budget caps how many objects are examined, not how many things are said about them:
        /// a pack whose generator dropped one component on every object could produce a finding per
        /// object per rule, each one a paragraph, all in one frame. Past this many the reader has
        /// the pattern and the count is more use than the list.
        /// </remarks>
        private const int MostFindingsWorthPrinting = 25;

        /// <summary>
        /// What actually schedules a framework system, and what to look at when one is not.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE OPPOSITE READING IS THE TEMPTING ONE AND IT IS WRONG: that <c>[UpdateInGroup]</c> is
        /// only read when the game builds its own groups, "which happened before this mod loaded".
        /// Core Keeper builds its worlds when a
        /// save loads, which is after the mod assembly is in the AppDomain, and a player build's
        /// system sweep walks <c>AppDomain.CurrentDomain.GetAssemblies()</c> — so the attribute is
        /// read and the systems are scheduled by the game.
        /// </para>
        /// <para>
        /// AND THE REMEDY THAT READING LEADS TO IS WORSE THAN THE COMPLAINT. <c>AddScheduledSystem</c> resolves
        /// to <c>SimulationSystemGroup.AddSystemToUpdateList</c> and nothing else. For the systems
        /// already in that group it is a no-op, because the add de-duplicates. For the four that
        /// live elsewhere — tile capture in <c>SerializationSystemGroup</c>, skill experience in
        /// <c>PredictedSimulationSystemGroup</c>, blast fire and the biome heartbeat in
        /// <c>BeforePredictedSimulationSystemGroup</c> — nothing removes them from the list they
        /// are already in, so they would sit in two and update twice a frame.
        /// </para>
        /// </remarks>
        private const string HowSchedulingIsMeantToWork =
            "How these get scheduled: the game builds its worlds after this mod is in memory, and "
            + "world creation sweeps the loaded assemblies and puts every system it finds into the "
            + "group its [UpdateInGroup] names. That is what schedules them — not the "
            + "GetOrCreateSystemManaged calls in ExpandNullforgeModEntry, which allocate a system "
            + "and schedule nothing. A system missing from every update list was passed over by "
            + "that sweep, so check three things on the class: that it carries no "
            + "[DisableAutoCreation], that its [WorldSystemFilter] covers this world, and that the "
            + "group its [UpdateInGroup] names exists here. Do not reach for AddScheduledSystem: it "
            + "adds to SimulationSystemGroup and nothing else, and four of these belong in other "
            + "groups, so it would leave them in two update lists and run them twice a frame.";

        // THERE IS DELIBERATELY NO THIRD CONSTANT HERE. How far the game's own scheduling of a
        // mod's systems has been watched on a SERVER world is a fact about how much of this
        // framework has been tested, not about the world in front of the reader. As a log line it
        // would be identical on every run, could not be retired by anybody actually watching a
        // server world, and would reach a player's log as a note addressed to the framework's own
        // authors — on every single-player and every host session, which is the whole of normal
        // use. The note lives in the class remarks above instead, where it can be read by whoever
        // needs it and changed by whoever retires it.

        /// <summary>Remembers a world so the next quiet frame audits it.</summary>
        public static void Arm(World world, bool isClient)
        {
            if (world == null || !world.IsCreated)
            {
                return;
            }

            for (int i = 0; i < Armed.Count; i++)
            {
                if (ReferenceEquals(Armed[i].World, world))
                {
                    return;
                }
            }

            ArmedWorld armed = new ArmedWorld();
            armed.World = world;
            armed.IsClient = isClient;
            Armed.Add(armed);

            // Latched rather than asked of the list later, because the patch pass runs five seconds
            // after a world load and reports on process-wide counters: what it needs to know is
            // whether this process ever had a world of each kind, not whether one is up right now.
            if (isClient)
            {
                sawClientWorld = true;
            }
            else
            {
                sawServerWorld = true;
            }

            // A new world starts the wait again. Without this, a second world loaded in the same
            // session would be audited on its first frame, against registries the bootstrap has
            // not refilled yet, and report failures that are not there.
            quietFrames = 0;
        }

        /// <summary>Forgets a world that has gone away.</summary>
        public static void Forget(World world)
        {
            for (int i = Armed.Count - 1; i >= 0; i--)
            {
                if (Armed[i].World == null || ReferenceEquals(Armed[i].World, world))
                {
                    Armed.RemoveAt(i);
                }
            }
        }

        /// <summary>Forgets everything, for a mod shutdown.</summary>
        public static void Reset()
        {
            Armed.Clear();
            LastWorldCounts.Clear();
            lastWorldDeclarationVersion = -1;
            worldsAudited = 0;
            patchPassDone = false;
            processChecksDone = false;
            entityAuditDone = false;
            checkedNothing = false;
            objectCheckSawItemsOnly = false;
            objectCheckDidNotRunHere = false;
            lastSeenDeclarationVersion = -1;
            quietFrames = 0;
            sawClientWorld = false;
            sawServerWorld = false;
        }

        /// <summary>
        /// Drives both passes. One integer compare per frame while nothing is due.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Called once a frame from <c>ExpandNullforgeModEntry.Update</c>. With no armed world it
        /// does nothing at all; with one, it waits for declarations to go quiet, runs the
        /// registration pass, and then counts frames to the liveness pass. After both have run for
        /// every armed world it is a loop over a short list of finished entries.
        /// </para>
        /// <para>
        /// IT CATCHES ITS OWN THROWS, and that is not defensiveness for its own sake. The caller's
        /// catch sets <c>updateDisabled</c> for the rest of the session — scene-table injection,
        /// the item report, Burst arming, generation, travel, player contexts and the persistence
        /// flush all stop. A prefab query on a world-streaming frame is enough to reach it. A
        /// diagnostic that can take the product down is worse than no diagnostic, so a throw in
        /// here ends the audit and nothing else.
        /// </para>
        /// </remarks>
        public static void Update()
        {
            // ASKED OUTSIDE THE AUDIT SWITCH, because it is not an audit finding. Two mods that
            // each shipped two asset bundles lose the second bundle's data; that is true whether
            // or not somebody passed -nfnoaudit, and folding it behind the switch made a
            // correctness check disappear with a diagnostics setting.
            DimensionModBundleDiagnostics.ReportMultiBundleModsOnce();

            if (!DimensionLogConfig.Audit || Armed.Count == 0)
            {
                return;
            }

            try
            {
                RunRegistrationPassesWhenContentIsQuiet();
                RunLivenessPassesThatAreDue();
            }
            catch (Exception exception)
            {
                // Every armed world is marked finished so this cannot throw again every frame.
                for (int i = 0; i < Armed.Count; i++)
                {
                    Armed[i].RegistrationDone = true;
                    Armed[i].LivenessDone = true;
                }

                DimensionLog.Problem(
                    Ch.Audit,
                    null,
                    "the world-load check threw and has stopped for this session. Nothing else in "
                        + "the framework is affected — this is the check, not the thing it checks — "
                        + "so whatever you were testing is still running. " + exception);
            }
        }

        private static void RunRegistrationPassesWhenContentIsQuiet()
        {
            bool anythingWaiting = false;
            for (int i = 0; i < Armed.Count; i++)
            {
                if (!Armed[i].RegistrationDone && Armed[i].World != null && Armed[i].World.IsCreated)
                {
                    anythingWaiting = true;
                    break;
                }
            }

            if (!anythingWaiting)
            {
                return;
            }

            int version = DimensionItemObjectRegistry.DeclarationVersion;
            if (version != lastSeenDeclarationVersion)
            {
                lastSeenDeclarationVersion = version;
                quietFrames = 0;
                return;
            }

            if (quietFrames < FramesToWaitAfterLastDeclaration)
            {
                quietFrames++;
                return;
            }

            // Re-check the ids that had not resolved yet, so the audit reads the same ledger the
            // item report reads rather than a staler one. Idempotent: it only walks what is still
            // pending.
            DimensionItemObjectRegistry.RefreshAll();
            DimensionGeneratedObjectLedger.RefreshAll();

            for (int i = 0; i < Armed.Count; i++)
            {
                ArmedWorld armed = Armed[i];
                if (armed.RegistrationDone || armed.World == null || !armed.World.IsCreated)
                {
                    continue;
                }

                armed.RegistrationDone = true;
                armed.LivenessDueFrame = Time.frameCount + FramesBeforeLivenessPass;
                RunRegistrationPass(armed);
            }
        }

        private static void RunLivenessPassesThatAreDue()
        {
            int frame = Time.frameCount;
            for (int i = 0; i < Armed.Count; i++)
            {
                ArmedWorld armed = Armed[i];
                if (!armed.RegistrationDone || armed.LivenessDone || frame < armed.LivenessDueFrame)
                {
                    continue;
                }

                armed.LivenessDone = true;
                if (armed.World == null || !armed.World.IsCreated)
                {
                    continue;
                }

                RunLivenessPass(armed);
            }
        }

        // ------------------------------------------------------------------ registration pass ---

        private static void RunRegistrationPass(ArmedWorld armed)
        {
            World world = armed.World;
            int before = armed.Problems;

            // THE FIRST THREE ARE PROCESS FACTS, NOT WORLD FACTS. Whether Init threw, whether the
            // service is bound and how many item ids resolved are the same answers in the server
            // world and the client world, and on a host each was printed twice, tagged with a
            // world it does not belong to.
            if (!processChecksDone)
            {
                processChecksDone = true;
                CheckInitFinished(armed);
                CheckServiceIsBound(armed);
                CheckItemLedger(armed);
            }

            CheckSystemsExistAndAreScheduled(armed);
            CheckTileRescueBracket(armed);
            CheckBurstArming(armed);
            CheckRegistryGrowthAcrossWorlds(armed);
            RunEntityAudit(armed);

            worldsAudited++;
            lastWorldDeclarationVersion = DimensionItemObjectRegistry.DeclarationVersion;

            if (armed.Problems == before)
            {
                DimensionLog.Milestone(
                    Ch.Audit,
                    world,
                    "checked its own wiring in " + world.Name + " and found nothing wrong."
                        + (checkedNothing
                            ? " Read that narrowly: no content pack declared anything this "
                                + "session, so the parts of this check that need a pack to exist "
                                + "had nothing to look at."
                            : string.Empty)
                        // THE SAME CAVEAT FOR THE OTHER EMPTY LIST. A pack that declared items and
                        // nothing else is not "nothing declared", so the clause above stays quiet
                        // for it, and the object half of this pass was still aimed at the item
                        // slice of a table written mostly for creatures and world objects.
                        + (objectCheckDidNotRunHere
                            ? " The object half of this pass did not run in " + world.Name + ", so "
                                + "nothing above tested whether this pack's objects carry what the "
                                + "systems that read them require. That is not a result about them "
                                + "either way."
                            : string.Empty)
                        + (objectCheckSawItemsOnly
                            ? " Read the object half of that narrowly too: the only objects it had "
                                + "to look at were this pack's items, so no creature, boss, "
                                + "summoning circle, plant or world object was in front of it. The "
                                + "object line above says what that covered."
                            : string.Empty)
                        + WhatWasNotAskedOf(armed.IsClient));
            }
            else
            {
                DimensionLog.Problem(
                    Ch.Audit,
                    world,
                    (armed.Problems - before) + " problem" + ((armed.Problems - before) == 1 ? "" : "s")
                        + " found in " + world.Name + ". The lines above each name what is wrong, "
                        + "what state proves it, and what to do. The liveness check runs "
                        + FramesBeforeLivenessPass + " frames from now and may add more."
                        + WhatWasNotAskedOf(armed.IsClient));
            }
        }

        /// <summary>
        /// The checks that were skipped in this world, said out loud in the summary.
        /// </summary>
        /// <remarks>
        /// <para>
        /// BECAUSE "NOTHING WRONG" AFTER TWO CHECKS DID NOT RUN IS A CLAIM THIS FILE HAD NOT
        /// EARNED. Two of the registration pass's checks are asked of a server world only, for good
        /// reasons written where each one skips: the tile-rescue bracket is a server-side pair of
        /// systems, and every rule in the companion table encodes a server-simulation query. Both
        /// skips say so with a <c>Trace</c>, and traces are off unless somebody turns their channel
        /// on (<c>DimensionLogConfig.Channels</c> is empty by default), so on the session a player
        /// actually has — joining somebody else's server — the summary said the world was clean and
        /// the two lines explaining what had not been looked at were invisible.
        /// </para>
        /// <para>
        /// It is one clause on a line that was going to be printed anyway rather than a line of its
        /// own, and it is worded as "not asked of this world" rather than "did not run", because on
        /// a host they did run — in the server world, which is the only place their answer means
        /// anything.
        /// </para>
        /// <para>
        /// It takes the answer rather than the world, and is internal, so that a test can ask it
        /// both questions without a world to ask them of.
        /// </para>
        /// </remarks>
        internal static string WhatWasNotAskedOf(bool isClient)
        {
            if (!isClient)
            {
                return string.Empty;
            }

            return " Two of these are asked of a server world only and so were not asked of this "
                + "one: whether tile capture is bracketed the right way round, and whether this "
                + "mod's objects carry what the game's systems require before they will look at "
                + "them. Both read server-side state, and a client world's copy of it answers a "
                + "different question.";
        }
    }
}
