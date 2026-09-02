using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Explosives
{

    /// <summary>A blast that should leave fire, and which of the two patches.</summary>
    public struct DimensionBlastFireCD : IComponentData
    {
        public int NapalmVariation;
    }

    [Preserve]
    public sealed class DimensionBlastFireConverter :
        SingleAuthoringComponentConverter<DimensionBlastFireAuthoring>
    {
        protected override void Convert(DimensionBlastFireAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionBlastFireCD
            {
                NapalmVariation = authoring.napalmVariation < 0 ? 0 : authoring.napalmVariation
            });
        }
    }
}
