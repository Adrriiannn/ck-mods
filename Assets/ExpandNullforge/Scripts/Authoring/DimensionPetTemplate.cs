using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A creature that follows the player and fights for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PetAuthoring</c> is on 42 vanilla prefabs and <c>PetWalkStateAuthoring</c> on 60 — pets
    /// have their own walk state rather than reusing the ordinary chase, because a pet paths to its
    /// owner rather than to a target. The two go together: a pet without the walk state stands
    /// where it was summoned.
    /// </para>
    /// <para>
    /// TALENTS ARE THE INTERESTING PART. A pet's talents are what it gives its owner — attack speed,
    /// crit, damage against bosses, a chance to apply burn or poison — and they are a plain list on
    /// the component. A modder adding a pet is really adding a small set of buffs with a creature
    /// attached, and this is where that set lives.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionPetTemplate
    {
        [Tooltip("It is a pet — it follows its owner and fights alongside them.")]
        [SerializeField] private bool isAPet;

        [Tooltip("How it fights: in melee, at range, or not at all because it buffs instead.")]
        [SerializeField] private DimensionPetKind fightsBy = DimensionPetKind.InMelee;

        [Tooltip("It flies, so terrain does not slow it down.")]
        [SerializeField] private bool flies;

        [Tooltip("What it gives its owner, by the game's own talent names.")]
        [SerializeField] private string[] talents = new string[0];

        [Tooltip("How long its happy animation runs when it is pleased.")]
        [Min(0f)]
        [SerializeField] private float happyAnimationSeconds = 1f;

        public bool IsAPet { get { return isAPet; } }

        public DimensionPetKind FightsBy { get { return fightsBy; } }

        public bool Flies { get { return flies; } }

        public string[] Talents { get { return talents ?? new string[0]; } }

        public float HappyAnimationSeconds
        {
            get { return happyAnimationSeconds < 0f ? 0f : happyAnimationSeconds; }
        }

        /// <summary>Whether talents were listed on something that is not a pet.</summary>
        public bool TalentsWillBeIgnored
        {
            get { return !isAPet && Talents.Length > 0; }
        }
    }

    /// <summary>
    /// How a pet fights. Core Keeper's <c>PetType</c>.
    /// </summary>
    /// <remarks>The order matches the game's enum and must stay that way.</remarks>
    public enum DimensionPetKind
    {
        /// <summary>It closes and hits things. Melee.</summary>
        InMelee = 0,

        /// <summary>It shoots from a distance. Range.</summary>
        AtRange = 1,

        /// <summary>It does not fight; it buffs its owner. Buff.</summary>
        ByBuffingItsOwner = 2
    }
}
