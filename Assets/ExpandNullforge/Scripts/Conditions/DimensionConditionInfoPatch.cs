using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Conditions
{
    /// <summary>
    /// Answers the client's questions about a stat effect a mod invented, instead of letting it
    /// walk off the end of Core Keeper's own list of answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS A CRASH FIX FIRST AND A FEATURE SECOND. Core Keeper keeps a second, entirely
    /// separate copy of its condition table for everything a player looks at — the row of buffs, an
    /// item's tooltip, the character sheet, the talent window. That copy is a plain
    /// <c>List&lt;ConditionInfo&gt;</c> on <c>ConditionsTable</c>, filled to exactly 357 entries by
    /// <c>Init</c> and read by <c>GetConditionInfo</c> with a bare indexer and no bounds check
    /// (<c>Pug.Base/ConditionsTable.cs:44-52</c>). The first condition a mod invents is number 357,
    /// so the very first read of it threw. Not once: <c>ConditionsContainerUI.UpdateConditions</c>
    /// walks the player's whole buffs list twice every frame and asks this question about every
    /// entry, so a mod's own buff landing on a player took the interface down until it expired.
    /// </para>
    /// <para>
    /// GROWING THE LIST BY HAND WOULD NOT HAVE HELD. <c>GetConditionInfo</c> starts by comparing the
    /// list's length to 357 and rebuilding it from scratch when they differ, so a longer list is
    /// thrown away on the next call. The question has to be intercepted rather than the answer
    /// table extended, which is what this does.
    /// </para>
    /// <para>
    /// AND SINCE IT IS ANSWERING ANYWAY, IT ANSWERS PROPERLY. The five things this copy of the table
    /// carries that the simulation's copy does not — the picture, whose wording to borrow, whether
    /// to show a decimal point, whether to show the sign, whether to appear in an item's stat list —
    /// have never been reachable from a mod. They are now, and they come from the same authored
    /// asset as everything else about the condition.
    /// </para>
    /// <para>
    /// COST WHEN UNUSED IS ONE BOOLEAN. Every vanilla condition on every player goes through here,
    /// up to eighteen of them twice a frame, so the first thing the method does is ask whether this
    /// mod has claimed anything at all — exactly as
    /// <see cref="DimensionConditionsTablePatch"/> already does for the simulation's table.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ConditionsTable), "GetConditionInfo")]
    internal static class DimensionConditionInfoPatch
    {
        /// <summary>How many times this patch has actually run. Read by the world-load self-audit.</summary>
        /// <remarks>
        /// A patch that binds cleanly and never runs is its own bug class, and nothing else in the
        /// process can tell the two apart: the mod sandbox denies <c>HarmonyLib.Harmony</c>, so the
        /// framework cannot ask Harmony what it bound. One static increment is the whole of the
        /// evidence, and it costs one add on a path the game was already walking.
        /// </remarks>
        internal static int Fired;

        /// <summary>
        /// Numbers already complained about, so a buff nobody can explain says so once rather than
        /// thirty-six times a second.
        /// </summary>
        private static readonly HashSet<int> alreadyReported = new HashSet<int>();

        [HarmonyPrefix]
        private static bool Prefix(ConditionID conditionID, ref ConditionInfo __result)
        {
            Fired++;

            ConditionInfo ours;
            if (!TryAnswer(conditionID, out ours))
            {
                return true;
            }

            __result = ours;
            return false;
        }

        /// <summary>
        /// Whether this number is one of ours, and what to say about it if so.
        /// </summary>
        /// <remarks>
        /// The whole of the prefix's decision, in a method that can be asked the question without a
        /// running game — the patch itself needs one.
        /// </remarks>
        internal static bool TryAnswer(ConditionID conditionID, out ConditionInfo info)
        {
            info = default(ConditionInfo);
            if (!DimensionConditionRegistry.HasAny)
            {
                return false;
            }

            int number = (int)conditionID;
            if (number < DimensionConditionRegistry.FirstFreeNumber)
            {
                // One of Core Keeper's own. Its own table has the answer and is in range.
                return false;
            }

            DimensionCustomCondition ours;
            if (!DimensionConditionRegistry.TryGetByNumber(number, out ours))
            {
                ReportOnce(number);

                // Nothing sensible to say about it, but saying nothing is still better than the
                // exception vanilla would throw one line later. A blank answer reads as a buff with
                // no picture, which the buff row skips.
                info = new ConditionInfo { Id = conditionID };
                return true;
            }

            info = new ConditionInfo
            {
                // Without this the id is None, the wording lookup becomes "Conditions/None", and the
                // buff shows the game's blank line instead of the mod's own — with nothing logged.
                Id = conditionID,
                effect = ours.Effect,
                isNegative = ours.IsBad,
                isPermanent = ours.LastsForever,
                isUnique = ours.OnlyOneAtATime,
                isAdditiveWithSelf = ours.AddsToItself,
                isInheritedByProjectiles = ours.PassedOnByShots,
                overrideIfRemainingValueIsHigher = ours.AStrongerOneWins,
                icon = ours.Icon,
                useSameDescAsId = DimensionConditionRegistry.ResolveByName(ours.ReadsLikeThisOne),
                showDecimal = ours.ShowsADecimal,
                skipShowingSignInfrontOfValue = ours.HidesThePlusSign,
                skipShowingStatText = ours.HidesTheStatLine
            };
            return true;
        }

        private static void ReportOnce(int number)
        {
            if (!alreadyReported.Add(number))
            {
                return;
            }

            DimensionLog.Problem(DimensionLogChannels.Condition, null, 
                "Something is carrying stat effect number " + number +
                ", which no condition in this dimension claims. It shows as a blank buff with no " +
                "name and no picture. This is what an old save looks like after a condition was " +
                "renamed or removed from the mod: put the condition back under its old name, or " +
                "wait for the effect to run out.");
        }

        /// <summary>Forgets what has been complained about, for a mod reloaded in the editor.</summary>
        internal static void ForgetReports()
        {
            alreadyReported.Clear();
        }
    }
}
