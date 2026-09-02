using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>Whether a clip repeats.</summary>
    public enum DimensionClipRepeat
    {
        /// <summary>What the game's own creatures do with this one.</summary>
        SameAsTheGame = 0,

        /// <summary>Keeps going until something else is played.</summary>
        KeepsGoing = 1,

        /// <summary>Plays through once and hands back to standing.</summary>
        PlaysOnce = 2
    }
}
