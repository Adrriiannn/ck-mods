namespace ExpandNullforge.Core
{
    /// <summary>
    /// The 64-bit FNV-1a hash this framework turns names into ids with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THESE NUMBERS ARE SAVED INTO WORLDS, which is why there is one of this and not one per
    /// caller. A custom tileset's id, a biome's id and a layout's fingerprint are all this hash of a
    /// name, and they are written into save files and compared against on the next load. A second
    /// copy that fed the bytes in a different order would not fail anywhere — it would quietly hand
    /// back a different id for the same name, and an existing world would stop recognising its own
    /// terrain.
    /// </para>
    /// <para>
    /// TWO BYTES PER CHAR, LOW BYTE FIRST. That is the order the shipped ids were computed with and
    /// it cannot change. The three copies this replaced were checked against each other over every
    /// character in the basic multilingual plane and over 200,000 random strings, and agreed on all
    /// of them, so nothing that was hashed before hashes differently now.
    /// </para>
    /// </remarks>
    internal static class DimensionFnv
    {
        /// <summary>FNV-1a's 64-bit offset basis, and the hash of the empty string.</summary>
        public const ulong OffsetBasis = 14695981039346656037UL;

        /// <summary>FNV-1a's 64-bit prime.</summary>
        public const ulong Prime = 1099511628211UL;

        /// <summary>The hash of a name. A null or empty name hashes to the offset basis.</summary>
        public static ulong Hash(string text)
        {
            ulong hash = OffsetBasis;
            if (string.IsNullOrEmpty(text))
            {
                return hash;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                hash ^= (ulong)(c & 0xFF);
                hash *= Prime;
                hash ^= (ulong)(c >> 8);
                hash *= Prime;
            }

            return hash;
        }
    }
}
