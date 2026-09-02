using System;

namespace ExpandNullforge.Api
{
    /// <summary>
    /// Supported custom-item archetypes. An archetype decides which Core Keeper authoring
    /// components a generated object needs, so a creator never hand-assembles a prefab and the
    /// framework never emits a monolithic one. Anything a creator does not customize stays
    /// vanilla.
    /// </summary>
    public enum DimensionItemArchetype
    {
        /// <summary>Plain stackable resource (bar, fibre, dust). No behaviour.</summary>
        Material = 0,

        /// <summary>World pick-uppable found on the ground (e.g. Ancient Feather).</summary>
        Pickup = 1,

        /// <summary>Placeable terrain/building block.</summary>
        Block = 2,

        /// <summary>Placeable decorative or functional object (furniture, decor).</summary>
        Placeable = 3,

        /// <summary>Functional building a player interacts with (workbench-like).</summary>
        Building = 4,

        /// <summary>Minable ore/resource node that yields drops.</summary>
        Ore = 5,

        /// <summary>Breakable prop such as a bush or vase that drops loot.</summary>
        Breakable = 6,

        /// <summary>Food, potion, or other one-shot use item.</summary>
        Consumable = 7,

        /// <summary>Tool such as a pickaxe or watering can.</summary>
        Tool = 8,

        /// <summary>Melee or ranged weapon.</summary>
        Weapon = 9,

        /// <summary>Equippable armour piece (may participate in a set bonus).</summary>
        Armor = 10,

        /// <summary>Passive creature or critter.</summary>
        Creature = 11,

        /// <summary>Hostile mob.</summary>
        Mob = 12,

        /// <summary>Boss encounter.</summary>
        Boss = 13,

        /// <summary>
        /// A bomb: an item you place, which goes off and damages what is around it.
        /// </summary>
        /// <remarks>
        /// Core Keeper builds a bomb out of TWO objects, never one — the thing you place, and the
        /// blast it turns into — and the numbers live on different halves of that pair. How much it
        /// hurts and how much terrain it breaks are on the bomb; how far the blast reaches is on the
        /// blast. The framework generates the second object for the author so a bomb stays one
        /// thing they make.
        /// </remarks>
        Explosive = 14,

        /// <summary>Creator-defined; the framework validates only the shared basics.</summary>
        Custom = 100
    }

    /// <summary>
    /// Core Keeper authoring components a generated object may need. Flags so an archetype can
    /// declare exactly its required set and the generator emits nothing more.
    /// </summary>
    [Flags]
    public enum DimensionItemAuthoringComponents
    {
        None = 0,

        /// <summary>ObjectAuthoring: object id, type, rarity. Always required.</summary>
        Object = 1 << 0,

        /// <summary>LocalizationAuthoring + TextDataBlock name/description. Always required.</summary>
        Localization = 1 << 1,

        /// <summary>SpriteObject visual + SpriteAsset/icon. Always required.</summary>
        Visual = 1 << 2,

        /// <summary>InventoryItemAuthoring: stack, buy/sell, loot sprite, crafting.</summary>
        InventoryItem = 1 << 3,

        /// <summary>Placement/occupancy authoring for world-placed objects.</summary>
        Placement = 1 << 4,

        /// <summary>DurabilityAuthoring for equipment wear and reinforcement.</summary>
        Durability = 1 << 5,

        /// <summary>CooldownAuthoring for handheld slot cooldowns.</summary>
        Cooldown = 1 << 6,

        /// <summary>WeaponDamageAuthoring damage scaling.</summary>
        WeaponDamage = 1 << 7,

        /// <summary>GivesConditionsWhenEquippedAuthoring stat modifiers / set bonuses.</summary>
        EquipmentConditions = 1 << 8,

        /// <summary>SecondaryUseAuthoring right-click ability.</summary>
        SecondaryUse = 1 << 9,

        /// <summary>Health/breakable authoring for props and nodes.</summary>
        Breakable = 1 << 10,

        /// <summary>Loot table / drop authoring.</summary>
        Loot = 1 << 11,

        /// <summary>Creature AI, health, and spawn authoring.</summary>
        Creature = 1 << 12,

        /// <summary>Boss-specific arena/encounter hooks.</summary>
        BossEncounter = 1 << 13,

        /// <summary>
        /// ExplosiveAuthoring plus whatever sets the bomb off, and the blast object it names.
        /// </summary>
        Explosive = 1 << 14
    }

    /// <summary>
    /// Maps an archetype to the authoring components its generated object requires. This is the
    /// contract the item generator and the authoring dashboard both read, so the fields a
    /// creator sees and the components that get emitted can never drift apart.
    /// </summary>
    public static class DimensionItemArchetypeRules
    {
        /// <summary>Components every generated object needs regardless of archetype.</summary>
        public const DimensionItemAuthoringComponents Always =
            DimensionItemAuthoringComponents.Object |
            DimensionItemAuthoringComponents.Localization |
            DimensionItemAuthoringComponents.Visual;

        public static DimensionItemAuthoringComponents GetRequiredComponents(
            DimensionItemArchetype archetype)
        {
            switch (archetype)
            {
                case DimensionItemArchetype.Material:
                case DimensionItemArchetype.Pickup:
                    return Always | DimensionItemAuthoringComponents.InventoryItem;

                case DimensionItemArchetype.Block:
                case DimensionItemArchetype.Placeable:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Placement;

                case DimensionItemArchetype.Building:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Placement |
                           DimensionItemAuthoringComponents.SecondaryUse;

                case DimensionItemArchetype.Ore:
                case DimensionItemArchetype.Breakable:
                    return Always |
                           DimensionItemAuthoringComponents.Placement |
                           DimensionItemAuthoringComponents.Breakable |
                           DimensionItemAuthoringComponents.Loot;

                case DimensionItemArchetype.Consumable:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Cooldown |
                           DimensionItemAuthoringComponents.SecondaryUse;

                // A TOOL HITS THINGS. Every one of the game's own picks, shovels, hoes, sledges,
                // drills and seeders carries WeaponDamageAuthoring, and it is not decoration:
                // LevelEntitiesBufferConverter returns without building a LevelEntitiesBuffer for
                // anything that has none, QueueHitSystem then never finds a level entity and never
                // assigns the swing any damage, and InventoryUtility.CanBeRepaired fails on its
                // first clause. Without it a generated pickaxe mines nothing and can never be
                // repaired — which is what every tool this framework made did, until
                // WeaponDamage joined the list below.
                case DimensionItemArchetype.Tool:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Durability |
                           DimensionItemAuthoringComponents.Cooldown |
                           DimensionItemAuthoringComponents.WeaponDamage |
                           DimensionItemAuthoringComponents.SecondaryUse;

                case DimensionItemArchetype.Weapon:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Durability |
                           DimensionItemAuthoringComponents.Cooldown |
                           DimensionItemAuthoringComponents.WeaponDamage |
                           DimensionItemAuthoringComponents.SecondaryUse;

                case DimensionItemArchetype.Armor:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Durability |
                           DimensionItemAuthoringComponents.EquipmentConditions;

                case DimensionItemArchetype.Creature:
                    return Always |
                           DimensionItemAuthoringComponents.Creature |
                           DimensionItemAuthoringComponents.Loot;

                case DimensionItemArchetype.Mob:
                    return Always |
                           DimensionItemAuthoringComponents.Creature |
                           DimensionItemAuthoringComponents.Breakable |
                           DimensionItemAuthoringComponents.Loot;

                case DimensionItemArchetype.Boss:
                    return Always |
                           DimensionItemAuthoringComponents.Creature |
                           DimensionItemAuthoringComponents.Breakable |
                           DimensionItemAuthoringComponents.Loot |
                           DimensionItemAuthoringComponents.BossEncounter;

                // A bomb is carried, placed, and goes off. It gets health as well, because a bomb
                // with none cannot be shot, broken, or set off by another blast — chain reactions
                // are the whole appeal of a pile of bombs and they run through the health pool.
                // Breakable is deliberately NOT used: that flag also means "drops a loot table",
                // and vanilla's bombs drop nothing when they go off.
                case DimensionItemArchetype.Explosive:
                    return Always |
                           DimensionItemAuthoringComponents.InventoryItem |
                           DimensionItemAuthoringComponents.Placement |
                           DimensionItemAuthoringComponents.Explosive;

                case DimensionItemArchetype.Custom:
                default:
                    return Always;
            }
        }

        /// <summary>True when the archetype produces an object placed in the world.</summary>
        public static bool IsWorldPlaced(DimensionItemArchetype archetype)
        {
            return Requires(archetype, DimensionItemAuthoringComponents.Placement) ||
                   Requires(archetype, DimensionItemAuthoringComponents.Creature);
        }

        /// <summary>True when the archetype carries an inventory representation.</summary>
        public static bool IsInventoryItem(DimensionItemArchetype archetype)
        {
            return Requires(archetype, DimensionItemAuthoringComponents.InventoryItem);
        }

        public static bool Requires(
            DimensionItemArchetype archetype,
            DimensionItemAuthoringComponents component)
        {
            return (GetRequiredComponents(archetype) & component) == component;
        }

        /// <summary>Short label for dashboards and generated documentation.</summary>
        public static string Describe(DimensionItemArchetype archetype)
        {
            switch (archetype)
            {
                case DimensionItemArchetype.Material: return "Material";
                case DimensionItemArchetype.Pickup: return "World pickup";
                case DimensionItemArchetype.Block: return "Block";
                case DimensionItemArchetype.Placeable: return "Placeable object";
                case DimensionItemArchetype.Building: return "Building";
                case DimensionItemArchetype.Ore: return "Ore node";
                case DimensionItemArchetype.Breakable: return "Breakable prop";
                case DimensionItemArchetype.Consumable: return "Consumable";
                case DimensionItemArchetype.Tool: return "Tool";
                case DimensionItemArchetype.Weapon: return "Weapon";
                case DimensionItemArchetype.Armor: return "Armor";
                case DimensionItemArchetype.Creature: return "Creature";
                case DimensionItemArchetype.Mob: return "Mob";
                case DimensionItemArchetype.Boss: return "Boss";
                case DimensionItemArchetype.Explosive: return "Bomb";
                case DimensionItemArchetype.Custom: return "Custom";
                default: return archetype.ToString();
            }
        }

        /// <summary>
        /// Human-readable list of the Core Keeper authoring components this archetype will emit,
        /// so a creator can see what their choice actually builds before generating anything.
        /// </summary>
        public static string DescribeComponents(DimensionItemArchetype archetype)
        {
            DimensionItemAuthoringComponents required = GetRequiredComponents(archetype);
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            Append(builder, required, DimensionItemAuthoringComponents.Object, "ObjectAuthoring");
            Append(builder, required, DimensionItemAuthoringComponents.Localization, "Localization");
            Append(builder, required, DimensionItemAuthoringComponents.Visual, "Sprite");
            Append(builder, required, DimensionItemAuthoringComponents.InventoryItem, "InventoryItem");
            Append(builder, required, DimensionItemAuthoringComponents.Placement, "Placement");
            Append(builder, required, DimensionItemAuthoringComponents.Durability, "Durability");
            Append(builder, required, DimensionItemAuthoringComponents.Cooldown, "Cooldown");
            Append(builder, required, DimensionItemAuthoringComponents.WeaponDamage, "WeaponDamage");
            Append(builder, required, DimensionItemAuthoringComponents.EquipmentConditions, "EquipConditions");
            Append(builder, required, DimensionItemAuthoringComponents.SecondaryUse, "SecondaryUse");
            Append(builder, required, DimensionItemAuthoringComponents.Breakable, "Breakable");
            Append(builder, required, DimensionItemAuthoringComponents.Loot, "Loot");
            Append(builder, required, DimensionItemAuthoringComponents.Creature, "Creature");
            Append(builder, required, DimensionItemAuthoringComponents.BossEncounter, "BossEncounter");
            Append(builder, required, DimensionItemAuthoringComponents.Explosive, "Explosive");

            return builder.Length == 0 ? "no components" : builder.ToString();
        }

        private static void Append(
            System.Text.StringBuilder builder,
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component,
            string label)
        {
            if ((required & component) != component)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }
    }
}
