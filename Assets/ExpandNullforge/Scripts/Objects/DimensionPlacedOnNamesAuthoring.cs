using System.Collections.Generic;
using ExpandNullforge.Foundation;
using Pug.Conversion;
using UnityEngine;
using UnityEngine.Scripting;

namespace ExpandNullforge.Objects
{
    /// <summary>
    /// The property names Core Keeper's placement check reads "what this can stand on" out of.
    /// </summary>
    /// <remarks>
    /// Spelling is identity: a property is filed under <c>Property.StringToHash(name)</c> and
    /// <c>PlacementHandler</c> is Burst-compiled with those hashes as literal integers
    /// (<c>ck-db\PugProperties\Pug\Properties\PropertyID.cs:111</c> holds <c>-789473209</c> for
    /// <c>canBePlacedOnObjects</c>). A name one character out hashes to something else, the check
    /// finds an empty list, and the object can be placed nowhere — with no word in the log.
    /// </remarks>
    public static class DimensionPlacementProperties
    {
        public const string CanBePlacedOnObjects = "PlaceableObject/canBePlacedOnObjects";

        public const string AllowedObjects = "CanBePlaced/allowedObjects";
    }

    /// <summary>
    /// What an object may and may not be put down on, named rather than numbered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS ONE CANNOT BE HYDRATED AT RUNTIME, unlike every other object link in the framework.
    /// <c>PlaceableObjectConverter</c> writes these lists with <c>SetPropertyList</c>
    /// (<c>ck-db\Pug.ECS.Conversion\PlaceableObjectConverter.cs:70-72</c>), into the per-object
    /// property blob. That blob is sealed the moment <c>ConversionManager.ConvertEnqueuedObjects</c>
    /// calls <c>FinalizeProperties</c>, and it is read afterwards by the Burst-compiled
    /// <c>PlacementHandler</c>. There is no component to write, no buffer to append to, and the
    /// reader cannot be patched. A hydration system cannot reach it on any tick, at any cost.
    /// </para>
    /// <para>
    /// So the names ride here instead, and the two halves below resolve them at conversion — which
    /// happens inside a running game, after every mod has registered its objects.
    /// </para>
    /// <para>
    /// Parallel string arrays rather than a list of a small class: a serialized
    /// <c>List&lt;CustomClass&gt;</c> on an authoring component does not survive the trip into the
    /// game, and the failure is an empty list rather than an error.
    /// </para>
    /// </remarks>
    public sealed class DimensionPlacedOnNamesAuthoring : MonoBehaviour
    {
        [Tooltip("Full object names this can be put down on. Vanilla names bare, your own with your mod in front, like 'MyMod:Jetty'.")]
        public string[] canBePlacedOnNames = new string[0];

        [Tooltip("Full object names of the tile objects this stays alive on. Empty means it does not care.")]
        public string[] allowedTileObjectNames = new string[0];

        // THERE ARE DELIBERATELY NO FIELDS HERE for "can never be put down on" or "is destroyed
        // on". Public and tooltipped, they would be written by no generator anywhere, and a field
        // a person can see and fill in that reaches nothing is worse than a missing feature,
        // because it reads as one that works. When a generator has a question to ask that needs
        // them, they arrive together with the pass that fills them in.
    }

    /// <summary>
    /// Writes the four placement lists when every name on them already resolves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ALL OR NOTHING, ON PURPOSE. <c>SetPropertyList</c> overwrites rather than appends, so a
    /// partial list written here and topped up later is not a thing that can happen — the second
    /// write would be the only one that counted anyway. When even one name is still unknown this
    /// writes nothing and <see cref="DimensionPlacedOnLinkPostConverter"/> writes the whole array
    /// once the conversion queue has drained.
    /// </para>
    /// <para>
    /// The framework's generator attaches THIS component and leaves the mod-owned names off
    /// vanilla's <c>PlaceableObjectAuthoring</c>, so the two never write different answers into the
    /// same property in an order nothing guarantees.
    /// </para>
    /// </remarks>
    [Preserve]
    public sealed class DimensionPlacedOnNamesConverter :
        SingleAuthoringComponentConverter<DimensionPlacedOnNamesAuthoring>
    {
        protected override void Convert(DimensionPlacedOnNamesAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            DimensionPlacedOnLists lists = DimensionPlacedOnLists.Build(authoring);
            if (!lists.EverythingResolved)
            {
                return;
            }

            lists.WriteWith(
                (property, values) => SetPropertyList(property, values));
        }
    }

    /// <summary>
    /// The two arrays this component turns into, resolved once and shared by both writers.
    /// </summary>
    /// <remarks>
    /// One place builds them so the converter and the post-converter cannot disagree about what
    /// "the complete list" is — which is the whole failure mode the post-converter exists to catch.
    /// </remarks>
    internal readonly struct DimensionPlacedOnLists
    {
        private DimensionPlacedOnLists(
            ObjectID[] canBePlacedOn,
            ObjectID[] allowedTiles,
            bool everythingResolved)
        {
            CanBePlacedOn = canBePlacedOn;
            AllowedTiles = allowedTiles;
            EverythingResolved = everythingResolved;
        }

        public ObjectID[] CanBePlacedOn { get; }

        public ObjectID[] AllowedTiles { get; }

        /// <summary>False when at least one name still answers to nothing.</summary>
        public bool EverythingResolved { get; }

        public bool HasAnything
        {
            get
            {
                return CanBePlacedOn.Length > 0 || AllowedTiles.Length > 0;
            }
        }

        public static DimensionPlacedOnLists Build(DimensionPlacedOnNamesAuthoring authoring)
        {
            bool all = true;
            ObjectID[] placedOn = Resolve(authoring.canBePlacedOnNames, ref all);

            // The game merges "what it can be placed on" into "what it survives on" itself
            // (ck-db\Pug.ECS.Conversion\DestroyIfNotOnTileConverter.cs:37-40), so the merge is
            // reproduced here rather than left to a converter that already read a shorter list.
            List<ObjectID> allowed = new List<ObjectID>(placedOn);
            ObjectID[] tiles = Resolve(authoring.allowedTileObjectNames, ref all);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (!allowed.Contains(tiles[i]))
                {
                    allowed.Add(tiles[i]);
                }
            }

            return new DimensionPlacedOnLists(placedOn, allowed.ToArray(), all);
        }

        /// <summary>Hands each non-empty list to the writer, which is a converter or a post-converter.</summary>
        public void WriteWith(System.Action<string, ObjectID[]> write)
        {
            if (CanBePlacedOn.Length > 0)
            {
                write(DimensionPlacementProperties.CanBePlacedOnObjects, CanBePlacedOn);
            }

            if (AllowedTiles.Length > 0)
            {
                write(DimensionPlacementProperties.AllowedObjects, AllowedTiles);
            }
        }

        private static ObjectID[] Resolve(string[] names, ref bool everythingResolved)
        {
            if (names == null || names.Length == 0)
            {
                return new ObjectID[0];
            }

            List<ObjectID> resolved = new List<ObjectID>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }

                ObjectID id = DimensionObjectNames.Resolve(names[i]);
                if (id == ObjectID.None)
                {
                    everythingResolved = false;
                    continue;
                }

                resolved.Add(id);
            }

            return resolved.ToArray();
        }
    }
}
