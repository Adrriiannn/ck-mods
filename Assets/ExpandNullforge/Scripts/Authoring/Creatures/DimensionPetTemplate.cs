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

        [Tooltip("Colours this pet can be recoloured into, as gradient maps. The first is the one it hatches with. Leave empty for a pet with one look.")]
        [SerializeField] private GradientMapDataBlock[] colours = new GradientMapDataBlock[0];

        public bool IsAPet { get { return isAPet; } }

        public DimensionPetKind FightsBy { get { return fightsBy; } }

        public bool Flies { get { return flies; } }

        public string[] Talents { get { return talents ?? new string[0]; } }

        public float HappyAnimationSeconds
        {
            get { return happyAnimationSeconds < 0f ? 0f : happyAnimationSeconds; }
        }

        /// <summary>
        /// The colours this pet can be recoloured into.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A PET'S SKINS ARE NOT ON THE PET. Core Keeper keeps them in <c>PetInfosTable</c>, a list
        /// of gradient maps per pet object, and reads them in three places: the converter, which
        /// turns the count into <c>PetCD.maxSkins</c>; the pet itself, which applies the chosen one
        /// to its sprite (<c>ck-db\Pug.Other\PetBase.cs:159-162</c>); and the inventory, which tints
        /// its icon to match. So a pet a mod adds has always converted with a skin count of zero,
        /// however many colours the mod shipped.
        /// </para>
        /// <para>
        /// EMPTY IS A PET WITH ONE LOOK, which is what the great majority of the game's own pets
        /// are. This is only worth filling for a pet meant to come in colours.
        /// </para>
        /// </remarks>
        public GradientMapDataBlock[] Colours
        {
            get { return colours ?? new GradientMapDataBlock[0]; }
        }

        /// <summary>Whether talents were listed on something that is not a pet.</summary>
        public bool TalentsWillBeIgnored
        {
            get { return !isAPet && Talents.Length > 0; }
        }

        /// <summary>Whether colours were listed on something that is not a pet.</summary>
        public bool ColoursWillBeIgnored
        {
            get { return !isAPet && Colours.Length > 0; }
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
