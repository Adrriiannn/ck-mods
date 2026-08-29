using System.Collections.Generic;
using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Creatures
{
    /// <summary>
    /// A shop's stock, by item NAME — hydrated into the vanilla vending buffer at runtime.
    /// </summary>
    /// <remarks>
    /// Vanilla's <c>VendingMachineAuthoring</c> bakes ObjectIDs, and the mod's own items have
    /// none at generation time. This bakes names beside the vanilla components; the hydration
    /// system appends the real ids into <c>VendingMachineItemBuffer</c>, and from then on the
    /// game's own buy window and pricing treat the object exactly like a Metropolis machine.
    /// </remarks>
    public sealed class DimensionShopStockAuthoring : MonoBehaviour
    {
        [Tooltip("Full object names of what it sells.")]
        public List<string> itemNames = new List<string>();

        [Tooltip("The shop window's grid.")]
        public int sizeX = 3;

        public int sizeY = 3;
    }

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
