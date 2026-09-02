using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>What part an item plays in cooking.</summary>
    public enum DimensionFoodRole
    {
        /// <summary>Nothing to do with cooking. Most items.</summary>
        NotFood = 0,

        /// <summary>Something you put in the pot. (79 vanilla prefabs)</summary>
        Ingredient = 1,

        /// <summary>Something that comes out of the pot. (45)</summary>
        CookedDish = 2
    }

    /// <summary>What sort of ingredient something is.</summary>
    /// <remarks>
    /// Core Keeper's own four, and there can be no fifth: the cook book's type filter is a hardcoded
    /// list of these three buttons, so a category the game does not have would be a category no
    /// player could filter by. The kind decides which of those buttons finds this ingredient in the
    /// cook book. It does NOT decide what a pair makes — that is the leading ingredient's dish, and
    /// nothing else.
    /// </remarks>
    public enum DimensionIngredientKind
    {
        None = 0,
        Plant = 1,
        Fish = 2,
        Meat = 3
    }

    /// <summary>
    /// An item's part in cooking, and the colours it lends to what it becomes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE COLOURS ARE THE WHOLE MECHANISM. Core Keeper does not draw a separate sprite for every
    /// possible dish — it draws one dish sprite and recolours it from the ingredients that went in.
    /// An ingredient carries a four-tone palette (<c>brightest</c>, <c>bright</c>, <c>dark</c>,
    /// <c>darkest</c>); a cooked dish carries <b>two</b> of those palettes, one per ingredient, and
    /// is tinted with both.
    /// </para>
    /// <para>
    /// WHICH MEANS THE DISH'S COLOURS ARE A COPY. In vanilla they are typed in twice, once on the
    /// ingredient and once on every dish it can appear in. A framework should not make somebody do
    /// that: name the two ingredients and the generator fills both palettes from them. The fields
    /// are still here to override, because a dish that should not look like the sum of its parts is
    /// a legitimate thing to want.
    /// </para>
    /// <para>
    /// A fish carries one more thing — <c>FishAuthoring</c>, a bare marker on 44 prefabs with no
    /// fields at all, which is what makes it catchable.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCookingTemplate
    {
        [Tooltip("What part this plays in cooking.")]
        [SerializeField] private DimensionFoodRole role = DimensionFoodRole.NotFood;

        [Header("As an ingredient")]
        [Tooltip("What sort of ingredient it is. The pot reads this to decide what a pair makes.")]
        [SerializeField] private DimensionIngredientKind ingredientKind = DimensionIngredientKind.Plant;

        [Tooltip("It can be caught with a fishing rod.")]
        [SerializeField] private bool canBeFished;

        [Tooltip("Which dish it makes when it leads the pair. A dish of your own, or one of the " +
            "game's fifteen — CookedSoup, CookedSalad, CookedSteak and the rest.")]
        [UnityEngine.Serialization.FormerlySerializedAs("cookedAloneBecomes")]
        [SerializeField] private string makesDish = string.Empty;

        [Tooltip("It counts as a flower. A rare flower in the pot is one of the two things that " +
            "can push a cooked dish up to epic, so tick this on anything blossom-like.")]
        [SerializeField] private bool countsAsAFlower;

        [Header("The colours it lends to a dish")]
        [Tooltip("Take the four shades from this item's own icon, brightest to darkest. Only an " +
            "icon dragged into the Icon field is read this way. Untick to choose them by hand below.")]
        [SerializeField] private bool coloursFromItsOwnPicture = true;

        [SerializeField] private Color brightest = new Color(1f, 0.85f, 0.5f, 1f);
        [SerializeField] private Color bright = new Color(0.9f, 0.7f, 0.35f, 1f);
        [SerializeField] private Color dark = new Color(0.6f, 0.42f, 0.2f, 1f);
        [SerializeField] private Color darkest = new Color(0.35f, 0.24f, 0.11f, 1f);

        [Header("What it gives")]
        [Tooltip("What eating it raw gives. Hunger goes here as HungerAddition.")]
        [SerializeField] private DimensionItemEffect[] givesRaw = new DimensionItemEffect[0];

        [Tooltip("What it gives once it has been through a pot. The game's own ingredients roughly " +
            "double their hunger here and add one effect that only the cooked form has.")]
        [SerializeField] private DimensionItemEffect[] givesCooked = new DimensionItemEffect[0];

        [Header("A golden version")]
        [Tooltip("Also make a rare, golden version of this ingredient, the way the game's crops " +
            "have golden ones. It makes the better version of the same dish.")]
        [SerializeField] private bool hasAGoldenVersion;

        [Tooltip("What the golden version is called. Leave empty for \"Golden \" and this item's name.")]
        [SerializeField] private string goldenName = string.Empty;

        [Tooltip("Which dish the golden version makes. Leave empty to use the rare version of the " +
            "dish above.")]
        [SerializeField] private string goldenMakesDish = string.Empty;

        [Header("As a cooked dish")]
        [Tooltip("The two ingredients it is made from. The generator takes its colours from these.")]
        [SerializeField] private string madeFrom = string.Empty;

        [SerializeField] private string madeFromAlso = string.Empty;

        [Tooltip("The better version of this dish. Empty means there is not one.")]
        [SerializeField] private string rareVersion = string.Empty;

        [Tooltip("The best version of this dish.")]
        [SerializeField] private string epicVersion = string.Empty;

        public DimensionFoodRole Role
        {
            get { return role; }
        }

        public bool IsAnIngredient
        {
            get { return role == DimensionFoodRole.Ingredient; }
        }

        public bool IsACookedDish
        {
            get { return role == DimensionFoodRole.CookedDish; }
        }

        /// <summary>What sort of ingredient it is, or none when it is not one.</summary>
        public DimensionIngredientKind IngredientKind
        {
            get { return IsAnIngredient ? ingredientKind : DimensionIngredientKind.None; }
        }

        /// <summary>Whether it can be caught with a rod.</summary>
        public bool CanBeFished
        {
            get { return IsAnIngredient && canBeFished; }
        }

        /// <summary>Which dish this makes when it leads the pair.</summary>
        public string MakesDish
        {
            get { return IsAnIngredient ? (makesDish ?? string.Empty) : string.Empty; }
        }

        /// <summary>Whether the game should count it as a flower for the epic upgrade.</summary>
        public bool CountsAsAFlower
        {
            get { return IsAnIngredient && countsAsAFlower; }
        }

        /// <summary>Whether the four shades should be read off the item's own picture.</summary>
        public bool ColoursFromItsOwnPicture
        {
            get { return coloursFromItsOwnPicture; }
        }

        /// <summary>What eating it raw gives, with the blank entries dropped.</summary>
        public DimensionItemEffect[] GivesRaw
        {
            get { return IsAnIngredient ? Compact(givesRaw) : new DimensionItemEffect[0]; }
        }

        /// <summary>What eating it cooked gives, with the blank entries dropped.</summary>
        public DimensionItemEffect[] GivesCooked
        {
            get { return IsAnIngredient ? Compact(givesCooked) : new DimensionItemEffect[0]; }
        }

        /// <summary>
        /// Whether somebody has typed into the raw or cooked lists on something that is not an
        /// ingredient, where the game will never read them.
        /// </summary>
        /// <remarks>
        /// Both lists are drawn on every item, and both are dropped on the floor for anything that
        /// is not an ingredient. That is not an oversight in this framework: the game reads what a
        /// meal does to you off the INGREDIENTS it was cooked from, not off the finished dish — so
        /// a dish with "gives cooked" filled in has nowhere for those effects to come from. It went
        /// unsaid, so a custom dish that was supposed to heal you healed nobody.
        /// </remarks>
        public bool RawOrCookedTypedWhereNothingReadsThem
        {
            get
            {
                return !IsAnIngredient &&
                       (Compact(givesRaw).Length > 0 || Compact(givesCooked).Length > 0);
            }
        }

        /// <summary>Whether a second, golden version of this ingredient should be made.</summary>
        public bool HasAGoldenVersion
        {
            get { return IsAnIngredient && hasAGoldenVersion; }
        }

        /// <summary>What the golden version is called.</summary>
        public string GoldenName
        {
            get { return goldenName ?? string.Empty; }
        }

        /// <summary>Which dish the golden version makes, or empty to derive it.</summary>
        public string GoldenMakesDish
        {
            get { return HasAGoldenVersion ? (goldenMakesDish ?? string.Empty) : string.Empty; }
        }

        /// <summary>The item id a golden version is generated under: the base id plus "Rare".</summary>
        /// <remarks>
        /// <para>
        /// Derived rather than authored so the step that creates the golden item and the step that
        /// generates it cannot disagree about its name, which would leave the ingredient's golden
        /// half orphaned in the project and absent from the game.
        /// </para>
        /// <para>
        /// THE SUFFIX IS THE SAME TRICK VANILLA USES. When the game builds a dish's name it strips a
        /// trailing "Rare" from each ingredient's object name before looking up its adjective and
        /// noun, which is what lets Heart Berry and its golden form read the same way in a dish
        /// name. An id spelled any other way needs its own pair of name terms or the dish comes out
        /// naming a raw id.
        /// </para>
        /// </remarks>
        public static string GoldenItemIdFor(string itemId)
        {
            return string.IsNullOrEmpty(itemId) ? string.Empty : itemId + "Rare";
        }

        private static DimensionItemEffect[] Compact(DimensionItemEffect[] effects)
        {
            if (effects == null)
            {
                return new DimensionItemEffect[0];
            }

            System.Collections.Generic.List<DimensionItemEffect> kept =
                new System.Collections.Generic.List<DimensionItemEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].NamesAnEffect)
                {
                    kept.Add(effects[i]);
                }
            }

            return kept.ToArray();
        }

        public Color Brightest
        {
            get { return brightest; }
        }

        public Color Bright
        {
            get { return bright; }
        }

        public Color Dark
        {
            get { return dark; }
        }

        public Color Darkest
        {
            get { return darkest; }
        }

        public string MadeFrom
        {
            get { return IsACookedDish ? (madeFrom ?? string.Empty) : string.Empty; }
        }

        public string MadeFromAlso
        {
            get { return IsACookedDish ? (madeFromAlso ?? string.Empty) : string.Empty; }
        }

        public string RareVersion
        {
            get { return IsACookedDish ? (rareVersion ?? string.Empty) : string.Empty; }
        }

        public string EpicVersion
        {
            get { return IsACookedDish ? (epicVersion ?? string.Empty) : string.Empty; }
        }

        /// <summary>Whether it takes part in cooking at all.</summary>
        public bool TakesPartInCooking
        {
            get { return role != DimensionFoodRole.NotFood; }
        }

        /// <summary>Whether a fish was ticked on something that is not an ingredient.</summary>
        /// <remarks>
        /// Reads back as false above rather than being written, because a fish that is not an
        /// ingredient is a fish nothing can cook.
        /// </remarks>
        public bool FishTickWasRefused
        {
            get { return canBeFished && !IsAnIngredient; }
        }

        /// <summary>Whether it is a dish that names no ingredients to take its colours from.</summary>
        /// <remarks>
        /// Not an error — the four colours can be set by hand, and a dish that should not look like
        /// the sum of its parts is a real thing to want. Worth knowing about, because the ordinary
        /// case is naming the two ingredients and letting the generator do it.
        /// </remarks>
        public bool IsADishThatNamesNoIngredients
        {
            get
            {
                return IsACookedDish
                    && string.IsNullOrEmpty(MadeFrom)
                    && string.IsNullOrEmpty(MadeFromAlso);
            }
        }

        /// <summary>Whether it is an ingredient with no kind, which the pot cannot match on.</summary>
        public bool IsAnIngredientOfNoKind
        {
            get { return IsAnIngredient && ingredientKind == DimensionIngredientKind.None; }
        }
    }
}
