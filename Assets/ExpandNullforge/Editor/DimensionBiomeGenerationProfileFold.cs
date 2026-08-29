using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Moves the generation passes of a retired Biome Generation Profile onto the biome that
    /// pointed at it, so a project authored before the fold keeps generating what it generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A biome used to run two lists of passes: its own, and the ones inside a separate
    /// BiomeGenerationProfileAsset it referenced. The profile had no creation path anywhere in the
    /// product and split a biome's recipe across two assets, so it was folded into the biome and
    /// the type deleted. The starter factory put the passes ONLY on the profile, which means every
    /// biome the factory ever made has an empty list of its own and would silently generate nothing
    /// after the fold. That is the failure this migration exists to prevent.
    /// </para>
    /// <para>
    /// It reads YAML text rather than loading the profile, because the profile's script is gone:
    /// <c>LoadAssetAtPath</c> returns null for an asset whose MonoBehaviour type no longer exists,
    /// and the biome's own <c>generationProfile</c> field is no longer part of the type either, so
    /// SerializedObject cannot see the reference that is still sitting on disk. The file text is
    /// the only place the link survives. Both are addressed by GUID, which outlives the type.
    /// </para>
    /// <para>
    /// Folding is not destructive: it adds passes and never deletes a file on its own. The profile
    /// asset left behind is named in the log, and the menu command deletes those files only because
    /// a person asked for it.
    /// </para>
    /// </remarks>
    internal static class DimensionBiomeGenerationProfileFold
    {
        private const string SessionKey = "ExpandNullforge.GenerationProfileFold.Ran";

        private const string ProfileFieldName = "generationProfile";

        private const string PassesFieldName = "generationPasses";

        /// <summary>
        /// A GUID reference as Unity writes it: <c>{fileID: 11400000, guid: ..., type: 2}</c>.
        /// An unassigned reference is <c>{fileID: 0}</c> and carries no guid, so it will not match.
        /// </summary>
        private static readonly Regex GuidPattern = new Regex(
            "guid:\\s*([0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        /// <summary>What one run did, so callers can log it or show it.</summary>
        internal sealed class FoldReport
        {
            internal FoldReport(
                int biomesFolded,
                int passesMoved,
                IReadOnlyList<string> leftoverProfilePaths,
                IReadOnlyList<string> warnings)
            {
                BiomesFolded = biomesFolded;
                PassesMoved = passesMoved;
                LeftoverProfilePaths = leftoverProfilePaths ?? new List<string>();
                Warnings = warnings ?? new List<string>();
            }

            internal int BiomesFolded { get; private set; }

            internal int PassesMoved { get; private set; }

            /// <summary>Profile assets whose passes were folded and which nothing points at now.</summary>
            internal IReadOnlyList<string> LeftoverProfilePaths { get; private set; }

            internal IReadOnlyList<string> Warnings { get; private set; }
        }

        [MenuItem("Dimensions API/Maintenance/Fold Generation Profiles Into Biomes")]
        private static void FoldFromMenu()
        {
            FoldReport report = Fold(true);
            string message = report.BiomesFolded == 0
                ? "No biome still points at a generation profile. Nothing to move."
                : "Moved " + report.PassesMoved + " generation pass(es) onto " +
                    report.BiomesFolded + " biome(s), and deleted the emptied profile asset(s).";

            if (report.Warnings.Count > 0)
            {
                message += "\n\nCould not finish everything:\n" + string.Join("\n", ToArray(report.Warnings));
            }

            EditorUtility.DisplayDialog("Generation profiles", message, "OK");
        }

        /// <summary>
        /// Runs the fold once per editor session, right after the recompile that removed the type.
        /// </summary>
        /// <remarks>
        /// Deferred with <c>delayCall</c> because a load-time callback can fire before the asset
        /// database will answer a search, which would report a project with no biomes in it and
        /// leave the passes stranded.
        /// </remarks>
        [InitializeOnLoadMethod]
        private static void FoldOnceThisSession()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                FoldReport report = Fold(false);
                if (report.BiomesFolded > 0)
                {
                    Debug.Log(
                        "Dimensions API moved " + report.PassesMoved +
                        " generation pass(es) off retired generation profiles and onto " +
                        report.BiomesFolded + " biome(s). A biome now owns the passes it runs. " +
                        "These profile assets are no longer used and can be deleted: " +
                        string.Join(", ", ToArray(report.LeftoverProfilePaths)));
                }

                for (int i = 0; i < report.Warnings.Count; i++)
                {
                    Debug.LogWarning(report.Warnings[i]);
                }
            };
        }

        /// <summary>Folds every biome that still names a profile, optionally deleting the profile.</summary>
        internal static FoldReport Fold(bool deleteFoldedProfiles)
        {
            List<string> leftovers = new List<string>();
            List<string> warnings = new List<string>();
            int biomesFolded = 0;
            int passesMoved = 0;

            string[] biomeGuids = AssetDatabase.FindAssets("t:BiomeTemplateAsset");
            for (int i = 0; i < biomeGuids.Length; i++)
            {
                string biomePath = AssetDatabase.GUIDToAssetPath(biomeGuids[i]);
                BiomeTemplateAsset biome = AssetDatabase.LoadAssetAtPath<BiomeTemplateAsset>(biomePath);
                if (biome == null)
                {
                    continue;
                }

                string profileGuid = ReadReferenceGuid(ReadText(biomePath), ProfileFieldName);
                if (string.IsNullOrEmpty(profileGuid))
                {
                    continue;
                }

                string profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                if (string.IsNullOrEmpty(profilePath))
                {
                    warnings.Add(
                        "Biome '" + biomePath + "' names a generation profile that is not in the " +
                        "project any more, so its passes could not be recovered. Add the passes it " +
                        "should run to the biome's Passes list.");
                    continue;
                }

                IReadOnlyList<string> passGuids = ReadReferenceListGuids(ReadText(profilePath), PassesFieldName);
                List<GenerationPassTemplateAsset> merged = new List<GenerationPassTemplateAsset>();
                int movedHere = 0;
                for (int p = 0; p < passGuids.Count; p++)
                {
                    string passPath = AssetDatabase.GUIDToAssetPath(passGuids[p]);
                    GenerationPassTemplateAsset pass = string.IsNullOrEmpty(passPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<GenerationPassTemplateAsset>(passPath);
                    if (pass == null)
                    {
                        warnings.Add(
                            "Generation profile '" + profilePath + "' listed a pass that is not in " +
                            "the project any more. Biome '" + biomePath + "' will run without it.");
                        continue;
                    }

                    if (!merged.Contains(pass))
                    {
                        merged.Add(pass);
                        movedHere++;
                    }
                }

                // The profile's passes ran BEFORE the biome's own, so they go in first: a carve step
                // that ran first and now runs last would bury everything the later steps placed.
                GenerationPassTemplateAsset[] own = biome.GenerationPasses;
                for (int o = 0; o < own.Length; o++)
                {
                    if (!merged.Contains(own[o]))
                    {
                        merged.Add(own[o]);
                    }
                }

                biome.SetGenerationPasses(merged);
                EditorUtility.SetDirty(biome);
                biomesFolded++;
                passesMoved += movedHere;
                if (!leftovers.Contains(profilePath))
                {
                    leftovers.Add(profilePath);
                }
            }

            if (biomesFolded > 0)
            {
                AssetDatabase.SaveAssets();
            }

            if (deleteFoldedProfiles)
            {
                for (int i = 0; i < leftovers.Count; i++)
                {
                    AssetDatabase.DeleteAsset(leftovers[i]);
                }

                if (leftovers.Count > 0)
                {
                    AssetDatabase.Refresh();
                }
            }

            return new FoldReport(biomesFolded, passesMoved, leftovers, warnings);
        }

        /// <summary>The GUID a single object-reference field names, or empty when it names nothing.</summary>
        /// <remarks>
        /// Kept as a pure text function so it can be tested without a project on disk. It reads one
        /// line, because Unity writes a single reference inline and never wraps it.
        /// </remarks>
        internal static string ReadReferenceGuid(string yaml, string fieldName)
        {
            string[] lines = SplitLines(yaml);
            for (int i = 0; i < lines.Length; i++)
            {
                string value;
                if (!TryReadField(lines[i], fieldName, out value))
                {
                    continue;
                }

                Match match = GuidPattern.Match(value);
                return match.Success ? match.Groups[1].Value : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// The GUIDs a list-of-references field names, in the order the file lists them.
        /// </summary>
        /// <remarks>
        /// Stops at the next field, so a profile that lists several reference arrays in a row does
        /// not bleed the following array's entries into this one.
        /// </remarks>
        internal static IReadOnlyList<string> ReadReferenceListGuids(string yaml, string fieldName)
        {
            List<string> guids = new List<string>();
            string[] lines = SplitLines(yaml);
            for (int i = 0; i < lines.Length; i++)
            {
                string value;
                if (!TryReadField(lines[i], fieldName, out value))
                {
                    continue;
                }

                if (value.Trim() == "[]")
                {
                    return guids;
                }

                for (int e = i + 1; e < lines.Length; e++)
                {
                    string entry = lines[e].Trim();
                    if (!entry.StartsWith("-"))
                    {
                        return guids;
                    }

                    Match match = GuidPattern.Match(entry);
                    if (match.Success && !guids.Contains(match.Groups[1].Value))
                    {
                        guids.Add(match.Groups[1].Value);
                    }
                }

                return guids;
            }

            return guids;
        }

        /// <summary>
        /// Whether this line is the named field, and what follows its colon.
        /// </summary>
        /// <remarks>
        /// Matches on the trimmed name so indentation cannot fool it, and requires the whole name
        /// rather than a prefix: without that, <c>generationPasses</c> would also answer for
        /// <c>generationPassesOverride</c>, and the reader would follow the wrong array.
        /// </remarks>
        private static bool TryReadField(string line, string fieldName, out string value)
        {
            value = string.Empty;
            int colon = line.IndexOf(':');
            if (colon < 0)
            {
                return false;
            }

            string name = line.Substring(0, colon).Trim();
            if (name.StartsWith("-"))
            {
                name = name.Substring(1).Trim();
            }

            if (name != fieldName)
            {
                return false;
            }

            value = line.Substring(colon + 1);
            return true;
        }

        private static string[] SplitLines(string yaml)
        {
            if (string.IsNullOrEmpty(yaml))
            {
                return new string[0];
            }

            return yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary>The text of a project asset, or empty when it cannot be read.</summary>
        /// <remarks>
        /// An unreadable file must not abort the whole sweep, because one broken asset would then
        /// strand the passes of every biome after it in the scan.
        /// </remarks>
        private static string ReadText(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string fullPath = Path.Combine(projectRoot, assetPath);
                return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private static string[] ToArray(IReadOnlyList<string> source)
        {
            string[] copy = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }
    }
}
