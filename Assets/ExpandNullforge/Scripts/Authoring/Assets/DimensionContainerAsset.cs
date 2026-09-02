using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// How big a container is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MEASURED, NOT GUESSED. Every one of the 216 prefabs in Core Keeper that carries an
    /// <c>InventoryAuthoring</c> was read from <c>Assets/GameObject/*.prefab</c>. Of the chests, all
    /// 26 ordinary ones are <c>sizeX 6, sizeY 3</c> and all 42 boss ones are <c>sizeX 12, sizeY 3</c>
    /// — no chest in the game is any other shape. The wiki says the same thing in player words:
    /// 18 slots and 36 slots, "single-wide" and "double-wide", which is where these names come from.
    /// </para>
    /// <para>
    /// The grid is literal, not a slot budget. <c>InventoryHandler</c> sets
    /// <c>columns = inventoryBuffer.sizeX</c> with no cap, and <c>InventoryUI</c> lays out
    /// <c>visibleRows = ceil(size / columns)</c> — so a container 12 across draws 12 across.
    /// </para>
    /// <para>
    /// <see cref="SingleSlot"/> and <see cref="Custom"/> are not invented conveniences: 93 vanilla
    /// containers are 1x1 (pedestals, incubators, spawner platforms, the locked chests) and vanilla
    /// uses 15 distinct grids in all — 3x1 aquariums and planter boxes, 2x2 tables, 3x3 merchants,
    /// 1x2 cooking pots, 4x2 ruins tables, 5x1 for the Core itself.
    /// </para>
    /// </remarks>
    public enum DimensionContainerSize
    {
        /// <summary>A chest: 6 across, 3 down — 18 slots. Every ordinary chest in the game.</summary>
        SingleWide = 0,

        /// <summary>A double-wide chest: 12 across, 3 down — 36 slots. Every boss chest in the game.</summary>
        DoubleWide = 1,

        /// <summary>One slot, for a pedestal, a socket, or a lock that takes a key.</summary>
        SingleSlot = 2,

        /// <summary>A grid sized to exactly what it is meant to hold.</summary>
        Custom = 100
    }

    /// <summary>
    /// How a container gets into the world.
    /// </summary>
    /// <remarks>
    /// This is not bookkeeping — it decides whether the container is allowed to be unbreakable. A
    /// player who crafts an unbreakable chest and places it can never take it back: it holds that tile
    /// in their base forever, with no tool and no recourse. A chest a dungeon puts down has no such
    /// problem, because the player never chose where it went and never spent anything on it.
    /// </remarks>
    public enum DimensionContainerOrigin
    {
        /// <summary>Players craft it at a station and place it wherever they like.</summary>
        CraftedByPlayers = 0,

        /// <summary>It only ever appears where a scene or dungeon puts it.</summary>
        PlacedByTheWorld = 1
    }

    /// <summary>One rule about what a container's slots will accept.</summary>
    /// <remarks>
    /// Core Keeper enforces two limits on these in <c>InventoryAuthoring.OnValidate</c>, so they are
    /// worth knowing before authoring: at most <b>7</b> named items per rule, and a rule may name
    /// items <b>or</b> tags but never both — setting both silently clears the tags.
    /// </remarks>
    [Serializable]
    public sealed class DimensionContainerSlotRule
    {
        [Tooltip("Apply to every slot rather than just the first.")]
        [SerializeField] private bool appliesToAllSlots = true;

        [Tooltip("Only accept items with these category tags. Leave empty for no tag restriction.")]
        [SerializeField] private string[] acceptsCategoryTags = new string[0];

        [Tooltip("Only accept these exact items (at most 7), the game's or your own. Leave empty for no item restriction.")]
        [SerializeField] private string[] acceptsItemIds = new string[0];

        [Tooltip("Refuse legendary items even when they would otherwise fit.")]
        [SerializeField] private bool denyLegendary;

        [Tooltip("Explain the restriction in the tooltip rather than only refusing silently.")]
        [SerializeField] private bool showHint = true;

        /// <summary>Core Keeper's own cap on named items in a single slot rule.</summary>
        /// <remarks>
        /// <c>InventoryAuthoring.OnValidate</c> truncates the list past this and logs
        /// "You should probably use object tags instead."
        /// </remarks>
        public const int MaxAcceptedItemIds = 7;

        public bool AppliesToAllSlots
        {
            get { return appliesToAllSlots; }
        }

        public string[] AcceptsCategoryTags
        {
            get { return acceptsCategoryTags ?? new string[0]; }
        }

        public string[] AcceptsItemIds
        {
            get { return acceptsItemIds ?? new string[0]; }
        }

        public bool DenyLegendary
        {
            get { return denyLegendary; }
        }

        public bool ShowHint
        {
            get { return showHint; }
        }

        /// <summary>Whether this rule actually restricts anything.</summary>
        /// <remarks>
        /// A rule that accepts everything is worse than no rule: the game still evaluates it on every
        /// item moved, and the player sees a restriction hint on a container that has none.
        /// </remarks>
        public bool RestrictsAnything
        {
            get { return AcceptsCategoryTags.Length > 0 || AcceptsItemIds.Length > 0 || DenyLegendary; }
        }

        /// <summary>Whether this rule names both items and tags, which the game will not honour.</summary>
        public bool NamesBothItemsAndTags
        {
            get { return AcceptsItemIds.Length > 0 && AcceptsCategoryTags.Length > 0; }
        }
    }

    /// <summary>
    /// Something that holds items: a chest, a stash, a pedestal, a lock that takes a key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS IS FOR. Core Keeper builds a container out of a dozen separate authoring components —
    /// an inventory grid here, a placement footprint there, health and damage reduction somewhere else,
    /// and a separate one again for what happens when a particular item is put inside. Every one of
    /// those is a knob worth having, and none of them is a question a person actually asks. The
    /// questions people ask are: how big is it, can it be broken, what fits in it, what does it look
    /// like, and what happens when something is put in. This asset asks those, and writes the dozen
    /// components.
    /// </para>
    /// <para>
    /// WHAT THIS DOES NOT DECIDE: what is inside when it appears. A chest is empty when it is crafted —
    /// that is what a chest IS — and a chest that arrives already holding something is a placement, not
    /// a kind of chest. The prefabs bear this out exactly: of the 216 containers in the game, 215 ship
    /// empty. The single exception is <c>MoldChestEntity_PuzzleGNature1T2Dynamos_55</c>, a per-scene
    /// variant of the ordinary mold chest carrying loot table 460 — Core Keeper's own way of saying
    /// "contents belong to the placement". So contents live on the scene object that places one.
    /// </para>
    /// <para>
    /// Nothing is hidden by simplifying. Every field maps onto something the game reads, and the ones
    /// that go deeper — slot rules, the mining threshold, the unlock reaction — are still fully
    /// reachable. The simplification is in the SHAPE of the question, not in the range of answers.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Container")]
    public sealed class DimensionContainerAsset : ScriptableObject
    {
        /// <summary>Hits an ordinary Core Keeper chest takes to break.</summary>
        /// <remarks>
        /// Every chest prefab in the game authors <c>maxHealth 2</c> with
        /// <c>DamageReductionAuthoring.maxDamagePerHit 1</c>, so each hit removes exactly one point
        /// however strong the tool is. Two hits, with anything. This is the vanilla default and the
        /// default here.
        /// </remarks>
        public const int VanillaChestHitsToBreak = 2;

        [Header("Identity")]
        [SerializeField] private string containerId = "container";

        [Tooltip("What players see it called.")]
        [SerializeField] private string displayName = "Chest";

        [TextArea(2, 4)]
        [SerializeField] private string description = string.Empty;

        [Tooltip("Its rarity colour in the world and in tooltips.")]
        [SerializeField] private string rarityId = string.Empty;

        [Tooltip("The label it already carries when placed, floating above it. A player can " +
                 "rename it in the box at the top of its window. Blank for no label, which is what " +
                 "the game's own chests do.")]
        [TextArea(1, 3)]
        [SerializeField] private string labelItComesWith = string.Empty;

        [Header("Look")]
        // There is deliberately no "borrow a vanilla container's art" field. Core Keeper resolves
        // an object's world body at CONVERSION time — GraphicalObjectConversion reads
        // ObjectAuthoring.graphicalPrefab and bakes it into GraphicalObjectPrefabCD — so it cannot
        // be pointed at another object's body afterwards, and the mod SDK ships none of the game's
        // prefabs for it to be pointed at beforehand. The field existed for two years and was
        // never once read; a container draws what is authored here.
        [Tooltip("The picture of it standing in the world.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Its icon in inventories. Falls back to the sprite when empty.")]
        [SerializeField] private Sprite icon;

        [Header("Capacity")]
        [Tooltip("Single-wide is a chest (18 slots). Double-wide is a boss chest (36).")]
        [SerializeField] private DimensionContainerSize size = DimensionContainerSize.SingleWide;

        [Tooltip("Slots across, when the size is Custom. This is literally how wide the window draws.")]
        [Min(1)]
        [SerializeField] private int customSlotsAcross = 6;

        [Tooltip("Slots down, when the size is Custom.")]
        [Min(1)]
        [SerializeField] private int customSlotsDown = 3;

        [Tooltip("Extra slots this can be upgraded to hold. No vanilla container uses this — only the player's own bag does.")]
        [Min(0)]
        [SerializeField] private int upgradeableExtraSlots;

        [Header("If it grows with the player")]
        [Tooltip("It is a pouch carried in the inventory rather than a chest placed in the world.")]
        [SerializeField] private bool isAPouch;

        [Tooltip("It holds the same number of slots at every level rather than growing. 0 means it grows.")]
        [Min(0)]
        [SerializeField] private int sameSizeAtEveryLevel;

        [Tooltip("It only accepts items carrying one of these category tags. Empty accepts anything.")]
        [SerializeField] private string[] onlyAcceptsCategoryTags = new string[0];

        [Header("Rules")]
        [Tooltip("One item per slot, however stackable it normally is.")]
        [SerializeField] private bool oneItemPerSlot;

        [Tooltip("Items can be seen but not taken out. For a display case or a shrine.")]
        [SerializeField] private bool contentsAreLocked;

        [Tooltip("Players cannot put anything in. For a container that only ever dispenses.")]
        [SerializeField] private bool cannotAddItems;

        [Tooltip("Take part in the game's quick-stack and auto-sort.")]
        [SerializeField] private bool autoTransfer = true;

        [SerializeField] private DimensionContainerSlotRule[] slotRules = new DimensionContainerSlotRule[0];

        [Header("Placement")]
        [Tooltip("How many tiles it occupies on the ground. Every vanilla chest is 1 x 1.")]
        [SerializeField] private Vector2Int tileSize = Vector2Int.one;

        [Tooltip("Can be placed on water.")]
        [SerializeField] private bool canBePlacedOnWater;

        [Tooltip("Faces the way the player was looking when it was placed.")]
        [SerializeField] private bool facesPlacementDirection = true;

        [Header("Where it comes from")]
        [Tooltip("Crafted: players make it and place it themselves. Placed by the world: it only appears where a scene or dungeon puts it.")]
        [SerializeField] private DimensionContainerOrigin origin = DimensionContainerOrigin.CraftedByPlayers;

        [Header("Breaking it")]
        [Tooltip("Cannot be broken at all. Everything below stops mattering. Only available for a container the world places — a craftable one would trap the player's own tile forever.")]
        [SerializeField] private bool indestructible;

        [Tooltip("How many hits to break it. Vanilla chests take 2, from any tool.")]
        [Min(1)]
        [SerializeField] private int hitsToBreak = VanillaChestHitsToBreak;

        [Tooltip("Mining damage a hit must EXCEED to do anything. 0 = any tool works, which is what every vanilla chest does. Raise it to gate on pick tier, the way walls do.")]
        [Min(0)]
        [SerializeField] private int requiredMiningDamage;

        [Tooltip("Only a drill will break it. Vanilla uses this for ore boulders and nothing else.")]
        [SerializeField] private bool requiresDrill;

        [Tooltip("Drop what was inside when it breaks. Off means the contents are destroyed with it.")]
        [SerializeField] private bool dropsContentsWhenBroken = true;

        [Tooltip("Drop the container itself so it can be picked back up.")]
        [SerializeField] private bool dropsItselfWhenBroken = true;

        [Header("When an item is put in")]
        [Tooltip("The item that triggers it — a key, a fuse, a charm. One of the game's, or one of yours. Empty for no reaction.")]
        [SerializeField] private string reactsToItemId = string.Empty;

        [Tooltip("Switch to this sprite variation. This alone is enough for a lid that opens.")]
        [Min(0)]
        [SerializeField] private int reactionVariation;

        [Tooltip("Stop blocking the tile as well. This is how vanilla's excavation doors open, " +
            "and it is one way only: once the thing has reacted it never blocks the tile again, " +
            "even if what triggered it is taken back out.")]
        [SerializeField] private bool removeColliderOnReaction;

        [Tooltip("Become this container instead, the game's or one of yours. This is exactly how a locked chest becomes an open one.")]
        [SerializeField] private string becomesContainerId = string.Empty;

        [Tooltip("Fill what it becomes from this loot table — a random draw, rolled when it opens.")]
        [SerializeField] private string becomesLootTableId = string.Empty;

        [Tooltip("Or fill what it becomes with exactly these items, every time.")]
        [SerializeField] private DimensionSceneContainerItem[] becomesContents = new DimensionSceneContainerItem[0];

        [Tooltip("Play this effect at the moment it changes.")]
        [SerializeField] private string reactionEffectId = string.Empty;

        [Tooltip("Where it is allowed to be put down, and how it behaves as the player lines it up.")]
        [SerializeField] private DimensionPlacementRulesTemplate placementRules = new DimensionPlacementRulesTemplate();

        [Tooltip("How it reacts to a melody played near it — the ocarina system.")]
        [SerializeField] private DimensionMelodyResponseTemplate melodyResponse = new DimensionMelodyResponseTemplate();

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        [Header("Where it sits in the world")]
        [Tooltip("Its world tier and which ways its art faces. The tier drives the stat curves.")]
        [SerializeField] private DimensionObjectBasicsTemplate basics = new DimensionObjectBasicsTemplate();

        [Header("Conditions")]
        [Tooltip("What it starts affected by, and what conditions cannot touch it.")]
        [SerializeField] private DimensionInitialConditionsTemplate conditions = new DimensionInitialConditionsTemplate();

        [Tooltip("The small things it simply is, or simply does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [Tooltip("What happens when a player walks up to it and uses it.")]
        [SerializeField] private DimensionInteractionTemplate interaction =
            new DimensionInteractionTemplate();

        public string ContainerId
        {
            get { return containerId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        /// <summary>
        /// The words floating above it before any player has renamed it.
        /// </summary>
        /// <remarks>
        /// This is a different thing from <see cref="Description"/>. The description is the tooltip
        /// line the localization files carry; this is the container's own
        /// <c>DescriptionBuffer</c> — live game data a player edits from the box at the top of the
        /// chest window, and what <c>WorldLabel</c> renders into the air above it.
        /// </remarks>
        public string LabelItComesWith
        {
            get { return labelItComesWith ?? string.Empty; }
        }

        /// <summary>The picture of the container standing in the world.</summary>
        /// <remarks>
        /// Read back in game rather than baked into the body prefab: containers share one pooled
        /// body, so the generator writes this into the object's own record
        /// (<c>ObjectInfo.additionalSprites</c>) and
        /// <see cref="ExpandNullforge.Containers.DimensionContainerView"/> reads it per entity.
        /// </remarks>
        public Sprite Sprite
        {
            get { return sprite; }
        }

        /// <summary>The inventory icon, falling back to the world sprite.</summary>
        public Sprite Icon
        {
            get { return icon != null ? icon : sprite; }
        }

        public DimensionContainerSize Size
        {
            get { return size; }
        }

        /// <summary>How many slots across — which is literally how wide the window draws.</summary>
        public int SlotsAcross
        {
            get
            {
                switch (size)
                {
                    case DimensionContainerSize.SingleWide:
                        return 6;
                    case DimensionContainerSize.DoubleWide:
                        return 12;
                    case DimensionContainerSize.SingleSlot:
                        return 1;
                    default:
                        return customSlotsAcross < 1 ? 1 : customSlotsAcross;
                }
            }
        }

        /// <summary>How many slots down.</summary>
        /// <remarks>
        /// Three for both named chest sizes: Core Keeper widens a chest, it does not deepen it.
        /// </remarks>
        public int SlotsDown
        {
            get
            {
                switch (size)
                {
                    case DimensionContainerSize.SingleWide:
                    case DimensionContainerSize.DoubleWide:
                        return 3;
                    case DimensionContainerSize.SingleSlot:
                        return 1;
                    default:
                        return customSlotsDown < 1 ? 1 : customSlotsDown;
                }
            }
        }

        /// <summary>Total slots, which is what a player counts.</summary>
        /// <remarks>
        /// Matches the game's own arithmetic in <c>InventoryConverter.SetupInventory</c>:
        /// <c>maxSize = sizeX * sizeY + maxExtraSize</c>.
        /// </remarks>
        public int TotalSlots
        {
            get { return SlotsAcross * SlotsDown + UpgradeableExtraSlots; }
        }

        /// <summary>Whether it is carried rather than placed.</summary>
        public bool IsAPouch { get { return isAPouch; } }

        /// <summary>A fixed slot count for every level, or 0 when it grows with the player.</summary>
        public int SameSizeAtEveryLevel
        {
            get { return sameSizeAtEveryLevel < 0 ? 0 : sameSizeAtEveryLevel; }
        }

        public bool HasAFixedSize { get { return SameSizeAtEveryLevel > 0; } }

        /// <summary>The category tags it accepts, blanks dropped.</summary>
        public string[] OnlyAcceptsCategoryTags
        {
            get
            {
                string[] all = onlyAcceptsCategoryTags ?? new string[0];
                System.Collections.Generic.List<string> kept =
                    new System.Collections.Generic.List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (!string.IsNullOrEmpty(all[i]))
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        /// <summary>Whether any of the growing-inventory settings were touched.</summary>
        /// <remarks>
        /// The component that carries them is only added when the container upgrades, so a pouch
        /// tick or a tag restriction on a container that never grows would go nowhere.
        /// </remarks>
        public bool WantsGrowingInventorySettings
        {
            get
            {
                return isAPouch || HasAFixedSize || OnlyAcceptsCategoryTags.Length > 0;
            }
        }

        /// <summary>Extra slots an upgrade can add.</summary>
        /// <remarks>
        /// Real engine field, but worth knowing that vanilla points it at exactly one thing: the
        /// player's own inventory (<c>PlayerGhostEntity</c>, 20). No placeable container in the game
        /// sets it, so there is no vanilla example of what an "upgraded chest" looks like to copy.
        /// </remarks>
        public int UpgradeableExtraSlots
        {
            get { return upgradeableExtraSlots < 0 ? 0 : upgradeableExtraSlots; }
        }

        public bool OneItemPerSlot
        {
            get { return oneItemPerSlot; }
        }

        public bool ContentsAreLocked
        {
            get { return contentsAreLocked; }
        }

        public bool CannotAddItems
        {
            get { return cannotAddItems; }
        }

        public bool AutoTransfer
        {
            get { return autoTransfer; }
        }

        /// <summary>Only the slot rules that actually restrict something.</summary>
        /// <remarks>
        /// Filtered here rather than at generation so the dashboard and the generator agree about how
        /// many rules a container really has — an empty rule is a hint shown for no reason.
        /// </remarks>
        public DimensionContainerSlotRule[] EffectiveSlotRules
        {
            get
            {
                DimensionContainerSlotRule[] all = slotRules ?? new DimensionContainerSlotRule[0];
                int count = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].RestrictsAnything)
                    {
                        count++;
                    }
                }

                DimensionContainerSlotRule[] kept = new DimensionContainerSlotRule[count];
                int next = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].RestrictsAnything)
                    {
                        kept[next++] = all[i];
                    }
                }

                return kept;
            }
        }

        /// <summary>Whether more rules were written than there are slots to apply them to.</summary>
        /// <remarks>
        /// Core Keeper drops the overflow in <c>InventoryAuthoring.OnValidate</c>, silently as far as
        /// a player is concerned, so it is worth saying out loud before the build.
        /// </remarks>
        public bool HasMoreSlotRulesThanSlots
        {
            get { return EffectiveSlotRules.Length > TotalSlots; }
        }

        public Vector2Int TileSize
        {
            get
            {
                return new Vector2Int(
                    tileSize.x < 1 ? 1 : tileSize.x,
                    tileSize.y < 1 ? 1 : tileSize.y);
            }
        }

        public bool CanBePlacedOnWater
        {
            get { return canBePlacedOnWater; }
        }

        public bool FacesPlacementDirection
        {
            get { return facesPlacementDirection; }
        }

        public DimensionContainerOrigin Origin
        {
            get { return origin; }
        }

        /// <summary>Whether the unbreakable tick is available at all.</summary>
        /// <remarks>
        /// Only for a container the world places. Vanilla has no unbreakable container of any kind —
        /// all 216 are breakable, and the 74 prefabs carrying <c>IndestructibleAuthoring</c> are set
        /// pieces like the Core and the caveling ships — so this is deliberately beyond vanilla, and
        /// the reason to bound it is concrete: a crafted one can be placed in a player's own base and
        /// then never removed.
        /// </remarks>
        public bool CanBeIndestructible
        {
            get { return origin == DimensionContainerOrigin.PlacedByTheWorld; }
        }

        /// <summary>Whether it can be broken at all.</summary>
        public bool IsBreakable
        {
            get { return !(indestructible && CanBeIndestructible); }
        }

        /// <summary>Whether the unbreakable tick was set but cannot be honoured.</summary>
        /// <remarks>
        /// Reported rather than silently obeyed or silently ignored: the author asked for something the
        /// rule does not allow, and the honest answer is to say which of the two to change.
        /// </remarks>
        public bool IndestructibleWasRefused
        {
            get { return indestructible && !CanBeIndestructible; }
        }

        /// <summary>
        /// How many hits it takes to break, with any tool.
        /// </summary>
        /// <remarks>
        /// Written as health, with damage capped at one point per hit — which is how every breakable
        /// object in Core Keeper does it (1,166 of the 1,286 prefabs that reduce damage cap it at 1).
        /// Capping the hit rather than scaling the health is what makes a chest take two swings from a
        /// copper pick and two from a solarite one.
        /// </remarks>
        public int HitsToBreak
        {
            get { return hitsToBreak < 1 ? 1 : hitsToBreak; }
        }

        /// <summary>
        /// The mining damage a hit has to exceed before it does anything.
        /// </summary>
        /// <remarks>
        /// Core Keeper's own number, not a tier: <c>DamageReductionCD.GetDamageDealt</c> computes
        /// <c>damage - reduction</c>, so a tool at or below this does nothing at all. Worth knowing
        /// that this is a WALL mechanism, not a chest one — every chest in the game leaves it at 0,
        /// while walls climb 22 (clay), 55 (stone), 135 (scarlet), 355 (galaxite), 2419 (excavation
        /// dungeon). Reported as zero when indestructible, because the field stops meaning anything
        /// then and a leftover value would read as a threshold somebody could reach.
        /// </remarks>
        public int RequiredMiningDamage
        {
            get { return indestructible ? 0 : (requiredMiningDamage < 0 ? 0 : requiredMiningDamage); }
        }

        /// <summary>Whether only a drill will break it.</summary>
        /// <remarks>
        /// Vanilla sets this on twelve prefabs, all of them ore boulders. It is a real mechanism and
        /// worth having, but it will read to a player as "this is an ore vein", so a chest that needs
        /// a drill is a genuinely new idea rather than a variation on a familiar one.
        /// </remarks>
        public bool RequiresDrill
        {
            get { return IsBreakable && requiresDrill; }
        }

        public bool DropsContentsWhenBroken
        {
            get { return dropsContentsWhenBroken; }
        }

        public bool DropsItselfWhenBroken
        {
            get { return dropsItselfWhenBroken; }
        }

        /// <summary>The item that sets off the reaction, or empty for none.</summary>
        public string ReactsToItemId
        {
            get { return reactsToItemId ?? string.Empty; }
        }

        public int ReactionVariation
        {
            get { return reactionVariation < 0 ? 0 : reactionVariation; }
        }

        public bool RemoveColliderOnReaction
        {
            get { return ReactsToContents && removeColliderOnReaction; }
        }

        /// <summary>What it turns into once the trigger item is inside, or empty to stay itself.</summary>
        /// <remarks>
        /// This is the whole of Core Keeper's locked-chest mechanism. <c>LockedCopperChest</c> (210) is
        /// a one-slot container whose only slot accepts <c>CopperKey</c> (212); putting the key in
        /// re-instantiates it as <c>CopperChest</c> (211). The same triple exists for iron, scarlet,
        /// octarine, galaxite, solarite, relucite and the three treasure chests.
        /// </remarks>
        public string BecomesContainerId
        {
            get { return becomesContainerId ?? string.Empty; }
        }

        public bool BecomesSomethingElse
        {
            get { return ReactsToContents && !string.IsNullOrEmpty(BecomesContainerId); }
        }

        /// <summary>A loot table to fill the new container from — a random draw, rolled on opening.</summary>
        public string BecomesLootTableId
        {
            get { return becomesLootTableId ?? string.Empty; }
        }

        /// <summary>Exact items to put in the new container, every time.</summary>
        /// <remarks>
        /// The game offers both this and a loot table on the same component and honours both, so this
        /// is not a choice between two names for one thing: a table is a random draw and this is a
        /// guarantee. A puzzle room that needs three specific keys wants this; a reward chest wants
        /// the table.
        /// </remarks>
        public DimensionSceneContainerItem[] BecomesContents
        {
            get { return becomesContents ?? new DimensionSceneContainerItem[0]; }
        }

        public string ReactionEffectId
        {
            get { return reactionEffectId ?? string.Empty; }
        }

        public bool ReactsToContents
        {
            get { return !string.IsNullOrEmpty(ReactsToItemId); }
        }

        /// <summary>Whether the reaction was configured but has nothing to do.</summary>
        /// <remarks>
        /// Naming a trigger item and then leaving every outcome blank costs a check on every item
        /// moved and gives the player nothing to see for it.
        /// </remarks>
        public bool ReactionDoesNothing
        {
            get
            {
                return ReactsToContents
                    && ReactionVariation == 0
                    && !removeColliderOnReaction
                    && string.IsNullOrEmpty(BecomesContainerId)
                    && string.IsNullOrEmpty(BecomesLootTableId)
                    && BecomesContents.Length == 0
                    && string.IsNullOrEmpty(ReactionEffectId);
            }
        }

        /// <summary>Whether contents were promised for something this never becomes.</summary>
        /// <remarks>
        /// The loot table and the item list are both fields on the reaction, and the game reads them
        /// only when it re-instantiates. Filling either without naming what it becomes drops them.
        /// </remarks>
        public bool HasOrphanedReactionContents
        {
            get
            {
                bool hasContents = !string.IsNullOrEmpty(BecomesLootTableId) || BecomesContents.Length > 0;
                return hasContents && !BecomesSomethingElse;
            }
        }

        public DimensionPlacementRulesTemplate PlacementRules
        {
            get { return placementRules ?? (placementRules = new DimensionPlacementRulesTemplate()); }
        }

        public DimensionMelodyResponseTemplate MelodyResponse
        {
            get { return melodyResponse ?? (melodyResponse = new DimensionMelodyResponseTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        /// <summary>What happens when a player uses it.</summary>
        public DimensionInteractionTemplate Interaction
        {
            get { return interaction ?? (interaction = new DimensionInteractionTemplate()); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }
        /// <summary>Its world tier and which ways its art faces.</summary>
        public DimensionObjectBasicsTemplate Basics
        {
            get { return basics ?? new DimensionObjectBasicsTemplate(); }
        }

        /// <summary>What it starts affected by, and what cannot touch it.</summary>
        public DimensionInitialConditionsTemplate Conditions
        {
            get { return conditions ?? new DimensionInitialConditionsTemplate(); }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }
    }
}
