using System.Globalization;
using System.Text;
using ExpandNullforge.Authoring;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits the half of a mod's world rules that only exists once the game is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MOST OF THE RULE BLOCKS ARE NOT FILES. Fishing and talents reach the game as settings
    /// files, written straight into the mod's Conf folder and read before anything of ours runs.
    /// The rest have no file: the game reads upgrade prices, the player's numbers, armour sets,
    /// backgrounds, the world's own events and the terrain rules off objects it holds in memory,
    /// once, as a world loads. So they are carried as instructions in the generated bootstrap and
    /// applied at that one moment.
    /// </para>
    /// <para>
    /// TWO MORE ARE NOT WRITTEN HERE AT ALL. Skill pictures and a pet's colours are Unity objects,
    /// not numbers, so they cannot be written into C# source; they are read straight off the mod's
    /// own template at <c>ModObjectLoaded</c>, the way the talent pictures and the boss map pins
    /// already are.
    /// </para>
    /// <para>
    /// PRICES CARRY NAMES, NOT NUMBERS. An item this mod adds has no number until the mod is
    /// loaded, which is after the editor has finished. Emitting the name and answering it at load
    /// is what lets a mod price an upgrade in its own bars — the same law the creature and loot
    /// emitters already follow.
    /// </para>
    /// <para>
    /// THE DRIFT CURVE IS EMITTED AS TWO ROWS OF NUMBERS. Generated code is C# source; a curve
    /// object cannot be written into it, but the times and the amounts it is made of can, and the
    /// runtime rebuilds the curve from them.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        /// <summary>Writes the upgrade prices and player overrides every rule set asks for.</summary>
        internal static void AppendWorldRulesRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {

            DimensionGameSetupAsset[] setups = template == null ? null : template.GlobalGameSetups;
            if (setups == null)
            {
                return;
            }

            for (int i = 0; i < setups.Length; i++)
            {
                DimensionGameSetupAsset setup = setups[i];
                if (setup == null || !setup.Enabled)
                {
                    continue;
                }

                AppendUpgradeCosts(builder, setup, modName);
                AppendFishFights(builder, setup, template, modName);
                AppendPlayerOverrides(builder, setup);
                AppendArmourSets(builder, setup, modName);
                AppendBackgrounds(builder, setup, modName);
                AppendWorldEvents(builder, setup, template, modName);
                AppendGamesOwnTerrain(builder, setup, template, modName);
            }
        }

        /// <summary>
        /// Writes the armour sets this mod adds, as names to be answered while the world loads.
        /// </summary>
        /// <remarks>
        /// Every piece and every effect is emitted as a NAME, for the reason the whole file works
        /// that way: a piece of this mod's own gear has no object number until the mod is loaded,
        /// and an effect of this mod's own has no condition number until the condition registry has
        /// handed them out. Both happen after the editor has finished.
        /// </remarks>
        private static void AppendArmourSets(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            string modName)
        {
            DimensionSetBonusTemplate armour = setup.ArmourSets;
            if (!armour.AddsArmourSets)
            {
                return;
            }

            if (armour.NothingWasAdded)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' adds armour sets and lists none, " +
                    "so nothing is added. Add a set, or switch the block off.");
                return;
            }

            DimensionSetBonus[] sets = armour.Sets;
            for (int i = 0; i < sets.Length; i++)
            {
                DimensionSetBonus set = sets[i];
                if (set == null || string.IsNullOrEmpty(set.SetId))
                {
                    continue;
                }

                string[] pieces = set.Pieces;
                DimensionSetBonusLine[] lines = set.Lines;
                if (pieces.Length == 0 || lines.Length == 0)
                {
                    Debug.LogWarning(
                        "[Dimensions API] The set '" + set.SetId + "' in '" + setup.DisplayName +
                        "' has no pieces or no effects, so wearing it could never give anything. " +
                        "It was not written into the mod.");
                    continue;
                }

                if (pieces.Length > ExpandNullforge.WorldRules.DimensionSetBonusRegistry.MostTheTooltipDraws)
                {
                    Debug.LogWarning(
                        "[Dimensions API] The set '" + set.SetId + "' has " +
                        pieces.Length.ToString(CultureInfo.InvariantCulture) +
                        " pieces. The hover panel draws six, so the rest of them count towards the " +
                        "bonus but are never listed.");
                }

                StringBuilder pieceNames = new StringBuilder();
                for (int p = 0; p < pieces.Length; p++)
                {
                    if (string.IsNullOrEmpty(pieces[p]))
                    {
                        continue;
                    }

                    if (pieceNames.Length > 0)
                    {
                        pieceNames.Append(", ");
                    }

                    pieceNames.Append(
                        ToCSharpString(QualifyWorldRuleReference(modName, pieces[p])));
                }

                StringBuilder lineRows = new StringBuilder();
                for (int l = 0; l < lines.Length; l++)
                {
                    if (string.IsNullOrEmpty(lines[l].EffectName))
                    {
                        continue;
                    }

                    if (lineRows.Length > 0)
                    {
                        lineRows.Append(", ");
                    }

                    lineRows
                        .Append("new ExpandNullforge.WorldRules.DimensionSetBonusRegistry.LineRow { ")
                        .Append("EffectName = ").Append(ToCSharpString(lines[l].EffectName))
                        .Append(", RequiredPieces = ")
                        .Append(lines[l].RequiredPieces.ToString(CultureInfo.InvariantCulture))
                        .Append(", Strength = ")
                        .Append(lines[l].Strength.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f, Seconds = ")
                        .Append(lines[l].Seconds.ToString("R", CultureInfo.InvariantCulture))
                        .Append("f }");
                }

                if (pieceNames.Length == 0 || lineRows.Length == 0)
                {
                    continue;
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionSetBonusRegistry.Register(");
                builder.Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, set.SetId)));
                builder.Append(", new string[] { ").Append(pieceNames).Append(" }, ");
                builder.Append(((int)set.Tier).ToString(CultureInfo.InvariantCulture)).Append(", ");
                builder.Append(((int)set.Rarity).ToString(CultureInfo.InvariantCulture));
                builder.Append(
                    ", new ExpandNullforge.WorldRules.DimensionSetBonusRegistry.LineRow[] { ");
                builder.Append(lineRows).AppendLine(" });");
            }
        }

        /// <summary>
        /// Writes what the game's own backgrounds start a new character with.
        /// </summary>
        /// <remarks>
        /// A third starter item is REFUSED here rather than written: the character-creation screen
        /// indexes its two text lines by the item's position with no bounds check, so a third one
        /// throws as a player skims onto that background. The runtime refuses it too — this only
        /// says so while the author can still do something about it.
        /// </remarks>
        private static void AppendBackgrounds(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            string modName)
        {
            DimensionBackgroundTemplate backgrounds = setup.Backgrounds;
            if (!backgrounds.ChangesWhatBackgroundsStartYouWith)
            {
                return;
            }

            if (backgrounds.NothingWasChanged)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' changes what backgrounds start " +
                    "you with and names none, so every background keeps what the game gives it. " +
                    "Add a row, or switch the block off.");
                return;
            }

            DimensionBackgroundKit[] kits = backgrounds.Backgrounds;
            for (int i = 0; i < kits.Length; i++)
            {
                DimensionBackgroundKit kit = kits[i];
                if (kit == null)
                {
                    continue;
                }

                if (kit.ListsMoreItemsThanTheScreenCanShow)
                {
                    Debug.LogWarning(
                        "[Dimensions API] The " + kit.Background +
                        " background is given " +
                        kit.StartsWith.Length.ToString(CultureInfo.InvariantCulture) +
                        " things to start with. The character-creation screen has two lines to " +
                        "write them on, so only the first two are given.");
                }

                if (kit.NamesASkillNomadWillNeverGet)
                {
                    Debug.LogWarning(
                        "[Dimensions API] The Nomad background was given a starting skill. The game " +
                        "skips the starting skill for Nomad by name, so nothing will grant it.");
                }

                StringBuilder itemNames = new StringBuilder();
                StringBuilder amounts = new StringBuilder();
                StringBuilder variations = new StringBuilder();
                DimensionBackgroundItem[] items = kit.StartsWith;
                for (int k = 0; k < items.Length; k++)
                {
                    if (string.IsNullOrEmpty(items[k].ItemId))
                    {
                        continue;
                    }

                    if (itemNames.Length > 0)
                    {
                        itemNames.Append(", ");
                        amounts.Append(", ");
                        variations.Append(", ");
                    }

                    itemNames.Append(
                        ToCSharpString(QualifyWorldRuleReference(modName, items[k].ItemId)));
                    amounts.Append(items[k].Amount.ToString(CultureInfo.InvariantCulture));
                    variations.Append(items[k].Variation.ToString(CultureInfo.InvariantCulture));
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionBackgroundRegistry.Register(");
                builder.Append(((int)kit.Background).ToString(CultureInfo.InvariantCulture));
                builder.Append(", ").Append(ToCSharpString(kit.SkillName));
                builder.Append(", new string[] { ").Append(itemNames).Append(" }");
                builder.Append(", new int[] { ").Append(amounts).Append(" }");
                builder.Append(", new int[] { ").Append(variations).AppendLine(" });");
            }
        }

        /// <summary>
        /// Writes where and when the world acts on a player of its own accord.
        /// </summary>
        /// <remarks>
        /// Biomes stay as NAMES so the runtime does the one resolution and there is one rule about
        /// what is allowed; tile parts and blocks are turned into NUMBERS here, because both are
        /// answerable in the editor and that is what the game's own table stores. A biome this mod
        /// adds is refused with a line: the check the game runs reads the player's biome out of its
        /// own radial ranges, which nothing in this framework writes, so a custom biome named here
        /// would be a rule that never matched.
        /// </remarks>
        private static void AppendWorldEvents(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionEnvironmentEventTemplate events = setup.WorldEvents;
            if (!events.ChangesWhenTheWorldActs)
            {
                return;
            }

            if (events.NothingWasChanged)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' changes when the world acts and " +
                    "names no event, so all four keep the game's own rules. Add a row, or switch " +
                    "the block off.");
                return;
            }

            System.Func<string, int> resolveTileset = BuildWorldRuleTilesetResolver(template, modName);
            DimensionWorldEventRule[] rules = events.Events;
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionWorldEventRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.CanNeverHappenAnywhere)
                {
                    Debug.LogWarning(
                        "[Dimensions API] The " + rule.WorldEvent + " event is left able to happen " +
                        "and given no biome to happen in. The game tests the player's biome against " +
                        "that list, so an empty one means it never happens anywhere.");
                }

                StringBuilder biomes = new StringBuilder();
                string[] biomeIds = rule.BiomeIds;
                for (int b = 0; b < biomeIds.Length; b++)
                {
                    if (string.IsNullOrEmpty(biomeIds[b]))
                    {
                        continue;
                    }

                    if (!ExpandNullforge.WorldRules.DimensionEnvironmentEventRegistry
                            .TheEventCheckCanSeeThisBiome(biomeIds[b]))
                    {
                        Debug.LogWarning(
                            "[Dimensions API] The " + rule.WorldEvent + " event is set to happen " +
                            "in '" + biomeIds[b] + "', which is not one of Core Keeper's own " +
                            "biomes. The game works out which biome a player is standing in from " +
                            "its own radial ranges, and nothing a mod writes reaches those, so a " +
                            "biome of your own can never match here. The game's are Slime, Larva, " +
                            "Stone, Nature, Sea, Desert, Crystal, Passage and Excavation.");
                        continue;
                    }

                    if (biomes.Length > 0)
                    {
                        biomes.Append(", ");
                    }

                    biomes.Append(ToCSharpString(biomeIds[b]));
                }

                CultureInfo invariant = CultureInfo.InvariantCulture;
                builder.Append(
                    "    ExpandNullforge.WorldRules.DimensionEnvironmentEventRegistry.Register(");
                builder.Append(((int)rule.WorldEvent).ToString(invariant)).Append(", ");
                builder.Append(rule.CanHappen ? "true" : "false");
                builder.Append(", new string[] { ").Append(biomes).Append(" }, ");
                builder.Append(rule.TilesFromTheCore.ToString("R", invariant)).Append("f, ");
                builder.Append(rule.MostThingsNearby.ToString(invariant)).Append(", ");
                builder.Append(rule.TilesNeededInTotal.ToString(invariant)).Append(", ");
                builder.Append(rule.IgnoresTheSharedCooldown ? "true" : "false").Append(", ");
                builder.Append(rule.MayStartNearABoss ? "true" : "false").Append(", ");
                builder.Append(rule.SetsItsOwnCooldown ? "true" : "false").Append(", ");
                builder.Append(rule.CooldownShortestSeconds.ToString("R", invariant)).Append("f, ");
                builder.Append(rule.CooldownLongestSeconds.ToString("R", invariant)).AppendLine("f);");

                DimensionWorldEventGround[] ground = rule.Ground;
                for (int g = 0; g < ground.Length; g++)
                {
                    int tilePart = ResolveTilePartName(ground[g].TilePart);
                    if (tilePart < 0)
                    {
                        Debug.LogWarning(
                            "[Dimensions API] The " + rule.WorldEvent + " event asks for '" +
                            ground[g].TilePart + "' tiles, which is not a part of a block the game " +
                            "has. That requirement was left out — the parts are named wall, ground, " +
                            "water, pit and so on.");
                        continue;
                    }

                    StringBuilder tilesets = new StringBuilder();
                    string[] blockIds = ground[g].BlockIds;
                    for (int t = 0; t < blockIds.Length; t++)
                    {
                        int resolved = resolveTileset(blockIds[t]);
                        if (resolved < 0)
                        {
                            Debug.LogWarning(
                                "[Dimensions API] The " + rule.WorldEvent + " event counts '" +
                                blockIds[t] + "' tiles, which is neither one of this mod's blocks " +
                                "nor one of the game's. That block was left out.");
                            continue;
                        }

                        if (tilesets.Length > 0)
                        {
                            tilesets.Append(", ");
                        }

                        tilesets.Append(resolved.ToString(invariant));
                    }

                    builder.Append(
                        "    ExpandNullforge.WorldRules.DimensionEnvironmentEventRegistry.AddGround(");
                    builder.Append(((int)rule.WorldEvent).ToString(invariant)).Append(", ");
                    builder.Append(tilePart.ToString(invariant));
                    builder.Append(", new int[] { ").Append(tilesets).Append(" }, ");
                    builder.Append(ground[g].HowMany.ToString(invariant)).AppendLine(");");
                }
            }
        }

        /// <summary>
        /// Writes the rules that put this mod's blocks into Core Keeper's own caves.
        /// </summary>
        /// <remarks>
        /// Everything here is a number by the time it is written: the generator's own vocabulary is
        /// fixed, and the block a rule lays is answerable in the editor the same way every other
        /// block reference is.
        /// </remarks>
        private static void AppendGamesOwnTerrain(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionWorldTerrainRuleTemplate terrain = setup.GamesOwnTerrain;
            if (!terrain.PutsBlocksInTheGamesWorld)
            {
                return;
            }

            if (terrain.NothingWasPlaced)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' puts blocks in the game's own " +
                    "world and lists no rules, so nothing is placed. Add a rule, or switch the " +
                    "block off.");
                return;
            }

            System.Func<string, int> resolveTileset = BuildWorldRuleTilesetResolver(template, modName);
            CultureInfo invariant = CultureInfo.InvariantCulture;
            DimensionWorldTerrainRule[] rules = terrain.Rules;
            for (int i = 0; i < rules.Length; i++)
            {
                DimensionWorldTerrainRule rule = rules[i];
                if (rule == null)
                {
                    continue;
                }

                if (rule.LaysNothing)
                {
                    Debug.LogWarning(
                        "[Dimensions API] A terrain rule in '" + setup.DisplayName + "' names no " +
                        "block to lay, so it would match tiles and put nothing on them. It was not " +
                        "written into the mod.");
                    continue;
                }

                if (rule.MatchesTheWholeWorld)
                {
                    Debug.LogWarning(
                        "[Dimensions API] A terrain rule in '" + setup.DisplayName + "' asks nothing " +
                        "at all, so it lays '" + rule.LayBlockId + "' over every tile the game " +
                        "generates. Narrow it by biome, by material or by a flag unless that really " +
                        "is what you want.");
                }

                int tilePart = ResolveTilePartName(rule.LayTilePart);
                if (tilePart < 0)
                {
                    Debug.LogWarning(
                        "[Dimensions API] A terrain rule in '" + setup.DisplayName + "' lays the '" +
                        rule.LayTilePart + "' part of a block, which the game does not have. The " +
                        "parts are named ground, wall, water, ore, pit and so on. The rule was not " +
                        "written into the mod.");
                    continue;
                }

                int block = resolveTileset(rule.LayBlockId);
                if (block < 0)
                {
                    Debug.LogWarning(
                        "[Dimensions API] A terrain rule in '" + setup.DisplayName + "' lays '" +
                        rule.LayBlockId + "', which is neither one of this mod's blocks nor one of " +
                        "the game's. The rule was not written into the mod.");
                    continue;
                }

                builder.Append(
                    "    ExpandNullforge.WorldRules.DimensionWorldTerrainRuleRegistry.Register(");
                builder.Append(((int)rule.InBiome).ToString(invariant)).Append(", ");
                builder.Append(((int)rule.MadeOf).ToString(invariant)).Append(", ");
                builder.Append(((int)rule.OpenFloor).ToString(invariant)).Append(", ");
                builder.Append(((int)rule.HoleInTheRoof).ToString(invariant)).Append(", ");
                builder.Append(((int)rule.GreatWall).ToString(invariant)).Append(", ");
                builder.Append(((int)rule.ResourceSlot).ToString(invariant)).Append(", ");
                builder.Append(tilePart.ToString(invariant)).Append(", ");
                builder.Append(block.ToString(invariant)).AppendLine(");");
            }
        }

        /// <summary>
        /// The number behind a part-of-a-block name — wall, ground, water, ore — or -1.
        /// </summary>
        private static int ResolveTilePartName(string tilePart)
        {
            if (string.IsNullOrEmpty(tilePart))
            {
                return -1;
            }

            PugTilemap.TileType named;
            return System.Enum.TryParse(tilePart, false, out named) ? (int)named : -1;
        }

        /// <summary>
        /// A block id to a tileset number: one of this mod's blocks first, then one of the game's.
        /// </summary>
        /// <remarks>
        /// This mod's blocks are asked about first for the same reason the object resolver asks the
        /// game's names first and this one does the opposite: a custom tileset's number comes from
        /// its own qualified name and is decided in the editor, so there is no later moment to ask.
        /// A block written unqualified is tried both ways, because an author naming their own block
        /// in a rule set writes the id they typed on the block, not the namespaced form.
        /// </remarks>
        private static System.Func<string, int> BuildWorldRuleTilesetResolver(
            DimensionTemplateAsset template,
            string modName)
        {
            System.Collections.Generic.Dictionary<string, int> custom =
                new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);

            DimensionTilesetAsset[] tilesets =
                template == null ? null : template.Tilesets;
            for (int i = 0; tilesets != null && i < tilesets.Length; i++)
            {
                DimensionTilesetAsset tileset = tilesets[i];
                if (tileset == null)
                {
                    continue;
                }

                string name = tileset.TilesetName;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                custom[name] = tileset.TilesetId;
                string local = DimensionObjectNamespace.LocalIdOf(name);
                if (!string.IsNullOrEmpty(local) && !custom.ContainsKey(local))
                {
                    custom[local] = tileset.TilesetId;
                }
            }

            return delegate(string blockId)
            {
                if (string.IsNullOrEmpty(blockId))
                {
                    return -1;
                }

                int id;
                if (custom.TryGetValue(blockId, out id))
                {
                    return id;
                }

                if (!string.IsNullOrEmpty(modName) &&
                    custom.TryGetValue(
                        DimensionObjectNamespace.Qualify(modName, blockId), out id))
                {
                    return id;
                }

                PugTilemap.Tileset named;
                return System.Enum.TryParse(blockId, false, out named) ? (int)named : -1;
            };
        }

        /// <summary>
        /// Writes the fights belonging to fish this mod adds — the half of fishing that cannot be
        /// a settings file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE SPLIT IS INVISIBLE TO THE AUTHOR AND DELIBERATE HERE. One list of turns in the
        /// editor becomes two things: a fight for one of Core Keeper's fish is a settings file the
        /// game reads before any code of ours runs, and a fight for one of this mod's own fish is a
        /// registration applied while the world loads. The reason is the settings file's own shape
        /// — it names the fish by object NUMBER, and this mod's objects have no number until the
        /// mod is loaded, which is after every settings file has been read.
        /// </para>
        /// <para>
        /// So the split is decided by exactly the question the file can answer: does Core Keeper's
        /// own object list have this name? The generator writes the ones it does and passes over
        /// the rest; this writes the rest. Both ask through the same
        /// <c>DimensionWorldRulesGenerator.IsOneOfTheGamesOwnFish</c>, so neither a fish written
        /// twice nor a fish written nowhere is possible.
        /// </para>
        /// </remarks>
        private static void AppendFishFights(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            DimensionTemplateAsset template,
            string modName)
        {
            DimensionFishingTemplate fishing = setup.Fishing;
            if (!fishing.ChangesWhatFishingCatches)
            {
                return;
            }

            System.Collections.Generic.Dictionary<
                string, System.Collections.Generic.List<DimensionFishFightTurn>> byFish;
            System.Collections.Generic.List<string> order =
                DimensionWorldRulesGenerator.GroupFishFights(fishing.FishFights, out byFish);

            for (int i = 0; i < order.Count; i++)
            {
                string fishName = order[i];
                ObjectID unused;
                if (DimensionWorldRulesGenerator.IsOneOfTheGamesOwnFish(fishName, out unused))
                {
                    continue;
                }

                System.Collections.Generic.List<DimensionFishFightTurn> forFish = byFish[fishName];
                bool anyPull;
                bool anyRest;
                DimensionWorldRulesGenerator.MeasureFishFight(forFish, out anyPull, out anyRest);
                if (!anyPull || !anyRest)
                {
                    Debug.LogWarning(
                        "[Dimensions API] " +
                        DimensionWorldRulesGenerator.DescribeUnresolvableFight(
                            setup.DisplayName, fishName, anyPull));
                    continue;
                }

                if (!TemplateHasAnItemNamed(template, fishName))
                {
                    // Not a refusal: a fish can be brought in by something this check does not
                    // know how to read, and refusing would take away a working fight. The name is
                    // answered again while the world loads, and the runtime says so by name if
                    // nothing answers it there either.
                    Debug.LogWarning(
                        "[Dimensions API] '" + setup.DisplayName + "' gives '" + fishName +
                        "' a fight, but that is neither one of the game's fish nor an item this " +
                        "mod makes. If it is a typo the fight will not reach the game — check the " +
                        "spelling against the item's own id.");
                }

                string qualified = QualifyWorldRuleReference(modName, fishName);

                StringBuilder pulls = new StringBuilder();
                StringBuilder seconds = new StringBuilder();
                for (int t = 0; t < forFish.Count; t++)
                {
                    if (t > 0)
                    {
                        pulls.Append(", ");
                        seconds.Append(", ");
                    }

                    pulls.Append(forFish[t].Pulls ? "true" : "false");
                    seconds.Append(
                        forFish[t].Seconds.ToString("R", CultureInfo.InvariantCulture)).Append('f');
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionFishFightRegistry.Register(");
                builder.Append(ToCSharpString(qualified));
                builder.Append(", new bool[] { ").Append(pulls);
                builder.Append(" }, new float[] { ").Append(seconds).AppendLine(" });");
            }
        }

        /// <summary>
        /// True when this mod authors an item by that id, so a fight aimed at a name nobody makes
        /// can be pointed out while the author is still in the editor.
        /// </summary>
        /// <remarks>
        /// Items and dishes are looked at because those are what a catchable thing is made as. The
        /// answer is only ever used to warn, never to refuse, so a fish arriving some other way
        /// costs a line in the console and nothing else.
        /// </remarks>
        private static bool TemplateHasAnItemNamed(DimensionTemplateAsset template, string fishName)
        {
            if (template == null || string.IsNullOrEmpty(fishName))
            {
                return false;
            }

            string wanted = DimensionObjectNamespace.LocalIdOf(fishName);

            DimensionItemAsset[] items = template.GlobalItems;
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null && items[i].Enabled &&
                        string.Equals(DimensionObjectNamespace.LocalIdOf(items[i].ItemId), wanted,
                            System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            DimensionDishAsset[] dishes = template.GlobalDishes;
            if (dishes != null)
            {
                for (int i = 0; i < dishes.Length; i++)
                {
                    if (dishes[i] == null || !dishes[i].Enabled)
                    {
                        continue;
                    }

                    if (NamesMatch(dishes[i].DishId, wanted) ||
                        NamesMatch(dishes[i].RareItemId, wanted) ||
                        NamesMatch(dishes[i].EpicItemId, wanted))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool NamesMatch(string candidate, string wanted)
        {
            return !string.IsNullOrEmpty(candidate) &&
                string.Equals(DimensionObjectNamespace.LocalIdOf(candidate), wanted,
                    System.StringComparison.Ordinal);
        }

        private static void AppendUpgradeCosts(
            StringBuilder builder,
            DimensionGameSetupAsset setup,
            string modName)
        {
            DimensionUpgradeCostTemplate upgrading = setup.Upgrading;
            if (!upgrading.ChangesWhatUpgradingCosts)
            {
                return;
            }

            if (upgrading.NothingWasPriced)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' changes what upgrading costs " +
                    "but prices no level, so every level keeps the game's own price. Add a row or " +
                    "switch the block off.");
                return;
            }

            DimensionUpgradeCost[] costs = upgrading.Costs;
            for (int i = 0; i < costs.Length; i++)
            {
                DimensionUpgradeCost cost = costs[i];
                if (string.IsNullOrEmpty(cost.ItemId))
                {
                    continue;
                }

                if (cost.Amount == 0)
                {
                    Debug.LogWarning(
                        "[Dimensions API] '" + setup.DisplayName + "' prices level " +
                        cost.Level.ToString(CultureInfo.InvariantCulture) + " at zero '" +
                        cost.ItemId + "', which reads as free rather than as unpriced. Set an " +
                        "amount above zero, or take the row out to leave the level alone.");
                }

                // A price may be paid in one of this mod's own items, and those are namespaced the
                // way every other reference to one is. Vanilla names are left untouched, because
                // qualifying one would point the price at an item that does not exist.
                string itemName = QualifyWorldRuleReference(modName, cost.ItemId);

                builder.Append("    ExpandNullforge.WorldRules.DimensionUpgradeCostRegistry.Register(");
                builder.Append(cost.Level.ToString(CultureInfo.InvariantCulture)).Append(", ");
                builder.Append(ToCSharpString(itemName)).Append(", ");
                builder.Append(cost.Amount.ToString(CultureInfo.InvariantCulture)).AppendLine(");");
            }
        }

        private static void AppendPlayerOverrides(
            StringBuilder builder,
            DimensionGameSetupAsset setup)
        {
            DimensionPlayerTemplate player = setup.Player;
            if (!player.OverridesTheGamesPlayer)
            {
                return;
            }

            CultureInfo invariant = CultureInfo.InvariantCulture;

            builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                .Append(".RegisterTurningDelay(")
                .Append(player.TurningCatchesUpAfter.ToString("R", invariant))
                .AppendLine("f);");

            AnimationCurve drift = player.VehicleDrift;
            if (player.DriftCurveIsEmpty)
            {
                Debug.LogWarning(
                    "[Dimensions API] '" + setup.DisplayName + "' overrides the player but its " +
                    "vehicle drift curve has no points, which would stop vehicles drifting rather " +
                    "than change how they drift. The game's own curve was kept — add at least two " +
                    "points to the curve.");
            }
            else if (drift != null && drift.length > 0)
            {
                StringBuilder times = new StringBuilder();
                StringBuilder amounts = new StringBuilder();
                for (int k = 0; k < drift.length; k++)
                {
                    if (k > 0)
                    {
                        times.Append(", ");
                        amounts.Append(", ");
                    }

                    times.Append(drift[k].time.ToString("R", invariant)).Append('f');
                    amounts.Append(drift[k].value.ToString("R", invariant)).Append('f');
                }

                builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                    .Append(".RegisterVehicleDrift(new float[] { ").Append(times)
                    .Append(" }, new float[] { ").Append(amounts).AppendLine(" });");
            }

            Vector3 aim = player.AimSitsAt;
            builder.Append("    ExpandNullforge.WorldRules.DimensionPlayerOverrideRegistry")
                .Append(".RegisterAimOffset(")
                .Append(aim.x.ToString("R", invariant)).Append("f, ")
                .Append(aim.y.ToString("R", invariant)).Append("f, ")
                .Append(aim.z.ToString("R", invariant)).AppendLine("f);");
        }

        /// <summary>
        /// The qualified name for a reference to one of this mod's items, or the name untouched
        /// when it is one of Core Keeper's.
        /// </summary>
        /// <remarks>
        /// A price is a reference, not a definition, so the same rule applies as everywhere else:
        /// a vanilla name must stay bare, because qualifying it points at an item nothing owns.
        /// </remarks>
        private static string QualifyWorldRuleReference(string modName, string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(modName))
            {
                return itemId ?? string.Empty;
            }

            if (DimensionObjectBinder.Vanilla(itemId) != ObjectID.None)
            {
                return itemId;
            }

            return DimensionObjectNamespace.Qualify(modName, itemId);
        }
    }
}
