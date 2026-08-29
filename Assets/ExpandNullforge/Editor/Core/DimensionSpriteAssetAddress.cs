namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The two halves of the address a generated sprite asset is found by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AN ADDRESS IS AN IDENTITY, so there is one of this. A sprite asset's address is what the game
    /// looks it up by, and it is recomputed every time the asset is regenerated. Four copies of this
    /// walk existed — two of them in the same file, twelve hundred lines apart — and a divergence
    /// between any two of them would not fail: it would re-address the asset, and everything holding
    /// the old address would find nothing, silently.
    /// </para>
    /// <para>
    /// IT IS NOT THE SAME HASH AS <c>DimensionFnv</c> and must not be folded into it. This one takes
    /// a salt into the basis and feeds one char per step; that one feeds two bytes per char and
    /// takes no salt. Both are FNV-1a, both are load-bearing, and they answer different questions.
    /// </para>
    /// <para>
    /// A ZERO ADDRESS IS THE ONE VALUE THAT CANNOT BE USED, because zero is what an unaddressed
    /// asset already reads as, so a name that happened to hash there would be indistinguishable
    /// from one that was never addressed. It becomes the salt with its low bit set instead.
    /// </para>
    /// </remarks>
    internal static class DimensionSpriteAssetAddress
    {
        /// <summary>One half of an address: the hash of a name under a salt.</summary>
        public static long Part(string value, ulong salt)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;

                ulong hash = offsetBasis ^ salt;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= prime;
                }

                return (long)(hash == 0UL ? salt | 1UL : hash);
            }
        }
    }
}
