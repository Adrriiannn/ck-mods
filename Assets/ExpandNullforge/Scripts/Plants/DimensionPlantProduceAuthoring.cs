using Pug.Conversion;
using Pug.Properties;
using PugMod;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// What a plant gives when it is picked, named rather than baked — and how many.
    /// </summary>
    /// <remarks>
    /// Same name-resolution reason as the seed. It also carries the harvest count, which vanilla's
    /// <c>PlantConverter</c> hardcodes to <c>numberOfPlantsToDrop = 1</c> with no way to author it:
    /// the field is on the mutable <c>PlantCD</c> and the game itself raises it at kill time for the
    /// harvest-chance stat, so a crop that yields three of something is entirely supportable and
    /// simply had no authoring surface.
    /// </remarks>
    public sealed class DimensionPlantProduceAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of what picking it gives.")]
        public string produceName;

        [Tooltip("How many of that a single harvest gives.")]
        public int numberOfPlantsToDrop = 1;

        [Tooltip("How many stages it passes through, how long each takes, and where it starts.")]
        public GrowingSettings growingSettings = new GrowingSettings();
    }
}
