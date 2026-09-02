using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;
using ExpandNullforge.Core;

namespace ExpandNullforge.Creatures
{

    public struct DimensionBossMarkerCD : IComponentData
    {
        public FixedString64Bytes BossName;

        public byte Hydrated;
    }

    [Preserve]
    public sealed class DimensionBossMarkerConverter :
        SingleAuthoringComponentConverter<DimensionBossMarkerAuthoring>
    {
        protected override void Convert(DimensionBossMarkerAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionBossMarkerCD
            {
                BossName = DimensionFixedStrings.ToFixed64(authoring.bossObjectName),
                Hydrated = 0
            });
        }
    }
}
