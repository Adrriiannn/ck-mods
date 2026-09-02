using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What fishing catches, and how hard each fish fights.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A CAST ASKS THE WATER, THEN THE BIOME. Core Keeper looks at the water you cast into first; if
    /// that kind of water has nothing of its own it falls back to the biome you are standing in. So
    /// a row about water changes fishing everywhere that water appears, and a row about a biome
    /// changes fishing everywhere in that biome that has no water rule of its own.
    /// </para>
    /// <para>
    /// EACH ROW REPLACES, IT DOES NOT ADD. Naming a biome or a water replaces what the game catches
    /// there outright. Two tables are named because the game keeps them apart: one for fish, one for
    /// the junk you pull up instead. BOTH are needed on every row — the game decides whether a rule
    /// exists at all by looking at its junk table, so a row naming only fish is passed over and the
    /// water goes on catching exactly what it caught before, silently.
    /// </para>
    /// <para>
    /// ONLY THE GAME'S OWN BIOMES AND WATERS CAN BE NAMED HERE. The table the game builds from these
    /// rows is a fixed row of twelve biome slots and seventy-five ground slots, addressed by number,
    /// and a biome or ground this mod invented sits far outside both. Generation says so by name
    /// rather than writing a row that would break the table when a world loads.
    /// </para>
    /// <para>
    /// A FISH'S FIGHT IS A RUN OF TURNS. Each row is one turn — the fish either pulls or rests, for
    /// so many seconds — and the rows for one fish play in the order they are listed, over and over,
    /// while the fish is on the line. A fish needs at least one pulling turn and one resting turn, or
    /// the fight never resolves.
    /// </para>
    /// <para>
    /// A FISH THIS MOD ADDS MAY HAVE ITS OWN FIGHT, unlike the two rules above. Fights reach the
    /// game by two different roads and generation picks the road per fish without being asked: one
    /// of Core Keeper's fish through a settings file it reads at start-up, one of this mod's own
    /// through an instruction applied while the world loads. A fish with no fight of its own fights
    /// the way the game's default fish does, which is what every fish did before this was written.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionFishingTemplate
    {
        [Header("Does this change what fishing catches?")]
        [Tooltip("Turn on to write your own fishing rules. Off leaves fishing exactly as the game has it.")]
        [SerializeField] private bool changesWhatFishingCatches;

        [Header("By biome")]
        [Tooltip("What is caught in a biome, when the water itself has no rule of its own.")]
        [SerializeField] private DimensionFishingInBiome[] biomes = new DimensionFishingInBiome[0];

        [Header("By water")]
        [Tooltip("What is caught in one kind of water, wherever that water appears.")]
        [SerializeField] private DimensionFishingInWater[] waters = new DimensionFishingInWater[0];

        [Header("How each fish fights")]
        [Tooltip("The turns of one fish's fight, in order. Leave empty to keep the game's own fight for every fish.")]
        [SerializeField] private DimensionFishFightTurn[] fishFights = new DimensionFishFightTurn[0];

        public bool ChangesWhatFishingCatches { get { return changesWhatFishingCatches; } }

        public DimensionFishingInBiome[] Biomes
        {
            get { return biomes ?? new DimensionFishingInBiome[0]; }
        }

        public DimensionFishingInWater[] Waters
        {
            get { return waters ?? new DimensionFishingInWater[0]; }
        }

        public DimensionFishFightTurn[] FishFights
        {
            get { return fishFights ?? new DimensionFishFightTurn[0]; }
        }

        /// <summary>Switched on with nothing in it, so fishing would be left exactly as it was.</summary>
        public bool NothingWasWritten
        {
            get
            {
                return changesWhatFishingCatches &&
                       Biomes.Length == 0 &&
                       Waters.Length == 0 &&
                       FishFights.Length == 0;
            }
        }
    }

    /// <summary>What fishing catches in one biome.</summary>
    [Serializable]
    public struct DimensionFishingInBiome
    {
        [Tooltip("The biome, named as the game names it: Slime, Larva, Stone, Nature, Sea, Desert, Crystal, Passage or Excavation.")]
        [SerializeField] private string biome;

        [Tooltip("The loot table the fish come from. One of the game's own, or one of this mod's.")]
        [SerializeField] private string fishCaught;

        [Tooltip("The loot table the junk comes from — what you pull up when it is not a fish. Required: the game reads a rule with no junk table as no rule at all.")]
        [SerializeField] private string junkCaught;

        public string Biome { get { return biome ?? string.Empty; } }

        public string FishCaught { get { return fishCaught ?? string.Empty; } }

        public string JunkCaught { get { return junkCaught ?? string.Empty; } }
    }

    /// <summary>What fishing catches in one kind of water.</summary>
    [Serializable]
    public struct DimensionFishingInWater
    {
        [Tooltip("The ground the water sits on, named as the game names it: Dirt, Stone, Nature, Sea, Desert, Crystal, Passage, Lava, Mold.")]
        [SerializeField] private string waterGround;

        [Tooltip("The loot table the fish come from. One of the game's own, or one of this mod's.")]
        [SerializeField] private string fishCaught;

        [Tooltip("The loot table the junk comes from — what you pull up when it is not a fish. Required: the game reads a rule with no junk table as no rule at all.")]
        [SerializeField] private string junkCaught;

        public string WaterGround { get { return waterGround ?? string.Empty; } }

        public string FishCaught { get { return fishCaught ?? string.Empty; } }

        public string JunkCaught { get { return junkCaught ?? string.Empty; } }
    }

    /// <summary>One turn of one fish's fight on the line.</summary>
    [Serializable]
    public struct DimensionFishFightTurn
    {
        [Tooltip("The fish. One of the game's own, named as the game names it, or an item this mod makes.")]
        [SerializeField] private string fish;

        [Tooltip("On for a turn where the fish pulls, off for a turn where it rests.")]
        [SerializeField] private bool pulls;

        [Tooltip("How long this turn lasts, in seconds.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        public string Fish { get { return fish ?? string.Empty; } }

        public bool Pulls { get { return pulls; } }

        public float Seconds { get { return seconds < 0f ? 0f : seconds; } }
    }
}
