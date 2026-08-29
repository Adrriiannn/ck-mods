using ExpandNullforge.Foundation;
using HarmonyLib;
using Pug.Conversion;
using UnityEngine;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// Writes "this can stand on that" after every object in the game has a name and an id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE TRAP THIS EXISTS FOR. A mod's objects get their ids one at a time, as they are converted,
    /// and mod objects are converted in the order Core Keeper's <c>ExtraAuthoring</c> list happens to
    /// hold them, which is sorted by a hash of the prefab name. So a vehicle converted before the
    /// block it stands on asks for an id that does not exist yet, gets <c>None</c>, and bakes a
    /// vehicle that can be put down nowhere — and whether that happens depends on a hash, so it can
    /// differ between two objects in the same mod and change when a prefab is renamed.
    /// </para>
    /// <para>
    /// A post-converter runs after the whole conversion queue is drained and before the property
    /// blobs are built (<c>ConversionManager.ConvertEnqueuedObjects</c> calls
    /// <c>FinalizeProperties</c> last), so by then every name resolves and the value still lands in
    /// the blob the game reads. That is the only point in the pipeline where both are true.
    /// </para>
    /// <para>
    /// NO <c>HasProperty</c> GUARD — and that is the one difference from the seed link, which does
    /// have one. Vanilla's own <c>PlaceableObjectConverter</c> and <c>DestroyIfNotOnTileConverter</c>
    /// also run on our prefab and will have written a SHORTER list from the ids that were already
    /// numbers. Skipping because the property exists would leave that shorter list standing. Ours
    /// must overwrite, and post-converters run after all converters, so it lands last.
    /// </para>
    /// <para>
    /// Core Keeper has no way for a mod to add a post-converter — <c>ECSManager</c> builds a fixed
    /// list — so the list is joined with one postfix.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(ECSManager), nameof(ECSManager.ConfigurePostConverters))]
    internal static class DimensionPlacedOnLinkPatch
    {
        [HarmonyPostfix]
        private static void After(ConversionManager conversionManager)
        {
            if (conversionManager == null)
            {
                return;
            }

            // Logged because a patch that stops binding after a game update is invisible otherwise,
            // and the symptom — a vehicle that refuses to go down on the mod's own rail — looks like
            // a content mistake.
            DimensionFrameworkLog.Verbose(
                "Placement-target linking joined this conversion run.");
            conversionManager.AddPostConverter(
                new DimensionPlacedOnLinkPostConverter(conversionManager));
        }
    }

    /// <summary>Fills in the placement lists once every object they name has an id.</summary>
    internal sealed class DimensionPlacedOnLinkPostConverter : PostConverter
    {
        /// <summary>
        /// Kept because the base class hands the manager to itself and does not share it, and the
        /// property builder is the whole reason this class exists.
        /// </summary>
        private readonly ConversionManager conversionManager;

        public DimensionPlacedOnLinkPostConverter(ConversionManager conversionManager)
        {
            this.conversionManager = conversionManager;
        }

        public override void PostConvert(GameObject authoring)
        {
            if (authoring == null || conversionManager == null)
            {
                return;
            }

            DimensionPlacedOnNamesAuthoring names;
            if (!TryGetActiveComponent(authoring, out names))
            {
                return;
            }

            DimensionPlacedOnLists lists = DimensionPlacedOnLists.Build(names);
            if (!lists.HasAnything)
            {
                return;
            }

            int index = UniqueIndexOf(authoring);
            lists.WriteWith(
                (property, values) =>
                    conversionManager.PropertyBuilder.SetPropertyList(index, property, values));

            if (!lists.EverythingResolved)
            {
                SayWhichNamesFailed(authoring.name, names);
            }
        }

        /// <summary>
        /// Names the entries that answered to nothing even after the whole queue converted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The lists that DID resolve are still written above rather than thrown away: an object
        /// whose placement list is empty can be put down nowhere at all, which is a worse outcome
        /// than one entry missing from it.
        /// </para>
        /// <para>
        /// Said in full here rather than through <c>ResolveOrSayWhy</c>. That helper builds its
        /// sentence around "a &lt;thing&gt; names …", which came out as "A thing something can be
        /// placed on names 'MyMod:Jetty'" — no object named, and advice to add the mod prefix to a
        /// name the generator had already written with one.
        /// </para>
        /// </remarks>
        private static void SayWhichNamesFailed(
            string ownerName,
            DimensionPlacedOnNamesAuthoring names)
        {
            Complain(ownerName, names.canBePlacedOnNames, "can also go on");
            Complain(ownerName, names.allowedTileObjectNames, "only stays where there is");
        }

        private static readonly System.Collections.Generic.HashSet<string> Said =
            new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        private static void Complain(string ownerName, string[] names, string what)
        {
            for (int i = 0; names != null && i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]) ||
                    DimensionObjectNames.Resolve(names[i]) != ObjectID.None)
                {
                    continue;
                }

                string line =
                    "'" + ownerName + "' " + what + " '" + names[i] + "', which nothing in this " +
                    "world is called. If it is one of yours, check it is still switched on in the " +
                    "dashboard and generate again; if it belongs to another mod, that mod is not " +
                    "installed.";
                if (Said.Add(line))
                {
                    DimensionFrameworkLog.Warning(line);
                }
            }
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
