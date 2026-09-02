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
    /// The individual checks the audit runs once a world has settled.
    /// </summary>
    internal static partial class DimensionSelfAudit
    {
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
                // NOTHING TO CHECK IS NOT THE SAME AS EVERYTHING CHECKING OUT. With no row on the
                // roster claiming this world, missing and unscheduled are both empty for the same
                // reason the subject list is — and the sentence read as a pass either way.
                DimensionLog.Milestone(
                    Ch.Audit,
                    world,
                    mine.Count == 0
                        ? "no framework system on the roster claims " + world.Name
                            + ", so nothing was looked for there and this is not a pass. The "
                            + "roster holds " + rows.Length + " row"
                            + (rows.Length == 1 ? "" : "s")
                            + ", and every one of them is marked for the other side of the game."
                        : mine.Count + " framework systems created and scheduled in "
                            + world.Name + ".");
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
        /// THE TRACE BELOW IS NOT WHAT TELLS THE READER THIS DID NOT RUN, and it was written as
        /// though it were. Traces are off unless somebody turns their channel on, so in the default
        /// configuration it is silence. What the reader actually sees is the clause
        /// <see cref="WhatWasNotAskedOf"/> puts on the world's own summary line, which is on by
        /// default. The trace stays because with the tileset channel on it lands next to the rest
        /// of the tile story, which is where somebody debugging tiles is looking.
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
                    // THE EMPTY CASE IS SAID, NOT LEFT OUT. A line that names only what it found
                    // reads as though that was all there
                    // was to find: a pack built before the framework kept the wider list declares
                    // its items and nothing else, and would look from here exactly like a pack that
                    // has no creatures.
                    + (DimensionGeneratedObjectLedger.DeclaredCount == 0
                        ? " No generated object was declared beside them — no creature, boss, "
                            + "summoning circle, plant, container or world object — so those are "
                            + "not being checked this session. A pack built before the framework "
                            + "kept that list has none in its manifest; generating it again is "
                            + "what puts them in."
                        : " Beside them, " + DimensionGeneratedObjectLedger.ResolvedCount + " of "
                            + DimensionGeneratedObjectLedger.DeclaredCount + " other generated "
                            + "objects — creatures, bosses, plants, containers, world objects — "
                            + "answer to their names. The ones that do not are not counted as "
                            + "faults: an id lands there when its asset was switched off after "
                            + "this pack was built, or when the generator that makes that kind of "
                            + "thing has not been run since."));
        }

        /// <summary>
        /// Says so when a registry holds more rows for this world than for the last one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE INFERENCE IS ONLY SOUND FOR A REGISTRY THAT NOTHING BUT A CONTENT PACK WRITES. The
        /// finding below says a pack registered itself twice, and it says it because the count went
        /// up with no new declaration to explain it. A registry the game fills while somebody plays
        /// breaks that reasoning outright: walk into a dimension, quit to the menu, load another
        /// world, and the armed-trap count is higher for the second world for a reason that has
        /// nothing to do with registration. Those rows say so on the roster
        /// (<see cref="DimensionSystemRoster.Row.CountGrowsDuringPlay"/>) and are left out here.
        /// </para>
        /// <para>
        /// IT IS ALSO SKIPPED WHOLESALE WHEN ANYTHING NEW WAS DECLARED, which is deliberate and
        /// narrows what it can catch: a pack that re-runs its whole declaration on a second world
        /// bumps the item version, and this says nothing. That case is loud elsewhere — every id it
        /// re-declares goes through the item registry. What is left for this check is the quiet
        /// one: a registry that grew while nothing declared anything.
        /// </para>
        /// </remarks>
        private static void CheckRegistryGrowthAcrossWorlds(ArmedWorld armed)
        {
            Dictionary<string, int> now = new Dictionary<string, int>(StringComparer.Ordinal);
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].CountWork == null || rows[i].CountGrowsDuringPlay ||
                    now.ContainsKey(rows[i].WorkName))
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
    }
}
