using System.IO;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Makes the emissive sheet a modder edits, instead of leaving them a blank canvas they have to
    /// size correctly themselves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Ticking "glows in the dark" used to hand the modder an empty texture field. The
    /// sheet they then had to supply must match the tileset sheet pixel for pixel — same size, same
    /// layout — because the emissive bake reads it at exactly the same coordinates as the colour bake.
    /// Get the size wrong and generation refuses; get the layout wrong and it succeeds and glows in the
    /// wrong places. Neither is a reasonable thing to ask someone to get right by hand.
    /// </para>
    /// <para>
    /// WHAT IT PUTS IN THE SHEET. Not black (correct but useless as a starting point) and not a copy of
    /// the whole sheet (which makes the entire block glow). It keeps the art's BRIGHTEST pixels and
    /// blacks out everything else. That default exists because the things people actually want to glow
    /// — crystal veins, lava cracks, lit panels, runes — are almost always the brightest part of the
    /// art already, so the first result usually looks deliberate. It is a starting point, not an
    /// answer: the modder paints out what should stay dark and paints in what the threshold missed.
    /// </para>
    /// <para>
    /// Transparent pixels stay transparent. A glow is light added on top of the block's own art, so a
    /// pixel that draws nothing must emit nothing — filling transparent areas would outline every tile
    /// in its own quad.
    /// </para>
    /// </remarks>
    internal static class DimensionTilesetEmissiveSheet
    {
        /// <summary>
        /// How bright a pixel must be, 0-255 on the brightest channel, to be kept as glowing.
        /// </summary>
        /// <remarks>
        /// Set against the brightest channel rather than an average so a saturated colour — a red lava
        /// crack, a blue rune — is treated as bright, which averaging would wash out to about a third
        /// and discard. The value is high enough that ordinary rock and dirt fall away and low enough
        /// that a dim-but-deliberate highlight survives.
        /// </remarks>
        internal const byte BrightnessThreshold = 170;

        /// <summary>
        /// Writes an emissive sheet next to the tileset's own, derived from its art.
        /// </summary>
        /// <param name="asset">The tileset to derive from; its sheet supplies both size and content.</param>
        /// <param name="created">The imported texture, ready to assign.</param>
        /// <param name="error">Why nothing was written.</param>
        internal static bool TryCreate(DimensionTilesetAsset asset, out Texture2D created, out string error)
        {
            created = null;

            if (asset == null || asset.TilesetTexture == null)
            {
                error = "Assign the block's tileset sheet first — the emissive sheet is derived from it.";
                return false;
            }

            Color32[] source;
            int width;
            int height;
            if (!DimensionTilesetPixelIo.TryLoadImageOrder(asset.TilesetTexture, out source, out width, out height))
            {
                error = "Could not read '" + asset.TilesetTexture.name + "' as a PNG.";
                return false;
            }

            Color32[] glow = ExtractHighlights(source);

            string sheetPath = UnityEditor.AssetDatabase.GetAssetPath(asset.TilesetTexture);
            string directory = Path.GetDirectoryName(sheetPath);
            if (string.IsNullOrEmpty(directory))
            {
                error = "Could not work out where to put the emissive sheet — '" +
                        asset.TilesetTexture.name + "' is not in the project.";
                return false;
            }

            // Named off the SHEET, not the tileset: the two live side by side and a modder scanning the
            // folder should be able to see at a glance which sheet a glow map belongs to.
            string path = directory.Replace('\\', '/') + "/" +
                          Path.GetFileNameWithoutExtension(sheetPath) + "_emissive.png";

            created = DimensionTilesetPixelIo.SaveImageOrderPng(glow, width, height, path);
            if (created == null)
            {
                error = "Failed to write the emissive sheet to '" + path + "'.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Keeps the bright pixels at full colour and blacks out the rest, preserving alpha.
        /// </summary>
        internal static Color32[] ExtractHighlights(Color32[] source)
        {
            Color32[] result = new Color32[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Color32 c = source[i];
                if (c.a == 0)
                {
                    // Nothing is drawn here, so nothing may be emitted here.
                    result[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                byte brightest = c.r;
                if (c.g > brightest)
                {
                    brightest = c.g;
                }

                if (c.b > brightest)
                {
                    brightest = c.b;
                }

                result[i] = brightest >= BrightnessThreshold
                    ? new Color32(c.r, c.g, c.b, c.a)
                    : new Color32(0, 0, 0, c.a);
            }

            return result;
        }
    }
}
