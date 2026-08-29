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
    /// THE PROJECT ALREADY STATES THIS RULE, at <c>Scripts/Authoring/DimensionItemAsset.cs:7-8</c>,
    /// and only for ScriptableObjects. There are no ScriptableObject offenders and there are
    /// fourteen MonoBehaviour ones, every one of them added to a root that goes through
    /// <c>PrefabUtility.SaveAsPrefabAsset</c> by <c>DimensionCreatureGenerator</c> or
    /// <c>DimensionPlantGenerator</c>. They are recorded here rather than fixed here: the fix is one
    /// file per class, which is the file-splitting stage, and this test is what stops a fifteenth
    /// arriving in the meantime.
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
        /// Measured, not copied from a report. Take an entry out in the same commit that gives the
        /// class its own file.
        /// </remarks>
        private static readonly Dictionary<string, string> KnownMismatches =
            new Dictionary<string, string>
            {
                { "DimensionBossMarkerAuthoring", "Scripts/Creatures/DimensionBossMarker.cs" },
                { "DimensionShopStockAuthoring", "Scripts/Creatures/DimensionShopAndHatching.cs" },
                { "DimensionHatchTargetAuthoring", "Scripts/Creatures/DimensionShopAndHatching.cs" },
                { "DimensionSummoningItemAuthoring", "Scripts/Creatures/DimensionSummoningItem.cs" },
                { "DimensionSummonAreaByNameAuthoring", "Scripts/Creatures/DimensionSummoningItem.cs" },
                { "DimensionHoldsFireAuthoring", "Scripts/Creatures/DimensionTemperament.cs" },
                { "DimensionBlastFireAuthoring", "Scripts/Explosives/DimensionBlastFire.cs" },
                { "DimensionCropTierSeedAuthoring", "Scripts/Plants/DimensionCropTierAuthoring.cs" },
                { "DimensionCropTierPlantAuthoring", "Scripts/Plants/DimensionCropTierAuthoring.cs" },
                { "DimensionSeedAuthoring", "Scripts/Plants/DimensionPlantAuthoring.cs" },
                { "DimensionPlantProduceAuthoring", "Scripts/Plants/DimensionPlantAuthoring.cs" },
                { "DimensionPlantDropsAuthoring", "Scripts/Plants/DimensionPlantAuthoring.cs" },
                { "DimensionPortalOfferingAuthoring", "Scripts/Portals/DimensionPortalOffering.cs" },
                { "DimensionCoordinatePresentationHost", "Scripts/UI/DimensionCoordinatePresentation.cs" },
            };

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

            for (int i = 0; i < sources.Count; i++)
            {
                string file = sources[i];
                string expected = Path.GetFileNameWithoutExtension(file);
                string text = File.ReadAllText(file);

                foreach (Match match in ClassDeclaration.Matches(text))
                {
                    string bases = match.Groups["bases"].Value;
                    if (!DerivesFromAUnityScript(bases))
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
        /// Whether a base-type list names one of the two things Unity resolves through a
        /// <c>MonoScript</c>.
        /// </summary>
        /// <remarks>
        /// The first entry in a base list is the base class, but an interface can be written first
        /// only in invalid C#, so every entry is checked rather than the first. A base named
        /// <c>SomethingMonoBehaviour</c> would be a false positive; there is none, and the word
        /// boundary keeps it that way.
        /// </remarks>
        private static bool DerivesFromAUnityScript(string bases)
        {
            string[] parts = bases.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string one = parts[i].Trim();
                int dot = one.LastIndexOf('.');
                if (dot >= 0)
                {
                    one = one.Substring(dot + 1);
                }

                if (one == "MonoBehaviour" || one == "ScriptableObject")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
