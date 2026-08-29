using System;
using Unity.Collections;

namespace ExpandNullforge.Core
{
    /// <summary>
    /// Turns a C# string into the fixed-size string an ECS component can hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE CONVERSION, BECAUSE THESE VALUES ARE COMPARED. A portal id, a dimension id and a travel
    /// id are written into components on one path and read back on another, and the two only match
    /// if both were encoded the same way. Sixteen copies of this used to exist and one of them was
    /// not the same: the item-portal spawn path used <c>CopyFromTruncated</c>, which truncates at
    /// 61 BYTES and keeps control characters, while every other path truncated at 63 CHARS and
    /// stripped them. A portal id carrying a non-ASCII character therefore encoded one way when the
    /// item portal wrote it and another way everywhere else, and nothing compared equal.
    /// </para>
    /// <para>
    /// CONTROL CHARACTERS ARE DROPPED, not replaced. They cannot be typed into an authoring field
    /// on purpose, they do not survive a round trip through the game's own text fields, and leaving
    /// them in means an id that looks identical in two places and is not.
    /// </para>
    /// <para>
    /// THE LIMIT IS COUNTED IN CHARS AND THE CAPACITY IS IN BYTES, which is worth knowing before
    /// relying on either. <c>FixedString64Bytes</c> holds 61 bytes of UTF-8, so 63 ASCII chars is
    /// already two over and the last two are dropped by <c>Append</c> rather than by the count. The
    /// behaviour is kept exactly as it was on the fifteen paths that agreed, because these values
    /// are identity: changing where the cut falls would re-encode every id longer than the limit,
    /// and any world already carrying one would stop matching.
    /// </para>
    /// <para>
    /// Only the two sizes with callers are here. A 32- or 512-byte version with nothing calling it
    /// is a surface nobody has ever run.
    /// </para>
    /// </remarks>
    internal static class DimensionFixedStrings
    {
        /// <summary>The most characters read out of a string for a 64-byte fixed string.</summary>
        public const int MaxCharsIn64 = 63;

        /// <summary>The most characters read out of a string for a 128-byte fixed string.</summary>
        public const int MaxCharsIn128 = 127;

        /// <summary>A 64-byte fixed string, control characters removed.</summary>
        public static FixedString64Bytes ToFixed64(string value)
        {
            FixedString64Bytes result = default(FixedString64Bytes);
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }

            int count = Math.Min(value.Length, MaxCharsIn64);
            for (int i = 0; i < count; i++)
            {
                if (!char.IsControl(value[i]))
                {
                    result.Append(value[i]);
                }
            }

            return result;
        }

        /// <summary>A 128-byte fixed string, control characters removed.</summary>
        public static FixedString128Bytes ToFixed128(string value)
        {
            FixedString128Bytes result = default(FixedString128Bytes);
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }

            int count = Math.Min(value.Length, MaxCharsIn128);
            for (int i = 0; i < count; i++)
            {
                if (!char.IsControl(value[i]))
                {
                    result.Append(value[i]);
                }
            }

            return result;
        }
    }
}
