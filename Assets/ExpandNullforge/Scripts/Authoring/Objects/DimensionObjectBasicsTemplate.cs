using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Which tier of the world a thing belongs to.
    /// </summary>
    /// <remarks>
    /// Core Keeper's own <c>AreaLevel</c>, with its own numbering — the values are not sequential
    /// (Clay is 10, Stone 30, Crystal 70) and are reproduced here so the mapping is exact.
    /// </remarks>
    public enum DimensionAreaTier
    {
        Slime = 0,
        StartArea = 1,
        Clay = 10,
        LarvaHive = 20,
        Stone = 30,
        Nature = 40,
        Mold = 50,
        Sea = 60,
        City = 61,
        Desert = 62,
        Lava = 63,
        Crystal = 70,
        Passage = 80,

        // Added when the armour-set work needed them: four of the game's own 62 sets are priced at
        // area level 85, which had no name here and so could not be chosen. Both values are the
        // game's own (`ck-db\Pug.Base\AreaLevel.cs`), and every member of this enum carries an
        // explicit number, so adding two at the end moves nothing already serialized.
        Excavation = 85,
        Obsidian = 100
    }

    /// <summary>Which ways a thing's art can face.</summary>
    /// <remarks>
    /// Core Keeper's <c>OrientationSupport</c>, a flags enum: Horizontal 1, Vertical 2,
    /// EightDirections 4. Measured across the game, 200 objects set it and 1,234 leave it at None —
    /// so the default is right for scenery and wrong for anything that turns.
    /// </remarks>
    public enum DimensionFacing
    {
        /// <summary>Its art never turns. Correct for most scenery.</summary>
        DoesNotTurn = 0,

        /// <summary>Left and right only.</summary>
        LeftAndRight = 1,

        /// <summary>Up and down only.</summary>
        UpAndDown = 2,

        /// <summary>All four ways. What the 200 vanilla objects that turn actually use.</summary>
        AllFourWays = 3,

        /// <summary>Eight directions.</summary>
        EightWays = 4
    }

    /// <summary>
    /// The two things every generated object needs and neither of which was ever asked about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TIER IS NOT COSMETIC. <c>AreaLevelAuthoring</c> is what the health and damage curves are
    /// computed from — the same curve that silently recomputes an authored number when the component
    /// is present. The framework has always attached that component and never set its tier, so every
    /// generated object has been resolving as <b>Slime</b>, the lowest tier in the game. A boss meant
    /// for the Crystal biome has been computing its numbers as though it lived in the starting area.
    /// </para>
    /// <para>
    /// THE FACING IS WHY A CREATURE MIGHT NOT TURN. <c>AnimationAuthoring.orientationSupport</c>
    /// declares whether an object's animation carries orientation data at all. Left at None — which
    /// is what every generated object has had — art drawn for four directions will not turn to face
    /// anything. Two hundred vanilla prefabs set it; the great majority of those use all four ways.
    /// </para>
    /// <para>
    /// <c>largeAnimationHistorySupport</c> is set by <b>zero</b> vanilla prefabs and is not offered,
    /// on the same principle as the projectile's shatter-on-collision.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionObjectBasicsTemplate
    {
        [Tooltip("Which tier of the world it belongs to. This drives the health and damage curves.")]
        [SerializeField] private DimensionAreaTier areaTier = DimensionAreaTier.Slime;

        [Tooltip("Which ways its art can face. Anything that turns needs more than Does not turn.")]
        [SerializeField] private DimensionFacing facing = DimensionFacing.DoesNotTurn;

        [Tooltip("Its stats scale with that tier. Off means the numbers you typed are kept exactly.")]
        [SerializeField] private bool scalesWithItsTier;

        public DimensionAreaTier AreaTier
        {
            get { return areaTier; }
        }

        public DimensionFacing Facing
        {
            get { return facing; }
        }

        /// <summary>Whether its stats are computed from the tier rather than taken as written.</summary>
        /// <remarks>
        /// Opt-in and off by default, because the component that does the scaling is the level trap:
        /// with it present, an authored attack damage is recomputed from the curve and the attack
        /// components have no way to say no. Creatures and plants already decide this for themselves
        /// through their own stat source; this is how everything else opts in.
        /// </remarks>
        public bool ScalesWithItsTier
        {
            get { return scalesWithItsTier; }
        }

        /// <summary>Whether a tier was chosen that nothing will read.</summary>
        public bool ChoseATierThatWillBeIgnored
        {
            get { return !scalesWithItsTier && areaTier != DimensionAreaTier.Slime; }
        }

        public bool TurnsToFaceThings
        {
            get { return facing != DimensionFacing.DoesNotTurn; }
        }
    }
}
