using ExpandNullforge.EditorTools.Generation;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the rule that decides what a generated emissive sheet says glows.
    /// </summary>
    /// <remarks>
    /// The sheet is a starting point a modder edits, so it does not have to be perfect — but it does
    /// have to be defensible, and two of its choices would produce confusing art if they were wrong:
    /// filling transparent pixels would light up the empty part of every tile's quad, and averaging
    /// the channels instead of taking the brightest would discard exactly the saturated colours people
    /// most want to glow.
    /// </remarks>
    public sealed class DimensionTilesetEmissiveSheetTests
    {
        private static Color32 Glow(Color32 source)
        {
            return DimensionTilesetEmissiveSheet.ExtractHighlights(new[] { source })[0];
        }

        [Test]
        public void ABrightPixelKeepsItsColour()
        {
            Color32 bright = new Color32(255, 240, 180, 255);
            Color32 result = Glow(bright);

            Assert.AreEqual(bright.r, result.r);
            Assert.AreEqual(bright.g, result.g);
            Assert.AreEqual(bright.b, result.b);
            Assert.AreEqual(255, result.a);
        }

        [Test]
        public void ADarkPixelIsBlackedOutButStaysOpaque()
        {
            Color32 result = Glow(new Color32(60, 55, 50, 255));

            Assert.AreEqual(0, result.r);
            Assert.AreEqual(0, result.g);
            Assert.AreEqual(0, result.b);
            Assert.AreEqual(
                255,
                result.a,
                "Alpha has to survive: the sheet is read at the same coordinates as the colour bake, " +
                "and punching holes in it would change which pixels the bake considers present.");
        }

        [Test]
        public void ATransparentPixelEmitsNothing()
        {
            Color32 result = Glow(new Color32(255, 255, 255, 0));

            Assert.AreEqual(
                0,
                result.a,
                "A pixel that draws nothing must emit nothing, or every tile glows over its own empty " +
                "quad corners.");
        }

        [Test]
        public void ASaturatedColourCountsAsBrightOnItsStrongestChannel()
        {
            // Averaging would score this 85 and discard it, which would throw away the lava cracks and
            // runes that are the whole point of the feature.
            Color32 lava = new Color32(255, 0, 0, 255);
            Color32 result = Glow(lava);

            Assert.AreEqual(255, result.r, "A pure red highlight is bright, not dark.");
        }

        [Test]
        public void TheThresholdIsInclusive()
        {
            byte t = DimensionTilesetEmissiveSheet.BrightnessThreshold;

            Assert.AreEqual(t, Glow(new Color32(t, 0, 0, 255)).r, "A pixel exactly at the threshold glows.");
            Assert.AreEqual(0, Glow(new Color32((byte)(t - 1), 0, 0, 255)).r, "One below it does not.");
        }

        [Test]
        public void EveryPixelIsAccountedFor()
        {
            Color32[] source =
            {
                new Color32(255, 255, 255, 255),
                new Color32(0, 0, 0, 255),
                new Color32(200, 10, 10, 128),
                new Color32(10, 10, 10, 0)
            };

            Color32[] result = DimensionTilesetEmissiveSheet.ExtractHighlights(source);

            Assert.AreEqual(source.Length, result.Length, "The glow map must line up with the sheet pixel for pixel.");
        }
    }
}
