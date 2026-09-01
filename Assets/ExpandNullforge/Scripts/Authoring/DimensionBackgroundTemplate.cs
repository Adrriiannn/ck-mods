using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// One of the eleven backgrounds a player picks at character creation.
    /// </summary>
    /// <remarks>
    /// The order and the names are Core Keeper's own <c>CharacterRole</c>, and both wikis call
    /// these "backgrounds" rather than roles, so that is what they are called here.
    /// </remarks>
    public enum DimensionBackground
    {
        Explorer = 0,
        Miner = 1,
        Fighter = 2,
        Chef = 3,
        Gardener = 4,
        Fisherman = 5,
        Nomad = 6,
        Ranger = 7,
        Mage = 8,
        Warlock = 9,
        Demolitionist = 10
    }

    /// <summary>
    /// What a background starts a new character with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A BACKGROUND IS A SKILL AND A KIT. Picking one at character creation puts a new character at
    /// level 3 in one skill and puts a couple of things in their bag, and that is all it ever does
    /// — the game reads both out of <c>RolePerksTable</c> the first time that character plays
    /// anywhere (<c>ck-db\Pug.Other\PlayerController.cs:3201-3215</c>). Everything a background
    /// gives is obtainable in the first hour by anyone.
    /// </para>
    /// <para>
    /// A TWELFTH BACKGROUND IS NOT POSSIBLE, AND THIS IS WHY. The character-creation carousel sizes
    /// itself from <c>Enum.GetNames(typeof(CharacterRole)).Length</c> and steps modulo that number
    /// (<c>ck-db\Pug.Other\CharacterCustomizationOption_Selection.cs:15,73</c>), so a twelfth row in
    /// the table can never be reached; the same screen then indexes an eleven-entry list of
    /// background pictures by the row's position, which would throw on the twelfth. Three separate
    /// walls, none of them a table a mod can write. So this changes the eleven that exist, and does
    /// not pretend to add a new one.
    /// </para>
    /// <para>
    /// TWO ITEMS IS THE CEILING, AND GOING OVER IT BREAKS THE SCREEN. The creation screen writes
    /// each starter item into <c>roleItemDescs[i]</c> with no bounds check, and that list holds two
    /// slots on the character-creation prefab. Every one of the game's eleven backgrounds carries
    /// exactly two items or none. A third would throw as the player skims onto that background, so
    /// a third is refused here rather than shipped.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBackgroundTemplate
    {
        [Header("Does this change what backgrounds start you with?")]
        [Tooltip("Turn on to change the game's backgrounds. Off leaves all eleven exactly as the game has them.")]
        [SerializeField] private bool changesWhatBackgroundsStartYouWith;

        [Tooltip("One row per background you are changing. A background nobody names keeps everything the game gives it.")]
        [SerializeField] private DimensionBackgroundKit[] backgrounds = new DimensionBackgroundKit[0];

        public bool ChangesWhatBackgroundsStartYouWith
        {
            get { return changesWhatBackgroundsStartYouWith; }
        }

        public DimensionBackgroundKit[] Backgrounds
        {
            get { return backgrounds ?? new DimensionBackgroundKit[0]; }
        }

        /// <summary>Switched on with no rows, so every background would keep what the game gives it.</summary>
        public bool NothingWasChanged
        {
            get { return changesWhatBackgroundsStartYouWith && Backgrounds.Length == 0; }
        }
    }

    /// <summary>What one background starts a new character with.</summary>
    [Serializable]
    public sealed class DimensionBackgroundKit
    {
        [Tooltip("Which of the game's eleven backgrounds this row is about.")]
        [SerializeField] private DimensionBackground background = DimensionBackground.Explorer;

        [Tooltip("The skill it starts you at level 3 in, named as the game names it: Mining, Running, Melee, Vitality, Crafting, Range, Gardening, Fishing, Cooking, Magic, Summoning, Explosives.")]
        [SerializeField] private string skillName = string.Empty;

        [Tooltip("What is in the bag on the first morning. Two at most: the character-creation screen has two lines to write them on, and every one of the game's own backgrounds uses two or none.")]
        [SerializeField] private DimensionBackgroundItem[] startsWith = new DimensionBackgroundItem[0];

        public DimensionBackground Background { get { return background; } }

        public string SkillName { get { return skillName ?? string.Empty; } }

        public DimensionBackgroundItem[] StartsWith
        {
            get { return startsWith ?? new DimensionBackgroundItem[0]; }
        }

        /// <summary>
        /// Whether more items were listed than the character-creation screen can write down.
        /// </summary>
        /// <remarks>
        /// The screen indexes its two text lines by the item's position with no bounds check, so a
        /// third item throws as the player skims onto this background rather than going unnoticed.
        /// </remarks>
        public bool ListsMoreItemsThanTheScreenCanShow
        {
            get { return StartsWith.Length > DimensionBackgroundKit.MostItemsTheScreenCanShow; }
        }

        /// <summary>
        /// How many starter items the character-creation screen can write down.
        /// </summary>
        /// <remarks>
        /// Measured on <c>GameObject/CharacterCustomizationUI.prefab</c>: the
        /// <c>CharacterCustomizationOption_Selection</c> that changes the background carries two
        /// <c>roleItemDescs</c> slots, and every other copy on that prefab carries none.
        /// </remarks>
        public const int MostItemsTheScreenCanShow = 2;

        /// <summary>Nomad is the one background the game deliberately gives nothing to.</summary>
        /// <remarks>
        /// <c>PlayerController</c> skips the starting skill for role 6 and the creation screen
        /// prints "None" in its place, both by a hardcoded test on the background rather than on
        /// anything in the table. So a skill named for Nomad is never granted, and saying so is
        /// better than shipping a line nothing reads.
        /// </remarks>
        public bool NamesASkillNomadWillNeverGet
        {
            get
            {
                return background == DimensionBackground.Nomad && !string.IsNullOrEmpty(SkillName);
            }
        }
    }

    /// <summary>One thing a background starts a character with.</summary>
    [Serializable]
    public struct DimensionBackgroundItem
    {
        [Tooltip("What it is, named as the game names it, or as one of this mod's own items is named.")]
        [SerializeField] private string itemId;

        [Tooltip("How many of it.")]
        [Min(1)]
        [SerializeField] private int amount;

        [Tooltip("Which variation of it, for an item that has more than one. Leave at zero for the ordinary one.")]
        [Min(0)]
        [SerializeField] private int variation;

        public string ItemId { get { return itemId ?? string.Empty; } }

        public int Amount { get { return amount < 1 ? 1 : amount; } }

        public int Variation { get { return variation < 0 ? 0 : variation; } }
    }
}
