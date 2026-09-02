using System.Globalization;
using System.Text;
using ExpandNullforge.Core;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A short, stable code for "the shape of this world", used to notice when a layout has changed
    /// under an existing save.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Core Keeper generates terrain lazily: the ground under a player's feet is
    /// created the first time anyone walks there, not when the world is made. So if a mod ships a new
    /// layout, an existing save does not regenerate — it keeps the old terrain where the player has
    /// been and produces the NEW layout everywhere they have not. The result is a seam, biomes that
    /// stop halfway, and a world that makes no sense, with nothing in any log to explain it.
    /// </para>
    /// <para>
    /// A version number alone cannot catch that, because the number only changes when the author
    /// remembers to change it. The fingerprint changes on its own whenever the world's shape does,
    /// which turns "I edited a ring and forgot to publish" from a silent corruption into a message.
    /// </para>
    /// <para>
    /// WHAT IS AND IS NOT INCLUDED. Only fields that move a biome boundary. Display names, notes and
    /// ids that never reach generation are deliberately left out — renaming a ring is not a reshape,
    /// and warning about it would train authors to ignore the warning that matters.
    /// </para>
    /// </remarks>
    public static class DimensionLayoutFingerprint
    {

        /// <summary>The fingerprint of a layout that does not exist, so callers need no null branch.</summary>
        public const string Empty = "00000000";

        /// <summary>
        /// Computes the fingerprint of a layout as an eight-character hex code.
        /// </summary>
        /// <remarks>
        /// Short on purpose. This is read by a person comparing two builds, not by a machine defending
        /// against tampering, and eight characters is enough that an accidental collision between two
        /// versions of one author's layout is not a practical concern.
        /// </remarks>
        public static string Compute(DimensionLayoutTemplateAsset layout)
        {
            if (layout == null)
            {
                return Empty;
            }

            StringBuilder canonical = new StringBuilder();
            AppendCanonicalForm(canonical, layout);
            return ToCode(DimensionFnv.Hash(canonical.ToString()));
        }

        /// <summary>
        /// Whether a stored fingerprint still describes this layout.
        /// </summary>
        /// <remarks>
        /// A blank stored fingerprint counts as matching. That is the case of a world made before this
        /// mechanism existed: there is nothing to compare against, and claiming a mismatch would send
        /// the author chasing a change nobody made.
        /// </remarks>
        public static bool Matches(DimensionLayoutTemplateAsset layout, string storedFingerprint)
        {
            if (string.IsNullOrEmpty(storedFingerprint))
            {
                return true;
            }

            return string.Equals(Compute(layout), storedFingerprint, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes the layout as text in a fixed order, which is what actually gets hashed.
        /// </summary>
        /// <remarks>
        /// Built as text rather than by mixing numbers directly so that a field added later cannot
        /// silently occupy the same hash space as an existing one — a new labelled line changes the
        /// input visibly, and the separators keep "12" followed by "3" apart from "1" followed by "23".
        /// </remarks>
        private static void AppendCanonicalForm(StringBuilder builder, DimensionLayoutTemplateAsset layout)
        {
            builder.Append("kind=").Append((int)layout.LayoutKind).Append('\n');

            DimensionLayoutRegionDefinition[] regions = layout.Regions;
            for (int i = 0; i < regions.Length; i++)
            {
                DimensionLayoutRegionDefinition region = regions[i];
                if (region == null || !region.Enabled)
                {
                    continue;
                }

                Api.DimensionBounds bounds = region.LocalBounds;
                builder.Append("region|")
                    .Append(region.BiomeId).Append('|')
                    .Append(region.ZoneId).Append('|')
                    .Append(Num(bounds.Min.x)).Append('|')
                    .Append(Num(bounds.Min.y)).Append('|')
                    .Append(Num(bounds.MaxExclusive.x)).Append('|')
                    .Append(Num(bounds.MaxExclusive.y)).Append('|')
                    .Append(Num(region.Priority)).Append('\n');
            }

            builder.Append("gridOrigin|")
                .Append(Num(layout.GridLocalMin.x)).Append('|')
                .Append(Num(layout.GridLocalMin.y)).Append('|')
                .Append(Num(layout.GridCellSize.x)).Append('|')
                .Append(Num(layout.GridCellSize.y)).Append('\n');

            DimensionLayoutGridCellDefinition[] cells = layout.GridCells;
            for (int i = 0; i < cells.Length; i++)
            {
                DimensionLayoutGridCellDefinition cell = cells[i];
                if (cell == null || !cell.Enabled)
                {
                    continue;
                }

                builder.Append("cell|")
                    .Append(cell.BiomeId).Append('|')
                    .Append(cell.ZoneId).Append('|')
                    .Append(Num(cell.Cell.x)).Append('|')
                    .Append(Num(cell.Cell.y)).Append('|')
                    .Append(Num(cell.CellSpan.x)).Append('|')
                    .Append(Num(cell.CellSpan.y)).Append('|')
                    .Append(Num(cell.Priority)).Append('\n');
            }

            builder.Append("band=").Append(Num(layout.RadialBandSizeTiles)).Append('\n');

            DimensionLayoutRadialRingDefinition[] rings = layout.RadialRings;
            for (int i = 0; i < rings.Length; i++)
            {
                DimensionLayoutRadialRingDefinition ring = rings[i];
                if (ring == null || !ring.Enabled)
                {
                    continue;
                }

                builder.Append("ring|")
                    .Append(ring.BiomeId).Append('|')
                    .Append(ring.ZoneId).Append('|')
                    .Append(Num(ring.MinRadiusTiles)).Append('|')
                    .Append(Num(ring.MaxRadiusTiles)).Append('|')
                    .Append(ring.HasAngleRange ? '1' : '0').Append('|')
                    .Append(Num(ring.StartAngleDegrees)).Append('|')
                    .Append(Num(ring.EndAngleDegrees)).Append('|')
                    .Append(Num(ring.Priority)).Append('\n');
            }

            builder.Append("mask=").Append(layout.BiomeMask == null ? "none" : layout.BiomeMask.name).Append('|')
                .Append(Num(layout.MaskLocalMin.x)).Append('|')
                .Append(Num(layout.MaskLocalMin.y)).Append('|')
                .Append(Num(layout.TilesPerMaskPixel.x)).Append('|')
                .Append(Num(layout.TilesPerMaskPixel.y)).Append('|')
                .Append(Num(layout.MaskAlphaThreshold)).Append('\n');

            DimensionLayoutMaskBiomeDefinition[] mappings = layout.MaskBiomeMappings;
            for (int i = 0; i < mappings.Length; i++)
            {
                DimensionLayoutMaskBiomeDefinition mapping = mappings[i];
                if (mapping == null || !mapping.Enabled)
                {
                    continue;
                }

                builder.Append("maskbiome|")
                    .Append(mapping.BiomeId).Append('|')
                    .Append(mapping.ZoneId).Append('|')
                    .Append(ColorCode(mapping.MaskColor)).Append('|')
                    .Append(Num(mapping.ColorTolerance)).Append('|')
                    .Append(Num(mapping.Priority)).Append('\n');
            }
        }

        /// <summary>
        /// A colour reduced to the eight bits per channel the mask comparison actually uses.
        /// </summary>
        /// <remarks>
        /// Float colours that differ far below one 8-bit step describe the same mask pixel, so they must
        /// not produce different fingerprints — otherwise merely reopening an asset could report a
        /// reshape.
        /// </remarks>
        private static string ColorCode(UnityEngine.Color color)
        {
            UnityEngine.Color32 quantized = color;
            return quantized.r.ToString("x2", CultureInfo.InvariantCulture) +
                quantized.g.ToString("x2", CultureInfo.InvariantCulture) +
                quantized.b.ToString("x2", CultureInfo.InvariantCulture);
        }

        private static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// An angle written to one decimal place.
        /// </summary>
        /// <remarks>
        /// Rounded deliberately. Angles arrive as floats from a dragged handle, and full precision would
        /// make a fingerprint that changes when nothing visible does.
        /// </remarks>
        private static string Num(float value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <summary>Folds the 64-bit hash down to the eight characters people actually read.</summary>
        private static string ToCode(ulong hash)
        {
            uint folded = (uint)(hash ^ (hash >> 32));
            return folded.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
