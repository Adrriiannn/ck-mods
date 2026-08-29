using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What happens when a player walks up to this and uses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// USING SOMETHING IS TWO PREFABS' WORK IN CORE KEEPER, which is why this needs a whole panel
    /// rather than a tickbox. The thing a player sees is a second prefab hanging off the first, and
    /// the "use" itself is a wired-up call on a component inside that second prefab. The framework
    /// builds both halves from the one answer below, so nobody has to know that.
    /// </para>
    /// <para>
    /// THE LIST IS SHORT BECAUSE THE GAME'S OWN LIST IS SHORT. Reading every prefab Core Keeper
    /// ships, only five things are ever wired to a use: a chest opening, a crafting bench opening,
    /// an animal being tended, an NPC being talked to, and a sign being read. Everything else with
    /// interaction wiring is menu furniture — sliders, scroll bars, options screens — which is not
    /// something in the world.
    /// </para>
    /// <para>
    /// LEAVING IS ITS OWN EVENT. Each of the five has a matching "the player walked away" call, and
    /// the framework wires that too. Without it a chest a player wandered off from stays open on
    /// their screen.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionInteractionTemplate
    {
        [Header("What using it does")]
        [Tooltip("What happens when a player uses it.")]
        [SerializeField] private DimensionUseBehaviour whatUsingItDoes = DimensionUseBehaviour.Nothing;

        [Header("Reaching it")]
        [Tooltip("How close a player has to be. The game's own default is 0.7 tiles.")]
        [Min(0f)]
        [SerializeField] private float reach = 0.7f;

        [Tooltip("It can be used from any side, rather than only the side it faces.")]
        [SerializeField] private bool worksFromAnySide;

        [Tooltip("Only whoever has claimed it may use it.")]
        [SerializeField] private bool onlyWhoeverClaimedIt;

        [Tooltip("Only one faction may use it. Blank for anyone.")]
        [SerializeField] private string onlyThisFactionMayUseIt = string.Empty;

        [Header("How it looks when a player is near")]
        [Tooltip("How strongly it competes with other things nearby to be the one selected.")]
        [Min(0f)]
        [SerializeField] private float howStronglyItAsksToBeUsed = 1f;

        [Tooltip("Its outline is one flat colour rather than shaded.")]
        [SerializeField] private bool aFlatOutlineColour;

        [Header("Words floating above it")]
        [Tooltip("How far above it the words float, in tiles. A chest that can be named, a sign " +
                 "that can be read and an animal that can be given a name all get words hanging " +
                 "over them. The game's own chest uses 0.375, its sign uses 0.75, and a camel's " +
                 "name tag sits at 2.")]
        [Min(0f)]
        [SerializeField] private float howHighTheWordsFloat = 0.375f;

        [Header("If it opens a crafting window")]
        [Tooltip("Which of the game's five crafting window looks it wears. Only used when using " +
                 "it opens a crafting bench.")]
        [SerializeField] private DimensionCraftingWindowLook craftingWindowLook =
            DimensionCraftingWindowLook.Wooden;

        [Header("If it opens like a chest")]
        [Tooltip("The sort and quick-stack buttons are shown when it is open.")]
        [SerializeField] private bool showsSortAndQuickStackButtons = true;

        [Tooltip("What it sells, when using it opens a shop. Item ids — your own or the game's. " +
                 "Prices come from each item's own value; stock never runs out, exactly like the " +
                 "Forlorn Metropolis vending machines this borrows.")]
        [SerializeField] private string[] soldItemIds = new string[0];

        [Tooltip("The shop window's grid, when it sells.")]
        [SerializeField] private Vector2Int shopGrid = new Vector2Int(3, 3);

        public DimensionUseBehaviour WhatUsingItDoes { get { return whatUsingItDoes; } }

        public bool IsUsable { get { return whatUsingItDoes != DimensionUseBehaviour.Nothing; } }

        public float Reach { get { return reach < 0f ? 0f : reach; } }

        public bool WorksFromAnySide { get { return worksFromAnySide; } }

        public bool OnlyWhoeverClaimedIt { get { return onlyWhoeverClaimedIt; } }

        public string OnlyThisFactionMayUseIt
        {
            get { return onlyThisFactionMayUseIt ?? string.Empty; }
        }

        public float HowStronglyItAsksToBeUsed
        {
            get { return howStronglyItAsksToBeUsed < 0f ? 0f : howStronglyItAsksToBeUsed; }
        }

        public bool AFlatOutlineColour { get { return aFlatOutlineColour; } }

        /// <summary>
        /// How far above the object its floating words sit, never below it.
        /// </summary>
        /// <remarks>
        /// Clamped at zero rather than warned about: a negative height is words drawn behind the
        /// object's own sprite, which reads as no words at all, and there is nothing an author
        /// gains from being told off for a value they will never have typed on purpose.
        /// </remarks>
        public float HowHighTheWordsFloat
        {
            get { return howHighTheWordsFloat < 0f ? 0f : howHighTheWordsFloat; }
        }

        /// <summary>
        /// Words hang in the air above this thing, and a player can change them by using it.
        /// </summary>
        /// <remarks>
        /// Only two of the seven uses read a <c>DescriptionBuffer</c>. A chest is a
        /// <c>WorldLabel</c> (<c>Chest : WorldLabel</c>) and shows the name a player gives it; a
        /// sign shows what is written on it. A bench, an animal, a character and a shop all show
        /// nothing, and an animal's name comes from a different component entirely.
        /// </remarks>
        public bool WordsFloatAboveIt
        {
            get
            {
                return whatUsingItDoes == DimensionUseBehaviour.OpensLikeAChest ||
                       whatUsingItDoes == DimensionUseBehaviour.ReadLikeASign;
            }
        }

        /// <summary>It carries a name tag, the way a penned animal does.</summary>
        public bool ItCarriesANameTag
        {
            get { return whatUsingItDoes == DimensionUseBehaviour.TendedLikeAnAnimal; }
        }

        /// <summary>
        /// Which of the game's crafting window looks this station's window wears.
        /// </summary>
        /// <remarks>
        /// Meaningless for anything that is not a crafting bench, and deliberately not hidden
        /// behind that condition here: the generator asks for it only on the bench branch, and a
        /// property that changed its answer depending on another field would be a second place for
        /// the two to disagree.
        /// </remarks>
        public DimensionCraftingWindowLook CraftingWindowLook { get { return craftingWindowLook; } }

        public bool ShowsSortAndQuickStackButtons { get { return showsSortAndQuickStackButtons; } }

        public string[] SoldItemIds { get { return soldItemIds ?? new string[0]; } }

        public Vector2Int ShopGrid
        {
            get
            {
                return new Vector2Int(
                    Mathf.Max(1, shopGrid.x),
                    Mathf.Max(1, shopGrid.y));
            }
        }

        /// <summary>A reach of nothing means no player can ever be close enough.</summary>
        public bool NobodyCanEverReachIt
        {
            get { return IsUsable && reach <= 0f; }
        }

        /// <summary>
        /// It opens like a chest but nothing else about it says it holds anything. The generator
        /// answers this one, not the template, so it is left to the caller.
        /// </summary>
        public bool OpensLikeAChest
        {
            get { return whatUsingItDoes == DimensionUseBehaviour.OpensLikeAChest; }
        }
    }

    /// <summary>The five things Core Keeper ever wires to a use.</summary>
    /// <remarks>
    /// Measured, not guessed: every prefab the game ships was read for wired-up use calls, and after
    /// the menu furniture is set aside these five are all that is left.
    /// </remarks>
    public enum DimensionUseBehaviour
    {
        /// <summary>Nothing happens. It is scenery.</summary>
        Nothing = 0,

        /// <summary>Its inventory opens, the way a chest does.</summary>
        OpensLikeAChest = 1,

        /// <summary>Its crafting window opens, the way a workbench does.</summary>
        OpensACraftingBench = 2,

        /// <summary>It is tended, the way a penned animal is.</summary>
        TendedLikeAnAnimal = 3,

        /// <summary>It is talked to, the way an NPC is.</summary>
        TalkedToLikeAnNpc = 4,

        /// <summary>It is read, the way a sign is.</summary>
        ReadLikeASign = 5,

        /// <summary>It sells things, the way the Forlorn Metropolis vending machines do.</summary>
        SellsLikeAShop = 6
    }

    /// <summary>The five looks Core Keeper's crafting window can wear.</summary>
    /// <remarks>
    /// <para>
    /// FIVE BECAUSE THE GAME AUTHORED FIVE, counted rather than assumed. The window's look is
    /// chosen by <c>UIManager.GetCraftingUITheme</c>, which walks the <c>craftingUIThemes</c> list
    /// on the game's own Main Manager and <b>logs an error and returns null</b> for a value the
    /// list does not hold — a null the caller dereferences immediately. That list ships with
    /// exactly five rows, one per value of <c>UIManager.CraftingUIThemeType</c>
    /// (<c>Resources/Global Objects (Main Manager).prefab:46880-46986</c>), so every value here is
    /// backed by real artwork and none of them can produce that null.
    /// </para>
    /// <para>
    /// THE NUMBERS MATTER AND MUST NOT BE REORDERED. Each one is cast straight to
    /// <c>UIManager.CraftingUIThemeType</c> at runtime rather than mapped through a switch, so the
    /// values are the game's own. They are also what Unity serializes, so renumbering would
    /// silently re-skin every station already authored.
    /// </para>
    /// <para>
    /// The names are what a player would call them rather than the game's internal ones, which is
    /// why "DangerousUsage" reads as <see cref="Dangerous"/> here.
    /// </para>
    /// </remarks>
    public enum DimensionCraftingWindowLook
    {
        /// <summary>Warm brown wood, the way a workbench looks. The game's own default.</summary>
        Wooden = 0,

        /// <summary>Cold blue stone, the way a furnace or an anvil looks.</summary>
        Stone = 1,

        /// <summary>The travelling merchant's window.</summary>
        Merchant = 2,

        /// <summary>The upgrade station's window.</summary>
        UpgradeForge = 3,

        /// <summary>The red-tinged window the game uses when a craft destroys what goes in.</summary>
        Dangerous = 4
    }
}
