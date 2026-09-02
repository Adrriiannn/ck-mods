#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Every <c>MonoBehaviour</c> and <c>ScriptableObject</c> in the shipped code lives in a file
    /// named after it, or is on the list below with the reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UNITY BINDS A SCRIPT BY FILE NAME. It builds one <c>MonoScript</c> per file, named for the
    /// file, and a class whose file is called something else gets no <c>MonoScript</c> at all. The
    /// class still compiles, still runs when something news it up, and can still be added to a
    /// GameObject in code — but the moment that GameObject is written to a prefab, the component
    /// serialises as <c>m_Script: {fileID: 0}</c> and comes back as a missing script. Nothing
    /// reports it: the generator says it added the component, the prefab exists, and the object
    /// does nothing.
    /// </para>
    /// <para>
    /// THE PROJECT ALREADY STATES THIS RULE, in the note at the top of
    /// <c>Scripts/Authoring/Assets/DimensionItemAsset.cs</c>,
    /// and only for ScriptableObjects. There were no ScriptableObject offenders and fourteen
    /// MonoBehaviour ones, every one of them added to a root that goes through
    /// <c>PrefabUtility.SaveAsPrefabAsset</c> by <c>DimensionCreatureGenerator</c> or
    /// <c>DimensionPlantGenerator</c>. All fourteen were given a file of their own in the
    /// file-splitting stage, so the list below is empty and every offender is a new one.
    /// </para>
    /// <para>
    /// IT COUNTS A CLASS THAT REACHES UNITY THROUGH CORE KEEPER. A view that says
    /// <c>: EntityMonoBehaviour</c> or <c>: Cattle</c> never writes the word
    /// <c>MonoBehaviour</c>, and while this test matched that word literally, six of ours were
    /// outside it. <see cref="VanillaScriptBases"/> names the game's own script bases and
    /// <see cref="EveryUnityBoundClass"/> follows the chain, so a subclass of one of ours counts
    /// too.
    /// </para>
    /// <para>
    /// IT FAILS BOTH WAYS. A class not on the list is a new offender. A name on the list that no
    /// longer offends means the split happened and the entry is stale, which is how a list like
    /// this stops being read.
    /// </para>
    /// </remarks>
    internal sealed class DimensionMonoScriptFileNameTests
    {
        /// <summary>
        /// The classes whose file is named for something else, each with the file it is in.
        /// </summary>
        /// <remarks>
        /// Empty, and meant to stay that way. It is kept rather than deleted because a class that
        /// genuinely cannot have its own file would need a reason written beside it, and there is
        /// nowhere else to write one. Anything put here must say which file it is in and why.
        /// </remarks>
        private static readonly Dictionary<string, string> KnownMismatches =
            new Dictionary<string, string>();

        /// <summary>
        /// A class declaration and the types it derives from, on one line or wrapped onto the next.
        /// </summary>
        private static readonly Regex ClassDeclaration = new Regex(
            @"\bclass\s+(?<name>[A-Za-z_]\w*)\s*(?<generic><[^>{]*>)?\s*:\s*(?<bases>[^{;]+)",
            RegexOptions.Compiled);

        [Test]
        public void EveryUnityBoundScriptIsInAFileNamedForIt()
        {
            List<string> sources = DimensionSandboxGuard.ShippedSourceFiles(Application.dataPath);
            string shipSetProblem =
                DimensionSandboxGuard.ShipSetProblem(Application.dataPath, sources.Count);
            Assert.That(
                shipSetProblem,
                Is.Null,
                "This test read no shipped sources, so it saw no scripts to name. " +
                shipSetProblem);

            List<string> newOffenders = new List<string>();
            HashSet<string> stillOffending = new HashSet<string>();
            HashSet<string> unityBound = EveryUnityBoundClass(sources);

            for (int i = 0; i < sources.Count; i++)
            {
                string file = sources[i];
                string expected = Path.GetFileNameWithoutExtension(file);
                string text = File.ReadAllText(file);

                foreach (Match match in ClassDeclaration.Matches(text))
                {
                    if (!unityBound.Contains(match.Groups["name"].Value))
                    {
                        continue;
                    }

                    string name = match.Groups["name"].Value;
                    if (name == expected)
                    {
                        continue;
                    }

                    string where;
                    if (KnownMismatches.TryGetValue(name, out where))
                    {
                        stillOffending.Add(name);
                        continue;
                    }

                    newOffenders.Add(
                        name + " is declared in " + Path.GetFileName(file) +
                        ", so Unity builds no MonoScript for it. Put it in " + name + ".cs.");
                }
            }

            newOffenders.Sort(System.StringComparer.Ordinal);

            Assert.That(
                newOffenders,
                Is.Empty,
                "A MonoBehaviour or ScriptableObject is in a file named for something else. Unity " +
                "binds by file name, so any prefab or asset this is written to serialises the " +
                "reference as fileID 0 and comes back as a missing script — the generator reports " +
                "success and the object does nothing:\n  " + string.Join("\n  ", newOffenders));

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, string> known in KnownMismatches)
            {
                if (!stillOffending.Contains(known.Key))
                {
                    stale.Add(
                        known.Key + " is recorded here as living in " + known.Value +
                        " and no longer does. Take it off this list.");
                }
            }

            stale.Sort(System.StringComparer.Ordinal);

            Assert.That(
                stale,
                Is.Empty,
                "This list names a mismatch that has been fixed. A list that keeps entries nothing " +
                "matches any more reads as more outstanding work than there is, and the next " +
                "reader stops trusting it:\n  " + string.Join("\n  ", stale));
        }

        /// <summary>
        /// Core Keeper's own script bases, each one a <c>MonoBehaviour</c> the game reaches through
        /// a <c>MonoScript</c> exactly as it reaches ours.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WITHOUT THIS THE GUARD COVERED FORTY-NINE OF FIFTY-FIVE. It matched the literal words
        /// <c>MonoBehaviour</c> and <c>ScriptableObject</c> in a base list, so six of our classes —
        /// the ones that reach Unity through a Core Keeper base instead of a Unity one — were
        /// invisible to it. All six happened to be in a file named for them, so the invariant held
        /// at fifty-five while the check stopped at forty-nine, which is the worst shape a guard
        /// can be in: correct today, silent tomorrow.
        /// </para>
        /// <para>
        /// Each name here was read out of the decompiled game rather than assumed. Every one of
        /// them is <c>: EntityMonoBehaviour</c>, and <c>EntityMonoBehaviour</c> is
        /// <c>: PoolableSimple</c>, which is <c>: MonoBehaviour</c>. Adding a name that is not
        /// really a script would make the guard demand a file for something Unity never binds; the
        /// cost of that is a false failure, which is the safe direction, and it is still worth
        /// checking before adding one.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> VanillaScriptBases =
            new HashSet<string>(System.StringComparer.Ordinal)
            {
                "EntityMonoBehaviour", "PoolableSimple",
                "Cattle", "CraftingBuilding", "NPC", "VendingMachine", "WorldLabel",
            };

        /// <summary>
        /// Every shipped class Unity resolves through a <c>MonoScript</c>, base chains included.
        /// </summary>
        /// <remarks>
        /// Resolved by repeated passes rather than by reading the first base only: our own views
        /// derive from each other — <c>DimensionCreatureView</c>, <c>DimensionPlantView</c> and
        /// <c>DimensionPortal</c> are all bases of something else here — so a single pass would
        /// answer for a subclass before it had answered for the class it is under. The loop ends
        /// when a pass adds nothing, which is at most as many passes as the chain is deep.
        /// </remarks>
        private static HashSet<string> EveryUnityBoundClass(List<string> sources)
        {
            Dictionary<string, List<string>> basesOf =
                new Dictionary<string, List<string>>(System.StringComparer.Ordinal);

            for (int i = 0; i < sources.Count; i++)
            {
                string text = File.ReadAllText(sources[i]);
                foreach (Match match in ClassDeclaration.Matches(text))
                {
                    string name = match.Groups["name"].Value;
                    if (!basesOf.ContainsKey(name))
                    {
                        basesOf.Add(name, new List<string>());
                    }

                    // The first entry in a base list is the base class, but an interface can be
                    // written first only in invalid C#, so every entry is kept rather than the
                    // first. A base named SomethingMonoBehaviour would be a false positive; there
                    // is none, and comparing whole names keeps it that way.
                    string[] parts = match.Groups["bases"].Value.Split(',');
                    for (int b = 0; b < parts.Length; b++)
                    {
                        string one = parts[b].Trim();
                        int dot = one.LastIndexOf('.');
                        if (dot >= 0)
                        {
                            one = one.Substring(dot + 1);
                        }

                        int generic = one.IndexOf('<');
                        if (generic >= 0)
                        {
                            one = one.Substring(0, generic);
                        }

                        basesOf[name].Add(one.Trim());
                    }
                }
            }

            HashSet<string> bound = new HashSet<string>(System.StringComparer.Ordinal);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (KeyValuePair<string, List<string>> declared in basesOf)
                {
                    if (bound.Contains(declared.Key))
                    {
                        continue;
                    }

                    for (int b = 0; b < declared.Value.Count; b++)
                    {
                        string one = declared.Value[b];
                        if (one != "MonoBehaviour" && one != "ScriptableObject" &&
                            !VanillaScriptBases.Contains(one) && !bound.Contains(one))
                        {
                            continue;
                        }

                        bound.Add(declared.Key);
                        grew = true;
                        break;
                    }
                }
            }

            return bound;
        }
    }
}
#endif
