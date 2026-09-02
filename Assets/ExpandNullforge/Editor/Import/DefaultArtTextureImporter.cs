using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Imports item artwork as a Core Keeper sprite so it can actually be assigned to an icon slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT GOES WRONG WITHOUT IT. Unity imports a raw PNG as a plain Texture, and a Texture cannot
    /// be dropped into a <c>Sprite</c> field — the slot simply refuses it. The modder sees an icon
    /// slot that will not accept the icon they just drew, with nothing explaining why. Even when a
    /// sprite does import, Unity's defaults are wrong for this game: bilinear filtering blurs
    /// pixel art, compression eats the palette, and the wrong pixels-per-unit scales it.
    /// </para>
    /// <para>
    /// WHICH FILES IT CLAIMS. Two cases, both places where the framework — not the modder — is
    /// responsible for the settings being right:
    /// </para>
    /// <list type="bullet">
    /// <item>the framework's own <c>DefaultArt</c> folder, which ships the stock icons;</item>
    /// <item>an <c>Icons</c> folder inside a mod, which is where the dashboard tells people to put
    /// artwork and where generation looks for it.</item>
    /// </list>
    /// <para>
    /// It deliberately does NOT claim every texture in a mod. A modder's tileset sheets, UI art and
    /// reference images live there too, and silently forcing all of them to Sprite/Point/uncompressed
    /// would be the framework overriding decisions that are not its to make. Scoping to a named
    /// folder keeps the rule predictable and gives an obvious escape hatch: put the file somewhere
    /// else and Unity's own defaults apply.
    /// </para>
    /// <para>
    /// The mod check runs only after the cheap folder-name test matches, so the common case — any
    /// unrelated texture import — costs one string search.
    /// </para>
    /// </remarks>
    internal sealed class DefaultArtTextureImporter : AssetPostprocessor
    {
        private const string DefaultArtFolder = "/ExpandNullforge/DefaultArt/";
        private const string IconsFolder = "/Icons/";
        private const string EditorFolder = "/Editor/";
        private const string ShellArtFolder = "/ExpandNullforge/Editor/UI/Art/";

        /// <summary>Core Keeper renders item icons at 16 pixels per unit.</summary>
        private const int ItemSpritePixelsPerUnit = 16;

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');

            // The Studio's own pixel art (the game-font title lettering) is a plain texture, not
            // a sprite, but it needs the same two mercies: point filtering and no compression.
            // Left to Unity's defaults it comes back bilinear-blurred — pixel lettering with
            // soft edges reads as a mistake, not a style.
            if (path.IndexOf(ShellArtFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TextureImporter shellImporter = assetImporter as TextureImporter;
                if (shellImporter != null)
                {
                    shellImporter.textureType = TextureImporterType.Default;
                    shellImporter.filterMode = FilterMode.Point;
                    shellImporter.mipmapEnabled = false;
                    shellImporter.alphaIsTransparency = true;
                    shellImporter.textureCompression = TextureImporterCompression.Uncompressed;
                }

                return;
            }

            if (!ShouldConfigure(path))
            {
                return;
            }

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ItemSpritePixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        private static bool ShouldConfigure(string path)
        {
            if (path.IndexOf(DefaultArtFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (path.IndexOf(IconsFolder, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            // Editor art is never an item sprite. This exclusion is load-bearing rather than tidy:
            // the dashboard's own section icons live in Assets/ExpandNullforge/Editor/Icons, and
            // because the framework itself ships as a mod, the check below would otherwise claim
            // them and re-import UI artwork at 16 pixels per unit.
            if (path.IndexOf(EditorFolder, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            // An "Icons" folder only counts inside a mod. Anywhere else it is somebody else's
            // folder that happens to share the name, and reaching into it would be presumptuous.
            return DimensionApiModFolderUtility.ResolveModSettingsForAssetPath(path) != null;
        }
    }
}
