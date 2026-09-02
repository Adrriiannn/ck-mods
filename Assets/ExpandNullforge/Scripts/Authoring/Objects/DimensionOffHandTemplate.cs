using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>What an off-hand item does.</summary>
    /// <remarks>
    /// Core Keeper's own seven. These are not cosmetic labels — the runtime switches on this enum,
    /// so a mod cannot invent an eighth kind of off-hand, only choose among these.
    /// </remarks>
    public enum DimensionOffHandKind
    {
        /// <summary>Not an off-hand item. Most items.</summary>
        NotAnOffHandItem = 0,

        /// <summary>It blocks. A shield.</summary>
        Shield = 1,

        /// <summary>It dashes.</summary>
        Dash = 2,

        /// <summary>It lures. Bait.</summary>
        Bait = 3,

        /// <summary>It teleports.</summary>
        Teleport = 4,

        /// <summary>It sets off your minions.</summary>
        MinionDetonation = 5,

        /// <summary>It sets off something you placed.</summary>
        RemoteDetonator = 6
    }

    /// <summary>
    /// What an item does in the off hand, and what it costs to use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>OffHandAuthoring</c> is on 24 vanilla prefabs and is one enum plus one number: what the
    /// item does, and how much of it. The number means something different per kind — how much a
    /// shield blocks, how far a dash goes — which is why it is named for the effect rather than
    /// given a unit.
    /// </para>
    /// <para>
    /// MANA IS SEPARATE AND BELONGS HERE ANYWAY. <c>ConsumesManaAuthoring</c> (20 prefabs) is its
    /// own component and applies to weapons and secondary uses as much as to off-hand items, but a
    /// creator asking "what does this cost to use" is asking one question. It is kept in this
    /// template for that reason and written regardless of whether the item is an off-hand one.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionOffHandTemplate
    {
        [Tooltip("What it does in the off hand.")]
        [SerializeField] private DimensionOffHandKind kind = DimensionOffHandKind.NotAnOffHandItem;

        [Tooltip("How much of it — how much a shield blocks, how far a dash goes.")]
        [Min(0f)]
        [SerializeField] private float strength = 1f;

        [Tooltip("Multiplier applied on top of the level curve.")]
        [Min(0f)]
        [SerializeField] private float strengthMultiplier = 1f;

        [Header("What it costs")]
        [Tooltip("Mana each use costs. 0 means it costs none.")]
        [Min(0)]
        [SerializeField] private int manaCost;

        [Tooltip("Multiplier on the mana cost.")]
        [Min(0f)]
        [SerializeField] private float manaCostMultiplier = 1f;

        public DimensionOffHandKind Kind
        {
            get { return kind; }
        }

        public bool IsAnOffHandItem
        {
            get { return kind != DimensionOffHandKind.NotAnOffHandItem; }
        }

        public float Strength
        {
            get { return strength < 0f ? 0f : strength; }
        }

        public float StrengthMultiplier
        {
            get { return strengthMultiplier < 0f ? 0f : strengthMultiplier; }
        }

        public int ManaCost
        {
            get { return manaCost < 0 ? 0 : manaCost; }
        }

        public float ManaCostMultiplier
        {
            get { return manaCostMultiplier < 0f ? 0f : manaCostMultiplier; }
        }

        public bool CostsMana
        {
            get { return ManaCost > 0; }
        }

        /// <summary>Whether it is an off-hand item that does nothing when used.</summary>
        /// <remarks>
        /// A shield that blocks nothing or a dash that covers no distance still equips, still plays
        /// its animation, and has no effect — which reads as the item being broken.
        /// </remarks>
        public bool DoesNothingWhenUsed
        {
            get { return IsAnOffHandItem && Strength <= 0f; }
        }
    }
}
