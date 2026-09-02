using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The biomes Core Keeper's own world generator works in.
    /// </summary>
    /// <remarks>
    /// These are <c>PugWorldGen.CoreKeeper.Biome</c>, which is a different and much shorter list
    /// than the biomes a player sees: it is the ten the generator's own graph can label a tile
    /// with, and the numbers are hashes rather than a count, so they are written out. A biome this
    /// mod adds cannot appear here — the generator hands the label out from a fixed ten-entry table
    /// (<c>ck-db\WorldGen.Runtime\PugWorldGen.CoreKeeper\CoreKeeperWorld.cs:32-43</c>).
    /// </remarks>
    public enum DimensionWorldGenBiome
    {
        AnyBiome = 0,
        Dirt = 1178975115,
        Clay = 69878167,
        Stone = -535531945,
        Forest = -1831433879,
        Desert = -2059347691,
        Sea = -1273337449,
        Crystal = 908712672,
        Passage = -2124009486,
        Excavation = 1687733072
    }

    /// <summary>
    /// What Core Keeper's own world generator decided a tile was made of.
    /// </summary>
    /// <remarks>
    /// <c>PugWorldGen.CoreKeeper.TileType</c>, the twenty-one labels the generator's graph can put
    /// on a tile before anything decides what block that becomes.
    /// </remarks>
    public enum DimensionWorldGenMaterial
    {
        Anything = 0,
        Pit = 339800669,
        Water = -1779147911,
        Dirt = 1178975115,
        Turf = 937056561,
        Clay = 69878167,
        Stone = -535531945,
        Sand = 762136440,
        Forest = -1831433879,
        Desert = -2059347691,
        Sea = -1273337449,
        Resource = 2051574762,
        Crystal = 908712672,
        Passage = -2124009486,
        ExplosiveWall = -1992919159,
        Excavation = 1687733072,
        ExcavationReinforced = -1915787391,
        ExcavationBorder = -1837835606,
        ExcavationVoid = -1777181242,
        ExcavationLava = -1931641002,
        ExcavationLavaBorder = 288917136
    }

    /// <summary>Which of a biome's five resource slots a tile was marked for.</summary>
    /// <remarks>
    /// Core Keeper's <c>TileTypeMapping.ResourceIndex</c>. Measured across the game's own 75 rules:
    /// the Dirt biome fills slots one and two, Clay and Stone fill one to four, Forest, Sea and
    /// Desert fill one to three, and Crystal and Excavation fill only the first. Which slots the
    /// generator actually marks in a given biome is decided inside the generator and cannot be read
    /// out of this table, so the game's own rules are the only evidence of what is in use.
    /// </remarks>
    public enum DimensionWorldGenSlot
    {
        AnySlot = 0,
        NoSlot = 1,
        First = 2,
        Second = 3,
        Third = 4,
        Fourth = 5,
        Fifth = 6
    }

    /// <summary>Whether a rule cares about one of the generator's three flags.</summary>
    /// <remarks>Core Keeper's <c>TileTypeMapping.FlagState</c>: Any, True, False.</remarks>
    public enum DimensionWorldGenFlag
    {
        DoesNotCare = 0,
        OnlyWhereItIs = 1,
        OnlyWhereItIsNot = 2
    }

    /// <summary>
    /// Where a mod's blocks and ores appear in Core Keeper's own world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE ONLY DOOR INTO THE GAME'S OWN TERRAIN. Everything else this framework generates
    /// places tiles inside a dimension of its own. Core Keeper's own caves are built by
    /// <c>SpawnProceduralTerrainSystem</c>, which turns the generator's labels into actual blocks
    /// through one table, <c>TileTypeMapping</c>, and nothing else. So a rule here is how an ore of
    /// a mod's own ends up in the walls of the Clay Caves.
    /// </para>
    /// <para>
    /// A RULE IS A QUESTION AND AN ANSWER. The question is what the generator decided about a
    /// tile — which biome, what it was made of, whether it is open floor, whether it is a hole in
    /// the roof, whether it is Great Wall, and which resource slot it was marked for. The answer is
    /// one part of one block. Every rule that matches puts its answer down, which is how the game
    /// lays a ground tile and a wall tile from the same label.
    /// </para>
    /// <para>
    /// A RULE OF YOURS BEATS THE GAME'S OWN FOR THE SAME PART OF THE SAME TILE. The rules are read
    /// into a multi-map and played back in reverse, so the earliest matching rule is written last
    /// and wins. Rules from here go in front of the game's, so naming a tile the game already fills
    /// replaces what it put there rather than fighting it. Naming a part the game leaves empty —
    /// an unused resource slot, say — simply adds.
    /// </para>
    /// <para>
    /// IT CHANGES THE WHOLE WORLD, NOT ONE DIMENSION. There is one such table and one procedural
    /// generator, so a rule written here applies everywhere Core Keeper generates ground. That is
    /// what makes it worth having and it is also the reason to be sparing with it.
    /// </para>
    /// <para>
    /// MEASURED, THE GAME'S OWN TABLE IS 75 RULES. Counted by what each rule lays, off
    /// <c>Assets/Resources/TileTypeMapping.asset</c> in the ripped assets: twenty-one lay ground,
    /// twenty lay walls, fifteen lay ore, ten lay water, six lay ancient crystal, and one each lays
    /// a pit, a hole in the roof and the Great Wall. That is all 75. Twenty-one of them — the ore
    /// and the ancient crystal — name a resource slot; twelve are keyed on the explosive-wall
    /// material rather than on a biome's own stone.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionWorldTerrainRuleTemplate
    {
        [Header("Does this put your blocks in the game's own world?")]
        [Tooltip("Turn on to add rules to the game's own terrain generator. Off leaves Core Keeper's caves exactly as they are.")]
        [SerializeField] private bool putsBlocksInTheGamesWorld;

        [Tooltip("One row per rule. Every rule that matches a tile puts its block down, so a rule per layer is normal.")]
        [SerializeField] private DimensionWorldTerrainRule[] rules = new DimensionWorldTerrainRule[0];

        public bool PutsBlocksInTheGamesWorld { get { return putsBlocksInTheGamesWorld; } }

        public DimensionWorldTerrainRule[] Rules
        {
            get { return rules ?? new DimensionWorldTerrainRule[0]; }
        }

        /// <summary>Switched on with no rules, so the game's world would be untouched.</summary>
        public bool NothingWasPlaced
        {
            get { return putsBlocksInTheGamesWorld && Rules.Length == 0; }
        }
    }

    /// <summary>One rule: what the generator decided, and what block to lay because of it.</summary>
    [Serializable]
    public sealed class DimensionWorldTerrainRule
    {
        [Tooltip("Which of the generator's own biomes this applies in.")]
        [SerializeField] private DimensionWorldGenBiome inBiome = DimensionWorldGenBiome.AnyBiome;

        [Tooltip("What the generator decided the tile was made of.")]
        [SerializeField] private DimensionWorldGenMaterial madeOf = DimensionWorldGenMaterial.Anything;

        [Tooltip("Whether the tile is open floor rather than solid rock. The game's own wall rules ask for Only where it is not.")]
        [SerializeField] private DimensionWorldGenFlag openFloor = DimensionWorldGenFlag.DoesNotCare;

        [Tooltip("Whether the tile is a hole in the roof.")]
        [SerializeField] private DimensionWorldGenFlag holeInTheRoof = DimensionWorldGenFlag.DoesNotCare;

        [Tooltip("Whether the tile belongs to the Great Wall.")]
        [SerializeField] private DimensionWorldGenFlag greatWall = DimensionWorldGenFlag.DoesNotCare;

        [Tooltip("Which of the biome's five resource slots the tile was marked for. This is how an ore gets into a biome.")]
        [SerializeField] private DimensionWorldGenSlot resourceSlot = DimensionWorldGenSlot.AnySlot;

        [Tooltip("Which part of a block to lay: ground, wall, water, ore, pit. Named as the game names it.")]
        [SerializeField] private string layTilePart = "ground";

        [Tooltip("Which block to lay it from — the game's own tilesets by name, or one of this mod's blocks by its id.")]
        [SerializeField] private string layBlockId = string.Empty;

        public DimensionWorldGenBiome InBiome { get { return inBiome; } }

        public DimensionWorldGenMaterial MadeOf { get { return madeOf; } }

        public DimensionWorldGenFlag OpenFloor { get { return openFloor; } }

        public DimensionWorldGenFlag HoleInTheRoof { get { return holeInTheRoof; } }

        public DimensionWorldGenFlag GreatWall { get { return greatWall; } }

        public DimensionWorldGenSlot ResourceSlot { get { return resourceSlot; } }

        public string LayTilePart { get { return layTilePart ?? string.Empty; } }

        public string LayBlockId { get { return layBlockId ?? string.Empty; } }

        /// <summary>
        /// A rule with nothing to lay, which would match tiles and put nothing on them.
        /// </summary>
        public bool LaysNothing
        {
            get { return string.IsNullOrEmpty(LayBlockId) || string.IsNullOrEmpty(LayTilePart); }
        }

        /// <summary>
        /// A rule that asks nothing at all, so it matches every tile in the world.
        /// </summary>
        /// <remarks>
        /// The game has two such rules and both are deliberate — the roof hole and the Great Wall —
        /// but each still narrows on a flag. A rule with no biome, no material, no flag and no slot
        /// would lay its block over every tile the generator makes, which is almost never meant.
        /// </remarks>
        public bool MatchesTheWholeWorld
        {
            get
            {
                return inBiome == DimensionWorldGenBiome.AnyBiome &&
                       madeOf == DimensionWorldGenMaterial.Anything &&
                       openFloor == DimensionWorldGenFlag.DoesNotCare &&
                       holeInTheRoof == DimensionWorldGenFlag.DoesNotCare &&
                       greatWall == DimensionWorldGenFlag.DoesNotCare &&
                       resourceSlot == DimensionWorldGenSlot.AnySlot;
            }
        }
    }
}
