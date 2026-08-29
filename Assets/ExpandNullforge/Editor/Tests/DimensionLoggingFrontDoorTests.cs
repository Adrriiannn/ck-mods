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
    /// Runtime code prints through the one front door, and the door has a control on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY IT MATTERS THAT THERE IS ONLY ONE. A raw <c>Debug.Log</c> in shipped code cannot be
    /// turned off, cannot say which world it came from, and cannot be deduplicated per world — so on
    /// a host it appears twice with no way to tell a duplicate from a disagreement, and on a
    /// streaming path it appears once per submap for the whole session. The live player log from the
    /// framework's first real session carried six lines from one subsystem and nothing at all from
    /// the other thirty; that is the state this guards against returning to.
    /// </para>
    /// <para>
    /// THE EDITOR IS NOT COVERED AND MUST NOT BE. A generator reporting to the console is the
    /// console doing its job, and the editor assembly never reaches a player.
    /// </para>
    /// </remarks>
    internal sealed class DimensionLoggingFrontDoorTests
    {
        /// <summary>
        /// The two shipped files allowed to call <c>Debug</c> directly, and why.
        /// </summary>
        private static readonly Dictionary<string, string> Allowed =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "DimensionLog.cs",
                    "it is the front door; the Debug call is the output primitive"
                },
                {
                    "DimensionTilesetAtlasCapture.cs",
                    "the whole file is one dump of the game's tileset bank, it runs only when the " +
                    "baked atlas is missing, and half a dump is no use to the person who needs it"
                },
            };

        private static readonly Regex RawDebugCall =
            new Regex(@"(?<![\w.])(?:UnityEngine\.)?Debug\.Log(Warning|Error|Format)?\(",
                RegexOptions.Compiled);

        [Test]
        public void NothingShippedPrintsExceptThroughTheFrontDoor()
        {
            List<string> sources = DimensionSandboxGuard.ShippedSourceFiles(Application.dataPath);
            string shipSetProblem =
                DimensionSandboxGuard.ShipSetProblem(Application.dataPath, sources.Count);
            Assert.That(
                shipSetProblem,
                Is.Null,
                "This test read no shipped sources, so it saw no logging at all. " + shipSetProblem);

            List<string> offenders = new List<string>();
            HashSet<string> allowedSeen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < sources.Count; i++)
            {
                string name = Path.GetFileName(sources[i]);
                string text = File.ReadAllText(sources[i]);

                foreach (Match match in RawDebugCall.Matches(text))
                {
                    // A mention inside a doc comment is an explanation, not a call. The cheap test
                    // is whether the line it sits on starts as a comment.
                    if (LineIsAComment(text, match.Index))
                    {
                        continue;
                    }

                    if (Allowed.ContainsKey(name))
                    {
                        allowedSeen.Add(name);
                        continue;
                    }

                    offenders.Add(name + ":" + LineNumber(text, match.Index));
                }
            }

            offenders.Sort(StringComparer.Ordinal);

            Assert.That(
                offenders,
                Is.Empty,
                "Shipped code calls UnityEngine.Debug directly. That line cannot be switched off, " +
                "does not say which world it came from, and repeats on a host with no way to tell " +
                "the duplicate from a disagreement. Route it through DimensionLog with a channel " +
                "and the World it is about:\n  " + string.Join("\n  ", offenders));

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, string> allowed in Allowed)
            {
                if (!allowedSeen.Contains(allowed.Key))
                {
                    stale.Add(
                        allowed.Key + " is on this test's allowlist and no longer calls Debug " +
                        "directly. Take it off, so the list keeps meaning something.");
                }
            }

            Assert.That(stale, Is.Empty, string.Join("\n  ", stale));
        }

        /// <summary>
        /// The switch on the front door is real: it answers differently depending on what was asked
        /// for.
        /// </summary>
        /// <remarks>
        /// The flag this replaces was a public field nothing assigned, so its thirty-three calls
        /// could not produce output under any circumstances and no test noticed. This asserts the
        /// property answers the config, in both directions.
        /// </remarks>
        [Test]
        public void TheDetailSwitchAnswersWhatWasAskedFor()
        {
            ExpandNullforge.Foundation.DimensionLogConfig.Reset();

            Assert.That(
                ExpandNullforge.Foundation.DimensionLogConfig.TraceOn("portal"),
                Is.False,
                "Nothing was switched on, so nothing should be traced.");
            Assert.That(
                ExpandNullforge.Foundation.DimensionFrameworkLog.VerboseRuntimeLogging,
                Is.False,
                "The old verbose flag now answers the config, and nothing is on.");

            ExpandNullforge.Foundation.DimensionLogConfig.SetChannels("portal, travel");

            Assert.That(
                ExpandNullforge.Foundation.DimensionLogConfig.TraceOn("portal"),
                Is.True,
                "A channel that was named must be on. This is exactly what was unreachable before: " +
                "the flag was a field nothing anywhere assigned.");
            Assert.That(
                ExpandNullforge.Foundation.DimensionLogConfig.TraceOn("tileset"),
                Is.False,
                "A channel that was not named must stay off, or the switch is a volume dial again.");
            Assert.That(
                ExpandNullforge.Foundation.DimensionFrameworkLog.VerboseRuntimeLogging,
                Is.True,
                "The five call sites that ask the old flag directly mean 'is anyone listening to " +
                "detail', and somebody is.");

            ExpandNullforge.Foundation.DimensionLogConfig.SetChannels("*");

            Assert.That(
                ExpandNullforge.Foundation.DimensionLogConfig.TraceOn("anything at all"),
                Is.True,
                "* means every channel.");

            ExpandNullforge.Foundation.DimensionLogConfig.Reset();
        }

        /// <summary>Every line carries its channel and which side of the game it came from.</summary>
        [Test]
        public void EveryLineSaysWhatItIsAboutAndWhereItCameFrom()
        {
            string line = ExpandNullforge.Foundation.DimensionLog.Line(
                ExpandNullforge.Foundation.DimensionLogChannels.Portal,
                null,
                "the portal did a thing");

            StringAssert.StartsWith("[NF/portal][", line);
            StringAssert.Contains("the portal did a thing", line);
            StringAssert.Contains(" f", line);
        }

        private static bool LineIsAComment(string text, int index)
        {
            int start = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            string line = text.Substring(start, index - start).TrimStart();
            return line.StartsWith("//", StringComparison.Ordinal) ||
                   line.StartsWith("*", StringComparison.Ordinal) ||
                   line.StartsWith("///", StringComparison.Ordinal);
        }

        private static int LineNumber(string text, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }
    }
}
#endif
