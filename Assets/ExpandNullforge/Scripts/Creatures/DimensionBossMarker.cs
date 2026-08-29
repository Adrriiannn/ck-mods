using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;
using ExpandNullforge.Core;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// The link between a boss's map marker object and the boss it pins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla's marker prefabs bake the boss's ObjectID straight into <c>MapMarkerAuthoring</c>
    /// and <c>CanBeScannedAuthoring</c>. A mod cannot: at generation time the mod's own objects
    /// have no ids yet (the authoring API is not loaded in the editor), so the id would bake as
    /// None. This authoring bakes the NAME instead, and the hydration system writes the real id
    /// into both components on the entity's first ticks — the offering pattern, already proven
    /// by the portals.
    /// </para>
    /// </remarks>
    public sealed class DimensionBossMarkerAuthoring : MonoBehaviour
    {
        [Tooltip("The full object name of the boss this marker pins.")]
        public string bossObjectName;
    }

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
