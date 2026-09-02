using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Turns the kind an author picked into the number Core Keeper reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only place in the framework that knows both vocabularies. <c>DimensionWhatItIs</c> is
    /// written in the words a player would use and lives in the API assembly, which cannot see the
    /// game at all; <c>ObjectType</c> is the game's own enum. Keeping the translation in one method
    /// is what lets the authoring surface stay in plain words without every caller learning the
    /// enum.
    /// </para>
    /// <para>
    /// There is no reverse map. Reading a type back out is the prefab's business, and the two
    /// directions would drift apart the moment either list grew.
    /// </para>
    /// </remarks>
    public static class DimensionItemObjectTypes
    {
        /// <summary>
        /// The swing time the game falls back on when a held item has no cooldown component of its
        /// own. Reproduced here because the durability formula divides by it, and because "zero
        /// means the vanilla default" has to be able to write the vanilla default down.
        /// </summary>
        public const float MeleeFallbackCooldown = 0.4f;

        /// <summary>The same for a ranged, thrown or summoning weapon.</summary>
        public const float RangedFallbackCooldown = 0.6f;

        /// <summary>
        /// The game's number for an authored kind. <c>NotSaid</c> comes back as NonUsable, which is
        /// what the game reads when nothing is written; callers resolve NotSaid before they get here.
        /// </summary>
        public static ObjectType ToObjectType(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.Helmet: return ObjectType.Helm;
                case DimensionWhatItIs.ChestArmour: return ObjectType.BreastArmor;
                case DimensionWhatItIs.PantsArmour: return ObjectType.PantsArmor;
                case DimensionWhatItIs.Necklace: return ObjectType.Necklace;
                case DimensionWhatItIs.Ring: return ObjectType.Ring;
                case DimensionWhatItIs.OffHand: return ObjectType.Offhand;
                case DimensionWhatItIs.Bag: return ObjectType.Bag;
                case DimensionWhatItIs.Lantern: return ObjectType.Lantern;
                case DimensionWhatItIs.Pouch: return ObjectType.Pouch;
                case DimensionWhatItIs.Pet: return ObjectType.Pet;

                case DimensionWhatItIs.MeleeWeapon: return ObjectType.MeleeWeapon;
                case DimensionWhatItIs.RangedWeapon: return ObjectType.RangeWeapon;
                case DimensionWhatItIs.ThrowingWeapon: return ObjectType.ThrowingWeapon;
                case DimensionWhatItIs.SummoningWeapon: return ObjectType.SummoningWeapon;
                case DimensionWhatItIs.BeamWeapon: return ObjectType.BeamWeapon;

                case DimensionWhatItIs.Pickaxe: return ObjectType.MiningPick;
                case DimensionWhatItIs.Shovel: return ObjectType.Shovel;
                case DimensionWhatItIs.Hoe: return ObjectType.Hoe;
                case DimensionWhatItIs.Sledgehammer: return ObjectType.Sledge;
                case DimensionWhatItIs.Drill: return ObjectType.DrillTool;
                case DimensionWhatItIs.RoofingGadget: return ObjectType.RoofingTool;
                case DimensionWhatItIs.Paintbrush: return ObjectType.PaintTool;
                case DimensionWhatItIs.FishingRod: return ObjectType.FishingRod;
                case DimensionWhatItIs.BugNet: return ObjectType.BugNet;
                case DimensionWhatItIs.WateringCan: return ObjectType.WaterCan;
                case DimensionWhatItIs.Bucket: return ObjectType.Bucket;
                case DimensionWhatItIs.Seeder: return ObjectType.Seeder;
                case DimensionWhatItIs.CastItem: return ObjectType.CastingItem;

                case DimensionWhatItIs.SomethingYouPlace: return ObjectType.PlaceablePrefab;
                case DimensionWhatItIs.Food: return ObjectType.Eatable;
                case DimensionWhatItIs.Instrument: return ObjectType.Instrument;
                case DimensionWhatItIs.Valuable: return ObjectType.Valuable;
                case DimensionWhatItIs.KeyItem: return ObjectType.KeyItem;
                case DimensionWhatItIs.UniqueCraftingComponent:
                    return ObjectType.UniqueCraftingComponent;
                case DimensionWhatItIs.Critter: return ObjectType.Critter;

                case DimensionWhatItIs.NothingInParticular:
                case DimensionWhatItIs.NotSaid:
                default:
                    return ObjectType.NonUsable;
            }
        }

        /// <summary>
        /// The time between uses the game itself would apply to this type when the item carries no
        /// cooldown of its own. Never zero.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Read off the slot behaviours. <c>MeleeWeaponSlot</c>, <c>BeamWeaponSlot</c> and
        /// <c>EatableSlot</c> start at 0.4 seconds and <c>RangeWeaponSlot</c> and
        /// <c>SummoningWeaponSlot</c> at 0.6, and each of the five then OVERWRITES that number with
        /// the item's own the moment the item has one — which is the whole reason this method
        /// exists: a cooldown component holding zero is not "no cooldown set", it is a weapon that
        /// swings with no delay at all.
        /// <para>
        /// <c>ShovelSlot</c>, <c>HoeSlot</c> and <c>RoofingToolSlot</c> read no cooldown at all: all
        /// three hand a literal 0.4 to <c>StartCooldownForItem</c> (a shovel 0.15 in god mode) and
        /// never look at the component. A number on one of those is inert, which is also why not one
        /// of the game's own shovels, hoes or roofing gadgets carries the component.
        /// </para>
        /// </para>
        /// <para>
        /// Zero would be a legitimate-looking answer and is deliberately not one of the returns. A
        /// caller that needs to know whether the durability formula will divide by this asks
        /// <see cref="DurabilityDividesByCooldown"/> instead.
        /// </para>
        /// </remarks>
        public static float VanillaCooldownFor(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.RangeWeapon:
                case ObjectType.ThrowingWeapon:
                case ObjectType.SummoningWeapon:
                    return RangedFallbackCooldown;
                default:
                    return MeleeFallbackCooldown;
            }
        }

        /// <summary>
        /// True when the game's durability formula divides by the item's cooldown, so the cooldown
        /// has to be a real number before durability is worked out.
        /// </summary>
        /// <remarks>
        /// Exactly three types, from <c>DurabilityAuthoring.CalculateObjectDurability</c>: melee is
        /// <c>350 x multiplier x 0.4 / cooldown</c> and ranged and thrown are
        /// <c>250 x multiplier x 0.6 / cooldown</c>. Every other type either has a flat constant or
        /// no case at all. A summoning weapon is not here — it is a flat 100 — even though its slot
        /// does read a cooldown.
        /// </remarks>
        public static bool DurabilityDividesByCooldown(ObjectType objectType)
        {
            return objectType == ObjectType.MeleeWeapon ||
                   objectType == ObjectType.RangeWeapon ||
                   objectType == ObjectType.ThrowingWeapon;
        }
    }
}
