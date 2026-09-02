using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// What happens when a player walks up to a creature and uses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THERE IS NO TICKBOX HERE, ON PURPOSE. Whether a window opens at all is already answered
    /// elsewhere and answering it twice is how two controls end up disagreeing: an animal opens the
    /// tending window because it is livestock, under its habits, and a creature opens the trading
    /// window because it is a trader, under its shop. These are the numbers those windows need and
    /// nothing else.
    /// </para>
    /// <para>
    /// The numbers are the game's own cow's, read off <c>Cow.prefab</c>: a reach of 1.3 tiles, a
    /// name tag two tiles up, and no working from behind.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureTendingTemplate
    {
        [Tooltip("How close a player has to be to use it. The game's own cow uses 1.3 tiles.")]
        [Min(0f)]
        [SerializeField] private float howCloseAPlayerMustBe = 1.3f;

        [Tooltip("It can be used from any side, rather than only the side it faces.")]
        [SerializeField] private bool worksFromAnySide;

        [Tooltip("How far above it its name floats, in tiles. The game's own cow uses 2.")]
        [Min(0f)]
        [SerializeField] private float howHighItsNameFloats = 2f;

        [Tooltip("How strongly it competes with other things nearby to be the one selected.")]
        [Min(0f)]
        [SerializeField] private float howStronglyItAsksToBeUsed = 1f;

        [Tooltip("Only this faction may use it, by the game's own name. Blank for anyone.")]
        [SerializeField] private string onlyThisFactionMayUseIt = string.Empty;

        public float HowCloseAPlayerMustBe
        {
            get { return howCloseAPlayerMustBe <= 0f ? 1.3f : howCloseAPlayerMustBe; }
        }

        public bool WorksFromAnySide { get { return worksFromAnySide; } }

        /// <summary>
        /// How far above the creature its name sits, never below it.
        /// </summary>
        /// <remarks>
        /// Clamped rather than warned about: a negative height draws the words behind the
        /// creature's own body, which reads as no words at all, and nobody types that on purpose.
        /// </remarks>
        public float HowHighItsNameFloats
        {
            get { return howHighItsNameFloats < 0f ? 0f : howHighItsNameFloats; }
        }

        public float HowStronglyItAsksToBeUsed
        {
            get { return howStronglyItAsksToBeUsed < 0f ? 0f : howStronglyItAsksToBeUsed; }
        }

        public string OnlyThisFactionMayUseIt
        {
            get { return onlyThisFactionMayUseIt ?? string.Empty; }
        }

        /// <summary>
        /// Whether the name would float low enough to be drawn inside the creature.
        /// </summary>
        /// <remarks>
        /// The chest default of 0.375 is right for a box on the floor and wrong for anything with
        /// legs, so a creature left near it is worth a sentence rather than a shrug.
        /// </remarks>
        public bool ItsNameWouldFloatInsideIt
        {
            get { return HowHighItsNameFloats < 1f; }
        }
    }

    /// <summary>
    /// A creature that belongs to whoever summoned it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THIS IS THE ONE WITH THE POWER. There is a second "is a minion" under a creature's simple
    /// traits; it writes a component Core Keeper never reads, and the framework says so when it is
    /// ticked. This one writes the component the game's own minion systems work on: it carries the
    /// damage a minion does for its tier, whether it mines as well, and — the part worth more than
    /// either — the record of who owns it, which is what hurting on touch and orbiting a summoner
    /// both require before they will run at all.
    /// </para>
    /// <para>
    /// WHO owns it is not answered here. Core Keeper writes the owner at runtime, when the thing
    /// that summons a minion summons it. A creature that should belong to a particular OBJECT at
    /// build time is a different answer, under mana and auras.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureMinionTemplate
    {
        [Tooltip("It is something summoned, and fights for whoever summoned it.")]
        [SerializeField] private bool isAMinion;

        [Tooltip("How hard it hits for its tier. 1 is the ordinary amount.")]
        [Min(0f)]
        [SerializeField] private float hitsThisHardForItsTier = 1f;

        [Tooltip("It digs through walls as well as fighting. Needs a melee attack.")]
        [SerializeField] private bool minesToo;

        [Tooltip("How hard it digs for its tier. 1 is the ordinary amount.")]
        [Min(0f)]
        [SerializeField] private float minesThisHardForItsTier = 1f;

        public bool IsAMinion { get { return isAMinion; } }

        public float HitsThisHardForItsTier
        {
            get { return hitsThisHardForItsTier < 0f ? 0f : hitsThisHardForItsTier; }
        }

        public bool MinesToo { get { return minesToo; } }

        public float MinesThisHardForItsTier
        {
            get { return minesThisHardForItsTier < 0f ? 0f : minesThisHardForItsTier; }
        }
    }

    /// <summary>
    /// What a creature does at zero health, when dying is not it.
    /// </summary>
    /// <remarks>
    /// One answer, because it is one component. Ticking it is a real change to what the creature
    /// is: the game stops short of destroying it, so it drops where it stands, never disappears and
    /// never drops loot until something else removes it. It is how the game animates a thing that
    /// is down rather than dead, and it is the only way the playing dead and getting back up clips
    /// are ever asked for.
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureLastStandTemplate
    {
        [Tooltip("At zero health it drops instead of dying, and stands back up if it is healed. " +
                 "It never disappears and drops no loot on its own.")]
        [SerializeField] private bool playsDeadInsteadOfDying;

        public bool PlaysDeadInsteadOfDying { get { return playsDeadInsteadOfDying; } }
    }
}
