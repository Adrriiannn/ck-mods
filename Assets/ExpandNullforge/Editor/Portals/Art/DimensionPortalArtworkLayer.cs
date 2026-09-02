using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ExpandNullforge.Authoring;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal enum DimensionPortalArtworkLayer
    {
        Frame,
        ChargeSweep,
        Milestones,
        Center,

        // The instant item portal's center: same profile reference property as Center, but a
        // three-animation framework contract (idle, opening, closing — five frames each). Kept
        // out of the Descriptors array so profile initialization and legacy palette migration
        // keep operating on the placed-portal contract only.
        CenterInstant
    }
}
