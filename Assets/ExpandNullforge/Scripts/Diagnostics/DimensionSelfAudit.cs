using System;
using System.Collections.Generic;
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
    /// </remarks>
    internal static class DimensionSelfAudit
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
        private static int lastSeenDeclarationVersion = -1;
        private static int quietFrames;

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
        /// THIS TEXT USED TO STATE THE OPPOSITE and prescribe a fix that would have damaged four
        /// systems. It said <c>[UpdateInGroup]</c> is only read when the game builds its own
        /// groups, "which happened before this mod loaded". Core Keeper builds its worlds when a
        /// save loads, which is after the mod assembly is in the AppDomain, and a player build's
        /// system sweep walks <c>AppDomain.CurrentDomain.GetAssemblies()</c> — so the attribute is
        /// read and the systems are scheduled by the game.
        /// </para>
        /// <para>
        /// AND THE REMEDY IT NAMED WAS WORSE THAN THE COMPLAINT. <c>AddScheduledSystem</c> resolves
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

        /// <summary>
        /// The one thing the framework's evidence for auto-scheduling does not cover.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The session log that settled this is a client joining a dedicated server: it has a
        /// <c>ClientWorld0</c> and no <c>ServerWorld</c> at all. The server world is built by the
        /// same <c>ClientServerBootstrap</c> path from the same sweep, so there is no reason to
        /// expect it to differ — but nobody has watched it happen, and the audit is not entitled to
        /// present a reasonable expectation as an observation.
        /// </para>
        /// <para>
        /// IT USED TO OPEN WITH "This is the first world of its kind this check has ever reported
        /// on", WHICH IS A CLAIM ABOUT THE PAST THAT A CONSTANT CANNOT MAKE. Nothing here remembers
        /// previous sessions, so from the second run onward that sentence was false and no run could
        /// retire it. What is true every time is what the evidence covers, which is what it says
        /// now.
        /// </para>
        /// <para>
        /// AND IT IS SAID WHETHER OR NOT THE WORLD LOOKS HEALTHY. It hung off the success milestone
        /// alone, so the case where "nobody has read this kind of world back" matters most — a
        /// server world that reports a problem — was the one case that did not get it.
        /// </para>
        /// </remarks>
        private const string ServerWorldHasNeverBeenWatched =
            "this is a server world, and the evidence that the game schedules a mod's systems by "
            + "itself comes from one session with no server world in it: a client joining a "
            + "dedicated server, which covers ClientWorld0 only. The server world is built by the "
            + "same code from the same sweep, so the expectation is that it behaves the same way. "
            + "The lines above are a reading of it rather than a confirmation of something already "
            + "watched.";

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
            lastSeenDeclarationVersion = -1;
            quietFrames = 0;
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

            // SAID FOR A SERVER WORLD WHETHER IT LOOKED HEALTHY OR NOT. It used to ride the
            // scheduling milestone, which only prints when nothing was wrong — so the caveat that
            // this kind of world has never been read back went missing in exactly the case where a
            // reader would want it.
            if (!armed.IsClient)
            {
                DimensionLog.Milestone(Ch.Audit, world, ServerWorldHasNeverBeenWatched);
            }

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
                            : string.Empty));
            }
            else
            {
                DimensionLog.Problem(
                    Ch.Audit,
                    world,
                    (armed.Problems - before) + " problem" + ((armed.Problems - before) == 1 ? "" : "s")
                        + " found in " + world.Name + ". The lines above each name what is wrong, "
                        + "what state proves it, and what to do. The liveness check runs "
                        + FramesBeforeLivenessPass + " frames from now and may add more.");
            }
        }

        /// <summary>
        /// Says so when <c>Init</c> threw after this world was already registered.
        /// </summary>
        /// <remarks>
        /// <c>ExpandNullforgeModEntry.InitFailed</c> was written and documented as "read by the
        /// boot banner and by the self-audit" and read by neither. It is worth reading here for one
        /// case: <c>Init</c> registers the server world part-way through its work, so a throw after
        /// that point leaves a world attached with the rest of the wiring missing, and PugMod marks
        /// a mod initialised BEFORE calling <c>Init</c>, so it is never retried. Everything the
        /// audit reports below is then a symptom of one cause, and the reader needs to know that
        /// before spending time on any of it.
        /// </remarks>
        private static void CheckInitFinished(ArmedWorld armed)
        {
            if (!ExpandNullforgeModEntry.InitFailed)
            {
                return;
            }

            Problem(
                armed,
                Ch.Boot,
                "ExpandNullforge threw out of Init and this world attached anyway, so part of the "
                    + "wiring is missing and nothing will retry it — the mod loader marks a mod "
                    + "initialised before calling Init. Whatever else this audit reports is "
                    + "probably that one throw. The stack trace is above, on the line that says "
                    + "ExpandNullforge threw out of Init. Restart the game after fixing it.");
        }

        private static void CheckServiceIsBound(ArmedWorld armed)
        {
            IDimensionService service;
            if (DimensionApi.TryGetService(out service) && service != null)
            {
                return;
            }

            Problem(
                armed,
                Ch.Service,
                "DimensionApi has no dimension service registered, and a world has already been "
                    + "attached. Nothing a content pack registers can reach the framework: no "
                    + "dimension, biome, portal, scene or item will exist. EarlyInit either did not "
                    + "run or threw — look above for a line saying ExpandNullforge threw out of "
                    + "EarlyInit.");
        }

        private static void CheckSystemsExistAndAreScheduled(ArmedWorld armed)
        {
            World world = armed.World;
            HashSet<ComponentSystemBase> scheduled = new HashSet<ComponentSystemBase>();
            foreach (ComponentSystemBase system in world.Systems)
            {
                ComponentSystemGroup group = system as ComponentSystemGroup;
                if (group == null)
                {
                    continue;
                }

                IReadOnlyList<ComponentSystemBase> members = group.ManagedSystems;
                if (members == null)
                {
                    continue;
                }

                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] != null)
                    {
                        scheduled.Add(members[i]);
                    }
                }
            }

            List<DimensionSystemRoster.Row> mine = new List<DimensionSystemRoster.Row>();
            List<DimensionSystemRoster.Row> missing = new List<DimensionSystemRoster.Row>();
            List<DimensionSystemRoster.Row> unscheduled = new List<DimensionSystemRoster.Row>();

            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                if (!rows[i].AppliesTo(armed.IsClient))
                {
                    continue;
                }

                mine.Add(rows[i]);
                ComponentSystemBase system = null;
                try
                {
                    system = rows[i].Find(world);
                }
                catch (Exception)
                {
                    // A system type the loader never registered answers with a throw rather than a
                    // null. That is the same finding as "not created", so it is reported that way.
                    system = null;
                }

                if (system == null)
                {
                    missing.Add(rows[i]);
                    continue;
                }

                if (!scheduled.Contains(system))
                {
                    unscheduled.Add(rows[i]);
                }
            }

            // EVERY ONE UNSCHEDULED IS ONE LINE, NOT THIRTY. It is a single cause with a single
            // fix, and thirty lines of the same sentence is how a scheme like this gets ignored.
            if (unscheduled.Count > 0 && unscheduled.Count == mine.Count)
            {
                Problem(
                    armed,
                    Ch.Audit,
                    "none of the " + mine.Count + " framework systems in " + world.Name
                        + " are in any system group's update list, so none of them will ever run. "
                        + "Every runtime feature is dead in this world: portals, travel, biomes, "
                        + "crops, cooking, hazards, traps, bosses, arenas, explosives and object "
                        + "links. " + HowSchedulingIsMeantToWork + " All of them failing at once "
                        + "means the sweep did not see this assembly at all, so the first thing to "
                        + "check is whether the mod finished compiling before the world was built "
                        + "— the loader prints its compile line above.");
            }
            else
            {
                for (int i = 0; i < unscheduled.Count; i++)
                {
                    DimensionSystemRoster.Row row = unscheduled[i];
                    Problem(
                        armed,
                        Ch.Audit,
                        row.Name + " exists in " + world.Name + " but is in no system group's "
                            + "update list, so it will never run. What stops working: "
                            + row.Capability + ". " + UpperFirst(DescribeWork(row)) + " "
                            + HowSchedulingIsMeantToWork);
                }
            }

            // A SYSTEM THAT IS NOT THERE IS A FAULT WHETHER OR NOT ANYTHING IS WAITING ON IT, and
            // that is the difference between this branch and the liveness pass. A system with an
            // empty registry has nothing to wake it up, which is not a fault — but a system that
            // does not exist cannot be woken up later either, and half the rows have no way to
            // count what is waiting on them, so gating this on a count meant the one instance the
            // project has actually hit (the skill-experience system, never created, no kill ever
            // paying out) was reported only when its registry happened to be full.
            // ALL OF THEM MISSING IS ONE LINE TOO, and this is now the likelier of the two collapses
            // rather than the other way round. The systems are created by the game's own sweep over
            // the loaded assemblies, so the way they fail together is the sweep not seeing this
            // assembly at all — and that would have printed twenty-eight copies of the sentence
            // below, which is how a scheme like this gets ignored.
            if (missing.Count > 0 && missing.Count == mine.Count)
            {
                Problem(
                    armed,
                    Ch.Audit,
                    "none of the " + mine.Count + " framework systems exists in " + world.Name
                        + ", so every runtime feature is dead in this world: portals, travel, "
                        + "biomes, crops, cooking, hazards, traps, bosses, arenas, explosives and "
                        + "object links. The game creates a mod's systems as it builds the world, "
                        + "from the assemblies loaded at that point, and all of them missing at "
                        + "once means this assembly was not among them. Check the loader's compile "
                        + "line above: if the mod finished compiling after the world was built, "
                        + "nothing in it was there to find.");
                return;
            }

            for (int i = 0; i < missing.Count; i++)
            {
                DimensionSystemRoster.Row row = missing[i];
                Problem(
                    armed,
                    Ch.Audit,
                    row.Name + " does not exist in " + world.Name + ". What stops working: "
                        + row.Capability + ". " + UpperFirst(DescribeWork(row))
                        + " The game creates a mod's systems as it builds the world, from the "
                        + "assemblies loaded at that point, so a class that is missing here either "
                        + "carries [DisableAutoCreation], or a [WorldSystemFilter] that leaves this "
                        + "world out, or was not in memory when the world was built.");
            }

            if (missing.Count == 0 && unscheduled.Count == 0)
            {
                DimensionLog.Milestone(
                    Ch.Audit,
                    world,
                    mine.Count + " framework systems created and scheduled in " + world.Name + ".");
            }
        }

        /// <summary>
        /// Checks that tile capture runs before the deserializer, in the world that captures.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ASKED OF THE SERVER ONLY, AND SKIPPING THAT TEST MADE THIS CHECK CRY WOLF ON EVERY
        /// CLIENT WORLD. <c>DimensionCustomTileCaptureSystem</c> is
        /// <c>[WorldSystemFilter(ServerSimulation)]</c> — everything that touches a serialized
        /// submap is server-side — so it is not created in a client world at all.
        /// <c>SerializationSystemGroup</c> carries no filter and IS created there, so the lookup
        /// below found the group, failed to find the capture system, and printed a Problem saying
        /// custom blocks would be gone on the next load and naming
        /// <c>EnsureSystemOrdering</c> as the fix. All three parts were wrong on a client: nothing
        /// was lost, the client-side call was deliberately removed, and the reader was sent after a
        /// system that is not meant to be there.
        /// </para>
        /// <para>
        /// A trace rather than silence, because "this check did not run here" and "this check
        /// passed here" are different answers and the log has to be able to tell them apart.
        /// </para>
        /// </remarks>
        private static void CheckTileRescueBracket(ArmedWorld armed)
        {
            World world = armed.World;
            if (armed.IsClient)
            {
                DimensionLog.Trace(
                    Ch.Tileset,
                    world,
                    "the tile-rescue bracket is a server-side pair — the systems that read and "
                        + "rewrite a serialized submap are declared for the server simulation only "
                        + "— so there is nothing to check in " + world.Name + ".");
                return;
            }

            SerializationSystemGroup group =
                world.GetExistingSystemManaged<SerializationSystemGroup>();
            if (group == null)
            {
                // Not every world serializes. Nothing to say.
                return;
            }

            IReadOnlyList<ComponentSystemBase> members = group.ManagedSystems;
            if (members == null)
            {
                return;
            }

            int capture = -1;
            int deserializer = -1;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] is Tilesets.DimensionCustomTileCaptureSystem)
                {
                    capture = i;
                }
                else if (members[i] is DeserializeComponentsSystem)
                {
                    deserializer = i;
                }
            }

            if (capture < 0)
            {
                Problem(
                    armed,
                    Ch.Tileset,
                    "the tile-capture system is not in SerializationSystemGroup in " + world.Name
                        + ", so nothing rescues custom tile layers before the game's deserializer "
                        + "throws them away. Custom blocks will be gone the next time this world "
                        + "loads. DimensionCustomTileRescue.EnsureSystemOrdering is what puts it "
                        + "there and it either did not run or could not find the group.");
                return;
            }

            if (deserializer < 0)
            {
                // The game's own deserializer not being in the group is not our finding to make.
                return;
            }

            if (capture > deserializer)
            {
                Problem(
                    armed,
                    Ch.Tileset,
                    "the tile-capture system sits at position " + capture
                        + " in SerializationSystemGroup in " + world.Name
                        + " and the game's deserializer at position " + deserializer
                        + ", so capture runs AFTER it. Custom tile layers are discarded before "
                        + "anything can rescue them, and custom blocks will vanish on reload in "
                        + "this world. Those are positions among the group's managed systems in "
                        + "run order, not update slots. DimensionCustomTileRescue.EnsureSystemOrdering "
                        + "re-sorts the group and is what should have put these the right way "
                        + "round; if it ran and this line still appears, the engine rejected the "
                        + "capture system's [UpdateBefore] and said so in a line above starting "
                        + "\"Ignoring invalid\".");
                return;
            }

            DimensionLog.Milestone(
                Ch.Tileset,
                world,
                "tile capture is at position " + capture + " in SerializationSystemGroup and the "
                    + "deserializer at " + deserializer + ": the rescue bracket is the right way "
                    + "round in " + world.Name + ".");
        }

        private static void CheckBurstArming(ArmedWorld armed)
        {
            int tilesets = CountOf(
                Tilesets.DimensionTilesetRegistry.All);
            int itemPortals = CountOf(
                Portals.DimensionItemPortalRegistry.RegisteredItemNames
                   );
            if (tilesets == 0 && itemPortals == 0)
            {
                return;
            }

            // ASKED OF THIS WORLD, not of the process. The old question was a single static set the
            // first time any world armed, so on a host the client arming answered for the server —
            // and the server is the peer whose missed arming eats every placement.
            if (ExpandNullforgeModEntry.WasBurstArmedFor(armed.World))
            {
                return;
            }

            Problem(
                armed,
                Ch.Tileset,
                armed.World.Name + " was not registered with the Burst disabler, and this session "
                    + "has " + tilesets + " custom tileset" + (tilesets == 1 ? "" : "s") + " and "
                    + itemPortals + " portal item" + (itemPortals == 1 ? "" : "s")
                    + " on the equipment path. EquipmentUpdateSystem still runs Burst-compiled "
                    + "there, and a Harmony patch is never reached inside Burst-compiled code, so "
                    + "placing a custom block reaches the game's untouched EntityUtility.AddTile "
                    + "and is rejected (\"Trying to add invalid tileset\"), and using a portal item "
                    + "does nothing. Placement will look right on the client for one frame and then "
                    + "be corrected away.");
        }

        private static void CheckItemLedger(ArmedWorld armed)
        {
            int declared = DimensionItemObjectRegistry.DeclaredCount;
            int resolved = DimensionItemObjectRegistry.ResolvedCount;
            int pending = DimensionItemObjectRegistry.PendingCount;

            if (declared == 0 && DimensionGeneratedObjectLedger.DeclaredCount == 0)
            {
                // The clean verdict at the end of the pass says so out loud when this is the case,
                // because "found nothing wrong" after "there was nothing to look at" is the one
                // sentence in this file a reader could act on wrongly.
                checkedNothing = true;

                // NOT SILENCE. Nothing declared means either no content pack is installed or the
                // generated bootstrap never reached the registry, and those look identical from
                // here, so both are named.
                DimensionLog.Milestone(
                    Ch.Item,
                    armed.World,
                    "no content pack declared any items this session. Either none is installed, or "
                        + "the generated bootstrap never reached the framework — if a pack IS "
                        + "installed, look above for its own registration line, because there "
                        + "should be one.");
                return;
            }

            if (pending > 0)
            {
                // The registry names each missing id itself, once, from ReportMissing. This is the
                // count, so the reader knows how many of those lines to expect.
                Problem(
                    armed,
                    Ch.Item,
                    declared + " items were declared and " + (declared - pending)
                        + " of them exist in this world; " + pending + " never registered. "
                        + "DimensionItemObjectRegistry names each missing one, once per session, "
                        + "in the lines around this one.");
                return;
            }

            DimensionLog.Milestone(
                Ch.Item,
                armed.World,
                declared + " declared items all exist in this world (" + resolved
                    + " names resolved)."
                    + (DimensionGeneratedObjectLedger.DeclaredCount == 0
                        ? string.Empty
                        : " Beside them, " + DimensionGeneratedObjectLedger.ResolvedCount + " of "
                            + DimensionGeneratedObjectLedger.DeclaredCount + " other generated "
                            + "objects — creatures, bosses, plants, containers, world objects — "
                            + "answer to their names. The ones that do not are not counted as "
                            + "faults: an id lands there when its asset was switched off after "
                            + "this pack was built, or when the generator that makes that kind of "
                            + "thing has not been run since."));
        }

        private static void CheckRegistryGrowthAcrossWorlds(ArmedWorld armed)
        {
            Dictionary<string, int> now = new Dictionary<string, int>(StringComparer.Ordinal);
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].CountWork == null || now.ContainsKey(rows[i].WorkName))
                {
                    continue;
                }

                now[rows[i].WorkName] = WorkCount(rows[i]);
            }

            bool anythingNewWasDeclared =
                DimensionItemObjectRegistry.DeclarationVersion != lastWorldDeclarationVersion;

            if (worldsAudited > 0 && !anythingNewWasDeclared)
            {
                foreach (KeyValuePair<string, int> pair in now)
                {
                    int previous;
                    if (!LastWorldCounts.TryGetValue(pair.Key, out previous) ||
                        pair.Value <= previous)
                    {
                        continue;
                    }

                    Problem(
                        armed,
                        Ch.Registry,
                        pair.Key + " held " + previous + " rows for the previous world and holds "
                            + pair.Value + " now, and no content pack declared anything new in "
                            + "between. The same content registered itself a second time when this "
                            + "world loaded, so every row in it is now duplicated: whatever it "
                            + "drives will happen twice. Fix: clear it in "
                            + "ExpandNullforgeModEntry.Shutdown, or make its Register call skip a "
                            + "row it already holds.");
                }
            }

            LastWorldCounts.Clear();
            foreach (KeyValuePair<string, int> pair in now)
            {
                LastWorldCounts[pair.Key] = pair.Value;
            }
        }

        /// <summary>
        /// Checks the framework's own objects against the game's queries, once, on the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ONCE PER PROCESS, NOT ONCE PER WORLD. It builds a lookup by querying every converted
        /// prefab and taking a sync point, and on a host both worlds are armed, so it was paying
        /// that twice on every load for the same answer.
        /// </para>
        /// <para>
        /// AND ON THE SERVER, BECAUSE THE RULES ARE THE SERVER'S. Every rule in the companion table
        /// encodes a server-simulation query. A client world's copy of a prefab is converted for
        /// the client and does not carry everything the server's copy does, so asking these
        /// questions there would name real objects, name components that are absent for a good
        /// reason, and hand the reader a fix that would be wrong. A player who joined somebody
        /// else's server has no server world in their process; they are told that rather than given
        /// a check that cannot be trusted.
        /// </para>
        /// </remarks>
        private static void RunEntityAudit(ArmedWorld armed)
        {
            if (!DimensionLogConfig.EntityAudit || entityAuditDone)
            {
                return;
            }

            if (armed.IsClient)
            {
                DimensionLog.Trace(
                    Ch.Audit,
                    armed.World,
                    "the object check reads server-side prefabs and this process has no server "
                        + "world yet, so it has not run. On a host or a single-player world it "
                        + "runs as that world loads.");
                return;
            }

            entityAuditDone = true;
            DimensionEntityAudit.Result result =
                DimensionEntityAudit.Run(armed.World, DimensionLogConfig.EntityAuditBudget);

            if (result.ResolvedItems > 0 && result.Checked == 0 && result.StoppedEarly)
            {
                DimensionLog.Milestone(
                    Ch.Audit,
                    armed.World,
                    "the object check was given a budget of " + DimensionLogConfig.EntityAuditBudget
                        + " objects, so it looked at none of the " + result.ResolvedItems
                        + " this mod has. Raise diagnostics entityAuditBudget, or switch the check "
                        + "off with entityAudit if that is what was meant.");
                return;
            }

            if (result.ResolvedItems == 0)
            {
                // FOUND NOTHING IS NOT THE SAME AS FOUND NOTHING WRONG, and saying so is the whole
                // reason this branch exists.
                DimensionLog.Milestone(
                    Ch.Audit,
                    armed.World,
                    "no object this framework generated has resolved in " + armed.World.Name
                        + ", so nothing was checked against what the game's systems ask for. This "
                        + "is not a clean result; it means the subject list was empty.");
                return;
            }

            // BOTH LISTS ARE CAPPED. One dropped component on a generator's shared pass produces a
            // finding per object per rule, and a pack with four hundred objects would put fifteen
            // thousand paragraphs through Debug.LogWarning in a single frame — each with a Unity
            // stack trace under it. Past the cap the reader has the pattern and needs the count.
            int printed = 0;
            for (int i = 0; i < result.WithoutAPrefab.Count && printed < MostFindingsWorthPrinting;
                 i++, printed++)
            {
                Problem(
                    armed,
                    Ch.Object,
                    result.WithoutAPrefab[i] + " has a name the game answers to, and "
                        + armed.World.Name + " holds no prefab carrying that number. Anything that "
                        + "spawns it will get nothing. The name resolved against the object list "
                        + "and the prefab did not reach this world's conversion.");
            }

            for (int i = 0; i < result.Findings.Count && printed < MostFindingsWorthPrinting;
                 i++, printed++)
            {
                Problem(armed, Ch.Object, DimensionEntityAudit.Describe(result.Findings[i]));
            }

            int total = result.WithoutAPrefab.Count + result.Findings.Count;
            if (total > printed)
            {
                Problem(
                    armed,
                    Ch.Object,
                    "and " + (total - printed) + " more of the same kind, not printed. "
                        + result.Findings.Count + " objects are missing something a system that "
                        + "reads them requires and " + result.WithoutAPrefab.Count
                        + " resolved to a number this world has no prefab for. A count this size "
                        + "is one pass of the generator, not one object: fix the first few above "
                        + "and generate again.");
            }

            if (result.Findings.Count == 0 && result.WithoutAPrefab.Count == 0)
            {
                DimensionLog.Milestone(
                    Ch.Audit,
                    armed.World,
                    result.Checked + " of this mod's objects checked in " + armed.World.Name
                        + " against " + DimensionQueryCompanionTable.All.Length
                        + " of the game's queries; all of them carry what the systems that read "
                        + "them require."
                        + (result.StoppedEarly
                            ? " The budget stopped the walk before the end of the list; raise "
                                + "diagnostics entityAuditBudget to check the rest."
                            : string.Empty));
            }
        }

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
        /// Once per process, not once per world, and with no world on the line: Harmony patching is
        /// process-wide, a count cannot be attributed to a world, and several of these targets run
        /// in neither. Attaching one would be a claim the number does not support.
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
                    + "on it is not a fault: most of them only run when a player does something.");
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
