using System;
using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ExpandNullforge.Portals
{
  /// <summary>Marks a portal entity as one that asks for an offering.</summary>
  public sealed class DimensionPortalOfferingAuthoring : MonoBehaviour
  {
    [Tooltip("The items the portal asks for — one slot each.")]
    public List<DimensionPortalOfferingAuthoringEntry> entries =
        new List<DimensionPortalOfferingAuthoringEntry>();
  }
}
