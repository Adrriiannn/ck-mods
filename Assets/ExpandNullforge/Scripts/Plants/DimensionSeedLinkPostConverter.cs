using ExpandNullforge.Foundation;
using HarmonyLib;
using Pug.Conversion;
using UnityEngine;

namespace ExpandNullforge.Plants
{
    /// <summary>
    /// Writes "this seed grows that plant" after every object in the game has a name and an id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TRAP THIS EXISTS FOR. A mod's objects get their ids one at a time, as they are converted:
    /// <c>ObjectConverter</c> is what puts a name into <c>API.Authoring</c>'s lookup, and it only
    /// runs on the object it is converting. Mod objects are converted in the order Core Keeper's
    /// <c>ExtraAuthoring</c> list happens to hold them, which is sorted by a hash of the prefab
    /// name. So a seed converted before its plant asks for an id that does not exist yet, gets
    /// <c>None</c>, and bakes a seed that grows nothing — and whether that happens depends on a
    /// hash, so it can differ between two crops in the same mod and change when a prefab is renamed.
    /// </para>
    /// <para>
    /// A post-converter runs after the whole conversion queue is drained and before the property
    /// blobs are built (<c>ConversionManager.ConvertEnqueuedObjects</c> calls
    /// <c>FinalizeProperties</c> last), so by then every name resolves and the value still lands in
    /// the blob the game reads. That is the only point in the pipeline where both are true.
    /// </para>
    /// <para>
    /// Core Keeper has no way for a mod to add a post-converter — <c>ECSManager</c> builds a fixed
    /// list — so the list is joined with one postfix. The seed's own converter still resolves the
    /// name when it can, and this skips any seed that already has the property, so the ordinary case
    /// costs nothing and the unlucky one is repaired.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ECSManager), nameof(ECSManager.ConfigurePostConverters))]
    internal static class DimensionSeedLinkPatch
    {
        [HarmonyPostfix]
        private static void After(ConversionManager conversionManager)
        {
            if (conversionManager == null)
            {
                return;
            }

            // Logged because a patch that stops binding after a game update is invisible otherwise,
            // and the symptom — seeds that grow nothing — looks like a content mistake.
            DimensionFrameworkLog.Verbose(
                "Seed-to-plant linking joined this conversion run.");
            conversionManager.AddPostConverter(new DimensionSeedLinkPostConverter(conversionManager));
        }
    }

    /// <summary>Fills in a seed's plant id once every plant has one.</summary>
    internal sealed class DimensionSeedLinkPostConverter : PostConverter
    {
        /// <summary>
        /// Kept because the base class hands the manager to itself and does not share it, and the
        /// property builder is the whole reason this class exists.
        /// </summary>
        private readonly ConversionManager conversionManager;

        public DimensionSeedLinkPostConverter(ConversionManager conversionManager)
        {
            this.conversionManager = conversionManager;
        }

        public override void PostConvert(GameObject authoring)
        {
            if (authoring == null || conversionManager == null)
            {
                return;
            }

            DimensionSeedAuthoring seed;
            if (!TryGetActiveComponent(authoring, out seed) ||
                string.IsNullOrEmpty(seed.turnsIntoPlantName))
            {
                return;
            }

            int index = UniqueIndexOf(authoring);
            string property = DimensionPlantProperties.TurnsIntoPlantId;
            if (conversionManager.PropertyBuilder.HasProperty(index, property))
            {
                return;
            }

            ObjectID plant = DimensionPlantNames.Resolve(seed.turnsIntoPlantName, "seed");
            if (plant == ObjectID.None)
            {
                return;
            }

            conversionManager.PropertyBuilder.SetProperty(index, property, plant);
        }

        /// <summary>
        /// The index the property builder files an object's properties under.
        /// </summary>
        /// <remarks>
        /// Reproduced from <c>ConversionManager.GetUniqueIndex</c>, which is private: the instance
        /// id, folded into the positive range when it is negative. Writing under any other number
        /// would file the property against an object that does not exist, and nothing would report
        /// it.
        /// </remarks>
        private static int UniqueIndexOf(GameObject gameObject)
        {
            int instanceId = gameObject.GetInstanceID();
            return instanceId < 0 ? int.MaxValue + instanceId : instanceId;
        }
    }
}
