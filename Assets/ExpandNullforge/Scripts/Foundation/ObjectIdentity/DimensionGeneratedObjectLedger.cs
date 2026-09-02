using System;
using System.Collections.Generic;
using PugMod;

namespace ExpandNullforge.Foundation
{
    /// <summary>
    /// Every object a generation run made, not only the items: creatures, bosses, summoning
    /// circles, map markers, plants, seeds, containers, workbenches, world objects, critters,
    /// vehicles, projectiles and blasts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS NOT <see cref="DimensionItemObjectRegistry"/>. That one is a promise: a content
    /// pack says "the game will have these items" and the framework names the ones that never
    /// arrived. Only the item generator feeds it, and its warning is worded for an item. This one
    /// is a list of everything the generators own, and its ids can legitimately never become an
    /// object — a creature whose generate pass has not been run yet, an asset the creator has
    /// since unticked — so nothing here complains about an id that does not resolve. It exists so
    /// the world-load audit has the whole subject list rather than the item slice of it.
    /// </para>
    /// <para>
    /// THE AUDIT WAS AIMED AT A LIST THAT COULD NOT CONTAIN ITS OWN EXEMPLAR. Its companion table
    /// is mostly creature and world-object queries — a summoning circle missing three of the four
    /// components <c>BossSummoningSystem</c> asks for is the case the table was written for — and
    /// its subject list held items only. On the two content packs in this repository that was two
    /// tileset blocks checked against thirty-nine creature rules, reported as a clean result.
    /// </para>
    /// <para>
    /// RESOLUTION GOES THROUGH THE ONE RESOLVER, <see cref="DimensionObjectNames"/>, so a name the
    /// game already has answers here as well as a mod's own qualified name.
    /// </para>
    /// </remarks>
    public static class DimensionGeneratedObjectLedger
    {
        private static readonly Dictionary<string, ObjectID> ResolvedObjects =
            new Dictionary<string, ObjectID>(StringComparer.Ordinal);

        /// <summary>Declared object id to the content pack that declared it.</summary>
        private static readonly Dictionary<string, string> DeclaredObjects =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>How many object ids content packs said they generated.</summary>
        public static int DeclaredCount
        {
            get { return DeclaredObjects.Count; }
        }

        /// <summary>How many of those the game answers to.</summary>
        public static int ResolvedCount
        {
            get { return ResolvedObjects.Count; }
        }

        /// <summary>Declared ids the game does not answer to yet.</summary>
        /// <remarks>
        /// Reported as a count and never as a fault. An id lands here when its asset was unticked
        /// after the manifest was written, or when the generator that builds that kind of object
        /// has not been run since — neither of which is something to warn a player about.
        /// </remarks>
        public static int UnresolvedCount
        {
            get { return DeclaredObjects.Count - ResolvedObjects.Count; }
        }

        /// <summary>Records the object ids a content pack's generation run produced.</summary>
        public static void Declare(string contentPackId, IEnumerable<string> objectIds)
        {
            if (objectIds == null)
            {
                return;
            }

            string owner = string.IsNullOrEmpty(contentPackId) ? "<unknown>" : contentPackId;
            foreach (string objectId in objectIds)
            {
                if (!string.IsNullOrEmpty(objectId))
                {
                    DeclaredObjects[objectId] = owner;
                }
            }
        }

        /// <summary>
        /// Re-checks every declared id that has no object yet, and returns how many resolved in
        /// total. Idempotent: it only looks at what is still unresolved.
        /// </summary>
        public static int RefreshAll()
        {
            foreach (KeyValuePair<string, string> declared in DeclaredObjects)
            {
                if (ResolvedObjects.ContainsKey(declared.Key))
                {
                    continue;
                }

                ObjectID id = DimensionObjectNames.Resolve(declared.Key);
                if (id != ObjectID.None)
                {
                    ResolvedObjects[declared.Key] = id;
                }
            }

            return ResolvedObjects.Count;
        }

        /// <summary>
        /// Every declared id that resolved, with the object it resolved to. A copy, because the
        /// audit takes a while over each entry.
        /// </summary>
        public static List<KeyValuePair<string, ObjectID>> Resolved()
        {
            List<KeyValuePair<string, ObjectID>> objects =
                new List<KeyValuePair<string, ObjectID>>(ResolvedObjects.Count);
            foreach (KeyValuePair<string, ObjectID> pair in ResolvedObjects)
            {
                if (pair.Value != ObjectID.None)
                {
                    objects.Add(pair);
                }
            }

            return objects;
        }

        /// <summary>Forgets everything, for a mod shutdown or a test.</summary>
        public static void Clear()
        {
            ResolvedObjects.Clear();
            DeclaredObjects.Clear();
        }
    }
}
