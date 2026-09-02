using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>The size of the dark blob under a creature's feet.</summary>
    /// <remarks>
    /// These are the game's own shadow sprites, named the way its shared Shadow asset names them.
    /// Picking one of these means a modded creature's shadow is the same art everything else in
    /// the world casts, rather than a lookalike that reads slightly wrong beside it.
    /// </remarks>
    public enum DimensionCreatureShadowSize
    {
        /// <summary>No shadow at all.</summary>
        None = 0,

        /// <summary>About the size of a critter.</summary>
        Tiny = 1,

        /// <summary>About the size of a slime.</summary>
        Small = 2,

        /// <summary>About the size of a caveling. The usual answer.</summary>
        Medium = 3,

        /// <summary>About the size of a brute.</summary>
        Large = 4,

        /// <summary>Boss sized.</summary>
        Huge = 5,

        /// <summary>Soft edged, for something that hovers.</summary>
        Soft = 6,

        /// <summary>Soft edged and wide, for something big that hovers.</summary>
        BigAndSoft = 7
    }
}
