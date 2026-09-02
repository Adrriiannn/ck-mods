#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The checks that keep those lists honest about the tree they describe.
    /// </summary>
    internal sealed partial class DimensionQueryCompanionTests
    {
        /// <summary>
        /// Every name excused from adding is a call the tree really makes on a Core Keeper surface.
        /// </summary>
        /// <remarks>
        /// The excuse list is the one place where a single word switches the guard off for a whole
        /// helper, and nothing checked that its names were even used. A name that no longer takes a
        /// Core Keeper surface anywhere is a standing invitation: write a new helper, call it one of
        /// those names, and every scan in this file passes over it. So the list has to shrink when
        /// the tree does.
        /// </remarks>
        [Test]
        public void EveryNameExcusedFromAddingIsAFormTheTreeReallyUses()
        {
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AnyGenericCall.Matches(file.Code))
                {
                    string caller = match.Groups[1].Value;
                    if (!GenericCallsThatDoNotAddAComponent.Contains(caller))
                    {
                        continue;
                    }

                    foreach (string argument in EveryTypeArgumentIn(match.Groups[2].Value))
                    {
                        string component = file.Resolve(argument);

                        // The same two filters the unclassified scan applies, so this list is
                        // measured against exactly the calls that scan would have questioned.
                        if (component.StartsWith("Dimension", StringComparison.Ordinal) ||
                            !IsACoreKeeperSurfaceName(component))
                        {
                            continue;
                        }

                        used.Add(caller);
                        break;
                    }
                }
            }

            List<string> neverUsed = new List<string>();
            foreach (string name in GenericCallsThatDoNotAddAComponent)
            {
                if (!used.Contains(name))
                {
                    neverUsed.Add(name);
                }
            }

            neverUsed.Sort(StringComparer.Ordinal);

            Assert.That(
                neverUsed,
                Is.Empty,
                "GenericCallsThatDoNotAddAComponent excuses a name that nothing in the framework " +
                "passes a Core Keeper component to. An excuse nobody needs is a place for a new " +
                "add helper to hide, because a name on this list is skipped by the component scan, " +
                "by the unclassified-form scan and by the sweep check all at once. Take the name " +
                "out; if the call comes back, the unclassified-form test will ask for it again and " +
                "whoever puts it back will have read what it does:\n  " +
                string.Join("\n  ", neverUsed));
        }

        [Test]
        public void EveryComponentTheGeneratorsAddHasBeenReadAgainstItsSystem()
        {
            Dictionary<string, string> answered = TheAnswerLists();

            List<string> unanswered = new List<string>();
            HashSet<string> alreadySaid = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                {
                    string component = file.Resolve(argument);

                    // The framework's own components answer to the framework's own systems,
                    // whose queries are in this repository and are read where they are written.
                    if (component.StartsWith("Dimension", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Not a suffix test any more. A Core Keeper surface is anything that is not on
                    // the written-down list of things that are not one, so InteractableObject and
                    // the runtime CD components can no longer walk past the question.
                    if (NotACoreKeeperSurface.Contains(component))
                    {
                        continue;
                    }

                    if (answered.ContainsKey(component))
                    {
                        continue;
                    }

                    if (alreadySaid.Add(component))
                    {
                        unanswered.Add(
                            component + "  (" + file.Name + ":" + file.LineAt(match.Index) + ")");
                    }
                }
                }

                // A COMPONENT COPIED RATHER THAN NAMED. There are none today, and this refuses
                // the form so the first one cannot arrive in silence.
                foreach (Match copy in CopiesAComponent.Matches(file.Code))
                {
                    string where = file.Name + ":" + file.LineAt(copy.Index);
                    if (alreadySaid.Add(where))
                    {
                        unanswered.Add(
                            "a component copied from another object at " + where +
                            ", which names no component this scan can read");
                    }
                }
            }

            unanswered.Sort(StringComparer.Ordinal);

            Assert.That(
                unanswered,
                Is.Empty,
                "A generator has started putting a NEW Core Keeper component on an object, and " +
                "nobody has said what the system that reads it also needs. Read that system's " +
                "query. If it needs something else beside this component, add a row to " +
                "DimensionQueryCompanions so the sweep supplies it or says in words why it cannot. " +
                "If the query is already satisfied by what we write, add the name to " +
                "ReadAgainstTheirSystemAndNeedNothingBeside. If you read it and wrote the verdict " +
                "into E:\\ck mods\\ck-research\\query-match-census.md, add it to " +
                "ReadAgainstTheirSystemAndTheVerdictIsRecorded. " +
                "NobodyHasReadTheSystemThatConsumesTheseYet is not a place to put a new surface — " +
                "it names what was already left over when this test was written, and " +
                "shipping one unread is exactly how a feature ends up generating cleanly and " +
                "doing nothing:\n  " +
                string.Join("\n  ", unanswered));
        }

        /// <summary>
        /// Runs the production sweep over a probe for every row, and reads the object afterwards.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS TEST MUST NOT AUDIT ITSELF. Calling <c>CloseTheGaps</c> and then
        /// <c>WhatIsStillMissing</c> does exactly that: the second runs the identical row logic and
        /// PERFORMS EVERY FILL while it looks — its own remark says so — so replacing the body of
        /// <c>CloseTheGaps</c> with nothing leaves this test green. The listing has done the work the
        /// listing is reporting on, the sentence list comes back empty and the only other
        /// assertion iterates zero times, and the one guard named for proving the companions work
        /// is the one thing in the file that cannot fail when the companions stop working.
        /// </para>
        /// <para>
        /// It reads the probe with <c>WhatIsStillMissingWithoutTouchingIt</c>, which only
        /// looks. Every gap it reports is a gap the production sweep really left, and the count of
        /// gaps the sweep closed is asserted to be greater than zero, so an empty sweep fails on
        /// both halves rather than passing on both.
        /// </para>
        /// <para>
        /// AND A ROW THAT SAYS IT FILLS SOMETHING HAS TO HAVE FILLED IT. Four rows carry a fill and
        /// a sentence, and the old check excused any gap that was spoken about — so gutting one of
        /// those four fills only changed the entry from "says nothing" to "says", which was
        /// skipped. Words excuse a row that has nothing but words.
        /// </para>
        /// <para>
        /// "A ROW THAT FILLS" IS PER OBJECT.
        /// Stamping the marker from <c>FillIn != null</c> alone counts three
        /// rows that hand a pure READ in as their fill — is this blast big enough, does this
        /// creature belong to somebody — and a fourth, the door, which can only be filled on a door
        /// with something to use on it. On the bare two-component probe every one of them answers
        /// no, correctly, and speaks; the marker then says they claimed to fill it and this test
        /// counts four working rows as four breaks. The three reads are not fills at all,
        /// and the door declares its precondition in <c>Companion.OnlyWhen</c>, which the listing
        /// asks before it stamps.
        /// </para>
        /// <para>
        /// AND EVERY SENTENCE HAS TO HAVE BEEN SAID. Nothing else checks that: the sentence in
        /// the listing is read off the row, not off anything emitted, so deleting the whole
        /// <c>say</c> block in <c>CloseTheGaps</c> leaves every test in this file green and takes ten
        /// rows whose entire value is a warning down with it. A gap that survives the sweep is
        /// looked up in what the sweep actually said.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryRowThatSaysItCanCloseAGapDoesCloseIt()
        {
            List<string> broken = new List<string>();
            int gapsTheSweepClosed = 0;
            int probesWithAGapBeforeTheSweep = 0;

            foreach (string component in DimensionQueryCompanions.CoveredAuthoringComponents())
            {
                Type type = FindAuthoringType(component);
                if (type == null)
                {
                    broken.Add(component + " is named by a row and is not a type this build has.");
                    continue;
                }

                GameObject probe = new GameObject(component + "Probe");
                try
                {
                    probe.AddComponent<ObjectAuthoring>();
                    probe.AddComponent(type);

                    // READ FIRST, so the count below is what the SWEEP did and not what the
                    // reading did. Nothing between these two lines touches the object.
                    List<string> before =
                        DimensionQueryCompanions.WhatIsStillMissingWithoutTouchingIt(probe);

                    List<string> said = new List<string>();
                    DimensionQueryCompanions.CloseTheGaps(probe, "probe", said.Add);

                    List<string> missing =
                        DimensionQueryCompanions.WhatIsStillMissingWithoutTouchingIt(probe);

                    if (before.Count > 0)
                    {
                        probesWithAGapBeforeTheSweep++;
                    }

                    if (before.Count > missing.Count)
                    {
                        gapsTheSweepClosed += before.Count - missing.Count;
                    }

                    for (int i = 0; i < missing.Count; i++)
                    {
                        // A ROW THAT CLAIMS TO FILL ITS OWN GAP GETS NO EXCUSE. Words are for a
                        // row that has only words.
                        if (missing[i].Contains(DimensionQueryCompanions.TheRowSaysItFillsThisIn))
                        {
                            broken.Add(
                                missing[i] +
                                " — the sweep ran and it is still not there.");
                            continue;
                        }

                        // EVERY remaining gap on the probe, not only the probed component's own.
                        // A fill that adds a component which itself has a row is the bug class one
                        // level down, and dropping those entries meant nothing ever checked that
                        // the sweep settles.
                        //
                        // A gap is allowed to stay open only when THIS row tells the author about
                        // it in words. Asking whether the probe produced any sentence at all let
                        // one row's sentence excuse every other row's silence on the same object —
                        // WayPointAuthoring was the live instance, with two rows and one sentence.
                        // Silence is the defect, not the gap.
                        if (!missing[i].EndsWith(
                                DimensionQueryCompanions.SaysNothingAboutIt,
                                StringComparison.Ordinal))
                        {
                            // AND THE SENTENCE HAS TO HAVE REACHED THE AUTHOR. The listing reads
                            // the words off the row, so a row can carry a perfect sentence that
                            // nothing ever emits — delete the say block in CloseTheGaps and every
                            // other assertion here stays green while ten warnings go silent. The
                            // sweep was given a sink above; this is what it put in it.
                            int wordsStart = missing[i].IndexOf(
                                DimensionQueryCompanions.SaysThis,
                                StringComparison.Ordinal);
                            if (wordsStart < 0)
                            {
                                continue;
                            }

                            string words = missing[i].Substring(
                                wordsStart + DimensionQueryCompanions.SaysThis.Length);
                            if (!said.Contains("'probe' " + words))
                            {
                                broken.Add(
                                    missing[i] +
                                    " — the row has those words and the sweep never said them.");
                            }

                            continue;
                        }

                        broken.Add(missing[i] + " — and nothing was said about it.");
                    }

                    // Every sentence has to name the object it is about. A generation report is a
                    // list of lines from a whole run, and one that does not say which thing it is
                    // talking about cannot be acted on.
                    for (int i = 0; i < said.Count; i++)
                    {
                        if (!said[i].StartsWith("'probe'", StringComparison.Ordinal))
                        {
                            broken.Add(
                                component + " said \"" + said[i] +
                                "\", which does not name the object it is about.");
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }

            Assert.That(
                broken,
                Is.Empty,
                "A companion row claims to supply what a system also needs, and after the sweep ran " +
                "it is still not there — or it cannot be supplied and nothing was said. Either way " +
                "the object generates cleanly and the feature does nothing:\n  " +
                string.Join("\n  ", broken));

            // THE SWEEP HAS TO HAVE DONE SOMETHING. Both assertions above are satisfied by an
            // object with no gaps, and an object the sweep never touched has no gaps only because
            // nothing looked. This one fails the moment CloseTheGaps stops closing anything —
            // which is the state the whole file exists to keep the tree out of, and the state it
            // could not detect while the listing performed the fills itself.
            Assert.That(
                gapsTheSweepClosed,
                Is.GreaterThan(0),
                "CloseTheGaps ran over " + probesWithAGapBeforeTheSweep + " probes that had an " +
                "open gap before it ran, and closed none of them. Every generated creature, " +
                "container, door, crafting station, vehicle, crop and boss would ship with none " +
                "of the companion components and none of the sentences: they would generate " +
                "cleanly and do nothing.");
        }

        [Test]
        public void TheRowsThatAnswerWithWordsAloneAreTheOnesWeKnowAbout()
        {
            List<string> wrong = new List<string>();
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);

            foreach (string row in DimensionQueryCompanions.RowsThatOnlyTheAuthorCanClose())
            {
                int colon = row.IndexOf(':');
                string component = colon < 0 ? row : row.Substring(0, colon);
                string words = colon < 0 ? string.Empty : row.Substring(colon + 1).Trim();

                found.Add(component);

                if (!AnsweredWithWordsBecauseNothingHereCanFillIt.ContainsKey(component))
                {
                    wrong.Add(
                        component + " answers with words and used to fill its gap in. If that was " +
                        "deliberate, write down here why nothing on the object can supply it.");
                }

                if (words.Length == 0)
                {
                    wrong.Add(component + " has neither a fill nor anything to say.");
                }
            }

            foreach (KeyValuePair<string, string> known in AnsweredWithWordsBecauseNothingHereCanFillIt)
            {
                if (!found.Contains(known.Key))
                {
                    wrong.Add(
                        known.Key + " is listed here as answerable only in words and its row now " +
                        "fills the gap in, or the row is gone. Take it off this list.");
                }
            }

            wrong.Sort(StringComparer.Ordinal);

            Assert.That(
                wrong,
                Is.Empty,
                "The rows that can only warn are no longer the ones written down here. A fill " +
                "quietly turned into a warning is a feature that generates cleanly and does " +
                "nothing, and every other check in this file passes over it:\n  " +
                string.Join("\n  ", wrong));
        }

        [Test]
        public void TheUnreadBacklogNamesOnlyThingsTheGeneratorsStillWrite()
        {
            HashSet<string> stillAdded = ComponentsTheGeneratorsAdd();

            List<string> stale = new List<string>();
            foreach (string name in NobodyHasReadTheSystemThatConsumesTheseYet)
            {
                if (!stillAdded.Contains(name))
                {
                    stale.Add(name);
                }
            }

            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                stale,
                Is.Empty,
                "The backlog of components nobody has read against their system names things the " +
                "generators no longer write. A backlog that keeps names nothing adds any more " +
                "reads as bigger work than it is, and hides the ones that still matter. Take these " +
                "out:\n  " + string.Join("\n  ", stale));
        }

        /// <summary>
        /// Every name that has been answered, and which list answered it.
        /// </summary>
        /// <remarks>
        /// ONE TABLE, NOT FOUR SETS CONSULTED IN TURN. Four sets meant a name could sit in two of
        /// them, and 25 of the 26 companion rows did — in the list documented as "needs nothing
        /// beside them", which is the opposite of what a row says. Deleting a row then left the
        /// build green. Building one table makes the second entry a collision that something can
        /// report, which is what <see cref="NoNameSitsInMoreThanOneOfTheAnswerLists"/> does.
        /// </remarks>
        private static Dictionary<string, string> TheAnswerLists()
        {
            return TheAnswerLists(new List<string>());
        }

        private static Dictionary<string, string> TheAnswerLists(List<string> saidTwice)
        {
            Dictionary<string, string> answered =
                new Dictionary<string, string>(StringComparer.Ordinal);

            FoldIn(
                answered,
                saidTwice,
                DimensionQueryCompanions.CoveredAuthoringComponents(),
                "a companion row");
            FoldIn(
                answered,
                saidTwice,
                ReadAgainstTheirSystemAndNeedNothingBeside,
                "ReadAgainstTheirSystemAndNeedNothingBeside");
            FoldIn(
                answered,
                saidTwice,
                ReadAgainstTheirSystemAndTheVerdictIsRecorded,
                "ReadAgainstTheirSystemAndTheVerdictIsRecorded");
            FoldIn(
                answered,
                saidTwice,
                NobodyHasReadTheSystemThatConsumesTheseYet,
                "NobodyHasReadTheSystemThatConsumesTheseYet");

            return answered;
        }

        private static void FoldIn(
            Dictionary<string, string> answered,
            List<string> saidTwice,
            IEnumerable<string> names,
            string which)
        {
            foreach (string name in names)
            {
                string already;
                if (answered.TryGetValue(name, out already))
                {
                    saidTwice.Add(name + " is answered by " + already + " AND by " + which);
                    continue;
                }

                answered[name] = which;
            }
        }

        [Test]
        public void NoNameSitsInMoreThanOneOfTheAnswerLists()
        {
            List<string> saidTwice = new List<string>();
            TheAnswerLists(saidTwice);
            saidTwice.Sort(StringComparer.Ordinal);

            Assert.That(
                saidTwice,
                Is.Empty,
                "A component is answered in two places at once, and the two say different things. " +
                "A companion row says its system needs something else beside the component; " +
                "ReadAgainstTheirSystemAndNeedNothingBeside says it needs nothing. While both were " +
                "consulted, deleting the row left the build green and the feature silently broken " +
                "— which is the failure this whole file exists to stop. Pick one and delete the " +
                "other:\n  " + string.Join("\n  ", saidTwice));
        }

        [Test]
        public void EveryNameApprovedOrBackloggedIsStillWrittenSomewhere()
        {
            HashSet<string> stillAdded = ComponentsTheGeneratorsAdd();

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, string> answer in TheAnswerLists())
            {
                if (answer.Value == "a companion row")
                {
                    // A row may name a component the generators do not write yet; the row is the
                    // knowledge, and EveryRowThatSaysItCanCloseAGapDoesCloseIt exercises it
                    // directly rather than through the generators.
                    continue;
                }

                if (!stillAdded.Contains(answer.Key))
                {
                    stale.Add(answer.Key + "  (" + answer.Value + ")");
                }
            }

            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                stale,
                Is.Empty,
                "A list here names a component nothing in the framework writes any more. On the " +
                "backlog that reads as more work than there is; on either of the two read lists it " +
                "is worse, because it is a standing approval for a surface nobody is writing and " +
                "the next person to add it gets no question at all. Take these out:\n  " +
                string.Join("\n  ", stale));
        }

        [Test]
        public void EveryNameSaidToHaveAVerdictHasOneInTheCensus()
        {
            if (!File.Exists(TheCensus))
            {
                Assert.Ignore(
                    "The census is not on this machine (" + TheCensus + "), so this run did not " +
                    "check that ReadAgainstTheirSystemAndTheVerdictIsRecorded's names have written " +
                    "verdicts. Reported as skipped rather than passed: a green here would say the " +
                    "promise was checked when nothing opened the file. The census is research " +
                    "material outside the Unity project and is not copied in, because a 400 KB " +
                    "markdown under Assets ships inside every mod built with the framework.");
            }

            string census = File.ReadAllText(TheCensus);

            List<string> unrecorded = new List<string>();
            foreach (string name in ReadAgainstTheirSystemAndTheVerdictIsRecorded)
            {
                if (census.IndexOf(name, StringComparison.Ordinal) < 0)
                {
                    unrecorded.Add(name);
                }
            }

            unrecorded.Sort(StringComparer.Ordinal);

            Assert.That(
                unrecorded,
                Is.Empty,
                "These names are in ReadAgainstTheirSystemAndTheVerdictIsRecorded and the census " +
                "does not mention them, so the verdict that list promises was never written. " +
                "Either write it, or move the name back into " +
                "NobodyHasReadTheSystemThatConsumesTheseYet where it belongs:\n  " +
                string.Join("\n  ", unrecorded));
        }

        [Test]
        public void TheScanReadsRealFilesAndFindsRealAdds()
        {
            List<ScannedFile> scanned = ScanTheFramework();

            Assert.That(
                scanned.Count,
                Is.GreaterThan(50),
                "The scan found almost no source files, so every other test in this file passed " +
                "over nothing. GeneratorSources walks Application.dataPath/ExpandNullforge and " +
                "yields nothing at all when that folder is renamed, moved into a package, or the " +
                "tests are run from somewhere else — and nothing used to notice.");

            Assert.That(
                ComponentsTheGeneratorsAdd().Count,
                Is.GreaterThan(200),
                "The scan read files and matched almost nothing in them. The add helpers have been " +
                "renamed out from under AddsAComponent, so the guard is reporting success over " +
                "code it can no longer read.");
        }
    }
}
#endif
