using Pug.Conversion;
using Pug.Properties;
using PugMod;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// The things a plant gives back besides its produce — its seed, and any extras a better
    /// version of the crop is worth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE CHANCE COVERS THE WHOLE LIST, and that is the game's shape rather than a simplification.
    /// <c>DropLootConverter</c> writes each entry into <c>DropsLootBuffer</c> and the single
    /// <c>customLoot.chance</c> into <c>ChanceToDropLootCD</c>, which is one component per object;
    /// <c>DropLootFromLootBuffersJob</c> then rolls that same number separately for every entry.
    /// There is nowhere to put a second chance.
    /// </para>
    /// <para>
    /// Two things move the number at harvest and both are vanilla's doing: a plant that has NOT
    /// finished growing drops everything at 100% (dig up a sprout and you always get the seed
    /// back), and a ripe one adds the Grateful Gardener talent on top.
    /// </para>
    /// </remarks>
    public sealed class DimensionPlantDropsAuthoring : MonoBehaviour
    {
        [Tooltip("Full object names of what it gives back. Parallel to the amounts below.")]
        public string[] itemNames = new string[0];

        [Tooltip("How many of each. Parallel to the names above.")]
        public int[] amounts = new int[0];

        [Tooltip("How often each of them is given, from 0 to 1. Vanilla crops return their seed at 0.75.")]
        public float chance = 0.75f;
    }
}
