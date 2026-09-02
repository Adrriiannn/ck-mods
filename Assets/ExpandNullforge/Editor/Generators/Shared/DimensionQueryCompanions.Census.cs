using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What is still missing from an object, and what a row can never close on its own.
    /// </summary>
    internal static partial class DimensionQueryCompanions
    {
        /// <summary>
        /// The automation answers every vanilla machine carries one of.
        /// </summary>
        /// <remarks>
        /// Read off the seventeen prefabs the census counted as machines —
        /// <c>CrudeDrillForwardEntity</c>, <c>RobotArmForwardEntity</c>, <c>ItemCollectorEntity</c>,
        /// <c>SprinklerEntity</c> — rather than guessed from the names.
        /// </remarks>
        /// <summary>Whether the object carries one of the automation answers.</summary>
        private static bool ItIsWired(GameObject root)
        {
            for (int i = 0; i < TheAnswersThatMakeSomethingAMachine.Length; i++)
            {
                if (HasNamed(root, TheAnswersThatMakeSomethingAMachine[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly string[] TheAnswersThatMakeSomethingAMachine =
        {
            "AutomatedMoverAuthoring",
            "AutomatedMoverSharedAuthoring",
            "AutomatedCrafterAuthoring",
            "AutomatedMinerAuthoring",
            "AutomatedStorageAuthoring",
            "AutomatedHarvestAndMoverAuthoring",
            "AutomatedMoveAndPlanterAuthoring",
            "DrillAuthoring",
            "SprinklerAuthoring",
        };

        /// <summary>Whether the machine's job is carrying items from one place to another.</summary>
        /// <remarks>
        /// The 36 vanilla shapes on Category00 plus Category12 split in two by exactly this, and
        /// the split is not a guess about the names: ItemCollectorEntity, the four RobotArm
        /// prefabs, the four RobotFarmArm prefabs and the four PulseCircuit prefabs watch 20, and
        /// every drill, SprinklerEntity, LampEntity, SirenLampEntity and ChineseLanternEntity also
        /// watch Category15.
        /// </remarks>
        private static bool ItMovesItemsAround(GameObject root)
        {
            for (int i = 0; i < TheAnswersThatCarryItemsAbout.Length; i++)
            {
                if (HasNamed(root, TheAnswersThatCarryItemsAbout[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly string[] TheAnswersThatCarryItemsAbout =
        {
            "AutomatedMoverAuthoring",
            "AutomatedMoverSharedAuthoring",
            "AutomatedStorageAuthoring",
            "AutomatedHarvestAndMoverAuthoring",
            "AutomatedMoveAndPlanterAuthoring",
        };

        /// <summary>Names the object in a sentence a deeper pass wrote about it.</summary>
        private static Action<string> Prefixed(string displayName, Action<string> say)
        {
            if (say == null)
            {
                return null;
            }

            return delegate(string message) { say("'" + displayName + "' " + message); };
        }

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> appends to a gap the author is
        /// never told about.
        /// </summary>
        public const string SaysNothingAboutIt = " | says nothing";

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> puts before a gap's own words.
        /// </summary>
        /// <remarks>
        /// Written down so the guard can take the words back out and look for them in what the
        /// sweep really said. The words in a listing come off the row; the words an author reads
        /// come out of <see cref="CloseTheGaps"/>, and until the guard compared the two, deleting
        /// the sweep's whole reporting block left every test in the file green.
        /// </remarks>
        public const string SaysThis = " | says: ";

        /// <summary>
        /// What <see cref="WhatIsStillMissingWithoutTouchingIt"/> appends to a gap whose row says
        /// it supplies the missing thing itself ON THIS OBJECT.
        /// </summary>
        /// <remarks>
        /// The last three words are the whole of it. Four rows fill when they can and speak when
        /// they cannot — a door with nothing to use on it, a touch attack on a creature nobody
        /// summoned — and stamping this from "the row has a fill" alone put the marker on a gap
        /// those rows never claimed. <c>Companion.OnlyWhen</c> is where a row writes down which
        /// objects its fill applies to, and this is stamped only when that says yes.
        /// </remarks>
        public const string TheRowSaysItFillsThisIn = " | the row says it fills this in";

        /// <summary>
        /// Lists what is still missing, WITHOUT changing the object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS EXISTS BECAUSE THE GUARD WAS AUDITING ITSELF. The lister it replaced ran
        /// the same row logic as <see cref="CloseTheGaps"/> and performed every fill while it
        /// looked,
        /// so the one test named for proving the companions work could not fail when they stopped
        /// working: empty the body of <c>CloseTheGaps</c> and the list came back identical, because
        /// the listing had done the filling. This one only reads. It is what the test observes
        /// after the production sweep has run, so what it reports is what the sweep left behind.
        /// </para>
        /// <para>
        /// THE MUTATING LISTER WAS DELETED RATHER THAN KEPT. It had no callers left once the guard
        /// stopped auditing itself, and a dead method carrying a doc that says the guard uses it is
        /// worse than no method: the next person reads the doc, not the call sites.
        /// </para>
        /// <para>
        /// It also marks the rows that CLAIM to fill their own gap. Four rows carry both a fill and
        /// a sentence, and a caller that excused any spoken-about gap excused those four as well —
        /// gut one of their fills and the entry simply changed from "says nothing" to "says", which
        /// the guard skipped. A row that says it fills something has to have filled it; only a row
        /// with nothing but words is allowed to leave the gap open.
        /// </para>
        /// </remarks>
        public static List<string> WhatIsStillMissingWithoutTouchingIt(GameObject root)
        {
            List<string> missing = new List<string>();
            if (root == null)
            {
                return missing;
            }

            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                Companion row = all[i];
                if (root.GetComponent(row.Present) == null)
                {
                    continue;
                }

                if (TheGapIsReallyClosed(root, row))
                {
                    continue;
                }

                // THE MARKER IS THE ROW'S CLAIM ABOUT THIS OBJECT, not about itself. Stamped from
                // FillIn != null alone it was a claim four rows never made: a door with nothing to
                // use on it, a tile-laying answer on something that is not a blast, a touch attack
                // on a creature nobody summoned. Those rows decline BY DESIGN and say so in words,
                // and a caller that reads the marker as "this had to be filled" reports a break
                // that is the row working. OnlyWhen is where a row writes down which objects its
                // fill applies to, and it is a pure read, so this stays a listing.
                bool theRowMeantToFillThisOne =
                    row.FillIn != null && (row.OnlyWhen == null || row.OnlyWhen(root));

                missing.Add(
                    row.Present.Name + " needs " + row.AlsoNeeds + " (" + row.ReadBy + ")" +
                    (theRowMeantToFillThisOne ? TheRowSaysItFillsThisIn : string.Empty) +
                    (string.IsNullOrEmpty(row.SayInstead)
                        ? SaysNothingAboutIt
                        : SaysThis + row.SayInstead));
            }

            return missing;
        }

        /// <summary>
        /// A gap a row said nothing can carry: a tracker that is present and sees nothing.
        /// </summary>
        public const string ATrackerThatCanActuallySeeSomething =
            "ATrackerThatCanActuallySeeSomething";

        /// <summary>A blast that is present and reaches nowhere.</summary>
        public const string ABlastWithARealRadius = "ABlastWithARealRadius";

        /// <summary>Every name a row may put in its "also needs" that is not a component.</summary>
        /// <remarks>
        /// Written down so the guard can insist every other "also needs" is a real type this build
        /// has. A typo in a component name would otherwise read as a gap nothing can ever close and
        /// would be reported forever.
        /// </remarks>
        public static readonly string[] GapsThatAreAValueRatherThanAComponent =
        {
            ATrackerThatCanActuallySeeSomething,
            ABlastWithARealRadius,

            // "BeamBufferHasNoProducerInTheGame" is deliberately not in this list, because it is
            // not true. The beam system fills its own buffer; the gap is one missing line in Core
            // Keeper's converter, and the framework supplies it with a converter of its own. See
            // the beam rows.
            "NothingInTheGameReadsThis",
        };

        /// <summary>
        /// Whether the fill actually left the object with what the row said it needed.
        /// </summary>
        /// <remarks>
        /// <c>Fill&lt;T&gt;</c>, <c>NoticesThingsNearby</c>, <c>NoticesAPlayerStandingOnIt</c>,
        /// <c>NoticesWhatAPlayerIsHoldingFromAcrossTheRoom</c> and <c>NoticesWhatItCanHurt</c> all
        /// return true unconditionally, so believing the return value meant the "does close it"
        /// half of the guard could not fail for any row that fills — which is every row that claims
        /// to close anything. This looks at the object instead.
        /// </remarks>
        private static bool TheGapIsReallyClosed(GameObject root, Companion row)
        {
            if (root == null || row == null)
            {
                return false;
            }

            if (row.AlsoNeeds == ATrackerThatCanActuallySeeSomething)
            {
                return HowFarItSees(root) > 0f && WhatItWatches(root) != 0u;
            }

            if (row.AlsoNeeds == ABlastWithARealRadius)
            {
                return ItIsABlastThatActuallyReaches(root);
            }

            // The two rows that ask for a pet accept a minion or a minion's data as well, because
            // the system behind them runs on anything somebody summoned.
            if (row.AlsoNeeds == "PetAuthoring")
            {
                return ItBelongsToSomebody(root);
            }

            return HasNamed(root, row.AlsoNeeds);
        }

        /// <summary>What every row says it also needs, so the guard can check the names.</summary>
        public static List<string> EveryThingARowSaysIsAlsoNeeded()
        {
            List<string> needed = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (!needed.Contains(all[i].AlsoNeeds))
                {
                    needed.Add(all[i].AlsoNeeds);
                }
            }

            return needed;
        }

        /// <summary>The authoring components this framework writes that have a row here.</summary>
        public static List<string> CoveredAuthoringComponents()
        {
            List<string> names = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (!names.Contains(all[i].Present.Name))
                {
                    names.Add(all[i].Present.Name);
                }
            }

            return names;
        }

        /// <summary>Rows whose gap only the author can close, with the sentence they get.</summary>
        public static List<string> RowsThatOnlyTheAuthorCanClose()
        {
            List<string> said = new List<string>();
            Companion[] all = Rows;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].FillIn == null)
                {
                    said.Add(all[i].Present.Name + ": " + all[i].SayInstead);
                }
            }

            return said;
        }
    }
}
