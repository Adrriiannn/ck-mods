using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// Marks an item as the offering that summons a boss, by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOT vanilla's <c>SummoningItemAuthoring</c>, deliberately. That one bakes a list of
    /// ObjectIDs, and a mod's own boss has no id at generation time — it would bake as None and
    /// the item would summon nothing, silently. This bakes names; the hydration system appends
    /// the real ids into the vanilla <c>SummoningItemBuffer</c> at runtime, and from then on the
    /// game's own <c>BossSummoningSystem</c> treats the item exactly like a vanilla idol —
    /// refusal while the boss lives, anticipation glow, tile clear, the whole staging.
    /// </para>
    /// </remarks>
    public sealed class DimensionSummoningItemAuthoring : MonoBehaviour
    {
        [Tooltip("Full object names of the bosses this item can summon.")]
        public List<string> bossObjectNames = new List<string>();
    }
}
