namespace ExpandNullforge.Api
{
    /// <summary>
    /// What an item IS, in the terms Core Keeper itself keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This one answer decides more than anything else on an item: which slot it goes into when a
    /// player holds it, whether it can be swung, worn, eaten or played, which cooldown it shares,
    /// and how much use it takes before it breaks. Left unanswered the generator works one out from
    /// the item's own weapon and worn blocks, then from its template, and says which it settled on —
    /// but where the template covers several of these at once (Tool covers eleven) it cannot, and
    /// the item comes out as nothing in particular: equipping to no slot, swinging at nothing, one
    /// point of durability.
    /// </para>
    /// <para>
    /// The game's own list has 39 entries. Three of them are its plumbing rather than anybody's
    /// choice and are left out here: NonObtainable, which stops a thing dropping and skips lag
    /// compensation, and which the framework writes itself on blasts, projectiles and the hidden
    /// halves of a plant; Creature, which the creature generator writes; and PlayerType, which is
    /// the player character. Picking one of those by hand could only break an item, so the
    /// remaining 36 are the whole list.
    /// </para>
    /// <para>
    /// The counts in each line are measured over the 3,073 vanilla prefabs that carry a type, so
    /// "6 vanilla prefabs" means the game shipped six of them and no more.
    /// </para>
    /// </remarks>
    public enum DimensionWhatItIs
    {
        /// <summary>
        /// Not said. The generator works one out from the item's template and puts what it chose in
        /// the generation report. This is the default so that every item written before this
        /// question existed keeps behaving exactly the way it did.
        /// </summary>
        NotSaid = 0,

        // The member above is the enum's zero on purpose: an item file written before this question
        // existed has no value stored for it, and Unity reads a missing enum as zero.

        // ---------------------------------------------------------------- worn ---

        /// <summary>Worn on the head. Durability 90 before the multiplier. (90 vanilla prefabs)</summary>
        Helmet = 1,

        /// <summary>Worn on the chest. Durability 100. (75)</summary>
        ChestArmour = 2,

        /// <summary>
        /// Worn on the legs. The game's own slot name for it is pants, which is what the creative
        /// menu and both wikis call it. Durability 95. (63)
        /// </summary>
        PantsArmour = 3,

        /// <summary>
        /// Worn around the neck. Does not wear out — the game has an empty durability case for it
        /// on purpose. (49)
        /// </summary>
        Necklace = 4,

        /// <summary>
        /// Worn on a finger. There are two ring slots and the game picks between them. Does not
        /// wear out either. (61)
        /// </summary>
        Ring = 5,

        /// <summary>
        /// Held in the off hand: shields, torches, anything used with the main hand busy.
        /// Durability 100, though only the seven shields carry a durability pool at all. (38)
        /// </summary>
        OffHand = 6,

        /// <summary>
        /// Worn for extra inventory space. Needs an extra-inventory-size component to add any,
        /// which the framework has no answer for yet. (8)
        /// </summary>
        Bag = 7,

        /// <summary>
        /// Worn to light the way. The game pulls a lantern out of the off hand into its own slot.
        /// The type moves it there and nothing else: the light itself is a separate piece the
        /// framework has no answer for on an item yet, so one made today is a dark lantern. (6)
        /// </summary>
        Lantern = 8,

        /// <summary>
        /// Worn in one of four pouch slots. Extra space, like a bag, and it needs the same
        /// component to give any. (18)
        /// </summary>
        Pouch = 9,

        /// <summary>
        /// A pet, worn in the pet slot. It follows its owner, fights alongside them and buffs them.
        /// The type is only the slot: the game rolls nine talents and a skin off the pet's own
        /// talent pool the first time one is picked up, and an item with no pool logs "couldn't
        /// initialize pet" and sits in the slot doing nothing. The framework has no answer for a
        /// pet's talents on an item yet. (14)
        /// </summary>
        Pet = 10,

        // ------------------------------------------------------------- weapons ---

        /// <summary>
        /// Swung. Swords, spears, axes. Durability is 350 times the multiplier at the ordinary 0.4
        /// second swing, and the game divides it by the swing time — so a 0.8 second swing halves
        /// it to 175. A slower weapon takes fewer swings to break, not more. (36)
        /// </summary>
        MeleeWeapon = 11,

        /// <summary>
        /// Fires something. Bows, guns, staffs that shoot. Durability 250 at the ordinary 0.6
        /// second shot, divided by the shot time the same way. (35)
        /// </summary>
        RangedWeapon = 12,

        /// <summary>
        /// Thrown. It goes in the ranged slot but is a stack rather than a durability pool — all
        /// seven of the game's throwing weapons stack, start at one, and carry no durability at
        /// all. An item generated as one is left the same way unless a durability is typed on it.
        /// (7)
        /// </summary>
        ThrowingWeapon = 13,

        /// <summary>
        /// Summons something to fight for you. Flat durability 100, and no weapon damage of its
        /// own: the thing it summons does the hitting. (7)
        /// </summary>
        SummoningWeapon = 14,

        /// <summary>
        /// Fires a held beam rather than a shot. Durability 250, and it spends that durability for
        /// as long as the button is held. (4)
        /// </summary>
        BeamWeapon = 15,

        // --------------------------------------------------------------- tools ---

        /// <summary>
        /// Mines walls and ore. Durability 800, the highest in the game, and it runs off the mining
        /// cooldown rather than the melee one. (10)
        /// </summary>
        Pickaxe = 16,

        /// <summary>Digs ground. Durability 100. (7)</summary>
        Shovel = 17,

        /// <summary>Tills soil for planting. Durability 250. (5)</summary>
        Hoe = 18,

        /// <summary>
        /// Smashes. Shares the melee slot with the pickaxe and the drill, and the mining cooldown
        /// with them. Durability 200. (6)
        /// </summary>
        Sledgehammer = 19,

        /// <summary>
        /// Held down to grind through what it touches. Durability 250, spent while held. (2)
        /// </summary>
        Drill = 20,

        /// <summary>
        /// Cuts and fills roof lights, one tile at a time or three by three. The game's own one is
        /// the Roofing Gadget. Durability 200. (1)
        /// </summary>
        RoofingGadget = 21,

        /// <summary>
        /// Repaints what it is used on. Needs a paint-tool component to carry a palette, which the
        /// framework has no answer for on an item yet. (14)
        /// </summary>
        Paintbrush = 22,

        /// <summary>
        /// Catches fish. The type is the whole of it: all seven of the game's rods carry no fishing
        /// component at all, and the game stops fishing the moment the held item is not one. (7)
        /// </summary>
        FishingRod = 23,

        /// <summary>
        /// Catches critters. Nothing else on it either, and it catches only things whose kind is
        /// Critter. (1)
        /// </summary>
        BugNet = 24,

        /// <summary>
        /// Waters crops. Needs a fullness pool to hold anything, which the framework has no answer
        /// for yet. (3)
        /// </summary>
        WateringCan = 25,

        /// <summary>Carries liquid. The same fullness pool, and the same gap. (8)</summary>
        Bucket = 26,

        /// <summary>
        /// Plants seeds over an area. The game has no durability of its own for a seeder — both of
        /// vanilla's carry 250 because their prefabs say 250, not because anything works it out —
        /// so this is one of the kinds where the number under Durability is the only number there
        /// is. Leave it blank and the seeder breaks on its first use. (2)
        /// </summary>
        Seeder = 27,

        /// <summary>
        /// Cast and held, doing a job rather than damage: scanners, cages, wands, the thing that
        /// opens a cracked egg. Does not wear out. (60)
        /// </summary>
        CastItem = 28,

        // --------------------------------------------------------------- other ---

        /// <summary>
        /// Something you set down in the world: blocks, furniture, workbenches, ore veins. The game
        /// refuses to place anything else. It also skips environmental conditions on it, counts it
        /// destructible for damage, and swallows the hit sparks a creature would show — those three
        /// read the type off the live object rather than the item table, which is why a generated
        /// item carries the framework's own type marker alongside the type itself. A chest, a
        /// workbench, a boat, a world prop or a plant is placed too, but it is made by its own
        /// maker and carries no marker, so it keeps showing sparks when it is hit. (1,281, over a
        /// third of everything)
        /// </summary>
        SomethingYouPlace = 29,

        /// <summary>
        /// Eaten. Comes from the item's cooking answer rather than being picked here: the game
        /// decides eating from the type, so every one of its 79 ingredients and 45 dishes carries
        /// this. (182)
        /// </summary>
        Food = 30,

        /// <summary>
        /// Played. Needs an instrument component to make any sound, which the item's music block
        /// fills in. (6)
        /// </summary>
        Instrument = 31,

        /// <summary>
        /// Treasure. It equips to nothing, and all the type buys is the coin marker the game draws
        /// on its inventory slot. (126)
        /// </summary>
        Valuable = 32,

        /// <summary>
        /// A key item: a quest piece. The game draws a key on its inventory slot and adds a line to
        /// its tooltip saying so. That is the whole of it — nothing stops a player moving, dropping
        /// or losing one. (12)
        /// </summary>
        KeyItem = 33,

        /// <summary>
        /// A crafting component the game singles out with a line in its tooltip. Like a key item,
        /// the line is all of it; it is carried and traded like anything else. (17)
        /// </summary>
        UniqueCraftingComponent = 34,

        /// <summary>A critter you can catch with a bug net and set down again. (50)</summary>
        Critter = 35,

        /// <summary>
        /// Nothing in particular: a bar, a fibre, a dust. Carried, crafted with, and used for
        /// nothing on its own. What 139 vanilla objects genuinely are, and what an item with no
        /// answer silently becomes.
        /// </summary>
        NothingInParticular = 36
    }

    /// <summary>
    /// Where an item's kind came from, when nobody typed one.
    /// </summary>
    /// <remarks>
    /// The generator says this out loud in its report, and the dashboard has to reach the same
    /// answer the generator will, so both ask the item rather than working it out apart.
    /// </remarks>
    public enum DimensionWhatItIsSource
    {
        /// <summary>The creator picked it.</summary>
        Said = 0,

        /// <summary>Taken from how the item says it attacks.</summary>
        HowItAttacks = 1,

        /// <summary>Taken from where the item says it is drawn on the character.</summary>
        WhereItIsWorn = 2,

        /// <summary>Nothing on the item said, so the template decided.</summary>
        ItsTemplate = 3
    }

    /// <summary>
    /// Whether several of a kind share one inventory slot in the game the framework is building for.
    /// </summary>
    /// <remarks>
    /// Counted over the 3,073 vanilla prefabs that carry an object type, per type. The three answers
    /// are what the count found: never (0 of however many there are), always (all of them), or both
    /// (some do and some do not, so the creator's own answer is the only one there is).
    /// </remarks>
    public enum DimensionStacking
    {
        /// <summary>Some of the game's own stack and some do not, so the creator decides.</summary>
        EitherWay = 0,

        /// <summary>Not one of the game's own stacks.</summary>
        NeverStacks = 1,

        /// <summary>Every one of the game's own stacks.</summary>
        AlwaysStacks = 2
    }

    /// <summary>
    /// Turns an unanswered <see cref="DimensionWhatItIs"/> into the one the item's template implies,
    /// and names each kind for a report line.
    /// </summary>
    /// <remarks>
    /// Kept beside the archetype rules and out of the generator on purpose: the archetype decides
    /// which authoring components get attached, this decides what the game thinks the thing is, and
    /// the two answers are independent. Nothing here touches Unity, so it can be read and tested
    /// without either the editor or the game.
    /// </remarks>
    public static class DimensionWhatItIsRules
    {
        /// <summary>The kind an archetype implies on its own, for an item that never said.</summary>
        /// <remarks>
        /// <para>
        /// Every answer here is the value the generator already wrote for that archetype before this
        /// question existed, so an item nobody reopens generates exactly the object it generated
        /// yesterday. World-placed archetypes wrote PlaceablePrefab; everything else wrote nothing
        /// at all, which the game reads as NonUsable.
        /// </para>
        /// <para>
        /// Three archetypes have no honest answer here and get NothingInParticular with a report
        /// line rather than a guess: Tool covers eleven game types whose durability runs from 100 to
        /// 800, Weapon covers five, and Armor covers three worn slots. The generator asks the item's
        /// own weapon and worn-on-the-character blocks first and falls back to this only when those
        /// are empty too. A pickaxe quietly defaulting to a shovel is a wrong tool nobody would be
        /// told about.
        /// </para>
        /// </remarks>
        public static DimensionWhatItIs DefaultFor(DimensionItemArchetype archetype)
        {
            switch (archetype)
            {
                case DimensionItemArchetype.Block:
                case DimensionItemArchetype.Placeable:
                case DimensionItemArchetype.Building:
                case DimensionItemArchetype.Ore:
                case DimensionItemArchetype.Breakable:
                case DimensionItemArchetype.Explosive:

                // Creature, Mob and Boss sit here rather than under the game's own Creature type
                // because a real creature is a DimensionMobAsset and goes through the creature
                // generator, which writes Creature itself. An ITEM carrying one of these archetypes
                // has no AI on it and has been generating as a placeable object all along; typing it
                // Creature would hand a thing with no behaviour the components the game adds to one.
                case DimensionItemArchetype.Creature:
                case DimensionItemArchetype.Mob:
                case DimensionItemArchetype.Boss:
                    return DimensionWhatItIs.SomethingYouPlace;

                // Consumable is deliberately not Food. The cooking block decides food, it runs after
                // this and sets the type itself, and a consumable that is a potion rather than a
                // meal has to stay nothing in particular or it would land in the eating slot.
                case DimensionItemArchetype.Material:
                case DimensionItemArchetype.Pickup:
                case DimensionItemArchetype.Consumable:
                case DimensionItemArchetype.Custom:
                case DimensionItemArchetype.Tool:
                case DimensionItemArchetype.Weapon:
                case DimensionItemArchetype.Armor:
                default:
                    return DimensionWhatItIs.NothingInParticular;
            }
        }

        /// <summary>
        /// True when the archetype alone cannot say what the item is, so leaving the question unset
        /// is worth telling the author about rather than filling in quietly.
        /// </summary>
        public static bool ArchetypeCannotSayWhatItIs(DimensionItemArchetype archetype)
        {
            return archetype == DimensionItemArchetype.Tool ||
                   archetype == DimensionItemArchetype.Weapon ||
                   archetype == DimensionItemArchetype.Armor;
        }

        /// <summary>True for the five kinds a player holds and attacks with.</summary>
        public static bool IsAWeapon(DimensionWhatItIs kind)
        {
            return kind == DimensionWhatItIs.MeleeWeapon ||
                   kind == DimensionWhatItIs.RangedWeapon ||
                   kind == DimensionWhatItIs.ThrowingWeapon ||
                   kind == DimensionWhatItIs.SummoningWeapon ||
                   kind == DimensionWhatItIs.BeamWeapon;
        }

        /// <summary>True for the three pieces drawn on the character.</summary>
        public static bool IsWornArmour(DimensionWhatItIs kind)
        {
            return kind == DimensionWhatItIs.Helmet ||
                   kind == DimensionWhatItIs.ChestArmour ||
                   kind == DimensionWhatItIs.PantsArmour;
        }

        /// <summary>
        /// Whether the game stacks this kind, never stacks it, or does both.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Counted over the 3,073 vanilla prefabs carrying an object type. Never, and unanimously:
        /// helmets 0 of 90, chest 0 of 75, pants 0 of 63, necklaces 0 of 49, rings 0 of 61, bags 0
        /// of 8, lanterns 0 of 6, pouches 0 of 18, pets 0 of 14, melee weapons 0 of 36, summoning 0
        /// of 7, beam 0 of 4, pickaxes 0 of 10, shovels 0 of 7, hoes 0 of 5, sledgehammers 0 of 6,
        /// drills 0 of 2, roofing gadgets 0 of 1, paintbrushes 0 of 14, fishing rods 0 of 7, bug
        /// nets 0 of 1, watering cans 0 of 3, buckets 0 of 8, seeders 0 of 2, instruments 0 of 6.
        /// Always, and unanimously: throwing weapons 7 of 7, food 182 of 182, valuables 126 of 126,
        /// unique crafting components 17 of 17, key items 12 of 12, critters 50 of 50.
        /// </para>
        /// <para>
        /// The rest genuinely go both ways and are left to the creator: off-hands 8 of 38 (torches
        /// stack, shields do not), ranged weapons 1 of 35, cast items 45 of 60, things you place
        /// 1,260 of 1,281, nothing in particular 123 of 139.
        /// </para>
        /// <para>
        /// This is not a nicety. <c>initialAmount</c> is the durability an item starts with when it
        /// wears out and the number handed over when it stacks, and the game tells them apart by
        /// this one bool — so a helmet that says it stacks is dealt out ninety at a time. Not one of
        /// the 343 vanilla prefabs with a durability pool is stackable.
        /// </para>
        /// </remarks>
        public static DimensionStacking StackingFor(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.Helmet:
                case DimensionWhatItIs.ChestArmour:
                case DimensionWhatItIs.PantsArmour:
                case DimensionWhatItIs.Necklace:
                case DimensionWhatItIs.Ring:
                case DimensionWhatItIs.Bag:
                case DimensionWhatItIs.Lantern:
                case DimensionWhatItIs.Pouch:
                case DimensionWhatItIs.Pet:
                case DimensionWhatItIs.MeleeWeapon:
                case DimensionWhatItIs.SummoningWeapon:
                case DimensionWhatItIs.BeamWeapon:
                case DimensionWhatItIs.Pickaxe:
                case DimensionWhatItIs.Shovel:
                case DimensionWhatItIs.Hoe:
                case DimensionWhatItIs.Sledgehammer:
                case DimensionWhatItIs.Drill:
                case DimensionWhatItIs.RoofingGadget:
                case DimensionWhatItIs.Paintbrush:
                case DimensionWhatItIs.FishingRod:
                case DimensionWhatItIs.BugNet:
                case DimensionWhatItIs.WateringCan:
                case DimensionWhatItIs.Bucket:
                case DimensionWhatItIs.Seeder:
                case DimensionWhatItIs.Instrument:
                    return DimensionStacking.NeverStacks;

                case DimensionWhatItIs.ThrowingWeapon:
                case DimensionWhatItIs.Food:
                case DimensionWhatItIs.Valuable:
                case DimensionWhatItIs.UniqueCraftingComponent:
                case DimensionWhatItIs.KeyItem:
                case DimensionWhatItIs.Critter:
                    return DimensionStacking.AlwaysStacks;

                default:
                    return DimensionStacking.EitherWay;
            }
        }

        /// <summary>
        /// Whether several of this item share one slot, given what it is and what its author said.
        /// </summary>
        /// <remarks>
        /// The kind wins wherever the game is unanimous and the author's answer stands wherever it
        /// is not. One method so that the generator and the dashboard summary cannot drift: a
        /// summary line reading "Stacks=yes" beside an item that generates as one per slot is the
        /// same lie as a field that reaches nothing.
        /// </remarks>
        public static bool StacksGiven(DimensionWhatItIs kind, bool theAuthorSaidItStacks)
        {
            switch (StackingFor(kind))
            {
                case DimensionStacking.NeverStacks:
                    return false;
                case DimensionStacking.AlwaysStacks:
                    return true;
                default:
                    return theAuthorSaidItStacks;
            }
        }

        /// <summary>
        /// True for the kinds whose vanilla examples carry a durability pool, so one generated
        /// without one is worth a word.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Necklaces, rings, bags, lanterns, pouches, throwing weapons, cast items, paintbrushes,
        /// fishing rods, bug nets, watering cans, buckets, instruments and valuables are absent
        /// because not one vanilla example of any of them wears out.
        /// </para>
        /// <para>
        /// Four of the kinds listed here are not unanimous, and
        /// <see cref="SomeVanillaOnesNeverWearOut"/> is which four: the legendary sword, bow,
        /// mortar and mining pick have no durability pool, and neither do the snowball or the
        /// lightning gun. Unbreakable legendary gear is a thing players already know about, so the
        /// word said about a durability-less one has to leave room for it.
        /// </para>
        /// </remarks>
        public static bool AlwaysWearsOut(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.Helmet:
                case DimensionWhatItIs.ChestArmour:
                case DimensionWhatItIs.PantsArmour:
                case DimensionWhatItIs.MeleeWeapon:
                case DimensionWhatItIs.RangedWeapon:
                case DimensionWhatItIs.SummoningWeapon:
                case DimensionWhatItIs.BeamWeapon:
                case DimensionWhatItIs.Pickaxe:
                case DimensionWhatItIs.Shovel:
                case DimensionWhatItIs.Hoe:
                case DimensionWhatItIs.Sledgehammer:
                case DimensionWhatItIs.Drill:
                case DimensionWhatItIs.RoofingGadget:
                case DimensionWhatItIs.Seeder:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True for the four kinds where a vanilla example without a durability pool exists, so a
        /// generated one without a pool is not against the grain of the game.
        /// </summary>
        /// <remarks>
        /// Counted over the vanilla prefabs: melee 35 of 36, ranged 32 of 35, pickaxe 9 of 10, beam
        /// 2 of 4. The seven that do not wear out are the legendary sword, bow, mortar, mining pick
        /// and staff, the snowball and the lightning gun. Everything else
        /// <see cref="AlwaysWearsOut"/> lists is unanimous.
        /// </remarks>
        public static bool SomeVanillaOnesNeverWearOut(DimensionWhatItIs kind)
        {
            return kind == DimensionWhatItIs.MeleeWeapon ||
                   kind == DimensionWhatItIs.RangedWeapon ||
                   kind == DimensionWhatItIs.Pickaxe ||
                   kind == DimensionWhatItIs.BeamWeapon;
        }

        /// <summary>
        /// True for the kinds the game has no durability number of its own for, so the only
        /// durability such an item can have is one the author types.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The game's <c>CalculateObjectDurability</c> has a case for the three worn armour pieces,
        /// the off-hand, the four weapon types that fight, and the six tools that dig, smash, drill
        /// or roof. Every other type falls out of the switch with the amount it was handed, which is
        /// what <c>initialAmount</c> holds. Necklace and Ring reach an EMPTY case, written that way
        /// on purpose so that jewellery does not wear out; the rest have no case at all.
        /// </para>
        /// <para>
        /// This matters because a seeder is exactly as much a tool as a hoe to a player, and the
        /// difference between them is invisible: the hoe works out 250 on its own and the seeder
        /// gets whatever is typed, or 1.
        /// </para>
        /// </remarks>
        public static bool TheGameHasNoDurabilityNumberFor(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.Helmet:
                case DimensionWhatItIs.ChestArmour:
                case DimensionWhatItIs.PantsArmour:
                case DimensionWhatItIs.OffHand:
                case DimensionWhatItIs.MeleeWeapon:
                case DimensionWhatItIs.RangedWeapon:
                case DimensionWhatItIs.ThrowingWeapon:
                case DimensionWhatItIs.SummoningWeapon:
                case DimensionWhatItIs.BeamWeapon:
                case DimensionWhatItIs.Pickaxe:
                case DimensionWhatItIs.Shovel:
                case DimensionWhatItIs.Hoe:
                case DimensionWhatItIs.Sledgehammer:
                case DimensionWhatItIs.Drill:
                case DimensionWhatItIs.RoofingGadget:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// The piece the kind needs beyond anything an archetype attaches, or empty when the type is
        /// the whole of it.
        /// </summary>
        /// <remarks>
        /// Measured, not guessed. A bag and a pouch are the only two kinds where every vanilla
        /// prefab carries an extra-inventory-size component, the watering can and the bucket the
        /// only two that all carry a fullness pool, and the paintbrush the only one whose prefabs
        /// all carry a paint palette. A fishing rod and a bug net carry nothing at all, which is why
        /// neither is listed.
        /// <para>
        /// A pet and a lantern are listed for a different reason: not because a component is 100%
        /// on their prefabs, but because the game reaches for something on the object the moment the
        /// type puts it in the slot, and finds nothing. Picking a pet up makes the game roll nine
        /// talents and a skin out of that pet's own talent pool and log an error when there is not
        /// one; a lantern in the lantern slot lights nothing without a light of its own.
        /// </para>
        /// </remarks>
        public static string MissingPieceFor(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.Bag:
                case DimensionWhatItIs.Pouch:
                    return "extra inventory space";
                case DimensionWhatItIs.WateringCan:
                case DimensionWhatItIs.Bucket:
                    return "a fullness pool";
                case DimensionWhatItIs.Paintbrush:
                    return "a paint palette";
                case DimensionWhatItIs.Pet:
                    return "a pet's own talents and skins";
                case DimensionWhatItIs.Lantern:
                    return "a light of its own";
                default:
                    return string.Empty;
            }
        }

        /// <summary>What to call the kind in a report line or a dashboard summary.</summary>
        public static string Describe(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.NotSaid: return "not said";
                case DimensionWhatItIs.Helmet: return "a helmet";
                case DimensionWhatItIs.ChestArmour: return "chest armour";
                case DimensionWhatItIs.PantsArmour: return "pants armour";
                case DimensionWhatItIs.Necklace: return "a necklace";
                case DimensionWhatItIs.Ring: return "a ring";
                case DimensionWhatItIs.OffHand: return "an off-hand item";
                case DimensionWhatItIs.Bag: return "a bag";
                case DimensionWhatItIs.Lantern: return "a lantern";
                case DimensionWhatItIs.Pouch: return "a pouch";
                case DimensionWhatItIs.Pet: return "a pet";
                case DimensionWhatItIs.MeleeWeapon: return "a melee weapon";
                case DimensionWhatItIs.RangedWeapon: return "a ranged weapon";
                case DimensionWhatItIs.ThrowingWeapon: return "a throwing weapon";
                case DimensionWhatItIs.SummoningWeapon: return "a summoning weapon";
                case DimensionWhatItIs.BeamWeapon: return "a beam weapon";
                case DimensionWhatItIs.Pickaxe: return "a pickaxe";
                case DimensionWhatItIs.Shovel: return "a shovel";
                case DimensionWhatItIs.Hoe: return "a hoe";
                case DimensionWhatItIs.Sledgehammer: return "a sledgehammer";
                case DimensionWhatItIs.Drill: return "a drill";
                case DimensionWhatItIs.RoofingGadget: return "a roofing gadget";
                case DimensionWhatItIs.Paintbrush: return "a paintbrush";
                case DimensionWhatItIs.FishingRod: return "a fishing rod";
                case DimensionWhatItIs.BugNet: return "a bug net";
                case DimensionWhatItIs.WateringCan: return "a watering can";
                case DimensionWhatItIs.Bucket: return "a bucket";
                case DimensionWhatItIs.Seeder: return "a seeder";
                case DimensionWhatItIs.SomethingYouPlace: return "something you place";
                case DimensionWhatItIs.Food: return "food";
                case DimensionWhatItIs.Instrument: return "an instrument";
                case DimensionWhatItIs.Valuable: return "treasure";
                case DimensionWhatItIs.KeyItem: return "a key item";
                case DimensionWhatItIs.UniqueCraftingComponent:
                    return "a unique crafting component";
                case DimensionWhatItIs.Critter: return "a critter";
                case DimensionWhatItIs.NothingInParticular: return "nothing in particular";
                default: return kind.ToString();
            }
        }
    }
}
