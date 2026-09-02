using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Cooldown, stacking, durability, and what an item of this kind must have.
    /// </summary>
    internal static partial class DimensionItemGenerator
    {
        /// <summary>
        /// Refuses to build a mod so large that an ingredient cannot be remembered by a dish.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cooked dish stores the two ingredients that made it packed into one integer, sixteen
        /// bits each. An ingredient whose object id is above 65535 loses its high bits on the way
        /// back out, and the dish is then tinted, buffed and named after whatever object happens to
        /// sit at the truncated number. Nothing anywhere reports it — not the pot, not the cook
        /// book, not the save. This is the identity-gate failure the framework exists to catch.
        /// </para>
        /// <para>
        /// Only half of it is knowable here. A mod's object ids are handed out at load, starting at
        /// 32768 and continuing past whatever loaded before it, so the FINAL number depends on the
        /// player's load order and the runtime check is the one that sees it. What IS knowable is
        /// the best case: a mod alone at the front of the load order gets 32768 upwards, so a mod
        /// carrying more objects than the room between 32768 and 65535 has ingredients that cannot
        /// fit under ANY load order. That is a build error, not a warning, because there is no
        /// version of the shipped mod in which it works.
        /// </para>
        /// </remarks>
        private static void CheckIngredientsCanFitInADish(
            List<DimensionItemAsset> items,
            int ingredientCount,
            DimensionItemGenerationReport report)
        {
            if (ingredientCount == 0)
            {
                return;
            }

            const int FirstModObjectId = 32768;
            int room = ExpandNullforge.Food.DimensionFoodPairing.MaximumPackableObjectId
                - FirstModObjectId + 1;
            if (items.Count <= room)
            {
                return;
            }

            report.Errors.Add(
                "This mod defines " + items.Count + " objects and " + ingredientCount +
                " of them are cooking ingredients. A dish can only remember an ingredient whose " +
                "object number is " +
                ExpandNullforge.Food.DimensionFoodPairing.MaximumPackableObjectId +
                " or below, and a mod is numbered from " + FirstModObjectId + " upwards, so at " +
                "most " + room + " objects can come before an ingredient. Past that a cooked dish " +
                "quietly remembers the wrong ingredient — wrong colours, wrong effects, wrong " +
                "name. Split this mod in two, or move the ingredients earlier in it.");
        }

        /// <summary>
        /// The one time-between-uses this item ships with, or zero for "it does not have one".
        /// </summary>
        /// <remarks>
        /// <para>
        /// ONE FIELD, NOT TWO. With a second one inside the effects block, the effects pass runs
        /// last and wins: whenever its own field is blank it deletes the cooldown component and
        /// takes the creator's number with it. The item's own field is the one that is drawn and
        /// the one three archetypes ask for, so it is the one that counts;
        /// <c>DimensionItemAsset.CooldownSeconds</c> falls back to the effects-block field when the
        /// item's own is blank, so no asset loses a number it already holds.
        /// </para>
        /// <para>
        /// A blank field on a template that asks for a cooldown gets the game's own number rather
        /// than a zero. A present <c>CooldownCD</c> holding zero is not "nothing set": every slot
        /// that reads one starts at the game's default and then overwrites it with the item's,
        /// so zero is a weapon that swings with no delay at all.
        /// </para>
        /// </remarks>
        private static float ResolveCooldownSeconds(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            DimensionItemAuthoringComponents required,
            DimensionItemGenerationReport report)
        {
            if (item.CooldownSeconds > 0f)
            {
                return item.CooldownSeconds;
            }

            if (!Requires(required, DimensionItemAuthoringComponents.Cooldown) &&
                !item.Effects.SharesACooldown)
            {
                return 0f;
            }

            float vanilla = DimensionItemObjectTypes.VanillaCooldownFor(
                DimensionItemObjectTypes.ToObjectType(kind));

            // Derived rather than a warning: the item card already says a blank one takes the
            // game's default, and saying it twice for every unhurried item would bury the lines
            // that matter.
            report.Derived.Add(
                Describe(item) + ": no time between uses was set, so the game's own " + vanilla +
                " seconds went in. Left at zero it would have had no delay at all, not the default.");
            return vanilla;
        }

        /// <summary>
        /// Whether several of these share one inventory slot, decided by what the thing is.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The kind decides because <c>initialAmount</c> means two things and the game picks between
        /// them by this one bool: on anything that wears out it is the durability the item starts
        /// with, and on anything that stacks it is the number a player is handed. Counted over the
        /// 3,073 vanilla prefabs, not one of the 343 carrying a durability pool is stackable — so a
        /// generated helmet answering "yes, it stacks" is handed out ninety at a time.
        /// </para>
        /// <para>
        /// Where the game itself goes both ways the creator's answer stands: off-hands (8 of 38
        /// stack), ranged weapons (1 of 35), cast items (45 of 60), placeables (1,260 of 1,281) and
        /// things that are nothing in particular (123 of 139). Where it never goes both ways the
        /// kind wins and the report says it did.
        /// </para>
        /// </remarks>
        private static bool ResolveStacking(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            bool keepsADurabilityPool,
            DimensionItemGenerationReport report)
        {
            bool stacks = DimensionWhatItIsRules.StacksGiven(kind, item.Stackable);
            DimensionStacking rule = DimensionWhatItIsRules.StackingFor(kind);
            string it = DimensionWhatItIsRules.Describe(kind);

            // The shared-timer check is deliberately not in this method. It lives beside the
            // write, in DimensionObjectSpine.ApplyItemEffects, which both this generator and the
            // world-object generator go through; put back here, a world object could never reach
            // it.

            if (rule == DimensionStacking.NeverStacks && item.Stackable)
            {
                report.Derived.Add(
                    Describe(item) + " was generated as one per slot. It is " + it +
                    ", and none of the game's own stack.");
            }
            else if (rule == DimensionStacking.AlwaysStacks && !item.Stackable)
            {
                report.Derived.Add(
                    Describe(item) + " was generated as a stack. It is " + it +
                    ", and all of the game's own stack.");
            }

            if (stacks && keepsADurabilityPool)
            {
                stacks = false;
                report.Warnings.Add(
                    Describe(item) + ": it wears out AND it was set to stack, and it cannot do " +
                    "both — the same number is the uses left on one of them and the size of the " +
                    "pile. It was generated as one per slot. Untick stacking, or take the " +
                    "durability off it.");
            }

            return stacks;
        }

        /// <summary>
        /// Works out the durability and the starting amount the way Core Keeper works them out, and
        /// writes both.
        /// </summary>
        /// <remarks>
        /// <para>
        /// No new mechanism: this calls the game's own <c>CalculateObjectDurability</c> on the
        /// component that is already on the object, which is the same method the SDK runs on prefab
        /// import. Doing it here means the prefab on disk already holds the number, so nothing
        /// depends on an import having happened.
        /// </para>
        /// <para>
        /// <c>initialAmount</c> is the same field twice over. On anything carrying a durability pool
        /// the game reads it as the durability the item starts with — the crafting preview reads it
        /// verbatim, and the durability bar is this over maxDurability. On everything else it is the
        /// stack the player is handed. Vanilla always keeps the first equal to the computed maximum,
        /// and writes 1 for the second on all but a handful of objects.
        /// </para>
        /// <para>
        /// THE AUTHOR'S OWN NUMBER GOES INTO <c>initialAmount</c>, NOT INTO <c>durability</c>. That
        /// is not a preference; it is the only field it survives in.
        /// <c>DurabilityAuthoring.OnValidate</c> recomputes <c>durability</c> and
        /// <c>maxDurability</c> from the object's type and <c>initialAmount</c> on every prefab
        /// import, so a number written straight into either of them lasts until the next import and
        /// no longer. Fed in as the amount, it is the value the game's own formula hands back
        /// unchanged for every kind the formula has no case for — a seeder, a fishing rod, a bag, a
        /// lantern, anything cast — which is exactly the set where the typed number is the only
        /// durability there is. On a kind the formula does have a case for, the formula wins, which
        /// is what a helmet reading 90 and a pickaxe reading 800 means.
        /// </para>
        /// </remarks>
        private static void ApplyDurabilityTheGameWay(
            GameObject root,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring == null)
            {
                return;
            }

            ObjectType objectType = objectAuthoring.objectType;
            int authored = item.AuthoredDurability;

            DurabilityAuthoring durability = root.GetComponent<DurabilityAuthoring>();
            if (durability == null)
            {
                // Nothing here wears out, so initialAmount is the stack the player is handed, which
                // vanilla writes as 1 on all but a handful of objects.
                objectAuthoring.initialAmount = 1;

                // AND SAY SO IF THEY FILLED THE BOXES IN. Wear, repair and reinforce are drawn on
                // every item, and thirteen of the sixteen templates never get a durability pool at
                // all — so those four numbers were typed, saved, and thrown away here without a
                // word. They still are thrown away; what changes is that the person is told.
                if (item.HasWearSettingsThatWillBeIgnored)
                {
                    report.Warnings.Add(
                        Describe(item) +
                        ": wear, repair or reinforce numbers are filled in, but a " +
                        DimensionItemArchetypeRules.Describe(item.Archetype) +
                        " never wears out, so none of them are read. Only a tool, a weapon and a " +
                        "piece of armour have durability — change the template, or clear those " +
                        "numbers.");
                }

                return;
            }

            // A thrown weapon is a stack, not a pool. Not one of the game's seven carries a
            // durability component, and on a stackable item initialAmount IS the number handed
            // over — so running the formula here would deal out 250 knives at a time and hang a
            // durability bar off a stack that has no maximum to fill. An author who typed a
            // durability anyway is taken at their word and keeps the pool; ResolveStacking above
            // has already taken the stacking off that one, because nothing in the game does both.
            if (objectType == ObjectType.ThrowingWeapon && authored <= 0)
            {
                RemoveComponentIfPresent<DurabilityAuthoring>(root);
                objectAuthoring.initialAmount = 1;
                report.Derived.Add(
                    Describe(item) + " was generated without a durability pool and as a stack of " +
                    "one, the way all seven of the game's throwing weapons are. Type a number " +
                    "under Durability points if you want one that wears out instead.");
                return;
            }

            int computed = DurabilityTheGameWouldGive(
                root, durability, authored > 0 ? authored : 1, objectType, item, report);
            durability.durability = computed;
            durability.maxDurability = computed;
            objectAuthoring.initialAmount = computed;
        }

        /// <summary>
        /// The number the game's own formula gives for this object, without changing the object to
        /// get it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Melee, ranged and thrown are the three types whose durability is divided by the time
        /// between uses. A cooldown component sitting at zero would divide by nothing and round
        /// infinity, which lands on int.MinValue — a weapon that reads as permanently reinforced,
        /// draws a negative bar, and breaks on its first swing.
        /// </para>
        /// <para>
        /// The cooldown site in <c>Configure</c> never leaves a zero behind any more, so the guard
        /// below is insurance rather than the everyday path. It is worth having because the cost of
        /// being wrong is silent and total. It closes the hole by writing the game's own number
        /// into the field: switching the component off would leave a <c>CooldownCD</c> of zero
        /// behind, because the converter reads the component through <c>GetComponent</c> and never
        /// asks whether it is enabled, and a present zero is what a slot reads as no delay at all.
        /// </para>
        /// </remarks>
        private static int DurabilityTheGameWouldGive(
            GameObject root,
            DurabilityAuthoring durability,
            int amount,
            ObjectType objectType,
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            CooldownAuthoring cooldown = root.GetComponent<CooldownAuthoring>();
            bool wouldDivideByNothing =
                DimensionItemObjectTypes.DurabilityDividesByCooldown(objectType) &&
                cooldown != null &&
                cooldown.enabled &&
                cooldown.cooldown <= 0f;

            if (!wouldDivideByNothing)
            {
                return durability.CalculateObjectDurability(amount, objectType);
            }

            // Written into the field, not switched off. A disabled cooldown component still bakes a
            // CooldownCD of zero — the converter reads it through GetComponent and never asks
            // whether it is enabled — and every slot honours a present zero as no delay at all. The
            // game's own number is the only thing that leaves both the durability sum and the swing
            // rate right.
            float vanilla = DimensionItemObjectTypes.VanillaCooldownFor(objectType);
            cooldown.cooldown = vanilla;
            report.Warnings.Add(
                Describe(item) + ": its time between uses came out at zero, which its durability " +
                "would have been divided by. The game's own " + vanilla + " seconds went in " +
                "instead. Set a time between uses on the item.");

            // The fallback divisor and the numerator's own constant are the same number, so the
            // base comes back undivided: 350 for a swing, 250 for a shot or a throw.
            float baseDurability = objectType == ObjectType.MeleeWeapon ? 350f : 250f;
            return Mathf.RoundToInt(baseDurability * durability.durabilityMultiplier);
        }

        /// <summary>
        /// The kind the item said it was, or the one its other answers imply when it never said.
        /// </summary>
        /// <remarks>
        /// The item's own blocks are asked before the template is, because they are more specific:
        /// an item whose weapon block already says "swung" has said it is a melee weapon in
        /// everything but name, and a piece of armour that says it is drawn on the head has said it
        /// is a helmet. Only when neither has anything to say does the template decide.
        /// </remarks>
        private static DimensionWhatItIs ResolveWhatItIs(
            DimensionItemAsset item,
            DimensionItemGenerationReport report)
        {
            DimensionWhatItIs kind = item.ResolveWhatItIs(out DimensionWhatItIsSource source);
            string it = DimensionWhatItIsRules.Describe(kind);

            switch (source)
            {
                case DimensionWhatItIsSource.HowItAttacks:
                    report.Derived.Add(
                        Describe(item) + " was generated as " + it +
                        ", from how it says it attacks.");
                    break;

                case DimensionWhatItIsSource.WhereItIsWorn:
                    report.Derived.Add(
                        Describe(item) + " was generated as " + it +
                        ", from where it says it is drawn on the character.");
                    break;

                case DimensionWhatItIsSource.ItsTemplate:
                    if (DimensionWhatItIsRules.ArchetypeCannotSayWhatItIs(item.Archetype))
                    {
                        report.Warnings.Add(
                            Describe(item) + ": nothing on it says what it is, and a " +
                            DimensionItemArchetypeRules.Describe(item.Archetype).ToLowerInvariant() +
                            " covers several kinds at once, so it was generated as " + it +
                            ". Set What It Is to the one you meant.");
                    }
                    else
                    {
                        report.Derived.Add(
                            Describe(item) + " was generated as " + it + ", from its template.");
                    }

                    break;
            }

            return kind;
        }

        /// <summary>
        /// The kinds where "it starts at one use" is a problem rather than the right answer.
        /// </summary>
        /// <remarks>
        /// A necklace, a ring and a thrown weapon are meant not to wear out — the first two reach an
        /// empty case in the game's formula on purpose, and no vanilla throwing weapon carries a
        /// pool at all. The kinds that never said what they are, or said nothing in particular, are
        /// left out because they already get a louder line of their own.
        /// </remarks>
        private static bool WearingOutIsMeaningfulFor(DimensionWhatItIs kind)
        {
            switch (kind)
            {
                case DimensionWhatItIs.NotSaid:
                case DimensionWhatItIs.NothingInParticular:
                case DimensionWhatItIs.SomethingYouPlace:
                case DimensionWhatItIs.Critter:
                case DimensionWhatItIs.Food:
                case DimensionWhatItIs.Necklace:
                case DimensionWhatItIs.Ring:
                case DimensionWhatItIs.ThrowingWeapon:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Says where the kind and the rest of the item disagree, in the terms the author answered
        /// in. Nothing here stops generation: each of these produces an object that exists, sits in
        /// the right slot, and quietly does nothing, which is exactly the failure a creator cannot
        /// see without being told.
        /// </summary>
        private static void ReportKindProblems(
            DimensionItemAsset item,
            DimensionWhatItIs kind,
            DimensionItemAuthoringComponents required,
            DimensionItemGenerationReport report)
        {
            string it = DimensionWhatItIsRules.Describe(kind);

            // PlaceObjectSlot refuses to put anything down that is not a placeable prefab, a critter
            // or cattle. Both halves of that are worth saying, because both produce an object that
            // looks finished.
            bool placedInTheWorld =
                Requires(required, DimensionItemAuthoringComponents.Placement) ||
                Requires(required, DimensionItemAuthoringComponents.Creature);
            if (kind == DimensionWhatItIs.SomethingYouPlace && !placedInTheWorld)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", but its template attaches nothing that " +
                    "places it, so the game has no footprint to put down. Use the Placeable " +
                    "object template, or say what it is some other way.");
            }
            else if (placedInTheWorld &&
                     kind != DimensionWhatItIs.SomethingYouPlace &&
                     kind != DimensionWhatItIs.Critter)
            {
                // Pet and Food are NOT exempt here, whatever the shape of the rule suggests. The
                // game routes a pet to the gear slots and food to the eating slot, and
                // PlaceObjectSlot turns both away — so a pet or a dish on a placing template is
                // exactly the case worth saying out loud, not one to skip.
                report.Warnings.Add(
                    Describe(item) + ": its template places it in the world, but it is " + it +
                    ". The game only lets a player set down something you place or a critter, so " +
                    "this one can be carried and never put anywhere.");
            }

            if (DimensionWhatItIsRules.IsAWeapon(kind))
            {
                if (!item.Weapon.IsAWeapon)
                {
                    report.Warnings.Add(
                        Describe(item) + ": it is " + it + ", but its 'As a weapon' block says it " +
                        "is not a weapon, so nothing on it makes the player swing, fire or cast.");
                }

                // A summoning weapon is the exception on purpose: not one of the game's seven
                // carries weapon damage, because the thing it summons does the hitting.
                if (kind != DimensionWhatItIs.SummoningWeapon &&
                    !Requires(required, DimensionItemAuthoringComponents.WeaponDamage))
                {
                    report.Warnings.Add(
                        Describe(item) + ": it is " + it + ", but its template carries no damage, " +
                        "so it equips to a weapon slot and hits for nothing.");
                }
            }

            // A DIGGING TOOL HITS FOR ITS MULTIPLIER, and that number can be typed to zero. The
            // block above only covers the five kinds a player fights with, so the six that dig,
            // smash and drill fell through it — and a pick with the multiplier zeroed carries a
            // damage component that computes nothing per level, mines nothing, and said nothing.
            // The multiplier is what the game's per-level curve reads, so it is the number named.
            if (Requires(required, DimensionItemAuthoringComponents.WeaponDamage) &&
                !DimensionWhatItIsRules.IsAWeapon(kind) &&
                item.DamageAmount <= 0 &&
                item.DamageMultiplierForItsTier <= 0f)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and both its damage and its damage " +
                    "multiplier are zero, so it hits for nothing at every level and breaks nothing " +
                    "it is swung at. Put the multiplier back to 1, or type a damage.");
            }

            if (DimensionWhatItIsRules.IsWornArmour(kind) && !item.EquipmentSkin.IsWorn)
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and it will equip and grant its stats, " +
                    "but nothing under 'Worn on the character' says how to draw it, so the " +
                    "character on screen stays bare.");
            }

            if (DimensionWhatItIsRules.AlwaysWearsOut(kind) &&
                !Requires(required, DimensionItemAuthoringComponents.Durability))
            {
                // Counted rather than assumed. Melee 35 of 36, ranged 32 of 35, pickaxe 9 of 10 and
                // beam 2 of 4 wear out; the ones that do not are legendary gear, the snowball and
                // the lightning gun. Telling somebody building an unbreakable legendary that
                // vanilla has none of those would be a lie they could check in ten seconds.
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and " +
                    (DimensionWhatItIsRules.SomeVanillaOnesNeverWearOut(kind)
                        ? "every vanilla one but the legendary gear wears out"
                        : "every vanilla one of those wears out") +
                    ", but its template carries no durability, so this one never will.");
            }

            // The kinds the game keeps no durability number for. A hoe works its 250 out on its
            // own; a seeder, which a player would call the same sort of tool, does not — both of
            // vanilla's carry 250 because their prefabs say 250. Nothing about the item shows the
            // difference, so it has to be said.
            if (Requires(required, DimensionItemAuthoringComponents.Durability) &&
                item.AuthoredDurability <= 0 &&
                DimensionWhatItIsRules.TheGameHasNoDurabilityNumberFor(kind) &&
                WearingOutIsMeaningfulFor(kind))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", and the game has no durability of its " +
                    "own for one — the ones it ships carry a number written on them. Nothing is " +
                    "typed under Durability points, so this starts at 1 use and breaks the first " +
                    "time it is used.");
            }

            string missing = DimensionWhatItIsRules.MissingPieceFor(kind);
            if (!string.IsNullOrEmpty(missing))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", which needs " + missing +
                    " to do anything. The framework has no answer for that yet, so it will take " +
                    "its slot and sit there.");
            }

            if (kind == DimensionWhatItIs.Instrument &&
                (item.Instrument == null || !item.Instrument.IsAnInstrument))
            {
                report.Warnings.Add(
                    Describe(item) + ": it is " + it + ", but its music block does not say it is " +
                    "one, so it takes the instrument slot and plays nothing.");
            }

            // Food is the one kind the item does not get to assert on its own: the cooking pass sets
            // the type from the cooking answer and clears it again when that answer says no.
            bool takesPartInCooking = item.Cooking != null && item.Cooking.TakesPartInCooking;
            if (kind == DimensionWhatItIs.Food && !takesPartInCooking)
            {
                report.Warnings.Add(
                    Describe(item) + ": it says it is food, but its cooking block says it plays no " +
                    "part in cooking, and that is the answer the game reads. It was generated as " +
                    "nothing in particular. Set 'Part in Cooking' under 'As food'.");
            }
            else if (takesPartInCooking &&
                     item.WhatItIs != DimensionWhatItIs.NotSaid &&
                     item.WhatItIs != DimensionWhatItIs.Food)
            {
                // Only when the author picked something else by hand. A worked-out kind is not a
                // disagreement: every ordinary food item is a Material with a cooking block, and
                // saying so on each of them would bury the lines that matter.
                report.Warnings.Add(
                    Describe(item) + ": its cooking block makes it food, and the game decides " +
                    "eating from the same answer that decides a slot, so it was generated as food " +
                    "rather than " + it + ".");
            }
        }
    }
}
