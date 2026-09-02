using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// How good a piece of gear is. Core Keeper's own <c>Rarity</c>, with its own numbering.
    /// </summary>
    /// <remarks>
    /// Poor is -1 in the game's enum, so the values are written out rather than left to run from
    /// zero. Measured over the game's 62 armour sets: 43 are Rare, 10 Epic, 6 Uncommon, 3 Common,
    /// and none is Poor or Legendary.
    /// </remarks>
    public enum DimensionRarity
    {
        Poor = -1,
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// Armour sets a mod adds, and what wearing enough of one gives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A SET IS A LIST OF PIECES AND A LIST OF LINES. Wear enough of the pieces and the lines
    /// apply. That is the whole of it, and it is what the game's own sets are: measured over the
    /// 62 sets in <c>SetBonusesTable</c>, 16 are two pieces, 40 are three, and six are four or
    /// five; 51 give one line and 11 give two.
    /// </para>
    /// <para>
    /// THE NUMBER ON A LINE IS NOT TYPED, IT IS WORKED OUT. Core Keeper computes every set
    /// bonus's number from the tier and the rarity of the set, not from anything written in the
    /// table — <c>SetBonusesTable.UpdateSetBonusDatas</c> throws away whatever value is stored and
    /// recomputes it through <c>LevelScaling.GetLevelFromAreaLevelAndRarity</c> the moment a world
    /// is created. So a set is priced by saying where in the world it belongs and how good it is,
    /// and the strength multiplier is the only dial over the top of that.
    /// </para>
    /// <para>
    /// A PIECE BELONGS TO ONE SET. The game builds an item-to-set lookup with a plain add, so an
    /// item named by two sets would take the whole condition system down as a world loads. A piece
    /// already claimed by another set — the game's or this mod's — is dropped from the later set
    /// and reported.
    /// </para>
    /// <para>
    /// SIX LINES AND SIX PIECES ARE ALL THE TOOLTIP DRAWS. Measured on the game's own hover panel:
    /// <c>setBonusesStats</c> and <c>setBonusesPieces</c> each hold six text slots, and going over
    /// either logs an error and stops drawing the rest. Nothing is broken by a bigger set, but
    /// nobody can read past the sixth.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSetBonusTemplate
    {
        [Header("Does this mod add armour sets?")]
        [Tooltip("Turn on to add sets of your own. Off leaves the game's own sets alone, which this never touches either way.")]
        [SerializeField] private bool addsArmourSets;

        [Tooltip("One row per set: its pieces, and what wearing enough of them gives.")]
        [SerializeField] private DimensionSetBonus[] sets = new DimensionSetBonus[0];

        public bool AddsArmourSets { get { return addsArmourSets; } }

        public DimensionSetBonus[] Sets { get { return sets ?? new DimensionSetBonus[0]; } }

        /// <summary>Switched on with no sets, so nothing would be added.</summary>
        public bool NothingWasAdded
        {
            get { return addsArmourSets && Sets.Length == 0; }
        }
    }

    /// <summary>One armour set: the pieces it is made of, and the lines wearing them gives.</summary>
    [Serializable]
    public sealed class DimensionSetBonus
    {
        [Tooltip("A short id for this set. It is never shown to a player; it is what the framework calls the set.")]
        [SerializeField] private string setId = "mod:set";

        [Tooltip("The gear that belongs to this set, named as the game names it, or as one of this mod's own items is named. Two to five is what the game's own sets use; the tooltip draws six.")]
        [SerializeField] private string[] pieces = new string[0];

        [Tooltip("Which tier of the world this set belongs to. This and the rarity are what the number on each line is worked out from.")]
        [SerializeField] private DimensionAreaTier tier = DimensionAreaTier.Clay;

        [Tooltip("How good the set is. The game's own sets are mostly Rare.")]
        [SerializeField] private DimensionRarity rarity = DimensionRarity.Rare;

        [Tooltip("What wearing enough pieces gives. The game's own sets give one line, sometimes two.")]
        [SerializeField] private DimensionSetBonusLine[] lines = new DimensionSetBonusLine[0];

        public string SetId { get { return setId ?? string.Empty; } }

        public string[] Pieces { get { return pieces ?? new string[0]; } }

        public DimensionAreaTier Tier { get { return tier; } }

        public DimensionRarity Rarity { get { return rarity; } }

        public DimensionSetBonusLine[] Lines
        {
            get { return lines ?? new DimensionSetBonusLine[0]; }
        }
    }

    /// <summary>One line of a set bonus: an effect, and how many pieces it takes to get it.</summary>
    [Serializable]
    public struct DimensionSetBonusLine
    {
        [Tooltip("The effect this line gives, named as the game names it, or as one of this mod's own effects is named.")]
        [SerializeField] private string effectName;

        [Tooltip("How many pieces of the set have to be worn before this line applies.")]
        [Min(1)]
        [SerializeField] private int requiredPieces;

        [Tooltip("How much stronger or weaker than the tier and rarity would make it. 1 is exactly what the tier says.")]
        [Min(0f)]
        [SerializeField] private float strength;

        [Tooltip("How long it lasts once it applies, in seconds. Leave at zero for an effect that lasts as long as the gear is worn.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        public string EffectName { get { return effectName ?? string.Empty; } }

        public int RequiredPieces { get { return requiredPieces < 1 ? 1 : requiredPieces; } }

        /// <summary>
        /// The multiplier, with zero read as one.
        /// </summary>
        /// <remarks>
        /// Zero is what an untouched row deserializes to, and the game reads a zero multiplier as
        /// one itself (<c>SetBonusesTable.UpdateSetBonusDatas</c>), so this matches rather than
        /// handing the game a set bonus of nothing.
        /// </remarks>
        public float Strength { get { return strength <= 0f ? 1f : strength; } }

        public float Seconds { get { return seconds < 0f ? 0f : seconds; } }
    }
}
