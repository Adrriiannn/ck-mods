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

    [InternalBufferCapacity(2)]
    public struct DimensionSummonBossNameBuffer : IBufferElementData
    {
        public FixedString64Bytes BossName;
    }

    public struct DimensionSummonHydrateCD : IComponentData
    {
        public byte Hydrated;
    }

    [Preserve]
    public sealed class DimensionSummoningItemConverter :
        SingleAuthoringComponentConverter<DimensionSummoningItemAuthoring>
    {
        protected override void Convert(DimensionSummoningItemAuthoring authoring)
        {
            if (authoring == null || authoring.bossObjectNames == null)
            {
                return;
            }

            AddComponentData(new DimensionSummonHydrateCD());

            // The vanilla buffer must exist even while empty: the summoning system's item walk
            // reads it by lookup, and hydration can only append into a buffer that is there.
            EnsureHasBuffer<SummoningItemBuffer>();
            EnsureHasBuffer<DimensionSummonBossNameBuffer>();
            for (int i = 0; i < authoring.bossObjectNames.Count; i++)
            {
                if (string.IsNullOrEmpty(authoring.bossObjectNames[i]))
                {
                    continue;
                }

                AddToBuffer(new DimensionSummonBossNameBuffer
                {
                    BossName = DimensionCreatureNames.ToFixed64(authoring.bossObjectNames[i])
                });
            }
        }
    }

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

    public struct DimensionSummonAreaNameCD : IComponentData
    {
        public FixedString64Bytes Boss;

        public FixedString64Bytes Optional;

        public byte Hydrated;
    }

    [Preserve]
    public sealed class DimensionSummonAreaByNameConverter :
        SingleAuthoringComponentConverter<DimensionSummonAreaByNameAuthoring>
    {
        protected override void Convert(DimensionSummonAreaByNameAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionSummonAreaNameCD
            {
                Boss = DimensionCreatureNames.ToFixed64(authoring.bossObjectName),
                Optional = DimensionCreatureNames.ToFixed64(authoring.optionalBossObjectName),
                Hydrated = 0
            });
        }
    }

    /// <summary>The name the creature converters ask by.</summary>
    /// <remarks>
    /// The conversion itself lives in <see cref="ExpandNullforge.Core.DimensionFixedStrings"/>, with
    /// every other path that writes an id into a component, because those ids are compared. This
    /// type stays because it is public and a consumer could be calling it, and because the creature
    /// converters read better asking a creature-shaped name than a general one.
    /// </remarks>
    public static class DimensionCreatureNames
    {
        /// <summary>A creature or boss object name as a 64-byte fixed string.</summary>
        public static FixedString64Bytes ToFixed64(string value)
        {
            return ExpandNullforge.Core.DimensionFixedStrings.ToFixed64(value);
        }
    }
}
