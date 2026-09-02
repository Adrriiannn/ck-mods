using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>Where a plant is able to grow.</summary>
    /// <remarks>
    /// Two genuinely different things in Core Keeper, not two settings of one thing. A crop is planted
    /// by a player into farmed soil and stays where it was put. A root plant spreads itself across
    /// tilesets it likes, on its own timer, with nobody planting anything — which is why it carries a
    /// different component (<c>RootPlantAuthoring</c>) with spread timings instead of a seed.
    /// </remarks>
    public enum DimensionPlantGround
    {
        /// <summary>A crop: a player plants a seed in farmed soil.</summary>
        PlantedInFarmedSoil = 0,

        /// <summary>A root plant: it spreads itself across ground it likes.</summary>
        SpreadsOnItsOwn = 1
    }

    /// <summary>
    /// A plant a player can grow: seed, growing plant, ripe plant, and what it yields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE ASSET, FOUR OBJECTS. Measured from Carrock, whose family is the pattern every vanilla crop
    /// follows: <c>CarrockSeed</c> (8010), <c>CarrockPlant</c> (8011), <c>Carrock</c> (8012) and
    /// <c>CarrockRare</c> (8103). Nobody wants to author four objects and wire the references between
    /// them by hand; they want to make a plant. So this asks about the plant and the generator writes
    /// the family.
    /// </para>
    /// <para>
    /// TWO THINGS THAT LOOK LIKE OBJECTS AND ARE NOT. The ripe plant is not a separate object — it is
    /// the same <c>ObjectID</c> as the growing one, on a second prefab that sits at variation 1 with
    /// <c>currentStage</c> equal to <c>highestStage</c>. And a better version's seed and plant are
    /// not separate objects either; they are <em>variations</em> of the ordinary ones (the game's own
    /// golden crops use seed variation 1 and plant variation 2). Only the better version's
    /// <em>produce</em> gets an ObjectID of its own. Getting this wrong would mint objects Core
    /// Keeper does not expect and break the seed's own reference to what it grows into.
    /// </para>
    /// <para>
    /// Growing time is asked as a total because that is the number a designer balances against a day
    /// length; the game stores it per stage, which the generator divides out.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Plant")]
    public sealed class DimensionPlantAsset : ScriptableObject
    {
        /// <summary>Growth stages a vanilla crop uses.</summary>
        /// <remarks>Carrock, and every crop measured beside it, authors <c>highestStage</c> 2.</remarks>
        public const int VanillaGrowthStages = 2;

        /// <summary>Minutes a vanilla crop takes to be ready.</summary>
        /// <remarks>
        /// Carrock is <c>timeBetweenStages</c> 240 across 2 stages — eight minutes end to end. That is
        /// the number to move away from deliberately rather than by accident.
        /// </remarks>
        public const float VanillaMinutesToGrow = 8f;

        [Header("Identity")]
        [SerializeField] private string plantId = "plant";

        [Tooltip("What this crop is called. The growing plant is never named on screen — the game gives no name to a plant in the ground — so this names the seed, and the harvest carries the name of whatever item it gives.")]
        [SerializeField] private string displayName = "Plant";

        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [Tooltip("What the seed is called in a slot. Leave empty for the crop's name with the word Seed after it.")]
        [SerializeField] private string seedName = string.Empty;

        [Tooltip("The line under the seed's name in its tooltip. Leave empty to reuse the crop's description.")]
        [TextArea(2, 4)]
        [SerializeField] private string seedDescription = string.Empty;

        [Tooltip("Its rarity colour in the world and in tooltips.")]
        [SerializeField] private string rarityId = string.Empty;

        [Header("Look")]
        [Tooltip("The seed's icon in the inventory.")]
        [SerializeField] private Sprite seedIcon;

        [Tooltip("What it looks like in the ground: a picture for each stage, the seed in the soil, and whether it glows.")]
        [SerializeField] private DimensionPlantArtTemplate art = new DimensionPlantArtTemplate();

        [Header("Growing")]
        [Tooltip("How many stages it visibly passes through before it is ready.")]
        [Min(1)]
        [SerializeField] private int growthStages = VanillaGrowthStages;

        [Tooltip("How long from planting to ready, in minutes. A vanilla crop takes 8.")]
        [Min(0f)]
        [SerializeField] private float minutesToGrow = VanillaMinutesToGrow;

        [Tooltip("It keeps resisting damage once ripe, rather than becoming easy to trample.")]
        [SerializeField] private bool staysToughWhenRipe;

        [Tooltip("Water washes it away.")]
        [SerializeField] private bool washedAwayByWater;

        [Header("Harvest")]
        [Tooltip("What picking it gives you. An ObjectID name, or one of your own items.")]
        [SerializeField] private string produceItemId = string.Empty;

        [Tooltip("How many of that one harvest gives.")]
        [Min(1)]
        [SerializeField] private int harvestAmount = 1;

        [Tooltip("How often picking it also gives its seed back, out of a hundred. Vanilla crops give it back 75 times in 100.")]
        [Min(0f)]
        [SerializeField] private float chanceToGetTheSeedBackPercent = VanillaSeedReturnPercent;

        [Header("Better versions")]
        [Tooltip("Rarer, better versions of this crop. The first one sits where the game's golden crops sit.")]
        [SerializeField] private DimensionCropVersionTemplate[] versions =
            new DimensionCropVersionTemplate[0];

        [Header("Where it grows")]
        [SerializeField] private DimensionPlantGround ground = DimensionPlantGround.PlantedInFarmedSoil;

        [Tooltip("Tilesets it will spread across. Only used when it spreads on its own.")]
        [SerializeField] private string[] spreadsOnTilesetIds = new string[0];

        [Tooltip("Which tileset it lays down as it spreads. Blank to use the first it can grow on.")]
        [SerializeField] private string becomesTilesetId = string.Empty;

        [Tooltip("Shortest wait before it spreads again, in seconds.")]
        [Min(0f)]
        [SerializeField] private float minSpreadSeconds = 30f;

        [Tooltip("Longest wait before it spreads again, in seconds.")]
        [Min(0f)]
        [SerializeField] private float maxSpreadSeconds = 90f;

        [Tooltip("Where it is allowed to be put down, and how it behaves as the player lines it up.")]
        [SerializeField] private DimensionPlacementRulesTemplate placementRules = new DimensionPlacementRulesTemplate();

        [Tooltip("The small things it is, or does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        /// <summary>The variation the game's own golden seed sits on.</summary>
        /// <remarks>
        /// Vanilla's number. It is a variation of the ordinary seed, not an object of its own, which
        /// is why it is a constant here rather than something to author. The first authored version
        /// takes this slot so the game's own golden roll can still place it.
        /// </remarks>
        public const int RareSeedVariation = 1;

        /// <summary>The variation the game's own golden plant sits on.</summary>
        public const int RarePlantVariation = 2;

        /// <summary>
        /// The variation the already-ripe copy of the plant sits on.
        /// </summary>
        /// <remarks>
        /// Vanilla's <c>Complete…PlantEntity</c> prefabs are variation 1 as well as being at their
        /// top stage — measured on <c>CompleteCarrockPlantEntity</c>. Two prefabs of one object at
        /// variation 0 is not a thing Core Keeper's database can hold, so this cannot be zero.
        /// </remarks>
        public const int CompletePlantVariation = 1;

        /// <summary>Out of a hundred picks, how often a vanilla crop gives its seed back.</summary>
        /// <remarks>Measured across every vanilla plant prefab: <c>customLoot.chance</c> 0.75.</remarks>
        public const float VanillaSeedReturnPercent = 75f;

        public string PlantId { get { return plantId ?? string.Empty; } }

        public string DisplayName { get { return displayName ?? string.Empty; } }

        public string Description { get { return description ?? string.Empty; } }

        /// <summary>
        /// The seed's name in a slot. The seed is the only part of a crop a player ever holds, so
        /// this is the one name the crop must have; falling back to "&lt;crop&gt; Seed" matches how
        /// every one of the game's own crops reads.
        /// </summary>
        public string SeedName
        {
            get
            {
                if (!string.IsNullOrEmpty(seedName))
                {
                    return seedName;
                }

                return string.IsNullOrEmpty(DisplayName) ? string.Empty : DisplayName + " Seed";
            }
        }

        /// <summary>The seed's tooltip line, falling back to the crop's own.</summary>
        public string SeedDescription
        {
            get
            {
                return string.IsNullOrEmpty(seedDescription) ? Description : seedDescription;
            }
        }

        public string RarityId { get { return rarityId ?? string.Empty; } }

        public Sprite SeedIcon { get { return seedIcon; } }

        /// <summary>What the crop looks like in the ground, stage by stage.</summary>
        /// <remarks>
        /// Never null. A crop whose art is left entirely empty still generates — it simply grows
        /// invisibly, which the generator warns about rather than refusing, because a plant with no
        /// art is a legitimate half-finished state to save an asset in.
        /// </remarks>
        public DimensionPlantArtTemplate Art
        {
            get { return art ?? (art = new DimensionPlantArtTemplate()); }
        }

        /// <summary>
        /// How many pictures the crop needs, counting the ripe one.
        /// </summary>
        /// <remarks>
        /// Core Keeper counts a plant's stages from zero up to and including <c>highestStage</c>,
        /// so the count is one more than the number a designer thinks of as "growth stages". This
        /// is the same off-by-one that <c>ObjectInfo.additionalSprites</c> has to satisfy on every
        /// vanilla plant prefab, and getting it wrong leaves the last stage with nothing to show.
        /// </remarks>
        public int PicturesNeeded
        {
            get { return GrowthStages + 1; }
        }

        /// <summary>How many stages it passes through, which is the game's <c>highestStage</c>.</summary>
        public int GrowthStages { get { return growthStages < 1 ? 1 : growthStages; } }

        public float MinutesToGrow { get { return minutesToGrow < 0f ? 0f : minutesToGrow; } }

        /// <summary>
        /// Seconds each stage takes, which is what the game actually stores.
        /// </summary>
        /// <remarks>
        /// The total is what a designer reasons about; <c>GrowingSettings.timeBetweenStages</c> is what
        /// the game reads. Dividing here keeps the two from drifting apart, which they would if both
        /// were authored.
        /// </remarks>
        public float SecondsBetweenStages
        {
            get { return MinutesToGrow * 60f / GrowthStages; }
        }

        public bool StaysToughWhenRipe { get { return staysToughWhenRipe; } }

        public bool WashedAwayByWater { get { return washedAwayByWater; } }

        public string ProduceItemId { get { return produceItemId ?? string.Empty; } }

        /// <summary>How many of the produce one harvest gives.</summary>
        /// <remarks>
        /// Core Keeper's own <c>PlantConverter</c> writes 1 here and offers no way to change it,
        /// even though the field it writes into is raised at harvest time by the player's
        /// harvest-chance gear. The framework writes it from this instead.
        /// </remarks>
        public int HarvestAmount { get { return harvestAmount < 1 ? 1 : harvestAmount; } }

        /// <summary>Out of a hundred picks, how often the seed comes back.</summary>
        public float ChanceToGetTheSeedBackPercent
        {
            get
            {
                if (chanceToGetTheSeedBackPercent < 0f)
                {
                    return 0f;
                }

                return chanceToGetTheSeedBackPercent > 100f ? 100f : chanceToGetTheSeedBackPercent;
            }
        }

        /// <summary>The better versions, in the order they were authored.</summary>
        public DimensionCropVersionTemplate[] Versions
        {
            get { return versions ?? new DimensionCropVersionTemplate[0]; }
        }

        /// <summary>
        /// The versions that will actually be generated, in order.
        /// </summary>
        /// <remarks>
        /// Order decides identity: a version's place in this list is what fixes the variations its
        /// seed and its plant sit on, so a disabled row is dropped here rather than left in place —
        /// and reordering the list after a world exists moves crops already in the ground onto other
        /// versions. Worth saying once in the Studio rather than discovering.
        /// </remarks>
        public DimensionCropVersionTemplate[] EnabledVersions
        {
            get
            {
                DimensionCropVersionTemplate[] all = Versions;
                int count = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].Enabled)
                    {
                        count++;
                    }
                }

                DimensionCropVersionTemplate[] kept = new DimensionCropVersionTemplate[count];
                int next = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].Enabled)
                    {
                        kept[next++] = all[i];
                    }
                }

                return kept;
            }
        }

        /// <summary>Whether any better version exists, which is what turns the extra machinery on.</summary>
        public bool HasVersions
        {
            get { return EnabledVersions.Length > 0; }
        }

        public DimensionPlantGround Ground { get { return ground; } }

        public bool SpreadsOnItsOwn
        {
            get { return ground == DimensionPlantGround.SpreadsOnItsOwn; }
        }

        /// <summary>Whether a player plants it, which is what decides if it needs a seed at all.</summary>
        public bool IsPlanted
        {
            get { return ground == DimensionPlantGround.PlantedInFarmedSoil; }
        }

        public string BecomesTilesetId { get { return becomesTilesetId ?? string.Empty; } }

        public string[] SpreadsOnTilesetIds
        {
            get { return spreadsOnTilesetIds ?? new string[0]; }
        }

        public float MinSpreadSeconds { get { return minSpreadSeconds < 0f ? 0f : minSpreadSeconds; } }

        public float MaxSpreadSeconds
        {
            get { return maxSpreadSeconds < MinSpreadSeconds ? MinSpreadSeconds : maxSpreadSeconds; }
        }

        /// <summary>Whether it grows but yields nothing.</summary>
        /// <remarks>
        /// The plant still grows and can still be picked; the pick simply produces nothing, which
        /// reads to a player as the harvest being broken rather than as a decision.
        /// </remarks>
        public bool HarvestGivesNothing
        {
            get { return string.IsNullOrEmpty(ProduceItemId); }
        }

        /// <summary>Whether it was told to spread but given nowhere to spread to.</summary>
        public bool SpreadsNowhere
        {
            get { return SpreadsOnItsOwn && SpreadsOnTilesetIds.Length == 0; }
        }

        /// <summary>Whether it is ready the instant it is planted.</summary>
        /// <remarks>
        /// Zero growing time is legal and occasionally wanted for a decorative plant, but on a crop it
        /// removes the entire point of planting one, so it is worth saying out loud.
        /// </remarks>
        public bool IsReadyImmediately
        {
            get { return MinutesToGrow <= 0f; }
        }

        public DimensionPlacementRulesTemplate PlacementRules
        {
            get { return placementRules ?? (placementRules = new DimensionPlacementRulesTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        public bool Enabled { get { return enabled; } }

        public string Notes { get { return notes ?? string.Empty; } }
    }
}
