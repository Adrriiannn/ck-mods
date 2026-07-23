using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Forces any PNG dropped into the framework's DefaultArt folder to import as a Core Keeper item
    /// sprite (Sprite type, Single, 16 pixels-per-unit, Point filter, uncompressed, alpha as
    /// transparency). Without this a raw PNG imports as a plain Texture, which cannot be assigned to a
    /// Sprite field — the reason the default portal icons could not be dropped into the icon slots.
    /// </summary>
    internal sealed class DefaultArtTextureImporter : AssetPostprocessor
    {
        private const string DefaultArtFolder = "/ExpandNullforge/DefaultArt/";

        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.IndexOf(DefaultArtFolder, System.StringComparison.OrdinalIgnoreCase) < 0)
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
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
