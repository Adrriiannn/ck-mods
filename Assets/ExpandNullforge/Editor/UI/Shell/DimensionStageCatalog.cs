using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Every kind of thing a dimension can contain, described in the words a player would use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This file is the product's vocabulary. A label here is what a creator reads, so it says
    /// Blocks rather than tilesets, Mobs rather than creatures, and Workbenches rather than
    /// crafting stations, the words the game itself and both wikis use.
    /// </para>
    /// <para>
    /// Two rules govern what appears. Every field carries an explanation, because a form that
    /// cannot explain itself is a form only its author can use. And nothing the framework
    /// generates on the creator's behalf, ids above all, is offered as something to fill in;
    /// those still exist and stay reachable under "Everything else", but they are plumbing rather
    /// than decisions.
    /// </para>
    /// </remarks>
    internal static class DimensionStageCatalog
    {
        private static Dictionary<string, DimensionStageDescriptor> stages;

        internal static bool TryGetStage(string sectionId, out DimensionStageDescriptor stage)
        {
            EnsureBuilt();
            return stages.TryGetValue(sectionId, out stage);
        }

        private static void EnsureBuilt()
        {
            if (stages != null)
            {
                return;
            }

            stages = new Dictionary<string, DimensionStageDescriptor>();
            Add(BuildItemsStage());
            Add(BuildNatureStage());
            Add(BuildMonstersStage());
            Add(BuildDungeonsStage());
            Add(BuildWorldRulesStage());
        }

        private static void Add(DimensionStageDescriptor stage)
        {
            stages[stage.SectionId] = stage;
        }

        // ------------------------------------------------------------- helpers ---

        private static DimensionFieldDescriptor F(string path, string label, string tooltip)
        {
            return new DimensionFieldDescriptor(path, label, tooltip);
        }

        private static DimensionGroupDescriptor G(
            string title,
            string hint,
            params DimensionFieldDescriptor[] fields)
        {
            return new DimensionGroupDescriptor(title, hint, fields);
        }

        private static IReadOnlyList<Object> WrapMany<TA, TB>(TA[] first, TB[] second)
            where TA : Object
            where TB : Object
        {
            List<Object> list = new List<Object>();
            AppendWrapped(list, first);
            AppendWrapped(list, second);
            return list;
        }

        private static void AppendWrapped<T>(List<Object> list, T[] items) where T : Object
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    list.Add(items[i]);
                }
            }
        }

        private static IReadOnlyList<Object> Wrap<T>(T[] items) where T : Object
        {
            List<Object> list = new List<Object>();
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null)
                    {
                        list.Add(items[i]);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Reads a field off any asset as text for the lists, whatever type it happens to be.
        /// </summary>
        /// <remarks>
        /// Asking a serialized property for the wrong kind of value throws rather than returning
        /// nothing, so a card naming an enum must be told it is reading an enum. Checking the type
        /// here keeps the catalog free to name any field without knowing its type.
        /// </remarks>
        private static string Str(Object asset, string propertyPath, string fallback)
        {
            if (asset == null)
            {
                return fallback;
            }

            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(asset);
            UnityEditor.SerializedProperty property = serialized.FindProperty(propertyPath);
            string value = null;
            if (property != null)
            {
                switch (property.propertyType)
                {
                    case UnityEditor.SerializedPropertyType.String:
                        value = property.stringValue;
                        break;

                    case UnityEditor.SerializedPropertyType.Enum:
                        string[] names = property.enumDisplayNames;
                        int index = property.enumValueIndex;
                        value = names != null && index >= 0 && index < names.Length
                            ? names[index]
                            : null;
                        break;

                    case UnityEditor.SerializedPropertyType.Integer:
                        value = property.intValue.ToString();
                        break;

                    case UnityEditor.SerializedPropertyType.ObjectReference:
                        value = property.objectReferenceValue == null
                            ? null
                            : property.objectReferenceValue.name;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return string.IsNullOrEmpty(asset.name) ? fallback : asset.name;
        }

        private static string Count(Object asset, string arrayPath, string singular)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(asset);
            UnityEditor.SerializedProperty property = serialized.FindProperty(arrayPath);
            int count = property != null && property.isArray ? property.arraySize : 0;
            return count + " " + singular + (count == 1 ? string.Empty : "s");
        }

        private static DimensionGroupDescriptor NotesGroup()
        {
            return G(
                "Notes",
                "just for you",
                F("notes", "Notes", "A note to yourself. Never shown to a player, never shipped."));
        }

        // The tilesets stage deliberately has NO catalog entry: the shell routes it to the
        // bespoke Tileset Studio page, so a descriptor here would be authored, tested, and
        // never shown — the unreachable-code class this codebase keeps a memory about.

        // --------------------------------------------------------------- items ---

        private static DimensionStageDescriptor BuildItemsStage()
        {
            DimensionCollectionDescriptor items = new DimensionCollectionDescriptor(
                "items",
                "Items",
                "item",
                "No items yet",
                "Items are everything a player can hold: gear, materials, tools, treasure. Most other things in a dimension end up pointing at one.",
                t => Wrap(t.GlobalItems),
                t => DimensionFrameworkAuthoringAssetUtility.CreateItem(t, DimensionItemKind.BaseItem),
                a => Str(a, "displayName", "Item"),
                a => Str(a, "archetype", "item"),
                G("Basics", "name and template",
                    F("displayName", "Name", "What the player sees in their inventory and in tooltips."),
                    F("description", "Description", "The line under the name in the tooltip. This is where an item's character lives."),
                    F("archetype", "Template", "A set of sensible values already filled in for this kind of item, so you are not starting from a blank form."),
                    F("whatItIs", "What It Is", "Which slot this goes in and what a player can do with it: swing it, wear it, eat it, set it down. Left at Not said, the generator works one out and tells you what it chose. Answer it when the template covers several things at once — Tool is eleven of the game's kinds, from a shovel to a bucket.")),
                G("Appearance", "icon and rarity",
                    F("iconSprite", "Icon", "The picture shown in the inventory."),
                    F("smallIconSprite", "Small Icon", "The smaller picture used where space is tight. Leave empty to reuse the icon."),
                    F("rarityId", "Rarity", "How special this item is. The game colours its name by this, from common through to legendary.")),
                G("Behaviour", "stacking, timing and wear",
                    F("cooldownSeconds", "Time Between Uses", "Seconds before it can be used again: the swing rate, the wait before the next bite, and the number a melee or ranged weapon's durability is divided by. Leave it blank and the game's own goes in: 0.4 seconds, or 0.6 for a bow, a thrown weapon, or a summoning weapon that summons a minion on right-click."),
                    F("stackable", "Stacks", "Several of these share one inventory slot, up to 9999. What it is decides this wherever the game is unanimous: armour, jewellery, bags, melee, summoning and beam weapons and the digging tools never stack, and all food, valuables, key items and thrown weapons always do. Your answer stands for the ones that go both ways, like torches, cast items, bows and off-hand pieces."),
                    F("durabilityMultiplier", "Durability", "How long this lasts before breaking, relative to normal. 2 on a helmet is 180 uses rather than 90. An item with no durability can never be reinforced."),
                    F("durabilityPoints", "Durability Points", "Uses before it wears out, for the kinds the game keeps no number for: a seeder, a fishing rod, a bag, a lantern, a necklace, anything cast. Leave it blank for a helmet, a sword or a pickaxe and the game's own number is used."),
                    F("repairMultiplier", "Repair Cost", "How expensive this is to repair, relative to normal."),
                    F("reinforceCostMultiplier", "Reinforce Cost", "The same, but for reinforcing.")),
                G("Source", "drops",
                    F("dropsFrom", "Dropped By", "Which mobs, chests or blocks give this item, and how often.")),
                G("As food", "what it does in the pot",
                    F("cooking.role", "Part in Cooking", "Whether it goes in the pot, comes out of it, or neither."),
                    F("cooking.makesDish", "Makes", "Which dish it makes when it leads the pair. One of yours, or one of the game's fifteen."),
                    F("cooking.ingredientKind", "Kind", "Plant, fish or meat. This is which button finds it in the cook book; it does not decide what a pair makes."),
                    F("cooking.canBeFished", "Can Be Caught", "It can be caught with a fishing rod."),
                    F("cooking.countsAsAFlower", "Counts as a Flower", "A rare flower in the pot is one of the two things that can push a cooked dish up to epic."),
                    F("cooking.coloursFromItsOwnPicture", "Colours From Its Picture", "Take the four shades a dish borrows from this item's own icon. Untick to choose them by hand."),
                    F("cooking.givesRaw", "Gives Raw", "What eating it raw gives. Hunger goes here as HungerAddition."),
                    F("cooking.givesCooked", "Gives Cooked", "What it gives once it has been through a pot."),
                    F("cooking.hasAGoldenVersion", "Golden Version", "Also make a rare, golden version that aims at the better dish."),
                    F("cooking.goldenName", "Golden Name", "What the golden version is called. Empty means Golden and this item's name.")));

            DimensionCollectionDescriptor recipes = new DimensionCollectionDescriptor(
                "recipes",
                "Recipes",
                "recipe",
                "No recipes yet",
                "A recipe turns ingredients into an item, at a Workbench or in the player's own hands. What a craft costs is kept on the item that comes out, so ingredients only apply to items you make.",
                t => Wrap(t.GlobalRecipes),
                t => DimensionFrameworkAuthoringAssetUtility.CreateRecipe(t),
                a => Str(a, "displayName", "Recipe"),
                a => Count(a, "ingredients", "ingredient"),
                G("Result", "the result",
                    F("displayName", "Name", "What this recipe is called in your project. Players see the item's name, not this."),
                    F("outputItemId", "Output", "Which item comes out."),
                    F("outputAmount", "Amount", "How many come out per craft.")),
                G("Ingredients", "ingredients and place",
                    F("ingredients", "Ingredients", "What goes in, and how much of each."),
                    F("craftingStationId", "Workbench", "Where this is made: one of the game's stations, one of yours, or leave it empty to have it made by hand."),
                    F("craftTimeSeconds", "Craft Time", "How many seconds the craft takes. Zero is instant.")),
                G("Availability", null,
                    F("enabled", "Enabled", "Turn off to keep this recipe in your project but out of the built dimension.")),
                NotesGroup());

            DimensionCollectionDescriptor workbenches = new DimensionCollectionDescriptor(
                "workbenches",
                "Workbenches",
                "Workbench",
                "No Workbenches yet",
                "A Workbench is where recipes are crafted. The game's own progression is a ladder of them, each one crafting the next.",
                t => Wrap(t.GlobalWorkbenches),
                t => DimensionFrameworkAuthoringAssetUtility.CreateWorkbench(t),
                a => Str(a, "displayName", "Workbench"),
                a => Count(a, "recipes", "recipe"),
                G("Basics", "name and kind",
                    F("displayName", "Name", "What the player sees on the placed Workbench and its item."),
                    F("kind", "Kind", "What sort of Workbench this is. This decides how its crafting window behaves."),
                    F("description", "Description", "The line under the name in the tooltip."),
                    F("rarityId", "Rarity", "How special the Workbench item is. Colours its name.")),
                G("Recipes", "its recipes",
                    F("recipes", "Recipes", "Everything craftable here. A recipe can appear at more than one Workbench.")),
                G("Behaviour", "in the world",
                    F("hitsToBreak", "Hits to Break", "How many hits it takes to knock this back into an item."),
                    F("facesPlacementDirection", "Directional", "Turn on if this Workbench should rotate to face however the player sets it down."),
                    F("showsALoopingEffectWhileWorking", "Working Glow", "Turn on for the warm working glow the game's own machines use."),
                    F("wholeInventoryIsOneCraft", "Bulk Crafting", "Turn on for a machine that consumes its whole inventory in a single craft."),
                    F("enabled", "Enabled", "Turn off to keep this Workbench in your project but out of the built dimension.")),
                NotesGroup());

            DimensionCollectionDescriptor loot = new DimensionCollectionDescriptor(
                "loot",
                "Loot tables",
                "loot table",
                "No loot tables yet",
                "A loot table is a list of possible drops with weights. Mobs, chests and dig spots all roll against one.",
                t => Wrap(t.GlobalLootTables),
                t => DimensionFrameworkAuthoringAssetUtility.CreateLootTable(t),
                a => Str(a, "displayName", "Loot table"),
                a => Count(a, "entries", "entry"),
                G("Basics", "name",
                    F("displayName", "Name", "What this table is called in your project.")),
                G("Drops", "the roll",
                    F("entries", "Entries", "Everything this table can give, each with a weight. Heavier entries come up more often."),
                    F("allowEmptyRoll", "Allow Empty Roll", "Turn on to let a roll come up empty, so a drop is not guaranteed."),
                    F("enabled", "Enabled", "Turn off to keep this table in your project but out of the built dimension.")),
                NotesGroup());

            DimensionCollectionDescriptor effects = new DimensionCollectionDescriptor(
                "effects",
                "Effects",
                "effect",
                "No effects yet",
                "An effect is a stat change the game can apply: a buff, a debuff, a passive bonus. Items, food and talents all hand these out.",
                t => Wrap(t.GlobalConditions),
                t => DimensionFrameworkAuthoringAssetUtility.CreateCondition(t),
                a => Str(a, "displayName", "Effect"),
                a => Str(a, "conditionName", "effect"),
                // No description row: the asset has no such field, and the old row rendered a
                // permanent "not on this asset any more" ghost. The buff's in-game line comes
                // from the game's own term table once term registration ships.
                G("Basics", "name and line",
                    F("displayName", "Name", "What you call this effect."),
                    F("tooltipLine", "Tooltip Line", "The line a player reads about it, with {0} where its number goes, as in \"{0}% mining damage\". Empty gives the number and then the name. The game gives an effect one piece of text and this is it.")));

            DimensionCollectionDescriptor projectiles = new DimensionCollectionDescriptor(
                "projectiles",
                "Projectiles",
                "projectile",
                "No projectiles yet",
                "A projectile is what a ranged weapon or a mob throws: arrows, bolts, spit, magic.",
                t => Wrap(t.GlobalProjectiles),
                t => DimensionFrameworkAuthoringAssetUtility.CreateProjectile(t),
                a => Str(a, "displayName", "Projectile"),
                a => Str(a, "projectileId", "projectile"),
                G("Basics", "name",
                    F("displayName", "Name", "What this projectile is called in your project.")));

            DimensionCollectionDescriptor explosions = new DimensionCollectionDescriptor(
                "explosions",
                "Explosions",
                "explosion",
                "No explosions yet",
                "An explosion is the blast a bomb, a trap or a dying mob leaves behind.",
                t => Wrap(t.GlobalExplosions),
                t => DimensionFrameworkAuthoringAssetUtility.CreateExplosion(t),
                a => Str(a, "displayName", "Explosion"),
                a => Str(a, "explosionId", "explosion"),
                G("Basics", "name",
                    F("displayName", "Name", "What this explosion is called in your project.")));

            // Storage, standing objects and vehicles live here rather than in a section of
            // their own: a placed thing IS an item with placement, which is exactly how the
            // game files it. One section per noun a player recognises, not per implementation
            // detail.
            return new DimensionStageDescriptor(
                "resources",
                items,
                recipes,
                workbenches,
                BuildContainersCollection(),
                BuildObjectsCollection(),
                BuildVehiclesCollection(),
                loot,
                effects,
                projectiles,
                explosions);
        }

        // -------------------------------------------------------------- nature ---

        private static DimensionStageDescriptor BuildNatureStage()
        {
            DimensionCollectionDescriptor plants = new DimensionCollectionDescriptor(
                "plants",
                "Plants",
                "plant",
                "No plants yet",
                "A plant is a crop or a wild growth. One plant covers its seed, its growing stages and what it gives when it ripens.",
                t => Wrap(t.GlobalPlants),
                t => DimensionFrameworkAuthoringAssetUtility.CreatePlant(t),
                a => Str(a, "displayName", "Plant"),
                a => Str(a, "produceItemId", "plant"),
                G("Basics", "name and look",
                    F("displayName", "Name", "What the crop is called. The plant in the ground is never named on screen, so this names the seed."),
                    F("seedName", "Seed Name", "What the seed is called in a slot. Empty means the crop's name with Seed after it."),
                    F("seedDescription", "Seed Description", "The line under the seed's name. Empty reuses the crop's description."),
                    F("description", "Description", "The line under the name in the tooltip."),
                    F("rarityId", "Rarity", "How special the seed is. Colours its name."),
                    F("seedIcon", "Seed Icon", "The picture shown on the seed in the inventory."),
                    F("art", "How It Looks", "What the crop looks like in the ground: one picture for each growth stage ending with the ripe one, the seed sitting in the soil, and whether it glows.")),
                G("Growth", "time and ground",
                    F("growthStages", "Growth Stages", "How many times it changes as it grows. The game's own crops use two, which is three pictures: just sprouted, half grown, ripe."),
                    F("minutesToGrow", "Growth Time", "How long a full growth takes."),
                    F("ground", "Grows On", "Which ground this can be planted in."),
                    F("washedAwayByWater", "Washed Away by Water", "Turn on if flooding should destroy this plant."),
                    F("staysToughWhenRipe", "Tough When Ripe", "Turn on if a ripe plant should resist being knocked down.")),
                G("Harvest", "harvest",
                    F("produceItemId", "Produce", "Which item harvesting a ripe plant yields."),
                    F("harvestAmount", "How Many Per Harvest", "How many of the produce one picking gives."),
                    F("chanceToGetTheSeedBackPercent", "Seed Comes Back", "Out of a hundred picks, how often you also get the seed back. The game's own crops give it back 75 times in 100.")),
                G("Better Versions", "golden and rarer",
                    F("versions", "Versions", "Rarer, better versions of this crop, like the golden ones the game's own crops have. Each one has its own name, how often it comes up, what it gives, and what extras come with it. Their order sets which version a planted crop already in a world belongs to, so add new ones at the end.")),
                G("Spreading", "wild growth",
                    F("spreadsOnTilesetIds", "Spreads Onto", "Which blocks this plant creeps across on its own."),
                    F("becomesTilesetId", "Converts Ground To", "Which block the ground becomes where this has taken hold.")));

            DimensionCollectionDescriptor dishes = new DimensionCollectionDescriptor(
                "dishes",
                "Dishes",
                "dish",
                "No dishes yet",
                "A dish is a kind of cooked food. The pot has no recipe list — every pair of ingredients is a recipe, and the dish that comes out is whichever one the leading ingredient makes. Add a dish only when you want a new kind of food; your ingredients can already make the game's fifteen.",
                t => Wrap(t.GlobalDishes),
                t => DimensionFrameworkAuthoringAssetUtility.CreateDish(t),
                a => Str(a, "displayName", "Dish"),
                a => Str(a, "dishId", "dish"),
                G("Basics", "name and look",
                    F("displayName", "Name", "What the player sees at the end of the dish's name, after the two ingredients that went in."),
                    F("description", "Description", "The line under the name in the tooltip."),
                    F("baseSprite", "Picture", "The dish drawn in the eight template colours. The game recolours it from whatever went in the pot."),
                    F("rareSprite", "Rare Picture", "The rare version's picture. Leave empty to reuse the ordinary one."),
                    F("epicSprite", "Epic Picture", "The same, for the epic version.")),
                G("How filling", "hunger",
                    F("hunger", "Hunger", "How much hunger the ordinary version restores. The game's own dishes sit between 5 and 20."),
                    F("rareHunger", "Rare Hunger", "How much the rare version restores."),
                    F("epicHunger", "Epic Hunger", "How much the epic version restores.")),
                G("What the better versions add", "bonuses",
                    F("extraOnRare", "On Rare", "Effects the rare version gives on top of what its ingredients give."),
                    F("extraOnEpic", "On Epic", "Effects the epic version gives on top of what its ingredients give.")),
                G("Availability", null,
                    F("enabled", "Enabled", "Turn off to keep this dish in your project but out of the built dimension.")),
                NotesGroup());

            // Ore veins deliberately have no tab here: a vein is a property of the wall it
            // hides in, so it is authored on the block, in Tileset Studio. Gardening is plants
            // and what they become in the pot.
            return new DimensionStageDescriptor("nature", plants, dishes);
        }

        // ------------------------------------------- placed things (Item Studio) ---

        private static DimensionCollectionDescriptor BuildContainersCollection()
        {
            return new DimensionCollectionDescriptor(
                "containers",
                "Chests",
                "chest",
                "No chests yet",
                "A chest is anything a player can store things in. Its contents are set where you place it, not here.",
                t => Wrap(t.GlobalContainers),
                t => DimensionFrameworkAuthoringAssetUtility.CreateContainer(t),
                a => Str(a, "displayName", "Chest"),
                a => Str(a, "size", "chest"),
                G("Basics", "name and look",
                    F("displayName", "Name", "What the player sees on the placed chest and its item."),
                    F("description", "Description", "The line under the name in the tooltip."),
                    F("rarityId", "Rarity", "How special the chest item is. Colours its name."),
                    F("sprite", "Artwork", "The picture of the placed chest."),
                    F("icon", "Icon", "The picture shown on the chest item in the inventory.")),
                G("Capacity", "slots",
                    F("size", "Size", "How big the chest is. The game's own chests are one row or two."),
                    F("customSlotsAcross", "Slots Across", "How many slots wide, when the size above is custom."),
                    F("customSlotsDown", "Slots Down", "And how many tall."),
                    F("upgradeableExtraSlots", "Upgrade Slots", "How many more slots an upgrade adds.")),
                // One row for the whole rules list: the five per-rule fields live on entries
                // INSIDE this array, so five root-path rows rendered nothing but "is not on
                // this asset any more" — while the mechanism itself was wired the whole time.
                G("Slot Rules", null,
                    F("slotRules", "Slot Rules", "Which slots take what. Each rule can cover " +
                      "one slot or the whole chest, accept items by name or by kind, refuse " +
                      "legendaries, and show the dimmed hint of what belongs in it.")));
        }

        private static DimensionCollectionDescriptor BuildObjectsCollection()
        {
            return new DimensionCollectionDescriptor(
                "objects",
                "Objects",
                "object",
                "No objects yet",
                "An object is anything else that stands in the world: furniture, decoration, machinery, scenery.",
                t => Wrap(t.GlobalWorldObjects),
                t => DimensionFrameworkAuthoringAssetUtility.CreateWorldObject(t),
                a => Str(a, "displayName", "Object"),
                a => Str(a, "objectIdentifier", "object"),
                G("Basics", "name",
                    F("displayName", "Name", "What the player sees on the placed object and its item."),
                    F("sprite", "Artwork", "The picture of the object standing in the world."),
                    F("icon", "Icon", "The picture shown on its item in the inventory. Falls back to the artwork."),
                    F("description", "Description", "The line under the name in the tooltip.")),
                G("Opening", "doors and gates",
                    F("gate", "It Opens When", "A held item, an object placed nearby, or a melody — how doors, hidden passages and singing walls work. A key put INSIDE something is a container; author that on a container instead.")),
                G("Light", "torches and lamps",
                    F("emittedLight", "The light it gives off", "Whether it lights the floor around it where it stands, and what that light looks like. Copy one of the game's own — a torch, a campfire, a lamp — or say the colour, reach and flicker yourself. Two of these on next-door tiles will not both light: the game keeps one real light per two tiles and draws the rest as a glow.")));
        }

        private static DimensionCollectionDescriptor BuildVehiclesCollection()
        {
            return new DimensionCollectionDescriptor(
                "vehicles",
                "Vehicles",
                "vehicle",
                "No vehicles yet",
                "A vehicle is something a player rides: a boat, a cart, a kart. Each one needs ground it can travel over.",
                t => Wrap(t.GlobalVehicles),
                t => DimensionFrameworkAuthoringAssetUtility.CreateVehicle(t),
                a => Str(a, "displayName", "Vehicle"),
                a => Str(a, "vehicleId", "vehicle"),
                G("Basics", "name",
                    F("displayName", "Name", "What a player sees on the vehicle and on the item that puts it down."),
                    F("description", "Description", "The line under the name in the tooltip.")),
                G("Riding", "getting on",
                    F("kind", "How it moves", "A kart that drives, a boat that needs water, or a minecart that follows rails. There are only three and the list cannot be added to."),
                    F("howCloseToGetOn", "How close to get on", "How near a player has to be standing before they can use it to get on.")),
                G("Breaking", "getting it back",
                    F("hitsToBreak", "Hits to break", "How many hits it takes. The game's own vehicles all take two."),
                    F("dropsItselfWhenBroken", "Breaking gives it back", "Breaking it returns the vehicle as an item.")));
        }

        // ------------------------------------------------------------ monsters ---

        /// <summary>The cards an ordinary monster shows. Cached: the shape never changes.</summary>
        private static DimensionGroupDescriptor[] mobGroups;

        /// <summary>The cards a boss shows. Cached for the same reason.</summary>
        private static DimensionGroupDescriptor[] bossGroups;

        private static DimensionStageDescriptor BuildMonstersStage()
        {
            mobGroups = BuildMobGroups();
            bossGroups = BuildBossGroups();

            // One list holds every creature, because to the game a boss IS a mob with more
            // wired to it. The list is one list and the selection one selection; only the
            // cards on the right change shape with the role of the thing selected, and each
            // role has its own add button so the choice is made where it belongs: at birth.
            DimensionCollectionDescriptor monsters = new DimensionCollectionDescriptor(
                "monsters",
                "Monsters",
                "monster",
                "No monsters yet",
                "Monsters are everything that lives in your dimension, mobs and bosses alike. Ordinary ones spawn from the ground they stand on; bosses arrive the way you decide.",
                t => WrapMany(t.GlobalMobs, t.GlobalBosses),
                null,
                a => Str(a, "displayName", "Monster"),
                a => a is DimensionBossAsset ? "Boss" : Str(a, "aggression", "mob"),
                mobGroups)
            {
                GroupsFor = item => item is DimensionBossAsset ? bossGroups : mobGroups,
                CreateChoices = new[]
                {
                    new DimensionCollectionCreateChoice(
                        "+ New Monster",
                        t => DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(
                            t, DimensionSpawnableKind.Mob)),
                    new DimensionCollectionCreateChoice(
                        "+ New Boss",
                        t => DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(
                            t, DimensionSpawnableKind.Boss)),
                },
            };

            DimensionCollectionDescriptor animals = new DimensionCollectionDescriptor(
                "animals",
                "Animals",
                "animal",
                "No animals yet",
                "Animals are the tameable, farmable ones: the mobs a player feeds, breeds and gathers from.",
                t => Wrap(t.GlobalAnimals),
                t => DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(t, DimensionSpawnableKind.Animal),
                a => Str(a, "displayName", "Animal"),
                a => Str(a, "animalId", "animal"),
                G("Basics", "name",
                    F("displayName", "Name", "What it is called wherever the game has to name it — a slot it ends up in, the console that spawns it. Nothing is written over its head."),
                    F("description", "Description", "The line under that name. The game's own animals leave it empty."),
                    F("aggression", "Temperament", "What starts the fight: it hunts you, it ignores you until you hit it, or it never fights at all.")),
                G("Presentation", "look and sound",
                    F("visual", "Appearance", "What it looks like: one row of pictures per thing it is seen doing, and the shadow it casts."),
                    F("audio", "Audio", "What it sounds like alive, hurt and dying.")));

            DimensionCollectionDescriptor critters = new DimensionCollectionDescriptor(
                "critters",
                "Critters",
                "critter",
                "No critters yet",
                "Critters are the small ambient life that makes a place feel inhabited: bugs, fish, things that wander underfoot and can be caught.",
                t => Wrap(t.GlobalCritters),
                t => DimensionFrameworkAuthoringAssetUtility.CreateSpawnable(t, DimensionSpawnableKind.Critter),
                a => Str(a, "displayName", "Critter"),
                a => Str(a, "critterId", "critter"),
                G("Basics", "name",
                    F("displayName", "Name", "What a player reads when a critter catcher puts this into a chest."),
                    F("description", "Description", "The line under that name.")),
                G("Presentation", "look and sound",
                    F("visual", "Appearance", "What it looks like: one row of pictures per thing it is seen doing, and the shadow it casts."),
                    F("audio", "Audio", "What it sounds like alive, hurt and dying.")));

            // No spawn-rules tab: where a creature lives is answered on the creature itself,
            // under Spawning. The separate rule assets said the same thing a second time and
            // nothing ever read them.
            return new DimensionStageDescriptor(
                "spawns", monsters, animals, critters);
        }

        private static DimensionGroupDescriptor[] BuildMobGroups()
        {
            return new[]
            {
                G("Basics", "name and temper",
                    F("displayName", "Name", "What it is called wherever the game has to name it — a slot it ends up in, the console that spawns it. Nothing is written over its head."),
                    F("description", "Description", "The line under that name. The game's own creatures leave it empty."),
                    F("aggression", "Temperament", "What starts the fight: it hunts you, it ignores you until you hit it, or it never fights at all."),
                    F("enabled", "Enabled", "Turn off to keep this mob in your project but out of the built dimension.")),
                G("Stats", "the fight",
                    F("creatureStats", "Stats", "Health, how it moves, how far it sees — the numbers the built creature actually carries."),
                    F("combat", "Attacks", "What it does to the player. Borrow one of the game's own attacks or build your own.")),
                G("Spawning", "spawning",
                    F("allowedBiomeIds", "Biomes", "Which of your biomes this mob belongs to."),
                    F("spawnsInWorld", "Natural Spawning", "Turn off for a mob only placed by hand or summoned."),
                    F("spawnChance", "Spawn Chance", "How likely it is to appear when the game considers spawning here."),
                    F("spawnAmount", "Group Size", "How many appear together when it does spawn."),
                    F("spawnsInGroups", "Spawns in Groups", "Turn on for a mob that travels as a pack.")),
                G("Drops", "loot",
                    F("lootTable", "Loot Table", "What it gives when killed."),
                    F("extraLoot", "Extra Loot", "Anything it drops on top of the table above.")),
                G("Hatching", "eggs",
                    F("hatching", "It Is an Egg", "A player coming near makes it hatch into something else, the way the Larva Hive's cocoons burst. Its own health and loot make it worth destroying carefully first.")),
                G("Coming Back", "respawning",
                    F("respawn", "It Keeps Coming Back", "Tiles of a chosen kind breed it over time, the way hive nests keep making larvae. Without this, placed mobs die once and stay gone — this is what keeps a dungeon or a biome dangerous on the tenth visit.")),
                G("Presentation", "look and sound",
                    F("visual", "Appearance", "What it looks like: one row of pictures per thing it is seen doing, and the shadow it casts."),
                    F("audio", "Audio", "What it sounds like alive, hurt and dying.")),
                G("Extras", null,
                    F("simpleTraits", "Traits", "The small switches: burns, floats, fears light, and the rest of the one-line behaviours."),
                    F("eliteVariant", "Elite Variant", "An occasional stronger version, the way the game's own mobs have elites."),
                    // The whole pet block is drawn here, children included: no chooser is written
                    // for it and ControlFor answers a Generic property with null, so Bound falls
                    // back to a PropertyField and its colours come with it. A second
                    // F("pet.colours", ...) row put the same list on the page twice, in two
                    // editors neither of which knew about the other. The colours field carries its
                    // own [Tooltip], which is the text that is drawn inside this row.
                    F("pet", "Pet", "Whether this mob can be tamed and follow a player, and the colours it can be recoloured into."),
                    F("behaviorScriptId", "Borrowed Behaviour", "Take one of the game's own creature behaviours wholesale, by name.")),
                NotesGroup(),
            };
        }

        private static DimensionGroupDescriptor[] BuildBossGroups()
        {
            return new[]
            {
                G("Basics", "name and temper",
                    F("displayName", "Name", "What the player sees on the health bar when the fight starts."),
                    F("aggression", "Temperament", "What starts the fight: it hunts you, it ignores you until you hit it, or it never fights at all."),
                    F("enabled", "Enabled", "Turn off to keep this boss in your project but out of the built dimension.")),
                G("Combat", "stats and phases",
                    F("creatureStats", "Stats", "Health, how it moves, how far it sees, how far it can reach — the numbers the built boss actually carries."),
                    F("combat", "Attacks", "What it does to the player."),
                    F("phases", "Phases", "The stages of the fight. The game's own bosses heal fully and change shape between phases."),
                    F("borrowedKit", "Borrowed Kit", "Borrow a vanilla boss's whole behaviour for this one."),
                    F("moreBorrowedKits", "More Borrowed Kits", "Further vanilla kits layered on top: the Core, the Wall and the Scarab live here."),
                    F("theRestOfTheKits", "Remaining Kits", "The rest of the game's boss kits: the Bird, the Robot, the Octopus, the Larva, the Shaman and the Snake.")),
                G("Summoning", "summoning and arena",
                    F("summoningItemId", "Summoning Item", "Which item calls this boss, when it needs one."),
                    F("arenaSceneId", "Arena", "Which handcrafted place this boss waits in."),
                    F("respawnCooldownMinutes", "Respawn Cooldown", "How many minutes before it can be fought again.")),
                G("Reward", "reward",
                    F("lootTable", "Loot Table", "What it drops when beaten."),
                    F("bossChest", "Chest", "The chest left behind, the way the game's own bosses reward you.")),
                G("Presentation", "look and sound",
                    F("visual", "Appearance", "What it looks like: one row of pictures per thing it is seen doing, under its floating name."),
                    F("audio", "Audio", "Its roars, its death."),
                    F("fightMusic", "Fight Music", "The music of the fight. BOSS is the game's own boss roster; it starts as a player approaches and stops when they leave."),
                    F("mapPin", "Map Pin", "The pin this boss shows on the map, with its icons and hover name. It goes dark when the boss dies, the way the game's own do.")),
                NotesGroup(),
            };
        }

        // ------------------------------------------------------------ dungeons ---

        private static DimensionStageDescriptor BuildDungeonsStage()
        {
            DimensionCollectionDescriptor scenes = new DimensionCollectionDescriptor(
                "scenes",
                "Places",
                "place",
                "No places yet",
                "A place is a handcrafted piece of world: a room, a ruin, a camp. You lay out its tiles and what stands in it, then tell the world where it may appear.",
                t => Wrap(t.GlobalScenes),
                t => DimensionFrameworkAuthoringAssetUtility.CreateScene(t, null),
                a => Str(a, "displayName", "Place"),
                a => Str(a, "kind", "place"),
                G("Basics", "name and kind",
                    F("displayName", "Name", "What this place is called in your project."),
                    F("kind", "Kind", "What sort of place this is.")),
                G("Contents", null,
                    F("tiles", "Tiles", "The ground and walls this place is drawn from, cell by cell. These reach the game exactly as authored."),
                    F("sceneObjects", "Objects", "Everything standing in this place: furniture, chests with their contents, creatures, machines — with facing and paint."),
                    F("triggers", "Triggers", "Patches of floor that do something when stepped on: summon creatures, apply a condition, deal damage. They arm wherever this place lands — except inside a generated dungeon room, which the game stamps without saying where.")),
                G("Placement", "placement",
                    F("allowedBiomeIds", "Biomes", "Which of your biomes may contain this place."),
                    F("placementMode", "Placement", "Whether the world drops this anywhere it fits, or at an exact spot you choose."),
                    F("footprintSize", "Footprint", "How many tiles across and down this place takes up."),
                    F("exactLocalPosition", "Exact Position", "Used when the placement above is set to an exact spot."),
                    F("minRadiusTiles", "Minimum Radius", "The ring's inner edge: how far from your dimension's centre — local 0, 0, where players arrive — this may appear. Never the far-away world Core."),
                    F("maxRadiusTiles", "Maximum Radius", "The ring's outer edge, from that same local centre."),
                    F("weight", "Weight", "How often this place comes up compared to the others competing for the same spot.")));

            // Dungeons returned the day their backend genuinely worked: the full pipeline
            // archetype, authored room and path rules, the fill shell, swaps and the
            // single-room form all reach the game now. The quest surface never earned its
            // backend — Core Keeper has no quest machinery to borrow — and was removed
            // rather than shipped as an editor over nothing.
            DimensionCollectionDescriptor dungeons = new DimensionCollectionDescriptor(
                "dungeons",
                "Dungeons",
                "dungeon",
                "No dungeons yet",
                "A dungeon is an arrangement of your places: the world picks a spot, grows rooms, carves corridors between them, and fills each room with one of the places you name.",
                t => Wrap(t.GlobalDungeons),
                t => DimensionFrameworkAuthoringAssetUtility.CreateDungeon(t),
                a => Str(a, "displayName", "Dungeon"),
                a => Str(a, "biomeId", "dungeon"),
                G("Basics", "name and where",
                    F("displayName", "Name", "What this dungeon is called in your project."),
                    F("biomeId", "Biome", "One of your biomes by id, or a vanilla biome by its game name for Overworld spawning. Empty means anywhere."),
                    F("radius", "Size", "How far across it reaches, in tiles."),
                    F("spawnChance", "Chance", "How likely it is to be chosen when the world places a dungeon in that biome. Overworld only — inside your dimension, placement is deliberate."),
                    F("minDistanceFromCentre", "Keeps Its Distance", "How close to the centre it may come. In your dimension that centre is the local 0, 0 where players arrive — never the far-away world Core."),
                    F("blockOtherSpawns", "Keep Creatures Out", "Wandering creatures stay outside it, the way vanilla dungeons work. This is the only control over that, and it applies whether the dungeon is a generated layout or one handmade room.")),
                G("Where It Grows", "in your dimension, in its own coordinates",
                    F("growsInThisDimension", "In This Dimension", "It grows inside your dimension. The game's own placer never runs there, so this is what makes it appear at all."),
                    F("dimensionPlacement", "Placement", "Anywhere it fits, at one exact spot, or on a ring. Every number here is in your dimension's OWN coordinates — local 0, 0 is its centre, the same numbers the coordinate readout shows in play."),
                    F("dimensionExactPosition", "Exact Spot", "The exact spot, in local coordinates. 0, 0 is where players arrive."),
                    F("dimensionMinRadius", "Ring Inner Edge", "Nearest to your dimension's centre — local 0, 0 — it may grow."),
                    F("dimensionMaxRadius", "Ring Outer Edge", "Furthest out from that same local centre. 0 means no outer limit."),
                    F("dimensionCount", "How Many", "How many of it one generated area may grow.")),
                G("In the Overworld", "a guaranteed pin, in WORLD coordinates",
                    F("pinnedInOverworld", "Pinned", "One guaranteed copy in the vanilla Overworld, placed by the game's own boss-and-temple machinery: scored spot, saved position, old saves included. This group alone speaks WORLD coordinates — the Core at 0, 0 — because it places into the vanilla world; everything under Where It Grows stays local."),
                    F("pinnedAtExactSpot", "Exact Spot", "Pin it at one world position rather than a scored distance."),
                    F("pinnedPosition", "Position", "The exact world position, when pinned at a spot. The Core is 0, 0."),
                    F("pinnedDistanceFromCore", "Distance From Core", "How far out it aims for, when scored. The game's own bosses run 65 to 750."),
                    F("pinnedBiomeName", "Vanilla Biome", "Keep it inside one of the game's biomes. Empty means anywhere."),
                    F("pinnedSpawnsImmediately", "Spawns Immediately", "It exists the moment the world loads, rather than when a player first nears its spot.")),
                G("Rooms", "what fills it",
                    F("roomGroups", "Room Groups", "Sets of interchangeable places for each part of the dungeon: the entrance, the main rooms, the dead ends worth walking to."),
                    F("roomFillings", "Room Fillings", "What the generated rooms hold: floors, chests, veins, decoration — placed procedurally in paint order, the way the game's own dungeons get their character."),
                    F("roomSize", "Room Fill", "How large the carved space around each room is."),
                    F("pathSize", "Corridor Width", "How wide the corridors between rooms are.")),
                G("Shape", "the generator's rules",
                    F("generatedShape", "Shape Rules", "The game's own dungeon generator: outline wobble, room and path rules, the blocks its shell is drawn from, and themed swaps.")))
            {
                CreateChoices = new[]
                {
                    new DimensionCollectionCreateChoice(
                        "+ New Dungeon",
                        t => DimensionFrameworkAuthoringAssetUtility.CreateDungeon(t)),
                    new DimensionCollectionCreateChoice(
                        "+ New Room Filling",
                        t => DimensionFrameworkAuthoringAssetUtility.CreateRoomFilling(t, null)),
                },
            };

            // A named area is a place made of blocks rather than walls of a room — it lives
            // beside the other place-makers because a dungeon's fill is how most areas get
            // their blocks into the world.
            DimensionCollectionDescriptor namedAreas = new DimensionCollectionDescriptor(
                "namedareas",
                "Named Areas",
                "named area",
                "No named areas yet",
                "A named area is a place with its own title card, ambience and music, carried by its signature blocks. Where enough of those blocks stand — placed by hand, by a dungeon, or by a scene — the area exists.",
                t => Wrap(t.NamedAreas),
                t => DimensionFrameworkAuthoringAssetUtility.CreateNamedArea(t),
                a => Str(a, "displayName", "Named Area"),
                a => Str(a, "areaId", "area"),
                G("Basics", "name and blocks",
                    F("displayName", "Name", "What the title card says when a player first walks in."),
                    F("blocks", "Blocks", "The blocks that ARE this area. Where enough of them stand, the area exists — and where they thin out, it fades, exactly like the game's own Meadow."),
                    F("showTitleOnDiscovery", "Title Card", "Its name appears across the screen on first visit."),
                    F("titleColor", "Title Color", "The color of that title."),
                    F("titleIconObjectId", "Title Icon", "An object whose icon appears beside the title.")),
                G("Atmosphere", "sound and music",
                    F("ambienceSoundKey", "Ambience", "The looping sound of standing here. A sound clip key; empty keeps the surroundings' ambience."),
                    F("ambienceVolume", "Ambience Volume", "How loud that loop plays."),
                    F("musicRosterName", "Music", "What plays here: a game roster name, or one of your own music cues.")));

            return new DimensionStageDescriptor("scenes", scenes, dungeons, namedAreas);
        }

        // ----------------------------------------------------------- the rules ---

        private static DimensionStageDescriptor BuildWorldRulesStage()
        {
            DimensionCollectionDescriptor setups = new DimensionCollectionDescriptor(
                "setups",
                "World rules",
                "rule set",
                "No world rules yet",
                "World rules are what a mod changes about the game rather than adds to: what upgrading costs, what fishing catches, what talents give, the numbers on the player, its armour sets, what a background starts you with, when the world acts on its own, what its own caves are made of, and how its skills are drawn. Switch on the block you want; nothing else changes.",
                t => Wrap(t.GlobalGameSetups),
                t => DimensionFrameworkAuthoringAssetUtility.CreateWorldRules(t),
                a => Str(a, "displayName", "Rule set"),
                a => Str(a, "setupIdentifier", "rules"),
                G("Basics", "name and id",
                    F("displayName", "Name", "What this rule set is called in your project."),
                    F("setupIdentifier", "Id", "A short id. It names the settings files written inside your mod, so keep it stable once people have played."),
                    F("enabled", "Applied", "Turn off to keep the rule set without changing anything.")),
                G("What upgrading costs", "prices per level",
                    F("upgrading.changesWhatUpgradingCosts", "Change it", "Off leaves every level at the game's own price."),
                    F("upgrading.costs", "Prices", "Each row is one ingredient at one level. Rows sharing a level become that level's whole price; a level nobody names keeps the game's. Names may be the game's items or your own.")),
                G("What fishing catches", "by biome, by water, and each fish's fight",
                    F("fishing.changesWhatFishingCatches", "Change it", "Off leaves fishing exactly as the game has it."),
                    F("fishing.biomes", "By biome", "What is caught in a biome, when the water itself has no rule of its own. Only the game's own biomes can be named here."),
                    F("fishing.waters", "By water", "What is caught in one kind of water, wherever that water appears. Only the game's own waters can be named here."),
                    F("fishing.fishFights", "Fish fights", "The turns of one fish's fight, in order — it pulls or it rests, for so many seconds. A fish needs at least one of each, or it can never be landed.")),
                G("What talents give", "the twelve skills the game has",
                    F("talents.changesWhatTalentsGive", "Change it", "Off leaves every talent as the game has it."),
                    F("talents.talents", "Talents", "Each row is one talent. Rows sharing a skill are that skill's talents in the order the talent window shows them, so changing the fourth means listing the first three too. A talent may grant one of your own effects.")),
                G("Overrides on the player", "the game owns the player; these write over its numbers",
                    F("player.overridesTheGamesPlayer", "Change it", "Off leaves the player exactly as the game has them."),
                    F("player.turningCatchesUpAfter", "Turning", "How long they carry on facing the old way after turning, in seconds."),
                    F("player.vehicleDrift", "Vehicle drift", "How much a vehicle slides sideways as it turns, over the length of the turn."),
                    F("player.aimSitsAt", "Aim sits at", "Where what they are aiming at sits, relative to them.")),
                G("Armour sets", "your own sets, and what wearing enough gives",
                    F("armourSets.addsArmourSets", "Add sets", "Off leaves the game's own 62 sets alone."),
                    F("armourSets.sets", "Sets", "Each row is one set: its pieces, which tier of the world it belongs to, how good it is, and what wearing enough of them gives. Two to five pieces is what the game's own sets use. The number on each line is worked out from the tier and the rarity, not typed.")),
                G("Backgrounds", "what a new character starts with",
                    F("backgrounds.changesWhatBackgroundsStartYouWith", "Change it", "Off leaves all eleven backgrounds exactly as the game has them."),
                    F("backgrounds.backgrounds", "Backgrounds", "Each row changes one of the game's eleven: the skill it starts you at level 3 in, and up to two things in the bag. Leave either half of a row empty to keep what the game already gives that background. There is no twelfth background and there cannot be one.")),
                G("When the world acts on its own", "cave-ins, swarms, tentacles",
                    F("worldEvents.changesWhenTheWorldActs", "Change it", "Off leaves all four events exactly as the game has them."),
                    F("worldEvents.events", "Events", "Each row re-aims one of the four: which of the game's biomes, how far from the core, what the ground has to be made of, and the cooldowns. Only the game's own biomes can be named. Your own blocks can be counted in the ground.")),
                G("Your blocks in the game's own caves", "terrain and ore rules",
                    F("gamesOwnTerrain.putsBlocksInTheGamesWorld", "Change it", "Off leaves Core Keeper's own caves exactly as they generate."),
                    F("gamesOwnTerrain.rules", "Rules", "Each row says what the generator decided about a tile and what block to lay because of it. This is how one of your ores ends up in the walls of a vanilla biome. It applies to the whole world, not to one dimension.")),
                G("Skill pictures", "the game's twelve skills",
                    F("skillPictures.changesTheSkillPictures", "Change it", "Off leaves all twelve as the game draws them."),
                    F("skillPictures.skills", "Pictures", "Each row repaints one of the game's twelve skills. Give the gold picture too for how it looks at the highest level. This does not add a thirteenth skill.")));

            // Passes returned the day the ladder genuinely ran them: every dimension now gets
            // a synthesized plan (terrain, then dungeons, then scenes, then ore), and an
            // authored pass slots into that order by its phase and priority. Tables still
            // carry no tab — nothing consumes an authored table's entries yet.
            DimensionCollectionDescriptor passes = new DimensionCollectionDescriptor(
                "passes",
                "Generation Order",
                "pass",
                "No custom passes yet",
                "Your dimension already generates in order — terrain first, then dungeons, then places, then ore. A custom pass adds your own step to that order, or scopes one step to a rectangle of the map.",
                t => Wrap(t.GlobalGenerationPasses),
                t => DimensionFrameworkAuthoringAssetUtility.CreateGenerationPass(t),
                a => Str(a, "displayName", "Pass"),
                a => Str(a, "passId", "pass"),
                G("Basics", "what and when",
                    F("displayName", "Name", "What this step is called."),
                    F("phase", "When", "Which part of generation it belongs to. Steps run phase by phase: terrain before structures, structures before places, places before ore."),
                    F("providerId", "Who Runs It", "Which generator runs this step. The framework's own are expandnullforge:tile-map, expandnullforge:safe-platform, expandnullforge:dungeon-placement, expandnullforge:scene-placement and expandnullforge:ore-scatter."),
                    F("priority", "Order Within When", "Lower runs earlier among steps of the same phase."),
                    F("enabled", "Enabled", "Turn off to keep the step without running it.")),
                G("Where", "scope",
                    F("hasExplicitLocalBounds", "Scoped", "It runs over one rectangle rather than the whole area."),
                    F("explicitLocalMin", "From", "The rectangle's corner nearest the map's origin."),
                    F("explicitLocalMaxExclusive", "To", "The far corner.")));

            return new DimensionStageDescriptor("worldrules", setups, passes);
        }
    }
}
