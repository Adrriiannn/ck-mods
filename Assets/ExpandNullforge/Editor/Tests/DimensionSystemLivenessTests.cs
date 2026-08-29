#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;

using ExpandNullforge.Diagnostics;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Holds every framework system to the three things that decide whether the game will schedule
    /// it, and holds the system roster to what those three things say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS FILE USED TO ASSERT, AND WHY IT WAS THE WRONG QUESTION. It read the mod entry
    /// point's source text and checked that each system's name appeared in a
    /// <c>GetOrCreateSystemManaged&lt;T&gt;</c> call, on the stated premise that "explicit creation
    /// is the liveness guarantee". It is not. That call allocates a system and adds it to the
    /// world's own lookup; it puts it in no group's update list, and a system in no update list
    /// cannot tick. Explicit creation is not sufficient.
    /// </para>
    /// <para>
    /// Nor is it necessary. Core Keeper builds its worlds when a save loads, which is after the mod
    /// assembly is in the AppDomain; world creation asks the type manager for every system in the
    /// loaded assemblies — a player build enumerates <c>AppDomain.CurrentDomain.GetAssemblies()</c>
    /// — and adds each one to the group its <c>[UpdateInGroup]</c> names. That is what schedules
    /// these, and it happens whether or not the mod entry point mentions them.
    /// </para>
    /// <para>
    /// SO THE HONEST QUESTION IS WHETHER A SYSTEM IS BUILT TO BE FOUND BY THAT SWEEP, which is
    /// three facts about the class and one agreement with the roster the runtime audit walks:
    /// nothing opts it out of creation, it says which group it belongs in, it says which worlds it
    /// belongs in, and the roster's peer column matches what it said. None of that is a text
    /// search: the attributes are read off the built assembly, so a class the compiler produced
    /// differently from how it reads is caught here rather than in game.
    /// </para>
    /// <para>
    /// WHAT IS STILL NOT COVERED, said rather than implied: whether the sweep actually ran over
    /// this assembly on a given launch, and whether a scheduled system's query is ever satisfied.
    /// Neither can be answered outside a running game. Both are answered in game by
    /// <c>DimensionSelfAudit</c>, which walks every group's <c>ManagedSystems</c> and then reads
    /// <c>LastSystemVersion</c> five seconds later.
    /// </para>
    /// </remarks>
    public sealed class DimensionSystemLivenessTests
    {
        /// <summary>
        /// The fewest systems this framework can plausibly have.
        /// </summary>
        /// <remarks>
        /// Every test here walks a list, so every one of them would pass on an empty list having
        /// checked nothing. Set below the real count so ordinary work never trips it, and far above
        /// zero so an empty scan cannot go green.
        /// </remarks>
        private const int FewestPlausibleSystems = 20;

        [Test]
        public void NoFrameworkSystemOptsOutOfBeingCreatedWithTheWorld()
        {
            List<string> optedOut = new List<string>();
            int checkedSystems = 0;

            foreach (Type system in FrameworkSystems())
            {
                checkedSystems++;
                if (system.GetCustomAttribute<DisableAutoCreationAttribute>() != null)
                {
                    optedOut.Add(system.FullName);
                }
            }

            Assert.GreaterOrEqual(
                checkedSystems,
                FewestPlausibleSystems,
                "Only " + checkedSystems + " framework systems were found, so this proved nothing "
                + "about the ones it did not see. The scan is broken.");
            Assert.IsEmpty(
                optedOut,
                "[DisableAutoCreation] is the one thing that stops the game creating a system as it "
                + "builds a world, and these carry it. Whatever they do never happens, with no "
                + "error and no log line: " + string.Join(", ", optedOut.ToArray()));
        }

        [Test]
        public void EveryFrameworkSystemSaysWhichGroupAndWhichWorldsItBelongsIn()
        {
            List<string> noGroup = new List<string>();
            List<string> noWorlds = new List<string>();
            int checkedSystems = 0;

            foreach (Type system in FrameworkSystems())
            {
                checkedSystems++;
                if (system.GetCustomAttribute<UpdateInGroupAttribute>() == null)
                {
                    noGroup.Add(system.FullName);
                }

                if (system.GetCustomAttribute<WorldSystemFilterAttribute>() == null)
                {
                    noWorlds.Add(system.FullName);
                }
            }

            Assert.GreaterOrEqual(
                checkedSystems,
                FewestPlausibleSystems,
                "Only " + checkedSystems + " framework systems were found. The scan is broken.");
            Assert.IsEmpty(
                noGroup,
                "A system with no [UpdateInGroup] is put in SimulationSystemGroup by default, which "
                + "silently discards any ordering it needs against a system in another group. Say "
                + "which group it belongs in: " + string.Join(", ", noGroup.ToArray()));
            Assert.IsEmpty(
                noWorlds,
                "A system with no [WorldSystemFilter] is created in whatever worlds its group's "
                + "default covers, which is how the tile capture and restore systems ended up "
                + "running in a client world where nothing they read exists. Say which worlds it "
                + "belongs in: " + string.Join(", ", noWorlds.ToArray()));
        }

        [Test]
        public void TheRostersPeerColumnMatchesTheWorldFilterOnTheClass()
        {
            DimensionSystemRoster.Row[] rows = DimensionSystemRoster.All;
            Assert.GreaterOrEqual(
                rows.Length,
                FewestPlausibleSystems,
                "The system roster holds " + rows.Length + " rows, so this compared almost "
                + "nothing.");

            Dictionary<string, Type> byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type system in FrameworkSystems())
            {
                byName[system.Name] = system;
            }

            // BOTH DIRECTIONS, AND THE SECOND ONE IS WHY THIS WALKS THE ASSEMBLY. The other
            // cross-check in the suite finds systems with the regex `class (Dimension\w+) :
            // SystemBase`, so a system named anything else, or deriving through an intermediate
            // base, is invisible to it — and a system with no roster row is invisible to the
            // runtime audit too, because the roster is the only list it has.
            HashSet<string> named = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Length; i++)
            {
                named.Add(rows[i].Name);
            }

            List<string> unnamed = new List<string>();
            foreach (KeyValuePair<string, Type> system in byName)
            {
                if (!named.Contains(system.Key))
                {
                    unnamed.Add(system.Value.FullName);
                }
            }

            Assert.IsEmpty(
                unnamed,
                "These systems have no roster row, so the world-load check never looks for them: "
                + "it cannot say they are missing, cannot say they are unscheduled, and cannot say "
                + "they have never run:\n" + string.Join("\n", unnamed.ToArray()));

            List<string> disagreements = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                Type system;
                if (!byName.TryGetValue(rows[i].Name, out system))
                {
                    disagreements.Add(
                        rows[i].Name + ": the roster names it and the assembly has no such system");
                    continue;
                }

                WorldSystemFilterAttribute filter =
                    system.GetCustomAttribute<WorldSystemFilterAttribute>();
                if (filter == null)
                {
                    // Named by the test above; not repeated as a second failure here.
                    continue;
                }

                bool server =
                    (filter.FilterFlags & WorldSystemFilterFlags.ServerSimulation) != 0;
                bool client =
                    (filter.FilterFlags & WorldSystemFilterFlags.ClientSimulation) != 0;
                DimensionSystemRoster.Peer declared =
                    server && client
                        ? DimensionSystemRoster.Peer.Both
                        : (client ? DimensionSystemRoster.Peer.Client
                                  : DimensionSystemRoster.Peer.Server);

                if (declared != rows[i].Where)
                {
                    disagreements.Add(
                        rows[i].Name + ": the roster says " + rows[i].Where + " and the class says "
                        + declared);
                }
            }

            // A DISAGREEMENT HERE MAKES THE RUNTIME AUDIT BLIND RATHER THAN WRONG, which is worse.
            // The audit only looks at a row in a world the row claims to apply to, so a system
            // scheduled somewhere its roster row does not mention is skipped in that world — the
            // failure is not reported, it becomes unreportable.
            Assert.IsEmpty(
                disagreements,
                "The roster's peer column is what decides which world the world-load check looks "
                + "for each system in, so a row that disagrees with the class makes that system's "
                + "failures invisible rather than reported:\n"
                + string.Join("\n", disagreements.ToArray()));
        }

        [Test]
        public void EveryOrderingConstraintNamesSomethingInTheSameGroupAndTheSameWorlds()
        {
            List<string> broken = new List<string>();
            int constraints = 0;

            foreach (Type system in FrameworkSystems())
            {
                Type group = GroupOf(system);
                WorldSystemFilterFlags worlds = WorldsOf(system);

                foreach (Type target in OrderingTargets(system))
                {
                    constraints++;
                    Type targetGroup = GroupOf(target);
                    if (group != null && targetGroup != null && group != targetGroup)
                    {
                        broken.Add(
                            system.Name + " is ordered against " + target.Name + ", which is in "
                            + targetGroup.Name + " while it is in " + group.Name);
                        continue;
                    }

                    WorldSystemFilterFlags targetWorlds = WorldsOf(target);
                    WorldSystemFilterFlags missing = worlds & ~targetWorlds;
                    if (missing != 0)
                    {
                        broken.Add(
                            system.Name + " is ordered against " + target.Name + ", which is not "
                            + "created in " + missing + " while it is");
                    }
                }
            }

            Assert.Greater(
                constraints,
                0,
                "No [UpdateBefore] or [UpdateAfter] was found on any framework system, so this "
                + "checked nothing. Either the scan is broken or every ordering was deleted.");

            // THE ENGINE DROPS A CONSTRAINT LIKE THIS AND CARRIES ON. It prints one line —
            // "Ignoring invalid [UpdateBeforeAttribute] ... can only order systems that are members
            // of the same ComponentSystemGroup instance" — and then runs the systems in whatever
            // order the sort happened to produce. That line was in the log for a year, three times
            // a session, and read as noise.
            Assert.IsEmpty(
                broken,
                "These orderings are silently discarded at world creation, and the systems then run "
                + "in whatever order the sort produces:\n" + string.Join("\n", broken.ToArray()));
        }

        private static Type GroupOf(Type system)
        {
            UpdateInGroupAttribute attribute = system.GetCustomAttribute<UpdateInGroupAttribute>();
            return attribute == null ? null : attribute.GroupType;
        }

        /// <summary>
        /// The simulation worlds a class declares, or all of them when it declares none.
        /// </summary>
        /// <remarks>
        /// A class with no <c>[WorldSystemFilter]</c> takes its group's default, which for
        /// everything the framework orders against is local, server and client. Reading absence as
        /// "everywhere" is the direction that cannot produce a false failure: it can only let a
        /// real one through, and the test above requires the framework's own classes to say.
        /// </remarks>
        private static WorldSystemFilterFlags WorldsOf(Type system)
        {
            WorldSystemFilterAttribute attribute =
                system.GetCustomAttribute<WorldSystemFilterAttribute>();
            WorldSystemFilterFlags everywhere =
                WorldSystemFilterFlags.LocalSimulation |
                WorldSystemFilterFlags.ServerSimulation |
                WorldSystemFilterFlags.ClientSimulation;
            if (attribute == null)
            {
                return everywhere;
            }

            WorldSystemFilterFlags declared = attribute.FilterFlags & everywhere;
            return declared == 0 ? everywhere : declared;
        }

        private static IEnumerable<Type> OrderingTargets(Type system)
        {
            foreach (UpdateBeforeAttribute before in
                     system.GetCustomAttributes<UpdateBeforeAttribute>())
            {
                if (before.SystemType != null)
                {
                    yield return before.SystemType;
                }
            }

            foreach (UpdateAfterAttribute after in
                     system.GetCustomAttributes<UpdateAfterAttribute>())
            {
                if (after.SystemType != null)
                {
                    yield return after.SystemType;
                }
            }
        }

        /// <summary>
        /// Every concrete system class the framework ships.
        /// </summary>
        /// <remarks>
        /// The base type is matched by NAME rather than by <c>typeof(ComponentSystemBase)</c> so the
        /// scan cannot start passing vacuously if the test assembly's Unity.Entities reference is
        /// ever reshuffled — a check that silently stops checking is the thing being guarded here.
        /// </remarks>
        private static IEnumerable<Type> FrameworkSystems()
        {
            Assembly assembly = typeof(ExpandNullforge.Portals.DimensionPortal).Assembly;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException loadFailure)
            {
                types = loadFailure.Types.Where(t => t != null).ToArray();
            }

            return types.Where(t =>
                t != null &&
                t.IsClass &&
                !t.IsAbstract &&
                !string.IsNullOrEmpty(t.Namespace) &&
                t.Namespace.StartsWith("ExpandNullforge", StringComparison.Ordinal) &&
                DerivesFromASystem(t));
        }

        private static bool DerivesFromASystem(Type type)
        {
            for (Type walk = type.BaseType; walk != null; walk = walk.BaseType)
            {
                if (walk.Name == "ComponentSystemBase" || walk.Name == "SystemBase")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
