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
    /// Walking the entities a world holds, and saying what the walk did not cover.
    /// </summary>
    internal static partial class DimensionSelfAudit
    {
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
        /// else's server has no server world in their process, so this does not run for them at
        /// all.
        /// </para>
        /// <para>
        /// AND THEY ARE TOLD SO ON A LINE THEY CAN SEE. The trace below used to be described as
        /// telling them, and it is off by default like every other trace, so the session where this
        /// check never ran read exactly like the session where it passed. The summary line for the
        /// world carries the clause now — see <see cref="WhatWasNotAskedOf"/>.
        /// </para>
        /// </remarks>
        private static void RunEntityAudit(ArmedWorld armed)
        {
            if (!DimensionLogConfig.EntityAudit || entityAuditDone)
            {
                objectCheckDidNotRunHere = true;
                return;
            }

            if (armed.IsClient)
            {
                DimensionLog.Trace(
                    Ch.Audit,
                    armed.World,
                    // IT DOES NOT SAY THERE IS NO SERVER WORLD, because on a host there is one and
                    // this line is reached anyway — the client world simply is not where the
                    // question is asked. Which is a different sentence from the one that used to
                    // be here.
                    "the object check reads server-side prefabs, so it is asked of the server "
                        + "world and not of " + armed.World.Name + ". On a host or a single-player "
                        + "world that happens as the server world loads; for a player joined to "
                        + "somebody else's server there is no server world here and it does not "
                        + "run at all.");
                objectCheckDidNotRunHere = true;
                return;
            }

            entityAuditDone = true;
            DimensionEntityAudit.Result result =
                DimensionEntityAudit.Run(armed.World, DimensionLogConfig.EntityAuditBudget);

            // Read by the world's own verdict below, which is the sentence most people see.
            objectCheckSawItemsOnly = result.ResolvedItems > 0 && result.LedgerSubjects == 0;

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
                // NOT SAID WHEN NOTHING WAS DECLARED IN THE FIRST PLACE. An empty subject list has
                // two causes and only one of them is a fault: a pack declared objects and none of
                // them resolved, or there is no pack. In the second case CheckItemLedger has
                // already said so on its own line, in the item channel, in more detail — and this
                // one arriving under it saying "this is not a clean result" turned a session with
                // no content pack installed into a session that looks broken.
                if (checkedNothing)
                {
                    return;
                }

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
                    WhatTheObjectCheckCoveredAndDidNot(result, armed.World.Name));
            }
        }

        /// <summary>
        /// The object check's own summary, scope first and verdict only over that scope.
        /// </summary>
        /// <remarks>
        /// <para>
        /// IT USED TO CONGRATULATE ON A SUBJECT LIST IT HAD NOT DESCRIBED. The line was
        /// "N of this mod's objects checked against 41 of the game's queries; all of them carry
        /// what the systems that read them require", where N was whatever resolved and 41 was the
        /// whole table — most of which is creature and world-object rules that an item cannot
        /// trigger. A content pack generated before the object ledger existed declares its items
        /// and nothing else, so on that pack the sentence measured two tileset blocks against
        /// thirty-nine creature rules and called the result clean. Nothing in it was false and the
        /// reader was still misled, because the two numbers in it were the size of the walk rather
        /// than the size of what the walk could have caught.
        /// </para>
        /// <para>
        /// SO THE VERDICT IS NOW SECOND AND NARROWER THAN THE OLD ONE. First how many rules were a
        /// test of anything, which is measured on the walk itself
        /// (<see cref="DimensionEntityAudit.Result.RulesApplied"/>) rather than assumed from the
        /// table's length; then what was not in the list at all, when the wider half of it is
        /// empty; and the affirmative clause is dropped altogether when no rule applied, because
        /// there is nothing there to be clean.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// It takes the world's NAME rather than the world, and is internal rather than private,
        /// for one reason: a test can then hand it a result and read the sentence back. The
        /// sentence is the whole of what this change is, and a test that could only reach it
        /// through a live <c>World</c> could not run outside Unity at all.
        /// </remarks>
        internal static string WhatTheObjectCheckCoveredAndDidNot(
            DimensionEntityAudit.Result result,
            string worldName)
        {
            StringBuilder line = new StringBuilder();
            line.Append(result.Checked);
            line.Append(" of this mod's objects checked in ");
            line.Append(worldName);
            line.Append(" against ");
            line.Append(result.RulesInTable);
            line.Append(" of the game's queries. ");

            if (result.RulesApplied == 0)
            {
                line.Append("None of those ");
                line.Append(result.RulesInTable);
                line.Append(" rules asked for anything these objects carry, so nothing here was "
                    + "actually tested — read this as a walk that found nothing to look at, not "
                    + "as a clean result.");
            }
            else
            {
                line.Append(result.RulesApplied);
                line.Append(result.RulesApplied == 1
                    ? " of them applied to something in this list, and what it asked for was there."
                    : " of them applied to something in this list, and everything they asked for "
                        + "was there.");
                if (result.RulesInTable > result.RulesApplied)
                {
                    line.Append(" The other ");
                    line.Append(result.RulesInTable - result.RulesApplied);
                    line.Append(" ask for a component nothing here carries, so they passed without "
                        + "looking and are not part of that.");
                }
            }

            // WHAT WAS NOT IN THE LIST, when the half of it that holds everything but items is
            // empty. The table is mostly creature and world-object rules, so an item-only list is
            // not a small version of the check — it is a different one, and the reader has no way
            // to know that from a count.
            if (result.LedgerSubjects == 0)
            {
                line.Append(" NOT LOOKED AT: this session's subject list is ");
                line.Append(result.ItemSubjects);
                line.Append(result.ItemSubjects == 1 ? " item" : " items");
                line.Append(" and nothing else. ");
                line.Append(DimensionGeneratedObjectLedger.DeclaredCount == 0
                    ? "No content pack declared any generated object beside its items, which is "
                        + "what a pack built before the framework kept that list looks like from "
                        + "here. Generate the pack again and its creatures, bosses, summoning "
                        + "circles, plants, containers and world objects come into this check with "
                        + "it."
                    : DimensionGeneratedObjectLedger.DeclaredCount
                        + " were declared and none of them resolved, so the game does not answer "
                        + "to any of their names and none could be checked.");
            }

            if (result.StoppedEarly)
            {
                line.Append(" The budget stopped the walk before the end of the list; raise "
                    + "diagnostics entityAuditBudget to check the rest.");
            }

            return line.ToString();
        }
    }
}
