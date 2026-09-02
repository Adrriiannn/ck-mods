using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The arena summoning circle's boss link, by name — the same pattern as the item.
    /// </summary>
    /// <remarks>
    /// Rides BESIDE the vanilla <c>SummonAreaAuthoring</c>, which carries every number the
    /// circle needs (anticipation, spawn time, tile clear) but whose boss id bakes as None for
    /// mod bosses. Hydration writes the real ids into <c>SummonAreaCD</c> on the first ticks.
    /// </remarks>
    public sealed class DimensionSummonAreaByNameAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of the boss this circle summons.")]
        public string bossObjectName;

        [Tooltip("A second boss it may summon instead. Usually empty.")]
        public string optionalBossObjectName;
    }
}
