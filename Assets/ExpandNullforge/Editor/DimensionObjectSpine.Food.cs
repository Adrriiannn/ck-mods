using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The spine a cooked or raw edible carries.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// What part an item plays in cooking, what it lends the dish, and what eating it gives.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE CATEGORY TAG IS WHAT LETS THE POT TAKE IT AT ALL. The cooking pot's two input slots
        /// accept objects by tag, not by component, so an ingredient with a perfect
        /// <c>CookingIngredientAuthoring</c> and no <c>CookingIngredient</c> tag simply cannot be
        /// dropped in — the slot refuses it and nothing says why. The tag is stamped here rather
        /// than left to the creator for exactly that reason. The two cooked-food tags are the same
        /// story for anything that reads dishes by tier.
        /// </para>
        /// <para>
        /// RAW AND COOKED ARE TWO HALVES OF ONE ENTRY. Core Keeper stores what a food gives as pairs
        /// — the raw payload and the cooked payload side by side — and picks the cooked half for
        /// anything that came out of a pot. Writing one value into both halves, which is what this
        /// did before, threw away the whole "better when cooked" mechanic; the two lists are now
        /// zipped, longest wins, and a missing half repeats its partner.
        /// </para>
        /// <para>
        /// THE ORDER OF THIS CALL MATTERS. It appends to the same consumed-conditions component that
        /// <see cref="ApplyItemEffects"/> writes, and it can only append safely because that method
        /// runs first and always either replaces the whole list or removes the component. Move this
        /// above it and every regenerate would stack another copy of the ingredient's effects on
        /// top of the last.
        /// </para>
        /// <para>
        /// A dish's <c>rareVersion</c>/<c>epicVersion</c> and an ingredient's dish are ObjectIDs. A
        /// vanilla target is a known number and is baked here; one of the mod's own is not knowable
        /// offline and is filled in at runtime by the food hydration system, so it is left at None
        /// and NOT warned about.
        /// </para>
        /// </remarks>
        public static void ApplyCooking(
            GameObject root,
            DimensionCookingTemplate cooking,
            Sprite ownPicture,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, DimensionCookingTemplate> findIngredient,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> reportUnknownCondition,
            System.Action<string> report)
        {
            if (cooking == null || !cooking.TakesPartInCooking)
            {
                RemoveComponentIfPresent<CookingIngredientAuthoring>(root);
                RemoveComponentIfPresent<CookedFoodAuthoring>(root);
                RemoveComponentIfPresent<FishAuthoring>(root);
                SetCategoryTag(root, ObjectCategoryTag.CookingIngredient, false);
                SetCategoryTag(root, ObjectCategoryTag.UncommonOrLowerCookedFood, false);
                SetCategoryTag(root, ObjectCategoryTag.RareOrHigherCookedFood, false);
                StopItBeingEdible(root);
                return;
            }

            if (!cooking.IsAnIngredient)
            {
                RemoveComponentIfPresent<CookingIngredientAuthoring>(root);
                RemoveComponentIfPresent<FishAuthoring>(root);
            }

            if (!cooking.IsACookedDish)
            {
                RemoveComponentIfPresent<CookedFoodAuthoring>(root);
            }

            MakeItEdible(root);

            if (cooking.IsAnIngredient)
            {
                ApplyIngredient(
                    root,
                    cooking,
                    ownPicture,
                    resolveObject,
                    isOneOfOurOwn,
                    reportUnknownCondition,
                    report);
                return;
            }

            ApplyDish(root, cooking, resolveObject, findIngredient, isOneOfOurOwn, report);
        }

        /// <summary>
        /// Marks the object as something a player can eat.
        /// </summary>
        /// <remarks>
        /// EATING IS DECIDED BY THE OBJECT TYPE, NOT BY HAVING FOOD DATA ON IT. The game routes a
        /// held item to a slot behaviour by its <c>ObjectType</c>, and only Eatable reaches the
        /// eating slot — so an ingredient with a full set of consume conditions and any other type
        /// is a thing that grants nothing because it can never be eaten. Every one of the game's 79
        /// ingredients and 45 dishes carries type 1100, including the ones that can also be placed
        /// in the world; placement rides on separate components and does not compete for the type.
        /// Set here rather than left to the creator because "it is food" is already the answer they
        /// gave.
        /// </remarks>
        private static void MakeItEdible(GameObject root)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null)
            {
                objectAuthoring.objectType = ObjectType.Eatable;
            }
        }

        /// <summary>
        /// Undoes <see cref="MakeItEdible"/> for something that has stopped being food.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only ever changes a type that says Eatable, and leaves every other value alone.
        /// Re-running generation must not leave an item that is no longer food still sitting in the
        /// eating slot.
        /// </para>
        /// <para>
        /// It used to be true that Eatable could only have come from this pass. It is not any more:
        /// an author can now say "this is food" on the item itself, and this method resets that too.
        /// That is on purpose and the item generator depends on it — the cooking block is the single
        /// source for food, because the game decides eating from the same answer that decides a
        /// slot, and two answers that could disagree would produce a dish nobody can eat. The
        /// generator says so in its report rather than letting the choice vanish.
        /// </para>
        /// </remarks>
        private static void StopItBeingEdible(GameObject root)
        {
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            if (objectAuthoring != null && objectAuthoring.objectType == ObjectType.Eatable)
            {
                objectAuthoring.objectType = ObjectType.NonUsable;
            }
        }

        private static void ApplyIngredient(
            GameObject root,
            DimensionCookingTemplate cooking,
            Sprite ownPicture,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> reportUnknownCondition,
            System.Action<string> report)
        {
            CookingIngredientAuthoring ingredient =
                EnsureComponent<CookingIngredientAuthoring>(root);
            ingredient.ingredientType = (IngredientType)(int)cooking.IngredientKind;

            Color[] shades = null;
            if (cooking.ColoursFromItsOwnPicture && ownPicture != null)
            {
                DimensionFoodPalette.TryExtractRamp(ownPicture, out shades);
            }

            if (shades != null)
            {
                ingredient.brightestColor = shades[0];
                ingredient.brightColor = shades[1];
                ingredient.darkColor = shades[2];
                ingredient.darkestColor = shades[3];
            }
            else
            {
                if (cooking.ColoursFromItsOwnPicture && report != null)
                {
                    report(
                        "should take its cooking colours from its own picture, but no picture " +
                        "could be read, so the four colours typed on the item were used instead. " +
                        "Drag the icon into the item's Icon field — an icon found by id is not " +
                        "read for colours — or untick taking the colours from the picture.");
                }

                ingredient.brightestColor = cooking.Brightest;
                ingredient.brightColor = cooking.Bright;
                ingredient.darkColor = cooking.Dark;
                ingredient.darkestColor = cooking.Darkest;
            }

            ingredient.turnsIntoFood = ResolveFoodTarget(
                cooking.MakesDish,
                "makes",
                resolveObject,
                isOneOfOurOwn,
                report);

            SetCategoryTag(root, ObjectCategoryTag.CookingIngredient, true);
            Toggle<FishAuthoring>(root, cooking.CanBeFished);
            SetCategoryTag(root, ObjectCategoryTag.Fish, cooking.CanBeFished);

            // A rare flower in the pot is one of the two things that can push a dish to epic, and
            // the game asks that question of the flower COMPONENT plus the object's rarity. The
            // rarity is the creator's own field on the item; this is the other half.
            if (cooking.CountsAsAFlower)
            {
                EnsureComponent<FlowerAuthoring>(root);
            }

            ApplyRawAndCookedConditions(root, cooking, reportUnknownCondition);

            // THE LEVEL TRAP, in the one place it cannot be switched off. Every other
            // condition-giving component has a "leave my numbers alone" flag;
            // GivesConditionsWhenConsumedAuthoring does not — it recomputes its whole list from
            // the world tier the moment Unity validates the prefab. So an ingredient with a tier
            // ships whatever the curve says, not what its author typed, and there is no field
            // anywhere that changes that.
            if (root.GetComponent<AreaLevelAuthoring>() != null &&
                (cooking.GivesRaw.Length > 0 || cooking.GivesCooked.Length > 0) &&
                report != null)
            {
                report(
                    "has a world tier, and the game recomputes what a food gives from that tier " +
                    "rather than from the numbers typed on it. There is no way to turn that off " +
                    "for food. Clear the world tier if the raw and cooked amounts here are meant " +
                    "to be exactly what a player gets.");
            }
        }

        private static void ApplyDish(
            GameObject root,
            DimensionCookingTemplate cooking,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, DimensionCookingTemplate> findIngredient,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> report)
        {
            // BOTH LISTS ARE DRAWN ON EVERY ITEM AND BOTH ARE DROPPED FOR A DISH. Not an oversight
            // here: the game works out what a meal does to you from the INGREDIENTS it was cooked
            // from — it reads their consumed-conditions and picks each one's cooked half — so a
            // finished dish has nowhere for its own effects to come from. Filling them in on a dish
            // used to grant nothing and say nothing, so a custom stew meant to heal healed nobody.
            if (cooking.RawOrCookedTypedWhereNothingReadsThem && report != null)
            {
                report(
                    "is a dish with 'gives raw' or 'gives cooked' filled in, and neither reaches " +
                    "anybody. What a meal does to whoever eats it comes from the ingredients it " +
                    "was cooked from — put the effects on those instead.");
            }

            CookedFoodAuthoring dish = EnsureComponent<CookedFoodAuthoring>(root);
            dish.rareVersion = ResolveFoodTarget(
                cooking.RareVersion, "upgrades to", resolveObject, isOneOfOurOwn, report);
            dish.epicVersion = ResolveFoodTarget(
                cooking.EpicVersion, "upgrades to", resolveObject, isOneOfOurOwn, report);

            DimensionCookingTemplate first = findIngredient == null
                ? null
                : findIngredient(cooking.MadeFrom);
            DimensionCookingTemplate second = findIngredient == null
                ? null
                : findIngredient(cooking.MadeFromAlso);

            if (first == null && !string.IsNullOrEmpty(cooking.MadeFrom) && report != null)
            {
                report(
                    "is made from '" + cooking.MadeFrom + "', which this mod does not define as an " +
                    "ingredient, so its colours are taken from the dish itself instead.");
            }

            if (second == null && !string.IsNullOrEmpty(cooking.MadeFromAlso) && report != null)
            {
                report(
                    "is made from '" + cooking.MadeFromAlso + "', which this mod does not define " +
                    "as an ingredient, so its colours are taken from the dish itself instead.");
            }

            DimensionCookingTemplate paletteA = first ?? cooking;
            DimensionCookingTemplate paletteB = second ?? cooking;

            dish.ingredient1BrightestColor = paletteA.Brightest;
            dish.ingredient1BrightColor = paletteA.Bright;
            dish.ingredient1DarkColor = paletteA.Dark;
            dish.ingredient1DarkestColor = paletteA.Darkest;
            dish.ingredient2BrightestColor = paletteB.Brightest;
            dish.ingredient2BrightColor = paletteB.Bright;
            dish.ingredient2DarkColor = paletteB.Dark;
            dish.ingredient2DarkestColor = paletteB.Darkest;

            // Which of the two tier tags a dish carries follows its rarity, because that is the
            // question the tags exist to answer: Uncommon and below on one, Rare and above on the
            // other. Read off the object that has already been configured rather than asked for
            // twice, so the tag can never disagree with the colour of the item's name.
            ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
            bool better = objectAuthoring != null && objectAuthoring.rarity >= Rarity.Rare;
            SetCategoryTag(root, ObjectCategoryTag.RareOrHigherCookedFood, better);
            SetCategoryTag(root, ObjectCategoryTag.UncommonOrLowerCookedFood, !better);
        }

        /// <summary>
        /// A dish or ingredient the item points at, as an ObjectID when that is knowable offline.
        /// </summary>
        /// <remarks>
        /// Three outcomes, and only one of them is a mistake. A vanilla name resolves to its number.
        /// One of this mod's own names cannot resolve here at all — the mod's numbers are handed out
        /// while the game loads — so it comes back as None and the runtime fills it in; saying
        /// anything about that would train creators to ignore the warning that matters. A name that
        /// is neither is a typo, and that one is worth saying out loud.
        /// </remarks>
        private static ObjectID ResolveFoodTarget(
            string name,
            string verb,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, bool> isOneOfOurOwn,
            System.Action<string> report)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ObjectID.None;
            }

            ObjectID resolved = resolveObject == null ? ObjectID.None : resolveObject(name);
            if (resolved != ObjectID.None)
            {
                return resolved;
            }

            if (isOneOfOurOwn != null && isOneOfOurOwn(name))
            {
                return ObjectID.None;
            }

            if (report != null)
            {
                report(
                    verb + " '" + name + "', which is neither one of this mod's own foods nor one " +
                    "the game has. Check the spelling, or add a dish with that id.");
            }

            return ObjectID.None;
        }

        /// <summary>
        /// Writes the raw and cooked halves of what an ingredient gives when it is eaten.
        /// </summary>
        /// <remarks>
        /// Appended to whatever the item's own eaten effects already put there, so an ingredient
        /// that is also an ordinary consumable keeps both. See the ordering note on
        /// <see cref="ApplyCooking"/> — appending is only safe because the effects pass has already
        /// rewritten the list from scratch this run.
        /// </remarks>
        private static void ApplyRawAndCookedConditions(
            GameObject root,
            DimensionCookingTemplate cooking,
            System.Action<string> reportUnknownCondition)
        {
            DimensionItemEffect[] raw = cooking.GivesRaw;
            DimensionItemEffect[] cooked = cooking.GivesCooked;
            int count = System.Math.Max(raw.Length, cooked.Length);
            if (count == 0)
            {
                return;
            }

            GivesConditionsWhenConsumedAuthoring eaten =
                EnsureComponent<GivesConditionsWhenConsumedAuthoring>(root);
            if (eaten.Values == null)
            {
                eaten.Values = new System.Collections.Generic.List<ConditionDataContainer>();
            }

            for (int i = 0; i < count; i++)
            {
                // A pair with only one half filled in means "the same either way", which is how the
                // game's own plain ingredients are authored.
                DimensionItemEffect rawHalf = i < raw.Length ? raw[i] : cooked[i];
                DimensionItemEffect cookedHalf = i < cooked.Length ? cooked[i] : raw[i];

                ConditionData rawData;
                ConditionData cookedData;
                if (!TryBuildConditionData(rawHalf, reportUnknownCondition, out rawData) ||
                    !TryBuildConditionData(cookedHalf, reportUnknownCondition, out cookedData))
                {
                    continue;
                }

                eaten.Values.Add(new ConditionDataContainer
                {
                    conditionData = rawData,
                    conditionDataWhenCooked = cookedData
                });
            }
        }

        private static bool TryBuildConditionData(
            DimensionItemEffect effect,
            System.Action<string> reportUnknownCondition,
            out ConditionData data)
        {
            data = default(ConditionData);
            ConditionID id;
            if (effect == null || !TryResolveCondition(effect.EffectId, out id))
            {
                if (effect != null && reportUnknownCondition != null)
                {
                    reportUnknownCondition(effect.EffectId);
                }

                return false;
            }

            data = new ConditionData
            {
                conditionID = id,
                duration = effect.Seconds,
                value = effect.Value,
                valueMultiplier = effect.ValueMultiplier
            };
            return true;
        }
    }
}
