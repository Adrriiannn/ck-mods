using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The machines that do work on their own, and the odds and ends that go with them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Belts, arms and things machines pull from were already built. This is the half that goes and
    /// <i>does</i> something: a drill that chews through wall, an automated miner with a shaped bite,
    /// a filter that decides what a belt will carry, an anvil, and the material a repair station
    /// reaches for first.
    /// </para>
    /// <para>
    /// THE MINER'S BITE IS A SHAPE, not a radius. <c>damagePositions</c> is a list of tile offsets
    /// from the machine, so a miner can chew a one-tile hole, a three-wide seam, or an L. That is
    /// why it is asked about as a pattern rather than a size.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionMachineRolesTemplate
    {
        [Header("Digging on its own")]
        [Tooltip("It is a drill — it bores through wall in front of it.")]
        [SerializeField] private bool isADrill;

        [Tooltip("It mines on its own, chewing the tiles listed below.")]
        [SerializeField] private bool isAnAutomatedMiner;

        [Tooltip("Which tiles around it get chewed, as offsets. (1,0) is one tile to the right.")]
        [SerializeField] private Vector2Int[] chewsTilesAt = new Vector2Int[0];

        [Tooltip("How much damage each bite does.")]
        [Min(0)]
        [SerializeField] private int bitesFor;

        [Tooltip("How long between bites.")]
        [Min(0f)]
        [SerializeField] private float secondsBetweenBites = 1f;

        [Header("Moving things about")]
        [Tooltip("It filters what the belts around it will carry.")]
        [SerializeField] private bool filtersWhatBeltsCarry;

        [Header("Workshop roles")]
        [Tooltip("It is an anvil — the station that reforges and reinforces.")]
        [SerializeField] private bool isAnAnvil;

        [Tooltip("Repair stations reach for this material before anything else.")]
        [SerializeField] private bool isPreferredForRepairs;

        [Tooltip("It is a command tool — it tells minions where to go.")]
        [SerializeField] private bool commandsMinions;

        public bool IsADrill { get { return isADrill; } }

        public bool IsAnAutomatedMiner { get { return isAnAutomatedMiner; } }

        public Vector2Int[] ChewsTilesAt { get { return chewsTilesAt ?? new Vector2Int[0]; } }

        public int BitesFor { get { return bitesFor < 0 ? 0 : bitesFor; } }

        public float SecondsBetweenBites
        {
            get { return secondsBetweenBites < 0f ? 0f : secondsBetweenBites; }
        }

        public bool FiltersWhatBeltsCarry { get { return filtersWhatBeltsCarry; } }

        public bool IsAnAnvil { get { return isAnAnvil; } }

        public bool IsPreferredForRepairs { get { return isPreferredForRepairs; } }

        public bool CommandsMinions { get { return commandsMinions; } }

        /// <summary>Whether it mines with no tiles listed to chew.</summary>
        public bool MinesNothing
        {
            get { return isAnAutomatedMiner && ChewsTilesAt.Length == 0; }
        }

        /// <summary>Whether it mines with no damage behind the bite.</summary>
        public bool BitesForNothing
        {
            get { return isAnAutomatedMiner && bitesFor <= 0; }
        }
    }

    /// <summary>
    /// Things that go off, remove ground, or pretend to be ground.
    /// </summary>
    /// <remarks>
    /// Four small vanilla components grouped because each is one idea about an object and terrain:
    /// blowing up on contact, taking its tile with it when it dies, standing in for a tile it is
    /// not, and arcing when thrown along the floor.
    /// </remarks>
    [Serializable]
    public sealed class DimensionTerrainEffectTemplate
    {
        [Header("Going off on contact")]
        [Tooltip("It explodes when something gets close enough.")]
        [SerializeField] private bool explodesOnContact;

        [Tooltip("How close something has to be.")]
        [Min(0f)]
        [SerializeField] private float explodesWithin = 1f;

        [Tooltip("How far the blast reaches.")]
        [Min(0f)]
        [SerializeField] private float blastRadius = 2f;

        [Tooltip("Flat blast damage, used when there is no tier to scale from.")]
        [Min(0)]
        [SerializeField] private int blastDamage;

        [Tooltip("How hard the blast hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float blastHitsThisHardForItsTier = 1f;

        [Tooltip("The blast lays ground down where it went off.")]
        [SerializeField] private bool blastLaysGround;

        [Tooltip("Which ground it lays. Blank for none.")]
        [SerializeField] private string blastGroundTilesetId = string.Empty;

        [Tooltip("Which kind of tile that is.")]
        [SerializeField] private PugTilemap.TileType blastGroundKind = PugTilemap.TileType.ground;

        [Header("Taking its ground with it")]
        [Tooltip("The tile beneath it is removed when it dies.")]
        [SerializeField] private bool takesItsTileWhenItDies;

        [Tooltip("Which ground gets removed. Blank for whatever is there.")]
        [SerializeField] private string removedTilesetId = string.Empty;

        [Tooltip("Which kind of tile gets removed.")]
        [SerializeField] private PugTilemap.TileType removedTileKind = PugTilemap.TileType.ground;

        [Tooltip("How likely that removal is, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float removalChance = 1f;

        [Header("Standing in for ground")]
        [Tooltip("It counts as a tile for anything that asks what the ground here is.")]
        [SerializeField] private bool countsAsATile;

        [Tooltip("Which ground it counts as.")]
        [SerializeField] private string countsAsTilesetId = string.Empty;

        [Tooltip("Which kind of tile it counts as.")]
        [SerializeField] private PugTilemap.TileType countsAsTileKind = PugTilemap.TileType.ground;

        [Header("Bouncing along the floor")]
        [Tooltip("It arcs and bounces when thrown, rather than flying flat.")]
        [SerializeField] private bool bouncesAlongTheGround;

        [Tooltip("The shape of that arc over its flight.")]
        [SerializeField] private AnimationCurve arc = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public bool ExplodesOnContact { get { return explodesOnContact; } }

        public float ExplodesWithin { get { return explodesWithin < 0f ? 0f : explodesWithin; } }

        public float BlastRadius { get { return blastRadius < 0f ? 0f : blastRadius; } }

        public int BlastDamage { get { return blastDamage < 0 ? 0 : blastDamage; } }

        public float BlastHitsThisHardForItsTier
        {
            get { return blastHitsThisHardForItsTier < 0f ? 0f : blastHitsThisHardForItsTier; }
        }

        public bool BlastLaysGround { get { return blastLaysGround; } }

        public string BlastGroundTilesetId
        {
            get { return blastGroundTilesetId ?? string.Empty; }
        }

        public PugTilemap.TileType BlastGroundKind { get { return blastGroundKind; } }

        public bool TakesItsTileWhenItDies { get { return takesItsTileWhenItDies; } }

        public string RemovedTilesetId { get { return removedTilesetId ?? string.Empty; } }

        public PugTilemap.TileType RemovedTileKind { get { return removedTileKind; } }

        public float RemovalChance
        {
            get
            {
                if (removalChance < 0f)
                {
                    return 0f;
                }

                return removalChance > 1f ? 1f : removalChance;
            }
        }

        public bool CountsAsATile { get { return countsAsATile; } }

        public string CountsAsTilesetId { get { return countsAsTilesetId ?? string.Empty; } }

        public PugTilemap.TileType CountsAsTileKind { get { return countsAsTileKind; } }

        public bool BouncesAlongTheGround { get { return bouncesAlongTheGround; } }

        public AnimationCurve Arc { get { return arc; } }

        /// <summary>Whether the blast is told to lay ground without naming any.</summary>
        public bool BlastGroundIsMissing
        {
            get { return blastLaysGround && string.IsNullOrEmpty(BlastGroundTilesetId); }
        }

        /// <summary>Whether it stands in for a tile without saying which.</summary>
        public bool StandsInForNothing
        {
            get { return countsAsATile && string.IsNullOrEmpty(CountsAsTilesetId); }
        }
    }
}
