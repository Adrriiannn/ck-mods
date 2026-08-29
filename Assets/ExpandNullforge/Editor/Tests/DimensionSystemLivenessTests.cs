#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

using ExpandNullforge.EditorTools;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Proves every framework system is explicitly created by the mod entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FRAMEWORK'S MOST EXPENSIVE FAILURE CLASS, caught here once instead of one domain at a
    /// time. A system that is written, tested and referenced by nothing costs no build error, no
    /// log line and no crash: it simply never ticks, and the feature it carries is dead while every
    /// other sign says it shipped. The boss-phase system, the biome heartbeat, the triggered-tile
    /// system, the hazard-condition system, the portal-offering system and the world-event system
    /// were each found that way, one at a time, long after they were declared finished.
    /// </para>
    /// <para>
    /// Leaning on ECS auto-creation is not an alternative, and <c>ExpandNullforgeModEntry</c> says
    /// so in its own comment: a mod assembly's systems are picked up by whichever loader version
    /// happens to scan them, so a build that works today stops working on a game update without
    /// anybody touching the mod. Explicit creation is the liveness guarantee, which makes "is it
    /// named in the mod entry point" the honest question to ask.
    /// </para>
    /// <para>
    /// The check reads the mod entry's SOURCE rather than reflecting over its behaviour, because
    /// the registration methods need a live <c>World</c> and a running game. The source is the
    /// artifact a reviewer changes, so the source is what this pins.
    /// </para>
    /// </remarks>
    public sealed class DimensionSystemLivenessTests
    {
        /// <summary>
        /// Systems whose creation call deliberately lives somewhere other than the mod entry point.
        /// </summary>
        /// <remarks>
        /// Empty on purpose. An entry here is a promise that some other line creates the system,
        /// and that promise has been wrong every time it was made informally — so making one costs
        /// a name, a reason, and the file:line of whatever does create it.
        /// </remarks>
        private static readonly HashSet<string> CreatedElsewhere =
            new HashSet<string>(StringComparer.Ordinal);

        [Test]
        public void EveryFrameworkSystemIsCreatedByTheModEntryPoint()
        {
            string source = ReadModEntry();
            List<string> missing = new List<string>();
            int checkedSystems = 0;

            foreach (Type system in FrameworkSystems())
            {
                if (CreatedElsewhere.Contains(system.Name))
                {
                    continue;
                }

                checkedSystems++;
                if (IsCreatedIn(source, system.Name))
                {
                    continue;
                }

                missing.Add(
                    system.FullName +
                    "  ->  world.GetOrCreateSystemManaged<" + system.FullName + ">();");
            }

            Assert.That(
                checkedSystems,
                Is.GreaterThan(0),
                "No framework systems were found, so this test proved nothing. The scan is broken.");
            Assert.That(
                missing,
                Is.Empty,
                "A framework system is never created, so it never ticks and whatever it carries is " +
                "dead in game with no error and no log line. Add each line below to " +
                "ExpandNullforgeModEntry, in the world the system belongs to (the server block for " +
                "simulation, the client block for anything a player sees, both when it is both):\n" +
                string.Join("\n", missing));
        }

        /// <summary>
        /// Whether the mod entry's source creates this system, however it spells the namespace.
        /// </summary>
        /// <remarks>
        /// Matching the whole call rather than the bare name is what stops a mention in a comment,
        /// or a <c>GetExistingSystemManaged</c> lookup that only reports on a system somebody else
        /// was supposed to create, from reading as a creation.
        /// </remarks>
        private static bool IsCreatedIn(string source, string systemName)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                source,
                @"GetOrCreateSystemManaged\s*<\s*(?:[A-Za-z_][A-Za-z0-9_]*\s*\.\s*)*" +
                System.Text.RegularExpressions.Regex.Escape(systemName) +
                @"\s*>");
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

        private static string ReadModEntry()
        {
            return DimensionFrameworkSourceScanner.ReadByName("ExpandNullforgeModEntry.cs");
        }
    }
}
#endif
