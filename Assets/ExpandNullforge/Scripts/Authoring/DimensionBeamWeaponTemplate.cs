using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A weapon that fires a continuous beam rather than a projectile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BeamWeaponAuthoring</c> is its own weapon class in Core Keeper, distinct from melee,
    /// ranged and cast. A beam is held rather than fired: it reaches out, it can grow the longer you
    /// hold it, and it can drain mana while it runs. None of that is expressible as a projectile.
    /// </para>
    /// <para>
    /// THE STICKY BEAM IS THE INTERESTING ONE. A sticky beam latches onto what it first touches and
    /// keeps hitting that, so the player aims once and then moves; an ordinary beam sweeps wherever
    /// they point. Together with damage-only-at-the-end, that is the difference between a cutting
    /// tool and a tether.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBeamWeaponTemplate
    {
        [Tooltip("It fires a held beam rather than a projectile.")]
        [SerializeField] private bool firesABeam;

        [Header("Reach")]
        [Tooltip("How far the beam reaches, in tiles.")]
        [Min(0f)]
        [SerializeField] private float reaches = 5f;

        [Tooltip("The beam grows the longer it is held.")]
        [SerializeField] private bool growsWhileHeld;

        [Tooltip("How long it takes to grow to full reach.")]
        [Min(0f)]
        [SerializeField] private float growsOverSeconds;

        [Tooltip("How short it starts before growing.")]
        [Min(0f)]
        [SerializeField] private float startsAtReach;

        [Header("What it touches")]
        [Tooltip("It latches onto the first thing it touches and keeps hitting that.")]
        [SerializeField] private bool latchesOn;

        [Tooltip("Only the far end of the beam hurts — the middle is harmless.")]
        [SerializeField] private bool onlyTheEndHurts;

        [Tooltip("The beam is drawn from the player's centre rather than from the weapon.")]
        [SerializeField] private bool drawnFromTheCentre;

        [Tooltip("Which physics layers it collides with. 0 leaves the game's own choice.")]
        [Min(0)]
        [SerializeField] private int collidesWithLayers;

        [Tooltip("Which physics layers it will attack. 0 leaves the game's own choice.")]
        [Min(0)]
        [SerializeField] private int attacksLayers;

        [Header("Cost and power")]
        [Tooltip("A condition drained while it runs — its mana cost. Blank for free.")]
        [SerializeField] private string manaCostCondition = string.Empty;

        [Tooltip("A condition that makes it hit harder. Blank for none.")]
        [SerializeField] private string damageBoostCondition = string.Empty;

        [Header("Extra beams")]
        [Tooltip("A second projectile fired alongside the beam. Blank for none.")]
        [SerializeField] private string secondProjectileId = string.Empty;

        [Tooltip("How many extra beams go out beside the first.")]
        [Min(0)]
        [SerializeField] private int extraBeams;

        [Tooltip("How far apart those extra beams spread, in degrees.")]
        [Min(0)]
        [SerializeField] private int spreadDegrees;

        [Tooltip("Where the beam starts relative to the player.")]
        [SerializeField] private Vector3 startsAtOffset = Vector3.zero;

        [Header("Animation")]
        [Tooltip("An animation name to play instead of the usual one.")]
        [SerializeField] private string overrideAnimation = string.Empty;

        [Tooltip("A second animation name, for the held part of the beam.")]
        [SerializeField] private string heldAnimation = string.Empty;

        [Tooltip("It uses the looping ranged animation while held.")]
        [SerializeField] private bool usesTheLoopingAnimation;

        public bool FiresABeam { get { return firesABeam; } }

        public float Reaches { get { return reaches < 0f ? 0f : reaches; } }

        public bool GrowsWhileHeld { get { return growsWhileHeld; } }

        public float GrowsOverSeconds
        {
            get { return growsOverSeconds < 0f ? 0f : growsOverSeconds; }
        }

        public float StartsAtReach { get { return startsAtReach < 0f ? 0f : startsAtReach; } }

        public bool LatchesOn { get { return latchesOn; } }

        public bool OnlyTheEndHurts { get { return onlyTheEndHurts; } }

        public bool DrawnFromTheCentre { get { return drawnFromTheCentre; } }

        public int CollidesWithLayers
        {
            get { return collidesWithLayers < 0 ? 0 : collidesWithLayers; }
        }

        public int AttacksLayers { get { return attacksLayers < 0 ? 0 : attacksLayers; } }

        public string ManaCostCondition { get { return manaCostCondition ?? string.Empty; } }

        public string DamageBoostCondition
        {
            get { return damageBoostCondition ?? string.Empty; }
        }

        public string SecondProjectileId { get { return secondProjectileId ?? string.Empty; } }

        public int ExtraBeams { get { return extraBeams < 0 ? 0 : extraBeams; } }

        public int SpreadDegrees { get { return spreadDegrees < 0 ? 0 : spreadDegrees; } }

        public Vector3 StartsAtOffset { get { return startsAtOffset; } }

        public string OverrideAnimation { get { return overrideAnimation ?? string.Empty; } }

        public string HeldAnimation { get { return heldAnimation ?? string.Empty; } }

        public bool UsesTheLoopingAnimation { get { return usesTheLoopingAnimation; } }

        /// <summary>Whether it grows with no time to grow over.</summary>
        public bool GrowsInstantly
        {
            get { return growsWhileHeld && growsOverSeconds <= 0f; }
        }

        /// <summary>Whether it starts longer than it can ever grow to.</summary>
        public bool StartsLongerThanItEnds
        {
            get { return growsWhileHeld && startsAtReach > reaches; }
        }

        /// <summary>Whether extra beams were asked for with no spread to put them in.</summary>
        /// <remarks>
        /// Every extra beam would leave along the same line as the first and be invisible behind it.
        /// </remarks>
        public bool ExtraBeamsWouldOverlap
        {
            get { return extraBeams > 0 && spreadDegrees <= 0; }
        }
    }
}
