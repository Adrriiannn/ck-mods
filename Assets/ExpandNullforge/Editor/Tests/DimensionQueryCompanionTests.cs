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
    /// Stops the framework writing one component of a query and none of the others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THIS GUARDS. A Core Keeper system only touches an entity carrying EVERY
    /// component its query names. The framework writes the one component that obviously belongs to
    /// a feature, and the others — which come from unrelated authoring components — go unwritten.
    /// The entity never matches, the system never runs, and nothing reports anything: the component
    /// we wrote is present, its converter ran, and the number in it is right. It was found by hand
    /// once, on the summoning circle, and a census then found a hundred more.
    /// </para>
    /// <para>
    /// WHAT THIS TEST DOES NOT DO, said plainly. It does not read Core Keeper's queries, and it
    /// does not read Core Keeper's own prefabs. Both are readable — the decompiled systems at
    /// <c>E:\ck mods\ck-db</c> are plain C# with the <c>WithAll</c> chains intact, and the game's
    /// prefabs are on disk — but they are outside this repository and outside anything this
    /// assembly references, so reading them is a person's job done against
    /// <c>E:\ck mods\ck-research\query-match-census.md</c> rather than a thing the build can check.
    /// An earlier version of this remark said the queries were unrecoverable. That was wrong, and
    /// it made a backlog of two hundred unread components look like a tooling limit instead of
    /// work nobody had done yet.
    /// </para>
    /// <para>
    /// WHAT IT DOES INSTEAD. Three things, and they cover the reintroduction path rather than the
    /// discovery path:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// It reads every framework source file — whole, not line by line, so a call wrapped across two
    /// lines is still seen — and fails when a Core Keeper surface it puts on something is answered
    /// by none of the four lists. "Core Keeper surface" is decided by
    /// <see cref="NotACoreKeeperSurface"/>, a written-down list of the things that are NOT one,
    /// rather than by a suffix: an earlier version dropped every name not ending in "Authoring"
    /// before consulting any list, which quietly excused <c>InteractableObject</c> and seven
    /// runtime components added straight to entities.
    /// </description></item>
    /// <item><description>
    /// It builds an object carrying each covered component, runs the PRODUCTION sweep over it, and
    /// then READS the object without touching it — and fails when a companion is still missing and
    /// either the row claimed to supply it or nothing was said. It also counts what the sweep
    /// closed and fails at zero, so an empty sweep cannot pass. Until this pass the reading step
    /// performed every fill itself, and emptying the sweep left the test green.
    /// </description></item>
    /// <item><description>
    /// It pins the two behaviours the sweep depends on: that seeing nearby things merges rather
    /// than overwrites, and that a thing which moves is allowed to turn.
    /// </description></item>
    /// </list>
    /// <para>
    /// SO THE GAP IT LEAVES IS DISCOVERY. It used to be two hundred components wide. Four more
    /// censuses on 2026-08-29 read 186 of those two hundred against the system that consumes
    /// them, and those names moved into
    /// <see cref="ReadAgainstTheirSystemAndTheVerdictIsRecorded"/>.
    /// <see cref="NobodyHasReadTheSystemThatConsumesTheseYet"/> is what NOBODY HAS OPENED AT ALL.
    /// It is not the whole of what is unresolved: the recorded-verdict list also holds names whose
    /// verdict is UNCERTAIN — the census marks <c>WallBossAuthoring</c>,
    /// <c>DetectCollisionAuthoring</c>, <c>CanClaimBedAuthoring</c>, <c>CoinAmountAuthoring</c> and
    /// <c>AffixAuthoring</c> that way — so a name there can mean read and not concluded. Read that
    /// list's own remark, and the census, before assuming anything.
    /// </para>
    /// <para>
    /// It also does not check that the component we add is the one the system wants, only that
    /// somebody looked, and — for the covered rows — that what the sweep promises to supply is
    /// actually there afterwards. Whether a recorded verdict is still true after the game updates
    /// is a person's job, done against `E:\ck mods\ck-research\query-match-census.md`, which
    /// <see cref="EveryNameSaidToHaveAVerdictHasOneInTheCensus"/> now at least opens.
    /// </para>
    /// <para>
    /// WHAT IT STILL CANNOT SEE, written down so nobody has to rediscover it. (1) A component that
    /// arrives on a LOADED prefab: fifteen generators regenerate in place through
    /// <c>PrefabUtility.LoadPrefabContents</c> and nothing strips what an older pass wrote, so a
    /// component the framework wrote in one release and dropped in the next is still on every
    /// user's prefab, still in the query, and the staleness tests here have since deleted the
    /// record that anybody read it. Closing that means reading generated prefabs, which are outside
    /// this assembly. (2) Answers are keyed by component NAME, so moving an already-answered name
    /// into a different generator passes. (3) The sweep check is one boolean per FILE: a file that
    /// sweeps object A and writes surfaces on object B passes, and a dead private method named like
    /// the sweep would satisfy it. Blanking the string literals closed the version of that hole
    /// that a warning sentence could walk through; the dead-method version is still open.
    /// </para>
    /// </remarks>
    internal sealed partial class DimensionQueryCompanionTests
    {

        /// <summary>
        /// Matches something that puts a component on a generated object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY HELPER IN THIS TREE THAT ADDS ONE, found by reading the generators rather than
        /// guessing. Six names are declared in the tree, in twenty-one places:
        /// <c>EnsureComponent&lt;T&gt;</c> is declared fourteen times — once in
        /// <c>DimensionGeneratedPrefabUtility</c> and once privately in each of thirteen other
        /// files — <c>ApplyComponent&lt;T&gt;</c> twice (<c>DimensionItemGenerator</c>),
        /// <c>Ensure&lt;T&gt;</c> twice (<c>DimensionQueryCompanions</c>,
        /// <c>DimensionTilesetBlockAuthoring</c>), and <c>Toggle&lt;T&gt;</c>,
        /// <c>Fill&lt;T&gt;</c> and <c>Set&lt;T&gt;</c> once each. Beside those the alternation
        /// carries Unity's own <c>AddComponent&lt;T&gt;</c>, the ECS
        /// <c>AddComponentData&lt;T&gt;</c> — which has no call in the tree yet and is one ECS add
        /// away, since the runtime loader already calls <c>AddComponent&lt;Prefab&gt;</c> on an
        /// entity — the converters' <c>EnsureHasComponent&lt;T&gt;</c>, and
        /// <c>GetOrAddComponent&lt;T&gt;</c> — that last
        /// one has no declaration and no call in the tree today and is carried because it is the
        /// usual name for the same helper.
        /// </para>
        /// <para>
        /// FOURTEEN PRIVATE COPIES OF ONE HELPER is why the shape of this scan matters as much as
        /// its contents: there is no single choke point to instrument, so the guard is a text scan,
        /// and a text scan has to read whole files rather than lines. See
        /// <see cref="ScannedFile"/>.
        /// </para>
        /// <para>
        /// TWO OF THOSE WERE MISSING and both were live: <c>ApplyComponent</c> puts the cooldown,
        /// the damageable and the destructible answers on every item, and <c>Set</c> puts the tile
        /// answers on every custom block. Neither matched, so twenty-two components the generators
        /// really do write were invisible to this test and were never even in the backlog. A
        /// helper added later and left out of this list is the same hole, which is why
        /// <see cref="EveryGenericCallOnAnAuthoringTypeIsAKnownForm"/> now fails on a form nobody
        /// has classified rather than passing over it.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// <para>
        /// THE TYPE ARGUMENT IS READ AS A LIST, and its characters allow every legal way of
        /// writing a name. Five forms slipped past the pattern this replaces, each verified by
        /// construction against the real tree: <c>EnsureComponent&lt;PetAuthoring, Tag&gt;</c>
        /// (two arguments matched neither scan), <c>global::Pug.PetAuthoring</c> (the character
        /// class could not cross <c>::</c>), <c>@PetAuthoring</c> (nor the verbatim <c>@</c>),
        /// <c>AddComponentData&lt;T&gt;</c> (the alternation demanded <c>AddComponent</c> exactly,
        /// while the runtime path already calls <c>AddComponent&lt;Prefab&gt;</c> on an entity),
        /// and an aliased type whose alias does not end in Authoring or CD.
        /// </para>
        /// <para>
        /// <c>EveryTypeArgumentIn</c> splits the list; <c>ScannedFile.Resolve</c> strips
        /// <c>global::</c>, the <c>@</c> and the namespace, and follows a using-alias, and it is
        /// called BEFORE anything decides whether the name is a surface.
        /// </para>
        /// </remarks>
        private static readonly Regex AddsAComponent = new Regex(
            @"\b(?:EnsureComponent|EnsureHasComponent|AddComponentData|AddComponent|" +
            @"ApplyComponent|GetOrAddComponent|Ensure|Toggle|Fill|Set)\s*<\s*" +
            @"([\w\.@:\s,]+?)\s*>",
            RegexOptions.Compiled);

        /// <summary>
        /// Copying a component from one object to another, which names no type any scan can read.
        /// </summary>
        /// <remarks>
        /// <c>ComponentUtility.CopyComponent</c> takes a component instance and
        /// <c>PasteComponentAsNew</c> names nothing at all, so a real Core Keeper component can
        /// land on a generated object with every scan in this file silent. There are none in the
        /// tree today; this refuses the form outright rather than waiting for the first one.
        /// </remarks>
        private static readonly Regex CopiesAComponent = new Regex(
            @"\b(?:CopyComponent|PasteComponentAsNew|PasteComponentValues)\s*\(",
            RegexOptions.Compiled);

        /// <summary>
        /// Matches any generic call whose type argument is a Core Keeper surface.
        /// </summary>
        /// <remarks>
        /// <c>*Authoring</c>, <c>GhostAuthoringComponent</c>, <c>InteractableObject</c> and the
        /// runtime <c>*CD</c> components. It used to be the authoring suffix alone, which left a
        /// new helper used only on a runtime component — <c>Attach&lt;CavelingCD&gt;(entity)</c> —
        /// matching neither this nor the add scan, so it would have shipped with nobody answering
        /// for it. Adding a CD by hand skips the converter that would have added its siblings, so
        /// that road is more exposed to this bug than the authoring one.
        /// </remarks>
        private static readonly Regex AnyGenericCall = new Regex(
            @"\b([A-Za-z_]\w*)\s*<\s*([\w\.@:\s,]+?)\s*>",
            RegexOptions.Compiled);

        /// <summary>
        /// Non-generic <c>AddComponent</c>, which names its component in a way no scan can follow.
        /// </summary>
        private static readonly Regex AddsAComponentByType = new Regex(
            @"\bAddComponent\s*\(\s*(?!\s*\))",
            RegexOptions.Compiled);

        /// <summary>
        /// The generic calls that take an authoring type and do NOT put one on an object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Anything not in this list and not in <see cref="AddsAComponent"/> is a form nobody has
        /// classified, and the test says so instead of quietly ignoring it — an add helper that
        /// goes unrecognised is exactly how a new authoring surface ships without anybody having
        /// read its query.
        /// </para>
        /// <para>
        /// WHICH MAKES THIS LIST THE CHEAPEST WAY TO TURN THE WHOLE GUARD OFF. One word here and a
        /// brand-new add helper is invisible to all three scans at once: its components are never
        /// questioned, its caller is never unclassified, and the file it lives in stops counting as
        /// one that writes a surface, so it needs no sweep either. Its own remark used to say the
        /// list "came from reading every such call in the tree", and nine of its thirty names had
        /// no such call anywhere —
        /// <c>GetComponentsInChildren</c>, <c>GetComponentInParent</c>, <c>TryGetComponent</c>,
        /// <c>SingleAuthoringComponentConverter</c>, <c>HashSet</c>, <c>IEnumerable</c>,
        /// <c>Dictionary</c>, <c>Func</c> and <c>Action</c> never take a Core Keeper surface in
        /// this tree. They are gone, and
        /// <see cref="EveryNameExcusedFromAddingIsAFormTheTreeReallyUses"/> now keeps the claim
        /// true: a name that stops being used has to come out, so nobody can leave a spare excuse
        /// lying about for a helper that arrives later.
        /// </para>
        /// <para>
        /// The add alternation is deliberately NOT held to the same rule.
        /// <c>AddComponentData</c> and <c>GetOrAddComponent</c> have no call in the tree and are
        /// carried on purpose: an unused name there can only make the guard ask about more, and an
        /// unused name here makes it ask about less.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> GenericCallsThatDoNotAddAComponent =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "GetComponent", "GetComponents", "GetComponentInChildren",
                "RemoveComponentIfPresent", "TryRemoveComponent", "HasComponent", "List",

                // The ECS side: reading, querying and removing a runtime component. None of these
                // puts one on anything.
                "GetComponentData", "TryGetComponentData", "HasComponentData", "IsComponentEnabled",
                "GetSingleton", "RequireForUpdate", "ReadOnly", "ReadWrite", "Exclude", "RefRO",
                "RemoveComponent", "NativeArray", "ToComponentDataArray", "BlobBuilderArray",
            };

        [Test]
        public void EveryFileThatWritesASurfaceEitherSweepsOrSaysWhyNot()
        {
            List<string> unexplained = new List<string>();
            List<string> stale = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> firstWithThatName =
                new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, bool> bySubject = new Dictionary<string, bool>(StringComparer.Ordinal);
            Dictionary<string, bool> sweptBySubject =
                new Dictionary<string, bool>(StringComparer.Ordinal);
            Dictionary<string, List<string>> excusesBySubject =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<string> subjects = new List<string>();

            foreach (ScannedFile file in ScanTheFramework())
            {
                // THE EXCUSES ARE KEYED BY FILE NAME, so two files with the same name share one.
                // A second DimensionObjectSpine.cs in a subfolder that wrote surfaces and never
                // swept would inherit the first one's excuse AND be marked as having been seen, so
                // the staleness half stayed quiet too. There are none today; this is what keeps it
                // that way, because keying on the whole path instead would mean writing seventeen
                // paths down and moving a file would then be the silent break.
                string alreadyHere;
                if (firstWithThatName.TryGetValue(file.Name, out alreadyHere))
                {
                    stale.Add(
                        file.Name + " is the name of two files in the framework (" + alreadyHere +
                        " and " + file.Where + "), and the excuses in " +
                        "WritesASurfaceAndDoesNotSweep are keyed by name, so one would silently " +
                        "answer for the other");
                }
                else
                {
                    firstWithThatName.Add(file.Name, file.Where);
                }

                bool writesASurface = false;
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                    foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                    {
                        string component = file.Resolve(argument);
                        if (!component.StartsWith("Dimension", StringComparison.Ordinal) &&
                            !NotACoreKeeperSurface.Contains(component))
                        {
                            writesASurface = true;
                            break;
                        }
                    }

                    if (writesASurface)
                    {
                        break;
                    }
                }

                // READ WITH THE STRING LITERALS BLANKED TOO. Comments were already blanked and
                // strings were not, so a warning that mentioned the sweep by name — or any other
                // sentence with the word in it — satisfied this check without a call existing.
                //
                // AND WITH THE PREPROCESSOR BLOCKS BLANKED. A call inside "#if NEVER_DEFINED" is
                // text the compiler never sees, and the check knew nothing about that, so a sweep
                // that had been switched off at the top of the file still read as a sweep.
                bool sweeps = RunsTheSweep.IsMatch(
                    WithConditionalBlocksBlanked(file.CodeWithoutText));

                // ANSWERED PER TYPE, NOT PER FILE. A type this size is written across several
                // files, and the question — does the thing that writes these components run the
                // sweep — is about the type. Asked per file, splitting one in two turns one honest
                // answer into one half that writes and never sweeps and another that sweeps and
                // writes nothing, so the split alone makes the test fail and the fix is to write
                // the same excuse out once per part.
                string subject = SubjectOf(file.Name);
                if (!writesASurface && !sweeps)
                {
                    continue;
                }

                if (!bySubject.ContainsKey(subject))
                {
                    bySubject.Add(subject, false);
                    sweptBySubject.Add(subject, false);
                    excusesBySubject.Add(subject, new List<string>());
                    subjects.Add(subject);
                }

                bySubject[subject] = bySubject[subject] || writesASurface;
                sweptBySubject[subject] = sweptBySubject[subject] || sweeps;

                // AN EXCUSE MAY NAME ONE PART OR THE WHOLE TYPE. Most of the list names a file that
                // is the whole of its type, and one — the runtime loader — names a single partial
                // of a type written across seventy files. Both forms are honoured, so writing an
                // excuse against one part does not quietly excuse the other sixty-nine.
                if (WritesASurfaceAndDoesNotSweep.ContainsKey(file.Name))
                {
                    excusesBySubject[subject].Add(file.Name);
                }

                if (file.Name != subject && WritesASurfaceAndDoesNotSweep.ContainsKey(subject))
                {
                    excusesBySubject[subject].Add(subject);
                }
            }

            for (int i = 0; i < subjects.Count; i++)
            {
                string subject = subjects[i];
                if (!bySubject[subject])
                {
                    continue;
                }

                List<string> excuses = excusesBySubject[subject];
                bool excused = excuses.Count > 0;

                if (!sweptBySubject[subject] && !excused)
                {
                    unexplained.Add(subject);
                }

                if (sweptBySubject[subject] && excused)
                {
                    stale.Add(subject + " is listed as not sweeping and does sweep");
                }

                for (int e = 0; e < excuses.Count; e++)
                {
                    seen.Add(excuses[e]);
                }
            }

            foreach (KeyValuePair<string, string> excused in WritesASurfaceAndDoesNotSweep)
            {
                if (!seen.Contains(excused.Key))
                {
                    stale.Add(excused.Key + " is listed and no longer writes a Core Keeper surface");
                }
            }

            unexplained.Sort(StringComparer.Ordinal);
            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                unexplained,
                Is.Empty,
                "A file puts Core Keeper components on something and nothing ever runs the sweep " +
                "over what it built, so every companion row is inert for everything it makes. " +
                "Either call CloseTheGaps once the object is finished, or add the file to " +
                "WritesASurfaceAndDoesNotSweep with the reason:\n  " +
                string.Join("\n  ", unexplained));

            Assert.That(
                stale,
                Is.Empty,
                "WritesASurfaceAndDoesNotSweep no longer describes the tree:\n  " +
                string.Join("\n  ", stale));
        }

        /// <summary>The sweep itself, and the three helpers that end in it.</summary>
        private static readonly Regex RunsTheSweep = new Regex(
            @"\b(?:CloseTheGaps|FinishAWorldObject|FinishACreature|FinishACritter)\s*\(",
            RegexOptions.Compiled);

        [Test]
        public void EveryRowNamesSomethingRealAsWhatIsAlsoNeeded()
        {
            HashSet<string> valueGaps = new HashSet<string>(
                DimensionQueryCompanions.GapsThatAreAValueRatherThanAComponent,
                StringComparer.Ordinal);

            List<string> unreal = new List<string>();
            foreach (string needed in DimensionQueryCompanions.EveryThingARowSaysIsAlsoNeeded())
            {
                if (valueGaps.Contains(needed) || FindAuthoringType(needed) != null)
                {
                    continue;
                }

                unreal.Add(needed);
            }

            unreal.Sort(StringComparer.Ordinal);

            Assert.That(
                unreal,
                Is.Empty,
                "A companion row says the system also needs something that is neither a component " +
                "this build has nor one of the named value gaps. A misspelt component name reads " +
                "as a gap nothing can ever close, and gets reported at every generate forever:\n  " +
                string.Join("\n  ", unreal));
        }

        /// <summary>
        /// Fails on a way of adding a component that this test does not recognise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE HOLE THIS CLOSES. The scan above knows a fixed list of add helpers by name. Two live
        /// ones were missing from it — <c>ApplyComponent&lt;T&gt;</c> in the item generator and
        /// <c>Set&lt;T&gt;</c> in the block generator — so twenty-two components the generators
        /// really write were invisible, were never in any of the four lists, and the build stayed
        /// green. A helper the scan does not know about is not a small gap: it is a whole generator
        /// whose authoring surfaces nobody has to answer for.
        /// </para>
        /// <para>
        /// So every generic call anywhere in the framework whose type argument is a Core Keeper
        /// authoring component must be either an add helper this test knows, or a call listed as
        /// not adding one. Anything else fails here, by name and by line, and the person who wrote
        /// it says which it is. That is the difference between a guard that can be walked past and
        /// one that cannot.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryGenericCallOnAnAuthoringTypeIsAKnownForm()
        {
            List<string> unclassified = new List<string>();
            HashSet<string> alreadySaid = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AnyGenericCall.Matches(file.Code))
                {
                    string caller = match.Groups[1].Value;

                    foreach (string argument in EveryTypeArgumentIn(match.Groups[2].Value))
                    {
                        // RESOLVED FIRST, THEN JUDGED. The pattern this replaces asked whether the
                        // RAW text ended in Authoring or CD, so an alias that did not — the one
                        // form the alias resolver was widened for — never reached the resolver at
                        // all.
                        string component = file.Resolve(argument);

                        if (component.StartsWith("Dimension", StringComparison.Ordinal) ||
                            !IsACoreKeeperSurfaceName(component))
                        {
                            continue;
                        }

                        if (GenericCallsThatDoNotAddAComponent.Contains(caller) ||
                            AddsAComponent.IsMatch(caller + "<X>"))
                        {
                            continue;
                        }

                        if (alreadySaid.Add(caller))
                        {
                            unclassified.Add(
                                caller + "<" + component + ">  (" + file.Name + ":" +
                                file.LineAt(match.Index) + ")");
                        }
                    }
                }

                // AddComponent with a Type instead of a type argument names its component in a
                // way no scan can follow, so it is refused outright rather than missed.
                foreach (Match match in AddsAComponentByType.Matches(file.Code))
                {
                    string where = file.Name + ":" + file.LineAt(match.Index);
                    if (alreadySaid.Add(where))
                    {
                        unclassified.Add(
                            "AddComponent(Type) at " + where +
                            ", which names no component this scan can read");
                    }
                }
            }

            unclassified.Sort(StringComparer.Ordinal);

            Assert.That(
                unclassified,
                Is.Empty,
                "Something in the framework passes a Core Keeper authoring component to a generic " +
                "call this test has never been told about. If it puts the component ON an object, " +
                "add its name to AddsAComponent — otherwise every component it writes is invisible " +
                "to the check above and ships without anybody reading the system that consumes it. " +
                "If it only reads or removes, add its name to " +
                "GenericCallsThatDoNotAddAComponent. If it adds a component named by a Type rather " +
                "than a type argument, use the generic form instead, because nothing can read the " +
                "other one:\n  " + string.Join("\n  ", unclassified));
        }

        /// <summary>Every Core Keeper surface the framework still writes.</summary>
        /// <remarks>
        /// <para>
        /// "WRITES" IS A TEXT MATCH AND IT CANNOT TELL AN ADD FROM A REMOVAL. <c>Toggle&lt;T&gt;</c>
        /// is in the add alternation whichever way its flag is set, so four components the
        /// framework only ever STRIPS — <c>AchievementTrackerAuthoring</c>,
        /// <c>ClientBiomeSamplesAuthoring</c>, <c>ClientSubMapAuthoring</c> and
        /// <c>WaterSpreaderAuthoring</c>, all four refused on purpose because they are world
        /// singletons or delete the object — are counted as components the generators write, and
        /// they sit in an approval list on that footing. The cost is one-sided: a surface demoted
        /// to a removal keeps its answer alive and the staleness check will not notice, which
        /// leaves a stale approval rather than an unanswered component. Reading the flag would
        /// mean parsing an argument, and a scan that reads arguments wrongly loses adds, which is
        /// the expensive direction. Written down rather than guessed at.
        /// </para>
        /// <para>
        /// It also cannot see a component that arrives on a prefab rather than through a call:
        /// <c>Object.Instantiate</c> of a template, and the fifteen generators that regenerate in
        /// place through <c>LoadPrefabContents</c>, both bring whatever the previous prefab had.
        /// </para>
        /// </remarks>
        private static HashSet<string> ComponentsTheGeneratorsAdd()
        {
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                    foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                    {
                        added.Add(file.Resolve(argument));
                    }
                }
            }

            return added;
        }
    }
}
#endif
