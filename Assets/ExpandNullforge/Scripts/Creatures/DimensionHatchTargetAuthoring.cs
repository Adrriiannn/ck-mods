using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// What a creature hatches into when a player nears, by NAME.
    /// </summary>
    /// <remarks>
    /// The game's cocoons are ordinary creatures carrying one component whose spawn target is
    /// a baked ObjectID; a mod's egg naming its own creature needs the name path. Hydration
    /// writes <c>HatchWhenPlayerNearbyStateCD.objectToSpawn</c> on the first server ticks —
    /// well inside the window, since nothing hatches until a player stands within five tiles.
    /// </remarks>
    public sealed class DimensionHatchTargetAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of what hatches out.")]
        public string spawnObjectName;
    }
}
