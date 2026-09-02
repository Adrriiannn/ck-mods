#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Holds the framework to exactly one place where Core Keeper's Burst compilation is switched off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SWITCHING BURST OFF IS NOT A LOCAL DECISION, which is the whole reason this test exists. The
    /// first <c>DisableBurstFor*</c> call anywhere in the process makes the mod loader install a
    /// Harmony prefix and postfix on <c>Unity.Entities.WorldUnmanagedImpl.UpdateSystem</c> — the
    /// dispatch point for every unmanaged system in every world, of which Core Keeper has roughly
    /// four hundred. The detour is paid on all of them, in both the client and server worlds, for the
    /// rest of the session, and nothing short of a domain reload removes it. A second call adds a
    /// second de-Bursted system but no second detour, so the cost reads as free at the call site and
    /// is anything but.
    /// </para>
    /// <para>
    /// WHY THE ONE THAT IS ALLOWED IS ALLOWED. Four of the game's calls into
    /// <c>EntityUtility.AddTile</c> can carry a custom tileset id — placing a block, tilling,
    /// watering, and emptying a bucket — and all four are inlined into the Burst-compiled equipment
    /// job, where a Harmony patch is simply never reached. Vanilla's own guard then refuses any id
    /// above 74 and reports it through Burst's NATIVE log path, which no managed log handler can
    /// intercept, so the alternative is not just a broken placement path but an unsuppressable error
    /// line for every block a player sets down.
    /// </para>
    /// <para>
    /// EVERY OTHER TEMPTATION HAS A BETTER ANSWER, and they are written down rather than rediscovered:
    /// bracket the Bursted work by capturing before and restoring after, the way
    /// <c>DimensionCustomTileRescue</c> carries custom tiles across deserialization; or run a narrow
    /// companion system that early-outs on an empty registry, the way
    /// <c>DimensionHazardConditionSystem</c> does. <c>Docs/IdentityGates.md</c> records the cases
    /// where the honest answer was to leave the gate alone and say so.
    /// </para>
    /// </remarks>
    internal sealed class DimensionBurstBudgetTests
    {
        /// <summary>
        /// The one file permitted to switch Burst off, and why.
        /// </summary>
        private static readonly Dictionary<string, string> Allowed =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "ExpandNullforgeModEntry.cs",
                    "the equipment update, armed per world and only when custom content is loaded"
                },
            };

        [Test]
        public void BurstIsSwitchedOffInExactlyOnePlace()
        {
            List<string> offenders = new List<string>();
            int allowedHits = 0;

            List<string> sources = RuntimeSources();
            string shipSetProblem =
                DimensionSandboxGuard.ShipSetProblem(Application.dataPath, sources.Count);
            Assert.That(
                shipSetProblem,
                Is.Null,
                "This test read no shipped sources, so it proved nothing about where Burst is " +
                "switched off. " + shipSetProblem);

            foreach (string file in sources)
            {
                string[] lines = File.ReadAllLines(file);
                string name = Path.GetFileName(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].IndexOf("BurstDisabler.DisableBurstFor", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    // A mention inside a comment is documentation, not a call.
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("///", StringComparison.Ordinal) ||
                        trimmed.StartsWith("*", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Allowed.ContainsKey(name))
                    {
                        allowedHits++;
                        continue;
                    }

                    offenders.Add(name + ":" + (i + 1) + "  " + trimmed);
                }
            }

            Assert.That(
                allowedHits,
                Is.GreaterThan(0),
                "Nothing switches Burst off any more, which is good news — but this test is now " +
                "guarding nothing and its allowlist should be emptied so a future call is caught.");

            Assert.That(
                offenders,
                Is.Empty,
                "Burst is switched off somewhere new. That installs a Harmony detour on EVERY " +
                "unmanaged system in every world for the whole session, so it is a framework-wide " +
                "cost, not a local one. Bracket the Bursted work instead (see " +
                "DimensionCustomTileRescue) or run a companion system that early-outs on an empty " +
                "registry (see DimensionHazardConditionSystem). If it genuinely cannot be avoided, " +
                "add the file to this test's allowlist with the reason:\n  " +
                string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The same set of files the sandbox guard scans: everything the asmdefs say is compiled
        /// into an assembly the game loads.
        /// </summary>
        /// <remarks>
        /// The caller checks the count against <see cref="DimensionSandboxGuard.ShipSetProblem"/>
        /// before reading any of them. An empty set and a clean set are the same thing to a loop.
        /// </remarks>
        private static List<string> RuntimeSources()
        {
            return DimensionSandboxGuard.ShippedSourceFiles(Application.dataPath);
        }
    }
}
#endif
