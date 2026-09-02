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
}
