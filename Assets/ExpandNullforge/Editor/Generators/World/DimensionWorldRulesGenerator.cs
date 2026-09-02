using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionWorldRulesGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "World rules: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Writes the settings files Core Keeper reads out of a mod's Conf folder while it starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY FILES AND NOT AN OBJECT. Fishing and talents are two of the four rule tables the game
    /// rebuilds at start-up by taking its own table and merging JSON files out of every loaded
    /// mod's <c>Conf</c> folder. Nothing has to run for that to happen and no code of ours is
    /// involved — the file being there is the whole mechanism. The mod build step copies every
    /// <c>.json</c> under <c>Conf</c> into the finished mod on its own.
    /// </para>
    /// <para>
    /// THE FILE SHAPES ARE THE GAME'S, NOT OURS. Each file is read back into one of Core Keeper's
    /// own little settings classes, field name for field name, and every id in them is a plain
    /// number — the game reads these with Unity's own JSON reader, which does not know what
    /// "Slime" or "CopperBar" means. So every name an author types is turned into its number here,
    /// while the mod's own loot tables and conditions are still answerable, and a name that
    /// answers to nothing is reported rather than written as a zero.
    /// </para>
    /// <para>
    /// THESE FOUR FOLDERS BELONG TO THE FRAMEWORK. Anything left in them that this run did not
    /// write is taken away, for the same reason a generated prefab is taken away when its asset
    /// is deleted: a rule switched off in the editor has to actually stop, and a settings file
    /// nobody remembers writing would go on changing the game forever.
    /// </para>
    /// </remarks>
    internal static class DimensionWorldRulesGenerator
    {

        private const string BiomeFishingFolder = "Conf/Loot/Fishing/Biome";
        private const string WaterFishingFolder = "Conf/Loot/Fishing/Water";
        private const string FishFightFolder = "Conf/Fishing";
        private const string TalentFolder = "Conf/Talents";

        /// <summary>
        /// The number of biome slots and ground slots the game lays out when it bakes the fishing
        /// table. A biome or ground numbered past the end of one of these would be written outside
        /// the table as the world loads, which is why a custom one is refused rather than emitted.
        /// </summary>
        private const int BiomeSlotCount = 12;

        private const int GroundSlotCount = 75;

        public static DimensionWorldRulesGenerationReport Generate(
            IEnumerable<DimensionGameSetupAsset> setups,
            string modRoot)
        {
            DimensionWorldRulesGenerationReport report = new DimensionWorldRulesGenerationReport();
            if (string.IsNullOrEmpty(modRoot))
            {
                report.Errors.Add(
                    "Could not work out which mod folder these world rules belong to, so no " +
                    "settings files were written. Save the dimension asset inside a mod folder.");
                return report;
            }

            HashSet<string> written = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                AssetDatabase.StartAssetEditing();

                if (setups != null)
                {
                    foreach (DimensionGameSetupAsset setup in setups)
                    {
                        GenerateOne(setup, modRoot, written, report);
                    }
                }

                PruneStaleFiles(modRoot, written, report);
                RemoveRetiredGameSetupPrefabs(modRoot, report);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static void GenerateOne(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            if (setup == null || !setup.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(setup.SetupIdentifier))
            {
                report.Skipped.Add("A rule set with no id was skipped — give it a short id first.");
                return;
            }

            if (setup.NothingIsSwitchedOn)
            {
                report.Skipped.Add(
                    "'" + setup.DisplayName + "' has none of its four blocks switched on, so " +
                    "nothing was written for it.");
                return;
            }

            WriteFishing(setup, modRoot, written, report);
            WriteTalents(setup, modRoot, written, report);
        }

        // ------------------------------------------------------------- fishing ---

        private static void WriteFishing(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            DimensionFishingTemplate fishing = setup.Fishing;
            if (!fishing.ChangesWhatFishingCatches)
            {
                return;
            }

            if (fishing.NothingWasWritten)
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' changes what fishing catches but names no biome, " +
                    "no water and no fish, so fishing is left exactly as the game has it. Add a " +
                    "row or switch the block off.");
                return;
            }

            WriteFishingBiomes(setup, modRoot, written, report);
            WriteFishingWaters(setup, modRoot, written, report);
            WriteFishFights(setup, modRoot, written, report);
        }

        private static void WriteFishingBiomes(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            DimensionFishingInBiome[] rows = setup.Fishing.Biomes;
            for (int i = 0; i < rows.Length; i++)
            {
                DimensionFishingInBiome row = rows[i];
                if (string.IsNullOrEmpty(row.Biome))
                {
                    continue;
                }

                Biome biome;
                if (!Enum.TryParse(row.Biome, false, out biome) || biome == Biome.None)
                {
                    report.Warnings.Add(
                        "'" + setup.DisplayName + "' sets what is fished in '" + row.Biome +
                        "', which is not a biome the game has. That row was left out — name one " +
                        "of Slime, Larva, Stone, Nature, Sea, Desert, Crystal, Passage or " +
                        "Excavation.");
                    continue;
                }

                if ((int)biome >= BiomeSlotCount)
                {
                    report.Warnings.Add(
                        "'" + setup.DisplayName + "' sets what is fished in '" + row.Biome +
                        "', which sits outside the twelve biome slots the game's fishing table " +
                        "has room for. That row was left out — a biome this mod invented cannot " +
                        "have its own fishing yet; key the rule to the water instead.");
                    continue;
                }

                LootTableID fish;
                LootTableID junk;
                if (!ResolveFishingTables(setup, row.FishCaught, row.JunkCaught, report,
                        row.Biome, out fish, out junk))
                {
                    continue;
                }

                StringBuilder json = new StringBuilder();
                json.Append("{\"biome\":").Append(((int)biome).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"junkLoot\":").Append(((int)junk).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"fishLoot\":").Append(((int)fish).ToString(CultureInfo.InvariantCulture));
                json.Append('}');

                WriteFile(
                    modRoot,
                    BiomeFishingFolder,
                    setup.SetupIdentifier + "_" + biome,
                    json.ToString(),
                    written,
                    report);
            }
        }

        private static void WriteFishingWaters(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            DimensionFishingInWater[] rows = setup.Fishing.Waters;
            for (int i = 0; i < rows.Length; i++)
            {
                DimensionFishingInWater row = rows[i];
                if (string.IsNullOrEmpty(row.WaterGround))
                {
                    continue;
                }

                Tileset ground;
                if (!Enum.TryParse(row.WaterGround, false, out ground))
                {
                    report.Warnings.Add(
                        "'" + setup.DisplayName + "' sets what is fished in '" + row.WaterGround +
                        "' water, which is not a ground the game has. That row was left out — a " +
                        "ground this mod invented cannot have its own fishing yet, because the " +
                        "game's fishing table has room only for the grounds it ships with.");
                    continue;
                }

                if ((int)ground < 0 || (int)ground >= GroundSlotCount)
                {
                    report.Warnings.Add(
                        "'" + setup.DisplayName + "' sets what is fished in '" + row.WaterGround +
                        "' water, which sits outside the seventy-five ground slots the game's " +
                        "fishing table has room for. That row was left out — name one of the " +
                        "game's own water grounds instead.");
                    continue;
                }

                LootTableID fish;
                LootTableID junk;
                if (!ResolveFishingTables(setup, row.FishCaught, row.JunkCaught, report,
                        row.WaterGround + " water", out fish, out junk))
                {
                    continue;
                }

                StringBuilder json = new StringBuilder();
                json.Append("{\"waterType\":").Append(((int)ground).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"junkLoot\":").Append(((int)junk).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"fishLoot\":").Append(((int)fish).ToString(CultureInfo.InvariantCulture));
                json.Append('}');

                WriteFile(
                    modRoot,
                    WaterFishingFolder,
                    setup.SetupIdentifier + "_" + ground,
                    json.ToString(),
                    written,
                    report);
            }
        }

        /// <summary>
        /// Turns the two loot table names on one fishing row into numbers, reporting either one
        /// that answers to nothing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A name that was typed but answers to nothing stops the row, because writing an empty
        /// table for it would silently turn the water barren.
        /// </para>
        /// <para>
        /// THE JUNK TABLE IS WHAT THE GAME USES TO ASK WHETHER THE RULE EXISTS AT ALL, which is
        /// why a blank one stops the row too. <c>FishingTableCD.GetFishingStats</c> reads a rule,
        /// then tests <c>lootTableID == LootTableID.Empty</c> — the JUNK table — and on finding it
        /// blank moves on to the next place to look, ending at the biome's rule
        /// (<c>ck-db\Pug.ECS.Components\FishingTableCD.cs:8-27</c>). A rule that names only fish
        /// therefore reads as no rule at all: the fish table is dropped on the floor and the water
        /// catches whatever it caught before, with nothing said anywhere. Requiring a junk table is
        /// the only way to write a rule the game will actually read.
        /// </para>
        /// </remarks>
        private static bool ResolveFishingTables(
            DimensionGameSetupAsset setup,
            string fishName,
            string junkName,
            DimensionWorldRulesGenerationReport report,
            string where,
            out LootTableID fish,
            out LootTableID junk)
        {
            fish = LootTableID.Empty;
            junk = LootTableID.Empty;

            if (!string.IsNullOrEmpty(fishName) &&
                !DimensionEditorLootTables.TryResolve(fishName, out fish))
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' catches fish from '" + fishName + "' in " + where +
                    ", which is neither one of the game's loot tables nor one this dimension " +
                    "makes. That row was left out — check the spelling against the loot table's " +
                    "own id.");
                return false;
            }

            if (!string.IsNullOrEmpty(junkName) &&
                !DimensionEditorLootTables.TryResolve(junkName, out junk))
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' pulls junk from '" + junkName + "' in " + where +
                    ", which is neither one of the game's loot tables nor one this dimension " +
                    "makes. That row was left out — check the spelling against the loot table's " +
                    "own id.");
                return false;
            }

            if (junk == LootTableID.Empty)
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' sets what is fished in " + where + " but names " +
                    "no junk table, and the game reads a rule with no junk as no rule at all — " +
                    "the fish named here would never be caught and nothing would say so. That row " +
                    "was left out. Name the table the junk comes from as well as the one the fish " +
                    "come from.");
                return false;
            }

            if (fish == LootTableID.Empty)
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' sets " + where + " to catch junk and no fish, so " +
                    "casting there mostly pulls up nothing at all. The row was written as asked — " +
                    "name a table the fish come from if that was not what you meant.");
            }

            return true;
        }

        /// <summary>
        /// Groups a rule set's fight turns by fish, keeping both the order the fish first appear
        /// and the order of each fish's own turns.
        /// </summary>
        /// <remarks>
        /// Shared with the bootstrap emitter on purpose. The two halves of fish fights — Core
        /// Keeper's fish through a settings file, this mod's fish through a registration — must
        /// agree exactly on which rows belong to which fish, or a fish could be written twice or
        /// not at all depending on which half read the rows.
        /// </remarks>
        internal static List<string> GroupFishFights(
            DimensionFishFightTurn[] turns,
            out Dictionary<string, List<DimensionFishFightTurn>> byFish)
        {
            List<string> order = new List<string>();
            byFish = new Dictionary<string, List<DimensionFishFightTurn>>(StringComparer.Ordinal);
            if (turns == null)
            {
                return order;
            }

            for (int i = 0; i < turns.Length; i++)
            {
                string fish = turns[i].Fish;
                if (string.IsNullOrEmpty(fish))
                {
                    continue;
                }

                List<DimensionFishFightTurn> forFish;
                if (!byFish.TryGetValue(fish, out forFish))
                {
                    forFish = new List<DimensionFishFightTurn>();
                    byFish.Add(fish, forFish);
                    order.Add(fish);
                }

                forFish.Add(turns[i]);
            }

            return order;
        }

        /// <summary>
        /// True when the name is one of Core Keeper's own objects, which is what decides whether a
        /// fight can be written as a settings file at all.
        /// </summary>
        /// <remarks>
        /// The file names the fish by NUMBER, and only Core Keeper's own names have a number while
        /// the editor is running. Everything else is this mod's, and travels the other road: a
        /// registration in the generated bootstrap, applied while the world loads and the mod's
        /// objects finally have numbers.
        /// </remarks>
        internal static bool IsOneOfTheGamesOwnFish(string fishName, out ObjectID fishId)
        {
            fishId = DimensionObjectBinder.Vanilla(fishName);
            return fishId != ObjectID.None;
        }

        /// <summary>
        /// A fight only ends if the fish both pulls and rests for a real length of time; a fight
        /// that is all one or all the other leaves the fish hooked forever.
        /// </summary>
        internal static void MeasureFishFight(
            List<DimensionFishFightTurn> forFish,
            out bool anyPull,
            out bool anyRest)
        {
            anyPull = false;
            anyRest = false;
            for (int t = 0; t < forFish.Count; t++)
            {
                if (forFish[t].Seconds <= 0f)
                {
                    continue;
                }

                if (forFish[t].Pulls)
                {
                    anyPull = true;
                }
                else
                {
                    anyRest = true;
                }
            }
        }

        /// <summary>The warning for a fight that can never be landed, worded the same on both roads.</summary>
        internal static string DescribeUnresolvableFight(string setupName, string fishName, bool anyPull)
        {
            return "'" + setupName + "' gives " + fishName + " a fight that is all " +
                (anyPull ? "pulling" : "resting") + ", so the fight never resolves and the " +
                "fish can never be landed. Add at least one " +
                (anyPull ? "resting" : "pulling") + " turn with a length above zero.";
        }

        /// <summary>
        /// Writes one file per fish, with that fish's turns in the order the author listed them.
        /// </summary>
        /// <remarks>
        /// Core Keeper's own fish only. The file names the fish by object id NUMBER, and a fish
        /// this mod adds has no number until the mod is loaded, which is long after these files
        /// have been read. Those fish are not reported here and are not lost either — the
        /// bootstrap emitter picks up every name this one leaves behind and registers it to be
        /// applied while the world loads.
        /// </remarks>
        private static void WriteFishFights(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            Dictionary<string, List<DimensionFishFightTurn>> byFish;
            List<string> order = GroupFishFights(setup.Fishing.FishFights, out byFish);

            for (int i = 0; i < order.Count; i++)
            {
                string fishName = order[i];
                ObjectID fishId;
                if (!IsOneOfTheGamesOwnFish(fishName, out fishId))
                {
                    continue;
                }

                List<DimensionFishFightTurn> forFish = byFish[fishName];
                bool anyPull;
                bool anyRest;
                MeasureFishFight(forFish, out anyPull, out anyRest);

                if (!anyPull || !anyRest)
                {
                    report.Warnings.Add(DescribeUnresolvableFight(setup.DisplayName, fishName, anyPull));
                    continue;
                }

                StringBuilder json = new StringBuilder();
                json.Append("{\"fish\":").Append(((int)fishId).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"struggleData\":[");
                for (int t = 0; t < forFish.Count; t++)
                {
                    if (t > 0)
                    {
                        json.Append(',');
                    }

                    json.Append("{\"isStruggling\":")
                        .Append(forFish[t].Pulls ? "true" : "false")
                        .Append(",\"time\":")
                        .Append(forFish[t].Seconds.ToString("R", CultureInfo.InvariantCulture))
                        .Append('}');
                }

                json.Append("]}");

                WriteFile(
                    modRoot,
                    FishFightFolder,
                    setup.SetupIdentifier + "_" + fishId,
                    json.ToString(),
                    written,
                    report);
            }
        }

        // ------------------------------------------------------------- talents ---

        /// <summary>
        /// Writes one file per skill, listing that skill's talents in the order the author gave.
        /// </summary>
        /// <remarks>
        /// The game merges a talent file into its own tree BY POSITION: the first talent in the
        /// file overwrites the skill's first talent, the second its second, and so on. There is no
        /// way to speak about the fourth talent without also giving the first three, which is why
        /// the authoring asks for the whole list rather than for a talent number.
        /// </remarks>
        private static void WriteTalents(
            DimensionGameSetupAsset setup,
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            DimensionTalentsTemplate talents = setup.Talents;
            if (!talents.ChangesWhatTalentsGive)
            {
                return;
            }

            if (talents.NothingWasWritten)
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' changes what talents give but lists no talents, " +
                    "so every talent is left as the game has it. Add a talent or switch the block " +
                    "off.");
                return;
            }

            // The grouping is asked of the template rather than done here, because three separate
            // things depend on a talent's position in its skill's list — this file, the wording
            // row that names the square, and the picture that goes on it — and two of them
            // counting differently would put a talent's picture on its neighbour with nothing
            // said. One rule, in one place.
            List<string> unknownSkills = new List<string>();
            List<DimensionTalentGroup> groups =
                DimensionTalentsTemplate.Group(talents.Talents, unknownSkills);

            for (int i = 0; i < unknownSkills.Count; i++)
            {
                report.Warnings.Add(
                    "'" + setup.DisplayName + "' writes talents for '" + unknownSkills[i] +
                    "', which is not one of the game's twelve skills. Those talents were left " +
                    "out — name one of Mining, Running, Melee, Vitality, Crafting, Range, " +
                    "Gardening, Fishing, Cooking, Magic, Summoning or Explosives.");
            }

            for (int i = 0; i < groups.Count; i++)
            {
                string skillName = groups[i].SkillName;
                SkillID skill = groups[i].Skill;

                DimensionTalent[] forSkill = groups[i].Talents;
                StringBuilder json = new StringBuilder();
                json.Append("{\"skill\":").Append(((int)skill).ToString(CultureInfo.InvariantCulture));
                json.Append(",\"talents\":[");

                bool wroteAny = false;
                for (int t = 0; t < forSkill.Length; t++)
                {
                    DimensionTalent talent = forSkill[t];
                    ConditionID condition = ConditionID.None;
                    if (!string.IsNullOrEmpty(talent.Gives) &&
                        !DimensionObjectSpine.TryResolveCondition(talent.Gives, out condition))
                    {
                        report.Warnings.Add(
                            "'" + setup.DisplayName + "' gives the " + skillName + " talent '" +
                            talent.TalentName + "' the effect '" + talent.Gives +
                            "', which is neither one of the game's own nor one this dimension " +
                            "makes. It was written as no effect at all — check the spelling " +
                            "against the condition's own name.");
                        condition = ConditionID.None;
                    }

                    if (string.IsNullOrEmpty(talent.TalentName))
                    {
                        report.Warnings.Add(
                            "'" + setup.DisplayName + "' leaves talent " +
                            (t + 1).ToString(CultureInfo.InvariantCulture) + " of " + skillName +
                            " unnamed, which clears the name the game gave it and leaves a blank " +
                            "square in the talent window. Type the game's own name for it if you " +
                            "only mean to change its numbers.");
                    }

                    if (wroteAny)
                    {
                        json.Append(',');
                    }

                    json.Append("{\"name\":").Append(Quote(talent.TalentName));
                    json.Append(",\"givesCondition\":")
                        .Append(((int)condition).ToString(CultureInfo.InvariantCulture));
                    json.Append(",\"conditionValuePerPoint\":")
                        .Append(talent.PerPoint.ToString(CultureInfo.InvariantCulture));
                    json.Append('}');
                    wroteAny = true;
                }

                json.Append("]}");

                if (!wroteAny)
                {
                    continue;
                }

                WriteFile(
                    modRoot,
                    TalentFolder,
                    setup.SetupIdentifier + "_" + skill,
                    json.ToString(),
                    written,
                    report);
            }
        }

        // --------------------------------------------------------------- files ---

        private static void WriteFile(
            string modRoot,
            string folder,
            string fileName,
            string contents,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            string assetFolder = modRoot.TrimEnd('/') + "/" + folder;
            if (!DimensionAssetFolders.EnsureExists(assetFolder))
            {
                report.Errors.Add(
                    "Could not create '" + assetFolder + "', so that rule was not written. Check " +
                    "that the mod folder is not read-only.");
                return;
            }

            string path = assetFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(fileName, "rules") + ".json";
            written.Add(path);

            string absolute = ToAbsolutePath(path);
            try
            {
                bool existed = System.IO.File.Exists(absolute);
                string previous = existed ? System.IO.File.ReadAllText(absolute) : null;
                if (string.Equals(previous, contents, StringComparison.Ordinal))
                {
                    report.Updated.Add(path);
                    return;
                }

                System.IO.File.WriteAllText(absolute, contents);
                AssetDatabase.ImportAsset(path);
                if (existed)
                {
                    report.Updated.Add(path);
                }
                else
                {
                    report.Created.Add(path);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add("Could not write '" + path + "': " + exception.Message);
            }
        }

        /// <summary>
        /// Takes away settings files this run did not write, so a rule switched off in the editor
        /// actually stops changing the game.
        /// </summary>
        private static void PruneStaleFiles(
            string modRoot,
            HashSet<string> written,
            DimensionWorldRulesGenerationReport report)
        {
            string[] folders =
            {
                BiomeFishingFolder,
                WaterFishingFolder,
                FishFightFolder,
                TalentFolder
            };

            for (int i = 0; i < folders.Length; i++)
            {
                string assetFolder = modRoot.TrimEnd('/') + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(assetFolder))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { assetFolder });
                for (int g = 0; g < guids.Length; g++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    if (string.IsNullOrEmpty(path) ||
                        !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                        written.Contains(path))
                    {
                        continue;
                    }

                    if (AssetDatabase.DeleteAsset(path))
                    {
                        report.Skipped.Add("Took away '" + path + "', which no rule asks for now.");
                    }
                }
            }
        }

        /// <summary>
        /// Takes away the two prefabs an older version of this stage used to write.
        /// </summary>
        /// <remarks>
        /// They were a faithful copy of the object a loaded world reads its rules from, and of the
        /// player — and neither ever reached a running game, because the mod pipeline drops any
        /// object that is not an entity, and the object database keeps whichever copy of an object
        /// arrived first, which is always Core Keeper's. A mod already built with them is carrying
        /// two prefabs that do nothing, so one generate after updating clears them out.
        /// </remarks>
        private static void RemoveRetiredGameSetupPrefabs(
            string modRoot,
            DimensionWorldRulesGenerationReport report)
        {
            string retired = modRoot.TrimEnd('/') + "/Items/GameSetup";
            if (!AssetDatabase.IsValidFolder(retired))
            {
                return;
            }

            if (AssetDatabase.DeleteAsset(retired))
            {
                report.Skipped.Add(
                    "Took away '" + retired + "'. Those two prefabs never reached a running game — " +
                    "what they promised is now under World rules, or under the loot, effect and " +
                    "spawn assets that already owned it.");
            }
        }

        /// <summary>A JSON string, with the five characters Unity's own reader cannot take raw.</summary>
        private static string Quote(string value)
        {
            StringBuilder quoted = new StringBuilder((value == null ? 0 : value.Length) + 2);
            quoted.Append('"');
            for (int i = 0; value != null && i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            quoted.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            quoted.Append(character);
                        }

                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            return System.IO.Path.Combine(projectRoot ?? string.Empty, assetPath)
                .Replace('\\', '/');
        }
    }
}
