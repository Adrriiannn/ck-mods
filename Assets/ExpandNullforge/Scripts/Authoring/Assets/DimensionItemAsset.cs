using System.Collections.Generic;
using ExpandNullforge.Api;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject classes that become standalone .asset files MUST live in a file
    // named after the class — Unity only binds assets to the MonoScript matching the filename,
    // and a mismatch silently severs the link on the next domain reload (the asset shows as
    // "(…)" with m_Script fileID 0 and all dashboard references break).
    [CreateAssetMenu(menuName = "Dimensions API/Item")]
    public sealed class DimensionItemAsset : ScriptableObject
    {
        [SerializeField] private string itemId = "mod:item";
        [SerializeField] private string displayName = "Item";
        [Tooltip("Decides which Core Keeper authoring components the generated object needs. Anything you do not customize stays vanilla.")]
        [SerializeField] private DimensionItemArchetype archetype = DimensionItemArchetype.Material;
        [Tooltip("Which slot this goes in and what a player can do with it. Left at Not said, the " +
            "generator works one out from the template and the answers below, and says in its " +
            "report what it settled on. Answer it here when the template covers several things at " +
            "once: Tool is eleven of the game's kinds, from a shovel to a bucket.")]
        [SerializeField] private DimensionWhatItIs whatItIs = DimensionWhatItIs.NotSaid;
        // Framework identity, not a creator's choice: the three values left are the ones the
        // generator branches on, and each is set by the button that makes the item (an ordinary
        // item, a tileset's block, a portal item). The old list offered Ore, Bar, Liquid, Fish and
        // Custom as well, none of which any code ever read — being an ore is decided by the
        // tileset that lists it, and being a fish by "Can Be Caught" in the cooking block.
        [HideInInspector]
        [SerializeField] private DimensionItemKind kind = DimensionItemKind.BaseItem;
        [Tooltip("Tooltip text shown in-game. Leave blank for no description.")]
        [SerializeField] private string description = string.Empty;
        [Tooltip("Inventory icon. 16x16 PNG, Pixels Per Unit 16, Point (no) filter. This is the reliable way to give an item art; the icon id below is only used as a fallback lookup.")]
        [SerializeField] private Sprite iconSprite;
        [Tooltip("Optional small icon shown in the player's hand and while the item sits on the cursor. 10x10 PNG, Pixels Per Unit 16, Point filter. Leave empty to reuse the inventory icon.")]
        [SerializeField] private Sprite smallIconSprite;
        [SerializeField] private string iconId = string.Empty;
        [SerializeField] private string objectId = string.Empty;
        [Tooltip("Several of these share one inventory slot. The game stacks to 9999 and has no " +
            "per-item limit, so this is the whole of the choice. What it is decides it wherever " +
            "the game is unanimous — armour, jewellery, bags, melee, summoning and beam weapons and " +
            "the digging tools never stack, and all food, valuables, key items and thrown " +
            "weapons always do — and your answer stands for the ones that go both ways, like " +
            "torches, cast items, bows and off-hand pieces.")]
        [SerializeField] private bool stackable = true;

        // Set once, the first time an item written before stacking became a yes/no is loaded. Until
        // then Stackable reads the old number instead, so an item nobody has opened still behaves
        // the way it was authored.
        [HideInInspector]
        [SerializeField] private bool stackableWasMigrated;

        // Kept only to migrate the assets that carry it. Core Keeper's seam is the bool above:
        // ObjectInfo has isStackable and nothing else, and the 9999 ceiling is a constant inside
        // InventoryUtility that applies to every stackable item alike. A number here could never
        // reach the game.
        [HideInInspector]
        [SerializeField] private int maxStack = 999;

        [SerializeField] private string rarityId = string.Empty;
        [Header("Where it drops from")]
        [Tooltip("Every place this item drops. Said from the item's side; the generator inverts it into each source's loot.")]
        [SerializeField] private DimensionDropSource[] dropsFrom = new DimensionDropSource[0];

        [Header("What it does for you")]
        [Tooltip("Effects it grants while held, worn or eaten.")]
        [SerializeField] private DimensionItemEffectsTemplate effects = new DimensionItemEffectsTemplate();

        [Header("Right-click")]
        [Tooltip("What the right mouse button does with it.")]
        [SerializeField] private DimensionSecondaryUseTemplate secondaryUse = new DimensionSecondaryUseTemplate();

        [Header("If it goes off")]
        [Tooltip("What happens when it explodes. Leave unticked for anything that does not.")]
        [SerializeField] private DimensionExplosiveTemplate explosive = new DimensionExplosiveTemplate();

        [Header("Other item kinds")]
        [Tooltip("It is a potion. The game treats potions as their own thing for cooldowns and effects.")]
        [SerializeField] private bool isAPotion;

        [Header("Where it sits in the world")]
        [Tooltip("Its world tier and which ways its art faces. The tier drives the stat curves.")]
        [SerializeField] private DimensionObjectBasicsTemplate basics = new DimensionObjectBasicsTemplate();

        [Header("Durability")]
        [Tooltip("How sturdy it is, as a multiplier on the base the game derives from the item type. " +
            "2 on a helmet is 180 rather than 90. For the kinds the game has no base for, it does " +
            "nothing and the number under Durability points is the whole answer.")]
        [Min(0f)]
        [SerializeField] private float durabilityMultiplier = 1f;

        [Tooltip("How cheap it is to repair, as a multiplier.")]
        [Min(0f)]
        [SerializeField] private float repairMultiplier = 1f;

        [Tooltip("How cheap it is to reinforce, as a multiplier.")]
        [Min(0f)]
        [SerializeField] private float reinforceCostMultiplier = 1f;

        [Header("Conditions")]
        [Tooltip("What it starts affected by, and what conditions cannot touch it.")]
        [SerializeField] private DimensionInitialConditionsTemplate conditions = new DimensionInitialConditionsTemplate();

        [Header("In the off hand")]
        [Tooltip("What it does in the off hand, and what a use costs in mana.")]
        [SerializeField] private DimensionOffHandTemplate offHand = new DimensionOffHandTemplate();

        [Tooltip("The polished version of this. One of the game's items, or one of yours. For jewellery. Empty means it does not polish.")]
        [SerializeField] private string polishesInto = string.Empty;

        [Tooltip("Using it scans for this object, the game's or one of yours. Empty means it is not a scanner.")]
        [SerializeField] private string scansForObjectId = string.Empty;

        [Tooltip("It summons what it finds rather than pointing at it.")]
        [SerializeField] private bool summonsInsteadOfScanning;

        [Tooltip("It only works in this biome. Empty means anywhere.")]
        [SerializeField] private string scannerOnlyInBiome = string.Empty;

        [Header("As food")]
        [Tooltip("What part it plays in cooking. Leave as Not food for anything else.")]
        [SerializeField] private DimensionCookingTemplate cooking = new DimensionCookingTemplate();

        [Header("As a weapon")]
        [Tooltip("What makes it swing, fire or cast. Leave as Not a weapon for anything else.")]
        [SerializeField] private DimensionWeaponTemplate weapon = new DimensionWeaponTemplate();

        [Header("What it sounds like to swing")]
        [Tooltip("Its own attack sounds. 0 leaves the sound the game would have used.")]
        [SerializeField] private DimensionAttackSoundsTemplate attackSounds = new DimensionAttackSoundsTemplate();

        [Header("Worn on the character")]
        [Tooltip("How it is drawn on the player. Leave as Not worn for anything that is not armour.")]
        [SerializeField] private DimensionEquipmentSkinTemplate equipmentSkin = new DimensionEquipmentSkinTemplate();

        [Header("Rarely used")]
        [Tooltip("The same number as Durability points above, kept for items authored before that " +
            "field was wired. Fill in Durability points instead; this is only read when that is left blank.")]
        [Min(0)]
        [SerializeField] private int flatDurability;

        [Tooltip("The same again. Read only when both of the others are blank.")]
        [Min(0)]
        [SerializeField] private int flatMaxDurability;

        [Tooltip("Nudges where the icon sits in the inventory slot.")]
        [SerializeField] private Vector2 iconOffset = Vector2.zero;

        [Tooltip("Players on casual ignore this item's own cooldown.")]
        [SerializeField] private bool casualIgnoresItsCooldown;

        [Tooltip("Grammatical gender of its name per language, written as English:Neutral. Empty for none.")]
        [SerializeField] private string[] nameGendersPerLanguage = new string[0];

        [Tooltip("Its damage is magic rather than physical.")]
        [SerializeField] private bool damageIsMagic;

        [Tooltip("Its damage counts as ranged rather than melee.")]
        [SerializeField] private bool damageIsRanged;

        [Tooltip("How hard it hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float damageMultiplierForItsTier = 1f;

        [Tooltip("Which look it is placed as.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("Its look is chosen at runtime rather than fixed.")]
        [SerializeField] private bool variationIsChosenAtRuntime;

        [Tooltip("Which look it toggles to when used.")]
        [Min(0)]
        [SerializeField] private int variationItTogglesTo;

        [Tooltip("Loot it drops other than on death: as it is hit, when it is used, or by season.")]
        [SerializeField] private DimensionExtraLootTemplate extraLoot = new DimensionExtraLootTemplate();

        [Tooltip("Whether it is an instrument a player can play, or a sheet of music for one.")]
        [SerializeField] private DimensionInstrumentTemplate instrument = new DimensionInstrumentTemplate();

        [Tooltip("Other roles it holds: shrine, summoning item, firefly, grave, barrier, minion.")]
        [SerializeField] private DimensionWorldRolesTemplate worldRoles = new DimensionWorldRolesTemplate();

        [Tooltip("The small things it simply is, or simply does — one tickbox each.")]
        [SerializeField] private DimensionSimpleTraitsTemplate simpleTraits =
            new DimensionSimpleTraitsTemplate();

        [SerializeField] private bool enabled = true;
        // Framework-managed: hidden items still generate (they are real objects the runtime needs) but
        // are kept out of the dashboard's editable item lists. Used for the auto-created ground
        // counterpart of a tileset block, which is infrastructure the modder should never have to see.
        [HideInInspector]
        [SerializeField] private bool hidden = false;
        [SerializeField] private string notes = string.Empty;

        [Header("Archetype data (only the fields your archetype needs are used)")]
        [Tooltip("Loot table awarded when this object is destroyed. Required by ore, breakable, mob and boss archetypes.")]
        [SerializeField] private string lootTableId = string.Empty;
        [Tooltip("Health before the object breaks or dies. Required by breakable and creature archetypes.")]
        [SerializeField] private int healthPoints = 0;
        [Tooltip("Uses before this wears out. Leave it blank for anything the game already has a " +
            "number for — a helmet is 90, a pickaxe 800, a sword 350 at an ordinary swing — and it " +
            "will use that. Fill it in for the kinds the game has no number for: a seeder, a " +
            "fishing rod, a bag, a lantern, a necklace, anything cast. Those start at 1 otherwise.")]
        [Min(0)]
        [SerializeField] private int durabilityPoints = 0;
        [Tooltip("Damage dealt per hit. Required by the weapon archetype.")]
        [SerializeField] private int damageAmount = 0;
        [Tooltip("Seconds between uses. This is the whole of it: the swing rate, the time before " +
            "the next bite, and the number a melee or ranged weapon's durability is divided by. " +
            "Leave it blank on a consumable, tool or weapon and the game's own default goes in " +
            "instead, which is 0.4 seconds, or 0.6 for a bow, a thrown weapon, or a summoning weapon that summons a minion on right-click.")]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds = 0f;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        /// <summary>In-game tooltip text, written to the mod's localization table.</summary>
        public string Description
        {
            get { return description ?? string.Empty; }
        }

        /// <summary>
        /// Directly assigned sprite. Preferred over <c>IconId</c>, which has to be looked up by
        /// name and can silently fail to resolve.
        /// </summary>
        public Sprite IconSprite
        {
            get { return iconSprite; }
        }

        /// <summary>
        /// Small icon shown in-hand and on the cursor. Optional; when unset the generator reuses the
        /// inventory icon so the item is never left without a held sprite.
        /// </summary>
        public Sprite SmallIconSprite
        {
            get { return smallIconSprite; }
        }

        /// <summary>True when the item has art the generator can actually use.</summary>
        public bool HasVisual
        {
            get
            {
                return iconSprite != null ||
                       !string.IsNullOrEmpty(iconId) ||
                       !string.IsNullOrEmpty(objectId);
            }
        }

        /// <summary>Loot table id awarded on destruction (loot-bearing archetypes).</summary>
        public string LootTableId
        {
            get { return lootTableId ?? string.Empty; }
        }

        /// <summary>Health before breaking/dying (breakable and creature archetypes).</summary>
        public int HealthPoints
        {
            get { return Mathf.Max(0, healthPoints); }
        }

        /// <summary>Equipment durability (tool, weapon, armor archetypes).</summary>
        public int DurabilityPoints
        {
            get { return Mathf.Max(0, durabilityPoints); }
        }

        /// <summary>
        /// The uses this item was typed to have, or zero for "let the game work it out".
        /// </summary>
        /// <remarks>
        /// <para>
        /// Three fields, one number. <c>durabilityPoints</c> is the one on the item's own card and
        /// the one to fill in; <c>flatDurability</c> and <c>flatMaxDurability</c> sit under "Rarely
        /// used" and are read only when it is blank, because assets written before this was wired
        /// have their number in one of those two and losing it would silently break them.
        /// </para>
        /// <para>
        /// This is the number that ends up in <c>initialAmount</c> on the generated object, which is
        /// the only place a typed durability survives: the game recomputes <c>durability</c> from
        /// the item's type and <c>initialAmount</c> every time the prefab is imported. For a kind
        /// the game has a formula for — a helmet, a sword, a pickaxe — that formula wins and this
        /// number is not read. For the kinds it has no formula for, this is the whole of it.
        /// </para>
        /// </remarks>
        public int AuthoredDurability
        {
            get
            {
                if (durabilityPoints > 0)
                {
                    return durabilityPoints;
                }

                if (flatDurability > 0)
                {
                    return flatDurability;
                }

                return flatMaxDurability > 0 ? flatMaxDurability : 0;
            }
        }

        /// <summary>Damage per hit (weapon archetype).</summary>
        public int DamageAmount
        {
            get { return Mathf.Max(0, damageAmount); }
        }

        /// <summary>Seconds between uses, or zero for "let the game's own default go in".</summary>
        /// <remarks>
        /// One number, two fields, and the wrong one wins if this does not decide. The effects block
        /// carries a second seconds-between-uses of its own, and because the effects pass runs after
        /// this one it destroys the cooldown component whenever its own field is blank — leaving the
        /// field a creator can see setting neither the swing rate nor the durability divisor, and
        /// every generated melee weapon coming out at the game's default. The field on the item is
        /// the only one drawn, and the other is read here so an asset that stored its number there
        /// keeps it.
        /// </remarks>
        public float CooldownSeconds
        {
            get
            {
                if (cooldownSeconds > 0f)
                {
                    return cooldownSeconds;
                }

                return effects != null ? effects.LegacyCooldownSeconds : 0f;
            }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public DimensionItemKind Kind
        {
            get { return kind; }
        }

        /// <summary>Archetype that decides the generated object's authoring components.</summary>
        public DimensionItemArchetype Archetype
        {
            get { return archetype; }
        }

        /// <summary>What the game thinks this thing is, or Not said for the generator to work out.</summary>
        /// <remarks>
        /// Orthogonal to the archetype and not a replacement for it. The archetype decides which
        /// authoring components the generated object carries; this decides which slot the game puts
        /// it in, whether it can be swung, worn or eaten, and the durability it starts with. An item
        /// written before this question existed reads Not said and the generator resolves it from the
        /// archetype to exactly the value that item generated with before.
        /// </remarks>
        public DimensionWhatItIs WhatItIs
        {
            get { return whatItIs; }
        }

        /// <summary>
        /// What this item is, whether or not anybody typed it.
        /// </summary>
        /// <remarks>
        /// The item's own blocks are asked before the template is, because they are more specific:
        /// an item whose weapon block already says "swung" has said it is a melee weapon in
        /// everything but name, and a piece of armour that says it is drawn on the head has said it
        /// is a helmet. Only when neither has anything to say does the template decide.
        /// <para>
        /// This lives on the item rather than in the generator because the generator is not the
        /// only one who needs the answer. The dashboard shows what an item will become before
        /// anything is generated, and the two working it out separately is how a summary ends up
        /// promising a stack of helmets.
        /// </para>
        /// </remarks>
        public DimensionWhatItIs ResolveWhatItIs(out DimensionWhatItIsSource source)
        {
            if (whatItIs != DimensionWhatItIs.NotSaid)
            {
                source = DimensionWhatItIsSource.Said;
                return whatItIs;
            }

            DimensionWeaponTemplate weaponBlock = Weapon;
            if (weaponBlock != null && weaponBlock.IsAWeapon)
            {
                source = DimensionWhatItIsSource.HowItAttacks;

                // The beam question is asked first because a beam weapon answers "fired" as well:
                // the beam is what comes out, and the game keeps beams in their own type with their
                // own durability and their own held-down behaviour.
                if (weaponBlock.Beam != null && weaponBlock.Beam.FiresABeam)
                {
                    return DimensionWhatItIs.BeamWeapon;
                }

                if (weaponBlock.IsMelee)
                {
                    return DimensionWhatItIs.MeleeWeapon;
                }

                if (weaponBlock.IsRanged)
                {
                    return DimensionWhatItIs.RangedWeapon;
                }

                if (weaponBlock.IsCast)
                {
                    return DimensionWhatItIs.CastItem;
                }
            }

            DimensionEquipmentSkinTemplate wornBlock = EquipmentSkin;
            if (wornBlock != null)
            {
                switch (wornBlock.Slot)
                {
                    case DimensionEquipmentSkinSlot.Head:
                        source = DimensionWhatItIsSource.WhereItIsWorn;
                        return DimensionWhatItIs.Helmet;
                    case DimensionEquipmentSkinSlot.Chest:
                        source = DimensionWhatItIsSource.WhereItIsWorn;
                        return DimensionWhatItIs.ChestArmour;
                    case DimensionEquipmentSkinSlot.Legs:
                        source = DimensionWhatItIsSource.WhereItIsWorn;
                        return DimensionWhatItIs.PantsArmour;
                }
            }

            source = DimensionWhatItIsSource.ItsTemplate;
            return DimensionWhatItIsRules.DefaultFor(archetype);
        }

        /// <summary>
        /// Whether this one ends up with a pool of uses that runs down.
        /// </summary>
        /// <remarks>
        /// The only pool the template asks for and does not get is a throwing weapon's with nothing
        /// typed, which sheds it the way all seven of the game's own do.
        /// </remarks>
        public bool KeepsADurabilityPool(DimensionWhatItIs kind)
        {
            return DimensionItemArchetypeRules.Requires(
                       archetype, DimensionItemAuthoringComponents.Durability) &&
                   !(kind == DimensionWhatItIs.ThrowingWeapon && AuthoredDurability <= 0);
        }

        /// <summary>
        /// Whether several of these will share one inventory slot once generated.
        /// </summary>
        /// <remarks>
        /// The kind decides wherever the game is unanimous, the creator's own answer stands where
        /// it is not, and anything carrying a pool of uses is one per slot whatever was ticked —
        /// the same number cannot be both the uses left on one and the size of the pile.
        /// </remarks>
        public bool StacksOnceGenerated()
        {
            DimensionWhatItIs kind = ResolveWhatItIs(out _);
            if (KeepsADurabilityPool(kind))
            {
                return false;
            }

            return DimensionWhatItIsRules.StacksGiven(kind, Stackable);
        }

        /// <summary>
        /// The exact Core Keeper authoring components this item's generated object requires.
        /// The generator emits only these, and the dashboard shows only the matching fields.
        /// </summary>
        public DimensionItemAuthoringComponents RequiredComponents
        {
            get { return DimensionItemArchetypeRules.GetRequiredComponents(archetype); }
        }

        public string IconId
        {
            get { return iconId ?? string.Empty; }
        }

        public string ObjectId
        {
            get { return objectId ?? string.Empty; }
        }

        /// <summary>Whether several of these share one inventory slot.</summary>
        /// <remarks>
        /// The one thing the game reads. <c>ObjectInfo.isStackable</c> is a bool, the ceiling is a
        /// hard-coded 9999 that every stackable item shares, and nothing anywhere consults a
        /// per-item stack size — so the old number was a control over a value the game does not
        /// have. An item written before this reads its old number here, so nothing changes meaning
        /// until somebody opens it.
        /// </remarks>
        public bool Stackable
        {
            get { return stackableWasMigrated ? stackable : maxStack > 1; }
        }

        /// <summary>
        /// Turns the old stack size into the yes/no the game actually reads, once per asset.
        /// </summary>
        /// <remarks>
        /// Unity fills a field the file does not mention from its initializer, so a newly added
        /// bool would come back true for every old item — including the ones authored with a stack
        /// size of one. The migration flag is what tells "the author said yes" apart from "nobody
        /// has said anything yet".
        /// </remarks>
        private void OnValidate()
        {
            if (stackableWasMigrated)
            {
                return;
            }

            stackable = maxStack > 1;
            stackableWasMigrated = true;
        }

        public string RarityId
        {
            get { return rarityId ?? string.Empty; }
        }

        /// <summary>Every place this item drops from, with the blank entries dropped.</summary>
        public DimensionDropSource[] DropsFrom
        {
            get
            {
                DimensionDropSource[] all = dropsFrom ?? new DimensionDropSource[0];
                System.Collections.Generic.List<DimensionDropSource> kept =
                    new System.Collections.Generic.List<DimensionDropSource>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        /// <summary>Effects it grants while held, worn or eaten.</summary>
        public DimensionItemEffectsTemplate Effects
        {
            get { return effects ?? new DimensionItemEffectsTemplate(); }
        }

        /// <summary>What the right mouse button does with it.</summary>
        public DimensionSecondaryUseTemplate SecondaryUse
        {
            get { return secondaryUse ?? new DimensionSecondaryUseTemplate(); }
        }

        /// <summary>What it does in the off hand, and what a use costs.</summary>
        public DimensionOffHandTemplate OffHand
        {
            get { return offHand ?? new DimensionOffHandTemplate(); }
        }

        /// <summary>The polished version of it, or empty when it does not polish.</summary>
        public string PolishesInto { get { return polishesInto ?? string.Empty; } }

        /// <summary>How sturdy it is, as a multiplier on the game's own base for its type.</summary>
        /// <remarks>
        /// The real durability dial. <c>DurabilityAuthoring</c> computes durability as a type-based
        /// base — 90, 100, 95, 350 or 250 depending on what the item is — times this. A written
        /// maxDurability is recomputed and lost, which is why the framework has always warned about
        /// it; this is the number that survives.
        /// </remarks>
        public float DurabilityMultiplier
        {
            get { return durabilityMultiplier < 0f ? 0f : durabilityMultiplier; }
        }

        public float RepairMultiplier
        {
            get { return repairMultiplier < 0f ? 0f : repairMultiplier; }
        }

        public float ReinforceCostMultiplier
        {
            get { return reinforceCostMultiplier < 0f ? 0f : reinforceCostMultiplier; }
        }

        /// <summary>
        /// Whether wear, repair or reinforce numbers were typed on something that never wears out.
        /// </summary>
        /// <remarks>
        /// The four controls are drawn on every item, and only a tool, a weapon and a piece of
        /// armour end up with a durability pool — so on the other thirteen templates the numbers
        /// were saved and then quietly dropped at generate. Anything moved off its default counts,
        /// including a deliberate zero, because setting a multiplier to zero is a choice.
        /// </remarks>
        public bool HasWearSettingsThatWillBeIgnored
        {
            get
            {
                const float Ordinary = 1f;
                return durabilityPoints > 0
                    || flatDurability > 0
                    || !Mathf.Approximately(DurabilityMultiplier, Ordinary)
                    || !Mathf.Approximately(RepairMultiplier, Ordinary)
                    || !Mathf.Approximately(ReinforceCostMultiplier, Ordinary);
            }
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

        /// <summary>Whether the game should treat it as a potion.</summary>
        public bool IsAPotion { get { return isAPotion; } }

        /// <summary>What using it scans for, or empty when it is not a scanner.</summary>
        public string ScansForObjectId { get { return scansForObjectId ?? string.Empty; } }

        public bool IsAScanner { get { return !string.IsNullOrEmpty(ScansForObjectId); } }

        public bool SummonsInsteadOfScanning
        {
            get { return IsAScanner && summonsInsteadOfScanning; }
        }

        public string ScannerOnlyInBiome
        {
            get { return IsAScanner ? (scannerOnlyInBiome ?? string.Empty) : string.Empty; }
        }

        /// <summary>What happens when it explodes.</summary>
        public DimensionExplosiveTemplate Explosive
        {
            get { return explosive ?? new DimensionExplosiveTemplate(); }
        }

        /// <summary>What part it plays in cooking.</summary>
        public DimensionCookingTemplate Cooking
        {
            get { return cooking ?? new DimensionCookingTemplate(); }
        }

        /// <summary>What makes it swing, fire or cast.</summary>
        public DimensionWeaponTemplate Weapon
        {
            get { return weapon ?? new DimensionWeaponTemplate(); }
        }

        /// <summary>What swinging or firing it sounds like.</summary>
        public DimensionAttackSoundsTemplate AttackSounds
        {
            get { return attackSounds ?? new DimensionAttackSoundsTemplate(); }
        }

        /// <summary>How it is drawn on the player character while worn.</summary>
        public DimensionEquipmentSkinTemplate EquipmentSkin
        {
            get { return equipmentSkin ?? new DimensionEquipmentSkinTemplate(); }
        }
        /// <summary>
        /// The older of the two places a typed durability can sit. Read through
        /// <see cref="AuthoredDurability"/>, which is what the generator asks; these two are kept
        /// so an asset that has its number here still works.
        /// </summary>
        public int FlatDurability { get { return flatDurability < 0 ? 0 : flatDurability; } }

        /// <summary>The same, one field along.</summary>
        public int FlatMaxDurability { get { return flatMaxDurability < 0 ? 0 : flatMaxDurability; } }

        public Vector2 IconOffset { get { return iconOffset; } }

        public bool CasualIgnoresItsCooldown { get { return casualIgnoresItsCooldown; } }

        public string[] NameGendersPerLanguage
        {
            get { return nameGendersPerLanguage ?? new string[0]; }
        }

        public bool DamageIsMagic { get { return damageIsMagic; } }

        public bool DamageIsRanged { get { return damageIsRanged; } }

        public float DamageMultiplierForItsTier
        {
            get { return damageMultiplierForItsTier < 0f ? 0f : damageMultiplierForItsTier; }
        }

        public int Variation { get { return variation < 0 ? 0 : variation; } }

        public bool VariationIsChosenAtRuntime { get { return variationIsChosenAtRuntime; } }

        public int VariationItTogglesTo
        {
            get { return variationItTogglesTo < 0 ? 0 : variationItTogglesTo; }
        }

        public DimensionExtraLootTemplate ExtraLoot
        {
            get { return extraLoot ?? (extraLoot = new DimensionExtraLootTemplate()); }
        }

        public DimensionInstrumentTemplate Instrument
        {
            get { return instrument ?? (instrument = new DimensionInstrumentTemplate()); }
        }

        public DimensionWorldRolesTemplate WorldRoles
        {
            get { return worldRoles ?? (worldRoles = new DimensionWorldRolesTemplate()); }
        }

        /// <summary>The small things it simply is, or simply does.</summary>
        public DimensionSimpleTraitsTemplate SimpleTraits
        {
            get { return simpleTraits ?? (simpleTraits = new DimensionSimpleTraitsTemplate()); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>
        /// Whether the generated object ends up with a health pool, and so can be hit at all.
        /// </summary>
        /// <remarks>
        /// Asked here rather than worked out twice. The generator uses it to decide whether to
        /// attach <c>HealthAuthoring</c>, and the bootstrap emitter uses it to decide whether
        /// shedding loot as the object is hit can work — Core Keeper's <c>DropLootConverter</c>
        /// logs an error and abandons the shed for anything with no health, so an object without
        /// one cannot shed no matter what is typed in the box. Two copies of this rule would
        /// eventually disagree and leave a row pointing at a component the object never got.
        /// </remarks>
        public bool GetsAHealthPool
        {
            get
            {
                DimensionItemAuthoringComponents required = RequiredComponents;
                return (required & DimensionItemAuthoringComponents.Breakable) ==
                        DimensionItemAuthoringComponents.Breakable
                    || (required & DimensionItemAuthoringComponents.Creature) ==
                        DimensionItemAuthoringComponents.Creature
                    || (required & DimensionItemAuthoringComponents.Explosive) ==
                        DimensionItemAuthoringComponents.Explosive
                    || Explosive.Explodes;
            }
        }

        /// <summary>
        /// True for framework infrastructure items that must generate but should never appear in the
        /// dashboard's editable item lists (e.g. a tileset block's auto-created ground counterpart).
        /// </summary>
        public bool Hidden
        {
            get { return hidden; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        public void AddAssetReferencesTo(
            string contentPackId,
            string dimensionId,
            string zoneId,
            List<DimensionAssetReferenceDefinition> references)
        {
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "icon",
                DisplayName + " Icon",
                DimensionAssetReferenceKind.Icon,
                IconId,
                string.Empty,
                0,
                Enabled,
                Notes);
            DimensionAuthoringAssetReferenceUtility.AddReference(
                references,
                contentPackId,
                dimensionId,
                zoneId,
                ItemId,
                "object",
                DisplayName + " Object",
                DimensionAssetReferenceKind.Object,
                ObjectId,
                string.Empty,
                10,
                Enabled,
                Notes);
        }
    }
}
