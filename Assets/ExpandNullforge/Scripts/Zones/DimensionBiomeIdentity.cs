using System.Collections.Generic;
using ExpandNullforge.Core;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// Gives a custom biome a <see cref="Biome"/> value of its own, so the game's own biome machinery
    /// can tell it apart from every other biome.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS SAFE. <c>Biome</c> is an int-backed enum with a dozen named members, but nothing in
    /// Core Keeper treats it as a bounded index — discovery is a <c>List&lt;Biome&gt;</c> tested with
    /// <c>Contains</c>, region titles are a list searched by equality, and the ambient and music
    /// handlers key off tilesets rather than biome. So a value outside the named range behaves
    /// correctly everywhere it travels, and it is what lets custom biomes reuse vanilla's own title
    /// card, music duck and discovery tracking instead of growing a parallel implementation.
    /// </para>
    /// <para>
    /// WHY THE OFFSET IS LARGE. Ids start at 1000, far past <c>__MAX_VALUE__</c>, for the same reason
    /// custom tileset ids do: Core Keeper keeps adding biomes, and a mod that squatted just past the
    /// current end of the enum would start colliding the first time the game shipped a new one.
    /// </para>
    /// <para>
    /// WHY IT IS DERIVED, NOT COUNTED. The value comes from the biome's id, so it is the same in every
    /// session, on every machine, and for every player in a multiplayer world — and it survives the
    /// author adding a biome above it in a list. A running counter would produce a different value the
    /// moment the ordering changed, which would silently un-discover every biome a player had found.
    /// </para>
    /// </remarks>
    public static class DimensionBiomeIdentity
    {
        /// <summary>The first value handed out, chosen to leave Core Keeper room to grow.</summary>
        public const int FirstCustomBiomeValue = 1000;

        /// <summary>One past the last value handed out.</summary>
        public const int EndCustomBiomeValue = int.MaxValue;


        private static readonly Dictionary<string, Biome> Assigned =
            new Dictionary<string, Biome>(System.StringComparer.Ordinal);

        private static readonly Dictionary<Biome, string> ByValue = new Dictionary<Biome, string>();

        /// <summary>
        /// The biome value for an id, assigning one on first use.
        /// </summary>
        /// <remarks>
        /// Collisions are resolved by walking forward from the hash rather than by re-hashing, so the
        /// answer stays deterministic for a given set of registered ids. Two biomes that landed on the
        /// same value would share a discovery record and a title, which is the one outcome worth a
        /// little extra work to avoid.
        /// </remarks>
        public static Biome GetOrAssign(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
            {
                return Biome.None;
            }

            Biome existing;
            if (Assigned.TryGetValue(biomeId, out existing))
            {
                return existing;
            }

            ulong hash = DimensionFnv.Hash(biomeId);
            ulong range = (ulong)(EndCustomBiomeValue - FirstCustomBiomeValue);
            int candidate = FirstCustomBiomeValue + (int)(hash % range);

            string owner;
            while (ByValue.TryGetValue((Biome)candidate, out owner))
            {
                candidate++;
                if (candidate >= EndCustomBiomeValue)
                {
                    candidate = FirstCustomBiomeValue;
                }
            }

            Biome assigned = (Biome)candidate;
            Assigned.Add(biomeId, assigned);
            ByValue.Add(assigned, biomeId);
            return assigned;
        }

        /// <summary>The id behind a value, for turning what the game reports back into our terms.</summary>
        public static bool TryGetBiomeId(Biome biome, out string biomeId)
        {
            return ByValue.TryGetValue(biome, out biomeId);
        }

        /// <summary>Whether a value is one of ours rather than one of Core Keeper's.</summary>
        public static bool IsCustom(Biome biome)
        {
            return (int)biome >= FirstCustomBiomeValue;
        }

        public static void Clear()
        {
            Assigned.Clear();
            ByValue.Clear();
        }

    }
}
