using Pug.Conversion;
using Pug.Properties;
using PugMod;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// A seed, naming the plant it grows into instead of pointing at a baked id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS AT ALL. Vanilla's <c>SeedAuthoring.turnsIntoPlantID</c> is an
    /// <c>ObjectID</c>, and a mod's own plant has no ObjectID while the prefab is being written in
    /// the editor: <c>PugMod.API.Authoring</c> is a static field a loaded mod context assigns, so in
    /// the editor it is null and every lookup answers <c>None</c>. A generator that bakes the id
    /// therefore bakes <c>None</c>, and the failure is silent — the ripe seed reaches
    /// <c>PlantsGrowJob</c>, finds no object behind the property, destroys itself and grows nothing.
    /// A player sees a seed that vanishes ten minutes after planting.
    /// </para>
    /// <para>
    /// THE FIX IS THE TIMING, NOT THE DATA. Names resolve during conversion, which happens inside a
    /// running game after every mod has registered its objects — vanilla's own
    /// <c>ObjectAuthoring.ObjectInfo</c> calls <c>API.Authoring.GetObjectID</c> for other objects by
    /// name at exactly that moment. So this component carries the name and the converter below does
    /// the lookup, emitting the identical property set vanilla's <c>SeedConverter</c> emits.
    /// </para>
    /// <para>
    /// The generated seed prefab carries THIS component and NOT vanilla's <c>SeedAuthoring</c>. Both
    /// would write the same properties, and which one landed last would depend on converter
    /// registration order, which nothing guarantees.
    /// </para>
    /// </remarks>
    public sealed class DimensionSeedAuthoring : MonoBehaviour
    {
        [Tooltip("Full object name of the plant this seed grows into.")]
        public string turnsIntoPlantName;

        [Tooltip("Which variation of the seed the game's own golden roll places. 0 turns that roll off.")]
        public int rareSeedVariation;

        [Tooltip("Which variation of the plant a golden seed grows into.")]
        public int rarePlantVariation;

        [Tooltip("How many stages it passes through, how long each takes, and where it starts.")]
        public GrowingSettings growingSettings = new GrowingSettings();
    }
}
