using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{

    [InternalBufferCapacity(4)]
    public struct DimensionShopStockNameBuffer : IBufferElementData
    {
        public FixedString64Bytes ItemName;
    }

    public struct DimensionShopHydrateCD : IComponentData
    {
        public byte Hydrated;
    }

    [Preserve]
    public sealed class DimensionShopStockConverter :
        SingleAuthoringComponentConverter<DimensionShopStockAuthoring>
    {
        protected override void Convert(DimensionShopStockAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new VendingMachineCD
            {
                sizeX = authoring.sizeX < 1 ? 1 : authoring.sizeX,
                sizeY = authoring.sizeY < 1 ? 1 : authoring.sizeY
            });
            AddComponentData(new DimensionShopHydrateCD());
            EnsureHasBuffer<VendingMachineItemBuffer>();
            EnsureHasBuffer<DimensionShopStockNameBuffer>();
            if (authoring.itemNames == null)
            {
                return;
            }

            for (int i = 0; i < authoring.itemNames.Count; i++)
            {
                if (string.IsNullOrEmpty(authoring.itemNames[i]))
                {
                    continue;
                }

                AddToBuffer(new DimensionShopStockNameBuffer
                {
                    ItemName = DimensionCreatureNames.ToFixed64(authoring.itemNames[i])
                });
            }
        }
    }

    public struct DimensionHatchTargetCD : IComponentData
    {
        public FixedString64Bytes SpawnName;

        public byte Hydrated;
    }

    [Preserve]
    public sealed class DimensionHatchTargetConverter :
        SingleAuthoringComponentConverter<DimensionHatchTargetAuthoring>
    {
        protected override void Convert(DimensionHatchTargetAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            AddComponentData(new DimensionHatchTargetCD
            {
                SpawnName = DimensionCreatureNames.ToFixed64(authoring.spawnObjectName),
                Hydrated = 0
            });
        }
    }
}
