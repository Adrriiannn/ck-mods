using System.Collections.Generic;
using ExpandNullforge.Authoring;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a creator reads about one of the game's own moves before they take it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TABLE IS GENERATED; THIS IS NOT. <c>DimensionBorrowedAttacks</c> is rebuilt from the
    /// game's own prefabs by a script, so it can only carry what a prefab records: numbers, and the
    /// file name the prefab was saved under. Two things a creator needs are not in a prefab at all
    /// — what the game calls that creature on screen, and what will be missing when the move lands
    /// on a creature that is not the one it came from. Both live here, beside the table rather than
    /// inside it, so a refresh of the table never wipes them and a correction here never needs the
    /// harvest re-run.
    /// </para>
    /// <para>
    /// NAMES ARE AN OVERRIDE LIST, NOT A CENSUS. Most harvested names are already the name a player
    /// reads — Caveling Brute, Bomb Scarab, Iron Sword. Only the ones that are demonstrably not get
    /// a line here, and each group of lines says where the name was read from. A source with no
    /// line keeps the name the harvest gave it, which is honest: an invented name would be worse
    /// than a plain one.
    /// </para>
    /// </remarks>
    internal static class DimensionBorrowedAttackCatalog
    {
        /// <summary>Distance, in tiles, past which a swing was plainly authored for a big body.</summary>
        /// <remarks>
        /// Measured across the 36 close-up swings in the table: 30 of them keep every hitbox
        /// offset, half-extent and reach at 2.5 tiles or under, and the six that do not are the
        /// four hydras (5), Malugaz (4) and Omoroth's tentacle (4) — all of them creatures that
        /// cover several tiles. Three is the gap between those two groups.
        /// </remarks>
        internal const float SwingBuiltForABigBody = 3f;

        // ------------------------------------------------------------------ names ---

        private static Dictionary<string, string> playerNames;

        /// <summary>
        /// What the game calls the thing a move came from.
        /// </summary>
        /// <remarks>
        /// Returns the harvested name unchanged when nothing better is recorded, so this can be
        /// called on every source without checking first.
        /// </remarks>
        internal static string PlayerName(string harvestedSource)
        {
            if (string.IsNullOrEmpty(harvestedSource))
            {
                return string.Empty;
            }

            EnsureNames();
            string better;
            return playerNames.TryGetValue(harvestedSource, out better) ? better : harvestedSource;
        }

        /// <summary>The preset's own label with the source renamed to what the game calls it.</summary>
        /// <remarks>
        /// The preset's <c>Name</c> stays the identity — it is what <c>ByName</c> looks up and what
        /// every existing caller passes around. This only changes what is drawn.
        /// </remarks>
        internal static string Label(DimensionBorrowedAttacks.Preset preset)
        {
            if (preset == null)
            {
                return string.Empty;
            }

            string source = preset.Creature ?? string.Empty;
            string better = PlayerName(source);
            if (string.Equals(better, source, System.StringComparison.Ordinal))
            {
                return preset.Name;
            }

            // The generated label is "<source> — <what it is>", so replacing the source keeps the
            // phrase after it exactly as the table wrote it.
            if (preset.Name != null && preset.Name.StartsWith(source, System.StringComparison.Ordinal))
            {
                return better + preset.Name.Substring(source.Length);
            }

            return preset.Name;
        }

        private static void EnsureNames()
        {
            if (playerNames != null)
            {
                return;
            }

            playerNames = new Dictionary<string, string>();

            // From the enemy roster in E:\ck mods\wiki-research\wiki-digests\creatures.md, section
            // 2a, which merges both wikis' per-biome enemy tables. Each of these is a line in that
            // table naming the creature the harvested prefab is.
            playerNames["Mushroom"] = "Shrooman";
            playerNames["Mushroom Brute"] = "Shrooman Brute";
            playerNames["Electric Pest"] = "Electro-Pest";
            playerNames["Crab"] = "Bubble Crab";
            playerNames["Small Tentacle"] = "Tentacle";
            playerNames["Octopus tentacle"] = "Omoroth's Tentacle";
            playerNames["Cicada"] = "Desert Cicada";
            playerNames["Nature Cicada"] = "Floracada";
            playerNames["Poison Slime Blob"] = "Purple Slime";
            playerNames["Poison Slime Blob (drops nothing)"] = "Purple Slime (drops nothing)";
            playerNames["Slippery Slime Blob"] = "Blue Slime";
            playerNames["Lava Slime Blob"] = "Lava Slime";
            playerNames["Royal Slime Blob"] = "Royal Slime";
            playerNames["Hive Big Larva"] = "Big Hive Larva";
            playerNames["Robot Miner"] = "Geobot Miner";
            playerNames["Robot Patroller"] = "Geobot Patroller";

            // The same roster lists exactly three robots in Breaker's Reach — Geobot Miner,
            // Geobot Patroller and Geobot Scourer — and the game ships exactly three robot
            // enemies, Miner, Patroller and Swarmer. Two of the three match by name, so the third
            // is the third. Recorded as an inference because that is what it is.
            playerNames["Robot Swarmer"] = "Geobot Scourer";

            // From the boss table in wiki-digests\bosses-progression.md. The three hydras are
            // named there one per biome — Druidra roams the Wilderness, Crydra the Sunken Sea,
            // Pyrdra the Desert — which is the same split the prefabs use. Oblidra is the fourth
            // boss carrying the hydras' identical 48,028 armour pool, and its biome is the Void.
            playerNames["Nature Hydra"] = "Druidra the Wild Titan";
            playerNames["Sea Hydra"] = "Crydra the Ice Titan";
            playerNames["Desert Hydra"] = "Pyrdra the Fire Titan";
            playerNames["Void Hydra"] = "Oblidra the Void Lord";

            // Crydra is the one hydra the same table gives an ice crystal "spraying spiral shard
            // volleys", so the shard is hers.
            playerNames["Hydra's ice shard"] = "Crydra's ice shard";

            // Malugaz's fight is recorded as "CavelingThroneRoom in ShamanBossRoom maze"; Ra-Akar's
            // arena as ScarabBossScene; Omoroth's fight is the one with the tentacle triads, and
            // the tentacle is separately listed as "Omoroth's Tentacle"; the Core Commander is the
            // boss that hides behind three orbiting Core Spheres.
            playerNames["Shaman Boss"] = "Malugaz the Corrupted";
            playerNames["Scarab Boss"] = "Ra-Akar the Sand Titan";
            playerNames["Scarab Boss's bomb scarab"] = "Ra-Akar's bomb scarab";
            playerNames["Octopus Boss"] = "Omoroth the Sea Titan";
            playerNames["The Core (boss)"] = "Core Commander";
            playerNames["The Core's orb"] = "Core Sphere";
            playerNames["core Boss_Electric Projectile"] = "Core Sphere's electric orb";

            // The two Masses whose biome the prefab names: the Molten Quarry one is Igneous, the
            // Sunken Sea one is Morpha, and slippery slime is the Sunken Sea's slime.
            playerNames["Lava Slime Boss"] = "Igneous the Molten Mass";
            playerNames["Slippery Slime Boss"] = "Morpha the Aquatic Mass";

            // "AF" on a prefab is the Abiotic Factor crossover. Both wikis list its additions by
            // their shop names, which carry no prefix: Pipe Club, Quill Rifle, Electro-Pest,
            // Electro-Pet.
            playerNames["AF Pipe Club"] = "Pipe Club";
            playerNames["AF Quill Rifle"] = "Quill Rifle";
            playerNames["AF Quill Projectile"] = "Quill Rifle's shot";
            playerNames["Electric Pet"] = "Electro-Pet";

            // Two weapons the harvest spelled from the prefab rather than from the game.
            playerNames["Atlantian Worm Sword"] = "Atlantean Worm Sword";
            playerNames["Shard Club"] = "Crystal Shard Club";
        }

        /// <summary>Every source this file gives a better name to.</summary>
        /// <remarks>
        /// Here so a guard can check that each one still names something the table carries. A
        /// correction whose source has been renamed upstream would otherwise sit here doing
        /// nothing, and the old name would go back to being shown.
        /// </remarks>
        internal static string[] SourcesWithABetterName()
        {
            EnsureNames();
            string[] keys = new string[playerNames.Count];
            playerNames.Keys.CopyTo(keys, 0);
            return keys;
        }

        // ----------------------------------------------------------------- bosses ---

        private static HashSet<string> bosses;

        /// <summary>Whether a move came out of a boss fight rather than off an ordinary creature.</summary>
        internal static bool IsFromABossFight(string harvestedSource)
        {
            if (string.IsNullOrEmpty(harvestedSource))
            {
                return false;
            }

            EnsureBosses();
            return bosses.Contains(harvestedSource);
        }

        private static void EnsureBosses()
        {
            if (bosses != null)
            {
                return;
            }

            // Every source in the table that is a boss, a boss's body part, or a thing that only
            // exists inside a boss fight. Written out because a boss is not something a name can be
            // tested for.
            bosses = new HashSet<string>
            {
                "The Core (boss)",
                "The Core's orb",
                "Desert Hydra",
                "Nature Hydra",
                "Sea Hydra",
                "Void Hydra",
                "Hydra's ice shard",
                "Lava Slime Boss",
                "Slippery Slime Boss",
                "Octopus Boss",
                "Octopus tentacle",
                "Robot Boss",
                "Scarab Boss",
                "Scarab Boss's bomb scarab",
                "Shaman Boss"
            };
        }

        // ------------------------------------------------- finishing a preset off ---

        private static Dictionary<string, string> explosionForSource;

        /// <summary>
        /// Values the harvest could not carry, so that a preset still arrives whole.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A prefab records a reference to another object as a file id, and a file id means nothing
        /// outside the file it was written in — so the harvest drops every one of them. For most
        /// components that costs nothing, because the reference is a sibling on the same prefab.
        /// For the ten things in the game that blow up it costs the explosion: <c>explosionID</c>
        /// is a plain number in the prefab and never reaches the table, so without these names
        /// borrowing an explosion gives a creature a timer that produces no bang.
        /// </para>
        /// <para>
        /// The ten numbers were read out of the shipped prefabs and turned back into names against
        /// the game's own <c>ObjectID</c> list. They are named here rather than added to the
        /// generated file, which is regenerated and must not be hand-edited.
        /// </para>
        /// </remarks>
        internal static DimensionBorrowedAttacks.Value[] FinishingValues(
            DimensionBorrowedAttacks.Preset preset)
        {
            if (preset == null || preset.Kind != "Explode")
            {
                return null;
            }

            EnsureExplosions();
            string explosion;
            if (!explosionForSource.TryGetValue(preset.Creature ?? string.Empty, out explosion))
            {
                return null;
            }

            return new DimensionBorrowedAttacks.Value[]
            {
                new DimensionBorrowedAttacks.Value("@ability.targetObjectId", explosion)
            };
        }

        private static void EnsureExplosions()
        {
            if (explosionForSource != null)
            {
                return;
            }

            // Read from explosionID on each prefab in E:\ck mods\.codex\ReferenceUnity\Prefabs,
            // then resolved against Pug.Base/ObjectID.cs.
            explosionForSource = new Dictionary<string, string>
            {
                { "Amoeba Bomb", "WormSulfurExplosion" },                    // 2208
                { "Bomb Scarab", "Explosion" },                              // 2020
                { "Golden Bomb Scarab", "FieryExplosion" },                  // 2014
                { "Scarab Boss's bomb scarab", "Explosion" },                // 2020
                { "Sulfur Bud", "SmallSulfurExplosion" },                    // 2024
                { "Explosive Barrel", "DesertExplosiveWallExplosion" },      // 2212
                { "Desert explosive block", "DesertExplosiveWallExplosion" },// 2212
                { "Explosive block", "ExplosiveWallExplosion" },             // 2210
                { "Natural explosive block", "ExplosiveWallExplosion" },     // 2210
                { "Forest explosive block", "SmallPoisonExplosion" }         // 2023
            };
        }

        /// <summary>Every source this file names an explosion for.</summary>
        /// <remarks>
        /// Here for the same reason as the name list: a guard can check that each one is still a
        /// thing in the table that explodes, and that the explosion named is one the game has.
        /// </remarks>
        internal static string[] SourcesWithAnExplosionNamed()
        {
            EnsureExplosions();
            string[] keys = new string[explosionForSource.Count];
            explosionForSource.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>The explosion the game's own version of this thing sets off, or empty.</summary>
        internal static string ExplosionFor(string harvestedSource)
        {
            EnsureExplosions();
            string explosion;
            return explosionForSource.TryGetValue(harvestedSource ?? string.Empty, out explosion)
                ? explosion
                : string.Empty;
        }

        // ---------------------------------------------------------------- caveats ---

        /// <summary>
        /// What will not come across with this move, in the words a creator thinks in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY LINE HERE IS MEASURED, and the measurement is written beside it. A warning nobody
        /// checked is worse than no warning: it teaches a creator to skip the warnings.
        /// </para>
        /// <para>
        /// This is the part that has to be read BEFORE the move is taken, which is why it is a
        /// method on the catalog rather than something the applier reports afterwards. Learning
        /// that the patrol route did not come across, after generating and loading a world, is the
        /// failure this exists to prevent.
        /// </para>
        /// <para>
        /// Nothing conditional on the creature belongs here — whether its damage will survive
        /// depends on the asset, not the preset, so the picker asks that question of the asset it
        /// is open on.
        /// </para>
        /// </remarks>
        internal static List<string> Caveats(DimensionBorrowedAttacks.Preset preset)
        {
            List<string> said = new List<string>();
            if (preset == null)
            {
                return said;
            }

            string source = preset.Creature ?? string.Empty;
            string kind = preset.Kind ?? string.Empty;

            // Of the 131 shipped prefabs that carry a random walk, exactly two name a walk pattern
            // asset: RobotMinerEntity and RobotPatrollerEntity. A mod cannot point at one, because
            // it lives inside the game rather than in the mod.
            if (kind == "Wander" && (source == "Robot Miner" || source == "Robot Patroller"))
            {
                said.Add(
                    "It walks a route the game keeps in its own files, and a mod has no way to " +
                    "point at that route. The distances and the pauses arrive; the route does not, " +
                    "so it wanders instead of patrolling.");
            }

            // The only one of the 35 ranged shots in the table that names an animation clip.
            if (kind == "Ranged" && source == "Robot Miner")
            {
                said.Add(
                    "It plays a clip called \"start\" before each shot. Your creature's sheet will " +
                    "not have one under that name, so nothing plays. The shot still fires on time.");
            }

            // The one preset outside the chases that writes "starts switched off".
            if (kind == "Ranged" && source == "The Core (boss)")
            {
                said.Add(
                    "It arrives with the chase switched off at spawn, the way the Core Commander " +
                    "waits behind its shield. Clear \"starts switched off\" under the chase if " +
                    "yours should give chase from the moment it appears.");
            }

            float widest = WidestReach(preset);
            if (widest >= SwingBuiltForABigBody)
            {
                said.Add(
                    "It was built for a body several tiles across — the swing reaches " +
                    widest.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " tiles from the middle. On a one-tile sprite it lands well away from the " +
                    "creature. Shrink the reach and the hitbox afterwards if that is not what you " +
                    "want.");
            }

            if (IsFromABossFight(source))
            {
                said.Add(
                    "This is one move out of a boss fight. The move itself arrives whole. The " +
                    "phases, the helpers it calls in and the moments it can be hurt are not part " +
                    "of a move, and are set up separately on a boss.");
            }

            return said;
        }

        /// <summary>
        /// The furthest this move reaches from the middle of the creature, in tiles.
        /// </summary>
        /// <remarks>
        /// Read off the preset's own numbers rather than a list, so a refreshed table cannot leave
        /// this stale. Only the fields that place the hit in space count: an offset, a half-extent,
        /// a reach or a radius. Cooldowns and damage are not distances.
        /// </remarks>
        internal static float WidestReach(DimensionBorrowedAttacks.Preset preset)
        {
            if (preset == null || preset.Values == null)
            {
                return 0f;
            }

            float widest = 0f;
            for (int i = 0; i < preset.Values.Length; i++)
            {
                string path = preset.Values[i].Path ?? string.Empty;
                if (path.IndexOf("hitboxOffset", System.StringComparison.Ordinal) < 0 &&
                    path.IndexOf("hitboxHalf", System.StringComparison.Ordinal) < 0 &&
                    path.IndexOf("hitReach", System.StringComparison.Ordinal) < 0 &&
                    path.IndexOf("hitRadius", System.StringComparison.Ordinal) < 0 &&
                    path.IndexOf("meleeReach", System.StringComparison.Ordinal) < 0 &&
                    path.IndexOf("meleeHitRadius", System.StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                float value;
                if (!float.TryParse(
                        preset.Values[i].Text,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value))
                {
                    continue;
                }

                if (value < 0f)
                {
                    value = -value;
                }

                if (value > widest)
                {
                    widest = value;
                }
            }

            return widest;
        }

        /// <summary>
        /// The sounds in this move that no shipped sound answers to.
        /// </summary>
        /// <remarks>
        /// A sound travels as the number the prefab stored, and the number is a hash of a name. The
        /// name list the framework carries turns nearly all of them back, but a hash with no name
        /// behind it cannot be written into a field that asks for a name, so that one sound is left
        /// silent. Counting them here lets the picker say how many before the move is taken.
        /// </remarks>
        internal static int SoundsWithNoNameLeft(DimensionBorrowedAttacks.Preset preset)
        {
            if (preset == null || preset.Values == null)
            {
                return 0;
            }

            int lost = 0;
            for (int i = 0; i < preset.Values.Length; i++)
            {
                string text = preset.Values[i].Text ?? string.Empty;
                if (!text.StartsWith("#sfx:", System.StringComparison.Ordinal))
                {
                    continue;
                }

                int hash;
                if (!int.TryParse(
                        text.Substring(5),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out hash))
                {
                    lost++;
                    continue;
                }

                if (DimensionSoundNames.NameForHash(hash) == null)
                {
                    lost++;
                }
            }

            return lost;
        }

        /// <summary>
        /// Whether this move carries a damage number of its own.
        /// </summary>
        /// <remarks>
        /// Worth asking because Core Keeper's attack components recompute their damage from the
        /// area the creature is in whenever the creature has an area level, with no way to opt out
        /// per attack. A creature whose numbers come from the level curve therefore throws a
        /// borrowed damage figure away, and a creator should hear that before they take the move
        /// rather than after they play it.
        /// </remarks>
        internal static bool CarriesADamageNumber(DimensionBorrowedAttacks.Preset preset)
        {
            if (preset == null || preset.Values == null)
            {
                return false;
            }

            for (int i = 0; i < preset.Values.Length; i++)
            {
                string path = preset.Values[i].Path ?? string.Empty;
                if (path == "meleeDamage" ||
                    path == "rangedDamage" ||
                    path == "@ability.power" ||
                    path == "moreCombat.rayDamage")
                {
                    string text = preset.Values[i].Text ?? string.Empty;
                    if (text.Length > 0 && text != "0")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // ------------------------------------------------------------ what a kind is ---

        /// <summary>What one sort of borrowable move is, in a sentence.</summary>
        /// <remarks>
        /// The kinds come from the generated table, so a new sort appears in the picker on its own.
        /// One without a sentence here is offered with its own name and no description, which reads
        /// plainly enough to ship while a better line is written.
        /// </remarks>
        internal static string WhatAKindIs(string kind)
        {
            switch (kind)
            {
                case "Melee":
                    return "A swing at whatever is standing next to it.";
                case "Ranged":
                    return "A shot, and the projectile it fires.";
                case "Chase":
                    return "How it closes on something once it has noticed it.";
                case "Wander":
                    return "How far and how often it drifts about when nothing is happening.";
                case "Sounds":
                    return "What it sounds like winding up, swinging and landing a hit.";
                case "Charge":
                    return "A run in a straight line, ending in a hit or a crash.";
                case "Jump":
                    return "A leap onto whatever it is chasing.";
                case "Explode":
                    return "Blowing up, and what the blast looks like.";
                case "Ray":
                    return "A beam that sweeps around it.";
                default:
                    return string.Empty;
            }
        }
    }
}
