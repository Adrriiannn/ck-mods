using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The four things the world does to a player on its own.
    /// </summary>
    /// <remarks>
    /// The names are Core Keeper's own <c>EnvironmentEventType</c>. There are four and there can
    /// only ever be four: the system that runs them holds a fixed table of four sets of compiled
    /// function pointers, written out one by one in its own OnCreate.
    /// </remarks>
    public enum DimensionWorldEvent
    {
        /// <summary>Larvae swarm out of the ground around the player.</summary>
        LarvaSwarm = 1,

        /// <summary>Bomb scarabs swarm out of the ground around the player.</summary>
        BombScarabSwarm = 2,

        /// <summary>Omoroth's tentacles come up through the water.</summary>
        OmorothTentacles = 3,

        /// <summary>The roof falls in.</summary>
        CaveIn = 4
    }

    /// <summary>
    /// When the world does something to a player on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FOUR EVENTS, AND THE REQUIREMENTS ARE THE WHOLE OF WHAT DECIDES THEM. Every few minutes the
    /// game asks, for each event, whether the player is standing somewhere it could happen: the
    /// right biome, far enough from the core, standing on enough of the right tiles, not next to a
    /// boss, and off cooldown (<c>ck-db\EnvironmentEvents\EnvironmentEventSystem.cs:459-540</c>).
    /// Every one of those answers comes out of <c>EnvironmentEventsTable</c>, which is why editing
    /// it is how a cave-in happens in a biome this mod added.
    /// </para>
    /// <para>
    /// AN EVENT WITH NO ROW NEVER HAPPENS. The check starts by looking the event up and returning
    /// false when it is not there, so switching an event off is a matter of saying so rather than
    /// of writing an impossible requirement.
    /// </para>
    /// <para>
    /// ONLY THE GAME'S OWN BIOMES CAN BE NAMED HERE, AND THIS IS MEASURED, NOT ASSUMED. The biome
    /// the check compares against does not come from a component anything can write: it comes from
    /// <c>BiomeLookup.GetBiome</c>, which reads either a byte per sampled tile
    /// (<c>BiomeSamplesCD.GetBiome</c> is <c>(Biome)Samples[index]</c>, so it cannot express a
    /// value above 255) or a <c>FixedList512Bytes&lt;BiomeRanges&gt;</c> indexed by the biome. A
    /// biome this mod adds is numbered from 1000 up and nothing in the framework writes either of
    /// those, so naming one here would compare against a value the lookup can never return. A name
    /// that is not one of Core Keeper's own is reported and dropped rather than shipped as a rule
    /// that reaches nothing. The game's own biome names are Slime, Larva, Stone, Nature, Sea,
    /// Desert, Crystal, Passage and Excavation.
    /// </para>
    /// <para>
    /// ONE ROW PER EVENT, AND THAT IS NOT A STYLE RULE. The game builds its lookup with
    /// <c>NativeParallelHashMap.Add</c> keyed by the event, which throws on a repeat — so a second
    /// row for the same event is an exception thrown while the world is being built. Naming an
    /// event here REPLACES its row rather than adding one, and naming it twice keeps the last.
    /// </para>
    /// <para>
    /// MEASURED, THE GAME'S OWN FOUR ROWS ARE: cave-ins in biomes 1, 2, 3 and 11, at least 90 tiles
    /// from the core, needing 20 wall and 20 ground tiles of dirt, clay, stone or excavation and 70
    /// such tiles in total, with at most 5 objects nearby; larva swarms in biome 2 on 30 clay wall
    /// tiles; tentacles in biome 7 on 50 water tiles of the sea tileset; bomb scarabs in biome 8 on
    /// 30 ground tiles of the desert tileset. None of the four ignores the global cooldown, none
    /// overrides its own, and none is allowed to start near a boss.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionEnvironmentEventTemplate
    {
        [Header("Does this change when the world acts on its own?")]
        [Tooltip("Turn on to change where and when the four world events happen. Off leaves all four exactly as the game has them.")]
        [SerializeField] private bool changesWhenTheWorldActs;

        [Tooltip("One row per event you are changing. An event nobody names keeps the game's own requirements.")]
        [SerializeField] private DimensionWorldEventRule[] events = new DimensionWorldEventRule[0];

        public bool ChangesWhenTheWorldActs { get { return changesWhenTheWorldActs; } }

        public DimensionWorldEventRule[] Events
        {
            get { return events ?? new DimensionWorldEventRule[0]; }
        }

        /// <summary>Switched on with no rows, so every event would keep the game's own rules.</summary>
        public bool NothingWasChanged
        {
            get { return changesWhenTheWorldActs && Events.Length == 0; }
        }
    }

    /// <summary>Where and when one world event may happen.</summary>
    [Serializable]
    public sealed class DimensionWorldEventRule
    {
        [Tooltip("Which of the four events this row is about.")]
        [SerializeField] private DimensionWorldEvent worldEvent = DimensionWorldEvent.CaveIn;

        [Tooltip("Turn off to stop this event happening anywhere at all. The game has no other switch for it.")]
        [SerializeField] private bool canHappen = true;

        [Tooltip("The biomes it may happen in, and nowhere else. Only the game's own: Slime, Larva, Stone, Nature, Sea, Desert, Crystal, Passage, Excavation. A biome this mod adds cannot be named here, because the game works out which biome a player is standing in from its own radial ranges rather than from anything a mod can write.")]
        [SerializeField] private string[] biomeIds = new string[0];

        [Tooltip("How far from the core the player has to be, in tiles.")]
        [Min(0f)]
        [SerializeField] private float tilesFromTheCore;

        [Tooltip("How many things may be standing nearby and still let it happen. Zero means it does not care.")]
        [Min(0)]
        [SerializeField] private int mostThingsNearby;

        [Tooltip("How many of the tiles below have to be around the player in total, counting all the rows together.")]
        [Min(0)]
        [SerializeField] private int tilesNeededInTotal;

        [Tooltip("What the ground around the player has to be made of. Every row has to be satisfied on its own as well.")]
        [SerializeField] private DimensionWorldEventGround[] ground = new DimensionWorldEventGround[0];

        [Tooltip("It may happen even while the shared cooldown between all four events is still running.")]
        [SerializeField] private bool ignoresTheSharedCooldown;

        [Tooltip("It may start while a boss is nearby. None of the game's own four may.")]
        [SerializeField] private bool mayStartNearABoss;

        [Tooltip("Set its own cooldown rather than taking the game's 1800 to 3600 seconds.")]
        [SerializeField] private bool setsItsOwnCooldown;

        [Tooltip("Shortest wait before it may happen again, in seconds.")]
        [Min(0f)]
        [SerializeField] private float cooldownShortestSeconds = 1800f;

        [Tooltip("Longest wait before it may happen again, in seconds.")]
        [Min(0f)]
        [SerializeField] private float cooldownLongestSeconds = 3600f;

        public DimensionWorldEvent WorldEvent { get { return worldEvent; } }

        public bool CanHappen { get { return canHappen; } }

        public string[] BiomeIds { get { return biomeIds ?? new string[0]; } }

        public float TilesFromTheCore { get { return tilesFromTheCore < 0f ? 0f : tilesFromTheCore; } }

        public int MostThingsNearby { get { return mostThingsNearby < 0 ? 0 : mostThingsNearby; } }

        public int TilesNeededInTotal { get { return tilesNeededInTotal < 0 ? 0 : tilesNeededInTotal; } }

        public DimensionWorldEventGround[] Ground
        {
            get { return ground ?? new DimensionWorldEventGround[0]; }
        }

        public bool IgnoresTheSharedCooldown { get { return ignoresTheSharedCooldown; } }

        public bool MayStartNearABoss { get { return mayStartNearABoss; } }

        public bool SetsItsOwnCooldown { get { return setsItsOwnCooldown; } }

        public float CooldownShortestSeconds
        {
            get { return cooldownShortestSeconds < 0f ? 0f : cooldownShortestSeconds; }
        }

        public float CooldownLongestSeconds
        {
            get
            {
                float longest = cooldownLongestSeconds < 0f ? 0f : cooldownLongestSeconds;
                return longest < CooldownShortestSeconds ? CooldownShortestSeconds : longest;
            }
        }

        /// <summary>
        /// Left able to happen but given no biome, so it can never happen anywhere.
        /// </summary>
        /// <remarks>
        /// The biome test is a loop over the row's own list, so an empty list matches nothing. That
        /// is a silent way to switch an event off, and it deserves saying out loud rather than
        /// leaving a creator to wonder why their cave-ins stopped.
        /// </remarks>
        public bool CanNeverHappenAnywhere
        {
            get { return canHappen && BiomeIds.Length == 0; }
        }
    }

    /// <summary>One kind of tile a world event needs around the player.</summary>
    [Serializable]
    public struct DimensionWorldEventGround
    {
        [Tooltip("Which part of a block: its wall, its ground, its water, and so on. Named as the game names it — wall, ground, water, pit.")]
        [SerializeField] private string tilePart;

        [Tooltip("Which blocks count — the game's own tilesets by name, or one of this mod's blocks by its id.")]
        [SerializeField] private string[] blockIds;

        [Tooltip("How many such tiles have to be around the player.")]
        [Min(0)]
        [SerializeField] private int howMany;

        public string TilePart { get { return tilePart ?? string.Empty; } }

        public string[] BlockIds { get { return blockIds ?? new string[0]; } }

        public int HowMany { get { return howMany < 0 ? 0 : howMany; } }
    }
}
