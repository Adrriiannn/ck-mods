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
    internal enum DimensionPortalArtworkReferenceKind
    {
        Empty,
        Framework,
        Managed,
        External,
        Unresolved
    }
}
