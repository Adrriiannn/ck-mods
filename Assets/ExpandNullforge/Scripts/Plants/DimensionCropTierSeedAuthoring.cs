using Pug.Conversion;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{
    /// <summary>The crop versions a seed can come up as, carried on every variation of that seed.</summary>
    /// <remarks>
    /// <para>
    /// PARALLEL ARRAYS, NOT A LIST OF ROWS. A serialized <c>List</c> of a mod-defined class does not
    /// survive the trip into the game — it arrives empty, with no error — so the row is split across
    /// four arrays that Unity can carry. The generator writes them together and the converter reads
    /// them together; nothing else should touch them.
    /// </para>
    /// <para>
    /// Every variation of the seed carries the same table, because the roll reads it off whichever
    /// seed happens to be in the ground and a seed that has already been rolled still has to be able
    /// to say which plant it becomes.
    /// </para>
    /// </remarks>
    public sealed class DimensionCropTierSeedAuthoring : MonoBehaviour
    {
        [Tooltip("Seed variation per version. Parallel to the other three arrays.")]
        public int[] seedVariations = new int[0];

        [Tooltip("Plant variation per version.")]
        public int[] plantVariations = new int[0];

        [Tooltip("Out of every hundred plantings, how many come up as this version.")]
        public float[] chancePercents = new float[0];

        [Tooltip("Whether the game's own golden roll decides this version instead of ours.")]
        public bool[] usesTheGamesGoldenRoll = new bool[0];

        /// <summary>
        /// The variation an ordinary planting lands on.
        /// </summary>
        /// <remarks>
        /// A planted seed has to end up on a variation that is not zero even when it rolled nothing,
        /// because zero is the only mark the roll has for "this has not been rolled yet". Without
        /// it a world reload would re-roll every seed already in the ground, and a player could keep
        /// reloading until a rare version came up.
        /// </remarks>
        [Tooltip("The seed variation an ordinary planting lands on, so it is never rolled twice.")]
        public int plainSeedVariation;
    }
}
